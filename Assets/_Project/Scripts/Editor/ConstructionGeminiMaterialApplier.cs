using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Applies accepted Gemini single-material PBR maps to first-party construction module materials.
    /// </summary>
    public static class ConstructionGeminiMaterialApplier
    {
        private const string GeminiSingleManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json";
        private const string GeminiAtlasRoot = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases";

        private static readonly Assignment[] Assignments =
        {
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_Corridor.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3412_pressure_base_interior_wall_trim_sheet",
                0.72f,
                0.92f,
                0.34f,
                0.48f,
                0.004f,
                "Corridor shell: pressure-base interior trim sheet gives readable wall panels, gaskets, and damp lower-wall wear."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_Foundation.mat",
                "gemini_20260607_salvage_worn_repair_metal",
                0.75f,
                1.05f,
                0.0f,
                0.34f,
                0.014f,
                "Foundation: salvage-worn repair metal for heavy base plates."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_CurrentTurbine.mat",
                "gemini_20260607_dark_anodized_tool_metal",
                0.65f,
                0.9f,
                0.0f,
                0.38f,
                0.010f,
                "Current turbine: dark anodized machinery metal, wet but not black void."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_ServicePump.mat",
                "gemini_20260607_wet_service_panel_biofilm",
                0.55f,
                0.95f,
                0.0f,
                0.36f,
                0.012f,
                "Service pump: wet service panel with biofilm; constrained tiling hides high-seam source."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/Mat_Module_Pylon.mat",
                "gemini_20260607_orange_safety_composite_panel",
                0.75f,
                0.85f,
                0.0f,
                0.32f,
                0.008f,
                "Pylon: orange safety composite as construction route/readability accent."),
            new Assignment(
                "Assets/_Project/Art/Materials/Construction/MAT_Equipment_Atlas.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3416_ribbed_flexible_hose_material",
                0.50f,
                0.85f,
                0.0f,
                0.30f,
                0.003f,
                "Equipment atlas: ribbed flexible hose material covers pipe/cable/equipment backing without pretending to be a full device texture.")
        };

        [MenuItem("Hecton8/Art/Apply Gemini PBR To Construction Materials")]
        public static void ExecuteMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            Apply(true);
        }

        public static void Apply(bool importFirst)
        {
            if (importFirst)
                ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            Dictionary<string, ExternalPbrAsset> assets = LoadAllManifestAssets();
            ValidateAssignments(assets);
            int applied = 0;

            for (int i = 0; i < Assignments.Length; i++)
            {
                Assignment assignment = Assignments[i];
                ExternalPbrAsset asset = RequireAsset(assets, assignment);
                Material target = RequireTargetMaterial(assignment);
                ApplyAsset(target, asset, assignment);
                EditorUtility.SetDirty(target);
                applied++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ConstructionGeminiMaterialApplier] Applied={applied}");
        }

        private static void ApplyAsset(Material target, ExternalPbrAsset asset, Assignment assignment)
        {
            if (asset.maps == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing map payload for {asset.id}: {assignment.MaterialPath}");

            Texture2D baseColor = RequireTexture(asset.maps.BaseColor, asset.id, "BaseColor", assignment.MaterialPath);
            Texture2D normal = RequireTexture(asset.maps.NormalGL, asset.id, "NormalGL", assignment.MaterialPath);
            Texture2D maskMap = RequireTexture(asset.maps.MaskMap_UnityURP, asset.id, "MaskMap_UnityURP", assignment.MaterialPath);
            Texture2D height = RequireTexture(asset.maps.Height, asset.id, "Height", assignment.MaterialPath);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
                target.shader = shader;

            SetTextureIfPresent(target, "_BaseMap", baseColor);
            SetTextureIfPresent(target, "_MainTex", baseColor);
            SetTextureIfPresent(target, "_BumpMap", normal);
            SetTextureIfPresent(target, "_MetallicGlossMap", maskMap);
            SetTextureIfPresent(target, "_OcclusionMap", maskMap);
            SetTextureIfPresent(target, "_ParallaxMap", height);
            float tilingScale = TilingScale(asset, assignment);
            SetTextureScaleIfPresent(target, "_BaseMap", tilingScale);
            SetTextureScaleIfPresent(target, "_MainTex", tilingScale);
            SetTextureScaleIfPresent(target, "_BumpMap", tilingScale);
            SetTextureScaleIfPresent(target, "_MetallicGlossMap", tilingScale);
            SetTextureScaleIfPresent(target, "_OcclusionMap", tilingScale);
            SetTextureScaleIfPresent(target, "_ParallaxMap", tilingScale);
            SetFloatIfPresent(target, "_BumpScale", assignment.NormalScale);
            SetFloatIfPresent(target, "_Metallic", assignment.Metallic);
            SetFloatIfPresent(target, "_Smoothness", assignment.Smoothness);
            SetFloatIfPresent(target, "_Parallax", assignment.HeightScale);
            SetFloatIfPresent(target, "_OcclusionStrength", 1f);
            SetFloatIfPresent(target, "_SmoothnessTextureChannel", 0f);
            SetKeyword(target, "_NORMALMAP", normal != null);
            SetKeyword(target, "_METALLICSPECGLOSSMAP", maskMap != null);
            SetKeyword(target, "_OCCLUSIONMAP", maskMap != null);
            SetKeyword(target, "_PARALLAXMAP", height != null);
            target.enableInstancing = true;
        }

        private static Dictionary<string, ExternalPbrAsset> LoadManifestAssets(string manifestPath)
        {
            Dictionary<string, ExternalPbrAsset> assets = new Dictionary<string, ExternalPbrAsset>(StringComparer.Ordinal);
            string resolvedManifestPath = ResolveProjectFilePath(manifestPath);
            if (!File.Exists(resolvedManifestPath))
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing manifest: {manifestPath}");

            ExternalPbrManifest manifest = JsonUtility.FromJson<ExternalPbrManifest>(File.ReadAllText(resolvedManifestPath));
            if (manifest == null || manifest.assets == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Invalid manifest payload: {manifestPath}");

            for (int i = 0; i < manifest.assets.Length; i++)
            {
                ExternalPbrAsset asset = manifest.assets[i];
                if (asset == null || string.IsNullOrWhiteSpace(asset.id))
                    throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Invalid material asset entry in {manifestPath} at index {i}");
                assets[asset.id] = asset;
            }

            return assets;
        }

        private static Dictionary<string, ExternalPbrAsset> LoadAllManifestAssets()
        {
            Dictionary<string, ExternalPbrAsset> assets = new Dictionary<string, ExternalPbrAsset>(StringComparer.Ordinal);
            MergeManifestAssets(assets, GeminiSingleManifestPath);

            string resolvedAtlasRoot = ResolveProjectFilePath(GeminiAtlasRoot);
            if (Directory.Exists(resolvedAtlasRoot))
            {
                string[] manifests = Directory.GetFiles(resolvedAtlasRoot, "GeminiMaterialAtlas_Manifest.json", SearchOption.AllDirectories);
                Array.Sort(manifests, StringComparer.Ordinal);
                for (int i = 0; i < manifests.Length; i++)
                    MergeManifestAssets(assets, manifests[i]);
            }

            return assets;
        }

        private static void MergeManifestAssets(Dictionary<string, ExternalPbrAsset> assets, string manifestPath)
        {
            Dictionary<string, ExternalPbrAsset> manifestAssets = LoadManifestAssets(manifestPath);
            foreach (KeyValuePair<string, ExternalPbrAsset> pair in manifestAssets)
            {
                if (assets.ContainsKey(pair.Key))
                    throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Duplicate Gemini material id: {pair.Key}");

                assets.Add(pair.Key, pair.Value);
            }
        }

        private static void ValidateAssignments(Dictionary<string, ExternalPbrAsset> assets)
        {
            for (int i = 0; i < Assignments.Length; i++)
            {
                Assignment assignment = Assignments[i];
                ExternalPbrAsset asset = RequireAsset(assets, assignment);

                if (asset.maps == null)
                    throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing map payload for {asset.id}: {assignment.MaterialPath}");

                RequireTargetMaterial(assignment);
                RequireTexture(asset.maps.BaseColor, asset.id, "BaseColor", assignment.MaterialPath);
                RequireTexture(asset.maps.NormalGL, asset.id, "NormalGL", assignment.MaterialPath);
                RequireTexture(asset.maps.MaskMap_UnityURP, asset.id, "MaskMap_UnityURP", assignment.MaterialPath);
                RequireTexture(asset.maps.Height, asset.id, "Height", assignment.MaterialPath);
            }
        }

        private static ExternalPbrAsset RequireAsset(Dictionary<string, ExternalPbrAsset> assets, Assignment assignment)
        {
            if (!assets.TryGetValue(assignment.MaterialId, out ExternalPbrAsset asset))
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing Gemini material id={assignment.MaterialId} for {assignment.MaterialPath}");

            return asset;
        }

        private static Material RequireTargetMaterial(Assignment assignment)
        {
            Material target = AssetDatabase.LoadAssetAtPath<Material>(assignment.MaterialPath);
            if (target == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing construction material: {assignment.MaterialPath}");

            return target;
        }

        private static Texture2D RequireTexture(string assetPath, string materialId, string mapKey, string materialPath)
        {
            Texture2D texture = LoadTexture(assetPath);
            if (texture == null)
                throw new InvalidOperationException($"[ConstructionGeminiMaterialApplier] Missing required map {mapKey} for {materialId}: {materialPath} source={NormalizeAssetPath(assetPath)}");

            return texture;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
        }

        private static string ResolveProjectFilePath(string assetOrFilePath)
        {
            string normalized = NormalizeAssetPath(assetOrFilePath);
            if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized))
                return normalized;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, normalized);
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetTextureScaleIfPresent(Material material, string property, float scale)
        {
            if (material.HasProperty(property))
                material.SetTextureScale(property, new Vector2(scale, scale));
        }

        private static float TilingScale(ExternalPbrAsset asset, Assignment assignment)
        {
            float sourceScale = asset.catalogVersion > 0 ? Mathf.Clamp(asset.tilingScale, 0.25f, 16f) : 1f;
            return Mathf.Clamp(sourceScale * assignment.TilingMultiplier, 0.25f, 16f);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private sealed class Assignment
        {
            public readonly string MaterialPath;
            public readonly string MaterialId;
            public readonly float TilingMultiplier;
            public readonly float NormalScale;
            public readonly float Metallic;
            public readonly float Smoothness;
            public readonly float HeightScale;
            public readonly string Reason;

            public Assignment(
                string materialPath,
                string materialId,
                float tilingMultiplier,
                float normalScale,
                float metallic,
                float smoothness,
                float heightScale,
                string reason)
            {
                MaterialPath = materialPath;
                MaterialId = materialId;
                TilingMultiplier = tilingMultiplier;
                NormalScale = normalScale;
                Metallic = metallic;
                Smoothness = smoothness;
                HeightScale = heightScale;
                Reason = reason;
            }
        }

        [Serializable]
        private sealed class ExternalPbrManifest
        {
            public ExternalPbrAsset[] assets;
        }

        [Serializable]
        private sealed class ExternalPbrAsset
        {
            public string id;
            public int catalogVersion;
            public float tilingScale;
            public ExternalPbrMaps maps;
        }

        [Serializable]
        private sealed class ExternalPbrMaps
        {
            public string BaseColor;
            public string NormalGL;
            public string MaskMap_UnityURP;
            public string ARM_AO_Rough_Metal;
            public string Height;
        }
    }
}
