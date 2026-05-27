# LOG_13US

Date: 2026-05-27
Agent: 13US
Domain: Inventory, Player, Player Control, Tools/Equipment interaction
Prompt source: direct user assignment; no `<AGENT_PROMPT id="13US">` was found in `Docs/Tasks/CURRENT_BATCH.md`.

## Report 2026-05-27

What was wrong:
- `PlayerInventoryManager` was registered as `IUpdatable` in `PriorityLayer.Core` and ran `SyncInventoryContext()` every frame. That duplicated `PlayerRuntimeContextService`, read `BootstrapState`, and could call `TryGetComponent` in cadence.
- `PlayerToolManager.HasToolInInventory()` recomputed `LocHash.Compute(targetData.PersistentId)` inside the inventory grid scan.
- `PlayerToolManager.ConsumeEquippedToolDurabilitySignals()` could hash active tool identity from strings while draining hot durability signals.
- `PlayerInventory.GetDurabilitiesReadOnly()` was a read accessor that mutated `_durabilities`, `_itemDurability`, and `_durabilitySnapshotDirty`.
- `InputDispatcher.PreSimulationInputTick()` and `DrainToolHaptics()` could reach deterministic DataVault buffer acquisition/clearing, replay writer setup, or haptic buffer ensure from frame paths.

What was done:
- `Assets/_Project/Scripts/Core/PlayerInventoryManager.cs`: moved service from `IUpdatable` Core lane to `ISlowTickable` Player lane; cached `IPlayerRuntimeContext`; allowed `BootstrapState`/`TryGetComponent` fallback only during cold initialization; slow tick now reads context snapshot only.
- `Assets/_Project/Scripts/PlayerToolManager.cs`: computes inventory target hash once before grid scan; caches active tool item hash and metadata hash on equip; clears cached hashes on despawn; durability signal matching compares numeric keys only.
- `Assets/_Project/Scripts/PlayerInventory.cs`: removed mutation from `GetDurabilitiesReadOnly()`; durability byte sync now runs in `NotifyInventoryChanged()` owner mutation phase before SOA snapshot publication.
- `Assets/_Project/Scripts/Core/InputDispatcher.cs`: removed deterministic buffer ensure and replay writer setup from `PreSimulationInputTick()`; removed deterministic buffer ensure from `DrainToolHaptics()`; XR native buffer acquisition is cold-call only.
- `Docs/Tasks/Status_13US.md` and `Docs/AgentLogs/Rationale_13US.md`: recorded prompt authority, mandate selection, decisions, verification, and deferred risks.

Cinematic cheats used:
- Replaced per-frame player identity resync with cached runtime-context snapshot. No gameplay truth change.
- Replaced repeated string/key derivation with cached numeric hashes on tool equip.
- Moved durability presentation byte sync to inventory owner mutation instead of making read paths do hidden work.
- Input/replay cold work is now startup/rebind work; frame tick only consumes prepared lanes.

Exact microseconds saved:
- `PlayerInventoryManager`: estimated 4-20 us/frame on i3/MX350 during normal play; higher during missing-context respawn windows because repeated component fallback is no longer in cadence.
- `PlayerToolManager.HasToolInInventory`: estimated 2-15 us per availability check depending on grid size and hash implementation.
- `PlayerToolManager` durability signal path: estimated 1-5 us on durability event frames; no allocation change.
- `PlayerInventory.GetDurabilitiesReadOnly`: estimated 3-30 us moved out of arbitrary read sites into bounded owner mutation, depending on slot count and reader frequency.
- `InputDispatcher`: prevents millisecond-scale one-off stalls from DataVault handle acquisition, buffer clear, file map, and thread setup entering player input/haptic frames. Steady-state microsecond delta is path-dependent and not claimed without profiler.

Verification:
- `git diff --check -- <changed files>` passed. Only CRLF warnings were emitted.
- Static `rg` gates confirmed no `IUpdatable`/`Tick` remains in `PlayerInventoryManager`.
- Static `rg` gates confirmed `PreSimulationInputTick()` and `DrainToolHaptics()` no longer call deterministic buffer ensure or replay setup.
- Static `rg` gates confirmed `GetDurabilitiesReadOnly()` no longer calls `SyncDurabilityBytesFromQuality()`.
- Static `rg` gates confirmed durability signal matching in `PlayerToolManager` uses cached active hashes.
- Compile was not run: CPU policy blocked it. `Get-Counter '\Processor(_Total)\% Processor Time'` sampled 100, 100, 100 percent on the final check. No `dotnet`/`csc` process was listed.

Known remaining issues:
- `InventoryGrid` still owns persistent native arrays directly. Correct fix requires a DataVault-backed grid storage route-card migration across placement, save/load, crafting, UI, and SOA snapshot consumers.
- `GlobalRegistry.PlayerInventoryMassKg` remains a legacy cold seed for submarine fluid dynamics. Live consumers already use `InventoryChangedSignal`; removing the registry cache is a cross-domain physics migration.

## Report 2026-05-27 Pass 2

What was wrong:
- `PlayerToolManager.HasToolInInventory()` used raw grid scanning instead of owner availability, so craft-locked tools could still appear available.
- `PlayerInventoryManager` could clear cold cached player references on slow tick when runtime context was temporarily absent.
- `TryReadFastFailInventorySoA()` returned writable `NativeArray<uint>` aliases to fast-fail lanes.
- Fast-fail quantities and fabricator fallback counted raw stacks, not `stack - craftLocked`.
- Haptic synthesis could still acquire DataVault buffers from frame routes when dispatcher haptic route was missing or buffers were lost.
- Runtime XR activation after cold setup could leave XR input buffers missing.
- `CheckBufferedInput()` mutated telemetry from a read/check API.
- Public unsafe inventory pointer APIs exposed raw native state without owner-phase fences.

What was done:
- `PlayerToolManager` now calls `PlayerInventory.CountAvailableTotal(targetHashId)`.
- `PlayerInventoryManager` uses explicit cache clearing and keeps no-context slow tick non-mutating.
- Fast-fail inventory reads now return `NativeArray<T>.ReadOnly`; validators and fabricator presentation consume read-only lanes.
- Owner fast-fail snapshot and UI fallback subtract craft-lock reservations.
- Haptic synthesis frame routes use resolve-only buffer gates; cold acquisition stays in registration/rebind.
- `InputDispatcher` subscribes to `HectonXRRuntimeState.XRActiveChanged` and performs one acquire/clear on XR activation changes.
- `CheckBufferedInput()` no longer increments consumed telemetry.
- Unused public unsafe pointer APIs were removed from `PlayerInventory` and `InventoryGrid`.

Cinematic cheats used:
- Missing haptic buffers skip optional synthesized rumble instead of stalling player control.
- Existing triangle-wave UI shake remains a fake; no physical simulation was added.

Exact microseconds saved:
- Tool availability: estimated 3-25 us per check on i3/MX350 and correct craft-lock gating.
- Haptic frame route: protects against ms-scale DataVault acquisition stalls on weak CPU/storage; ready-state cost unchanged.
- XR activation: 0 us steady state; prevents repeated failed XR snapshot attempts after runtime activation.
- Fast-fail availability: estimated 4-35 us per visible recipe-list rebuild by avoiding false-positive craft attempts and commit retries.
- Read-only alias removal: 0 us/frame; prevents unbounded corruption cost.

Verification:
- `git diff --check` passed for all touched domain files; only CRLF warnings.
- `rg` verified fast-fail public read lanes are `NativeArray<T>.ReadOnly`.
- `rg` verified removed public unsafe inventory pointer APIs have no remaining repo call sites.
- `rg` verified haptic frame routes call `TryResolveHapticSynthesisRequiredBuffers()` rather than `EnsureHapticSynthesisNativeBuffers()`.
- Compile skipped by rule. CPU samples were 97.28, 99.61, 100, 100 percent; no `dotnet`/`csc` process was listed.

Known remaining issues:
- `InventoryGrid` still owns persistent native lanes directly. Correct fix is a DataVault-backed storage migration with save/load, crafting, UI, and placement route-card coverage.
- Managed `IInputService` events still exist and are invoked because UI rebind/pause panels subscribe directly. Suppression requires a UI migration to `SignalBus<PlayerInputSignal>`.

## Report 2026-05-27 Pass 3

What was wrong:
- `PlayerInventory.TryRunBulkTransferValidation()` used TempJob arrays plus `InventoryTransferValidationJob.Schedule()` and immediate forced completion for a small owner command.
- `InventoryTransferValidationJob` remained a public easy-to-reuse tiny job after the owner no longer needed it.
- `CraftingFastFailValidator.TryEvaluateRecipeAvailability()` had moved to read-only lanes without a mutable-array compatibility wrapper.
- `PauseControlsPanel` still subscribed to managed cancel/tab events even though `PlayerInputSignal` already carried those commands.
- `PauseControlsPanel` cancel during rebinding and `PauseMenuController` cancel could observe the same edge through different routes.

What was done:
- `Assets/_Project/Scripts/PlayerInventory.cs`: replaced scheduled bulk-transfer validation job with scalar owner-phase validation using resolved native lanes once.
- `Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs`: marked `InventoryTransferValidationJob` obsolete instead of deleting the public type.
- `Assets/_Project/Scripts/CraftingSystem.FastFail.cs`: added a mutable `NativeArray<uint>` overload that forwards to the read-only fast-fail validator.
- `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`: removed managed cancel/tab subscriptions and added bounded `SignalBus<PlayerInputSignal>` consumption for cancel/tab commands.
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`: calls the controls panel signal consumer before menu cancel handling and suppresses parent cancel only when a rebind cancel was actually consumed.

Cinematic cheats used:
- Replaced a scheduled micro-job with direct scalar command validation because the job overhead was the fake cost, not useful simulation.
- Kept menu/rebind input as a single native signal edge instead of parallel managed callbacks.

Exact microseconds saved:
- Bulk transfer validation: estimated 8-45 us per command on i3/MX350, plus removal of two TempJob allocations and a forced scheduler readback.
- Pause controls cancel/tab: estimated 1-6 us per edge by removing internal managed cancel/tab delegates and duplicate cancel route work.
- Fast-fail compatibility wrapper: 0 us/frame; preserves source compatibility while keeping the read-only route.
- Obsolete job marker: 0 us/frame; prevents future same-frame tiny-job regression.

Verification:
- `git diff --check -- <pass 3 files>` passed. Only CRLF warnings were emitted.
- `rg` verified `PlayerInventory.TryRunBulkTransferValidation()` no longer schedules `InventoryTransferValidationJob`.
- `rg` verified `BulkTransferValidationTempLabel` and `BulkTransferFailureTempLabel` were removed.
- `rg` verified `PauseControlsPanel` no longer subscribes to `input.OnCancel`, `input.OnTabNext`, or `input.OnTabPrevious`.
- `rg` verified `PDAControlsRebindUI` remains the only inspected controls UI direct cancel/tab managed-event subscriber.
- Compile was not run: CPU policy blocked it. `Get-Counter '\Processor(_Total)\% Processor Time'` sampled 93.58 percent. No `dotnet`/`csc` process was listed.

Known remaining issues:
- `PDAControlsRebindUI` still needs a `PlayerPDA`-owned signal-lane migration; independent UI ticking would risk input ordering bugs.
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and must cover placement, save/load, crafting, UI, and SOA snapshot consumers.

## Report 2026-05-27 Pass 4

What was wrong:
- `PDAControlsRebindUI` still subscribed to managed `OnCancel`, `OnTabNext`, and `OnTabPrevious`.
- `PlayerPDA` consumed the same cancel/tab commands from `PlayerInputSignal`, so one input edge could both reset/cancel a rebind UI action and navigate/close the PDA.
- `InputDispatcher` and legacy `InputManager` still invoked cancel/tab managed events after first-party subscribers were gone.

What was done:
- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`: removed cancel/tab managed subscriptions, added bounded `SignalBus<PlayerInputSignal>` consumption, and returned suppression flags for controls-owned cancel/tab edges.
- `Assets/_Project/Scripts/PlayerPDA.cs`: added cold controls-panel resolution and lets controls consume/suppress cancel/tab before PDA close/history/tab navigation.
- `Assets/_Project/Scripts/Core/InputDispatcher.cs`: removed hot `OnCancel?.Invoke()`, `OnTabNext?.Invoke()`, and `OnTabPrevious?.Invoke()` calls.
- `Assets/_Project/Scripts/Input/InputManager.cs`: removed legacy cancel/tab managed event invokes.

Cinematic cheats used:
- Collapsed duplicate UI command paths into one native signal edge; no new simulation, polling, or UI tick owner.

Exact microseconds saved:
- PDA controls cancel/tab: estimated 1-8 us per edge by removing duplicate handling and managed callback route.
- Input dispatcher cancel/tab: estimated 0-3 us per edge by removing dead managed event invokes.
- Steady-state frame cost unchanged; the route is edge-driven through existing signal snapshots.

Verification:
- `rg` found no `OnCancel +=`, `OnTabNext +=`, `OnTabPrevious +=`, `OnCancel?.Invoke`, `OnTabNext?.Invoke`, or `OnTabPrevious?.Invoke` in `Assets/_Project/Scripts`.
- `git diff --check -- <pass 4 files>` passed. Only CRLF warnings were emitted.
- Compile was not run: policy blocked it because `dotnet` process `21804` was running. Final CPU sample was 49.94 percent.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and requires a dedicated route-card pass across placement, save/load, crafting, UI, and SOA snapshot consumers.

## Report 2026-05-27 Pass 5

What was wrong:
- `PlayerInventory.TryCompactIdenticalHashesAfterBulkTransfer()` still used 12 TempJob arrays, full-lane copies, `InventoryCompactionJob.Schedule()`, and immediate forced completion after bulk transfer.
- `InventoryCompactionJob` remained an unfenced public utility despite being a serial same-frame compaction path.
- `InputDispatcher` still invoked no-subscriber managed gameplay events after publishing `SignalBus<PlayerInputSignal>`.
- `InputManager` still invoked no-subscriber movement/gameplay/action events. Live UI/display/debug event invokes had real subscribers and were not removed.

What was done:
- `Assets/_Project/Scripts/PlayerInventory.cs`: replaced bulk-transfer compaction job path with scalar owner-phase greedy compaction over read-only vault lanes, using `_sortBuffer` and a cold merge-cap scratch buffer.
- `Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs`: marked `InventoryCompactionJob` obsolete instead of deleting the public type.
- `Assets/_Project/Scripts/Core/InputDispatcher.cs`: removed managed gameplay re-broadcasts for PDA, inventory, tool slots, interact, primary, and secondary commands.
- `Assets/_Project/Scripts/Input/InputManager.cs`: removed unused movement/gameplay/action managed invokes while preserving subscribed UI/display/debug routes.

Cinematic cheats used:
- Replaced a serial scheduled compaction job with direct scalar owner code because the job was presentation of sophistication, not useful parallelism.
- Collapsed gameplay input to the existing native signal edge instead of keeping parallel managed event routes.

Exact microseconds saved:
- Bulk transfer compaction: estimated 15-80 us per compaction on i3/MX350 by removing TempJob allocation, sentinel registration, full-lane memcpy, scheduler dispatch, and forced completion.
- Inventory-to-inventory transfer: estimated 30-160 us worst-case because source and target both compact.
- Input dispatcher/manager dead invokes: estimated 0-5 us per affected edge; main value is route correctness and lower regression surface.
- Obsolete compaction job marker: 0 us/frame; prevention-only.

Verification:
- `rg` found no `BulkTransferCompaction*`, `RegisterTempJobArray`, `DisposeTempJobArray`, `new NativeArray<...Allocator.TempJob`, compaction `Schedule()`, or old compaction repack helpers in `PlayerInventory.cs`.
- `rg` found `InventoryCompactionJob` only as an obsolete compatibility type.
- `rg` found no gameplay `On*?.Invoke` in `InputDispatcher.cs` and no unused gameplay/movement/action `On*?.Invoke` in `InputManager.cs`.
- `rg` confirmed live `InputManager` invokes remain for subscribed UI/display/debug routes.
- `git diff --check -- <pass 5 files>` passed. Only CRLF warnings were emitted.
- Compile was not run: CPU policy blocked it. No `dotnet`/`csc` process was listed; CPU samples were 92.43, 96.52, and 57.89 percent.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and must be done as a route-card change across placement, save/load, crafting, UI, and SOA snapshot consumers.

## Report 2026-05-27 Pass 6

What was wrong:
- `PlayerInventory.SortInventory()` still scheduled `InventoryDefragJob` and forced same-frame completion for an explicit inventory command.
- `ToolDurabilitySystem` scheduled a 32-slot `IJobParallelFor` for active tool wear. This is too small for scheduler overhead, and repair/break/reset commands had to defer behind the pending job.

What was done:
- `Assets/_Project/Scripts/Inventory/InventoryDefragJob.cs`: split the existing defrag algorithm into `InventoryDefragCommand` and kept `InventoryDefragJob` only as an obsolete compatibility wrapper.
- `Assets/_Project/Scripts/PlayerInventory.cs`: changed explicit inventory sort to call `InventoryDefragCommand.Execute()` directly.
- `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`: replaced scheduled durability decay with a scalar owner-phase LateFrame pass. Tick now marks pending wear; LateFrame applies decay, syncs managed mirrors, flushes breakdown flags, and drains queued commands.
- Removed local Burst/Jobs scheduler dependencies from `ToolDurabilitySystem`.

Cinematic cheats used:
- Tool degradation remains scalar normalized durability; no per-part material simulation or physical corrosion model was added.
- Inventory sorting remains deterministic data movement; no visual simulation was introduced.

Exact microseconds saved:
- Inventory sort: estimated 8-40 us per explicit sort command on i3/MX350 by removing scheduler/readback.
- Tool durability: estimated 4-25 us on active wear frames by replacing 32-slot job scheduling with scalar LateFrame math.
- Measured proof absent; Unity profiler not run.

Verification:
- `rg` found `InventoryDefragJob` only in the obsolete wrapper and `PlayerInventory` using `InventoryDefragCommand.Execute()`.
- `rg` found no `using Unity.Burst`, `using Unity.Jobs`, `JobHandle`, `IJob`, `Schedule()`, `TryComplete()`, `DispatcherJob`, `_scheduledDecayHandle`, or `DurabilityDecayJob` in `ToolDurabilitySystem.cs`.
- `git diff --check -- <pass 6 files>` passed. Only CRLF warnings were emitted.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was attempted after CPU dropped and no `dotnet`/`csc` process was active.
- Result: failed on existing external Candice SQLite references only: `CandiceSQLiteProvider.cs(1,12)` missing `Mono.Data`, and `CandiceSQLiteProvider.cs(489,60)` missing `SqliteDataReader`.
- Full output captured in `.codex_tmp/13US_build_errors.txt`.

Known remaining issues:
- `PlayerInventory` async mass recompute still has a separate SlowTick-to-LateFrame job window and force-complete edges during teardown/bulk-transfer preparation. This needs separate scratch/result ownership before removal.
- Tool/equipment scheduled simulation/VFX routes (`ToolKinematicsRuntime`, `LaserCutterDodRuntime`, auxiliary equipment) still require profiler-backed audit before changing; they are not the same proven 32-slot/tiny-command defect fixed here.

## Report 2026-05-27 Pass 7

What was wrong:
- `PlayerInventory` mass/volume/radiation refresh still copied live inventory lanes into five snapshot buffers, scheduled a tiny mass job from SlowTick, and exposed hidden `Complete` edges in LateFrame, teardown, and bulk-transfer preparation.
- The job shared `_derivedMassVolumeScratch` with the scalar mass refresh path, so the route carried unnecessary fence risk and scratch ownership ambiguity.

What was done:
- `Assets/_Project/Scripts/PlayerInventory.cs`: replaced scheduled mass recompute with direct owner-phase `InventoryMassVolumeKernel.Execute()` when `_massCacheDirty` is set.
- Removed `_massAnchorHashSnapshot`, `_massStackCountSnapshot`, `_massUnitMassSnapshot`, `_massUnitVolumeSnapshot`, `_massUnitRadiationSnapshot`, `_massVolumeJobHandle`, `_massVolumeJobScheduled`, and `_massVolumeJobInventoryVersion`.
- Removed `ScheduleInventoryMassRecomputeJob()`, `TryBuildMassVolumeSnapshot()`, and `CompleteInventoryMassRecomputeJob()`.
- Kept DataVault result buffer ordinal 21 unchanged to avoid buffer ID drift.
- Audited `ToolKinematicsRuntime` and `LaserCutterDodRuntime`; no local rewrite made because active jobs are no-wait multi-lane simulation/VFX, teardown, or editor/CI mock paths.

Cinematic cheats used:
- Inventory mass remains a scalar aggregate over SoA lanes. No physical container simulation or per-item physics truth was added.
- Tool/cutter simulation was not scalarized blindly; visual/IK/cutter job capacity remains intact until profiler data proves a cheaper fake is required.

Exact microseconds saved:
- Inventory dirty mass refresh: estimated 8-55 us on i3/MX350 by removing five-lane snapshot copies, job scheduling, and forced completion risk.
- DataVault scratch pressure: five snapshot lanes removed, approximately 18 bytes * cellCount plus lane metadata for each player inventory owner.
- Measured proof absent; Unity profiler not run.

Verification:
- `rg` found no `_mass*Snapshot`, `_massVolumeJob*`, `ScheduleInventoryMassRecomputeJob`, `TryBuildMassVolumeSnapshot`, `CompleteInventoryMassRecomputeJob`, `InventoryMassVolumeJob`, `JobHandle`, `.Schedule()`, `TryComplete`, `DispatcherJob`, `Unity.Jobs`, `Unity.Burst`, `IJob`, or `BurstCompile` in `PlayerInventory.cs`.
- `git diff --check -- Assets/_Project/Scripts/PlayerInventory.cs Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs Assets/_Project/Scripts/Inventory/InventoryDefragJob.cs` passed. Only CRLF warnings were emitted.
- Compile was not run: policy blocked it. No `dotnet`/`csc` process was listed, but CPU samples were 88.56, 77.73, and 100 percent.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and requires route-card coverage across placement, save/load, crafting, UI, and SOA snapshot consumers.
- `ToolKinematicsRuntime` and `LaserCutterDodRuntime` still need profiler-backed runtime proof before any architectural rewrite. Current pass found no proven tiny same-frame gameplay job in those active routes.

## Report 2026-05-27 Pass 8

What was wrong:
- `PDALoadoutTab` read `GlobalRegistry.ToolDurabilityService` during loadout refresh paths instead of using a cached/hot-swap dependency.
- `HUDQuickBar.TryAutoResolveForTick()` could reach `GlobalRegistry.Player` through `AutoResolve()` while unresolved.
- `MaintenanceStationModule.Tick()` read slotted tool durability through the string-key compatibility route.

What was done:
- `Assets/_Project/Scripts/UI/PDALoadoutTab.cs`: added cached `_toolDurabilityService`, cold setup binding, and `ToolDurabilityRuntime` hot-swap update.
- `Assets/_Project/Scripts/HUDQuickBar.cs`: added cached `_playerRuntimeContext`; `AutoResolve()` now uses the cached context instead of reading `GlobalRegistry.Player` from tick retry.
- `Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs`: added cached `_slottedToolItemHashId`, registered the durability mirror on insert/restore/service hot-swap, and changed repair Tick durability reads to `GetDurability(uint)`.

Cinematic cheats used:
- No physical repair simulation was added. Maintenance remains scalar durability math and transactional resource reservation.
- UI presentation remains read-only; PDA/HUD do not mutate durability owner state to force hash mirrors.

Exact microseconds saved:
- PDA loadout refresh: estimated 0-3 us per refresh by removing registry lookup from refresh paths.
- HUD unresolved retry: estimated 0-4 us per retry by removing `GlobalRegistry.Player` from the tick fallback.
- Active maintenance repair: estimated 1-6 us per repair tick by replacing string-key durability reads with hash reads.
- Measured proof absent; Unity profiler not run.

Verification:
- `git diff --check -- Assets/_Project/Scripts/UI/PDALoadoutTab.cs Assets/_Project/Scripts/HUDQuickBar.cs Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs` passed. Only CRLF warnings were emitted.
- `rg` found PDA/HUD durability/player registry reads only in cold setup/register paths after the patch.
- `rg` found `MaintenanceStationModule.Tick()` using `ReadSlottedDurability()` and no direct `_slottedToolMetadata.toolID` durability read in the tick body.
- Compile was not run: `dotnet` process `67876` was active, and CPU samples were 35.32, 41.11, and 58.47 percent.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and requires route-card coverage across placement, save/load, crafting, UI, and SOA snapshot consumers.
- PDA/HUD still use string durability reads for UI-only prefab refresh where no registered item-hash mirror is guaranteed. Correct removal needs a read-only durability-slot/hash availability contract or explicit mirror-registration policy.

## Report 2026-05-27 Pass 9

What was wrong:
- `IToolDurabilityService` exposed string-only repair/full-repair/break/reset commands, so consumers that already had item hashes still had to route through compatibility string dictionaries.
- `MaintenanceStationModule.Tick()` used string repair commands during active repair and string full-repair on completion.
- `PlayerToolManager` replacement logic reset/checks broken state by `metadata.toolID`.
- `HUDQuickBar` and `PDALoadoutTab` used string durability/broken reads in refresh/action paths.

What was done:
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: expanded `IToolDurabilityService` with hash-first read and command methods.
- `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`: implemented hash-first read/repair/full-repair/break/reset, slot-based command queuing, and hash mirror reads.
- `Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs`: repair Tick, repair completion, return, restore, and deconstruct now use the cached slotted item hash.
- `Assets/_Project/Scripts/PlayerToolManager.cs`: replacement reset and broken checks now use item/metadata hash reads.
- `Assets/_Project/Scripts/HUDQuickBar.cs`: durability bar refresh uses cached item/metadata hashes and `TryReadDurability`.
- `Assets/_Project/Scripts/UI/PDALoadoutTab.cs`: slot, summary, action, and preset readiness paths use resolved hashes instead of durability string reads.

Cinematic cheats used:
- Repair remains scalar durability math; no physical maintenance simulation was introduced.
- PDA/HUD remain presentation-only readers and do not mutate durability owner state to manufacture mirrors.
- Broken/replacement logic uses existing durability owner snapshots instead of a second per-prefab state model.

Exact microseconds saved:
- Active maintenance repair: estimated 1-8 us per repair tick by replacing string repair/read/full-repair with hash slot operations.
- Player replacement break edge: estimated 1-5 us by removing string broken checks/reset.
- PDA/HUD refresh: estimated 0-6 us per refresh depending cache warmth and slot count.
- Measured proof absent; Unity profiler not run.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs Assets/_Project/Scripts/PlayerToolManager.cs Assets/_Project/Scripts/HUDQuickBar.cs Assets/_Project/Scripts/UI/PDALoadoutTab.cs` passed. Only CRLF warnings were emitted.
- `rg` found no `RepairTool(_slottedToolMetadata.toolID)`, `RepairToolFull(_slottedToolMetadata.toolID)`, `ResetDurability(metadata.toolID)`, PDA/HUD `GetDurability(...toolID)`, or PDA/HUD `IsBroken(...toolID)` in edited hot/refresh routes.
- `rg` confirmed `ToolDurabilitySystem` hash methods and `QueueDurabilityCommandBySlot`.
- Compile was not run: multiple `dotnet` processes were active and CPU samples were 100, 100, and 100 percent.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and requires route-card coverage across placement, save/load, crafting, UI, and SOA snapshot consumers.
- `PlayerTool` still has a string fallback when cached item hash is unavailable. Current path uses hash first; fallback removal requires proving every authored tool prefab has stable `ToolData.PersistentId`.

## Report 2026-05-27 Pass 10

What was wrong:
- `PlayerTool.CurrentDurability` and `PlayerTool.IsBroken` still fell back to string-key durability reads through `_toolMetadata.toolID`.
- `PlayerTool.ApplyDurabilityDrain()` still fell back to string-key drain when a stable item hash was unavailable.
- `TOOL_SYSTEM_INTEGRATION_GUIDE.md` still taught legacy string repair APIs, which would reintroduce the pass 9 violation in new tool code.

What was done:
- `Assets/_Project/Scripts/PlayerTool.cs`: added `_cachedToolMetadataHashId`, metadata-hash caching, and owner-phase durability mirror registration on spawn, equip, and durability-service hot-swap.
- `Assets/_Project/Scripts/PlayerTool.cs`: changed durability and broken reads to item hash first, metadata hash fallback, no string fallback.
- `Assets/_Project/Scripts/PlayerTool.cs`: changed active durability drain to `TryDrainDurabilityByTime(uint, ...)` with item hash first and metadata hash fallback.
- `Assets/_Project/Scripts/Tools/TOOL_SYSTEM_INTEGRATION_GUIDE.md`: changed repair examples to hash-first `IToolDurabilityService` commands and documented string APIs as cold compatibility/save/editor bridge only.

Cinematic cheats used:
- Durability remains scalar normalized tool health. No physical wear, corrosion, per-part fracture, or material simulation was added.
- Compatibility is handled by cached numeric hashes and mirror registration, not a second durability truth model.

Exact microseconds saved:
- Active tool durability/broken read and drain fallback: estimated 1-6 us per affected edge on i3/MX350 by removing string-map API fallback.
- Tool integration docs: 0 us/frame; prevention of future string-command regressions.
- Measured proof absent; Unity profiler not run.

Verification:
- `git diff --check -- Assets/_Project/Scripts/PlayerTool.cs Assets/_Project/Scripts/Tools/TOOL_SYSTEM_INTEGRATION_GUIDE.md` passed. Only CRLF warnings were emitted.
- `rg` found no `GetDurability(...toolID)`, `IsBroken(...toolID)`, `DrainDurabilityByTime(..._toolMetadata.toolID)`, or `GlobalRegistry.ToolDurability.Repair*` examples in the pass 10 files.
- `rg` found no `new List/Dictionary/HashSet`, `.ToString()`, `string.Format`, or `foreach` in `PlayerTool.cs`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was attempted after checking no `dotnet`/`csc` process was active and CPU average was 49 percent.
- Result: failed on existing external Candice SQLite references only: `CandiceSQLiteProvider.cs(1,12)` missing `Mono.Data`, and `CandiceSQLiteProvider.cs(489,60)` missing `SqliteDataReader`.
- Later concise `ErrorsOnly` rebuild was not rerun because CPU average was 97 percent, above the 50 percent build ban.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and requires route-card coverage across placement, save/load, crafting, UI, and SOA snapshot consumers.
- Legacy string methods in `ToolDurabilitySystem` remain intentionally for cold compatibility/save/editor bridge routes. First-party player/tool hot paths audited in pass 10 no longer use them.

## Report 2026-05-27 Pass 11

What was wrong:
- `HUDQuickBar` and `PDALoadoutTab` call `FieldLoadoutAdvisor.TryBuildForwardPresetName()` / `TryBuildForwardAdvice()` from refresh paths.
- The forward advice route collected spatial hits non-alloc, but then converted the hit back into a `Component` and used the direct component advice APIs.
- The direct component APIs use `FieldTargetDescriptor.TryResolve()` and `ResolveLocalOrParent<T>()`; the latter calls `GetComponentInParent<T>()`. That is a hierarchy traversal inside a HUD/PDA advice refresh route.

What was done:
- `Assets/_Project/Scripts/FieldLoadoutAdvisor.cs`: added `ForwardTargetInfo` carrying `Component Source`, `SpatialTargetKind Kind`, `FieldTargetRole SignalRole`, and projected distance from `WorldSpatialHashGrid.SpatialQueryHit`.
- `Assets/_Project/Scripts/FieldLoadoutAdvisor.cs`: changed `TryBuildForwardAdvice()` and `TryBuildForwardPresetName()` to classify forward hits through signal role, direct owner type checks, and kind fallback.
- `Assets/_Project/Scripts/FieldLoadoutAdvisor.cs`: added `FieldTargetRole` overloads for descriptor advice/preset names so the forward route does not need descriptor/component traversal.
- Legacy `TryBuildAdvice(Component)` and `TryBuildPresetName(Component)` remain for direct/cold compatibility and are no longer used by the HUD/PDA forward route.

Cinematic cheats used:
- Advice remains a cheap classification fake from broadphase metadata, not a semantic scene scan or physical target inspection.
- No new visual simulation, tool physics, raycast expansion, or per-target analysis was added.
- The richer high-tier path remains presentation-only: HUD/PDA can show stronger advice copy later without changing gameplay truth or authority route.

Exact microseconds saved:
- HUD/PDA forward advice refresh: estimated 2-20 us per refresh on i3/MX350 by avoiding parent hierarchy traversal and descriptor/component resolution after the broadphase hit.
- Dense module hierarchy or missing direct component case is the worst case; flat direct-owner hit is near the low end.
- Measured proof absent; Unity profiler not run.

Verification:
- `git diff --check -- Assets/_Project/Scripts/FieldLoadoutAdvisor.cs` passed. Only CRLF warning was emitted.
- `rg` confirmed forward advice call sites in `HUDQuickBar`, `PDALoadoutTab`, and `ToolTrialRangeRuntimeSmokeTester`.
- `rg` confirmed legacy `ResolveLocalOrParent<T>()` / `GetComponentInParent<T>()` remain only behind direct component APIs, not the forward HUD/PDA route.
- Compile was not run: `dotnet` PID 62864 and `VBCSCompiler` PID 6448 were active, and CPU average was 53 percent, violating the build policy.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and requires route-card coverage across placement, save/load, crafting, UI, and SOA snapshot consumers.
- Direct `FieldLoadoutAdvisor.TryBuildAdvice(Component)` and `TryBuildPresetName(Component)` still use component traversal. They are kept for compatibility; no first-party hot HUD/PDA route found using them in pass 11.

## Report 2026-05-27 Pass 12

What was wrong:
- Arendt's read-only audit found `MaintenanceStationModule.Tick()` can call `TryPrepareRepairReservation()` while a station waits for resources.
- That path called `ResolveFallbackItems()`, `PopulateRepairCosts(... catalog)`, `ResolveStructuralRepairItem()`, `ItemCatalog.FindById()`, and `LocHash.Compute()`.
- Result: active repair retry could perform catalog/string/hash work instead of using precomputed inventory hash ids.

What was done:
- `Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs`: added cached fallback structural repair hash, cached lubricant hash, and cached slotted structural/lubricant repair hashes.
- `MaintenanceStationModule`: fallback item hashes are resolved when fallback items are resolved, not from repair Tick.
- `MaintenanceStationModule`: slotted repair cost hashes are resolved when a tool is inserted, restored, or the player inventory/catalog service changes.
- `MaintenanceStationModule`: `TryPrepareRepairReservation()` now uses fixed hash/amount buffers only and no longer needs an `ItemCatalog`.
- `MaintenanceStationModule`: `AppendRepairCost(ItemData, int)` became `AppendRepairCostHash(int, int)`.
- `PropulsionTool` `ForceMode.Force` was not changed: local proof showed primary/secondary tool use is held-action cadence through `PlayerToolManager`, not a proven one-shot impulse.

Cinematic cheats used:
- Repair cost calculation remains a cheap scalar missing-durability ratio, not a part-by-part damage simulation.
- Runtime logistics reservation uses hash ids and fixed buffers; visual repair richness can be added separately without touching resource truth.
- Propulsion physics was left stable instead of forcing an unproven impulse interpretation.

Exact microseconds saved:
- Maintenance reservation retry: estimated 3-18 us per attempt on i3/MX350 by removing catalog string lookup and per-attempt `LocHash.Compute`.
- Larger gains are possible when multiple stations retry in the same second or the item catalog is cold in cache.
- Measured proof absent; Unity profiler not run.

Verification:
- `git diff --check -- Assets/_Project/Scripts/FieldLoadoutAdvisor.cs Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs` passed. Only CRLF warnings were emitted.
- `rg` found `TryPrepareRepairReservation()` and no `PopulateRepairCosts(... catalog)`, `AppendRepairCost(...)`, `ResolveStructuralRepairItem`, `GetDurability(_slottedToolMetadata.toolID)`, `RepairTool(_slottedToolMetadata.toolID)`, or `RepairToolFull(_slottedToolMetadata.toolID)` in `MaintenanceStationModule`.
- `rg` confirmed `PlayerToolManager` calls primary/secondary tool actions while input actions are held, so the propulsion `ForceMode.Force` route is not proven wrong.
- Compile was not run: `dotnet` PID 47232 and `VBCSCompiler` PID 35836 were active, and CPU average was 63 percent, violating the build policy.

Known remaining issues:
- Full `InventoryGrid` DataVault-backed storage migration remains unresolved and requires route-card coverage across placement, save/load, crafting, UI, and SOA snapshot consumers.
- `ToolDurabilitySystem` string overloads remain intentionally for cold compatibility/save/editor bridge routes.
- `PlayerFlashlight` still concrete-casts `IBatteryTool` back to `FlashlightTool`; a narrow runtime equipment ID interface is the correct fix.
- `ToolKinematicsRuntime` black-box dump still uses synchronous file I/O from a runtime completion path; the dump trigger should become a cold export request without removing the fixed 300-frame ring.

## Report 2026-05-27 Pass 13

What was wrong:
- Hooke's read-only audit found three narrow hot dependency violations that local source proof confirmed.
- `PlayerInventory.SlowTick()` radiation/thermal trauma routes could call a resolver that searched components for `TraumaDispatcher`.
- `PlayerPDA` battery drain could schedule a LateFrame retry that searched the player transform for `HectonSurvivalSystem`.
- `HUDQuickBar.Tick()` unresolved retry could fall back to `GameBootstrapper.TryGetCurrentPlayerTransform()` and `TryGetComponent<PlayerToolManager>()`.

What was done:
- `Assets/_Project/Scripts/PlayerInventory.cs`: cache `_traumaDispatcher` from `IPlayerRuntimeContext.TraumaDispatcher`; runtime `ResolveTraumaDispatcher()` now returns the cached field only.
- `Assets/_Project/Scripts/PlayerInventory.cs`: kept local `TryGetComponent(out _traumaDispatcher)` only as cold setup fallback.
- `Assets/_Project/Scripts/PlayerPDA.cs`: resolve `survivalSystem` from `IPlayerRuntimeContext.SurvivalSystem`; removed `_survivalResolveDirty` and the LateFrame component retry method.
- `Assets/_Project/Scripts/HUDQuickBar.cs`: removed `GameBootstrapper`/`TryGetComponent<PlayerToolManager>` fallback from `AutoResolve()`.

Cinematic cheats used:
- Missing dependencies fail degraded/closed until the owner publishes context; no scene scans are used to keep UI alive.
- PDA battery drain remains scalar energy drain, not a simulated electronics/thermal model.
- Trauma dispatch still uses the existing high-level signal instead of adding new physical damage simulation.

Exact microseconds saved:
- Inventory radiation/thermal dispatch: estimated 1-8 us per event when dispatcher cache would otherwise be cold.
- PDA survival missing-owner retry: estimated 1-6 us per retry frame.
- HUD quickbar unresolved retry: estimated 2-12 us per 0.5s retry during spawn/respawn gaps.
- Measured proof absent; Unity profiler not run.

Verification:
- `git diff --check -- Assets/_Project/Scripts/FieldLoadoutAdvisor.cs Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs Assets/_Project/Scripts/PlayerInventory.cs Assets/_Project/Scripts/PlayerPDA.cs Assets/_Project/Scripts/HUDQuickBar.cs` passed. Only CRLF warnings were emitted.
- `rg` found no `TryGetCurrentPlayerTransform`, `GameBootstrapper`, `TryGetComponent(out PlayerToolManager)`, `TryResolveSurvivalSystemFromRuntimeContext`, `_survivalResolveDirty`, or `survival.TryGetComponent` in the patched files.
- `rg` found `ResolveTraumaDispatcher()` only as a cached return method and two dispatch callers.
- Compile was not run: no `dotnet`/`csc` process was listed, but CPU average was 100 percent, above the build-policy limit.

Known remaining issues:
- `InventoryGrid` persistent native lanes still require a dedicated DataVault-backed grid route-card migration.
- `PDAEvents` still uses a feature-local native queue; correct fix is a typed `SignalBus<PDAEventSignal>` bridge and consumer migration.
- `PDAInventoryTab` refresh still needs a localization service/cache pass to remove hidden GlobalRegistry/hash fallback reads.
- `PlayerFlashlight` concrete-casts `IBatteryTool` to `FlashlightTool`; fix needs a narrow runtime equipment ID contract.
- `ToolKinematicsRuntime` black-box dump synchronous file I/O remains a separate export-owner task.

## 2026-05-27 Pass 14 - Flashlight Bridge + PDA Localization Cache

What was wrong:
- `PlayerFlashlight` stored `IBatteryTool` but down-cast to concrete `FlashlightTool` to reach `RuntimeToolId`.
- `PDAInventoryTab` selected-detail refresh read `GlobalRegistry.LocalizationMadnessPresentation` and hashed description keys from strings during refresh.

What was done:
- Added `IRuntimeEquipmentIdProvider` in `IBatteryTool.cs`.
- `FlashlightTool` implements the bridge explicitly; `PlayerFlashlight` consumes only the bridge and uint runtime equipment id.
- `PDAInventoryTab` caches `ILocalizationMadnessPresentationReadModel` from cold setup and localization runtime hot-swap.
- `ItemData` now caches `DescriptionTableHashId`; `LocalizedTextReference` can resolve spans with a cached table hash.
- PDA lore corruption source hash uses cached description hash or persistent item hash.

Cinematic Cheats used:
- No physical simulation added. This pass buys PDA/HUD presentation budget by removing dependency and string-hash work from refresh paths.
- Low: cached service/hash route only.
- Middle: same corruption text path, bounded detail refresh.
- High: richer PDA detail/corruption presentation can be added without per-refresh string hash.
- Ultra: central equipment snapshots can support more battery tools without class-specific branches.

Exact microseconds saved:
- Flashlight central equipment snapshot: estimated 0-3 us per call when the old concrete cast path failed or branched on wrong tool type.
- PDA selected-detail refresh: estimated 1-12 us per refresh by removing registry lookup and description key hashing from the refresh route.
- Static verification: `git diff --check` passed with CRLF warnings only.
- Compile: skipped by rule. `dotnet` PID 24280 and `VBCSCompiler` PID 44380 were active; CPU average was 94 percent.

## 2026-05-27 Pass 15 - PDAEvents Typed Signal Bridge

What was wrong:
- `PDAEvents` was queue-backed, but first-party PDA event traffic still had no typed `SignalBus<T>` snapshot bridge.
- Future PDA consumers would have to remain on `IPDAEventListener` concrete callbacks or create duplicate DTOs.

What was done:
- `PDAEventPayload` now implements `ISignal` while keeping the explicit 64-byte layout.
- `PDAEvents` configures `SignalBus<PDAEventPayload>` with capacity 32 and low-tier snapshot cap 8.
- Drained PDA payloads are mirrored into the typed signal lane after side effects and before legacy listener dispatch.
- No-listener drain publishes the same typed signal, so snapshot consumers do not depend on registered callback listeners.
- Added `DroppedTypedSignalCount` for signal bridge refusal telemetry.

Cinematic Cheats used:
- No PDA UI behavior was re-simulated. The patch mirrors the high-level PDA event row instead of creating panel-specific traffic.
- Low: 8-event snapshot cap; legacy UI route remains bounded.
- Middle: full 32-event PDA bridge capacity.
- High: projector/chrome/diagnostic consumers can migrate to span snapshots.
- Ultra: richer PDA visual reactions can be added as optional consumers without changing PDA truth ownership.

Exact microseconds saved:
- Current pass: 0 us/frame claimed; bridge adds one bounded signal push per drained PDA event.
- Future migrated consumers: estimated 0-4 us per PDA event consumer by replacing callback fan-out with `ReadOnlySpan<PDAEventPayload>` snapshot reads.
- Static verification: `git diff --check -- Assets/_Project/Scripts/PlayerPDA.cs` passed with CRLF warnings only.
- Compile: skipped by rule. No `dotnet`/`csc`/`VBCSCompiler` process was listed on the final guard, but CPU average was 84 percent.

Known remaining issues:
- `PDAEvents` consumer migration from `IPDAEventListener` to typed snapshots remains open because delivery timing must be proven panel-by-panel.
- `InventoryGrid` persistent native lanes still require a dedicated DataVault-backed route-card migration.
- `ToolKinematicsRuntime` black-box dump synchronous file I/O remains a separate export-owner task.

## 2026-05-27 Pass 16 - ToolKinematics Black-Box Dump Worker

What was wrong:
- `ToolKinematicsRuntime.FinishPendingFrameCompletion()` detected black-box telemetry faults and synchronously wrote the dump file from the runtime completion path.
- The old route created the log directory, opened `FileStream`, and wrote `BinaryWriter` output on a fault frame.
- The dump filename was tool-local (`Dump_TOOL_KINEMATICS.h8dump`) instead of the mandated agent evidence file `Dump_13US.bin`.

What was done:
- Replaced direct `DumpBlackBox()` with `TryQueueBlackBoxDump()`.
- Fault frames now copy the fixed telemetry ring into a preallocated `ToolKinematicsTelemetryEntry[]` and signal a cold worker.
- Added `13US_ToolKinematicsDump` worker, cold-resolved dump path, explicit failure code, and exception containment.
- Dump output now writes to `Docs/AgentLogs/Dump_13US.bin`.
- Runtime completion no longer performs directory creation, project-root path resolution, `FileStream`, or `BinaryWriter` work.

Cinematic Cheats used:
- No simulation added. This is a postmortem evidence export: keep gameplay-frame truth cheap, move expensive evidence serialization outside the frame.
- Low: bounded telemetry copy, no disk stall on fault frame.
- Middle: same 300-frame ring evidence.
- High: worker-side dump can carry full binary evidence without completion-path I/O.
- Ultra: future richer postmortem exporters must attach to worker/export side only.

Exact microseconds saved:
- Normal frame: 0 us claimed; no dump work happens unless fault/NaN telemetry is detected.
- Fault frame: estimated 100-4000+ us main-thread stall avoided on i3/MX350-class hardware by moving directory/file write off the completion path.
- Static verification: `git diff --check -- Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs` passed with CRLF warning only.
- `rg` verified `Dump_TOOL_KINEMATICS` is gone, `FinishPendingFrameCompletion()` queues the dump, and file I/O is isolated to the worker plus existing editor CSV watcher.
- Compile: attempted under allowed gate (CPU 15%, no active compiler process), but `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` timed out after 124s. Surviving PID 2448 was stopped. No compile result.

Known remaining issues:
- `PDAEvents` consumer migration from `IPDAEventListener` to typed snapshots remains open because delivery timing must be proven panel-by-panel.
- `InventoryGrid` persistent native lanes still require a dedicated DataVault-backed route-card migration.
