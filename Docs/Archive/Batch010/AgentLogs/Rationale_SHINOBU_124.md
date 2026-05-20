# Rationale_SHINOBU_124

## 2026-05-19 Preflight

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="SHINOBU_124">`, so the mandated XML task count is 0.
Solution: Record the missing XML as a hard evidence fact, use the inline user directive only as local task context, and avoid neighboring prompt influence.
Rejected Alternatives: Reading SHINOBU_120 as a proxy, inventing a task count, or pulling archived prompts into the current batch.
Scalability potential: No runtime effect; prevents wrong-domain code from being added under batch ambiguity.
Hardware Impact: 0 us runtime gain on i3/MX350; process-only guard.

Problem: Flora currently risks physical-collider interaction cost if bending is handled by object-level physics.
Solution: Target a visual fake: a Vault-owned 3D displacement/impulse field sampled by shaders, fed by vehicle force injection through decoupled interfaces.
Rejected Alternatives: Per-blade Rigidbody/Collider, trigger volumes per plant, or Unity physics queries for every vegetation instance.
Scalability potential: Low uses coarse field stride and decay; Middle tightens cell size/update cadence; High adds wake persistence; Ultra spends saved CPU on richer shader harmonics and higher field resolution.
Hardware Impact: Estimated savings versus collider vegetation is unmeasured but structurally removes per-flora physics broadphase and managed callback risk; expected target is under 0.1 ms CPU update on i3/MX350 after implementation.

Problem: `H8Memory.BufferID` is already dirty in the shared workspace and high numeric ranges are being used by other agents through private casts.
Solution: Keep the new flora sway Vault IDs private to `FloraInteractionManager` at `71580..71582`, after an exact-source scan found no collisions.
Rejected Alternatives: Editing the shared enum while another agent owns unrelated memory IDs, or reusing `WakeVectorBuffer` as a fake 3D field.
Scalability potential: One authoritative field can scale resolution, cell size, update cadence, and damping from weak hardware to visual-overkill hardware without changing the contract.
Hardware Impact: Avoids cross-agent merge churn; runtime effect is 0 us beyond the actual field update.

Problem: Existing shader path bent flora from a direct submarine sphere global, which is a one-source shortcut and not a Vault field.
Solution: Generate a 3D `float4` displacement field from decoupled wake sources, store it in Vault, upload through double `GraphicsBuffer`, and make the vegetation vertex shader sample `_HectonFloraSwayDisplacementField`.
Rejected Alternatives: Per-plant colliders, per-blade physics callbacks, compute simulation of water volume, or adding a direct `VehicleMotor` reference.
Scalability potential: Weak devices use 8-ish resolution, larger cells, fewer source slots, and slower updates; middle/high/ultra continuously densify to 16^3 nodes, smaller cells, more sources, faster refresh, and higher displacement gain.
Hardware Impact: Structural saving is removal of submarine-to-flora collider requirement; field update is bounded to 4096 nodes and 16 sources worst case, with `GlobalQualityWeight` reducing nodes/sources/update rate before thermal failure.

Problem: Compile verification is required but the explicit CPU gate forbids dotnet/csc while total CPU is over 50%.
Solution: Run non-build static gates only and record the build as blocked by CPU gate after repeated `Processor(_Total)` samples stayed at 100%.
Rejected Alternatives: Launching dotnet anyway, claiming Unity compile success without evidence, or waiting indefinitely while 20+ agents keep the machine saturated.
Scalability potential: No runtime effect; preserves shared workstation stability.
Hardware Impact: Prevents build contention; runtime estimate remains pending profiler data.

## 2026-05-19 XML Restoration / Titanium Pass

Problem: `CURRENT_BATCH.md` now contains the real 20-task `SHINOBU_124` prompt, invalidating the earlier XML-absent status.
Solution: Re-extract the XML by ID, rewrite `Status_SHINOBU_124.md` with the 20-task matrix, and keep all old "missing XML" facts only as historical log lines.
Rejected Alternatives: Continue using the inline prompt only, or let neighboring SHINOBU tasks influence architecture.
Scalability potential: Process-level correction only; prevents wrong-domain code.
Hardware Impact: 0 us runtime.

Problem: The first pass used a CPU `float4` loop and kept `Pack=1` telemetry, which violated the explicit ARM64/Burst mandate.
Solution: Replace the field storage with `FloraDisplacementDTO` `[StructLayout(LayoutKind.Explicit, Size = 16)]`, offsets 0/12; use `FloraSwayFieldTelemetryEntry` explicit 64B; validate via `UnsafeUtility.SizeOf/GetFieldOffset`.
Rejected Alternatives: `float4` as an implicit contract, `Pack=1`, C# properties, or managed class state for nodes.
Scalability potential: Low/Middle/High/Ultra all use the same 16B ABI and only change resolution/cadence/math.
Hardware Impact: Prevents ARM64 unaligned access penalties; expected gain is structural, not profiler-measured yet.

Problem: Collider bending and large-flora proxy colliders contradicted the visual-field mandate.
Solution: Remove `Physics.OverlapSphereNonAlloc` from `FloraInteractionManager` dynamic bend collection and replace `HectonMapMagicVegetationBridgeFloraCollisionProxies` with pure no-op partial methods so partial call sites remain but collider/proxy types disappear from the source lane.
Rejected Alternatives: NonAlloc physics queries, trigger scripts, or pooled `BoxCollider` proxies for procedural sway.
Scalability potential: Weak devices avoid broadphase work; high-end devices spend saved CPU on 64^3 field and shader interpolation.
Hardware Impact: Removes one per-frame physics query and proxy GameObject churn from the flora sway lane.

Problem: Source-force scatter would need atomics or write races when many bodies affect one cell.
Solution: Use a deterministic cell-driven gather: `AccumulateFloraForcesJob` reads bounded wake sources and writes exactly one cell index per worker; `[NoAlias]` fields let Burst vectorize safely.
Rejected Alternatives: Atomic scatter, per-source NativeQueue fanout, or CPU loops over 64^3 nodes.
Scalability potential: Source limit scales continuously with `GlobalQualityWeight`; 16^3 low and 64^3 ultra use the same kernel.
Hardware Impact: Avoids false sharing and atomics; expected i3/MX350 saving depends on active source count and needs profiler proof.

Problem: A localized field can smear stale displacement when the player crosses a quantized origin boundary.
Solution: Quantize the grid from camera/player AUP, subtract source AUP in double/long space before float cast, and make the decay job reset active nodes when resolution/origin changes.
Rejected Alternatives: Casting absolute 100km positions to float, or keeping stale rows after a center jump.
Scalability potential: Low uses coarser cells and fewer origin changes; ultra tightens cells without precision drift.
Hardware Impact: Prevents jitter/stretch faults; reset cost is a Burst linear pass instead of main-thread zeroing.

Problem: Missing `flora_stiffness_profiles.h8bin` or CSV must not crash boot or force managed parsing in runtime lanes.
Solution: Add Vault-owned fallback rules (`71583`) and a cold CSV ingestor using native scratch (`71584`), byte-level FNV-1a hashing, and unmanaged rule mutation.
Rejected Alternatives: `string.Split`, `File.ReadAllLines`, ScriptableObjects, or failing initialization when the baker payload is absent.
Scalability potential: Designers can tune stiffness without C# recompilation; runtime shader path remains stable.
Hardware Impact: Hot path 0 B GC; CSV import is cold/editor only.

Problem: Designers needed a facade and forensic visibility without adding runtime GameObjects.
Solution: Add UI Toolkit `Procedural Flora Sway Tuner`, live max/resolution/cell readout, sliders for decay/current/mass, mock/gizmo toggles, and `OnDrawGizmos` line sampling from Vault.
Rejected Alternatives: in-game debug text, runtime arrows, or requiring code edits for tuning.
Scalability potential: Weak devices tune decay/current down; high/ultra raise field density and visual response.
Hardware Impact: Editor-only allocation surface; no player hot-path GC.

Problem: The shader still relied on direct sphere/interaction offsets after the field was added.
Solution: Sample `_HectonFloraSwayDisplacementField`, use vertex color red as stiffness/tip mask, use nearest sampling at low quality and trilinear sampling above the quality curve, and suppress direct sphere/player offsets while the field is active.
Rejected Alternatives: CPU leaf deformation, direct submarine sphere as authoritative bending, or always-eight-tap sampling on low quality.
Scalability potential: 16^3 nearest on low, 64^3 trilinear on ultra.
Hardware Impact: Moves interaction cost to vertex ALU/structured-buffer reads; CPU broadphase removed.

Problem: The no-op large-flora source still carried `CollisionProxy` names in method call sites and filename, so literal grep gates could misclassify the lane as proxy-backed.
Solution: Rename the partial source to `HectonMapMagicVegetationBridgeFloraVisualSway.cs`, rename the five no-op partial methods to visual-sway names, and update the local call sites in `HectonMapMagicVegetationBridge.cs`.
Rejected Alternatives: Keep proxy names with comments, or delete call sites and risk partial-lifecycle compile drift.
Scalability potential: No runtime algorithm change; it hardens the proof that procedural sway has no PhysX representation from weak devices to ultra.
Hardware Impact: 0 us measured; prevents accidental reactivation of collider proxy churn.

Problem: Touched runtime files still contained legacy `Pack=1` layouts, which violates the ARM64 alignment mandate even if those structs predated SHINOBU_124.
Solution: Convert `ParasiteNode` to explicit 64B layout with `double BirthTimeSeconds` at offset 0, and convert `AbyssalPathTelemetryEntry` to explicit 64B layout with manual padding at offsets 56 and 60.
Rejected Alternatives: Ignore adjacent Pack=1 as "not my task", or change field sizes without preserving 64B stride.
Scalability potential: Uniform explicit DTO layouts keep mobile ARM64 and desktop SIMD paths predictable across quality levels.
Hardware Impact: Avoids unaligned double access risk on i3/MX350-class and ARM64 targets; exact gain pending profiler proof.

Problem: `FloraSwayTunerWindow.cs` was a new Unity asset without a `.meta` file, leaving GUID generation to local Unity import.
Solution: Add `FloraSwayTunerWindow.cs.meta` with a fixed GUID.
Rejected Alternatives: Let Unity generate a local GUID later, which would be nondeterministic across agents.
Scalability potential: Editor-only asset hygiene; no runtime scalability effect.
Hardware Impact: 0 us runtime.

Problem: Compile proof is still required, but the latest CPU gate sample ended at `75.3%`.
Solution: Do not launch dotnet/csc; run only static gates and keep Task 20 open until the build gate is legal.
Rejected Alternatives: Build under load, or claim compile success from static review.
Scalability potential: No runtime effect; protects shared workstation throughput.
Hardware Impact: Prevents build contention; runtime frame cost remains pending Unity/profiler proof.

## 2026-05-19 Ultra Polish Pass 2

Problem: Flora sway scheduling and wake-source stamps still depended on `Time.frameCount`, which is non-deterministic presentation timing and weak rollback hygiene.
Solution: Add owner-local monotonic counters: `_floraSwaySimulationFrameCounter` for the displacement-field phase/upload metadata, `_proceduralWakeSignalFrameCounter` for wake-source and fluid-impulse stamps, and `_wakeTrailDispatchSerial` for same-tick compute dispatch guarding.
Rejected Alternatives: Continue reading `Time.frameCount`, or import a cross-domain simulation-clock dependency not present in this lane.
Scalability potential: Low/Middle/High/Ultra all retain the same deterministic counter route while quality only changes resolution, source count, cadence, and shader sampling cost.
Hardware Impact: 0 us target runtime cost beyond integer increments; removes non-deterministic frame-counter reads from the sway/wake source path.

Problem: Hot flora sway resolve methods could fall back through `GlobalRegistry.DataVault` after boot if `_wakeDataVault` was null, hiding a service-lifetime fault and adding a global lookup surface.
Solution: Restrict hot resolve methods to cached `_wakeDataVault`; only cold handle acquisition routes use `GlobalRegistry.DataVault` to request Vault buffers.
Rejected Alternatives: GlobalRegistry lookup inside every field update, or direct sibling-domain references to fetch memory.
Scalability potential: Field update cost scales only through `GlobalQualityWeight`, not service lookup behavior.
Hardware Impact: Saves only tiny control-path overhead, but hardens ownership proof and keeps the Compile Wall clean.

Problem: Several touched Burst jobs lacked the exact `CompileSynchronously = true` directive required by the mandate.
Solution: Update all `[BurstCompile]` attributes in the touched runtime files to `CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard`.
Rejected Alternatives: Leave preexisting jobs with partial flags because they were not introduced by this agent.
Scalability potential: Predictable Burst compilation behavior across Quest-class ARM64 and desktop x86; quality scaling remains mathematical.
Hardware Impact: Prevents fallback/safe-eval risk; exact frame gain requires profiler proof.

Problem: The editor tuner readout generated a formatted string every `EditorApplication.update`.
Solution: Throttle readout sampling to 10Hz, cache integer millimeter/resolution/cell values, and update UI Toolkit label text only when a value changes.
Rejected Alternatives: Keep per-update `.ToString("0.000")`, or claim true UI Toolkit zero-GC while the Label API still consumes managed strings.
Scalability potential: Editor-only; weak machines avoid needless editor churn while technical artists retain live tuning.
Hardware Impact: Player hot path remains 0 B GC; editor allocations are reduced to value-change events.

Problem: Compile verification remains mandatory but the CPU gate still forbids build launch.
Solution: Sampled CPU returned `83`, `65.8`, `99.8` with no `dotnet`/`csc`; build remains blocked and Task 20 stays open.
Rejected Alternatives: Launch dotnet under >50% CPU, or mark compile proof as complete from static scans.
Scalability potential: No runtime effect.
Hardware Impact: Prevents shared workstation contention.

Problem: A later CPU sample dipped below 50% on two readings but spiked to `77.9%` between them.
Solution: Treat the gate as unstable under parallel-agent load; do not launch build until the sample window is clean.
Rejected Alternatives: Start `dotnet build` after one low sample while another sample in the same window is over 50%.
Scalability potential: No runtime effect.
Hardware Impact: Avoids compile contention during load spikes.

## 2026-05-19 Ultra Polish Pass 3

Problem: The UI Toolkit facade still could allocate a new max-magnitude formatted string when the sampled value changed.
Solution: Split the readout into a max-magnitude label backed by a cold precomputed millimeter string cache (`0.000`..`9.999`) and a secondary details label that updates only on editor value changes.
Rejected Alternatives: Claim `Label.text` is true per-update zero-GC, or replace the required UI Toolkit facade with a custom TMP runtime HUD outside the editor scope.
Scalability potential: Editor-only; weak developer machines avoid repeated numeric formatting while retaining live max-force visibility.
Hardware Impact: Player hot path remains 0 B GC; editor max readout changes now reuse existing string instances.

Problem: `ClearFloraSwayDisplacementField()` was still capable of walking the full 64^3 node buffer and uploading it to the GPU just to disable a visual field.
Solution: Clear only the four metadata vectors, set `_floraSwayFieldActive = false`, publish inactive shader globals, and rely on the next scheduled Burst reset to clean active node range before accumulation.
Rejected Alternatives: Main-thread `for` loop over 262,144 `FloraDisplacementDTO` entries, or forced 4 MB `GraphicsBuffer` upload of a disabled field.
Scalability potential: Low/Middle/High/Ultra all avoid pointless clear/upload when the field is disabled; visual safety comes from the shader active flag.
Hardware Impact: Avoids a worst-case 4 MB CPU memory write plus 4 MB CPU->GPU upload on clear events; exact microseconds pending profiler proof.

Problem: Build verification became more blocked, not less.
Solution: CPU samples returned `100`, `97.7`, `90.4`, and an external `dotnet` process was active as PID `16624`; build remains forbidden.
Rejected Alternatives: Compete with another dotnet process, or claim compile proof from static scans.
Scalability potential: No runtime effect.
Hardware Impact: Prevents compile contention under saturated workstation load.

## 2026-05-19 Ultra Polish Pass 4

Problem: The wake source budget feeding procedural flora still used low-tier and stress boolean gates, and `_GlobalWakeParams.y` was consumed as a low-tier flag by a compute shader.
Solution: Replace the wake slot limit with a continuous `ResolveWakeBudgetWeight()` / `ResolveWakeBudgetPressure01()` curve derived from `GlobalQualityWeight` and thermal stress. `_GlobalWakeParams.y` now carries budget pressure, and the Sargassum consumer lerps slot count toward the cheap path instead of thresholding that metadata.
Rejected Alternatives: Keep a `lowTier || stressCap` branch, or only rename the flag while preserving the same binary behavior.
Scalability potential: Low uses about four wake slots and cheap shader consumers; Middle/High progressively reopen slots; Ultra reaches the full 16-slot wake feed and spends the visual budget on denser flora/microfauna wake response.
Hardware Impact: Avoids hard pop between 4 and 16 wake slots; exact microseconds pending profiler proof.

Problem: `WakeSource` and `WakeTelemetryEntry`, which are consumed by the flora sway pipeline, still used `[StructLayout(Pack = 1)]`.
Solution: Remove `Pack=1`, keep explicit field offsets and sizes (`WakeSource` 128B, `WakeTelemetryEntry` 64B), add manual `uint` padding to `WakeSource` offsets 108..124, rename telemetry byte 60 from `LowTier01` to `BudgetPressure01`, and update `WakeDecayJob` with exact Burst flags plus `[NoAlias]`.
Rejected Alternatives: Ignore the DTO because it lives under VFX/Wakes, or change field order and risk binary route drift.
Scalability potential: Same DTO ABI supports every quality level; only source budget and shader sampling change.
Hardware Impact: Removes ARM64 unaligned-access risk on the wake feed read by the flora field; exact gain pending profiler proof.

Problem: Compile verification is still blocked after the patch.
Solution: Latest CPU gate sample was `100`, `100`, `100`, with active `csc` PID `44272` and `dotnet` PID `31508`; no build was launched.
Rejected Alternatives: Build under a saturated CPU and active compiler process.
Scalability potential: No runtime effect.
Hardware Impact: Prevents compile contention while parallel agents are already compiling.

Problem: Build gate was rechecked after the DTO padding correction.
Solution: Latest CPU gate sample stayed at `100`, `100`, `100`; no `dotnet`/`csc` process was active, but CPU alone still blocks compilation.
Rejected Alternatives: Launch build on a saturated machine because compiler processes were no longer visible.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local compile from competing with the current 100% CPU workload.

## 2026-05-19 Ultra Polish Pass 5

Problem: The optional `MockDisplacementInjectorJob` ran after production accumulation clamping, so the editor/CI stress path could add synthetic force above the quality-scaled max displacement even though the production wake path was clamped.
Solution: Sanitize the existing cell value inside the mock job, clamp `DecayTimer`, add the deterministic ghost force, then clamp `ForceVector` back to `math.lerp(0.28f, FloraSwayFieldMaxDisplacementMeters, SmoothStepJob(QualityWeight))` with guarded `math.rsqrt(math.max(...))`.
Rejected Alternatives: Trusting the mock path because it is editor-facing, or moving the mock before accumulation and changing stress-test behavior.
Scalability potential: Low/Middle/High/Ultra all share the same displacement ceiling; quality changes the ceiling smoothly instead of letting synthetic stress bypass it.
Hardware Impact: Adds a tiny constant ALU cost only when mock injection is enabled; prevents shader vertex explosions during deterministic throughput tests.

Problem: The self-audit validator checked only the owned flora DTO and not the wake ABI consumed by the field.
Solution: Add editor-time validation for consumed `WakeSource` and `WakeTelemetryEntry` sizes and key offsets, and wire those checks into the UI Toolkit tuner `OnEnable` error path.
Rejected Alternatives: Rely on log-only layout math, or validate by comments without executable offset checks.
Scalability potential: No direct visual cost; protects the same ABI from Quest-class ARM64 through desktop x86.
Hardware Impact: 0 us player hot path; editor-only validation.

Problem: Some adjacent Burst jobs in the same manager still left NativeArray aliasing implicit, and several safe-normalize helpers used `math.rsqrt(lengthSq)` after a threshold check rather than an explicit max guard.
Solution: Add `[NoAlias]` to cascade/parasite job arrays and wrap the remaining inspected `rsqrt` operands with `math.max`.
Rejected Alternatives: Treat the jobs as unrelated legacy code while they share the same source and Burst lane.
Scalability potential: No behavior change; gives Burst stronger aliasing facts across quality levels.
Hardware Impact: Expected to help vectorization safety; exact gain pending profiler proof.

## 2026-05-19 Ultra Polish Pass 6

Problem: The documented quality cadence said 5Hz to 60Hz, but the code still used an approximate middle value from the earlier rough pass.
Solution: Set `FloraSwayFieldMinUpdateIntervalSeconds` to `1f / 60f` and `FloraSwayFieldMaxUpdateIntervalSeconds` to `0.2f`, preserving the existing smooth `math.lerp`/`SmoothStep01` curve.
Rejected Alternatives: Keep the 25Hz to 7Hz compromise, add a hardware-profile branch, or let shader interpolation hide an incorrect scheduler contract.
Scalability potential: Low/Middle/High/Ultra now use the same continuous curve from exact 5Hz thermal survival to exact 60Hz visual-overkill response.
Hardware Impact: Low quality sheds scheduled field updates to 5Hz; ultra spends the reclaimed budget on a full 64^3, trilinear shader response. Exact microseconds pending profiler proof.

Problem: The runtime source contained `System.Reflection` for layout validation, even though it was intended as an editor-only facade check.
Solution: Wrap `ValidateFloraDisplacementDtoLayout`, `ValidateFloraSwayTelemetryLayout`, consumed wake ABI validators, and `ResolveFieldOffset` in `#if UNITY_EDITOR`. The editor tuner still calls them; player builds strip the reflection path.
Rejected Alternatives: Leave reflection visible in player source, remove executable layout checks, or replace the editor facade with comments.
Scalability potential: No gameplay visual difference; keeps hot/player paths free of reflection while preserving designer-visible ABI failure.
Hardware Impact: 0 us player hot path; editor validation remains cold and deliberate.

Problem: The Ultra mandate requested proof that flora draw work is not sent blind when occluded.
Solution: Statically verify the existing vegetation owner route instead of adding a second culling owner: `HectonIndirectVegetationRenderer.TryRenderGpuIndirect()` calls `BuildDepthPyramid()`, binds `_HectonDepthPyramid`, dispatches `FloraCulling.compute`, moves append counts with `GraphicsBuffer.CopyCount`, and submits with `Graphics.RenderMeshIndirect`.
Rejected Alternatives: Duplicate HZB culling inside SHINOBU_124, download the GPU pyramid to CPU for a Burst pass, or invent direct dependencies on the renderer. The existing GPU route is owner-local and avoids CPU readback latency.
Scalability potential: Low keeps GPU culling cheap through indirect append and existing density decimation; Middle/High/Ultra can spend saved vertex work on denser visible flora and richer shader response.
Hardware Impact: Prevents already-occluded vegetation from reaching the vertex shader on the indirect route. Exact savings need Frame Debugger/profiler proof.

Problem: Compile proof is still required after the pass 6 audit patch.
Solution: Recheck the build gate after static scans. CPU samples stayed at `100`, `100`, `100`; `dotnet/csc=0`. No build was launched because CPU alone violates the guard.
Rejected Alternatives: Launch compilation on a saturated workstation, or mark Task 20 closed from static scans.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local compile contention under current parallel-agent load.

## 2026-05-19 Ultra Polish Pass 7

Problem: Task 12 still relied on reset-on-origin-change, which was safe but did not satisfy the requested modulo row/layer wrapping. It also discarded useful wake energy whenever the camera crossed a single quantized cell.
Solution: Add a toroidal ring-offset model. Quantized AUP center deltas are converted into integer cell shifts; `DecayFloraForcesJob`, `AccumulateFloraForcesJob`, `MockDisplacementInjectorJob`, editor gizmo sampling, and `Hecton_IndirectVegetation.shader` all translate logical cells through the same modulo physical index. Newly exposed wrapped rows/layers are cleared in the decay pass; full reset is reserved for resolution changes, cell-size changes, invalid previous center, or shifts equal to or larger than the active resolution.
Rejected Alternatives: Main-thread memmove of the 64^3 field, full buffer reset for every one-cell recenter, CPU readback/repack of the GPU buffer, or allowing stale rows to smear after camera movement.
Scalability potential: Low quality keeps coarse 16^3 wrapping with minimal row clears; Middle/High progressively preserve more local wake history; Ultra keeps 64^3 displacement continuity while only clearing exposed surfaces on ordinary motion.
Hardware Impact: On one-cell recenter, avoids discarding/rebuilding the whole active field. Exact microseconds are not profiler-measured; static complexity changes from full reset on ordinary recenter to modulo-preserved field with only exposed slices zeroed inside the existing Burst linear pass.

Problem: Compile proof remains required after the modulo-ring patch, but the workstation still violates the explicit build gate.
Solution: Rechecked CPU and compiler process state. CPU samples were `52.5`, `38.1`, `75.9`; no `dotnet`/`csc` process was active. No build was launched because the sample window exceeded 50%.
Rejected Alternatives: Launch dotnet during a >50% sample window, or report compile success from static scans.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local compilation from competing with the current parallel-agent workload.

## 2026-05-19 Ultra Polish Pass 8

Problem: The first modulo shader patch rounded `_HectonFloraSwayFieldRingOffset` inside `SampleFloraSwayFieldCell`, so ultra trilinear sampling could repeat the same global round for up to eight taps.
Solution: Compute the integer ring offset once inside `ResolveFloraSwayFieldOffset` and pass it into every nearest/trilinear `SampleFloraSwayFieldCell` call.
Rejected Alternatives: Trust shader compiler common-subexpression elimination, or leave repeated ALU hidden behind the visual feature.
Scalability potential: Low nearest mode saves a small constant; High/Ultra trilinear mode avoids repeated ring-offset conversion across eight structured-buffer taps.
Hardware Impact: Not profiler-measured; removes redundant scalar/vector conversion work from the vertex shader path.

Problem: The scheduler treated `_floraSwayFieldActive == false` as a center-change proxy, causing an inactive but valid centered field to request a reset even when a wake simply started.
Solution: Split the predicates. `centerChanged` now tracks AUP validity and integer cell shift only; `wakeStarted` schedules immediate response when a valid inactive field receives active wakes without forcing a full reset.
Rejected Alternatives: Preserve the blunt inactive reset or delay first wake response until the cadence timer expires.
Scalability potential: Low devices avoid unnecessary reset churn after quiet periods; Ultra keeps immediate wake response without discarding a valid toroidal field.
Hardware Impact: Prevents full active-range reset on wake start when the center is valid; exact microseconds pending profiler proof.

Problem: Compile proof remains required after pass 8.
Solution: Rechecked the build gate. CPU samples were `20.6`, `15`, `22.4`, but seven external `dotnet` processes were active, so no build was launched.
Rejected Alternatives: Start another dotnet build while other compiler/runtime processes are already active.
Scalability potential: No runtime effect.
Hardware Impact: Prevents compile contention with concurrent agents.

## 2026-05-19 Ultra Polish Pass 9

Problem: The toroidal grid could be verified by source scan, but a crash dump could not distinguish a full reset from a preserved wrapped recenter without reconstructing control flow from logs.
Solution: Add owner-local reset/wrapped-shift telemetry flags and mix the current ring offset plus last center-shift cells into the existing `FloraSwayFieldTelemetryEntry.StateHash`. The DTO remains 64 bytes; no new Vault buffer or managed allocation is introduced.
Rejected Alternatives: Expand the telemetry DTO beyond one cache line, add a second telemetry buffer, or write managed text per frame.
Scalability potential: Low/Middle/High/Ultra all use the same forensic route; quality changes field density/cadence, not the dump ABI.
Hardware Impact: Adds seven integer FNV-style hash mixes only when a black-box entry is recorded. No per-node or shader cost; exact microseconds pending profiler proof.

Problem: Compile proof remains required after pass 9.
Solution: Rechecked the build gate. CPU samples were `82`, `91`, `92.9`, and seven external `dotnet` processes were active, so no build was launched.
Rejected Alternatives: Start another build while other agents still hold dotnet processes.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local compile contention.

## 2026-05-19 Ultra Polish Pass 10

Problem: The field scaled continuously, but resolution and cell-size were derived directly from current `GlobalQualityWeight`, so tiny thermal/profiler jitter could force repeated active-range resets and GPU upload shape changes.
Solution: Split runtime quality from layout quality. Cadence, source count, displacement gain, and shader interpolation still consume current `GlobalQualityWeight`, while `ResolveFloraSwayLayoutQualityWeight()` commits resolution/cell-size changes only after a `0.035` quality delta using `math.step`.
Rejected Alternatives: Binary low/high tiers, per-frame cell-size rebuilds, or a delayed coroutine/timer allocation path.
Scalability potential: Low/Middle/High/Ultra still breathe continuously in cost and visual response; only expensive topology/layout changes get hysteresis to prevent visible reset churn.
Hardware Impact: Avoids repeated full field resets and 64^3 upload-shape churn during small quality oscillations. Adds one scalar field and constant-time math; exact microseconds pending profiler proof.

Problem: Compile proof remains required after pass 10.
Solution: Rechecked the build gate. CPU samples were `100`, `100`, `100`; no `dotnet`/`csc` process was visible, but CPU alone blocks compilation.
Rejected Alternatives: Launch build on a saturated machine because compiler processes were absent.
Scalability potential: No runtime effect.
Hardware Impact: Prevents compile contention during current system saturation.

## 2026-05-19 Ultra Polish Pass 11

Problem: The flora sway Vault IDs `71580..71584` were not owner-local in the current checkout. Static source scan found SHINOBU_155 physiology respawn already owns `71580..71589` in `Assets/_Project/Scripts/Physiology/ShinobuRespawnData.cs`, so SHINOBU_124 could alias respawn state, telemetry, tuning, or request buffers.
Solution: Move the active flora sway Vault IDs to `71650..71654`: displacement field `71650`, metadata `71651`, 300-frame black box `71652`, stiffness rules `71653`, and CSV scratch `71654`. Focused source/doc scan found no `BufferID` owner for that range; incidental `7165xx` values in generated hash files are not Vault handles.
Rejected Alternatives: Keeping the old range and relying on boot order, adding the IDs to the shared `BufferID` enum during a multi-agent batch, or sharing physiology buffers because they happen to have the same numeric value. All three break one fact -> one owner -> one route.
Scalability potential: Runtime math does not change. Low/Middle/High/Ultra quality curves still control resolution, cadence, source budget, gain, and shader interpolation; the fix prevents cross-domain memory aliasing at every tier.
Hardware Impact: Prevents undefined data corruption and false black-box evidence. Frame-time delta is 0 us by design; the gain is correctness and avoiding catastrophic Vault alias stalls/crashes on i3/MX350 and ARM64 devices.

## 2026-05-19 Ultra Polish Pass 12

Problem: `ClearFloraSwayDisplacementField()` and `OnOriginShift()` could force-complete an in-flight flora displacement job even when the goal was only to disable or invalidate presentation data. That violates the dependency-chain rule and can stall the main thread during origin shifts.
Solution: Add `_floraSwayFieldDiscardScheduledUpload`. Clear/origin-shift now publishes inactive shader globals immediately; if the field job is still running, its upload is skipped after natural completion. Metadata is zeroed only when no job is writing it or when the scheduled job is already completed, avoiding a main-thread write race with the final metadata job.
Rejected Alternatives: Force `JobHandle.Complete()` during clear/origin shift, write metadata while `UploadDisplacementTextureJob` may still be running, or let stale completed data upload after the field was explicitly disabled.
Scalability potential: Low/Middle/High/Ultra all keep the same visual math. The fix matters more on low hardware because a 64^3 field job is more likely to collide with an origin/clear event under frame pressure.
Hardware Impact: Removes a potential main-thread wait from clear/origin-shift presentation invalidation. Exact microseconds are not profiler-measured; worst-case avoided cost is waiting for the remaining Burst field chain plus a stale GPU upload.

## 2026-05-19 Ultra Polish Pass 13

Problem: The discard-only path avoided a main-thread stall but could silently erase the forensic difference between a deliberately discarded stale upload and a normal quiet frame.
Solution: Add `FloraSwayFieldDiscardedUploadFlag` and `RecordDiscardedFloraSwayFieldUpload()`. Completed-but-cleared and in-flight-discarded jobs now write one 64B black-box entry before pending wake count, ring offset, center-shift cells, and pending AUP are cleared. The helper reads completed metadata only after the job handle is done, validates finite `float4` metadata before using it, and falls back to `VaultMissing`/`NaN` flags instead of trusting corrupt values.
Rejected Alternatives: Treat discard as a non-event, dump managed text on clear, expand `FloraSwayFieldTelemetryEntry`, or upload stale data just to preserve a visible state sample.
Scalability potential: Low/Middle/High/Ultra all keep the same 16B field and 64B telemetry ABI. Weak devices get the same non-blocking discard behavior; high/ultra keep forensic proof when dense 64^3 updates are invalidated by origin shifts.
Hardware Impact: Adds one constant-size telemetry write only when a scheduled upload is intentionally discarded. Frame-path savings from pass 12 remain; no new per-node work, no GPU upload, and no managed allocation are introduced.
