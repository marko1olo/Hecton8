using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Builds explicit Unity material handoff assets for Batch34 UV/pickup atlas sources.
    /// These materials are source handoff targets for future mesh/UV owners; they do not mutate flora/fauna prefabs.
    /// </summary>
    public static class Batch34UvAtlasMaterialHandoffBuilder
    {
        public const string OutputFolder = "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff";

        private static readonly AtlasMaterialSpec[] Specs =
        {
            new AtlasMaterialSpec(
                "B34-3433",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3433_brine_vane_flora_uv_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Flora_BrineVaneAtlas_Cutout.mat",
                true,
                0.42f,
                new Color(0.58f, 0.88f, 0.82f, 1f),
                "brine-zone vane flora atlas cutout handoff for translucent flora mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3434",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3434_shallow_alien_seagrass_blade_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Flora_SeagrassBladeAtlas_Cutout.mat",
                true,
                0.42f,
                new Color(0.78f, 0.95f, 0.86f, 1f),
                "shallow alien seagrass blade atlas cutout handoff for flora cards and mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3435",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3435_plate_coral_rim_uv_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Flora_PlateCoralRimAtlas_Cutout.mat",
                true,
                0.45f,
                new Color(1.00f, 0.88f, 0.80f, 1f),
                "plate coral rim atlas cutout handoff for reef/coral mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3436",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3436_sponge_pore_organic_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Flora_SpongePoreOrganicAtlas_Cutout.mat",
                true,
                0.44f,
                new Color(0.92f, 0.88f, 0.76f, 1f),
                "sponge pore organic atlas cutout handoff for porous reef organism mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3437",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/UvAtlases/TX_B34_3437_kelp_holdfast_root_atlas_BaseColorCandidate.jpg",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Flora_KelpHoldfastRootAtlas_Opaque.mat",
                false,
                0.5f,
                new Color(0.72f, 0.86f, 0.72f, 1f),
                "kelp holdfast/root atlas opaque handoff for root pads and terrain contact meshes"),
            new AtlasMaterialSpec(
                "B34-3439",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3439_spore_pod_and_seed_sac_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_plant_giant.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Flora_SporePodSeedSacAtlas_Cutout.mat",
                true,
                0.42f,
                new Color(0.74f, 0.94f, 0.82f, 1f),
                "spore pod and seed sac atlas cutout handoff for harvestable flora mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3441",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3441_neutral_grazer_skin_uv_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Fauna_NeutralGrazerSkinAtlas_Cutout.mat",
                true,
                0.48f,
                new Color(0.78f, 0.92f, 0.95f, 1f),
                "neutral grazer skin atlas cutout handoff for first-route fauna mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3442",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3442_filter_feeder_gill_membrane_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Fauna_FilterFeederGillMembraneAtlas_Cutout.mat",
                true,
                0.44f,
                new Color(0.86f, 0.92f, 1.00f, 1f),
                "filter-feeder gill membrane atlas cutout handoff for translucent creature organs"),
            new AtlasMaterialSpec(
                "B34-3445",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3445_translucent_larva_egg_sac_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_egg_cluster.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Fauna_LarvaEggSacAtlas_Cutout.mat",
                true,
                0.40f,
                new Color(0.82f, 0.96f, 0.92f, 1f),
                "larva and egg sac atlas cutout handoff for ecology props and nest mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3446",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3446_scavenged_carcass_bone_flesh_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_large_threat.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Fauna_CarcassBoneFleshAtlas_Cutout.mat",
                true,
                0.46f,
                new Color(0.90f, 0.82f, 0.76f, 1f),
                "scavenged carcass bone/flesh atlas cutout handoff for environmental evidence props"),
            new AtlasMaterialSpec(
                "B34-3448",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/pickup_resource/TX_B34-3448_resource_nodule_pickup_uv_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_resource.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Pickup_ResourceNoduleAtlas_Cutout.mat",
                true,
                0.48f,
                Color.white,
                "resource nodule pickup atlas cutout handoff for 3D pickup mesh UV islands"),
            new AtlasMaterialSpec(
                "B34-3449",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/PickupAtlases/TX_B34_3449_industrial_salvage_small_parts_atlas_BaseColorCandidate.jpg",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Pickup_IndustrialSalvageSmallPartsAtlas_Opaque.mat",
                false,
                0.5f,
                Color.white,
                "industrial salvage small-parts atlas opaque handoff for 3D pickup mesh UVs"),
            new AtlasMaterialSpec(
                "B34-3450",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/pickup_salvage/TX_B34-3450_data_core_wet_circuit_ceramic_atlas_AlphaCandidate.png",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff/MAT_B34_Pickup_DataCoreCircuitAtlas_Cutout.mat",
                true,
                0.46f,
                Color.white,
                "data core wet circuit ceramic atlas cutout handoff for scanner/salvage prop mesh UV islands"),
        };

        [MenuItem("Hecton8/Art/Build Batch34 UV Atlas Material Handoff")]
        public static void ExecuteMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            ValidateSpecs();
            EnsureFolder(OutputFolder);

            int written = 0;
            for (int i = 0; i < Specs.Length; i++)
            {
                CreateOrUpdateMaterial(Specs[i]);
                written++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Batch34UvAtlasMaterialHandoffBuilder] Material handoffs written={written}");
        }

        private static void CreateOrUpdateMaterial(AtlasMaterialSpec spec)
        {
            Texture2D texture = RequireTexture(spec);
            Material template = RequireTemplateMaterial(spec);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material == null)
            {
                material = new Material(template);
                AssetDatabase.CreateAsset(material, spec.MaterialPath);
            }
            else
            {
                material.shader = template.shader;
                material.CopyPropertiesFromMaterial(template);
            }

            material.enableInstancing = template.enableInstancing;
            material.doubleSidedGI = template.doubleSidedGI;
            material.globalIlluminationFlags = template.globalIlluminationFlags;
            material.SetOverrideTag("RenderType", spec.UseAlphaClip ? "TransparentCutout" : "Opaque");
            SetTextureIfPresent(material, "_BaseMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetColorIfPresent(material, "_BaseColor", spec.Tint);
            SetColorIfPresent(material, "_Color", spec.Tint);
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            SetFloatIfPresent(material, "_Cull", 2f);
            SetFloatIfPresent(material, "_AlphaClip", spec.UseAlphaClip ? 1f : 0f);
            SetFloatIfPresent(material, "_Cutoff", spec.AlphaCutoff);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", spec.UseAlphaClip ? 0.36f : 0.28f);
            SetFloatIfPresent(material, "_ReceiveShadows", 1f);
            SetKeyword(material, "_ALPHATEST_ON", spec.UseAlphaClip);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", false);
            material.renderQueue = spec.UseAlphaClip ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            EditorUtility.SetDirty(material);
        }

        private static void ValidateSpecs()
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                AtlasMaterialSpec spec = Specs[i];
                RequireTexture(spec);
                RequireTemplateMaterial(spec);
            }
        }

        private static Texture2D RequireTexture(AtlasMaterialSpec spec)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexturePath);
            if (texture == null)
                throw new InvalidOperationException($"[Batch34UvAtlasMaterialHandoffBuilder] Missing texture for {spec.SourceId}: {spec.TexturePath}");

            return texture;
        }

        private static Material RequireTemplateMaterial(AtlasMaterialSpec spec)
        {
            Material template = AssetDatabase.LoadAssetAtPath<Material>(spec.TemplateMaterialPath);
            if (template == null)
                throw new InvalidOperationException($"[Batch34UvAtlasMaterialHandoffBuilder] Missing template material for {spec.SourceId}: {spec.TemplateMaterialPath}");

            return template;
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private readonly struct AtlasMaterialSpec
        {
            public readonly string SourceId;
            public readonly string TexturePath;
            public readonly string TemplateMaterialPath;
            public readonly string MaterialPath;
            public readonly bool UseAlphaClip;
            public readonly float AlphaCutoff;
            public readonly Color Tint;
            public readonly string Note;

            public AtlasMaterialSpec(
                string sourceId,
                string texturePath,
                string templateMaterialPath,
                string materialPath,
                bool useAlphaClip,
                float alphaCutoff,
                Color tint,
                string note)
            {
                SourceId = sourceId;
                TexturePath = texturePath;
                TemplateMaterialPath = templateMaterialPath;
                MaterialPath = materialPath;
                UseAlphaClip = useAlphaClip;
                AlphaCutoff = alphaCutoff;
                Tint = tint;
                Note = note;
            }
        }
    }
}
