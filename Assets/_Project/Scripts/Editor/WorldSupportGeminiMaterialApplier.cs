#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - WorldSupportGeminiMaterialApplier.cs
//
// Editor-only generated-material handoff for procedural world-support final
// materials. Existing support prefabs reference Mat_Support_* assets by GUID;
// this tool updates those assets through Unity API and keeps prefab references
// stable across rebuilds.
// ============================================================================

namespace Hecton8.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;

    public static class WorldSupportGeminiMaterialApplier
    {
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607";

        private static readonly Assignment[] Assignments =
        {
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ResourcePocket.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3404_abyssal_manganese_nodule_plain",
                "Gemini_Batch20260608_TextureExpansion",
                new Color(0.82f, 0.66f, 0.32f, 1f),
                "Resource pocket support uses manganese nodule seabed detail; current primitive shards keep stable GUIDs until pickup meshes replace them."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_HazardPocket.mat",
                "gemini_biome_20260607_hydrothermal_vent_mineral_crust",
                "GeminiBiome_20260607",
                new Color(0.86f, 0.42f, 0.18f, 1f),
                "Hazard pocket support uses vent mineral crust to match bubble/sheen authoring instead of flat orange debug color."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_SafePocket.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3401_photic_limestone_rubble_shelf",
                "Gemini_Batch20260608_TextureExpansion",
                new Color(0.55f, 0.82f, 0.78f, 1f),
                "Safe pocket support uses bright photic limestone for readable early-route shelter material."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePassive.mat",
                "gemini_biome_20260607_soft_jelly_membrane",
                "GeminiBiome_20260607",
                new Color(0.46f, 0.82f, 0.58f, 1f),
                "Passive creature support uses wet jelly membrane detail for living spawn proxies."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePredator.mat",
                "gemini_biome_20260607_abyssal_predator_hide",
                "GeminiBiome_20260607",
                new Color(0.78f, 0.28f, 0.22f, 1f),
                "Predator support uses abyssal hide detail so threat proxies stop reading as red placeholder geometry."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_AbyssApex.mat",
                "gemini_biome_20260607_abyssal_predator_hide",
                "GeminiBiome_20260607",
                new Color(0.38f, 0.46f, 0.64f, 1f),
                "Abyss apex support keeps predator-hide material truth with colder depth tint."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ReefApex.mat",
                "gemini_biome_20260607_bioluminescent_coral_flesh",
                "GeminiBiome_20260607",
                new Color(0.86f, 0.76f, 0.42f, 1f),
                "Reef apex support uses bioluminescent coral flesh for organic reef-zone identity."),
            new Assignment(
                "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_RuinApex.mat",
                "gemini_Batch20260608_TextureExpansion_b34_3411_pressure_base_exterior_hull_trim_sheet",
                "Gemini_Batch20260608_TextureExpansion",
                new Color(0.42f, 0.68f, 0.82f, 1f),
                "Ruin apex support uses pressure-base exterior hull trim to align with industrial decal children.")
        };

        [MenuItem("Hecton8/Art/Apply Gemini World Support Materials")]
        public static void ApplyFromMenu()
        {
            Apply(true);
        }

        public static WorldSupportGeminiMaterialApplyReport Apply(bool importFirst)
        {
            if (importFirst)
                ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            ValidateSourceMaterials();
            WorldSupportGeminiMaterialApplyReport report = new WorldSupportGeminiMaterialApplyReport();
            for (int i = 0; i < Assignments.Length; i++)
            {
                Assignment assignment = Assignments[i];
                ApplyAssignment(assignment);
                report.MaterialsUpdated++;
            }

            AssetDatabase.SaveAssets();
            report.Success = true;
            Debug.Log($"[WorldSupportGeminiMaterialApplier] Applied world-support materials. materials={report.MaterialsUpdated}");

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
                throw new InvalidOperationException("[WorldSupportGeminiMaterialApplier] Missing generated source material: " + assignment.SourceMaterialPath);

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
            AssetDatabase.SetLabels(target, new[] { "Gemini", "WorldSupport", "ProductFace" });
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
                throw new InvalidOperationException($"[WorldSupportGeminiMaterialApplier] Missing generated source materials. missing={missing}, first={firstMissing}");
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

        public sealed class WorldSupportGeminiMaterialApplyReport
        {
            public bool Success;
            public int MaterialsUpdated;
        }
    }
}
#endif
