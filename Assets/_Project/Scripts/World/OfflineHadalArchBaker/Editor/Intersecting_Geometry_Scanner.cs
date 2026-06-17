using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineHadalArchBaker.Editor
{
    public static class Intersecting_Geometry_Scanner
    {
        private const string RenderingReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string RuntimeCsgReportPath = "Docs/Reports/HADAL_RUNTIME_CSG_INQUISITION.json";

        [MenuItem("Hecton8/Hadal Structure Forge/Scan Intersecting Geometry")]
        public static void ScanIntersectingGeometryMenu()
        {
            ScanIntersectingGeometry();
        }

        [MenuItem("Hecton8/Hadal Structure Forge/Runtime CSG Inquisition")]
        public static void RuntimeCsgInquisitionMenu()
        {
            RuntimeCsgInquisition();
        }

        public static int RuntimeCsgInquisition()
        {
            Directory.CreateDirectory("Docs/Reports");
            string root = "Assets/_Project/Scripts/Environment";
            string[] files = Directory.Exists(root) ? Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories) : new string[0];
            int offenders = 0;
            StringBuilder rows = new StringBuilder(512);
            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                bool hasRuntimeHook = ContainsAny(text, "Awake(", "Start(", "Update(", "FixedUpdate(", "LateUpdate(");
                bool hasCsg = ContainsAny(text, "CSG", "Constructive", "ProBuilder", "Boolean", "Marching", "Voxel", "Carv", "SDF");
                if (!hasRuntimeHook || !hasCsg)
                    continue;

                if (offenders > 0)
                    rows.Append(", ");
                offenders++;
                AppendJsonString(rows, files[i]);
            }

            StringBuilder report = new StringBuilder(1024);
            report.Append("{\n");
            report.Append("  \"agent\": \"SHINOBU_215\",\n");
            report.Append("  \"scanRoot\": \"").Append(root).Append("\",\n");
            report.Append("  \"offenderCount\": ").Append(offenders).Append(",\n");
            report.Append("  \"mandate\": \"Runtime CSG, boolean mesh operations, ProBuilder runtime APIs, and dynamic voxel carving under Environment must be replaced by baked static mesh references.\",\n");
            report.Append("  \"offenders\": [");
            report.Append(rows);
            report.Append("]\n}\n");
            File.WriteAllText(RuntimeCsgReportPath, report.ToString());
            AssetDatabase.Refresh();
            return offenders;
        }

        public static int ScanIntersectingGeometry()
        {
            Directory.CreateDirectory("Docs/Reports");
            List<RendererRecord> records = new List<RendererRecord>(2048);
            MeshRenderer[] loadedRenderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();
            for (int i = 0; i < loadedRenderers.Length; i++)
                TryAddRendererRecord(loadedRenderers[i], "loaded_scene", records);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                    TryAddRendererRecord(renderers[r], path, records);
            }

            int clusterCount = 0;
            int maxClusterSize = 0;
            StringBuilder clusters = new StringBuilder(2048);
            bool[] consumed = new bool[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                if (consumed[i])
                    continue;

                int clusterSize = 1;
                consumed[i] = true;
                for (int j = i + 1; j < records.Count; j++)
                {
                    if (consumed[j] || !SameSource(records[i], records[j]) || !records[i].Bounds.Intersects(records[j].Bounds))
                        continue;

                    consumed[j] = true;
                    clusterSize++;
                }

                if (clusterSize <= 5)
                    continue;

                if (clusterCount > 0)
                    clusters.Append(",\n");
                clusters.Append("    { \"source\": ");
                AppendJsonString(clusters, records[i].Source);
                clusters.Append(", \"root\": ");
                AppendJsonString(clusters, records[i].Name);
                clusters.Append(", \"intersectingRenderers\": ").Append(clusterSize).Append(" }");
                clusterCount++;
                maxClusterSize = Mathf.Max(maxClusterSize, clusterSize);
            }

            StringBuilder report = new StringBuilder(4096);
            report.Append("{\n");
            report.Append("  \"agent\": \"SHINOBU_215\",\n");
            report.Append("  \"rendererRecords\": ").Append(records.Count).Append(",\n");
            report.Append("  \"clusterThreshold\": 5,\n");
            report.Append("  \"clusterCount\": ").Append(clusterCount).Append(",\n");
            report.Append("  \"maxClusterSize\": ").Append(maxClusterSize).Append(",\n");
            report.Append("  \"replacementMandate\": \"Clusters above threshold must be replaced with one baked Hadal monolith mesh generated by the Forge.\",\n");
            report.Append("  \"clusters\": [\n");
            report.Append(clusters);
            report.Append("\n  ]\n}\n");
            File.WriteAllText(RenderingReportPath, report.ToString());
            AssetDatabase.Refresh();
            return clusterCount;
        }

        private static void TryAddRendererRecord(MeshRenderer renderer, string source, List<RendererRecord> records)
        {
            if (renderer == null || renderer.gameObject == null || !IsRockOrTerrain(renderer))
                return;

            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude <= 0.0001f)
                return;

            records.Add(new RendererRecord
            {
                Source = source,
                Name = renderer.gameObject.name,
                Bounds = bounds
            });
        }

        private static bool IsRockOrTerrain(MeshRenderer renderer)
        {
            string name = renderer.gameObject.name;
            if (ContainsRockToken(name))
                return true;

            if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && ContainsRockToken(filter.sharedMesh.name))
                return true;

            Material shared = renderer.sharedMaterial;
            return shared != null && ContainsRockToken(shared.name);
        }

        private static bool ContainsRockToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("terrain", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("cave", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("arch", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (text.IndexOf(needles[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool SameSource(in RendererRecord a, in RendererRecord b)
        {
            return string.Equals(a.Source, b.Source, System.StringComparison.Ordinal);
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c == '"' || c == '\\')
                        builder.Append('\\');
                    builder.Append(c);
                }
            }

            builder.Append('"');
        }

        private struct RendererRecord
        {
            public string Source;
            public string Name;
            public Bounds Bounds;
        }
    }
}
