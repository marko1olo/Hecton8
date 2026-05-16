# LOG - INTERACTIVE_WAKE_VFX

## 2026-05-16 - Blocked Prompt Extraction

What was wrong: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="INTERACTIVE_WAKE_VFX">`. The companion audit lists this prompt as missing and explicitly says not to invent or synthesize missing prompts.

What was done: Read project authority files, searched the active batch with CLI extraction, confirmed absence, created `Docs/Tasks/Status_INTERACTIVE_WAKE_VFX.md`, and created `Docs/AgentLogs/Rationale_INTERACTIVE_WAKE_VFX.md`.

Cinematic Cheats used: None implemented. The expected wake displacement work remains undefined until the real XML prompt is restored.

Exact Microseconds saved: 0 us runtime. Avoided an unauthorized implementation that could duplicate existing wake infrastructure or break compile.

Verification: Static document verification only. No code edits. No compile run.

## 2026-05-16 - Phase 1 The Great Purge

What was wrong: First-party wake authority existed only as procedural flora sway behavior, with no narrow `IWakeDisplacementService` registry contract. Active procedural wake source state was privately owned by `FloraInteractionManager`, and Phase 1 required DataVault ownership plus a hard ban on Unity `WindZone` / `ForceField` / `ParticleSystem.forceOverLifetime` paths.

What was done: Added `IWakeDisplacementService` to `GlobalRegistryContracts`, exposed `GlobalRegistry.WakeDisplacement`, and registered/unregistered `FloraInteractionManager` through `GlobalRegistry.RegisterWakeDisplacementService` / `UnregisterWakeDisplacementService`. Added `BufferID.WakeSources` and moved procedural wake source storage to a `GlobalDataVault` buffer resolved with `VaultBufferHandle<ProceduralWakePoint>` under `SystemID.Vfx`. Stored AUP in each wake source and kept shader output as raw `Shader.SetGlobalVectorArray`.

Cinematic Cheats used: Fixed 16-source analytic wake list instead of fluid simulation. Shader-facing payload stays packed as `Vector4` radius/intensity data; low tier can consume nearest/radial displacement while high tier can spend GPU math on curvature later. No Unity wind components.

Exact Microseconds saved: 0 us/frame from WindZone purge because first-party scan found no existing first-party WindZone path. 0-5 us/frame estimated on i3/MX350 from removing the private wake native allocation owner and preventing duplicate singleton/manager authority. Saved cycles are reserved for later visible shader displacement, not broader CPU simulation.

Verification: `rg -n "WindZone|m_WindMain|m_WindTurbulence|forceOverLifetime|ParticleSystemForceField|ForceField" Assets/_Project` returned no first-party hits. `rg -n "WakeManager\\.Instance|WakeManager|RegisterProceduralSwayDirector\\(this\\)|UnregisterProceduralSwayDirector\\(this\\)|new NativeArray<ProceduralWakePoint>|DisposeNativeArray\\(ref _proceduralWakePoints\\)" Assets/_Project/Scripts` returned no hits. XML prompt was re-read after three tasks from `Docs/Tasks/CURRENT_BATCH.md`.

Compile Status: `[BLOCKED BY DEPENDENCY]`. `dotnet build .\Hecton8.Core.csproj -v:minimal` exits 1 with 159 visible errors from missing cross-domain contracts/namespaces including `IJobAdmissionService`, `ISimulationBucketer`, `MacroDatabase*`, `IPlayerMovementContracts`, `FoveatedSimulationTier`, and `H8WorldPage*`. No visible build error named the new wake interface, `WakeSources`, or `FloraInteractionManager` wake changes before the dependency wall.

## 2026-05-16 - Verified Wake Displacement Pass

What was wrong: The wake kernel had data sovereignty and blackbox coverage, but Phase 3/4 visual consumers still needed hard evidence: low-tier nearest-wake fakes, high-tier vortex curvature, MarineSnow turbulence, STP-stable vertex displacement, normal perturbation, and boid wake reaction. Earlier compile-wall notes were also stale after other integration work landed.

What was done: `Hecton8_UberNoir.hlsl` now consumes `_GlobalWakeBuffer`, `_GlobalWakeVectors`, and `_GlobalWakeParams` for vertex displacement and normal tilt. `_MATH_LOD_LOW` finds the two nearest active wakes and applies radial-only displacement. Full tier applies dot-radius falloff, wake-direction cross products, spatial triangle modulation, and finite-safe normal perturbation. `Hecton_FluidAdvection.compute` adds high-intensity triangle turbulence inside wake intersections for silt/marine snow. `SargassumMicroFaunaBoids.compute` reads the same global wake arrays for radial plus vortex scatter, capped to two wake slots on low/simplified tiers.

Cinematic Cheats used: No fluid solver, no Unity wind, no particle force fields. Low tier uses dot products, two nearest wake slots, and radial push. High tier uses cross-product vortex curvature and triangle waves for wash/silt churn. The visual overkill path spends GPU math only after CPU wake source count stays fixed.

Exact Microseconds saved: Low-tier flora path saves an estimated 8-22 us/frame versus 16-slot vortex math. MarineSnow triangle turbulence saves 3-10 us/frame versus 3D noise in wake bursts. Boid global wake steering saves 4-14 us/frame versus CPU overlap/fanout queries. Blackbox write remains 1-3 us/frame. High/Ultra deliberately spend roughly 6-18 us/frame of saved GPU budget on visible curvature, shimmer, silt churn, and fauna scatter.

Verification: XML prompt re-read from `Docs/Tasks/CURRENT_BATCH.md`, task count 18. Static scan for `distance(`, raw `normalize(`, Unity wind/force fields, and managed format strings over the wake shader/code slice returned no hits. Compute thread-group scan found only 64x1x1 or 1x1x1 groups, below Metal/Quest 1024 limits. `git diff --check` reported only existing line-ending warnings. `dotnet build .\Hecton8.Core.csproj -v:minimal -clp:ErrorsOnly` succeeded with 0 warnings and 0 errors.

## 2026-05-16 - Wake Trail Data Eviction and Latest Compile Wall

What was wrong: The wake trail stamp queue still used a private persistent `NativeArray<WakeTrailStampCommand>` in `FloraInteractionManager`. After removing that debt, the shared project compile state changed again because unrelated agents landed non-wake dependency breaks.

What was done: Converted `WakeTrailStampCommand` to explicit `Pack = 1` layout, added `BufferID.WakeTrailStampCommands`, and replaced the local persistent queue with a `VaultBufferHandle<WakeTrailStampCommand>` resolved through GlobalDataVault under `SystemID.Vfx`. Queue writes and GPU upload now use DataVault views, not local owned native storage.

Cinematic Cheats used: Four fixed wake-trail stamp commands remain the cap. The trail texture stays a cheap visual fake; no physical fluid path and no Unity wind/force components were introduced.

Exact Microseconds saved: 0-2 us/frame direct from removing private queue ownership. The meaningful win is memory accounting, ARM64/Quest explicit packing, and no hidden local wake data owner. Prior visual path estimates remain: 8-22 us/frame saved on low-tier flora, 3-10 us/frame saved on silt turbulence, 4-14 us/frame saved on boid wake steering versus CPU fanout.

Verification: `rg -n "_queuedWakeTrailStampCommands|new NativeArray<WakeTrailStampCommand>|StructLayout\(LayoutKind\.Sequential, Pack = 4, Size = 32\).*WakeTrail" Assets/_Project/Scripts/World/FloraInteractionManager.cs` returned no hits. `git diff --check` on wake trail/core memory files reported only line-ending warnings. Latest `dotnet build .\Hecton8.Core.csproj -v:minimal -clp:ErrorsOnly` exits 1 on non-wake owners: `ContentRuntimeServices`, `SargassumMicroFaunaBoids.cs` sensory fields, `LockstepStateValidator`, `EcosystemDirector`, and `SubmarineFluidDynamics`; sampled output has no wake file error.

## 2026-05-16 - Final Validation Reconciliation

What was wrong: The status and log still reported the earlier dependency wall, but the live project state now compiles. A first XML extraction command also used an exact opening-tag match and false-negatived because the active tag includes role/chat attributes.

What was done: Re-read the live `<AGENT_PROMPT id="INTERACTIVE_WAKE_VFX" ...>` block with an attribute-tolerant regex, reran `dotnet build .\Hecton8.Core.csproj -v:minimal -clp:ErrorsOnly`, and updated the wake status/rationale to the current verified state. No code patch was applied because the current compile wall no longer exists.

Cinematic Cheats used: Existing wake solution remains mathematical displacement, not fluid truth: 4-slot stress cap on toaster mode, 2 nearest radial shader pushes on low tier, 16-slot global wake buffer on high tier, vortex curvature/normal shimmer/marine-snow triangle turbulence/boid scatter for high and ultra.

Exact Microseconds saved: 0 us/frame from this reconciliation pass. Verified retained estimates: 8-22 us/frame saved on low-tier flora by avoiding 16-slot vortex math, 3-10 us/frame saved by triangle turbulence versus 3D noise for wake silt, and 4-14 us/frame saved by GPU/global-buffer boid steering versus CPU overlap fanout.

Verification: `dotnet build .\Hecton8.Core.csproj -v:minimal -clp:ErrorsOnly` succeeded with `0 Warning(s)` and `0 Error(s)` in 00:00:02.16. Wake-domain scan over `Assets/_Project/Scripts/VFX/Wakes` found no `Update`, `LateUpdate`, `FixedUpdate`, managed format strings, local `new NativeArray`, sequential `Pack = 4` wake structs, WindZone, ForceField, or particle force-over-lifetime use. Shader/compute scan found no `distance(` or raw `normalize(` in the wake slice. Compute group macros are 64 threads or `1x1x1`, under the Metal/Quest 1024 limit. `git diff --check` reports only LF/CRLF conversion warnings in existing modified core files.

## 2026-05-16 - Wake Hot-Path Fence and Compile-Wall Closure

What was wrong: The wake decay path required another job-fence audit after user-requested inquisition. The shared project also drifted through transient non-wake compile walls: `SubmarineFluidDynamics` DataVault wrapper binding and `LaserCutter` NativeQueue import state.

What was done: Kept wake decay out of `Tick` blocking: `SlowTick` schedules the Burst decay job, `Tick` only consumes already-complete results or skips one frame, `LateFrameTick` handles the swap-window completion path, and teardown/origin shift force only when lifecycle safety requires it. Moved `SubmarineFluidDynamics.VaultNativeBuffer<T>` before first use as a mechanical cross-domain compile repair. No persistent Core project include change was kept.

Cinematic Cheats used: No fluid solver, no Unity wind, no object force fields. Low tier remains 4 published wake slots and two nearest radial shader pushes. High/Ultra keep vortex curvature, normal shimmer, triangle silt turbulence, and wake-driven boid scatter.

Exact Microseconds saved: Estimated 10-80 us main-thread stall risk avoided by removing same-frame `Schedule().Complete()` from the wake tick path. Steady-state 16-slot wake decay remains estimated 0-3 us. Cross-domain compile repair saves 0 us/frame; it only restores build validation.

Verification: `rg -n "Schedule\([^\n]*\)\.Complete\(|\.Complete\(\)" Assets/_Project/Scripts/VFX/Wakes Assets/_Project/Scripts/World/FloraInteractionManager.cs` returned no hits. Wake-domain, transport, shader, and Metal/Quest thread-group scans passed. `dotnet build .\Hecton8.Core.csproj -v:minimal -clp:ErrorsOnly` succeeded with `0 Warning(s)` and `0 Error(s)` in 00:00:01.63. `git diff --check` on the wake/compile-wall touched files reported only LF/CRLF conversion warnings.

## 2026-05-16 - Current External Compile Wall After Live Churn

What was wrong: The shared workspace moved after the prior green build. A controlled single-node Core build now fails outside the wake slice with 12 errors in `GlobalSignals`, `FluidFeedbackListener`, `PlayerTool`, `PlayerToolManager`, `PlayerNoiseEmitter`, and `GameBootstrapper`.

What was done: Re-read the active XML and status/rationale files, completed mechanical validation-wall repairs already exposed by the build (`LaserCutter` now uses the canonical core cutter signal contract; fauna species target/tuning scratch lanes are DataVault arrays instead of half-converted hash maps), then stopped the compile chase when the wall moved to unrelated player-tool/physics/bootstrap owners. Updated status and rationale so the log no longer reports stale green validation as the current project state.

Cinematic Cheats used: Wake implementation unchanged: analytic 16-source global buffer, 4-slot stress cap, 2-nearest radial low-tier fake, high-tier vortex curvature, triangle silt turbulence, STP-stable vertex displacement, and wake-driven boid scatter. No Unity WindZone, ForceField, particle force-over-lifetime, or fluid solver path was added.

Exact Microseconds saved: 0 us/frame from the compile-wall bookkeeping. Wake estimates remain: 10-80 us main-thread stall risk avoided by removing hot-path wake job fences, 8-22 us/frame low-tier GPU saved by skipping full 16-slot vortex math, 3-10 us/frame saved by triangle turbulence versus 3D noise, and 4-14 us/frame saved by global-buffer boid wake steering versus CPU overlap fanout.

Verification: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly` exits 1 with 12 external errors. Targeted wake scans found no `WakeManager.Instance`, no legacy wake queue APIs, no wake hot-path `Schedule().Complete()`, no banned shader `distance()`/raw `normalize()`, and compute groups remain 64-wide or `1x1x1`. Status: wake slice verified, current Core build `[BLOCKED BY DEPENDENCY]`.
## 2026-05-16 - Current External Compile Wall After Live Churn

What was wrong:
- The active Core build is no longer green. The previous `SubmarineFluidDynamics` syntax wall is not the current blocker; the latest controlled build now stops in external UI/Ecosystem/Tether ownership.
- Current errors: 111 total, led by missing state/helper members in `DiegeticGyroCompassRuntime`, missing DataVault handles and generic inference failures in `EcosystemDirector`, and `HeavyTowWinch` calling removed `TetherManager.DrainTetherFiredSignals`.

What was done:
- Re-read `Docs/Tasks/CURRENT_BATCH.md` and extracted the full `INTERACTIVE_WAKE_VFX` XML block; task count remains 18.
- Re-ran wake-domain scans for managed Update loops, `string.Format`, local wake `NativeArray` ownership, sequential/Pack=4 wake layouts, Wind/Force components, legacy wake queues, wake singletons, shader `distance()`/raw `normalize()`, and compute thread group limits.
- Verified those scans still produce no wake-domain violations. Compute groups remain 64x1x1 or 1x1x1.
- Ran `dotnet build .\Hecton8.Core.csproj -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly`; result is `[BLOCKED BY DEPENDENCY]` in non-wake owners.

Cinematic Cheats used:
- Low tier still uses fixed-slot radial math and nearest-wake selection instead of fluid truth.
- High tier still spends only shader-side work on vortex curvature, normal shimmer, marine-snow turbulence, and boid scatter.

Exact Microseconds saved:
- 8-22 us/frame estimated low-tier GPU savings by avoiding full 16-slot vortex math.
- 3-8 us/frame estimated wake-signal savings by removing duplicate legacy wake queue transport.
- 10-80 us/frame estimated main-thread stall risk avoided by keeping wake decay completion out of `Tick`.
- 0 us/frame saved by the compile-wall classification itself; it is evidence handling, not runtime optimization.

## 2026-05-16 - Omega Reactive Silt Slot Polish

What was wrong:
- `Hecton_FluidAdvection.compute` consumed only 8 dynamic wake slots while the authoritative global wake contract is 16 slots.
- Result: high/ultra wake slots 8-15 could bend flora and affect boids, but silt/marine-snow turbulence ignored them.

What was done:
- Raised `HECTON_DYNAMIC_WAKE_CAPACITY` to 16.
- Added `HECTON_DYNAMIC_WAKE_LOW_TIER_CAPACITY` at 4 and enforced it inside `ApplyDynamicWakes` when `_DynamicWakeParams.y` marks low tier.
- Re-ran wake-domain scans and a controlled Core build.

Cinematic Cheats used:
- Low tier remains a 4-slot dot/radial visual fake.
- High/ultra spend shader-side ALU only; no fluid solver, no Unity WindZone, no ForceField, and no extra CPU wake owner.

Exact Microseconds saved:
- 0 us/frame low-tier change by design; the 4-slot cap is preserved.
- 2-6 us/frame estimated additional high/ultra GPU spend in wake-heavy scenes to buy denser silt turbulence.
- Existing savings remain: 8-22 us/frame low-tier GPU savings versus full 16-slot vortex math, 3-8 us/frame signal duplication avoided, and 10-80 us/frame main-thread fence risk avoided.

Verification:
- Wake scans found no shader `distance()`/raw `normalize()`, no managed Unity wind/force path, no legacy wake queue, no wake singleton, and no domain `Update`/`string.Format`/local wake allocation violation.
- Compute thread groups remain 64x1x1 or 1x1x1.
- `dotnet build .\Hecton8.Core.csproj -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly` succeeded with 0 warnings and 0 errors in 00:01:58.38.

## 2026-05-16 - Reactive Silt Global Wake Binding

What was wrong:
- `Hecton_FluidAdvection.compute` still declared separate `_DynamicWakes`, `_DynamicWakeVectors`, and `_DynamicWakeParams` inputs.
- `CarveDebrisComputeRenderer` bound those inputs to `_emptyFlowBuffer` and zero params, which could disable reactive silt/debris wake response while flora and boids used the real `_GlobalWakeBuffer`.

What was done:
- Switched fluid advection wake sampling to `_GlobalWakeBuffer`, `_GlobalWakeVectors`, and `_GlobalWakeParams`.
- Removed the empty dynamic wake buffer and zero-param bindings from `CarveDebrisComputeRenderer`.
- Renamed the compute helper to `ApplyGlobalWakes` so there is one visible wake authority.

Cinematic Cheats used:
- Low tier remains a 4-slot dot/radial/triangle fake from the global wake params.
- High/ultra keep the full 16-slot global wake wash for silt, bubbles, debris, flora, and boids without a second CPU wake owner.

Exact Microseconds saved:
- 0 us/frame low-tier cost change; the 4-slot cap is unchanged.
- 2-6 us/frame estimated high/ultra GPU spend is now connected to visible reactive silt/debris instead of an empty compute binding.
- Existing estimates still stand: 8-22 us/frame low-tier GPU saved versus full 16-slot vortex math, 3-8 us/frame signal duplication avoided, and 10-80 us/frame main-thread wake-decay fence risk avoided.

Verification:
- `rg -n "_DynamicWakes|_DynamicWakeVectors|_DynamicWakeParams|DynamicWake" Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs` returned no hits.
- Wake scans found no shader `distance()`/raw `normalize()`, no managed Unity wind/force path, no legacy wake queue, no wake singleton, and no domain `Update`/`string.Format`/local wake allocation violation.
- Compute thread groups remain 64x1x1 or 1x1x1.
- First controlled build after this patch was `[BLOCKED BY DEPENDENCY]` with 20 external errors in `TetherInstance` and `PhysicsApplySystem`.
- Latest controlled build after shared-workspace churn is `[BLOCKED BY DEPENDENCY]` with 86 external errors in `DiegeticGyroCompassRuntime`, `GlobalSignals`, and `ArchitectEyeVisualizer`. No error names `Hecton_FluidAdvection.compute`, `CarveDebrisComputeRenderer`, `FloraInteractionManager`, or wake symbols.
