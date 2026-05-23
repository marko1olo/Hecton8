# Rationale SHINOBU_337

Status: PENDING COMPILE - DOTNET BUSY
Evidence class: STATIC_SOURCE until Unity/Burst/Profiler proof exists.

## Bootstrap Decisions

Problem: Reactor heat integration crosses Vehicles, Thermodynamics, Combat, VFX, and Core authority surfaces.
Solution: Route gameplay truth through Vault-backed unmanaged DTO buffers and typed SignalBus payloads; keep registry lookup cold only.
Rejected Alternatives: Unity trigger volumes, managed `List<HeatSource>`, direct sibling-domain concrete references, and hot `GlobalRegistry` polling. Standard Unity trigger callbacks sync Transforms with PhysX broadphase and allocate/fragment authority through component identity.
Scalability potential: Low uses one central voxel and later diffusion; Middle uses partial local cluster; High and Ultra expand spatial injection and richer visual heat signals without changing DTO truth.
Hardware Impact: Estimated low-end i3/MX350 gain is removal of PhysX trigger broadphase participation and managed callback overhead, target sub-0.1 ms static budget. Measured proof absent.

Problem: ARM64 and Steam Deck runtime structs cannot tolerate hidden padding or properties that force defensive copies.
Solution: Primary runtime DTOs will use unmanaged fields only, explicit layout where mandated, and editor validation via `UnsafeUtility.SizeOf` / `UnsafeUtility.GetFieldOffset` equivalent checks.
Rejected Alternatives: Auto-properties, `LayoutKind.Sequential` for mandated reactor DTO, runtime `bool`, `Pack=1`. Standard C# encapsulation is rejected for hot Burst DTOs because it hides copy semantics and layout.
Scalability potential: Low through Ultra share the same truth layout; visual overkill rides side-channel signals, not bloated gameplay DTOs.
Hardware Impact: 32-byte ReactorStateDTO keeps two reactors per 64-byte cache line; expected fewer L1 misses on i3/MX350 and no unaligned ARM64 traps. Measured proof absent.

## Loop 1 Decisions

Problem: Vehicles/Environment scan roots contain no `SubmarineEngineHeat.cs`, `Heater.cs`, `ThermalVolume`, `OnTriggerStay` thermal trigger, or `List<HeatSource>` authority route.
Solution: Leave nonexistent/deleted legacy scripts untouched and write the proof into `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_337.json`; route all reactor heat into existing `AbyssalThermalCellInjection` instead.
Rejected Alternatives: Creating a replacement `ThermalVolume` facade or deleting unrelated atmosphere/reactor authoring code outside the requested roots. That would create cross-domain damage without evidence.
Scalability potential: Low uses one central weighted voxel; Middle keeps partial neighbor weights; High and Ultra expand toward 3x3x3 injection and visual shader scalar without changing gameplay truth.
Hardware Impact: Removing trigger broadphase participation avoids Transform-to-PhysX sync cost; static estimate 6-18 us saved per active submarine on i3/MX350. Measured proof absent.

Problem: Reactor state ABI must satisfy prompt offsets exactly and survive ARM64 alignment.
Solution: `ReactorStateDTO` is explicit size 32, raw fields only, with offsets 0/4/8/12/16 and private uint padding at 20/24/28; `ReactorThermalLayoutValidator` checks size and offsets at boot.
Rejected Alternatives: Sequential structs, auto-properties, `Pack=1`, `bool`, and managed class wrappers. They increase defensive-copy and alignment risk.
Scalability potential: The same 32-byte truth row scales from one mock reactor to a fleet; visual overkill remains in signals/shaders.
Hardware Impact: Two reactor rows per cache line; estimated low-end gain 2-5 us across 16 reactors vs property/class traversal. Measured proof absent.

Problem: There is no guaranteed live submarine reactor buffer in isolation.
Solution: `GenerateMockReactorLoadJob` overwrites Vault-backed reactor and kinematic rows with deterministic oscillating synthetic loads when reactor count is zero.
Rejected Alternatives: Waiting for player-built vehicle state, using MonoBehaviour test emitters, or spawning prefabs. Those block math verification and add scene-coupled dependencies.
Scalability potential: Low still tests one central cell; Ultra tests full 3x3x3 heat spread by tuning only.
Hardware Impact: Mock generation is `IJobParallelFor` over 16 rows, static target below 5 us on i3/MX350. Measured proof absent.

## Loop 2 Decisions

Problem: Reactor heat must enter the existing 3D thermal grid without a second thermodynamics owner.
Solution: `InjectReactorHeatJob` writes atomic float deltas into `AbyssalThermalCellInjection` before the existing diffusion passes; no new grid ownership exists.
Rejected Alternatives: Creating a standalone reactor thermal volume or mutating `ThermalCellDTO` front buffer directly. Direct front writes would race diffusion ownership and bypass solver relaxation.
Scalability potential: Low writes one central voxel; Middle/High/Ultra expand atomic writes through the same kernel and let visual scalar overkill ride shader globals.
Hardware Impact: Static estimate is 1-27 atomic float CAS writes per reactor depending on quality; low-end i3/MX350 path avoids 26 atomics per reactor. Measured proof absent.

Problem: Reactor core temperature has to obey energy conservation while still overheating if water cannot absorb heat.
Solution: Power output raises core joules first, forced-convection cooling removes joules against the local water temperature, and only removed joules are injected into the grid.
Rejected Alternatives: Injecting target MW directly into water without changing core temperature, or subtracting a fixed Celsius value. Both break conservation and hide lava/hot-water failure.
Scalability potential: Low through Ultra share identical gameplay truth; only spatial distribution and visual side channels scale.
Hardware Impact: Scalar math per reactor is bounded and branch-light; expected under 10 us for 16 reactors on i3/MX350. Measured proof absent.

Problem: Meltdown and heat shimmer cannot instantiate prefabs or geometry from the hot path.
Solution: The Burst job enqueues unmanaged `ThermalStateChangedSignal` and `CombatDamageSignal`; visual sync uploads hottest reactor cell/core scalar and runtime point to shader globals.
Rejected Alternatives: `Instantiate`, `Destroy`, particle heat haze, or direct combat component calls. Those allocate and couple domains.
Scalability potential: Low can consume only scalar shimmer; Ultra can use the same shader point for heavier post-process distortion without changing simulation DTOs.
Hardware Impact: Signal writes are bounded by cadence and reactor count; shader globals replace CPU geometry work. Measured proof absent.

## Loop 3 Decisions

Problem: Forced convection needs to be predictable and cheap.
Solution: The injection job computes `SpeedSq = math.lengthsq(Velocity)` and scales cooling by `1 + SpeedSq * ForcedConvectionMultiplier`, clamped by tuning.
Rejected Alternatives: fluid sampling, raycasts, wake particles, or Navier-Stokes approximations. Those are too slow and non-authoritative for reactor truth.
Scalability potential: Low uses same polynomial but small injection volume; Ultra spends saved cycles on visual shader distortion, not gameplay divergence.
Hardware Impact: Three multiply-add operations replace fluid simulation; static estimate 10-40 us saved per moving submarine on i3/MX350. Measured proof absent.

Problem: AUP precision must survive 100km coordinates without casting absolute positions to float.
Solution: `TryMapAupToCell` subtracts `GridOriginAup` from reactor `double3` first, then casts the local delta to `float3` for bounds/index math.
Rejected Alternatives: absolute float coordinates, Transform position, or Unity physics trigger position. Those drift at map edges.
Scalability potential: Same precision path for Low through Ultra; only kernel radius changes.
Hardware Impact: Double subtraction cost is fixed and cheaper than correcting wrong-cell diffusion artifacts. Measured proof absent.

Problem: Postmortem needs a deterministic 300-frame Black Box without heap allocations in the hot path.
Solution: `ReactorTelemetryRecorderJob` writes a 300-entry Vault ring, hot reactor AUP, hot reactor/entity hashes, state hash, joules, active count, signal count, flags, and a timing proxy; fault dump writes `Dump_SHINOBU_337.bin` via `ReadOnlySpan<byte>`.
Rejected Alternatives: managed log strings, `List<T>` histories, Unity console spam, or hash-only telemetry without the hot AUP. They allocate or lose the position needed for forensic reconstruction.
Scalability potential: Low through Ultra record the same 128-byte truth row; optional visual overkill consumes shader scalars, not a different telemetry ABI.
Hardware Impact: 38,400-byte Black Box total; constant memory footprint and one sequential telemetry job. Measured proof absent.

## Loop 4 Decisions

Problem: Designers need live balancing without recompiling reactor math.
Solution: `SubmarineThermodynamicsTunerWindow` uses UI Toolkit sliders and writes sanitized `ReactorThermalTuningDTO` values through the Vault lock using `UnsafeUtility.AsRef`.
Rejected Alternatives: serialized MonoBehaviour knobs, inspector-only floats, or rebuilding constants. Those desync the Vault truth and force recompiles.
Scalability potential: Low/Middle/High/Ultra all tune the same DTO; visual overkill remains a consumer of shader scalars.
Hardware Impact: Editor-only; runtime hot path unchanged. Measured proof absent.

Problem: Reactor profiles must be cold data, not managed parser state.
Solution: `ReactorThermalProfileCsvParser` slices `ReadOnlySpan<byte>`, hashes names with deterministic FNV-1a, and parses floats manually into Vault profile rows.
Rejected Alternatives: `float.Parse`, LINQ CSV libraries, or ScriptableObject lookups in the hot path. Those allocate and add localization/runtime dependency risk.
Scalability potential: Low can use the default profile; Ultra can load more reactor profile rows without changing jobs.
Hardware Impact: Parser runs only during editor cold boot; runtime frame impact 0 us. Measured proof absent.

Problem: Debugging wrong-cell heat injection must be visible without logs.
Solution: `ReactorThermalDebugGizmo` reads Vault reactor rows and draws colored reactor spheres, voxel wireframes, and red injection dots in SceneView under `UNITY_EDITOR`.
Rejected Alternatives: runtime gizmo MonoBehaviours, string labels, or Debug.Log spam. Those allocate and pollute gameplay.
Scalability potential: Low debug shows one dot; Ultra debug can inspect 3x3x3 spread via existing cell/heat data.
Hardware Impact: Editor-only; runtime hot path unchanged. Measured proof absent.

## Loop 6 Ultra Polish Decisions

Problem: The initial SHINOBU_337 `BufferID` range ended at `71820`, which collides with the documented SHINOBU_264 async buoyancy ownership `71820..71831`.
Solution: Move every SHINOBU_337 reactor lane to exact-search-clean `73620..73630` and record `71810..71820` as a rejected candidate range.
Rejected Alternatives: Keeping the old range because only one endpoint collided, or using local numeric casts outside the central enum. Numeric Vault identity aliases by integer key; type names do not protect the buffer.
Scalability potential: Low through Ultra now read the same non-colliding Vault lanes; no gameplay or visual scaling behavior changes.
Hardware Impact: Prevents cross-domain Vault corruption. No microsecond performance gain is claimed.

Problem: `AbyssalThermodynamicsSolver.ReactorBridge` directly imported `Hecton8.Physics.Vehicles.SubmarineKinematicState`, creating sibling runtime coupling pressure.
Solution: Remove the concrete vehicle type path. The reactor injector now consumes only `ReactorKinematicStateDTO` from `BufferID.Shinobu337ReactorKinematics`; vehicle owners can publish into that DTO route without Thermodynamics referencing their runtime assembly.
Rejected Alternatives: Adding `Hecton8.Physics.Vehicles` as an asmdef reference, probing `BufferID.SubmarineKinematicStates`, or defining an interface array. Those increase compile-wall blast radius and violate the Global Authority boundary.
Scalability potential: Low through Ultra keep identical kinematic truth shape; only producer cadence/quality can vary outside this consumer.
Hardware Impact: Runtime ALU unchanged; iteration-time risk reduced by deleting sibling concrete dependency.

Problem: Mock reactor load did not stress the promised overheating path strongly enough.
Solution: Replace the mild sine around 720 C with a deterministic smooth triangular overload ramp from authored base temp toward 2000 C plus bounded oscillation.
Rejected Alternatives: `UnityEngine.Random`, scene prefabs, or live Cyclops dependence. Those would either desync rollback or delay isolated math validation.
Scalability potential: Low validates central-cell injection under critical heat; Ultra validates 3x3x3 spread under the same worst-case thermal load.
Hardware Impact: Mock-only fallback path; target remains below the static 5 us expectation for 16 rows. Measured proof absent.

Problem: The previous report implied exact Burst execution timing, but the code only measured schedule-call overhead without blocking on job completion.
Solution: Keep the non-blocking timing proxy and mark it with `TelemetryFlagTimingProxy`; do not claim exact Burst wall time until profiler/Burst instrumentation exists.
Rejected Alternatives: Calling `.Complete()` to measure exact duration in the frame, or reporting the proxy as exact. Both violate the dispatcher/job discipline.
Scalability potential: Low through Ultra preserve job parallelism; richer timing proof can be added through profiler capture without changing gameplay DTOs.
Hardware Impact: Avoids a main-thread synchronization stall. Exact measured microseconds remain pending.

Problem: Debug readback returned mutable `NativeArray` aliases from a `TryGet*` accessor.
Solution: Change the editor/debug facade to return `NativeArray<T>.ReadOnly` views and keep mutation routes confined to explicit write APIs.
Rejected Alternatives: Letting editor gizmos hold mutable Vault views, or copying every row into managed arrays. Mutable readback breaks accessor purity; managed copies allocate.
Scalability potential: Editor-only; runtime Low through Ultra unaffected.
Hardware Impact: Runtime 0 us; editor safety improved.

Problem: The shared physics optimization report carried only the initial minimal SHINOBU_337 scanner entry and did not expose the repaired Vault range or timing caveat.
Solution: Update `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` with `73620..73630`, rejected `71810..71820`, compile-wall isolation, blackbox route, and `TelemetryFlagTimingProxy` disclosure.
Rejected Alternatives: Leaving proof only in the sidecar report and self-audit. Integrators read the shared report first; stale shared metadata would misroute follow-up work.
Scalability potential: No runtime scaling behavior changes; this is proof-surface alignment for Low through Ultra.
Hardware Impact: Runtime 0 us; reduces integration risk.

Problem: Compile verification is required, but the generated project files do not include the new SHINOBU_337 Thermodynamics sources and the legal build guard sampled CPU above 50%.
Solution: Do not launch `dotnet build` while CPU is 63.3%. Record that `Hecton8.Core.csproj` would only cover the `H8Memory.cs` BufferID edit because the Thermodynamics asmdef project has not been regenerated by Unity import.
Rejected Alternatives: Running a useless Core-only build under CPU pressure and reporting it as full reactor compile proof. That would violate the build discipline and generate false evidence.
Scalability potential: Runtime unchanged; verification waits for legal CPU window and Unity project regeneration.
Hardware Impact: Prevents local machine saturation during parallel agent work.

Problem: The first quality curve rounded `math.lerp(1,3,q)` to diameter 2 and then shifted it into `halfExtent=1`, so quality around 0.3 could already execute the full 27-cell cube.
Solution: Resolve diameter by `math.step(0.30, q)` and gate diagonal/corner shell weight by `math.smoothstep(0.55, 0.95, q)`. Below 0.30 only the center cell exists; mid quality writes center plus six axial neighbors; high/ultra smoothly admits the 20 diagonal/corner cells.
Rejected Alternatives: Keeping the binary 1-or-27 atomic jump, or adding hardware-tier branches. The fixed route is quality-driven and keeps gameplay truth unchanged.
Scalability potential: Low = 1 write, Middle = up to 7 writes, High/Ultra = up to 27 weighted writes plus richer shader scalar use.
Hardware Impact: Mid-tier avoids up to 20 atomic CAS pairs per reactor versus the premature full-cube path. Static estimate only.

Problem: `TryReadReactorTelemetry` could return a telemetry row while the scheduled recorder job was still writing the ring.
Solution: Add `_hasPendingJob` as a read fence, matching the existing debug readback fence.
Rejected Alternatives: Completing the job inside the accessor or accepting a race for editor graphs. `.Complete()` in a getter violates dispatcher ownership; racing reads produce false forensic evidence.
Scalability potential: No Low/Ultra behavior change; editor/debug consumers wait for the owner completion window.
Hardware Impact: Runtime hot path unchanged.

Problem: Post-patch compile verification became legal by CPU percentage but not by process contention.
Solution: Refuse build while seven active `dotnet` processes exist, even though CPU sampled 30.9%.
Rejected Alternatives: Starting an additional build under compiler contention. This violates the explicit rebuild protection rule and hides whether failures belong to this lane or another active agent.
Scalability potential: Runtime unchanged.
Hardware Impact: Protects workstation IO/CPU during parallel agent work.

Problem: Global doctrine requires Data Monolith readiness proof, but `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent in this workspace.
Solution: Fence the claim in the binary payload ledger, shared/dedicated physics reports, self-audit, and status file. SHINOBU_337 profile hydration remains unmanaged defaults plus editor-only source CSV parsing until a real Data Monolith payload and boot validation exist.
Rejected Alternatives: Treating source CSV/default rows as runtime Data Monolith readiness, or inventing a payload in this domain. That would create false readiness evidence and cross-domain data ownership drift.
Scalability potential: Low through Ultra keep the same DTO/profile ABI; future baked static data can widen profile coverage without changing gameplay truth ownership.
Hardware Impact: Runtime 0 us; this is proof-surface correction only.

Problem: Post-fence verification needed syntax and route proof, but build execution remained illegal.
Solution: Parse JSON/XML proof files, run targeted `git diff --check`, scan for forbidden sibling dependency/hot-path sync/allocation patterns, and stop before build because CPU sampled 55.7% with seven active `dotnet` processes.
Rejected Alternatives: Launching `dotnet build` under explicit CPU/process violation or treating unparsed report edits as proof. Both generate unreliable evidence and contention with other agents.
Scalability potential: Runtime unchanged; proof fidelity improves for every quality tier.
Hardware Impact: Prevents workstation saturation; no runtime microsecond gain claimed.

Problem: Extending telemetry from 64 to 128 bytes risks partial writes, stale consumers, or unchecked ABI drift if only the struct is edited.
Solution: Re-read the telemetry writer, completion-window consumers, dump path, shader upload path, and layout validator. The writer fills the new hot AUP/hash fields before the ring assignment, consumers read only after `_hasPendingJob` is false, and the validator checks the new 128-byte offsets.
Rejected Alternatives: Trusting the struct patch without checking consumer phase order, or adding a getter-side `.Complete()` to make debug graphs simpler. That would violate dispatcher ownership and risk frame stalls.
Scalability potential: Low through Ultra share the same 128-byte forensic row; quality only changes write count and visual scalar richness.
Hardware Impact: Prevents false postmortem data without adding hot allocations or main-thread sync points.

Problem: `ReactorTelemetryRecorderJob` used a `uint` ring index for pointer indexing and cursor writes after the 128-byte telemetry expansion.
Solution: Keep `Frame` and serialized `RingIndex` as unsigned truth, but compute the actual memory index as `int ringIndex = (int)(Frame % (uint)TelemetryCapacity)` before pointer indexing and cursor assignment.
Rejected Alternatives: Leaving unsigned pointer indexing to compiler/operator inference, or changing the telemetry DTO field to signed. The former risks compile failure; the latter churns the ABI and proof docs without need.
Scalability potential: Runtime quality behavior unchanged; Low through Ultra write the same telemetry row shape.
Hardware Impact: Runtime 0 us; removes a compile-surface defect without changing memory layout.

Problem: Task 17 had a cold parser route but no actual `vehicle_reactor_profiles.csv` source file in the workspace, and proof text said editor/development even though the route is guarded by `UNITY_EDITOR`.
Solution: Add `Assets/_SourceData/Thermodynamics/vehicle_reactor_profiles.csv` with three deterministic profile rows plus Unity meta files, and reconcile docs to editor-only source hydration while player runtime waits for Data Monolith.
Rejected Alternatives: Leaving parser-only proof, or widening file IO into player/development runtime without a Data Monolith route. Parser-only gives designers no bridge; runtime text IO would violate static data ownership.
Scalability potential: Low profile Ion_Cell, middle Fission_Reactor, and high/ultra Abyssal_Breeder rows alter tuning inputs without changing DTO layout or authority route.
Hardware Impact: Player runtime 0 us; editor cold load reads at most 4096 bytes into Vault scratch.

Problem: Compile proof remains required, but current generated csproj files do not include the new SHINOBU_337 Thermodynamics files and seven `dotnet` processes are still alive.
Solution: Re-run source/project inclusion scans, record that no generated csproj contains the new reactor files, and keep build blocked even though CPU sampled 28.1%.
Rejected Alternatives: Running `dotnet build` anyway and presenting unrelated/stale project coverage as reactor proof. That would violate rebuild discipline and create false evidence.
Scalability potential: Runtime unchanged; verification waits for Unity import/project regeneration.
Hardware Impact: Prevents build contention; no runtime speed claim.

Problem: New SHINOBU_337 C# artifacts had no `.meta` files, so Unity import would generate unstable GUIDs and create avoidable merge/import churn.
Solution: Add stable two-line Unity meta files for every new Thermodynamics C# artifact and recheck `Hecton8.Thermodynamics.asmdef` references.
Rejected Alternatives: Waiting for Unity to generate GUIDs or moving files into another assembly. Generated GUIDs are nondeterministic proof noise; moving files risks domain boundary drift.
Scalability potential: Runtime behavior unchanged across all quality weights.
Hardware Impact: Runtime 0 us; protects iteration/import stability.

Problem: `AbyssalThermodynamicsSolver.cs` and `ThermodynamicsHazardGridRuntime.cs` carried `using Hecton8.World;` even though Thermodynamics has no World asmdef reference and the referenced origin types are in `Hecton8.Core`.
Solution: Remove the sibling namespace imports and keep Thermodynamics routed through Core/Core.Memory/Core.Contracts only.
Rejected Alternatives: Adding a World assembly reference or leaving a namespace import that can trip compile/import diagnostics. A direct World edge violates compile-wall isolation for this lane.
Scalability potential: Runtime quality behavior unchanged; authority route stays Vault/Signal/Core.
Hardware Impact: Runtime 0 us; compile dependency surface reduced.

Problem: Post-import-hygiene validation needed to prove no sibling import or stale proof syntax remained.
Solution: Run targeted `rg` for `Hecton8.World`, `Hecton8.Physics.Vehicles`, and `SubmarineKinematicState`, parse JSON/XML/CSV proof files, and run `git diff --check`. Refuse build because seven `dotnet` processes remain active at CPU 48.0%.
Rejected Alternatives: Reporting clean compile-wall hygiene without scanning, or launching a build during active compiler contention.
Scalability potential: Runtime unchanged.
Hardware Impact: Runtime 0 us; protects local workstation from build contention.

Problem: Goodall audit found `TryReadTelemetry` could read the inherited thermal telemetry ring while the solver job was still in flight.
Solution: Add `_hasPendingJob` to the accessor fence so both inherited thermal telemetry and reactor telemetry are readable only after the owner completion window.
Rejected Alternatives: Calling `.Complete()` inside `TryReadTelemetry`, or allowing editor diagnostics to race the recorder job. A getter-side completion violates dispatcher ownership; a race corrupts blackbox proof.
Scalability potential: Low through Ultra keep identical simulation truth and telemetry layout; only read availability changes during the in-flight frame.
Hardware Impact: Runtime speed gain is not claimed. The avoided cost is an illegal main-thread sync point.

Problem: Goodall audit found `SubmarineThermodynamicsTunerWindow` claimed UI Toolkit but rendered the graph through `IMGUIContainer`, `GUILayoutUtility`, `EditorGUI`, and `Handles`.
Solution: Replace the graph with a retained UI Toolkit `VisualElement` using `generateVisualContent` and `Painter2D` for background, guide lines, and heat/core telemetry segments.
Rejected Alternatives: Keeping an IMGUI island for convenience, or moving the graph to a runtime GameObject. IMGUI undercuts the editor facade proof; runtime debug objects violate the no-gameplay-allocation route.
Scalability potential: Editor Low displays the same bounded 80-sample proof graph; Ultra can increase visual detail later without touching runtime DTOs or authority routes.
Hardware Impact: Editor-only, runtime 0 us. The change removes facade inconsistency without adding player-frame work.

Problem: Post-Goodall verification needed local proof without touching unrelated dirty files.
Solution: Parse proof JSON/XML/CSV, run targeted SHINOBU_337 `git diff --check`, scan the tuner for IMGUI tokens, scan hot reactor files for sync/allocation hazards, and refuse build because seven active `dotnet` processes remain even after CPU later sampled 29.4%.
Rejected Alternatives: Repo-wide whitespace cleanup across other agents' work, or launching a build under explicit CPU/process guard violation.
Scalability potential: Runtime unchanged; evidence quality improves for every GlobalQualityWeight value.
Hardware Impact: Runtime 0 us; protects workstation from build contention.
