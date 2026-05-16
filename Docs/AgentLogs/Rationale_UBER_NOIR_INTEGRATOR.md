# Rationale_UBER_NOIR_INTEGRATOR

Status: SOURCE TASKS 02-17 STATIC-VERIFIED; TASKS 01/18 COMPILE-GATED BY UNRELATED DEPENDENCIES; OMEGA NOT CLAIMED
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

## Decision 009 - Noir Extinction And Dither Suture
Problem: Separate fog and HLOD fades create gray underwater washout and alpha-overdraw debt.
Solution: Kept fog in the Uber shader with Beer-Lambert fallback sigma `(0.2303, 0.061, 0.018)` so red dies near 10m while blue persists, then remapped fog toward `_NoirAbyssFloorColor`. Cutout transitions use blue noise or Bayer instead of alpha blend.
Rejected Alternatives: URP gray fog was rejected because it destroys noir contrast. Full volumetric raymarch on MX350 was rejected by the visual-fake mandate.
Scalability potential: Low uses depth/noise fakes and no volumetric truth; Middle keeps same fog curve with richer lighting; High/Ultra can spend budget on caustic/refraction overkill while fog remains stable.
Hardware Impact: Low tier avoids blended overdraw and raymarching. GPU microsecond proof is absent until the compile gate clears.

## Decision 010 - High-Tier Refraction And Visual Overkill
Problem: RTX-tier materials need visible payoff without adding a separate glass/refraction shader family.
Solution: Added `_UberNoirRefractionParams` and `_UberNoirIorLut` to the Uber material CBUFFER, reused `Hecton_SnellRefractionCore.hlsl`, sampled `_CameraOpaqueTexture`, and gated the path by keyword/high-cost runtime state. Added a high-tier overkill scalar for stronger wake curl, caustics, and salt-crystal glints.
Rejected Alternatives: `GrabPass` was forbidden. A new porthole-only shader was rejected because it fragments the material family. Raw material YAML mutation was rejected.
Scalability potential: Low compiles/sheds refraction. Middle can use analytical caustics only. High/Ultra can enable Snell refraction, chromatic offset, stronger caustics, wake/silt curl, and salt crystal sparkle.
Hardware Impact: Low saves 1-3 scene-color taps and 16 POM taps under shed. High spends those taps for visible glass distortion.

## Decision 011 - Displaced Motion Vectors
Problem: STP ghosting occurs if hull dents, crush bends, and wake offsets move vertices in ForwardLit but the MotionVectors pass sees only undeformed mesh positions.
Solution: Added a `MotionVectors` pass that runs the same hull dent, dynamic bend, and wake displacement chain for current and previous transforms, then outputs non-jittered motion vectors.
Rejected Alternatives: Relying on Unity default object motion was rejected because vertex displacement would be invisible to STP. Recomputing mesh data on CPU was rejected as memory/CPU debt.
Scalability potential: Low keeps cheaper displacement math; High/Ultra get richer displacement without temporal smearing.
Hardware Impact: Motion pass adds draw cost only for active materials, but avoids visible STP trails. Exact GPU cost is pending Unity/RenderDoc validation.

## Decision 012 - Data Sovereignty And Telemetry Ring
Problem: Shader feature state had no blackbox and the initial shader-global bridge cached a direct `NativeArray<float4>`, violating the DataVault sovereignty requirement.
Solution: Added `HectonUberNoirRuntimeBridge` with a Pack=1 48-byte `UberNoirShaderTelemetryEntry` and a fixed 300-entry `BufferID.ShaderFeatureTelemetryRing`. Replaced direct global-bridge `NativeArray` ownership with a `VaultBufferHandle<float4>` and lock/resolve writes.
Rejected Alternatives: Private persistent `NativeArray` ownership was rejected. Managed delegates/EventBus strings were rejected; this bridge writes typed shader globals and a DataVault ring.
Scalability potential: Low has a single 48-byte late-frame telemetry write and immediate feature shed; High/Ultra records active overkill state for crash triage.
Hardware Impact: 0 GC by static review; one bounded ring write per late frame. Fault dump writes `Docs/AgentLogs/Dump_UBER_NOIR_INTEGRATOR.bin`.

## Decision 013 - Multiplatform Inquisition
Problem: Quest/Android, Metal/Mac, Steam Deck, and RTX paths have different failure modes: struct padding, shader portability, I/O stalls, and visual under-spend.
Solution: Verified new telemetry struct uses `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`; no `GrabPass`, sampler2D, DirectX-only syntax, or compute kernel was added to UberNoir. Thread-group scan shows relevant shader compute constants at 64 or 8x8, below Metal's 1024 limit. Runtime dump I/O is fault-only; hot path performs no file reads.
Rejected Alternatives: Platform-specific shader shortcuts and hot-path FileStream reads were rejected. Broad cross-domain compute rewrites were rejected because this prompt owns UberNoir, not every compute shader.
Scalability potential: Toaster mode uses salt crust, triangle caustics, no POM/refraction, and feature shedding. High/Ultra uses 16-tap POM, Snell refraction, stronger caustics, wake/silt curl, bent hulls, and salt-crystal highlights.
Hardware Impact: MX350 avoids texture-heavy paths; RTX pays additional texture taps and ALU only when high-cost runtime gate allows it.

## Decision 014 - Final Validation Block
Problem: Required 0-error Vulkan/DX12 validation cannot run while Unity script compilation fails in unrelated assemblies.
Solution: Reran Unity 6000.4.1f1 batch compile. The current log fails in `Animation/IK/VRPhysicalHandPresenceIkJobs.cs`, `Core/Bucketing/ModuloSimulationBucketer.cs`, and `Audio/Virtualization/AudioVirtualizationJobs.cs`; no errors reference UberNoir shader/runtime files.
Rejected Alternatives: Editing IK, Core Bucketing, or Audio from a Rendering/URP prompt was rejected as domain violation. Claiming Master Grade without compile/build proof was rejected.
Scalability potential: Source-side scalability work is present, but build/runtime proof remains blocked by other domains.
Hardware Impact: No measured runtime numbers can be claimed until the compile dependency clears.

## Decision 015 - Omega Branch Scrub Boundary
Problem: The polish mandate asks for fragment `if` removal, but fully branchless POM would force 16 rust taps even when homeostasis disables POM, contradicting low-tier and stress-shed goals.
Solution: Removed fragment helper branches where it does not add texture work: safe normalize, dither selection, rust corrosion blend, and blood overlay now use masks/lerps. Retained POM early-outs and vertex/wake culling branches because they are the actual work-shedding gates.
Rejected Alternatives: Running 16 POM taps with `pomEnabled=0` was rejected because it lies about disabling POM under stress. Removing wake/instance cull branches was rejected because it expands vertex ALU on low tier.
Scalability potential: Low and stress-shed paths still avoid POM and texture caustic costs; High/Ultra keeps branch-pruned cosmetic blends and overkill math.
Hardware Impact: Removes several dynamic branch sites in fragment helpers without sacrificing the large low-tier tap savings. Exact microsecond proof remains compile-blocked.

## Decision 016 - AUP Runtime Transform Correction
Problem: The UberNoir helper named `H8UberNoirObjectToAupWorld` subtracted `_TotalUniverseOffset` from object-to-world translation before clip-space projection. `HectonFloatingOrigin` already shifts scene transforms to runtime space (`absolute - TotalOffset`), and `Hecton_CoreLit.hlsl` uses `_TotalUniverseOffset` as runtime-to-absolute phase data (`positionWS + _TotalUniverseOffset`), not as a second geometry offset.
Solution: Renamed the helper to `H8UberNoirObjectToRuntimeWorld`, kept it as finite translation sanitation only, and left `_TotalUniverseOffset` on the procedural AUP phase math for buckling, caustics, salt crust, and crystal glints. Current and previous motion-vector transforms now stay in the same runtime-space convention as the rest of URP.
Rejected Alternatives: Keeping the subtraction was rejected because it can double-apply origin shifts once the material-facing Uber shader is active. Moving all procedural math to runtime-only coordinates was rejected because it reintroduces phase swimming after floating-origin rebases.
Scalability potential: Low/Middle/High/Ultra all get stable geometry placement; High/Ultra retain AUP-stable visual overkill without camera-relative jitter.
Hardware Impact: No claimed microsecond gain. This is correctness and temporal-stability debt removal; profiler proof remains blocked by unrelated compile errors.

## Decision 017 - Sign-Preserving Reciprocal
Problem: `H8UberNoirSafeRcp` used `rcp(max(abs(value), eps))`, which prevents division-by-zero but loses denominator sign. That is harmless for radii, but wrong for view-dependent UV math if a negative scale or view component reaches the POM remap.
Solution: Changed the reciprocal to `sign(value) / max(abs(value), eps)` using `step` and `lerp`, preserving NaN resistance while keeping POM offset direction correct.
Rejected Alternatives: Maintaining the sign-losing reciprocal was rejected as a subtle UV inversion risk. Using raw `rcp(value)` was rejected because zero/denormal inputs can produce INF/NaN on mobile GPUs.
Scalability potential: Low tier still strips POM; Middle/High/Ultra get safer parallax and texture-scale math.
Hardware Impact: Adds two scalar ALU ops where the helper is used; exact cost is unmeasured and expected to be below measurement noise, pending profiler validation.
