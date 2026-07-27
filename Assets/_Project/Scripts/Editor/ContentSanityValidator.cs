#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Dev;
using Hecton8.Editor;
using Hecton8.EditorTools.Diagnostics;
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
        private const string StarterSurvivalStatsPath = DataRoot + "/Survival/Standard_Suit_V1.asset";
        private const string ProductionWorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string FabricatorScriptPath = "Assets/_Project/Scripts/Fabricator.cs";
        private const string AssemblyHologramMaterialPath = "Assets/_Project/Art/Materials/MAT_FabricatorAssembly_Hologram.asset";
        private const string ForwardFabricatorObjectName = "Forward_Fabricator";
        private const string ForwardFabricatorSocketId = "socket.fabrication.forward";
        private const string ResourceDistributionDirectorScriptPath = "Assets/_Project/Scripts/World/ResourceDistributionDirector.cs";
        private const string ScavengingLootOracleRuntimeScriptPath = "Assets/_Project/Scripts/Scavenging/ScavengingLootOracleRuntime.cs";
        private const string LoreSystemsRootScriptPath = "Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs";
        private const string QuestManagerScriptPath = "Assets/_Project/Scripts/Quest/QuestManager.cs";
        private const string FirstHourDirectorScriptPath = "Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs";
        private const string PdaLoadoutTabScriptPath = "Assets/_Project/Scripts/UI/PDALoadoutTab.cs";
        private const string PdaLoadoutPresetPathPrefix = "Assets/_Project/Data/Tools/Presets/";
        private const string RuntimeOrePrefabPath = "Assets/_Project/Prefabs/Resources/Nodes/PFB_Ore_Generic.prefab";
        private const string RuntimeMagmaVentPrefabPath = "Assets/_Project/Prefabs/Resources/Nodes/PFB_Ore_MagmaVentMarker.prefab";
        private const string ToolHeldPrefabRoot = "Assets/_Project/Prefabs/Tools/Held";
        private const string GenericResourceScanEntryId = "scan.resource_node";
        private const string ScannerToolItemId = "Item_Tool_Scanner";
        private const string SeafloorDrillItemId = "Item_Tool_SeafloorDrill";
        private const string SeafloorDrillRecipePath = DataRoot + "/Crafting/Recipes/Recipe_SeafloorDrill.asset";
        private const string ArrivalQuestId = "quest_arrival";
        private const string StarterDrillQuestId = "quest_starter_drill";
        private const string CopperSampleQuestId = "quest_copper_sample";
        private const string FirstBreathQuestId = "quest_first_breath";
        private const string StarterDrillQuestPath = DataRoot + "/Lore/Quests/Quest_StarterDrill.asset";
        private const string CopperSampleQuestPath = DataRoot + "/Lore/Quests/Quest_CopperSample.asset";
        private const string FirstBreathQuestPath = DataRoot + "/Lore/Quests/Quest_FirstBreath.asset";
        private const string GlassPanelItemId = "Comp_GlassPanel";
        private const string FiberMeshItemId = "Comp_FiberMesh";
        private const string SilicaShardsItemId = "Data_SilicaShards";
        private const string FiberKelpItemId = "Data_FiberKelp";
        private const string GlassPanelRecipePath = DataRoot + "/Crafting/Recipes/Recipe_GlassPanel.asset";
        private const string FiberMeshRecipePath = DataRoot + "/Crafting/Recipes/Recipe_FiberMesh.asset";
        private const string SilicaShardClusterTemplatePath = DataRoot + "/Scavenging/ResourceNodes/ResourceNodeTemplate_SilicaShardCluster.asset";
        private const string FiberKelpStandTemplatePath = DataRoot + "/Scavenging/ResourceNodes/ResourceNodeTemplate_FiberKelpStand.asset";
        private const string CopperItemId = "Data_Copper";
        private const string CopperWireItemId = "Comp_CopperWire";
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
            "Comp_PressureSeal",
            SeafloorDrillItemId
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
            public int ResourceDistributionRouteErrorCount;
            public int FirstHourCraftGateErrorCount;
            public int FirstHourDrillRouteErrorCount;
            public int FirstHourOxygenRouteErrorCount;
            public int PlayerPdaHeadlessOpenRiskCount;
            public int PlayerPdaBridgeWarningCount;
            public int PdaLoadoutPresetPathErrorCount;
            public int PlayerDevProvisionerStartupRiskCount;
            public int PlayerStarterLoadoutErrorCount;
            public int PlayerSurfaceProbeAuthoringErrorCount;
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
            ValidateResourceDistributionRuntimeRoute(result);
            ValidateFirstHourDrillRoute(result);
            ValidateFirstHourQuestSpine(result);
            ValidateFirstHourRuntimeSceneOwners(result);
            ValidateFirstHourFabricatorSceneRoute(result);
            ValidateBaseModuleTemplates(result);
            ValidatePlayerPdaShell(result);
            ValidatePdaLoadoutPresetReferences(result);
            ValidatePlayerDevProvisioning(result);
            ValidatePlayerStarterLoadout(result);
            ValidatePlayerSurfaceProbeAuthoring(result);

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
                int hashId = ItemData.ResolvePersistentHashId(item);
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

                int hashId = ItemData.ResolvePersistentHashId(item);
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
            int hashId = ItemData.ResolvePersistentHashId(item);
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
            int hashId = ItemData.ResolvePersistentHashId(item);
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
                ValidateResourceNodeHostLayers(result, template, assetPath);

                ValidateResourceNodeYieldArray(result, itemCatalog, template, assetPath, "harvestYield", "harvestYield");
                ValidateResourceNodeYieldArray(result, itemCatalog, template, assetPath, "rarityDrops", "rarityDrops");
            }
        }

        private static void ValidateResourceDistributionRuntimeRoute(ValidationResult result)
        {
            if (result.ResourceNodeCount <= 0)
                return;

            // Presence is a format-agnostic question and is answered through the dependency graph.
            // The YAML read below is only for the serialized field values, which no dependency
            // edge can carry, and it now fails with a specific reason when the scene is binary.
            bool sceneTextAvailable = TryReadProductionSceneYaml(out string sceneText, out string sceneReadFailure);
            string directorGuid = AssetDatabase.AssetPathToGUID(ResourceDistributionDirectorScriptPath);
            string directorSceneBlock = string.Empty;
            bool hasDirectorInScene = ProductionSceneSerializesScript(ResourceDistributionDirectorScriptPath);
            bool directorBlockAvailable = hasDirectorInScene &&
                                          sceneTextAvailable &&
                                          !string.IsNullOrWhiteSpace(directorGuid) &&
                                          TryExtractMonoBehaviourBlockByScriptGuid(sceneText, directorGuid, out directorSceneBlock);
            if (string.IsNullOrWhiteSpace(directorGuid))
            {
                AddResourceDistributionRouteError(
                    result,
                    $"{ResourceDistributionDirectorScriptPath}: missing script asset; production resource distribution cannot register a runtime director.");
            }
            else if (!hasDirectorInScene)
            {
                AddResourceDistributionRouteError(
                    result,
                    $"{ProductionWorldScenePath}: production scene has ResourceNodeTemplate data but no serialized ResourceDistributionDirector component. " +
                    $"Run HECTON-8/World/Install Resource Distribution Director in the loaded world scene. " +
                    $"Director script GUID={directorGuid}. " +
                    "Resolved through the AssetDatabase dependency graph, which reads binary and text scenes alike.");
            }

            ValidateScavengingLootOracleHost(result);

            bool hasOreFallbackPrefab = ValidateResourceDistributionOreFallbackPrefab(result);
            bool hasMagmaVentPrefab = ValidateResourceDistributionMagmaVentPrefab();
            CountResourceTemplateRuntimePrefabCoverage(out int templateCount, out int validTemplatePrefabCount);
            bool allTemplatesHaveRuntimePrefab = templateCount > 0 && validTemplatePrefabCount == templateCount;
            if (directorBlockAvailable)
            {
                ValidateResourceDistributionSceneAssignments(
                    result,
                    directorSceneBlock,
                    hasOreFallbackPrefab,
                    hasMagmaVentPrefab,
                    allTemplatesHaveRuntimePrefab);
            }
            else if (hasDirectorInScene)
            {
                // The director IS in the scene, so this is not a wiring defect - the field-level
                // check simply cannot run. Say that, loudly and specifically. Silently skipping it
                // would leave a merge gate that is vacuously green.
                result.Warnings.Add(
                    $"{ProductionWorldScenePath}: ResourceDistributionDirector is serialized in the production scene, but its field " +
                    $"assignments could not be inspected: {sceneReadFailure} " +
                    "Prefab and template assignments on the director are therefore UNVERIFIED, not proven correct.");
            }

            if (!hasOreFallbackPrefab && !allTemplatesHaveRuntimePrefab)
            {
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: missing valid ResourceNode fallback prefab and only {validTemplatePrefabCount}/{templateCount} ResourceNodeTemplate assets have valid runtimeNodePrefab assignments. " +
                    $"Runtime ore spawning will fail closed; run HECTON-8/World/Install Resource Distribution Director or author valid template prefabs.");
            }

            if (!hasMagmaVentPrefab)
            {
                result.Warnings.Add(
                    $"{RuntimeMagmaVentPrefabPath}: optional magma-vent marker prefab is missing or invalid. " +
                    "Seismic vent markers will be silent until the resource-distribution bootstrap is installed.");
            }
        }

        private static void ValidateScavengingLootOracleHost(ValidationResult result)
        {
            string oracleGuid = AssetDatabase.AssetPathToGUID(ScavengingLootOracleRuntimeScriptPath);
            if (string.IsNullOrWhiteSpace(oracleGuid))
            {
                AddResourceDistributionRouteError(
                    result,
                    $"{ScavengingLootOracleRuntimeScriptPath}: missing script asset; ResourceNode incremental yield cannot queue item acquisition signals.");
                return;
            }

            if (ProductionSceneSerializesScript(ScavengingLootOracleRuntimeScriptPath))
                return;

            AddResourceDistributionRouteError(
                result,
                $"{ProductionWorldScenePath}: production resource distribution has no serialized ScavengingLootOracleRuntime host. " +
                "ResourceNode extraction can deplete nodes while failing to publish item pickup signals. " +
                $"Run HECTON-8/World/Install Resource Distribution Director. Loot oracle script GUID={oracleGuid}. " +
                "Resolved through the AssetDatabase dependency graph, which reads binary and text scenes alike.");
        }

        private static bool ValidateResourceDistributionOreFallbackPrefab(ValidationResult result)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeOrePrefabPath);
            if (prefab == null)
                return false;

            bool valid = true;
            if (prefab.GetComponent<ResourceNode>() == null)
            {
                valid = false;
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: fallback prefab exists but its root has no ResourceNode component. ResourceDistributionDirector only accepts ResourceNode roots.");
            }

            MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                valid = false;
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: fallback prefab root must keep a MeshFilter with a shared mesh so meshless ResourceNodeTemplate assets remain visible.");
            }

            MeshRenderer meshRenderer = prefab.GetComponent<MeshRenderer>();
            if (meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                valid = false;
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: fallback prefab root must keep a MeshRenderer with a shared material so meshless ResourceNodeTemplate assets remain visible.");
            }

            if (WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh(RuntimeOrePrefabPath))
            {
                valid = false;
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: fallback prefab uses a Unity built-in primitive mesh. Run the resource-distribution bootstrap to replace it with generated production mesh assets.");
            }

            if (!IsLayerIncludedInMask(prefab.layer, HectonLayerMasks.FieldToolSurfaceLayerMask) ||
                !IsLayerIncludedInMask(prefab.layer, HectonLayerMasks.FieldToolScanLayerMask))
            {
                valid = false;
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: fallback prefab root layer {prefab.layer} is not included by field tool surface/scan masks. " +
                    "Spawned resources would exist but handheld tools could miss them.");
            }

            BoxCollider boxCollider = prefab.GetComponent<BoxCollider>();
            SphereCollider sphereCollider = prefab.GetComponent<SphereCollider>();
            if (boxCollider == null || sphereCollider == null)
            {
                valid = false;
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: fallback prefab root must keep both BoxCollider and SphereCollider. " +
                    "ResourceNodeTemplate.RuntimeColliderShape swaps these primitive colliders at spawn time.");
            }
            else if (boxCollider.isTrigger || sphereCollider.isTrigger)
            {
                valid = false;
                AddResourceDistributionRouteError(
                    result,
                    $"{RuntimeOrePrefabPath}: fallback prefab primitive colliders must be non-trigger colliders for handheld tool raycasts.");
            }

            return valid;
        }

        private static bool ValidateResourceDistributionMagmaVentPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeMagmaVentPrefabPath);
            if (prefab == null)
                return false;

            MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = prefab.GetComponent<MeshRenderer>();
            return meshFilter != null &&
                   meshFilter.sharedMesh != null &&
                   meshRenderer != null &&
                   meshRenderer.sharedMaterial != null &&
                   !WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh(RuntimeMagmaVentPrefabPath);
        }

        private static void ValidateResourceDistributionSceneAssignments(
            ValidationResult result,
            string directorSceneBlock,
            bool hasOreFallbackPrefab,
            bool hasMagmaVentPrefab,
            bool allTemplatesHaveRuntimePrefab)
        {
            if (string.IsNullOrWhiteSpace(directorSceneBlock))
                return;

            if (hasOreFallbackPrefab && !allTemplatesHaveRuntimePrefab)
            {
                string orePrefabGuid = AssetDatabase.AssetPathToGUID(RuntimeOrePrefabPath);
                if (!string.IsNullOrWhiteSpace(orePrefabGuid) &&
                    directorSceneBlock.IndexOf(orePrefabGuid, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    AddResourceDistributionRouteError(
                        result,
                        $"{ProductionWorldScenePath}: ResourceDistributionDirector route exists but does not serialize {RuntimeOrePrefabPath}. " +
                        "Runtime ore spawning can still fail closed; rerun HECTON-8/World/Install Resource Distribution Director.");
                }
            }

            if (hasMagmaVentPrefab)
            {
                string magmaVentPrefabGuid = AssetDatabase.AssetPathToGUID(RuntimeMagmaVentPrefabPath);
                if (!string.IsNullOrWhiteSpace(magmaVentPrefabGuid) &&
                    directorSceneBlock.IndexOf(magmaVentPrefabGuid, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    result.Warnings.Add(
                        $"{ProductionWorldScenePath}: ResourceDistributionDirector route exists but does not serialize {RuntimeMagmaVentPrefabPath}. " +
                        "Seismic vent markers remain disabled until the bootstrap is rerun.");
                }
            }

            if (directorSceneBlock.IndexOf("resourceTemplates:", StringComparison.Ordinal) < 0)
            {
                AddResourceDistributionRouteError(
                    result,
                    $"{ProductionWorldScenePath}: ResourceDistributionDirector route exists but no resourceTemplates field was found in scene serialization.");
            }
            else
            {
                ValidateResourceDistributionSceneTemplateCoverage(result, directorSceneBlock);
            }
        }

        private static void ValidateResourceDistributionSceneTemplateCoverage(ValidationResult result, string directorSceneBlock)
        {
            string[] resourceGuids = AssetDatabase.FindAssets("t:ResourceNodeTemplate", DataRoots);
            if (resourceGuids == null || resourceGuids.Length == 0)
                return;

            int missingCount = 0;
            List<string> missingSamples = new List<string>(4);
            for (int i = 0; i < resourceGuids.Length; i++)
            {
                string templateGuid = resourceGuids[i];
                if (string.IsNullOrWhiteSpace(templateGuid) ||
                    directorSceneBlock.IndexOf(templateGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                missingCount++;
                if (missingSamples.Count < 4)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(templateGuid);
                    missingSamples.Add(string.IsNullOrWhiteSpace(assetPath) ? templateGuid : assetPath);
                }
            }

            if (missingCount <= 0)
                return;

            AddResourceDistributionRouteError(
                result,
                $"{ProductionWorldScenePath}: ResourceDistributionDirector.resourceTemplates is missing {missingCount}/{resourceGuids.Length} ResourceNodeTemplate assets. " +
                $"Examples: {string.Join(", ", missingSamples)}. Rerun HECTON-8/World/Install Resource Distribution Director.");
        }

        private static void CountResourceTemplateRuntimePrefabCoverage(out int templateCount, out int validTemplatePrefabCount)
        {
            templateCount = 0;
            validTemplatePrefabCount = 0;

            string[] resourceGuids = AssetDatabase.FindAssets("t:ResourceNodeTemplate", DataRoots);
            for (int i = 0; i < resourceGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(resourceGuids[i]);
                ResourceNodeTemplate template = AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(assetPath);
                if (template == null)
                    continue;

                templateCount++;
                if (template.ValidateRuntimeNodePrefabCold())
                    validTemplatePrefabCount++;
            }
        }

        private static void AddResourceDistributionRouteError(ValidationResult result, string message)
        {
            result.ResourceDistributionRouteErrorCount++;
            result.Errors.Add(message);
        }

        private static bool IsLayerIncludedInMask(int layer, int layerMask)
        {
            return layer >= 0 &&
                   layer < 32 &&
                   (layerMask & (1 << layer)) != 0;
        }

        /// <summary>
        /// True when the production world scene serializes at least one component of the script at
        /// <paramref name="scriptAssetPath"/>.
        ///
        /// THIS REPLACES A GATE THAT COULD NEVER PASS. The previous route read the scene with
        /// <see cref="TryReadProjectTextFile"/> and searched the resulting string for the script's
        /// 32-character hex GUID. Assets/_Project/Scenes/02_HECTON_WORLD.unity is BINARY on disk -
        /// it opens with null bytes and has no %YAML header, because ProjectSettings/
        /// EditorSettings.asset sets m_SerializationMode: 2 (ForceBinary). Binary Unity
        /// serialization stores a script reference as raw GUID bytes, never as hex text, so the
        /// search could not match. Worse, File.ReadAllText does not throw on binary input - it
        /// lossily decodes it - so the read "succeeded", the read-failure suffix came back empty,
        /// and the validator reported a confident "component is missing" for every component in
        /// the production scene whether it was authored there or not.
        ///
        /// A direct (non-recursive) AssetDatabase dependency is exactly what that text search was
        /// looking for: a script GUID stored in the scene file itself. Same question, answered
        /// through the importer, so it is correct for binary and text scenes alike.
        /// </summary>
        private static bool ProductionSceneSerializesScript(string scriptAssetPath)
        {
            return H8_FormatAgnosticTypeCensus.AssetDirectlyReferencesScript(ProductionWorldScenePath, scriptAssetPath);
        }

        /// <summary>
        /// Reads the production world scene as YAML text, and fails with a specific, actionable
        /// reason when the scene is not text-serialized instead of handing back a lossily decoded
        /// binary blob that every downstream <c>IndexOf</c> will silently miss in.
        ///
        /// Only the gates that need a serialized PROPERTY BLOCK (layer, socket id, collider,
        /// object name, field assignments) may use this. Presence questions go through
        /// <see cref="ProductionSceneSerializesScript"/>, which does not care about the format.
        /// </summary>
        private static bool TryReadProductionSceneYaml(out string sceneText, out string failure)
        {
            sceneText = string.Empty;
            if (!H8_FormatAgnosticTypeCensus.IsYamlTextSerialized(ProductionWorldScenePath, out string formatDetail))
            {
                failure =
                    $"scene is not YAML text ({formatDetail}), so a text parse over it cannot resolve object names, " +
                    "layers, colliders, or serialized field values. This is a structural limit of the text route, not a missing component. " +
                    "Resolve scene contents with the object model instead: Unity.exe -batchmode -quit -projectPath . " +
                    "-executeMethod Hecton8.EditorTools.Diagnostics.H8_FormatAgnosticTypeCensus.Run";
                return false;
            }

            return TryReadProjectTextFile(ProductionWorldScenePath, out sceneText, out failure);
        }

        private static bool TryExtractMonoBehaviourBlockByScriptGuid(string sceneText, string scriptGuid, out string block)
        {
            block = string.Empty;
            if (string.IsNullOrWhiteSpace(sceneText) || string.IsNullOrWhiteSpace(scriptGuid))
                return false;

            int guidIndex = sceneText.IndexOf(scriptGuid, StringComparison.OrdinalIgnoreCase);
            if (guidIndex < 0)
                return false;

            int blockStart = sceneText.LastIndexOf("\n--- !u!114", guidIndex, StringComparison.Ordinal);
            if (blockStart < 0)
                blockStart = sceneText.LastIndexOf("--- !u!114", guidIndex, StringComparison.Ordinal);
            if (blockStart < 0)
                return false;

            int blockEnd = sceneText.IndexOf("\n--- !u!", guidIndex, StringComparison.Ordinal);
            if (blockEnd < 0)
                blockEnd = sceneText.Length;

            block = sceneText.Substring(blockStart, blockEnd - blockStart);
            return block.IndexOf(scriptGuid, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryReadProjectTextFile(string projectAssetPath, out string text, out string failure)
        {
            failure = string.Empty;
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(projectAssetPath))
                return false;

            string absolutePath = ProjectAssetPathToAbsolutePath(projectAssetPath);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                failure = "File is missing.";
                return false;
            }

            try
            {
                text = File.ReadAllText(absolutePath);
                return true;
            }
            catch (Exception exception)
            {
                failure = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static string ProjectAssetPathToAbsolutePath(string projectAssetPath)
        {
            if (string.IsNullOrWhiteSpace(projectAssetPath))
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string normalizedPath = projectAssetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalizedPath));
        }

        private static void ValidateResourceNodeHostLayers(
            ValidationResult result,
            ResourceNodeTemplate template,
            string assetPath)
        {
            SerializedObject serializedTemplate = new SerializedObject(template);
            SerializedProperty validLayersProperty = serializedTemplate.FindProperty("validLayers");
            if (validLayersProperty == null || validLayersProperty.propertyType != SerializedPropertyType.LayerMask)
            {
                result.Errors.Add($"{assetPath}: ResourceNodeTemplate.validLayers is missing or no longer serialized as a LayerMask.");
                return;
            }

            int authoredMask = validLayersProperty.intValue;
            int resolvedMask = HectonLayerMasks.ResolveResourceNodeHostLayerMask(authoredMask);
            bool hasTerrainSdfHost = (resolvedMask & HectonLayerMasks.TerrainSdfProbeLayerMask) != 0;
            if (authoredMask == 0 ||
                authoredMask == HectonLayerMasks.StrictInteractionLayerMask ||
                HectonLayerMasks.IsEverythingLayerMask(authoredMask) ||
                !hasTerrainSdfHost)
            {
                result.Errors.Add(
                    $"{assetPath}: ResourceNodeTemplate.validLayers must target terrain/SDF host surfaces. " +
                    $"Current mask={authoredMask}, expected={HectonLayerMasks.TerrainSdfProbeLayerMask}.");
                return;
            }

            if (authoredMask != resolvedMask)
            {
                result.Warnings.Add(
                    $"{assetPath}: ResourceNodeTemplate.validLayers is missing one or more terrain/SDF host layers. " +
                    $"Current mask={authoredMask}, resolved={resolvedMask}.");
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
            bool hasPlayerKnownToolRegistry = PlayerKnownToolPrefabRegistryContainsItemId(
                SeafloorDrillItemId,
                out string playerKnownToolRegistryFailure);
            RecipeData drillRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(SeafloorDrillRecipePath);
            bool hasCraftRecipe = RecipeResultMatchesPersistentId(drillRecipe, SeafloorDrillItemId) &&
                                  drillRecipe.ingredients != null &&
                                  drillRecipe.ingredients.Count > 0;
            bool hasRecipeScanGate = drillRecipe != null &&
                                     string.Equals(drillRecipe.RequiredScanEntryId, GenericResourceScanEntryId, StringComparison.Ordinal);
            bool hasCircularCopperIngredient =
                RecipeUsesItemPersistentId(drillRecipe, CopperItemId) ||
                RecipeUsesItemPersistentId(drillRecipe, CopperWireItemId);
            bool hasEarlyIngredientRoute = HasFirstHourDrillIngredientRoute(drillRecipe, out string ingredientRouteFailure);
            if (drillItem != null &&
                hasHeldPrefab &&
                hasPlayerKnownToolRegistry &&
                hasCraftRecipe &&
                hasRecipeScanGate &&
                !hasCircularCopperIngredient &&
                hasEarlyIngredientRoute)
            {
                return;
            }

            result.FirstHourDrillRouteErrorCount++;
            List<string> missing = new List<string>(6);
            if (drillItem == null)
                missing.Add("ItemData");
            if (!hasHeldPrefab)
                missing.Add("held prefab");
            if (!hasPlayerKnownToolRegistry)
                missing.Add($"player known-tool registry ({playerKnownToolRegistryFailure})");
            if (!hasCraftRecipe)
                missing.Add("craft recipe");
            if (!hasRecipeScanGate)
                missing.Add($"{GenericResourceScanEntryId} scan gate");
            if (hasCircularCopperIngredient)
                missing.Add("non-circular drill recipe ingredients");
            if (!hasEarlyIngredientRoute)
                missing.Add($"pre-copper ingredient route ({ingredientRouteFailure})");
            result.Errors.Add(
                $"{CopperVeinTemplatePath}: copper is Drill-gated, but first-hour seafloor drill route is incomplete; " +
                $"missing {string.Join(", ", missing)} for PersistentId='{SeafloorDrillItemId}'. " +
                $"Do not fall back to Knife/Any and do not require copper/copper wire before the copper drill gate opens.");
        }

        private static void ValidateFirstHourQuestSpine(ValidationResult result)
        {
            List<string> failures = new List<string>(8);
            QuestData starterDrill = AssetDatabase.LoadAssetAtPath<QuestData>(StarterDrillQuestPath);
            QuestData copperSample = AssetDatabase.LoadAssetAtPath<QuestData>(CopperSampleQuestPath);
            QuestData firstBreath = AssetDatabase.LoadAssetAtPath<QuestData>(FirstBreathQuestPath);

            ValidateQuestIdentity(starterDrill, StarterDrillQuestPath, StarterDrillQuestId, failures);
            ValidateQuestIdentity(copperSample, CopperSampleQuestPath, CopperSampleQuestId, failures);
            ValidateQuestIdentity(firstBreath, FirstBreathQuestPath, FirstBreathQuestId, failures);

            if (starterDrill != null)
            {
                if (starterDrill.completionType != QuestCompletionType.OnCraftCompleted ||
                    !string.Equals(starterDrill.completionId, SeafloorDrillItemId, StringComparison.Ordinal))
                {
                    failures.Add($"{StarterDrillQuestPath} must complete on crafting '{SeafloorDrillItemId}'");
                }

                if (!HasPrerequisiteQuestId(starterDrill, ArrivalQuestId))
                    failures.Add($"{StarterDrillQuestPath} must depend on '{ArrivalQuestId}'");
            }

            if (copperSample != null)
            {
                if (copperSample.completionType != QuestCompletionType.OnItemCollected ||
                    !string.Equals(copperSample.completionId, CopperItemId, StringComparison.Ordinal))
                {
                    failures.Add($"{CopperSampleQuestPath} must complete on collecting '{CopperItemId}'");
                }

                if (!HasPrerequisiteQuestId(copperSample, StarterDrillQuestId))
                    failures.Add($"{CopperSampleQuestPath} must depend on '{StarterDrillQuestId}' so copper never becomes the pre-drill route");
            }

            if (firstBreath != null && !HasPrerequisiteQuestId(firstBreath, CopperSampleQuestId))
                failures.Add($"{FirstBreathQuestPath} must depend on '{CopperSampleQuestId}' so depth pressure follows the drill/copper chain");

            if (failures.Count <= 0)
                return;

            result.FirstHourDrillRouteErrorCount++;
            result.Errors.Add("FirstHourQuestSpine: " + string.Join("; ", failures));
        }

        /// <summary>
        /// Gates the three owners the First 20 Minutes contract's source anchor depends on
        /// (Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:15 names
        /// FirstHourDirector.cs by name).
        ///
        /// This gate was permanently, silently failing. It read the binary production scene as
        /// text and searched for three hex GUIDs that binary serialization never writes as hex, so
        /// all three owners were reported missing on every single run regardless of the truth, and
        /// the read never failed loudly enough to reveal why. It now resolves each owner through
        /// the AssetDatabase dependency graph, which is format-agnostic.
        /// </summary>
        private static void ValidateFirstHourRuntimeSceneOwners(ValidationResult result)
        {
            List<string> missing = new List<string>(3);
            RequireSceneScriptReference(LoreSystemsRootScriptPath, "HectonLoreSystemsRoot", missing);
            RequireSceneScriptReference(QuestManagerScriptPath, "QuestManager", missing);
            RequireSceneScriptReference(FirstHourDirectorScriptPath, "FirstHourDirector", missing);
            if (missing.Count <= 0)
                return;

            result.FirstHourDrillRouteErrorCount++;
            result.Errors.Add(
                $"{ProductionWorldScenePath}: missing first-hour runtime owner(s): {string.Join(", ", missing)}. " +
                "QuestData assets alone do not run the drill/copper/first-breath chain; run Tools/Hecton8/Lore Systems/Bootstrap Production World Scene in Unity, " +
                "or execute Hecton8.Editor.LoreSystemsBootstrapUtility.BootstrapProductionWorldSceneBatch in Unity batchmode, before claiming runtime integration. " +
                "Resolved through the AssetDatabase dependency graph; this reports SCENE AUTHORING only and does not cover an owner created at runtime by AddComponent.");
        }

        private static void RequireSceneScriptReference(string scriptPath, string label, List<string> missing)
        {
            string guid = AssetDatabase.AssetPathToGUID(scriptPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                missing.Add(label + " (script asset itself is missing)");
                return;
            }

            if (ProductionSceneSerializesScript(scriptPath))
                return;

            missing.Add(label);
        }

        private static void ValidateQuestIdentity(
            QuestData quest,
            string assetPath,
            string expectedQuestId,
            List<string> failures)
        {
            if (quest == null)
            {
                failures.Add($"{assetPath} missing QuestData");
                return;
            }

            if (!string.Equals(quest.questId, expectedQuestId, StringComparison.Ordinal))
                failures.Add($"{assetPath} questId must be '{expectedQuestId}'");
        }

        private static bool HasPrerequisiteQuestId(QuestData quest, string prerequisiteId)
        {
            string[] prerequisites = quest != null ? quest.prerequisiteQuestIds : null;
            if (prerequisites == null)
                return false;

            for (int i = 0; i < prerequisites.Length; i++)
            {
                if (string.Equals(prerequisites[i], prerequisiteId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void ValidateFirstHourFabricatorSceneRoute(ValidationResult result)
        {
            string fabricatorScriptGuid = AssetDatabase.AssetPathToGUID(FabricatorScriptPath);
            string drillRecipeGuid = AssetDatabase.AssetPathToGUID(SeafloorDrillRecipePath);
            string glassPanelRecipeGuid = AssetDatabase.AssetPathToGUID(GlassPanelRecipePath);
            string fiberMeshRecipeGuid = AssetDatabase.AssetPathToGUID(FiberMeshRecipePath);
            string hologramMaterialGuid = AssetDatabase.AssetPathToGUID(AssemblyHologramMaterialPath);
            // This gate needs the serialized property block - object name, m_Layer, the socket id
            // string, the BoxCollider marker, field assignments. No dependency edge carries any of
            // those, so unlike the presence gates above it genuinely cannot leave the text route.
            // What it CAN do is stop pretending: when the scene is not YAML the failure is now
            // reported as a structural limit of the instrument, with the command that answers the
            // question, instead of as "the Forward_Fabricator object is missing".
            bool sceneTextAvailable = TryReadProductionSceneYaml(out string sceneText, out string sceneReadFailure);
            if (!sceneTextAvailable)
            {
                bool fabricatorPresent = ProductionSceneSerializesScript(FabricatorScriptPath);
                result.FirstHourDrillRouteErrorCount++;
                result.Errors.Add(
                    $"{ProductionWorldScenePath}: first-hour fabricator route is UNVERIFIED, not proven absent. {sceneReadFailure} " +
                    $"Dependency-graph cross-check: the scene {(fabricatorPresent ? "DOES" : "does NOT")} serialize {FabricatorScriptPath}, " +
                    "which covers component presence only and cannot confirm layer, socket id, collider, or recipe assignments.");
                return;
            }

            if (!TryExtractSceneObjectBlockByName(sceneText, ForwardFabricatorObjectName, out string fabricatorBlock))
            {
                result.FirstHourDrillRouteErrorCount++;
                result.Errors.Add(
                    $"{ProductionWorldScenePath}: missing '{ForwardFabricatorObjectName}' scene object. First-hour crafting cannot reach the seafloor drill route.");
                return;
            }

            List<string> missing = new List<string>(6);
            if (fabricatorBlock.IndexOf($"m_Name: {ForwardFabricatorObjectName}", StringComparison.Ordinal) < 0)
                missing.Add("named scene object");
            if (fabricatorBlock.IndexOf("m_Layer: 3", StringComparison.Ordinal) < 0)
                missing.Add("Interactable layer");
            if (fabricatorBlock.IndexOf(ForwardFabricatorSocketId, StringComparison.Ordinal) < 0)
                missing.Add("fabrication socket id");
            if (fabricatorBlock.IndexOf("--- !u!65", StringComparison.Ordinal) < 0)
                missing.Add("BoxCollider");
            if (string.IsNullOrWhiteSpace(fabricatorScriptGuid) ||
                fabricatorBlock.IndexOf(fabricatorScriptGuid, StringComparison.OrdinalIgnoreCase) < 0)
            {
                missing.Add("Fabricator component");
            }
            if (string.IsNullOrWhiteSpace(drillRecipeGuid) ||
                fabricatorBlock.IndexOf(drillRecipeGuid, StringComparison.OrdinalIgnoreCase) < 0)
            {
                missing.Add("seafloor drill recipe ref");
            }
            if (fabricatorBlock.IndexOf("assemblyFallbackMesh:", StringComparison.Ordinal) < 0 ||
                fabricatorBlock.IndexOf("assemblyFallbackMesh: {fileID: 0}", StringComparison.Ordinal) >= 0)
            {
                missing.Add("assembly fallback mesh ref");
            }
            if (fabricatorBlock.IndexOf("assemblyPreviewMeshFilter:", StringComparison.Ordinal) < 0 ||
                fabricatorBlock.IndexOf("assemblyPreviewMeshFilter: {fileID: 0}", StringComparison.Ordinal) >= 0)
            {
                missing.Add("assembly preview mesh filter ref");
            }
            else
            {
                ValidateSceneFileIdReference(
                    sceneText,
                    fabricatorBlock,
                    "assemblyPreviewMeshFilter",
                    "33",
                    "assembly preview MeshFilter object",
                    missing);
            }
            if (fabricatorBlock.IndexOf("assemblyPreviewRenderer:", StringComparison.Ordinal) < 0 ||
                fabricatorBlock.IndexOf("assemblyPreviewRenderer: {fileID: 0}", StringComparison.Ordinal) >= 0)
            {
                missing.Add("assembly preview renderer ref");
            }
            else
            {
                ValidateSceneFileIdReference(
                    sceneText,
                    fabricatorBlock,
                    "assemblyPreviewRenderer",
                    "23",
                    "assembly preview MeshRenderer object",
                    missing);
            }
            if (string.IsNullOrWhiteSpace(hologramMaterialGuid) ||
                fabricatorBlock.IndexOf(hologramMaterialGuid, StringComparison.OrdinalIgnoreCase) < 0)
            {
                missing.Add("assembly hologram material ref");
            }
            if (fabricatorBlock.IndexOf("outputSocket:", StringComparison.Ordinal) < 0 ||
                fabricatorBlock.IndexOf("outputSocket: {fileID: 0}", StringComparison.Ordinal) >= 0)
            {
                missing.Add("craft output socket ref");
            }
            else
            {
                ValidateSceneFileIdReference(
                    sceneText,
                    fabricatorBlock,
                    "outputSocket",
                    "4",
                    "craft output Transform object",
                    missing);
            }
            if (fabricatorBlock.IndexOf("deconstructOutputSocket:", StringComparison.Ordinal) < 0 ||
                fabricatorBlock.IndexOf("deconstructOutputSocket: {fileID: 0}", StringComparison.Ordinal) >= 0)
            {
                missing.Add("deconstruct output socket ref");
            }
            else
            {
                ValidateSceneFileIdReference(
                    sceneText,
                    fabricatorBlock,
                    "deconstructOutputSocket",
                    "4",
                    "deconstruct output Transform object",
                    missing);
            }
            if (string.IsNullOrWhiteSpace(glassPanelRecipeGuid) ||
                fabricatorBlock.IndexOf(glassPanelRecipeGuid, StringComparison.OrdinalIgnoreCase) < 0)
            {
                missing.Add("glass panel recipe ref");
            }
            if (string.IsNullOrWhiteSpace(fiberMeshRecipeGuid) ||
                fabricatorBlock.IndexOf(fiberMeshRecipeGuid, StringComparison.OrdinalIgnoreCase) < 0)
            {
                missing.Add("fiber mesh recipe ref");
            }

            if (missing.Count <= 0)
                return;

            result.FirstHourDrillRouteErrorCount++;
            result.Errors.Add(
                $"{ProductionWorldScenePath}: '{ForwardFabricatorObjectName}' is not a complete first-hour fabricator route; " +
                $"missing {string.Join(", ", missing)}.");
        }

        private static bool TryExtractSceneObjectBlockByName(string sceneText, string objectName, out string block)
        {
            block = string.Empty;
            if (string.IsNullOrWhiteSpace(sceneText) || string.IsNullOrWhiteSpace(objectName))
                return false;

            int nameIndex = sceneText.IndexOf($"m_Name: {objectName}", StringComparison.Ordinal);
            if (nameIndex < 0)
                return false;

            int blockStart = sceneText.LastIndexOf("\n--- !u!1 &", nameIndex, StringComparison.Ordinal);
            if (blockStart < 0)
                blockStart = sceneText.LastIndexOf("--- !u!1 &", nameIndex, StringComparison.Ordinal);
            if (blockStart < 0)
                return false;

            int nextBlockStart = sceneText.IndexOf("\n--- !u!1 &", nameIndex + objectName.Length, StringComparison.Ordinal);
            if (nextBlockStart < 0)
                nextBlockStart = sceneText.Length;

            block = sceneText.Substring(blockStart, nextBlockStart - blockStart);
            return true;
        }

        private static void ValidateSceneFileIdReference(
            string sceneText,
            string ownerBlock,
            string propertyName,
            string unityClassId,
            string label,
            List<string> missing)
        {
            if (!TryExtractSceneFileIdReference(ownerBlock, propertyName, out string fileId) ||
                string.Equals(fileId, "0", StringComparison.Ordinal))
            {
                return;
            }

            if (!SceneTextHasUnityObject(sceneText, unityClassId, fileId))
                missing.Add(label);
        }

        private static bool TryExtractSceneFileIdReference(string ownerBlock, string propertyName, out string fileId)
        {
            fileId = string.Empty;
            if (string.IsNullOrWhiteSpace(ownerBlock) || string.IsNullOrWhiteSpace(propertyName))
                return false;

            int valueStartIndex = FindFileIdValueStartIndex(ownerBlock, propertyName);
            if (valueStartIndex < 0)
                return false;

            return TryParseFileIdValue(ownerBlock, valueStartIndex, out fileId);
        }

        private static int FindMarkerIndex(string text, string propertyName, out int markerLength)
        {
            string marker = propertyName + ": {fileID:";
            markerLength = marker.Length;
            return text.IndexOf(marker, StringComparison.Ordinal);
        }

        private static int SkipWhitespace(string text, int startIndex)
        {
            int cursor = startIndex;
            while (cursor < text.Length && text[cursor] == ' ')
                cursor++;
            return cursor;
        }

        private static int FindFileIdValueStartIndex(string ownerBlock, string propertyName)
        {
            int markerIndex = FindMarkerIndex(ownerBlock, propertyName, out int markerLength);
            if (markerIndex < 0)
                return -1;

            return SkipWhitespace(ownerBlock, markerIndex + markerLength);
        }

        private static bool TryParseFileIdValue(string ownerBlock, int startIndex, out string fileId)
        {
            fileId = string.Empty;
            int cursor = startIndex;
            while (cursor < ownerBlock.Length &&
                   (ownerBlock[cursor] == '-' || char.IsDigit(ownerBlock[cursor])))
            {
                cursor++;
            }

            if (cursor <= startIndex)
                return false;

            fileId = ownerBlock.Substring(startIndex, cursor - startIndex);
            return true;
        }

        private static bool SceneTextHasUnityObject(string sceneText, string unityClassId, string fileId)
        {
            if (string.IsNullOrWhiteSpace(sceneText) ||
                string.IsNullOrWhiteSpace(unityClassId) ||
                string.IsNullOrWhiteSpace(fileId))
            {
                return false;
            }

            string marker = $"--- !u!{unityClassId} &{fileId}";
            return sceneText.StartsWith(marker, StringComparison.Ordinal) ||
                   sceneText.IndexOf("\n" + marker, StringComparison.Ordinal) >= 0;
        }

        private static bool RecipeResultMatchesPersistentId(RecipeData recipe, string persistentId)
        {
            if (recipe == null || recipe.resultItem == null || string.IsNullOrWhiteSpace(persistentId))
                return false;

            return string.Equals(recipe.resultItem.PersistentId, persistentId, StringComparison.Ordinal);
        }

        private static bool RecipeUsesItemPersistentId(RecipeData recipe, string persistentId)
        {
            if (recipe == null || recipe.ingredients == null || string.IsNullOrWhiteSpace(persistentId))
                return false;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null)
                    continue;

                if (string.Equals(cost.item.PersistentId, persistentId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasFirstHourDrillIngredientRoute(RecipeData drillRecipe, out string failure)
        {
            failure = string.Empty;
            List<string> failures = new List<string>(8);
            if (drillRecipe == null)
            {
                failure = "drill recipe missing";
                return false;
            }

            RequireRecipeIngredient(drillRecipe, SeafloorDrillRecipePath, GlassPanelItemId, failures);
            RequireRecipeIngredient(drillRecipe, SeafloorDrillRecipePath, FiberMeshItemId, failures);
            ValidateFirstHourDrillRecipeIngredientSet(drillRecipe, failures);

            ValidateEarlyComponentRecipe(GlassPanelRecipePath, GlassPanelItemId, SilicaShardsItemId, failures);
            ValidateEarlyComponentRecipe(FiberMeshRecipePath, FiberMeshItemId, FiberKelpItemId, failures);

            ValidatePreDrillResourceNode(SilicaShardClusterTemplatePath, SilicaShardsItemId, failures);
            ValidatePreDrillResourceNode(FiberKelpStandTemplatePath, FiberKelpItemId, failures);

            if (failures.Count <= 0)
                return true;

            failure = string.Join("; ", failures);
            return false;
        }

        private static void ValidateFirstHourDrillRecipeIngredientSet(
            RecipeData recipe,
            List<string> failures)
        {
            if (recipe == null || recipe.ingredients == null)
                return;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                ItemData item = cost.item;
                string persistentId = item != null ? item.PersistentId : null;
                if (item == null || string.IsNullOrWhiteSpace(persistentId))
                {
                    failures.Add($"{SeafloorDrillRecipePath} ingredient[{i}] has no valid ItemData");
                    continue;
                }

                if (cost.amount <= 0)
                    failures.Add($"{SeafloorDrillRecipePath} ingredient '{persistentId}' must have amount > 0");

                if (!string.Equals(persistentId, GlassPanelItemId, StringComparison.Ordinal) &&
                    !string.Equals(persistentId, FiberMeshItemId, StringComparison.Ordinal))
                {
                    failures.Add(
                        $"{SeafloorDrillRecipePath} ingredient '{persistentId}' is not proven reachable in the pre-drill safe-depth route");
                }
            }
        }

        private static void RequireRecipeIngredient(
            RecipeData recipe,
            string recipePath,
            string persistentId,
            List<string> failures)
        {
            if (RecipeUsesItemPersistentId(recipe, persistentId))
                return;

            failures.Add($"{recipePath} missing ingredient '{persistentId}'");
        }

        private static void ValidateEarlyComponentRecipe(
            string recipePath,
            string resultItemId,
            string rawIngredientItemId,
            List<string> failures)
        {
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(recipePath);
            if (!RecipeResultMatchesPersistentId(recipe, resultItemId))
            {
                failures.Add($"{recipePath} must produce '{resultItemId}'");
                return;
            }

            if (recipe.RequiresScanUnlock)
                failures.Add($"{recipePath} must not require a scan gate before the drill route opens");

            if (!RecipeUsesItemPersistentId(recipe, rawIngredientItemId))
                failures.Add($"{recipePath} missing raw ingredient '{rawIngredientItemId}'");

            if (RecipeUsesItemPersistentId(recipe, CopperItemId) || RecipeUsesItemPersistentId(recipe, CopperWireItemId))
                failures.Add($"{recipePath} must not require copper/copper wire before the copper drill gate opens");
        }

        private static void ValidatePreDrillResourceNode(
            string templatePath,
            string yieldedItemId,
            List<string> failures)
        {
            ResourceNodeTemplate template = AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(templatePath);
            if (template == null)
            {
                failures.Add($"{templatePath} missing ResourceNodeTemplate");
                return;
            }

            if (template.RequiredToolClass == ResourceNodeTemplate.HarvestToolClass.Drill)
                failures.Add($"{templatePath} must not be Drill-gated before the drill route opens");

            SurvivalStats starterStats = AssetDatabase.LoadAssetAtPath<SurvivalStats>(StarterSurvivalStatsPath);
            if (starterStats == null)
            {
                failures.Add($"{StarterSurvivalStatsPath} missing starter SurvivalStats; cannot prove pre-drill safe-depth resource access");
            }
            else if (template.MinimumDepthMeters > starterStats.SafeDepth)
            {
                failures.Add(
                    $"{templatePath} starts at {template.MinimumDepthMeters:0.#}m, beyond starter safe depth {starterStats.SafeDepth:0.#}m; " +
                    "pre-drill ingredient resources must be reachable before pressure attrition");
            }

            if (!ResourceNodeTemplateYieldsPersistentId(template, yieldedItemId))
                failures.Add($"{templatePath} must yield '{yieldedItemId}'");

            if (template.DefaultLootCount <= 0)
                failures.Add($"{templatePath} must have DefaultLootCount > 0 so depletion cannot tombstone before first-hour yield");

            if (template.LootPickupPrefab == null)
                failures.Add($"{templatePath} must keep a loot pickup prefab for first-hour world-item contract proof");

            if (template.ExtractorYieldItem == null ||
                !string.Equals(template.ExtractorYieldItem.PersistentId, yieldedItemId, StringComparison.Ordinal))
            {
                failures.Add($"{templatePath} extractor/fallback yield item must resolve to '{yieldedItemId}'");
            }
        }

        private static bool ResourceNodeTemplateYieldsPersistentId(ResourceNodeTemplate template, string persistentId)
        {
            if (template == null || string.IsNullOrWhiteSpace(persistentId))
                return false;

            SerializedObject serializedTemplate = new SerializedObject(template);
            SerializedProperty yieldProperty = serializedTemplate.FindProperty("harvestYield");
            if (yieldProperty == null || !yieldProperty.isArray)
                return false;

            for (int i = 0; i < yieldProperty.arraySize; i++)
            {
                SerializedProperty entryProperty = yieldProperty.GetArrayElementAtIndex(i);
                SerializedProperty itemProperty = entryProperty != null ? entryProperty.FindPropertyRelative("item") : null;
                if (itemProperty == null || !(itemProperty.objectReferenceValue is ItemData item))
                    continue;

                if (string.Equals(item.PersistentId, persistentId, StringComparison.Ordinal))
                    return true;
            }

            return false;
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

            int itemHash = ItemData.ResolvePersistentHashId(item);
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

        private static void ValidatePdaLoadoutPresetReferences(ValidationResult result)
        {
            string absolutePath = ProjectAssetPathToAbsolutePath(PdaLoadoutTabScriptPath);
            if (!File.Exists(absolutePath))
            {
                result.PdaLoadoutPresetPathErrorCount++;
                result.Errors.Add($"{PdaLoadoutTabScriptPath}: script missing; PDA loadout preset auto-resolve references cannot be validated.");
                return;
            }

            string sourceText;
            try
            {
                sourceText = File.ReadAllText(absolutePath);
            }
            catch (Exception exception)
            {
                result.PdaLoadoutPresetPathErrorCount++;
                result.Errors.Add($"{PdaLoadoutTabScriptPath}: failed to read script for loadout preset reference validation: {exception.Message}");
                return;
            }

            HashSet<string> referencedPresetPaths = new HashSet<string>(StringComparer.Ordinal);
            int cursor = 0;
            while (cursor < sourceText.Length)
            {
                int start = sourceText.IndexOf(PdaLoadoutPresetPathPrefix, cursor, StringComparison.Ordinal);
                if (start < 0)
                    break;

                int end = sourceText.IndexOf(".asset", start, StringComparison.Ordinal);
                if (end < 0)
                {
                    result.PdaLoadoutPresetPathErrorCount++;
                    result.Errors.Add($"{PdaLoadoutTabScriptPath}: malformed PDA loadout preset path starting near character {start}.");
                    break;
                }

                end += ".asset".Length;
                string assetPath = sourceText.Substring(start, end - start).Replace('\\', '/');
                cursor = end;
                if (!referencedPresetPaths.Add(assetPath))
                    continue;

                ToolLoadoutPreset preset = AssetDatabase.LoadAssetAtPath<ToolLoadoutPreset>(assetPath);
                if (preset != null)
                    continue;

                result.PdaLoadoutPresetPathErrorCount++;
                result.Errors.Add($"{PdaLoadoutTabScriptPath}: PDA loadout preset reference does not resolve to ToolLoadoutPreset -> {assetPath}");
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

                if (prefabRoot.GetComponentInChildren<ScanLogSystem>(true) == null)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: canonical player prefab is missing ScanLogSystem; '{GenericResourceScanEntryId}' cannot unlock first-hour crafting.");
                }

                SerializedObject serializedToolManager = new SerializedObject(toolManager);
                SerializedProperty grantProperty = serializedToolManager.FindProperty("grantAssignedToolItemsOnRuntimeStart");
                SerializedProperty budgetProperty = serializedToolManager.FindProperty("runtimeStartToolGrantBudget");
                SerializedProperty fieldLoadoutAdviceMaskProperty = serializedToolManager.FindProperty("fieldLoadoutAdviceMask");
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

                ValidatePlayerFieldLoadoutAdviceMask(result, fieldLoadoutAdviceMaskProperty);

                if (toolPrefabsProperty == null || !toolPrefabsProperty.isArray)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: PlayerToolManager.toolPrefabs array is missing; starter quick slots cannot be validated.");
                    return;
                }

                ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
                int assignedCount = 0;
                int validItemCount = 0;
                bool hasStarterScanner = false;
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

                    int itemHash = ItemData.ResolvePersistentHashId(item);
                    if (itemHash == 0 || itemCatalog == null || !ReferenceEquals(itemCatalog.FindByHash(itemHash), item))
                    {
                        result.PlayerStarterLoadoutErrorCount++;
                        result.Errors.Add($"{PlayerPrefabPath}: starter tool slot {i} item '{item.PersistentId}' is not the active ItemCatalog entry.");
                        continue;
                    }

                    validItemCount++;
                    if (string.Equals(item.PersistentId, ScannerToolItemId, StringComparison.Ordinal))
                        hasStarterScanner = true;
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

                if (!hasStarterScanner)
                {
                    result.PlayerStarterLoadoutErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: production starter loadout must include '{ScannerToolItemId}' because the first-hour drill recipe is gated by '{GenericResourceScanEntryId}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidatePlayerFieldLoadoutAdviceMask(
            ValidationResult result,
            SerializedProperty fieldLoadoutAdviceMaskProperty)
        {
            if (fieldLoadoutAdviceMaskProperty == null ||
                fieldLoadoutAdviceMaskProperty.propertyType != SerializedPropertyType.LayerMask)
            {
                result.PlayerStarterLoadoutErrorCount++;
                result.Errors.Add($"{PlayerPrefabPath}: PlayerToolManager.fieldLoadoutAdviceMask is missing or no longer serialized as a LayerMask.");
                return;
            }

            int authoredMask = fieldLoadoutAdviceMaskProperty.intValue;
            int resolvedMask = HectonLayerMasks.ResolveFieldToolScanLayerMask(authoredMask);
            if (authoredMask == HectonLayerMasks.FieldToolScanLayerMask &&
                resolvedMask == HectonLayerMasks.FieldToolScanLayerMask)
            {
                return;
            }

            result.PlayerStarterLoadoutErrorCount++;
            result.Errors.Add(
                $"{PlayerPrefabPath}: PlayerToolManager.fieldLoadoutAdviceMask must be FieldToolScanLayerMask " +
                $"for production loadout advice. Current mask={authoredMask}, resolved={resolvedMask}, expected={HectonLayerMasks.FieldToolScanLayerMask}.");
        }

        private static void ValidatePlayerSurfaceProbeAuthoring(ValidationResult result)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                result.PlayerSurfaceProbeAuthoringErrorCount++;
                result.Errors.Add($"{PlayerPrefabPath}: failed to load prefab contents for player surface-probe validation.");
                return;
            }

            try
            {
                HectonPlayerMovement movement = prefabRoot.GetComponentInChildren<HectonPlayerMovement>(true);
                if (movement == null)
                {
                    result.PlayerSurfaceProbeAuthoringErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: canonical player prefab is missing HectonPlayerMovement; terrain/SDF movement probes cannot be validated.");
                }
                else
                {
                    SerializedObject serializedMovement = new SerializedObject(movement);
                    ValidateTerrainSdfProbeMask(
                        result,
                        serializedMovement,
                        "groundLayers",
                        $"{PlayerPrefabPath}: HectonPlayerMovement.groundLayers");
                }

                Hecton8.Physics.BuoyancyObject buoyancy = prefabRoot.GetComponentInChildren<Hecton8.Physics.BuoyancyObject>(true);
                if (buoyancy == null)
                {
                    result.PlayerSurfaceProbeAuthoringErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: canonical player prefab is missing BuoyancyObject; terrain/SDF buoyancy ground suppression cannot be validated.");
                }
                else
                {
                    SerializedObject serializedBuoyancy = new SerializedObject(buoyancy);
                    ValidateTerrainSdfProbeMask(
                        result,
                        serializedBuoyancy,
                        "groundLayers",
                        $"{PlayerPrefabPath}: BuoyancyObject.groundLayers");
                }

                PlayerFootstepAudio footstepAudio = prefabRoot.GetComponentInChildren<PlayerFootstepAudio>(true);
                if (footstepAudio == null)
                {
                    result.PlayerSurfaceProbeAuthoringErrorCount++;
                    result.Errors.Add($"{PlayerPrefabPath}: canonical player prefab is missing PlayerFootstepAudio; terrain/SDF surface audio probes cannot be validated.");
                    return;
                }

                SerializedObject serializedFootsteps = new SerializedObject(footstepAudio);
                ValidateTerrainSdfProbeMask(
                    result,
                    serializedFootsteps,
                    "surfaceLayers",
                    $"{PlayerPrefabPath}: PlayerFootstepAudio.surfaceLayers");
                SerializedProperty terrainLayerIndexProperty = serializedFootsteps.FindProperty("terrainLayerIndex");
                if (terrainLayerIndexProperty == null ||
                    terrainLayerIndexProperty.propertyType != SerializedPropertyType.Integer ||
                    terrainLayerIndexProperty.intValue != HectonLayerMasks.Terrain)
                {
                    result.PlayerSurfaceProbeAuthoringErrorCount++;
                    int currentValue = terrainLayerIndexProperty != null ? terrainLayerIndexProperty.intValue : -1;
                    result.Errors.Add(
                        $"{PlayerPrefabPath}: PlayerFootstepAudio.terrainLayerIndex must match HectonLayerMasks.Terrain. " +
                        $"Current={currentValue}, expected={HectonLayerMasks.Terrain}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidateTerrainSdfProbeMask(
            ValidationResult result,
            SerializedObject serializedObject,
            string propertyName,
            string context)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.LayerMask)
            {
                result.PlayerSurfaceProbeAuthoringErrorCount++;
                result.Errors.Add($"{context} is missing or no longer serialized as a LayerMask.");
                return;
            }

            int authoredMask = property.intValue;
            int resolvedMask = HectonLayerMasks.ResolveTerrainSdfProbeLayerMask(authoredMask);
            bool includesTerrainSdf = (resolvedMask & HectonLayerMasks.TerrainSdfProbeLayerMask) == HectonLayerMasks.TerrainSdfProbeLayerMask;
            if (authoredMask != 0 &&
                authoredMask != HectonLayerMasks.StrictInteractionLayerMask &&
                !HectonLayerMasks.IsEverythingLayerMask(authoredMask) &&
                includesTerrainSdf)
            {
                return;
            }

            result.PlayerSurfaceProbeAuthoringErrorCount++;
            result.Errors.Add(
                $"{context} must author explicit terrain/SDF host layers, not legacy broad/empty masks. " +
                $"Current mask={authoredMask}, resolved={resolvedMask}, requiredBits={HectonLayerMasks.TerrainSdfProbeLayerMask}.");
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

        private static bool PlayerKnownToolPrefabRegistryContainsItemId(string persistentId, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                failure = "empty persistent id";
                return false;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                failure = "player prefab failed to load";
                return false;
            }

            try
            {
                PlayerToolManager toolManager = prefabRoot.GetComponentInChildren<PlayerToolManager>(true);
                if (toolManager == null)
                {
                    failure = "missing PlayerToolManager";
                    return false;
                }

                SerializedObject serializedToolManager = new SerializedObject(toolManager);
                SerializedProperty knownToolPrefabs = serializedToolManager.FindProperty("knownToolPrefabs");
                if (knownToolPrefabs == null || !knownToolPrefabs.isArray)
                {
                    failure = "knownToolPrefabs is missing";
                    return false;
                }

                for (int i = 0; i < knownToolPrefabs.arraySize; i++)
                {
                    SerializedProperty element = knownToolPrefabs.GetArrayElementAtIndex(i);
                    GameObject knownPrefab = element != null ? element.objectReferenceValue as GameObject : null;
                    if (knownPrefab == null || !knownPrefab.TryGetComponent(out PlayerTool tool) || tool == null)
                        continue;

                    ItemData item = tool.ToolData;
                    if (item != null && string.Equals(item.PersistentId, persistentId, StringComparison.Ordinal))
                        return true;
                }

                failure = $"'{persistentId}' not present in PlayerToolManager.knownToolPrefabs";
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
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
                string persistentId = template.PersistentId;
                int expectedTemplateHashId = string.IsNullOrWhiteSpace(persistentId) ? 0 : LocHash.Compute(persistentId);
                if (template.TemplateHashId == 0)
                    result.Errors.Add($"{assetPath}: BaseModuleTemplate.TemplateHashId resolves to 0.");
                else if (template.TemplateHashId != expectedTemplateHashId)
                {
                    result.Errors.Add(
                        $"{assetPath}: BaseModuleTemplate.TemplateHashId '{template.TemplateHashId}' does not match " +
                        $"canonical PersistentId '{persistentId}' hash '{expectedTemplateHashId}'.");
                }

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
                $"ResourceDistributionRouteErrors={result.ResourceDistributionRouteErrorCount}, " +
                $"FirstHourCraftGateErrors={result.FirstHourCraftGateErrorCount}, " +
                $"FirstHourDrillRouteErrors={result.FirstHourDrillRouteErrorCount}, " +
                $"FirstHourOxygenRouteErrors={result.FirstHourOxygenRouteErrorCount}, " +
                $"PlayerPdaHeadlessOpenRisk={result.PlayerPdaHeadlessOpenRiskCount}, " +
                $"PlayerPdaBridgeWarnings={result.PlayerPdaBridgeWarningCount}, " +
                $"PdaLoadoutPresetPathErrors={result.PdaLoadoutPresetPathErrorCount}, " +
                $"PlayerDevProvisionerStartupRisk={result.PlayerDevProvisionerStartupRiskCount}, " +
                $"PlayerStarterLoadoutErrors={result.PlayerStarterLoadoutErrorCount}, " +
                $"PlayerSurfaceProbeAuthoringErrors={result.PlayerSurfaceProbeAuthoringErrorCount}, " +
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
