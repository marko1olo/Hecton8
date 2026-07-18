using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Diagnostics
{
    public static class EnableTerrainPOMTask
    {
        [MenuItem("Hecton8/Diagnostics/Enable Terrain POM")]
        public static void RunFix()
        {
            string matPath = "Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                mat.EnableKeyword("_PARALLAX_ON");
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnableTerrainPOMTask] Enabled _PARALLAX_ON for {matPath}. Active keywords: {string.Join(", ", mat.shaderKeywords)}");
            }
            else
            {
                Debug.LogError($"[EnableTerrainPOMTask] Could not find material at {matPath}");
            }
        }

        // Headless entry point
        public static void ExecuteHeadless()
        {
            RunFix();
            EditorApplication.Exit(0);
        }
    }
}
