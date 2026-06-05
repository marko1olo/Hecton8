# Rationale 1801

## Decisions

- Selected evidence scope is static-only because the task forbids taking over the live Unity editor. Runtime visual quality remains PENDING UNITY/PLAYER-CAPTURE VERIFICATION.
- Selected mandates: QA_Evidence_Text_Filter_Audit, OPT_Cinematic_Cheat_Protocol_Visual_Fake_First, OPT_Performance_Budgets_FrameTime_VRAM_Limits, REND_URP_Graphics_HotPath_Optimization_HLOD, REND_Shader_Noir_Aesthetics_Dithering_Fog, REND_Abyssal_Lighting_Voxel_Occlusion_Shadows, REND_Terrain_VirtualTexturing, VOX_MapMagic_Voxel_Seam_Alignment_Integration.
- Surface/photic route acceptance cannot be inferred from YAML or static screenshots alone. Static evidence can identify assets, references, broken bindings, stale route assumptions, and required capture angles.
- Rejected the `Hecton8_Surface.prefab` missing-visual-material lead after inspection: the null `m_Material` hit is a `SphereCollider` physic-material slot, not the MeshRenderer material list.
- Classified `H8_SURFACE_OCEAN_READ_1428`, `H8_AEGIR_SKY_BACKDROP_1428`, and `SURFACE_GAS_GIANT_1428` as candidate/stale until Unity proves active renderer/material use; scene YAML shows inactive or disabled state for those candidates.
- Kept Ocean/Crest fixes in visual-fake/single-pass/material territory for future Unity work because source confirms Crest realtime depth and foam generation are disabled and the task explicitly forbids proposing Crest camera re-enable as the easy route.

## Authority Read

- AGENTS.md
- PROJECT_BIBLES.md
- VISION_LOCKS.md
- TASTE.md
- quality.md
- world.md
- terrain.md
- water.md
- rendering.md
- lighting.md
- presentation.md
- Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md
- Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md
- .agents-skills/README.md
