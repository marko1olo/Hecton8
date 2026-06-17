using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DiagTerrain
{
    public static void Execute()
    {
        // Check both scenes
        string[] scenes = {
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
            "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity",
            "Assets/_Project/Scenes/010_TEST.unity"
        };

        foreach (var scenePath in scenes)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.Log("[DiagTerrain] Scene not found: " + scenePath);
                continue;
            }

            Debug.Log("[DiagTerrain] === SCENE: " + scenePath + " ===");
            EditorSceneManager.OpenScene(scenePath);

            var terrains = Terrain.activeTerrains;
            Debug.Log("[DiagTerrain] Active terrains: " + terrains.Length);

            foreach (var t in terrains)
            {
                Debug.Log("[DiagTerrain] Terrain: " + t.name + " pos=" + t.transform.position);
                Debug.Log("[DiagTerrain]   terrainData=" + (t.terrainData != null ? t.terrainData.name : "NULL"));
                Debug.Log("[DiagTerrain]   materialTemplate=" + (t.materialTemplate != null ? t.materialTemplate.name + " shader=" + t.materialTemplate.shader.name : "NULL"));

                if (t.terrainData != null)
                {
                    Debug.Log("[DiagTerrain]   size=" + t.terrainData.size);
                    Debug.Log("[DiagTerrain]   heightmapRes=" + t.terrainData.heightmapResolution);
                    var layers = t.terrainData.terrainLayers;
                    Debug.Log("[DiagTerrain]   terrainLayers=" + (layers != null ? layers.Length.ToString() : "NULL"));
                    if (layers != null)
                    {
                        for (int i = 0; i < layers.Length; i++)
                        {
                            if (layers[i] != null)
                                Debug.Log("[DiagTerrain]     layer[" + i + "]=" + layers[i].name + " diffuse=" + (layers[i].diffuseTexture != null ? layers[i].diffuseTexture.name : "null"));
                        }
                    }
                    var alphamaps = t.terrainData.alphamapTextures;
                    Debug.Log("[DiagTerrain]   alphamapTextures=" + (alphamaps != null ? alphamaps.Length.ToString() : "NULL"));
                }
            }

            // Find all GameObjects with "MapMagic" in name
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            int mmCount = 0;
            foreach (var go in allGOs)
            {
                if (go.name.Contains("MapMagic") && go.scene.IsValid())
                {
                    Debug.Log("[DiagTerrain] MapMagic GO: " + go.name + " active=" + go.activeInHierarchy);
                    mmCount++;
                }
            }
            Debug.Log("[DiagTerrain] MapMagic objects: " + mmCount);

            // Check cameras
            var cams = Camera.allCameras;
            Debug.Log("[DiagTerrain] Cameras: " + cams.Length);
            foreach (var c in cams)
            {
                Debug.Log("[DiagTerrain] Cam: " + c.name + " pos=" + c.transform.position + " bg=" + c.backgroundColor);
            }
        }

        EditorApplication.Exit(0);
    }
}
