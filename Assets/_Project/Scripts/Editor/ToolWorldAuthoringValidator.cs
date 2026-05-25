using System.Collections.Generic;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    public static class ToolWorldAuthoringValidator
    {
        private const string ToolItemRoot = "Assets/_Project/Data/Items/Tools";

        [MenuItem("Hecton/Validation/Validate Tool World Authoring", priority = 231)]
        public static void Validate()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { ToolItemRoot });
            List<string> issues = new List<string>(64);
            Dictionary<ItemData, int> stagedCounts = BuildStagingCounts();

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (item == null)
                    continue;

                ValidateItemData(item, assetPath, issues, stagedCounts);
            }

            if (issues.Count == 0)
            {
                Debug.Log("[ToolWorldValidation] PASS no issues found.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
                Debug.LogWarning("[ToolWorldValidation] " + issues[i]);

            Debug.LogWarning($"[ToolWorldValidation] FAIL {issues.Count} issue(s) found.");
        }

        private static void ValidateItemData(ItemData item, string assetPath, List<string> issues, Dictionary<ItemData, int> stagedCounts)
        {
            if (item.worldPrefab == null)
            {
                issues.Add($"{assetPath}: worldPrefab is missing.");
                return;
            }

            if (item.worldBuoyancyProfile == null)
                issues.Add($"{assetPath}: worldBuoyancyProfile is missing.");

            string prefabPath = AssetDatabase.GetAssetPath(item.worldPrefab);
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                issues.Add($"{assetPath}: worldPrefab has no asset path.");
                return;
            }

            if (!prefabPath.Contains("/Prefabs/Items/Tools/"))
                issues.Add($"{assetPath}: worldPrefab is outside expected tool world prefab folder -> {prefabPath}");

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ValidateWorldPrefab(item, assetPath, prefabPath, prefabRoot, issues);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            if (!stagedCounts.TryGetValue(item, out int stagedCount) || stagedCount <= 0)
                issues.Add($"{assetPath}: active scene Tool_Staging has no staged instance for this tool item.");
        }

        private static void ValidateWorldPrefab(ItemData item, string assetPath, string prefabPath, GameObject prefabRoot, List<string> issues)
        {
            PickupItem pickup = prefabRoot.GetComponentInChildren<PickupItem>(true);
            HectonItem hectonItem = prefabRoot.GetComponentInChildren<HectonItem>(true);

            if (pickup == null && hectonItem == null)
            {
                issues.Add($"{assetPath}: worldPrefab has neither PickupItem nor HectonItem -> {prefabPath}");
                return;
            }

            if (pickup != null)
            {
                if (pickup.ItemData != item)
                    issues.Add($"{assetPath}: PickupItem on worldPrefab is linked to a different ItemData -> {prefabPath}");
                if (pickup.Quantity < 1)
                    issues.Add($"{assetPath}: PickupItem quantity must be >= 1 -> {prefabPath}");
            }

            if (hectonItem != null && hectonItem.Data != item)
                issues.Add($"{assetPath}: HectonItem on worldPrefab is linked to a different ItemData -> {prefabPath}");

            Rigidbody body = prefabRoot.GetComponentInChildren<Rigidbody>(true);
            if (body == null)
                issues.Add($"{assetPath}: worldPrefab has no Rigidbody -> {prefabPath}");

            if (item.worldBuoyancyProfile != null)
            {
                BuoyancyObject buoyancy = prefabRoot.GetComponentInChildren<BuoyancyObject>(true);
                if (buoyancy == null)
                    issues.Add($"{assetPath}: worldPrefab has no BuoyancyObject while worldBuoyancyProfile is assigned -> {prefabPath}");
            }

            Collider col = prefabRoot.GetComponentInChildren<Collider>(true);
            if (col == null)
                issues.Add($"{assetPath}: worldPrefab has no Collider -> {prefabPath}");
        }

        private static Dictionary<ItemData, int> BuildStagingCounts()
        {
            Dictionary<ItemData, int> counts = new Dictionary<ItemData, int>(32);
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return counts;

            GameObject stagingRoot = FindInScene("Tool_Staging");
            if (stagingRoot == null)
                return counts;

            PickupItem[] pickups = stagingRoot.GetComponentsInChildren<PickupItem>(true);
            for (int i = 0; i < pickups.Length; i++)
            {
                ItemData item = pickups[i].ItemData;
                if (item == null)
                    continue;
                counts.TryGetValue(item, out int current);
                counts[item] = current + 1;
            }

            HectonItem[] hectonItems = stagingRoot.GetComponentsInChildren<HectonItem>(true);
            for (int i = 0; i < hectonItems.Length; i++)
            {
                ItemData item = hectonItems[i].Data;
                if (item == null)
                    continue;
                counts.TryGetValue(item, out int current);
                counts[item] = current + 1;
            }

            return counts;
        }

        private static GameObject FindInScene(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindRecursive(roots[i].transform, name);
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }

        private static Transform FindRecursive(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindRecursive(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
