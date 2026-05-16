# Status_UBER_NOIR_INTEGRATOR

Agent: UBER_NOIR_INTEGRATOR
Domain: RENDERING/URP
Task count: 18
Status: PHASE 1 COMPILE-GATED BY UNRELATED DEPENDENCY

## Hygiene
- [x] Status file initialized | Justification: state-machine checklist is mandatory before shader edits | Alternative rejected: chat-only progress | Estimate: 35 us
- [x] Rationale file initialized | Justification: non-trivial shader integration decisions need durable disk memory | Alternative rejected: final-only rationale | Estimate: 35 us
- [x] Prompt extracted from CURRENT_BATCH.md | Justification: strict batch prompt protocol requires exact XML extraction by ID | Alternative rejected: using launcher instruction text | Estimate: 35 us
- [x] Domain boundary read | Justification: rendering ownership is constrained to Art/Shaders/Core and Rendering | Alternative rejected: broad project edits | Estimate: 35 us
- [x] Mandates selected/read | Justification: URP, fake-first, noir fog, performance budget, descriptor binding, and AUP rules drive this task | Alternative rejected: generic shader cleanup | Estimate: 250 us

## Core Checklist
- [ ] Task 01 PURGE_PASSES: Identify materials using more than 2 SetPass calls and merge properties into H8_UberNoir CBUFFER [BLOCKED BY DEPENDENCY FOR MATERIAL WRITE] | Justification: static scan identified 17 project materials bound to shaders with more than 2 passes; added material-facing `Hecton8/Rendering/UberNoir` shader and Editor API consolidator for DryZone hard-surface materials, but Unity executeMethod is blocked by unrelated existing script compile errors before the method can run | Alternative rejected: raw `.mat` YAML mass edit and converting terrain/flora/celestial domain shaders without their deformation semantics | Estimate: target 5 DryZone materials 4 passes -> 1 pass, approx 40-120 us render-thread state overhead saved when compile gate opens
- [x] Task 02 DEBT_CLEANUP: Eradicate fragment-stage _MainTex_ST polling and use manual UV transforms | Justification: `H8UberNoirSampleSurface` now consumes `uvPack` and `baseUvScale` emitted by the vertex shader; static search shows `_BaseMap_ST` only in CBUFFER/vertex path for UberNoir | Alternative rejected: fragment `TRANSFORM_TEX` and recomputing raw POM UV from ST in fragment | Estimate: approx 2 scalar uniform reads and 1 macro path removed per fragment
- [x] Task 03 DATA_EVICTION: Ensure _BiolumMasterPhase and _AupShiftOffset globals are sourced from DataVault bridge | Justification: added `HectonShaderGlobalDataVaultBridge` backed by `BufferID.ShaderGlobalState`; biolum and AUP publishers now write through the bridge instead of direct scattered global writes | Alternative rejected: direct `Shader.SetGlobalVector` ownership in Biolum/VFX/FloatingOrigin paths | Estimate: 0 GPU us; CPU write path remains O(1) with cached DataVault slot
- [x] Task 04 CAUSTIC_WIRING: Integrate analytical caustics into ForwardLit with low/high tier select path | Justification: `ForwardLit` evaluates UberNoir caustics; low tier uses 1D triangle-noise fake, high tier lerps procedural vs Snell-offset caustic map without a runtime branch | Alternative rejected: separate caustic material/pass and branch-gated texture sample path | Estimate: low tier avoids caustic texture fetch, high tier keeps one optional map sample
- [x] Task 05 RUST_POM_WIRING: Implement 16-tap rust POM scaled by salinity corrosion data | Justification: verified 16-step unrolled `_RustDetailMap` POM in `H8UberNoirResolveRustPomUv`; rust amount is driven by `_HectonEquipmentRust01` / `_HectonMaterialDecayRuntime`, which is fed by durability corrosion signals emitted from the salinity corrosion pipeline | Alternative rejected: per-fragment inventory/salinity buffer lookup | Estimate: keeps corrosion cost to one global scalar/vector plus existing rust atlas samples
- [ ] Task 06 FOG_BEYOND_DEPTH: Implement Beer-Lambert noir extinction | Justification: pending source audit | Alternative rejected: pending | Estimate: pending
- [ ] Task 07 DITHER_SUTURE: Blue-noise dithered HLOD/impostor transition | Justification: pending source audit | Alternative rejected: pending | Estimate: pending
- [ ] Task 08 LOW_TIER_STRIP: Strip POM and caustic texture lookups for _MATH_LOD_LOW | Justification: pending variant scan | Alternative rejected: pending | Estimate: pending
- [ ] Task 09 HIGH_END_OVERKILL: Enable screen-space refraction fake via Snell_Lens_LUT for visor/portholes | Justification: pending source audit | Alternative rejected: pending | Estimate: pending
- [ ] Task 10 REACTIVE_VFX: Link HullDents / pressure solver data to vertex displacement | Justification: pending source audit | Alternative rejected: pending | Estimate: pending
- [ ] Task 11 STP_STABILIZATION: Preserve accurate motion vectors for displaced vertices | Justification: pending source audit | Alternative rejected: pending | Estimate: pending
- [ ] Task 12 NAN_VACCINATION: Guard pow and rsqrt | Justification: pending shader scan | Alternative rejected: pending | Estimate: pending
- [ ] Task 13 BLACKBOX_LOGGING: Push ActiveShaderFeatureMask to telemetry ring | Justification: pending C# telemetry scan | Alternative rejected: pending | Estimate: pending
- [ ] Task 14 TRIPLE_STRIKE_REPAIR: Fix RenderGraph AddRasterRenderPass API drift if present | Justification: pending compile/static scan | Alternative rejected: pending | Estimate: pending
- [ ] Task 15 HOMEOSTASIS_ADAPTATION: Disable POM and secondary caustics when SystemStress01 > 0.8 | Justification: pending binding scan | Alternative rejected: pending | Estimate: pending
- [ ] Task 16 BRG_COMPATIBILITY: Keep BatchRendererGroup indirect compatibility | Justification: pending shader CBUFFER/instance scan | Alternative rejected: pending | Estimate: pending
- [ ] Task 17 NORMAL_RECON_FAKE: Bias bent hull normals without TBN recompute | Justification: pending shader scan | Alternative rejected: pending | Estimate: pending
- [ ] Task 18 FINAL_VALIDATION: Compile for Vulkan and DX12 / 0 errors | Justification: pending external compile state | Alternative rejected: pending | Estimate: pending

## Loop Ledger
- Loop 0: Prompt restored and extracted. Phase 1 source discovery not started. Runtime verification absent.
- Loop 1: Tasks 1-5 audited/implemented. Unity 6000.4.1f1 batch compile failed before `ConsolidateProjectMaterials` due unrelated pre-existing assembly errors in Physics.Tethers.Contracts, Audio.Virtualization, and legacy editor tooling; log: `Docs/AgentLogs/Unity_UBER_NOIR_INTEGRATOR.log`. No errors referencing UberNoir files were found in that log.
