using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class FixTerrain {
    [InitializeOnLoadMethod]
    public static void RunFix() {
        try {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
            
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
                
                // Fix height
                var heightField = mmType.GetField("height");
                if (heightField != null) heightField.SetValue(mmObj, 3000f);
                
                // Fix draftsInEditor
                var draftsField = mmType.GetField("draftsInEditor");
                if (draftsField != null) draftsField.SetValue(mmObj, true);
                
                var graphField = mmType.GetField("graph");
                var graph = graphField?.GetValue(mmObj) as Object;
                
                if (graph != null) {
                    var generatorsField = graph.GetType().GetField("generators");
                    var generators = generatorsField?.GetValue(graph) as System.Collections.IEnumerable;
                    if (generators != null) {
                        foreach (var gen in generators) {
                            string genName = gen.GetType().Name;
                            if (genName == "HectonSandboxAbyssalShelfMapMagicNode") {
                                // Scale down parameters for 10x10km map
                                gen.GetType().GetField("descentRadiusMeters")?.SetValue(gen, 3500f);
                                gen.GetType().GetField("shelfRunMeters")?.SetValue(gen, 3500f);
                                gen.GetType().GetField("plateCellSizeMeters")?.SetValue(gen, 1500f);
                                gen.GetType().GetField("ridgeHeightMeters")?.SetValue(gen, 1000f);
                                gen.GetType().GetField("ridgeWidthMeters")?.SetValue(gen, 450f);
                                gen.GetType().GetField("junctionWidthMeters")?.SetValue(gen, 800f);
                                gen.GetType().GetField("domainWarpMeters")?.SetValue(gen, 400f);
                                gen.GetType().GetField("domainWarpFrequency")?.SetValue(gen, 0.0003f);
                                gen.GetType().GetField("slopeNoiseFrequency")?.SetValue(gen, 0.0001f);
                                gen.GetType().GetField("trenchDepthMeters")?.SetValue(gen, 1500f);
                                gen.GetType().GetField("trenchWidthMeters")?.SetValue(gen, 250f);
                                gen.GetType().GetField("islandCenterRadiusMeters")?.SetValue(gen, 1000f);
                            }
                            else if (genName == "HectonBiomeMatrixMapMagicPostProcessNode") {
                                gen.GetType().GetField("tectonicFrequency")?.SetValue(gen, 0.0005f); // scaled up frequency
                            }
                        }
                    }
                    EditorUtility.SetDirty(graph);
                    AssetDatabase.SaveAssets();
                }
                
                EditorUtility.SetDirty(mmObj);
                EditorSceneManager.SaveScene(scene);
            }
        } catch (System.Exception ex) {
            File.WriteAllText("fix_error.txt", ex.ToString());
        } finally {
            EditorApplication.Exit(0);
        }
    }
}
