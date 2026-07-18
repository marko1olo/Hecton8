using UnityEditor;
using UnityEngine;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using System.Reflection;
using System.Text;
using System.IO;

public static class GraphNodeAuditor
{
    public static void Execute()
    {
        StringBuilder sb = new StringBuilder();
        try {
            string graphPath = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(graphPath);
            if (graph == null) {
                sb.AppendLine("[GRAPH_AUDIT] ERROR: Graph not found at " + graphPath);
            } else {
                sb.AppendLine("[GRAPH_AUDIT] Successfully loaded graph: " + graph.name);
                foreach (var gen in graph.generators) {
                    string typeName = gen.GetType().Name;
                    bool isDraft = false; // MapMagic 2 drafts
                    
                    FieldInfo draftField = gen.GetType().GetField("draft", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (draftField != null) isDraft = (bool)draftField.GetValue(gen);
                    
                    FieldInfo enabledField = gen.GetType().GetField("enabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    bool isEnabled = true;
                    if (enabledField != null) isEnabled = (bool)enabledField.GetValue(gen);

                    sb.Append($"[GRAPH_AUDIT] Node: {typeName} | Draft: {isDraft} | Enabled: {isEnabled}");
                    
                    // Specific fields
                    if (typeName.Contains("Erosion") || typeName.Contains("Noise") || typeName.Contains("Blend")) {
                        FieldInfo[] fields = gen.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var f in fields) {
                            if (f.FieldType.IsPrimitive || f.FieldType == typeof(string) || f.FieldType == typeof(Vector2)) {
                                sb.Append($" | {f.Name}={f.GetValue(gen)}");
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
        } catch (System.Exception e) {
            sb.AppendLine("[GRAPH_AUDIT] Exception: " + e.Message);
        }
        File.WriteAllText("C:/hades/Hecton8/proof_graph_audit_result.txt", sb.ToString());
    }
}
