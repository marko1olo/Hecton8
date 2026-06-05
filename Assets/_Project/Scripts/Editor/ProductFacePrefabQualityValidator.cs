using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class ProductFacePrefabQualityValidator
    {
        private static readonly string[] ExactProductFacePrefabs =
        {
            "Assets/_Project/Prefabs/Player.prefab",
            "Assets/_Project/Prefabs/Sky_System.prefab",
            "Assets/_Project/Prefabs/Ocean_Crest.prefab",
            "Assets/_Project/Prefabs/Item_Titanium.prefab",
            "Assets/_Project/Prefabs/STRUCTURES.prefab",
            "Assets/_Project/Prefabs/Buildings/Cube.prefab",
        };

        private static readonly string[] ProductFacePrefabRoots =
        {
            "Assets/_Project/Prefabs/Tools/Held",
            "Assets/_Project/Prefabs/Items/Tools",
            "Assets/_Project/Prefabs/Resources/Pickups",
            "Assets/_Project/Prefabs/Transport",
        };

        [MenuItem("Hecton8/Validation/Product-Face Prefab Quality Gate")]
        public static void ValidateFromMenu()
        {
            if (!ValidateProductFacePrefabs(out int checkedCount, out int errorCount))
                Debug.LogError($"[ProductFacePrefabQualityValidator] Product-face prefab quality gate FAILED. Checked={checkedCount}, Errors={errorCount}.");
            else
                Debug.Log($"[ProductFacePrefabQualityValidator] Product-face prefab quality gate passed. Checked={checkedCount}.");
        }

        public static bool ValidateProductFacePrefabs(out int checkedCount, out int errorCount)
        {
            checkedCount = 0;
            errorCount = 0;

            HashSet<string> prefabPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < ExactProductFacePrefabs.Length; i++)
                AddRequiredPrefabPath(prefabPaths, ExactProductFacePrefabs[i], ref errorCount);

            for (int rootIndex = 0; rootIndex < ProductFacePrefabRoots.Length; rootIndex++)
            {
                string root = ProductFacePrefabRoots[rootIndex];
                if (!AssetDatabase.IsValidFolder(root))
                {
                    Debug.LogError($"[ProductFacePrefabQualityValidator] Missing product-face prefab root: {root}");
                    errorCount++;
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                    AddExistingPrefabPath(prefabPaths, path);
                }
            }

            foreach (string prefabPath in prefabPaths)
            {
                checkedCount++;
                ValidateSinglePrefab(prefabPath, ref errorCount);
            }

            return errorCount == 0;
        }

        private static void AddExistingPrefabPath(HashSet<string> prefabPaths, string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
                prefabPaths.Add(prefabPath);
        }

        private static void AddRequiredPrefabPath(HashSet<string> prefabPaths, string prefabPath, ref int errorCount)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[ProductFacePrefabQualityValidator] Missing required product-face prefab: {prefabPath}");
                errorCount++;
                return;
            }

            prefabPaths.Add(prefabPath);
        }

        private static void ValidateSinglePrefab(string prefabPath, ref int errorCount)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[ProductFacePrefabQualityValidator] Missing product-face prefab: {prefabPath}");
                errorCount++;
                return;
            }

            if (WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh(prefabPath))
            {
                Debug.LogError(
                    $"[ProductFacePrefabQualityValidator] {prefabPath} uses Unity built-in primitive mesh ids. "
                    + "Player-facing prefabs need authored/generated production meshes or explicit hidden-input proof.");
                errorCount++;
            }

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogError($"[ProductFacePrefabQualityValidator] {prefabPath} has no renderer hierarchy. Hidden-only product-face prefabs need explicit proof outside this generic gate.");
                errorCount++;
            }
        }
    }
}
