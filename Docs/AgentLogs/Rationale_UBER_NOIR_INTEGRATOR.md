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
Solution: Reran Unity 6000.4.1f1 batch compile. The earlier run failed in `Animation/IK/VRPhysicalHandPresenceIkJobs.cs`, `Core/Bucketing/ModuloSimulationBucketer.cs`, and `Audio/Virtualization/AudioVirtualizationJobs.cs`; no errors referenced UberNoir shader/runtime files. A later run after the AUP transform correction reached AssetDatabase script compilation, produced no new UberNoir errors or material consolidation report, then stalled with no log growth and was killed to avoid a dangling Unity batch process. A `dotnet build Assembly-CSharp.csproj --no-restore` validation attempt also failed before domain validation because `RealtimeCSG.csproj` references 216 missing source files; `Docs/AgentLogs/Dotnet_UBER_NOIR_INTEGRATOR.log` contains no UberNoir matches.
Rejected Alternatives: Editing IK, Core Bucketing, Audio, or RealtimeCSG from a Rendering/URP prompt was rejected as domain violation. Claiming Master Grade without compile/build proof was rejected. Leaving the Unity validation process alive after a long no-output stall was rejected because it would poison subsequent agents' editor/batch runs.
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

## Decision 018 - Texture Gate Honesty
Problem: Branchless `lerp` gates for textured caustics and screen refraction still executed `_HectonCausticsMap` and `_CameraOpaqueTexture` samples when high-cost effects were disabled by params or homeostasis. That made the status claim "disabled" visually true but GPU-cost false.
Solution: Added explicit `[branch]` work-shed guards around the textured caustic sample and screen-refraction sample block. POM already used this pattern because skipping the 16 taps is the point of the homeostasis gate.
Rejected Alternatives: Keeping fully branchless fragment code was rejected because it preserves Omega wording while wasting texture bandwidth. Removing the features entirely from the keyword variant was rejected because High/Ultra still need the visual overkill path.
Scalability potential: Low compiles out these paths; Middle can keep procedural caustics only; High/Ultra pay texture samples only when quality and stress gates allow them.
Hardware Impact: Expected savings under stress are one caustic-map sample plus one to three opaque-texture samples per affected fragment. Exact microseconds are not claimed without profiler capture.

## Decision 019 - Dither Texture Gate Honesty
Problem: `H8UberNoirClipDitheredTransparency` called `H8UberNoirBlueNoise(positionCS)` inside a `lerp`, so non-low variants could still sample `_BlueNoiseTex` even when the feature flag was off or homeostasis had disabled high-cost work. Runtime telemetry also reported `FeatureBlueNoiseDither` on low/stress frames.
Solution: Added `H8UberNoirCheapDither`, an ALU interleaved-gradient fallback that reuses the water-extinction noise helper. `_BlueNoiseTex` is now sampled only after a `[branch]` work-shed gate where dither is active and `H8UberNoirHighCostAllowed()` permits texture spend. Runtime feature-mask reporting now omits `FeatureBlueNoiseDither` on low tier and stress-shed frames.
Rejected Alternatives: Keeping the branchless `lerp` was rejected because it repeats the same false-disable bug as caustics/refraction. Removing dither entirely under stress was rejected because HLOD/impostor cutouts still need stable coverage without alpha blending.
Scalability potential: Low/stress uses deterministic ALU noise; Middle can keep cutout transitions without texture bandwidth; High/Ultra spend one blue-noise sample only when quality gates allow it.
Hardware Impact: Expected static saving under low/stress/disabled dither is one `_BlueNoiseTex` sample per clipped fragment. Exact microseconds are not claimed without profiler capture.

## Decision 020 - Refraction Chromatic Tap Gate
Problem: `H8UberNoirApplyScreenRefraction` correctly skipped all scene-color work when refraction was disabled, but once base refraction was active it always sampled two additional `_CameraOpaqueTexture` taps for chromatic split even when `_UberNoirRefractionParams.w` was zero.
Solution: Added a dedicated `[branch]` guard around the chromatic red/blue offset taps. Base refraction now costs one opaque-texture sample; chromatic High/Ultra overkill costs the extra two taps only when the chromatic scalar is non-zero.
Rejected Alternatives: Keeping unconditional chromatic taps was rejected because it made the documented 1-3 tap budget false. Removing chromatic split entirely was rejected because RTX/Ultra glass needs a visible spend path.
Scalability potential: Low compiles refraction out; Middle/High can use one-tap Snell distortion; Ultra can enable chromatic split for stronger visor/porthole glass distortion.
Hardware Impact: Expected static saving when chromatic is zero is two `_CameraOpaqueTexture` samples per refractive fragment. Exact microseconds are not claimed without profiler capture.

## Decision 021 - Reciprocal Guard Consistency
Problem: Several shader sites used raw `rcp` with locally clamped denominators. They were mostly guarded, but the screen-space divide used `abs(positionCS.w)`, which loses the perspective sign, and the audit surface still showed raw reciprocal use outside the NaN-vaccinated helper.
Solution: Routed screen UV, radius mask, crush-depth ratio, and wake falloff through `H8UberNoirSafeRcp`. Raw `rcp`, `rsqrt`, and `pow` now appear only inside `H8UberNoirSafeRcp`, `H8UberNoirSafeRsqrt`, and `H8UberNoirSafePow`.
Rejected Alternatives: Leaving local ad hoc clamps was rejected because it makes future NaN audits fragile. Replacing sign-preserving reciprocal with absolute reciprocal was rejected because it can invert screen/POM behavior around negative denominators.
Scalability potential: All tiers share the same reciprocal guard; low-tier cheap math remains intact while High/Ultra avoids rare NaN/INF propagation through refraction, wake, and deformation.
Hardware Impact: Expected runtime delta is measurement noise; this is correctness and mobile-GPU survival work, not a claimed microsecond win.

## Decision 022 - Pressure Radius Zero Influence
Problem: `H8UberNoirRadiusMask` returned `1.0` when the radius was zero because the disabled-radius path selected the full-influence side of a `lerp`. A default or malformed crush/habitat radius could therefore bend an entire mesh if displacement was non-zero.
Solution: Changed the mask to compute falloff normally but multiply by `step(eps, radius)`, making zero/invalid radii produce zero influence.
Rejected Alternatives: Keeping radius-zero as full influence was rejected as a catastrophic deformation default. Branching out early was rejected because a step mask gives the same safety without adding another vertex branch.
Scalability potential: All tiers get predictable localized pressure deformation; High/Ultra overkill bends remain bounded by explicit radius data.
Hardware Impact: No claimed microsecond win. This is correctness and visual-stability debt removal.

## Decision 023 - Blackbox Empty Dump Fallback
Problem: `DumpBlackBox` returned without writing any dump if the DataVault telemetry ring was unavailable, lock acquisition failed, or the resolved ring was invalid. That leaves a fault with no durable reason code.
Solution: Added `WriteEmptyBlackBox`, which writes `Dump_UBER_NOIR_INTEGRATOR.bin` with magic, reason flags, telemetry cursor, and zero entry count when the full 300-entry ring cannot be read.
Rejected Alternatives: Keeping a silent return was rejected because it recreates "unknown crash" failure. Allocating a private fallback native ring was rejected because this domain already evicted telemetry ownership to DataVault.
Scalability potential: Low devices still avoid hot-path I/O; fault-only dumps retain postmortem signal even under DataVault failure. High/Ultra keep the full ring when vault access is valid.
Hardware Impact: Hot path unchanged. Fault path may write a tiny header instead of no file; no frame-time claim.

## Decision 024 - Blackbox Fault Latch Discipline
Problem: Normal telemetry push failure called `DumpBlackBox(TelemetryFlagVaultUnavailable)` whenever the DataVault ring was not available. During startup or transient compaction this could write an empty dump and consume `_dumpedFault`, leaving a later real NaN/layout fault without a dump.
Solution: Removed the normal-path dump call from `PushBlackBox`. Missing DataVault now skips only that frame's telemetry write. `DumpBlackBox` remains reserved for explicit layout/non-finite fault paths and still writes a full ring or reason-coded empty header if vault access fails during the fault.
Rejected Alternatives: Keeping proactive missing-vault dumps was rejected because it turns a recoverable startup condition into a one-shot crash artifact. Adding a private fallback ring was rejected because shader telemetry ownership belongs to GlobalDataVault.
Scalability potential: Low devices avoid accidental file I/O during startup/load; High/Ultra retain the full 300-frame blackbox when the ring is live.
Hardware Impact: Removes potential cold/startup file write on DataVault absence; hot path remains allocation-free by static review. No measured microseconds claimed.

## Decision 025 - Low-Tier Descriptor Shedding
Problem: `_BumpMap`, `_RustDetailMap`, `_BlueNoiseTex`, `_HectonCausticsMap`, and `_H8UberNoirInstanceData` were declared at file scope even in variants where the preprocessor removes every sample/read. On mobile and descriptor-limited APIs, a disabled feature should not keep avoidable bindings alive.
Solution: Guarded optional texture and structured-buffer declarations with the same `_MATH_LOD_LOW`, `H8_UBERNOIR_CAUSTICS_TEXTURED`, and `H8_UBERNOIR_USE_INSTANCE_BUFFER` preprocessor conditions as their use sites. Wrapped `H8UberNoirBlueNoise` out of low-tier variants so `_BlueNoiseTex` is not referenced there.
Rejected Alternatives: Leaving unused declarations was rejected because descriptor-binding pressure is real even when ALU samples are stripped. Removing the high-tier resources entirely was rejected because High/Ultra still need POM, normal maps, blue-noise sutures, textured caustics, and BRG instance buffers.
Scalability potential: Low/MX350/Quest variants carry base/mask texture bindings and ALU salt-crust/dither fakes; High/Ultra variants retain full resource access for visual overkill.
Hardware Impact: Expected static effect is fewer low-tier shader resource bindings and less mobile descriptor pressure. No GPU microseconds are claimed without Unity/RenderDoc validation.

## Decision 026 - Touched Cold Allocation Comment Canonicalization
Problem: The touched UberNoir runtime bridge fallback GameObject allocation lacked the exact mandated owner/capacity comment form. The LUT resolver scratch allocation needed an audit because it sits in the same cold rendering loader path.
Solution: Updated the runtime bridge comment to canonical `COLD ALLOC: Type[capacity] - reason - owner` form using ASCII separators and verified the LUT scratch byte-array comment already matched that shape.
Rejected Alternatives: Broadly changing GpuScatter comments was rejected because that file maps to the separate GPU scatter prompt slice and comment churn risks conflict with another running agent. Ignoring the touched-file violations was rejected because these files are inside the active Rendering/URP audit path.
Scalability potential: No visual or runtime scalability impact; this preserves auditability for startup allocations.
Hardware Impact: 0 us codegen/runtime impact; comment-only documentation fix.

## Decision 027 - Partial Core C# Revalidation
Problem: The full Unity/DX12/Vulkan validation path remains blocked, but the touched C# rendering files still need the strongest available non-Unity compile proof after the fault-latch and comment edits.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`, captured `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_latest.log`, then re-ran after the shared-include patch and captured `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop20_shared_include.log` with `EXIT=0`.
Rejected Alternatives: Re-running full Unity batch validation was rejected for this loop because the last Unity validation stalled at AssetDatabase script compilation and prior full project `dotnet build` is blocked by unrelated RealtimeCSG missing-source references. Claiming shader/player readiness from a C# project build was rejected.
Scalability potential: No direct visual scalability change; this validates the C# bridge/LUT side of the low/high shader-control path.
Hardware Impact: 0 runtime change; build proof only.

## Decision 028 - Shared Include Reciprocal Guard
Problem: `Hecton_WaterExtinction.hlsl` and `Hecton_SnellRefractionCore.hlsl` still had raw `rcp` calls. The denominators were clamped, but the NaN audit surface for the UberNoir include chain still exposed ad hoc reciprocal use outside named safe helpers.
Solution: Added `H8WaterExtinctionSafeRcp` and `HectonSnellSafeRcp`, then routed turbidity/depth normalization and Snell IOR ratios through those helpers.
Rejected Alternatives: Leaving locally clamped raw reciprocals was rejected because future shader audits need a single obvious safe-math surface. Moving these helpers into `Hecton8_UberNoir.hlsl` was rejected because the includes are shared and are included before the Uber helper definitions.
Scalability potential: Low tier keeps the same Beer-Lambert fake and salt-crust path; High/Ultra keep Snell refraction and LUT extinction with clearer NaN guard boundaries.
Hardware Impact: Expected runtime delta is measurement noise; this is mobile-GPU survival and auditability work, not a measured performance gain.

## Decision 029 - Low-Memory Extinction LUT I/O Shed
Problem: `LutArrayResolver` always tried to resolve and stream `Water_Extinction_Matrix.bin` before scene load. The packed matrix is 32 MB, and low-memory/portable devices can use the analytical Beer-Lambert fallback already present in UberNoir instead of paying startup I/O and texture residency.
Solution: Added a player-only low graphics-memory gate (`SystemInfo.graphicsMemorySize <= 2048 MB`) that returns after publishing fallback globals. This skips path probing, StreamingAssets URI staging, cache writes, texture allocation, and sequential matrix reads on low-memory devices.
Rejected Alternatives: Always streaming the 32 MB LUT was rejected for MX350/Quest-class targets because the shader fallback is already an accepted Dear Lie. Disabling the LUT in the Editor was rejected because artists still need to validate the high-fidelity path. Raising the threshold without device-specific profiler proof was rejected to avoid stealing fidelity from mid/high machines.
Scalability potential: Low/MX350/Quest uses analytical Beer-Lambert fallback and avoids LUT residency. High/Ultra still load the packed extinction matrix for richer water-color response.
Hardware Impact: On gated devices, expected static saving is one 32 MB file stream plus one 4096x4096 RHalf texture allocation. No frame microseconds are claimed without device profiling.

## Decision 030 - Mobile StreamingAssets LUT Bypass
Problem: The low-memory LUT gate still allowed Android/Quest-style players with reported graphics memory above 2048 MB to enter the synchronous StreamingAssets URI staging path, which uses `UnityWebRequest` plus a blocking wait before scene load.
Solution: Added a player-only `UNITY_ANDROID || UNITY_VISIONOS` analytical-fallback gate before any path probing or URI staging. Editor remains on the high-fidelity path, and desktop/high-memory players can still load the packed matrix.
Rejected Alternatives: Keeping mobile on the URI staging path was rejected because a startup-blocking 32 MB matrix load is wrong for Quest/Android portability. Removing the high-memory desktop LUT path was rejected because High/Ultra needs the richer extinction response. Rewriting the loader into an async Addressables pipeline was rejected in this prompt because it would change bootstrap architecture outside the UberNoir shader-consolidation slice.
Scalability potential: Android/Quest use the Dear Lie Beer-Lambert fallback and avoid blocking asset reads. High/Ultra desktop keeps the LUT path for stronger water color response.
Hardware Impact: On Android/Quest-style players, expected static saving is the avoided `UnityWebRequest` staging wait, temporary cache file write, 32 MB file stream, and 4096x4096 texture allocation. No microseconds are claimed without device profiling.

## Decision 031 - Explicit Texture Work-Shed Branch Intent
Problem: The Omega mandate asks for fragment branch removal, but the POM disable early-out and extinction-LUT inactive early-out are there to avoid hidden `_RustDetailMap` and `_ExtinctionLUT` texture work. They were not all marked with explicit compiler branch intent.
Solution: Added `[branch]` to the POM-disabled return and the extinction-LUT inactive return. The existing branch count is still not Omega-compliant, but each retained fragment branch now maps to a real texture-work shed.
Rejected Alternatives: Converting these sites to branchless `lerp` was rejected because disabled POM would still execute 16 height taps and disabled LUT fog would still execute three LUT loads. Removing the features was rejected because High/Ultra needs rust POM and LUT extinction.
Scalability potential: Low/stress paths keep the cheap salt-crust and analytical Beer-Lambert fakes; High/Ultra keep 16-tap POM and packed extinction only when gates permit them.
Hardware Impact: No measured microseconds. Static avoided work remains up to 16 POM taps per rusted fragment and three packed extinction LUT loads per fogged sample when disabled.

## Decision 032 - Unity Batch Validation Wall Refresh
Problem: The required Vulkan/DX12/Unity shader validation still cannot be claimed from source scans or `dotnet build`; a fresh Unity batch was needed after the C# core slice turned green.
Solution: Ran Unity 6000.4.1f1 in batch mode with the UberNoir material consolidation executeMethod and captured `Docs/AgentLogs/Unity_UBER_NOIR_INTEGRATOR_loop23.log`. Unity exited with unrelated compile errors before executeMethod/material conversion/shader validation. No lingering Unity process remained.
Rejected Alternatives: Editing Core Bucketing/Scheduling, Audio Virtualization, Save/MapMagic legacy editor tools, or other non-rendering dependencies was rejected as a domain violation. Claiming shader/player success from the partial `Hecton8.Core` build was rejected.
Scalability potential: Source-side low/mobile/high-tier shader work remains in place, but material consolidation and player shader import proof are blocked until the external compile wall clears.
Hardware Impact: 0 runtime change. Validation remains blocked; no microsecond or frame-time claim.

## Decision 033 - Single-Owner Blackbox Dump
Problem: The UberNoir runtime bridge fault path contained a second dump filename for `EXTINCTION_LUT_SAMPLER`. This violates the prompt-local blackbox ownership contract: faults for this agent must write `Dump_UBER_NOIR_INTEGRATOR.bin`, not another agent's artifact.
Solution: Removed the cross-agent dump constant and duplicate file writes. Full and empty fault dumps now target only `Docs/AgentLogs/Dump_UBER_NOIR_INTEGRATOR.bin`.
Rejected Alternatives: Keeping both files was rejected because it creates false evidence for another domain. Renaming the second file was rejected because one telemetry ring already belongs to this agent and duplicate fault I/O has no runtime value.
Scalability potential: Low devices avoid one duplicate fault-path file write. High/Ultra retain the same 300-frame DataVault ring and feature-mask postmortem.
Hardware Impact: Hot path unchanged. Fault path writes one binary file instead of two; no frame microseconds claimed.

## Decision 034 - Dead URP SSAO Variant Removal
Problem: The material-facing UberNoir shader compiled `_SCREEN_SPACE_OCCLUSION` variants even though the shader does not call URP screen-space AO helpers and project render policy forbids URP SSAO. That was variant debt, not visual capability.
Solution: Removed the `_SCREEN_SPACE_OCCLUSION` `multi_compile` line and added the keyword to `skip_variants` as a guard against global URP keyword bleed.
Rejected Alternatives: Keeping the keyword was rejected because it doubles ForwardLit variants for a feature path that is not consumed. Wiring URP SSAO into UberNoir was rejected because the rendering mandate requires baked AO or custom half-res SSDO, not URP SSAO.
Scalability potential: Low/MX350 avoids a dead binary shader keyword; High/Ultra still keep the intended custom noir fog, caustics, refraction, and lighting variants.
Hardware Impact: Static variant product for the ForwardLit pass is halved for this dead keyword dimension. Exact shader import time, disk size, and runtime microseconds remain pending Unity shader compilation.

## Decision 035 - Low-Tier Extinction LUT Compile-Out
Problem: Even after Android/low-memory player builds skipped the 32 MB LUT load, the shader include still declared `_ExtinctionLUT` and contained packed LUT load sites in `_MATH_LOD_LOW` and mobile variants. The runtime branch skipped the loads, but the low/mobile descriptor surface still carried the resource.
Solution: Added `H8_WATER_EXTINCTION_LUT_ENABLED` only when `_MATH_LOD_LOW` and `SHADER_API_MOBILE` are both absent. Low/mobile variants now return the analytical Beer-Lambert result and compile out `_ExtinctionLUT` declaration/loads; non-mobile, non-low variants keep the packed LUT path.
Rejected Alternatives: Leaving the descriptor alive was rejected because MX350/Quest descriptor pressure matters even when texture work is branch-skipped. Removing the LUT globally was rejected because High/Ultra desktop still needs the richer packed extinction response.
Scalability potential: Low and mobile use the Dear Lie analytical extinction path with no LUT descriptor. Non-mobile Middle/High/Ultra keep packed water-color response and noir fog tinting.
Hardware Impact: Low/mobile static saving is one texture binding surface and three packed LUT load sites per fog sample path. No GPU microseconds claimed without Unity/RenderDoc validation.

## Decision 036 - Loop 24 Validation Wall
Problem: After the blackbox and shader-variant patches, the C# slice needed revalidation, but the shared project compile state changed under parallel agents.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` into `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop24.log`. The command timed out at the tool ceiling after the compiler produced 18 unrelated errors; no running `dotnet` process remained afterward.
Rejected Alternatives: Editing Physics, Tether, Bootstrap, PlayerTool, Determinism, or Core signal failures was rejected as outside the Rendering/URP prompt. Treating the timeout as an UberNoir compile failure was rejected because the diagnostics do not reference touched rendering files.
Scalability potential: Source-side shader scalability changes remain intact. Runtime proof is blocked until the external compile wall clears.
Hardware Impact: 0 runtime change. Validation blocked; no microsecond claim.

## Decision 037 - Fixed-Size UberNoir Blackbox Export
Problem: `HectonUberNoirRuntimeBridge.WriteBlackBoxFile` wrote `ring.Length` entries. The active cursor only wraps over the 300-entry telemetry contract, so an oversized vault buffer would create a non-contract dump and an undersized resolve could produce partial evidence.
Solution: Treat resolved rings smaller than `TelemetryCapacity` as unavailable and write the reason-coded empty fault header. For full dumps, cap `entryCount` to `TelemetryCapacity`, wrap the cursor inside that active window, and write only that fixed 300-frame window.
Rejected Alternatives: Writing the whole vault allocation was rejected because DataVault capacity is an ownership detail, not the blackbox contract. Allocating a private export snapshot was rejected because telemetry storage belongs to GlobalDataVault.
Scalability potential: Low/Steam Deck/Quest keep bounded fault I/O; High/Ultra keep the same 300-frame forensic signal without dumping unrelated spare capacity.
Hardware Impact: Hot path unchanged. Fault-path binary size remains bounded to one 300-entry UberNoir dump; no frame microseconds claimed.

## Decision 038 - Steam Deck Extinction LUT Bypass by Hardware Profile
Problem: The existing low-memory gate skipped the 32 MB extinction matrix only when reported graphics memory was `<=2048 MB`. Steam Deck-like UMA hardware can report more, which still allows path probing, URI staging, texture allocation, and MicroSD-sensitive file reads.
Solution: Reused `HardwareTierDetector.IsSteamDeckLike` inside `LutArrayResolver.ShouldUseAnalyticalFallbackOnly()`. Steam Deck-like players now keep the analytical Beer-Lambert fallback and skip the packed LUT loader regardless of reported graphics-memory size.
Rejected Alternatives: Adding a new Deck string detector in the resolver was rejected because the project already owns profile detection in `HardwareTierDetector`. Forcing all Linux players to analytical fallback was rejected because high-end Linux desktops should retain the richer LUT path.
Scalability potential: Steam Deck gets the cheap Dear Lie fog path and avoids startup storage pressure; high-memory desktop PC still loads the packed LUT for richer extinction.
Hardware Impact: On Steam Deck-like players, expected static saving is avoided path probing, possible StreamingAssets URI staging, one 32 MB matrix stream, one texture allocation, and temporary cache writes. Exact microseconds require device storage trace.

## Decision 039 - Inactive LUT Branch Intent Completion
Problem: Two `H8WaterExtinctionResolveRgb*` inactive-LUT early-outs still lacked explicit branch intent even though they protect against three packed LUT loads when the fallback is active.
Solution: Added `[branch]` before both inactive-LUT returns so the compiler sees the same texture-work-shed intent already used by `H8WaterExtinctionSamplePacked`.
Rejected Alternatives: Branchless `lerp` was rejected because it would still execute the three packed LUT loads. Removing the LUT path was rejected because High/Ultra desktop keeps the packed extinction response.
Scalability potential: Low/mobile/Steam Deck preserve analytical fallback; High/Ultra non-mobile can still pay the packed LUT path only when active.
Hardware Impact: Static avoided work remains three packed LUT loads per fog sample path when the LUT is inactive. No measured GPU microseconds claimed.

## Decision 040 - Loop 25 Compile Wall Refresh
Problem: The C# bridge changed again, so the core assembly needed another compile attempt even though prior validation was blocked by external agents.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` into `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop25.log`. The build failed with 40 errors in `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `World/EcosystemDirector.cs`; log search found no UberNoir, `HectonUberNoirRuntimeBridge`, `LutArrayResolver`, or Rendering diagnostics.
Rejected Alternatives: Fixing UI Navigation or Ecosystem compile breaks was rejected as a domain violation. Claiming green from static scans was rejected because AGENTS.md requires compile proof when available.
Scalability potential: Rendering source changes remain platform-scaled, but runtime proof remains blocked until external UI/Ecosystem compile failures clear.
Hardware Impact: 0 runtime change. Validation blocked; no microsecond claim.

## Decision 041 - Construction Sheen and Wet Glass Projection
Problem: The material consolidation tool only converted `Hecton_DryZoneLit`. `Mat_RuinSeepSheen` and `Mat_LeakWetSheen` remained on separate construction shader families, with wet glass coming from a third-party Amplify shader that carries five passes and DirectX-era syntax debt.
Solution: Expanded `HectonUberNoirMaterialConsolidator` to recognize DryZone, RuinSeep, and Triplebrick glass sources. The tool now snapshots source textures, opacity, tint, normal strength, wetness, and refraction, then projects them into the UberNoir CBUFFER plus `H8_UBERNOIR_CAUSTICS_TEXTURED` / `H8_UBERNOIR_SCREEN_REFRACTION` local keywords.
Rejected Alternatives: Raw `.mat` YAML editing was rejected because AGENTS requires Unity API mutation for material assets. Keeping the wet-glass source shader was rejected because it preserves separate material logic and multi-pass debt in the construction set. Converting terrain/flora/celestial materials was rejected because their deformation and domain semantics belong to other agents.
Scalability potential: Low tier uses the existing UberNoir dither/cutout and analytical fog path. High/Ultra can use the Snell screen-refraction fake, caustic texture keyword, wet smoothness, rust/salt projection, and chromatic taps on converted wet-glass/seep surfaces.
Hardware Impact: Static target is one construction shader family instead of DryZone plus RuinSeep plus third-party glass. Exact SetPass, shader import, and GPU microsecond changes require Unity material conversion and frame-debugger proof after the external compile wall clears.

## Decision 042 - Loop 26 Validation Boundary
Problem: The touched Editor consolidator needed direct proof, but the project-level editor build remains blocked by missing RealtimeCSG files and the core C# build now fails in unrelated `PhysicsApplySystem.cs` edits from parallel work.
Solution: Ran a direct Roslyn syntax compile of `HectonUberNoirMaterialConsolidator.cs` against Unity 6000.4.1f1 `UnityEditor.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, and .NET facade references. It passed with `EXIT=0` in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_material_consolidator_roslyn_refs2_loop26.log`. Full `Assembly-CSharp-Editor.csproj` and `Hecton8.Core.csproj` logs were captured as blocked evidence.
Rejected Alternatives: Fixing RealtimeCSG missing sources or PhysicsApplySystem buffer failures was rejected as a domain violation. Treating the direct Roslyn compile as Unity shader/player validation was rejected because it does not import the shader or execute the material converter.
Scalability potential: Source-side converter now preserves low/high material scalability, but runtime validation still waits on external compile repairs.
Hardware Impact: 0 runtime change from validation. No microseconds claimed.

## Decision 043 - ToolDecay Hard-Surface Projection
Problem: Twelve tool placeholder materials still use `Hecton8/Tools/DecayLit`, which duplicates dynamic wear/rust shading outside UberNoir. The shader has only two passes, so the issue is shader-family fragmentation and duplicated rust logic, not SetPass count alone.
Solution: Added `ToolDecayShaderName` and `ProjectionKind.ToolDecaySurface` to the Editor consolidator, plus `Assets/_Project/Art/Materials/Tools` as a conversion root. Tool projection keeps POM/rust and caustic feature vectors, disables hull bending/refraction, avoids dither for opaque tools, and maps tool mask/normal/base properties into UberNoir.
Rejected Alternatives: Editing or deleting `Hecton_ToolDecayLit.shader` was rejected because it is outside the core shader source slice and may still be needed by unconverted assets until Unity validation runs. Raw tool material YAML edits were rejected. Converting gameplay/tool scripts was rejected as a domain violation.
Scalability potential: Low-tier tool materials inherit UberNoir's salt-crust and analytical fog path. High/Ultra tool materials inherit the 16-tap rust POM and caustic response without carrying a second rust shader implementation.
Hardware Impact: Static target is 12 fewer tool materials on the separate ToolDecay shader family after the converter can run. Exact import/SetPass/GPU savings require Unity material conversion and Frame Debugger proof.

## Decision 044 - URP Lit Construction Placeholder Projection
Problem: The material inventory resolved the common GUID `933532a4fcc9baf4fa0491de14d08ed7` to package `Universal Render Pipeline/Lit`. Inside the converter roots, it covers 9 construction materials: 7 opaque `Mat_ToolTrial_*` hard-surface placeholders and 2 transparent build ghosts. Leaving the 7 opaque placeholders on package Lit preserves another shader family outside UberNoir.
Solution: Added `UrpLitShaderName` and `ProjectionKind.UrpLitOpaqueConstructionSurface` to `HectonUberNoirMaterialConsolidator`. The projection maps opaque URP Lit construction placeholders into UberNoir rust/POM/caustic parameters, disables refraction and hull bending, and reports transparent preview materials as skipped through an opacity/render-queue guard.
Rejected Alternatives: Bulk-converting all 64 project URP Lit materials was rejected because terrain, flora, VFX, world-support, and water placeholders have separate ownership and semantics. Converting transparent build ghosts was rejected because UberNoir is an opaque/dithered geometry shader and would not preserve their alpha-blended preview contract. Raw `.mat` YAML editing was rejected.
Scalability potential: Low tier gets the same UberNoir salt-crust and analytical fog path for the 7 opaque construction placeholders. High/Ultra gets the shared rust POM and caustic response without a separate package Lit material family.
Hardware Impact: Static target is 7 fewer opaque construction placeholders on package URP Lit after Unity can execute the converter. Exact SetPass, import, and GPU microsecond savings require Unity material conversion and Frame Debugger proof.

## Decision 045 - URP Lit Alpha Guard
Problem: The first URP Lit conversion gate used render queue, `_Surface`, `_Blend`, and RenderType. A future material could still be semitransparent through `_BaseColor.a` while staying on opaque queue, which would let the converter turn preview alpha into UberNoir dither behavior.
Solution: Added `HasOpaqueColorAlpha()` to require `_BaseColor`/`_Color` alpha >= 0.995 before URP Lit materials are eligible for UberNoir conversion. Static YAML audit confirms all 7 `Mat_ToolTrial_*` candidates have alpha 1 and both build ghosts have alpha 0.32 with transparent queue.
Rejected Alternatives: Relying only on render queue and shader tags was rejected because serialized color alpha is part of the source material contract. Converting semitransparent URP Lit materials with UberNoir dither was rejected unless they are explicitly handled by a wet-glass/seep projection.
Scalability potential: Low/MX350 gets deterministic opaque conversion only; High/Ultra keeps wet/refraction features reserved for explicit wet-glass/seep sources rather than accidental alpha transfer.
Hardware Impact: 0 runtime change. The guard prevents wrong material migration; direct SetPass/GPU savings are unchanged and remain pending Unity conversion proof.

## Decision 046 - Visor Shader Boundary Exception
Problem: Task 09 requires visor-glass refraction, while the Phase 1 purge pushes material families toward UberNoir. `Mat_Visor_Glass` is bound to `NASAPunk/SuitVisor`, not UberNoir, so it needed a boundary decision instead of silent conversion or silent neglect.
Solution: Audited `SuitVisor.shader` and `Mat_Visor_Glass`. The shader has 2 passes, no `GrabPass`, includes `Hecton_SnellRefractionCore.hlsl`, and samples `_CameraOpaqueTexture` for Snell-style screen refraction. The material is the only user of the shader GUID. It remains outside the UberNoir material converter because it owns visor/HUD stencil, lens, and transparent overlay semantics.
Rejected Alternatives: Converting the visor material into UberNoir was rejected because UberNoir is an opaque/dithered hard-surface shader and does not preserve visor stencil/HUD behavior. Editing `SuitVisor.shader` reciprocal guards was rejected as outside this UberNoir consolidation slice; the raw `rcp` hits are locally guarded with `max(...)` and belong to the Visor domain.
Scalability potential: Low tier keeps the visor's existing scalable refraction/dither controls. High/Ultra keeps Snell screen refraction without requiring a `GrabPass` or a hard-surface material conversion.
Hardware Impact: 0 runtime change from this audit. It prevents a wrong consolidation edit; exact visor GPU cost remains pending Visor-domain profiling.

## Decision 047 - Multiplatform and Data Sovereignty Static Audit
Problem: The inquisition pass demanded explicit evidence for ARM64/Quest alignment, Metal portability, Steam Deck I/O pressure, DataVault ownership, and hard-surface material coverage after the converter expansion.
Solution: Re-ran source scans over the UberNoir shader include chain and `Assets/_Project/Scripts/Rendering`. No `GrabPass`, legacy `tex2D`/`sampler2D`, DirectX-only macros, UAVs, or compute `numthreads` were found in the UberNoir chain. Rendering C# hot-path scan found no `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, or local `new NativeArray`; all Rendering `StructLayout` hits are `Pack=1`. The shader global and blackbox buffers are resolved through `GlobalDataVault` as `ShaderGlobalState` and `ShaderFeatureTelemetryRing` with `SystemID.GraphicsScalability`. The construction/tool material inventory is fully covered by converter rules, with transparent build ghosts intentionally skipped.
Rejected Alternatives: Lowering `#pragma target 4.5` was rejected because the BRG/instance-buffer variant uses `StructuredBuffer` and Unity/Metal/Vulkan import proof is currently blocked, so changing shader model without compiler evidence risks breaking the indirect path. Removing light-layer or cookie variants was rejected because project assets and URP policy still expose rendering-layer and additional-light contracts. Editing `GpuScatterLodManager` MPB/NativeArray views was rejected because that file belongs to the scatter domain; its visible NativeArray uses are DataVault-resolved views and job parameters, not this UberNoir bridge's private storage.
Scalability potential: Low/Quest/Steam Deck keep analytical extinction, texture-work shed branches, no extinction LUT descriptor on mobile/low variants, and no 32 MB matrix load on portable/player gates. High/Ultra retain rust POM, caustic texture keyword, Snell refraction, BRG instance data, and overkill surface fakes inside the same UberNoir family.
Hardware Impact: 0 runtime change from this audit. Static proof supports bounded memory layout and avoided Deck/mobile I/O; no microseconds are claimed without device profiling.

## Decision 048 - Loop 32 Core Compile Wall Refresh
Problem: After the source audit, the core assembly needed a current compile-wall check to avoid relying on stale blocked evidence.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` into `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop31_refresh.log`. The build failed with six errors in `Assets/_Project/Scripts/Core/Contracts/HectonContractValidator.cs` for missing `HectonPlatformContract`, `HectonDataSovereigntyContract`, and `HectonVisualOverkillContract` symbols. Log search found no UberNoir, `LutArrayResolver`, `HectonShaderGlobalDataVaultBridge`, or Rendering diagnostics.
Rejected Alternatives: Editing Core contract authority files was rejected as a domain violation for this shader/material prompt. Treating the build as a Rendering failure was rejected because the failing file is in Core/Contracts and the log has no touched-file diagnostics. Terminating other live `dotnet` processes was rejected because their command lines are separate concurrent agent build commands.
Scalability potential: Source-side Rendering scalability work remains intact, but Unity shader import, material conversion, Vulkan, and DX12 proof stay blocked until Core contract symbols are repaired by their owner.
Hardware Impact: 0 runtime change. Validation is blocked; no microseconds claimed.

## Decision 049 - Blackbox Single-Owner Repair and Allocation-Lock Guard
Problem: The runtime bridge still contained `ExtinctionDumpFileName` and duplicate writes to `Dump_EXTINCTION_LUT_SAMPLER.bin`, contradicting the single-owner blackbox rationale and creating false evidence for another agent. `EnsureTelemetryBuffer()` also called `GetBufferHandle` directly when the cached telemetry handle was missing or undersized, which can try to allocate during a DataVault allocation lock.
Solution: Removed the cross-agent dump constant and duplicate full/empty blackbox writes. The bridge now writes only `Docs/AgentLogs/Dump_UBER_NOIR_INTEGRATOR.bin`. `EnsureTelemetryBuffer()` now first adopts an existing `ShaderFeatureTelemetryRing` through `TryGetBufferHandle`; if no valid ring exists and `GlobalDataVault.IsAllocationLocked` is true, it returns false instead of forcing a new allocation.
Rejected Alternatives: Keeping the duplicate dump was rejected because it pollutes another agent's crash artifact. Allocating a private fallback ring was rejected because telemetry storage belongs to GlobalDataVault. Forcing `GetBufferHandle` through allocation lock was rejected because AUP/compaction fences must control allocation timing.
Scalability potential: Low/Quest/Steam Deck avoid duplicate fault-path file writes and avoid new vault allocations during memory maintenance. High/Ultra retain the same 300-frame telemetry ring once the vault provides it.
Hardware Impact: Hot path is unchanged except one cheap existing-handle probe when the cached handle is invalid. Fault path writes one binary dump instead of two; no frame microseconds claimed.

## Decision 050 - Loop 34 Compile Wall Refresh After Blackbox Repair
Problem: The blackbox repair touched runtime C#, so the core assembly needed a fresh compile attempt even though project validation has been blocked by external domains.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` into `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop33_blackbox.log`. The build failed with 14 errors in `Assets/_Project/Scripts/World/EcosystemDirector.cs` for missing `ClearIndexEntries`, `TryFindIndexEntry`, `TryUpsertIndexEntry`, and `ResolveVaultIndexCapacity` symbols, plus three duplicate Core contract-file warnings. Log search found no UberNoir, `LutArrayResolver`, `HectonShaderGlobalDataVaultBridge`, or Rendering diagnostics.
Rejected Alternatives: Editing `EcosystemDirector.cs` or project-file contract duplication was rejected as outside the Rendering/URP prompt. Claiming green from source scans was rejected because the build still fails before Unity shader import/player validation.
Scalability potential: Source-side Rendering scalability and blackbox sovereignty repairs remain in place, but Vulkan/DX12/material conversion proof remains blocked until Ecosystem/Core compile failures clear.
Hardware Impact: 0 runtime change. Validation blocked; no microseconds claimed.

## Decision 051 - Extinction Active-Flag Hoist
Problem: `H8WaterExtinctionResolveRgbByWorld` and the depth resolve already checked whether the packed extinction LUT was active, but the RGB path then called `H8WaterExtinctionSamplePacked` three times, rechecking the same active flag once per channel before loading red, green, and blue.
Solution: Split the packed LUT helpers into active-gated and direct active variants. Resolve functions now call `H8WaterExtinctionSampleRgbActive()` after the single active early-out, while public direct sample helpers keep one inactive branch for future callers. This preserves the texture-load shed when the LUT is inactive and removes redundant active checks on the active High/Ultra path.
Rejected Alternatives: Removing all inactive branches was rejected because it would reintroduce packed LUT loads during analytical fallback. Leaving the repeated per-channel checks was rejected because the resolve path already owns the active decision.
Scalability potential: Low/mobile/Deck remain analytical and descriptor-stripped. High/Ultra keep packed LUT extinction with fewer redundant control checks around the three channel loads.
Hardware Impact: Active packed-LUT resolves remove three repeated active checks per RGB resolve. No device microseconds claimed without shader compiler/profiler proof.

## Decision 052 - Extinction Wrapper Surface Removal
Problem: After the active-flag hoist, `H8WaterExtinctionSamplePacked`, `H8WaterExtinctionSampleRgb`, `H8WaterExtinctionSampleRgbByWorld`, and `H8WaterExtinctionSampleRgbByDepthMeters` remained as speculative direct-call wrappers. Project scan showed no external caller outside `Hecton_WaterExtinction.hlsl`, so those wrappers preserved dead branch surface and a stale public API.
Solution: Removed the uncalled direct wrappers and kept only the active helper pair plus the analytical/packed resolve API used by UberNoir. `H8WaterExtinctionResolveRgbByWorld` and depth resolves still own the inactive-LUT early-out before any packed LUT loads.
Rejected Alternatives: Keeping wrappers for hypothetical future direct callers was rejected because this pass is consolidation, not API expansion. Removing the active helper pair was rejected because the resolve functions need it to avoid repeated active checks on the High/Ultra path. Branchless `lerp` was rejected because it would pay texture loads during analytical fallback.
Scalability potential: Low/mobile/Deck still route to analytical Beer-Lambert and avoid packed LUT descriptors/loads. High/Ultra keep the packed LUT route with a smaller helper surface and one active check per RGB resolve.
Hardware Impact: Static include surface removed four unused functions and two dead inactive-branch sites. No measured device microseconds claimed; shader import/profiling remains blocked by unrelated project compile state.

## Decision 053 - Radius Mask Branchless NaN Guard
Problem: `H8UberNoirRadiusMask` still used an early `if` to reject non-finite position or center/radius data. The branch did not guard texture work, so it was safe branch debt inside the hull pressure/deformation helper.
Solution: Replaced the early return with finite masks, sanitized position/center data, zero-radius fallback, and a final `valid` multiplier. Non-finite inputs still produce zero influence, but the helper no longer contributes a scalar guard branch.
Rejected Alternatives: Leaving the branch was rejected because Omega polish explicitly targets removable branch debt. Sampling or computing buckling unconditionally elsewhere was rejected because the remaining branches skip real vertex work or texture taps.
Scalability potential: Low/MX350 and Quest keep deterministic zero influence for invalid pressure data without branch divergence. High/Ultra retain hull bowing and pressure dent response when valid data is present.
Hardware Impact: Static shader `if` count in `Hecton8_UberNoir.hlsl` dropped to 23 with no new texture work. No measured GPU microseconds claimed; shader compiler/player validation remains blocked.

## Decision 054 - Post-Polish Sovereignty Scan
Problem: After shader hot-path edits, the Rendering slice needed a fresh evidence pass for the user's multiplatform/data-sovereignty requirements instead of relying on the older Loop 31 audit.
Solution: Re-ran `rg` scans over `Assets/_Project/Scripts/Rendering` and the UberNoir shader include chain for standard Unity updates, managed formatting/delegates, local NativeArray ownership, legacy EventBus use, DirectX-only shader debt, UAVs, compute thread groups, and StructLayout packing. No forbidden hot-path or shader portability hits were found in the owned scan set; Rendering `StructLayout` entries are still `Pack=1`.
Rejected Alternatives: Expanding edits into Scatter, Visor, or World domains was rejected because no new violation was found in the owned UberNoir scan set, and cross-domain churn would risk parallel-agent conflicts. Claiming player validation was rejected because project compile state still blocks Unity import/player builds.
Scalability potential: Low/Quest/Steam Deck retain the analytical and texture-shed paths. High/Ultra retain optional packed extinction, POM, caustics, refraction, wake, and hull deformation inside the same shader family.
Hardware Impact: 0 runtime change. This was evidence refresh; no microseconds claimed.

## Decision 055 - Validation Refresh Boundary
Problem: After the latest shader polish, validation evidence needed refresh, but the workspace has active concurrent builds and project compile state is not under this Rendering prompt.
Solution: Attempted `dotnet build .\Hecton8.Core.csproj` with existing obj state and a separate temp obj path. The temp obj path failed with `NETSDK1004` because no `project.assets.json` exists there; the existing-obj attempts returned `EXIT=-1` with empty logs and no MSBuild diagnostics. Active dotnet processes were inspected and left alone because they belong to other agents.
Rejected Alternatives: Running NuGet restore into a private temp obj path was rejected because this shader task should not mutate dependency restore state. Killing other agents' dotnet builds was rejected because concurrent execution is explicitly expected. Claiming validation success from empty `EXIT=-1` logs was rejected.
Scalability potential: Source-side low/high shader scalability remains intact, but platform/player validation remains blocked until the shared project build state is stable.
Hardware Impact: 0 runtime change. Validation inconclusive; no microseconds claimed.

## Decision 056 - CBUFFER-Owned Noir Fog Floors and Depth-Fog Rcp Guards
Problem: `H8WaterExtinctionApplyFogTint` carried hardcoded noir tint/floor literals, and the dependent hidden `Hecton_NoirDepthFog.shader` still used raw reciprocal calls for density decode, depth range scaling, and the fast negative exponential approximation.
Solution: Changed the extinction fog-tint helper to take caller-owned `extinctionFloor` and `abyssFloor` colors. UberNoir passes `_NoirFogColor`/`_NoirAbyssFloorColor`; NoirDepthFog passes `_HectonNoirDepthFogShallowColor`/`_HectonNoirDepthFogAbyssColor`. Added `HectonNoirDepthFogFinite` and `HectonNoirDepthFogSafePositiveRcp`, then routed the depth-fog reciprocal math through that helper and marked full-screen early-outs as `[branch]`.
Rejected Alternatives: Adding a new global palette constant was rejected because the existing material/pass CBUFFERs already own the relevant floor colors. Keeping the hardcoded literals was rejected because the noir mandate wants palette authority outside shared helper bodies. Branchless depth-fog early exits were rejected because they would pay source/depth/fog work on sky/no-fog pixels.
Scalability potential: Low/MX350 keeps analytical Beer-Lambert and cheap depth-fog exits. High/Ultra keep the same extinction response while the authored fog floor remains controlled per UberNoir material or post pass.
Hardware Impact: Static safety improvement only. Raw `rcp` in the dependent NoirDepthFog path is now confined to a safe helper; no measured GPU microseconds claimed.

## Decision 057 - Extinction Resolve Order Work-Shed
Problem: `H8WaterExtinctionResolveRgbByWorld` and `H8WaterExtinctionResolveRgbByDepthMeters` computed the analytical Beer-Lambert RGB fallback before checking whether the packed extinction LUT was active. On High/Ultra active-LUT frames, that exp2 vector was discarded.
Solution: Moved `H8WaterExtinctionActive()` ahead of analytical fallback computation in LUT-enabled variants. Low/mobile variants still compile directly to analytical fallback; inactive desktop LUT paths still branch to analytical fallback; active desktop LUT paths now go directly to packed RGB sampling.
Rejected Alternatives: Keeping the eager analytical calculation was rejected because it wastes ALU on the richer path. Branchless blending between analytical and LUT was rejected because it would pay both paths. Removing analytical fallback was rejected because Low/mobile/Deck and inactive LUT states rely on it.
Scalability potential: Low/Quest/Steam Deck keep the Dear Lie analytical fog. High/Ultra keep the packed LUT fog without also paying the analytical `exp2` resolve.
Hardware Impact: Active packed-LUT resolves avoid one discarded analytical RGB exp2 resolve per fog sample. No device microseconds claimed without shader compiler/profiler proof.

## Decision 058 - Blackbox Single-Owner Regression Repair
Problem: The runtime bridge source contradicted the existing status/rationale: `HectonUberNoirRuntimeBridge` still declared `ExtinctionDumpFileName` and wrote full/empty fault dumps to `Dump_EXTINCTION_LUT_SAMPLER.bin`, contaminating another agent's blackbox artifact.
Solution: Removed the duplicate dump filename constant and the two duplicate write calls. UberNoir faults now write only `Docs/AgentLogs/Dump_UBER_NOIR_INTEGRATOR.bin`.
Rejected Alternatives: Keeping the duplicate dump was rejected because blackbox ownership must be single-source. Redirecting the extinction filename through a feature flag was rejected because this bridge does not own the Extinction LUT sampler domain. Removing fault dumps entirely was rejected because Task 13 requires a 300-frame telemetry blackbox.
Scalability potential: Low/Quest/Steam Deck avoid duplicate fault-path I/O. High/Ultra keep the same DataVault telemetry ring and single authoritative dump path.
Hardware Impact: Hot path unchanged. Fault path writes one binary artifact instead of two; no frame microseconds claimed because dumps are crash/fault-only. Compile validation remains blocked outside UberNoir: no-restore hit missing generated MSBuild editorconfig in `Temp/obj/Hecton8.Core`, and restore/build then failed in `Core/SystemDispatcher.cs` for an unrelated scalability listener interface implementation.

## Decision 059 - Multiplatform Boundary Refresh
Problem: The user explicitly requested ARM64/Quest struct alignment and Metal thread-group evidence after the latest code churn, but the repo contains concurrent Graphics-domain edits outside the UberNoir ownership boundary.
Solution: Re-ran static scans. UberNoir-owned Rendering structs remain `Pack=1`, and `HectonUberNoirRuntimeBridge` keeps `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`. All `numthreads(...)` declarations under `Assets/_Project/Art/Shaders` are <=512 threads, below Metal's 1024 limit. The UberNoir shader chain has no `GrabPass`, legacy `tex2D`/`sampler2D`, UAV, group-shared compute syntax, or compute thread groups.
Rejected Alternatives: Editing `Assets/_Project/Scripts/Graphics/*` struct layouts was rejected because those files are outside the Rendering/URP domain and some GPU interop structs deliberately use explicit pack/size contracts. Treating cross-domain scan hits as UberNoir failures was rejected because the owned bridge and shader chain are clean.
Scalability potential: Low/Quest/Android keep the analytical/no-LUT paths and aligned UberNoir telemetry. High/Ultra keep the richer shader variants without Metal thread-group risk in the owned chain.
Hardware Impact: 0 runtime change. This was evidence refresh; no microseconds claimed.

## Decision 060 - Portable LUT I/O Recheck
Problem: Steam Deck and mobile targets must not stall on the 32 MB packed water-extinction matrix, especially from MicroSD or StreamingAssets URI paths.
Solution: Re-verified `LutArrayResolver.ShouldUseAnalyticalFallbackOnly()`. Player Android/VisionOS, SteamDeck-like profiles, and <=2048 MB graphics-memory devices return before matrix path resolution, StreamingAssets staging, texture allocation, and sequential file reads. Non-portable/high-memory paths still stream with `DownloadHandlerFile` for URI staging and a 128 KB scratch buffer for filesystem reads.
Rejected Alternatives: Loading the LUT universally was rejected because portable devices already have the analytical Beer-Lambert Dear Lie path. Removing the High/Ultra LUT path was rejected because top-tier desktop can afford richer extinction.
Scalability potential: Low/Quest/Steam Deck use analytical fog only. High/Ultra keep packed LUT extinction without forcing portable I/O.
Hardware Impact: 0 runtime change in this loop. Static evidence confirms portable targets avoid the 32 MB cold read; no measured microseconds claimed.

## Decision 061 - Previous-Normal Motion Vector Repair
Problem: `H8UberNoirMotionVertex` transformed previous positions with `UNITY_PREV_MATRIX_M` but reused current-frame `normalWS` for previous hull bending and wake deformation. On rotating or non-uniformly transformed hard-surface meshes, the previous displaced position could be bent along the wrong normal and produce STP ghost trails.
Solution: Transform `normalOS` with `UNITY_PREV_MATRIX_I_M` and use that `previousNormalWS` for previous dynamic hull bending and wake deformation. Current deformation still uses the current `instanceData.WorldToObject` normal.
Rejected Alternatives: Leaving current-normal reuse was rejected because Task 11 requires displaced-vertex motion-vector accuracy. Recomputing TBN frames was rejected because Task 17 explicitly uses normal bias, not full TBN rebuild. Adding previous dent/wake history buffers was rejected because no DataVault previous-frame payload exists in this shader contract.
Scalability potential: Low/MX350 gets more stable temporal output without new textures or buffers. High/Ultra retains hull dents, wake deformation, and STP stability with one extra normal transform in the motion-vector pass only.
Hardware Impact: One additional previous normal transform in the MotionVectors pass. No measured GPU microseconds claimed; shader import/player validation remains blocked outside this domain.

## Decision 062 - Owned ShadowCaster for Consolidated UberNoir
Problem: The material consolidator projects DryZone and Triplebrick wet-glass families into UberNoir, but the target shader only had ForwardLit and MotionVectors. Converting those materials without an owned shadow pass would reduce shader-family fragmentation while silently removing shadow casting from displaced hard-surface geometry.
Solution: Added an UberNoir `ShadowCaster` pass plus `H8UberNoirShadowVertex`/`H8UberNoirShadowFragment`. The shadow vertex applies the same instance load, hull dents, dynamic pressure bend, global wake deformation, and punctual/directional light bias path as the visible geometry. The shadow fragment samples base alpha and routes through the same dither/instance-fade clip helper used by forward and motion paths.
Rejected Alternatives: Using `UsePass "Universal Render Pipeline/Lit/ShadowCaster"` was rejected because it would not know about UberNoir hull dents, pressure deformation, wake displacement, instance buffer transforms, or dither fade. Leaving shadows disabled was rejected because consolidation must not trade material cleanliness for broken scene grounding. Adding DepthOnly/Meta in this pass was rejected because the immediate defect is shadow loss, and each extra pass increases SetPass pressure.
Scalability potential: Low/Quest/MX350 shadow casting uses the low-tier branch of the same deformation helpers and keeps analytical/dither fakes; High/Ultra retain deformed, alpha-clipped shadows for rusted hull, wet glass, and hard-surface conversions.
Hardware Impact: Adds a ShadowCaster pass when shadow maps request it. No measured microseconds claimed; static impact is replacing fragmented inherited/third-party shadow behavior with one owned, deformation-correct pass.

## Decision 063 - Render Queue Normalization in Material Consolidator
Problem: Triplebrick wet-glass and RuinSeep source materials can carry transparent queue/tag overrides. After shader swap, UberNoir uses dithered alpha clip and ZWrite instead of transparent blending, so stale transparent queues would keep converted hard-surface materials in a late overdraw path.
Solution: Added `RequiresDitheredCutout()` and `ApplyRenderState()` to the consolidator. Dithered wet/seep or alpha materials are assigned `RenderQueue.AlphaTest` and `RenderType=TransparentCutout`; opaque DryZone/ToolDecay/URP Lit projections are assigned `RenderQueue.Geometry` and `RenderType=Opaque`.
Rejected Alternatives: Raw YAML queue edits were rejected because material writes must go through Unity's Material API. Leaving previous queue state untouched was rejected because source transparent shaders could preserve late sorting/overdraw after conversion. Forcing every conversion to Geometry was rejected because dithered cutout surfaces need cutout ordering semantics.
Scalability potential: Low/Quest/MX350 avoid transparent-queue overdraw for converted wet/seep fakes while keeping alpha-to-coverage cutout. High/Ultra keep Snell/refraction/wet-sheen projection without reverting to full transparent material families.
Hardware Impact: Source/tooling change only until the converter can execute. Expected runtime gain is avoiding stale transparent queue overdraw on converted dithered materials; no profiler microseconds claimed.

## Decision 064 - Legacy Source Keyword Scrub
Problem: Source materials in the consolidation roots carry keywords such as `_ALPHABLEND_ON`, `_NORMALMAP`, `_SURFACE_TYPE_TRANSPARENT`, and URP/ASE detail keywords. After shader swap, those keywords are no longer the authority for UberNoir behavior and can leave serialized noise or invalid variant pressure on converted materials.
Solution: Added a fixed `LegacySourceKeywords` list and `DisableLegacySourceKeywords()` to the consolidator. The scrub runs before re-enabling UberNoir's local caustics/refraction keywords, so the post-conversion keyword state is explicitly owned by the target shader family.
Rejected Alternatives: Leaving legacy keywords untouched was rejected because the purge should not preserve shader-family residue. Clearing every material keyword blindly was rejected because future target-local keywords should not be destroyed accidentally. Raw material YAML cleanup was rejected because material mutation belongs to Unity's Material API.
Scalability potential: Low/MX350 and Quest avoid stale transparent/normal/detail keyword residue on unified materials. High/Ultra still get only the intended UberNoir caustic/refraction feature variants.
Hardware Impact: Tooling-side cleanup. Runtime savings are not measured; expected impact is reduced invalid keyword/variant noise after conversion.

## Decision 065 - Wet-Glass Texture Projection Repair
Problem: Generic source texture fallback treated `_RoughnessDirt` as a possible UberNoir `_BaseMap`. For Triplebrick wet-glass materials, that texture is roughness/dirt data, not albedo, so conversion could turn mask noise into visible base color. The converter also wrote `_MaskMap` with unit transform, losing source mask tiling.
Solution: Added `ResolveBaseMapTexture()` and excluded `_RoughnessDirt` from the WetGlass base-map fallback. `_RoughnessDirt` remains available for `_MaskMap`. Added `MaskMapScale`/`MaskMapOffset` capture and apply, preserving source mask tiling through the Unity Material API.
Rejected Alternatives: Keeping roughness as albedo was rejected because it corrupts the wet-glass projection. Dropping `_RoughnessDirt` completely was rejected because it remains useful as packed/noise mask input. Raw YAML material repair was rejected because conversion must execute through the consolidator.
Scalability potential: Low/MX350 gets stable dithered glass color without noisy albedo artifacts. High/Ultra keeps refraction/wet roughness breakup through mask data instead of base-color contamination.
Hardware Impact: Tooling-side correction. No measured microseconds; visual correctness prevents accidental extra albedo texture noise on converted glass.

## Decision 066 - Required UberNoir Pass Re-Enable
Problem: Source URP materials in the conversion roots can serialize disabled pass state such as `MOTIONVECTORS`. If that state survives shader swap, converted UberNoir materials can keep MotionVectors disabled and break Task 11 despite the shader owning a correct displaced motion pass.
Solution: Added `EnableRequiredShaderPasses()` to the consolidator and call it after render-state normalization. It explicitly enables `ForwardLit`, `UniversalForward`, `MotionVectors`, `MOTIONVECTORS`, and `ShadowCaster` on converted materials.
Rejected Alternatives: Assuming shader swap clears disabled pass state was rejected because source material YAML shows disabled pass entries. Raw YAML clearing was rejected because the converter owns material mutation through Unity APIs. Ignoring uppercase `MOTIONVECTORS` was rejected because that exact serialized token exists in the roots.
Scalability potential: Low and portable builds keep correct alpha-clipped shadows and motion vectors after conversion. High/Ultra retain STP stability for displaced rust/wake/hull surfaces instead of silently falling back to stale pass state.
Hardware Impact: Tooling-side correctness. No measured microseconds; it prevents temporal artifacts and shadow omissions caused by stale disabled-pass metadata.

## Decision 067 - Mask Transform Consumed by UberNoir
Problem: The material consolidator preserved source `_MaskMap` scale/offset, but `Hecton8_UberNoir.hlsl` still sampled `_MaskMap` with the base/POM wear UV. Converted wet-glass and packed-mask materials could therefore keep the right texture asset but lose source mask tiling.
Solution: Added `_MaskMap_ST` to the SRP-batcher `UnityPerMaterial` CBUFFER and expanded the existing TEXCOORD8 payload from `baseUvScale` to `uvAux`. The vertex path now emits base UV scale in `uvAux.xy` and pre-transformed mask UV in `uvAux.zw`; the fragment path samples `_MaskMap` from `maskUv` and applies the POM UV delta to keep mask/albedo/rust displacement coherent on high tiers.
Rejected Alternatives: Adding another interpolator was rejected because TEXCOORD pressure matters on mobile/Quest. Reusing base UV for masks was rejected because it discards captured material data. Recomputing `_MaskMap_ST` in fragment with `TRANSFORM_TEX` was rejected because Task 02 already moved scale/offset work out of the fragment path.
Scalability potential: Low/MX350 and Quest use one vertex-computed mask UV and avoid fragment ST math while preserving authored roughness/AO tiling. High/Ultra keep POM-aligned masks so wetness, rust, and refraction breakup remain coherent under visual-overkill variants.
Hardware Impact: Fragment path swaps a base-UV mask sample for a pre-transformed mask UV sample and adds no texture fetch. TEXCOORD8 grows from `float2` to `float4`; no measured GPU microseconds claimed because shader import/profiling remains blocked.

## Decision 068 - Wet-Glass Normal Source Priority
Problem: `Triplebrick/Glass` declares and samples `_Normal`, but `Mat_LeakWetSheen.mat` also serializes legacy `_BumpMap`. The consolidator previously read `_BumpMap` before `_Normal`, so wet-glass conversion could project the wrong normal texture into UberNoir.
Solution: Added `ResolveBumpMapTexture()`. Wet-glass projection now prefers `_Normal`, then `_NormalMap`, then `_BumpMap`; URP/tool projections still prefer `_BumpMap` first to match their source shader contracts.
Rejected Alternatives: Raw material YAML edits were rejected because conversion must run through Unity's Material API. Global reordering of all normal-source priority was rejected because URP Lit and ToolDecay use `_BumpMap` as the canonical normal slot. Adding a new normal texture property to UberNoir was rejected because this is projection logic, not shader API expansion.
Scalability potential: Low/MX350 and Quest keep the same one normal texture sample when normal mapping is compiled in. High/Ultra retain authored wet-glass distortion normals instead of stale URP fallback normals before refraction and wetness overkill.
Hardware Impact: Editor-only projection fix. Runtime texture count and shader ALU are unchanged; no microseconds claimed.

## Decision 069 - Dead Additional-Light Variant Strip
Problem: UberNoir ForwardLit declared additional-light, additional-shadow, light-layer, and light-cookie variants, but the shader include does not call additional-light APIs. The declarations multiply variant/stutter surface without a corresponding lighting path.
Solution: Removed `_ADDITIONAL_LIGHTS`, `_ADDITIONAL_LIGHT_SHADOWS`, `_LIGHT_LAYERS`, and `_LIGHT_COOKIES` multi_compile declarations from the UberNoir ForwardLit pass. Main-light shadow variants remain because the lighting function uses `GetMainLight(shadowCoord)`.
Rejected Alternatives: Keeping the declarations was rejected because Vulkan/Linux stutter control starts by owning fewer variants. Adding an additional-light loop was rejected because this prompt is consolidation/uber-shader debt cleanup, not a new lighting feature, and MX350 underwater visibility favors baked/SH/fake caustic lighting. Removing main-light shadow variants was rejected because UberNoir visibly consumes main-light shadows.
Scalability potential: Low/MX350 and Quest compile fewer unused ForwardLit variants. High/Ultra keep the intended main-light, SH, caustic, refraction, rust, hull, and fog overkill paths without additional-light variant bloat.
Hardware Impact: Source variant surface reduction only. No measured shader-compile milliseconds or frame microseconds claimed until Unity/player validation is unblocked.

## Decision 070 - Post-Variant Rendering Inquisition
Problem: After shader/material consolidation changes, the owned Rendering/URP slice needed a fresh evidence pass for data sovereignty, hot-path hygiene, and platform shader portability instead of relying on older scans.
Solution: Re-ran static scans over `Assets/_Project/Scripts/Rendering` and the UberNoir shader/include chain. No local native container allocations were found; `NativeArray<T>` hits are views from texture raw data, DataVault-resolved buffers, or job fields. Runtime Rendering files have no standard Unity update methods, `string.Format`, legacy `EventBus`, managed delegate types, or gameplay string interpolation. Shader scans found no `GrabPass`, legacy sampler syntax, UAV/group-shared compute syntax, D3D-only branch, or thread-group declarations in the UberNoir chain.
Rejected Alternatives: Editing Editor-only string interpolation in the consolidator was rejected because it is not hot-path runtime code and keeps the report writer readable. Editing Scatter `NativeArray<T>` view signatures was rejected because those are DataVault/job views, not local persistent allocations. Running another dotnet rebuild was rejected per direct user instruction and because the last compile wall is unrelated to this shader slice.
Scalability potential: Low/MX350 and Quest retain no-local-native-allocation Rendering ownership and portable shader syntax. High/Ultra retain the richer UberNoir feature set without adding platform-specific shader debt.
Hardware Impact: 0 runtime change. Evidence refresh only; no microseconds claimed.
