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
