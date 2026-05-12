# Rationale_SUBMARINE_DAMAGE_CONTROL

STATUS: PENDING VERIFICATION

## Initialization

Problem: Existing prompt reports ParticleSystem GameObject avalanche from submarine leaks and repair sparks.
Solution: Treat leak state as packed data and use GPU/Burst paths where existing architecture allows.
Rejected Alternatives: Per-leak GameObjects, ParticleSystem instantiation, coroutine repair loops.
Scalability potential: Low = process 64 logic breaches but show 8; Middle = show 16-32; High = full 64; Ultra = full 64 plus richer shader/decal response.
Hardware Impact: Expected gain on i3/MX350 comes from removing GameObject/ParticleSystem churn and centralizing one data dispatch; measured proof absent.

## Mandate Ingestion

Problem: The prompt spans structural damage, repair interaction, compute VFX, flood mass, audio, and zero-GC events.
Solution: Bound the implementation to `SubmarineStructuralGrid`, `RepairTool`, `SubmarineFluidDynamics`, `GlobalSignals`, and the existing abyssal screen-space fluid decal runtime.
Rejected Alternatives: New submarine damage manager, scene GameObjects per leak, direct Physics.Raycast, coroutine repair loops, simulated interior water fill.
Scalability potential: Low = 64 logical breaches with 8 visible compute plumes; Middle = 16-32 visible plumes; High = 64 visible; Ultra = 64 visible plus stronger downstream shader/decal/audio response.
Hardware Impact: i3/MX350 target avoids Transform/Renderer/ParticleSystem cost per breach; one Burst repair pass over 64 entries is bounded and branch-light.

## Implementation Decisions

Problem: Submarine breach state needed repair, VFX, audio, and flood coupling without per-leak GameObjects.
Solution: `SubmarineStructuralGrid` owns a 64-slot `NativeArray<float4>` where xyz is submarine-local and w is severity; repair runs as `BreachRepairJob`; compute upload uses lock-buffer `GraphicsBuffer` double buffering.
Rejected Alternatives: New damage manager, `List<Breach>`, spawning leak prefabs, or storing AUP per breach.
Scalability potential: Low = 64 logic / 8 visible plumes; Middle = 16-32 visible if precision policy is raised later; High and Ultra = full 64 visible plus stronger screen-space spray intensity.
Hardware Impact: Removes the claimed 10 ParticleSystem leak avalanche path from submarine breaches; bounded 64-slot Burst loop is below suspicious frame-time territory on i3/MX350.

Problem: Repair sparks previously depended on a local `ParticleSystem` field and could drift into prefab-specific runtime behavior.
Solution: `RepairTool` publishes `DebrisSpawnSignal` with the existing repair-spark species hash and keeps the RaycastCommand-backed `TryResolveQueuedRaycast` path.
Rejected Alternatives: Direct `Physics.Raycast`, `sparksVFX.Play()`, or new one-off event IDs.
Scalability potential: Low = GPU scatter budget decides spark visibility; Ultra = downstream scatter can overdraw richer sparks without touching repair logic.
Hardware Impact: Removes emitter playback/Transform churn from the repair path; runtime allocation estimate is 0 B if the reused search list capacity holds.

Problem: Leak severity needed to matter physically without simulating visible interior water.
Solution: `sum(w) * ambientPressureKPa` calls `SubmarineFluidDynamics.ApplyDamageControlLeakMass`, adding bounded sinking mass; visual cabin fill remains the existing screen-space cheat.
Rejected Alternatives: Cabin water mesh, fluid volume simulation, or duplicating compartment flood state in the structural grid.
Scalability potential: Low = mass-only consequence; High/Ultra = same saved budget can feed heavier overlay/audio without changing math.
Hardware Impact: One scalar mass update path avoids particle/water simulation cost and stays deterministic enough for fixed-tick review.

Problem: Compile verification could not reach a clean project state.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`, then shut down the build server; recorded unrelated dependency wall.
Rejected Alternatives: Editing outside assigned domain to fix suit/audio/IK/celestial/UI errors or claiming a clean compile.
Scalability potential: Not applicable until dependency wall is cleared.
Hardware Impact: No runtime estimate from compile gate; code remains PENDING VERIFICATION.

## OMEGA POLISH CHANGES

Problem: The first pass used `Vector3.normalized` for breach spray direction, an unnecessary honest normalization on a visual-only decal/spray vector.
Solution: Replaced it with the existing `ResolveSafeDirection` path, which uses reciprocal square-root style scaling and fallback direction.
Rejected Alternatives: Leaving Unity normalization in the hot visual-feedback path.
Scalability potential: Low = same visual direction approximation; High/Ultra = no change needed because exact normalization buys no visible benefit here.
Hardware Impact: Removes one unnecessary normalization helper call from breach feedback; microsecond gain is below standalone measurement but aligned with MX350/i3 policy.

Problem: Compute visibility needed a hard low-tier gate without reducing damage truth.
Solution: `ResolveVisibleBreachCount` caps visible breach plumes to 8 when `H8_LOW_MEMORY_PROFILE` or low math precision is active; Burst repair and flood math still process all 64.
Rejected Alternatives: Reducing the logical breach array to 8 or spreading physical repair across frames.
Scalability potential: Low = 8 visible / 64 logic; High = 64 visible / 64 logic; Ultra can increase downstream shader density without changing the SOA.
Hardware Impact: Low-tier compute writes 32 visible plume points instead of 256 while preserving deterministic repair/flood results.

Problem: Hidden managed allocation and runtime bloat risk needed a final scan.
Solution: Scanned touched paths for `Array.Copy`, coroutines, `math.sqrt`, `math.normalize`, `.normalized`, interpolated strings, and unmanaged hot allocations. New `new` sites are cold setup, value-type signal structs, or error-only black-box dump I/O.
Rejected Alternatives: Removing unrelated pre-existing hull impact ParticleSystem cold allocation; it is outside the submarine leak/repair-spark prompt and would be a refactor loop.
Scalability potential: Low/High unaffected; leak/repair hot path stays data-oriented.
Hardware Impact: Hot-path allocation estimate remains 0 B assuming the reused `List<MonoBehaviour>(4)` lookup buffer does not grow.

Problem: Domain boundary check was required.
Solution: `Docs/Actual Domains of Project.txt` places fluid incursion, submarine OS/navigation, tools, and vehicles in HABITAT & VEHICLES / equipment adjacency; edits to `RepairTool`, `SubmarineStructuralGrid`, and `SubmarineFluidDynamics` are justified by the cross-domain repair/flood interface in the prompt.
Rejected Alternatives: Editing global audio/scatter consumers or unrelated compile blockers.
Scalability potential: Cross-domain integration remains via `GlobalSignals` and a small interface, not concrete prefab dependencies.
Hardware Impact: EventBus signal coupling avoids direct component fan-out.

Final Git Diff (scoped summary):
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs`: breach SOA, Burst repair job, GPU buffer dispatch, screen-space spray, flood/audio/klaxon coupling, compaction, black-box telemetry.
- `Assets/_Project/Scripts/RepairTool.cs`: queued-raycast submarine damage branch, EventBus repair sparks, legacy `sparksVFX.Play()` removed.
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: damage-control leak mass scalar and Rigidbody mass coupling.
- `Assets/_Project/Art/Shaders/Hecton_LeakPlume.compute`: one-dispatch 64-slot GPU leak plume expansion.
- `Docs/AgentLogs/RECON_SUBMARINE_DAMAGE_CONTROL.md`: prefab reconnaissance log.
- `Docs/Tasks/Status_SUBMARINE_DAMAGE_CONTROL.md`: task ledger.

## CONTINUATION HARDENING

Problem: `OnHullBreach` could mutate the `_breaches` NativeArray while `BreachRepairJob` still owned it if a damage signal arrived between fixed and post-fixed lanes.
Solution: Added a fixed `float4[16]` deferred breach lane and flush it only after the repair job is no longer running; post-fixed telemetry now records a job-in-flight flag without reading the NativeArray.
Rejected Alternatives: Main-thread `JobHandle.Complete()` on breach intake, dropping breach events during repair, or adding a managed queue.
Scalability potential: Low = no job/read race on weak CPUs; Middle/High/Ultra = same deterministic lane, higher breach event bursts retain strongest queued deferred entries.
Hardware Impact: Avoids safety-system stalls and NativeArray races; fixed array write is constant-time and allocation-free on i3/MX350.

Problem: Empty leak state still had a path to dispatch the compute kernel every post-fixed tick after the GPU buffers were already clean.
Solution: Preserve one dirty upload/dispatch to clear stale plume output, then set global visible count to 0 and skip unchanged empty dispatches.
Rejected Alternatives: Always dispatching 64 threads forever or clearing the particle buffer from CPU every frame.
Scalability potential: Low = cheapest idle leak path; High/Ultra = active breaches still use the full 64-slot visual budget.
Hardware Impact: Idle breach state saves one compute dispatch per post-fixed tick after clear; exact microseconds require Unity profiler proof.

Problem: The first compute shader used sine-based pseudo-random hashing for four plume points per breach.
Solution: Replaced sine hash with integer avalanche hash plus triangle-wave offsets.
Rejected Alternatives: Keeping trig in a visual-only MX350 kernel or moving wobble to CPU.
Scalability potential: Low = cheaper ALU with similar spray motion; Ultra = saved ALU can fund denser downstream plume rendering without changing C#.
Hardware Impact: Removes shader trig from the leak-plume kernel; estimated gain is small per dispatch but aligned with MX350 compute mandate.

Problem: `RepairTool` interface lookup scratch used `List<MonoBehaviour>(4)`, which could grow on first submarine collider lookup with several parent components.
Solution: Increased the cold scratch capacity to 16.
Rejected Alternatives: Directly referencing `SubmarineStructuralGrid` from the tool or tolerating first-hit list growth.
Scalability potential: Low/High unaffected visually; hot repair path keeps allocation risk lower.
Hardware Impact: Adds a tiny cold managed reserve to reduce first-hit GC risk; runtime repair path remains expected 0 B.

Problem: Editor-side validation was requested but the Unity MCP session was unavailable.
Solution: Attempted `validate_script` for `SubmarineStructuralGrid`, `RepairTool`, and `SubmarineFluidDynamics`; all returned `no_unity_session`, then ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`.
Rejected Alternatives: Claiming Unity verification or editing unrelated audio/world dependencies.
Scalability potential: Not applicable until a Unity session/import pass is available.
Hardware Impact: No runtime proof from this gate; status remains PENDING VERIFICATION.

## GPU RENDER BRIDGE

Problem: The compute kernel produced `_LeakPlumeParticleBuffer`, but the visible plume shader still only supported conventional mesh rendering, which left the compute output without an actual draw bridge.
Solution: Added a procedural `_UseLeakParticleBuffer` path to `Hecton_LeakPlume.shader` and a late-frame `Graphics.RenderPrimitives` submission from `SubmarineStructuralGrid` using a cached `MaterialPropertyBlock`.
Rejected Alternatives: CPU-reading the particle buffer to build matrices, spawning per-leak quads/GameObjects, or mutating the shared material directly every frame.
Scalability potential: Low = 8 visible breaches * 4 quads, no shadows/light probes; Middle = 16-32 visible breaches; High/Ultra = 64 visible breaches and stronger material tuning without adding CPU transforms.
Hardware Impact: 1 procedural draw call for up to 256 tiny billboard quads on high tier, 32 quads on low tier; no CPU particle readback and no per-leak GameObject cost.

Problem: Late-frame rendering needed camera-facing billboards without using `Camera.main`.
Solution: Camera right/up vectors come from `GlobalRegistry.Player.PlayerCamera`; missing camera falls back to world axes so the draw path fails visually soft, not catastrophically.
Rejected Alternatives: Scene camera search, Update polling, or binding cross-scene camera references.
Scalability potential: Low/High share the same per-frame draw path; only instance count scales.
Hardware Impact: Avoids scene search and keeps rendering in the dispatcher late-frame lane.

Problem: The prior compile wall changed while other agents worked in parallel.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`; C# compile now succeeds with 0 warnings and 0 errors, then build servers were shut down.
Rejected Alternatives: Leaving task 15 blocked after the dependency wall cleared, or claiming shader/import verification from C# compile.
Scalability potential: Compile success does not prove frame time or visual quality; Unity import/profiler validation remains required.
Hardware Impact: No runtime metric measured; status remains PENDING VERIFICATION.

## PROCEDURAL PLUME HARDENING

Problem: `Graphics.RenderPrimitives` supplies procedural vertex and instance IDs, but no reliable mesh vertex color stream. The first render bridge multiplied plume color/alpha by `input.color`, which can be zero on procedural draws and make active leak plumes invisible.
Solution: The procedural `_UseLeakParticleBuffer` branch now writes `half4(1,1,1,severity)` before the fragment mask/tint path. The legacy mesh path still preserves authored vertex color.
Rejected Alternatives: Creating a CPU quad mesh to provide colors, adding per-instance CPU material variants, or reading back particle data to rebuild mesh streams.
Scalability potential: Low = same 8-breach visual cap with guaranteed visible quads; Middle/High/Ultra = full breach count still uses one procedural draw and material tint, with no CPU stream upload.
Hardware Impact: Avoids a failed visual path without adding CPU work; no measured frame-time delta because Unity playmode/profiler proof is still unavailable.

Problem: A missing or failed compute kernel import could throw from `FindKernel`, turning a content/import problem into a runtime exception path.
Solution: `EnsureLeakPlumeGpuResources` now checks `leakPlumeCompute.HasKernel("CSSpawnLeakParticles")` before resolving the kernel index.
Rejected Alternatives: Letting `FindKernel` throw, adding a `Resources.Load` runtime fallback, or hard-coding asset repair from gameplay code.
Scalability potential: All tiers fail soft if the compute asset is not imported correctly; valid assets still allocate the same double-buffered GPU resources.
Hardware Impact: No hot-path cost after the kernel index is cached; one cold boolean guard during resource initialization.

Problem: Editor validation was partially available but unstable.
Solution: Unity MCP validated `SubmarineStructuralGrid.cs` with 0 warnings/0 errors, confirmed `Hecton_LeakPlume.compute`, `Hecton_LeakPlume.shader`, and `Mat_LeakPlume.mat` assets exist/import, and showed `Mat_LeakPlume` uses `HECTON/VFX/LeakPlume`. `RepairTool.cs` validation had one generic warning and no errors; `SubmarineFluidDynamics.cs` validation timed out in the MCP regex engine; console later showed unrelated `PDAMapTab.cs` errors before the Unity session dropped again.
Rejected Alternatives: Treating partial MCP results as runtime verification or editing out-of-domain UI compile errors.
Scalability potential: Not applicable; this is evidence hygiene.
Hardware Impact: No runtime metric measured; status remains PENDING VERIFICATION.

## BLACK BOX FOOTPRINT HARDENING

Problem: Damage-control black-box telemetry carried a wider struct than needed for the mandated 300-frame postmortem lane.
Solution: `DamageControlTelemetryEntry` is now explicitly `Size = 32`, with `ushort` active/visible counts and retained first breach local position, severity sum, frame, flags, and state hash.
Rejected Alternatives: Keeping 32-bit counts for a 64-breach system, dropping the hash, or moving telemetry to managed log strings.
Scalability potential: Low/Middle/High/Ultra all keep the same 300-frame history; lower footprint leaves more budget for future high-tier visual telemetry without changing gameplay.
Hardware Impact: Native telemetry ring drops from wider records to 32 bytes per frame, keeping the full ring around 9.6 KB before allocator overhead.

Problem: The normal parallel build path was overloaded by several existing dotnet/MSBuild workers from other sessions.
Solution: Re-ran a single-node build with shared compilation disabled: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false -v:minimal`.
Rejected Alternatives: Killing unknown dotnet workers from parallel agents or claiming the earlier timed-out build as evidence.
Scalability potential: Not runtime-related; this is verification hygiene under multi-agent load.
Hardware Impact: Build completed with 0 errors; the warnings came from external package projects, not damage-control code.

## LEAK MASS CAP HARDENING

Problem: Damage-control leak mass could keep accumulating if submarine fluid capacity resolved to zero, which made a fallback damage path unbounded instead of predictable.
Solution: `ApplyDamageControlLeakMass` now clamps added leak ballast against exterior displacement water mass, falling back to dry rigidbody mass only when displacement is unavailable.
Rejected Alternatives: Ignoring zero-capacity hulls, simulating interior compartments visually, or adding a new managed flood container outside the existing fluid dynamics owner.
Scalability potential: Low = bounded scalar mass response only; Middle = same cap with richer overlay/audio; High/Ultra = saved simulation budget can fund stronger visual plume and decal response without changing physical truth.
Hardware Impact: One extra scalar max/min path prevents runaway mass and avoids any cabin-water simulation; runtime cost is below useful standalone measurement on i3/MX350.

Problem: The latest leak-mass cap needed a fresh compile after a prior `--no-dependencies` check failed on missing Temp metadata.
Solution: Re-ran the full single-node C# build with shared compilation disabled.
Rejected Alternatives: Treating the metadata-only `--no-dependencies` failure as a code error or killing unrelated build workers.
Scalability potential: Compile hygiene only; runtime verification still requires Unity Play Mode/profiler evidence.
Hardware Impact: `Hecton8.Core.dll` built with 0 errors; 47 warnings came from URP/GPUInstancer/Crest/ShaderGraph package projects, not touched damage-control code.
