# SHINOBU_238 Rationale

Status: PENDING VERIFICATION

## Decision 00 - Authority Lane

Problem: Prompt demands global biolum oscillator, GPU matrix upload, shader spatial waves, and DataVault-backed presentation state while other agents may own celestial, apex AI, and rollback.

Solution: Work inside existing biolum/rendering lanes; add presentation-only DTOs, Burst jobs, shader globals, and editor/test facades without changing gameplay truth ownership. Runtime phases: PRE_SIMULATION for scalar/job inputs, POST_SIMULATION for telemetry, VISUAL_SYNC for shader upload.

Rejected Alternatives: Per-flora MonoBehaviours and per-material mutation are rejected because they clone materials, break SRP batching, and scale with visible object count. Direct dependency on unfinished Celestial/Apex systems is rejected; fallback DTO inputs and cold interfaces keep the route decoupled.

Scalability potential: Low uses one global matrix and vertex wave interpolation. Middle keeps spatial wave math and darkness scalar. High adds stronger per-pixel blend. Ultra spends saved CPU on richer shader interference through continuous quality weight.

Hardware Impact: MX350/i3 path targets one 64-byte matrix upload per frame plus one 1-element Burst job, estimated under 10 us CPU excluding Unity render-thread internals. The old per-renderer SetFloat path would scale by flora count and is unbounded.

## Decision 01 - Mandate Set

Problem: Task touches hot path rendering, Burst jobs, data layout, AUP precision, shader global authority, and designer tooling.

Solution: Mandates selected before coding: zero-GC, ARM64 DTO layout, native jobs, AUP precision, deterministic AUP sync, dispatcher phases, instanced flora rendering, GPU sovereignty.

Rejected Alternatives: Reading every mandate is noise and risks cross-domain drift. Reading only shader rules misses DTO, jobs, and AUP constraints.

Scalability potential: Mandate set covers low through ultra because it links CPU, GPU, and AUP precision gates.

Hardware Impact: Reduces risk of hidden GC, unaligned 64-byte payloads, shader jitter, and per-object CPU traversal on low-end silicon.

## Decision 02 - Matrix Constant Buffer Route

Problem: The runtime already had a global biolum matrix path, but the scheduled oscillator referenced sync pulse arrays without resolving or locking them, creating a compile/runtime wall before the GPU route could be trusted.

Solution: Keep the existing single-owner `BiolumPulseSyncRuntime` and repair the DataVault lock chain for pulse state, profiles, weather, predator signal, sync pulses, and pulse ages. The job now receives all unmanaged arrays explicitly and the VISUAL_SYNC lane publishes one `_GlobalBiolumDearLieGroups` matrix.

Rejected Alternatives: Adding per-renderer property blocks or per-material floats was rejected because it scales with visible flora count and breaks the "one fact -> one owner -> one route" rule. Adding a new runtime beside the existing one was rejected because it would create dual authority over the same shader globals.

Scalability potential: Low uses the four-row matrix and vertex interpolation. Middle uses the same matrix with depth/ambient activation. High and Ultra use the same upload while the shader blends into per-pixel interference through `GlobalQualityWeight`.

Hardware Impact: i3/MX350 path stays one matrix upload plus one 4-row job. Estimated CPU saving versus 10,000 renderer mutations: 200-600 us/frame, with GC clone risk removed.

## Decision 03 - Local AUP Shader Coordinates

Problem: Fragment biolum waves were evaluating against absolute world coordinates, which can phase-jitter when the floating origin/AUP offset grows.

Solution: Keep the CPU matrix as phase/frequency/amplitude/offset only. The shader now names the input `localAupCoord` and computes fragment waves from `input.positionWS - input.originWS`; vertex prepass keeps the stable AUP seed path already present.

Rejected Alternatives: Passing absolute `double3` to GPU is impossible and passing absolute float world coordinates is precision debt. Uploading per-instance epicenters for every plant was rejected as a false simulation.

Scalability potential: Low gets cheap local vertex wave. Middle keeps localized fragment wave. High adds secondary interference. Ultra uses the same local coordinate basis for filament detail without CPU growth.

Hardware Impact: Zero CPU cost. GPU ALU cost is quality-weighted and avoids precision artifacts that would otherwise force expensive corrective systems.

## Decision 04 - Depth Darkness Without Celestial Dependency

Problem: The prompt requires darkness response from eclipse/daylight and physical depth, but the Celestial owner is another agent and cannot be pulled into this compile surface.

Solution: Treat ambient level in `MockWeatherSignal` as the cold Celestial bridge and add AUP-depth darkness from the runtime AUP reference. The oscillator takes the max of ambient/eclipse darkness and depth darkness before multiplying amplitude.

Rejected Alternatives: Hot `GlobalRegistry` polling for Celestial/player transforms was rejected. Direct Agent 129 types were rejected because they introduce a dependency on code outside the assigned lane.

Scalability potential: Low disables glow through one scalar multiply. Middle responds to trenches through depth. High/Ultra spend the recovered visibility budget on richer shader waves.

Hardware Impact: Adds a few scalar operations in a 4-row Burst job; estimated below 0.1 us/frame on i3/MX350.

## Decision 05 - Editor Layout And Telemetry Facades

Problem: Runtime layout checks existed, but Task 04 requires editor-time failure, and the tuner displayed pulse boxes without reading the black-box telemetry ring directly.

Solution: Add `BiolumPulseLayoutGuard` in the editor assembly to assert DTO sizes and offsets. Use `CopyEditorTelemetryEntries(Span<BiolumPulseTelemetryEntry>)` and draw a preallocated 16-bar telemetry graph in the Abyssal Glow Tuner.

Rejected Alternatives: Reflection-only runtime logging was too late. `TryReadEditor*` naming was rejected because these editor facades lock Vault buffers and copy snapshots. Allocating graph rows per refresh was rejected; the UI elements and scratch array are allocated once during `CreateGUI`.

Scalability potential: Low-end builds pay zero editor cost. High-end editor sessions can inspect telemetry without changing runtime data ownership.

Hardware Impact: Player runtime unchanged except editor copy facades; editor-only visualization has no shipping frame cost.

## Decision 07 - Accessor Purity And Mock Burst Entry

Problem: Ultra-polish audit found two correctness risks: editor methods with read-like names were locking DataVault buffers, and cold mock seeding called `IJob.Execute()` directly, bypassing the job-system entry point for a Burst-labeled kernel.

Solution: Rename editor snapshot APIs to `CopyEditor*`, copy telemetry through one caller-owned `Span<T>`, and change the cold mock seed to `job.Run()`. The hot oscillator remains scheduled through the dispatcher-owned job fence.

Rejected Alternatives: Keeping `TryReadEditorTelemetryEntry` was rejected because read accessors must be pure. Scheduling a one-row mock seed job and completing it was rejected as a tiny sync job with no profiler case.

Scalability potential: Low through Ultra shipping runtime is unchanged; editor diagnostics become cheaper and stricter. Cold mock data remains available without pulling Celestial/Apex compile dependencies.

Hardware Impact: Player runtime cost unchanged. Editor telemetry graph avoids 15 redundant Vault lock attempts per refresh. Cold boot mock seed remains bounded to one 64-byte state write.

## Decision 08 - Verification Boundary

Problem: The protocol requires compilation, but the same protocol forbids launching dotnet/compile when CPU is above 50 percent or another dotnet/csc process is active.

Solution: Run static scans, diff whitespace checks, process checks, and CPU checks. Do not launch dotnet while CPU reports 100 percent; an earlier same-pass guard also saw active `dotnet` PID `29148`, while the latest process scan returned no dotnet/csc output. Record this as a policy block, not a pass.

Rejected Alternatives: Running `dotnet build` under 100 percent CPU was rejected because it violates the explicit batch protocol and risks competing with other agents; running beside PID `29148` was rejected while that process existed.

Scalability potential: Build verification can resume when CPU drops; no architecture decision depends on skipping it.

Hardware Impact: Avoids adding load to an already saturated workstation.

## Decision 09 - Shader Consumer AUP Sweep

Problem: Ultra-polish scan showed the matrix route was not consumed only by `Hecton_IndirectVegetation.shader`. Coral, kelp, sargassum, and procedural bio shaders also selected matrix rows and filament waves from absolute `positionWS` plus `_GlobalBiolumAupOffset.x/z`, which can drift or pop when the floating origin/AUP offset grows.

Solution: Keep the same four-row matrix ABI and rewrite assigned flora/coral/biostructure shader consumers to derive selector and filament waves from local finite coordinates only. Indirect vegetation uses per-instance `positionWS - originWS`; standard mesh shaders use current floating-origin `positionWS` as the local coordinate and no longer add `_GlobalBiolumAupOffset` into phase math.

Rejected Alternatives: Subtracting `_GlobalBiolumAupOffset` inside ordinary object shaders was rejected after review because their `positionWS` is already floating-origin local; subtracting the accumulated origin offset can recreate a large absolute coordinate. Editing Leviathan/fish shaders was rejected as fauna-domain work without a SHINOBU_238 route card.

Scalability potential: Low tier still samples the same matrix row with cheap triangle/selector math. Middle keeps local spatial variation. High and Ultra spend ALU on filament/secondary-state blends without CPU growth or absolute coordinate jitter.

Hardware Impact: CPU cost unchanged. GPU ALU count is effectively unchanged, but high-magnitude float operands are removed from the assigned flora/coral/bio shader phase path, reducing precision-risk at large map offsets.

## Decision 10 - Generated Project File Caveat

Problem: Static compile-wall scan found `Hecton8.Core.csproj` includes `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` even though the authoritative Unity assembly definition is `Hecton8.VFX.Bioluminescence.Runtime.asmdef`.

Solution: Record the caveat and leave generated project metadata untouched. The Unity asmdef route remains the source of assembly authority for this pass.

Rejected Alternatives: Editing `Hecton8.Core.csproj` was rejected because generated project files are not the owner source for Unity asmdef routing and other agents may be depending on current generated project state.

Scalability potential: No runtime behavior change. Correct project-file regeneration would reduce editor/IDE compile-wall noise without changing the biolum route.

Hardware Impact: No frame-time impact. Potential iteration-time impact only if local dotnet builds consume stale generated project includes.

## Decision 11 - Post-Compaction Proof Discipline

Problem: Context compaction removed live chat detail, and a transient prompt-counter command searched for XML `<task id>` elements even though the source SHINOBU_238 prompt uses literal `Task 01` through `Task 20` headings.

Solution: Re-read `Status_SHINOBU_238.md`, `Rationale_SHINOBU_238.md`, and the SHINOBU_238 block from `CURRENT_BATCH.md`; treat the on-disk prompt hash `9d5db96674f0d27a` and task headings as the authority. Re-run assigned static scans and CPU build guard before any final report.

Rejected Alternatives: Trusting compressed chat memory was rejected. Updating generated `.csproj` metadata was rejected. Launching compile/import under 100 percent CPU was rejected by the explicit no-build guard.

Scalability potential: No runtime algorithm change. The discipline protects the biolum route from accidental cross-domain edits and preserves the continuous quality path already implemented.

Hardware Impact: No frame-time impact. Build was deliberately not launched while CPU was saturated, preventing additional local IO and compiler contention.

## Decision 12 - Legacy Global Float Retirement

Problem: `HectonBiolumController` still published `_HectonLegacyBiolumIntensity` and `_BiolumPulseTime` through `Shader.SetGlobalFloat`, including `Time.time` on Atlas/sonar pulses. Live source scan found no first-party shader readers for either property, while SHINOBU_238 now owns the matrix/vector biolum route.

Solution: Remove the dead shader float property IDs, the `ApplyShader` method, all `Shader.SetGlobalFloat` calls, and the biolum `Time.time` pulse writes from `HectonBiolumController`. Keep the controller's registration and local proxy-light response intact because that is world/lore behavior outside the matrix constant-buffer route.

Rejected Alternatives: Keeping the dead writes was rejected because it preserves a second apparent shader authority and violates the "one fact -> one owner -> one route" rule. Deleting the whole controller was rejected because it still handles world event registration and authored local `Light` proxies. Re-routing those lights to SHINOBU_238 was rejected as cross-domain scope creep.

Scalability potential: Low through Ultra now share one shader authority. Weak devices avoid stray global-property writes during signal/sonar bursts. High and Ultra spend the same visual budget in the matrix-driven shader path instead of an unconsumed scalar route.

Hardware Impact: Removes up to two `Shader.SetGlobalFloat` calls on burst events and one slow-tick scalar write. Estimated event-frame saving is 0.2-2 us depending on Unity property upload state; the primary gain is authority hygiene and deterministic visual timing.

## Decision 13 - Mock Seed Lock Narrowing

Problem: Manual diff review found `GenerateMockLightingState()` locking sync pulse and pulse-age buffers even though the cold mock lighting seed does not read or write sync pulse rows.

Solution: Remove those two locks from the mock seed path. Keep sync pulse locks in initialization, pulse injection, expiration, and the scheduled oscillator where those buffers are actually consumed.

Rejected Alternatives: Keeping the extra locks was rejected because it widens DataVault contention surface without data ownership need. Scheduling a separate validation job was rejected as a tiny same-frame job with no profiler proof.

Scalability potential: Runtime visual scalability is unchanged. Low-end/editor boot paths do less Vault locking while high/ultra keep the same shader matrix behavior.

Hardware Impact: Cold boot/editor seed avoids two DataVault lock attempts. No steady hot-frame delta, but lower risk of avoidable lock contention during bootstrap.

## Decision 14 - Vault Rebind Fence And Editor Reload Discipline

Problem: Manual lifecycle review found two edge risks. The editor `Reload Biolum Profiles` context menu reused teardown force-completion, and Vault generation/service rebinding could release cached handles while `_stateJobScheduled` or `_jobLocksHeld` still referenced the old NativeArray views.

Solution: The editor reload route now only finalizes an already completed job and returns if the job is still running. Vault rebinding and generation-mismatch refresh paths now fence scheduled jobs before invalidating handles.

Rejected Alternatives: Leaving handle release unfenced was rejected because it can leak DataVault locks and stale NativeArray views under compaction or hot service replacement. Force-completing for manual editor reload was rejected because it is not teardown and can create a visible editor hitch.

Scalability potential: Normal Low/Middle/High/Ultra frame behavior is unchanged. Rare Vault relocation/hotswap paths become fail-safe without adding hot polling or new dependencies.

Hardware Impact: No steady frame cost. Prevents rare stalled locks/stale-pointer crashes during Vault regeneration. Editor reload avoids a possible blocking completion on weak CPUs.

## Decision 15 - Support Vector Authority Fence

Problem: After retiring `_HectonLegacyBiolumIntensity` and `_BiolumPulseTime`, a separate world biolum manager still contains fallback writes to `_BiolumIntensity`, which assigned shaders use as a support vector beside the matrix route.

Solution: Keep `BiolumPulseSyncRuntime` as the active owner of `_BiolumIntensity` whenever `_GlobalBiolumParams.x > 0.5`. Source review shows `HectonBiolumManager.IsGlobalPulseSyncOwningLegacyIntensity()` suppresses both update and reset writes in that state. Rename the PulseSync helper from `ResolveLegacyBiolumIntensity` to `ResolveMatrixDerivedBiolumIntensity` so the route name matches the actual owner.

Rejected Alternatives: Deleting `HectonBiolumManager` fallback publication was rejected because it can still serve non-PulseSync fallback scenes when the matrix owner is inactive. Moving world-zone color ownership into PulseSync was rejected as cross-domain scope creep. Keeping "legacy" naming was rejected because it advertises a dead route.

Scalability potential: Low through Ultra matrix mode now has one support-vector owner and one matrix owner. Fallback mode remains available only when PulseSync is not publishing active group rows, so weak-device fallback scenes do not lose biolum entirely.

Hardware Impact: Matrix-active frames avoid one competing `Shader.SetGlobalVector` call from the manager. Estimated direct saving is sub-microsecond to 1 us on affected frames; the larger impact is preventing last-writer-wins flicker between matrix-derived intensity and world-zone fallback intensity.

## Decision 16 - Mock Job Mutability Contract

Problem: Static alias review initially treated `GenerateMockLightingStateJob` weather and predator rows as sampled inputs, but source review showed the cold mock seed intentionally mutates `WeatherSignal[0]` and `PredatorSignal[0]`.

Solution: Keep those two fields write-capable with `[NoAlias]`; only profile floats are `[ReadOnly, NoAlias]` in the mock job. The oscillator job remains stricter because it samples weather, predator, sync pulses, and ages without writing them.

Rejected Alternatives: Adding `[ReadOnly]` to weather/predator in the mock job was rejected because it would cause a compile break and violate Task 05's synthetic mock-state injection. Splitting mock weather seeding into another tiny job was rejected because it adds scheduling overhead without runtime value.

Scalability potential: Shipping hot path is unchanged. Low-end cold boot/editor seed remains a single bounded job that seeds mock darkness/predator state without external Celestial/Apex dependencies.

Hardware Impact: No steady frame cost. Avoids a compile-breaking annotation while preserving Burst alias information through `[NoAlias]`.

## Decision 17 - Matrix ABI Capacity Wording

Problem: The SHINOBU route card still said "8 group rows" even though the live shader ABI is a single `Matrix4x4`, which exposes exactly four `float4` rows. The runtime separately keeps 16 cold profile slots for authored profiles.

Solution: Correct the route card to distinguish 4 shader matrix rows, 16 cold profile slots, 16 sync pulse rows, and 300 telemetry rows.

Rejected Alternatives: Widening the GPU payload to two matrices was rejected because Task 07 mandates one global `float4x4` Dear Lie matrix and the existing shaders consume four groups. Leaving the stale documentation was rejected because it invites future ABI drift.

Scalability potential: Low through Ultra quality levels keep the same 64-byte matrix ABI. Additional authored profiles remain cold tuning slots that are mapped into the four active shader groups, not extra hot shader rows.

Hardware Impact: No runtime cost. Prevents an accidental 128-byte/two-matrix route that would double global payload and shader branch surface.

## Decision 18 - Shader Matrix Row ABI Color Fix

Problem: Assigned coral, kelp, sargassum, and procedural bio shader consumers read `_GlobalBiolumDearLieGroups[row].rgb` as an RGB color and `.w` as intensity. The live CPU/GPU ABI defines each row as `phase, frequency, amplitude, spatialOffset`, so those reads converted phase/frequency/amplitude into color and spatial offset into brightness.

Solution: Keep the one-matrix ABI unchanged. Add local deterministic group tint helpers to the six affected assigned shaders and read intensity from `.z`. Preserve `.w` exclusively for spatial wave offset where the shader actually needs it.

Rejected Alternatives: Packing color into the matrix was rejected because it would overwrite phase/frequency/amplitude or require a second constant payload. Reusing material colors for group identity was rejected because it loses the global Dear Lie group signal and can vary per material. Editing fauna/Leviathan consumers remains rejected as external-domain work.

Scalability potential: Low tier gets stable group tint with the same cheap matrix row selection. Middle keeps spatial variation. High and Ultra spend quality-weighted shader ALU on overdrive/filament waves without expanding CPU uploads or shader constants.

Hardware Impact: CPU cost unchanged. GPU cost is a small branchless `lerp/step` tint helper already present in indirect vegetation; it replaces invalid color reads and prevents visible hue/flicker corruption caused by phase/frequency data.

## Decision 19 - Emergency Mock Glow Buffer Fill

Problem: The runtime requested 50,000 uninitialized mock glow/AUP rows but `GenerateEmergencyMockGlows()` seeded only four rows because it used `SyncGroupCount`. That leaves most fallback rows undefined and makes black-box telemetry report shader group capacity as active instance count.

Solution: Seed up to `MaxGlowInstances` rows during the cold mock path and track `_activeGlowingInstanceCount`. Telemetry now records the seeded instance count, clamped to the fixed capacity.

Rejected Alternatives: Clearing the entire buffer to zero was rejected because the prompt requires a useful fallback/mock data generator and the allocator intentionally uses uninitialized memory. Scheduling a new hot job was rejected; this is a cold bootstrap/editor seed, not a recurring gameplay lane. Reporting `SyncGroupCount` as active instances was rejected because it confuses four matrix rows with instance coverage.

Scalability potential: Low tier still pays no per-frame instance CPU work; all apparent per-instance variety remains shader-side. Middle through Ultra can use the fully seeded mock data for editor/CI diagnostics without adding material or GameObject traffic.

Hardware Impact: Hot path cost is unchanged. Cold bootstrap performs a bounded 50,000-row unmanaged fill; rough cost target is sub-millisecond to a few milliseconds on i3/MX350 depending on memory bandwidth, outside gameplay cadence. Telemetry adds one integer clamp below measurable frame cost.

## Decision 20 - First 20 Minutes Route Binding

Problem: The product contract requires every global route to name the First 20 Minutes moment it serves. The biolum route is presentation-heavy and would be rejectable as visual breadth if it did not tie directly to route readability.

Solution: Bind SHINOBU_238 to World load, Swim, and Hazard readability on the selected Copper Wire route. The matrix route makes the first abyss/coral/flora read visible without per-material mutation and supports darkness perception without gameplay truth ownership.

Rejected Alternatives: Broad "ecosystem beauty" justification was rejected because it does not answer the product route gate. Expanding into fauna/Leviathan presentation was rejected because it is not required to prove the selected route and crosses domain ownership.

Scalability potential: Low tier gets readable glowing route landmarks with one matrix. Middle keeps localized waves. High and Ultra add richer filament/interference only where the route capture can prove value.

Hardware Impact: Documentation-only decision; no frame cost. It constrains future work away from route-irrelevant visual expansion.

## Decision 21 - Object-Relative Shader Coordinates

Problem: Sidecar review found standard coral, kelp, sargassum, and procedural-bio shaders still stored `positionWS` in `biolumLocalAupCoord`. Although `_GlobalBiolumAupOffset` was no longer injected, `positionWS` still makes the global-biolum phase depend on object placement instead of local vertex coordinates.

Solution: Store object-relative finite deltas in the varying: `positionWS - TransformObjectToWorld(0)` for standard objects and drift-aware `positionWS - biolumOriginWS` for sargassum.

Rejected Alternatives: Keeping floating-origin `positionWS` was rejected because it is not a local vertex coordinate. Uploading per-object AUP seeds was rejected because it expands CPU/GPU payload and defeats the Dear Lie route.

Scalability potential: Low tier keeps a cheap local vertex wave. Middle keeps stable spatial variation. High and Ultra add shader interference on the same local basis without CPU growth.

Hardware Impact: Adds one vertex subtraction in affected shaders. CPU cost is zero. The exchange removes precision/phase risk without adding buffers.

## Decision 22 - Editor-Only CSV Reload Gate

Problem: Sidecar review found `Tick()` reached CSV file I/O via `ApplyCsvOverridesIfReady()`, including `File.Exists`, timestamp checks, and `FileStream` allocation/open. That violates the shipping hot-path zero-GC/no-I/O claim.

Solution: Gate CSV watcher setup, teardown, and apply calls under `UNITY_EDITOR`. Designer CSV hot reload remains an editor tooling bridge. Player/runtime uses baked binary/default profile data and never polls CSV from `Tick()`.

Rejected Alternatives: Shipping per-frame file polling was rejected. Moving file I/O to another runtime tick phase was rejected because the defect is I/O in gameplay cadence, not phase placement. Creating a background runtime loader was rejected for this pass because DataVault writes must remain owner-controlled and the route already has binary profile fallback.

Scalability potential: Low through Ultra player runtime has identical hot-path behavior. Editor sessions keep designer control without changing player memory ownership.

Hardware Impact: Player hot path removes possible filesystem stall/allocation. Editor-only reload cost is cold/diagnostic and not part of the frame contract.

## Decision 23 - Retire `_GlobalBiolumPhase`

Problem: The shared shader bridge still published `_GlobalBiolumPhase` as a scalar fallback when the dispatcher was inactive, and the global dispatcher also set it. Full source scan showed no live shader readers, only dead declarations in coral shaders.

Solution: Remove the scalar property ID and scalar publication from `HectonShaderGlobalDataVaultBridge` and `GlobalShaderDispatcher`, and remove the dead coral shader declarations. `_BiolumMasterPhase` vector remains as the support vector, while `_GlobalBiolumDearLieGroups` remains the matrix payload.

Rejected Alternatives: Keeping the scalar was rejected because it creates a ghost biolum authority. Removing `_BiolumMasterPhase` was rejected because assigned shaders still use it as a support vector separate from the matrix row ABI.

Scalability potential: Low through Ultra keep the same matrix/vector route. No quality tier depends on the retired scalar.

Hardware Impact: Removes one unused global scalar write from the biolum master-phase path. Exact CPU/render-thread delta requires Unity profiler proof.

## Decision 24 - Route Card Phase Accuracy

Problem: The route card claimed formal POST_SIMULATION and VISUAL_SYNC behavior, while current code uses `IUpdatable.Tick` and `ILateFrameTickable.LateFrameTick`.

Solution: Downgrade the route card to the exact current dispatcher interfaces: tick consumes signals, updates support scalars, schedules jobs, and records telemetry; late frame finalizes completed jobs and publishes the matrix.

Rejected Alternatives: Leaving overclaimed phase wording was rejected. Refactoring dispatcher phases in this pass was rejected because it would touch broader engine contracts without proof.

Scalability potential: Documentation accuracy only; runtime quality behavior unchanged.

Hardware Impact: No frame cost. Prevents false integration assumptions.

## Decision 25 - Player CSV Compilation Surface Trim

Problem: The first sidecar fix gated CSV reload calls under `UNITY_EDITOR`, but the runtime class still compiled the `FileSystemWatcher`, CSV path state, timestamp state, and `FileStream` read path into player builds as private unused members/methods.

Solution: Wrap the watcher state and the file-I/O methods from watcher setup through CSV scratch read/path resolution in `UNITY_EDITOR`. Keep the unmanaged byte parser available because it is allocation-free tooling code and already isolated from player call sites.

Rejected Alternatives: Leaving private file-I/O methods in player compilation was rejected because the mandate is stricter than "not called in Tick"; the player runtime should not carry a designer hot-reload bridge. Removing CSV scratch/profile parser entirely was rejected because Task 18 requires the human-readable tuning bridge for editor/CI workflows.

Scalability potential: Low through Ultra player runtime now has no CSV watcher or file-stream route in the SHINOBU runtime class. Editor sessions keep hot reload without changing DataVault layout or shader authority.

Hardware Impact: No steady frame cost. Reduces player compilation/link surface and removes accidental future call risk; editor-only reload cost remains cold/diagnostic.

## Decision 26 - Fixed Matrix Row Count, Continuous Fidelity

Problem: `ResolveStateCount(HectonQualityTier)` kept a discrete tier switch that could publish only one row during cold-start/default tier while the hot matrix route later publishes four rows. That creates a possible visual pop and contradicts the continuous `GlobalQualityWeight` rule.

Solution: Remove `_activeStateCount` and `ResolveStateCount`. The shader ABI is always the fixed four-row `Matrix4x4`; low-device degradation happens through continuous cadence, darkness/amplitude, and shader quality weights, not by changing row count.

Rejected Alternatives: Keeping 1/4/16 tier branches was rejected because matrix capacity is fixed at four rows and high/ultra cannot expose sixteen rows through one `Matrix4x4`. Adding a second matrix was rejected because the prompt requires a single matrix constant-buffer route.

Scalability potential: Low tier still collapses work through 5Hz cadence and lower amplitude/fragment weighting. Middle/high/ultra keep the same row ABI and increase visual richness through shader math, avoiding layout or authority changes.

Hardware Impact: No steady-frame cost. Removes one stale cold-path switch and prevents a row-count pop without increasing the 64-byte GPU payload.

## Decision 27 - Shader Variant Surface And Cold I/O Boundary

Problem: The shader changes touched several assigned flora/coral/procedural-bio shaders, so they had to be checked for accidental new variants and first-use warmup debt. The CSV tuning bridge also had to be proven editor-only after moving it out of the player tick route.

Solution: Diff-scan assigned shaders for added `#pragma`, `multi_compile`, and `shader_feature` lines; no additions were found. Keep all quality behavior in scalar/vector/matrix values driven by `GlobalQualityWeight`. Verify CSV watcher state, apply route, path resolution, and CSV `FileStream` read are wrapped in `UNITY_EDITOR`; leave binary profile loading as cold boot and black-box dump as fault-only.

Rejected Alternatives: Adding shader keywords for low/high biolum was rejected because it creates variant warmup debt and binary quality behavior. Shipping CSV polling or watcher code in the player runtime was rejected because designer hot reload is an editor tooling bridge, not gameplay cadence.

Scalability potential: Low through Ultra all use the same compiled shader route and one matrix ABI. Fidelity scales through continuous weights, cadence, amplitude, and shader ALU, not keyword permutations.

Hardware Impact: No direct frame-time change. Avoids adding first-use variant compilation risk from this pass and removes player-side CSV filesystem stall surface.

## Decision 28 - Asynchronous Black-Box Dump Writer

Problem: Sidecar audit found `DumpBlackBox()` still performed `Path` construction, directory creation, and `FileStream` writes directly from NaN/overrun/AUP-invalid fault call stacks. Fault-only is not enough; it was still reachable from `Tick()` and `LateFrameTick()`.

Solution: Allocate the dump paths, signal, and writer thread during owner enable, while the 9,616-byte staging buffer is owned by Vault buffer `70312` after the later H-PHI correction. On fault, `DumpBlackBox()` copies the current header plus 300 telemetry rows into that preallocated Vault scratch and signals `H8_SHINOBU_238_BlackBoxDump`; the background writer owns `Directory.CreateDirectory` and `FileStream` work. Teardown signals and joins the writer outside the gameplay tick lane.

Rejected Alternatives: Keeping synchronous file I/O in the fault frame was rejected because it can stall the main thread during an already unstable frame. Making dumps editor-only was rejected because the black-box mandate requires a forensic artifact. Allocating the snapshot buffer on fault was rejected because fault paths must not create managed garbage.

Scalability potential: Low through Ultra frame behavior is unchanged. Fault forensics stay available, but disk I/O no longer competes with the frame that detected the fault.

Hardware Impact: Steady frame cost is unchanged. Fault frame cost drops to a bounded 9,616-byte native memory copy plus event signal; two file writes move to the background writer.

## Decision 29 - Bounded Dump Snapshot And Worker Rebind

Problem: Read-only sidecar found two forensic defects after the async writer change. `TryLockBlackBoxBuffer()` permits any buffer length at or above 300, but the fixed dump scratch is sized for exactly 300 entries. The same audit found that a timed-out worker shutdown could leave a stale `_blackBoxDumpThread` field, making the next enable skip worker creation.

Solution: Clamp dump copy count to `BlackBoxFrameCount`, copy the newest 300-entry window from the source ring, and write the clamped count into the dump header. Reject queueing if the worker thread is dead. On enable, clear and recreate non-alive worker references; on stop timeout, record failed dump state explicitly.

Rejected Alternatives: Resizing the dump scratch on fault was rejected because it would create variable forensic cost and, before the Vault correction, would have implied managed allocation risk. Assuming Vault capacity is always exactly 300 was rejected because the lock helper explicitly accepts larger buffers. Ignoring join timeout was rejected because it silently disables future dump writes on that runtime instance.

Scalability potential: Low through Ultra frame behavior is unchanged. The forensic route remains fixed-size and bounded, which keeps crash artifacts predictable on weak storage and avoids scaling the dump cost with accidental Vault capacity.

Hardware Impact: No steady-frame cost. Fault-frame copy remains bounded to 9,616 bytes. The main hardware gain is preventing dump-scratch overrun and preserving dump availability after lifecycle churn.

## Decision 30 - VISUAL_SYNC Shader Publication Boundary

Problem: Sidecar audit found `Tick()` still called `UploadShaderScalars()`, which writes `_GlobalBiolumParams`, `_GlobalBiolumClock`, `_GlobalBiolumAupOffset`, `_BiolumIntensity`, and the master phase bridge from the simulation/update phase. That contradicted the route card and left a shader scalar path outside late-frame presentation sync.

Solution: Remove shader scalar upload from `Tick()`. `LateFrameTick()` now publishes all biolum shader globals: it finalizes and publishes the matrix when the oscillator job completed, otherwise it publishes scalar support values from the cached matrix/state without touching the simulation phase.

Rejected Alternatives: Leaving scalar publication in both phases was rejected because it creates two presentation routes. Publishing only when a matrix job completes was rejected because strobe, clock, quality, and overload support scalars still need late-frame updates on frames where cadence skips the oscillator job.

Scalability potential: Low uses the same cached four-row matrix and scalar cadence; Middle through Ultra keep richer shader ALU but still receive one late-frame global publication route. Quality remains continuous and independent of task phase.

Hardware Impact: Expected shader global call count per visual frame is unchanged. The gain is phase discipline: no simulation-phase shader writes, no duplicated authority, and no extra job completion pressure.

## Decision 31 - Editor Facade Player Surface

Problem: `CopyEditor*`, `TryWriteEditor*`, and `TryTriggerEditorGlobalPulse()` were public static DataVault-locking editor facades compiled into the runtime class for player builds, despite being called only by the editor tuner. This leaves accidental player call surface and keeps editor-only `Span<T>` snapshot APIs in the shipping compile surface.

Solution: Wrap the editor facade block in `UNITY_EDITOR`. Keep `TryMemCpyInitializeGlowRange()` runtime-visible because it is an unmanaged initialization helper and not editor tooling. Also correct editor telemetry copy to wrap against the source ring length when capacity exceeds 300.

Rejected Alternatives: Leaving public editor facades in player builds was rejected as compile-surface debt. Moving them to a new editor-only partial class was rejected for this pass because the existing editor assembly can use the `UNITY_EDITOR` facade without creating another file/asmdef route.

Scalability potential: Shipping Low through Ultra runtime loses no feature; editor tooling remains available in editor builds. The player path carries less managed/editor surface.

Hardware Impact: No steady-frame delta. Player compile/link surface shrinks, and editor telemetry correctness improves for oversized black-box buffers.

## Decision 32 - Burst Fast Math Compliance

Problem: The two SHINOBU_238 jobs still used `FloatMode.Deterministic`. The route has deterministic seeds and bounded presentation state, but it is not rollback, kinematics, or authoritative gameplay state. The batch mandate requires `FloatMode.Fast` for mathematical jobs outside those exception domains.

Solution: Change `GenerateMockLightingStateJob` and `AdvanceBiolumPhasesJob` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Keep deterministic hashes and seeded `Unity.Mathematics.Random` where they are used for mock/fallback presentation inputs; that does not require deterministic Burst float mode.

Rejected Alternatives: Keeping deterministic float mode was rejected because it violates the explicit Burst directive and can leave performance on the table for a visual-only oscillator. Moving biolum phase into rollback state was rejected because Tasks 14 and the route card intentionally keep glow phase out of gameplay truth.

Scalability potential: Low devices benefit from Burst fast math in the four-row oscillator while continuous `GlobalQualityWeight` still reduces cadence and amplitude. Middle through Ultra keep the same matrix ABI and spend shader ALU on visual overkill.

Hardware Impact: The job is tiny, so measured gain needs Unity profiler/Burst Inspector proof. Expected direct CPU saving is small in absolute microseconds, but the directive is now structurally compliant and lets Burst use cheaper math on ARM64 and x86.

## Decision 33 - Matrix Row Phase Authority In All Assigned Shaders

Problem: Sidecar shader audit found assigned shaders had stopped abusing matrix rows as RGB in several places, but some still used `_BiolumMasterPhase`, `_BiolumIntensity.x`, `_GlobalBiolumClock`, or `_GlobalBiolumAupOffset` as glow phase/amplitude sources. That leaves multiple authorities for the same visible fact.

Solution: Make the assigned shader set consume the row ABI directly: `.x phase`, `.y frequency`, `.z amplitude`, `.w spatial offset`. Coral, kelp, sargassum, procedural-bio, and indirect vegetation now build primary/secondary/filament waves from local coordinates and matrix row data. Group color remains deterministic shader tint, not matrix payload.

Rejected Alternatives: Keeping global clock phase was rejected because the Burst oscillator already owns phase. Keeping `_BiolumIntensity.x` as a max amplitude override was rejected because it can overpower matrix amplitude and resurrect last-writer-wins behavior. Editing Leviathan shaders was rejected because fauna presentation is outside SHINOBU_238 ownership.

Scalability potential: Low uses vertex/cheap waves from the same row ABI. Middle keeps local spatial variation. High and Ultra spend quality-weighted shader ALU on secondary interference without widening the 64-byte GPU payload or adding keywords.

Hardware Impact: CPU cost remains one matrix upload and unchanged scalar support publication. GPU cost is similar arithmetic, but phase inputs are now finite/local and single-authority; no material/global scalar route needs to be maintained for assigned flora/coral/procedural-bio.

## Decision 34 - Vault-Owned Dump Scratch

Problem: The async dump worker removed main-thread file I/O but still owned a private managed `byte[]` snapshot buffer. It was cold-path, but it violated the Vault law for persistent/cross-frame diagnostic storage and weakened the H-PHI claim.

Solution: Add local `BiolumBlackBoxDumpScratchBufferId = 70312` and request a Vault `byte` buffer of exactly 9,616 bytes. `CopyBlackBoxDumpSnapshot()` writes the 16-byte header plus 300 telemetry rows into that native scratch. The background writer locks the same scratch and writes `ReadOnlySpan<byte>` from the native pointer.

Rejected Alternatives: Keeping the private `byte[]` was rejected because the user mandate explicitly forbids private persistent array ownership. Allocating on fault was rejected because fault paths must not create GC pressure. Moving file writing back into the fault call stack was rejected because it would reintroduce the stall fixed in Decision 28.

Scalability potential: Low through Ultra steady-frame behavior is unchanged. The forensic artifact remains fixed-size and predictable, so weak storage is not hit with variable dump sizes while high-end builds retain full 300-frame history.

Hardware Impact: Steady frame remains 0 us. Fault frame remains a bounded 9,616-byte native copy and event signal; managed heap pressure from the previous 9,616-byte array is removed from owner enable.

## Decision 35 - Editor Pulse Local AUP Delta

Problem: `TryTriggerEditorGlobalPulse()` was editor-only, but it still hashed and scaled a supplied `originAUP` by casting absolute `double3` coordinates to `float`. That violates the same AUP precision rule used by the runtime sync pulse job.

Solution: Require an active `BiolumPulseSyncRuntime` for the editor trigger, build an AUP reference from the runtime origin offset, subtract with `AupPrecisionMath.LocalDeltaDouble(originAUP, aupReference)`, and downcast only the localized delta through `AupPrecisionMath.DowncastLocalDelta`.

Rejected Alternatives: Keeping direct `(float)originAUP.x/y/z` was rejected because a 50km editor scene can still generate unstable spatial offsets and misleading pulse hashes. Adding a second editor-only AUP service lookup was rejected because the active runtime already owns the presentation origin for this facade.

Scalability potential: Shipping Low through Ultra runtime is unchanged. Editor tooling now previews the same local-coordinate rule that shaders and sync pulses use.

Hardware Impact: Player runtime 0 us. Editor trigger adds one local delta calculation only when a human presses the button.

## Decision 36 - Dump Worker Rebind Fence

Problem: DataVault hotswap/teardown could release cached Vault handles after signaling the dump writer even if the writer failed to join within the timeout. That leaves a rare use-after-invalidation risk on the Vault-owned 9,616-byte dump scratch.

Solution: Make `StopBlackBoxDumpWorker()` return a boolean. `BindDataVault()` and Vault generation-mismatch refresh paths now abort handle invalidation if the writer cannot stop. `OnDisable()` and `Dispose()` release Vault handles only after a confirmed writer shutdown.

Rejected Alternatives: Always releasing handles after a stop signal was rejected because the writer can still hold a resolved `NativeArray<byte>`. Blocking indefinitely was rejected because teardown must not deadlock the editor/player.

Scalability potential: Low through Ultra steady-frame behavior is unchanged. Hotswap and shutdown now fail closed instead of trading forensic I/O for memory safety.

Hardware Impact: Steady frame 0 us. Rare teardown/hotswap path may retain handles after timeout, which is safer than invalidating memory under an active background writer.

## Decision 37 - Legacy Master Phase Writer Suppression

Problem: `HectonBiolumManager` already suppressed its `_BiolumIntensity` write when PulseSync advertised active matrix ownership through `_GlobalBiolumParams.x`, but it still published `_BiolumMasterPhase`. That left a second bridge writer for biolum phase/support data beside `BiolumPulseSyncRuntime`.

Solution: Broaden the legacy ownership guard to `IsGlobalPulseSyncOwningLegacyBiolumGlobals()`. `PublishGlobalBiolumPhase()` now skips both `PublishBiolumMasterPhase()` and `_BiolumIntensity` writes while PulseSync owns the matrix route; reset also avoids wiping the bridge phase under active PulseSync ownership.

Rejected Alternatives: Removing `_BiolumMasterPhase` globally was rejected because UberNoir/external material domains still consume it as a support scalar and need their own route approval. Leaving the legacy manager as a competing writer was rejected because it can create last-writer-wins drift against the matrix route.

Scalability potential: Low through Ultra keep the same four-row matrix ABI. The legacy manager becomes a fallback bridge only when PulseSync is inactive; when active, saved bridge writes and authority clarity buy stable flora/coral visual sync instead of more CPU simulation.

Hardware Impact: On active PulseSync frames this removes one potential bridge vector write from the legacy manager and prevents visible scalar contention. Direct CPU saving is tiny; the value is single-authority route safety.

## Decision 38 - Indirect Vegetation Local Vertex Pulse

Problem: Read-only shader audit found `Hecton_IndirectVegetation` still fed `stableAupSeed` into the vertex global-biolum pulse and spatial pulse offset. The fragment lane already used `positionWS - originWS`; the vertex lane was still vulnerable to large-coordinate phase drift.

Solution: Use `animatedPositionWS - renderOriginWS` for the vertex global-biolum pulse. Build the authored pulse offset from local biolum coordinates plus deterministic non-AUP template/variation/instance seed so individual offset remains stable without using absolute world coordinates.

Rejected Alternatives: Keeping `stableAupSeed` was rejected because it is absolute/stable position data and violates the local-coordinate shader phase rule. Removing the authored pulse offset entirely was rejected because it would flatten existing per-instance flora rhythm outside the matrix route.

Scalability potential: Low devices keep the cheap vertex lane. Middle through Ultra still blend toward fragment/pixel interference through `GlobalQualityWeight`; the coordinate input is now local in both lanes.

Hardware Impact: CPU 0 us. GPU ALU is the same order as before, with one local subtract already available in the vertex context; the benefit is precision stability at map edges.

## Decision 39 - Dump Worker Owner-Swap Recovery And Path Helper Naming

Problem: Subagent audit found two lifecycle defects: `Dispose()` nulled `_dataVault` even if the dump worker failed to join, and `TryRefreshExistingVaultHandlesNoAllocate()` could stop the worker on generation mismatch without restarting it after reacquiring the Vault scratch handle. The same pass found impure `Resolve*` path helpers.

Solution: Preserve `_dataVault` and leave `_disposed=false` when the dump worker stop times out, so the worker can finish against valid Vault state and a later dispose can retry. Restart the dump worker after successful owner-swap handle reacquisition. Rename the refresh helper to `TryRefreshExistingVaultHandlesForOwnerSwap`, and rename file path helpers to `BuildColdProfilePath`, `TryFindProfilePath`, and `BuildEditorCsvOverridePath`.

Rejected Alternatives: Nulling the Vault under a live worker was rejected as teardown race risk. Leaving the worker dead after generation refresh was rejected because it disables mandated black-box forensics. Keeping `Resolve*` names was rejected because those methods build paths, call `File.Exists`, or mutate editor path cache.

Scalability potential: Low through Ultra steady frame behavior is unchanged. The fixes only affect fault forensics, DataVault owner-swap, and cold/editor file path routes.

Hardware Impact: Steady frame 0 us. Owner-swap may recreate one background writer thread; fault artifact availability is preserved without hot-path file I/O.

## Decision 40 - Legacy Zone Registry Bounded Arrays

Problem: Static scan found `HectonBiolumManager` still held three `List<HectonBiolumZone>` registries for cave, ocean, and floor zones. They were pre-sized, but a scene with more than 32 zones can force `List<T>` growth in the runtime fallback bridge, and the file now participates in PulseSync legacy-global suppression.

Solution: Replace the three lists with fixed `HectonBiolumZone[32]` arrays and explicit counters. Registration uses a bounded duplicate scan and fail-closed insert. Removal compacts by index. Overflow increments `_zoneRegistryOverflowCount` and sets telemetry flag bit `16` in the 300-frame ring.

Rejected Alternatives: Keeping `List<T>` was rejected because pre-sizing is not a hard capacity. Moving this manager into the SHINOBU Vault route was rejected because it is an existing world/legacy bridge, not the PulseSync matrix owner. Silently dropping zone overflow was rejected because bounded failure must be visible in telemetry.

Scalability potential: Low/MX350 gets fixed memory and no surprise list growth. Middle and High scenes can still use the same bounded fallback bridge, with overflow visible for content tuning. Ultra visual overkill remains in the shader matrix path, not in more world-manager zone allocations.

Hardware Impact: Registration remains cold/event-driven and bounded to 32 slots per zone family. Steady Tick path reads counters/arrays directly. Estimated heap saving is the avoided `List<T>` growth allocation when content exceeds the old initial capacity.

## Decision 41 - Legacy Touch Ripple Continuous Quality Budget

Problem: `HectonBiolumManager.PublishTouchRippleBuffer()` used `GlobalRegistry.ScalabilityTier` plus `DistanceMath.IsHighQualityTier()` to choose between zero touch ripples and up to sixteen touch ripples. That is a binary quality switch in a file touched by the biolum fallback route.

Solution: Use `HomeostasisBrain.GlobalQualityWeight` directly and convert it into a continuous upload capacity: `round(lerp(0, 16, smoothstep(0.12, 0.72, qualityWeight)))`. The shader parameter now receives `writeCount`, `uploadBlend`, and `qualityWeight`, so low devices fade toward fewer ripples while high/ultra restores full detail.

Rejected Alternatives: Keeping the tier branch was rejected because visual pop is visible when quality crosses tier boundaries. Always uploading all 16 ripples was rejected because MX350/thermal fallback should shed GPU upload and shader work continuously. Moving this fallback bridge into PulseSync was rejected because it remains a legacy world-zone/touch-response bridge, not the global pulse matrix owner.

Scalability potential: Low quality can collapse to zero or a few nearest ripples. Middle quality gradually restores ripple count. High and Ultra use the full fixed 16-ripple buffer and keep richer local touch response without changing route identity or DTO layout.

Hardware Impact: Upper bound is unchanged at 16 entries. Low quality avoids unnecessary upload/shader consumption without a branch on named hardware tier. CPU work remains bounded; shader visual cost tracks continuous thermal state.

## Decision 42 - Legacy Bridge Accessor Purity And Cached Service Routing

Problem: The touched legacy biolum bridge still had read-like accessors and helper names hiding mutable work. `GetCameraAup()` rebuilt cached AUP state, `GetCameraPosition()` still read live `Transform.position` instead of an owner-phase snapshot, several `Resolve*` helpers updated state or selected buffers, and runtime helpers polled `GlobalRegistry.DataVault`, `GlobalRegistry.TickDispatcher`, `GlobalRegistry.Fluid`, or `GlobalRegistry.Player`.

Solution: Move camera reference/position/AUP refresh into owner phases through `RefreshCameraSnapshotForOwnerPhase(...)`; leave `GetCameraPosition()` and `GetCameraAup()` as pure cached snapshot reads. Cache `DataVault`, `TickDispatcher`, `Fluid`, and `Player` once during lifecycle and rebind through `IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(...)`. Rename mutating helpers to `Update*`, `Sample*`, `Select*`, `TryOpen*`, `TryCache*`, or `TryBuild*`. Rename controller survival binding to `TryBindSurvivalSystemFromPlayerContext()`. Fix `SampleCameraCacheClockSeconds()` to be an instance method because it reads `_cachedTickDispatcher`.

Rejected Alternatives: Leaving hidden cache mutation in `Get*` methods was rejected because read accessors must be pure. Continuing to poll `GlobalRegistry` from tick/sample/ensure paths was rejected because GlobalRegistry is cold identity/DI, not a hot polling bus. Replacing `GlobalRegistry.CelestialRuntimeSnapshot` in this pass was rejected because it is exposed as a snapshot property, not a typed service interface, and changing that route would be a core-domain contract edit.

Scalability potential: Low/MX350 avoids hidden registry/cache work in repeated fallback-zone and ripple reads. Middle keeps the bounded legacy zone/ripple bridge. High and Ultra keep touch/flow/celestial presentation richness while PulseSync matrix ownership remains the visual-overkill route.

Hardware Impact: Removes several registry property reads, live Transform reads from accessors, and hidden AUP cache rebuild opportunities from touched fallback bridge paths. Direct frame gain is expected to be low microseconds or sub-microsecond depending on call frequency; the higher-value impact is preserving single-owner phase discipline and preventing compile failure from the static `_cachedTickDispatcher` read.
