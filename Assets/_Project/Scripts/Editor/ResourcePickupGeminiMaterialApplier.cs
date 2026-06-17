#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - ResourcePickupGeminiMaterialApplier.cs
//
// Editor-only generated-material handoff for current resource pickup materials.
// Existing pickup prefabs reference Mat_Resource_* assets by GUID; this tool
// updates those material assets through Unity API instead of editing prefab YAML.
// Batch34 pickup atlases remain UV/mesh source assets, not cube material maps.
// ============================================================================

namespace Hecton8.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;

    public static class ResourcePickupGeminiMaterialApplier
    {
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607";

        private static readonly Assignment[] Assignments =
        {
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat",
                "gemini_Batch20260607_MicroPanel_repaired_salvage_metal",
                "Gemini_Batch20260607_MicroPanel",
                new Color(0.55f, 0.63f, 0.69f, 1f),
                "Titanium scrap uses repaired salvage metal detail while preserving readable scrap tint."),
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3404_abyssal_manganese_nodule_plain",
                "Gemini_Batch20260608_TextureExpansion",
                new Color(0.78f, 0.47f, 0.22f, 1f),
                "Copper ore uses nodule ground material as current primitive-pickup proxy; proper pickup atlas mesh remains future UV work."),
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat",
                "gemini_biome_20260607_pale_tube_coral_calcium",
                "GeminiBiome_20260607",
                new Color(0.72f, 0.82f, 0.92f, 1f),
                "Silica shards use pale calcium mineral detail as a bright shard proxy until dedicated silica pickup mesh source exists."),
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat",
                "gemini_biome_20260607_living_kelp_frond_surface",
                "GeminiBiome_20260607",
                new Color(0.28f, 0.66f, 0.38f, 1f),
                "Fiber kelp uses living frond surface detail for harvestable organic pickup readability."),
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat",
                "gemini_biome_20260607_soft_jelly_membrane",
                "GeminiBiome_20260607",
                new Color(0.42f, 0.78f, 0.62f, 1f),
                "Membrane tissue uses soft jelly membrane detail for wet translucent organic pickup readability."),
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat",
                "gemini_Batch20260607_MicroPanel_worn_steel_inset",
                "Gemini_Batch20260607_MicroPanel",
                new Color(0.75f, 0.78f, 0.84f, 1f),
                "Silver ore uses worn steel inset detail as a metallic pickup proxy while preserving pale silver identity."),
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat",
                "gemini_biome_20260607_hydrothermal_vent_mineral_crust",
                "GeminiBiome_20260607",
                new Color(0.88f, 0.82f, 0.24f, 1f),
                "Sulfur clumps use hydrothermal mineral crust detail for chemical seep material truth."),
            new Assignment(
                "Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat",
                "gemini_biome_20260607_bioluminescent_coral_flesh",
                "GeminiBiome_20260607",
                new Color(0.58f, 0.36f, 0.14f, 1f),
                "Hydrocarbon resin uses wet organic coral-flesh detail as a temporary rich organic pickup proxy.")
        };

        [MenuItem("Hecton8/Art/Apply Gemini Resource Pickup Materials")]
        public static void ApplyFromMenu()
        {
            Apply(true);
        }

        public static ResourcePickupGeminiMaterialApplyReport Apply(bool importFirst)
        {
            if (importFirst)
                ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            ValidateSourceMaterials();
            ResourcePickupGeminiMaterialApplyReport report = new ResourcePickupGeminiMaterialApplyReport();
            for (int i = 0; i < Assignments.Length; i++)
            {
                Assignment assignment = Assignments[i];
                ApplyAssignment(assignment);
                report.MaterialsUpdated++;
            }

            AssetDatabase.SaveAssets();
            report.Success = true;
            Debug.Log($"[ResourcePickupGeminiMaterialApplier] Applied resource pickup materials. materials={report.MaterialsUpdated}");

            return report;
        }

        public static Assignment[] GetAssignmentsForStaticAudit()
        {
            Assignment[] copy = new Assignment[Assignments.Length];
            Array.Copy(Assignments, copy, Assignments.Length);
            return copy;
        }

        public static bool AreSourceMaterialsAvailable()
        {
            for (int i = 0; i < Assignments.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(Assignments[i].SourceMaterialPath) == null)
                    return false;
            }

            return true;
        }

        private static void ApplyAssignment(Assignment assignment)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(assignment.SourceMaterialPath);
            if (source == null)
                throw new InvalidOperationException("[ResourcePickupGeminiMaterialApplier] Missing generated source material: " + assignment.SourceMaterialPath);

            Material target = AssetDatabase.LoadAssetAtPath<Material>(assignment.TargetMaterialPath);
            if (target == null)
            {
                target = new Material(source);
                AssetDatabase.CreateAsset(target, assignment.TargetMaterialPath);
            }
            else
            {
                target.shader = source.shader;
                target.CopyPropertiesFromMaterial(source);
            }

            target.name = System.IO.Path.GetFileNameWithoutExtension(assignment.TargetMaterialPath);
            target.enableInstancing = true;
            SetColorIfPresent(target, "_BaseColor", assignment.Tint);
            SetColorIfPresent(target, "_Color", assignment.Tint);
            AssetDatabase.SetLabels(target, new[] { "Gemini", "ResourcePickup", "ProductFace" });
            EditorUtility.SetDirty(target);
        }

        private static void ValidateSourceMaterials()
        {
            int missing = 0;
            string firstMissing = string.Empty;
            for (int i = 0; i < Assignments.Length; i++)
            {
                string sourcePath = Assignments[i].SourceMaterialPath;
                if (AssetDatabase.LoadAssetAtPath<Material>(sourcePath) != null)
                    continue;

                missing++;
                if (string.IsNullOrEmpty(firstMissing))
                    firstMissing = sourcePath;
            }

            if (missing > 0)
                throw new InvalidOperationException($"[ResourcePickupGeminiMaterialApplier] Missing generated source materials. missing={missing}, first={firstMissing}");
        }

        private static void SetColorIfPresent(Material material, string property, Color color)
        {
            if (material.HasProperty(property))
                material.SetColor(property, color);
        }

        public readonly struct Assignment
        {
            public readonly string TargetMaterialPath;
            public readonly string SourceMaterialId;
            public readonly string Provider;
            public readonly Color Tint;
            public readonly string Note;

            public Assignment(
                string targetMaterialPath,
                string sourceMaterialId,
                string provider,
                Color tint,
                string note)
            {
                TargetMaterialPath = targetMaterialPath;
                SourceMaterialId = sourceMaterialId;
                Provider = provider;
                Tint = tint;
                Note = note;
            }

            public string SourceMaterialPath => MaterialRoot + "/" + Provider + "/MAT_EXT_" + Provider + "_" + SourceMaterialId + ".mat";
        }

        public sealed class ResourcePickupGeminiMaterialApplyReport
        {
            public bool Success;
            public int MaterialsUpdated;
        }
    }
}
#endif
