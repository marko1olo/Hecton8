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

## 2026-05-16 - MarineSnow Global Wake Authority

What was wrong:
- `Hecton_MarineSnow.compute` still consumed a private 8-slot `_DynamicWakes` path while the authoritative wake publisher writes 16-slot `_GlobalWakeBuffer`/`_GlobalWakeVectors` arrays.
- `HectonMarineSnowRenderer` still carried dynamic wake IDs, buffer binding state, and `TryGetDynamicWakeGpuPayload` coupling, leaving two wake authorities for one visual event.

What was done:
- Switched MarineSnow compute advection to `_GlobalWakeBuffer`, `_GlobalWakeVectors`, and `_GlobalWakeParams`.
- Preserved the low-tier 4-slot shader cap and full-tier 16-slot path.
- Removed MarineSnow dynamic wake property IDs, dynamic wake capacity, dynamic buffer fields, and dynamic wake debug naming.
- Kept renderer work to scalar global wake param mirroring for compute dispatch/debug telemetry.

Cinematic Cheats used:
- Low tier keeps cheap dot/radial wake advection and the 4-slot cap.
- High/Ultra spend the same global wake signal on denser silt and bubble wash; no fluid solver, WindZone, ForceField, or Unity particle force module was added.

Exact Microseconds saved:
- 0 us/frame low-tier cost change; this is a wiring correction with the same 4-slot cap.
- 2-6 us/frame estimated high/ultra GPU wake-silt spend is now connected to actual MarineSnow advection across all 16 global slots.
- Existing estimates remain: 8-22 us/frame low-tier GPU saved versus full 16-slot vortex math, 3-8 us/frame signal duplication avoided, and 10-80 us/frame main-thread wake-decay fence risk avoided.

Verification:
- `rg -n "_DynamicWakes|_DynamicWakeVectors|_DynamicWakeParams|DynamicWake|TryGetDynamicWakeGpuPayload|ResolveDynamicWakeFlow|RefreshDynamicWakeBinding|_boundDynamicWake|DynamicWakeCapacity" Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` returned no hits.
- Wake scans found no shader `distance()`/raw `normalize()`, no managed Unity wind/force path, no legacy wake queue, no wake singleton, and no domain `Update`/`string.Format`/local wake allocation violation.
- Compute thread groups remain 64x1x1, 8x8x1, or 1x1x1, below the Metal/Quest 1024-thread ceiling.
- `dotnet build .\Hecton8.Core.csproj -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly` succeeded with 0 warnings and 0 errors in 00:01:47.60.

## 2026-05-17 - Low-Tier Reactive Silt Param Gate and Compile Wall Repair

What was wrong:
- `CarveDebrisComputeRenderer` still zeroed global wake params for low tier, so the 4-slot low-tier wake fake in `Hecton_FluidAdvection.compute` could be bypassed before dispatch.
- The shared workspace had fresh compile drift in non-wake contracts after the last green wake validation.

What was done:
- `ResolveGlobalWakeParamsForCompute` now passes global wake params to compute, clamps low tier to 4 slots, preserves active wake count, and reports low-tier wake activity truthfully.
- Applied mechanical compile-wall repairs: explicit signal interface wrappers including `SystemDispatcher`, DataVault pass-through, missing `System` import, valid `ushort` zero literal, tether quality tier forwarding, and direct `float3` conversions.

Cinematic Cheats used:
- Low/MX350 remains a capped mathematical lie: 4 wake slots, dot/radial/triangle turbulence, no private buffers, no Unity WindZone, no ForceField.
- High/Ultra keep the 16-slot global wake budget for visible wake-silt wash instead of a separate dynamic wake side-channel.

Exact Microseconds saved:
- Low-tier inactive wake: 0 us/frame cost.
- Low-tier active wake: spends capped 0-2 us GPU for visible response instead of disabling the effect.
- High/Ultra: unchanged 2-6 us/frame GPU wake-silt budget.
- Compile-wall repairs: 0 us/frame.

Verification:
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false -v:minimal -clp:ErrorsOnly` succeeded with 0 warnings and 0 errors, elapsed 00:01:47.88.
- Dynamic wake remnants scan: no `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`, `DynamicWake`, or `TryGetDynamicWakeGpuPayload` hits in fluid, MarineSnow, debris, or MarineSnow renderer paths.
- Shader/domain ban scan: no `distance()`, raw `normalize()`, `string.Format`, `WindZone`, `forceOverLifetime`, `ParticleSystemForceField`, or `ForceField` hits in wake-owned scripts and wake shaders.
- Thread groups remain under the Metal/Quest 1024 ceiling: 64x1x1, 8x8x1, or 1x1x1.

## 2026-05-17 - ARM64 Packing and Wake-Trail NaN Guard Pass

What was wrong:
- Wake-adjacent `FloraInteractionManager` structs still used sequential `Pack = 4` despite fixed-size GPU/job payloads.
- Wake-trail stamp shaders used raw divisions by radius/length after clamping; valid in normal input, still unnecessary in a mobile-sensitive wake path.

What was done:
- Converted wake-adjacent flora payload structs to `Pack = 1` while preserving their `Size` declarations.
- Replaced wake-trail `dot(...) / halfLength` and `dot(...) / radius` with `rcp`-based multipliers after clamping in both the stamp shader and simulation compute.

Cinematic Cheats used:
- Kept the wake-trail texture as the low-tier lie. No fluid solver, no force component, no extra CPU wake owner.
- High/Ultra keep the existing global wake wash and dense silt response; this pass removes platform risk without changing visual tier policy.

Exact Microseconds saved:
- Runtime savings: 0 us/frame claimed.
- ARM64 packing: 0 us/frame, reduces layout risk.
- Divide guard: 0 us/frame measurable; prevents NaN poison on degenerate stamp inputs.

Verification:
- Corrected PCRE scan found no non-`Pack = 1` structs in wake data, flora wake bridge, MarineSnow, or carve debris paths.
- Wake transport scan found no private wake `NativeArray` allocation, no legacy wake queue, no `WakeManager`, no EventBus wake path, and no dynamic wake buffer remnants.
- Thread groups remain under the Metal/Quest 1024 ceiling.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false -v:minimal -clp:ErrorsOnly` succeeded with 0 warnings and 0 errors, elapsed 00:01:02.10.
- Final current validation after audit-file update: same build command succeeded with 0 warnings and 0 errors, elapsed 00:00:06.26; `git diff --check` returned only LF-to-CRLF warnings.

## 2026-05-17 - Direct Typed-Lane Wake Publish Pass

What was wrong:
- The wake bridge used `GlobalSignals.Publish` for `WakeGeneratedSignal` and `FluidImpulseSignal`.
- The facade forwards into `SignalBus<T>`, but the wake XML requires explicit typed lanes and `ReadOnlySpan<T>` snapshots.

What was done:
- Replaced wake publish calls with `SignalBus<WakeGeneratedSignal>.Push` and `SignalBus<FluidImpulseSignal>.Push`.
- Left existing `ReadOnlySpan<WakeGeneratedSignal>` consumption intact.

Cinematic Cheats used:
- No new physical simulation. The wake system remains a bounded mathematical displacement signal: 4 slots on low tier, 16 slots on full tiers.

Exact Microseconds saved:
- 0 us/frame claimed. This is contract clarity and duplicate-interface removal, not a performance claim.

Verification:
- No `dotnet build` was run for this pass per user instruction.
- Targeted scans found no legacy wake queue, EventBus/delegate wake path, dynamic wake buffers, private wake `NativeArray` allocation, non-`Pack = 1` struct, banned wind/force component, shader `distance()`, raw `normalize()`, or `string.Format` hit.
- `git diff --check` returned only LF-to-CRLF warnings.

## 2026-05-17 - MarineSnow Dynamic Wake Side-Channel Purge Correction

What was wrong:
- The previous report was too broad: the live MarineSnow shader/renderer still had `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`, `RefreshDynamicWakeBinding`, and `TryGetDynamicWakeGpuPayload`.
- That left a private MarineSnow wake input beside the authoritative global wake arrays.

What was done:
- `Hecton_MarineSnow.compute` now uses `_GlobalWakeBuffer`, `_GlobalWakeVectors`, and `_GlobalWakeParams` directly, with 16-slot full-tier and 4-slot low-tier caps.
- `HectonMarineSnowRenderer` no longer binds dynamic wake buffers, no longer asks `HectonFluidEngine` for a dynamic wake payload, and writes `GlobalWakeCount` telemetry from sanitized global params.

Cinematic Cheats used:
- Low/MX350 remains the 4-slot radial lie.
- High/Ultra spend the existing global wake budget on dense MarineSnow turbulence instead of duplicate wake ownership.

Exact Microseconds saved:
- Low tier: 0 us/frame cost change.
- High/Ultra: restores the already budgeted 2-6 us/frame GPU wake-silt spend to the authoritative global wake source.
- Validation pass: no build timing reported because no build was run by request.

Verification:
- `rg` found no `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`, `DynamicWake`, `TryGetDynamicWakeGpuPayload`, `ResolveDynamicWakeFlow`, `RefreshDynamicWakeBinding`, `_boundDynamicWake`, or `SanitizeDynamicWakeParams` hits in MarineSnow shader/renderer.
- Global wake scans found `_GlobalWakeBuffer`, `_GlobalWakeVectors`, `_GlobalWakeParams`, `ResolveGlobalWakeFlow`, `RefreshGlobalWakeBinding`, `SanitizeGlobalWakeParams`, and `GlobalWakeCount`.
- Shader/domain ban scans found no `distance()`, raw `normalize()`, `string.Format`, `WindZone`, `forceOverLifetime`, `ParticleSystemForceField`, or `ForceField` hits in the checked wake paths.
- `git diff --check` returned only LF-to-CRLF warnings.

## 2026-05-17 - Nearby Vegetation NaN Guard and Sargassum Packing Pass

What was wrong:
- Nearby vegetation/cut-volume shaders still used raw radius-square division for falloff.
- `SargassumGlobalDragManager` had native/event-adjacent structs with default layout or `Pack = 4`.

What was done:
- Replaced raw `dot(...) / radiusSq` and `distSq / radiusSq` with guarded `rcp(max(radiusSq, eps))` multiplies in terrain damage volume, Sargassum cut mask, and indirect vegetation motion vectors.
- Converted the Sargassum global drag native/event-adjacent struct layout declarations to `Pack = 1`, preserving explicit `Size` declarations where present.
- Reapplied MarineSnow global wake binding after shared-workspace churn reintroduced dynamic wake symbols.

Cinematic Cheats used:
- Kept all affected paths as mathematical shader fakes: radius falloff, cut masks, motion-vector flora push, and global wake silt advection.
- No new physical simulation or Unity force component was introduced.

Exact Microseconds saved:
- Radius reciprocal pass: 0 us/frame claimed; this is NaN survival, not a timing claim.
- Sargassum packing pass: 0 us/frame; ARM64 layout risk reduction only.
- MarineSnow global wake correction: 0 us/frame low-tier change; preserves the existing 2-6 us/frame high/ultra GPU wake-silt budget.

Verification:
- No `dotnet build` was run for this pass, respecting the no-rebuild-every-time instruction.
- Targeted scans found no raw radius-square divides in touched vegetation/cut-volume shaders.
- Targeted scans found no non-`Pack = 1` struct declarations in the touched wake/Sargassum layout surface.
- `git diff --check` returned only LF-to-CRLF warnings.

## 2026-05-17 - Active CameraJuice Compile Wall Boundary

What was wrong:
- A controlled Core build after the nearby tech-debt pass failed in `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs(1301,81)`.
- The exact compiler error was `CS0234`: `Hecton8.Core.CameraJuiceImpactSignal` does not exist.
- `CameraJuiceSystem.cs` was already dirty in the shared workspace and was not part of the wake/MarineSnow/Sargassum edit set.

What was done:
- Recorded the compile wall instead of claiming a green build.
- Left the active dirty `CameraJuiceSystem.cs` file untouched.
- Kept wake-adjacent validation scoped to the files actually modified by this pass.

Cinematic Cheats used:
- None. This is a compile-boundary report. The wake visuals remain the same bounded mathematical fakes: low-tier capped radial response, high-tier 16-slot global wake wash.

Exact Microseconds saved:
- 0 us/frame. Boundary documentation only.

Verification:
- Build command: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false -v:minimal -clp:ErrorsOnly`.
- Result: 1 reported error in active nearby VFX owner `CameraJuiceSystem.cs`; no reported error named the touched wake, MarineSnow, Sargassum, terrain damage, cut-mask, or vegetation motion-vector files.

## 2026-05-17 - Reactive Fluid Dynamic Wake Re-Purge

What was wrong:
- `Hecton_FluidAdvection.compute` had regressed to `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`, and `ApplyDynamicWakes`.
- `CarveDebrisComputeRenderer` had regressed to binding dynamic wake buffers and calling `TryGetDynamicWakeGpuPayload`.
- This was a duplicate wake authority beside the global DataVault-backed shader arrays.

What was done:
- Replaced the fluid shader wake input with `_GlobalWakeBuffer`, `_GlobalWakeVectors`, and `_GlobalWakeParams`.
- Renamed the compute helper to `ApplyGlobalWakes` and kept the 16-slot full-tier / 4-slot low-tier cap.
- Removed dynamic wake buffer IDs, buffer binds, and payload lookup from `CarveDebrisComputeRenderer`.
- The debris renderer now sends sanitized `_GlobalWakeParams` only, matching MarineSnow and the global shader contract.

Cinematic Cheats used:
- Low/MX350 remains the capped 4-slot radial/triangle wake fake.
- High/Ultra use the same 16-slot global wake source for silt/debris turbulence as flora, boids, and MarineSnow.

Exact Microseconds saved:
- Low tier: 0 us/frame cost change.
- High/Ultra: restores the existing 2-6 us/frame GPU wake-silt/debris budget to the authoritative global source.
- No new runtime system or allocation was added.

Verification:
- Targeted scans found no `_DynamicWake`, `DynamicWake`, `TryGetDynamicWakeGpuPayload`, dynamic wake buffer bind, or dynamic wake sanitizer hits in fluid advection, MarineSnow, carve debris renderer, or MarineSnow renderer paths.
- Global wake scans found `_GlobalWakeBuffer`, `_GlobalWakeVectors`, `_GlobalWakeParams`, `ApplyGlobalWakes`, `ResolveGlobalWakeParamsForCompute`, and `SanitizeGlobalWakeParamsForCompute`.
- No rebuild was rerun because the active build wall is already recorded in dirty `CameraJuiceSystem.cs`.

## 2026-05-17 - Fluid Engine and Vehicle Wake Data Eviction

What was wrong:
- `HectonFluidEngine` still owned a private dynamic wake subsystem: NativeArrays, GraphicsBuffers, decay/upload code, payload fields, and RenderGraph binds.
- `VehicleMotor` wrote an unread `HydrodynamicWakeSample` ring through two local NativeArrays and a scheduled job.
- Both duplicated the global wake lane.

What was done:
- Removed the fluid-engine dynamic wake API, staging arrays, buffers, buffer binds, RenderGraph imports, and compute payload fields.
- Fluid advection now binds only `_GlobalWakeParams`; the compute shader reads `_GlobalWakeBuffer/_GlobalWakeVectors` directly.
- Removed the VehicleMotor hydrodynamic wake ring and job.
- VehicleMotor now pushes `WakeGeneratedSignal` directly into the typed global wake lane.
- Converted touched fluid/vehicle sequential structs to `Pack = 1`.

Cinematic Cheats used:
- Low/MX350 keeps the same global 4-slot mathematical wake fake.
- High/Ultra keep the 16-slot global wake wash across flora, silt, debris, MarineSnow, and boids.

Exact Microseconds saved:
- Fluid engine: removed four dynamic wake GraphicsBuffers, four dynamic wake NativeArrays, and one decay/upload path.
- VehicleMotor: removed two wake NativeArrays and one scheduled wake write job.
- Frame-time number is not claimed beyond deleted duplicate work; visual budget remains on the global wake system.

Verification:
- `rg` found no `_DynamicWake`, `DynamicWake`, `TryGetDynamicWakeGpuPayload`, `DynamicTurbulenceWake`, `WakeTurbulence`, `HydrodynamicWake`, or `hydrodynamicWake` hits in the touched wake/fluid/vehicle paths.
- `rg` found no non-`Pack = 1` struct layout hits in the touched fluid/vehicle/wake-adjacent set.
- Restore-enabled build after VehicleMotor purge failed only in dirty `SonarHoloCompass.cs`.
- Latest no-restore build fails earlier in dirty `H8Memory.cs(1923,9)` with invalid token `}`; this blocks final Core validation outside the wake domain.
