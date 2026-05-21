#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    public static class HUDCanvasInquisition
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string SharedReportSectionKey = "shinobu_270_visor_ar_stencil";
        private const string CoreCsprojRelativePath = "Hecton8.Core.csproj";
        private const string RendererFeatureProjectPath = "Assets\\_Project\\Scripts\\Visor\\HectonVisorARStencilRendererFeature.cs";
        private const string StencilPreviewGizmoProjectPath = "Assets\\_Project\\Scripts\\Visor\\HectonVisorStencilPreviewGizmo.cs";

        [MenuItem("Hecton8/UI/HUD Canvas Inquisition")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            int hudPrefabCount = 0;
            int hudCanvasCount = 0;
            int hudRaycasterCount = 0;
            int forceUpdateCallCount = CountRuntimeToken(root, string.Concat("Canvas.", "ForceUpdateCanvases"));
            int graphicRaycasterScriptCount = CountRuntimeToken(root, "GraphicRaycaster");
            string coreCsprojText = ReadAllTextSafe(Path.Combine(root, CoreCsprojRelativePath));
            bool rendererFeatureInGeneratedProject = ContainsCompileInclude(coreCsprojText, RendererFeatureProjectPath);
            bool previewGizmoInGeneratedProject = ContainsCompileInclude(coreCsprojText, StencilPreviewGizmoProjectPath);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!IsHudPath(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                hudPrefabCount++;
                hudCanvasCount += prefab.GetComponentsInChildren<Canvas>(true).Length;
                hudRaycasterCount += CountComponentsByFullName(prefab, "UnityEngine.UI.GraphicRaycaster");
            }

            string reportPath = Path.Combine(root, ReportRelativePath);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string reportObject = BuildReportObject(
                hudPrefabCount,
                hudCanvasCount,
                hudRaycasterCount,
                forceUpdateCallCount,
                graphicRaycasterScriptCount,
                rendererFeatureInGeneratedProject,
                previewGizmoInGeneratedProject);
            UpsertSharedReportObject(reportPath, SharedReportSectionKey, reportObject);
            AssetDatabase.Refresh();
            Debug.Log($"HUD Canvas Inquisition wrote {reportPath}");
        }

        private static string BuildReportObject(
            int hudPrefabCount,
            int hudCanvasCount,
            int hudRaycasterCount,
            int forceUpdateCallCount,
            int graphicRaycasterScriptCount,
            bool rendererFeatureInGeneratedProject,
            bool previewGizmoInGeneratedProject)
        {
            bool generatedProjectStale = !rendererFeatureInGeneratedProject || !previewGizmoInGeneratedProject;
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_270\",");
            builder.AppendLine("  \"domain\": \"ECHELON 8 Presentation & UX / Visor AR (HUD)\",");
            builder.Append("  \"hudPrefabCount\": ").Append(hudPrefabCount).AppendLine(",");
            builder.Append("  \"managedHudCanvasComponents\": ").Append(hudCanvasCount).AppendLine(",");
            builder.Append("  \"managedHudGraphicRaycasters\": ").Append(hudRaycasterCount).AppendLine(",");
            builder.Append("  \"forceUpdateCanvasesCalls\": ").Append(forceUpdateCallCount).AppendLine(",");
            builder.Append("  \"graphicRaycasterRuntimeTokenCount\": ").Append(graphicRaycasterScriptCount).AppendLine(",");
            builder.Append("  \"managedHudElementsPurged\": ").Append(hudCanvasCount == 0 && hudRaycasterCount == 0 && forceUpdateCallCount == 0 && graphicRaycasterScriptCount == 0 ? "true" : "false").AppendLine(",");
            builder.Append("  \"generatedProjectIncludesRendererFeature\": ").Append(rendererFeatureInGeneratedProject ? "true" : "false").AppendLine(",");
            builder.Append("  \"generatedProjectIncludesStencilPreviewGizmo\": ").Append(previewGizmoInGeneratedProject ? "true" : "false").AppendLine(",");
            builder.Append("  \"generatedProjectStale\": ").Append(generatedProjectStale ? "true" : "false").AppendLine(",");
            builder.AppendLine("  \"takeoverPath\": \"HectonVisorARStencilRendererFeature + Hecton_VisorAR.shader\",");
            builder.AppendLine("  \"aggregatePolicy\": \"UPSERT_SECTION_PRESERVE_NEIGHBOR_REPORTS\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void UpsertSharedReportObject(string reportPath, string sectionKey, string reportObject)
        {
            string existing = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
            if (!LooksLikeJsonObject(existing))
            {
                File.WriteAllText(reportPath, WrapSingleSection(sectionKey, reportObject));
                return;
            }

            string trimmed = RemoveTopLevelProperty(existing.Trim(), sectionKey).Trim();
            if (!LooksLikeJsonObject(trimmed))
            {
                File.WriteAllText(reportPath, WrapSingleSection(sectionKey, reportObject));
                return;
            }

            int close = trimmed.LastIndexOf('}');
            string body = close > 1 ? trimmed.Substring(1, close - 1).Trim() : string.Empty;
            StringBuilder builder = new StringBuilder(trimmed.Length + reportObject.Length + 128);
            builder.AppendLine("{");
            if (!string.IsNullOrEmpty(body))
            {
                builder.AppendLine(body);
                builder.AppendLine(",");
            }

            builder.Append("  \"").Append(sectionKey).AppendLine("\": ");
            AppendIndented(builder, reportObject, 2);
            builder.AppendLine();
            builder.AppendLine("}");
            File.WriteAllText(reportPath, builder.ToString());
        }

        private static string WrapSingleSection(string sectionKey, string reportObject)
        {
            StringBuilder builder = new StringBuilder(reportObject.Length + sectionKey.Length + 32);
            builder.AppendLine("{");
            builder.Append("  \"").Append(sectionKey).AppendLine("\": ");
            AppendIndented(builder, reportObject, 2);
            builder.AppendLine();
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static bool LooksLikeJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
        }

        private static void AppendIndented(StringBuilder builder, string text, int spaces)
        {
            string padding = new string(' ', spaces);
            using (StringReader reader = new StringReader(text))
            {
                string line;
                bool first = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!first)
                        builder.AppendLine();
                    builder.Append(padding).Append(line);
                    first = false;
                }
            }
        }

        private static string RemoveTopLevelProperty(string json, string key)
        {
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int nameEnd = FindStringEnd(json, i);
                    if (depth == 1 && nameEnd > i)
                    {
                        int colon = SkipWhitespace(json, nameEnd + 1);
                        if (colon < json.Length && json[colon] == ':' &&
                            string.CompareOrdinal(json, i + 1, key, 0, key.Length) == 0 &&
                            key.Length == nameEnd - i - 1)
                        {
                            int valueStart = SkipWhitespace(json, colon + 1);
                            int valueEnd = FindValueEnd(json, valueStart);
                            int removeStart = i;
                            int removeEnd = valueEnd;
                            int previous = SkipWhitespaceBack(json, i - 1);
                            if (previous >= 0 && json[previous] == ',')
                            {
                                removeStart = previous;
                            }
                            else
                            {
                                int next = SkipWhitespace(json, valueEnd);
                                if (next < json.Length && json[next] == ',')
                                    removeEnd = next + 1;
                            }

                            return json.Remove(removeStart, removeEnd - removeStart);
                        }
                    }

                    i = nameEnd;
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
            }

            return json;
        }

        private static int FindStringEnd(string text, int quoteStart)
        {
            bool escaped = false;
            for (int i = quoteStart + 1; i < text.Length; i++)
            {
                char c = text[i];
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
                    return i;
            }

            return text.Length - 1;
        }

        private static int FindValueEnd(string text, int valueStart)
        {
            if (valueStart >= text.Length)
                return text.Length;

            char start = text[valueStart];
            if (start == '{' || start == '[')
            {
                int depth = 0;
                for (int i = valueStart; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '"')
                    {
                        i = FindStringEnd(text, i);
                        continue;
                    }

                    if (c == '{' || c == '[')
                        depth++;
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                        if (depth == 0)
                            return i + 1;
                    }
                }

                return text.Length;
            }

            for (int i = valueStart; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ',' || c == '}')
                    return i;
            }

            return text.Length;
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
            return index;
        }

        private static int SkipWhitespaceBack(string text, int index)
        {
            while (index >= 0 && char.IsWhiteSpace(text[index]))
                index--;
            return index;
        }

        private static bool IsHudPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Visor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Waypoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("AR", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountComponentsByFullName(GameObject root, string fullName)
        {
            if (root == null || string.IsNullOrEmpty(fullName))
                return 0;

            Component[] components = root.GetComponentsInChildren<Component>(true);
            int count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                Type type = component != null ? component.GetType() : null;
                if (type != null && string.Equals(type.FullName, fullName, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static int CountRuntimeToken(string root, string token)
        {
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            if (!Directory.Exists(scriptsRoot))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                if (file.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string text = File.ReadAllText(files[i]);
                int index = 0;
                while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    index += token.Length;
                }
            }

            return count;
        }

        private static string ReadAllTextSafe(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static bool ContainsCompileInclude(string csprojText, string projectPath)
        {
            if (string.IsNullOrEmpty(csprojText) || string.IsNullOrEmpty(projectPath))
                return false;

            string normalizedBackslash = projectPath.Replace('/', '\\');
            string normalizedSlash = projectPath.Replace('\\', '/');
            return csprojText.IndexOf("Compile Include=\"" + normalizedBackslash + "\"", StringComparison.Ordinal) >= 0 ||
                   csprojText.IndexOf("Compile Include=\"" + normalizedSlash + "\"", StringComparison.Ordinal) >= 0;
        }
    }
}
#endif
