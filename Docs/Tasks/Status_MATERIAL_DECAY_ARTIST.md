# MATERIAL_DECAY_ARTIST Status

PROMPT IDENTIFIED: MATERIAL_DECAY_ARTIST
ROLE: VFX_TECHNICAL_ARTIST
DOMAIN: Hecton8.VFX.Materials / Dynamic Wear & Tear POM
TASK COUNT: 18
STATUS: PENDING VERIFICATION

## Mandates Loaded Before Coding
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_DescriptorBinding_Reality_Check.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- CORE_Tools_Equipment_Interaction_Raycast_Heat.txt

## State Machine
- [ ] Task 1: Singleton eradication N/A. DOD: verify no new singleton. Alternative rejected: manager singleton. Estimate: 0.0us hot path.
- [ ] Task 2: Consume ItemDurabilityChangedSignal. DOD: locate existing signal path or document dependency block. Alternative rejected: direct concrete coupling. Estimate: pending.
- [ ] Task 3: ASMDEF isolation Hecton8.VFX.Materials -> Contracts. DOD: asmdef dependency verified/created. Alternative rejected: broad Assembly-CSharp coupling. Estimate: 0.0us hot path.
- [ ] Task 4: Dead code hunt rust decal GameObjects. DOD: scan scene/assets/scripts. Alternative rejected: decal-based corrosion. Estimate: 0.0us hot path.
- [ ] Task 5: Add _RustDetailMap macro pack. DOD: SRP Batcher-compatible material property path. Alternative rejected: extra texture set per object. Estimate: shader fetch only.
- [ ] Task 6: Cheap 4-step POM gated by rust scalar. DOD: early-out when rust <= 0.3 and quality low disabled. Alternative rejected: real mesh pits/deformation. Estimate: <= 0.1ms GPU pending capture.
- [ ] Task 7: Rust normal blend. DOD: tangent-space blend from packed XY. Alternative rejected: separate normal texture. Estimate: one packed sample shared with Task 5.
- [ ] Task 8: Edge wear fake. DOD: math curvature/fresnel bias. Alternative rejected: authored edge mask texture. Estimate: ALU only.
- [ ] Task 9: _HectonPlayerBloodSplatter global. DOD: shader property plus runtime driver if available. Alternative rejected: material clones/decals. Estimate: scalar upload only.
- [ ] Task 10: Wetness factor. DOD: waterline/recent-submerge state without allocations. Alternative rejected: per-object wet material clone. Estimate: scalar math only.
- [ ] Task 11: UV distortion. DOD: gated by rust depth and disabled on low tier. Alternative rejected: geometry deformation. Estimate: ALU only.
- [ ] Task 12: Math LOD. DOD: low tier disables POM/distortion via shader keyword/preprocessor. Alternative rejected: runtime branching everywhere. Estimate: zero on low path except blend.
- [ ] Task 13: AUP shift safety. DOD: local-space material math only. Alternative rejected: world-space rust simulation. Estimate: 0.0us CPU.
- [ ] Task 14: Zero-GC scalar uploads. DOD: static property IDs, no per-frame allocation. Alternative rejected: string shader property access. Estimate: scalar upload only.
- [ ] Task 15: 512x512 shared rust atlas. DOD: generated/import setting or documented missing asset path. Alternative rejected: per-object rust maps. Estimate: 1MB class texture budget pending format.
- [ ] Task 16: Blackbox MaterialDecayState telemetry. DOD: fixed-size struct ring or dependency block. Alternative rejected: Debug.Log status spam. Estimate: <= 0.05ms debug path.
- [ ] Task 17: Audio sync ToolAcousticSignal pitch. DOD: decoupled signal path or dependency block. Alternative rejected: direct audio manager call. Estimate: scalar signal only.
- [ ] Task 18: Omega compile/SRP batcher check. DOD: Unity compile/console and static shader CBUFFER audit. Alternative rejected: chat-only report. Estimate: verification only.

## Loop Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md; status/rationale created; mandates loaded. No code written yet.
