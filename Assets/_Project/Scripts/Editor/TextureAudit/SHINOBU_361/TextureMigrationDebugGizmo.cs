#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.TextureAudit
{
    /// <summary>
    /// Scene View migration x-ray for materials with missing or stubbed texture rows in the production manifest.
    /// </summary>
    [InitializeOnLoad]
    internal static class TextureMigrationDebugGizmo
    {
        private const string ManifestPath = "Docs/Reports/production_texture_manifest.csv";
        private static readonly Dictionary<string, int> MaterialPriorityByPath = new Dictionary<string, int>(256, StringComparer.OrdinalIgnoreCase); // COLD ALLOC: editor manifest material priority cache - owner: TextureMigrationDebugGizmo
        private static readonly List<RendererIssue> RendererIssues = new List<RendererIssue>(256); // COLD ALLOC: editor scene renderer issue cache - owner: TextureMigrationDebugGizmo
        private static readonly List<Material> MaterialScratch = new List<Material>(16); // COLD ALLOC: editor shared material scratch list - owner: TextureMigrationDebugGizmo
        private static bool _enabled;
        private static bool _dirty = true;

        static TextureMigrationDebugGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("HECTON-8/Texture Audit/Toggle Migration Debug Gizmo", false, 3610)]
        private static void Toggle()
        {
            _enabled = !_enabled;
            _dirty = true;
            SceneView.RepaintAll();
        }

        [MenuItem("HECTON-8/Texture Audit/Refresh Migration Debug Gizmo", false, 3611)]
        private static void Refresh()
        {
            _dirty = true;
            RefreshCaches();
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_enabled)
                return;

            RefreshIfNeeded();
            for (int i = 0; i < RendererIssues.Count; i++)
            {
                RendererIssue issue = RendererIssues[i];
                if (issue.Renderer == null)
                    continue;

                Handles.color = ColorForPriority(issue.Priority);
                Bounds bounds = issue.Renderer.bounds;
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
        }

        private static void RefreshIfNeeded()
        {
            if (_dirty)
                RefreshCaches();
        }

        private static void RefreshCaches()
        {
            _dirty = false;
            MaterialPriorityByPath.Clear();
            RendererIssues.Clear();

            string manifestFullPath = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
            if (!File.Exists(manifestFullPath))
                return;

            LoadManifest(manifestFullPath);
            if (MaterialPriorityByPath.Count == 0)
                return;

            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                int priority = FindRendererPriority(renderer);
                if (priority > 0)
                    RendererIssues.Add(new RendererIssue(renderer, priority));
            }
        }

        private static void LoadManifest(string manifestFullPath)
        {
            using (StreamReader reader = new StreamReader(manifestFullPath))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                    return;

                string[] headers = SplitCsv(headerLine);
                int sourceIndex = IndexOf(headers, "source_asset_path");
                int priorityIndex = IndexOf(headers, "priority");
                int stateIndex = IndexOf(headers, "reference_state");
                int typeIndex = IndexOf(headers, "source_type");
                if (sourceIndex < 0 || priorityIndex < 0 || stateIndex < 0 || typeIndex < 0)
                    return;

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] cols = SplitCsv(line);
                    if (cols.Length <= Math.Max(Math.Max(sourceIndex, priorityIndex), Math.Max(stateIndex, typeIndex)))
                        continue;
                    if (!string.Equals(cols[typeIndex], "material", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!IsDefectState(cols[stateIndex]))
                        continue;

                    int priority = PriorityToInt(cols[priorityIndex]);
                    if (priority <= 0)
                        continue;
                    string materialPath = cols[sourceIndex].Replace('\\', '/');
                    int existing;
                    if (!MaterialPriorityByPath.TryGetValue(materialPath, out existing) || priority > existing)
                        MaterialPriorityByPath[materialPath] = priority;
                }
            }
        }

        private static int FindRendererPriority(Renderer renderer)
        {
            MaterialScratch.Clear();
            renderer.GetSharedMaterials(MaterialScratch);
            int priority = 0;
            for (int i = 0; i < MaterialScratch.Count; i++)
            {
                Material material = MaterialScratch[i];
                if (material == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(path))
                    continue;
                int candidate;
                if (MaterialPriorityByPath.TryGetValue(path.Replace('\\', '/'), out candidate) && candidate > priority)
                    priority = candidate;
            }
            MaterialScratch.Clear();
            return priority;
        }

        private static bool IsDefectState(string state)
        {
            return string.Equals(state, "EMPTY_REQUIRED_SLOT", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(state, "MISSING_GUID", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(state, "MISSING_EMBEDDED_TEXTURE", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(state, "STUB_TEXTURE", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(state, "IMPORT_ISSUE", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(state, "BUILTIN_DEFAULT_TEXTURE", StringComparison.OrdinalIgnoreCase);
        }

        private static int PriorityToInt(string priority)
        {
            if (string.Equals(priority, "BLOCKER", StringComparison.OrdinalIgnoreCase))
                return 3;
            if (string.Equals(priority, "MEDIUM", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(priority, "LOW", StringComparison.OrdinalIgnoreCase))
                return 1;
            return 0;
        }

        private static Color ColorForPriority(int priority)
        {
            if (priority >= 3)
                return Color.red;
            if (priority == 2)
                return new Color(1.0f, 0.45f, 0.0f, 1.0f);
            return Color.gray;
        }

        private static int IndexOf(string[] headers, string name)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string[] SplitCsv(string line)
        {
            List<string> values = new List<string>(32); // COLD ALLOC: editor CSV row parse - owner: TextureMigrationDebugGizmo
            int start = 0;
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(Unescape(line.Substring(start, i - start)));
                    start = i + 1;
                }
            }

            values.Add(Unescape(line.Substring(start)));
            return values.ToArray();
        }

        private static string Unescape(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
            return value;
        }

        private readonly struct RendererIssue
        {
            public readonly Renderer Renderer;
            public readonly int Priority;

            public RendererIssue(Renderer renderer, int priority)
            {
                Renderer = renderer;
                Priority = priority;
            }
        }
    }
}
#endif
