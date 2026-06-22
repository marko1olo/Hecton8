using UnityEngine;
using UnityEditor;

public class FixTerrainArrayAssets {
    public static void Execute() {
        string[] paths = {
            "Assets/_Project/Art/TEXTURES/TerrainArrays/Terrain_AlbedoArray.asset",
            "Assets/_Project/Art/TEXTURES/TerrainArrays/Terrain_NormalArray.asset",
            "Assets/_Project/Art/TEXTURES/TerrainArrays/Terrain_MaskArray.asset"
        };
        
        foreach(var path in paths) {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (tex != null) {
                Debug.Log($"[FTA] Modifying {path}");
                Debug.Log($"[FTA] Before: wrap={tex.wrapMode}, filter={tex.filterMode}, aniso={tex.anisoLevel}");
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Trilinear;
                tex.anisoLevel = 16;
                EditorUtility.SetDirty(tex);
            } else {
                Debug.Log($"[FTA] Not found: {path}");
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[FTA] Done.");
    }
}
