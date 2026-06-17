using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor
{
    /// <summary>
    /// Adds small generated-material detail primitives to tool prefabs without replacing the primary tool body material.
    /// </summary>
    public static class ToolSurfaceDetailGeminiIntegrator
    {
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607";
        private const string GeminiSinglesProvider = "GeminiSingles_20260607";
        private const string GeminiMicroPanelProvider = "Gemini_Batch20260607_MicroPanel";
        private const string GeminiBatch34TextureExpansionProvider = "Gemini_Batch20260608_TextureExpansion";

        private static readonly DetailSpec[] Details =
        {
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab", "Detail_LensGlass", GeminiSinglesProvider, "gemini_20260607_transparent_pressure_glass_edge_wear", new Vector3(0f, -0.105f, 0.49f), new Vector3(0f, 0f, 0f), new Vector3(0.095f, 0.012f, 0.055f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab", "Detail_CeramicSensorCap", GeminiSinglesProvider, "gemini_20260607_white_ceramic_sensor_casing", new Vector3(0f, -0.015f, 0.39f), new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.018f, 0.065f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab", "Detail_RubberGripPad", GeminiSinglesProvider, "gemini_20260607_black_waterproof_grip_rubber", new Vector3(0f, -0.12f, 0.18f), new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.018f, 0.16f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab", "Detail_AgedServicePatch", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_aged_green_service_metal", new Vector3(0f, 0.02f, 0.39f), new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.014f, 0.065f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab", "Detail_RibbedTrimBand", GeminiSinglesProvider, "gemini_20260607_fine_ribbed_metal_trim", new Vector3(0f, 0.04f, 0.36f), new Vector3(0f, 0f, 0f), new Vector3(0.17f, 0.018f, 0.08f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab", "Detail_CleanHousingPlate", GeminiSinglesProvider, "gemini_20260607_clean_nasa_punk_tool_housing_metal", new Vector3(0f, 0.015f, 0.34f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.014f, 0.12f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab", "Detail_GrayPolymerServiceCap", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_gray_polymer", new Vector3(0f, 0.055f, 0.26f), new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.016f, 0.10f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab", "Detail_AmberEmergencyLens", GeminiBatch34TextureExpansionProvider, "gemini_Batch20260608_TextureExpansion_b34_3417_amber_emergency_lens_material", new Vector3(0f, -0.006f, 0.43f), new Vector3(0f, 0f, 0f), new Vector3(0.085f, 0.010f, 0.060f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab", "Detail_RibbedTrimBand", GeminiSinglesProvider, "gemini_20260607_fine_ribbed_metal_trim", new Vector3(0f, 0.04f, 0.32f), new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.018f, 0.08f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab", "Detail_RubberGripPad", GeminiSinglesProvider, "gemini_20260607_black_waterproof_grip_rubber", new Vector3(0f, -0.09f, 0.02f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.018f, 0.18f)),
            new("Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab", "Detail_AgedServicePatch", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_aged_green_service_metal", new Vector3(0f, 0.02f, 0.32f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.014f, 0.075f)),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab", "Detail_LensGlass", GeminiSinglesProvider, "gemini_20260607_transparent_pressure_glass_edge_wear", new Vector3(0f, 0.07f, 0.42f), new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.03f, 0.10f)),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab", "Detail_RubberGripPad", GeminiSinglesProvider, "gemini_20260607_black_waterproof_grip_rubber", new Vector3(0f, -0.12f, 0.05f), new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.035f, 0.24f)),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_SeafloorDrill_World.prefab", "Detail_RibbedTrimBand", GeminiSinglesProvider, "gemini_20260607_fine_ribbed_metal_trim", new Vector3(0f, 0.11f, 0.36f), new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.035f, 0.13f)),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Builder_World.prefab", "Detail_CleanHousingPlate", GeminiSinglesProvider, "gemini_20260607_clean_nasa_punk_tool_housing_metal", new Vector3(0f, 0.10f, 0.34f), new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.032f, 0.18f)),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab", "Detail_GrayPolymerServiceCap", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_gray_polymer", new Vector3(0f, 0.12f, 0.26f), new Vector3(0f, 0f, 0f), new Vector3(0.20f, 0.032f, 0.16f)),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab", "Detail_AmberEmergencyLens", GeminiBatch34TextureExpansionProvider, "gemini_Batch20260608_TextureExpansion_b34_3417_amber_emergency_lens_material", new Vector3(0f, 0.052f, 0.44f), new Vector3(0f, 0f, 0f), new Vector3(0.150f, 0.020f, 0.095f)),
            new("Assets/_Project/Prefabs/Items/Tools/Item_Tool_SalvageSampler_World.prefab", "Detail_AgedServicePatch", GeminiMicroPanelProvider, "gemini_Batch20260607_MicroPanel_aged_green_service_metal", new Vector3(0f, 0.12f, 0.28f), new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.032f, 0.13f)),
        };

        [MenuItem("Hecton8/Art/Apply Gemini Tool Surface Detail Primitives")]
        public static void ExecuteMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            ValidateDetails();
            int detailCount = 0;
            int prefabCount = 0;
            string currentPrefab = string.Empty;
            GameObject prefabRoot = null;
            bool currentDirty = false;

            try
            {
                for (int i = 0; i < Details.Length; i++)
                {
                    DetailSpec spec = Details[i];
                    RequirePrefab(spec);

                    if (!string.Equals(currentPrefab, spec.PrefabPath, StringComparison.Ordinal))
                    {
                        SaveAndUnloadCurrent(ref prefabRoot, currentPrefab, currentDirty, ref prefabCount);
                        currentPrefab = spec.PrefabPath;
                        prefabRoot = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
                        currentDirty = false;
                    }

                    Material material = RequireMaterial(spec);

                    EnsureDetailPrimitive(prefabRoot.transform, spec, material);
                    currentDirty = true;
                    detailCount++;
                }

                SaveAndUnloadCurrent(ref prefabRoot, currentPrefab, currentDirty, ref prefabCount);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ToolSurfaceDetailGeminiIntegrator] Details={detailCount}, PrefabsSaved={prefabCount}");
        }

        private static void EnsureDetailPrimitive(Transform root, DetailSpec spec, Material material)
        {
            Transform existing = root.Find(spec.ChildName);
            GameObject detail = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            detail.name = spec.ChildName;
            detail.transform.SetParent(root, false);
            detail.transform.localPosition = spec.LocalPosition;
            detail.transform.localRotation = Quaternion.Euler(spec.LocalEulerAngles);
            detail.transform.localScale = spec.LocalScale;

            if (detail.TryGetComponent(out Collider collider))
                UnityEngine.Object.DestroyImmediate(collider);

            if (detail.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(detail);
        }

        private static Material LoadMaterial(string providerName, string id)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{providerName}/MAT_EXT_{providerName}_{id}.mat");
        }

        private static void ValidateDetails()
        {
            for (int i = 0; i < Details.Length; i++)
            {
                DetailSpec spec = Details[i];
                RequirePrefab(spec);
                RequireMaterial(spec);
            }
        }

        private static GameObject RequirePrefab(DetailSpec spec)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"[ToolSurfaceDetailGeminiIntegrator] Missing prefab: {spec.PrefabPath}");

            return prefab;
        }

        private static Material RequireMaterial(DetailSpec spec)
        {
            Material material = LoadMaterial(spec.ProviderName, spec.MaterialId);
            if (material == null)
                throw new InvalidOperationException($"[ToolSurfaceDetailGeminiIntegrator] Missing generated material provider={spec.ProviderName} id={spec.MaterialId}");

            return material;
        }

        private static void SaveAndUnloadCurrent(
            ref GameObject prefabRoot,
            string prefabPath,
            bool dirty,
            ref int prefabCount)
        {
            if (prefabRoot == null)
                return;

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                prefabCount++;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
            prefabRoot = null;
        }

        private readonly struct DetailSpec
        {
            public readonly string PrefabPath;
            public readonly string ChildName;
            public readonly string ProviderName;
            public readonly string MaterialId;
            public readonly Vector3 LocalPosition;
            public readonly Vector3 LocalEulerAngles;
            public readonly Vector3 LocalScale;

            public DetailSpec(
                string prefabPath,
                string childName,
                string providerName,
                string materialId,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                Vector3 localScale)
            {
                PrefabPath = prefabPath;
                ChildName = childName;
                ProviderName = providerName;
                MaterialId = materialId;
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                LocalScale = localScale;
            }
        }
    }
}
