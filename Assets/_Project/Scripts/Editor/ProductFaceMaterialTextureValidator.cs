#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class ProductFaceMaterialTextureValidator
    {
        private const string MenuPath = "Hecton8/Validation/Product-Face Material Texture Gate";
        private const string DefaultPackageMaterialGuid = "31321ba15b8f8eb4c954353edc038b1d";
        private const string PackageCacheToken = "PackageCache";

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

        private static readonly string[] StaticTextScanPaths =
        {
            "Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md",
            "Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md",
            "Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_ROLE_PACKAGE.md",
            "Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md",
            "Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md",
            "Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET.md",
        };

        private static readonly MaterialTarget[] MaterialTargets =
        {
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_BeaconDeployer_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_Builder_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_EnvAnalyzer_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_Flashlight_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_HarpoonLauncher_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_Knife_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_LaserCutter_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_Propulsion_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_Repair_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_SalvageSampler_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_Scanner_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Tools/Mat_Tool_StunPistol_Placeholder.mat", ProductFaceMaterialRoute.Tool, true, true, true, string.Empty, true),

            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat", ProductFaceMaterialRoute.Resource, true, true, true, string.Empty, false),

            new MaterialTarget("Assets/_Project/Art/Materials/Gameplay/MAT_PlayerSwimBlockout.mat", ProductFaceMaterialRoute.Player, true, true, true, string.Empty, true),
            new MaterialTarget("Assets/_Project/Art/Materials/Mat_Visor_Glass.mat", ProductFaceMaterialRoute.Player, false, true, false, "VISOR: runoff normal and droplet mask are shader-specific; full suit body roles still need separate albedo/normal/packed masks.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_DirtyPressureGlass.mat", ProductFaceMaterialRoute.Player, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetPressureMetal.mat", ProductFaceMaterialRoute.Transport, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/RuntimeShell1428/H8_Shell_Submarine_WetSteel.mat", ProductFaceMaterialRoute.Transport, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/RuntimeShell1428/MAT_H8Shell_PressureHull.mat", ProductFaceMaterialRoute.Transport, true, true, true, string.Empty, false),
            new MaterialTarget("Assets/_Project/Art/Materials/Construction/MAT_Equipment_Atlas.mat", ProductFaceMaterialRoute.SharedEquipment, true, true, true, "EQUIPMENT_ATLAS: shader-declared equipment packed mask; exact channel layout must be confirmed before product-face relink.", false),

            new MaterialTarget("Assets/_Project/Art/Materials/Celestial/MAT_SurfaceCloudPanorama_1428.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "SKY: cloud texture masks only; allowed on sky route.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Ione.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "MOON: albedo identity; normal/phase maps need future route proof.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "MOON: albedo identity; normal/phase maps need future route proof.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Nammu.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "MOON: albedo identity; normal/phase maps need future route proof.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Pelagia.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "MOON: albedo identity; normal/phase maps need future route proof.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "MOON: albedo identity; normal/phase maps need future route proof.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Varda.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "MOON: albedo identity; normal/phase maps need future route proof.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat", ProductFaceMaterialRoute.SkyOcean, true, true, false, "OCEAN: Crest material route; assign only through sky/ocean owner proof.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat", ProductFaceMaterialRoute.SkyOcean, true, false, false, "AEGIR: baked disc route; allowed on sky route.", false),
            new MaterialTarget("Assets/_Project/Art/Materials/MAT_SurfaceSkyDomeNoir_1428.mat", ProductFaceMaterialRoute.EventOnlyEnvironment, true, false, false, "EVENT_ONLY: storm/eclipse/noir route only.", true),
            new MaterialTarget("Assets/_Project/Art/Materials/MAT_SurfaceNoirProceduralSkybox_1428.mat", ProductFaceMaterialRoute.EventOnlyEnvironment, true, false, false, "EVENT_ONLY: storm/eclipse/noir route only.", true),
            new MaterialTarget("Assets/_Project/Art/Materials/World/MAT_SurfaceStormWater_1428.mat", ProductFaceMaterialRoute.EventOnlyEnvironment, true, true, false, "WEATHER_ONLY: storm route only.", true),
            new MaterialTarget("Assets/_Project/Art/Materials/MAT_NoirDepthFog.mat", ProductFaceMaterialRoute.DeepOnlyEnvironment, false, false, false, "DEEP_ONLY: depth route only.", true),
            new MaterialTarget("Assets/_Project/Art/Materials/MAT_H8WorldDeepAbyss_1428.mat", ProductFaceMaterialRoute.DeepOnlyEnvironment, true, false, false, "DEEP_ONLY: depth route only.", true),
            new MaterialTarget("Assets/_Project/Art/Materials/MAT_H8WorldDepthCurtain_1428.mat", ProductFaceMaterialRoute.DeepOnlyEnvironment, true, false, false, "DEEP_ONLY: depth route only.", true),

            new MaterialTarget("Assets/_Project/Art/Materials/Diagnostics/MAT_ErrorCube.mat", ProductFaceMaterialRoute.Diagnostic, false, false, false, string.Empty, true),
            new MaterialTarget(".codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat", ProductFaceMaterialRoute.PackageDefault, false, false, false, string.Empty, true),
        };

        [MenuItem(MenuPath)]
        public static void ValidateFromMenu()
        {
            ProductFaceMaterialTextureValidationReport report = ValidateSources();
            for (int i = 0; i < report.Findings.Count; i++)
            {
                ProductFaceMaterialTextureValidationFinding finding = report.Findings[i];
                string line =
                    $"[ProductFaceMaterialTextureValidator] {finding.Severity} | {finding.Category} | {finding.AssetPath} | {finding.Context} | {finding.Message}";

                if (finding.Severity == ProductFaceMaterialTextureFindingSeverity.Fail)
                    Debug.LogError(line);
                else if (finding.Severity == ProductFaceMaterialTextureFindingSeverity.Warning)
                    Debug.LogWarning(line);
                else
                    Debug.Log(line);
            }

            if (report.FailureCount > 0)
            {
                Debug.LogError(
                    $"[ProductFaceMaterialTextureValidator] FAILED. Prefabs={report.CheckedPrefabCount}, Materials={report.CheckedMaterialCount}, "
                    + $"Failures={report.FailureCount}, Warnings={report.WarningCount}. Product-face relink must not proceed.");
            }
            else
            {
                Debug.Log(
                    $"[ProductFaceMaterialTextureValidator] No source-failure findings. Prefabs={report.CheckedPrefabCount}, Materials={report.CheckedMaterialCount}, "
                    + $"Warnings={report.WarningCount}. This is static editor inspection only; Unity import, screenshots, Frame Debugger, profiler, and runtime proof remain pending.");
            }
        }

        public static ProductFaceMaterialTextureValidationReport ValidateSources()
        {
            ProductFaceMaterialTextureValidationReport report = new ProductFaceMaterialTextureValidationReport();
            ValidateStaticMaterialTargets(report);
            ValidateProductFacePrefabs(report);
            ValidateStaticTextDebt(report);
            return report;
        }

        public static bool ValidateSources(out ProductFaceMaterialTextureValidationReport report)
        {
            report = ValidateSources();
            return report.FailureCount == 0;
        }

        private static void ValidateStaticMaterialTargets(ProductFaceMaterialTextureValidationReport report)
        {
            for (int i = 0; i < MaterialTargets.Length; i++)
            {
                MaterialTarget target = MaterialTargets[i];
                if (target.Route == ProductFaceMaterialRoute.PackageDefault)
                {
                    report.AddFail(
                        ProductFaceMaterialTextureFindingCategory.PackageDefaultMaterial,
                        target.Path,
                        "static-target",
                        "Package URP default Lit material route is forbidden for product-face bodies.");
                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(target.Path);
                if (material == null)
                {
                    report.AddWarning(
                        ProductFaceMaterialTextureFindingCategory.MissingStaticMaterialTarget,
                        target.Path,
                        "static-target",
                        "Static target material path is not loadable in this project state. Future Unity owner must resolve or remove this role target.");
                    continue;
                }

                report.CheckedMaterialCount++;
                ValidateMaterial(material, target.Path, "static-target", target, report);
            }
        }

        private static void ValidateProductFacePrefabs(ProductFaceMaterialTextureValidationReport report)
        {
            HashSet<string> prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ExactProductFacePrefabs.Length; i++)
                AddPrefabPath(prefabPaths, ExactProductFacePrefabs[i], true, report);

            for (int rootIndex = 0; rootIndex < ProductFacePrefabRoots.Length; rootIndex++)
            {
                string root = ProductFacePrefabRoots[rootIndex];
                if (!AssetDatabase.IsValidFolder(root))
                {
                    report.AddFail(
                        ProductFaceMaterialTextureFindingCategory.MissingPrefabRoot,
                        root,
                        "prefab-root",
                        "Missing product-face prefab root. Missing source is a failure, not a pass.");
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                    AddPrefabPath(prefabPaths, AssetDatabase.GUIDToAssetPath(guids[guidIndex]), false, report);
            }

            foreach (string prefabPath in prefabPaths)
                ValidatePrefabMaterials(prefabPath, report);
        }

        private static void AddPrefabPath(
            HashSet<string> prefabPaths,
            string prefabPath,
            bool required,
            ProductFaceMaterialTextureValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                if (required)
                {
                    report.AddFail(
                        ProductFaceMaterialTextureFindingCategory.MissingPrefab,
                        prefabPath,
                        "prefab",
                        "Missing required product-face prefab.");
                }

                return;
            }

            prefabPaths.Add(prefabPath);
        }

        private static void ValidatePrefabMaterials(string prefabPath, ProductFaceMaterialTextureValidationReport report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return;

            report.CheckedPrefabCount++;
            if (File.Exists(prefabPath))
                ValidateTextFileForDefaultDebt(prefabPath, "prefab-yaml", true, report);

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                report.AddWarning(
                    ProductFaceMaterialTextureFindingCategory.NoRendererMaterialRoute,
                    prefabPath,
                    prefab.name,
                    "No renderer material route found. Hidden-only source needs explicit proof outside this material gate.");
                return;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;

                string componentPath = BuildTransformPath(prefab.transform, renderer.transform);
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    report.AddFail(
                        ProductFaceMaterialTextureFindingCategory.MissingMaterialSlot,
                        prefabPath,
                        componentPath,
                        "Renderer has no shared material slots.");
                    continue;
                }

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    string context = componentPath + "/slot" + materialIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (material == null)
                    {
                        report.AddFail(
                            ProductFaceMaterialTextureFindingCategory.MissingMaterialSlot,
                            prefabPath,
                            context,
                            "Renderer material slot is null.");
                        continue;
                    }

                    string materialPath = AssetDatabase.GetAssetPath(material);
                    ProductFaceMaterialRoute route = ClassifyMaterialRoute(materialPath, material.name);
                    if (IsForbiddenMaterialRoute(route, materialPath, material.name))
                    {
                        report.AddFail(
                            CategoryForForbiddenRoute(route),
                            string.IsNullOrEmpty(materialPath) ? prefabPath : materialPath,
                            context,
                            "Forbidden product-face material route is assigned to a renderer.");
                        continue;
                    }

                    if (IsEnvironmentRoute(route) && !IsAllowedEnvironmentPrefabScope(prefabPath, route))
                    {
                        report.AddFail(
                            ProductFaceMaterialTextureFindingCategory.EnvironmentRouteOutOfScope,
                            materialPath,
                            context,
                            "Environment-route material is assigned outside its allowed sky/ocean/event/depth scope.");
                    }
                }
            }
        }

        private static void ValidateStaticTextDebt(ProductFaceMaterialTextureValidationReport report)
        {
            for (int i = 0; i < StaticTextScanPaths.Length; i++)
            {
                string path = StaticTextScanPaths[i];
                if (!File.Exists(path))
                {
                    report.AddWarning(
                        ProductFaceMaterialTextureFindingCategory.MissingStaticReport,
                        path,
                        "static-text-scan",
                        "Static report path requested for material debt scan is missing.");
                    continue;
                }

                ValidateTextFileForDefaultDebt(path, "static-report", false, report);
            }
        }

        private static void ValidateTextFileForDefaultDebt(
            string path,
            string context,
            bool failOnDebt,
            ProductFaceMaterialTextureValidationReport report)
        {
            string text = File.ReadAllText(path);
            if (text.IndexOf(DefaultPackageMaterialGuid, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddTextDebtFinding(
                    report,
                    failOnDebt,
                    ProductFaceMaterialTextureFindingCategory.UnresolvedDefaultGuid,
                    path,
                    context,
                    "Known unresolved/default package material GUID appears in scanned product-face source text.");
            }

            if (text.IndexOf(PackageCacheToken, StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("Lit.mat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddTextDebtFinding(
                    report,
                    failOnDebt,
                    ProductFaceMaterialTextureFindingCategory.PackageDefaultMaterial,
                    path,
                    context,
                    "Package-cache Lit material route appears in scanned product-face source text.");
            }
        }

        private static void AddTextDebtFinding(
            ProductFaceMaterialTextureValidationReport report,
            bool failOnDebt,
            ProductFaceMaterialTextureFindingCategory category,
            string path,
            string context,
            string message)
        {
            if (failOnDebt)
            {
                report.AddFail(category, path, context, message);
                return;
            }

            report.AddWarning(category, path, context, "Historical/static report debt: " + message);
        }

        private static void ValidateMaterial(
            Material material,
            string materialPath,
            string context,
            MaterialTarget target,
            ProductFaceMaterialTextureValidationReport report)
        {
            ProductFaceMaterialRoute route = ClassifyMaterialRoute(materialPath, material.name);
            if (target.AlwaysReject || IsForbiddenMaterialRoute(route, materialPath, material.name))
            {
                report.AddFail(
                    CategoryForForbiddenRoute(route),
                    materialPath,
                    context,
                    "Static target is forbidden as a product-face material source.");
            }

            if (target.RequiresAlbedo && !HasAnyTexture(material, AlbedoProperties))
            {
                report.AddFail(
                    ProductFaceMaterialTextureFindingCategory.MissingAlbedo,
                    materialPath,
                    context,
                    "Required albedo/base texture slot is missing.");
            }

            if (target.RequiresNormal && !HasAnyTexture(material, NormalProperties))
            {
                report.AddFail(
                    ProductFaceMaterialTextureFindingCategory.MissingNormal,
                    materialPath,
                    context,
                    "Required normal/detail-normal texture slot is missing.");
            }

            if (target.RequiresPackedMask)
            {
                if (!HasAnyTexture(material, PackedMaskProperties))
                {
                    report.AddFail(
                        ProductFaceMaterialTextureFindingCategory.MissingPackedMask,
                        materialPath,
                        context,
                        "Required packed material mask slot is missing.");
                }

                if (string.IsNullOrWhiteSpace(target.PackedChannelDeclaration))
                {
                    report.AddFail(
                        ProductFaceMaterialTextureFindingCategory.MissingChannelDeclaration,
                        materialPath,
                        context,
                        "Packed material mask is required, but the static role target does not declare channel semantics.");
                }
            }
        }

        private static readonly string[] AlbedoProperties =
        {
            "_BaseMap",
            "_MainTex",
            "_AlbedoAtlas",
            "_CloudTexA",
            "_CloudTexB",
            "_PlanetTex",
        };

        private static readonly string[] NormalProperties =
        {
            "_BumpMap",
            "_NormalMap",
            "_NormalAtlas",
            "_DetailNormalMap",
            "_WaterRunoffNormalTex",
            "_Normals",
        };

        private static readonly string[] PackedMaskProperties =
        {
            "_MaskMap",
            "_MetallicGlossMap",
            "_ArmMap",
            "_ORMAtlas",
            "_MraoMap",
            "_MRAOMap",
            "_OcclusionMap",
        };

        private static bool HasAnyTexture(Material material, string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string property = propertyNames[i];
                if (material.HasProperty(property) && material.GetTexture(property) != null)
                    return true;
            }

            return false;
        }

        private static ProductFaceMaterialRoute ClassifyMaterialRoute(string materialPath, string materialName)
        {
            string path = materialPath ?? string.Empty;
            string name = materialName ?? string.Empty;

            if (path.IndexOf(PackageCacheToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ProductFaceMaterialRoute.PackageDefault;
            }

            if (name.IndexOf("Placeholder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("ProceduralPlaceholders", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ProductFaceMaterialRoute.Placeholder;
            }

            if (name.IndexOf("MAT_PlayerSwimBlockout", StringComparison.OrdinalIgnoreCase) >= 0)
                return ProductFaceMaterialRoute.Blockout;

            if (name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Checker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Diagnostic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("FlatColor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ProductFaceMaterialRoute.Diagnostic;
            }

            if (path.IndexOf("/Celestial/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("SurfaceCloud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Aegir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("CelestialMoon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("SurfaceCrestOcean", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("SurfaceGasGiant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ProductFaceMaterialRoute.SkyOcean;
            }

            if (name.IndexOf("Storm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Noir", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ProductFaceMaterialRoute.EventOnlyEnvironment;
            }

            if (name.IndexOf("DeepAbyss", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("DepthCurtain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("DepthFog", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ProductFaceMaterialRoute.DeepOnlyEnvironment;
            }

            return ProductFaceMaterialRoute.Unknown;
        }

        private static bool IsForbiddenMaterialRoute(ProductFaceMaterialRoute route, string materialPath, string materialName)
        {
            if (route == ProductFaceMaterialRoute.PackageDefault ||
                route == ProductFaceMaterialRoute.Placeholder ||
                route == ProductFaceMaterialRoute.Blockout ||
                route == ProductFaceMaterialRoute.Diagnostic)
            {
                return true;
            }

            string name = materialName ?? string.Empty;
            string path = materialPath ?? string.Empty;
            return path.IndexOf("Mat_ToolTrial_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Lit", StringComparison.OrdinalIgnoreCase) == 0 && path.IndexOf("render-pipelines", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ProductFaceMaterialTextureFindingCategory CategoryForForbiddenRoute(ProductFaceMaterialRoute route)
        {
            if (route == ProductFaceMaterialRoute.PackageDefault)
                return ProductFaceMaterialTextureFindingCategory.PackageDefaultMaterial;
            if (route == ProductFaceMaterialRoute.Placeholder)
                return ProductFaceMaterialTextureFindingCategory.PlaceholderMaterial;
            if (route == ProductFaceMaterialRoute.Blockout)
                return ProductFaceMaterialTextureFindingCategory.BlockoutMaterial;
            if (route == ProductFaceMaterialRoute.Diagnostic)
                return ProductFaceMaterialTextureFindingCategory.DiagnosticMaterial;

            return ProductFaceMaterialTextureFindingCategory.ForbiddenMaterialRoute;
        }

        private static bool IsEnvironmentRoute(ProductFaceMaterialRoute route)
        {
            return route == ProductFaceMaterialRoute.SkyOcean ||
                   route == ProductFaceMaterialRoute.EventOnlyEnvironment ||
                   route == ProductFaceMaterialRoute.DeepOnlyEnvironment;
        }

        private static bool IsAllowedEnvironmentPrefabScope(string prefabPath, ProductFaceMaterialRoute route)
        {
            if (route == ProductFaceMaterialRoute.SkyOcean)
            {
                return prefabPath.Equals("Assets/_Project/Prefabs/Sky_System.prefab", StringComparison.OrdinalIgnoreCase) ||
                       prefabPath.Equals("Assets/_Project/Prefabs/Ocean_Crest.prefab", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static string BuildTransformPath(Transform root, Transform leaf)
        {
            if (leaf == null)
                return string.Empty;

            Stack<string> names = new Stack<string>();
            Transform current = leaf;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root)
                    break;

                current = current.parent;
            }

            return string.Join("/", names);
        }

        private readonly struct MaterialTarget
        {
            public readonly string Path;
            public readonly ProductFaceMaterialRoute Route;
            public readonly bool RequiresAlbedo;
            public readonly bool RequiresNormal;
            public readonly bool RequiresPackedMask;
            public readonly string PackedChannelDeclaration;
            public readonly bool AlwaysReject;

            public MaterialTarget(
                string path,
                ProductFaceMaterialRoute route,
                bool requiresAlbedo,
                bool requiresNormal,
                bool requiresPackedMask,
                string packedChannelDeclaration,
                bool alwaysReject)
            {
                Path = path;
                Route = route;
                RequiresAlbedo = requiresAlbedo;
                RequiresNormal = requiresNormal;
                RequiresPackedMask = requiresPackedMask;
                PackedChannelDeclaration = packedChannelDeclaration;
                AlwaysReject = alwaysReject;
            }
        }
    }

    public sealed class ProductFaceMaterialTextureValidationReport
    {
        private readonly List<ProductFaceMaterialTextureValidationFinding> _findings =
            new List<ProductFaceMaterialTextureValidationFinding>(64);

        public IReadOnlyList<ProductFaceMaterialTextureValidationFinding> Findings => _findings;
        public int CheckedPrefabCount { get; internal set; }
        public int CheckedMaterialCount { get; internal set; }
        public int FailureCount { get; private set; }
        public int WarningCount { get; private set; }

        public void AddFail(
            ProductFaceMaterialTextureFindingCategory category,
            string assetPath,
            string context,
            string message)
        {
            FailureCount++;
            Add(ProductFaceMaterialTextureFindingSeverity.Fail, category, assetPath, context, message);
        }

        public void AddWarning(
            ProductFaceMaterialTextureFindingCategory category,
            string assetPath,
            string context,
            string message)
        {
            WarningCount++;
            Add(ProductFaceMaterialTextureFindingSeverity.Warning, category, assetPath, context, message);
        }

        public void AddInfo(
            ProductFaceMaterialTextureFindingCategory category,
            string assetPath,
            string context,
            string message)
        {
            Add(ProductFaceMaterialTextureFindingSeverity.Info, category, assetPath, context, message);
        }

        private void Add(
            ProductFaceMaterialTextureFindingSeverity severity,
            ProductFaceMaterialTextureFindingCategory category,
            string assetPath,
            string context,
            string message)
        {
            _findings.Add(new ProductFaceMaterialTextureValidationFinding(
                severity,
                category,
                assetPath ?? string.Empty,
                context ?? string.Empty,
                message ?? string.Empty));
        }
    }

    public readonly struct ProductFaceMaterialTextureValidationFinding
    {
        public readonly ProductFaceMaterialTextureFindingSeverity Severity;
        public readonly ProductFaceMaterialTextureFindingCategory Category;
        public readonly string AssetPath;
        public readonly string Context;
        public readonly string Message;

        public ProductFaceMaterialTextureValidationFinding(
            ProductFaceMaterialTextureFindingSeverity severity,
            ProductFaceMaterialTextureFindingCategory category,
            string assetPath,
            string context,
            string message)
        {
            Severity = severity;
            Category = category;
            AssetPath = assetPath;
            Context = context;
            Message = message;
        }
    }

    public enum ProductFaceMaterialTextureFindingSeverity
    {
        Info = 0,
        Warning = 1,
        Fail = 2
    }

    public enum ProductFaceMaterialTextureFindingCategory
    {
        MissingStaticMaterialTarget = 0,
        MissingPrefabRoot = 1,
        MissingPrefab = 2,
        MissingStaticReport = 3,
        NoRendererMaterialRoute = 4,
        MissingMaterialSlot = 5,
        MissingAlbedo = 6,
        MissingNormal = 7,
        MissingPackedMask = 8,
        MissingChannelDeclaration = 9,
        UnresolvedDefaultGuid = 10,
        PackageDefaultMaterial = 11,
        PlaceholderMaterial = 12,
        BlockoutMaterial = 13,
        DiagnosticMaterial = 14,
        ForbiddenMaterialRoute = 15,
        EnvironmentRouteOutOfScope = 16
    }

    public enum ProductFaceMaterialRoute
    {
        Unknown = 0,
        Tool = 1,
        Resource = 2,
        Player = 3,
        Transport = 4,
        SharedEquipment = 5,
        SkyOcean = 6,
        EventOnlyEnvironment = 7,
        DeepOnlyEnvironment = 8,
        Placeholder = 9,
        Blockout = 10,
        Diagnostic = 11,
        PackageDefault = 12
    }
}
#endif
