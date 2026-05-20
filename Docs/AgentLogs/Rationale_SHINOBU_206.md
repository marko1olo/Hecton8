# Rationale_SHINOBU_206

Date: 2026-05-20
Agent: SHINOBU_206
Status: PENDING VERIFICATION

## Decision 001 - Scope Anchor

Problem: The batch demands elimination of premature `Complete()` and `Run()` across the gameplay path, but the project already has central helpers (`DispatcherJobFence`, `DispatcherJobSwap`) and a large existing call surface.

Solution: Start from central dispatcher/fence files, then patch highest-risk hot-path call sites found by static scan. Use source evidence only. Any unmodified offender remains explicitly reported, not silently treated as solved.

Rejected Alternatives: A global regex deletion would break result readback and native disposal. A full architecture rewrite would collide with 20+ active agents and violate the batch dependency boundary.

Scalability potential: Low tier gains by removing main-thread waits; middle/high/ultra can spend worker headroom on denser simulation and visual sync. The topology remains constant; quality changes cadence and batch size, not binary feature presence.

Hardware Impact: On i3/MX350, each eliminated mid-frame job wait can recover 50-800 us depending on worker backlog. Exact runtime proof is absent until Unity profiler/GCMonitor.

## Decision 002 - Mandate Selection

Problem: Job fences touch memory, execution phases, struct layout, telemetry, and AUP rebase correctness.

Solution: Loaded the job/native memory, zero-GC, ARM64 layout, execution phase, AUP determinism, telemetry, performance budget, and global authority boundary mandates before edits.

Rejected Alternatives: Reading only the batch prompt misses existing DataVault/global authority restrictions and causes incompatible surfaces.

Scalability potential: The same fence law supports weak CPUs by avoiding stalls and high-end rigs by keeping worker queues saturated.

Hardware Impact: Static architecture choice; expected impact is lower main-thread idle on i3/MX350 and improved worker occupancy on 8+ core CPUs.

## Decision 003 - Central Fence Ownership

Problem: `DispatcherJobSwap` and `DispatcherJobFence` held overlapping swap-window behavior, which lets systems bypass the Core fence law or diverge warning state.

Solution: Kept `DispatcherJobSwap` as a compatibility facade and moved real Begin/End/TryComplete/TryFinalizeCompleted behavior to Core `DispatcherJobFence`.

Rejected Alternatives: Duplicating window counters in World code was rejected because Core systems cannot safely depend on sibling runtime state. Rewriting all callers in one pass was rejected because other agents own many call sites.

Scalability potential: Low devices avoid accidental waits from duplicated windows; middle/high/ultra keep the same fence contract while increasing worker density.

Hardware Impact: Expected low-end gain is from avoiding illegal mid-frame waits, not from the facade itself. Estimate: 0-50 us/frame by preventing duplicate-window misuse; profiler proof absent.

## Decision 004 - Domain Fence Buffers

Problem: A single master simulation handle hides whether simulation, physics, audio, or netcode owns a stall.

Solution: Added four dispatcher fence domains, stored domain handles in DataVault, combined domain handles into the master fence, and recorded raw handle bits plus a domain mask in a 300-frame ring.

Rejected Alternatives: Sequential pairwise completion was rejected because it serializes visibility and hides domain ownership. Managed dictionaries were rejected due hot-path allocation risk.

Scalability potential: Low tier can shed visual/audio presentation when one domain is late; high/ultra can run denser domain jobs while preserving POST_SIM synchronization.

Hardware Impact: Expected i3/MX350 benefit is diagnostic and scheduling control, 25-200 us saved when domain-specific shedding prevents a full-frame wait. Runtime proof absent.

## Decision 005 - Explicit DTO Layout

Problem: Job dependency telemetry carried `JobHandle`-like state through layout that could drift and trigger CS1612/property-copy failure patterns.

Solution: Replaced handle telemetry with raw fields in `JobDependencyDTO` and added explicit 32-byte/64-byte layout guards for job dependency and fence telemetry DTOs.

Rejected Alternatives: Keeping implicit sequential layout was rejected because ARM64 alignment is not negotiable. Storing managed `JobHandle` wrappers in telemetry was rejected because telemetry is not an ownership surface.

Scalability potential: Stable DTO layout lets cheap devices record minimal telemetry and high-end builds increase graph density without changing binary contracts.

Hardware Impact: Expected low-end gain is mainly cache predictability and avoidance of defensive copies, 5-30 us in dispatcher telemetry paths. Runtime proof absent.

## Decision 006 - Hot Path Completion Deferral

Problem: Several gameplay/UI systems completed job handles inside Tick/LateFrame even when the result was not ready.

Solution: Patched highest-risk owned call sites to use `DispatcherJobFence.TryComplete` or `TryFinalizeCompleted`, preserving previous-frame output when jobs are pending and forcing completion only on teardown/AUP barriers.

Rejected Alternatives: Replacing every `Complete()` token blindly was rejected because many sites immediately read native results or dispose buffers. Such a patch would move the stall or create data races.

Scalability potential: Low tier keeps stale-but-valid presentation for one frame instead of stalling; high/ultra spends worker slack on richer mesh/wave/spatial results.

Hardware Impact: Static estimate on i3/MX350 is 50-800 us per eliminated mid-frame wait depending on backlog. Runtime proof absent.

## Decision 007 - Tiny Run Jobs

Problem: `VocalWarningSystem`, `HectonSurvivalSystem`, and `SuitUpgradeManager` used `IJob.Run()` for 1-16 element work that was consumed immediately.

Solution: Removed those synchronous job invocations. Vocal warning uses inline fixed-size loops; survival and suit upgrade call the existing scalar Execute path for one result row.

Rejected Alternatives: Scheduling and immediately completing was rejected because it preserves the stall. One-frame staging was rejected for cooldown and suit-stat paths because external callers read/write the same state in the same frame.

Scalability potential: Low tier avoids scheduler overhead on trivial work; high/ultra does not lose visible quality because these are scalar control paths, not visual-density workers.

Hardware Impact: Expected i3/MX350 gain is 3-40 us per slow tick or equipment stat resolve from removed scheduler/fence overhead. Runtime proof absent.

## Decision 008 - Safety Restriction Audit

Problem: Native queue writers with `NativeDisableContainerSafetyRestriction` lacked local proof of producer ownership and registered handles.

Solution: Added three-paragraph safety justification where patched and registered the seismic evaluation handle with `H8Memory` immediately after schedule. Existing fabrication signal job already registers its returned handle.

Rejected Alternatives: Removing the safety attribute was rejected because Unity safety cannot model dispatcher-owned NativeQueue producer/consumer phase separation. Main-thread signal emission was rejected because it adds scans and managed callback risk.

Scalability potential: Low tier keeps signal emission off managed callbacks; high/ultra can increase signal density with the same registered-handle rule.

Hardware Impact: Expected gain is prevention of race/debug stalls rather than raw speed. Estimate: 0-100 us avoided on frames with seismic/fabrication signals; runtime proof absent.

## Decision 009 - Verification Gate

Problem: Build verification was requested by the state machine, but project rules forbid launching `dotnet build` while CPU is busy or compiler processes are active.

Solution: Checked CPU and compiler state. CPU sampled at 100%, no `dotnet` or `csc` process existed, and no `.sln` file was present. Ran static `git diff --check`, `rg` token scans, and JSON validation instead.

Rejected Alternatives: Launching a build at 100% CPU was rejected by explicit project law. Claiming compile success without Unity import was rejected as fake reporting.

Scalability potential: Verification status remains honest; low/middle/high/ultra behavior still needs Unity/Burst import proof.

Hardware Impact: No runtime gain. Prevented contention with other agents and avoided a prohibited build stall.

## Decision 010 - Method-Scope Stall Scanner

Problem: The previous stall scanner used a 12-line context window. A `Run()` or `Complete()` deep inside a long `FixedTick` method could be missed, producing a false low-risk report.

Solution: Replaced the context-only check with a brace-scoped frame-method map for `Tick`, `FixedTick`, `FastTick`, `LateFrameTick`, `PostFixedTick`, Unity update methods, and scheduler methods. Added comment skipping and a two-line cold annotation window.

Rejected Alternatives: A full Roslyn call graph was rejected in this pass because it would introduce a larger tooling dependency and risk a compile wall. Keeping the old scanner was rejected because it undercounted actual hot-method bodies.

Scalability potential: Low-tier devices benefit from stricter stall detection because scheduler stalls are most visible on weak CPUs. High/ultra devices benefit by exposing hidden sync points that prevent full worker occupancy.

Hardware Impact: Audit-only change. It does not save frame time by itself, but it prevents false reporting. Latest static evidence: total sync tokens 313, cold/editor 127, method hot 0, runtime run tokens 71.

## Decision 011 - Tether SHINOBU_143 Fence Unification

Problem: `TetherManager` used `DispatcherJobFence` for the SHINOBU_132 cable mock handle but completed the adjacent SHINOBU_143 AUP mock handle directly.

Solution: Routed SHINOBU_143 completion through `DispatcherJobFence.TryComplete` on teardown and `TryFinalizeCompleted` during runtime polling, matching the already present SHINOBU_132 pattern.

Rejected Alternatives: Leaving the raw `.Complete()` was rejected because it remained a method-scoped hot offender. Replacing it with `Schedule().Complete()` was irrelevant because the job was already scheduled; the defect was direct completion.

Scalability potential: Low and middle tiers avoid unnecessary main-thread waits when the mock AUP solver overruns. High/ultra tiers keep the same worker occupancy while denser tether visual work can proceed.

Hardware Impact: Static estimate on i3/MX350: 20-150 us recovered on frames where the mock AUP solver is not ready by poll time. Runtime proof absent.

## Decision 012 - Player KCC Run Removal Without Fake Async

Problem: `PlayerKinematicsRuntime.FixedTick` executed two same-tick player control kernels through `IJob.Run()`: body integration and SDF squeeze intervention. The results are immediately consumed by KCC state, velocity signals, movement acoustics, and rollback hash staging.

Solution: Replaced `Run()` with direct `Execute()` for those single-row scalar kernels. This removes the Job System synchronous run path and scheduler fence without pretending the work is asynchronous. A true async KCC rewrite requires owner-domain staging and latency acceptance.

Rejected Alternatives: `Schedule().Complete()` was rejected as a rename of the same stall. One-frame deferred KCC application was rejected in this pass because it changes player-control authority, motor velocity timing, and rollback hash cadence without a KCC owner contract.

Scalability potential: Low tier removes scheduler overhead for the player-only row and retains same-frame control. Middle/high/ultra need a separate KCC architecture pass if the scalar body/squeeze math becomes measurable; that pass should double-buffer state and publish delayed presentation safely.

Hardware Impact: Static estimate on i3/MX350: 3-80 us of scheduler/fence overhead removed from KCC fixed tick. ALU cost remains on main thread; no runtime profiler proof exists.

## Decision 013 - Missing Polish Document

Problem: The user mandate asked to read `Docs/Tasks/POLISH.txt`, but the file does not exist in this workspace.

Solution: Recorded the missing file as a verification gap instead of inventing polish rules.

Rejected Alternatives: Treating the missing file as implicitly satisfied was rejected. Creating a new polish file was rejected because no task authorized defining new polish authority.

Scalability potential: None directly. The missing doc does not change the fence architecture.

Hardware Impact: None.

## Decision 014 - Runtime IJob.Run Eradication Pass

Problem: The residual `.Run(` report still mixed real `IJob.Run`, editor windows, smoke-test string assertions, and managed `Task.Run`, hiding the actual hot gameplay debt.

Solution: Removed real runtime `IJob.Run` call sites by either direct `Execute()` for scalar/cold/presentation kernels or real scheduling for multi-row gameplay work. The scanner now reports `runtimeRunTokens=0`.

Rejected Alternatives: `Schedule().Complete()` was rejected because it preserves the stall under a different spelling. Converting managed `Task.Run` IO to Job System was rejected as a construction/streaming owner API change, not an `IJob.Run` defect.

Scalability potential: Low tier avoids scheduler/run overhead for one-row work and no longer blocks on upload/flood/celestial workers. Middle/high/ultra retain worker occupancy and can spend freed main-thread time on denser visual presentation.

Hardware Impact: Static i3/MX350 estimate is 3-150 us saved per removed synchronous runner and 50-1200 us avoided when deferred workers miss the current frame.

## Decision 015 - PDA Cartography Upload Fence

Problem: `TryPrepareCartographyUpload` formatted 524,288 packed voxels plus rollback words synchronously from the PDA visual path.

Solution: Converted upload formatting and rollback snapshot copy into parallel scheduled jobs, combined their handles, registered them under `SystemID.UI`, and returned no upload until `TryFinalizeCompleted` succeeds. Pre-simulation and slow fallback skip discovery writes while an upload reads the same discovery mask.

Rejected Alternatives: Direct `Execute()` over the full upload was rejected because it keeps the visual-sync CPU spike. `Schedule().Complete()` was rejected because it is the same stall.

Scalability potential: Low tier keeps the previous hologram map buffer for one or more frames instead of hitching; high/ultra can run full density upload cadence without blocking the render path.

Hardware Impact: Static low-end estimate: 0.3-1.2 ms visual-sync hitch avoided on map upload frames; profiler proof pending.

## Decision 016 - Deferred Gameplay Worker Fences

Problem: Tether Verlet solve, Habitat flood propagation, ModSandbox validation, and celestial mechanics were same-frame synchronous workers or already scheduled without a proper nonblocking finalization contract.

Solution: Tether now schedules integration -> constraint -> telemetry and publishes previous tension until finalization. Habitat flood propagation schedules and applies deltas on a later completed fence. ModSandbox pre-sim validation schedules into an existing active-handle lane. Celestial mechanics schedules and returns fallback/cached tide until the job finalizes.

Rejected Alternatives: Inline `Execute()` on heavy kernels was rejected because it removes the token but not the main-thread burden. Immediate completion was rejected because it violates the swap-window fence law.

Scalability potential: Low tier receives stale-but-valid presentation or conservative fallback for a frame. High/ultra keeps workers saturated and can raise solver cadence through existing quality curves.

Hardware Impact: Static estimate: 100-1200 us avoided on congested frames, especially map/flood/celestial/tether overlaps. Runtime verification still blocked.

## Decision 017 - Scanner Classification Correction

Problem: The scanner counted editor-guarded windows and managed `Task.Run` as runtime `IJob.Run` debt.

Solution: Added top-of-file `UNITY_EDITOR` guard detection and excluded `Task.Run` from `IJob.Run` token counting. Current JSON report: total sync tokens 254, cold/editor 147, hot path 0, method hot 0, runtime run 0, unclassified runtime sync tokens 107.

Rejected Alternatives: Deleting editor bake/smoke-test runners was rejected because they are outside the hot gameplay path. Counting `Task.Run` as an `IJob` defect was rejected as a false-positive metric.

Scalability potential: Cleaner metrics let subsequent passes focus on real runtime `Complete()` debt instead of editor noise.

Hardware Impact: Audit-only. No direct frame gain.

## Decision 018 - Broad Complete Residue Boundary

Problem: Broad `rg` still found six raw `.Complete()` calls after hot-path cleanup: two inside `DispatcherJobFence` and four inside MapMagic bridge nodes. The previous scanner undercounted Core because it treated a conditional editor `using` guard as a whole-file editor guard.

Solution: Tightened `Stall_Eradication_Scanner.HasUnityEditorFileGuard` so only a true top-of-file `#if UNITY_EDITOR` wrapper marks a file as editor-only. The updated static report now exposes two unclassified runtime sync tokens, both inside the central Core fence helper. MapMagic bridge completions remain classified as cold/bridge residue by local annotations and are not rewritten because 3rd-party bridge mutation is outside this pass unless it enters gameplay tick.

Rejected Alternatives: Rewriting MapMagic nodes was rejected by the 3rd-party asset integrity rule. Hiding `DispatcherJobFence` under editor-file classification was rejected because it creates false proof. Replacing the helper's forced `handle.Complete()` with no-op was rejected because teardown and AUP hard fences require a single explicit blocking point.

Scalability potential: Low/middle tiers keep arbitrary systems from blocking independently; high/ultra machines keep worker saturation because the only raw completion surface is centralized and visible in telemetry.

Hardware Impact: Audit-only directly. Indirect protection is preventing hidden mid-frame stalls from being misclassified; expected low-end recovery remains tied to patched call sites, not the scanner.

## Decision 019 - Dynamic Decal Visual Sync Fence

Problem: Decal visual sync generated, decayed, and built upload buffers in the VISUAL_SYNC path, then needed the result immediately for GPU upload. Immediate completion would turn decal bursts into a main-thread fence.

Solution: `ExecuteVisualSync` now first tries to finalize a pending chain. If the chain is not complete, it returns the last completed upload stats. New generate/decay/upload work is scheduled, registered with `H8Memory`, and stored in `_pendingVisualSyncHandle` when not ready. Runtime vault buffers stay locked until finalization, preventing writers from mutating data still owned by the scheduled chain.

Rejected Alternatives: `Schedule().Complete()` was rejected as a renamed stall. Direct `Execute()` was rejected because decal bursts are visual density work, not authoritative gameplay truth. Releasing vault locks while the handle was pending was rejected because it permits producer/consumer aliasing.

Scalability potential: Low tier can reuse one-frame-old decals and shed visual freshness under pressure. Middle/high/ultra keep full decal density and let worker threads finish the upload chain without stalling the main render path.

Hardware Impact: Static i3/MX350 estimate: 50-700 us avoided on decal-heavy visual-sync frames. Runtime profiler proof is absent until Unity import and GCMonitor/profiler capture.

## Decision 020 - Build Gate Recheck

Problem: Verification needed a compile/import pass, but project rules forbid builds while another compiler host is active.

Solution: Sampled CPU and compiler state after patches. CPU was 2.92%, but `dotnet:40832` was active. Build was not launched. Static gates used instead: `rg` for raw completion/run debt, targeted `git diff --check`, and JSON parse.

Rejected Alternatives: Launching a second build while `dotnet` exists was rejected by explicit project law. Reporting compile success from static checks was rejected as false verification.

Scalability potential: None directly; this preserves multi-agent iteration stability.

Hardware Impact: No runtime gain. Prevented local IO/CPU contention with another agent's process.

## Decision 021 - Forced Fence Scanner Audit

Problem: Raw `.Complete()` and `IJob.Run()` scans missed the hidden blocking surface: `DispatcherJobFence.TryComplete(... forceComplete: true)` and `DispatcherJobSwap.TryComplete(..., true)`. A forced helper call is still a hard fence even when the raw `.Complete()` token is centralized.

Solution: Extended `Stall_Eradication_Scanner` to count forced helper calls separately and emit `forcedFenceTokens` plus `forcedHotPathTokens` into `DISPATCHER_OPTIMIZATION_REPORT.json`. The hot-method mapper now skips comment/XML-doc lines so a documentation reference to `FixedTick(float)` cannot mark teardown methods as frame-loop code.

Rejected Alternatives: Treating forced helper calls as safe because they route through Core was rejected; the Core helper centralizes proof, but call-site phase still matters. A full Roslyn analyzer was deferred to avoid tooling compile-wall risk in this pass.

Scalability potential: Low-tier devices benefit because hidden hard waits in frame loops are now visible before they become hitches. Middle/high/ultra devices benefit because worker saturation is no longer masked by helper naming.

Hardware Impact: Audit-only. Latest non-editor/dev/test scan reports 232 forced fence tokens and 0 forced hot-path tokens after the latest patches.

## Decision 022 - Mod Event Projection Deferred Dispatch

Problem: `ModEventProjectionBridge.DispatchLateFrame` attempted a nonblocking finalize, then immediately force-completed the same projection job and drained `_projectedEvents`. This preserved correctness but converted mod event bursts into late-frame stalls.

Solution: If the projection handle is not ready, late frame now publishes the existing warning and returns without draining the queue. `_projectionScheduled` remains true, so the next late-frame pass retries finalization and dispatch only after the worker has finished.

Rejected Alternatives: Dropping queued events was rejected because managed mod consumers would lose signals. Copying the queue to managed storage was rejected because it violates the zero-GC hot path.

Scalability potential: Low tier can carry projected mod events one frame later instead of stalling. High/ultra keeps high-volume mod projection on worker threads without sacrificing the main frame.

Hardware Impact: Static i3/MX350 estimate: 50-400 us avoided during mod event bursts. Runtime profiler proof absent.

## Decision 023 - KCC Rollback Nonblocking Resimulation Gate

Problem: `HydrodynamicKccRuntime.TryRunRollbackResimulation` force-completed `_postSimulationHandle` inside a gameplay-owned rollback resim path. That was a hidden hard wait even though the token was routed through `DispatcherJobFence`.

Solution: Replaced the forced completion with `TryFinalizeCompleted`. If post-simulation work is still active, the method restores rollback bypass state and returns `false`; normal late-frame fence finalization remains the owner of pending work.

Rejected Alternatives: Keeping the forced fence was rejected because rollback attempts must not stall the main frame. Scheduling another resim layer was rejected because no external call site currently exists and changing KCC authority timing requires owner approval.

Scalability potential: Low tier fails fast and preserves frame time when KCC workers are still pending. High/ultra can still complete immediately when worker throughput is sufficient, so behavior scales with available silicon without binary switches.

Hardware Impact: Static i3/MX350 estimate: 50-300 us avoided on rollback attempts with pending KCC post-sim jobs. Runtime profiler proof absent.

## Decision 024 - PlayerBuilder Socket Snap Async Chain

Problem: `PlayerBuilder` still executed `EvaluateSocketSnappingJob.Run(ghostCount)` and `SelectBestSocketSnapJob.Run()` in the build-preview path. This was a real Unity Job System synchronous runner, not managed IO.

Solution: Replaced both runners with a scheduled chain: `EvaluateSocketSnappingJob.Schedule(ghostCount, ResolveInnerloopBatchCount(...))` followed by `SelectBestSocketSnapJob.Schedule(evaluateHandle)`. The combined handle is registered under `SystemID.Construction`. While the chain is pending, the builder returns the last valid snapped pose as a Dear Lie; if no cached pose exists, it returns unsnapped for that frame.

Rejected Alternatives: `Schedule().Complete()` was rejected as the same stall under a new spelling. Direct `Execute()` was rejected because socket snap can scan multiple ghost sockets and candidate ranges; it would remove the token but keep the preview hitch.

Scalability potential: Low tier carries one-frame-old snap placement during dense socket previews instead of stalling. Middle/high/ultra can resolve the scheduled chain inside the same or next frame depending on available worker throughput, with batch size continuously driven by `GlobalQualityWeight`.

Hardware Impact: Static i3/MX350 estimate: 80-600 us avoided on dense socket preview frames. Runtime profiler proof absent.

## Decision 025 - Final Build Guard

Problem: Static gates passed after the PlayerBuilder patch, but the project law forbids launching `dotnet build` while CPU load is above 50% or compiler processes are active.

Solution: Rechecked the guard. CPU sampled at 90.91%, with no `dotnet` or `csc` process. Build/import verification was not launched. Static gates used instead: JSON parse, `git diff --check`, raw `.Complete()`, `.Run(`, and `Schedule(...).Complete()` scans.

Rejected Alternatives: Launching a compile at 90.91% CPU was rejected by explicit instruction. Reporting compile success from static scans was rejected as false verification.

Scalability potential: None directly; this preserves local iteration stability while other workloads are active.

Hardware Impact: No runtime gain. Prevented build contention on already-loaded hardware.

## Decision 026 - Call-Propagated Forced Fence Truth Reset

Problem: The previous JSON report claimed `forcedHotPathTokens=0`, but a stricter same-file call-propagation scan found hot methods calling helper methods that still contained `forceComplete:true` branches. That made the previous proof too weak.

Solution: Treat helper calls as part of the hot graph, split shared `bool forceComplete` methods into no-wait runtime finalizers and teardown/hard-barrier finalizers, then update the report with remaining candidates instead of suppressing them.

Rejected Alternatives: Keeping the old zero was rejected as false proof. Blindly deleting every forced helper call was rejected because AUP shifts, teardown, DataVault replacement, and deterministic hash validation require hard barriers.

Scalability potential: Low devices avoid hidden main-thread waits from helper indirection. Middle/high/ultra keep worker occupancy because hot callers return cached or stale presentation rather than draining jobs.

Hardware Impact: Audit plus static patch impact. Candidates reduced from 53 to 36 under the stricter analyzer; runtime microseconds require profiler proof.

## Decision 027 - KCC/Hand/Tether/Chemical Finalizer Split

Problem: KCC rollback, hand environment probes, tether mock solvers, habitat flood, and chemical diffusion used shared force/no-force methods. Hot callers passed `false`, but the method bodies still carried hard-fence branches and in one KCC path rollback called teardown drain directly.

Solution: Split runtime paths to `TryFinalize*` methods using `TryFinalizeCompleted` or non-forced swap completion. Teardown, reset, DataVault replacement, and release paths use separate `*ForTeardown` hard-fence methods.

Rejected Alternatives: Leaving shared bool methods was rejected because it keeps hard-fence code reachable in the hot call graph. Forcing rollback cleanup was rejected because it stalls player/physics rollback attempts.

Scalability potential: Low tier can skip/return false while workers finish. High and ultra tiers still complete immediately when worker throughput is sufficient, without a binary quality switch.

Hardware Impact: Static low-end estimate: 20-700 us avoided per pending worker chain depending on collision/raycast/flood/diffusion load. Runtime proof absent.

## Decision 028 - SpatialAudio and DroneFleet Runtime Fence Changes

Problem: `SpatialAudioManager.LateFrameTick` forced virtual voice sort and acoustic occlusion before injecting selections. `DroneFleetManager.ResolveDockingObstacleAborts` forced a `RaycastCommand.ScheduleBatch` inside headless simulation Tick.

Solution: SpatialAudio late frame now tries no-wait finalizers and injects the last valid voice/DSP state when work is pending. DroneFleet stores the docking raycast handle/count, registers it as `SystemID.Construction`, applies hits on a later no-wait finalize, and reserves hard completion for reset/release.

Rejected Alternatives: Blocking before audio injection was rejected because stale voice selection is a valid Dear Lie. Direct raycasts or forced scheduled raycasts were rejected because docking obstacle checks are advisory and can tolerate one-frame latency.

Scalability potential: Low tier preserves frame time with stale acoustic/docking decisions. High/ultra resolves the same jobs sooner through worker throughput and can spend saved main-thread time on richer audio/BRG presentation.

Hardware Impact: Static estimate: 50-500 us avoided on heavy virtual audio frames, 80-600 us avoided on dense drone docking raycast frames. Runtime proof absent.

## Decision 029 - Inventory Salinity Fence Removal Boundary

Problem: `PlayerInventory.ApplyInventorySalinityCorrosion` scheduled a job and immediately force-completed it in `SlowTick`. Making it truly async without a wider inventory write-lock would allow the job to mutate item SOA while add/remove/craft mutation paths also write those arrays.

Solution: Replace the forced scheduled handle with direct `ItemSalinityCorrosionJob.Execute()` in the slow lane. This removes the hidden JobHandle hard fence now and leaves a clearly bounded future design: async salinity requires inventory-wide deferred mutation/write-lock ownership.

Rejected Alternatives: `Schedule().Complete()` was the original defect. Scheduling without locking was rejected as a data race. Adding a broad inventory mutation queue in this pass was rejected because it crosses owner-domain behavior beyond the fence-enforcer mandate.

Scalability potential: Low tier removes scheduler/fence overhead but still pays ALU in slow tick. Middle/high/ultra need a future owner patch to double-buffer inventory durability or defer inventory mutations during salinity jobs.

Hardware Impact: Static estimate: 20-150 us of scheduler/fence overhead removed on corrosion frost ticks; ALU scan cost remains.

## Decision 030 - PersistentWorld and GlobalPhysics Residue

Problem: Two confirmed owner-review surfaces remain: `PersistentWorldRegistry` forces tombstone sweep before live delta mutation, and `GlobalPhysicsStateManager` forces physics culling before tracked-body mutations. Removing the wait naively would mutate containers while scheduled jobs read them.

Solution: Do not fake a fix. Report them as owner-review residue in JSON. Safe elimination requires snapshot buffers or deferred mutation queues: tombstone sweep over compact delta snapshots, and tracked-body culling over immutable culling snapshots or queued body-delta application after no-wait finalization.

Rejected Alternatives: Mutating `_deltaRecords` while `TombstoneDecayCollectJob` reads `_deltaRecords.AsArray()` was rejected as a NativeArray race. Dropping delta mutations was rejected because save state would diverge. Force-deleting the culling fence was rejected because origin/body registry consistency would be undefined.

Scalability potential: Low tier needs these owner fixes to eliminate long-tail hitches during save/world mutation spikes. High/ultra would benefit by allowing sweeps and culling to overlap mutation-heavy frames.

Hardware Impact: Pending owner integration. Static evidence remains in `DISPATCHER_OPTIMIZATION_REPORT.json` as forced-hot samples, not hidden as success.

## Decision 031 - Ecology, Metabolism, and VFX Shared Helper Split

Problem: Additional runtime systems still used shared `CompleteFrameJob(bool forceComplete)` or unnamed hard completion helpers. Hot late-frame callers passed `false`, but the helper bodies still contained hard-fence branches, so call-propagation could conservatively mark teardown waits as hot-reachable.

Solution: Split `ShinobuFloraFaunaSymbiosisSolver`, `ShinobuEcosystemBalancer`, and `ShinobuMetabolismRuntime` into `TryFinalize*NoWait` plus `*ForTeardown` paths. Renamed `EcosystemPopulationBalancer` and `BiolumPulseSyncRuntime` hard helpers to teardown-only names where runtime no-wait finalization already existed.

Rejected Alternatives: Keeping shared `bool forceComplete` helpers was rejected because it preserves hidden hard-fence reachability. Blindly deleting teardown fences was rejected because DataVault replacement, disable, and editor reload must not release buffers while jobs still own them.

Scalability potential: Low tier can skip late-frame result consumption and keep stale ecology/metabolism/VFX output while workers finish. Middle/high/ultra complete naturally sooner through available worker throughput and can increase population, flocking, and pulse density without changing the fence contract.

Hardware Impact: Static estimate on i3/MX350: 20-700 us avoided when the ecology, metabolism, or pulse worker misses the late-frame window. Runtime profiler proof absent.

## Decision 032 - Last Runtime IJob.Run Residue

Problem: A filtered `rg` pass exposed two real runtime `IJob.Run()` residues after the prior report: `DrainageMockNetworkJob.Run()` in `SumpPumpPipeGridRuntime` and atmosphere topology/CSR bootstrap runs in `BaseAtmosphereLogisticsRuntime`.

Solution: Replaced those cold bootstrap invocations with direct `Execute()` calls. They are deterministic same-method initialization kernels, so scheduling them would add overhead without parallel benefit.

Rejected Alternatives: `Schedule().Complete()` was rejected because it preserves the same synchronous wait under different spelling. Making the mock/bootstrap default generation async was rejected because the surrounding code immediately consumes initialized counters and topology state before entering runtime ticks.

Scalability potential: Low tier avoids needless scheduler overhead during bootstrap/mock reset. High/ultra loses no visual quality because these are cold topology construction paths, not visual-density workers.

Hardware Impact: Static estimate on i3/MX350: 3-150 us scheduler/run overhead removed per cold bootstrap invocation.

## Decision 033 - Report Truth After Analyzer Timeout

Problem: The full-tree PowerShell call-propagation analyzer exceeded 240 seconds on the current multi-agent worktree. Reusing the stale exact forced-hot count as current proof would be false.

Solution: Updated `DISPATCHER_OPTIMIZATION_REPORT.json` with fast `rg` gates that completed: legacy shared helper regex is zero, filtered runtime `IJob.Run()` is zero, filtered runtime `Schedule().Complete()` is zero. The report keeps the last completed call-propagation baseline separately and marks the fresh full-tree scan as timed out.

Rejected Alternatives: Reporting a fresh exact forced-hot count without a completed scan was rejected. Spending more build/CPU budget on another heavy analyzer was rejected because the machine was already at 100% CPU and the user forbade unnecessary rebuild-style pressure.

Scalability potential: The gates protect low-tier frame time by removing known blocking shapes now; high/ultra still need a completed owner-review scan to erase hard-barrier residue safely.

Hardware Impact: Audit-only. Prevented extra CPU saturation while preserving truthful verification state.
