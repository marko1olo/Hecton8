# PBR Texture Promotion Manifest: TX_B31_PhoticShellSandSubstrate_2102

- **Evidence Class**: `STATIC_IMAGE_PREP_ONLY`
- **Unity Imported**: `false`
- **Visual Acceptance**: `false`
- **Semantic Status**: `BLOCKED_CHANNEL_SEMANTICS`
- **Promotion Ready**: `false`

## Context & Boundary
These files represent local PBR source bakes prepped for future Unity inspection. They are NOT runtime-ready, NOT imported into Unity, and have NOT been validated for mip residency, VRAM consumption, frame-time spikes, GC, material binding, or channel semantics.

## Paths & Hashes

### Source Assets
- **Albedo Source**: [Albedo Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_AlbedoSource.png)
- **Normal Source**: [Normal Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_NormalSource.png)
- **MRAO Source**: [MRAO Source File](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_MRAOSource.png)

### Generated Promotion Assets
- **Albedo PROMO**: [Albedo](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_PROMO_Albedo.png) (Hash: `b2143da41b9c0f489996fcca3502d457b01c40911a9e357ddbdf814d4fee4cd9`)
- **Normal PROMO**: [Normal](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_PROMO_Normal.png) (Hash: `eb8047e159a8f96f28ad826df9fa725577aaad160c0c78645ee75a606e2cb31f`)
- **MRAO Candidate**: [MRAO Candidate](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_PROMO_MRAO_Candidate.png) (Hash: `c5f60a647aeb58810f64b5c40e58d4c526c14586f99b956e20bea92725869aa0`)
- **Channel Debug Sheet**: [Channel Debug](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_PROMO_ChannelDebug.png) (Hash: `1295928556b9deee6adbc39f7bcdbe67610e7b773e175ae2d06759932937ab8f`)
- **Albedo 2x2 Tile Preview**: [Albedo 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_PROMO_Albedo_2x2.png) (Hash: `c922e7ab065b0a47a27bb8ee6690f3b9d1223eb2eff092e07c90f918ad05e938`)
- **Normal 2x2 Tile Preview**: [Normal 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_PROMO_Normal_2x2.png) (Hash: `0f9bdade30cd0b57b69d666627eaa9f7877978a811e38c961a3ddcc334f80185`)
- **MRAO 2x2 Tile Preview**: [MRAO 2x2](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_PROMO_MRAO_2x2.png) (Hash: `15f459eda29c7b3c1f5f094dd8fc4cfc790fed7dedd58628f010081f1c492a63`)

## MRAO Candidate Channel Configuration (Blocked Semantics)
- **Red Channel**: Metallic (0.0 / Black)
- **Green Channel**: Candidate AO (Source MRAO Green; source semantics unverified)
- **Blue Channel**: Candidate Smoothness (Source MRAO Blue; source semantics unverified)
- **Alpha Channel**: Emission (0.0 / Black)

### Channel Statistics
- **Metallic**: Min = 0, Max = 0, Mean = 0.00, Std = 0.00
- **AO**: Min = 184, Max = 207, Mean = 191.44, Std = 2.21
- **Smoothness**: Min = 147, Max = 255, Mean = 214.33, Std = 19.90
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
