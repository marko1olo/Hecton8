# Rationale 17-C

State: STATIC GATE CLEAN; AUDIO SYNTHESIS SOURCE/MCP GATE PASS; FLOCKING DRIFT CLOSED; ROSLYN PASS EXIT 0 BEFORE LATEST ATMOSPHERE/OCEAN/PREDATOR/AIRLOCK/PHYSIOLOGY/CORE_INPUT/MESOFAUNA/SOMATIC/AUDIO/FLOCKING PATCHES; FINAL ROSLYN BLOCKED BY CPU/DOTNET GUARD
Evidence class: STATIC_SOURCE until fresh compile/runtime/profiler artifacts exist.

## Session Boundary

Problem: The requested batch protocol names an XML prompt extraction flow, but `Docs/Tasks/CURRENT_BATCH.md` contains no `<AGENT_PROMPT id="17-C">`.
Solution: Treat the current chat request as the only operative assignment and record the extraction miss.
Rejected Alternatives: Reading archived batch prompts was rejected because AGENTS.md forbids stale batch context unless explicitly ordered.
Scalability potential: Static triage prevents cross-domain edits that would create compile debt across 20+ parallel agents.
Hardware Impact: Avoids speculative changes; estimated 0 us runtime change until source edits occur.

## Mandate Selection

Problem: The task targets hidden runtime C# math/allocation defects across first-party scripts.
Solution: Use Zero-GC, Native Memory/Jobs, ARM64 Struct Layout, i3 rsqrt/SIMD, CI math gate, Performance Budget, and Telemetry/Black Box mandates.
Rejected Alternatives: Broad design docs only were rejected because this task is code-level runtime surgery.
Scalability potential: Mandates force low, middle, high, ultra lanes through continuous quality/cadence rather than binary switches.
Hardware Impact: The scan targets i3/MX350 cost centers: heap pressure, scalar sqrt/normalize, cache-misaligned DTOs, and hidden job stalls.

## Drone Navigation Math Surgery

Problem: Drone batched jobs used scalar length routes in hot math: flow speed stress in cognition, drone velocity speed in metabolism, and A* line-clearance sample distance.
Solution: Replaced target sites with `lengthsq * math.rsqrt(math.max(lengthsq, epsilon))`, retaining squared early-outs for near-zero line segments.
Rejected Alternatives: Replacing battery drain with squared flow stress was rejected because it changes gameplay truth. Rewriting drone routing/SDF ownership was rejected because another agent already has active changes in the same files. Leaving tokens for a later global pass was rejected because this was a contained Burst job hot-path win.
Scalability potential: Low uses same truth with cheaper scalar form and no extra allocations. Middle keeps full route behavior. High/Ultra can spend saved budget in VISUAL_SYNC on richer drone lights, camera feed noise, and diagnostic overlays without changing command authority.
Hardware Impact: Static cost model: one sqrt/length removal per active drone cognition row, one per metabolism row, and one per A* clearance check. Estimated 0.01-0.06 us saved per 1k affected rows on i3/MX350-class CPUs; measured profiler proof pending.

## Verification Boundary

Problem: Build verification is required, but CPU guard forbids launching dotnet when system CPU is over 50%.
Solution: Checked guard three times. CPU was 67.7%, then 81.4%, then 92.5%; `csc.exe` count was 0. Build was not launched.
Rejected Alternatives: Running `dotnet build` under load was rejected because it violates the user's explicit compile discipline. Reporting compile success from static scans was rejected because `QUALITY_GATES.md` forbids it.
Scalability potential: Avoids stealing CPU from parallel agents and prevents false compile-wall attribution.
Hardware Impact: 0 us runtime; verification remains `[BLOCKED BY CPU GUARD]`.

## Roslyn Verification

Problem: Previous drone edit had static proof only.
Solution: After CPU dropped to 29.3% with `csc=0` and `dotnet=0`, ran `dotnet build .\Assembly-CSharp.csproj -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false --no-restore`.
Rejected Alternatives: Full Unity import/Play Mode claim was rejected because this was only local Roslyn syntax/assembly proof. Parallel build was rejected because shared CPU discipline matters with active agents.
Scalability potential: Confirms the math surgery did not add compile debt to the runtime assembly.
Hardware Impact: 0 us runtime; compile proof only. Result: exit 0, 3 reference warnings, 0 errors.

## Tools SDF Math Surgery

Problem: `LaserCutterDodJobs.cs` used scalar `math.length` in SDF payload bounds and laser-carve radial evaluation, plus a vector divide by `safeCell` in the SDF sampler.
Solution: Replaced the target length routes with finite `lengthsq * math.rsqrt(math.max(lengthSq, epsilon))` helpers and changed `(worldPosition - VolumeOrigin) / safeCell` to multiplication by `math.rcp(safeCell)`.
Rejected Alternatives: Changing SDF step count, cut depth, deformation DTO fields, or hit authority was rejected because tool truth must stay owned by the interaction/tool system. Rewriting `SafeNormalize` to fully branchless `math.select` was rejected because it would evaluate non-finite lanes before selection and had no local profiler proof.
Scalability potential: Low keeps the same cutter hit truth with cheaper math. Middle keeps existing sparks/heat/decal behavior. High/Ultra can spend saved cycles on richer GPU sparks, heat decals, slag, and cockpit/visor feedback without changing collision or carve authority.
Hardware Impact: Static cost model: removes two scalar length routes and one vector divide path from batched SDF probe/hit work. Estimated 0.02-0.08 us saved per 1k probe/hit rows on i3/MX350-class CPUs; measured profiler proof pending.

## Tools Verification Boundary

Problem: Loop 6 needs Roslyn verification, but compile launch is forbidden under high CPU or active compiler/dotnet contention.
Solution: Ran targeted static gate successfully, then sampled the guard six times. CPU was 98.1%, 92.0%, 98.4%, 100.0%, 100.0%, 100.0%; `csc=0`; `dotnet` count rose from 0 to 8/5/3 during later samples. Build was not launched.
Rejected Alternatives: Running `dotnet build` during 94.8%-100% CPU load was rejected. Marking this as a dependency compile wall was rejected because no compile attempt failed.
Scalability potential: Preserves host CPU for parallel agents and prevents false compile debt attribution.
Hardware Impact: 0 us runtime; verification remains `[BLOCKED BY CPU GUARD]` for Loop 6.

## Player ZeroG Movement Math Surgery

Problem: `ZeroGMovementJobs.cs` used scalar `math.length`/`math.sqrt` for thrust drain, brake effort, orientation angle, depenetration, collision impulse, and assertion/fuzzer magnitudes. It also used direct divisions in substep and propellant scaling.
Solution: Added a single shared `ZeroGMathGuards.LengthFromSq` helper using finite `lengthsq * math.rsqrt(max(lengthSq, epsilon))`; precomputed local thrust magnitude once per frame; replaced `dt / substepCount` and `propellant01 / requestedDrain` with reciprocal multiplication; converted collision/orientation/assertion magnitudes to squared routes.
Rejected Alternatives: Rewriting movement force semantics, changing AUP position ownership, changing telemetry ring layout, or touching DTO structs was rejected because player movement truth must stay predictable and authority-owned. Removing quaternion sanitization was rejected because it is a correctness guard, not a cosmetic cost.
Scalability potential: Low keeps readable zero-G movement with cheaper magnitude math. Middle keeps existing haptic/camera trauma facts. High/Ultra can spend saved cycles on richer suit haptics, visor wobble, cockpit feedback, and water-pressure presentation without changing movement truth.
Hardware Impact: Static cost model: removes repeated scalar magnitude and divide routes from a fixed-step player movement job and validation jobs. Estimated 0.03-0.12 us saved per 1k ZeroG substeps/assertion rows on i3/MX350-class CPUs; measured profiler proof pending.

## Player ZeroG Verification Boundary

Problem: Loop 7 needs Roslyn verification, but the machine is still above the build guard.
Solution: Ran targeted static gate successfully. Build guard showed CPU 77.0%, `csc=0`, `dotnet=2`; build was not launched.
Rejected Alternatives: Running `dotnet build` under 77% CPU and existing `dotnet` processes was rejected. Reporting compile success from static source was rejected.
Scalability potential: Prevents parallel-agent CPU contention and false compile-wall records.
Hardware Impact: 0 us runtime; verification remains `[BLOCKED BY CPU GUARD]` for Loop 7.

## Voxel A* Math Surgery

Problem: `VoxelAStarJobs.cs` used scalar length/divide routes in mock SDF generation, A* movement cost, weighted heuristic cost, line-of-sight sampling, and voxel coordinate mapping.
Solution: Extended existing `VoxelAStarConstants` with a finite `FastLengthFromSq` helper; replaced hot `math.length` routes with squared-distance `rsqrt`, replaced per-sample divisions with reciprocal multiplication, and moved A* telemetry stopwatch conversion to a cold static reciprocal.
Rejected Alternatives: Adding a new math utility class was rejected because the existing A* contract owner is the correct integration point. Changing heuristic truth, route statuses, result DTOs, or smoothing limits was rejected because it would alter AI authority rather than math cost.
Scalability potential: Low keeps the same route truth with cheaper neighbor expansion. Middle keeps current smoothing. High/Ultra can spend saved budget on larger request rings, richer debug overlays, or higher visual path presentation without changing gameplay ownership.
Hardware Impact: Static cost model: removes repeated scalar length and divide routes from node expansion and smoothing; estimated 0.04-0.16 us saved per 1k A* neighbor/smoothing samples on i3/MX350-class CPUs; profiler proof pending.

## Reactor Thermodynamics Math Surgery

Problem: `ReactorThermalGridJobs.cs` used scalar length/sqrt and direct divides in hot reactor heat injection and kernel weighting paths.
Solution: Added `ReactorThermalMath.FastLengthFromSq`, converted AUP cell mapping and kernel falloff to reciprocal multiply, removed a redundant velocity sqrt, reused heat-capacity reciprocal, and converted signal severity/water-capacity scaling to reciprocal math.
Rejected Alternatives: Changing heat ownership, damage emission cadence, radiation authority, thermal DTO layout, or signal bus route was rejected because those are domain contracts. Replacing the thermal model with a fake was rejected here because this job is the authoritative heat producer; fakes belong in presentation.
Scalability potential: Low uses the same thermal truth with cheaper math. Middle keeps existing heat shimmer. High/Ultra can spend saved cycles on VISUAL_SYNC thermal buffer density, stronger shimmer, and richer reactor warning presentation without adding simulation debt.
Hardware Impact: Static cost model: removes scalar magnitude/divide work from reactor heat rows and kernel injection cells; estimated 0.03-0.14 us saved per 1k reactor heat/kernel rows on i3/MX350-class CPUs; profiler proof pending.

## Auxiliary Equipment Math Surgery

Problem: `AuxiliaryEquipmentJobs.cs` still paid direct lifetime/radius divisions and a tether `sqrt` in routed auxiliary signal generation.
Solution: Reused existing `AuxiliaryEquipmentMath`, added `FastLengthFromSq`, converted battery/life/radius scaling to `math.rcp`, and capped tether distance squared before `rsqrt` so rest length remains bounded by authored max length.
Rejected Alternatives: Adding a separate math helper class, changing flare/sonar/tether signal DTOs, or editing cold router lifecycle was rejected because the existing contracts already own the math and layout.
Scalability potential: Low keeps auxiliary readability with cheaper signal rows. Middle keeps current flare/sonar/tether behavior. High/Ultra can spend saved budget on VISUAL_SYNC flare shimmer, sonar rings, cable/tether glow, and cockpit feedback.
Hardware Impact: Static cost model: removes scalar division/sqrt routes from auxiliary row generation; estimated 0.02-0.08 us per 1k route rows on i3/MX350-class CPUs; profiler proof pending.

## Ecosystem Spatial Grid Math Surgery

Problem: `ShinobuSpatialGridSolver.cs` used direct divisions in cell-radius/quantization helpers and a scalar `sqrt` for deterministic mock spatial disk spread.
Solution: Read ecosystem/world/terrain/creatures docs, kept spatial query truth unchanged, converted cell divisions to reciprocal multiplication, replaced `sqrt(u)` with finite `u * rsqrt(max(u, epsilon))`, and moved 24-bit reciprocal constants out of the job expression.
Rejected Alternatives: Changing bucket range DTOs, query ordering, forensic dump lifecycle, or spatial truth ownership was rejected. Removing the cold forensic `NativeArray<byte>` snapshot allocation was rejected because it is not a steady-state query/job allocation.
Scalability potential: Low keeps deterministic ecology/grid eligibility with cheaper query math. Middle keeps normal fauna/flora pressure. High/Ultra can spend saved budget on richer visible ecology density and debug overlays without changing spawn truth.
Hardware Impact: Static cost model: removes repeated scalar divide/sqrt work from query radius and mock entity generation; estimated 0.03-0.10 us per 1k spatial query/mock rows on i3/MX350-class CPUs; profiler proof pending.

## Procedural Bite IK Math Surgery

Problem: `ProceduralBiteIkJobs.cs` retained one scalar `math.length` in snap-miss recovery and duplicated distance reconstruction in jaw reach/mandible paths.
Solution: Added a private `LengthFromSq` helper inside the existing Burst job, replaced the remaining `math.length`, and routed existing distance calculations through the same finite `lengthsq * rsqrt` path.
Rejected Alternatives: Changing contact flags, miss recovery timing, telemetry DTOs, bone SOA layout, or adding a new IK utility was rejected because animation owns presentation only and must not mutate gameplay truth.
Scalability potential: Low keeps bite telegraph silhouette with cheaper IK math. Middle keeps current contact polish. High/Ultra can spend saved budget on richer tentacle wrap, audio jaw snap polish, and secondary creature presentation.
Hardware Impact: Static cost model: removes one scalar length route and deduplicates two distance routes; estimated 0.01-0.04 us per 1k bite IK solves on i3/MX350-class CPUs; profiler proof pending.

## Utility Cognition And Flocking Math Surgery

Problem: Utility AI and ecosystem flocking still reconstructed acoustic speed/radial distance with scalar `sqrt`/`length` routes after the broader AI pass.
Solution: Added `UtilityAICognitionJobMath.FastLengthFromSq`, reused it in cognition acoustic velocity, anxiety mock shelter radial SDF, and ecosystem flocking movement threat speed. The combined gate caught and closed a missed `FlockingAvoidance` `math.sqrt` site.
Rejected Alternatives: Adding an ecosystem-local duplicate helper, changing SignalBus snapshot ownership, or rewriting flocking threat budgets was rejected because the existing cognition math owner was sufficient and route truth must remain unchanged.
Scalability potential: Low keeps sensory threat readability with cheaper signal rows. Middle keeps current anxiety/flocking response. High/Ultra can spend saved budget on richer acoustic/flocking presentation without making AI omniscient.
Hardware Impact: Static cost model: removes scalar speed/radial reconstruction from cognition and flocking signal rows; estimated 0.02-0.07 us per 1k rows on i3/MX350-class CPUs; profiler proof pending.

## APEX Static Verification

Problem: The latest source changes require proof without violating the build throttle.
Solution: Combined source gate over all 17-C edited C# files found no `math.length`, `math.sqrt`, `GetComponent`, `GlobalRegistry.Get<T>`, `.Complete()`, or `WaitForCompletion`. Case-sensitive managed scan found only cold forensic `new NativeArray<byte>` snapshot storage in `ShinobuSpatialGridForensics`, not a steady-state `Execute/Tick/LateFrameTick` allocation. `git diff --check` had no whitespace errors. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Running Roslyn under latest `CPU=88`, `dotnet=1`, `csc=0` was rejected. Claiming runtime GCMonitor/profiler proof was rejected because no Unity runtime capture was executed.
Scalability potential: Static gates block drift before expensive runtime tests and avoid stealing CPU from parallel agents.
Hardware Impact: 0 us runtime; verification-only.

## Predator Cognition Steering And Acoustic Math Surgery

Problem: `PredatorCognitionDomain_Steering.cs` and `PredatorCognitionDomain.AcousticSdf.cs` retained scalar sqrt/length/normalize routes in mock SDF obstacle generation, lunge/current speed reconstruction, telemetry speed sampling, AUP double-distance clamp, movement acoustic intensity, constant mock acoustic axes, and inverse-square acoustic attenuation divides.
Solution: Extended the existing `PredatorCognitionDomain` owner only with shared finite `FastLengthFromSq` overloads. Replaced target sqrt/length/normalize routes with `lengthSq * rsqrt(max(lengthSq, epsilon))`, reused `NormalizeOrDominant` for mock acoustic axes, and converted protected attenuation divides to `math.rcp` multiplication.
Rejected Alternatives: Adding a new shared math utility was rejected because `PredatorCognitionDomain` already owns these job contracts. Changing predator DTO layouts, steering telemetry layout, acoustic SignalBus payloads, or DataVault alias protocol was rejected because the current pass is math-route surgery, not a vault ownership rewrite.
Scalability potential: Low keeps predator sensory and steering truth readable with cheaper row math. Middle keeps current threat pressure. High/Ultra can spend saved budget on stronger acoustic cue layering, richer leviathan lunge presentation, and denser high-tier fauna staging without making AI omniscient.
Hardware Impact: Static model: 0.03-0.12 us per 1k predator steering/acoustic rows on i3/MX350-class CPUs; profiler proof pending.

## Predator Verification Boundary

Problem: Latest predator edits need proof without violating build throttling.
Solution: Scoped predator source gates returned clean for sqrt/length/normalize/dependency/hot allocation tokens. Unity MCP `validate_script` returned 0 diagnostics for `PredatorCognitionDomain.cs` and `PredatorCognitionDomain_Steering.cs`. `PredatorCognitionDomain.AcousticSdf.cs` could not be validated by MCP because dot-named scripts are rejected by the validator. Fresh orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Running Roslyn under CPU 99.2% with `dotnet=2` was rejected. Claiming runtime GCMonitor/profiler proof was rejected because no Unity runtime capture was executed.
Scalability potential: Keeps predator source shape within math law while avoiding CPU contention with parallel agents.
Hardware Impact: 0 us runtime; verification-only.

## Audio Virtualization, Hull DSP, And GPR Math Surgery

Problem: `GroundRadarJobs.cs` used scalar `sqrt(rayCount)` to derive the 2D ray grid side; `AudioVirtualizationJobs.cs` used scalar `sqrt(u)` for deterministic mock emitter disk radius and a vector divide in SDF grid mapping; `HullStressGranularDspKernel.cs` used scalar `sqrt(meanSq)` for DSP RMS telemetry and repeated literal reciprocal decodes.
Solution: Extended existing owners only: `GroundRadarConstants`, `VirtualVoiceUtility`, and `HullStressGranularDspMath`. GPR now resolves the exact 1-64 ray grid thresholds with integer `math.select` steps. Audio mock radius and DSP RMS preserve their mathematical square-root result via finite `value * rsqrt(max(value, epsilon))`. SDF grid mapping uses reciprocal multiplication, and byte/10-bit decode constants are centralized.
Rejected Alternatives: Adding a new shared math utility was rejected because each domain already had a first-party math owner. Changing GPR scan shape, acoustic delay/doppler truth, DSP telemetry DTOs, or audio-thread callback signatures was rejected because those are contract surfaces.
Scalability potential: Low keeps GPR/audio truth cheap and deterministic. Middle keeps current scan/audio presentation. High/Ultra can spend saved budget on richer sonar/GPR visuals, stronger acoustic occlusion presentation, and denser hull stress grain polish without changing authority.
Hardware Impact: Static model: GPR 0.005-0.02 us per scan job; audio virtualization/DSP 0.02-0.09 us per 1k rows/frames on i3/MX350-class CPUs; profiler proof pending.

## Roslyn Pass And Final Guard

Problem: Loops after the first drone pass needed compile proof, but the build throttle forbids compile under CPU/dotnet contention.
Solution: When guard cleared at CPU 20.2%, `csc=0`, `dotnet=0`, ran one accumulated throttled build: `dotnet build .\Assembly-CSharp.csproj -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false --no-restore`. Result: exit 0, 851 existing/reference warnings, 0 errors. After that, the combined gate found a concurrent drift in `ShinobuEcosystemBalancer.FlockingAvoidance.cs`; the one-line `math.sqrt` route was re-patched to `UtilityAICognitionJobMath.FastLengthFromSq`.
Rejected Alternatives: A second immediate build was rejected because the latest guard returned CPU 100.0%, `csc=0`, `dotnet=2`. Claiming the post-drift re-patch had fresh Roslyn proof was rejected.
Scalability potential: One accumulated compile keeps verification real without choking parallel agents.
Hardware Impact: 0 us runtime; verification-only.

## PDA Cartography Math Surgery

Problem: `CartographyGridJobs.cs` still reconstructed sonar reveal row radius, fallback surface shell distance, and mock cluster shell distance with scalar `sqrt`. It also retained repeated macro-cell divides and literal hash normalization in hot cartography jobs.
Solution: Extended existing `CartographyGridConstants` and `CartographyGridMath`; added finite `FastLengthFromSq` overloads for `double` and `float`; replaced the three scalar sqrt routes with `lengthSq * rsqrt`; hoisted per-execute reciprocal cell-size scaling; centralized 24-bit hash normalization. DTO layouts, word/sector layout, reveal radius, shell logic, and PDA projector ownership were unchanged.
Rejected Alternatives: Replacing spherical sonar reveal with squared-threshold-only math was rejected because the code needs row X bounds and shell distance. Rewriting black-box dump allocation was rejected because the reported allocations are crash/fault writer paths, not steady-state `Execute/Tick/LateFrameTick`.
Scalability potential: Low keeps cartography reveal cheap and predictable. Middle keeps current PDA map behavior. High/Ultra can spend saved budget on denser cartography presentation, scan shimmer, and richer PDA projection without changing discovery truth.
Hardware Impact: Static model: 0.03-0.11 us per 1k reveal/mock rows on i3/MX350-class CPUs; profiler proof pending.

## Latest Verification Boundary

Problem: Latest cartography edit and repeated Flocking drift re-patch need final compile proof.
Solution: Combined source gate returned clean after re-patching Flocking. `git diff --check` had no whitespace errors; literal-path orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Running another build was rejected because guard returned CPU 100.0%, `csc=0`, `dotnet=9`.
Scalability potential: Avoids CPU contention with other agents and keeps proof boundaries honest.
Hardware Impact: 0 us runtime; verification-only.

## Atmosphere Surface/Storm/Toxic Math Surgery

Problem: Atmosphere runtime still had hot math debt: storm fog advection reconstructed surge magnitude through `math.length`; toxic outgassing mock world sampling used `math.length` for cave shell radius and protected scalar divides in diffusion/sampling; surface weather math/director used repeated protected divisions for thunder delay, blend factors, interference, km/h conversion, stopwatch scaling, and 24-bit random normalization.
Solution: Extended existing owners only. `ShinobuStormPropagationMath` now owns `FastLengthFromSq`; toxic runtime uses a private finite length helper and `math.rcp` protected denominators; surface weather math/director use reciprocal multiplication and named constants. No new manager, no new DTO, no new SignalBus lane, no DataVault route mutation.
Rejected Alternatives: Replacing storm/toxic fields with new physical simulation was rejected because atmosphere/water docs require visual-fake-first scalar fields. Editing cold `TryGetComponent` cache refreshes in `HectonSurfaceWeatherDirector` was rejected because the hot refresh path reads cached runtime context; the detected component lookups are cold/editor assignment paths.
Scalability potential: Low keeps cheap scalar storm/toxic/weather routes and readable danger cues. Middle keeps full current presentation. High/Ultra can spend saved budget on denser rain/silt/fog, richer toxic biolum, and stronger thunder/visor presentation without changing weather, gas, or survival truth.
Hardware Impact: Static model: storm 0.005-0.03 us per 1k publishes; toxic grid/sample rows 0.03-0.12 us per 1k rows; surface weather 0.005-0.04 us per 1k weather ticks/utility calls on i3/MX350-class CPUs. Profiler proof pending.

## ZeroG/Flocking Drift Closure

Problem: Combined 17-C gate found two leftover/drift defects after the atmosphere pass: `ZeroGMovementJobs.cs` still used `math.normalize` in quaternion sanitization, and `ShinobuEcosystemBalancer.FlockingAvoidance.cs` again reverted movement speed to `math.sqrt`.
Solution: Replaced quaternion normalization with explicit `float4 * rsqrt(lengthSq)` construction and re-applied flocking movement speed through `UtilityAICognitionJobMath.FastLengthFromSq`.
Rejected Alternatives: Trusting earlier gates was rejected because parallel-agent drift had already occurred multiple times.
Scalability potential: Keeps movement and ecology signal math deterministic in source shape; high-tier presentation can use saved budget for richer suit/flocking feedback.
Hardware Impact: Static source cleanup only; microsecond estimate already counted in ZeroG and cognition/flocking loops.

## Latest Verification Boundary 2

Problem: Latest atmosphere, ZeroG, and Flocking edits need compile proof.
Solution: Combined source gate over all 17-C edited C# files returned clean for `math.sqrt`, `math.length`, `math.normalize`, `Mathf.Sqrt`, `Vector3.Distance`, `.normalized`, `GlobalRegistry.Get<T>`, `WaitForCompletion`, and `.Complete()`. Managed scan on latest touched files returned no steady-state allocation hits. `git diff --check` had no whitespace errors; CRLF warnings only. Unity MCP `validate_script` returned 0 diagnostics for six latest files; it rejected `ShinobuEcosystemBalancer.FlockingAvoidance.cs` because the validator disallows dots in script names.
Rejected Alternatives: Running another build was rejected because the latest guard returned CPU 40.6%, `csc=0`, `dotnet=1`; the CPU lane cleared but the no-foreign-dotnet rule did not.
Scalability potential: Keeps static drift closed without stealing CPU from parallel agents.
Hardware Impact: 0 us runtime; verification-only.

## Ocean Surface Atmosphere Math Surgery

Problem: `ShinobuOceanSurfaceAtmosphereContracts.cs` and `ShinobuOceanSurfaceAtmosphereRuntime.cs` retained scalar sqrt/normalize and protected division routes in wave phase-speed generation, legacy/editor weather import, GPU readback normal reconstruction, wave contribution math, phase wrapping, and evaluation cadence quantization. Concurrent edits also reverted `ShinobuEcosystemBalancer.FlockingAvoidance.cs` to a movement-speed `math.sqrt`.
Solution: Extended the existing ocean owner only: `OceanSurfaceAtmosphereConstants` now owns gravity and reciprocal constants, while `HectonOceanSurfaceMath` owns `FastSqrtNonNegative` and `Normalize3OrDefault`. Phase-speed and readback normal reconstruction now use finite rsqrt-based helpers; reciprocal literals/divides were routed through constants or `math.rcp`; flocking movement speed was re-routed through existing `UtilityAICognitionJobMath.FastLengthFromSq`.
Rejected Alternatives: Adding a new shared math helper was rejected because ocean already has a first-party math owner. Changing wave count, readback capacity, DTO layout, DataVault handles, or shader buffer layout was rejected because that would mutate water/atmosphere authority instead of reducing math cost. Removing the `EnsureWaveReadbackData` allocation was rejected because it is a canonical cold buffer ensure, not a steady-state hot allocation.
Scalability potential: Low keeps ocean surface readable with cheaper wave/readback math. Middle keeps current swell and weather response. High/Ultra can spend saved cycles on richer surface shimmer, rain disturbance, silt/fog response, and visual overkill in presentation while wave truth and buffer layouts remain stable.
Hardware Impact: Static model: 0.01-0.06 us per 1k wave/readback evaluations on i3/MX350-class CPUs; profiler proof pending.

## Ocean Verification Boundary

Problem: Latest ocean and flocking edits need proof without violating the build throttle.
Solution: Targeted and combined source gates returned clean for `math.sqrt`, `math.length`, `math.normalize`, `GetComponent`, `GlobalRegistry.Get<T>`, `.Complete()`, and `WaitForCompletion` across the latest files and all 17-C edited files. Unity MCP `validate_script` returned 0 diagnostics for ocean contracts/runtime. Managed scan found only `EnsureWaveReadbackData` cold `NativeArray<float4>` allocation. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Running Roslyn was rejected because the final guard returned CPU 48.9%, `csc=0`, `dotnet=1`; the active `dotnet` process is Unity `VBCSCompiler.dll`, and killing it without an explicit request was rejected.
Scalability potential: Keeps source drift closed while avoiding contention with other active agents.
Hardware Impact: 0 us runtime; verification-only.

## Airlock Pressurization Math Surgery

Problem: `AirlockPressurizationMath` retained two protected scalar divisions in pressure equalization scalar math: normalized pressure delta and duration estimation.
Solution: Kept authority inside the existing `AirlockPressurizationMath` owner and replaced both divisions with `math.rcp(math.max(...))` multiplication. DTO layouts, DataVault descriptors, job scheduling, telemetry dump, and SignalBus outputs were not changed.
Rejected Alternatives: Changing `ApproximateSqrtPositive` was rejected because it already uses finite `rsqrt` and changing the near-zero branch could alter pressure timing. Adding a new math helper class was rejected because the existing contract file owns this math. Reworking airlock gameplay or locks was rejected because the current evidence supported only scalar route cleanup.
Scalability potential: Low keeps airlock pressure truth cheap and predictable. Middle keeps current survival pacing. High/Ultra can spend saved budget on richer door fog, pressure UI, water mist, and VISUAL_SYNC warning presentation without changing pressure truth.
Hardware Impact: Static model: 0.002-0.015 us per 1k equalization evaluations on i3/MX350-class CPUs; profiler proof pending.

## Airlock Verification Boundary

Problem: Latest airlock edit needs proof while respecting compile throttling and separating runtime code from editor scanner false positives.
Solution: Runtime-only source gate found no hot sqrt/length/normalize/dependency/completion/allocation tokens in non-editor `AirlockPressurization` files. Direct-division PCRE hits were XML comments or path strings, not code expressions. `UnsafeUtility.SizeOf<T>()` layout validation already covers every airlock DTO with 8-byte-multiple explicit sizes. Unity MCP `validate_script` returned 0 diagnostics for `AirlockPressurizationContracts.cs`. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Running Roslyn was rejected because guard returned CPU 43.3%, `csc=0`, `dotnet=1`; Unity owns the active `dotnet.exe`, and killing it would be unsafe. Treating editor `.ToString()` scanner hits as runtime GC debt was rejected because they are inside `Editor/AirlockPressurizationEditor.cs`.
Scalability potential: Keeps the survival pressure route source-clean without adding build contention for parallel agents.
Hardware Impact: 0 us runtime; verification-only.

## Physiology Sensory And Metabolism Math Surgery

Problem: Physiology runtime retained scalar magnitude and division debt in hot Burst paths: sensory drift telemetry used `math.length(move/look)`, metabolism mock thermal generation used `math.length(local - hotspot)`, and thermal/chemical grid sampling divided vectors by `cellSize`. Parallel drift also restored `math.sqrt(signal.VelocitySq)` in flocking threat capture.
Solution: Reused existing owners only. `ShinobuSensoryImpairmentJobMath` and `ShinobuMetabolismJobMath` now own finite `FastLengthFromSq` helpers. Sensory telemetry, metabolism hotspot falloff, and thermal/chemical grid scaling use `rsqrt`/`rcp` forms. Flocking threat speed again uses existing `UtilityAICognitionJobMath.FastLengthFromSq`.
Rejected Alternatives: Adding a new global math utility was rejected because each domain already has a first-party math owner. Changing survival formulas, DTO layouts, grid cell indexing semantics, DataVault lock routes, or SignalBus payloads was rejected. Replacing integer cell index divisions was rejected because those are coordinate decomposition, not floating-point scalar math debt.
Scalability potential: Low keeps physiology/survival clarity cheap and deterministic. Middle keeps current sensory drift and metabolism sampling. High/Ultra can spend saved cycles on richer visor impairment, thermal shimmer, contamination cues, and flocking presentation without changing survival truth.
Hardware Impact: Static model: 0.015-0.07 us per 1k physiology telemetry/metabolism sample rows on i3/MX350-class CPUs; profiler proof pending.

## Physiology Verification Boundary

Problem: Latest physiology/flocking edits need proof without violating build throttle and without overstating dot-named partial validation.
Solution: Scoped source gate over touched files returned clean for `math.sqrt`, `math.length`, `math.normalize`, hot `GetComponent`, hot `GlobalRegistry.Get<T>`, `.Complete()`, `WaitForCompletion`, LINQ, managed container, and hot string tokens. Direct division scan reported integer grid decomposition and a dump path string only. `git diff --check` returned CRLF warnings only. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`. Unity MCP `validate_script` returned 0 diagnostics for the three normal physiology filenames; `ShinobuEcosystemBalancer.FlockingAvoidance.cs` was rejected by MCP validator because the file name contains dots.
Rejected Alternatives: Running Roslyn was rejected because guard returned CPU 56.9%, `csc=0`, `dotnet=1`. Claiming runtime GCMonitor/profiler proof was rejected because no player runtime capture was executed.
Scalability potential: Keeps survival math source-clean while respecting parallel-agent CPU discipline.
Hardware Impact: 0 us runtime; verification-only.

## Core Input Deadzone And Look Math Surgery

Problem: `InputDispatcher` and `SteamDeckInputPal` retained scalar `math.sqrt` and protected direct divide routes in per-frame analog deadzone, mouse-look acceleration, viewport scaling, and Steam Deck trackpad radial filtering.
Solution: Kept the changes inside the existing input owners. Added private finite length-from-squared helpers using `rsqrt`; converted deadzone normalization, look scaling, and trackpad radial scaling to reciprocal multiplication. Action ids, profile DTOs, input buffer ownership, haptic queue shape, and device classification were not changed.
Rejected Alternatives: Rewriting the input dispatcher or adding a global math utility was rejected because the current defect is local scalar math debt. Changing deadzone semantics to squared-only curves was rejected because it would alter player feel. Touching existing haptic priority edits in the dirty file was rejected because they are outside this pass.
Scalability potential: Low keeps the same deterministic input snapshot with cheaper poll-boundary math. Middle keeps current Steam Deck/KBM feel. High/Ultra can spend saved budget on richer haptic layering and device diagnostics without changing gameplay input truth.
Hardware Impact: Static model: 0.006-0.025 us per 1k input poll/deadzone evaluations on i3/MX350-class CPUs; profiler proof pending.

## Core Input Verification Boundary

Problem: Latest Core input edits need proof without violating build throttling or claiming runtime GC proof.
Solution: Scoped source gate over `InputDispatcher.cs` and `SteamDeckInputPal.cs` returned clean for `math.sqrt`, `math.length`, `math.normalize`, hot `GetComponent`, hot `GlobalRegistry.Get<T>`, `.Complete()`, `WaitForCompletion`, LINQ, managed container, and hot string tokens. Direct division scan reported existing constants/docs/haptic/stopwatch paths outside the patched deadzone/look/trackpad route. Unity MCP `validate_script` returned 0 diagnostics for both files. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Running Roslyn was rejected because guard returned CPU 80.0%, `csc=0`, `dotnet=0`. Claiming GCMonitor proof was rejected because no Unity runtime capture was executed.
Scalability potential: Keeps input source shape clean while respecting parallel-agent CPU discipline.
Hardware Impact: 0 us runtime; verification-only.

## Mesofauna Behavior Math Surgery

Problem: `MesofaunaBehavioralStateMachine.cs` retained scalar `math.length`/`math.sqrt` routes in behavior continuity speed, intercept lead distance, and visual target distance. It also kept a vector divide in voxel obstacle probing, a direct divide in Bhaskara sine approximation, repeated literal reciprocals, and duplicate `lengthsq` work inside local direction normalization helpers.
Solution: Kept the change inside existing Mesofauna owners. `MesofaunaBehaviorConstants` now owns finite length reconstruction and reciprocal constants. Behavior speed, intercept, visual sync distance, voxel cell mapping, sine approximation, byte/32/3 decode paths, and both `ResolveDirection` helpers now use `rsqrt`/`rcp` or named reciprocals. DTO layout, state transitions, target selection, spatial hash ownership, and visual-sync schema were not changed.
Rejected Alternatives: Adding a shared math utility was rejected because the Mesofauna constants owner already exists. Squared-only distance outputs were rejected because visual distance and lead time need meter scalars. Changing creature sensory/aggression behavior was rejected because the source evidence supported math-route cleanup only.
Scalability potential: Low keeps mesofauna behavior readable and cheaper. Middle keeps current ecology pressure. High/Ultra can spend saved cycles on richer animation/audio telegraphs, denser local fauna staging, and visual overkill without changing AI truth.
Hardware Impact: Static model: 0.02-0.09 us per 1k mesofauna behavior rows on i3/MX350-class CPUs; profiler proof pending.

## Mesofauna Verification Boundary

Problem: Latest Mesofauna edits need proof without claiming runtime or compile success falsely.
Solution: Scoped source gates returned clean for `math.sqrt`, `math.length`, `math.normalize`, direct division, hot `GetComponent`, hot `GlobalRegistry.Get<T>`, `.Complete()`, `WaitForCompletion`, LINQ, managed container, and hot string tokens. Layout scan confirmed all Mesofauna explicit DTOs remain covered by `UnsafeUtility.SizeOf<T>()` checks. Unity MCP `validate_script` returned 0 diagnostics. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Running Roslyn after the guard changed was rejected; launch-time guard reported CPU 60.0%, `csc=0`, `dotnet=2`. Claiming GCMonitor proof was rejected because no Unity runtime capture was executed.
Scalability potential: Keeps fauna behavior source-clean while not choking parallel agents.
Hardware Impact: 0 us runtime; verification-only.

## Somatic Kinematics Math Surgery

Problem: `SomaticKinematicsRuntime.cs` retained scalar `math.length`/`math.sqrt` routes in mock cave SDF distance, pushout meters, hand stroke deltas, CCD speed, and stealth acoustic magnitude. Surface buoyancy also used protected direct divisions by blend distance.
Solution: Kept the changes inside the existing gameplay/kinematics owner. `MockWorldSampler` now owns finite length reconstruction for the local Burst-compatible route. Cave SDF, pushout, hand stroke, CCD, and acoustic magnitudes use `lengthsq * rsqrt`; surface breach/submersion use one reciprocal. Kinematic DTO layout, black-box layout, DataVault routes, service snapshot policy, and storage lifecycle were not changed.
Rejected Alternatives: Adding a global math utility was rejected because the owner file already had a local Burst-compatible struct. Squared-only haptic/acoustic outputs were rejected because these consumers need meter scalars. Editing cold `EnsureOnPlayerRoot`/persistent `NativeArray` storage was rejected because those are bootstrap/storage paths, not steady-state `Execute`.
Scalability potential: Low keeps player comfort and motion readability cheaper. Middle keeps current hand-stroke/thrust feel. High/Ultra can spend saved budget on richer haptics, visor shake, water resistance presentation, and comfort cues without changing movement truth.
Hardware Impact: Static model: 0.02-0.08 us per 1k somatic kinematic samples/CCD rows on i3/MX350-class CPUs; profiler proof pending.

## Somatic Verification Boundary

Problem: Latest Somatic edits need proof, but the file is large and build/Unity validation must not be overstated.
Solution: Scoped source gates returned clean for `math.sqrt`, `math.length`, `math.normalize`, direct division, hot `GlobalRegistry.Get<T>`, `.Complete()`, `WaitForCompletion`, LINQ, and hot string tokens. Managed allocation/lookup hits were storage ensure, cold bootstrap `TryGetComponent/AddComponent`, and black-box dump payload. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`.
Rejected Alternatives: Claiming Unity MCP validation was rejected because `validate_script` timed out in its regex engine. Running Roslyn was rejected because guard reported CPU 100.0%, `csc=0`, `dotnet=2`. Claiming GCMonitor proof was rejected because no Unity runtime capture was executed.
Scalability potential: Keeps player kinematic source debt lower without adding compile contention.
Hardware Impact: 0 us runtime; verification-only.

## Audio Synthesis DSP Math Surgery

Problem: Vocal decode and dynamic music synthesis retained protected scalar divisions and scalar square-root reconstruction in DSP-adjacent code: vocal interpolation, RMS telemetry, ducking alpha, PCM/ADPCM conversion, Dear Lie radio filter shaping, dynamic-music CSV float parsing, stopwatch conversion, hash normalization, depth/quality scaling, pitch increment, stinger decay, RMS telemetry, and biquad coefficient normalization.
Solution: Kept the changes inside existing owners only. `VocalBankConstants` now owns PCM/U16 reciprocal constants; `VocalDecodeKernel` reconstructs RMS via finite `value * rsqrt(max(value, epsilon))` and uses `math.rcp` for protected denominators. `DynamicMusicGranularSynthesizer` owns its reciprocal constants and finite RMS helper; dynamic music synthesis, mock tension, grain-bank setup, stinger decay, and biquad coefficient paths now use reciprocal/rsqrt forms.
Rejected Alternatives: Adding a global audio math helper was rejected because both files already own their contract/math surface. Changing vocal bank binary layout, synth DTO layout, raw output buffers, voice count, grain-bank shape, or audio phase ownership was rejected because the evidence supported math-route cleanup only. Replacing the existing multi-buffer `TryLockBuffer` audio job route was rejected in this pass because those pins protect raw pointers across a scheduled Burst job and need a dedicated DataVault proof route.
Scalability potential: Low keeps vocal decode and dynamic music cheap with identical authored truth. Middle keeps current dynamic-music tension and radio grit. High/Ultra can spend saved budget on denser grain layers, stronger radio coloration, richer stinger presentation, and more VISUAL_SYNC audio-reactive polish without changing audio authority.
Hardware Impact: Static model: 0.01-0.05 us per 1k vocal decode/music frames on i3/MX350-class CPUs. Profiler proof pending.

## Audio Verification Boundary

Problem: Latest audio edits need proof while respecting compile throttling and without hiding the DataVault lock-shape debt found during review.
Solution: Scoped source gates over `VocalBankContracts.cs` and `DynamicMusicGranularSynthesizer.cs` returned clean for banned hot sqrt/length/normalize, registry/component hot lookup, blocking completion, LINQ, string formatting, and `.ToString()` tokens. Managed scan hits were cold `TryGetComponent` routes or arithmetic addition, not steady-state allocation. Direct division hits were integer/block/indexing or ADPCM integer quantization. Unity MCP `validate_script` returned 0 diagnostics for both audio files. Orphan `.meta` scan returned `NO_ORPHAN_META_FOUND`; `git diff --check` returned CRLF warnings only.
Rejected Alternatives: Running Roslyn was rejected because guard reported CPU 91.0%, `csc=0`, `dotnet=1`. Claiming no thread ever holds more than one DataVault relocation pin was rejected because `TryLockSynthJobBuffers` already pins multiple buffers for a raw-pointer synth job. Claiming GCMonitor/runtime proof was rejected because no Unity runtime capture was executed.
Scalability potential: Keeps DSP source debt lower and records the exact lock-route boundary for the integrator instead of hiding it under a fake pass.
Hardware Impact: 0 us runtime; verification-only.

## Flocking Movement Threat Drift Closure

Problem: A fresh broad source scan after the audio pass found `ShinobuEcosystemBalancer.FlockingAvoidance.cs` had drifted back to `math.sqrt(signal.VelocitySq)` in movement acoustic threat capture. The same partial also exposed a literal reciprocal as `1f / 48f`, keeping the direct-division gate noisy.
Solution: Reused the existing first-party cognition math owner: movement velocity now flows through `UtilityAICognitionJobMath.SanitizeNonNegative` and `UtilityAICognitionJobMath.FastLengthFromSq`. The threat radius reciprocal is now an owner-local named constant, preserving the exact scale.
Rejected Alternatives: Adding an ecosystem-local helper was rejected because `UtilityAICognitionJobMath` already owns the route. Changing flocking threat intensity, radius semantics, SignalBus snapshot ownership, or black-box dump layout was rejected because this was a drift closure, not a behavior redesign.
Scalability potential: Low keeps flocking avoidance cheap and predictable. Middle keeps existing threat response. High/Ultra can spend saved cycles on richer flocking/audio presentation without changing AI truth.
Hardware Impact: Static model already counted in earlier cognition/flocking work; incremental closure estimate 0.002-0.01 us per 1k movement-threat captures on i3/MX350-class CPUs. Profiler proof pending.

## Flocking Verification Boundary

Problem: The dot-named partial needs proof without claiming unsupported Unity MCP validation.
Solution: Scoped source gate returned clean for banned sqrt/length/normalize, hot lookup, blocking completion, LINQ, managed allocation, and hot string tokens. Direct-division scan returned only the dump path string. `git diff --check` returned CRLF warnings only. Orphan `.meta` scan remains clean.
Rejected Alternatives: Calling `validate_script` was rejected because previous Unity MCP attempts reject dot-named script paths. Running Roslyn was rejected because the latest guard reported CPU 51.0%, `csc=0`, `dotnet=1`.
Scalability potential: Keeps the repeatedly drifting hot path closed and records why validation is source-only here.
Hardware Impact: 0 us runtime; verification-only.
