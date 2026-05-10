using System;
using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class FabricationBootstrapAuthoring
    {
        private const string RecipesFolder = "Assets/_Project/Data/Crafting/Recipes";
        private const string BeaconItemPath = "Assets/_Project/Data/Items/Tools/Item_Tool_BeaconDeployer.asset";
        private const string AnalyzerItemPath = "Assets/_Project/Data/Items/Tools/Item_Tool_EnvAnalyzer.asset";
        private const string SamplerItemPath = "Assets/_Project/Data/Items/Tools/Item_Tool_SalvageSampler.asset";
        private const string FlashlightItemPath = "Assets/_Project/Data/Items/Tools/Item_Tool_Flashlight.asset";
        private const string ScannerItemPath = "Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset";
        private const string RepairItemPath = "Assets/_Project/Data/Items/Tools/Item_Tool_Repair.asset";
        private const string CopperWirePath = "Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset";
        private const string GlassPanelPath = "Assets/_Project/Data/Items/Resources/Components/Comp_GlassPanel.asset";
        private const string FiberMeshPath = "Assets/_Project/Data/Items/Resources/Components/Comp_FiberMesh.asset";
        private const string SealantPackPath = "Assets/_Project/Data/Items/Resources/Components/Comp_SealantPack.asset";
        private const string BatteryCellPath = "Assets/_Project/Data/Items/Resources/Components/Comp_BatteryCell.asset";
        private const string CircuitBoardPath = "Assets/_Project/Data/Items/Resources/Components/Comp_CircuitBoard.asset";
        private const string SensorPackagePath = "Assets/_Project/Data/Items/Resources/Components/Comp_SensorPackage.asset";
        private const string BeaconCorePath = "Assets/_Project/Data/Items/Resources/Components/Comp_BeaconCore.asset";
        private const string RecipeCopperWirePath = "Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset";
        private const string RecipeGlassPanelPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_GlassPanel.asset";
        private const string RecipeFiberMeshPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_FiberMesh.asset";
        private const string RecipeSealantPackPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_SealantPack.asset";
        private const string RecipeBatteryCellPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_BatteryCell.asset";
        private const string RecipeCircuitBoardPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_CircuitBoard.asset";
        private const string RecipeSensorPackagePath = "Assets/_Project/Data/Crafting/Recipes/Recipe_SensorPackage.asset";
        private const string RecipeBeaconCorePath = "Assets/_Project/Data/Crafting/Recipes/Recipe_BeaconCore.asset";
        private const string RecipeStructuralBracketPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_StructuralBracket.asset";
        private const string RecipePumpRotorPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_PumpRotor.asset";
        private const string RecipeEmergencyO2CanisterPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_EmergencyO2Canister.asset";
        private const string RecipeFieldMedGelPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_FieldMedGel.asset";
        private const string RecipeElectrolyteAmpoulePath = "Assets/_Project/Data/Crafting/Recipes/Recipe_ElectrolyteAmpoule.asset";
        private const string RecipePowerCouplerPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_PowerCoupler.asset";

        private const string TrialRootName = "Fabrication_Trial";
        private const string TrialFabricatorName = "Trial_Fabricator";

        private const string WorldRootName = "--- WORLD ---";
        private const string OutpostRootName = "Fabrication_Outpost";
        private const string OutpostFabricatorName = "Forward_Fabricator";

        [MenuItem("Hecton/Authoring/Rebuild Starter Fabrication Kit", priority = 170)]
        public static void RebuildStarterFabricationKit()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Crafting");
            EnsureFolder(RecipesFolder);

            ItemData beacon = AssetDatabase.LoadAssetAtPath<ItemData>(BeaconItemPath);
            ItemData analyzer = AssetDatabase.LoadAssetAtPath<ItemData>(AnalyzerItemPath);
            ItemData sampler = AssetDatabase.LoadAssetAtPath<ItemData>(SamplerItemPath);
            ItemData flashlight = AssetDatabase.LoadAssetAtPath<ItemData>(FlashlightItemPath);
            ItemData scanner = AssetDatabase.LoadAssetAtPath<ItemData>(ScannerItemPath);
            ItemData repair = AssetDatabase.LoadAssetAtPath<ItemData>(RepairItemPath);
            ItemData copperWire = AssetDatabase.LoadAssetAtPath<ItemData>(CopperWirePath);
            ItemData glassPanel = AssetDatabase.LoadAssetAtPath<ItemData>(GlassPanelPath);
            ItemData fiberMesh = AssetDatabase.LoadAssetAtPath<ItemData>(FiberMeshPath);
            ItemData sealantPack = AssetDatabase.LoadAssetAtPath<ItemData>(SealantPackPath);
            ItemData batteryCell = AssetDatabase.LoadAssetAtPath<ItemData>(BatteryCellPath);
            ItemData circuitBoard = AssetDatabase.LoadAssetAtPath<ItemData>(CircuitBoardPath);
            ItemData sensorPackage = AssetDatabase.LoadAssetAtPath<ItemData>(SensorPackagePath);
            ItemData beaconCore = AssetDatabase.LoadAssetAtPath<ItemData>(BeaconCorePath);
            RecipeData copperWireRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeCopperWirePath);
            RecipeData glassPanelRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeGlassPanelPath);
            RecipeData fiberMeshRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeFiberMeshPath);
            RecipeData sealantPackRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeSealantPackPath);
            RecipeData batteryCellRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeBatteryCellPath);
            RecipeData circuitBoardRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeCircuitBoardPath);
            RecipeData sensorPackageRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeSensorPackagePath);
            RecipeData beaconCoreRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeBeaconCorePath);
            RecipeData structuralBracketRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeStructuralBracketPath);
            RecipeData pumpRotorRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipePumpRotorPath);
            RecipeData emergencyO2Recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeEmergencyO2CanisterPath);
            RecipeData fieldMedGelRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeFieldMedGelPath);
            RecipeData electrolyteAmpouleRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeElectrolyteAmpoulePath);
            RecipeData powerCouplerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipePowerCouplerPath);

            if (beacon == null || analyzer == null || sampler == null ||
                flashlight == null || scanner == null || repair == null ||
                copperWire == null || glassPanel == null || fiberMesh == null || sealantPack == null ||
                batteryCell == null || circuitBoard == null || sensorPackage == null || beaconCore == null ||
                copperWireRecipe == null || glassPanelRecipe == null || fiberMeshRecipe == null || sealantPackRecipe == null ||
                batteryCellRecipe == null || circuitBoardRecipe == null || sensorPackageRecipe == null || beaconCoreRecipe == null ||
                structuralBracketRecipe == null || pumpRotorRecipe == null ||
                emergencyO2Recipe == null || fieldMedGelRecipe == null || electrolyteAmpouleRecipe == null ||
                powerCouplerRecipe == null)
            {
                Debug.LogError("[FabricationBootstrap] Missing required ItemData assets. Starter fabrication kit was not rebuilt.");
                return;
            }

            RecipeData beaconRecipe = CreateOrUpdateRecipe(
                "Recipe_FieldBeacon.asset",
                "Field Beacon",
                "Compact route beacon package for long return paths and deep navigation.",
                "scan.structure_relay",
                FabricationGroup.Tools,
                beacon,
                1,
                new InventoryCost { item = beaconCore, amount = 1 },
                new InventoryCost { item = copperWire, amount = 1 });

            RecipeData analyzerRecipe = CreateOrUpdateRecipe(
                "Recipe_EnvAnalyzer.asset",
                "Environmental Analyzer",
                "Portable survey package tuned for expedition telemetry and hazard classification.",
                "scan.expedition_contact",
                FabricationGroup.Tools,
                analyzer,
                1,
                new InventoryCost { item = sensorPackage, amount = 1 },
                new InventoryCost { item = circuitBoard, amount = 1 });

            RecipeData samplerRecipe = CreateOrUpdateRecipe(
                "Recipe_SalvageSampler.asset",
                "Salvage Sampler",
                "Field recovery sampler for breaking down caches and weakened resource targets.",
                "scan.resource_cache",
                FabricationGroup.Tools,
                sampler,
                1,
                new InventoryCost { item = sealantPack, amount = 1 },
                new InventoryCost { item = sensorPackage, amount = 1 });

            RecipeData flashlightRecipe = CreateOrUpdateRecipe(
                "Recipe_Flashlight.asset",
                "Dive Flashlight",
                "Portable beam unit for dark trenches, search sweeps, and distant hazard reads.",
                "scan.resource_node",
                FabricationGroup.Tools,
                flashlight,
                1,
                new InventoryCost { item = glassPanel, amount = 1 },
                new InventoryCost { item = batteryCell, amount = 1 });

            RecipeData scannerRecipe = CreateOrUpdateRecipe(
                "Recipe_Scanner.asset",
                "Survey Scanner",
                "Acoustic pulse scanner for route planning, structure checks, and contact sweeps.",
                "scan.expedition_contact",
                FabricationGroup.Tools,
                scanner,
                1,
                new InventoryCost { item = sensorPackage, amount = 1 },
                new InventoryCost { item = copperWire, amount = 1 });

            RecipeData repairRecipe = CreateOrUpdateRecipe(
                "Recipe_RepairTool.asset",
                "Repair Tool",
                "Field maintenance head for sealed modules, hull fractures, and flooded systems.",
                "scan.structure_relay",
                FabricationGroup.Tools,
                repair,
                1,
                new InventoryCost { item = sealantPack, amount = 1 },
                new InventoryCost { item = batteryCell, amount = 1 },
                new InventoryCost { item = fiberMesh, amount = 1 });

            RecipeData[] starterRecipes =
            {
                copperWireRecipe,
                glassPanelRecipe,
                fiberMeshRecipe,
                sealantPackRecipe,
                batteryCellRecipe,
                circuitBoardRecipe,
                sensorPackageRecipe,
                beaconCoreRecipe,
                structuralBracketRecipe,
                pumpRotorRecipe,
                powerCouplerRecipe,
                emergencyO2Recipe,
                fieldMedGelRecipe,
                electrolyteAmpouleRecipe,
                beaconRecipe,
                analyzerRecipe,
                samplerRecipe,
                flashlightRecipe,
                scannerRecipe,
                repairRecipe
            };

            CreateOrUpdateSceneFabricator(
                TrialRootName,
                null,
                TrialFabricatorName,
                new Vector3(8f, 0.9f, 124f),
                new Vector3(1.4f, 1.8f, 1.2f),
                starterRecipes,
                "Polevoy verstak");

            CreateOrUpdateSceneFabricator(
                OutpostRootName,
                WorldRootName,
                OutpostFabricatorName,
                new Vector3(84f, 4900.8f, 1662f),
                new Vector3(1.6f, 2.0f, 1.35f),
                starterRecipes,
                "Forpost-fabrikator");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[FabricationBootstrap] Starter fabrication recipes and fabricator stations rebuilt.");
        }

        [MenuItem("Hecton/Validation/Validate Starter Fabrication Kit", priority = 171)]
        public static void ValidateStarterFabricationKit()
        {
            int errors = 0;

            RecipeData beaconRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_FieldBeacon.asset");
            RecipeData analyzerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_EnvAnalyzer.asset");
            RecipeData samplerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_SalvageSampler.asset");
            RecipeData flashlightRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_Flashlight.asset");
            RecipeData scannerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_Scanner.asset");
            RecipeData repairRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_RepairTool.asset");
            RecipeData structuralBracketRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_StructuralBracket.asset");
            RecipeData pumpRotorRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_PumpRotor.asset");
            RecipeData emergencyO2Recipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_EmergencyO2Canister.asset");
            RecipeData fieldMedGelRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_FieldMedGel.asset");
            RecipeData electrolyteAmpouleRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_ElectrolyteAmpoule.asset");
            RecipeData powerCouplerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_PowerCoupler.asset");

            ValidateRecipe(beaconRecipe, "scan.structure_relay", ref errors);
            ValidateRecipe(analyzerRecipe, "scan.expedition_contact", ref errors);
            ValidateRecipe(samplerRecipe, "scan.resource_cache", ref errors);
            ValidateRecipe(flashlightRecipe, "scan.resource_node", ref errors);
            ValidateRecipe(scannerRecipe, "scan.expedition_contact", ref errors);
            ValidateRecipe(repairRecipe, "scan.structure_relay", ref errors);
            ValidateRecipe(structuralBracketRecipe, string.Empty, ref errors);
            ValidateRecipe(pumpRotorRecipe, string.Empty, ref errors);
            ValidateRecipe(emergencyO2Recipe, string.Empty, ref errors);
            ValidateRecipe(fieldMedGelRecipe, string.Empty, ref errors);
            ValidateRecipe(electrolyteAmpouleRecipe, string.Empty, ref errors);
            ValidateRecipe(powerCouplerRecipe, string.Empty, ref errors);

            ValidateSceneFabricator(TrialRootName, TrialFabricatorName, ref errors);
            ValidateSceneFabricator($"{WorldRootName}/{OutpostRootName}", OutpostFabricatorName, ref errors);

            if (errors == 0)
                Debug.Log("[FabricationBootstrap] PASS no issues found.");
            else
                Debug.LogError($"[FabricationBootstrap] FAIL {errors} issue(s) found.");
        }

        private static RecipeData CreateOrUpdateRecipe(
            string fileName,
            string recipeName,
            string description,
            string requiredScanEntryId,
            FabricationGroup fabricationGroup,
            ItemData resultItem,
            int resultQuantity,
            params InventoryCost[] costs)
        {
            string assetPath = $"{RecipesFolder}/{fileName}";
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(assetPath);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeData>();
                AssetDatabase.CreateAsset(recipe, assetPath);
            }

            recipe.recipeName = recipeName;
            recipe.description = description;
            recipe.requiredScanEntryId = requiredScanEntryId;
            recipe.fabricationGroup = fabricationGroup;
            recipe.resultItem = resultItem;
            recipe.resultQuantity = Mathf.Max(1, resultQuantity);
            recipe.craftTime = 2.5f;
            recipe.ingredients = new List<InventoryCost>(costs ?? Array.Empty<InventoryCost>());

            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static void CreateOrUpdateSceneFabricator(
            string rootName,
            string parentName,
            string fabricatorName,
            Vector3 worldPosition,
            Vector3 localScale,
            RecipeData[] recipes,
            string displayName)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            string rootPath = string.IsNullOrEmpty(parentName) ? rootName : $"{parentName}/{rootName}";
            GameObject root = GameObject.Find(rootPath);
            if (root == null)
            {
                root = new GameObject(rootName);

                if (!string.IsNullOrEmpty(parentName))
                {
                    GameObject parent = GameObject.Find(parentName);
                    if (parent != null)
                        root.transform.SetParent(parent.transform, false);
                }
            }

            GameObject existing = GameObject.Find($"{rootPath}/{fabricatorName}");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cube);
            station.name = fabricatorName;
            station.transform.SetParent(root.transform, false);
            station.transform.position = worldPosition;
            station.transform.localScale = localScale;

            Renderer renderer = station.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            Fabricator fabricator = station.AddComponent<Fabricator>();
            SerializedObject so = new SerializedObject(fabricator);
            SerializedProperty recipesProp = so.FindProperty("availableRecipes");
            recipesProp.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
                recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];

            SerializedProperty nameProp = so.FindProperty("fabricatorName");
            if (nameProp != null)
                nameProp.stringValue = displayName;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fabricator);
        }

        private static void ValidateSceneFabricator(string rootPath, string fabricatorName, ref int errors)
        {
            GameObject root = GameObject.Find(rootPath);
            if (root == null)
            {
                Debug.LogError($"[FabricationBootstrap] Missing root '{rootPath}'.");
                errors++;
                return;
            }

            Transform fabricatorTransform = root.transform.Find(fabricatorName);
            if (fabricatorTransform == null)
            {
                Debug.LogError($"[FabricationBootstrap] Missing '{fabricatorName}' object.");
                errors++;
                return;
            }

            Fabricator fabricator = fabricatorTransform.GetComponent<Fabricator>();
            if (fabricator == null)
            {
                Debug.LogError($"[FabricationBootstrap] {fabricatorName} is missing Fabricator component.", fabricatorTransform.gameObject);
                errors++;
                return;
            }

            if (fabricator.TotalRecipeCount < 10)
            {
                Debug.LogError($"[FabricationBootstrap] {fabricatorName} has incomplete recipe list.", fabricatorTransform.gameObject);
                errors++;
            }
        }

        private static void ValidateRecipe(RecipeData recipe, string expectedScanEntryId, ref int errors)
        {
            if (recipe == null)
            {
                Debug.LogError($"[FabricationBootstrap] Missing recipe asset for scan gate '{expectedScanEntryId}'.");
                errors++;
                return;
            }

            if (recipe.fabricationGroup == FabricationGroup.Unspecified)
            {
                Debug.LogError($"[FabricationBootstrap] Recipe '{recipe.name}' is missing fabrication group.", recipe);
                errors++;
            }

            if (recipe.resultItem == null)
            {
                Debug.LogError($"[FabricationBootstrap] Recipe '{recipe.name}' has no result item.", recipe);
                errors++;
            }

            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
            {
                Debug.LogError($"[FabricationBootstrap] Recipe '{recipe.name}' has no ingredient list.", recipe);
                errors++;
            }

            if (!string.Equals(recipe.RequiredScanEntryId, expectedScanEntryId, StringComparison.Ordinal))
            {
                Debug.LogError($"[FabricationBootstrap] Recipe '{recipe.name}' has wrong scan gate '{recipe.RequiredScanEntryId}'. Expected '{expectedScanEntryId}'.", recipe);
                errors++;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] split = folderPath.Split('/');
            string current = split[0];
            for (int i = 1; i < split.Length; i++)
            {
                string next = $"{current}/{split[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, split[i]);

                current = next;
            }
        }
    }
}
