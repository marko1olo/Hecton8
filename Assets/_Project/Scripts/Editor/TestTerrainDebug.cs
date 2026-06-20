using UnityEngine;
using UnityEditor;
using System.IO;

public class TestTerrainDebug
{
    public static void Run()
    {
        var terrains = UnityEngine.Terrain.activeTerrains;
        if (terrains.Length == 0) {
            Debug.Log("No terrains found.");
            return;
        }

        var t = terrains[0];
        var td = t.terrainData;
        Debug.Log("Terrain Data: " + (td != null));
        if (td != null) {
            Debug.Log("Alphamap layers: " + td.alphamapLayers);
            Debug.Log("Alphamap textures: " + td.alphamapTextures.Length);
            
            var mat = t.materialTemplate;
            Debug.Log("Material: " + (mat != null));
            if (mat != null) {
                var ctrl = mat.GetTexture("_Control");
                Debug.Log("_Control bound: " + (ctrl != null));
            }
        }
        EditorApplication.Exit(0);
    }
}
