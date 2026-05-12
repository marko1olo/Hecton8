# Rationale_PHYSICS_FLUIDS

Status: PENDING VERIFICATION

## Initial Mandate Selection
Problem: Fluid prompt spans ocean currents, buoyancy, drag, flooding, event telemetry, AUP phase stability, and rsqrt replacement.
Solution: Loaded the smallest directly relevant mandate set: visual fake first, zero-GC, native jobs, physics determinism, interior fluid incursion, abyssal flow fields, AUP, and rsqrt.
Rejected Alternatives: Loading the entire 35+ file registry would waste context and blur domain ownership. Starting from code edits before mandates would violate the batch SOP.
Scalability potential: Low uses baked/cached 3D vector samples, dominant-axis approximations, bounded BFS, and no CPU noise. Middle raises sampling/entity counts. High enables richer hero normalization and shader-sync detail. Ultra can spend saved CPU on visual overkill through GPU-side flow and denser VFX, not CPU Navier-Stokes.
Hardware Impact: Expected benefit for i3/MX350 is removal of sqrt/CPU-noise hot-path debt and bounded work per frame; exact microseconds remain PENDING VERIFICATION until build/profiler proof.

## Decision 0 - File Identity
Problem: User named Agent Identity HYDRO_ENGINEER and Prompt ID PHYSICS_FLUIDS, while the XML block requires Status_PHYSICS_FLUIDS.md and Rationale_PHYSICS_FLUIDS.md.
Solution: Use PHYSICS_FLUIDS as the task/file ID and HYDRO_ENGINEER as role metadata.
Rejected Alternatives: Using HYDRO_ENGINEER for status/log files would contradict the XML completion contract.
Scalability potential: Correct file identity prevents integrator/chronicler lookup misses across many simultaneous agents.
Hardware Impact: No runtime impact; prevents process failure.

## Decision 1 - Tasks 1 and 3, Organic Current Without CPU Noise
Problem: Dominant-axis currents were cheap but visibly blocky; runtime CPU noise would violate the prompt and hot-path budget.
Solution: Keep the existing 32x32x32 prebaked curl `Texture3D` plus unmanaged `NativeArray<float3>` cache, sampled by masked AUP cell coordinates in `SamplePrebakedVectorCurrent`; modulate intensity with deterministic triangle wave.
Rejected Alternatives: Runtime Perlin/curl noise, Navier-Stokes, or per-object fluid displacement; all add invisible cause simulation instead of the needed visible flow cue.
Scalability potential: Low uses 2-cell masked samples and dominant axis. Middle uses full 32-cell lookup. High/Ultra keep full vector samples and spend saved CPU on denser VFX/GPU drift.
Hardware Impact: Estimated MX350/i3 gain versus CPU noise is 20-80 us per 100 fluid bodies; exact value remains PENDING VERIFICATION until Unity profiler capture.

## Decision 2 - Tasks 2, 4, and 5, Force Cheats Over Fluid Truth
Problem: Hero craft need stable normals and readable flow, but debris and propwash cannot afford exact physics.
Solution: Keep exact `math.normalize` only behind the hero/player exact-normal flag and high scalability tier; use dominant-axis debris fallback; propwash uses squared distance/dot cone; whirlpool now uses `math.cross(up, toCenter)` with `rsqrt` reciprocal length for tangential fake.
Rejected Alternatives: Universal exact normals, fluid displacement volumes, and `math.distance`/`sqrt` radius checks; these spend CPU where the player sees only direction/intensity.
Scalability potential: Low keeps dominant-axis and squared checks. Middle enables more sampled objects. High/Ultra keep smooth whirlpool tangent and stronger visual overkill while the gameplay proxy remains cheap.
Hardware Impact: Estimated low-end saving is 5-30 us per active fluid cluster versus normalized vectors and distance calls; PENDING VERIFICATION.

## Decision 3 - Loop 1 Compile Gate
Problem: Hydro edits introduced one namespace compile miss for `ImpactSignal`; initial build also showed unrelated `PlayerFootstepAudio` errors from another domain.
Solution: Added `Hecton8.Core.Signals` import to `SubmarineFluidDynamics` and reran `dotnet build Assembly-CSharp.csproj --no-restore`.
Rejected Alternatives: Editing player audio from the hydro prompt would violate domain boundaries; ignoring the hydro namespace error would fail the compile gate.
Scalability potential: Compile-clean hydro signal bridge lets downstream audio consume native impact events without direct subsystem coupling.
Hardware Impact: No runtime cost beyond existing `NativeQueue` publish; compile result after fix was 0 errors, 0 warnings.

## Decision 4 - Tasks 6 and 7, Bounded Drag and Deep Full-Buoyancy Fast Path
Problem: Drag can spike on low-end hardware and deep bodies were still allowed to pay wave-sample cost with only a shallow 0.5m margin.
Solution: Preserve capped quadratic drag using approximate speed and `ClampVectorMagnitude`; change CPU and GPU deep-submerged early-out to 5m below max trough envelope before Gerstner sampling.
Rejected Alternatives: Exact speed magnitude, uncapped drag, and wave sampling for bodies fully below the playable surface read.
Scalability potential: Low gets full-buoyancy early-out and capped drag. Middle/High can afford more wave-sampled near-surface bodies. Ultra spends saved time on richer surface visuals, not deeper CPU truth.
Hardware Impact: Estimated 8-25 us saved in heavy underwater object sets; worst-force spikes remain capped by mass. PENDING VERIFICATION.

## Decision 5 - Tasks 8 and 9, Thermocline and Interior Flood Bounds
Problem: Deep density changes and submarine flooding must affect gameplay but cannot become continuous fluid simulation.
Solution: Keep density multiplier default 1.5 behind halocline/deep-layer threshold and add constant Z shear force; keep `InteriorFloodBfsJob` hard-limited to 5 nodes/frame.
Rejected Alternatives: Particle fluid, per-compartment continuous solvers, and full graph flood traversal every frame.
Scalability potential: Low processes five flood nodes and simple Z shear. Middle raises authored graph quality without raising per-frame node cap. High/Ultra can add visual slosh/fog/audio overkill from the same bounded state.
Hardware Impact: Estimated savings versus full graph traversal are 15-60 us for compartment-heavy subs; PENDING VERIFICATION.

## Decision 6 - Task 10, AUP Tide Phase
Problem: Tide offset used local frame time, so origin/time resets could desync global sea level from AUP-celestial time.
Solution: Resolve tide triangle-wave phase from valid `CelestialRuntimeSnapshot.AbsoluteUniverseTime`, falling back to `GlobalRegistry.AbsoluteUniverseTime`, then local time only if the AUP clock is invalid.
Rejected Alternatives: `Time.time`, per-scene tide clocks, or sine orbit math.
Scalability potential: Same scalar triangle wave across all tiers; higher tiers can layer visual tide foam while physical level remains deterministic.
Hardware Impact: Runtime delta is negligible scalar math; determinism gain is process-level, not frame-time. PENDING VERIFICATION.

## Decision 7 - Task 11, Acoustic Splash Native Corridor
Problem: Water-entry detection existed as a local fluid impact queue, but downstream acoustic systems need decoupled `ImpactSignal` packets on the global native bus.
Solution: Convert job-produced `FluidImpactEvent` and submarine exterior splash events into `Hecton8.Core.Signals.ImpactSignal`, then publish through `GlobalSignals.Publish`.
Rejected Alternatives: Direct audio calls, string event names, managed delegates, or per-listener references from the fluid engine.
Scalability potential: Low drains one compact queue. Middle/High can attach richer audio/VFX listeners without adding dependencies to hydro jobs.
Hardware Impact: One unmanaged queue enqueue per impact; estimated under 2 us per splash burst on low-end silicon, PENDING VERIFICATION.

## Decision 8 - Tasks 12 and 13, Shared GPU Gerstner Constants
Problem: CPU buoyancy and GPU buoyancy must read the same analytical Gerstner parameters without scalar division debt in the shader.
Solution: Keep CPU parameters sourced from `WeatherRuntimeSnapshot.Wave0..2`, set matching compute shader constant names, and group GPU parameters under `HectonGpuBuoyancyConstants`; shader divisions use `rcp(max(...))`.
Rejected Alternatives: Duplicated shader-only wave authoring, sampled texture waves for physics, or raw `/` divisions in GPU hot code.
Scalability potential: Low may disable GPU buoyancy. Middle shares exact analytical constants. High/Ultra can dispatch more GPU samples while CPU still uses the same wave payload.
Hardware Impact: Reciprocal max saves small ALU latency; estimated 1-4 us per large GPU buoyancy dispatch, PENDING VERIFICATION.

## Decision 9 - Tasks 14 and 15, Cache and Viscosity LUT
Problem: `BuoyancyParams` lacked explicit stride proof and viscosity gradients must not evaluate dynamic curves in Burst hot paths.
Solution: Set `BuoyancyParams` explicit sequential size to 96 bytes, a 32-byte multiple, and keep the 16-sample persistent viscosity LUT with smoothstep values.
Rejected Alternatives: Runtime curve evaluation, arbitrary struct packing, and managed animation curves in the fluid job.
Scalability potential: Low gets aligned strides and 16 LUT reads. Middle/High can add more authored viscosity regions while keeping the sample cost bounded. Ultra can drive extra visual distortion from the same LUT state.
Hardware Impact: Estimated 2-8 us saved in viscosity-heavy regions and fewer cache-line stalls; PENDING VERIFICATION.

## Decision 10 - Tasks 16, 17, and 18, Numeric Reset Telemetry and Deterministic Splash Variance
Problem: Emergency resets and splash generation must not allocate strings or use nondeterministic `UnityEngine.Random`.
Solution: Keep reset telemetry on numeric `GlobalTelemetryBus.PublishPerformanceWarning` hashes; verify footprint fallback uses `safeValue * math.rsqrt`; add AUP/sample-index LCG hashing to splash energy modulation.
Rejected Alternatives: String-context logging, `math.sqrt(footprintArea)`, and random splash jitter.
Scalability potential: Low gets deterministic zero-GC event variance. Middle/High can map the same hash into richer VFX/audio variants. Ultra can spend visual budget without changing physical truth.
Hardware Impact: Estimated 1-6 us saved in reset/splash-heavy frames and zero managed allocation risk; PENDING VERIFICATION.

## Decision 11 - Tasks 19 and 20, Late-Swap Ownership and Compile Wall
Problem: Buoyancy jobs must not block fixed-step scheduling, and previous ACL extraction allegedly broke `HectonUnderwaterVisuals` interfaces.
Solution: Preserve early `FixedTick` scheduling and nonblocking `DispatcherJobSwap.TryComplete(..., false)` drain in `PostFixedTick`; verify `HectonUnderwaterVisuals` implements `Tick`, `SlowTick`, `LateFrameTick`, and `Render`.
Rejected Alternatives: Calling `.Complete()` immediately in `FixedTick`, direct Unity update loops, or editing unrelated compile-failing systems.
Scalability potential: Low skips a fixed step instead of blocking if a job overruns. Middle/High get same deterministic ownership with larger batches. Ultra can raise counts while completion stays in dispatcher swap windows.
Hardware Impact: Avoids worst-case main-thread stalls; estimated 50-200 us saved during fluid spikes on i3/MX350, PENDING VERIFICATION.

## Decision 12 - Final Compile Dependency Wall
Problem: A clean post-hydro `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` succeeded earlier, but a later build failed after unrelated concurrent edits in `GlobalSignals.cs`, `ConstructionManager.cs`, and `FaunaBrain.cs`.
Solution: Do not edit out-of-domain files. Mark omega compile as dependency-blocked while retaining the earlier hydro-clean compile evidence.
Rejected Alternatives: Fixing core event bus, construction origin-shift, or fauna proxy types from the hydro prompt would violate domain ownership and risk overwriting other agents.
Scalability potential: No runtime design change; preserves multi-agent isolation.
Hardware Impact: No runtime impact; integration compile wall must be handled by owning agents/integrator.

## OMEGA POLISH CHANGES
Problem: The polish mandate required an anti-bloat audit after all 20 tasks were checked or dependency-blocked. It also conflicted with the prompt by demanding `VERIFIED MASTER GRADE`; project law and the PHYSICS_FLUIDS XML require `PENDING VERIFICATION`, so status remains PENDING VERIFICATION.
Solution: Parsed `<POLISH_MANDATE id="OMEGA_POLISH">`, reran diff-level scans for `sqrt`, unconditional normalize, managed foreach/string formatting, `.ToString()`, and `UnityEngine.Random`, restored exact `math.normalize` only in the high-tier hero/player exact-normal branch, and kept debris/low-tier normals on dominant-axis fallback.
Rejected Alternatives: Marking verified despite external compile failures, deleting unrelated warnings, or replacing the explicitly required hero/player exact normal with a universal approximation.
Scalability potential: Low uses dominant-axis flow/normals, masked 3D vector lookup, capped drag, 5-node flood BFS, LCG splash variance, and nonblocking job drain. Middle keeps the same math but raises entity/sample budgets. High enables exact hero normals and full vector current samples. Ultra spends saved CPU/GPU budget on visual overkill from VFX/audio/shader presentation, not physical fluid truth.
Hardware Impact: Aggregate estimated gain for i3/MX350 is 120-430 us in worst hydro spikes versus honest fluid/noise/full-graph approaches; numbers are PENDING VERIFICATION until Unity profiler capture.

Honest calculations replaced with cinematic cheats:
- CPU fluid noise -> prebaked 32x32x32 curl vector lookup using AUP cell mask.
- Continuous current variation -> deterministic triangle-wave modulation.
- Propwash fluid displacement -> squared-distance/dot cone force.
- Whirlpool simulation -> `cross(up,toCenter)` tangent plus centripetal fake.
- Deep Gerstner sampling -> 5m submerged full-buoyancy early-out.
- Dynamic viscosity curve -> 16-sample LUT.
- Random splash variation -> AUP/sample-index LCG hash.
- Local tide clock -> AUP-synchronized triangle wave.

Zero-GC and math scan:
- Diff scan found no added `math.sqrt`, `Mathf.Sqrt`, `.normalized`, `Vector3.Distance`, managed `foreach`, string interpolation, `string.Format`, `.ToString()`, or `UnityEngine.Random`.
- Added `math.normalize` is conditional: high scalability tier plus `ExactSurfaceNormalFlag`, matching the player/hero requirement.
- Added `new` expressions are value types (`Vector3`, `double3`, `ImpactSignal`) and do not allocate managed heap memory.

Cache and locality:
- `BuoyancyParams` is explicitly `Size = 96`, a 32-byte multiple.
- Hot arrays remain `NativeArray`/`NativeQueue` and are accessed linearly by job index.
- Impact signaling uses existing native `GlobalSignals`, not direct subsystem references.

Silo and build health:
- Hydro-owned edits: `HectonFluidEngine.cs`, `SubmarineFluidDynamics.cs`, `Hecton_GpuBuoyancy.compute`.
- Cross-domain edit justified by task 10: `GlobalPhysicsStateManager.cs` AUP tide phase. Note: this file already contains concurrent non-hydro diffs; PHYSICS_FLUIDS only owns the tide synchronization change.
- No edits were made to `HectonUnderwaterVisuals.cs`; interface presence was verified by scan and earlier compile.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` succeeded earlier with 0 errors and 0 warnings after the hydro namespace fix.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` later failed outside hydro with 47 warnings and 11 errors: `HabitatGraphManager.cs` missing `TransitionHatchMeshState`, `ConstructionManager.cs` missing `Hecton8.Physics.SyncTransforms`, `SaveBinaryPayloadCodec.cs` bool/int mismatch, and `SaveBinaryStorage.cs` unsafe/MMF namespace errors.

Final Git Diff:
- Modified: `Assets/_Project/Art/Shaders/Hecton_GpuBuoyancy.compute`
- Modified: `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`
- Modified: `Assets/_Project/Scripts/HectonFluidEngine.cs`
- Modified: `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`
- Added: `Docs/Tasks/Status_PHYSICS_FLUIDS.md`
- Added: `Docs/AgentLogs/Rationale_PHYSICS_FLUIDS.md`
- Stat for tracked code files: 4 files changed, 261 insertions, 40 deletions.
