# PBR Texture Promotion Manifest: TX_B31_WetBasaltShoreline_1429

- **Evidence Class**: `STATIC_IMAGE_PREP_ONLY`
- **Unity Imported**: `false`
- **Visual Acceptance**: `false`
- **Semantic Status**: `BLOCKED_CHANNEL_SEMANTICS`
- **Promotion Ready**: `false`

## Context & Boundary
These files represent local PBR source bakes prepped for future Unity inspection. They are NOT runtime-ready, NOT imported into Unity, and have NOT been validated for mip residency, VRAM consumption, frame-time spikes, GC, material binding, or channel semantics.

## Paths & Hashes

### Source Assets
- **Albedo Source**: [Albedo Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_AlbedoSource.png)
- **Normal Source**: [Normal Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_NormalSource.png)
- **MRAO Source**: [MRAO Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_MRAOSource.png)

### Generated Promotion Assets
- **Albedo PROMO**: [Albedo](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_PROMO_Albedo.png) (Hash: `b7b25ec2925bd5804c309f95e85aeda3900d5d18553b2b33caf4af38c931b58d`)
- **Normal PROMO**: [Normal](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_PROMO_Normal.png) (Hash: `7d1e06c266ddb42ed9e8284388d983b2350d37ee9c47e9d999aa486009d5e96f`)
- **MRAO Candidate**: [MRAO Candidate](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_PROMO_MRAO_Candidate.png) (Hash: `372f539ad87dc89e9b39fb7a3853c88c32c00e1b12efc4b2330d71119f4ee8f9`)
- **Channel Debug Sheet**: [Channel Debug](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_PROMO_ChannelDebug.png) (Hash: `630032f6c6694ee2d2e42208c2cc1beedf5836a4ac6cfa2594b85f7e6bb00599`)
- **Albedo 2x2 Tile Preview**: [Albedo 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_PROMO_Albedo_2x2.png) (Hash: `0e216af5d60240a83fa1a85e56de8803e6359bd9258ef57c6bc855725ceff03a`)
- **Normal 2x2 Tile Preview**: [Normal 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_PROMO_Normal_2x2.png) (Hash: `bc401653383c2fbb356207f04a6da15327dd08f366ba7563c34bbc3ef4773c06`)
- **MRAO 2x2 Tile Preview**: [MRAO 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_PROMO_MRAO_2x2.png) (Hash: `532447a020758ea4fc4b37ff1abca3a04189c47a17b1efc497756a777d6a7721`)

## MRAO Candidate Channel Configuration (Blocked Semantics)
- **Red Channel**: Metallic (0.0 / Black)
- **Green Channel**: Candidate AO (Source MRAO Green; source semantics unverified)
- **Blue Channel**: Candidate Smoothness (Source MRAO Blue; source semantics unverified)
- **Alpha Channel**: Emission (0.0 / Black)

### Channel Statistics
- **Metallic**: Min = 0, Max = 0, Mean = 0.00, Std = 0.00
- **AO**: Min = 114, Max = 169, Mean = 151.02, Std = 8.92
- **Smoothness**: Min = 147, Max = 255, Mean = 188.60, Std = 23.69
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
