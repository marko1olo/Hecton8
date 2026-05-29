# Rationale_14POLIK

Status: COMPLETE_TARGETED_BUILD_VERIFIED_SIXTH_PASS

Problem: Runtime resource spawn work was committed from `LateFrameTick`, including pooled node activation, template application, spatial registration, and active-node list insertion.
Solution: Move pending node deactivation and spawn processing into `SlowTick`; keep `LateFrameTick` reserved for visual-only work.
Rejected Alternatives: Keep spawn in late frame and call it "presentation"; that mutates resource truth after simulation settle.
Scalability potential: Low tier avoids late-frame spawn spikes; middle/high/ultra can raise `maxSpawnsPerSlowTick` without visual phase drift.
Hardware Impact: Removes pooled spawn and list growth from the render-adjacent phase; worst-case low-end spike reduction is burst dependent.

Problem: Pressure metamorphism and autonomous extraction held DataVault-owned job workspaces through scheduled jobs.
Solution: Move those job workspaces to owner-owned persistent `NativeArray` buffers; use DataVault only for cold handles where still needed.
Rejected Alternatives: Cross-frame write locks or mutation guards around scheduled jobs.
Scalability potential: Low tier can cadence jobs; high/ultra can increase candidate/module counts without vault contention.
Hardware Impact: Removes unbounded deadlock/stall vector and avoids lock convoy on i3/MX350-class CPUs.

Problem: Scavenging loot resolution used DataVault request/result/telemetry buffers as hot job scratch.
Solution: Keep loot tables in vault, but route hot request/result/telemetry scratch through owner persistent `NativeArray`; publish resolved yields only after `PostSimulationTick` fence.
Rejected Alternatives: Publish from the job chain or hold mutation guard until a later frame.
Scalability potential: Low tier gets bounded signal output; high/ultra can raise visual scavenge signal capacity without DTO changes.
Hardware Impact: Removes cross-frame guard lifetime and keeps signal transfer zero-GC.

Problem: `PlayerInventory.LateFrameTick` applied scavenging item grants and inventory commands directly.
Solution: Late frame now copies `ItemAcquiredSignal` and `InventoryCommandSignal` DTOs into fixed cold arrays; `SlowTick` applies inventory truth. Respawn death AUP sideband is captured with the command.
Rejected Alternatives: Delay by reading SignalBus next slow tick; frame snapshots would be lost.
Scalability potential: Low tier absorbs bursty loot signals by hash merge; high/ultra keeps richer visual feedback from the signal lane.
Hardware Impact: Removes inventory grid mutation from late frame; no per-frame managed allocation.

Problem: Plant regrowth invoked UnityEvent from the regrow timer path reachable by `Tick`.
Solution: Queue segment ids into a preallocated int array and flush `OnSegmentRegrown` from `LateFrameTick`.
Rejected Alternatives: Invoke managed UnityEvent inside regrow simulation.
Scalability potential: Low tier avoids managed callback jitter; high/ultra can add visual/audio regrow presentation safely.
Hardware Impact: Fixed array cost equals segment count; hot path writes one int.

Problem: Runtime resource template application warmed loot oracle payload with hierarchy scan.
Solution: `ApplyRuntimeTemplate` now caches payload from deterministic template yield hash only; child hierarchy scan remains cold scene-authored `Awake` fallback.
Rejected Alternatives: `GetComponentInChildren` during pooled resource spawn.
Scalability potential: Weak devices skip scene traversal during resource bursts; high tier spends saved time on visual density.
Hardware Impact: Avoids transform hierarchy traversal per runtime node.

Problem: Fabrication/fabricator write-lock helpers released failed acquisitions manually, not inside strict `finally`.
Solution: Add ownership-transfer flags and `finally` release on failed buffer validation.
Rejected Alternatives: Manual release after condition branch.
Scalability potential: Same data route; stronger failure safety on all tiers.
Hardware Impact: Deadlock risk reduced; no frame cost change.

Problem: Verification had to avoid compile spam while proving syntax and hot-path contracts.
Solution: One full build attempt was allowed only under CPU/process throttle, timed out, and was not repeated. Final proof used Roslyn `csc.dll` syntax probes using `/out:NUL`, method-body guards, and `git diff --check`.
Rejected Alternatives: Repeating full solution builds after timeout or while another compiler was active.
Scalability potential: Shared workstation remains available to other agents.
Hardware Impact: Full build spam avoided; final csc syntax diagnostics 0.

Problem: Third-pass review found inert DataVault lifecycle residue after hot scratch had already moved to owner-owned persistent `NativeArray`.
Solution: Removed extractor job/state DataVault handles, resource metamorphism vault workspace handle, and scavenging request/resolved/telemetry vault handles. Kept only cold/shared vault ownership for loot tables, biome modifiers, distribution audit, and CSV scratch.
Rejected Alternatives: Leave dead vault handles in lifecycle code; that preserves a false ownership route and makes deadlock proof depend on convention.
Scalability potential: Low tier avoids lifecycle rebinding stalls; middle/high/ultra can scale extractor, metamorphism, and scavenge request volume through owner-local buffers without vault lock coupling.
Hardware Impact: Removes cold rebinding work and eliminates a write-lock class from three hot systems; i3/MX350-class gain is mainly stall-risk removal, not raw frame-time arithmetic.

Problem: `LootMagnetSystem.LateFrameTick` completed pull jobs and directly committed pickup acquisition into `PlayerInventory`, including death-cache item restores and acquisition truth signals.
Solution: Split completed pull handling into late-frame presentation/proxy pose plus fixed-array pending truth queues. `SlowTick` now applies real pickup and data-only death-cache inventory mutations; successful acquisition visual feedback is queued back to `LateFrameTick`.
Rejected Alternatives: Keep `TryHandleInventoryPickup` in `LateFrameTick`; it mutates inventory, persistent pickup state, and world proxy lifetime after simulation settle. Move all commit to `SlowTick`; that would also move visual proxy pose updates out of the visual phase.
Scalability potential: Low tier gets bounded 64-acquisition cold queues and zero-GC transfer; middle/high/ultra can keep denser acoustic/wake/debris feedback through continuous `GlobalQualityWeight` budgets without changing truth ownership.
Hardware Impact: Removes inventory grid mutation and death-cache drain from render-adjacent phase. Expected gain on i3/MX350 is burst-jitter reduction, not steady-state throughput.

Problem: Fourth-pass review still allowed completed pull job commit and vault slot mutation from `LateFrameTick`, and the first pending-pickup split could clear a vault slot before inventory acceptance was known.
Solution: Register a cold `IDispatcherSystem` bridge for `DispatcherPhase.PostSimulation`; complete pull jobs, mutate vault slots, and apply real pickup inventory truth only after the dispatcher job fence. Keep `LateFrameTick` limited to fixed-queue presentation signals and proxy pose upload. Real pickups now commit in PostSimulation with immediate vault reconciliation: rejected pickups restore active flags and physics, partial pickups keep remaining quantity in a valid active slot, fully accepted pickups clear the slot.
Rejected Alternatives: Keep non-blocking completion in `LateFrameTick`; that is render-adjacent truth mutation. Keep fire-and-forget pending pickup queues after `ClearVaultSlot`; that can strand a kinematic pickup if inventory rejects or partially accepts.
Scalability potential: Low tier keeps bounded acquisition work after simulation settle and avoids late-frame stalls; middle/high/ultra can raise visual density through acoustic/wake budgets without changing truth ownership or DTO layout.
Hardware Impact: Removes job completion/vault mutation from the render-adjacent phase and fixes a correctness path that could leave pickup physics suppressed. i3/MX350 gain is burst jitter reduction and dead-state avoidance; high/ultra spend saved visual phase time on presentation only.

Problem: After direct PostSimulation pickup commit, the old fixed pending-pickup arrays had no caller and still allocated cold storage.
Solution: Delete the pending real-pickup arrays, queue method, apply method, and clear method. Keep only death-cache pending DTO storage and presentation DTO storage.
Rejected Alternatives: Leave dead arrays as future-proofing; that creates a false ownership route and wastes owner memory.
Scalability potential: Low tier saves memory and code path complexity; middle/high/ultra keep the same presentation budgets without a second truth route.
Hardware Impact: Removes four fixed arrays per loot magnet owner and eliminates dead branch review cost.

Problem: Compile verification had to respect other agents' active builds.
Solution: Waited while external `dotnet build .\Hecton8.Editor.csproj` and `csc.exe` held the compiler lane. Ran targeted `Assembly-CSharp.csproj` builds only after code edits and only after CPU/process gates were clear; final build was `dotnet build .\Assembly-CSharp.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false --no-restore`.
Rejected Alternatives: Run full solution or parallel build during another agent's compiler pass; repeat builds without source changes.
Scalability potential: Shared workstation remains stable under 20+ agents.
Hardware Impact: Final targeted compile completed in 14.23s with 0 warnings and 0 errors; no build-server churn.

Problem: `ProceduralOreSpawner.LateFrameTick` still had render-adjacent truth work: spawn job retirement, spawn output commit, AUP/drop-pod signal drain, and telemetry write.
Solution: Move completed spawn job retirement/commit into `SlowTick` through `CommitCompletedSpawnJobIfReady`; remove AUP/drop-pod drains, spawn commit, and telemetry writes from `LateFrameTick`.
Rejected Alternatives: Keep job retirement in late frame and label it presentation; it mutates vault-backed geology truth after simulation settle.
Scalability potential: Low tier avoids render-adjacent generation spikes; middle/high/ultra can spend visual phase on matrix upload and denser dormant ore drawing.
Hardware Impact: Removes 20-120 us burst jitter estimate from late phase during sector generation; exact cost is sector-density dependent.

Problem: Procedural ore depletion/runtime-shift paths acquired separate DataVault mutation guards for each buffer, creating a stacked guard route.
Solution: Use one `GeologyVaultMutationGuardMask` for guarded geology transactions; the first buffer acquisition obtains the single mask, subsequent acquisitions reuse it, and release remains in `finally`.
Rejected Alternatives: Keep per-buffer mutation guard stacking because it "usually" orders buffers consistently; that still leaves a multi-lock deadlock surface.
Scalability potential: Low tier avoids lock convoy under mining/shift bursts; middle/high/ultra can scale ore capacity without increasing guard cardinality.
Hardware Impact: Deadlock vector removed; no fake steady-state microsecond claim.

Problem: Loot magnet partial success handling still had two correctness faults.
Solution: Partial real pickups restore suppressed Rigidbody/collision state before remaining active. Death-cache restore now uses a state-preserving `TryAddItemWithState(..., out addedQuantity)` overload and requeues only the remainder.
Rejected Alternatives: Treat `false` return as full reject after partial inventory mutation; that duplicates recovered items. Leave partial pickups kinematic; that strands world loot.
Scalability potential: Same fixed acquisition capacity on low/middle/high/ultra; correctness does not branch by quality.
Hardware Impact: Stability/correctness fix; no throughput claim.

Problem: Loot magnet ticks could register even if the PostSimulation dispatcher bridge failed.
Solution: Register PostSimulation bridge before fast/slow/late tick lanes, abort tick registration if it fails, hard-gate `FastTick`, and on dispatcher hot-swap drop tick lanes before re-registering.
Rejected Alternatives: Allow pull scheduling with no guaranteed post-simulation completion path.
Scalability potential: Stable under dispatcher capacity pressure and duplicate-hash failure on all tiers.
Hardware Impact: Prevents orphaned scheduled jobs and suppressed pickup state; no frame-time claim.

Problem: Sixth-pass compile proof had to avoid build spam under active parallel agents.
Solution: Waited through external `dotnet build Hecton8.slnx` and `dotnet build .\Hecton8.Core.csproj` compiler windows. Launched one targeted `Assembly-CSharp.csproj` build only after no compiler process was active and CPU dropped below 50%.
Rejected Alternatives: Launch another `dotnet build` or Roslyn probe during an active compiler process; run full solution.
Scalability potential: Shared workstation remains usable for parallel agents.
Hardware Impact: CPU contention avoided. Final targeted compile: 0 warnings, 0 errors, 30.11s.

Problem: `LootMagnetSystem` could reach `EnsureManagedSidecars()` from fast/slow/post-simulation routes through `TryResolveVaultViews(... allowAllocate:true)`.
Solution: Treat sidecar allocation as cold only. Hot routes now request existing vault views with `allowAllocate:false`; allocation-enabled resolve fails closed unless sidecars are already valid.
Rejected Alternatives: Let runtime capacity mismatch allocate arrays during a pull or telemetry frame.
Scalability potential: Low tier avoids GC spikes; middle/high/ultra keep the same fixed queue semantics and spend budget on presentation signals.
Hardware Impact: Edited hot bodies allocate 0 B/frame; capacity mismatch now fails closed until cold setup.

Problem: `ScavengePopulator.LateFrameTick` performed spawn/cull truth work and transitive `TryGetComponent`/string ID work through `ProcessSpawnQueue`.
Solution: Move spawn queue processing and chunk culling into `SlowTick`; `LateFrameTick` only applies diagnostics after slow-phase work.
Rejected Alternatives: Keep pooled resource node activation in late frame because it is time-sliced; it still mutates world truth after settle.
Scalability potential: Low tier keeps chunk work time-sliced; middle/high/ultra can raise `maxSpawnsPerTick` without dirtying visual phase.
Hardware Impact: Removes pooled spawn/cull and transitive component lookup from render-adjacent phase; exact burst cost depends on resource density.

Problem: Fabricator, harvestable outcrop, and item settle paths mutated GlobalRegistry lanes from dispatcher callbacks.
Solution: Remove unregister calls from hot callbacks. Fabricator stays registered after idle until lifecycle cleanup; outcrop late tick no-ops when no pending presentation work; item fixed tick no-ops after settle until lifecycle cleanup.
Rejected Alternatives: Unregister from inside the same lane that may be iterating.
Scalability potential: Stable dispatcher iteration on all tiers; low tier pays only one cheap branch on idle registered objects.
Hardware Impact: Eliminates hot registry mutation hazard; idle branch cost is accepted over iterator mutation risk.
