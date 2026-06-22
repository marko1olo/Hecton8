using UnityEngine;
using UnityEditor;
public class ForceSplatmaps {
    public static void Execute() {
        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        foreach(var t in terrains) {
            t.materialTemplate = mat;
            var alphamaps = t.terrainData.alphamapTextures;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            t.GetSplatMaterialPropertyBlock(block);
            if (alphamaps.Length > 0) block.SetTexture("_Control", alphamaps[0]);
            if (alphamaps.Length > 1) block.SetTexture("_Control1", alphamaps[1]);
            t.SetSplatMaterialPropertyBlock(block);
        }
        Debug.Log("[FAS] Splatmaps forced into MaterialPropertyBlocks.");
        EditorApplication.Exit(0);
    }
}
