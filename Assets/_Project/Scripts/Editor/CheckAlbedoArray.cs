using UnityEngine;
using UnityEditor;
public class CheckAlbedoArray {
    public static void Execute() {
        var albedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_Project/Art/Textures/TerrainArrays/Terrain_AlbedoArray.asset");
        if (albedoArray == null) { Debug.Log("[FAS] Array not found!"); EditorApplication.Exit(1); return; }
        for (int i=0; i<albedoArray.depth; i++) {
            Color[] pixels = albedoArray.GetPixels(i, 0);
            float r=0,g=0,b=0;
            foreach(var p in pixels) { r+=p.r; g+=p.g; b+=p.b; }
            int c = pixels.Length;
            Debug.Log($"[FAS] Slice {i} avg RGB: {r/c:F3}, {g/c:F3}, {b/c:F3}");
        }
        EditorApplication.Exit(0);
    }
}
