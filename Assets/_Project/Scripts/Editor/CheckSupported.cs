using UnityEngine;
using UnityEditor;

public static class CheckSupported
{
    public static void Execute()
    {
        string path = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/Tiles/gemini_Batch20260608_TextureExpansion_b34_3408_clay_silt_turbidity_slope/TX_B34_gemini_Batch20260608_TextureExpansion_b34_3408_clay_silt_turbidity_slope_MaskMap.jpg";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
        {
            Debug.Log($"[CheckTex] Format: {tex.format}");
        }
        else
        {
            Debug.Log($"[CheckTex] Texture not found at {path}");
        }
        EditorApplication.Exit(0);
    }
}
