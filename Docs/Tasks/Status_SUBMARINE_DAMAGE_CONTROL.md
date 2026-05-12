# Status_SUBMARINE_DAMAGE_CONTROL

STATUS: PENDING VERIFICATION
Prompt: SUBMARINE_DAMAGE_CONTROL
Domain: HABITAT & VEHICLES
Task count: 15

## Mandates Selected
- [x] `CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt` | Justification: breach state, repair feedback, and damage visual response must stay data-driven | Alternative rejected: prefab leak emitters | Estimate: 900 us
- [x] `CORE_Submarine_Vehicles_Kinematics_AUP.txt` | Justification: submarine damage must not corrupt AUP/local-space authority | Alternative rejected: world-space breach drift | Estimate: 700 us
- [x] `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt` | Justification: repair tool must use the existing RaycastCommand-backed interaction lane | Alternative rejected: direct Physics.Raycast loop | Estimate: 650 us
- [x] `PHYS_Fluid_Incursion_Interior.txt` | Justification: breach severity must buy sinking/flood mass response, not just VFX | Alternative rejected: cosmetic-only leak severity | Estimate: 800 us
- [x] `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt` | Justification: one dispatch and lock-buffer uploads are required for low-end GPUs | Alternative rejected: one emitter per breach | Estimate: 850 us
- [x] `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt` | Justification: leak plume must be a visual cheat on GPU data | Alternative rejected: simulated water volume | Estimate: 700 us
- [x] `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` | Justification: Burst repair and NativeArray ownership must be explicit | Alternative rejected: managed breach list | Estimate: 850 us
- [x] `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` | Justification: runtime repair/leak sparks cannot allocate or instantiate | Alternative rejected: ParticleSystem play path | Estimate: 700 us

## Loop 0 - Initialization
- [x] Extracted `<AGENT_PROMPT id="SUBMARINE_DAMAGE_CONTROL">` from `Docs/Tasks/CURRENT_BATCH.md` | Justification: batch prompt protocol requires CLI extraction from cover to cover before architecture decisions | Alternative rejected: relying on IDE tab context or neighboring prompts | Estimate: 1200 us
- [x] Verified status/rationale hygiene | Justification: missing files indicate fresh batch state for this ID | Alternative rejected: reading unrelated agents' prior logs | Estimate: 300 us

## Tasks 1-5
- [x] Task 1 - BREACH S.O.A. BUFFER | Justification: `NativeArray<float4>` stores xyz local position and w severity, capped at 64 | Alternative rejected: managed list / breach GameObjects | Estimate: 900 us
- [x] Task 2 - LOCAL-SPACE INTEGRITY | Justification: breach positions are authored and repaired through submarine-local `Transform.InverseTransformPoint` only | Alternative rejected: AUP shifting breaches | Estimate: 450 us
- [x] Task 3 - GPU WATER SPRAY | Justification: `Hecton_LeakPlume.compute` receives the packed breach buffer and expands all 64 slots in one dispatch | Alternative rejected: per-leak ParticleSystem prefabs | Estimate: 1200 us
- [x] Task 4 - REPAIR TOOL RAYCAST | Justification: `RepairTool` keeps the existing queued RaycastCommand path and routes submarine hits through `ISubmarineDamageControlTarget` | Alternative rejected: direct `Physics.Raycast` | Estimate: 700 us
- [x] Task 5 - BURST REPAIR JOB | Justification: `BreachRepairJob` scans 64 entries with `math.distancesq` and reduces `w` inside the 1m radius | Alternative rejected: main-thread managed repair loop | Estimate: 650 us

## Tasks 6-10
- [x] Task 6 - DECAL PROJECTION | Justification: breach scorch/spray feedback is routed through `AbyssalFluidDecalManager.RegisterPressureSpray`, not Decal GameObjects | Alternative rejected: pooled URP DecalProjector per breach | Estimate: 500 us
- [x] Task 7 - FLOODING MATH COUPLING | Justification: sum(w) * ambient pressure feeds `SubmarineFluidDynamics.ApplyDamageControlLeakMass` to increase hull mass | Alternative rejected: cosmetic severity only | Estimate: 700 us
- [x] Task 8 - ZERO-GC SPARKS | Justification: repair sparks publish `DebrisSpawnSignal` with the existing repair-spark species hash | Alternative rejected: `RepairTool.sparksVFX.Play()` | Estimate: 450 us
- [x] Task 9 - AUDIO COUPLING | Justification: active leaks publish cadence-limited `ImpactSignal` at submarine AUP | Alternative rejected: single-use AudioEvent ID | Estimate: 400 us
- [x] Task 10 - AUTO-HEAL REMOVAL | Justification: inactive breaches compact by swap-last and zeroing the tail slot | Alternative rejected: `Array.Copy` / stable-order compaction | Estimate: 300 us

## Tasks 11-15
- [x] Task 11 - VISUAL CHEAT | Justification: no cabin water mesh/volume was added; leak visuals stay screen-space/GPU and sinking physics carries consequence | Alternative rejected: simulated interior water fill | Estimate: 250 us
- [x] Task 12 - MATH LOD | Justification: low memory or low math precision caps visible compute breaches to 8 while logic keeps 64 | Alternative rejected: reducing logical breach capacity | Estimate: 350 us
- [x] Task 13 - NO COROUTINES | Justification: repair/flood/leak dispatch run through fixed/post-fixed dispatcher lanes only | Alternative rejected: coroutine timers | Estimate: 250 us
- [x] Task 14 - RECONNAISSANCE PROTOCOL | Justification: prefab scan logged to `Docs/AgentLogs/RECON_SUBMARINE_DAMAGE_CONTROL.md` | Alternative rejected: relying on memory of prefab layout | Estimate: 1000 us
- [x] Task 15 - OMEGA COMPILE CHECK | Justification: latest single-node `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false -v:minimal` succeeded with 0 errors; 47 warnings are external URP/GPUInstancer/Crest/ShaderGraph package warnings, not touched damage-control code | Alternative rejected: claiming Unity import/shader/playmode verification without stable editor proof | Estimate: 151810000 us

## Iterative Loops
- [x] Loop 1 - Tasks 1-5 implementation + compile check | Result: implementation present; compile gate blocked by unrelated project errors | Estimate: 4300 us
- [x] Loop 2 - Tasks 6-10 implementation + compile check | Result: coupling/signals/compaction present; no new compile errors surfaced before dependency wall | Estimate: 3400 us
- [x] Loop 3 - Tasks 11-15 implementation + compile check | Result: visual cheat, LOD, no coroutine, recon done; task 15 blocked by dependency wall | Estimate: 2800 us
- [x] Loop 4 - Self-audit array compaction, prompt re-read, code review | Result: prompt re-extracted; `rg` found no `Array.Copy`/coroutine in touched damage-control paths; existing hull impact ParticleSystem left untouched as outside prompt | Estimate: 1500 us
- [x] Loop 5 - Polish mandate, compile/console check, final report | Result: polish audit applied `ResolveSafeDirection` replacement; compile remains dependency-blocked; final report appended | Estimate: 1700 us
- [x] Loop 6 - Continuation hardening | Result: deferred breach adds while Burst repair owns `_breaches`, skipped empty unchanged compute dispatches, removed sine hash from leak plume shader, expanded repair-tool interface lookup scratch to avoid first-hit List growth; Unity MCP unavailable; `dotnet build` still dependency-blocked in `AcousticOcclusionUtility.cs` | Estimate: 1900 us
- [x] Loop 7 - GPU render bridge + compile recovery | Result: `Hecton_LeakPlume.shader` now has a `_UseLeakParticleBuffer` procedural path; `SubmarineStructuralGrid` renders computed plume points via late-frame `Graphics.RenderPrimitives`; `dotnet build` is clean; Unity MCP console/script validation remains unavailable (`no_unity_session`) | Estimate: 104500 us
- [x] Loop 8 - Procedural plume visibility hardening + Unity partial validation | Result: procedural plume branch no longer relies on absent `COLOR` vertex streams from `Graphics.RenderPrimitives`; compute kernel lookup now uses `HasKernel` before `FindKernel`; `dotnet build` remains 0 warnings/0 errors; Unity MCP validated `SubmarineStructuralGrid.cs` with 0 diagnostics and confirmed shader/compute/material asset imports before the session disconnected again | Estimate: 16800 us
- [x] Loop 9 - Black-box telemetry footprint hardening | Result: `DamageControlTelemetryEntry` is now an explicit 32-byte record using ushort counts while preserving local breach position, severity sum, frame, flags, and state hash; single-node C# build succeeded with 0 errors and only external package warnings | Estimate: 151300000 us
- [x] Loop 10 - Leak mass cap hardening + full rebuild | Result: `SubmarineFluidDynamics.ApplyDamageControlLeakMass` now falls back to exterior displacement mass or dry rigidbody mass when capacity is zero, preventing unbounded leak ballast accumulation; latest full C# build succeeded with 0 errors and 47 external package warnings; Unity MCP console read failed at transport layer | Estimate: 151810000 us
