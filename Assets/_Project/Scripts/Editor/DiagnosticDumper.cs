using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;

public static class DiagnosticDumper {
    public static void RunDump() {
        try {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
            
            using (var writer = new StreamWriter("diagnostics_final_text.txt", false)) {
                writer.WriteLine("--- MAP MAGIC ---");
                
                // Get MapMagic Object via string to avoid assembly issues if it fails, or just use type if it exists
                var allComponents = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Component mmObj = null;
                foreach (var c in allComponents) {
                    if (c != null && c.GetType().Name == "MapMagicObject") {
                        mmObj = c;
                        break;
                    }
                }
                
                if (mmObj != null) {
                    var mmType = mmObj.GetType();
                    var graphField = mmType.GetField("graph");
                    var graph = graphField?.GetValue(mmObj) as Object;
                    
                    writer.WriteLine("tileSize: " + mmType.GetField("tileSize")?.GetValue(mmObj));
                    writer.WriteLine("tileResolution: " + mmType.GetField("tileResolution")?.GetValue(mmObj));
                    writer.WriteLine("draftsInEditor: " + mmType.GetField("draftsInEditor")?.GetValue(mmObj));
                    writer.WriteLine("graphName: " + (graph != null ? graph.name : "null"));
                    writer.WriteLine("graphPath: " + (graph != null ? AssetDatabase.GetAssetPath(graph) : "null"));
                    
                    if (graph != null) {
                        writer.WriteLine("--- NODES ---");
                        var generatorsField = graph.GetType().GetField("generators");
                        var generators = generatorsField?.GetValue(graph) as System.Collections.IEnumerable;
                        if (generators != null) {
                            foreach (var gen in generators) {
                                writer.WriteLine("Node: " + gen.GetType().Name);
                                writer.WriteLine(EditorJsonUtility.ToJson(gen));
                            }
                        }
                    }
                }
                
                writer.WriteLine("--- TERRAINS ---");
                var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                writer.WriteLine("TerrainCount: " + terrains.Length);
                
                float minH = float.MaxValue, maxH = float.MinValue, sumH = 0;
                int heightSamples = 0;
                
                foreach (var t in terrains) {
                    var td = t.terrainData;
                    writer.WriteLine($"Terrain: {t.name}, size: {td.size}, heightmapRes: {td.heightmapResolution}, alphamapRes: {td.alphamapResolution}, pos: {t.transform.position}");
                    
                    if (t == terrains[0]) {
                        float[,] heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
                        for (int x=0; x<td.heightmapResolution; x+=2) {
                            for (int y=0; y<td.heightmapResolution; y+=2) {
                                float h = heights[x,y];
                                if (h < minH) minH = h;
                                if (h > maxH) maxH = h;
                                sumH += h;
                                heightSamples++;
                            }
                        }
                    }
                }
                
                if (heightSamples > 0) {
                    writer.WriteLine($"HeightMap: min={minH}, max={maxH}, avg={sumH/heightSamples}, samples={heightSamples}");
                }
                
                writer.WriteLine("--- SHADERS ---");
                writer.WriteLine("SunDirection: " + Shader.GetGlobalVector("_SunDirection"));
                writer.WriteLine("HectonTimeOfDay01: " + Shader.GetGlobalFloat("_HectonTimeOfDay01"));
            }
        } catch (System.Exception ex) {
            File.WriteAllText("diagnostics_final_error.txt", ex.ToString());
        } finally {
            EditorApplication.Exit(0);
        }
    }
}
