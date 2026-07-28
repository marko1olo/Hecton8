using System;
using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
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
        private const string SeafloorDrillItemPath = "Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset";
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
        private const string RecipeSeafloorDrillPath = "Assets/_Project/Data/Crafting/Recipes/Recipe_SeafloorDrill.asset";

        private const string TrialRootName = "Fabrication_Trial";
        private const string TrialFabricatorName = "Trial_Fabricator";
        private const string AssemblyPreviewChildName = "Assembly_Preview";
        private const string OutputSocketChildName = "Output_Socket";
        private const string DeconstructOutputSocketChildName = "Deconstruct_Output_Socket";
        private const string AssemblyHologramShaderPath = "Assets/_Project/Art/Shaders/Hecton_HologramAssembly.shader";
        private const string AssemblyHologramMaterialPath = "Assets/_Project/Art/Materials/MAT_FabricatorAssembly_Hologram.asset";

        private const string WorldRootName = "--- WORLD ---";
        private const string OutpostRootName = "Fabrication_Outpost";
        private const string OutpostFabricatorName = "Forward_Fabricator";
        private const float DefaultSurfaceWaterLevelY = 14.02f;

        [MenuItem("Hecton8/Authoring/Rebuild Starter Fabrication Kit", priority = 170)]
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
            ItemData seafloorDrill = AssetDatabase.LoadAssetAtPath<ItemData>(SeafloorDrillItemPath);
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

            if (seafloorDrill == null)
            {
                Debug.LogError("[FabricationBootstrap] Missing seafloor drill route assets. Starter fabrication kit was not rebuilt.");
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

            RecipeData seafloorDrillRecipe = CreateOrUpdateRecipe(
                "Recipe_SeafloorDrill.asset",
                "Seafloor Drill",
                "Field drill for opening hard seabed resource veins without collapsing the early copper route.",
                "scan.resource_node",
                FabricationGroup.Tools,
                seafloorDrill,
                1,
                new InventoryCost { item = glassPanel, amount = 1 },
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
                repairRecipe,
                seafloorDrillRecipe
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
                new Vector3(84f, DefaultSurfaceWaterLevelY + 0.8f, 1662f),
                new Vector3(1.6f, 2.0f, 1.35f),
                starterRecipes,
                "Forpost-fabrikator");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[FabricationBootstrap] Starter fabrication recipes and fabricator stations rebuilt.");
        }

        [MenuItem("Hecton8/Validation/Validate Starter Fabrication Kit", priority = 171)]
        public static void ValidateStarterFabricationKit()
        {
            int errors = 0;

            RecipeData beaconRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_FieldBeacon.asset");
            RecipeData analyzerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_EnvAnalyzer.asset");
            RecipeData samplerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_SalvageSampler.asset");
            RecipeData flashlightRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_Flashlight.asset");
            RecipeData scannerRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_Scanner.asset");
            RecipeData repairRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>($"{RecipesFolder}/Recipe_RepairTool.asset");
            RecipeData seafloorDrillRecipe = AssetDatabase.LoadAssetAtPath<RecipeData>(RecipeSeafloorDrillPath);
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
            ValidateRecipe(seafloorDrillRecipe, "scan.resource_node", ref errors);
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

        /// <summary>
        /// Resolves a "/"-separated hierarchy path inside one scene, INCLUDING inactive objects.
        ///
        /// <see cref="GameObject.Find"/> returns only active objects. This tool used it three times to
        /// decide whether the outpost root, its parent and the fabricator already existed. Once
        /// <c>Assets/_Project/Editor/H8_SceneCleaner.cs</c> reparented <c>--- WORLD ---</c> under
        /// <c>DEPRECATED_STUFF</c> and disabled it (:41-42, then <c>SaveScene</c> at :47), all three
        /// lookups went blind at the same time, and the consequences compounded: the root check missed
        /// the buried <c>Fabrication_Outpost</c> and made a new one, then the parent lookup for
        /// <c>--- WORLD ---</c> ALSO returned null, and because the reparent below is guarded by
        /// <c>if (parent != null)</c> the freshly created outpost was left as an ORPHAN SCENE ROOT.
        /// In a binary scene there is no diff to notice that.
        ///
        /// <see cref="Transform.Find"/> already accepts a slash-separated path and already sees inactive
        /// children, so only the first segment needed a scene-root scan.
        /// </summary>
        private static GameObject FindByPathIncludingInactive(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(path))
                return null;

            int firstSeparator = path.IndexOf('/');
            string rootName = firstSeparator < 0 ? path : path.Substring(0, firstSeparator);

            GameObject[] roots = scene.GetRootGameObjects();
            GameObject matchedRoot = null;
            for (int i = 0; i < roots.Length; i++)
            {
                if (!string.Equals(roots[i].name, rootName, StringComparison.Ordinal))
                    continue;

                matchedRoot = roots[i];
                break;
            }

            if (matchedRoot == null)
                return null;

            if (firstSeparator < 0)
                return matchedRoot;

            Transform child = matchedRoot.transform.Find(path.Substring(firstSeparator + 1));
            return child != null ? child.gameObject : null;
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
            GameObject root = FindByPathIncludingInactive(activeScene, rootPath);
            if (root == null)
            {
                root = new GameObject(rootName);

                if (!string.IsNullOrEmpty(parentName))
                {
                    GameObject parent = FindByPathIncludingInactive(activeScene, parentName);
                    if (parent != null)
                        root.transform.SetParent(parent.transform, false);
                }
            }

            GameObject station = FindByPathIncludingInactive(activeScene, $"{rootPath}/{fabricatorName}");
            bool createdStation = station == null;
            if (createdStation)
            {
                station = GameObject.CreatePrimitive(PrimitiveType.Cube);
                station.name = fabricatorName;
                station.transform.SetParent(root.transform, false);
                station.transform.position = worldPosition;
                station.transform.localScale = localScale;
            }

            if (station.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            EnsureCollider(station);
            EnsureMeshFallback(station, out Mesh fallbackMesh);
            EnsureAssemblyPreviewHost(station, out MeshFilter previewMeshFilter, out MeshRenderer previewRenderer);
            Transform outputSocket = EnsureChildTransform(station.transform, OutputSocketChildName, new Vector3(0f, 0.55f, 0.7f));
            Transform deconstructOutputSocket = EnsureChildTransform(station.transform, DeconstructOutputSocketChildName, new Vector3(0f, 0.45f, -0.65f));

            if (!station.TryGetComponent(out Fabricator fabricator))
                fabricator = station.AddComponent<Fabricator>();

            SerializedObject so = new SerializedObject(fabricator);
            SerializedProperty recipesProp = so.FindProperty("availableRecipes");
            recipesProp.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
                recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];

            SerializedProperty nameProp = so.FindProperty("fabricatorName");
            if (nameProp != null)
                nameProp.stringValue = displayName;

            SetObjectReference(so, "assemblyFallbackMesh", fallbackMesh);
            SetObjectReference(so, "assemblyPreviewMeshFilter", previewMeshFilter);
            SetObjectReference(so, "assemblyPreviewRenderer", previewRenderer);
            SetObjectReference(so, "hologramAssemblyMaterial", ResolveAssemblyHologramMaterial());
            SetObjectReference(so, "outputSocket", outputSocket);
            SetObjectReference(so, "deconstructOutputSocket", deconstructOutputSocket);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fabricator);
            EditorUtility.SetDirty(station);
        }

        private static void ValidateSceneFabricator(string rootPath, string fabricatorName, ref int errors)
        {
            // Inactive-inclusive, because this is a VALIDATOR: with GameObject.Find it reported
            // "Missing root" for content that is present and merely disabled, which is a false absence
            // and the single most misleading thing a validator can say in this project.
            GameObject root = FindByPathIncludingInactive(SceneManager.GetActiveScene(), rootPath);
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

            if (!fabricatorTransform.TryGetComponent(out Fabricator fabricator))
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

            SerializedObject so = new SerializedObject(fabricator);
            if (so.FindProperty("assemblyFallbackMesh")?.objectReferenceValue == null)
            {
                Debug.LogError($"[FabricationBootstrap] {fabricatorName} is missing required assembly fallback mesh.", fabricatorTransform.gameObject);
                errors++;
            }

            if (so.FindProperty("assemblyPreviewMeshFilter")?.objectReferenceValue == null ||
                so.FindProperty("assemblyPreviewRenderer")?.objectReferenceValue == null)
            {
                Debug.LogError($"[FabricationBootstrap] {fabricatorName} is missing assembly preview host references.", fabricatorTransform.gameObject);
                errors++;
            }

            if (so.FindProperty("hologramAssemblyMaterial")?.objectReferenceValue == null)
            {
                Debug.LogError($"[FabricationBootstrap] {fabricatorName} is missing assembly hologram material.", fabricatorTransform.gameObject);
                errors++;
            }

            if (so.FindProperty("outputSocket")?.objectReferenceValue == null ||
                so.FindProperty("deconstructOutputSocket")?.objectReferenceValue == null)
            {
                Debug.LogError($"[FabricationBootstrap] {fabricatorName} is missing physical output sockets.", fabricatorTransform.gameObject);
                errors++;
            }
        }

        private static void EnsureCollider(GameObject station)
        {
            if (station == null)
                return;

            if (!station.TryGetComponent(out Collider _))
            {
                BoxCollider collider = station.AddComponent<BoxCollider>();
                collider.size = new Vector3(1.2f, 1.15f, 1.2f);
                collider.center = new Vector3(0f, 0.55f, 0f);
            }
        }

        private static void EnsureMeshFallback(GameObject station, out Mesh fallbackMesh)
        {
            fallbackMesh = null;
            if (station == null)
                return;

            if (!station.TryGetComponent(out MeshFilter meshFilter))
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                MeshFilter primitiveFilter = primitive.GetComponent<MeshFilter>();
                fallbackMesh = primitiveFilter != null ? primitiveFilter.sharedMesh : null;
                UnityEngine.Object.DestroyImmediate(primitive);

                meshFilter = station.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = fallbackMesh;
            }
            else
            {
                fallbackMesh = meshFilter.sharedMesh;
            }

            if (fallbackMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                MeshFilter primitiveFilter = primitive.GetComponent<MeshFilter>();
                fallbackMesh = primitiveFilter != null ? primitiveFilter.sharedMesh : null;
                UnityEngine.Object.DestroyImmediate(primitive);
                meshFilter.sharedMesh = fallbackMesh;
            }

            if (!station.TryGetComponent(out MeshRenderer meshRenderer))
                meshRenderer = station.AddComponent<MeshRenderer>();

            if (meshRenderer.sharedMaterial == null)
                meshRenderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        }

        private static void EnsureAssemblyPreviewHost(GameObject station, out MeshFilter previewMeshFilter, out MeshRenderer previewRenderer)
        {
            previewMeshFilter = null;
            previewRenderer = null;
            if (station == null)
                return;

            Transform preview = EnsureChildTransform(station.transform, AssemblyPreviewChildName, new Vector3(0f, 1.35f, 0.25f));
            if (!preview.TryGetComponent(out previewMeshFilter))
                previewMeshFilter = preview.gameObject.AddComponent<MeshFilter>();

            if (!preview.TryGetComponent(out previewRenderer))
                previewRenderer = preview.gameObject.AddComponent<MeshRenderer>();

            previewRenderer.enabled = false;
            previewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            previewRenderer.receiveShadows = false;

            Material material = ResolveAssemblyHologramMaterial();
            if (material != null)
                previewRenderer.sharedMaterial = material;
        }

        private static Transform EnsureChildTransform(Transform parent, string childName, Vector3 localPosition)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                child = childObject.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static Material ResolveAssemblyHologramMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssemblyHologramMaterialPath);
            if (material != null)
                return material;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AssemblyHologramShaderPath);
            if (shader == null)
                return null;

            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            material = new Material(shader)
            {
                name = "MAT_FabricatorAssembly_Hologram"
            };
            AssetDatabase.CreateAsset(material, AssemblyHologramMaterialPath);
            return material;
        }

        private static void SetObjectReference(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
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
