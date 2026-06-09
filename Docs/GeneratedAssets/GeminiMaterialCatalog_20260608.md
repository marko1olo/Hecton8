# Gemini Material Catalog 2026-06-08

Generated-material memory for future agents. Static validated; Unity import remains pending until Editor proof.

- catalog: `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialCatalog_20260608.json`
- latest alias: `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialCatalog_Latest.json`
- material manifests: 4
- material assets: 53
- source atlases: 23
- alpha candidates: 21
- padded atlas sources: 6
- split atlas candidate sources: 6 entries / 150 islands
- consumer bindings: 124
- source consumer bindings: 43
- Batch34 selected Gemini sources: 50 / 50

## Material Manifests

- `Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json` - provider `GeminiBiome_20260607`, assets=10, unity=PENDING UNITY IMPORT
- `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260607_MicroPanel/GeminiMaterialAtlas_Manifest.json` - provider `Gemini_Batch20260607_MicroPanel`, assets=16, unity=PENDING UNITY IMPORT
- `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/GeminiMaterialAtlas_Manifest.json` - provider `Gemini_Batch20260608_TextureExpansion`, assets=18, unity=PENDING UNITY IMPORT
- `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json` - provider `GeminiSingles_20260607`, assets=9, unity=PENDING UNITY IMPORT

## Batch34 Intake And Curation

- entries: 50
- selected download sources: 50 / 50
- ignored duplicate download candidates: 1
- warnings: 23 entries / 23 warnings
- issues: 0 entries / 0 issues
- watermark detected/repaired: 1 / 1
- curation: CURATED_READY_ALPHA_SOURCE=5, CURATED_READY_STATIC=36, LOCAL_ONLY_OR_REGEN_SEAMLESS=1, LOCAL_ONLY_STATIC=2, MANUAL_SPLIT_BEFORE_IMPORT=1, PAD_OR_SPLIT_BEFORE_IMPORT=5
- verdict: INTAKE_READY_STATIC=27, REVIEW_REQUIRED=23
- source type: DECAL_ATLAS=11, PICKUP_ATLAS=3, SEAMLESS_TILE=15, TRIM_SHEET=6, UV_ATLAS=15

## Source Atlases

- `damage_decal`: 5
- `fauna_uv`: 4
- `flora_uv`: 6
- `glass_decal`: 3
- `organic_decal`: 2
- `pickup_resource`: 1
- `pickup_salvage`: 2

## Alpha Candidate Status

- `ALPHA_CANDIDATE_UNITY_SOURCE_PENDING_REVIEW`: 21

## Padded Atlas Sources

- `damage_decal`: 1
- `fauna_uv`: 3
- `flora_uv`: 1
- `organic_decal`: 1

## Split Atlas Candidates

- raw candidate atlases: 6
- raw candidate islands: 150
- use only after manual/UV review; padded atlases are the safer whole-atlas handoff.

## Consumer Bindings

- `construction_insulation_backing_material`: 1
- `construction_material`: 6
- `held_tool_prefab_renderer`: 14
- `imported_flora_texture_slots`: 8
- `player_suit_aux_material_palette`: 3
- `player_suit_material_palette`: 4
- `resource_pickup_material`: 8
- `terrain_layer_builder`: 8
- `tool_surface_detail_primitives`: 18
- `world_proxy_material`: 33
- `world_support_material`: 8
- `world_tool_prefab_renderer`: 13

## Source Consumer Bindings

- `batch34_uv_atlas_material_handoff`: 13
- `visor_trauma_decal_array_slice`: 16
- `world_support_generated_decal_material`: 10
- `world_support_generated_decal_prefab_child`: 4

## Integration Tools

- `Assets/_Project/Scripts/Editor/ExternalPbrTexturePackImporter.cs`
- `Tools/ValidateExternalPbrImporterBindings.py`
- `Tools/ValidateExternalPbrMaterialAssets.py`
- `Tools/BuildExternalPbrLitPreview.py`
- `Tools/ValidateExternalPbrLitPreview.py`
- `Tools/ValidateGeminiGeneratedMaterialState.py`
- `Tools/RunGeminiMaterialStaticPreflight.ps1`
- `Tools/RunExternalPbrUnityImport.ps1`
- `Tools/RunWorldProxyGeminiBiomeApply.ps1`
- `Assets/_Project/Scripts/Editor/WorldProxyGeminiBiomeMaterialApplier.cs`
- `Tools/ValidateWorldProxyGeminiBiomeAssignments.py`
- `Assets/_Project/Scripts/Editor/HeldToolExternalPbrMaterialApplier.cs`
- `Tools/ValidateHeldToolExternalPbrRules.py`
- `Tools/RunHeldToolExternalPbrApply.ps1`
- `Assets/_Project/Scripts/Editor/WorldToolExternalPbrMaterialApplier.cs`
- `Tools/ValidateWorldToolExternalPbrRules.py`
- `Tools/RunWorldToolExternalPbrApply.ps1`
- `Assets/_Project/Scripts/Editor/ToolSurfaceDetailGeminiIntegrator.cs`
- `Tools/ValidateToolSurfaceDetailGeminiRoute.py`
- `Assets/_Project/Scripts/Editor/Batch34UvAtlasMaterialHandoffBuilder.cs`
- `Tools/ValidateBatch34UvAtlasMaterialHandoff.py`
- `Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs`
- `Tools/RunGeminiMaterialUnityApplyAll.ps1`
- `Tools/ValidateGeminiUnityApplyRunner.py`
- `Assets/_Project/Scripts/Editor/ConstructionGeminiMaterialApplier.cs`
- `Tools/ValidateConstructionGeminiMaterialAssignments.py`
- `Tools/RunConstructionGeminiMaterialApply.ps1`
- `Assets/_Project/Scripts/Editor/ConstructionInsulationBackingIntegrator.cs`
- `Tools/ValidateConstructionInsulationBackingRoute.py`
- `Tools/ApplyGeminiBiomeToFloraImported.py`
- `Tools/ValidateGeminiBiomeFloraImportedAssignments.py`
- `Tools/ValidateBatch34DirectPromptQueue.py`
- `Tools/ValidateBatch34TargetedPromptQueues.py`
- `Tools/ProcessBatch34RegenTargets.py`
- `Tools/ValidateBatch34RegenTargets.py`
- `Assets/_Project/Scripts/Editor/Batch34SourceAtlasImporter.cs`
- `Tools/ValidateBatch34SourceAtlasPack.py`
- `Tools/ValidateBatch34SourceAtlasImporter.py`
- `Assets/_Project/Scripts/Editor/Batch34TerrainLayerAssetBuilder.cs`
- `Tools/ValidateBatch34TerrainLayerBuilder.py`
- `Assets/_Project/Scripts/Editor/ProductFacePlayerSuitGeminiMaterialApplier.cs`
- `Tools/ValidateProductFacePlayerSuitGeminiMaterialRoute.py`
- `Assets/_Project/Scripts/Editor/ResourcePickupGeminiMaterialApplier.cs`
- `Tools/ValidateResourcePickupGeminiMaterialRoute.py`
- `Assets/_Project/Scripts/Editor/WorldSupportGeminiMaterialApplier.cs`
- `Tools/ValidateWorldSupportGeminiMaterialRoute.py`
- `Assets/_Project/Scripts/Editor/WorldSupportGeneratedDecalMaterialBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalAuthoring.cs`
- `Tools/PromoteBatch34AlphaCandidatesToUnitySources.py`
- `Tools/ValidateBatch34AlphaCandidatePack.py`
- `Tools/ExtractBatch34SourceAtlasAlphaCandidates.py`
- `Tools/OptimizeBatch34AlphaPngSources.py`
- `Tools/BuildBatch34PaddedNeedsWorkAtlases.py`
- `Tools/ValidateBatch34PaddedAtlasSources.py`
- `Tools/SplitBatch34NeedsWorkAtlases.py`
- `Tools/ValidateBatch34SplitAtlasCandidates.py`
- `Assets/_Project/Scripts/Editor/Batch34VisorTraumaDecalArrayIntegrator.cs`
- `Tools/ValidateBatch34VisorTraumaDecalArrayRoute.py`
- `Tools/ValidateBatch34VisorTraumaProfileCsv.py`

## Material Sources

- `Aegir Surface Foamless Wet Rock` - `GeminiBiome_20260607`, aegir_wet_surface_rock, ok, bound, seam 9.4831->1.0426
- `Pressure Suit Fabric Composite` - `GeminiBiome_20260607`, pressure_suit_fabric_composite, ok, bound, seam 5.7969->0.5908
- `Creature Bone Plate Material` - `GeminiBiome_20260607`, creature_bone_plate, ok, bound, seam 11.6079->1.4224
- `Pale Tube Coral Calcium` - `GeminiBiome_20260607`, pale_tube_coral_calcium, ok, bound, seam 10.5353->1.2807
- `Wet Basalt Cave Wall` - `GeminiBiome_20260607`, wet_basalt_cave_wall, ok, bound, seam 8.2689->0.9266
- `Hydrothermal Vent Mineral Crust` - `GeminiBiome_20260607`, hydrothermal_vent_mineral_crust, ok, bound, seam 15.0736->0.2155
- `Soft Jelly Membrane` - `GeminiBiome_20260607`, soft_jelly_membrane, ok, bound, seam 7.7363->0.8757
- `Abyssal Predator Hide` - `GeminiBiome_20260607`, abyssal_predator_hide, ok, bound, seam 3.6076->0.4149
- `Bioluminescent Coral Flesh` - `GeminiBiome_20260607`, bioluminescent_coral_flesh, ok, bound, seam 12.9421->1.4881
- `Living Kelp Frond Surface` - `GeminiBiome_20260607`, living_kelp_frond_surface, ok, bound, seam 3.3554->0.3875
- `Blue Painted Metal` - `Gemini_Batch20260607_MicroPanel`, clean_tool_housing, ok, bound
- `Black Grip Rubber` - `Gemini_Batch20260607_MicroPanel`, waterproof_rubber, ok, bound
- `Dark Anodized Metal` - `Gemini_Batch20260607_MicroPanel`, dark_anodized_tool_metal, ok, bound
- `Orange Safety Composite` - `Gemini_Batch20260607_MicroPanel`, safety_composite_panel, ok, bound
- `White Ceramic Casing` - `Gemini_Batch20260607_MicroPanel`, scientific_ceramic_casing, ok, bound
- `Fine Ribbed Trim` - `Gemini_Batch20260607_MicroPanel`, fine_corrugated_trim, ok, bound
- `Worn Steel Inset` - `Gemini_Batch20260607_MicroPanel`, aged_panel_steel, ok, bound
- `Smoky Acrylic Glass` - `Gemini_Batch20260607_MicroPanel`, pressure_acrylic_viewport, ok, bound
- `Gray Polymer` - `Gemini_Batch20260607_MicroPanel`, neutral_polymer_shell, ok, bound
- `Salt Scuffed Metal` - `Gemini_Batch20260607_MicroPanel`, salt_scuffed_tool_metal, ok, bound
- `Clean Graphite Panel` - `Gemini_Batch20260607_MicroPanel`, clean_graphite_panel, ok, bound
- `Aged Green Service Metal` - `Gemini_Batch20260607_MicroPanel`, aged_green_service_metal, ok, bound
- `Brushed Titanium` - `Gemini_Batch20260607_MicroPanel`, brushed_titanium, ok, bound
- `Black Gasket Rubber` - `Gemini_Batch20260607_MicroPanel`, black_gasket_rubber, ok, bound
- `Repaired Salvage Metal` - `Gemini_Batch20260607_MicroPanel`, repaired_salvage_metal, ok, bound
- `Matte Carbon Composite` - `Gemini_Batch20260607_MicroPanel`, matte_carbon_composite, ok, bound
- `Photic Limestone Rubble Shelf` - `Gemini_Batch20260608_TextureExpansion`, terrain_photic_photic_limestone_rubble_shelf, ok, bound
- `Shallow Seagrass Root-Mat Substrate` - `Gemini_Batch20260608_TextureExpansion`, terrain_photic_shallow_seagrass_root_mat_substrate, ok, bound
- `Brine Canyon Salt-Crust Silt` - `Gemini_Batch20260608_TextureExpansion`, terrain_brine_brine_canyon_salt_crust_silt, ok, bound
- `Abyssal Manganese Nodule Plain` - `Gemini_Batch20260608_TextureExpansion`, terrain_abyssal_abyssal_manganese_nodule_plain, ok, bound
- `Methane Hydrate Crack Vein` - `Gemini_Batch20260608_TextureExpansion`, terrain_cold_seep_methane_hydrate_crack_vein, ok, bound
- `Serpentinite Fault Rock` - `Gemini_Batch20260608_TextureExpansion`, terrain_geology_serpentinite_fault_rock, ok, bound
- `Clay Silt Turbidity Slope` - `Gemini_Batch20260608_TextureExpansion`, terrain_sediment_clay_silt_turbidity_slope, ok, bound
- `Limestone Cave Ceiling Mineral Drip` - `Gemini_Batch20260608_TextureExpansion`, terrain_cave_limestone_cave_ceiling_mineral_drip, ok, bound
- `Drowned Concrete Rubble` - `Gemini_Batch20260608_TextureExpansion`, hard_surface_ruin_drowned_concrete_rubble, ok, bound
- `Pressure Base Exterior Hull Trim Sheet` - `Gemini_Batch20260608_TextureExpansion`, hard_surface_trim_pressure_base_exterior_hull_trim_sheet, ok, bound
- `Pressure Base Interior Wall Trim Sheet` - `Gemini_Batch20260608_TextureExpansion`, hard_surface_trim_pressure_base_interior_wall_trim_sheet, ok, bound
- `Rubber Gasket Ring Trim Sheet` - `Gemini_Batch20260608_TextureExpansion`, rubber_trim_rubber_gasket_ring_trim_sheet, ok, bound
- `Ribbed Flexible Hose Material` - `Gemini_Batch20260608_TextureExpansion`, rubber_cable_ribbed_flexible_hose_material, ok, bound
- `Amber Emergency Lens Material` - `Gemini_Batch20260608_TextureExpansion`, glass_lens_amber_emergency_lens_material, ok, bound
- `Welded Seam And Rivet Row Trim Sheet` - `Gemini_Batch20260608_TextureExpansion`, hard_surface_trim_welded_seam_and_rivet_row_trim_sheet, ok, bound
- `Salvage Cut Cross-Section Trim Atlas` - `Gemini_Batch20260608_TextureExpansion`, hard_surface_trim_salvage_cut_cross_section_trim_atlas, ok, bound
- `Damped Insulation Blanket Material` - `Gemini_Batch20260608_TextureExpansion`, fabric_insulation_damped_insulation_blanket_material, ok, bound
- `Pressure Suit Patch Trim Sheet` - `Gemini_Batch20260608_TextureExpansion`, suit_fabric_pressure_suit_patch_trim_sheet, ok, bound
- `Wet Service Panel Biofilm` - `GeminiSingles_20260607`, wet_service_panel_biofilm, ok, bound, seam 15.0735->0.2283
- `Transparent Pressure Glass Edge Wear` - `GeminiSingles_20260607`, pressure_acrylic_viewport, ok, bound, seam 1.2414->1.2414
- `Salvage-Worn Repair Metal` - `GeminiSingles_20260607`, repaired_salvage_metal, ok, bound, seam 10.1272->1.1238
- `White Ceramic Sensor Casing` - `GeminiSingles_20260607`, scientific_ceramic_casing, ok, bound, seam 3.1278->0.3552
- `Dark Anodized Tool Metal` - `GeminiSingles_20260607`, dark_anodized_tool_metal, ok, bound, seam 7.8688->0.5778
- `Fine Ribbed Metal Trim` - `GeminiSingles_20260607`, fine_corrugated_trim, ok, bound, seam 2.9822->0.394
- `Orange Safety Composite Panel` - `GeminiSingles_20260607`, safety_composite_panel, ok, bound, seam 2.5816->0.2989
- `Black Waterproof Grip Rubber` - `GeminiSingles_20260607`, waterproof_rubber, ok, bound, seam 4.0347->0.4451
- `Clean NASA-Punk Tool Housing Metal` - `GeminiSingles_20260607`, clean_tool_housing, ok, bound, seam 3.1604->0.3583

## Source-Only Atlases

- `B34-3418` Thick Viewport Glass Edge Decal Atlas - `glass_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3423` Leak Rust Biofilm Decal Atlas - `damage_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3425` Salt Mineral Deposit Decal Atlas - `damage_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3426` Instrument Glass Smudge Alpha Decal Atlas - `glass_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3427` Pressure Crack Glass Decal Atlas - `glass_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3428` Warning Paint Stripe Decal Atlas - `damage_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3429` Cutter Burn Scorch Decal Atlas - `damage_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3430` Barnacle Colony Decal Variants - `organic_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3431` Wetness Rivulet Decal Atlas - `damage_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3432` Contamination Biohazard Stain Atlas - `organic_decal`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3433` Brine Vane Flora UV Atlas - `flora_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3434` Shallow Alien Seagrass Blade Atlas - `flora_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3435` Plate Coral Rim UV Atlas - `flora_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3436` Sponge Pore Organic Atlas - `flora_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3437` Kelp Holdfast Root Atlas - `flora_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3439` Spore Pod And Seed Sac Atlas - `flora_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3441` Neutral Grazer Skin UV Atlas - `fauna_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3442` Filter Feeder Gill Membrane Atlas - `fauna_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3445` Translucent Larva Egg Sac Atlas - `fauna_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3446` Scavenged Carcass Bone Flesh Atlas - `fauna_uv`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3448` Resource Nodule Pickup UV Atlas - `pickup_resource`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3449` Industrial Salvage Small Parts Atlas - `pickup_salvage`, PENDING SPLIT_OR_ALPHA_EXTRACTION
- `B34-3450` Data Core Wet Circuit Ceramic Atlas - `pickup_salvage`, PENDING SPLIT_OR_ALPHA_EXTRACTION

## Padded Atlas Handoff

- `B34-3424` Paint Chip Scratch Decal Atlas - `damage_decal`, PADDED_SOURCE_ATLAS_PENDING_UV_BINDING, edge alpha 0.0
- `B34-3438` Tube Worm Soft Crown Atlas - `flora_uv`, PADDED_SOURCE_ATLAS_PENDING_UV_BINDING, edge alpha 0.0
- `B34-3440` Cave Lichen Biofilm Sheet Atlas - `organic_decal`, PADDED_SOURCE_ATLAS_PENDING_UV_BINDING, edge alpha 0.0
- `B34-3443` Small Predator Jaw Fin Eye Atlas - `fauna_uv`, PADDED_SOURCE_ATLAS_PENDING_UV_BINDING, edge alpha 0.0
- `B34-3444` Armored Benthic Shell Atlas - `fauna_uv`, PADDED_SOURCE_ATLAS_PENDING_UV_BINDING, edge alpha 0.0
- `B34-3447` Creature Eye Lens Wet Organ Atlas - `fauna_uv`, PADDED_SOURCE_ATLAS_PENDING_UV_BINDING, edge alpha 0.0

## Split Atlas Raw Candidates

- `B34-3424` Paint Chip Scratch Decal Atlas - `damage_decal`, 36 islands, review before binding
- `B34-3438` Tube Worm Soft Crown Atlas - `flora_uv`, 17 islands, review before binding
- `B34-3440` Cave Lichen Biofilm Sheet Atlas - `organic_decal`, 28 islands, review before binding
- `B34-3443` Small Predator Jaw Fin Eye Atlas - `fauna_uv`, 10 islands, review before binding
- `B34-3444` Armored Benthic Shell Atlas - `fauna_uv`, 31 islands, review before binding
- `B34-3447` Creature Eye Lens Wet Organ Atlas - `fauna_uv`, 28 islands, review before binding
