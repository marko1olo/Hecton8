#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Dev;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Scavenging;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Performs cold-path content sanity validation for authored data assets and their referenced prefabs.
    /// Runtime gameplay code is not touched by this validator.
    /// </summary>
    internal static class ContentSanityValidator
    {
        private const string MenuPath = "Hecton-8/Validate Content";
        private const string DataRoot = "Assets/_Project/Data";
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const string CopperVeinTemplatePath = DataRoot + "/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ToolHeldPrefabRoot = "Assets/_Project/Prefabs/Tools/Held";
        private const string SeafloorDrillItemId = "Item_Tool_SeafloorDrill";
        private const string EmergencyO2CanisterItemId = "Data_EmergencyO2Canister";
        private const string ItemCatalogPath = DataRoot + "/Items/ItemCatalog.asset";
        private const string GeneratedRoot = DataRoot + "/Diagnostics/Generated/ContentSanity";
        private const string GeneratedMeshPath = GeneratedRoot + "/MESH_ContentSanityWireCube.asset";
        private const string GeneratedMaterialPath = GeneratedRoot + "/MAT_ContentSanityWireframe.mat";
        private const string FloraProxyFolder = GeneratedRoot + "/FloraGhostProxies";
        private const string InjectedProxyName = "__ContentSanityWireProxy";
        private static readonly string[] DataRoots = { DataRoot };
        private static readonly string[] FirstHourCraftMilestoneItemIds =
        {
            "Comp_CopperWire",
            EmergencyO2CanisterItemId,
            "Item_Tool_BeaconDeployer",
            "Item_Tool_Repair",
            "Comp_PressureSeal"
        };

        private sealed class ValidationResult
        {
            public readonly Dictionary<uint, string> HashOwners = new Dictionary<uint, string>(256);
            public readonly Dictionary<string, string> ItemPersistentIdOwners = new Dictionary<string, string>(256, StringComparer.Ordinal);
            public readonly List<string> Errors = new List<string>(128);
            public readonly List<string> Warnings = new List<string>(128);
            public readonly List<string> AutoFixes = new List<string>(128);
            public readonly HashSet<string> ProcessedPrefabPaths = new HashSet<string>(256, StringComparer.OrdinalIgnoreCase);

            public int DataPrefabCount;
            public int ReferencedPrefabCount;
            public int ItemCount;
            public int RecipeCount;
            public int QuestCount;
            public int ToolMetadataCount;
            public int ToolHeldPrefabCount;
            public int FloraCount;
            public int FaunaCount;
            public int ResourceNodeCount;
            public int BaseModuleCount;
            public int InjectedProxyCount;
            public int GeneratedFloraProxyCount;
            public int MeshColliderViolationCount;
            public int HashCollisionCount;
            public int ItemDataDuplicatePersistentIdCount;
            public int ItemCatalogNullEntryCount;
            public int ItemCatalogDuplicateHashCount;
            public int ItemCatalogMissingRuntimeDescriptorCount;
            public int ItemCatalogLookupAmbiguityCount;
            public int RecipeRouteErrorCount;
            public int RecipeScanGateWarningCount;
            public int QuestRouteErrorCount;
            public int ToolMetadataOrphanCount;
            public int ToolRouteErrorCount;
            public int AudioMaterialViolationCount;
            public int ResourceNodeYieldMissingWorldPrefabCount;
            public int ResourceNodeYieldNotCatalogedCount;
            public int ResourceNodeYieldInvalidWorldPrefabContractCount;
            public int ResourceNodeToolGateErrorCount;
            public int FirstHourCraftGateErrorCount;
            public int FirstHourDrillRouteErrorCount;
            public int FirstHourOxygenRouteErrorCount;
            public int PlayerPdaHeadlessOpenRiskCount;
            public int PlayerPdaBridgeWarningCount;
            public int PlayerDevProvisionerStartupRiskCount;
            public int PlayerStarterLoadoutErrorCount;
        }

        [MenuItem(MenuPath, priority = 141)]
        private static void ValidateContent()
        {
            ValidationResult result = new ValidationResult();
            EnsureFolder(DataRoot);
            EnsureFolder(GeneratedRoot);
            EnsureFolder(FloraProxyFolder);

            Mesh wireMesh = EnsureWireCubeMesh();
            Material wireMaterial = EnsureWireframeMaterial();

            ScanDataFolderPrefabs(result, wireMesh, wireMaterial);
            ValidateItemTemplates(result, wireMesh, wireMaterial);
            ValidateItemCatalog(result);
            HashSet<string> craftableItemIds = ValidateRecipeData(result);
            ValidateQuestData(result, craftableItemIds);
            ValidateToolAuthoring(result);
            ValidateFloraTemplates(result, wireMesh, wireMaterial);
            ValidateFaunaTemplates(result, wireMesh, wireMaterial);
            ValidateResourceNodeTemplates(result);
            ValidateFirstHourDrillRoute(result);
            ValidateBaseModuleTemplates(result);
            ValidatePlayerPdaShell(result);
            ValidatePlayerDevProvisioning(result);
            ValidatePlayerStarterLoadout(result);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EmitSummary(result);
        }

        private static void ScanDataFolderPrefabs(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", DataRoots);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                result.DataPrefabCount++;
                ValidatePrefabAsset(
                    prefabPath,
                    "data prefab",
                    result,
                    wireMesh,
                    wireMaterial,
                    allowMeshCollider: false);
            }
        }

        private static void ValidateItemTemplates(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] itemGuids = AssetDatabase.FindAssets("t:ItemData", DataRoots);
            for (int i = 0; i < itemGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (item == null)
                    continue;

                result.ItemCount++;
                string persistentId = item.PersistentId ?? string.Empty;
                RegisterItemPersistentId(result, persistentId, assetPath);
                int hashId = string.IsNullOrWhiteSpace(persistentId) ? 0 : LocHash.Compute(persistentId);
                RegisterHash(result, unchecked((uint)hashId), $"ItemData:{persistentId}", assetPath);

                ValidateItemAudioMaterial(result, item, assetPath);

                if (item.worldPrefab == null)
                    continue;

                string prefabPath = AssetDatabase.GetAssetPath(item.worldPrefab);
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    result.Errors.Add($"{assetPath}: ItemData.worldPrefab has no valid asset path.");
                    continue;
                }

                result.ReferencedPrefabCount++;
                ValidatePrefabAsset(
                    prefabPath,
                    $"ItemData worldPrefab <- {assetPath}",
                    result,
                    wireMesh,
                    wireMaterial,
                    allowMeshCollider: false);
            }
        }

        private static void ValidateItemCatalog(ValidationResult result)
        {
            ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            if (itemCatalog == null)
            {
                result.Errors.Add($"{ItemCatalogPath}: ItemCatalog asset is missing; catalog validation cannot run.");
                return;
            }

            SerializedObject serializedCatalog = new SerializedObject(itemCatalog);
            SerializedProperty allItemsProperty = serializedCatalog.FindProperty("allItems");
            if (allItemsProperty == null || !allItemsProperty.isArray)
            {
                result.Errors.Add($"{ItemCatalogPath}: ItemCatalog.allItems is missing or not serialized as an array.");
                return;
            }

            Dictionary<int, string> catalogHashOwners = new Dictionary<int, string>(allItemsProperty.arraySize);
            for (int i = 0; i < allItemsProperty.arraySize; i++)
            {
                SerializedProperty itemProperty = allItemsProperty.GetArrayElementAtIndex(i);
                if (itemProperty == null || itemProperty.objectReferenceValue == null)
                {
                    result.ItemCatalogNullEntryCount++;
                    result.Errors.Add($"{ItemCatalogPath}: allItems[{i}] is null.");
                    continue;
                }

                if (!(itemProperty.objectReferenceValue is ItemData item))
                {
                    result.Errors.Add($"{ItemCatalogPath}: allItems[{i}] is not ItemData.");
                    continue;
                }

                string itemPath = AssetDatabase.GetAssetPath(item);
                if (string.IsNullOrWhiteSpace(itemPath))
                    result.Errors.Add($"{ItemCatalogPath}: allItems[{i}] '{item.name}' has no valid asset path.");

                string persistentId = item.PersistentId;
                if (string.IsNullOrWhiteSpace(persistentId))
                {
                    result.Errors.Add($"{ItemCatalogPath}: allItems[{i}] '{item.name}' has empty PersistentId.");
                    continue;
                }

                int hashId = LocHash.Compute(persistentId);
                if (hashId == 0)
                {
                    result.Errors.Add($"{ItemCatalogPath}: allItems[{i}] '{item.name}' PersistentId '{persistentId}' resolves to hash 0.");
                    continue;
                }

                if (catalogHashOwners.TryGetValue(hashId, out string existingPath))
                {
                    result.ItemCatalogDuplicateHashCount++;
                    result.Errors.Add(
                        $"{ItemCatalogPath}: duplicate ItemCatalog hash 0x{hashId:X8} / PersistentId '{persistentId}' between '{existingPath}' and '{itemPath}'.");
                }
                else
                {
                    catalogHashOwners.Add(hashId, string.IsNullOrWhiteSpace(itemPath) ? item.name : itemPath);
                }

                if (!itemCatalog.TryGetRuntimeDescriptor(hashId, out ItemCatalog.ItemRuntimeDescriptor descriptor) || !ItemCatalog.IsValidDescriptor(in descriptor))
                {
                    result.ItemCatalogMissingRuntimeDescriptorCount++;
                    result.Errors.Add($"{ItemCatalogPath}: allItems[{i}] '{item.name}' has no valid runtime descriptor for hash 0x{hashId:X8}.");
                }
            }

            if (itemCatalog.HasLookupAmbiguity)
            {
                result.ItemCatalogLookupAmbiguityCount++;
                result.Errors.Add($"{ItemCatalogPath}: ItemCatalog lookup ambiguity: {itemCatalog.LookupAmbiguitySummary}");
            }
        }

        private static HashSet<string> ValidateRecipeData(ValidationResult result)
        {
            HashSet<string> craftableItemIds = new HashSet<string>(128, StringComparer.Ordinal);
            HashSet<string> knownScanEntryIds = CollectKnownScanEntryIds(result);
            ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            string[] recipeGuids = AssetDatabase.FindAssets("t:RecipeData", DataRoots);
            Dictionary<uint, string> recipeHashOwners = new Dictionary<uint, string>(Math.Max(recipeGuids.Length, 1));

            for (int i = 0; i < recipeGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(recipeGuids[i]);
                RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(assetPath);
                if (recipe == null)
                    continue;

                result.RecipeCount++;
                ValidateRecipeIdentity(result, recipeHashOwners, recipe, assetPath);
                ValidateRecipeResult(result, itemCatalog, recipe, assetPath, craftableItemIds);
                ValidateRecipeIngredients(result, itemCatalog, recipe, assetPath);
                ValidateRecipeScanGate(result, recipe, assetPath, knownScanEntryIds);
            }

            ValidateFirstHourCraftGate(result, itemCatalog, craftableItemIds);
            ValidateFirstHourOxygenRoute(result, itemCatalog, craftableItemIds);

            return craftableItemIds;
        }

        private static void ValidateFirstHourOxygenRoute(
            ValidationResult result,
            ItemCatalog itemCatalog,
            HashSet<string> craftableItemIds)
        {
            ItemData oxygenItem = FindItemDataByPersistentId(EmergencyO2CanisterItemId);
            if (oxygenItem == null)
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add($"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' ItemData is missing.");
                return;
            }

            if (!oxygenItem.isConsumable)
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add($"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' must stay consumable; early oxygen failsafe cannot be a dead inventory item.");
            }

            if (oxygenItem.oxygenRestore <= 0f)
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add($"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' must restore oxygen; authored oxygenRestore={oxygenItem.oxygenRestore}.");
            }

            if (!oxygenItem.stackable || oxygenItem.maxStack < 2)
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add($"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' must remain stackable with maxStack >= 2 for first-hour route safety.");
            }

            if (craftableItemIds == null || !craftableItemIds.Contains(EmergencyO2CanisterItemId))
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add($"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' is not produced by any valid RecipeData.resultItem.");
            }

            int itemHash = LocHash.Compute(EmergencyO2CanisterItemId);
            if (itemHash == 0)
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add($"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' hashes to 0.");
                return;
            }

            if (itemCatalog == null ||
                !itemCatalog.TryGetRuntimeDescriptor(itemHash, out ItemCatalog.ItemRuntimeDescriptor descriptor) ||
                !ItemCatalog.IsValidDescriptor(in descriptor))
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add($"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' has no valid ItemCatalog runtime descriptor.");
                return;
            }

            if (descriptor.IsConsumable == 0 || descriptor.OxygenRestore <= 0f)
            {
                result.FirstHourOxygenRouteErrorCount++;
                result.Errors.Add(
                    $"FirstHourOxygenRoute: '{EmergencyO2CanisterItemId}' catalog descriptor must be consumable and restore oxygen; " +
                    $"IsConsumable={descriptor.IsConsumable}, OxygenRestore={descriptor.OxygenRestore}.");
            }
        }

        private static void ValidateFirstHourCraftGate(
            ValidationResult result,
            ItemCatalog itemCatalog,
            HashSet<string> craftableItemIds)
        {
            for (int i = 0; i < FirstHourCraftMilestoneItemIds.Length; i++)
            {
                string itemId = FirstHourCraftMilestoneItemIds[i];
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    result.FirstHourCraftGateErrorCount++;
                    result.Errors.Add($"FirstHourCraftGate[{i}]: item id is empty.");
                    continue;
                }

                int itemHash = LocHash.Compute(itemId);
                if (itemHash == 0)
                {
                    result.FirstHourCraftGateErrorCount++;
                    result.Errors.Add($"FirstHourCraftGate[{i}] '{itemId}' hashes to 0.");
                    continue;
                }

                if (itemCatalog == null ||
                    !itemCatalog.TryGetRuntimeDescriptor(itemHash, out ItemCatalog.ItemRuntimeDescriptor descriptor) ||
                    !ItemCatalog.IsValidDescriptor(in descriptor))
                {
                    result.FirstHourCraftGateErrorCount++;
                    result.Errors.Add($"FirstHourCraftGate[{i}] '{itemId}' has no valid ItemCatalog runtime descriptor.");
                }

                if (craftableItemIds == null || !craftableItemIds.Contains(itemId))
                {
                    result.FirstHourCraftGateErrorCount++;
                    result.Errors.Add($"FirstHourCraftGate[{i}] '{itemId}' is not produced by any RecipeData.resultItem.");
                }
            }
        }

        private static HashSet<string> CollectKnownScanEntryIds(ValidationResult result)
        {
            HashSet<string> knownScanEntryIds = new HashSet<string>(64, StringComparer.Ordinal)
            {
                "scan.resource_node"
            };

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                    continue;

                try
                {
                    ScannableTarget[] targets = prefabRoot.GetComponentsInChildren<ScannableTarget>(true);
                    if (targets == null)
                        continue;

                    for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                    {
                        ScannableTarget target = targets[targetIndex];
                        if (target == null)
                            continue;

                        string entryId = target.EntryId ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(entryId))
                        {
                            result.RecipeScanGateWarningCount++;
                            result.Warnings.Add($"{prefabPath}: ScannableTarget at '{BuildTransformPath(target.transform)}' has no stable EntryId for recipe scan-gate validation.");
                            continue;
                        }

                        knownScanEntryIds.Add(entryId);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return knownScanEntryIds;
        }

        private static void ValidateRecipeIdentity(
            ValidationResult result,
            Dictionary<uint, string> recipeHashOwners,
            RecipeData recipe,
            string assetPath)
        {
            string recipeObjectName = recipe.name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeObjectName))
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData asset name is empty; CraftingEvents recipe hash cannot be stable.");
                return;
            }

            int recipeHash = LocHash.Compute(recipeObjectName);
            if (recipeHash == 0)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData asset name '{recipeObjectName}' hashes to 0.");
                return;
            }

            uint recipeHashKey = unchecked((uint)recipeHash);
            if (recipeHashOwners.TryGetValue(recipeHashKey, out string existingPath))
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: duplicate RecipeData runtime hash 0x{recipeHashKey:X8} already authored by '{existingPath}'.");
                return;
            }

            recipeHashOwners.Add(recipeHashKey, assetPath);
        }

        private static void ValidateRecipeResult(
            ValidationResult result,
            ItemCatalog itemCatalog,
            RecipeData recipe,
            string assetPath,
            HashSet<string> craftableItemIds)
        {
            if (recipe.resultItem == null)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.resultItem is null.");
                return;
            }

            if (recipe.resultQuantity <= 0)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.resultQuantity must be positive.");
            }

            if (recipe.fabricationGroup == FabricationGroup.Unspecified)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.fabricationGroup is Unspecified; fabrication UI/category route is not authored.");
            }

            if (ValidateRecipeItemReference(result, itemCatalog, assetPath, "resultItem", recipe.resultItem))
                craftableItemIds.Add(recipe.resultItem.PersistentId);
        }

        private static void ValidateRecipeIngredients(
            ValidationResult result,
            ItemCatalog itemCatalog,
            RecipeData recipe,
            string assetPath)
        {
            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.ingredients is empty; Fabricator.CanCraft will reject the recipe.");
                return;
            }

            for (int ingredientIndex = 0; ingredientIndex < recipe.ingredients.Count; ingredientIndex++)
            {
                InventoryCost cost = recipe.ingredients[ingredientIndex];
                string label = $"ingredients[{ingredientIndex}]";
                if (cost == null)
                {
                    result.RecipeRouteErrorCount++;
                    result.Errors.Add($"{assetPath}: RecipeData.{label} is null.");
                    continue;
                }

                if (cost.amount <= 0)
                {
                    result.RecipeRouteErrorCount++;
                    result.Errors.Add($"{assetPath}: RecipeData.{label}.amount must be positive.");
                }

                ValidateRecipeItemReference(result, itemCatalog, assetPath, $"{label}.item", cost.item);
            }
        }

        private static void ValidateRecipeScanGate(
            ValidationResult result,
            RecipeData recipe,
            string assetPath,
            HashSet<string> knownScanEntryIds)
        {
            string requiredScanEntryId = recipe.RequiredScanEntryId;
            if (string.IsNullOrWhiteSpace(requiredScanEntryId))
                return;

            if (ScanEvents.ComputeEntryHash(requiredScanEntryId) == 0u)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.requiredScanEntryId '{requiredScanEntryId}' hashes to 0.");
                return;
            }

            if (knownScanEntryIds != null && knownScanEntryIds.Contains(requiredScanEntryId))
                return;

            result.RecipeScanGateWarningCount++;
            result.Warnings.Add(
                $"{assetPath}: RecipeData.requiredScanEntryId '{requiredScanEntryId}' has no known generic scan route and no authored ScannableTarget prefab route under {PrefabRoot}. " +
                "If this is generated by an editor bootstrap scene, runtime unlock proof is still required.");
        }

        private static bool ValidateRecipeItemReference(
            ValidationResult result,
            ItemCatalog itemCatalog,
            string assetPath,
            string label,
            ItemData item)
        {
            if (item == null)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.{label} is null.");
                return false;
            }

            string itemPath = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.{label} '{item.name}' has no valid asset path.");
            }

            string persistentId = item.PersistentId ?? string.Empty;
            int hashId = string.IsNullOrWhiteSpace(persistentId) ? 0 : LocHash.Compute(persistentId);
            if (hashId == 0)
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.{label} '{item.name}' has invalid PersistentId '{persistentId}'.");
                return false;
            }

            if (itemCatalog == null ||
                !itemCatalog.TryGetRuntimeDescriptor(hashId, out ItemCatalog.ItemRuntimeDescriptor descriptor) ||
                !ItemCatalog.IsValidDescriptor(in descriptor))
            {
                result.RecipeRouteErrorCount++;
                result.Errors.Add($"{assetPath}: RecipeData.{label} '{persistentId}' has no valid ItemCatalog runtime descriptor.");
                return false;
            }

            return true;
        }

        private static void ValidateQuestData(ValidationResult result, HashSet<string> craftableItemIds)
        {
            ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            string[] questGuids = AssetDatabase.FindAssets("t:QuestData", DataRoots);
            Dictionary<string, string> questIdOwners = new Dictionary<string, string>(Math.Max(questGuids.Length, 1), StringComparer.Ordinal);

            for (int i = 0; i < questGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(questGuids[i]);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(assetPath);
                if (quest == null)
                    continue;

                result.QuestCount++;
                string questId = quest.questId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(questId))
                {
                    result.QuestRouteErrorCount++;
                    result.Errors.Add($"{assetPath}: QuestData.questId is empty.");
                    continue;
                }

                if (questIdOwners.TryGetValue(questId, out string existingPath))
                {
                    result.QuestRouteErrorCount++;
                    result.Errors.Add($"{assetPath}: duplicate QuestData.questId '{questId}' already authored by '{existingPath}'.");
                    continue;
                }

                questIdOwners.Add(questId, assetPath);
            }

            for (int i = 0; i < questGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(questGuids[i]);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(assetPath);
                if (quest == null)
                    continue;

                ValidateQuestPrerequisites(result, quest, assetPath, questIdOwners);
                ValidateQuestSignalItemId(result, itemCatalog, assetPath, "triggerId", quest.triggerType, quest.triggerId);
                ValidateQuestSignalItemId(result, itemCatalog, assetPath, "completionId", quest.completionType, quest.completionId);
                ValidateQuestCraftRecipeRoute(result, craftableItemIds, assetPath, "triggerId", quest.triggerType, quest.triggerId);
                ValidateQuestCraftRecipeRoute(result, craftableItemIds, assetPath, "completionId", quest.completionType, quest.completionId);

                if (!string.IsNullOrWhiteSpace(quest.criticalItemId))
                    ValidateQuestItemId(result, itemCatalog, assetPath, "criticalItemId", quest.criticalItemId);
            }
        }

        private static void ValidateQuestPrerequisites(
            ValidationResult result,
            QuestData quest,
            string assetPath,
            Dictionary<string, string> questIdOwners)
        {
            if (quest.prerequisiteQuestIds == null)
                return;

            for (int i = 0; i < quest.prerequisiteQuestIds.Length; i++)
            {
                string prerequisiteId = quest.prerequisiteQuestIds[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(prerequisiteId))
                    continue;

                if (questIdOwners.ContainsKey(prerequisiteId))
                    continue;

                result.QuestRouteErrorCount++;
                result.Errors.Add($"{assetPath}: prerequisiteQuestIds[{i}] references missing questId '{prerequisiteId}'.");
            }
        }

        private static void ValidateQuestSignalItemId(
            ValidationResult result,
            ItemCatalog itemCatalog,
            string assetPath,
            string propertyName,
            QuestTriggerType triggerType,
            string signalId)
        {
            if (triggerType != QuestTriggerType.OnItemCollected &&
                triggerType != QuestTriggerType.OnCraftCompleted)
            {
                return;
            }

            ValidateQuestItemId(result, itemCatalog, assetPath, propertyName, signalId);
        }

        private static void ValidateQuestSignalItemId(
            ValidationResult result,
            ItemCatalog itemCatalog,
            string assetPath,
            string propertyName,
            QuestCompletionType completionType,
            string signalId)
        {
            if (completionType != QuestCompletionType.OnItemCollected &&
                completionType != QuestCompletionType.OnCraftCompleted)
            {
                return;
            }

            ValidateQuestItemId(result, itemCatalog, assetPath, propertyName, signalId);
        }

        private static void ValidateQuestCraftRecipeRoute(
            ValidationResult result,
            HashSet<string> craftableItemIds,
            string assetPath,
            string propertyName,
            QuestTriggerType triggerType,
            string signalId)
        {
            if (triggerType != QuestTriggerType.OnCraftCompleted)
                return;

            ValidateQuestCraftRecipeRoute(result, craftableItemIds, assetPath, propertyName, signalId);
        }

        private static void ValidateQuestCraftRecipeRoute(
            ValidationResult result,
            HashSet<string> craftableItemIds,
            string assetPath,
            string propertyName,
            QuestCompletionType completionType,
            string signalId)
        {
            if (completionType != QuestCompletionType.OnCraftCompleted)
                return;

            ValidateQuestCraftRecipeRoute(result, craftableItemIds, assetPath, propertyName, signalId);
        }

        private static void ValidateQuestCraftRecipeRoute(
            ValidationResult result,
            HashSet<string> craftableItemIds,
            string assetPath,
            string propertyName,
            string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            if (craftableItemIds != null && craftableItemIds.Contains(itemId))
                return;

            result.QuestRouteErrorCount++;
            result.Errors.Add($"{assetPath}: QuestData.{propertyName} '{itemId}' uses OnCraftCompleted but no valid RecipeData.resultItem route crafts that item.");
        }

        private static void ValidateQuestItemId(
            ValidationResult result,
            ItemCatalog itemCatalog,
            string assetPath,
            string propertyName,
            string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                result.QuestRouteErrorCount++;
                result.Errors.Add($"{assetPath}: QuestData.{propertyName} must reference a catalog item PersistentId.");
                return;
            }

            int hashId = LocHash.Compute(itemId);
            if (hashId == 0 ||
                itemCatalog == null ||
                !itemCatalog.TryGetRuntimeDescriptor(hashId, out ItemCatalog.ItemRuntimeDescriptor descriptor) ||
                !ItemCatalog.IsValidDescriptor(in descriptor))
            {
                result.QuestRouteErrorCount++;
                result.Errors.Add($"{assetPath}: QuestData.{propertyName} '{itemId}' has no valid ItemCatalog runtime descriptor.");
            }
        }

        private static void ValidateToolAuthoring(ValidationResult result)
        {
            ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            Dictionary<ToolMetadata, string> heldPrefabByMetadata = new Dictionary<ToolMetadata, string>(64);
            Dictionary<string, string> toolIdOwners = new Dictionary<string, string>(64, StringComparer.Ordinal);

            string[] heldPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ToolHeldPrefabRoot });
            for (int i = 0; i < heldPrefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(heldPrefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    result.ToolRouteErrorCount++;
                    result.Errors.Add($"{prefabPath}: failed to load held tool prefab for tool route validation.");
                    continue;
                }

                try
                {
                    PlayerTool[] tools = prefabRoot.GetComponentsInChildren<PlayerTool>(true);
                    if (tools == null || tools.Length == 0)
                    {
                        result.ToolRouteErrorCount++;
                        result.Errors.Add($"{prefabPath}: held tool prefab has no PlayerTool component.");
                        continue;
                    }

                    result.ToolHeldPrefabCount++;
                    for (int toolIndex = 0; toolIndex < tools.Length; toolIndex++)
                    {
                        PlayerTool tool = tools[toolIndex];
                        if (tool == null)
                            continue;

                        string transformPath = BuildTransformPath(tool.transform);
                        ToolMetadata metadata = tool.Metadata;
                        ItemData item = tool.ToolData;

                        if (metadata == null)
                        {
                            result.ToolRouteErrorCount++;
                            result.Errors.Add($"{prefabPath}: PlayerTool at '{transformPath}' has no ToolMetadata.");
                        }
                        else if (!heldPrefabByMetadata.ContainsKey(metadata))
                        {
                            heldPrefabByMetadata.Add(metadata, $"{prefabPath}:{transformPath}");
                        }

                        ValidateHeldToolItemRoute(result, itemCatalog, item, prefabPath, transformPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            string[] metadataGuids = AssetDatabase.FindAssets("t:ToolMetadata", DataRoots);
            for (int i = 0; i < metadataGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(metadataGuids[i]);
                ToolMetadata metadata = AssetDatabase.LoadAssetAtPath<ToolMetadata>(assetPath);
                if (metadata == null)
                    continue;

                result.ToolMetadataCount++;
                string toolId = metadata.toolID ?? string.Empty;
                if (string.IsNullOrWhiteSpace(toolId))
                {
                    result.ToolRouteErrorCount++;
                    result.Errors.Add($"{assetPath}: ToolMetadata.toolID is empty.");
                }
                else if (toolIdOwners.TryGetValue(toolId, out string existingPath))
                {
                    result.ToolRouteErrorCount++;
                    result.Errors.Add($"{assetPath}: duplicate ToolMetadata.toolID '{toolId}' already authored by '{existingPath}'.");
                }
                else
                {
                    toolIdOwners.Add(toolId, assetPath);
                }

                if (!heldPrefabByMetadata.ContainsKey(metadata))
                {
                    result.ToolMetadataOrphanCount++;
                    result.Errors.Add(
                        $"{assetPath}: ToolMetadata '{toolId}' has no held PlayerTool prefab route under {ToolHeldPrefabRoot}. " +
                        "Active tool metadata without a held prefab, ItemData, catalog descriptor, and world prefab is orphan gameplay content.");
                }
            }
        }

        private static void ValidateHeldToolItemRoute(
            ValidationResult result,
            ItemCatalog itemCatalog,
            ItemData item,
            string prefabPath,
            string transformPath)
        {
            string context = $"{prefabPath}:{transformPath}";
            if (item == null)
            {
                result.ToolRouteErrorCount++;
                result.Errors.Add($"{context}: PlayerTool has no ItemData.");
                return;
            }

            string itemPath = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                result.ToolRouteErrorCount++;
                result.Errors.Add($"{context}: PlayerTool ItemData '{item.name}' has no valid asset path.");
            }

            if (item.category != ItemCategory.Tool)
            {
                result.ToolRouteErrorCount++;
                result.Errors.Add($"{context}: PlayerTool ItemData '{item.name}' category is {item.category}, expected Tool.");
            }

            string persistentId = item.PersistentId ?? string.Empty;
            int hashId = string.IsNullOrWhiteSpace(persistentId) ? 0 : LocHash.Compute(persistentId);
            if (hashId == 0)
            {
                result.ToolRouteErrorCount++;
                result.Errors.Add($"{context}: PlayerTool ItemData '{item.name}' has invalid PersistentId '{persistentId}'.");
            }
            else if (itemCatalog == null ||
                     !itemCatalog.TryGetRuntimeDescriptor(hashId, out ItemCatalog.ItemRuntimeDescriptor descriptor) ||
                     !ItemCatalog.IsValidDescriptor(in descriptor))
            {
                result.ToolRouteErrorCount++;
                result.Errors.Add($"{context}: PlayerTool ItemData '{item.name}' is missing a valid ItemCatalog runtime descriptor.");
            }

            if (item.worldPrefab == null)
            {
                result.ToolRouteErrorCount++;
                result.Errors.Add($"{context}: PlayerTool ItemData '{item.name}' has no worldPrefab for pickup/drop acquisition.");
            }
        }

        private static void ValidateFloraTemplates(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] floraGuids = AssetDatabase.FindAssets("t:FloraDataTemplate", DataRoots);
            for (int i = 0; i < floraGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(floraGuids[i]);
                FloraDataTemplate template = AssetDatabase.LoadAssetAtPath<FloraDataTemplate>(assetPath);
                if (template == null)
                    continue;

                result.FloraCount++;
                int hashId = string.IsNullOrWhiteSpace(template.StableId) ? 0 : LocHash.Compute(template.StableId);
                RegisterHash(result, unchecked((uint)hashId), $"FloraDataTemplate:{template.StableId}", assetPath);

                if (template.AudioMaterialID == (byte)FloraDataTemplate.AudioMaterialId.None)
                {
                    result.AudioMaterialViolationCount++;
                    result.Errors.Add($"{assetPath}: FloraDataTemplate.AudioMaterialID is None (0).");
                }

                if (template.Mesh == null)
                {
                    if (template.ProxyPrefab == null)
                    {
                        GameObject generatedProxy = CreateOrUpdateFloraGhostProxy(template, wireMesh, wireMaterial, assetPath);
                        if (generatedProxy != null)
                        {
                            SerializedObject serializedTemplate = new SerializedObject(template);
                            SerializedProperty proxyPrefabProperty = serializedTemplate.FindProperty("proxyPrefab");
                            if (proxyPrefabProperty != null && proxyPrefabProperty.objectReferenceValue != generatedProxy)
                            {
                                proxyPrefabProperty.objectReferenceValue = generatedProxy;
                                serializedTemplate.ApplyModifiedPropertiesWithoutUndo();
                                EditorUtility.SetDirty(template);
                                result.GeneratedFloraProxyCount++;
                                result.AutoFixes.Add($"{assetPath}: assigned generated flora ghost proxy '{AssetDatabase.GetAssetPath(generatedProxy)}'.");
                            }
                        }
                        else
                        {
                            result.Errors.Add($"{assetPath}: missing Mesh and failed to generate flora ghost proxy.");
                        }
                    }

                    if (template.ProxyPrefab != null)
                    {
                        string proxyPath = AssetDatabase.GetAssetPath(template.ProxyPrefab);
                        if (string.IsNullOrWhiteSpace(proxyPath))
                        {
                            result.Errors.Add($"{assetPath}: FloraDataTemplate.proxyPrefab has no valid asset path.");
                        }
                        else
                        {
                            result.ReferencedPrefabCount++;
                            ValidatePrefabAsset(
                                proxyPath,
                                $"FloraDataTemplate proxyPrefab <- {assetPath}",
                                result,
                                wireMesh,
                                wireMaterial,
                                allowMeshCollider: false);
                        }
                    }
                }
                else if (template.ProxyPrefab != null)
                {
                    string proxyPath = AssetDatabase.GetAssetPath(template.ProxyPrefab);
                    if (!string.IsNullOrWhiteSpace(proxyPath))
                    {
                        result.ReferencedPrefabCount++;
                        ValidatePrefabAsset(
                            proxyPath,
                            $"FloraDataTemplate proxyPrefab <- {assetPath}",
                            result,
                            wireMesh,
                            wireMaterial,
                            allowMeshCollider: false);
                    }
                }
            }
        }

        private static void ValidateFaunaTemplates(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] faunaTemplateGuids = AssetDatabase.FindAssets("t:FaunaDataTemplate", DataRoots);
            for (int i = 0; i < faunaTemplateGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(faunaTemplateGuids[i]);
                FaunaDataTemplate template = AssetDatabase.LoadAssetAtPath<FaunaDataTemplate>(assetPath);
                if (template == null)
                    continue;

                result.FaunaCount++;
                RegisterHash(result, unchecked((uint)template.SpeciesId), $"FaunaDataTemplate:{template.SpeciesId}", assetPath);

                if (template.SpeciesId <= 0)
                    result.Errors.Add($"{assetPath}: FaunaDataTemplate.SpeciesId is not authored.");
            }

            string[] archetypeGuids = AssetDatabase.FindAssets("t:CreatureArchetypeData", DataRoots);
            for (int i = 0; i < archetypeGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(archetypeGuids[i]);
                CreatureArchetypeData archetype = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(assetPath);
                if (archetype == null)
                    continue;

                if (archetype.prefab == null)
                {
                    result.Warnings.Add($"{assetPath}: CreatureArchetypeData.prefab is unassigned.");
                    continue;
                }

                string prefabPath = AssetDatabase.GetAssetPath(archetype.prefab);
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    result.Errors.Add($"{assetPath}: CreatureArchetypeData.prefab has no valid asset path.");
                    continue;
                }

                result.ReferencedPrefabCount++;
                ValidatePrefabAsset(
                    prefabPath,
                    $"CreatureArchetypeData prefab <- {assetPath}",
                    result,
                    wireMesh,
                    wireMaterial,
                    allowMeshCollider: false);
            }
        }

        private static void ValidateResourceNodeTemplates(ValidationResult result)
        {
            ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            if (itemCatalog == null)
                result.Errors.Add($"{ItemCatalogPath}: ItemCatalog asset is missing; resource-node yield catalog validation cannot run.");

            string[] resourceGuids = AssetDatabase.FindAssets("t:ResourceNodeTemplate", DataRoots);
            for (int i = 0; i < resourceGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(resourceGuids[i]);
                ResourceNodeTemplate template = AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(assetPath);
                if (template == null)
                    continue;

                result.ResourceNodeCount++;
                if (template.StableHashId == 0)
                    result.Errors.Add($"{assetPath}: ResourceNodeTemplate.StableHashId resolves to 0.");

                ValidateFirstHourResourceNodeToolGate(result, template, assetPath);

                if (template.NodeMesh == null)
                    result.Warnings.Add($"{assetPath}: nodeMesh is null. Runtime ghost-box standard remains active.");

                ValidateResourceNodeYieldArray(result, itemCatalog, template, assetPath, "harvestYield", "harvestYield");
                ValidateResourceNodeYieldArray(result, itemCatalog, template, assetPath, "rarityDrops", "rarityDrops");
            }
        }

        private static void ValidateFirstHourResourceNodeToolGate(
            ValidationResult result,
            ResourceNodeTemplate template,
            string assetPath)
        {
            if (!string.Equals(assetPath, CopperVeinTemplatePath, StringComparison.OrdinalIgnoreCase))
                return;

            if (template.RequiredToolClass == ResourceNodeTemplate.HarvestToolClass.Drill)
                return;

            result.ResourceNodeToolGateErrorCount++;
            result.Errors.Add(
                $"{assetPath}: first-hour copper vein must be Drill-gated. " +
                $"Current RequiredToolClass={template.RequiredToolClass}; Knife/Salvage/Any would cheapen early tool progression.");
        }

        private static void ValidateFirstHourDrillRoute(ValidationResult result)
        {
            ResourceNodeTemplate copperTemplate = AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(CopperVeinTemplatePath);
            if (copperTemplate == null || copperTemplate.RequiredToolClass != ResourceNodeTemplate.HarvestToolClass.Drill)
                return;

            ItemData drillItem = FindItemDataByPersistentId(SeafloorDrillItemId);
            bool hasHeldPrefab = HasHeldToolPrefabForItemId(SeafloorDrillItemId);
            if (drillItem != null && hasHeldPrefab)
                return;

            result.FirstHourDrillRouteErrorCount++;
            string missing = drillItem == null && !hasHeldPrefab
                ? "ItemData and held prefab"
                : drillItem == null
                    ? "ItemData"
                    : "held prefab";
            result.Errors.Add(
                $"{CopperVeinTemplatePath}: copper is Drill-gated, but first-hour seafloor drill route is incomplete; " +
                $"missing {missing} for PersistentId='{SeafloorDrillItemId}'. Do not fall back to Knife/Any; author the tool route or an explicit validated alternative.");
        }

        private static void ValidateResourceNodeYieldArray(
            ValidationResult result,
            ItemCatalog itemCatalog,
            ResourceNodeTemplate template,
            string assetPath,
            string propertyName,
            string label)
        {
            SerializedObject serializedTemplate = new SerializedObject(template);
            SerializedProperty tableProperty = serializedTemplate.FindProperty(propertyName);
            if (tableProperty == null || !tableProperty.isArray)
                return;

            if (propertyName == "harvestYield" && tableProperty.arraySize <= 0)
            {
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.harvestYield is empty; node can deplete without an authored primary pickup.");
                return;
            }

            for (int i = 0; i < tableProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = tableProperty.GetArrayElementAtIndex(i);
                SerializedProperty itemProperty = elementProperty != null ? elementProperty.FindPropertyRelative("item") : null;
                ValidateResourceNodeYieldItem(result, itemCatalog, assetPath, itemProperty, $"{label}[{i}]");
            }
        }

        private static void ValidateResourceNodeYieldItem(
            ValidationResult result,
            ItemCatalog itemCatalog,
            string assetPath,
            SerializedProperty itemProperty,
            string label)
        {
            if (itemProperty == null || itemProperty.objectReferenceValue == null)
            {
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item is null.");
                return;
            }

            if (!(itemProperty.objectReferenceValue is ItemData item))
            {
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item is not ItemData.");
                return;
            }

            string itemPath = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrWhiteSpace(itemPath))
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item has no valid asset path.");

            int itemHash = !string.IsNullOrWhiteSpace(item.PersistentId) ? LocHash.Compute(item.PersistentId) : 0;
            if (itemHash == 0)
            {
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' has empty PersistentId.");
            }
            else if (itemCatalog == null || !ReferenceEquals(itemCatalog.FindByHash(itemHash), item))
            {
                result.ResourceNodeYieldNotCatalogedCount++;
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' is not the active ItemCatalog entry for hash 0x{itemHash:X8}.");
            }

            if (item.worldPrefab == null)
            {
                result.ResourceNodeYieldMissingWorldPrefabCount++;
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' has null ItemData.worldPrefab; PersistentWorldRegistry drops will reject it.");
                return;
            }

            string worldPrefabPath = AssetDatabase.GetAssetPath(item.worldPrefab);
            if (string.IsNullOrWhiteSpace(worldPrefabPath))
            {
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' worldPrefab has no valid asset path.");
                return;
            }

            ValidateResourceYieldWorldPrefabContract(result, assetPath, label, item, worldPrefabPath);
        }

        private static void ValidateResourceYieldWorldPrefabContract(
            ValidationResult result,
            string assetPath,
            string label,
            ItemData item,
            string worldPrefabPath)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(worldPrefabPath);
            if (prefabRoot == null)
            {
                result.ResourceNodeYieldInvalidWorldPrefabContractCount++;
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' failed to load worldPrefab contents -> {worldPrefabPath}.");
                return;
            }

            try
            {
                bool hasPickupContract =
                    prefabRoot.GetComponentInChildren<PickupItem>(true) != null ||
                    prefabRoot.GetComponentInChildren<HectonItem>(true) != null;

                if (!hasPickupContract)
                {
                    result.ResourceNodeYieldInvalidWorldPrefabContractCount++;
                    result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' worldPrefab has neither PickupItem nor HectonItem -> {worldPrefabPath}.");
                }

                if (prefabRoot.GetComponentInChildren<Collider>(true) == null)
                {
                    result.ResourceNodeYieldInvalidWorldPrefabContractCount++;
                    result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' worldPrefab has no Collider -> {worldPrefabPath}.");
                }

                if (prefabRoot.GetComponentInChildren<Rigidbody>(true) == null)
                {
                    result.ResourceNodeYieldInvalidWorldPrefabContractCount++;
                    result.Errors.Add($"{assetPath}: ResourceNodeTemplate.{label}.item '{item.name}' worldPrefab has no Rigidbody -> {worldPrefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidatePlayerPdaShell(ValidationResult result)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                result.PlayerPdaHeadlessOpenRiskCount++;
                result.Errors.Add($"{PlayerPrefabPath}: failed to load prefab contents for PDA shell validation.");
                return;
            }

            try
            {
                PlayerPDA playerPda = prefabRoot.GetComponentInChildren<PlayerPDA>(true);
                if (playerPda == null)
                    return;

                DiegeticPDAController diegeticPda = prefabRoot.GetComponentInChildren<DiegeticPDAController>(true);
                if (playerPda.PanelRoot == null && diegeticPda == null)
                {
                    result.PlayerPdaHeadlessOpenRiskCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: PlayerPDA has no serialized panel and no DiegeticPDAController bridge; opening PDA can become a headless input lock.");
                    return;
                }

                if (diegeticPda == null)
                    return;

                SerializedObject serializedBridge = new SerializedObject(diegeticPda);
                WarnIfMissingObjectReference(result, serializedBridge, "diegeticPanelRoot", $"{PlayerPrefabPath}: DiegeticPDAController.diegeticPanelRoot is not serialized; runtime auto-resolve must prove the shell.");
                WarnIfMissingObjectReference(result, serializedBridge, "diegeticPanelCanvasGroup", $"{PlayerPrefabPath}: DiegeticPDAController.diegeticPanelCanvasGroup is not serialized; runtime auto-resolve must prove fade/input gating.");
                WarnIfMissingObjectReference(result, serializedBridge, "tabletRoot", $"{PlayerPrefabPath}: DiegeticPDAController.tabletRoot is not serialized; PDA may have UI backend without a physical tablet presentation.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void WarnIfMissingObjectReference(
            ValidationResult result,
            SerializedObject serializedObject,
            string propertyName,
            string message)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null)
                return;

            result.PlayerPdaBridgeWarningCount++;
            result.Warnings.Add(message);
        }

        private static void ValidatePlayerDevProvisioning(ValidationResult result)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                result.PlayerDevProvisionerStartupRiskCount++;
                result.Errors.Add($"{PlayerPrefabPath}: failed to load prefab contents for dev provisioning validation.");
                return;
            }

            try
            {
                ToolLoadoutProvisioner[] provisioners = prefabRoot.GetComponentsInChildren<ToolLoadoutProvisioner>(true);
                if (provisioners == null || provisioners.Length == 0)
                    return;

                if (provisioners.Length > 1)
                    result.Warnings.Add($"{PlayerPrefabPath}: contains {provisioners.Length} ToolLoadoutProvisioner components; canonical player should not need multiple dev provisioners.");

                for (int i = 0; i < provisioners.Length; i++)
                {
                    ToolLoadoutProvisioner provisioner = provisioners[i];
                    if (provisioner == null)
                        continue;

                    SerializedObject serializedProvisioner = new SerializedObject(provisioner);
                    string context = $"{PlayerPrefabPath}:{BuildTransformPath(provisioner.transform)}";
                    ErrorIfSerializedBoolTrue(
                        result,
                        serializedProvisioner,
                        "provisionInventoryOnStart",
                        $"{context}: ToolLoadoutProvisioner.provisionInventoryOnStart must stay disabled on the canonical player prefab.");
                    ErrorIfSerializedBoolTrue(
                        result,
                        serializedProvisioner,
                        "assignCoreLoadoutOnStart",
                        $"{context}: ToolLoadoutProvisioner.assignCoreLoadoutOnStart must stay disabled on the canonical player prefab.");
                    ErrorIfSerializedBoolTrue(
                        result,
                        serializedProvisioner,
                        "provisionConstructionMaterialsOnStart",
                        $"{context}: ToolLoadoutProvisioner.provisionConstructionMaterialsOnStart must stay disabled on the canonical player prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidatePlayerStarterLoadout(ValidationResult result)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                result.PlayerStarterLoadoutErrorCount++;
                result.Errors.Add($"{PlayerPrefabPath}: failed to load prefab contents for production starter loadout validation.");
                return;
            }

            try
            {
                PlayerToolManager toolManager = prefabRoot.GetComponentInChildren<PlayerToolManager>(true);
                if (toolManager == null)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: canonical player prefab is missing PlayerToolManager; starter tools cannot be validated.");
                    return;
                }

                SerializedObject serializedToolManager = new SerializedObject(toolManager);
                SerializedProperty grantProperty = serializedToolManager.FindProperty("grantAssignedToolItemsOnRuntimeStart");
                SerializedProperty budgetProperty = serializedToolManager.FindProperty("runtimeStartToolGrantBudget");
                SerializedProperty toolPrefabsProperty = serializedToolManager.FindProperty("toolPrefabs");
                if (grantProperty == null || !grantProperty.boolValue)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: PlayerToolManager.grantAssignedToolItemsOnRuntimeStart must exist and stay enabled for production starter tools.");
                }

                if (budgetProperty == null)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: PlayerToolManager.runtimeStartToolGrantBudget is missing.");
                }

                if (toolPrefabsProperty == null || !toolPrefabsProperty.isArray)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: PlayerToolManager.toolPrefabs array is missing; starter quick slots cannot be validated.");
                    return;
                }

                ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
                int assignedCount = 0;
                int validItemCount = 0;
                for (int i = 0; i < toolPrefabsProperty.arraySize; i++)
                {
                    SerializedProperty slotProperty = toolPrefabsProperty.GetArrayElementAtIndex(i);
                    GameObject prefab = slotProperty != null ? slotProperty.objectReferenceValue as GameObject : null;
                    if (prefab == null)
                        continue;

                    assignedCount++;
                    if (!prefab.TryGetComponent(out PlayerTool tool) || tool == null)
                    {
                        result.PlayerStarterLoadoutErrorCount++;
                        result.Errors.Add($"{PlayerPrefabPath}: starter tool slot {i} prefab '{prefab.name}' is missing PlayerTool on prefab root.");
                        continue;
                    }

                    ItemData item = tool.ToolData;
                    if (item == null || string.IsNullOrWhiteSpace(item.PersistentId))
                    {
                        result.PlayerStarterLoadoutErrorCount++;
                        result.Errors.Add($"{PlayerPrefabPath}: starter tool slot {i} prefab '{prefab.name}' has no valid ToolData PersistentId.");
                        continue;
                    }

                    int itemHash = LocHash.Compute(item.PersistentId);
                    if (itemHash == 0 || itemCatalog == null || !ReferenceEquals(itemCatalog.FindByHash(itemHash), item))
                    {
                        result.PlayerStarterLoadoutErrorCount++;
                        result.Errors.Add($"{PlayerPrefabPath}: starter tool slot {i} item '{item.PersistentId}' is not the active ItemCatalog entry.");
                        continue;
                    }

                    validItemCount++;
                }

                if (assignedCount < 3 || validItemCount < assignedCount)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: production starter loadout requires at least three valid authored quick-slot tools; assigned={assignedCount}, valid={validItemCount}.");
                }

                int grantBudget = budgetProperty != null ? budgetProperty.intValue : 0;
                if (grantBudget < assignedCount)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: runtimeStartToolGrantBudget={grantBudget} is lower than authored starter tool count={assignedCount}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static ItemData FindItemDataByPersistentId(string persistentId)
        {
            if (string.IsNullOrWhiteSpace(persistentId))
                return null;

            string[] itemGuids = AssetDatabase.FindAssets("t:ItemData", DataRoots);
            for (int i = 0; i < itemGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (item != null && string.Equals(item.PersistentId, persistentId, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private static bool HasHeldToolPrefabForItemId(string persistentId)
        {
            if (string.IsNullOrWhiteSpace(persistentId))
                return false;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ToolHeldPrefabRoot });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null || !prefab.TryGetComponent(out PlayerTool tool) || tool == null)
                    continue;

                ItemData item = tool.ToolData;
                if (item != null && string.Equals(item.PersistentId, persistentId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void ErrorIfSerializedBoolTrue(
            ValidationResult result,
            SerializedObject serializedObject,
            string propertyName,
            string message)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.PlayerDevProvisionerStartupRiskCount++;
                result.Errors.Add($"{serializedObject.targetObject.name}: expected serialized bool '{propertyName}' was not found.");
                return;
            }

            if (!property.boolValue)
                return;

            result.PlayerDevProvisionerStartupRiskCount++;
            result.Errors.Add(message);
        }

        private static void ValidateBaseModuleTemplates(ValidationResult result)
        {
            string[] moduleGuids = AssetDatabase.FindAssets("t:BaseModuleTemplate", DataRoots);
            for (int i = 0; i < moduleGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(moduleGuids[i]);
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(assetPath);
                if (template == null)
                    continue;

                result.BaseModuleCount++;
                if (template.TemplateHashId == 0)
                    result.Errors.Add($"{assetPath}: BaseModuleTemplate.TemplateHashId resolves to 0.");

                Vector3 proxyBoundsSize = template.ProxyBoundsSize;
                if (proxyBoundsSize.x <= 0.01f || proxyBoundsSize.y <= 0.01f || proxyBoundsSize.z <= 0.01f)
                    result.Errors.Add($"{assetPath}: BaseModuleTemplate.ProxyBoundsSize is degenerate.");
            }
        }

        private static void ValidateItemAudioMaterial(ValidationResult result, ItemData item, string assetPath)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty autoResolveProperty = serializedItem.FindProperty("autoResolvePhysicalMetadata");
            SerializedProperty audioMaterialProperty = serializedItem.FindProperty("audioMaterialId");
            bool autoResolve = autoResolveProperty != null && autoResolveProperty.boolValue;
            int serializedAudioMaterial = audioMaterialProperty != null ? audioMaterialProperty.intValue : -1;

            if (!autoResolve && !Enum.IsDefined(typeof(ItemAudioMaterialId), serializedAudioMaterial))
            {
                result.AudioMaterialViolationCount++;
                result.Errors.Add($"{assetPath}: serialized ItemData.audioMaterialId value '{serializedAudioMaterial}' is invalid.");
                return;
            }

            ItemAudioMaterialId resolvedDefault = ItemPhysicalMetadataUtility.ResolveDefaultAudioMaterialId(
                item.category,
                item.resourceFamily,
                item.PersistentId);

            if (!autoResolve &&
                audioMaterialProperty != null &&
                serializedAudioMaterial == (int)ItemAudioMaterialId.Organic &&
                resolvedDefault != ItemAudioMaterialId.Organic)
            {
                result.AudioMaterialViolationCount++;
                result.Errors.Add(
                    $"{assetPath}: AudioMaterialID is Organic while classification resolves to {resolvedDefault}. " +
                    "Likely stale or missing explicit audio-material authoring.");
            }
        }

        private static void RegisterHash(ValidationResult result, uint hash, string ownerLabel, string assetPath)
        {
            if (hash == 0u)
            {
                result.Errors.Add($"{assetPath}: authored hash resolves to 0 for '{ownerLabel}'.");
                return;
            }

            if (result.HashOwners.TryGetValue(hash, out string existingOwner))
            {
                result.HashCollisionCount++;
                result.Errors.Add(
                    $"{assetPath}: HASH COLLISION 0x{hash:X8} between '{existingOwner}' and '{ownerLabel}'.");
                return;
            }

            result.HashOwners.Add(hash, ownerLabel);
        }

        private static void RegisterItemPersistentId(ValidationResult result, string persistentId, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                result.Errors.Add($"{assetPath}: ItemData.PersistentId is empty.");
                return;
            }

            if (result.ItemPersistentIdOwners.TryGetValue(persistentId, out string existingPath))
            {
                result.ItemDataDuplicatePersistentIdCount++;
                result.Errors.Add(
                    $"{assetPath}: DUPLICATE ItemData.PersistentId '{persistentId}' already authored by '{existingPath}'.");
                return;
            }

            result.ItemPersistentIdOwners.Add(persistentId, assetPath);
        }

        private static void ValidatePrefabAsset(
            string prefabPath,
            string context,
            ValidationResult result,
            Mesh wireMesh,
            Material wireMaterial,
            bool allowMeshCollider)
        {
            if (string.IsNullOrWhiteSpace(prefabPath) || !result.ProcessedPrefabPaths.Add(prefabPath))
                return;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                result.Errors.Add($"{prefabPath}: failed to load prefab contents for {context}.");
                return;
            }

            bool changed = false;
            try
            {
                MeshCollider[] meshColliders = prefabRoot.GetComponentsInChildren<MeshCollider>(true);
                if (!allowMeshCollider && meshColliders != null && meshColliders.Length > 0)
                {
                    result.MeshColliderViolationCount += meshColliders.Length;
                    for (int i = 0; i < meshColliders.Length; i++)
                    {
                        MeshCollider meshCollider = meshColliders[i];
                        if (meshCollider == null)
                            continue;

                        result.Errors.Add(
                            $"{prefabPath}: MeshCollider is forbidden for {context} -> {BuildTransformPath(meshCollider.transform)}");
                    }
                }

                if (!HasRenderableMesh(prefabRoot))
                {
                    Vector3 center;
                    Vector3 size;
                    ResolveLocalBounds(prefabRoot, out center, out size);
                    if (EnsureWireframeProxy(prefabRoot, center, size, wireMesh, wireMaterial))
                    {
                        changed = true;
                        result.InjectedProxyCount++;
                        result.AutoFixes.Add($"{prefabPath}: injected wireframe proxy for {context}.");
                    }
                }

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool HasRenderableMesh(GameObject root)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    return true;
            }

            SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[i];
                if (renderer != null && renderer.sharedMesh != null)
                    return true;
            }

            return false;
        }

        private static bool EnsureWireframeProxy(GameObject root, Vector3 localCenter, Vector3 localSize, Mesh wireMesh, Material wireMaterial)
        {
            Transform existingProxy = FindChildRecursive(root.transform, InjectedProxyName);
            GameObject proxyObject = existingProxy != null
                ? existingProxy.gameObject
                : new GameObject(InjectedProxyName);

            bool changed = false;
            if (existingProxy == null)
            {
                proxyObject.transform.SetParent(root.transform, false);
                changed = true;
            }

            Vector3 sanitizedSize = SanitizeSize(localSize);
            if (proxyObject.transform.localPosition != localCenter)
            {
                proxyObject.transform.localPosition = localCenter;
                changed = true;
            }

            if (proxyObject.transform.localRotation != Quaternion.identity)
            {
                proxyObject.transform.localRotation = Quaternion.identity;
                changed = true;
            }

            if (proxyObject.transform.localScale != sanitizedSize)
            {
                proxyObject.transform.localScale = sanitizedSize;
                changed = true;
            }

            MeshFilter meshFilter = proxyObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = proxyObject.AddComponent<MeshFilter>();
                changed = true;
            }

            if (meshFilter.sharedMesh != wireMesh)
            {
                meshFilter.sharedMesh = wireMesh;
                changed = true;
            }

            MeshRenderer meshRenderer = proxyObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = proxyObject.AddComponent<MeshRenderer>();
                changed = true;
            }

            if (meshRenderer.sharedMaterial != wireMaterial)
            {
                meshRenderer.sharedMaterial = wireMaterial;
                changed = true;
            }

            if (meshRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                changed = true;
            }

            if (meshRenderer.receiveShadows)
            {
                meshRenderer.receiveShadows = false;
                changed = true;
            }

            return changed;
        }

        private static GameObject CreateOrUpdateFloraGhostProxy(
            FloraDataTemplate template,
            Mesh wireMesh,
            Material wireMaterial,
            string ownerPath)
        {
            EnsureFolder(FloraProxyFolder);
            string prefabName = $"PFB_{SanitizeToken(template.name)}_GhostProxy.prefab";
            string prefabPath = $"{FloraProxyFolder}/{prefabName}";

            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabName));
            try
            {
                ConfigureFloraGhostCapsuleCollider(root, template);

                EnsureWireframeProxy(root, template.BoundingBoxCenter, template.BoundingBoxSize, wireMesh, wireMaterial);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                    Debug.LogError($"[ContentSanityValidator] Failed to save flora ghost proxy for '{ownerPath}'.");

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Vector3 SanitizeSize(Vector3 value)
        {
            return new Vector3(
                Mathf.Max(0.1f, Mathf.Abs(value.x)),
                Mathf.Max(0.1f, Mathf.Abs(value.y)),
                Mathf.Max(0.1f, Mathf.Abs(value.z)));
        }

        private static void ConfigureFloraGhostCapsuleCollider(GameObject root, FloraDataTemplate template)
        {
            Vector3 size = SanitizeSize(template.BoundingBoxSize);
            Vector3 extents = size * 0.5f;
            int axis = ResolveFloraGhostCapsuleAxis(template.Category, template.ProxyShapeType, extents);
            int secondaryA = (axis + 1) % 3;
            int secondaryB = (axis + 2) % 3;
            float secondaryMin = Mathf.Min(GetAxis(size, secondaryA), GetAxis(size, secondaryB));
            float radius = Mathf.Max(0.05f, secondaryMin * 0.5f);

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = template.BoundingBoxCenter;
            collider.direction = axis;
            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, GetAxis(size, axis));
        }

        private static int ResolveFloraGhostCapsuleAxis(
            FloraDataTemplate.FloraCategory category,
            FloraDataTemplate.ProxyShape proxyShape,
            Vector3 extents)
        {
            if (category == FloraDataTemplate.FloraCategory.HarvestableKelp ||
                category == FloraDataTemplate.FloraCategory.GiantSargassum)
            {
                return 1;
            }

            if (category == FloraDataTemplate.FloraCategory.HardCoral ||
                proxyShape == FloraDataTemplate.ProxyShape.Fan ||
                proxyShape == FloraDataTemplate.ProxyShape.SphereCluster)
            {
                return extents.x >= extents.z ? 0 : 2;
            }

            if (extents.y >= extents.x && extents.y >= extents.z)
                return 1;

            return extents.x >= extents.z ? 0 : 2;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : (axis == 1 ? value.y : value.z);
        }

        private static void ResolveLocalBounds(GameObject root, out Vector3 localCenter, out Vector3 localSize)
        {
            bool hasBounds = false;
            Bounds combinedBounds = default;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                EncapsulateWorldBounds(root.transform, collider.bounds, ref hasBounds, ref combinedBounds);
            }

            if (!hasBounds)
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    EncapsulateWorldBounds(root.transform, renderer.bounds, ref hasBounds, ref combinedBounds);
                }
            }

            if (!hasBounds)
            {
                localCenter = Vector3.zero;
                localSize = Vector3.one;
                return;
            }

            localCenter = combinedBounds.center;
            localSize = SanitizeSize(combinedBounds.size);
        }

        private static void EncapsulateWorldBounds(
            Transform root,
            Bounds worldBounds,
            ref bool hasBounds,
            ref Bounds combinedBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 localPoint = root.InverseTransformPoint(corners[i]);
                if (!hasBounds)
                {
                    combinedBounds = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(localPoint);
                }
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildRecursive(root.GetChild(i), childName);
                if (child != null)
                    return child;
            }

            return null;
        }

        private static string BuildTransformPath(Transform target)
        {
            if (target == null)
                return "<null>";

            string path = target.name;
            Transform cursor = target.parent;
            while (cursor != null)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return path;
        }

        private static Mesh EnsureWireCubeMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedMeshPath);
            if (mesh != null)
                return mesh;

            mesh = new Mesh
            {
                name = "MESH_ContentSanityWireCube"
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)
            };

            int[] indices =
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            };

            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            mesh.UploadMeshData(false);
            AssetDatabase.CreateAsset(mesh, GeneratedMeshPath);
            return mesh;
        }

        private static Material EnsureWireframeMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, GeneratedMaterialPath);
            }

            Color color = new Color(1f, 0.15f, 0.15f, 1f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static string SanitizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (char.IsLetterOrDigit(current) || current == '_')
                    continue;

                chars[i] = '_';
            }

            return new string(chars);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void EmitSummary(ValidationResult result)
        {
            string summary =
                $"[ContentSanityValidator] DataPrefabs={result.DataPrefabCount}, " +
                $"ReferencedPrefabs={result.ReferencedPrefabCount}, " +
                $"Items={result.ItemCount}, Recipes={result.RecipeCount}, Quests={result.QuestCount}, ToolMetadata={result.ToolMetadataCount}, " +
                $"ToolHeldPrefabs={result.ToolHeldPrefabCount}, Flora={result.FloraCount}, Fauna={result.FaunaCount}, " +
                $"ResourceNodes={result.ResourceNodeCount}, BaseModules={result.BaseModuleCount}, " +
                $"InjectedProxyCount={result.InjectedProxyCount}, GeneratedFloraProxyCount={result.GeneratedFloraProxyCount}, " +
                $"MeshColliderViolations={result.MeshColliderViolationCount}, HashCollisions={result.HashCollisionCount}, " +
                $"ItemDataDuplicatePersistentId={result.ItemDataDuplicatePersistentIdCount}, " +
                $"ItemCatalogNullEntries={result.ItemCatalogNullEntryCount}, " +
                $"ItemCatalogDuplicateHashes={result.ItemCatalogDuplicateHashCount}, " +
                $"ItemCatalogMissingRuntimeDescriptors={result.ItemCatalogMissingRuntimeDescriptorCount}, " +
                $"ItemCatalogLookupAmbiguities={result.ItemCatalogLookupAmbiguityCount}, " +
                $"RecipeRouteErrors={result.RecipeRouteErrorCount}, " +
                $"RecipeScanGateWarnings={result.RecipeScanGateWarningCount}, " +
                $"QuestRouteErrors={result.QuestRouteErrorCount}, " +
                $"ToolMetadataOrphans={result.ToolMetadataOrphanCount}, " +
                $"ToolRouteErrors={result.ToolRouteErrorCount}, " +
                $"AudioMaterialViolations={result.AudioMaterialViolationCount}, " +
                $"ResourceNodeYieldMissingWorldPrefab={result.ResourceNodeYieldMissingWorldPrefabCount}, " +
                $"ResourceNodeYieldNotCataloged={result.ResourceNodeYieldNotCatalogedCount}, " +
                $"ResourceNodeYieldInvalidWorldPrefabContract={result.ResourceNodeYieldInvalidWorldPrefabContractCount}, " +
                $"ResourceNodeToolGateErrors={result.ResourceNodeToolGateErrorCount}, " +
                $"FirstHourCraftGateErrors={result.FirstHourCraftGateErrorCount}, " +
                $"FirstHourDrillRouteErrors={result.FirstHourDrillRouteErrorCount}, " +
                $"FirstHourOxygenRouteErrors={result.FirstHourOxygenRouteErrorCount}, " +
                $"PlayerPdaHeadlessOpenRisk={result.PlayerPdaHeadlessOpenRiskCount}, " +
                $"PlayerPdaBridgeWarnings={result.PlayerPdaBridgeWarningCount}, " +
                $"PlayerDevProvisionerStartupRisk={result.PlayerDevProvisionerStartupRiskCount}, " +
                $"PlayerStarterLoadoutErrors={result.PlayerStarterLoadoutErrorCount}, " +
                $"Errors={result.Errors.Count}, Warnings={result.Warnings.Count}.";

            if (result.Errors.Count > 0)
            {
                Debug.LogError(summary);
                for (int i = 0; i < result.Errors.Count; i++)
                    Debug.LogError("[ContentSanityValidator] " + result.Errors[i]);
            }
            else
            {
                Debug.Log(summary);
            }

            for (int i = 0; i < result.Warnings.Count; i++)
                Debug.LogWarning("[ContentSanityValidator] " + result.Warnings[i]);

            for (int i = 0; i < result.AutoFixes.Count; i++)
                Debug.Log("[ContentSanityValidator] FIX " + result.AutoFixes[i]);
        }
    }
}
#endif
