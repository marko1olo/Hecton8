using UnityEngine;
using UnityEditor;

public static class CheckMaterial {
    public static void Execute() {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat == null) { Debug.Log("[CM] MAT IS NULL"); return; }
        Debug.Log("[CM] Shader: " + mat.shader.name);
        Texture tex = mat.GetTexture("_AlbedoArray");
        if (tex != null) {
            Debug.Log("[CM] _AlbedoArray is BOUND: " + tex.name + " type: " + tex.GetType());
        } else {
            Debug.Log("[CM] _AlbedoArray is NULL! ");
        }
    }
}
