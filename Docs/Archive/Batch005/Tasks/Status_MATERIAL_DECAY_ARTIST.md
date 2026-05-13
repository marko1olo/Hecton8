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
- [x] Task 1: Singleton eradication N/A. DOD: no public Instance, no DontDestroyOnLoad, no manager registry singleton; runtime bridge is scene-local cold bootstrap only. Alternative rejected: material decay singleton manager. Estimate: 0.0us hot path.
- [x] Task 2: Consume ItemDurabilityChangedSignal. DOD: MaterialDecayRuntime drains SignalBus<ItemDurabilityChangedSignal>; PlayerInventory already publishes the signal. Alternative rejected: direct ToolDurabilitySystem/Inventory concrete polling. Estimate: span scan only on signal lane, 1-4us when events exist.
- [x] Task 3: ASMDEF isolation Hecton8.VFX.Materials -> Contracts. DOD: Hecton8.VFX.Materials.asmdef created with Core.Contracts plus Core for current SignalBus/GlobalSignals location. Alternative rejected: Assembly-CSharp coupling. Estimate: 0.0us hot path.
- [x] Task 4: Dead code hunt rust decal GameObjects. DOD: scan found no instantiated "Rust Decal" equipment GameObjects; only construction BaseDegradationSystem atlas constants outside this domain. Alternative rejected: deleting unrelated construction rupture decals. Estimate: 0.0us hot path.
- [x] Task 5: Add _RustDetailMap macro pack. DOD: Hecton_CoreLit.hlsl declares _RustDetailMap, sampler, and ST; pack uses R height, G/B normal XY, A roughness. Alternative rejected: per-tool texture set/material clone. Estimate: one sample on rust > 0.
- [x] Task 6: Cheap 4-step POM gated by rust scalar. DOD: POM loop sits behind rust > 0.3 and _MATH_LOD_LOW/runtime low-tier bypass; rust == 0 returns before texture sample. Alternative rejected: mesh pitting/deformation. Estimate: 4 samples only on high rust/high quality; 0 samples clean.
- [x] Task 7: Rust normal blend. DOD: packed G/B decoded into tangent-space rust normal and blended to world normal. Alternative rejected: second normal texture. Estimate: ALU only after shared packed sample.
- [x] Task 8: Edge wear fake. DOD: fresnel/curvature edge term biases rust without authored edge mask. Alternative rejected: edge-mask authoring/decal projectors. Estimate: dot + cheap power.
- [x] Task 9: _HectonPlayerBloodSplatter global. DOD: runtime drives stress + health damage from PlayerStressSignal and UIStateStore.Health01; shader overlays dark glossy patches. Alternative rejected: blood decals/material clones. Estimate: global vector upload only on change.
- [x] Task 10: Wetness factor. DOD: shader reads _InternalWaterlineY and runtime 5s recent-wet fade; smoothness pushes to 1.0. Alternative rejected: wet material swaps. Estimate: scalar math per shaded tool pixel.
- [x] Task 11: UV distortion. DOD: deep rust packed normals warp UV only after POM gate and low-tier bypass. Alternative rejected: warped mesh vertices. Estimate: ALU only on active rust path.
- [x] Task 12: Math LOD. DOD: _MATH_LOD_LOW compile branch plus runtime low-tier scalar blocks POM/distortion. Alternative rejected: full POM on MX350. Estimate: low tier pays blend only.
- [x] Task 13: AUP shift safety. DOD: wear math uses UV/tangent frame; world read is limited to waterline comparison. Alternative rejected: world-space rust simulation. Estimate: no CPU cost.
- [x] Task 14: Zero-GC scalar uploads. DOD: static property IDs, no per-frame strings/material clones; upload vectors only when values change except active wetness fade. Alternative rejected: renderer.material or MaterialPropertyBlock churn. Estimate: 0B/frame managed allocation after cold bootstrap.
- [x] Task 15: 512x512 shared rust atlas. DOD: one runtime global 512 RGBA atlas generated cold via NativeArray fallback and bound to _RustDetailMap. Alternative rejected: per-object rust maps. Estimate: ~1MB RGBA plus mips, cold only.
- [x] Task 16: Blackbox MaterialDecayState telemetry. DOD: fixed NativeArray<MaterialDecayState>[300] ring and Dump_MATERIAL_DECAY_ARTIST.bin fault writer. Alternative rejected: Debug.Log spam. Estimate: one ring write per tick.
- [x] Task 17: Audio sync ToolAcousticSignal pitch. DOD: rusted item durability events publish decoupled ToolAcousticSignal pitch drop. Alternative rejected: direct audio manager call. Estimate: signal push only on rust event.
- [x] Task 18: OMEGA compile/SRP batcher check [BLOCKED BY DEPENDENCY]. DOD: C# validation passed; Unity compile blocked by unrelated GlobalDataVault.cs Core.Memory errors and missing Hecton8.Vehicles.VFX assembly. SRP audit: new material props are in UnityPerMaterial CBUFFER; decay globals are global uniforms. Alternative rejected: claiming clean compile. Estimate: verification only.

## Loop Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md; status/rationale created; mandates loaded. No code written yet.
- Loop 1: Tasks 1-5 implemented/static-verified. Existing PlayerInventory signal publisher was reused; no rust decal equipment GameObjects found.
- Loop 2: Tasks 6-10 implemented. Shader path adds rust POM, normal/roughness blend, blood, and wetness without decals/material clones.
- Loop 3: Tasks 11-15 implemented. Low-tier/POM bypass, UV pit distortion, AUP-safe tangent math, zero-GC property IDs, and shared 512 runtime atlas added.
- Loop 4: Tasks 16-18 implemented/verified where possible. Native blackbox and ToolAcousticSignal bridge added; compile blocked by unrelated Core.Memory/assembly failures.
- Loop 5: Self-review fix. Removed Time.time from runtime uniform to stop forced per-frame uploads; moved _RustDetailMap sample behind rust == 0 early-out.
- Loop 6: Omega polish. Replaced exact rust-normal sqrt reconstruction with cheap visual fake; dotnet build requested by mandate and blocked by unrelated HectonUnderwaterVisuals.cs syntax errors.

## Verification Evidence
- MaterialDecayRuntime.cs Unity validate_script: 0 errors, 0 warnings.
- Console wall after refresh: shared workspace still red; latest visible error is unrelated HectonUnderwaterVisuals.cs line 7413. Earlier refresh also showed GlobalDataVault.cs and missing Hecton8.Vehicles.VFX assembly. No MaterialDecayRuntime.cs errors reported.
- dotnet build Hecton8.Core.csproj: blocked by unrelated HectonUnderwaterVisuals.cs syntax errors at 7040/7103.
- Static scans: _RustDetailMap, HectonCoreLitResolveDynamicWearUv, HectonCoreLitApplyDynamicWearPOM, _HectonPlayerBloodSplatter, _HectonMaterialDecayRuntime present.
- Tool materials: 12 existing placeholder tool materials now reference Hecton8/Tools/DecayLit shader GUID 8d6e37f0e8b94e56aaac0d9f25a11704.
