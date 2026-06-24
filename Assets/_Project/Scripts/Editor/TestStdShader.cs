using UnityEngine;
using UnityEditor;

public static class TestStdShader {
    public static void Execute() {
        var stdShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        var ourShader = Shader.Find("Hecton8/URP/Terrain_TextureArray");

        Debug.Log("[ShaderTest] Standard URP Terrain shader: " + (stdShader != null ? "FOUND" : "NULL"));
        if (stdShader != null) {
            Debug.Log("[ShaderTest] Standard isSupported: " + stdShader.isSupported);
            Debug.Log("[ShaderTest] Standard passCount: " + stdShader.passCount);
        }

        Debug.Log("[ShaderTest] Hecton8 Terrain shader: " + (ourShader != null ? "FOUND" : "NULL"));
        if (ourShader != null) {
            Debug.Log("[ShaderTest] Hecton8 isSupported: " + ourShader.isSupported);
            Debug.Log("[ShaderTest] Hecton8 passCount: " + ourShader.passCount);
        }

        // Also check what shader the terrain material currently uses
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
        if (mat != null) {
            Debug.Log("[ShaderTest] Material shader: " + mat.shader.name);
            Debug.Log("[ShaderTest] Material shader isSupported: " + mat.shader.isSupported);
        }

        EditorApplication.Exit(0);
    }
}
