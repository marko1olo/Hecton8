using UnityEngine;
using UnityEditor;
public static class CompileShader {
    public static void Execute() {
        Shader s = Shader.Find("Hecton8/URP/Terrain_TextureArray");
        if (s == null) {
            Debug.LogError("Shader not found!");
            EditorApplication.Exit(1);
        }
        if (!s.isSupported) {
            Debug.LogError("Shader not supported (compiled with errors)!");
            EditorApplication.Exit(1);
        }
        Debug.Log("Shader compiles successfully!");
        EditorApplication.Exit(0);
    }
}
