# Rationale_13US

Date: 2026-05-27
Agent: 13US
Domain: Inventory, Player, Player Control, Tools/Equipment interaction
Status: STATIC VERIFIED; LATEST CLI BUILD ATTEMPT TIMED OUT AFTER 124S AND WAS STOPPED; NO NEW COMPILE RESULT

## Decision 001 - Prompt Authority

Problem: The required batch extraction found no `<AGENT_PROMPT id="13US">` in `Docs/Tasks/CURRENT_BATCH.md`; the active domain was supplied directly by the user.
Solution: Treat the user assignment as the active domain boundary and record task count as 0 XML tasks. Use project docs and domain roster for authority.
Rejected Alternatives: Guessing a neighboring batch prompt such as `1317` would import another agent's task and violate strict parsing.
Scalability potential: No runtime impact.
Hardware Impact: 0 us/frame; prevents wrong-domain edits rather than saving CPU.

## Decision 002 - Mandate Selection

Problem: The domain spans inventory storage, player kinematics, controls, and tools, with high risk of hot-path GC and dependency violations.
Solution: Load inventory SoA, tools interaction, player kinematics/AUP, input abstraction, cold-DI/GlobalRegistry, SignalBus, zero-GC, and frame-budget mandates.
Rejected Alternatives: Reading all 35+ mandates would burn time and increase unrelated influence; reading only inventory would miss player-control defects.
Scalability potential: Low tier requires fixed buffers and cheap math; middle/high/ultra can buy richer haptics/tool visuals without changing gameplay truth.
Hardware Impact: Audit-only so far; target gains depend on defects found.

## Decision 003 - PlayerInventoryManager Runtime Context Route

Problem: `PlayerInventoryManager` mirrored player inventory/tool references through an `IUpdatable` Core-lane tick that read `BootstrapState` and could call `TryGetComponent` every frame, duplicating `PlayerRuntimeContextService`.
Solution: Move the service to `ISlowTickable` in `PriorityLayer.Player`, cache `IPlayerRuntimeContext` from cold/hot-swap routes, and allow `BootstrapState`/`TryGetComponent` fallback only during explicit cold initialization.
Rejected Alternatives: Keeping a per-frame tick with a throttle still leaves a hot GlobalRegistry/bootstrap polling owner; inventing a new signal would duplicate the existing player runtime context contract.
Scalability potential: Low tier removes repeated scene/component checks from player frames; middle/high/ultra retain the same service facade and can spend frame budget on player presentation instead of identity polling.
Hardware Impact: Estimated 4-20 us saved on low-end i3/MX350 during normal play, higher during missing-context respawn windows because repeated component fallback is no longer in cadence.

## Decision 004 - Active Tool Inventory Hashing

Problem: `PlayerToolManager.HasToolInInventory` recomputed `LocHash.Compute(targetData.PersistentId)` inside the grid-cell scan.
Solution: Resolve the target hash once before scanning and compare integer cell hashes only.
Rejected Alternatives: Building a new inventory index would cross owner boundaries and risk stale authority; comparing ScriptableObject references would not match the hash-only SOA grid route.
Scalability potential: Low tier avoids repeated string hashing per slot check; middle/high/ultra keep identical gameplay truth with spare budget available for tool animation/haptics.
Hardware Impact: Estimated 2-15 us per inventory tool-availability check on i3/MX350 depending on grid size and hash implementation.

## Decision 005 - Active Tool Durability Signal Keys

Problem: The tool tick drained durability signals and could compute `LocHash`/`Animator.StringToHash` when broken-signal rows were present.
Solution: Cache active tool item hash and metadata hash on equip/spawn, clear them on despawn, and compare durability events against cached numeric keys only.
Rejected Alternatives: Resolving hashes lazily after the first broken signal still leaves string work in the gameplay tick; changing signal payloads would be cross-domain.
Scalability potential: Low tier removes string work from the hot tool-event path; higher tiers preserve exact break/replacement behavior and can use the saved budget for richer break feedback.
Hardware Impact: Estimated 1-5 us saved on durability event frames; 0 allocation change.

## Decision 006 - Inventory Read Accessor Purity

Problem: `PlayerInventory.GetDurabilitiesReadOnly()` was a read accessor but called `SyncDurabilityBytesFromQuality()`, mutating `_durabilities`, `_itemDurability`, and `_durabilitySnapshotDirty`.
Solution: Move durability byte synchronization into `NotifyInventoryChanged()` owner mutation phase, before SoA snapshot publication, and make the accessor return the current read-only view only.
Rejected Alternatives: Renaming the accessor to imply mutation would still violate consumers' read expectations; forcing consumers to call a sync method spreads ownership.
Scalability potential: Low/middle tiers get deterministic read cost; high/ultra can issue more UI/crafting reads without hidden owner mutation.
Hardware Impact: Estimated 3-30 us moved out of arbitrary read sites into bounded inventory owner mutation; avoids repeated full-slot sync from presentation readers.

## Decision 007 - Input Dispatcher Cold Work Fence

Problem: `PreSimulationInputTick()` could call deterministic buffer acquisition/clearing and replay writer setup, including DataVault handle creation and file/thread setup.
Solution: Keep cold preparation in initialization, enable, and DataVault rebind paths; input tick now captures state and uses deterministic lanes only if already prepared.
Rejected Alternatives: Retrying file/thread setup from the player input frame hides IO latency in the most sensitive control path; disabling input when deterministic buffers are absent would break fallback playability.
Scalability potential: Low tier avoids first-frame/retry hitches in control input; higher tiers keep replay/deterministic lanes when cold setup succeeds.
Hardware Impact: Prevents millisecond-scale one-off stalls on weak storage/CPU; steady-state frame cost unchanged except removal of hot readiness work.

## Decision 008 - Deferred InventoryGrid DataVault Migration

Problem: `InventoryGrid` owns persistent `NativeArray` lanes directly, while project doctrine says cross-domain native ownership belongs in `GlobalDataVault`.
Solution: Record this as an unresolved architecture debt requiring a route-card migration of grid cells/anchors to vault handles; do not rewrite the placement core in the same hot-path cleanup patch.
Rejected Alternatives: Moving `InventoryGrid` storage ad hoc would touch placement, save/load, crafting, UI, and SOA snapshots at once without a safe compile/profiler window; leaving it unreported would be dishonest.
Scalability potential: Low tier would benefit from unified native lifetime and less alias risk; middle/high/ultra would gain safer batch query expansion. Requires dedicated migration.
Hardware Impact: Not changed in this patch. Potential future gain is stability/lifetime correctness more than direct microseconds.

## Decision 009 - Tool Availability Uses Owner Availability Route

Problem: `PlayerToolManager.HasToolInInventory()` manually scanned grid hashes and ignored craft reservations.
Solution: Resolve the prefab item hash once and call `PlayerInventory.CountAvailableTotal(targetHashId)`.
Rejected Alternatives: Keeping raw `GetItemHashAt()` scan was cheaper to leave but violated one-owner availability truth; adding a separate tool index risks stale state.
Scalability potential: Low tier reduces grid scan work and avoids false equip; middle/high/ultra keep the same gameplay truth and can spend saved frame budget on tool presentation.
Hardware Impact: Estimated 3-25 us saved on i3/MX350 depending grid size, plus correctness under concurrent crafting reservations.

## Decision 010 - Runtime Context Cache Clear Semantics

Problem: A slow tick with no `IPlayerRuntimeContext` could clear a valid cold fallback cache before the runtime context service appeared.
Solution: Explicitly clear cached player references only on shutdown or null player hot-swap; no-context slow tick returns without mutating cache.
Rejected Alternatives: Re-running `BootstrapState` and `TryGetComponent` from slow tick would revive hidden polling; never clearing on null hot-swap would leave stale references.
Scalability potential: Low tier avoids unnecessary cache churn; higher tiers keep respawn/hot-swap correctness.
Hardware Impact: 0-8 us saved in missing-context windows; main gain is avoiding stale/null oscillation.

## Decision 011 - Fast-Fail Inventory Read-Only Contract

Problem: `TryReadFastFailInventorySoA()` exposed writable `NativeArray<uint>` aliases to DataVault-backed fast-fail lanes.
Solution: Return `NativeArray<T>.ReadOnly` views and update fabricator validators/presentation to accept read-only lanes.
Rejected Alternatives: Documenting "do not write" leaves corruption possible; copying to managed arrays would allocate and break zero-GC.
Scalability potential: Low/middle/high/ultra all keep zero-copy reads without write alias risk.
Hardware Impact: 0 us/frame intended; prevents unbounded cross-domain mutation faults.

## Decision 012 - Craft-Lock Availability in Fast-Fail

Problem: Fast-fail quantity snapshots and fabricator fallback counted raw stack counts, so reserved ingredients could appear craftable.
Solution: Publish only `max(0, stack - reserved)` in owner fast-fail snapshots and subtract `GetCraftLockedCountsReadOnly()` in UI fallback.
Rejected Alternatives: Relying on `CurrentInventoryMask` alone is insufficient because a locked stack still carries the material bit; moving all UI checks through fabricator commit path would delay feedback.
Scalability potential: Low tier avoids failed craft churn; high/ultra can evaluate more visible recipes without semantic divergence.
Hardware Impact: Estimated 4-35 us per visible recipe-list rebuild, depending recipe count; prevents failed commit retries.

## Decision 013 - Haptic Synthesis Resolve-Only Frame Path

Problem: Haptic fallback and dispatcher synthesis routes could call buffer acquisition from frame execution.
Solution: Keep `EnsureHapticSynthesisNativeBuffers()` in cold registration/rebind; frame routes now require already initialized buffers and otherwise skip synthesis for that frame.
Rejected Alternatives: Retrying DataVault acquisition in `LateFrameTick` hides allocator/handle work in the control path; disabling haptics permanently after one miss is worse than recover-on-rebind.
Scalability potential: Low tier avoids millisecond hitch spikes; middle/high/ultra keep richer synthesized haptics once cold buffers are ready.
Hardware Impact: Prevents ms-scale acquisition stalls on weak storage/CPU; normal ready-state cost unchanged.

## Decision 014 - XR Runtime Activation Rebind

Problem: XR buffers were acquired during cold setup only; activating XR later could leave controller snapshots unavailable because `CaptureState()` is resolve-only.
Solution: Subscribe to `HectonXRRuntimeState.XRActiveChanged`; acquire XR buffers and controller bindings once on active, clear/release once on inactive.
Rejected Alternatives: Allowing `CaptureState()` to acquire buffers would restore hidden hot-path allocation; polling XR active state from input tick would add repeated work.
Scalability potential: Low tier pays one activation cost; high/ultra get live XR switching without per-frame retry.
Hardware Impact: 0 us steady state; avoids repeated failed reads and fixes runtime XR activation.

## Decision 015 - Buffered Input Check Purity

Problem: `CheckBufferedInput()` was a read-style API but incremented consumed telemetry without consuming a bit.
Solution: Remove the telemetry mutation from `CheckBufferedInput()` and keep the increment only in the true consume path.
Rejected Alternatives: Renaming the method would not fix existing callers; consuming the button bit from a check would change behavior.
Scalability potential: Stable telemetry across device tiers; no gameplay truth change.
Hardware Impact: 0-1 us; removes false black-box noise.

## Decision 016 - Public Unsafe Inventory Pointer Surface

Problem: `PlayerInventory` and `InventoryGrid` exposed public unsafe "read-only" pointers that callers could cast and mutate outside owner phase.
Solution: Delete unused public pointer APIs after repo-wide call-site scan; retain read-only `NativeArray<T>.ReadOnly` accessors.
Rejected Alternatives: Marking obsolete still leaves the mutation surface; wrapping raw pointers in comments is not a contract.
Scalability potential: All tiers gain safer native lifetime and owner-phase discipline; future DataVault migration has fewer external aliases to preserve.
Hardware Impact: 0 us/frame; reduces crash/corruption surface.

## Decision 017 - Managed Input Events Deferred

Problem: `IInputService` managed events are still invoked from the input frame path.
Solution: Defer suppression because `PauseControlsPanel` and `PDAControlsRebindUI` currently subscribe directly; correct fix is a UI-side migration to `SignalBus<PlayerInputSignal>`.
Rejected Alternatives: Silently stopping invokes would break menu/rebind controls; patching UI subscribers without a UI-domain pass risks behavioral regression.
Scalability potential: Future migration removes managed hot broadcast from all tiers.
Hardware Impact: Not changed. Expected future gain is small per input edge but important for doctrine consistency.

## Decision 018 - Bulk Transfer Validation Scalar Owner Phase

Problem: `PlayerInventory.TryRunBulkTransferValidation()` allocated two TempJob arrays, scheduled `InventoryTransferValidationJob`, then immediately forced completion for a small command-validation loop.
Solution: Resolve the source/target native lanes once and run the validation as scalar owner-phase code, producing the same `BulkTransferResult` fields without scheduler/readback overhead.
Rejected Alternatives: Keeping Burst for a tiny same-frame job violates the frame-time doctrine; deleting the public job type outright could break external source in a dirty multi-agent workspace.
Scalability potential: Low devices avoid scheduler and TempJob allocator spikes during inventory moves; middle/high/ultra keep identical transfer truth and can spend saved command latency on UI/audio feedback.
Hardware Impact: Estimated 8-45 us saved per bulk transfer command on i3/MX350, depending slot count and scheduler pressure; no steady-frame cost.

## Decision 019 - Deprecated Validation Job Fence

Problem: `InventoryTransferValidationJob` remained a public utility after the owner moved away from it, leaving an attractive path for future same-frame schedule/readback regression.
Solution: Mark the job `[Obsolete]` with a non-error warning while keeping binary/source compatibility.
Rejected Alternatives: Removing the type now would be a public API break; leaving it unmarked hides the doctrine violation.
Scalability potential: Prevents reintroduction of tiny jobs across all device tiers.
Hardware Impact: 0 us/frame; prevention-only.

## Decision 020 - Fast-Fail Validator Compatibility Wrapper

Problem: The read-only fast-fail validator signature was correct for ownership, but external mutable-array call sites could fail to compile after the read-only migration.
Solution: Restore a `NativeArray<uint>` overload that forwards through `AsReadOnly()` to the read-only validator.
Rejected Alternatives: Returning or accepting writable aliases as the canonical route reopens mutation risk; forcing every external caller to change in one pass is not safe with 20+ agents editing concurrently.
Scalability potential: All tiers keep zero-copy validation while preserving source compatibility.
Hardware Impact: 0 us/frame beyond the same validation work; wrapper cost is inlined and one-time per call.

## Decision 021 - Pause Controls Signal-Lane Migration

Problem: `PauseControlsPanel` subscribed to managed `OnCancel`, `OnTabNext`, and `OnTabPrevious` events from the input service; cancel during rebinding could also be seen by `PauseMenuController` through `PlayerInputSignal`, causing duplicate semantic handling.
Solution: Remove those managed subscriptions and let `PauseMenuController` call `PauseControlsPanel.ConsumePlayerInputSignals()` before its own cancel handling. The panel consumes cancel/tab from `SignalBus<PlayerInputSignal>` and suppresses parent cancel only when it actually canceled a rebind.
Rejected Alternatives: Globally suppressing `InputDispatcher` event invokes would break `PDAControlsRebindUI`; giving the panel its own Update loop would violate dispatcher ownership; ignoring duplicate cancel keeps a known UX/control bug.
Scalability potential: Low tier removes managed cancel/tab delegates from pause controls; middle/high/ultra get deterministic menu/rebind command ownership without changing gameplay input truth.
Hardware Impact: Estimated 1-6 us saved per pause-controls cancel/tab edge and avoids duplicate menu transition work; steady-frame signal scan is bounded by the existing 64-entry input signal lane and only called from pause menu flow.

## Decision 022 - PDA Controls Event Migration Deferred

Problem: `PDAControlsRebindUI` still uses managed cancel/tab events, but `PlayerPDA` also consumes the same `PlayerInputSignal` commands for PDA navigation.
Solution: Defer this until `PlayerPDA` owns a first-pass controls-panel signal hook, mirroring the pause-menu fix, so reset/cancel shortcuts and PDA tab navigation have a single edge owner.
Rejected Alternatives: Adding an independent UI tick to `PDAControlsRebindUI` risks ordering races with `PlayerPDA`; removing the events without owner integration drops rebind shortcuts.
Scalability potential: Future fix removes the remaining managed cancel/tab UI subscriber.
Hardware Impact: Not changed in pass 3; expected gain is small per input edge but important for signal-lane doctrine.

## Decision 023 - PDA Controls Signal-Lane Migration

Problem: `PDAControlsRebindUI` handled cancel/tab through managed input events while `PlayerPDA` handled the same commands through `SignalBus<PlayerInputSignal>`, so controls reset/cancel and PDA close/tab navigation could both react to one edge.
Solution: Add cold `controlsRebindUI` resolution in `PlayerPDA`; let the controls UI consume the current `PlayerInputSignal` snapshot first and return suppression flags for cancel/tab commands that it owns.
Rejected Alternatives: A standalone controls-panel tick would create ordering races and another hot owner; suppressing PDA navigation globally on controls tab would break cancel-to-close when no rebind is active.
Scalability potential: Low devices avoid duplicate command work; higher tiers keep deterministic tab/rebind behavior without changing input truth.
Hardware Impact: Estimated 1-8 us saved per PDA controls cancel/tab edge, mostly from eliminating duplicate command handling and managed delegate dispatch.

## Decision 024 - Cancel/Tab Managed Invoke Removal

Problem: After all first-party controls subscribers migrated, `InputDispatcher` and legacy `InputManager` still invoked `OnCancel`, `OnTabNext`, and `OnTabPrevious` in hot input paths.
Solution: Stop invoking those three managed events while retaining the public event declarations for source compatibility. First-party cancel/tab commands now route only through `SignalBus<PlayerInputSignal>`.
Rejected Alternatives: Removing the public events would break interface compatibility; leaving null-conditional invokes preserves a dead managed hot path with no repo subscribers.
Scalability potential: All device tiers get one route for cancel/tab truth; future systems must consume the bounded native signal snapshot.
Hardware Impact: Estimated 0-3 us saved per cancel/tab edge; main gain is removing duplicate command ownership and managed broadcast risk.

## Decision 025 - Bulk Transfer Compaction Scalar Owner Phase

Problem: `PlayerInventory.TryCompactIdenticalHashesAfterBulkTransfer()` allocated 12 TempJob arrays, copied full SoA lanes, scheduled serial `InventoryCompactionJob`, then forced same-frame completion before scalar repack.
Solution: Replace the job with scalar owner-phase greedy compaction over read-only vault lanes, using existing `_sortBuffer` plus a cold `ushort[]` merge-cap scratch so merge caps still come from `_grid.AnchorMaxStacks` while final placement descriptors remain catalog-derived.
Rejected Alternatives: Reusing `InventoryDefragJob` would sort and change merge semantics; adding fields to public `ItemPlacement` would expand public surface; keeping Burst for a serial same-frame command path violates the job policy.
Scalability potential: Low devices avoid allocator/scheduler/memcpy spikes during item transfer; middle devices get smoother inventory UI; high/ultra can spend saved command latency on richer transfer audio/haptics without changing gameplay truth.
Hardware Impact: Estimated 15-80 us saved per compaction on i3/MX350; inventory-to-inventory transfer pays this on source and target. Measured proof absent.

## Decision 026 - Deprecated Compaction Job Fence

Problem: `InventoryCompactionJob` remained a public utility after the owner path stopped using it, making same-frame compaction job regression easy.
Solution: Mark the job `[Obsolete]` with a warning and keep the type for source compatibility.
Rejected Alternatives: Deleting the type could break external source in a multi-agent workspace; leaving it unmarked hides an invalid job pattern.
Scalability potential: Prevention-only across all tiers; future compaction must stay owner-phase or prove a real amortized dispatcher-owned job window.
Hardware Impact: 0 us/frame.

## Decision 027 - Dead Gameplay Managed Input Invoke Removal

Problem: `InputDispatcher` published `SignalBus<PlayerInputSignal>` and still invoked dead managed gameplay events; `InputManager` also invoked movement/gameplay/action events with no source subscribers.
Solution: Remove only no-subscriber gameplay/movement/action invokes. Keep public events for compatibility and preserve live `InputManager` UI/display/debug invokes (`OnPause`, `OnNavigate`, `OnSubmit`, display-style, debug toggles).
Rejected Alternatives: Removing public events would break interface compatibility; removing subscribed UI invokes would break pause/fabricator/rebind UI; leaving dead invokes preserves a second first-party gameplay command route.
Scalability potential: Low tier removes managed delegate dispatch from input edges; middle/high/ultra keep deterministic `SignalBus<PlayerInputSignal>` ownership and can add visual/haptic response consumers without gameplay route duplication.
Hardware Impact: Estimated 0-5 us saved per affected input edge; main gain is eliminating duplicate command ownership and future subscriber regression risk.

## Decision 028 - Inventory Defrag Owner-Phase Command

Problem: `PlayerInventory.SortInventory()` copied data into defrag lanes, scheduled `InventoryDefragJob`, then forced same-frame completion for an explicit UI/command sort.
Solution: Move the same merge/compact/insertion-sort algorithm into `InventoryDefragCommand.Execute()` and call it directly from the inventory owner phase. Keep `InventoryDefragJob` as an obsolete wrapper only for future dispatcher-owned async windows with proof.
Rejected Alternatives: Deleting `InventoryDefragJob` would break external source in a dirty multi-agent workspace; leaving schedule/readback in a command path violates the tiny-job rule; changing to a different sort/merge algorithm risks save/UI ordering regressions.
Scalability potential: Low devices avoid scheduler/readback overhead on inventory sort; middle devices keep predictable UI command latency; high/ultra can spend the saved command budget on transfer audio/haptics without changing inventory truth.
Hardware Impact: Estimated 8-40 us saved per explicit sort command on i3/MX350. Measured proof absent.

## Decision 029 - Tool Durability Scalar LateFrame Pass

Problem: `ToolDurabilitySystem` scheduled a 32-slot `IJobParallelFor` for tool wear and forced public repair/break/reset calls to defer behind a pending job.
Solution: Preserve the old phase ordering by marking a pending decay pass in `Tick` and executing a scalar owner-phase pass in `LateFrameTick`, then syncing managed mirrors and publishing `ItemDurabilityChangedSignal`.
Rejected Alternatives: Applying durability wear immediately inside public drain methods would change event ordering and command coalescing; keeping Burst for 32 elements costs more scheduler overhead than math; removing queue semantics would risk repair/break racing active wear.
Scalability potential: Low tier removes a scheduler/readback path from active tool usage; middle/high tiers keep deterministic equipment wear and can spend budget on tool VFX/haptics; ultra tier can add presentation consumers through `SignalBus` without reintroducing gameplay-route duplication.
Hardware Impact: Estimated 4-25 us saved on active wear frames on i3/MX350. No allocation change; profiler proof still required.

## Decision 030 - Mass Recompute Job Deferred

Problem: `PlayerInventory` still has an async mass recompute job that can be force-completed by teardown and bulk-transfer preparation.
Solution: Record as deferred instead of patching ad hoc. The existing route has a SlowTick-to-LateFrame non-force completion window; removing the remaining force-complete risk needs separate result scratch ownership so scalar refresh and scheduled recompute never share `_derivedMassVolumeScratch`.
Rejected Alternatives: Deleting the job without scratch-route redesign could introduce concurrent result writes or stale mass publication; changing bulk-transfer preparation semantics could corrupt weight/volume limits.
Scalability potential: Future fix can remove rare command hitches while preserving exact mass truth across low/middle/high/ultra tiers.
Hardware Impact: Not changed. Potential future gain is burst-hitch prevention rather than steady-frame microseconds.

## Decision 031 - Inventory Mass Recompute Owner-Phase Kernel

Problem: Deeper pass proved the deferred concern was local and removable: `PlayerInventory` copied mass inputs into five snapshot lanes, scheduled a tiny mass job from SlowTick, and force-completed it from teardown and bulk-transfer preparation. The job shared `_derivedMassVolumeScratch` with scalar refresh, so the route carried fence risk and unnecessary DataVault scratch.
Solution: Replace the scheduled route with direct owner-phase `InventoryMassVolumeKernel.Execute()` when `_massCacheDirty` is set. Remove `_mass*Snapshot` lanes 16-20, `_massVolumeJobHandle`, `_massVolumeJobScheduled`, `_massVolumeJobInventoryVersion`, `ScheduleInventoryMassRecomputeJob()`, `TryBuildMassVolumeSnapshot()`, and `CompleteInventoryMassRecomputeJob()`. Keep result buffer ordinal 21 unchanged.
Rejected Alternatives: Keeping the job with a separate result scratch still preserves scheduler overhead for inventory-sized work; shifting DataVault ordinals would create compatibility risk; changing the formula or mass publication route would alter gameplay truth.
Scalability potential: Low tier avoids scheduler, snapshot-copy, and rare force-complete stalls. Middle tier gets deterministic inventory command latency. High and ultra tiers can spend the saved command budget on richer inventory transfer/haptic/audio presentation without changing mass truth.
Hardware Impact: Estimated 8-55 us saved per dirty mass refresh on i3/MX350, plus removal of five scratch lanes worth 18 bytes * cellCount from this inventory owner. Measured proof absent.

## Decision 032 - Tool/Cutter Job Windows Left Intact

Problem: Static scan found `ToolKinematicsRuntime` and `LaserCutterDodRuntime` job handles and force-complete sites, but the active gameplay paths are no-wait completion windows or teardown; one cutter force-complete is an editor/CI mock generator.
Solution: Do not rewrite those jobs in pass 7. Record them as audited and require profiler proof or a specific defect before replacing multi-lane tool simulation/VFX work.
Rejected Alternatives: Pattern-deleting all jobs would degrade tool IK/SDF/beam/cutter routes and violate evidence-based coding; moving teardown completion without a shutdown ownership redesign risks disposing live DataVault buffers.
Scalability potential: Low tier keeps amortized tool simulation instead of scalarizing large VFX/IK batches blindly. High and ultra tiers keep capacity for richer beam/cutter presentation.
Hardware Impact: 0 us changed. Risk avoided: unverified regression in tool kinematics/cutter visual sync.

## Decision 033 - PDA Loadout Durability Service Cache

Problem: `PDALoadoutTab` already cached player/inventory/expression routes, but loadout slot, summary, and activate paths still read `GlobalRegistry.ToolDurabilityService` directly during refresh.
Solution: Add cached `_toolDurabilityService`, populate it during cold setup, and update it through `GlobalRegistryServiceSlot.ToolDurabilityRuntime`.
Rejected Alternatives: Leaving registry reads in refresh violates cold-DI discipline; registering every visible tool mirror from UI would mutate gameplay owner state from presentation.
Scalability potential: Low tier avoids repeated service lookup during PDA refresh; middle/high/ultra keep richer PDA presentation without changing durability truth.
Hardware Impact: Estimated 0-3 us per loadout refresh on i3/MX350. Measured proof absent.

## Decision 034 - HUD Quickbar Player Context Cache

Problem: `HUDQuickBar.TryAutoResolveForTick()` could call `AutoResolve()` every 0.5 seconds while unresolved, and `AutoResolve()` read `GlobalRegistry.Player` from that tick retry path.
Solution: Cache `IPlayerRuntimeContext` in cold setup and update it on `GlobalRegistryServiceSlot.Player`; `AutoResolve()` now uses the cached context only.
Rejected Alternatives: Keeping a throttled registry read still violates hot path doctrine; adding a new signal for one UI resolver would create unnecessary global surface.
Scalability potential: Low tier avoids retry-tick service polling during boot/respawn UI gaps; higher tiers keep the same quickbar presentation path.
Hardware Impact: Estimated 0-4 us per unresolved retry. Main gain is route correctness.

## Decision 035 - Maintenance Station Hash Durability Read

Problem: `MaintenanceStationModule.Tick()` read slotted tool durability through `GetDurability(string toolID, ...)` at least once per repair tick and again after applying repair, using the compatibility string dictionary path.
Solution: Cache `_slottedToolItemHashId` when a tool is inserted/restored, register the centralized durability mirror on insert/restore/service hot-swap, and read current slotted durability via `GetDurability(uint itemHashId, ...)` in Tick.
Rejected Alternatives: Extending the public repair API with hash repair methods would change the interface mid-batch; reading string IDs in Tick preserves unnecessary string dictionary hashing; UI-only string reads remain because no hash mirror is guaranteed for every visible prefab.
Scalability potential: Low tier avoids string-key durability reads while a station is repairing. Middle/high/ultra can keep more active maintenance bays without turning repair state into a string-map workload.
Hardware Impact: Estimated 1-6 us per active maintenance repair tick on i3/MX350. Measured proof absent.

## Decision 036 - Tool Durability Hash Command Contract

Problem: `IToolDurabilityService` had hash reads for some active equipment paths, but repair/full-repair/break/reset remained string-only, forcing maintenance/tool replacement code back through compatibility dictionaries.
Solution: Add hash-first `TryReadDurability`, `TryReadBroken`, `TryRepairTool`, `TryRepairToolFull`, `TryBreakTool`, and `TryResetDurability` methods while retaining legacy string methods for cold compatibility. `ToolDurabilitySystem` resolves the native slot by item hash and queues commands by slot when a decay pass is pending.
Rejected Alternatives: Removing string methods would break existing cold/save/UI compatibility; making callers compute and pass `toolID` again preserves the string route; adding a second durability service would split truth ownership.
Scalability potential: Low tier avoids string dictionary work during repair/break/refresh edges. Middle/high/ultra keep one durability owner and can spend saved command budget on richer tool presentation without changing gameplay truth.
Hardware Impact: Estimated 1-8 us saved per affected durability command/read on i3/MX350. Measured proof absent.

## Decision 037 - Maintenance Station Hash Repair Route

Problem: `MaintenanceStationModule.Tick()` still repaired by `_slottedToolMetadata.toolID`, and repair completion still forced the string full-repair path.
Solution: Use `_slottedToolItemHashId` for active repair delta, full repair completion, inventory return, and deconstruct extraction. Register the slotted durability mirror when a tool is inserted/restored or the durability service hot-swaps.
Rejected Alternatives: Falling back to string repair in Tick would preserve the exact violation; recomputing `LocHash` on every return/deconstruct command is unnecessary once the slot owns the item hash; moving repair truth into maintenance would duplicate durability ownership.
Scalability potential: Low tier keeps active repair scalar and hash-addressed. Middle/high/ultra can run more maintenance stations or add stronger repair visuals without a string-map workload.
Hardware Impact: Estimated 1-8 us saved per active repair tick and 1-3 us per return/deconstruct command on i3/MX350. Measured proof absent.

## Decision 038 - Tool Replacement Broken State Hash Route

Problem: `PlayerToolManager` reset replacement durability and checked candidate prefab broken state through `metadata.toolID`.
Solution: Reset by item hash after consuming the broken inventory entry, and check replacement broken state via item hash first with metadata hash fallback for legacy registered slots.
Rejected Alternatives: String fallback in replacement search keeps dictionary/string dependence in a player-control decision; refusing replacement when metadata hash is absent would break old prefab/save states.
Scalability potential: Low tier avoids string reads on break/replacement edges. Higher tiers keep deterministic replacement behavior and can spend budget on break feedback and haptics.
Hardware Impact: Estimated 1-5 us saved per break/replacement edge on i3/MX350. Measured proof absent.

## Decision 039 - PDA/HUD Hash Durability Refresh

Problem: PDA loadout and HUD quickbar refresh paths still read durability/broken state through `toolID`, and PDA preset readiness recomputed item hashes directly in presentation code.
Solution: Add fixed-size slot hash caches and hash read helpers. HUD/PDA now use item hash then metadata hash fallback for durability state. PDA uses resolved item hash for inventory counts and preset readiness.
Rejected Alternatives: Registering durability mirrors from UI refresh would mutate gameplay owner state from presentation; keeping string reads because UI is "not physics" violates the same hot/refresh route rule; dictionary caches for every preset prefab would add extra managed surface without proof.
Scalability potential: Low tier keeps 4-slot UI refresh bounded and zero-GC. Middle/high/ultra can render richer loadout/quickbar state without changing durability truth or adding owner mutation.
Hardware Impact: Estimated 0-6 us saved per PDA/HUD refresh depending slot count and cache warmth on i3/MX350. Measured proof absent.

## Decision 040 - PlayerTool Hash-Only Durability Read/Drain

Problem: `PlayerTool.CurrentDurability`, `IsBroken`, and active drain still fell back to `_toolMetadata.toolID` string APIs when the cached item hash was unavailable, keeping a compatibility dictionary route inside active player/tool behavior.
Solution: Cache `_cachedToolMetadataHashId`, register the durability mirror from owner-phase spawn/equip/service-hot-swap paths, read durability/broken state by item hash then metadata hash, and drain through `TryDrainDurabilityByTime(uint, ...)` only.
Rejected Alternatives: Keeping string fallback preserves the exact hot-path violation; forcing every prefab to have `ToolData.PersistentId` before patching would block a local fix; registering mirrors from read accessors would make reads impure.
Scalability potential: Low tier avoids string-map fallback on tool read/drain edges. Middle/high/ultra keep identical gameplay truth and can spend the saved budget on tool animation, haptics, and break feedback.
Hardware Impact: Estimated 1-6 us saved per affected active tool read/drain edge on i3/MX350. Measured proof absent.

## Decision 041 - Tool Integration Guide Hash Repair Contract

Problem: `TOOL_SYSTEM_INTEGRATION_GUIDE.md` still instructed new tool code to use legacy string repair APIs, creating a documentation path back to the pass 9 violation.
Solution: Update the repair examples to `GlobalRegistry.ToolDurabilityService.TryRepairToolFull(itemHashId, ...)` and `TryRepairTool(itemHashId, ...)`; explicitly mark string repair APIs as cold compatibility/save/editor bridge only.
Rejected Alternatives: Leaving docs stale would reintroduce string commands in future tool work; deleting legacy API documentation entirely would hide why the methods still exist.
Scalability potential: All device tiers benefit indirectly because new tools will follow the hash contract instead of string-map durability commands.
Hardware Impact: 0 us/frame in this patch; prevention-only.

## Decision 042 - Forward Loadout Advice Broadphase Metadata Route

Problem: HUD/PDA loadout advice called `FieldLoadoutAdvisor.TryBuildForwardAdvice()` / `TryBuildForwardPresetName()`, which found a forward spatial candidate and then routed through direct component advice APIs. Those APIs call `FieldTargetDescriptor.TryResolve()` and `ResolveLocalOrParent<T>()`, including `GetComponentInParent<T>()`, from a refresh path.
Solution: Carry `SpatialQueryHit.Kind`, `SignalRole`, `Owner`, and distance into a `ForwardTargetInfo` struct. The forward route now classifies by signal role first, direct owner type second, and `SpatialTargetKind` fallback third. Legacy direct component APIs stay intact for cold/direct callers.
Rejected Alternatives: Caching resolved components in HUD/PDA would create stale presentation-owned authority; deleting the direct component APIs would break compatibility and smoke-test callers; accepting the traversal because the UI is throttled still violates hot/refresh route doctrine.
Scalability potential: Low tier avoids hierarchy traversal while updating HUD/PDA advice. Middle tier gets stable advice refresh cost under denser scenes. High and ultra tiers can spend the saved budget on richer PDA/HUD affordances without changing loadout truth ownership.
Hardware Impact: Estimated 2-20 us saved per advice refresh on i3/MX350 depending hierarchy depth and candidate type. Measured proof absent; Unity profiler not run.

## Decision 043 - Maintenance Repair Cost Hash Cache

Problem: `MaintenanceStationModule.Tick()` could call `TryPrepareRepairReservation()`, which called `ResolveFallbackItems()`, `PopulateRepairCosts(... catalog)`, `ResolveStructuralRepairItem()`, `ItemCatalog.FindById()`, and `LocHash.Compute()` while active repair waited for resources.
Solution: Cache fallback repair item hashes during fallback resolution and cache slotted structural/lubricant repair hashes when a tool is inserted, restored, or the player inventory/catalog service changes. Runtime reservation now assembles fixed hash/amount buffers through `AppendRepairCostHash(int, int)`.
Rejected Alternatives: Keeping the route throttled at 0.5s still leaves string/catalog work in active repair; authoring numeric repair hashes into every metadata asset is a larger data migration; resolving from UI/presentation would duplicate inventory truth.
Scalability potential: Low tier avoids catalog/hash spikes during active station repair. Middle tier can run several maintenance bays without string lookup cadence. High and ultra tiers can add richer repair VFX/audio while logistics truth stays hash-only.
Hardware Impact: Estimated 3-18 us saved per reservation attempt on i3/MX350, plus fewer cache misses from catalog string lookup. Measured proof absent.

## Decision 044 - Propulsion ForceMode No-Fix

Problem: Arendt flagged `PropulsionTool.ApplyDirectedForce()` using `ForceMode.Force` as if the route were a one-shot impulse.
Solution: Leave it unchanged in this pass. Local proof showed `PlayerToolManager` calls `UsePrimary()` and `UseSecondary()` while `PlayerInputAction.PrimaryFire` / `SecondaryFire` are held. `PropulsionTool.UsePrimary()` applies push every held tick when no cargo is locked, and secondary applies pull on held secondary after the lock edge. That is continuous thrust semantics, not a proven one-frame impulse.
Rejected Alternatives: Switching to `ForceMode.Impulse` because labels say "IMPULSE" would change gameplay force integration and likely over-accelerate held input. Renaming UX text is possible, but outside the proven hot-path defect fixed in pass 12.
Scalability potential: No runtime change. Correct future option is a split contract: edge launch uses impulse, held field uses force or fixed-step accumulator.
Hardware Impact: 0 us changed; avoided unproven physics regression.

## Decision 045 - PlayerInventory Trauma Dispatcher Cache

Problem: `PlayerInventory.SlowTick()` can dispatch radiation trauma, and `ResolveTraumaDispatcher()` searched through `survival.TryGetComponent()` and local `TryGetComponent()` when the dispatcher cache was empty.
Solution: Cache `_traumaDispatcher` from `IPlayerRuntimeContext.TraumaDispatcher` during cold setup and player hot-swap. Keep local `TryGetComponent` only as a cold setup fallback. Runtime resolver now returns the cached field only.
Rejected Alternatives: Leaving the lookup in a resolver hides component search in a read-style API; resolving through `survival` every radiation edge duplicates runtime context ownership; publishing a new signal for this narrow dependency would add surface without need.
Scalability potential: Low tier avoids component lookup when inventory radiation/thermal events fire. Middle/high/ultra keep the same trauma semantics and can spend frame budget on feedback, not dependency recovery.
Hardware Impact: Estimated 1-8 us saved per radiation/thermal dispatch edge when dispatcher cache would otherwise be cold. Measured proof absent.

## Decision 046 - PlayerPDA Survival Context Cache

Problem: `PlayerPDA.Tick()` marked `_survivalResolveDirty` when PDA battery drain needed a survival owner, then `LateFrameTick()` called `TryResolveSurvivalSystemFromRuntimeContext()`, which used `playerTransform.TryGetComponent(out survivalSystem)`.
Solution: Resolve `survivalSystem` from `IPlayerRuntimeContext.SurvivalSystem` during cold setup and player hot-swap. Remove the LateFrame retry and fail closed for battery drain when no survival owner is published.
Rejected Alternatives: Keeping LateFrame retry violates hot UI dependency rules; reading the scene from PDA is duplicate ownership; requiring a serialized survival reference would be fragile across respawn and multiplayer.
Scalability potential: Low tier avoids repeated missing-survival component checks. Higher tiers preserve deterministic PDA battery behavior through the player runtime context route.
Hardware Impact: Estimated 1-6 us saved per missing-survival retry frame. Measured proof absent.

## Decision 047 - HUDQuickBar AutoResolve Hot Fallback Removal

Problem: `HUDQuickBar.Tick()` calls `TryAutoResolveForTick()`. If unresolved, `AutoResolve()` could call `GameBootstrapper.TryGetCurrentPlayerTransform()` and `TryGetComponent<PlayerToolManager>()`.
Solution: Remove the scene/component fallback from `AutoResolve()`. Tool manager now resolves only from `IPlayerInventoryService.ToolManager` or cached `IPlayerRuntimeContext.ToolManager`; otherwise the quickbar remains degraded until the owner publishes the dependency.
Rejected Alternatives: Keeping a 0.5s throttled fallback still violates UI Tick dependency rules; forcing a new global lookup route duplicates `IPlayerRuntimeContext`; hiding the lookup behind another helper would not change behavior.
Scalability potential: Low tier avoids retry spikes during spawn/respawn gaps. Middle/high/ultra preserve the same quickbar presentation once the owner context is available.
Hardware Impact: Estimated 2-12 us saved per unresolved quickbar retry on i3/MX350. Measured proof absent.

## Decision 048 - Flashlight Runtime Equipment ID Bridge

Problem: `PlayerFlashlight.TryGetCentralEquipmentSnapshot()` stored an `IBatteryTool` but cast it back to concrete `FlashlightTool` to read `RuntimeToolId`, coupling the player flashlight presentation owner to one tool class and blocking other battery tools from using the same central equipment snapshot contract.
Solution: Add `IRuntimeEquipmentIdProvider` as a narrow optional interface beside `IBatteryTool`. `FlashlightTool` implements it explicitly and returns its owner runtime equipment id. `PlayerFlashlight` now asks for that interface and uses the uint id only.
Rejected Alternatives: Adding `RuntimeEquipmentId` to `IBatteryTool` would force all battery tools to pretend they publish central equipment state; casting to `PlayerTool` would still couple the flashlight to gameplay inheritance instead of the needed contract; keeping `FlashlightTool` cast preserves the exact violation.
Scalability potential: Low tier avoids concrete-type dependency and keeps snapshot reads scalar. Middle/high/ultra can attach richer battery tools to the central equipment engine without adding more class-specific branches.
Hardware Impact: Estimated 0-3 us saved per snapshot call from avoiding a failed concrete cast path when a non-flashlight battery tool is bound; main gain is dependency correctness.

## Decision 049 - PDA Localization Read-Model Cache

Problem: `PDAInventoryTab` rebuilds selected item details from `LateFrameTick`, but detail refresh read `GlobalRegistry.LocalizationMadnessPresentation` directly and the helper `ResolveLocalizedSpan()` hid the same registry lookup.
Solution: Cache `_localizationMadnessPresentation` during cold setup and update it on `GlobalRegistryServiceSlot.LocalizationRuntime` hot-swap. Detail refresh and madness visual state now consume the cached read-model.
Rejected Alternatives: Leaving registry reads because UI is not physics still violates hot refresh doctrine; making localization static would break hot-swap and language/corruption runtime ownership; resolving through another helper would hide the same global lookup.
Scalability potential: Low tier avoids service lookup during PDA detail refresh. Middle/high/ultra keep the same PDA corruption presentation and can spend UI budget on richer detail rendering.
Hardware Impact: Estimated 0-4 us saved per selected-detail refresh on i3/MX350. Measured proof absent.

## Decision 050 - Item Description Hash Cold Cache

Problem: PDA selected-detail refresh used item description table keys for localization/corruption and could compute `LocHash` from strings during refresh through `GetDescriptionSpan()` and `ResolvePdaLoreSourceHash()`.
Solution: Cache `ItemData.DescriptionTableHashId` during item cold hash refresh and add a `LocalizedTextReference.ResolveSpanOrFallback(..., tableKeyHash)` overload. PDA lore source hash now uses the cached description hash or persistent item hash.
Rejected Alternatives: Computing the hash once per PDA selection still leaves hidden string work in presentation; adding a PDA-side dictionary cache adds managed lifetime and invalidation surface; changing corruption to ignore description identity would alter presentation semantics.
Scalability potential: Low tier keeps selected-item description refresh bounded and zero-GC. Middle/high/ultra can run richer PDA corruption effects without string hash work in the refresh path.
Hardware Impact: Estimated 1-12 us saved per selected-detail refresh depending item key length and cache warmth on i3/MX350. Measured proof absent.

## Decision 051 - PDAEvents Typed Signal Bridge

Problem: `PDAEvents` was already queue-backed and fixed-slot, but it remained a feature-local native queue plus `IPDAEventListener` dispatch with no typed `SignalBus<T>` snapshot lane. That kept future first-party PDA consumers on a concrete callback surface and left no standard signal telemetry/drop counter for PDA open/tab/map/marker/logbook events.
Solution: Reuse the existing explicit 64-byte `PDAEventPayload` as the typed signal payload by implementing `ISignal`. Configure `SignalBus<PDAEventPayload>` from `PDAEvents.EnsureInitialized()` with full 32-event capacity and an 8-event low-tier frame cap. Publish the already-normalized payload during `FlushPending()` and `DrainWithoutDispatch()` after side effects are applied, preserving legacy listener delivery while creating a bounded first-party snapshot bridge.
Rejected Alternatives: Creating a separate `PDAEventSignal` DTO would duplicate the same ABI and invite drift. Publishing from `Enqueue()` would expose events before owner-phase drain and before no-listener side effects. Migrating all `IPDAEventListener` consumers now was rejected because `SignalBus<T>` snapshots may shift delivery timing and each PDA UI panel needs separate phase proof.
Scalability potential: Low tier can consume at most 8 PDA payload snapshots per signal flush while legacy UI remains bounded. Middle tier keeps full PDA state fan-out without new managed allocations. High and ultra tiers can add projector/chrome/telemetry consumers through snapshots instead of class callbacks, spending the saved coupling budget on richer PDA presentation.
Hardware Impact: Estimated 0-4 us saved per future migrated PDA event consumer by avoiding direct interface callback fan-out and enabling span-based snapshot reads. Current patch adds a cold native lane and one bounded push per drained PDA event; measured proof absent.

## Decision 052 - ToolKinematics Black-Box Dump Export Worker

Problem: `ToolKinematicsRuntime.FinishPendingFrameCompletion()` detected fault/NaN telemetry and then synchronously created `Docs/AgentLogs`, opened `FileStream`, and wrote `BinaryWriter` output on the completion path. That path is a runtime post-fixed owner path; file I/O and directory creation there violate zero-GC/frame-time doctrine and can hide a disk stall exactly on the failure frame.
Solution: Keep fault detection in the owner completion path, but change the response to a bounded snapshot only. The fault path copies the fixed telemetry ring into a preallocated `ToolKinematicsTelemetryEntry[]`, records frame/capacity/cursor metadata, and signals a cold background worker. The worker writes `Docs/AgentLogs/Dump_13US.bin` with the same explicit telemetry entries and catches I/O/permission/general failures into `LastBlackBoxDumpFailureCode`.
Rejected Alternatives: Keeping synchronous dump write was rejected because rare faults are still runtime frames and can freeze the moment that needs evidence. Moving the write to `SlowTick` was rejected because it would put I/O back on the gameplay dispatcher and could lose evidence during teardown. Allocating a task/threadpool closure on fault was rejected because it would trade file I/O for managed fault-path allocation. Moving the telemetry ring into a new DataVault contract was rejected as a broader route-card migration not needed to remove the proven I/O violation.
Scalability potential: Low tier copies a fixed maximum ring and returns, avoiding disk stalls on i3/MX350. Middle tier keeps the same 300-frame evidence with no gameplay truth change. High tier can dump richer diagnostics from the worker without changing completion cost. Ultra tier can add optional postmortem exporters as worker-side consumers only, never as gameplay-frame work.
Hardware Impact: Estimated 100-4000+ us stall avoided on weak storage/CPU during a fault dump. Normal frames unchanged. Fault frame pays a bounded copy of up to `MaxToolCapacity * BlackBoxCapacity` telemetry entries into a preallocated array; file write moves off the runtime completion path. Measured proof absent; local `dotnet build` timed out before compile result.
