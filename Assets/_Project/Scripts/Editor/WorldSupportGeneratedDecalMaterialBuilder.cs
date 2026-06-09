#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - WorldSupportGeneratedDecalMaterialBuilder.cs
//
// Editor-only material builder for first-party generated world-support decals.
// This replaces support-final reliance on vendor decal prefabs with Batch34
// source atlas alpha candidates while keeping prefab authoring deterministic.
// ============================================================================

namespace Hecton8.EditorTools
{
    using System;
    using UnityEditor;
    using UnityEngine;

    public static class WorldSupportGeneratedDecalMaterialBuilder
    {
        public const string OutputFolder = "Assets/_Project/Art/Materials/WorldSupport/Generated";
        public const string LeakRustMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_LeakRustBiofilmDecal.mat";
        public const string SaltMineralMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_SaltMineralDepositDecal.mat";
        public const string InstrumentGlassSmudgeMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_InstrumentGlassSmudgeDecal.mat";
        public const string ViewportGlassEdgeMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_ViewportGlassEdgeWearDecal.mat";
        public const string PressureGlassCrackMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_PressureGlassCrackDecal.mat";
        public const string WarningStripeMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_WarningStripeDecal.mat";
        public const string CutterScorchMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_CutterScorchDecal.mat";
        public const string BarnacleColonyMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_BarnacleColonyDecal.mat";
        public const string WetnessRivuletMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_WetnessRivuletDecal.mat";
        public const string ContaminationStainMaterialPath = OutputFolder + "/MAT_B34_WorldSupport_ContaminationStainDecal.mat";

        private static readonly DecalMaterialSpec[] Specs =
        {
            new DecalMaterialSpec(
                LeakRustMaterialPath,
                "B34-3423",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3423_leak_rust_biofilm_decal_atlas_AlphaCandidate.png",
                new Color(0.62f, 0.46f, 0.34f, 0.58f),
                "Leak/rust/biofilm support decal uses Batch34 residue atlas for old hull and base damage overlays."),
            new DecalMaterialSpec(
                SaltMineralMaterialPath,
                "B34-3425",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3425_salt_mineral_deposit_decal_atlas_AlphaCandidate.png",
                new Color(0.86f, 0.92f, 0.86f, 0.52f),
                "Salt/mineral support decal uses Batch34 deposit atlas for wet seam and cave-edge residue overlays."),
            new DecalMaterialSpec(
                InstrumentGlassSmudgeMaterialPath,
                "B34-3426",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/glass_decal/TX_B34-3426_instrument_glass_smudge_alpha_decal_atlas_AlphaCandidate.png",
                new Color(0.72f, 0.86f, 0.92f, 0.38f),
                "Instrument glass smudge support decal uses Batch34 glass wear atlas for viewport and panel overlays."),
            new DecalMaterialSpec(
                ViewportGlassEdgeMaterialPath,
                "B34-3418",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/glass_decal/TX_B34-3418_thick_viewport_glass_edge_decal_atlas_AlphaCandidate.png",
                new Color(0.70f, 0.88f, 0.96f, 0.42f),
                "Viewport glass edge support decal uses regenerated Batch34 glass-edge atlas for cockpit, scanner, and pressure-window rim wear."),
            new DecalMaterialSpec(
                PressureGlassCrackMaterialPath,
                "B34-3427",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/glass_decal/TX_B34-3427_pressure_crack_glass_decal_atlas_AlphaCandidate.png",
                new Color(0.76f, 0.92f, 0.98f, 0.62f),
                "Pressure glass crack support decal uses Batch34 glass crack atlas for viewport damage overlays."),
            new DecalMaterialSpec(
                WarningStripeMaterialPath,
                "B34-3428",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3428_warning_paint_stripe_decal_atlas_AlphaCandidate.png",
                new Color(1.0f, 0.78f, 0.46f, 0.78f),
                "Warning stripe support decal uses Batch34 worn warning paint atlas."),
            new DecalMaterialSpec(
                CutterScorchMaterialPath,
                "B34-3429",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3429_cutter_burn_scorch_decal_atlas_AlphaCandidate.png",
                new Color(0.82f, 0.52f, 0.36f, 0.62f),
                "Scuff support decal uses Batch34 cutter burn/scorch atlas as industrial damage overlay."),
            new DecalMaterialSpec(
                BarnacleColonyMaterialPath,
                "B34-3430",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/organic_decal/TX_B34-3430_barnacle_colony_decal_variants_AlphaCandidate.png",
                new Color(0.70f, 0.82f, 0.72f, 0.74f),
                "Barnacle colony support decal uses Batch34 organic colony atlas for hull and rock overgrowth overlays."),
            new DecalMaterialSpec(
                WetnessRivuletMaterialPath,
                "B34-3431",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3431_wetness_rivulet_decal_atlas_AlphaCandidate.png",
                new Color(0.62f, 0.78f, 0.82f, 0.34f),
                "Wetness/rivulet support decal uses Batch34 water-trail atlas for leak and damp-edge overlays."),
            new DecalMaterialSpec(
                ContaminationStainMaterialPath,
                "B34-3432",
                "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/organic_decal/TX_B34-3432_contamination_biohazard_stain_atlas_AlphaCandidate.png",
                new Color(0.42f, 0.74f, 0.48f, 0.50f),
                "Contamination support decal uses Batch34 organic residue atlas for restrained biofilm and hazard stains.")
        };

        [MenuItem("Hecton8/Art/Build Batch34 World Support Decal Materials")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static WorldSupportGeneratedDecalMaterialBuildReport Build()
        {
            EnsureFolder(OutputFolder);

            WorldSupportGeneratedDecalMaterialBuildReport report = new WorldSupportGeneratedDecalMaterialBuildReport();
            for (int i = 0; i < Specs.Length; i++)
            {
                DecalMaterialSpec spec = Specs[i];
                BuildSpec(spec);
                report.MaterialsUpdated++;
            }

            AssetDatabase.SaveAssets();
            report.Success = true;
            Debug.Log($"[WorldSupportGeneratedDecalMaterialBuilder] Built world-support decal materials. materials={report.MaterialsUpdated}");

            return report;
        }

        public static DecalMaterialSpec[] GetSpecsForStaticAudit()
        {
            DecalMaterialSpec[] copy = new DecalMaterialSpec[Specs.Length];
            Array.Copy(Specs, copy, Specs.Length);
            return copy;
        }

        public static bool AreSourceTexturesAvailable()
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(Specs[i].SourceTexturePath) == null)
                    return false;
            }

            return true;
        }

        private static void BuildSpec(DecalMaterialSpec spec)
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.SourceTexturePath);
            if (source == null)
                throw new InvalidOperationException("[WorldSupportGeneratedDecalMaterialBuilder] Missing generated decal source texture: " + spec.SourceId + " path=" + spec.SourceTexturePath);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("[WorldSupportGeneratedDecalMaterialBuilder] No supported decal shader found for " + spec.SourceId);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, spec.MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(spec.MaterialPath);
            material.enableInstancing = true;
            SetTextureIfPresent(material, "_BaseMap", source);
            SetTextureIfPresent(material, "_MainTex", source);
            SetColorIfPresent(material, "_BaseColor", spec.Tint);
            SetColorIfPresent(material, "_Color", spec.Tint);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.SetLabels(material, new[] { "Gemini", "WorldSupport", "Decal", spec.SourceId });
            EditorUtility.SetDirty(material);
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }

        private static void SetColorIfPresent(Material material, string property, Color color)
        {
            if (material.HasProperty(property))
                material.SetColor(property, color);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separatorIndex = path.LastIndexOf('/');
            string parentFolder = separatorIndex > 0 ? path.Substring(0, separatorIndex) : string.Empty;
            string newFolderName = separatorIndex > 0 ? path.Substring(separatorIndex + 1) : path;
            if (!string.IsNullOrEmpty(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
                EnsureFolder(parentFolder);

            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }

        public readonly struct DecalMaterialSpec
        {
            public readonly string MaterialPath;
            public readonly string SourceId;
            public readonly string SourceTexturePath;
            public readonly Color Tint;
            public readonly string Note;

            public DecalMaterialSpec(
                string materialPath,
                string sourceId,
                string sourceTexturePath,
                Color tint,
                string note)
            {
                MaterialPath = materialPath;
                SourceId = sourceId;
                SourceTexturePath = sourceTexturePath;
                Tint = tint;
                Note = note;
            }
        }

        public sealed class WorldSupportGeneratedDecalMaterialBuildReport
        {
            public bool Success;
            public int MaterialsUpdated;
        }
    }
}
#endif
