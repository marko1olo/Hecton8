# LOG_TECH_SCOUT

## 2026-05-11 - Loop 1 Mandate Scout

What was wrong:
- No `TECH_SCOUT` status/rationale files existed.
- `Docs/Tasks/CURRENT_BATCH.txt` was empty, so batch XML extraction was impossible.
- Project package lock uses Burst 1.8.28, not a verified Burst 2.0 package.
- Runtime static scan still finds `Graphics.Blit` in `DiegeticPanelController`.
- Runtime static scan still finds `Task.Run` in `GameBootstrapper` and `SaveBinaryStorage`.

What was done:
- Added `.agents-skills/REND_GPU_Sovereignty.txt`.
- Added `.agents-skills/STRM_Async_Standard.txt`.
- Added `.agents-skills/MANDATE_VERSION_6.0.txt`.
- Added `Docs/Tasks/Status_TECH_SCOUT.md`.
- Added `Docs/AgentLogs/Rationale_TECH_SCOUT.md`.
- Ran isolated Core compile: passed with 0 warnings and 0 errors.

Cinematic cheats used:
- GPU Resident Drawer law treats static environment as renderer-owned GPU residency instead of sim-owned draw submission.
- Eco-Director compute boundary keeps deterministic CPU FrostTick authority and moves only dense visual/scalar fields to GPU.
- Low-tier math law prefers triangle/parabolic/LUT oscillation over `math.sin` until profiler proof exists.

Exact microseconds saved:
- PENDING VERIFICATION. No runtime profiler capture was produced.
- Compile evidence exists only for first-party Core with `--no-dependencies`.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Loop 2 Mandate Scout

What was wrong:
- LZ4 dictionary compression was requested, but the current save codec does not bind dictionary APIs.
- `Graphics.SetDescriptorSet` was requested, but no Unity C# API/project symbol was found.
- No live root `PROJECT_ATLAS.md` exists; only archived references were found.
- Virtual Texturing was requested for terrain, but static evidence showed MicroSplat/Texture2DArray paths, not active SVT.

What was done:
- Added `.agents-skills/STRM_ModuleDTO_LZ4_Dictionary.txt`.
- Added `.agents-skills/REND_DescriptorBinding_Reality_Check.txt`.
- Added `.agents-skills/REND_Terrain_VirtualTexturing.txt`.
- Added `.agents-skills/NET_Logistics_Quantum.txt`.
- Updated `Docs/Tasks/Status_TECH_SCOUT.md`.
- Updated `Docs/AgentLogs/Rationale_TECH_SCOUT.md`.

Cinematic cheats used:
- Long-distance power sync is graph-delta replication plus shader-time visual pulse, not physical propagation.
- Terrain low-tier path stays texture-array/mip-bias first instead of expensive material variety.

Exact microseconds saved:
- PENDING VERIFICATION. These are mandates and static audits; no profiler capture was produced.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Compile Medic

What was wrong:
- Isolated Core compile failed after another first-party change introduced references to `ScalabilityEvents` and `ScalabilityTierProfiles`.
- The symbols existed in `Assets/_Project/Scripts/Core/IPlatformIntegration.cs`, but the generated CLI project file did not include that new script.

What was done:
- Added `Assets/_Project/Scripts/Core/IPlatformIntegration.cs` to `Hecton8.Core.csproj`.
- Re-ran isolated Core compile. Result: 0 warnings, 0 errors.

Cinematic cheats used:
- None. Compile repair only.

Exact microseconds saved:
- 0 runtime microseconds claimed.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Loop 3 Mandate Scout

What was wrong:
- Zero-GC law did not explicitly govern `Span<char>`/`ReadOnlySpan<char>` and `string.Create`.
- Blue-noise shadow usage lacked guardrails.
- Linux/Vulkan shader stutter was not proven by player capture.
- Arena allocator existed, but 2.0 ownership rules were not documented.
- Haptic spatialization existed as a pattern, but not as a hard event-authored law.

What was done:
- Updated `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`.
- Updated `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`.
- Updated `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`.
- Added `.agents-skills/REND_Shader_Stutter_Linux_Vulkan.txt`.
- Added `.agents-skills/OPT_HectonArenaAllocator_2_0.txt`.
- Updated TECH_SCOUT status/rationale.

Cinematic cheats used:
- Blue-noise/Bayer shadow fades instead of more PCF taps.
- Haptic spatialization as event-authored dominant-axis motor weighting instead of continuous simulation.

Exact microseconds saved:
- PENDING VERIFICATION. No profiler capture was produced.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Loop 4 Mandate Scout

What was wrong:
- AUP law lacked a hard rule for planet-circumference wrapping.
- DirectStorage was requested, but no Unity-managed DirectStorage API or project wrapper exists in current evidence.
- Physics determinism was not strict enough for multithreaded body solving.
- GPU occlusion guidance existed in fragments but lacked Unity 6000 Forward+/GRD/RenderGraph gates.
- Evidence reporting could still confuse text search with verification.

What was done:
- Updated `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`.
- Added `.agents-skills/STRM_DirectStorage_Reality_Check.txt`.
- Added `.agents-skills/PHYS_Determinism_Multithreaded_Body_Solving.txt`.
- Added `.agents-skills/REND_GPU_Occlusion_Culling_6000.txt`.
- Added `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`.
- Updated TECH_SCOUT status/rationale.

Cinematic cheats used:
- Low-tier planetary wrapping hides discontinuity with fog, depth darkness, and chunk fade instead of seamless physical world continuity.
- Low-tier physics determinism uses primitive/dominant-axis approximations before expensive contact richness.

Exact microseconds saved:
- PENDING VERIFICATION. No runtime profiler capture was produced.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Loop 5 Mandate Scout

What was wrong:
- The project had useful `math.rsqrt` usage, but no hard i3 rsqrt law and remaining runtime `.normalized` candidates.
- VR stencil masking existed in another agent log, but no mandate recorded the performance conditions.
- `MATH_VIOLATIONS` had no existing CI gate.
- VRS was unsafe to treat as an MX350 feature.
- Weighted random "slot machine" selection lacked a hard deterministic integer rule.

What was done:
- Added `.agents-skills/MATH_Rsqrt_i3_SIMD.txt`.
- Added `.agents-skills/REND_VR_Stencil_Masking.txt`.
- Added `.agents-skills/CI_MATH_VIOLATIONS_Gate.txt`.
- Added `.agents-skills/REND_VRS_MX350_Reality_Check.txt`.
- Added `.agents-skills/MATH_Deterministic_RNG_SlotMachine.txt`.
- Updated TECH_SCOUT status/rationale.

Cinematic cheats used:
- Low-tier visual vector normalization may use dominant-axis/L1 approximation before exact unit vectors.
- MX350 uses render scale, stencil, occlusion, and LOD instead of VRS.
- Slot-machine variation separates deterministic gameplay result from richer Ultra presentation variance.

Exact microseconds saved:
- PENDING VERIFICATION. No profiler capture was produced.
- Prior VR stencil local estimate remains 40-120 us GPU, not verified by this pass.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Compile Medic 2

What was wrong:
- Isolated Core compile failed after active DataArchaeology edits.
- `ScannerTool.cs` referenced `DataArchaeologyRuntime`, but `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs` was not included in `Hecton8.Core.csproj`.
- `DataArchaeologyRuntime.cs` depended on `LoreMmfEncyclopedia.cs`, also missing from the generated project file.
- `ScannableFragment.cs` used `unlockId.AsSpan()` without `using System;`.

What was done:
- Added `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs` to `Hecton8.Core.csproj`.
- Added `Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs` to `Hecton8.Core.csproj`.
- Added `using System;` to `Assets/_Project/Scripts/Gameplay/ScannableFragment.cs`.
- Re-ran isolated Core compile. Result: 0 warnings, 0 errors.

Cinematic cheats used:
- None. Compile repair only.

Exact microseconds saved:
- 0 runtime microseconds claimed.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Loop 6 Mandate Scout

What was wrong:
- Async upload policy did not explicitly set time slice and persistent-buffer behavior from the Scalability Matrix.
- Non-reload scene transitions lacked a hard global/static reset protocol.
- Compute shader group sizing could drift into invalid 256-thread assumptions despite MX350 kernels using 64.
- The "Pentarchy" model is stale against the authoritative 9-echelons / 85-domains map.
- Mandate v6 needed the final loop updates and explicit broken/outdated findings.

What was done:
- Added `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`.
- Added `.agents-skills/CORE_Global_State_Reset_NonReload_Transitions.txt`.
- Added `.agents-skills/GPU_Compute_Warp_Sizing_Mobile.txt`.
- Added `.agents-skills/ARCH_Pentarchy_Audit.txt`.
- Updated `.agents-skills/MANDATE_VERSION_6.0.txt`.
- Updated `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` to set async upload time slice and persistent buffer from bootstrap tier policy.
- Corrected the stale `BoidSimulation.compute` comment from 256-thread tiles to the actual 64-thread MX350 baseline.
- Updated TECH_SCOUT status/rationale.

Cinematic cheats used:
- Low/MX350 async streaming uses smaller upload slices and visual tolerance for gradual mip/asset residency instead of forcing blocking uploads.
- Low/mobile compute policy prefers 32/64-thread work and staggered dispatch instead of brute-force wider groups.
- Pentarchy was replaced by explicit enforcement pillars: evidence, scalability, streaming, determinism, and anti-corruption.

Exact microseconds saved:
- PENDING VERIFICATION. No Unity Player profiler or GPU capture was produced.
- Runtime code cost added: boot-time QualitySettings assignments only; steady-frame cost is 0 us.

Final status:
- PENDING VERIFICATION.

## 2026-05-11 - Omega Anti-Bloat Inquisition

What was wrong:
- TECH_SCOUT changes had to be rechecked for hidden hot-path math, managed allocation, string formatting, per-frame branching, and fake verification.
- The first isolated compile rerun failed before source compilation because dependency DLLs were missing from `Temp/bin/Debug`.

What was done:
- Ran dependency-building Core compile: `dotnet build Hecton8.Core.csproj -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Result: Build succeeded, 47 dependency/package warnings, 0 errors.
- Re-ran isolated Core compile: `dotnet build Hecton8.Core.csproj --no-dependencies -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`.
- Result: Build succeeded, 0 warnings, 0 errors.
- Ran `git diff --check` on TECH_SCOUT touched paths. Result: no whitespace/conflict-marker errors; Git reported LF-to-CRLF normalization warnings only.
- Scanned touched runtime files for `foreach`, `string.Format`, interpolated strings, `.ToString()`, `sqrt`, `normalize`, `.normalized`, `Mathf.Sqrt`, and `Vector3.Distance`.

Cinematic cheats used:
- No live physical/mathematical runtime simulation was edited by TECH_SCOUT, so no existing honest calculation was replaced in code.
- Mandated cheats now recorded: fog/chunk fade for low-tier planetary wrap seams, dominant-axis/L1 visual vector fakes, stencil rejection for hidden VR HUD fragments, deterministic integer RNG with richer Ultra presentation variance, and 32/64-thread or staggered compute before brute-force wide groups.

Scalability Matrix adaptation:
- Low/MX350: async upload 64 MB / 1 ms, 64-thread compute default, visual fakes before expensive math.
- Middle: async upload 128 MB / 2 ms, capped work, explicit reset/streaming ownership.
- High: async upload 256 MB / 4 ms, optional wider compute variants only after capture.
- Ultra: spend verified headroom on density and presentation, not on authority drift.

Exact microseconds saved:
- 0 us steady-frame runtime cost added by TECH_SCOUT code. The QualitySettings writes execute during bootstrap scalability application.
- Async upload hitch savings: PENDING PLAYER PROFILER.
- Compute group sizing savings: PENDING GPU CAPTURE.
- Prior VR stencil estimate remains 40-120 us GPU, not verified here.

Final Git Diff:
- Runtime/code: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`, `Assets/_Project/Scripts/BoidSimulation.compute`, `Assets/_Project/Scripts/Gameplay/ScannableFragment.cs`, `Hecton8.Core.csproj`.
- Mandates added/updated under `.agents-skills/`: GPU Resident Drawer, async standard, RenderGraph, LZ4 dictionary, descriptor binding, terrain VT, logistics quantum, shader stutter, arena 2.0, DirectStorage, physics determinism, GPU occlusion, evidence reporting, rsqrt, VR stencil, CI math gate, VRS, deterministic RNG, async upload, global reset, compute warp sizing, Pentarchy audit, and mandate v6 summary.
- Logs/status: `Docs/Tasks/Status_TECH_SCOUT.md`, `Docs/AgentLogs/Rationale_TECH_SCOUT.md`, `Docs/AgentLogs/LOG_TECH_SCOUT.md`.

Final status:
- PENDING VERIFICATION. `VERIFIED MASTER GRADE` is rejected until Unity Console, PlayMode, GCMonitor, profiler, RenderGraph Viewer, Frame Debugger, and Player captures exist.
