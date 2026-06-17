using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Applies accepted Gemini biome PBR maps to world proxy materials through Unity's material API.
    /// </summary>
    public static class WorldProxyGeminiBiomeMaterialApplier
    {
        private const string GeminiAtlasRoot = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases";
        private const string GeminiBiomeManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json";
        private const string GeminiSingleManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json";

        private static readonly Assignment[] Assignments =
        {
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_small_floor.mat",
                "gemini_biome_20260607_aegir_surface_foamless_wet_rock",
                0.75f,
                1.25f,
                0.0f,
                0.42f,
                0.012f,
                "Aegir surface wet rock: readable near-floor geology without foam texture misuse."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_cluster_medium.mat",
                "gemini_biome_20260607_hydrothermal_vent_mineral_crust",
                0.55f,
                1.1f,
                0.0f,
                0.36f,
                0.018f,
                "Hydrothermal crust: mineralized hazard/vent-route rock breakup."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_arch_large.mat",
                "gemini_biome_20260607_wet_basalt_cave_wall",
                0.85f,
                1.35f,
                0.0f,
                0.46f,
                0.014f,
                "Wet basalt: large cave arch material."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_shelf_large.mat",
                "gemini_biome_20260607_wet_basalt_cave_wall",
                0.8f,
                1.45f,
                0.0f,
                0.44f,
                0.014f,
                "Wet basalt: broad shelf material."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_cave_entrance.mat",
                "gemini_biome_20260607_wet_basalt_cave_wall",
                0.7f,
                1.2f,
                0.0f,
                0.40f,
                0.016f,
                "Wet basalt: replaces purple cave-entrance placeholder."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_predator.mat",
                "gemini_biome_20260607_abyssal_predator_hide",
                0.55f,
                0.95f,
                0.0f,
                0.30f,
                0.010f,
                "Predator hide: spawn proxy reads as living threat, not red debug color."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_abyss_apex.mat",
                "gemini_biome_20260607_abyssal_predator_hide",
                0.5f,
                0.9f,
                0.0f,
                0.28f,
                0.010f,
                "Abyss apex hide: deep predator silhouette material."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_large_threat.mat",
                "gemini_biome_20260607_creature_bone_plate_material",
                0.55f,
                1.0f,
                0.0f,
                0.34f,
                0.014f,
                "Bone plate: armored large-threat creature proxy."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_ruin_apex.mat",
                "gemini_biome_20260607_creature_bone_plate_material",
                0.5f,
                1.0f,
                0.0f,
                0.32f,
                0.014f,
                "Bone plate: ruin apex creature proxy."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat",
                "gemini_biome_20260607_soft_jelly_membrane",
                0.5f,
                0.78f,
                0.0f,
                0.58f,
                0.006f,
                "Jelly membrane: passive spawn material."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_reef_apex.mat",
                "gemini_biome_20260607_soft_jelly_membrane",
                0.45f,
                0.8f,
                0.0f,
                0.54f,
                0.006f,
                "Jelly membrane: reef-apex creature proxy."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_egg_cluster.mat",
                "gemini_biome_20260607_soft_jelly_membrane",
                0.42f,
                0.72f,
                0.0f,
                0.62f,
                0.004f,
                "Jelly membrane: egg cluster wet organic surface."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat",
                "gemini_biome_20260607_bioluminescent_coral_flesh",
                0.65f,
                0.9f,
                0.0f,
                0.50f,
                0.006f,
                "Bioluminescent coral flesh: branching coral proxy color and wet organic detail."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat",
                "gemini_biome_20260607_bioluminescent_coral_flesh",
                0.55f,
                0.85f,
                0.0f,
                0.48f,
                0.006f,
                "Bioluminescent coral flesh: brittle coral proxy accent."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat",
                "gemini_biome_20260607_pale_tube_coral_calcium",
                0.7f,
                0.95f,
                0.0f,
                0.44f,
                0.010f,
                "Pale tube coral calcium: low coral proxy calcium structure."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat",
                "gemini_biome_20260607_pale_tube_coral_calcium",
                0.75f,
                1.0f,
                0.0f,
                0.42f,
                0.012f,
                "Pale tube coral calcium: massive coral proxy mineral structure."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat",
                "gemini_biome_20260607_pale_tube_coral_calcium",
                0.65f,
                0.9f,
                0.0f,
                0.46f,
                0.010f,
                "Pale tube coral calcium: plate coral proxy calcium surface."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat",
                "gemini_biome_20260607_living_kelp_frond_surface",
                0.65f,
                0.8f,
                0.0f,
                0.52f,
                0.006f,
                "Living kelp frond: tall kelp proxy wet strap surface."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat",
                "gemini_biome_20260607_living_kelp_frond_surface",
                0.55f,
                0.75f,
                0.0f,
                0.54f,
                0.006f,
                "Living kelp frond: canopy kelp proxy translucent wet surface."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat",
                "gemini_biome_20260607_living_kelp_frond_surface",
                0.5f,
                0.75f,
                0.0f,
                0.52f,
                0.005f,
                "Living kelp frond: dense kelp patch proxy."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat",
                "gemini_biome_20260607_living_kelp_frond_surface",
                0.48f,
                0.76f,
                0.0f,
                0.50f,
                0.005f,
                "Living kelp frond: abyssal kelp proxy uses wet strap surface instead of unassigned placeholder."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_plant_giant.mat",
                "gemini_biome_20260607_living_kelp_frond_surface",
                0.58f,
                0.78f,
                0.0f,
                0.50f,
                0.005f,
                "Living kelp frond: giant plant proxy gets readable wet alien plant surface."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_landmark_spire.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3406_serpentinite_fault_rock",
                0.75f,
                1.05f,
                0.0f,
                0.38f,
                0.010f,
                "Serpentinite fault rock: distinct green-black landmark geology instead of generic proxy tint."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_safe.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3401_photic_limestone_rubble_shelf",
                0.72f,
                0.92f,
                0.0f,
                0.30f,
                0.006f,
                "Photic limestone: readable safe-pocket terrain in shallow/early route pockets."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_resource.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3404_abyssal_manganese_nodule_plain",
                0.70f,
                0.95f,
                0.04f,
                0.25f,
                0.006f,
                "Manganese nodule plain: resource pocket ground that reads as geology, not painted pickup dots."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_hazard.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3405_methane_hydrate_crack_vein",
                0.75f,
                1.00f,
                0.0f,
                0.34f,
                0.006f,
                "Methane hydrate crack vein: cold seep hazard pocket substrate."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3420_salvage_cut_cross_section_trim_atlas",
                0.82f,
                0.92f,
                0.48f,
                0.42f,
                0.004f,
                "Salvage-cut cross sections: debris scatter gets cut-edge industrial breakup."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_field.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3419_welded_seam_and_rivet_row_trim_sheet",
                0.86f,
                0.92f,
                0.62f,
                0.44f,
                0.004f,
                "Welded seam trim: broad debris fields read as damaged pressure hull material."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_module_single.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3411_pressure_base_exterior_hull_trim_sheet",
                0.80f,
                0.90f,
                0.48f,
                0.54f,
                0.004f,
                "Pressure base exterior hull trim: single ruin modules get NASA-punk hull identity."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_cluster_medium.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3410_drowned_concrete_rubble",
                0.82f,
                0.95f,
                0.05f,
                0.24f,
                0.004f,
                "Drowned concrete rubble: medium ruin clusters get old colony infrastructure material."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_megastructure.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3411_pressure_base_exterior_hull_trim_sheet",
                0.72f,
                0.86f,
                0.48f,
                0.52f,
                0.004f,
                "Pressure hull trim: megastructure proxy reads as pressure-rated ruined base geometry."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_service_scar.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3419_welded_seam_and_rivet_row_trim_sheet",
                0.78f,
                0.92f,
                0.62f,
                0.42f,
                0.004f,
                "Welded seams and rivets: service scars show repaired industrial pressure joints."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_route_power.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3414_rubber_gasket_ring_trim_sheet",
                0.85f,
                0.80f,
                0.0f,
                0.32f,
                0.003f,
                "Rubber gasket rings: power-route proxy gets pressure seal/cable-adjacent material.")
        };

        [MenuItem("Hecton8/Art/Apply Gemini Biome PBR To World Proxy Materials")]
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
            Debug.Log($"[WorldProxyGeminiBiomeMaterialApplier] Applied={applied}");
        }

        private static void ApplyAsset(Material target, ExternalPbrAsset asset, Assignment assignment)
        {
            if (asset.maps == null)
                throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Missing map payload for {asset.id}: {assignment.MaterialPath}");

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
                throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Missing manifest: {manifestPath}");

            ExternalPbrManifest manifest = JsonUtility.FromJson<ExternalPbrManifest>(File.ReadAllText(resolvedManifestPath));
            if (manifest == null || manifest.assets == null)
                throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Invalid manifest payload: {manifestPath}");

            for (int i = 0; i < manifest.assets.Length; i++)
            {
                ExternalPbrAsset asset = manifest.assets[i];
                if (asset == null || string.IsNullOrWhiteSpace(asset.id))
                    throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Invalid material asset entry in {manifestPath} at index {i}");
                assets[asset.id] = asset;
            }

            return assets;
        }

        private static Dictionary<string, ExternalPbrAsset> LoadAllManifestAssets()
        {
            Dictionary<string, ExternalPbrAsset> assets = new Dictionary<string, ExternalPbrAsset>(StringComparer.Ordinal);
            MergeManifestAssets(assets, GeminiBiomeManifestPath);
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
                    throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Duplicate Gemini material id: {pair.Key}");

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
                    throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Missing map payload for {asset.id}: {assignment.MaterialPath}");

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
                throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Missing Gemini material id={assignment.MaterialId} for {assignment.MaterialPath}");

            return asset;
        }

        private static Material RequireTargetMaterial(Assignment assignment)
        {
            Material target = AssetDatabase.LoadAssetAtPath<Material>(assignment.MaterialPath);
            if (target == null)
                throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Missing proxy material: {assignment.MaterialPath}");

            return target;
        }

        private static Texture2D RequireTexture(string assetPath, string materialId, string mapKey, string materialPath)
        {
            Texture2D texture = LoadTexture(assetPath);
            if (texture == null)
                throw new InvalidOperationException($"[WorldProxyGeminiBiomeMaterialApplier] Missing required map {mapKey} for {materialId}: {materialPath} source={NormalizeAssetPath(assetPath)}");

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
