using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Diagnostics
{
    public static class DynamicGraphAuditorTask
    {
        [MenuItem("Hecton8/Diagnostics/Dynamic Graph Audit")]
        public static void Audit()
        {
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Object graphObj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (graphObj == null)
            {
                Debug.LogError("Could not find graph");
                EditorApplication.Exit(1);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# MapMagic Graph Dynamic Audit\n");

            Type graphType = graphObj.GetType();
            
            FieldInfo genField = graphType.GetField("generators", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (genField == null)
            {
                sb.AppendLine("Could not find 'generators' field in Graph.");
            }
            else
            {
                IEnumerable generators = genField.GetValue(graphObj) as IEnumerable;
                if (generators != null)
                {
                    foreach (object gen in generators)
                    {
                        Type t = gen.GetType();
                        FieldInfo idField = t.GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        object id = idField?.GetValue(gen);
                        sb.AppendLine($"Node ID: {id} | Type: {t.Name}");
                        
                        if (t.Name == "HectonSandboxAbyssalShelfMapMagicNode")
                        {
                            foreach (var fieldName in new[] {"ridgeHeightMeters", "ridgeMultiplier", "trenchDepthMeters", "trenchMultiplier"})
                            {
                                FieldInfo f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (f != null) sb.AppendLine($"  - {fieldName}: {f.GetValue(gen)}");
                            }
                        }
                        else if (t.Name == "Levels200")
                        {
                            foreach (var fieldName in new[] {"inMin", "inMax", "gamma", "outMin", "outMax"})
                            {
                                FieldInfo f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (f != null) sb.AppendLine($"  - {fieldName}: {f.GetValue(gen)}");
                            }
                        }
                        else if (t.Name == "Blend200")
                        {
                            FieldInfo f = t.GetField("algorithm", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (f != null) sb.AppendLine($"  - Algorithm: {f.GetValue(gen)}");
                        }
                        
                        // Check linked outlets to map the tree
                        IEnumerable inlets = null;
                        try {
                            MethodInfo m = t.GetMethod("Inlets", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (m != null) inlets = m.Invoke(gen, null) as IEnumerable;
                            else {
                                PropertyInfo p = t.GetProperty("Inlets", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (p != null) inlets = p.GetValue(gen) as IEnumerable;
                            }
                        } catch {}

                        if (inlets != null)
                        {
                            foreach (object inlet in inlets)
                            {
                                if (inlet == null) continue;
                                Type inType = inlet.GetType();
                                string inName = inType.GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(inlet) as string;
                                
                                try {
                                    MethodInfo getLinkMethod = graphType.GetMethod("GetLink", new Type[] { inType });
                                    if (getLinkMethod == null) getLinkMethod = graphType.GetMethod("GetLink", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    
                                    if (getLinkMethod != null)
                                    {
                                        object outlet = getLinkMethod.Invoke(graphObj, new object[] { inlet });
                                        if (outlet != null)
                                        {
                                            Type outType = outlet.GetType();
                                            PropertyInfo outGenProp = outType.GetProperty("Gen", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                            object parentNode = outGenProp?.GetValue(outlet);
                                            object parentId = parentNode != null ? parentNode.GetType().GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(parentNode) : "Unknown";
                                            sb.AppendLine($"  - INLET {(inName ?? "unnamed")} <- Outlet of Node {parentId}");
                                        }
                                    }
                                } catch {}
                            }
                        }
                        sb.AppendLine("");
                    }
                }
            }

            string outPath = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\graph_dump_dynamic.md";
            File.WriteAllText(outPath, sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
