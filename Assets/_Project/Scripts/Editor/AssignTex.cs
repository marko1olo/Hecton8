using UnityEngine;
using UnityEditor;
public static class AssignTex {
    public static void Execute() {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat != null) {
            var a = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_Project/Art/Textures/TerrainArrays/Terrain_AlbedoArray.asset");
            var n = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_Project/Art/Textures/TerrainArrays/Terrain_NormalArray.asset");
            var m = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_Project/Art/Textures/TerrainArrays/Terrain_MaskArray.asset");
            if (a != null) { mat.SetTexture("_AlbedoArray", a); Debug.Log("Set _AlbedoArray"); } else { Debug.Log("AlbedoArray not found!"); }
            if (n != null) { mat.SetTexture("_NormalArray", n); Debug.Log("Set _NormalArray"); } else { Debug.Log("NormalArray not found!"); }
            if (m != null) { mat.SetTexture("_MaskArray", m); Debug.Log("Set _MaskArray"); } else { Debug.Log("MaskArray not found!"); }
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("Done assigning textures!");
        } else {
            Debug.Log("Mat not found!");
        }
        EditorApplication.Exit(0);
    }
}
