#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.AITextureControlMaps
{
    internal static class Material_Setup_Scanner
    {
        private static readonly string[] CandidateFolders =
        {
            "Assets/_Project/Materials",
            "Assets/_Project/Art/Materials",
            AITextureControlMapConstants.ImportedMaterialFolder
        };

        private static readonly string[] ProjectFallbackFolders =
        {
            "Assets/_Project"
        };

        [MenuItem("HECTON-8/AI Texture Control Maps/Scan Material Setup", false, 2690)]
        internal static void RunScan()
        {
            string[] folders = BuildFolderFilter();
            string[] guids = AssetDatabase.FindAssets("t:Material", folders);
            int missingArm = 0;
            int albedoSrgbErrors = 0;
            int scanned = 0;
            StringBuilder findings = new StringBuilder(4096); // COLD ALLOC: JSON finding body - owner: Material_Setup_Scanner
            bool first = true;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                scanned++;
                if (material.HasProperty("_ArmMap") && material.GetTexture("_ArmMap") == null)
                {
                    missingArm++;
                    AppendFinding(findings, ref first, path, "MISSING_ARM_MAP");
                }

                Texture albedo = FindAlbedoTexture(material);
                if (albedo != null)
                {
                    string texturePath = AssetDatabase.GetAssetPath(albedo);
                    TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (importer != null && !importer.sRGBTexture)
                    {
                        albedoSrgbErrors++;
                        AppendFinding(findings, ref first, path, "ALBEDO_SRGB_FALSE");
                    }
                }
            }

            EnsureReportFolder(AITextureControlMapConstants.MaterialSetupReportPath);
            EnsureReportFolder(AITextureControlMapConstants.MaterialAuditReportPath);
            int errorsPrevented = missingArm + albedoSrgbErrors;
            StringBuilder builder = new StringBuilder(8192); // COLD ALLOC: material setup report - owner: Material_Setup_Scanner
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.rendering_material_setup_scan.v1", true);
            AppendJson(builder, "agent", "SHINOBU_269", true);
            AppendJson(builder, "evidenceClass", "UNITY_EDITOR_MENU_SCAN", true);
            AppendJson(builder, "materialsScanned", scanned, true);
            AppendJson(builder, "missingArmMap", missingArm, true);
            AppendJson(builder, "albedoSrgbErrors", albedoSrgbErrors, true);
            AppendJson(builder, "manualMaterialErrorsPrevented", errorsPrevented, true);
            AppendJson(builder, "status", errorsPrevented > 0 ? "CRITICAL_WARNING" : "PENDING_UNITY_VERIFICATION", true);
            builder.Append("  \"findings\": [");
            if (findings.Length > 0)
                builder.Append('\n').Append(findings).Append('\n');
            builder.Append("  ]\n");
            builder.Append("}\n");
            string report = builder.ToString();
            File.WriteAllText(AITextureControlMapConstants.MaterialSetupReportPath, report, new UTF8Encoding(false));
            MergeIntoSharedRenderingReport(report);
            Debug.Log("[Material_Setup_Scanner] Materials=" + scanned.ToString(CultureInfo.InvariantCulture) +
                      " ErrorsPrevented=" + errorsPrevented.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static string[] BuildFolderFilter()
        {
            int count = 0;
            for (int i = 0; i < CandidateFolders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(CandidateFolders[i]))
                    count++;
            }

            if (count == 0)
                return ProjectFallbackFolders;
            if (count == CandidateFolders.Length)
                return CandidateFolders;

            string[] folders = new string[count]; // COLD ALLOC: string[validMaterialFolderCount] - editor material scan folder filter - owner: Material_Setup_Scanner
            int cursor = 0;
            for (int i = 0; i < CandidateFolders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(CandidateFolders[i]))
                    folders[cursor++] = CandidateFolders[i];
            }

            return folders;
        }

        private static Texture FindAlbedoTexture(Material material)
        {
            if (material == null)
                return null;
            if (material.HasProperty("_BaseMap"))
                return material.GetTexture("_BaseMap");
            if (material.HasProperty("_MainTex"))
                return material.GetTexture("_MainTex");
            return null;
        }

        private static void AppendFinding(StringBuilder builder, ref bool first, string materialPath, string issue)
        {
            if (!first)
                builder.Append(",\n");
            first = false;
            builder.Append("    { \"material\": \"").Append(Escape(materialPath)).Append("\", \"issue\": \"").Append(issue).Append("\" }");
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(Escape(value)).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append(comma ? ",\n" : "\n");
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static void MergeIntoSharedRenderingReport(string report)
        {
            const string key = "shinobu_269_ai_texture_control_maps";
            string path = AITextureControlMapConstants.MaterialAuditReportPath;
            string current = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            string merged = MergeJsonObjectEntry(current, key, report);
            File.WriteAllText(path, merged, new UTF8Encoding(false));
        }

        private static string MergeJsonObjectEntry(string currentJson, string key, string entryJson)
        {
            string trimmed = string.IsNullOrWhiteSpace(currentJson) ? string.Empty : currentJson.Trim();
            List<string> members = new List<string>(16); // COLD ALLOC: editor shared rendering report member merge - owner: Material_Setup_Scanner
            if (trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}')
                ExtractTopLevelMembers(trimmed, key, members);

            StringBuilder builder = new StringBuilder(Mathf.Max(4096, trimmed.Length + entryJson.Length + 128)); // COLD ALLOC: shared rendering report merge - owner: Material_Setup_Scanner
            builder.Append("{\n");
            for (int i = 0; i < members.Count; i++)
                builder.Append("  ").Append(members[i]).Append(",\n");

            builder.Append("  \"").Append(key).Append("\": ");
            AppendIndentedObject(builder, entryJson, 2);
            builder.Append('\n').Append("}\n");
            return builder.ToString();
        }

        private static void ExtractTopLevelMembers(string json, string excludedKey, List<string> members)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            int memberStart = 1;
            for (int i = 1; i < json.Length - 1; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
                else if (c == ',' && depth == 0)
                {
                    AddMemberIfOwned(json, memberStart, i, excludedKey, members);
                    memberStart = i + 1;
                }
            }

            AddMemberIfOwned(json, memberStart, json.Length - 1, excludedKey, members);
        }

        private static void AddMemberIfOwned(string json, int start, int end, string excludedKey, List<string> members)
        {
            while (start < end && char.IsWhiteSpace(json[start]))
                start++;
            while (end > start && char.IsWhiteSpace(json[end - 1]))
                end--;
            if (end <= start)
                return;

            string member = json.Substring(start, end - start);
            if (StartsWithProperty(member, excludedKey))
                return;

            members.Add(member);
        }

        private static bool StartsWithProperty(string member, string key)
        {
            int index = 0;
            while (index < member.Length && char.IsWhiteSpace(member[index]))
                index++;
            if (index >= member.Length || member[index] != '"')
                return false;

            index++;
            int keyStart = index;
            while (index < member.Length && member[index] != '"')
                index++;
            if (index >= member.Length)
                return false;

            return string.Equals(member.Substring(keyStart, index - keyStart), key, StringComparison.Ordinal);
        }

        private static void AppendIndentedObject(StringBuilder builder, string json, int indent)
        {
            string trimmed = string.IsNullOrWhiteSpace(json) ? "{}" : json.Trim();
            string pad = new string(' ', indent);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                builder.Append(c);
                if (c == '\n' && i < trimmed.Length - 1)
                    builder.Append(pad);
            }
        }

        private static void EnsureReportFolder(string reportPath)
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
#endif
