# Rationale_UBER_NOIR_INTEGRATOR

Status: PHASE 1 COMPILE-GATED BY UNRELATED DEPENDENCY
Agent: UBER_NOIR_INTEGRATOR

## Decision 001 - Prompt Authority Restored
Problem: The first launch had no `UBER_NOIR_INTEGRATOR` XML in `CURRENT_BATCH.md`, so task count and scope could not be verified.
Solution: Re-extracted the restored XML from `Docs/Tasks/CURRENT_BATCH.md` and accepted the explicit `RENDERING/URP` domain and 18 tasks.
Rejected Alternatives: Using the launcher instruction as the task source was rejected because it lacks the 18-task contract. Synthesizing a task list was rejected as prompt contamination.
Scalability potential: Low/Middle/High/Ultra requirements now come from the restored prompt, not guesswork.
Hardware Impact: 0 us runtime; prevents unscoped edits on i3/MX350.

## Decision 002 - Mandate Set
Problem: The work touches URP RenderGraph discipline, SRP batching, noir fog, caustics, rust POM, AUP offsets, descriptor binding, and low-tier load shedding.
Solution: Read `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`, `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `REND_DescriptorBinding_Reality_Check.txt`, and `MATH_AUP_Determinism_Sync.txt`.
Rejected Alternatives: Reading unrelated physics/AI mandates was rejected as context noise. Coding from the prompt alone was rejected because shader integration is governed by registry constraints.
Scalability potential: Low strips POM/texture caustics/refraction; Middle keeps cheap analytical fakes; High/Ultra spend budget on POM, Snell LUT, richer caustics, and motion-vector stability.
Hardware Impact: Expected low-tier savings come from fewer SetPass/material variants and fewer texture samples; numeric proof remains pending capture.

## Decision 003 - Consolidation First
Problem: The project already has `Hecton8_UberNoir.hlsl`, but the restored task says the material library remains fragmented around caustics, rust, and fog.
Solution: Start with Phase 1 source discovery: shader/material scans, CBUFFER inventory, `_MainTex_ST` use, global binding ownership, and existing SetPass/material family evidence before writing shader code.
Rejected Alternatives: Blindly adding a new shader or material variant was rejected because the goal is one Uber shader, not another branch of debt.
Scalability potential: One material family keeps MX350 state changes lower and lets RTX tiers spend saved CPU on visible overkill.
Hardware Impact: Target impact is SetPass reduction; current value is unmeasured and marked pending.

## Decision 004 - Material-Facing Uber Shader
Problem: `Hecton8_UberNoir.hlsl` existed as a core include, but no material-facing shader asset in the authorized Core shader boundary exposed the unified CBUFFER to the material library.
Solution: Added `Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader` with one `ForwardLit` URP pass and properties matching `UnityPerMaterial` in the UberNoir HLSL include.
Rejected Alternatives: Reusing `Hecton_DryZoneLit` was rejected because it still carries multiple passes and duplicated visual blocks. Raw `.mat` YAML migration was rejected because Unity material mutation must go through the Editor API.
Scalability potential: Low uses one hard-surface Uber path and strips texture caustics/POM through keywords; Middle keeps analytic caustics; High/Ultra can enable POM, textured caustics, and later refraction without spawning material families.
Hardware Impact: Target DryZone consolidation is 4 passes to 1 pass, expected to save roughly 40-120 us CPU render-thread overhead across the five construction materials once Unity compile allows the migration method to run.

## Decision 005 - Vertex UV Transform Ownership
Problem: Fragment-stage base texture scale/offset work was still present through `TRANSFORM_TEX` and the rust POM remap path.
Solution: Packed transformed base UV and raw UV into `uvPack`, passed `_BaseMap_ST.xy` as `baseUvScale` from vertex, and made rust POM return final base-space UV from those varyings.
Rejected Alternatives: Keeping `_BaseMap_ST` in fragment was rejected because the task explicitly calls out ST polling debt. Recomputing raw UV from transformed UV was rejected because offset/zero-scale cases are fragile.
Scalability potential: Low/Middle/High/Ultra all share the same deterministic vertex UV path; fragment work drops instead of branching by tier.
Hardware Impact: Approximate savings are small per pixel but persistent: two scalar uniform reads and one transform macro path are removed from the hot fragment path.

## Decision 006 - DataVault Shader Global Bridge
Problem: `_BiolumMasterPhase` and `_AupShiftOffset` were direct shader globals owned by separate systems, creating scattered authority and no DataVault-backed handoff.
Solution: Added `HectonShaderGlobalDataVaultBridge` in `Rendering/`, added `BufferID.ShaderGlobalState`, and routed `HectonBiolumManager`, `BiolumPulseSyncRuntime`, and `HectonFloatingOrigin` through the bridge.
Rejected Alternatives: Keeping direct `Shader.SetGlobalVector` calls was rejected as authority scatter. Adding per-material copies was rejected because these are frame globals and would break SRP batcher locality.
Scalability potential: Low devices get one cached O(1) DataVault slot read/write path; top-tier devices can add more global feature state without material mutation.
Hardware Impact: 0 GPU us; CPU cost remains constant and avoids managed allocation in the render loop.

## Decision 007 - Caustic And Rust Phase 2 Prewire
Problem: The restored task requires caustics and rust to live in the Uber `ForwardLit` path, not in extra materials.
Solution: Kept caustics inside `H8UberNoirEvaluateMainLighting`, added low-tier 1D triangle-noise caustics, and changed high-tier texture caustics to `lerp` selection. Verified 16-step rust POM uses `_RustDetailMap` and corrosion globals.
Rejected Alternatives: A separate caustic projector material pass was rejected as SetPass debt. Per-pixel salinity data fetch was rejected because salinity corrosion already resolves into durability/corrosion globals.
Scalability potential: Low has a no-texture caustic fake; Middle has procedural caustics; High/Ultra can sample the projected caustic map with Snell-style normal offset.
Hardware Impact: Low tier avoids one caustic texture sample. High tier pays one sample only when the textured keyword is compiled.

## Decision 008 - Compile Gate Is External
Problem: Unity batch execution could not run the material consolidator because script compilation fails before `executeMethod`.
Solution: Captured `Docs/AgentLogs/Unity_UBER_NOIR_INTEGRATOR.log` and verified the reported errors reference unrelated existing assemblies/editor tools, not UberNoir files.
Rejected Alternatives: Editing Physics, Audio, Save, MapMagic, or legacy editor assemblies was rejected as cross-domain sabotage. Raw material YAML edits were rejected despite the compile gate.
Scalability potential: The consolidator remains ready for the next clean compile and is limited to DryZone hard-surface materials to avoid breaking terrain/flora/celestial specialization.
Hardware Impact: Runtime savings are blocked until the compile dependency is cleared; no material assets were mutated by this failed batch run.
