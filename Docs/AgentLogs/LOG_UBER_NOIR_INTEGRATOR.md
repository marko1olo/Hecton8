# LOG_UBER_NOIR_INTEGRATOR

## 2026-05-16 Phase 1-4 Source Pass

What was wrong:
- UberNoir had a core include but the material-facing shader, screen refraction hook, displaced motion vectors, and runtime feature telemetry were incomplete.
- Shader globals for biolum/AUP were partially scattered before the DataVault bridge pass.
- The first DataVault bridge version still cached a direct native array handle, which violated the new sovereignty demand.
- Unity material consolidation cannot execute while unrelated assemblies fail compilation.

What was done:
- Added/extended `Hecton8/Rendering/UberNoir` with one ForwardLit pass, MotionVectors pass, SRP Batcher CBUFFER, DOTS instancing keyword, and Snell refraction properties.
- Wired Beer-Lambert noir extinction, blue-noise/Bayer dither cutouts, low-tier salt crust, analytical/textured caustics, 16-tap rust POM, hull dents, crush/habitat bends, wake/silt offsets, and normal-bias deformation into the Uber path.
- Added `HectonUberNoirRuntimeBridge` with Pack=1 48-byte telemetry entries, 300-frame DataVault ring, `_HectonActiveShaderFeatureMask`, homeostasis shed gate, and fault dump path.
- Converted `HectonShaderGlobalDataVaultBridge` from direct native array caching to `VaultBufferHandle<float4>`.
- Reduced fragment helper branches in normalize, dither selection, rust corrosion, and blood overlay. POM early-outs remain deliberately because removing them would still execute 16 taps while claiming POM is disabled.
- Reran Unity batch compile. It still fails outside this domain: IK local shadowing, Core Bucketing missing `GlobalRegistry`, Audio Virtualization assembly/reference errors. No log entry references UberNoir source files.

Cinematic cheats used:
- Beer-Lambert curve instead of physical volumetric water simulation.
- Triangle/Bayer/blue-noise dither instead of alpha blending and expensive HLOD fades.
- Salt crust scalar overlay on low tier instead of rust POM texture traversal.
- Snell screen-space opaque-texture offset instead of GrabPass or ray-traced refraction.
- Vertex-only hull dents/crush/wake displacement instead of CPU mesh deformation or collider rebuilds.
- Normal bias toward camera instead of TBN reconstruction.

Microseconds saved:
- Exact measured microseconds: not available because Unity compile blocks player/profile validation.
- Static target: low tier removes 16 rust POM taps plus caustic texture sampling and refraction taps when `_MATH_LOD_LOW` or homeostasis shed is active.
- Static target: material consolidation is expected to save roughly 40-120 us render-thread state overhead for the identified DryZone hard-surface set after compile permits Editor API material migration.

Verification:
- Static scans found no `GrabPass`, `Update()`, `string.Format`, non-Pack=1 struct, or DirectX-only syntax in the touched UberNoir/runtime files.
- HLSL and shader brace counts are balanced.
- Metal thread-group audit: no new compute kernel added; relevant existing compute constants in scanned shader set are 64 or 8x8, below 1024 threads.
- Status remains `PENDING VERIFICATION`, not Master Grade, until unrelated compile blockers are cleared and Vulkan/DX12 builds run.
- Omega status is not claimed. `_TotalUniverseOffset` is used in vertex/stable noise paths; remaining shader `if` branches are work-shed/culling paths retained for low-tier and homeostasis correctness.

## 2026-05-16 Loop 7 - AUP Transform Correction

What was wrong:
- The active Uber shader path inherited a helper that subtracted `_TotalUniverseOffset` from object-to-world translation before clip-space projection.
- `HectonFloatingOrigin` already keeps scene transforms in runtime space by applying `runtime = absolute - TotalOffset`; subtracting `_TotalUniverseOffset` again risks double-shifting visible geometry and displaced motion vectors.
- `Hecton_CoreLit.hlsl` treats `_TotalUniverseOffset` as runtime-to-absolute phase input for stable procedural noise, not as a pre-projection geometry offset.

What was done:
- Renamed `H8UberNoirObjectToAupWorld` to `H8UberNoirObjectToRuntimeWorld`.
- Kept finite translation sanitation but removed the geometry subtraction of `_TotalUniverseOffset`.
- Updated ForwardLit and MotionVectors paths to use runtime-space object transforms.
- Kept `_TotalUniverseOffset` on procedural phase math for buckling, salt crust, caustics, and salt-crystal highlights.
- Changed `H8UberNoirSafeRcp` to preserve denominator sign while still clamping by epsilon.

Cinematic cheats used:
- Runtime-space geometry for stable STP and clip projection.
- AUP-space phase fakes for visual continuity across floating-origin shifts.
- Sign-safe POM reciprocal so high-tier rust depth does not invert under negative UV scale/view edge cases.

Exact microseconds saved:
- None claimed. This pass is correctness/stability work, not a measured optimization. Compile/profiler validation is still blocked by unrelated project errors.

Validation:
- Static scans after the AUP correction found no stale `H8UberNoirObjectToAupWorld` references.
- HLSL brace count remains balanced.
- Forbidden-pattern scan over UberNoir-owned shader/runtime files found no `GrabPass`, legacy `sampler2D`, `tex2D`, DirectX-only marker, compute `numthreads`, `NativeArray<`, `Update()`, or `string.Format`.
- Unity batch validation was rerun. The editor reached AssetDatabase script compilation and emitted no UberNoir errors in the partial log, but no `UberNoirMaterialConsolidationReport.md` was produced and the process stalled with no log growth; it was terminated rather than left running.

## 2026-05-16 Loop 9 - Texture Gate Honesty

What was wrong:
- The branchless textured-caustic `lerp` still sampled `_HectonCausticsMap` when high-cost caustics were disabled.
- The branchless screen-refraction `lerp` still sampled `_CameraOpaqueTexture` one to three times when refraction params or homeostasis disabled the effect.
- That was a false economy: visually disabled, but still paying bandwidth.

What was done:
- Added a `[branch]` guard around the textured caustic sample.
- Added a `[branch]` early return around the screen-refraction sample block.
- Kept POM early-outs for the same reason: disabled high-cost effects must actually skip texture work.

Cinematic cheats used:
- Low/Middle stay on procedural caustics and no refraction taps.
- High/Ultra keep Snell refraction and textured caustics only when the runtime gate allows the spend.

Exact microseconds saved:
- None measured. Static effect: under homeostasis shed, affected fragments skip one caustic map sample and one to three opaque-texture samples. Profiler proof is still blocked.

## 2026-05-16 Loop 10 - Dither Texture Gate Honesty

What was wrong:
- `H8UberNoirClipDitheredTransparency` sampled `_BlueNoiseTex` through a branchless `lerp`, so disabled/stress-shed dither could still pay a texture fetch.
- `HectonUberNoirRuntimeBridge` reported `FeatureBlueNoiseDither` even on low-tier/stress frames where the shader should not spend texture bandwidth on blue noise.

What was done:
- Added `H8UberNoirCheapDither` as an ALU interleaved-gradient fallback.
- Gated `_BlueNoiseTex` behind dither-active and high-cost runtime state.
- Updated runtime feature-mask generation so blue-noise telemetry is emitted only when non-low high-cost work is allowed.

Cinematic cheats used:
- Low/stress HLOD coverage uses deterministic ALU noise instead of blue-noise texture bandwidth.
- High/Ultra keep the blue-noise transition quality only when the runtime gate permits it.

Exact microseconds saved:
- None measured. Static effect: disabled/low/stress dither paths skip one `_BlueNoiseTex` sample per clipped fragment. Profiler proof is still blocked.

Verification:
- HLSL brace count remains balanced (`63/63`).
- Forbidden-pattern scan over UberNoir-owned shader/runtime files found no `GrabPass`, legacy `sampler2D`, `tex2D`, DirectX-only marker, compute `numthreads`, `NativeArray<`, `Update()`, or `string.Format`.
- No Unity process was left running. Unity validation was not rerun because the previous batch session stalled during AssetDatabase script compilation.
- `dotnet build Assembly-CSharp.csproj --no-restore` failed before domain validation in `RealtimeCSG.csproj` due 216 missing source files; `Docs/AgentLogs/Dotnet_UBER_NOIR_INTEGRATOR.log` has no UberNoir matches.

## 2026-05-16 Loop 11 - Refraction Tap Audit

What was wrong:
- Base refraction was gated, but chromatic split still sampled `_CameraOpaqueTexture` twice whenever refraction was active, even with `_UberNoirRefractionParams.w = 0`.
- The documented "1-3 opaque texture taps" budget was therefore false in the one-tap configuration.

What was done:
- Added a `[branch]` guard around the chromatic red/blue scene-color samples.
- Base Snell refraction now pays one opaque-texture tap; chromatic High/Ultra overkill pays the two extra taps only when enabled.

Cinematic cheats used:
- One-tap screen-space Snell distortion for normal high-tier glass.
- Optional chromatic split for Ultra glass overkill without paying it on every refractive fragment.

Exact microseconds saved:
- None measured. Static effect: chromatic-off refractive fragments skip two `_CameraOpaqueTexture` samples. Profiler proof is still blocked.

Verification:
- HLSL brace count remains balanced (`64/64`).
- Forbidden-pattern scan over UberNoir-owned shader/runtime files remains clean.

## 2026-05-16 Loop 12 - Reciprocal Guard Audit

What was wrong:
- Shader reciprocal sites were individually clamped but still used raw `rcp` outside the safe helper.
- Screen UV used `abs(positionCS.w)`, which prevents divide-by-zero but loses the perspective sign.

What was done:
- Routed screen UV, radius mask, crush ratio, and wake falloff through `H8UberNoirSafeRcp`.
- Verified raw `rcp`, `rsqrt`, and `pow` now appear only inside the safe helper implementations.

Cinematic cheats used:
- No new visual simulation. This is NaN survival and temporal correctness work for existing fakes.

Exact microseconds saved:
- None claimed. This adds minimal ALU consistency and reduces mobile GPU fault risk.

Verification:
- HLSL brace count remains balanced (`64/64`).
- Reciprocal scan now shows raw `rcp` only inside `H8UberNoirSafeRcp`.

## 2026-05-16 Loop 13 - Pressure Radius Mask Fix

What was wrong:
- `H8UberNoirRadiusMask` returned full influence when radius was zero.
- A default `_HectonSubmarineCrushCenterRadius.w` or `_HectonHabitatStressCenterRadius.w` of zero could therefore bend the whole mesh if displacement was non-zero.

What was done:
- Changed zero/invalid radius behavior to zero influence using a step mask.
- Kept the mask branchless after finite validation.

Cinematic cheats used:
- Localized pressure deformation remains a vertex fake, but now has deterministic bounds.

Exact microseconds saved:
- None claimed. This prevents catastrophic visual deformation, not a measured optimization.

Verification:
- HLSL brace count remains balanced (`64/64`).
- Raw reciprocal scan remains confined to safe helper implementations.

## 2026-05-16 Loop 14 - Blackbox Empty Dump Fallback

What was wrong:
- `DumpBlackBox` silently returned when the DataVault telemetry ring was unavailable, could not lock, or resolved invalid.
- That produced no durable reason code for vault failure faults.

What was done:
- Added `WriteEmptyBlackBox` to emit `Dump_UBER_NOIR_INTEGRATOR.bin` with magic, reason flags, cursor, and zero entries when the full ring cannot be read.
- Kept the full 300-entry dump path unchanged when the DataVault ring is valid.

Cinematic cheats used:
- None. This is crash forensics plumbing.

Exact microseconds saved:
- None claimed. Hot path is unchanged; the new write is fault-only.

Verification:
- Forbidden-pattern scan over UberNoir-owned runtime files still finds no `NativeArray<`, `Update()`, `string.Format`, managed delegates, or EventBus usage.

## 2026-05-16 Loop 15 - Blackbox Fault Latch Audit

What was wrong:
- Normal telemetry push failure called `DumpBlackBox` when the DataVault ring was unavailable.
- That could write an empty startup dump and consume `_dumpedFault`, preventing a later real NaN/layout fault from producing the useful dump.

What was done:
- Removed the normal-path dump call from `PushBlackBox`.
- Kept `DumpBlackBox` fault-only: layout and non-finite failures still write the full 300-entry ring, or an empty reason-coded header if the ring cannot be read during the actual fault.

Cinematic cheats used:
- None. This is crash-forensics correctness.

Exact microseconds saved:
- None measured. Static effect: avoids accidental cold/startup file I/O when DataVault is temporarily unavailable.

Verification:
- Source patch is limited to `HectonUberNoirRuntimeBridge.cs`.
- Core C# compile validation passed in `Build_INTEGRATION_ASSEMBLY_SURGEON_20260516_inquisition03_ubernoir_latch.log`; Unity shader import/player compile was not run.

## 2026-05-16 Loop 16 - Low-Tier Descriptor Shedding

What was wrong:
- Optional UberNoir resources were declared even in variants that cannot sample or read them.
- Low-tier stripped POM/normal/blue-noise/textured-caustic work, but the file-scope declarations still exposed avoidable binding pressure.

What was done:
- Guarded `_BumpMap`, `_RustDetailMap`, and `_BlueNoiseTex` declarations behind `!_MATH_LOD_LOW`.
- Guarded `_HectonCausticsMap` behind `H8_UBERNOIR_CAUSTICS_TEXTURED`.
- Guarded `_H8UberNoirInstanceData` behind `H8_UBERNOIR_USE_INSTANCE_BUFFER`.
- Wrapped `H8UberNoirBlueNoise` out of low-tier variants.

Cinematic cheats used:
- Low-tier remains salt-crust plus analytical dither/caustics. High/Ultra keeps POM, normal mapping, blue-noise sutures, textured caustics, and BRG buffers.

Exact microseconds saved:
- None measured. Static effect: fewer low-tier shader resource bindings; profiler/RenderDoc proof remains compile-blocked.

Verification:
- HLSL brace count remains balanced (`64/64`).
- HLSL preprocessor balance is `31/31` for if-like directives and `#endif`.
- `git diff --check` reports only line-ending warnings.

## 2026-05-16 Loop 17 - Cold Allocation Comment Audit

What was wrong:
- The touched UberNoir runtime bridge fallback GameObject allocation lacked the canonical capacity/reason/owner comment shape.
- The LUT resolver scratch allocation needed audit because it was touched by the same rendering cold-path sweep.

What was done:
- Updated the fallback runtime GameObject comment to include `GameObject[1]`, reason, and owner.
- Verified the LUT scratch byte-array comment already uses canonical capacity/reason/owner form with ASCII separators; no source change was needed there.

Cinematic cheats used:
- None. Documentation hygiene only.

Exact microseconds saved:
- 0 us. Comment-only.

Verification:
- Touched-file COLD ALLOC comments now use the mandated owner/capacity shape.

## 2026-05-16 Loop 18 - Partial Core C# Validation

What was wrong:
- Full Unity shader/player validation remains blocked by the recorded project compile/stall state.

What was done:
- Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`.
- Captured output in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_latest.log`.
- Re-ran after the shared-include reciprocal patch and captured `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop20_shared_include.log`.

Cinematic cheats used:
- None. Validation only.

Exact microseconds saved:
- 0 us. Validation only.

Verification:
- `Hecton8.Core` build succeeded with 0 warnings and 0 errors.
- `Build_UBER_NOIR_INTEGRATOR_core_loop20_shared_include.log` ended with `EXIT=0`.
- This does not prove Unity shader import, Vulkan, DX12, material consolidation, or player build readiness.

## 2026-05-16 Loop 19 - Shared Include NaN Audit

What was wrong:
- The UberNoir include chain still had raw `rcp` calls in `Hecton_WaterExtinction.hlsl` and `Post/Hecton_SnellRefractionCore.hlsl`.
- Denominators were clamped, but the audit surface was inconsistent with the safe-helper rule used in the Uber shader.

What was done:
- Added `H8WaterExtinctionSafeRcp` and routed turbidity/depth normalization through it.
- Added `HectonSnellSafeRcp` and reused one inverse-glass-IOR value for eta and exit contrast.

Cinematic cheats used:
- Beer-Lambert LUT and Snell refraction remain the same visual fakes; only safe-math plumbing changed.

Exact microseconds saved:
- None claimed. This is NaN vaccination/auditability work.

Verification:
- Raw `rcp`, `rsqrt`, and `pow` now appear only inside safe helper implementations across `Hecton8_UberNoir.hlsl`, `Hecton_WaterExtinction.hlsl`, and `Hecton_SnellRefractionCore.hlsl`.
- Both edited include files have balanced braces and preprocessor guards.
- Core C# build revalidation passed in `Build_UBER_NOIR_INTEGRATOR_core_loop20_shared_include.log`; shader import/player validation was not run.

## 2026-05-16 Loop 20 - Low-Memory LUT I/O Shed

What was wrong:
- `LutArrayResolver` always attempted to resolve and stream the 32 MB packed water-extinction matrix before gameplay.
- Toaster/mobile-class devices already have the analytical Beer-Lambert fallback in UberNoir and should not pay that startup I/O/texture residency by default.

What was done:
- Added a player-only low graphics-memory gate at `<=2048 MB`.
- On gated devices, fallback globals remain published and the resolver skips path probing, StreamingAssets URI staging, cache writes, texture allocation, and file reads.
- Editor and higher-memory devices still keep the packed LUT path for validation and High/Ultra water response.

Cinematic cheats used:
- Dear Lie Beer-Lambert fallback replaces the packed extinction matrix on low-memory devices.

Exact microseconds saved:
- None measured. Static saving on gated devices is the 32 MB matrix stream plus 4096x4096 RHalf texture allocation/residency.

Verification:
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` succeeded with 0 warnings and 0 errors after this patch.
- Unity shader/player validation remains blocked by unrelated project state.

## 2026-05-16 Loop 21 - Android/Quest LUT Bootstrap Bypass

What was wrong:
- The low-memory LUT gate did not cover Android/Quest builds that report graphics memory above 2048 MB.
- Those builds could still enter synchronous StreamingAssets URI staging and block before scene load on a 32 MB matrix transfer.

What was done:
- Added a player-only `UNITY_ANDROID || UNITY_VISIONOS` analytical-fallback gate in `LutArrayResolver`.
- Updated the gated diagnostic text to describe portable or low-memory targets rather than memory only.
- Desktop high-memory and Editor paths still keep the packed LUT for High/Ultra water response.

Cinematic cheats used:
- Dear Lie Beer-Lambert fallback replaces the packed extinction matrix on mobile/portable player builds.

Exact microseconds saved:
- None measured. Static avoided work on gated targets is URI staging wait, temporary cache write, 32 MB matrix stream, and 4096x4096 texture allocation/residency.

Verification:
- `git diff --check` passed for the touched shader/include/C# files, with only existing line-ending warnings.
- First `Hecton8.Core` build attempt failed in unrelated `ArchitectEyeVisualizer` missing-method symbols during concurrent workspace changes.
- Immediate retry passed with 0 warnings and 0 errors in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop21_retry.log`.
- Unity shader import, Vulkan, DX12, and material consolidation remain blocked/pending; no Master Grade claim.

## 2026-05-16 Loop 22 - Fragment Branch Intent and Domain Inquisition

What was wrong:
- Two retained fragment branches were legitimate texture-work gates but did not explicitly tell the compiler they should branch.
- The domain needed another cross-platform static sweep after the mobile LUT patch.

What was done:
- Added `[branch]` to the POM-disabled return in `Hecton8_UberNoir.hlsl`.
- Added `[branch]` to the inactive extinction-LUT return in `Hecton_WaterExtinction.hlsl`.
- Re-scanned `Assets/_Project/Scripts/Rendering` for non-packed structs, local native allocation markers, standard Unity update loops, managed event/delegate drift, and string formatting.
- Parsed all compute shader `numthreads` declarations under `Assets/_Project/Art/Shaders`.

Cinematic cheats used:
- Retained branch-gated Dear Lie paths instead of branchless fake disables that still spend texture bandwidth.

Exact microseconds saved:
- None measured. Static avoided work remains up to 16 POM height taps and three packed extinction LUT loads on disabled paths.

Verification:
- `git diff --check` passed for the touched shader files, with only line-ending warnings.
- `Hecton8_UberNoir.hlsl` and `Hecton_WaterExtinction.hlsl` both report `brace_delta=0` and `pp_delta=0`.
- Raw `rcp`, `rsqrt`, and `pow` remain confined to safe helper implementations across the UberNoir include chain.
- Rendering C# scan found no non-`Pack=1` `StructLayout`, no `Update`/`LateUpdate`/`FixedUpdate`, and no `string.Format`.
- Compute thread-group parse found max product 512, below Metal's 1024 thread-group limit.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` passed with 0 warnings and 0 errors in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop22_final.log`.
- Unity shader import/player validation remains pending.

## 2026-05-16 Loop 23 - Unity Batch Validation Refresh

What was wrong:
- Unity shader/material validation had not been reattempted after the C# core slice passed.
- Task 18 still cannot be closed as Vulkan/DX12 verified without Unity import/build proof.

What was done:
- Ran Unity 6000.4.1f1 batch mode with `Hecton8.Rendering.Editor.HectonUberNoirMaterialConsolidator.ConsolidateProjectMaterials`.
- Captured the log in `Docs/AgentLogs/Unity_UBER_NOIR_INTEGRATOR_loop23.log`.
- Searched the log for UberNoir shader/runtime diagnostics and checked for the consolidation report.
- Confirmed no Unity process remained running after the failed batch.

Cinematic cheats used:
- None. Validation only.

Exact microseconds saved:
- 0 us. Validation only.

Verification:
- Unity exited with compile errors before material conversion/shader import.
- Blocking diagnostics are outside this Rendering/URP prompt: `ModuloSimulationBucketer.cs`, `BurstTokenBucketJobAdmissionService.cs`, `AudioVirtualizationJobs.cs`, and legacy Editor tools with missing cross-assembly types.
- No `Hecton8_UberNoir`, `Hecton_WaterExtinction`, `LutArrayResolver`, or Snell shader/runtime diagnostics were found in the Unity log.
- `Docs/AgentLogs/UberNoirMaterialConsolidationReport.md` was not produced.
- Task 18 remains blocked; no Master Grade or Vulkan/DX12 success claim.

## 2026-05-16 Loop 24 - Blackbox Ownership and Low-Tier Variant Debt

What was wrong:
- `HectonUberNoirRuntimeBridge` fault dumps wrote an extra `Dump_EXTINCTION_LUT_SAMPLER.bin`, which is cross-agent evidence contamination.
- `Hecton8_UberNoir.shader` still compiled `_SCREEN_SPACE_OCCLUSION` variants despite no UberNoir SSAO consumption and project policy forbidding URP SSAO.
- `_MATH_LOD_LOW` and mobile shader targets still saw the shared `_ExtinctionLUT` declaration in the water-extinction include, even though low/mobile now rely on analytical Beer-Lambert.

What was done:
- Removed the extra cross-agent dump file path; this agent now writes only `Dump_UBER_NOIR_INTEGRATOR.bin`.
- Removed the dead `_SCREEN_SPACE_OCCLUSION` `multi_compile` from ForwardLit and added it to `skip_variants`.
- Wrapped `_ExtinctionLUT` declaration and packed-load path behind `H8_WATER_EXTINCTION_LUT_ENABLED`, which is absent on `_MATH_LOD_LOW` and `SHADER_API_MOBILE`.

Cinematic cheats used:
- Low/mobile tiers keep the deterministic analytical Beer-Lambert fog fake instead of carrying the packed LUT descriptor.

Exact microseconds saved:
- None measured. Static savings: one duplicate fault-path file write removed, one dead ForwardLit binary keyword dimension removed, and low-tier extinction LUT binding/load surface compiled out.

Verification:
- Braces and preprocessor deltas are zero for `Hecton_WaterExtinction.hlsl`, `Hecton8_UberNoir.hlsl`, and `Hecton8_UberNoir.shader`.
- `git diff --check` passed for the touched files, with line-ending warnings only.
- Raw `rcp`, `rsqrt`, and `pow` remain confined to named safe helper implementations.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` is blocked in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop24.log` by unrelated Physics/Tether/Bootstrap/PlayerTool/Determinism/Core signal errors. No rendering/UberNoir diagnostics were emitted.
- Unity shader import, Vulkan, DX12, and material consolidation remain blocked by the external compile wall. No Master Grade claim.

## 2026-05-16 Loop 25 - Fixed Blackbox Window and Deck I/O Gate

What was wrong:
- The UberNoir fault dump writer used `ring.Length`, not the mandated 300-frame active telemetry window. An oversized DataVault buffer would dump spare capacity instead of the strict blackbox contract.
- Steam Deck-like hardware could still load the 32 MB extinction matrix when reported graphics memory was above the old 2048 MB fallback threshold.
- Two inactive-LUT exits in `Hecton_WaterExtinction.hlsl` still lacked explicit `[branch]` intent.

What was done:
- Capped `WriteBlackBoxFile` to `TelemetryCapacity` and wrapped the exported cursor inside that active 300-entry window.
- Treat undersized telemetry resolves as unavailable and write the existing reason-coded empty dump header instead of partial binary evidence.
- Routed `LutArrayResolver.ShouldUseAnalyticalFallbackOnly()` through `HardwareTierDetector.IsSteamDeckLike`, preserving the high-fidelity desktop path while forcing Steam Deck-like players to analytical Beer-Lambert fallback.
- Added `[branch]` on both remaining inactive-LUT early-outs in the water-extinction resolver path.

Cinematic cheats used:
- Steam Deck keeps deterministic analytical Beer-Lambert fog rather than reading and resident-binding the packed LUT.

Exact microseconds saved:
- None measured. Static savings on Deck-like players: no 32 MB matrix read, no URI staging/cache write, and no 4096x4096 texture allocation. Fault-path binary dump remains bounded to the single 300-entry UberNoir blackbox.

Verification:
- `Hecton_WaterExtinction.hlsl`, `Hecton8_UberNoir.hlsl`, and `Hecton8_UberNoir.shader` report `brace_delta=0` and `pp_delta=0` with a preprocessor scanner that counts `#if/#ifdef/#ifndef`.
- Raw `rcp`, `rsqrt`, and `pow` are still confined to named safe helper bodies across the UberNoir include chain.
- Static Rendering-domain scan still shows only `Pack=1` `StructLayout` hits and no `Update`/`LateUpdate`/`FixedUpdate` or `string.Format` in `Assets/_Project/Scripts/Rendering`.
- `git diff --check` passed on the touched shader/runtime/docs files, with line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` failed in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop25.log` with 40 unrelated errors in `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `World/EcosystemDirector.cs`; no UberNoir/Rendering diagnostics were emitted.
- Unity shader import, Vulkan, DX12, and material consolidation remain blocked by the external compile wall. No Master Grade claim.

## 2026-05-16 Loop 26 - Construction Material Projection Coverage

What was wrong:
- `HectonUberNoirMaterialConsolidator` only recognized `Hecton_DryZoneLit`, so construction seep and wet-glass materials stayed on separate shader families.
- `Mat_LeakWetSheen` points at third-party `Triplebrick/Glass`, which carries five passes and legacy `tex2D`/Amplify output debt.
- No converted material path was setting the UberNoir local refraction or textured-caustic shader keywords.

What was done:
- Expanded the consolidator to recognize `Hecton8/Environment/Hecton_DryZoneLit`, `HECTON/Environment/RuinSeepSheen`, and `Triplebrick/Glass`.
- Added source-specific projection for base/mask/normal textures, tint, opacity, wet smoothness, rust/salt wear, dither alpha, caustic vectors, and screen-space refraction vectors.
- Added keyword writes for `H8_UBERNOIR_CAUSTICS_TEXTURED` and `H8_UBERNOIR_SCREEN_REFRACTION`, and enabled instancing on converted construction materials.
- Kept material mutation in Unity Editor API code; no raw `.mat` YAML was edited.

Cinematic cheats used:
- Ruin seep and wet-glass surfaces become UberNoir dither/refraction fakes instead of separate transparent shader stacks.
- High/Ultra gets Snell screen refraction and chromatic taps through the existing guarded UberNoir path; low tier keeps the compiled-out refraction path and cutout/dither fallback.

Exact microseconds saved:
- None measured. Static target: 5 DryZone materials plus `Mat_RuinSeepSheen` and `Mat_LeakWetSheen` move into one UberNoir material family when Unity compile allows the converter to run. The wet-glass source is a five-pass shader; exact SetPass and GPU savings require Unity material conversion plus Frame Debugger proof.

Verification:
- `git diff --check` passed on touched rendering files with line-ending warnings only.
- Direct Roslyn syntax compile of `Assets/_Project/Scripts/Rendering/Editor/HectonUberNoirMaterialConsolidator.cs` passed with `EXIT=0` in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_material_consolidator_roslyn_refs2_loop26.log`.
- `dotnet build .\Assembly-CSharp-Editor.csproj` remains blocked by missing RealtimeCSG source files; no `HectonUberNoirMaterialConsolidator` diagnostic was emitted before the dependency failure.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` remains blocked by unrelated `PhysicsApplySystem.cs` errors in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop26.log`; no UberNoir/Rendering diagnostics were emitted.
- Unity shader import, Vulkan, DX12, and actual material conversion remain blocked by the external compile wall. No Master Grade claim.

## 2026-05-16 Loop 27 - Tool Decay Shader Family Eviction

What was wrong:
- Twelve tool placeholder materials still point to `Hecton8/Tools/DecayLit`.
- `ToolDecayLit` has two passes, but it duplicates wear/rust/POM-style surface work outside the UberNoir shader family.

What was done:
- Added `Hecton8/Tools/DecayLit` as a supported source shader in `HectonUberNoirMaterialConsolidator`.
- Added `Assets/_Project/Art/Materials/Tools` to the Editor conversion roots.
- Added `ToolDecaySurface` projection: POM/rust stays enabled, caustics stay enabled, hull bending and refraction stay disabled, and opaque tool materials avoid blue-noise dither.
- No tool gameplay code and no raw `.mat` YAML were touched.

Cinematic cheats used:
- Tool rust/wear is projected into UberNoir's existing salt-crust/POM path instead of preserving a second tool-only decay shader.

Exact microseconds saved:
- None measured. Static target: 12 tool placeholder materials can move from `ToolDecayLit` to the UberNoir material family once Unity compile allows the converter to run. Runtime SetPass/GPU proof remains blocked.

Verification:
- Direct Roslyn syntax compile of `HectonUberNoirMaterialConsolidator.cs` passed with `EXIT=0` in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_material_consolidator_roslyn_refs3_loop27.log`.
- `git diff --check` and brace balance passed for the touched consolidator source.
- Full Unity/editor/player validation remains blocked by unrelated compile failures already logged in Loop 26. No Master Grade claim.

## 2026-05-16 Loop 28 - URP Lit Placeholder Gate

What was wrong:
- Package `Universal Render Pipeline/Lit` was still present inside the construction material root.
- Static GUID resolution found 9 construction materials on URP Lit: 7 opaque `Mat_ToolTrial_*` placeholders and 2 transparent build ghosts.
- Bulk conversion would have corrupted the transparent build-preview materials because UberNoir is opaque/dithered, not alpha-blended preview glass.

What was done:
- Added `Universal Render Pipeline/Lit` as a supported source shader in the Editor consolidator.
- Added `UrpLitOpaqueConstructionSurface` projection for opaque construction placeholders only.
- Added an opacity/render-queue guard that skips and reports transparent URP Lit build ghosts.
- Kept terrain, flora, VFX, sky, water, and world-support URP Lit materials out of scope.

Cinematic cheats used:
- Opaque construction placeholders inherit UberNoir salt-crust, rust POM, analytical noir fog, and caustic response instead of staying on package Lit.
- Transparent build ghosts remain on their preview path because their alpha-blended feedback is a UI/construction authoring signal, not a hard-surface noir material.

Exact microseconds saved:
- None measured. Static target: 7 additional opaque construction placeholders can move from package URP Lit to UberNoir once Unity compile allows the converter to run. Runtime SetPass/GPU proof remains blocked.

Verification:
- Direct Roslyn syntax compile of `HectonUberNoirMaterialConsolidator.cs` passed with `EXIT=0` in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_material_consolidator_roslyn_refs7_loop28.log`.
- `git diff --check` passed for the touched consolidator source with line-ending warnings only.
- Brace balance for the consolidator reports `Delta=0`.
- Rendering-domain scan still shows no `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, or `new NativeArray` in `Assets/_Project/Scripts/Rendering`; remaining `StructLayout` hits are `Pack=1`.
- Full Unity/material conversion/Vulkan/DX12 validation remains blocked by unrelated project compile state. No Master Grade claim.

## 2026-05-16 Loop 29 - URP Lit Alpha Guard

What was wrong:
- The URP Lit conversion guard did not inspect serialized color alpha.
- A future semitransparent package Lit material could pass queue/surface checks and then be converted into UberNoir dither behavior by accident.

What was done:
- Added `HasOpaqueColorAlpha()` to require `_BaseColor`/`_Color` alpha >= 0.995 before URP Lit materials are eligible.
- Rechecked construction material YAML: all 7 `Mat_ToolTrial_*` candidates are alpha 1, queue -1, RenderType Opaque; both `Mat_BuildGhost_*` materials are alpha 0.32, queue 3000, RenderType Transparent.

Cinematic cheats used:
- None new. This is a migration safety guard so explicit wet-glass/seep projections keep ownership of semitransparent/refraction fakes.

Exact microseconds saved:
- None measured. Runtime savings are unchanged; the value is preventing wrong material migration.

Verification:
- Direct Roslyn syntax compile of `HectonUberNoirMaterialConsolidator.cs` passed with `EXIT=0` in `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_material_consolidator_roslyn_refs8_loop29.log`.
- Static YAML audit confirms the 7/2 opaque-versus-transparent split in the construction URP Lit set.
- Brace balance remains `Delta=0`; `git diff --check` passes with line-ending warnings only.

## 2026-05-16 Loop 30 - Visor Refraction Boundary Audit

What was wrong:
- `Mat_Visor_Glass` still uses `NASAPunk/SuitVisor`, so Task 09 needed explicit proof that visor-glass refraction is covered without forcing it through UberNoir.

What was done:
- Resolved the SuitVisor GUID and found `Mat_Visor_Glass` is the only material using it.
- Audited `SuitVisor.shader`: 2 passes, 0 `GrabPass`, includes `Hecton_SnellRefractionCore.hlsl`, and samples `_CameraOpaqueTexture`.
- Recorded the shader as a visor/HUD boundary exception rather than a hard-surface material-consolidation target.

Cinematic cheats used:
- Existing visor path already uses screen-space Snell refraction through `_CameraOpaqueTexture`; no `GrabPass` is present.

Exact microseconds saved:
- None measured. This loop prevents a bad conversion, not a runtime optimization.

Verification:
- Static scan found 1 material using SuitVisor GUID: `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat`.
- Static scan found 2 `Pass` blocks, 0 `GrabPass`, 6 `_CameraOpaqueTexture` references, and 9 raw guarded `rcp` calls in `SuitVisor.shader`.
- No SuitVisor source was edited; reciprocal cleanup remains Visor-domain work.

## 2026-05-16 Loop 31 - Multiplatform and Data Sovereignty Audit

What was wrong:
- The disk status needed a fresh evidence pass for Quest/Android alignment, Metal-safe shader syntax, Steam Deck I/O pressure, and GlobalDataVault ownership after material consolidation grew beyond DryZone.
- Task 06 status still referenced the old `H8UberNoirBeerLambertFallback` helper even though fog fallback ownership now lives in `Hecton_WaterExtinction.hlsl`.

What was done:
- Corrected Task 06 status to name `H8WaterExtinctionResolveRgbByWorld` as the current Beer-Lambert/LUT resolver.
- Re-scanned the UberNoir shader chain for `GrabPass`, `tex2D`, `sampler2D`, DirectX-only macros, UAVs, and compute thread groups; none were found.
- Re-scanned `Assets/_Project/Scripts/Rendering` for `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, local `new NativeArray`, and non-`Pack=1` structs; no UberNoir-owned violation was found.
- Rebuilt the material inventory for converter roots: 5 DryZone, 1 RuinSeep, 1 wet-glass, 12 ToolDecay, 7 opaque URP Lit placeholders, and 2 transparent URP Lit build ghosts skipped by design.

Cinematic cheats used:
- No new visual code. The audit confirms existing Dear Lie paths: analytical Beer-Lambert on low/mobile/Deck, texture-work shedding for POM/caustics/refraction/dither, and screen-space Snell instead of `GrabPass`.

Exact microseconds saved:
- None measured. Static savings already logged remain: low/mobile variants avoid the extinction LUT descriptor/loads, Deck-like players skip the 32 MB matrix path, and stress/low gates skip POM and optional texture taps.

Verification:
- `rg` scan over `Hecton8_UberNoir.shader`, `Hecton8_UberNoir.hlsl`, `Hecton_WaterExtinction.hlsl`, `Hecton_SnellRefractionCore.hlsl`, and Rendering C# found no forbidden shader portability tokens in the UberNoir chain.
- Rendering C# scan found only `StructLayout(... Pack = 1 ...)` entries for layout-sensitive structs and no local `new NativeArray`.
- Full Unity shader import, material conversion, Vulkan, and DX12 validation remain blocked by unrelated project compile state. No Master Grade claim.

## 2026-05-16 Loop 32 - Core Compile Wall Refresh

What was wrong:
- Runtime/player proof was still blocked, so the current Core build state needed fresh evidence after the audit.

What was done:
- Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` and captured `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop31_refresh.log`.
- The build failed before Rendering with six missing Core contract symbols in `Assets/_Project/Scripts/Core/Contracts/HectonContractValidator.cs`: `HectonPlatformContract`, `HectonDataSovereigntyContract`, and `HectonVisualOverkillContract`.
- Log search found no UberNoir, `LutArrayResolver`, `HectonShaderGlobalDataVaultBridge`, or Rendering diagnostics.

Cinematic cheats used:
- None. This was validation boundary work.

Exact microseconds saved:
- None. Compile validation remains blocked.

Verification:
- Build log ends with `EXIT=1`.
- `Get-CimInstance Win32_Process` showed remaining `dotnet` processes are separate concurrent build commands (`Assembly-CSharp.csproj`, `Hecton8.Core.csproj` with different flags, and `Hecton8.Editor.csproj`), so this task did not terminate them.
- Full Unity shader import, material conversion, Vulkan, and DX12 validation remain blocked by the Core contract compile wall. No Master Grade claim.

## 2026-05-16 Loop 33 - Blackbox Single-Owner Repair

What was wrong:
- `HectonUberNoirRuntimeBridge` still wrote `Dump_EXTINCTION_LUT_SAMPLER.bin` in full and empty fault-dump paths.
- That contradicted the single-owner blackbox rationale and could create false crash evidence for another domain.
- The telemetry handle path could call `GetBufferHandle` while the DataVault allocation lock was active.

What was done:
- Removed the cross-agent dump filename constant and duplicate writes.
- Fault dumps now target only `Docs/AgentLogs/Dump_UBER_NOIR_INTEGRATOR.bin`.
- `EnsureTelemetryBuffer()` now tries `TryGetBufferHandle(BufferID.ShaderFeatureTelemetryRing, ...)` before allocating, and returns false while `vault.IsAllocationLocked` is true instead of forcing a grow/allocation attempt.

Cinematic cheats used:
- None. This is stability and data-sovereignty repair.

Exact microseconds saved:
- None measured. Fault path writes one binary file instead of two; hot path adds only an existing-handle probe when the cached handle is invalid.

Verification:
- Static scan confirms no `Dump_EXTINCTION_LUT_SAMPLER` or `ExtinctionDumpFileName` remains in `HectonUberNoirRuntimeBridge.cs`.
- Static scan confirms `IntegratorDumpFileName` is the only dump filename in the bridge.
- Brace balance for `HectonUberNoirRuntimeBridge.cs` is `Delta=0`.

## 2026-05-16 Loop 34 - Post-Repair Core Compile Refresh

What was wrong:
- Runtime C# changed in the blackbox bridge, so a fresh Core build attempt was needed to separate Rendering failures from external compile-wall failures.

What was done:
- Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` and captured `Docs/AgentLogs/Build_UBER_NOIR_INTEGRATOR_core_loop33_blackbox.log`.
- The build failed before Rendering with 14 missing helper-symbol errors in `Assets/_Project/Scripts/World/EcosystemDirector.cs`.
- The build also reports duplicate source-file warnings for the Core contract files, but no touched Rendering file diagnostics.

Cinematic cheats used:
- None. This was validation boundary work.

Exact microseconds saved:
- None. Compile validation remains blocked.

Verification:
- Build log ends with `EXIT=1`.
- Log search found no `HectonUberNoir`, `LutArrayResolver`, `HectonShaderGlobalDataVaultBridge`, or Rendering diagnostics.
- Full Unity shader import, material conversion, Vulkan, and DX12 validation remain blocked by unrelated Ecosystem/Core compile state. No Master Grade claim.

## 2026-05-16 Loop 35 - Extinction Active-Flag Hoist

What was wrong:
- Packed extinction resolve checked `H8WaterExtinctionActive()` before leaving analytical fallback, then each RGB channel sample checked the same flag again.

What was done:
- Added `H8WaterExtinctionSamplePackedActive()` and `H8WaterExtinctionSampleRgbActive()`.
- `H8WaterExtinctionResolveRgbByWorld()` and `H8WaterExtinctionResolveRgbByDepthMeters()` now perform one active early-out, then pass the active scalar through the RGB sample path.
- Public direct sample helpers keep a single active early-out so future direct callers still avoid LUT loads when fallback is active.

Cinematic cheats used:
- No new effect. This keeps the Dear Lie analytical fallback intact while trimming redundant control work from the High/Ultra packed LUT path.

Exact microseconds saved:
- None measured. Static saving is three redundant active checks removed per active RGB LUT resolve.

Verification:
- `Hecton_WaterExtinction.hlsl` brace and preprocessor deltas are zero.
- `git diff --check` passes for `Hecton_WaterExtinction.hlsl` with line-ending warning only.
- Project scan shows no external caller uses the direct `H8WaterExtinctionSampleRgbByWorld`, `H8WaterExtinctionSampleRgbByDepthMeters`, or `H8WaterExtinctionSamplePacked` helpers outside the water-extinction include.

## 2026-05-16 Loop 36 - Extinction Wrapper Surface Removal

What was wrong:
- The water-extinction include still carried uncalled direct sample wrappers after the active resolve path took ownership of the LUT active check.
- Those wrappers kept speculative API surface and inactive branch sites that no project shader calls.

What was done:
- Removed `H8WaterExtinctionSamplePacked`, `H8WaterExtinctionSampleRgb`, `H8WaterExtinctionSampleRgbByWorld`, and `H8WaterExtinctionSampleRgbByDepthMeters`.
- Kept `H8WaterExtinctionResolveRgbByWorld` and `H8WaterExtinctionResolveRgbByDepthMeters` as the public resolve path.
- Kept the active helper pair used by the resolve path to avoid repeated High/Ultra active checks.

Cinematic cheats used:
- No new visual effect. This preserves the Dear Lie analytical Beer-Lambert fallback and trims dead wrapper control flow.

Exact microseconds saved:
- None measured. Static removal is four unused functions and two dead inactive-branch sites from the include.

Verification:
- `rg` now finds only `H8WaterExtinctionSamplePackedActive`, `H8WaterExtinctionSampleRgbActive`, and the two resolve functions in `Hecton_WaterExtinction.hlsl`.
- `Hecton_WaterExtinction.hlsl` reports `BraceDelta=0`, `Open=18`, `Close=18`, `IfCount=2`.
- `git diff --check -- Assets/_Project/Art/Shaders/Hecton_WaterExtinction.hlsl` passes.

## 2026-05-16 Loop 37 - Radius Mask Branchless NaN Guard

What was wrong:
- `H8UberNoirRadiusMask` still had a scalar NaN guard `if`.
- That branch was not protecting texture loads or expensive loop work, so it was removable shader branch debt.

What was done:
- Replaced the early return with finite masks and sanitized position/center data.
- Invalid position or pressure center/radius data still returns zero influence through a final validity multiplier.
- Kept the work-shed branches that skip POM, caustic texture, blue-noise, refraction, chromatic taps, and wake loop work.

Cinematic cheats used:
- Branchless finite/active mask math. No new visual effect.

Exact microseconds saved:
- None measured. Static shader branch count dropped by one in the UberNoir include.

Verification:
- `Hecton8_UberNoir.hlsl` reports `BraceDelta=0`, `Open=63`, `Close=63`, `IfCount=23`, `PreIf=31`, `PreEndif=31`.
- `git diff --check -- Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl` passes with only an existing line-ending warning.

## 2026-05-16 Loop 38 - Post-Polish Sovereignty Scan

What was wrong:
- After the latest shader edits, the old sovereignty scan evidence was stale.

What was done:
- Re-ran Rendering C# scans for `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, local `new NativeArray`, managed delegate patterns, and legacy `EventBus`.
- Re-ran shader scans for `GrabPass`, legacy `tex2D`/`sampler2D`, UAVs, and `numthreads`.
- Re-ran raw `rcp`/`rsqrt`/`pow` scan across the UberNoir include chain.

Cinematic cheats used:
- None. This was static verification.

Exact microseconds saved:
- None. Verification only.

Verification:
- No forbidden Rendering C# hot-path or ownership hits were returned.
- Rendering `StructLayout` results remain `Pack=1`: `GpuScatterLodManager` entries and `HectonUberNoirRuntimeBridge.TelemetryEntry`.
- No forbidden shader portability hits were returned in the UberNoir include chain.
- Raw `rcp`/`rsqrt`/`pow` hits remain confined to safe helpers in Snell, WaterExtinction, and UberNoir.

## 2026-05-16 Loop 39 - Validation Refresh Boundary

What was wrong:
- After shader edits, compile validation evidence had to be refreshed.
- The project is still under concurrent multi-agent build activity, so validation cannot be assumed green.

What was done:
- Ran an existing-obj Core build refresh into `Build_UBER_NOIR_INTEGRATOR_core_loop38_refresh.log`.
- Ran an isolated temp-obj Core build into `Build_UBER_NOIR_INTEGRATOR_core_loop39_unique.log`.
- Ran another existing-obj Core build with disabled build servers into `Build_UBER_NOIR_INTEGRATOR_core_loop40_existing_obj.log`.
- Inspected active `dotnet.exe` command lines and left other agents' builds running.

Cinematic cheats used:
- None. This was validation boundary work.

Exact microseconds saved:
- None. Validation only.

Verification:
- `Build_UBER_NOIR_INTEGRATOR_core_loop39_unique.log` fails with `NETSDK1004` because the isolated temp obj path has no restored `project.assets.json`.
- `Build_UBER_NOIR_INTEGRATOR_core_loop38_refresh.log` and `Build_UBER_NOIR_INTEGRATOR_core_loop40_existing_obj.log` contain only `EXIT=-1` with no MSBuild diagnostics.
- This is not a Rendering compile diagnostic and not a green build. Unity shader import, material conversion, Vulkan, and DX12 player validation remain blocked/inconclusive.

## 2026-05-17 Loop 40 - CBUFFER-Owned Noir Fog Floors

What was wrong:
- `H8WaterExtinctionApplyFogTint` still owned hardcoded abyss/tint floor literals.
- `Hecton_NoirDepthFog.shader`, which consumes the same extinction helper, still had raw `rcp` calls in depth range, density decode, and fast exponential approximation math.

What was done:
- Changed `H8WaterExtinctionApplyFogTint` to accept caller-owned `extinctionFloor` and `abyssFloor`.
- Updated UberNoir to pass `_NoirFogColor` and `_NoirAbyssFloorColor`.
- Updated hidden NoirDepthFog to pass `_HectonNoirDepthFogShallowColor` and `_HectonNoirDepthFogAbyssColor`.
- Added `HectonNoirDepthFogFinite` and `HectonNoirDepthFogSafePositiveRcp`.
- Routed NoirDepthFog reciprocal math through the safe helper and marked full-screen skip exits with `[branch]`.

Cinematic cheats used:
- Still the same Dear Lie: analytical Beer-Lambert/depth fog and marine-snow density modulation, not physical volumetric simulation.

Exact microseconds saved:
- None measured. Static improvement is safer reciprocal math and preserved branch exits for sky/no-fog pixels.

Verification:
- `H8WaterExtinctionApplyFogTint` call surface is exactly two callers plus its definition.
- `Hecton8_UberNoir.hlsl`, `Hecton_WaterExtinction.hlsl`, `Hecton_NoirDepthFog.shader`, and `Hecton_SnellRefractionCore.hlsl` all report `BraceDelta=0` with matched preprocessor counts.
- `git diff --check` passes for the touched shader files with line-ending warnings only.

## 2026-05-17 Loop 41 - Extinction Resolve Order Work-Shed

What was wrong:
- Active packed-LUT extinction resolves computed analytical Beer-Lambert RGB first, then discarded it when the LUT was active.

What was done:
- Moved `H8WaterExtinctionActive()` ahead of analytical fallback computation in LUT-enabled variants.
- Kept Low/mobile compiled paths analytical.
- Kept inactive desktop LUT paths analytical.
- Kept active High/Ultra paths on packed RGB LUT sampling without the discarded `exp2` vector.

Cinematic cheats used:
- No new effect. This preserves the Dear Lie analytical fallback and removes wasted ALU from the richer LUT path.

Exact microseconds saved:
- None measured. Static saving is one discarded analytical RGB `exp2` resolve per active packed-LUT fog sample.

Verification:
- `Hecton_WaterExtinction.hlsl` reports `BraceDelta=0`, `Open=18`, `Close=18`, `IfCount=2`, `PreIf=5`, `PreEndif=5`.
- `git diff --check -- Assets/_Project/Art/Shaders/Hecton_WaterExtinction.hlsl` passes with line-ending warning only.

## 2026-05-17 Loop 42 - Blackbox Single-Owner Regression Repair

What was wrong:
- `HectonUberNoirRuntimeBridge` source still wrote full and empty fault dumps to `Dump_EXTINCTION_LUT_SAMPLER.bin`.
- That contradicted the existing single-owner blackbox rationale and polluted another agent's diagnostic artifact.

What was done:
- Removed `ExtinctionDumpFileName`.
- Removed the duplicate full fault-dump write.
- Removed the duplicate empty reason-coded fault-dump write.
- Kept the UberNoir 300-frame telemetry ring and `Dump_UBER_NOIR_INTEGRATOR.bin` output intact.

Cinematic cheats used:
- None. This was blackbox ownership repair.

Exact microseconds saved:
- None measured. Fault path writes one binary artifact instead of two; hot path is unchanged.

Verification:
- Static scan of `HectonUberNoirRuntimeBridge.cs` now shows only `IntegratorDumpFileName` writes.
- `rg` finds no `Dump_EXTINCTION_LUT_SAMPLER` or `ExtinctionDumpFileName` in `Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs`.
- `git diff --check` passes for the touched bridge/docs files with line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` failed in `Build_UBER_NOIR_INTEGRATOR_core_loop42_blackbox_regression.log` because `Temp/obj/Hecton8.Core/Hecton8.Core.GeneratedMSBuildEditorConfig.editorconfig` is missing; the log contains no UberNoir diagnostics.
- `dotnet build .\Hecton8.Core.csproj -v:minimal` restored/up-to-date successfully, then failed in `Build_UBER_NOIR_INTEGRATOR_core_loop42_restore_attempt.log` at unrelated `Core/SystemDispatcher.cs(65,113)` because `IScalabilityChangedEventListener.OnScalabilityChanged(in ScalabilityChangedEvent)` is not implemented; the log contains no UberNoir diagnostics.

## 2026-05-17 Loop 43 - Multiplatform Boundary Refresh

What was wrong:
- Multiplatform evidence needed a fresh pass after bridge and shader churn.
- Repo-wide Graphics scans include non-`Pack=1` structs outside the UberNoir Rendering/URP boundary.

What was done:
- Scanned all `numthreads(...)` declarations under `Assets/_Project/Art/Shaders`.
- Scanned UberNoir-owned Rendering structs and hot-path ownership markers.
- Scanned the UberNoir shader chain for `GrabPass`, legacy samplers, UAVs, and compute-only syntax.
- Recorded cross-domain Graphics struct-layout hits without editing them.

Cinematic cheats used:
- None. This was portability evidence.

Exact microseconds saved:
- None. Verification only.

Verification:
- Maximum shader thread group found is `numthreads(8, 8, 8)` = 512 threads in `Hecton_SonarMap.compute`, below Metal's 1024 limit.
- UberNoir-owned `HectonUberNoirRuntimeBridge.UberNoirShaderTelemetryEntry` remains `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`.
- The owned UberNoir shader chain has no `GrabPass`, `tex2D`, `sampler2D`, UAV, `groupshared`, or `numthreads` hits.
- Cross-domain non-`Pack=1` hits are in `Assets/_Project/Scripts/Graphics/*` and were not edited under this prompt's domain boundary.

## 2026-05-17 Loop 44 - Portable LUT I/O Recheck

What was wrong:
- Steam Deck/mobile I/O evidence needed a fresh source read after the latest inquisition pass.

What was done:
- Re-read `LutArrayResolver.ShouldUseAnalyticalFallbackOnly()`.
- Re-read the LUT streaming/staging path and scratch-buffer copy path.
- Verified the fallback path publishes analytical globals before the early return.

Cinematic cheats used:
- Analytical Beer-Lambert Dear Lie on portable/low-memory targets instead of a 32 MB packed LUT.

Exact microseconds saved:
- None newly measured. Static behavior avoids the 32 MB matrix path on Android/VisionOS, SteamDeck-like profiles, and <=2048 MB graphics memory.

Verification:
- `ShouldUseAnalyticalFallbackOnly()` returns true for `UNITY_ANDROID || UNITY_VISIONOS`.
- It returns true for `HardwareTierDetector.IsSteamDeckLike`.
- It returns true when `SystemInfo.graphicsMemorySize` is `> 0 && <= 2048`.
- Those cases return before `ResolveMatrixPath()`, `UnityWebRequest`, texture allocation, and `TryStreamFileIntoRawTexture`.
- High-memory non-portable filesystem reads use `FileOptions.SequentialScan` and a 128 KB scratch buffer; URI staging uses `DownloadHandlerFile`.

## 2026-05-17 Loop 45 - Previous-Normal Motion Vector Repair

What was wrong:
- `H8UberNoirMotionVertex` used `UNITY_PREV_MATRIX_M` for previous position but reused current-frame `normalWS` for previous hull bending and wake deformation.
- Rotating or non-uniformly transformed hard-surface meshes could therefore write motion vectors from a previous displaced position bent along the wrong normal frame.

What was done:
- Added previous-frame normal transform through `UNITY_PREV_MATRIX_I_M`.
- Routed previous dynamic hull bending and wake deformation through `previousNormalWS`.
- Kept current-frame deformation on `instanceData.WorldToObject`.

Cinematic cheats used:
- No new visual fake. This protects the existing displaced-hull and wake fakes from STP ghosting.

Exact microseconds saved:
- None measured. Cost is one additional previous normal transform in the MotionVectors pass only; benefit is temporal correctness, not cheaper shading.

Verification:
- `Hecton8_UberNoir.hlsl` reports `BraceDelta=0`, `Open=63`, `Close=63`, `IfCount=23`, `PreIf=31`, `PreEndif=31`.
- URP package input defines `UNITY_PREV_MATRIX_I_M` as `unity_MatrixPreviousMI`.
- `git diff --check` passes for the touched shader/docs files with line-ending warnings only.
- No `dotnet build` was run for this shader-only pass per user instruction.

## 2026-05-17 Loop 46 - ShadowCaster Consolidation Repair

What was wrong:
- UberNoir consolidation could move DryZone/Triplebrick hard-surface materials off their source shaders, but the target shader had no owned ShadowCaster pass.
- That would make the purge look clean in material inventory while converted displaced/dithered surfaces stopped casting correct shadows.

What was done:
- Added a `ShadowCaster` pass to `Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader`.
- Added `H8UberNoirShadowVertex` and `H8UberNoirShadowFragment` to `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl`.
- Shadow vertices now reuse UberNoir instance transforms, hull dents, dynamic pressure bend, global wake deformation, and URP shadow bias/clamping.
- Shadow fragments reuse base alpha, instance fade, and `H8UberNoirClipDitheredTransparency`.

Cinematic cheats used:
- Same dithered cutout fake as ForwardLit/MotionVectors; no alpha blend shadow surface.

Exact microseconds saved:
- None measured. This is correctness for the consolidation path; extra cost occurs only during shadow map rendering for objects using the shader.

Verification:
- `Hecton8_UberNoir.hlsl` reports `BraceDelta=0`, `Open=66`, `Close=66`, `IfCount=23`, `PreIf=36`, `PreEndif=36`.
- `rg` confirms `H8UberNoirShadowVertex`, `H8UberNoirShadowFragment`, `ApplyShadowBias`, `ApplyShadowClamping`, `_LightDirection`, and `_LightPosition` are present in the owned shader chain.
- `git diff --check` passes for the touched shader files with line-ending warnings only.
- No `dotnet build` or Unity rebuild was run per user instruction.

## 2026-05-17 Loop 47 - Material Queue Normalization

What was wrong:
- Wet-glass/seep source materials can keep transparent `renderQueue`/RenderType state after switching to the alpha-clipped UberNoir shader.
- That would preserve late transparent ordering and overdraw even though UberNoir resolves those surfaces as dithered cutouts with ZWrite.

What was done:
- Added `RequiresDitheredCutout()` to the material consolidator.
- Added `ApplyRenderState()` so dithered conversions use `RenderQueue.AlphaTest` and `RenderType=TransparentCutout`.
- Opaque DryZone, ToolDecay, and URP Lit construction projections now use `RenderQueue.Geometry` and `RenderType=Opaque`.

Cinematic cheats used:
- Dithered alpha-cutout queue instead of transparent blending for wet/seep glass projection.

Exact microseconds saved:
- None measured. The intended saving is future overdraw reduction when the Unity converter can run.

Verification:
- `rg` confirms `ApplyRenderState`, `RequiresDitheredCutout`, `renderQueue`, and `SetOverrideTag` in `HectonUberNoirMaterialConsolidator.cs`.
- `git diff --check` passes for the touched consolidator with line-ending warning only.
- No `dotnet build` or Unity rebuild was run per user instruction.

## 2026-05-17 Loop 48 - Legacy Keyword Scrub

What was wrong:
- Material YAML in the conversion roots still carries source-shader keywords such as `_ALPHABLEND_ON`, `_NORMALMAP`, and `_SURFACE_TYPE_TRANSPARENT`.
- Those keywords are not UberNoir feature authority and can survive shader swap as serialized residue.

What was done:
- Added a fixed `LegacySourceKeywords` list to the material consolidator.
- Added `DisableLegacySourceKeywords()` and call it before enabling UberNoir caustics/refraction keywords.
- Kept target keyword ownership limited to `H8_UBERNOIR_CAUSTICS_TEXTURED` and `H8_UBERNOIR_SCREEN_REFRACTION`.

Cinematic cheats used:
- None. This is material-state cleanup.

Exact microseconds saved:
- None measured. Expected impact is reduced invalid keyword/variant residue after conversion.

Verification:
- `rg` confirms `LegacySourceKeywords` and `DisableLegacySourceKeywords` in `HectonUberNoirMaterialConsolidator.cs`.
- `git diff --check` passes for the touched consolidator with line-ending warning only.
- No `dotnet build` or Unity rebuild was run per user instruction.
