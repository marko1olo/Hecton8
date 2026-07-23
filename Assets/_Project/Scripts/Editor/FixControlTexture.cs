using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class FixControlTexture : MonoBehaviour {
    static FixControlTexture() {
        EditorApplication.update += UpdateTerrains;
    }

    static void UpdateTerrains() {
        if (Application.isPlaying) return;
        var terrains = Terrain.activeTerrains;
        foreach (var t in terrains) {
            if (t.terrainData != null && t.terrainData.alphamapTextures.Length > 1) {
                var block = new MaterialPropertyBlock();
                t.GetSplatMaterialPropertyBlock(block);
                block.SetTexture("_Control1", t.terrainData.alphamapTextures[1]);
                t.SetSplatMaterialPropertyBlock(block);
            }
        }
    }
}
