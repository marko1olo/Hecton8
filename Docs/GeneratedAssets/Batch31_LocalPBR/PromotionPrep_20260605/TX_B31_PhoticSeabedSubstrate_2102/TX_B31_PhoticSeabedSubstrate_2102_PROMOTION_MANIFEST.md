# PBR Texture Promotion Manifest: TX_B31_PhoticSeabedSubstrate_2102

- **Evidence Class**: `STATIC_IMAGE_PREP_ONLY`
- **Unity Imported**: `false`
- **Visual Acceptance**: `false`
- **Semantic Status**: `BLOCKED_CHANNEL_SEMANTICS`
- **Promotion Ready**: `false`

## Context & Boundary
These files represent local PBR source bakes prepped for future Unity inspection. They are NOT runtime-ready, NOT imported into Unity, and have NOT been validated for mip residency, VRAM consumption, frame-time spikes, GC, material binding, or channel semantics.

## Paths & Hashes

### Source Assets
- **Albedo Source**: [Albedo Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_AlbedoSource.png)
- **Normal Source**: [Normal Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_NormalSource.png)
- **MRAO Source**: [MRAO Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_MRAOSource.png)

### Generated Promotion Assets
- **Albedo PROMO**: [Albedo](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_PROMO_Albedo.png) (Hash: `0f29bb37314c417c3825aee193cb752347cd33ec59245f73c0d5d1d4a36cf898`)
- **Normal PROMO**: [Normal](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_PROMO_Normal.png) (Hash: `f096cdaab727376fdce7cb4d3778dba8310f5bb1d46ee922c26eb7a35b40a828`)
- **MRAO Candidate**: [MRAO Candidate](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_PROMO_MRAO_Candidate.png) (Hash: `cee2412ef6c856aaaabc3f1c2e4a2500273ce796c6595d4f15aa5c1bac96d68e`)
- **Channel Debug Sheet**: [Channel Debug](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_PROMO_ChannelDebug.png) (Hash: `8968f0b7b0fc7fcc30bed5c6814121be244ea2cf22da7b1204872ee1deebaa93`)
- **Albedo 2x2 Tile Preview**: [Albedo 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_PROMO_Albedo_2x2.png) (Hash: `70efbdd245f3bd0a28cd72366cddb8e88cf58a1a877a1fec831739c5570a4949`)
- **Normal 2x2 Tile Preview**: [Normal 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_PROMO_Normal_2x2.png) (Hash: `9af5bb5189ea4a12a051661034395966eb080501e2cc690763bfd3ec753b1802`)
- **MRAO 2x2 Tile Preview**: [MRAO 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_PROMO_MRAO_2x2.png) (Hash: `f292cd851e6e2a4cfca9400b254aa2be925465a71df659819b8812292197deea`)

## MRAO Candidate Channel Configuration (Blocked Semantics)
- **Red Channel**: Metallic (0.0 / Black)
- **Green Channel**: Candidate AO (Source MRAO Green; source semantics unverified)
- **Blue Channel**: Candidate Smoothness (Source MRAO Blue; source semantics unverified)
- **Alpha Channel**: Emission (0.0 / Black)

### Channel Statistics
- **Metallic**: Min = 0, Max = 0, Mean = 0.00, Std = 0.00
- **AO**: Min = 176, Max = 204, Mean = 185.80, Std = 2.97
- **Smoothness**: Min = 147, Max = 255, Mean = 213.31, Std = 21.34
- **Emission**: Min = 0, Max = 0, Mean = 0.00, Std = 0.00

## Target Unity Import Intent (Blocked Until Channel Decision)
- **Albedo**: `sRGB = true`, `Texture Type = Default`, `Compression = BC7`, `Generate Mip Maps = true`
- **Normal**: `sRGB = false`, `Texture Type = Normal map`, `Compression = BC5`, `Generate Mip Maps = true`
- **MRAO Candidate**: `sRGB = false`, `Texture Type = Default`, `Compression = BC7`, `Generate Mip Maps = true`; **do not import until channel semantics are resolved**

## Scalability Consequences
- **Low (Continuous Weight 0.0)**: Uses low-res (512px) texture mips, basic ambient occlusion, and disabled specular details to protect memory. The visual quality is kept at the absolute minimum acceptable standard.
- **Middle (Continuous Weight 0.5)**: Uses standard resolution (1024px) texture mips after channel-owner approval; normal maps provide standard depth breakup.
- **High (Continuous Weight 0.8)**: Uses approved resolution with enhanced normal detail after channel-owner approval; no terrain realism claim is made from this static artifact.
- **Ultra (Continuous Weight 1.0)**: Sensory overkill. Uses maximum texture mip levels, advanced micro-variation shaders, and extra near-field normal/specular detail mapping without changing authority routes.
