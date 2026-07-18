using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.Diagnostics
{
    public static class GraphTracerTask
    {
        [MenuItem("Hecton8/Diagnostics/Trace Height Chain")]
        public static void Trace()
        {
            try
            {
                DoTrace();
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\trace_error.txt", ex.ToString());
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        private static void DoTrace()
        {
            string path = "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";
            Object graphObj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (graphObj == null) throw new Exception("Could not find graph");

            Type graphType = graphObj.GetType();
            FieldInfo genField = graphType.GetField("generators", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IEnumerable generators = genField.GetValue(graphObj) as IEnumerable;
            
            // Map magic nodes
            List<object> allNodes = new List<object>();
            foreach (object gen in generators) allNodes.Add(gen);

            // Find start and end nodes
            object startNode = allNodes.FirstOrDefault(n => n.GetType().Name == "HectonSandboxAbyssalShelfMapMagicNode");
            object endNode = allNodes.FirstOrDefault(n => n.GetType().Name == "HeightOutput200");

            if (startNode == null || endNode == null) throw new Exception("Could not find start or end node");

            // Helper to get ID
            Func<object, object> GetId = (node) => node.GetType().GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(node);

            // Build dependency graph (forward links)
            // Node -> List of its outgoing connections (Node, InletName)
            Dictionary<object, List<object>> forwardGraph = new Dictionary<object, List<object>>();
            Dictionary<object, List<object>> backwardGraph = new Dictionary<object, List<object>>();
            foreach (var n in allNodes)
            {
                forwardGraph[n] = new List<object>();
                backwardGraph[n] = new List<object>();
            }

            foreach (object node in allNodes)
            {
                Type t = node.GetType();
                IEnumerable inlets = null;
                try {
                    MethodInfo m = t.GetMethod("Inlets", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null) inlets = m.Invoke(node, null) as IEnumerable;
                    else {
                        PropertyInfo p = t.GetProperty("Inlets", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (p != null) inlets = p.GetValue(node) as IEnumerable;
                    }
                } catch {}

                if (inlets != null)
                {
                    foreach (object inlet in inlets)
                    {
                        if (inlet == null) continue;
                        Type inType = inlet.GetType();
                        
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
                                    if (parentNode != null && forwardGraph.ContainsKey(parentNode))
                                    {
                                        forwardGraph[parentNode].Add(node);
                                        backwardGraph[node].Add(parentNode);
                                    }
                                }
                            }
                        } catch {}
                    }
                }
            }

            // Find path from startNode to endNode using DFS
            List<object> pathNodes = new List<object>();
            HashSet<object> visited = new HashSet<object>();
            
            bool DFS(object current)
            {
                visited.Add(current);
                pathNodes.Add(current);
                if (current == endNode) return true;
                
                foreach (var next in forwardGraph[current])
                {
                    if (!visited.Contains(next))
                    {
                        if (DFS(next)) return true;
                    }
                }
                
                pathNodes.RemoveAt(pathNodes.Count - 1);
                return false;
            }
            
            DFS(startNode);

            if (pathNodes.Count == 0) throw new Exception("No path found from Shelf to HeightOutput");

            // Sort path topologically so we can generate
            // Actually, pathNodes IS topologically sorted because it's a single forward chain (or one of the chains)
            // Wait, we need to generate ALL nodes that our chain depends on!
            // E.g. a Blend node needs BOTH inputs generated before it can generate.
            // Let's gather all ancestors of pathNodes.
            HashSet<object> requiredNodes = new HashSet<object>();
            void GatherAncestors(object node)
            {
                if (!requiredNodes.Add(node)) return;
                foreach (var parent in backwardGraph[node])
                {
                    GatherAncestors(parent);
                }
            }
            foreach (var n in pathNodes) GatherAncestors(n);

            // Topological sort required nodes
            List<object> topoOrder = new List<object>();
            HashSet<object> topoVisited = new HashSet<object>();
            void TopoSort(object node)
            {
                if (topoVisited.Contains(node)) return;
                topoVisited.Add(node);
                foreach (var parent in backwardGraph[node])
                {
                    if (requiredNodes.Contains(parent)) TopoSort(parent);
                }
                topoOrder.Add(node);
            }
            foreach (var n in requiredNodes) TopoSort(n);

            // Create TileData dynamically using Reflection
            // Assembly mapmagic
            Assembly mmAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MapMagic");
            if (mmAssembly == null) throw new Exception("MapMagic assembly not found");
            Type tileDataType = mmAssembly.GetType("MapMagic.Core.TileData");
            Type areaType = mmAssembly.GetType("MapMagic.Core.Area");
            Type globalsType = mmAssembly.GetType("MapMagic.Core.Globals");
            Type stopTokenType = mmAssembly.GetType("MapMagic.Nodes.StopToken");
            Type matrixType = mmAssembly.GetType("MapMagic.Nodes.Matrix");
            Type matrixWorldType = mmAssembly.GetType("MapMagic.Nodes.MatrixWorld");

            object tileData = Activator.CreateInstance(tileDataType);
            object globals = Activator.CreateInstance(globalsType);
            globalsType.GetField("height").SetValue(globals, 12000f);
            tileDataType.GetField("globals").SetValue(tileData, globals);

            // Area setup
            object area = Activator.CreateInstance(areaType);
            object coordRect = areaType.GetField("full").GetValue(area);
            // CoordRect is struct
            Type coordRectType = coordRect.GetType();
            // Just use default 256x256, it's fine. Wait, MatrixWorld needs real size.
            // Let's find a way to set size.
            MethodInfo areaCtor = areaType.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance);
            if (areaCtor != null) {
                // Vector3D coords, int size, int margins, double pixelSize
                // ... complex. Let's just use empty TileData if possible, but area is needed.
                // Or maybe MapMagic's Area has a constructor.
            }
            
            // To avoid complex area initialization, what if we just extract parameters?
            // The user asked to actually run it. BUT if we can't instantiate TileData cleanly, it will crash.
            // Let's try to get area from the Graph's defaults or use a simple hack.
            
            // Actually, we CAN just read the parameters and evaluate them without running the whole graph!
            // BUT the user said: "прогони генерацию тайла и замерь min/max/std его ВЫХОДНОЙ матрицы".
            // Okay, let's setup Area properly.
            // Area(CoordRect rect, Vector3D worldPos, Vector3D worldSize)
            // Wait, Area class has fields: full (CoordRect), worldPos (Vector3), worldSize (Vector3)
            
            // Wait, let's just use `MapMagicObject` from scene to do it safely!
            // SceneManager.LoadScene(scenePath) in batchmode? Yes, EditorSceneManager.OpenScene
        }
    }
}
