using UnityEngine;
using UnityEditor;
using System.IO;

public static class CheckAlphamaps {
    public static void Execute() {
        var terrains = Terrain.activeTerrains;
        string log = "Terrains found: " + terrains.Length + "\n";
        if (terrains.Length > 0) {
            Terrain t = terrains[0];
            TerrainData td = t.terrainData;
            if (td != null) {
                log += "Alphamap resolution: " + td.alphamapResolution + "\n";
                log += "Alphamap layers: " + td.alphamapLayers + "\n";
                log += "Alphamap textures: " + td.alphamapTextures.Length + "\n";
                if (td.alphamapLayers > 0) {
                    float[,,] maps = td.GetAlphamaps(td.alphamapResolution/2, td.alphamapResolution/2, 1, 1);
                    for (int i=0; i<td.alphamapLayers; i++) {
                        log += "Layer " + i + " weight at center: " + maps[0,0,i] + "\n";
                    }
                }
            } else {
                log += "TerrainData is null\n";
            }
        }
        File.WriteAllText("alphamap_check.txt", log);
        EditorApplication.Exit(0);
    }
}
