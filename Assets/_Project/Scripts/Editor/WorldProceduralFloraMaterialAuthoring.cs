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
        private const string KelpTallMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat";
        private const string KelpPatchMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat";
        private const string KelpCanopyMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat";
        private const string CoralLowMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat";
        private const string CoralBranchingMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat";
        private const string CoralMassiveMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat";
        private const string CoralPlateMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat";

        [MenuItem("Hecton/Authoring/Apply Procedural Flora Materials", priority = 176)]
        public static void Apply()
        {
            Shader kelpShader = Shader.Find(KelpShaderName);
            if (kelpShader == null)
            {
                Debug.LogWarning($"[WorldProceduralFloraMaterialAuthoring] Missing shader '{KelpShaderName}'.");
                return;
            }

            int touchedMaterials = 0;
            if (ApplyKelpMaterial(KelpTallMaterialPath, kelpShader, new Color(0.18f, 0.46f, 0.24f), new Color(0.34f, 0.70f, 0.38f), new Color(0.18f, 0.48f, 0.30f), new Color(0.28f, 0.74f, 0.38f), 0.07f, 1.6f))
                touchedMaterials++;

            if (ApplyKelpMaterial(KelpPatchMaterialPath, kelpShader, new Color(0.14f, 0.40f, 0.22f), new Color(0.30f, 0.62f, 0.34f), new Color(0.16f, 0.44f, 0.28f), new Color(0.24f, 0.66f, 0.34f), 0.06f, 1.4f))
                touchedMaterials++;

            if (ApplyKelpMaterial(KelpCanopyMaterialPath, kelpShader, new Color(0.20f, 0.52f, 0.26f), new Color(0.42f, 0.80f, 0.46f), new Color(0.20f, 0.54f, 0.34f), new Color(0.32f, 0.82f, 0.42f), 0.09f, 2.0f))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralLowMaterialPath))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralBranchingMaterialPath))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralMassiveMaterialPath))
                touchedMaterials++;

            if (ApplyCoralMaterial(CoralPlateMaterialPath))
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
            float swayFrequency)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Debug.LogWarning($"[WorldProceduralFloraMaterialAuthoring] Missing material '{materialPath}'.");
                return false;
            }

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

            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_TipColor", tipColor);
            material.SetColor("_RimColor", rimColor);
            material.SetColor("_TransmissionColor", transmissionColor);
            material.SetFloat("_Smoothness", 0.36f);
            material.SetFloat("_AmbientStrength", 0.46f);
            material.SetFloat("_RimPower", 3.1f);
            material.SetFloat("_RimStrength", 0.34f);
            material.SetFloat("_TransmissionStrength", 0.52f);
            material.SetFloat("_VertexTintStrength", 0.82f);
            material.SetFloat("_AgeDarkening", 0.22f);
            material.SetFloat("_MoistureBoost", 0.18f);
            material.SetFloat("_DetailStrength", 0.34f);
            material.SetFloat("_NormalStrength", 0.82f);
            material.SetFloat("_ThicknessStrength", 0.64f);
            material.SetFloat("_SpecularNoiseStrength", 0.42f);
            material.SetFloat("_CausticStrength", 0.24f);
            material.SetFloat("_CausticScale", 1.8f);
            material.SetFloat("_CausticSpeed", 0.58f);
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
            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool ApplyCoralMaterial(string materialPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Debug.LogWarning($"[WorldProceduralFloraMaterialAuthoring] Missing material '{materialPath}'.");
                return false;
            }

            material.enableInstancing = true;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetFloat("_ReceiveShadows", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_GlossyReflections", 0f);
            EditorUtility.SetDirty(material);
            return true;
        }

        private static string ResolveFamilyIdFromMaterialPath(string materialPath)
        {
            if (materialPath == KelpTallMaterialPath)
                return "family.kelp.tall";

            if (materialPath == KelpPatchMaterialPath)
                return "family.kelp.patch.dense";

            if (materialPath == KelpCanopyMaterialPath)
                return "family.kelp.canopy";

            return string.Empty;
        }
    }
}
