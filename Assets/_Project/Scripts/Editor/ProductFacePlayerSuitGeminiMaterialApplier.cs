#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - ProductFacePlayerSuitGeminiMaterialApplier.cs
//
// Editor-only material handoff for generated player suit mesh sources.
// Source of truth stays in generated Gemini PBR manifests/materials; this tool
// copies curated slot materials into the ProductFace player-suit output folder
// so future prefab/mesh owners do not bind directly to transient proof mats.
// ============================================================================

namespace Hecton8.Editor.ProductFace
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public static class ProductFacePlayerSuitGeminiMaterialApplier
    {
        public const string OutputFolder = "Assets/_Project/Art/Generated/ProductFace/PlayerSuit/Materials";

        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607";

        private static readonly PlayerSuitGeminiMaterialSpec[] MaterialSpecs =
        {
            new PlayerSuitGeminiMaterialSpec(
                0,
                "GraphiteFabric",
                "MAT_GEN_PlayerSuit_Slot0_GraphiteFabric",
                "GeminiBiome_20260607",
                "gemini_biome_20260607_pressure_suit_fabric_composite",
                "Primary soft suit composite for glove, limb, harness, and graphite-rubber body zones."),
            new PlayerSuitGeminiMaterialSpec(
                1,
                "WetHardShell",
                "MAT_GEN_PlayerSuit_Slot1_WetHardShell",
                "Gemini_Batch20260607_MicroPanel",
                "gemini_Batch20260607_MicroPanel_blue_painted_metal",
                "Readable blue-gray NASA-punk hard-shell plates for chest, helmet, fins, and pressure armor zones."),
            new PlayerSuitGeminiMaterialSpec(
                2,
                "PatchTrim",
                "MAT_GEN_PlayerSuit_Slot2_PatchTrim",
                "Gemini_Batch20260608_TextureExpansion",
                "gemini_Batch20260608_TextureExpansion_b34_3422_pressure_suit_patch_trim_sheet",
                "Repair patch, seam tape, strap, pouch, and latch-adjacent textile trim zones."),
            new PlayerSuitGeminiMaterialSpec(
                3,
                "ViewportGlass",
                "MAT_GEN_PlayerSuit_Slot3_ViewportGlass",
                "Gemini_Batch20260607_MicroPanel",
                "gemini_Batch20260607_MicroPanel_smoky_acrylic_glass",
                "Smoky cyan pressure glass/acrylic for visor rim, scanner lens, and instrument edge zones."),
            new PlayerSuitGeminiMaterialSpec(
                -1,
                "GasketSealAux",
                "MAT_GEN_PlayerSuit_Aux_GasketSeal",
                "Gemini_Batch20260608_TextureExpansion",
                "gemini_Batch20260608_TextureExpansion_b34_3414_rubber_gasket_ring_trim_sheet",
                "Auxiliary pressure seal/gasket material for future suit rim, wrist, ankle, and hose socket submesh splits."),
            new PlayerSuitGeminiMaterialSpec(
                -1,
                "RibbedHoseAux",
                "MAT_GEN_PlayerSuit_Aux_RibbedHose",
                "Gemini_Batch20260608_TextureExpansion",
                "gemini_Batch20260608_TextureExpansion_b34_3416_ribbed_flexible_hose_material",
                "Auxiliary ribbed hose material for oxygen/coolant hose geometry and first-person suit cable details."),
            new PlayerSuitGeminiMaterialSpec(
                -1,
                "SafetyLatchAux",
                "MAT_GEN_PlayerSuit_Aux_SafetyLatch",
                "Gemini_Batch20260607_MicroPanel",
                "gemini_Batch20260607_MicroPanel_orange_safety_composite",
                "Auxiliary orange safety accent material for latch blocks and readable emergency/service tabs.")
        };

        [MenuItem("Hecton8/Art/Apply Gemini Player Suit Material Palette")]
        public static void ApplyFromMenu()
        {
            Apply(true);
        }

        public static PlayerSuitGeminiMaterialApplyReport Apply(bool importFirst)
        {
            if (importFirst)
                Hecton8.Editor.ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            ValidateSourceMaterials();
            EnsureFolder(OutputFolder);

            PlayerSuitGeminiMaterialApplyReport report = new PlayerSuitGeminiMaterialApplyReport();
            for (int i = 0; i < MaterialSpecs.Length; i++)
            {
                PlayerSuitGeminiMaterialSpec spec = MaterialSpecs[i];
                ApplyMaterial(spec);
                report.MaterialsWritten++;
            }

            AssetDatabase.SaveAssets();
            report.Success = true;
            report.OutputFolder = OutputFolder;

            Debug.Log($"[ProductFacePlayerSuitGeminiMaterialApplier] Applied player suit material palette. materials={report.MaterialsWritten}, output={OutputFolder}");

            return report;
        }

        public static PlayerSuitGeminiMaterialSpec[] GetSpecsForStaticAudit()
        {
            PlayerSuitGeminiMaterialSpec[] copy = new PlayerSuitGeminiMaterialSpec[MaterialSpecs.Length];
            Array.Copy(MaterialSpecs, copy, MaterialSpecs.Length);
            return copy;
        }

        public static string[] GetRequiredMaterialPathsForStaticAudit()
        {
            string[] paths = new string[MaterialSpecs.Length];
            for (int i = 0; i < MaterialSpecs.Length; i++)
                paths[i] = MaterialSpecs[i].OutputPath;
            return paths;
        }

        public static string[] GetPrimarySlotMaterialPathsForStaticAudit()
        {
            string[] paths = new string[4];
            int written = 0;
            for (int i = 0; i < MaterialSpecs.Length; i++)
            {
                if (MaterialSpecs[i].SlotIndex < 0)
                    continue;

                if (MaterialSpecs[i].SlotIndex >= paths.Length)
                    continue;

                paths[MaterialSpecs[i].SlotIndex] = MaterialSpecs[i].OutputPath;
                written++;
            }

            if (written != paths.Length)
                throw new InvalidOperationException("Player suit primary material slot contract must contain exactly four slots.");

            return paths;
        }

        private static void ApplyMaterial(PlayerSuitGeminiMaterialSpec spec)
        {
            string sourcePath = spec.SourceMaterialPath;
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
                throw new InvalidOperationException("[ProductFacePlayerSuitGeminiMaterialApplier] Missing generated source material for player suit slot " + spec.SlotName + ": " + sourcePath);

            Material target = AssetDatabase.LoadAssetAtPath<Material>(spec.OutputPath);
            if (target == null)
            {
                target = new Material(source)
                {
                    name = spec.OutputName,
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(target, spec.OutputPath);
            }
            else
            {
                target.shader = source.shader;
                target.CopyPropertiesFromMaterial(source);
                target.name = spec.OutputName;
                target.enableInstancing = true;
            }

            AssetDatabase.SetLabels(target, spec.UnityLabels);
            EditorUtility.SetDirty(target);
        }

        private static void ValidateSourceMaterials()
        {
            int missing = 0;
            string firstMissing = string.Empty;
            for (int i = 0; i < MaterialSpecs.Length; i++)
            {
                string sourcePath = MaterialSpecs[i].SourceMaterialPath;
                if (AssetDatabase.LoadAssetAtPath<Material>(sourcePath) != null)
                    continue;

                missing++;
                if (string.IsNullOrEmpty(firstMissing))
                    firstMissing = sourcePath;
            }

            if (missing > 0)
                throw new InvalidOperationException($"[ProductFacePlayerSuitGeminiMaterialApplier] Missing generated source materials. missing={missing}, first={firstMissing}");
        }

        private static void EnsureFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/').Trim('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("Asset folder must start with Assets/: " + folder);

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public readonly struct PlayerSuitGeminiMaterialSpec
        {
            public readonly int SlotIndex;
            public readonly string SlotName;
            public readonly string OutputName;
            public readonly string Provider;
            public readonly string SourceMaterialId;
            public readonly string Role;

            public PlayerSuitGeminiMaterialSpec(
                int slotIndex,
                string slotName,
                string outputName,
                string provider,
                string sourceMaterialId,
                string role)
            {
                SlotIndex = slotIndex;
                SlotName = slotName;
                OutputName = outputName;
                Provider = provider;
                SourceMaterialId = sourceMaterialId;
                Role = role;
            }

            public string SourceMaterialPath => MaterialRoot + "/" + Provider + "/MAT_EXT_" + Provider + "_" + SourceMaterialId + ".mat";

            public string OutputPath => OutputFolder + "/" + OutputName + ".mat";

            public string[] UnityLabels => SlotIndex >= 0
                ? new[] { "Gemini", "ProductFace", "PlayerSuit", "Slot" + SlotIndex.ToString() }
                : new[] { "Gemini", "ProductFace", "PlayerSuit", "Auxiliary" };
        }

        public sealed class PlayerSuitGeminiMaterialApplyReport
        {
            public bool Success;
            public int MaterialsWritten;
            public string OutputFolder;
        }
    }
}
#endif
