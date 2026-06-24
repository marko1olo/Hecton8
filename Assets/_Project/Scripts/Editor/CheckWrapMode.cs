using UnityEngine;
using UnityEditor;

public class CheckWrapMode {
    public static void Execute() {
        var albedoPath = "Assets/_Project/Textures/Terrain/TerrainAlbedoArray.asset";
        var albedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(albedoPath);
        if (albedoArray != null) {
            Debug.Log($"[CWM] Albedo Array WrapMode: {albedoArray.wrapMode}");
            Debug.Log($"[CWM] Albedo Array FilterMode: {albedoArray.filterMode}");
            Debug.Log($"[CWM] Albedo Array Aniso: {albedoArray.anisoLevel}");
        } else {
            Debug.Log("[CWM] Not found!");
        }
    }
}
