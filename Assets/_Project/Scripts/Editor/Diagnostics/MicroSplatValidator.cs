using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Hecton8.Diagnostics
{
    public static class MicroSplatValidator
    {
        public static void Run()
        {
            Debug.Log("[MICROSPLAT] Starting Validation...");
            
            string scenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
            EditorSceneManager.OpenScene(scenePath);

            var mmObject = UnityEngine.Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
            if (mmObject == null)
            {
                Debug.LogError("[MICROSPLAT] MapMagicObject not found in 02_HECTON_WORLD.");
                EditorApplication.Exit(1);
                return;
            }

            Material mat = mmObject.terrainSettings.material;
            if (mat == null)
            {
                Debug.LogError("[MICROSPLAT] TerrainSettings.Material is NULL.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[MICROSPLAT] Terrain Material: {mat.name} (Shader: {mat.shader.name})");

            bool hasTessellation = mat.IsKeywordEnabled("_TESSELLATION_ON");
            bool hasParallax = mat.IsKeywordEnabled("_PARALLAX_ON");

            Debug.Log($"[MICROSPLAT] Tessellation Enabled: {hasTessellation}");
            Debug.Log($"[MICROSPLAT] Parallax Enabled: {hasParallax}");

            if (!hasTessellation && !hasParallax)
            {
                Debug.Log("[MICROSPLAT] STATUS: MicroSplat details are DISABLED. This confirms Task 4 hypothesis.");
            }
            else
            {
                Debug.Log("[MICROSPLAT] STATUS: MicroSplat details are ENABLED.");
            }

            EditorApplication.Exit(0);
        }
    }
}
