using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Single Unity batch entry point for the generated Gemini/External PBR material integration chain.
    /// </summary>
    public static class GeminiMaterialIntegrationApplier
    {
        [MenuItem("Hecton8/Art/Apply Gemini Material Integration All")]
        public static void ExecuteMenu()
        {
            ApplyAll();
        }

        public static void ApplyAll()
        {
            try
            {
                RunStage("external_pbr_texture_pack_import", delegate { ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks(); });
                RunStage("batch34_source_atlas_import", delegate { Batch34SourceAtlasImporter.ImportBatch34SourceAtlases(); });
                RunStage("world_support_generated_decal_materials", delegate { WorldSupportGeneratedDecalMaterialBuilder.Build(); });
                RunStage("batch34_visor_trauma_decal_array", delegate { Batch34VisorTraumaDecalArrayIntegrator.BakeAndBindVisorTraumaArray(); });
                RunStage("batch34_terrain_layers", delegate { Batch34TerrainLayerAssetBuilder.BuildTerrainLayers(false); });
                RunStage("mapmagic_clean_terrain_material_route", ApplyCleanMapMagicTerrainMaterialRoute);
                RunStage("mapmagic_clean_terrain_material_validate", ValidateCleanMapMagicTerrainMaterialRoute);
                RunStage("product_face_player_suit_materials", delegate { Hecton8.Editor.ProductFace.ProductFacePlayerSuitGeminiMaterialApplier.Apply(false); });
                RunStage("resource_pickup_materials", delegate { ResourcePickupGeminiMaterialApplier.Apply(false); });
                RunStage("world_support_materials", delegate { WorldSupportGeminiMaterialApplier.Apply(false); });
                RunStage("held_tool_external_pbr_materials", delegate { HeldToolExternalPbrMaterialApplier.ApplyExternalPbrToHeldTools(false); });
                RunStage("world_tool_external_pbr_materials", delegate { WorldToolExternalPbrMaterialApplier.ApplyWorldToolMaterials(false); });
                RunStage("world_proxy_biome_materials", delegate { WorldProxyGeminiBiomeMaterialApplier.Apply(false); });
                RunStage("construction_materials", delegate { ConstructionGeminiMaterialApplier.Apply(false); });
                RunStage("tool_surface_detail_materials", delegate { ToolSurfaceDetailGeminiIntegrator.Apply(); });
                RunStage("batch34_uv_atlas_material_handoff", delegate { Batch34UvAtlasMaterialHandoffBuilder.Apply(); });
                RunStage("construction_insulation_backing", delegate { ConstructionInsulationBackingIntegrator.Apply(); });
                RunStage("asset_database_save", delegate { AssetDatabase.SaveAssets(); });
                RunStage("asset_database_refresh", delegate { AssetDatabase.Refresh(); });
                Debug.Log("[GeminiMaterialIntegrationApplier] Completed generated material integration apply-all.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[GeminiMaterialIntegrationApplier] Apply-all failed.\n" + exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        private static void RunStage(string stageName, Action stage)
        {
            Debug.Log("[GeminiMaterialIntegrationApplier] Stage start: " + stageName);
            try
            {
                stage();
            }
            catch (Exception exception)
            {
                Debug.LogError("[GeminiMaterialIntegrationApplier] Stage failed: " + stageName + "\n" + exception);
                throw;
            }

            Debug.Log("[GeminiMaterialIntegrationApplier] Stage complete: " + stageName);
        }

        private static void ApplyCleanMapMagicTerrainMaterialRoute()
        {
            Hecton8.Editor.HectonCleanTerrainMapMagicGraphIntegrator.IntegrationResult result =
                Hecton8.Editor.HectonCleanTerrainMapMagicGraphIntegrator.ApplyMaterialRoute(
                    Hecton8.Editor.HectonCleanTerrainMapMagicGraphIntegrator.ActiveSandboxGraphPath);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);
        }

        private static void ValidateCleanMapMagicTerrainMaterialRoute()
        {
            Hecton8.Editor.HectonCleanTerrainMapMagicGraphIntegrator.ValidationResult result =
                Hecton8.Editor.HectonCleanTerrainMapMagicGraphIntegrator.ValidateActiveSandboxGraph();
            if (!result.Success)
                throw new InvalidOperationException(result.Message);
        }
    }
}
