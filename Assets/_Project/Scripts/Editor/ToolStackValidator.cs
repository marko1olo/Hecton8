using System.Collections.Generic;
using System.Reflection;
using Hecton8.Dev;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class ToolStackValidator
    {
        private const string ToolItemsRoot = "Assets/_Project/Data/Items/Tools";
        private const string ToolMetaRoot = "Assets/_Project/Data/Tools";
        private const string HeldPrefabRoot = "Assets/_Project/Prefabs/Tools/Held";
        private const string ItemCatalogPath = "Assets/_Project/Data/Items/ItemCatalog.asset";

        [MenuItem("Hecton/Validation/Validate Tool Stack", priority = 241)]
        public static void ValidateToolStack()
        {
            int errorCount = 0;
            int warningCount = 0;

            List<ItemData> toolItems = LoadToolItems(ref errorCount, ref warningCount);
            HashSet<ItemData> itemSet = new HashSet<ItemData>(toolItems);
            HashSet<string> toolIds = new HashSet<string>(32, System.StringComparer.Ordinal);
            HashSet<string> itemIds = new HashSet<string>(toolItems.Count, System.StringComparer.Ordinal);
            Dictionary<string, ItemData> itemAliases = new Dictionary<string, ItemData>(toolItems.Count * 2, System.StringComparer.Ordinal);

            ValidateToolItemIdentities(toolItems, itemIds, itemAliases, ref errorCount, ref warningCount);

            ValidateToolMetadata(toolIds, ref errorCount, ref warningCount);
            ValidateHeldPrefabs(itemSet, ref errorCount, ref warningCount);
            ValidateItemCatalog(toolItems, ref errorCount, ref warningCount);
            ValidateProvisioner(itemSet, ref errorCount, ref warningCount);
            ValidateToolStaging(itemSet, ref errorCount, ref warningCount);
            ValidateOperationalHudCoverage(ref errorCount, ref warningCount);

            if (errorCount <= 0 && warningCount <= 0)
            {
                Debug.Log("[ToolStackValidation] PASS no issues found.");
                return;
            }

            Debug.LogWarning($"[ToolStackValidation] COMPLETE errors={errorCount} warnings={warningCount}");
        }

        private static List<ItemData> LoadToolItems(ref int errorCount, ref int warningCount)
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { ToolItemsRoot });
            List<ItemData> results = new List<ItemData>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item == null)
                    continue;

                results.Add(item);

                if (item.category != ItemCategory.Tool)
                {
                    Debug.LogError($"[ToolStackValidation] ItemData is not categorized as Tool: {path}", item);
                    errorCount++;
                }

                if (string.IsNullOrWhiteSpace(item.itemName))
                {
                    Debug.LogError($"[ToolStackValidation] Tool item has empty itemName: {path}", item);
                    errorCount++;
                }

                if (item.worldPrefab == null)
                {
                    Debug.LogError($"[ToolStackValidation] Tool item missing worldPrefab: {path}", item);
                    errorCount++;
                }

                if (item.worldBuoyancyProfile == null)
                {
                    Debug.LogWarning($"[ToolStackValidation] Tool item missing worldBuoyancyProfile: {path}", item);
                    warningCount++;
                }

                if (item.stackable || item.maxStack > 1)
                {
                    Debug.LogWarning($"[ToolStackValidation] Tool item is stackable; expected unique tool semantics: {path}", item);
                    warningCount++;
                }
            }

            return results;
        }

        private static void ValidateToolItemIdentities(
            List<ItemData> toolItems,
            HashSet<string> itemIds,
            Dictionary<string, ItemData> itemAliases,
            ref int errorCount,
            ref int warningCount)
        {
            for (int i = 0; i < toolItems.Count; i++)
            {
                ItemData item = toolItems[i];
                if (item == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(item);
                string persistentId = item.PersistentId;
                if (string.IsNullOrWhiteSpace(persistentId))
                {
                    Debug.LogError($"[ToolStackValidation] Tool item resolves to empty PersistentId: {assetPath}", item);
                    errorCount++;
                }
                else if (!itemIds.Add(persistentId))
                {
                    Debug.LogError($"[ToolStackValidation] Duplicate tool PersistentId '{persistentId}': {assetPath}", item);
                    errorCount++;
                }

                SerializedObject serializedItem = new SerializedObject(item);
                SerializedProperty stableIdProperty = serializedItem.FindProperty("stableId");
                if (stableIdProperty == null || string.IsNullOrWhiteSpace(stableIdProperty.stringValue))
                {
                    Debug.LogWarning(
                        $"[ToolStackValidation] Tool item relies on asset-name fallback for PersistentId. Stamp explicit stableId before rename-sensitive content work: {assetPath}",
                        item);
                    warningCount++;
                }

                RegisterAlias(itemAliases, persistentId, item, assetPath, ref errorCount);
                RegisterAlias(itemAliases, item.name, item, assetPath, ref errorCount);
            }
        }

        private static void ValidateToolMetadata(HashSet<string> toolIds, ref int errorCount, ref int warningCount)
        {
            string[] guids = AssetDatabase.FindAssets("t:ToolMetadata", new[] { ToolMetaRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ToolMetadata metadata = AssetDatabase.LoadAssetAtPath<ToolMetadata>(path);
                if (metadata == null)
                    continue;

                if (string.IsNullOrWhiteSpace(metadata.toolID))
                {
                    Debug.LogError($"[ToolStackValidation] ToolMetadata missing toolID: {path}", metadata);
                    errorCount++;
                }
                else if (!toolIds.Add(metadata.toolID))
                {
                    Debug.LogError($"[ToolStackValidation] Duplicate toolID '{metadata.toolID}': {path}", metadata);
                    errorCount++;
                }

                if (metadata.maxDurability <= 0f)
                {
                    Debug.LogError($"[ToolStackValidation] ToolMetadata maxDurability <= 0: {path}", metadata);
                    errorCount++;
                }

                if (metadata.energyConsumptionRate < 0f)
                {
                    Debug.LogError($"[ToolStackValidation] ToolMetadata energyConsumptionRate < 0: {path}", metadata);
                    errorCount++;
                }

                if (metadata.maxUpgradeSlots < 0 || metadata.maxUpgradeSlots > 3)
                {
                    Debug.LogError($"[ToolStackValidation] ToolMetadata maxUpgradeSlots out of supported range: {path}", metadata);
                    errorCount++;
                }
            }
        }

        [MenuItem("Hecton/Validation/Validate Tool Operational HUD", priority = 242)]
        public static void ValidateToolOperationalHud()
        {
            int errorCount = 0;
            int warningCount = 0;

            ValidateOperationalHudCoverage(ref errorCount, ref warningCount);

            if (errorCount <= 0 && warningCount <= 0)
            {
                Debug.Log("[ToolOperationalValidation] PASS no issues found.");
                return;
            }

            Debug.LogWarning($"[ToolOperationalValidation] COMPLETE errors={errorCount} warnings={warningCount}");
        }

        private static void ValidateHeldPrefabs(HashSet<ItemData> toolItems, ref int errorCount, ref int warningCount)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { HeldPrefabRoot });
            HashSet<ItemData> heldPrefabItems = new HashSet<ItemData>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                PlayerTool tool = prefab.GetComponent<PlayerTool>();
                if (tool == null)
                {
                    Debug.LogError($"[ToolStackValidation] Held prefab missing PlayerTool: {path}", prefab);
                    errorCount++;
                    continue;
                }

                if (tool.ToolData == null)
                {
                    Debug.LogError($"[ToolStackValidation] Held prefab missing ToolData binding: {path}", prefab);
                    errorCount++;
                }
                else if (!toolItems.Contains(tool.ToolData))
                {
                    Debug.LogError($"[ToolStackValidation] Held prefab ToolData not found in tool item set: {path}", prefab);
                    errorCount++;
                }
                else if (!heldPrefabItems.Add(tool.ToolData))
                {
                    Debug.LogError($"[ToolStackValidation] Duplicate held prefab coverage for ToolData '{tool.ToolData.PersistentId}': {path}", prefab);
                    errorCount++;
                }

                if (tool.Metadata == null)
                {
                    Debug.LogError($"[ToolStackValidation] Held prefab missing ToolMetadata binding: {path}", prefab);
                    errorCount++;
                }

                if (prefab.GetComponentInChildren<Renderer>(true) == null)
                {
                    Debug.LogWarning($"[ToolStackValidation] Held prefab has no renderable child content: {path}", prefab);
                    warningCount++;
                }
            }

            foreach (ItemData item in toolItems)
            {
                if (item != null && !heldPrefabItems.Contains(item))
                {
                    Debug.LogError($"[ToolStackValidation] Tool item has no held prefab coverage: {item.PersistentId}.", item);
                    errorCount++;
                }
            }
        }

        private static void ValidateItemCatalog(List<ItemData> toolItems, ref int errorCount, ref int warningCount)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[ToolStackValidation] Missing ItemCatalog asset: {ItemCatalogPath}");
                errorCount++;
                return;
            }

            for (int i = 0; i < toolItems.Count; i++)
            {
                ItemData item = toolItems[i];
                ItemData resolvedByPersistentId = catalog.FindById(item.PersistentId);
                if (!ReferenceEquals(item, resolvedByPersistentId))
                {
                    Debug.LogError($"[ToolStackValidation] ItemCatalog does not resolve tool PersistentId '{item.PersistentId}'.", catalog);
                    errorCount++;
                }

                ItemData resolvedByLegacyAlias = catalog.FindById(item.name);
                if (!ReferenceEquals(item, resolvedByLegacyAlias))
                {
                    Debug.LogError($"[ToolStackValidation] ItemCatalog does not resolve tool legacy alias '{item.name}'.", catalog);
                    errorCount++;
                }
            }
        }

        private static void RegisterAlias(
            Dictionary<string, ItemData> itemAliases,
            string alias,
            ItemData item,
            string assetPath,
            ref int errorCount)
        {
            if (string.IsNullOrWhiteSpace(alias) || item == null)
                return;

            if (itemAliases.TryGetValue(alias, out ItemData existing))
            {
                if (!ReferenceEquals(existing, item))
                {
                    Debug.LogError(
                        $"[ToolStackValidation] Identity alias collision '{alias}' between '{existing.name}' and '{item.name}': {assetPath}",
                        item);
                    errorCount++;
                }

                return;
            }

            itemAliases.Add(alias, item);
        }

        private static void ValidateProvisioner(HashSet<ItemData> toolItems, ref int errorCount, ref int warningCount)
        {
            ToolLoadoutProvisioner provisioner = FindSceneObjectIncludingInactive<ToolLoadoutProvisioner>();
            if (provisioner == null)
            {
                Debug.LogWarning("[ToolStackValidation] No ToolLoadoutProvisioner found in the loaded scene.");
                warningCount++;
                return;
            }

            SerializedObject so = new SerializedObject(provisioner);
            SerializedProperty allToolItems = so.FindProperty("allToolItems");
            SerializedProperty coreQuickSlots = so.FindProperty("coreQuickSlotPrefabs");

            if (allToolItems == null || allToolItems.arraySize < toolItems.Count)
            {
                Debug.LogError(
                    $"[ToolStackValidation] ToolLoadoutProvisioner allToolItems is undersized. expected>={toolItems.Count} actual={(allToolItems != null ? allToolItems.arraySize : 0)}",
                    provisioner);
                errorCount++;
            }
            else
            {
                HashSet<ItemData> provisionerItems = new HashSet<ItemData>(allToolItems.arraySize);
                for (int i = 0; i < allToolItems.arraySize; i++)
                {
                    Object itemRefObject = allToolItems.GetArrayElementAtIndex(i).objectReferenceValue;
                    ItemData itemRef = itemRefObject as ItemData;
                    if (itemRef == null)
                    {
                        Debug.LogError($"[ToolStackValidation] ToolLoadoutProvisioner allToolItems[{i}] is null.", provisioner);
                        errorCount++;
                        continue;
                    }

                    if (!toolItems.Contains(itemRef))
                    {
                        Debug.LogError($"[ToolStackValidation] ToolLoadoutProvisioner allToolItems[{i}] is not a tool ItemData: {itemRef.name}.", provisioner);
                        errorCount++;
                    }

                    if (!provisionerItems.Add(itemRef))
                    {
                        Debug.LogError($"[ToolStackValidation] ToolLoadoutProvisioner allToolItems duplicate: {itemRef.name}.", provisioner);
                        errorCount++;
                    }
                }

                foreach (ItemData item in toolItems)
                {
                    if (item != null && !provisionerItems.Contains(item))
                    {
                        Debug.LogError($"[ToolStackValidation] ToolLoadoutProvisioner allToolItems missing tool item: {item.PersistentId}.", provisioner);
                        errorCount++;
                    }
                }
            }

            if (coreQuickSlots == null || coreQuickSlots.arraySize < 4)
            {
                Debug.LogError("[ToolStackValidation] ToolLoadoutProvisioner coreQuickSlotPrefabs is undersized.", provisioner);
                errorCount++;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    if (coreQuickSlots.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        Debug.LogWarning($"[ToolStackValidation] Core quick slot prefab {i} is null.", provisioner);
                        warningCount++;
                    }
                }
            }
        }

        private static void ValidateToolStaging(HashSet<ItemData> toolItems, ref int errorCount, ref int warningCount)
        {
            GameObject staging = FindSceneGameObjectIncludingInactive("Tool_Staging");
            if (staging == null)
            {
                Debug.LogWarning("[ToolStackValidation] Tool_Staging root not found in loaded scene.");
                warningCount++;
                return;
            }

            Transform stagingTransform = staging.transform;
            int childCount = stagingTransform.childCount;
            if (childCount <= 0)
            {
                Debug.LogWarning("[ToolStackValidation] Tool_Staging contains no child pickups.", staging);
                warningCount++;
                return;
            }

            HashSet<ItemData> stagedItems = new HashSet<ItemData>(childCount);
            for (int i = 0; i < childCount; i++)
            {
                Transform child = stagingTransform.GetChild(i);
                if (child == null)
                {
                    errorCount++;
                    continue;
                }

                PickupItem pickup = child.GetComponent<PickupItem>();
                if (pickup == null)
                {
                    if (child.GetComponent<WorldInterestAnchor>() != null ||
                        child.GetComponent<WorldZoneAnchor>() != null)
                    {
                        continue;
                    }

                    Debug.LogError($"[ToolStackValidation] Staging child '{child.name}' missing PickupItem.", child.gameObject);
                    errorCount++;
                    continue;
                }

                SerializedObject pickupSo = new SerializedObject(pickup);
                SerializedProperty itemDataProp = pickupSo.FindProperty("itemData");
                ItemData boundItem = itemDataProp != null ? itemDataProp.objectReferenceValue as ItemData : null;
                if (boundItem == null)
                {
                    Debug.LogError($"[ToolStackValidation] Staging pickup '{child.name}' missing itemData binding.", child.gameObject);
                    errorCount++;
                    continue;
                }

                stagedItems.Add(boundItem);
            }

            foreach (ItemData toolItem in toolItems)
            {
                if (!stagedItems.Contains(toolItem))
                {
                    Debug.LogWarning($"[ToolStackValidation] Tool_Staging missing '{toolItem.itemName}'.", staging);
                    warningCount++;
                }
            }
        }

        private static void ValidateOperationalHudCoverage(ref int errorCount, ref int warningCount)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { HeldPrefabRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                PlayerTool tool = prefab.GetComponent<PlayerTool>();
                if (tool == null)
                    continue;

                ValidateOverride(tool, path, nameof(PlayerTool.BuildLegacyOperationalSummaryString), ref errorCount, ref warningCount);
                ValidateOverride(tool, path, nameof(PlayerTool.BuildLegacyOperationalDirectiveString), ref errorCount, ref warningCount);
            }

            HUDQuickBar quickBar = FindSceneObjectIncludingInactive<HUDQuickBar>();
            if (quickBar == null)
            {
                Debug.LogError("[ToolOperationalValidation] HUDQuickBar not found in the loaded scene.");
                errorCount++;
            }
        }

        private static void ValidateOverride(PlayerTool tool, string path, string methodName, ref int errorCount, ref int warningCount)
        {
            MethodInfo method = tool.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                Debug.LogError($"[ToolOperationalValidation] Missing method '{methodName}' on {tool.GetType().Name}: {path}", tool);
                errorCount++;
                return;
            }

            if (method.DeclaringType == typeof(PlayerTool))
            {
                Debug.LogWarning($"[ToolOperationalValidation] Tool uses fallback '{methodName}' from PlayerTool: {path}", tool);
                warningCount++;
            }
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    GameObject root = roots[rootIndex];
                    if (root == null)
                        continue;

                    T candidate = root.GetComponentInChildren<T>(true);
                    if (candidate != null)
                        return candidate;
                }
            }

            return null;
        }

        private static GameObject FindSceneGameObjectIncludingInactive(string name)
        {
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    GameObject candidate = FindChildGameObjectRecursive(roots[rootIndex], name);
                    if (candidate != null)
                        return candidate;
                }
            }

            return null;
        }

        private static GameObject FindChildGameObjectRecursive(GameObject root, string name)
        {
            if (root == null)
                return null;

            if (string.Equals(root.name, name, System.StringComparison.Ordinal))
                return root;

            Transform transform = root.transform;
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null)
                    continue;

                GameObject found = FindChildGameObjectRecursive(child.gameObject, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
