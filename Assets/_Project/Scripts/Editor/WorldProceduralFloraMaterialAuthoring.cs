using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Owns editor-only shader/material assignment for procedural flora starter and baked-final materials.
    /// </summary>
    public static class WorldProceduralFloraMaterialAuthoring
    {
        private const string KelpShaderName = "Hecton8/Flora/KelpMaster";
        private const string KelpGpuiShaderName = "GPUInstancer/Hecton8/Flora/KelpMaster";
        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";
        private const string CoralGpuiShaderName = "GPUInstancer/Hecton8/Flora/CoralMaster";
        private const string KelpShaderPath = "Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader";
        private const string KelpGpuiShaderPath = "Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader";
        private const string CoralShaderPath = "Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader";
        private const string CoralGpuiShaderPath = "Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader";
        internal const string QualityMx350Keyword = "_QUALITY_MX350";
        internal const string QualityHighKeyword = "_QUALITY_HIGH";
        internal const string NormalScaleProperty = "_NormalScale";
        internal const string TriplanarScaleProperty = "_TriplanarScale";
        internal const string TriplanarSharpnessProperty = "_TriplanarSharpness";
        internal const string CurvatureWetnessStrengthProperty = "_CurvatureWetnessStrength";
        internal const string FresnelStrengthProperty = "_FresnelStrength";
        internal const string FresnelPowerProperty = "_FresnelPower";
        internal const string HeightScaleProperty = "_HeightScale";
        private const string KelpTallMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat";
        private const string KelpPatchMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat";
        private const string KelpCanopyMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat";
        private const string KelpAbyssalMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat";
        private const string CoralLowMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat";
        private const string CoralBranchingMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat";
        private const string CoralMassiveMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat";
        private const string CoralPlateMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat";
        private const string CoralBrittleMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat";

        [MenuItem("Hecton/Authoring/Apply Procedural Flora Materials", priority = 176)]
        public static void Apply()
        {
            Shader kelpShader = ResolvePreferredFloraShader(
                KelpGpuiShaderPath,
                KelpShaderPath,
                KelpGpuiShaderName,
                KelpShaderName);
            Shader coralShader = ResolvePreferredFloraShader(
                CoralGpuiShaderPath,
                CoralShaderPath,
                CoralGpuiShaderName,
                CoralShaderName);
            if (kelpShader == null)
            {
                Debug.LogWarning(
                    "[WorldProceduralFloraMaterialAuthoring] Missing kelp shader. Expected " +
                    DescribeExpectedShaderVariant("family.kelp.tall") +
                    ".");
                return;
            }

            if (coralShader == null)
            {
                Debug.LogWarning(
                    "[WorldProceduralFloraMaterialAuthoring] Missing coral shader. Expected " +
                    DescribeExpectedShaderVariant("family.coral.low") +
                    ".");
                return;
            }

            int touchedMaterials = 0;
            if (ApplyKelpMaterial(KelpTallMaterialPath, kelpShader, new Color(0.18f, 0.46f, 0.24f), new Color(0.34f, 0.70f, 0.38f), new Color(0.18f, 0.48f, 0.30f), new Color(0.28f, 0.74f, 0.38f), 0.07f, 1.6f))
                touchedMaterials++;

            if (ApplyKelpMaterial(KelpPatchMaterialPath, kelpShader, new Color(0.14f, 0.40f, 0.22f), new Color(0.30f, 0.62f, 0.34f), new Color(0.16f, 0.44f, 0.28f), new Color(0.24f, 0.66f, 0.34f), 0.06f, 1.4f))
                touchedMaterials++;

            if (ApplyKelpMaterial(KelpCanopyMaterialPath, kelpShader, new Color(0.20f, 0.52f, 0.26f), new Color(0.42f, 0.80f, 0.46f), new Color(0.20f, 0.54f, 0.34f), new Color(0.32f, 0.82f, 0.42f), 0.09f, 2.0f))
                touchedMaterials++;
            if (ApplyKelpMaterial(KelpAbyssalMaterialPath, kelpShader, new Color(0.04f, 0.07f, 0.08f), new Color(0.12f, 0.20f, 0.22f), new Color(0.16f, 0.48f, 0.54f), new Color(0.18f, 0.82f, 0.88f), 0.05f, 1.1f, 0.52f, new Color(0.20f, 0.90f, 0.88f)))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralLowMaterialPath, coralShader, new Color(0.50f, 0.30f, 0.28f), new Color(0.86f, 0.62f, 0.48f), new Color(0.22f, 0.62f, 0.68f), new Color(0.94f, 0.70f, 0.50f), 0.36f))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralBranchingMaterialPath, coralShader, new Color(0.42f, 0.24f, 0.30f), new Color(0.86f, 0.58f, 0.48f), new Color(0.26f, 0.66f, 0.72f), new Color(0.90f, 0.62f, 0.48f), 0.52f))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralMassiveMaterialPath, coralShader, new Color(0.56f, 0.32f, 0.24f), new Color(0.92f, 0.72f, 0.54f), new Color(0.24f, 0.60f, 0.64f), new Color(0.98f, 0.70f, 0.54f), 0.42f))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralPlateMaterialPath, coralShader, new Color(0.34f, 0.36f, 0.42f), new Color(0.84f, 0.78f, 0.60f), new Color(0.20f, 0.58f, 0.64f), new Color(0.88f, 0.72f, 0.56f), 0.46f))
                touchedMaterials++;
            if (ApplyCoralMaterial(CoralBrittleMaterialPath, coralShader, new Color(0.08f, 0.10f, 0.12f), new Color(0.34f, 0.46f, 0.48f), new Color(0.16f, 0.56f, 0.64f), new Color(0.24f, 0.70f, 0.72f), 0.58f, 0.34f, new Color(0.18f, 0.74f, 0.76f)))
                touchedMaterials++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WorldProceduralFloraMaterialAuthoring] Applied flora materials. TouchedMaterials={touchedMaterials}.");
        }

        private static bool ApplyKelpMaterial(
            string materialPath,
            Shader shader,
            Color baseColor,
            Color tipColor,
            Color rimColor,
            Color transmissionColor,
            float swayAmplitude,
            float swayFrequency,
            float biolumStrength = 0f,
            Color? biolumColorOverride = null)
        {
            Material material = LoadOrCreateMaterial(materialPath, shader);
            if (material == null)
                return false;

            material.shader = shader;
            material.enableInstancing = true;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            string familyId = ResolveFamilyIdFromMaterialPath(materialPath);
            Texture2D baseTexture = WorldProceduralFloraTextureAuthoring.LoadKelpBaseTexture(familyId);
            Texture2D detailTexture = WorldProceduralFloraTextureAuthoring.LoadKelpDetailTexture(familyId);
            Texture2D normalTexture = WorldProceduralFloraTextureAuthoring.LoadKelpNormalTexture(familyId);
            Texture2D maskTexture = WorldProceduralFloraTextureAuthoring.LoadKelpMaskTexture(familyId);
            if (baseTexture != null)
                material.SetTexture("_BaseMap", baseTexture);

            if (detailTexture != null)
                material.SetTexture("_DetailMap", detailTexture);

            if (normalTexture != null)
                material.SetTexture("_NormalMap", normalTexture);

            if (maskTexture != null)
                material.SetTexture("_MaskMap", maskTexture);

            bool hasImportedBaseTexture = IsImportedTexture(baseTexture);
            bool hasImportedDetailTexture = IsImportedTexture(detailTexture);
            bool hasImportedNormalTexture = IsImportedTexture(normalTexture);
            bool hasImportedMaskTexture = IsImportedTexture(maskTexture);
            bool hasAnyImportedTexture = hasImportedBaseTexture || hasImportedDetailTexture || hasImportedNormalTexture || hasImportedMaskTexture;
            bool hasCompleteImportedTextureSet = hasImportedBaseTexture && hasImportedDetailTexture && hasImportedNormalTexture && hasImportedMaskTexture;

            if (hasCompleteImportedTextureSet)
            {
                ApplySharedFloraShaderContract(material, 0.75f, 0.32f, 4.0f, 0.34f, 0.18f, 3.6f, 0.012f);
                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_TipColor", Color.Lerp(Color.white, tipColor, 0.12f));
                material.SetColor("_RimColor", Color.Lerp(Color.white, rimColor, 0.10f));
                material.SetColor("_TransmissionColor", Color.Lerp(Color.white, transmissionColor, 0.18f));
                material.SetFloat("_Smoothness", 0.20f);
                material.SetFloat("_VertexTintStrength", 0.42f);
                material.SetFloat("_AgeDarkening", 0.10f);
                material.SetFloat("_MoistureBoost", 0.14f);
                material.SetFloat("_DetailStrength", hasImportedDetailTexture ? 0.22f : 0.38f);
                material.SetFloat("_NormalStrength", hasImportedNormalTexture ? 0.82f : 0.96f);
                material.SetFloat("_BladeCurveNormalStrength", 0.18f);
                material.SetFloat("_ThicknessStrength", 0.68f);
                material.SetFloat("_SpecularNoiseStrength", 0.34f);
                material.SetFloat("_MidribDarkening", 0.06f);
                material.SetFloat("_MidribGlossBoost", 0.14f);
                material.SetFloat("_EdgeWearDarkening", 0.05f);
                material.SetFloat("_EdgeDetailBoost", 0.08f);
                material.SetFloat("_CausticStrength", 0.12f);
                material.SetFloat("_CausticScale", 1.5f);
                material.SetFloat("_CausticSpeed", 0.52f);
            }
            else if (hasAnyImportedTexture)
            {
                ApplySharedFloraShaderContract(material, 0.76f, 0.34f, 4.1f, 0.38f, 0.20f, 3.8f, 0.014f);
                material.SetColor("_BaseColor", Color.Lerp(baseColor, Color.white, 0.42f));
                material.SetColor("_TipColor", Color.Lerp(tipColor, Color.white, 0.18f));
                material.SetColor("_RimColor", Color.Lerp(rimColor, Color.white, 0.14f));
                material.SetColor("_TransmissionColor", Color.Lerp(transmissionColor, Color.white, 0.20f));
                material.SetFloat("_Smoothness", 0.22f);
                material.SetFloat("_VertexTintStrength", 0.58f);
                material.SetFloat("_AgeDarkening", 0.16f);
                material.SetFloat("_MoistureBoost", 0.18f);
                material.SetFloat("_DetailStrength", hasImportedDetailTexture ? 0.28f : 0.42f);
                material.SetFloat("_NormalStrength", hasImportedNormalTexture ? 0.88f : 1.00f);
                material.SetFloat("_BladeCurveNormalStrength", 0.20f);
                material.SetFloat("_ThicknessStrength", 0.72f);
                material.SetFloat("_SpecularNoiseStrength", 0.42f);
                material.SetFloat("_MidribDarkening", 0.10f);
                material.SetFloat("_MidribGlossBoost", 0.16f);
                material.SetFloat("_EdgeWearDarkening", 0.06f);
                material.SetFloat("_EdgeDetailBoost", 0.10f);
                material.SetFloat("_CausticStrength", 0.14f);
                material.SetFloat("_CausticScale", 1.56f);
                material.SetFloat("_CausticSpeed", 0.54f);
            }
            else
            {
                ApplySharedFloraShaderContract(material, 0.78f, 0.36f, 4.2f, 0.42f, 0.26f, 4.1f, 0.018f);
                material.SetColor("_BaseColor", baseColor);
                material.SetColor("_TipColor", tipColor);
                material.SetColor("_RimColor", rimColor);
                material.SetColor("_TransmissionColor", transmissionColor);
                material.SetFloat("_Smoothness", 0.24f);
                material.SetFloat("_VertexTintStrength", 0.96f);
                material.SetFloat("_AgeDarkening", 0.26f);
                material.SetFloat("_MoistureBoost", 0.24f);
                material.SetFloat("_DetailStrength", 0.52f);
                material.SetFloat("_NormalStrength", 1.08f);
                material.SetFloat("_BladeCurveNormalStrength", 0.24f);
                material.SetFloat("_ThicknessStrength", 0.82f);
                material.SetFloat("_SpecularNoiseStrength", 0.58f);
                material.SetFloat("_MidribDarkening", 0.16f);
                material.SetFloat("_MidribGlossBoost", 0.22f);
                material.SetFloat("_EdgeWearDarkening", 0.10f);
                material.SetFloat("_EdgeDetailBoost", 0.18f);
                material.SetFloat("_CausticStrength", 0.18f);
                material.SetFloat("_CausticScale", 1.65f);
                material.SetFloat("_CausticSpeed", 0.58f);
            }

            material.SetFloat("_AmbientStrength", 0.48f);
            material.SetFloat("_RimPower", 2.7f);
            material.SetFloat("_RimStrength", 0.28f);
            material.SetFloat("_TransmissionStrength", 0.68f);
            material.SetFloat("_EdgeTransmissionBoost", 0.34f);
            material.SetColor("_BiolumColor", biolumColorOverride ?? new Color(0.22f, 0.88f, 0.82f, 1f));
            material.SetFloat("_BiolumStrength", biolumStrength);
            material.SetFloat("_BiolumMaskStrength", 1.06f);
            material.SetFloat("_BiolumPulseAmplitude", 0.18f);
            material.SetFloat("_BiolumPulseFrequency", 0.72f);
            material.SetFloat("_BiolumCurrentResponse", 0.38f);
            material.SetFloat("_SwayAmplitude", swayAmplitude);
            material.SetFloat("_SwayFrequency", swayFrequency);
            material.SetFloat("_SwaySpeed", 0.85f);
            material.SetFloat("_SwayPhaseScale", 0.72f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_ReceiveShadows", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_GlossyReflections", 0f);
            material.SetFloat("_BumpScale", 1f);

            if (familyId == "family.kelp.tall")
            {
                material.SetColor("_BaseColor", new Color(0.54f, 0.66f, 0.44f, 1f));
                material.SetColor("_TipColor", new Color(0.68f, 0.78f, 0.54f, 1f));
                material.SetColor("_TransmissionColor", new Color(0.40f, 0.58f, 0.30f, 1f));
                material.SetColor("_RimColor", new Color(0.60f, 0.70f, 0.50f, 1f));
                material.SetFloat("_Smoothness", 0.17f);
                material.SetFloat("_NormalStrength", Mathf.Max(material.GetFloat("_NormalStrength"), 0.88f));
                material.SetFloat("_ThicknessStrength", Mathf.Max(material.GetFloat("_ThicknessStrength"), 0.76f));
                material.SetFloat("_VertexTintStrength", Mathf.Max(material.GetFloat("_VertexTintStrength"), 0.56f));
                material.SetFloat("_AgeDarkening", Mathf.Max(material.GetFloat("_AgeDarkening"), 0.22f));
                material.SetFloat("_AmbientStrength", 0.40f);
                material.SetFloat("_TransmissionStrength", 0.66f);
                material.SetFloat("_EdgeTransmissionBoost", 0.30f);
                material.SetFloat("_RimStrength", 0.16f);
                material.SetFloat("_CausticStrength", 0.08f);
                material.SetFloat("_CausticScale", 1.36f);
            }
            else if (familyId == "family.kelp.patch.dense")
            {
                material.SetColor("_BaseColor", new Color(0.60f, 0.76f, 0.52f, 1f));
                material.SetColor("_TipColor", new Color(0.72f, 0.84f, 0.60f, 1f));
                material.SetColor("_TransmissionColor", new Color(0.46f, 0.66f, 0.36f, 1f));
                material.SetColor("_RimColor", new Color(0.64f, 0.78f, 0.58f, 1f));
                material.SetFloat("_Smoothness", 0.16f);
                material.SetFloat("_NormalStrength", Mathf.Max(material.GetFloat("_NormalStrength"), 0.90f));
                material.SetFloat("_ThicknessStrength", Mathf.Max(material.GetFloat("_ThicknessStrength"), 0.74f));
                material.SetFloat("_VertexTintStrength", Mathf.Max(material.GetFloat("_VertexTintStrength"), 0.60f));
                material.SetFloat("_AgeDarkening", Mathf.Max(material.GetFloat("_AgeDarkening"), 0.18f));
                material.SetFloat("_AmbientStrength", 0.42f);
                material.SetFloat("_TransmissionStrength", 0.70f);
                material.SetFloat("_EdgeTransmissionBoost", 0.34f);
                material.SetFloat("_RimStrength", 0.18f);
                material.SetFloat("_CausticStrength", 0.08f);
                material.SetFloat("_CausticScale", 1.40f);
            }
            else if (familyId == "family.kelp.canopy")
            {
                material.SetColor("_BaseColor", new Color(0.72f, 0.88f, 0.58f, 1f));
                material.SetColor("_TipColor", new Color(0.78f, 0.92f, 0.62f, 1f));
                material.SetColor("_TransmissionColor", new Color(0.60f, 0.82f, 0.44f, 1f));
                material.SetColor("_RimColor", new Color(0.74f, 0.88f, 0.66f, 1f));
                material.SetFloat("_Smoothness", 0.18f);
                material.SetFloat("_NormalStrength", Mathf.Max(material.GetFloat("_NormalStrength"), 0.88f));
                material.SetFloat("_ThicknessStrength", Mathf.Max(material.GetFloat("_ThicknessStrength"), 0.78f));
                material.SetFloat("_VertexTintStrength", Mathf.Max(material.GetFloat("_VertexTintStrength"), 0.62f));
                material.SetFloat("_AgeDarkening", Mathf.Max(material.GetFloat("_AgeDarkening"), 0.18f));
                material.SetFloat("_AmbientStrength", 0.44f);
                material.SetFloat("_TransmissionStrength", 0.72f);
                material.SetFloat("_EdgeTransmissionBoost", 0.38f);
                material.SetFloat("_RimStrength", 0.22f);
                material.SetFloat("_CausticStrength", 0.10f);
                material.SetFloat("_CausticScale", 1.46f);
            }
            else if (familyId == "family.kelp.abyssal")
            {
                material.SetColor("_BaseColor", new Color(0.22f, 0.34f, 0.34f, 1f));
                material.SetColor("_TipColor", new Color(0.30f, 0.50f, 0.48f, 1f));
                material.SetColor("_TransmissionColor", new Color(0.16f, 0.34f, 0.32f, 1f));
                material.SetColor("_RimColor", new Color(0.30f, 0.48f, 0.48f, 1f));
                material.SetFloat("_Smoothness", 0.18f);
                material.SetFloat("_NormalStrength", Mathf.Max(material.GetFloat("_NormalStrength"), 0.88f));
                material.SetFloat("_ThicknessStrength", Mathf.Max(material.GetFloat("_ThicknessStrength"), 0.72f));
                material.SetFloat("_AmbientStrength", 0.36f);
                material.SetFloat("_RimStrength", 0.16f);
                material.SetFloat("_BiolumStrength", 0.18f);
                material.SetFloat("_BiolumMaskStrength", 0.64f);
                material.SetFloat("_BiolumPulseAmplitude", 0.06f);
                material.SetFloat("_BiolumPulseFrequency", 0.28f);
                material.SetColor("_BiolumColor", new Color(0.14f, 0.46f, 0.46f, 1f));
                material.SetFloat("_CausticStrength", 0.06f);
                material.SetFloat("_CausticScale", 1.12f);
            }

            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool IsImportedTexture(Texture texture)
        {
            if (texture == null)
                return false;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string normalizedPath = assetPath.Replace('\\', '/');
            return normalizedPath.Contains("/WorldProceduralFlora/Imported/");
        }

        private static bool ApplyCoralMaterial(
            string materialPath,
            Shader shader,
            Color baseColor,
            Color accentColor,
            Color rimColor,
            Color subsurfaceColor,
            float cavityStrength,
            float biolumStrength = 0f,
            Color? biolumColorOverride = null)
        {
            Material material = LoadOrCreateMaterial(materialPath, shader);
            if (material == null)
                return false;

            material.shader = shader;
            material.enableInstancing = true;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            string familyId = ResolveFamilyIdFromMaterialPath(materialPath);
            Texture2D baseTexture = WorldProceduralFloraTextureAuthoring.LoadCoralBaseTexture(familyId);
            Texture2D detailTexture = WorldProceduralFloraTextureAuthoring.LoadCoralDetailTexture(familyId);
            Texture2D normalTexture = WorldProceduralFloraTextureAuthoring.LoadCoralNormalTexture(familyId);
            Texture2D maskTexture = WorldProceduralFloraTextureAuthoring.LoadCoralMaskTexture(familyId);
            if (baseTexture != null)
                material.SetTexture("_BaseMap", baseTexture);

            if (detailTexture != null)
                material.SetTexture("_DetailMap", detailTexture);

            if (normalTexture != null)
                material.SetTexture("_NormalMap", normalTexture);

            if (maskTexture != null)
                material.SetTexture("_MaskMap", maskTexture);

            bool hasImportedBaseTexture = IsImportedTexture(baseTexture);
            bool hasImportedDetailTexture = IsImportedTexture(detailTexture);
            bool hasImportedNormalTexture = IsImportedTexture(normalTexture);
            bool hasImportedMaskTexture = IsImportedTexture(maskTexture);
            bool hasAnyImportedTexture = hasImportedBaseTexture || hasImportedDetailTexture || hasImportedNormalTexture || hasImportedMaskTexture;
            bool hasCompleteImportedTextureSet = hasImportedBaseTexture && hasImportedDetailTexture && hasImportedNormalTexture && hasImportedMaskTexture;

            if (hasCompleteImportedTextureSet)
            {
                ApplySharedFloraShaderContract(material, 0.72f, 0.40f, 4.6f, 0.46f, 0.18f, 4.2f, 0.022f);
                material.SetColor("_BaseColor", Color.white);
                material.SetColor("_AccentColor", Color.Lerp(Color.white, accentColor, 0.18f));
                material.SetColor("_RimColor", Color.Lerp(Color.white, rimColor, 0.12f));
                material.SetColor("_SubsurfaceColor", Color.Lerp(Color.white, subsurfaceColor, 0.22f));
                material.SetFloat("_Smoothness", 0.24f);
                material.SetFloat("_VertexTintStrength", 0.42f);
                material.SetFloat("_AgeDarkening", 0.10f);
                material.SetFloat("_MoistureBoost", 0.08f);
                material.SetFloat("_DetailStrength", hasImportedDetailTexture ? 0.26f : 0.36f);
                material.SetFloat("_NormalStrength", hasImportedNormalTexture ? 0.68f : 0.76f);
                material.SetFloat("_ThicknessStrength", 0.44f);
                material.SetFloat("_SpecularNoiseStrength", 0.26f);
                material.SetFloat("_CavityStrength", Mathf.Clamp01(cavityStrength * 0.72f));
                material.SetFloat("_CausticStrength", 0.12f);
                material.SetFloat("_CausticScale", 1.2f);
                material.SetFloat("_CausticSpeed", 0.28f);
            }
            else if (hasAnyImportedTexture)
            {
                ApplySharedFloraShaderContract(material, 0.74f, 0.42f, 4.7f, 0.52f, 0.20f, 4.4f, 0.024f);
                material.SetColor("_BaseColor", Color.Lerp(baseColor, Color.white, 0.34f));
                material.SetColor("_AccentColor", Color.Lerp(accentColor, Color.white, 0.22f));
                material.SetColor("_RimColor", Color.Lerp(rimColor, Color.white, 0.16f));
                material.SetColor("_SubsurfaceColor", Color.Lerp(subsurfaceColor, Color.white, 0.26f));
                material.SetFloat("_Smoothness", 0.25f);
                material.SetFloat("_VertexTintStrength", 0.52f);
                material.SetFloat("_AgeDarkening", 0.12f);
                material.SetFloat("_MoistureBoost", 0.10f);
                material.SetFloat("_DetailStrength", hasImportedDetailTexture ? 0.28f : 0.38f);
                material.SetFloat("_NormalStrength", hasImportedNormalTexture ? 0.70f : 0.80f);
                material.SetFloat("_ThicknessStrength", 0.46f);
                material.SetFloat("_SpecularNoiseStrength", 0.28f);
                material.SetFloat("_CavityStrength", Mathf.Clamp01(cavityStrength * 0.82f));
                material.SetFloat("_CausticStrength", 0.13f);
                material.SetFloat("_CausticScale", 1.26f);
                material.SetFloat("_CausticSpeed", 0.30f);
            }
            else
            {
                ApplySharedFloraShaderContract(material, 0.75f, 0.44f, 4.8f, 0.64f, 0.22f, 4.8f, 0.03f);
                material.SetColor("_BaseColor", baseColor);
                material.SetColor("_AccentColor", accentColor);
                material.SetColor("_RimColor", rimColor);
                material.SetColor("_SubsurfaceColor", subsurfaceColor);
                material.SetFloat("_Smoothness", 0.28f);
                material.SetFloat("_VertexTintStrength", 0.68f);
                material.SetFloat("_AgeDarkening", 0.18f);
                material.SetFloat("_MoistureBoost", 0.12f);
                material.SetFloat("_DetailStrength", 0.40f);
                material.SetFloat("_NormalStrength", 0.76f);
                material.SetFloat("_ThicknessStrength", 0.48f);
                material.SetFloat("_SpecularNoiseStrength", 0.34f);
                material.SetFloat("_CavityStrength", cavityStrength);
                material.SetFloat("_CausticStrength", 0.16f);
                material.SetFloat("_CausticScale", 1.4f);
                material.SetFloat("_CausticSpeed", 0.32f);
            }

            material.SetFloat("_AmbientStrength", 0.52f);
            material.SetFloat("_RimPower", 2.6f);
            material.SetFloat("_RimStrength", 0.22f);
            material.SetFloat("_SubsurfaceStrength", 0.34f);
            material.SetColor("_BiolumColor", biolumColorOverride ?? new Color(0.26f, 0.94f, 0.82f, 1f));
            material.SetFloat("_BiolumStrength", biolumStrength);
            material.SetFloat("_BiolumMaskStrength", 1.12f);
            material.SetFloat("_BiolumPulseAmplitude", 0.24f);
            material.SetFloat("_BiolumPulseFrequency", 0.56f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_ReceiveShadows", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_GlossyReflections", 0f);
            material.SetFloat("_BumpScale", 1f);

            if (familyId == "family.coral.low")
            {
                material.SetColor("_BaseColor", new Color(0.78f, 0.68f, 0.58f, 1f));
                material.SetColor("_AccentColor", new Color(0.88f, 0.72f, 0.56f, 1f));
                material.SetColor("_RimColor", new Color(0.72f, 0.64f, 0.58f, 1f));
                material.SetColor("_SubsurfaceColor", new Color(0.86f, 0.74f, 0.60f, 1f));
                material.SetFloat("_Smoothness", 0.22f);
                material.SetFloat("_AmbientStrength", 0.46f);
                material.SetFloat("_RimStrength", 0.14f);
                material.SetFloat("_SubsurfaceStrength", 0.22f);
                material.SetFloat("_CausticStrength", 0.08f);
                material.SetFloat("_CausticScale", 1.12f);
            }
            else if (familyId == "family.coral.branching")
            {
                material.SetColor("_BaseColor", new Color(0.74f, 0.62f, 0.60f, 1f));
                material.SetColor("_AccentColor", new Color(0.90f, 0.60f, 0.54f, 1f));
                material.SetColor("_RimColor", new Color(0.76f, 0.58f, 0.56f, 1f));
                material.SetColor("_SubsurfaceColor", new Color(0.88f, 0.68f, 0.60f, 1f));
                material.SetFloat("_Smoothness", 0.23f);
                material.SetFloat("_AmbientStrength", 0.48f);
                material.SetFloat("_RimStrength", 0.18f);
                material.SetFloat("_SubsurfaceStrength", 0.28f);
                material.SetFloat("_CausticStrength", 0.10f);
                material.SetFloat("_CausticScale", 1.18f);
            }
            else if (familyId == "family.coral.massive")
            {
                material.SetColor("_BaseColor", new Color(0.74f, 0.70f, 0.62f, 1f));
                material.SetColor("_AccentColor", new Color(0.84f, 0.74f, 0.58f, 1f));
                material.SetColor("_RimColor", new Color(0.72f, 0.68f, 0.60f, 1f));
                material.SetColor("_SubsurfaceColor", new Color(0.86f, 0.76f, 0.62f, 1f));
                material.SetFloat("_Smoothness", 0.22f);
                material.SetFloat("_AmbientStrength", 0.46f);
                material.SetFloat("_RimStrength", 0.14f);
                material.SetFloat("_SubsurfaceStrength", 0.22f);
                material.SetFloat("_CavityStrength", Mathf.Max(material.GetFloat("_CavityStrength"), 0.40f));
                material.SetFloat("_NormalStrength", Mathf.Max(material.GetFloat("_NormalStrength"), 0.72f));
                material.SetFloat("_CausticStrength", 0.08f);
                material.SetFloat("_CausticScale", 1.14f);
            }
            else if (familyId == "family.coral.plate")
            {
                material.SetColor("_BaseColor", new Color(0.70f, 0.68f, 0.60f, 1f));
                material.SetColor("_AccentColor", new Color(0.86f, 0.78f, 0.62f, 1f));
                material.SetColor("_SubsurfaceColor", new Color(0.88f, 0.80f, 0.64f, 1f));
                material.SetColor("_RimColor", new Color(0.76f, 0.72f, 0.62f, 1f));
                material.SetFloat("_Smoothness", 0.24f);
                material.SetFloat("_DetailStrength", Mathf.Max(material.GetFloat("_DetailStrength"), 0.30f));
                material.SetFloat("_NormalStrength", Mathf.Max(material.GetFloat("_NormalStrength"), 0.74f));
                material.SetFloat("_ThicknessStrength", Mathf.Max(material.GetFloat("_ThicknessStrength"), 0.56f));
                material.SetFloat("_CavityStrength", Mathf.Max(material.GetFloat("_CavityStrength"), 0.42f));
                material.SetFloat("_AmbientStrength", 0.44f);
                material.SetFloat("_SubsurfaceStrength", 0.28f);
                material.SetFloat("_RimStrength", 0.18f);
                material.SetFloat("_CausticStrength", 0.10f);
                material.SetFloat("_CausticScale", 1.18f);
            }
            else if (familyId == "family.coral.brittle")
            {
                material.SetColor("_BaseColor", new Color(0.52f, 0.58f, 0.56f, 1f));
                material.SetColor("_AccentColor", new Color(0.58f, 0.66f, 0.62f, 1f));
                material.SetColor("_RimColor", new Color(0.50f, 0.58f, 0.56f, 1f));
                material.SetColor("_SubsurfaceColor", new Color(0.44f, 0.52f, 0.50f, 1f));
                material.SetColor("_BiolumColor", new Color(0.10f, 0.38f, 0.38f, 1f));
                material.SetFloat("_Smoothness", 0.22f);
                material.SetFloat("_AmbientStrength", 0.40f);
                material.SetFloat("_BiolumStrength", 0.06f);
                material.SetFloat("_BiolumMaskStrength", 0.56f);
                material.SetFloat("_BiolumPulseAmplitude", 0.04f);
                material.SetFloat("_BiolumPulseFrequency", 0.24f);
                material.SetFloat("_CausticStrength", 0.05f);
                material.SetFloat("_CausticScale", 1.02f);
                material.SetFloat("_RimStrength", 0.10f);
                material.SetFloat("_SubsurfaceStrength", 0.12f);
                material.SetFloat("_VertexTintStrength", 0.30f);
                material.SetFloat("_ThicknessStrength", 0.36f);
            }

            EditorUtility.SetDirty(material);
            return true;
        }

        internal static bool TryGetShaderContractFailure(Material material, out string failureLabel)
        {
            if (material == null)
            {
                failureLabel = "missing-material";
                return true;
            }

            bool mx350Enabled = material.IsKeywordEnabled(QualityMx350Keyword);
            bool highEnabled = material.IsKeywordEnabled(QualityHighKeyword);
            if (!mx350Enabled || highEnabled)
            {
                failureLabel = "quality-keyword-mismatch";
                return true;
            }

            if (!HasPositiveFloatProperty(material, NormalScaleProperty))
            {
                failureLabel = "missing-_NormalScale";
                return true;
            }

            if (!HasPositiveFloatProperty(material, TriplanarScaleProperty))
            {
                failureLabel = "missing-_TriplanarScale";
                return true;
            }

            if (!HasPositiveFloatProperty(material, TriplanarSharpnessProperty))
            {
                failureLabel = "missing-_TriplanarSharpness";
                return true;
            }

            if (!HasPositiveFloatProperty(material, CurvatureWetnessStrengthProperty))
            {
                failureLabel = "missing-_CurvatureWetnessStrength";
                return true;
            }

            if (!HasPositiveFloatProperty(material, FresnelStrengthProperty))
            {
                failureLabel = "missing-_FresnelStrength";
                return true;
            }

            if (!HasPositiveFloatProperty(material, FresnelPowerProperty))
            {
                failureLabel = "missing-_FresnelPower";
                return true;
            }

            if (!HasPositiveFloatProperty(material, HeightScaleProperty))
            {
                failureLabel = "missing-_HeightScale";
                return true;
            }

            failureLabel = string.Empty;
            return false;
        }

        internal static bool IsAcceptedFloraShader(Shader shader, string familyId)
        {
            if (shader == null || string.IsNullOrWhiteSpace(familyId))
                return false;

            switch (ResolveFloraCategory(familyId))
            {
                case FloraShaderCategory.Kelp:
                    return IsAcceptedShaderVariant(shader, KelpShaderPath, KelpGpuiShaderPath, KelpShaderName, KelpGpuiShaderName);

                case FloraShaderCategory.Coral:
                    return IsAcceptedShaderVariant(shader, CoralShaderPath, CoralGpuiShaderPath, CoralShaderName, CoralGpuiShaderName);

                default:
                    return false;
            }
        }

        internal static string DescribeExpectedShaderVariant(string familyId)
        {
            switch (ResolveFloraCategory(familyId))
            {
                case FloraShaderCategory.Kelp:
                    return $"'{KelpShaderName}' or '{KelpGpuiShaderName}'";

                case FloraShaderCategory.Coral:
                    return $"'{CoralShaderName}' or '{CoralGpuiShaderName}'";

                default:
                    return "known flora shader variant";
            }
        }

        private static string ResolveFamilyIdFromMaterialPath(string materialPath)
        {
            if (materialPath == KelpTallMaterialPath)
                return "family.kelp.tall";

            if (materialPath == KelpPatchMaterialPath)
                return "family.kelp.patch.dense";

            if (materialPath == KelpCanopyMaterialPath)
                return "family.kelp.canopy";
            if (materialPath == KelpAbyssalMaterialPath)
                return "family.kelp.abyssal";

            if (materialPath == CoralLowMaterialPath)
                return "family.coral.low";

            if (materialPath == CoralBranchingMaterialPath)
                return "family.coral.branching";

            if (materialPath == CoralMassiveMaterialPath)
                return "family.coral.massive";

            if (materialPath == CoralPlateMaterialPath)
                return "family.coral.plate";
            if (materialPath == CoralBrittleMaterialPath)
                return "family.coral.brittle";

            return string.Empty;
        }

        private static Material LoadOrCreateMaterial(string materialPath, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;

            int lastSeparator = materialPath.LastIndexOf('/');
            if (lastSeparator <= 0)
            {
                Debug.LogWarning($"[WorldProceduralFloraMaterialAuthoring] Invalid material path '{materialPath}'.");
                return null;
            }

            string folderPath = materialPath.Substring(0, lastSeparator);
            EnsureFolder(folderPath);
            material = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(materialPath)
            };
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private static Shader ResolvePreferredFloraShader(
            string preferredAssetPath,
            string fallbackAssetPath,
            string preferredShaderName,
            string fallbackShaderName)
        {
            Shader shader = LoadShaderAsset(preferredAssetPath);
            if (shader != null)
                return shader;

            shader = LoadShaderAsset(fallbackAssetPath);
            if (shader != null)
                return shader;

            shader = Shader.Find(preferredShaderName);
            if (shader != null)
                return shader;

            return Shader.Find(fallbackShaderName);
        }

        private static Shader LoadShaderAsset(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
        }

        private static bool IsAcceptedShaderVariant(
            Shader shader,
            string baseShaderPath,
            string gpuiShaderPath,
            string baseShaderName,
            string gpuiShaderName)
        {
            if (shader == null)
                return false;

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (!string.IsNullOrWhiteSpace(shaderPath))
            {
                if (string.Equals(shaderPath, baseShaderPath, System.StringComparison.Ordinal)
                    || string.Equals(shaderPath, gpuiShaderPath, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return string.Equals(shader.name, baseShaderName, System.StringComparison.Ordinal)
                || string.Equals(shader.name, gpuiShaderName, System.StringComparison.Ordinal);
        }

        private static FloraShaderCategory ResolveFloraCategory(string familyId)
        {
            if (string.IsNullOrWhiteSpace(familyId))
                return FloraShaderCategory.Unknown;

            if (familyId.StartsWith("family.kelp.", System.StringComparison.Ordinal))
                return FloraShaderCategory.Kelp;

            if (familyId.StartsWith("family.coral.", System.StringComparison.Ordinal))
                return FloraShaderCategory.Coral;

            return FloraShaderCategory.Unknown;
        }

        private static void ApplySharedFloraShaderContract(
            Material material,
            float normalScale,
            float triplanarScale,
            float triplanarSharpness,
            float curvatureWetnessStrength,
            float fresnelStrength,
            float fresnelPower,
            float heightScale)
        {
            material.DisableKeyword(QualityHighKeyword);
            material.EnableKeyword(QualityMx350Keyword);
            material.SetFloat(NormalScaleProperty, normalScale);
            material.SetFloat(TriplanarScaleProperty, triplanarScale);
            material.SetFloat(TriplanarSharpnessProperty, triplanarSharpness);
            material.SetFloat(CurvatureWetnessStrengthProperty, curvatureWetnessStrength);
            material.SetFloat(FresnelStrengthProperty, fresnelStrength);
            material.SetFloat(FresnelPowerProperty, fresnelPower);
            material.SetFloat(HeightScaleProperty, heightScale);
        }

        private static bool HasPositiveFloatProperty(Material material, string propertyName)
        {
            if (material == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            if (material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.0001f)
                return true;

            float serializedValue;
            return TryGetSerializedFloat(material, propertyName, out serializedValue) && serializedValue > 0.0001f;
        }

        private static bool TryGetSerializedFloat(Material material, string propertyName, out float value)
        {
            value = 0f;
            if (material == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            SerializedObject serializedMaterial = new SerializedObject(material);
            SerializedProperty floatProperties = serializedMaterial.FindProperty("m_SavedProperties.m_Floats");
            if (floatProperties == null || !floatProperties.isArray)
                return false;

            for (int i = 0; i < floatProperties.arraySize; i++)
            {
                SerializedProperty floatEntry = floatProperties.GetArrayElementAtIndex(i);
                if (floatEntry == null)
                    continue;

                SerializedProperty keyProperty = floatEntry.FindPropertyRelative("first");
                SerializedProperty valueProperty = floatEntry.FindPropertyRelative("second");
                if (keyProperty == null || valueProperty == null)
                    continue;

                if (!string.Equals(keyProperty.stringValue, propertyName, System.StringComparison.Ordinal))
                    continue;

                value = valueProperty.floatValue;
                return true;
            }

            return false;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int lastSeparator = assetPath.LastIndexOf('/');
            if (lastSeparator <= 0)
                return;

            string parentPath = assetPath.Substring(0, lastSeparator);
            string folderName = assetPath.Substring(lastSeparator + 1);
            EnsureFolder(parentPath);

            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private enum FloraShaderCategory : byte
        {
            Unknown = 0,
            Kelp = 1,
            Coral = 2
        }
    }
}
