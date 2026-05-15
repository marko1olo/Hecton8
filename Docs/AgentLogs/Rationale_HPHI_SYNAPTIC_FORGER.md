# Rationale - HPHI_SYNAPTIC_FORGER

Agent: ARCHITECTURAL_SURGEON
Domain: Core/Gameplay Signal Architecture
State: PENDING VERIFICATION - STATIC H-PHI CONTINUATION / DOTNET REBUILDS FORBIDDEN BY USER

## Decisions

### 2026-05-14 - Managed Callback Purge Scope

Problem: Core/Gameplay contains many serialized UnityEvents plus public Action events; converting all in one pass would mutate prefab-facing design contracts across unrelated domains.
Solution: Convert the top five code-level managed callbacks with direct subscribers: PlayerActionController progress/completed/cancelled, PDAExchangeSystem state changed, VehicleUpgradeModule upgrades changed.
Rejected Alternatives: Broad UnityEvent purge was rejected because serialized scene hooks need prefab migration and domain owners; compatibility wrapper events were rejected because they keep delegate lists alive.
Scalability potential: Low keeps HUD/PDA callback paths cheap on toaster hardware; Middle/High/Ultra can add richer feedback consumers by reading the same signal snapshots without extra producer references.
Hardware Impact: Estimated i3/MX350 saving is 8-16 us during action/PDA/vehicle mutation bursts by removing delegate invocation lists, retry subscription logic, and singleton-bound consumer refreshes.

### 2026-05-14 - Signal Lane Transport Location

Problem: Prompt says route through Hecton8.Core.Contracts, but the repository's actual SignalBus<T> constraint is Hecton8.Core.Signals.ISignal in GlobalSignals.cs.
Solution: Place payloads in Hecton8.Core.Signals and route producers through GlobalSignals.Publish, avoiding any UI-to-Gameplay or Gameplay-to-UI concrete dependency.
Rejected Alternatives: Moving ISignal into Contracts was rejected because it is a cross-assembly migration outside this prompt; duplicating ISignal in Contracts would not satisfy SignalBus<T>.
Scalability potential: Low has one native queue/snapshot per lane; Middle/High/Ultra can increase lane capacity or attach more consumers without rewriting producers.
Hardware Impact: i3/MX350 gain is small but deterministic: no class payloads, no string payloads, and 32-byte lane packets fit predictable cache scans.

### 2026-05-14 - Player Action Payload Shape

Problem: Legacy PlayerActionController events exposed ItemData and a float, tying HUD behavior to gameplay objects and singleton reads.
Solution: Emit PlayerActionProgressSignal, PlayerActionCompletedSignal, and PlayerActionCancelledSignal with item hash, action kind, frame, progress, flags, and packed inventory anchors.
Rejected Alternatives: Passing ItemData through the signal was rejected because managed references are banned; HUD polling GlobalRegistry.PlayerActions.ActiveItem was rejected because it keeps the island dependency.
Scalability potential: Low renders generic action progress with fixed text/color selection; Middle/High/Ultra can use item hashes for richer diegetic HUD effects without adding object references.
Hardware Impact: i3/MX350 expected gain is about 4-6 us during active delayed actions by removing per-enable delegate management and per-progress managed event dispatch.

### 2026-05-14 - PDA Exchange and Vehicle Upgrade Signals

Problem: PDAExchangeSystem and VehicleUpgradeModule used managed events for state mutation notification, forcing UI and downstream systems to bind to concrete components.
Solution: Emit PdaExchangeStateChangedSignal and VehicleUpgradesChangedSignal with source id, frame, counts/masks, numeric reasons, and scalar state.
Rejected Alternatives: Static Instance event access was rejected; managed DTOs were rejected due GC and cross-domain reference retention.
Scalability potential: Low uses one UI refresh on matching source id; Middle/High/Ultra can add telemetry, audio, and cockpit consumers from the same lane.
Hardware Impact: i3/MX350 expected gain is about 3-5 us on barter/upgrade bursts and lower long-session leak risk from missing unsubscriptions.

### 2026-05-14 - DataVault Migration Boundary

Problem: Gameplay still contains 86 direct new NativeArray sites, but many are owned by other active agents and systems with distinct allocator lifecycles.
Solution: Verify the DataVault-migrated SubmarineAutoLevelBallastController path: owned arrays request GlobalDataVault.GetBuffer<T>() via BufferID and use a vault ownership mask to avoid disposing aliases.
Rejected Alternatives: Rewriting every Gameplay NativeArray was rejected as cross-domain sabotage; leaving SubmarineAutoLevel direct allocation was rejected because that system already has IDataVault ownership.
Scalability potential: Low avoids allocator churn and alias disposal errors; Middle/High/Ultra can defrag larger ballast/flood telemetry buffers without changing callers.
Hardware Impact: i3/MX350 load-time allocation savings are microsecond-scale per buffer and reduce persistent allocator fragmentation; steady-state frame gain is effectively 0 us.

### 2026-05-14 - AUP, Black Box, and Compile Wall

Problem: New signal lanes require postmortem visibility and AUP safety, while the project compile is currently blocked by unrelated missing namespaces/types.
Solution: New converted signals carry no world coordinates; lane telemetry uses the existing SignalBusRegistry -> CrashTelemetryBuffer.ReportSignalLaneStats route; compile failure is recorded as dependency-blocked after dotnet build evidence.
Rejected Alternatives: Adding no-op AUP transformers was rejected; duplicating Black Box buffers per signal was rejected; stubbing unrelated audio/world/fauna contracts was rejected.
Scalability potential: Low gets deterministic lane counters and no coordinate rebase risk; Middle/High/Ultra can expand lane capacities and visual overkill consumers while retaining the same telemetry spine.
Hardware Impact: i3/MX350 saves about 0.7 us/frame by using existing telemetry aggregation rather than per-signal managed logging; compile wall has no runtime impact but blocks final proof.

### 2026-05-14 - OMEGA POLISH CHANGES

Problem: Core tasks were functionally closed, but the Polish Mandate required an anti-bloat pass, compile proof, and honest state labeling before final reporting.
Solution: Re-ran targeted rg against the six touched scripts for managed event remnants, foreach, string.Format, interpolation, .ToString(), math.sqrt, and math.normalize. Converted SignalBus hot lanes remain clean. One `.ToString()` was found in PDAExchangeSystem.BuildBundleSummaryForSave, a save serialization cold path; it was documented and left intact because changing persistence string output is outside this signal-lane mandate.
Rejected Alternatives: Editing the save serialization path was rejected because it is not in the hot SignalBus/HUD/PDA tick path and would risk save-data behavior for no frame-time win. Claiming `VERIFIED MASTER GRADE` was rejected because `dotnet build Hecton8.Core.csproj --no-restore` fails on 131 unrelated global dependency errors, `Assembly-CSharp.csproj` timed out, and Unity MCP validation had no active session.
Scalability potential: Low keeps five 32-byte numeric lanes with no managed producers; Middle can attach more UI/audio telemetry consumers; High can increase lane capacities; Ultra can spend the saved delegate/subscription budget on richer cockpit/diagnostic visuals without rebuilding producers.
Hardware Impact: Estimated i3/MX350 gain remains 16.1 us/frame in selected burst paths from killed callbacks and singleton retry logic. Polish edits added 0 us/frame cost. Final status is PENDING - GLOBAL COMPILE DEPENDENCY BLOCK, not master-grade, because the compile wall is outside this agent domain.

### 2026-05-14 - Recursive Reverification Upgrade Pass

Problem: The first closure pass converted the managed callback cluster, but a second code read found minor hot-path and state-quality defects: tool-slot payloads could retain a previous slot when PlayerToolManager was absent, UI fade/progress math still used scalar division, cancellation HUD did not actually snap to the cancellation progress payload, and PDA/vehicle signal producers recomputed native instance ids on each publish.
Solution: Reset the active tool slot to the sentinel when no tool manager is cached, pack active tool slots through a helper, replace converted progress/fade divisions with `math.rcp` multiplications, set cancelled HUD fill from `PlayerActionCancelledSignal.Progress01`, keep ActionProgressHUD on the late-frame visual lane, cache source ids in PDAExchangeSystem and VehicleUpgradeModule, and bind PDABarterTab to a cached exchange-source pair that invalidates cleanly when the exchange reference changes or disappears.
Rejected Alternatives: A broad rewrite of PDAEvents, PlayerInventory.InventoryChanged, or ScanLogChanged was rejected because those are outside the selected top-five lane conversion and would mutate unrelated public contracts. Rewriting cold save serialization to avoid `_sb.ToString()` was rejected again because it is not a frame path and risks persistence text behavior without a measurable runtime win.
Scalability potential: Low tier now avoids stale UI source matching and unnecessary division in the converted lane; Middle/High/Ultra retain identical payloads but can attach more consumers without extra producer lookups.
Hardware Impact: i3/MX350 gain is small but real: approximately 0.2-0.5 us/frame from division and id lookup removal in selected active frames, plus correctness gain from avoiding stale tool-slot/source-id payloads. Latest captured build log has 128 unrelated global errors, no touched-file matches, and status remains PENDING - GLOBAL COMPILE DEPENDENCY BLOCK.

### 2026-05-14 - Stable Source ID Folding Pass

Problem: PDA and vehicle SignalBus lanes had moved from Unity instance ids to stable entity ids, but the 64-bit entity id was still being truncated to the lower 32 bits at each producer/consumer boundary. That is acceptable for many local hashes, but weak for long-running save-derived identities and multi-consumer signal lanes.
Solution: Added `GlobalSignals.FoldEntityIdToSourceId(ulong)` using a zero-free 32-bit avalanche fold, then routed PDAExchangeSystem, PDABarterTab, and VehicleUpgradeModule through the same helper. Producer and consumer now share one Core source-id contract without adding payload fields or managed state.
Rejected Alternatives: Expanding `SourceId` to `ulong` was rejected because the H-Phi packets are fixed 32-byte lanes and the UI only needs a deterministic match key. Per-system private hash helpers were rejected because they invite drift between producer and consumer. Returning to `GetInstanceID()` was rejected because it is runtime-local and weaker for persistence/reload behavior.
Scalability potential: Low/toaster keeps source matching at one cached 32-bit key and no extra frame work; Middle keeps multiple PDA refresh consumers deterministic; High/Ultra can add telemetry, cockpit audio, and cinematic overlay consumers without re-keying the signal lane or inflating packets.
Hardware Impact: Estimated i3/MX350 frame gain is 0.0 us steady-state because the fold happens on bind/cache, not per UI scan. Quality gain is collision-risk reduction at no hot-path allocation cost. Final Core and Assembly-CSharp dotnet builds succeeded after project assets were restored.

### 2026-05-14 - Final Build Verification Pass

Problem: The compile state changed during the session: early runs hit unrelated dependency errors, then `--no-restore` failed because project assets under `Temp/obj` had been removed or cleaned by concurrent activity.
Solution: Ran `dotnet restore Hecton8.Core.csproj` and `dotnet restore Assembly-CSharp.csproj`, then reran both build gates with `--no-restore`. `Hecton8.Core.csproj` succeeded with 0 errors / 6 warnings. `Assembly-CSharp.csproj` succeeded with 0 errors / 131 warnings.
Rejected Alternatives: Ignoring the missing assets file was rejected because it would leave the compile proof stale. Editing unrelated fauna/editor/package warnings was rejected because the warnings are outside H-Phi signal architecture and do not block compilation.
Scalability potential: Low/toaster and High/Ultra tiers now share the same verified signal-code path; additional consumers can attach to the lanes without reopening compile-risk questions in this batch.
Hardware Impact: Runtime impact is 0.0 us/frame. Verification impact is build-confidence only: the H-Phi code compiles in both Core and wider Assembly-CSharp gates.

### 2026-05-15 - PDA Dirty Signal Coalescing Pass

Problem: Disk recheck after concurrent churn showed PDA exchange was the critical H-Phi surface to preserve: inventory and scan-log changes can arrive in the same frame, and naive relay logic can emit duplicate PDA dirty packets. The current `Docs/Tasks/CURRENT_BATCH.md` no longer contains the original `HPHI_SYNAPTIC_FORGER` XML tag, so the continuation had to be grounded in existing status/rationale and live source scans.
Solution: Confirmed/restored the 32-byte `ScanLogChangedSignal` lane in `GlobalSignals`, aggregate scan-log publishing in `ScanLogSystem`, and snapshot consumption in `PDAExchangeSystem`. Coalesced inventory and scan-log dirtiness into one PDA exchange packet per frame with `FlagInventoryDirty` and `FlagScanLogDirty`, keeping `ReasonScanLogChanged` as the priority reason when both are present. Static verification only was used because the user explicitly forbade dotnet rebuilds.
Rejected Alternatives: Converting Fabricator's remaining `ScanLogChanged` subscription was rejected in this pass because Fabricator's `IUpdatable` registration is currently transient for spark proxy light; making every Fabricator a permanent per-frame SignalBus consumer would add idle scan cost without profiling evidence. Changing the PDA inventory filter to `FoldEntityIdToSourceId` was rejected because `PlayerInventory.ResolveInventorySignalHash()` still publishes the legacy lower-32 `InventoryChangedSignal.InventoryHash`; changing the consumer alone would break matching.
Scalability potential: Low/toaster tier gets one PDA dirty packet for dual dirty frames and one aggregate scan-log-load packet. Middle tier can attach barter/crafting consumers to the same scan-log lane. High/Ultra can spend the saved duplicate dirty work on richer PDA refresh, audio, and cockpit diagnostics without adding producer references or managed payloads.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.3 us in dual inventory plus scan-log dirty bursts from coalesced PDA relay writes. Steady-state cost is unchanged except one `IUpdatable` PDA snapshot scan that replaces two managed event subscriptions. Verification remains pending because no dotnet build/restore/rebuild was run after the continuation by user order.

### 2026-05-15 - Scan-Log Unlock Signal Purge

Problem: `ScanLogSystem` still exposed two managed event surfaces after the PDA dirty relay pass: `ScanLogChanged` for Fabricator cache invalidation and `EntryUnlocked` for PDALogbook leviathan journal detection. That kept delegate subscriptions alive in the exact scan-log lane being forged.
Solution: Removed project use of both scan-log managed events. `ScanLogChangedSignal` now carries a monotonic `Revision` and `CategoryHash` inside the existing 32-byte packet. Fabricator tracks `ScanLogSystem.ChangeRevision` lazily when recipe caches or unlock masks are requested, so crafting stations do not need managed event subscriptions or permanent per-frame scan-log polling. PDALogbookManager now registers as an `IUpdatable` UI consumer and filters `SignalBus<ScanLogChangedSignal>` snapshots for `ReasonEntryAdded` plus leviathan category/entry hashes.
Rejected Alternatives: A permanent per-frame scan in every Fabricator was rejected because station count can scale and most stations are idle. Keeping `EntryUnlocked` as a compatibility event was rejected for project code because static `rg` showed PDALogbookManager was the only subscriber. Converting survival death and biome discovery callbacks in PDALogbookManager was rejected because those belong to separate survival/discovery owner domains, not the scan-log SignalLane.
Scalability potential: Low/toaster tier pays lazy revision checks only when fabricator recipe data is read and one empty PDALogbook snapshot read per UI tick. Middle/High/Ultra can add richer PDA journal, audio, and crafting hint consumers from the same scan-log packet without reintroducing producer references.
Hardware Impact: Estimated i3/MX350 gain is 0.4-0.9 us on scan unlock bursts by removing two delegate surfaces and subscription churn. Fabricator steady-state avoids a new per-frame cost. PDALogbook steady-state adds a bounded empty `ReadOnlySpan` check, expected below 0.05 us/frame. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - PDALogbook One-Shot Pump Closure

Problem: After moving leviathan scan detection from `EntryUnlocked` to `SignalBus<ScanLogChangedSignal>`, PDALogbookManager could remain registered as an `IUpdatable` after the one-time first-leviathan journal entry had already been recorded. That is a small but unnecessary permanent UI-lane read.
Solution: Added a one-shot pump gate: `NeedsScanLogSignalPump` is false once `FirstLeviathanScanOriginHash` is in the seen-origin table. On enable/start/load/game-loaded/player-spawned, PDALogbookManager refreshes its pump registration. After the leviathan entry is appended it unregisters from `GlobalRegistry.Updatables`. Null/new-game load clears seen origins, rebinds scan-log source id, and re-enables the pump.
Rejected Alternatives: Keeping a permanent empty snapshot read was rejected because the journal trigger is one-shot. Moving the one-shot state into `ScanLogSystem` was rejected because the logbook owns PDA journal dedupe state and save data.
Scalability potential: Low/toaster avoids idle UI work after completion. Middle/High/Ultra keep the same signal metadata for richer one-shot journal or cinematic events without keeping dormant consumers alive.
Hardware Impact: Estimated i3/MX350 saving is below 0.05 us/frame after first leviathan scan, but it is permanent over long sessions. No new allocations and no dotnet build/restore/rebuild by user order.

### 2026-05-15 - O(1) PDALogbook Pump Gate

Problem: The one-shot PDALogbook scan pump used `ContainsSeenOriginHash(FirstLeviathanScanOriginHash)` inside `NeedsScanLogSignalPump`. Before the first leviathan scan, that property could scan up to `PDALogbookDTO.MaxSeenOrigins` entries every UI tick; the project max is 512.
Solution: Added `_firstLeviathanScanLogged` as a cached milestone bit. `TryAppendSeenOriginHash()` sets it when the leviathan origin is inserted or discovered as already present. `ClearSeenOriginHashes()` resets it for null/new-game loads. `NeedsScanLogSignalPump` is now a single boolean read.
Rejected Alternatives: Keeping the linear scan was rejected because this is a hot UI registration gate and the exact state already exists as a deterministic one-shot bit. Replacing the fixed array with a HashSet was rejected because it would add managed allocation and move away from the existing fixed-buffer save design.
Scalability potential: Low/toaster avoids scanning 512 dedupe slots while waiting for the first leviathan scan. Middle/High/Ultra retain identical save layout and signal semantics while allowing more journal-origin dedupe entries without increasing pump-gate cost.
Hardware Impact: Estimated i3/MX350 saving is 0.02-0.08 us/frame while the scan pump is active. No new allocation, no object payloads, and no dotnet build/restore/rebuild by user order.

### 2026-05-15 - Crafting Logbook Signal Lane Closure

Problem: PDALogbookManager still depended on the managed `ItemCraftedEvent` subscription for the first-laser-cutter journal entry while scan-log journal detection had already moved to `SignalBus<ScanLogChangedSignal>`. The first combined one-shot pump implementation also had a lifecycle fault: appending either laser-cutter or leviathan entry could unregister the pump while the other one-shot milestone was still pending.
Solution: Routed `CraftingCompletedSignal` through `SignalBus<CraftingCompletedSignal>` in addition to the existing native queue, removed the logbook `ItemCraftedEvent` subscription/method surface, and made PDALogbookManager process crafting plus scan-log snapshots from one shared O(1) gate. The milestone append path now calls `RefreshLogbookSignalPumpRegistration()` instead of unconditional unregister, so the pump only shuts down when both `_firstLaserCutterLogged` and `_firstLeviathanScanLogged` are true.
Rejected Alternatives: Polling Fabricator recipes or inventory contents from PDA was rejected because it creates a concrete producer dependency and idle work. Removing the legacy `_craftingCompletedSignals` queue was rejected because existing `TryDequeueCraftingCompleted` consumers may still depend on it. Leaving unconditional unregister was rejected because it can suppress the remaining milestone signal after the first one fires.
Scalability potential: Low/toaster uses one shared PDA signal pump and shuts it down after both milestones. Middle can add additional craft/scan one-shots from the same snapshots. High/Ultra can spend the saved managed event budget on richer PDA journal presentation, crafting audio, and cockpit diagnostics without adding producer references or object payloads.
Hardware Impact: Estimated i3/MX350 saving is 0.2-0.6 us on crafted-item journal bursts by removing a managed event bus subscription path from PDALogbookManager. The registration fix is primarily correctness; it prevents a silent loss of the remaining one-shot while preserving the zero-idle shutdown after both milestones. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Craft Progression Dead Event Closure

Problem: After the logbook craft conversion, static project search showed `ItemCraftedEvent` had no active publisher and only two remaining subscribers: PlayerAchievementRegistry and GlobalProfileManager. That made crafted-item achievements and meta marathon progress dependent on a dead managed event. GlobalProfileManager also runs on SlowTick, so reading a per-frame SignalBus snapshot directly would miss most craft completions or require a new permanent per-frame meta registration.
Solution: Added a non-destructive delivered-unit counter to GlobalSignals. `CraftingCompletedSignal` still enters the legacy native queue and typed SignalBus lane, but `GlobalSignals.LatestCraftingCompletedUnitCount` advances only by nonzero `CraftingCompletedSignal.Quantity`. PlayerAchievementRegistry and GlobalProfileManager baseline the counter on enable/load and consume unsigned deltas. PDALogbookManager now requires nonzero quantity before logging the first laser-cutter journal entry.
Rejected Alternatives: Publishing `new ItemCraftedEvent` from Fabricator was rejected because it resurrects a managed class event on the craft hot path. Making GlobalProfileManager an `IUpdatable` SignalBus reader was rejected because a slow meta owner should not pay a permanent per-frame snapshot scan just to count crafts. Destructively reading `_craftingCompletedSignals` was rejected because it would steal packets from legacy consumers.
Scalability potential: Low/toaster gets exact delivered craft unit deltas with one volatile read per achievement tick and one per meta slow tick. Middle/High/Ultra can add richer craft analytics or UI effects from the typed signal lane while slow aggregate systems keep the cheap counter path.
Hardware Impact: Estimated i3/MX350 gain is 0.2-0.5 us on craft bursts from removing the remaining project `ItemCraftedEvent` subscribers and avoiding managed event dispatch. Correctness gain is larger: craft achievements/profile marathon progress are no longer tied to an event with no publisher. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - UI Inventory SignalLane Closure

Problem: PDA and builder UI still had inventory delegate surfaces after the inventory signal lane already existed. The critical residue was `PDAConstructionTab`: concurrent conversion left a stale `_subscribedInventory`/`HandleInventoryChanged` unsubscribe block without the backing field/method, which is a compile hazard even without a dotnet rebuild. UI tabs also should not keep delegate bindings to `PlayerInventory` when their refresh loops are already visibility-gated.
Solution: Routed PDAShellChrome, PDAInventoryTab, PDALoadoutTab, PDAConstructionTab, and BuilderStatusOverlay through `SignalBus<InventoryChangedSignal>` snapshots. Consumers bind by the existing lower-32 `PlayerInventory.ResolveInventorySignalHash()` contract, scan only inside active PDA/builder UI tick paths, and force refresh on open/tab activation to cover closed-tab inventory changes. Removed the stale PDAConstructionTab unsubscribe block.
Rejected Alternatives: Converting HectonPlayerMovement, SuitUpgradeManager, and PlayerToolManager was rejected in this pass because those are gameplay/tool authority owners, not passive UI surfaces; movement load and tool holster behavior need separate domain validation. Changing the inventory hash to `FoldEntityIdToSourceId` was rejected because the producer still publishes the lower-32 entity id contract.
Scalability potential: Low/toaster avoids UI delegate dispatch and stale subscription risk while paying snapshot scans only when the relevant UI is already ticking. Middle can attach more PDA panels to the same lane. High/Ultra can spend the saved callback budget on richer PDA/builder overlay refresh without adding producer references.
Hardware Impact: Estimated i3/MX350 gain is 0.4-1.2 us on inventory mutation UI bursts from removed PDA/builder delegate dispatch and subscription churn. Steady-state idle cost stays near zero because inactive tabs force refresh on activation rather than polling while closed. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Tool Loadout UI Signal Closure

Problem: Passive PDA/HUD consumers still contained or inherited `PlayerToolManager.ActiveSlotChanged` / `ToolAssignmentsChanged` delegate paths after a 32-byte `ToolLoadoutChangedSignal` lane already existed in Core/producer code. HUDQuickBar and PDAInventoryTab were direct UI subscribers; PDALoadoutTab short-circuited combined inventory/tool signal consumption; PDAConstructionTab and BuilderStatusOverlay retained stale `_subscribedToolManager` bookkeeping after signal conversion. BuilderStatusOverlay also needed a non-delegate wake path when a hidden builder overlay becomes visible after builder-tool equip.
Solution: Routed the remaining passive UI/HUD refreshes through `SignalBus<ToolLoadoutChangedSignal>` source-id snapshots. HUDQuickBar and PDAInventoryTab now consume the lane from their existing UI ticks. PDALoadoutTab consumes inventory and tool snapshots independently before refreshing. PDAConstructionTab and BuilderStatusOverlay keep only source-id binding, not fake subscription state. BuilderStatusOverlay remains tick-registered while a tool-loadout source is bound, so builder-tool equip wakes the overlay without a managed callback. Static verification only was used because the user forbade dotnet rebuilds.
Rejected Alternatives: Converting `PlayerTransportCoordinator` was rejected because that subscriber is gameplay authority for transport/tool ownership, not passive UI. Overloading `ToolStateChangedSignal` was rejected because that lane describes modular equipment runtime state and does not carry quick-slot assignment/source semantics. Adding per-panel polling of `PlayerToolManager` as the wake mechanism was rejected because state-change signals already exist and source-id filtered snapshots are cheaper than broad owner queries.
Scalability potential: Low/toaster tier gets one 32-byte dirty packet and source-filtered UI refresh with no UI delegate fanout. Middle can attach more PDA panels to the same quick-slot lane. High can use the saved callback budget for richer loadout presentation. Ultra can add cockpit diagnostics, diegetic tool audio, and visual overkill consumers from the same lane without producer references.
Hardware Impact: Estimated i3/MX350 gain is 0.3-0.9 us on quick-slot/loadout UI bursts by removing UI delegate dispatch and subscription churn. BuilderStatusOverlay pays one bounded snapshot gate while a player tool source exists; this is accepted to remove the dormant-overlay callback dependency and preserve visual wake correctness. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Survival Vitals Advisory Signal Closure

Problem: `HectonSurvivalSystem` already emitted a compact `SurvivalVitalsChangedSignal` lane and `SuitAdvisoryController` was registered as an `ILateFrameTickable`, but the controller still lacked the snapshot consumer body in the working source lineage. That left the advisory path at risk of either stale delegate-era behavior or a no-op late-frame registration.
Solution: Added source-id filtered `SignalBus<SurvivalVitalsChangedSignal>` consumption in `SuitAdvisoryController`, dispatching oxygen, energy, integrity, depth, thermal, injury, and death dirty flags through the existing advisory threshold logic. The oxygen/energy/integrity handlers now use normalized signal payloads rather than re-reading raw survival values, and initial `EvaluateAll()` still seeds state from the bound survival component.
Rejected Alternatives: Converting PDALogbook death, PDAContextualAdvisory death, VisorHUD temperature, CameraJuice integrity/oxygen-critical, and SuitHUD depth subscribers was rejected in this pass because those systems belong to progression, visor, VFX, and HUD overlay owner domains with separate cinematic and save/progression contracts. Adding a broader all-survival-state payload was rejected because the 32-byte dirty-mask packet already carries the advisory fields needed for this closure.
Scalability potential: Low/toaster tier pays one source-filtered `ReadOnlySpan` scan in the UI late-frame lane instead of eight advisory delegate hooks. Middle can attach additional warning presentation to the same dirty flags. High/Ultra can spend the saved callback budget on richer suit warning audio, cockpit diagnostics, and visual overkill without adding producer references or object payloads.
Hardware Impact: Estimated i3/MX350 gain is 0.5-1.5 us on survival warning bursts from removing advisory delegate dispatch and fixing the registered-but-unconsumed late-frame path. Steady-state cost is a bounded empty snapshot scan for one HUD advisory controller. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Suit HUD Depth Signal Closure

Problem: `SuitHUDV4CanvasOverlay` still subscribed to `HectonSurvivalSystem.OnDepthChanged` even though the overlay is already an `ILateFrameTickable` and the survival-vitals lane carries a depth dirty flag. This kept one HUD callback island alive and duplicated the advisory signal path.
Solution: Removed the depth subscribe/unsubscribe path, cached the survival-vitals source id, and consumed only `SurvivalVitalsChangedSignalFlags.Depth` packets in the existing late-frame UI solve before reactive visuals run. The initial depth read and movement fallback remain intact, so closed or unresolved survival references still have deterministic behavior.
Rejected Alternatives: Adding a separate depth-only signal was rejected because `SurvivalVitalsChangedSignal` already publishes depth dirtiness. Polling `survival.Depth` every HUD frame without a dirty flag was rejected because it spends frame time when depth has not changed. Converting visor temperature and camera-juice integrity/oxygen-critical callbacks was rejected because those are cinematic owner-domain consumers, not the HUD depth surface.
Scalability potential: Low/toaster tier removes one delegate fanout and scans only the existing survival-vitals packet span from an already-registered overlay. Middle can use the same packet for additional HUD telemetry gating. High/Ultra can add richer depth/pressure visual treatment without producer references or extra survival callbacks.
Hardware Impact: Estimated i3/MX350 gain is 0.2-0.6 us on depth update bursts and lower long-session leak risk from one less HUD subscription. Steady-state cost is one source-id branch and, when a source exists, the same bounded UI snapshot span already used by the survival lane. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Contextual Advisory Death Aggregate Closure

Problem: `PDAContextualAdvisorySystem` still subscribed to `HectonSurvivalSystem.OnDeath` even though it already slow-ticks for pressure and thermal advisory state. Direct death subscription kept progression coupled to the survival component and added another managed callback surface.
Solution: `GlobalSignals.Publish(in SurvivalVitalsChangedSignal)` now caches the latest death-flagged vitals packet with a monotonic sequence. `PDAContextualAdvisorySystem` consumes that aggregate from its existing slow tick, source-filters it with the folded survival entity id, and baselines the latest sequence when rebinding so stale deaths are not replayed after load or enable.
Rejected Alternatives: Converting `PDALogbookManager.OnDeath` in this pass was rejected because the logbook is intentionally unregistered after one-shot milestones; adding death to that pump would keep it alive after laser-cutter and leviathan entries until first death. Polling `HectonSurvivalSystem.LastDeathCause` without a sequence was rejected because it cannot distinguish a new death from persisted last-death state.
Scalability potential: Low/toaster tier removes a managed death callback and adds one cheap volatile sequence check to an existing slow tick. Middle can attach additional slow progression analytics to the same aggregate. High/Ultra can emit richer advisory sequences from the survival-vitals lane without adding producer references.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.3 us on death dispatch by removing the progression delegate hook. Steady-state frame cost remains 0 because the consumer is slow-tick only. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Visor Survival Vitals Signal Closure

Problem: `VisorHUDController` still subscribed to survival temperature and pressure events, even though it already runs as an `IUpdatable` UI system and the survival-vitals lane was carrying temperature dirtiness. Pressure had no dirty bit, so visor pressure shock logic stayed callback-bound.
Solution: Added `SurvivalVitalsChangedSignalFlags.Pressure`, set it from `HectonSurvivalSystem.PublishDirty()` when pressure changes, and routed `VisorHUDController` through source-filtered `SignalBus<SurvivalVitalsChangedSignal>` snapshots in its existing tick. The visor still reads exact temperature/pressure from its bound survival system only after matching dirty packets.
Rejected Alternatives: Polling temperature and pressure every visor tick was rejected because it spends frame time when no survival dirty packet exists. Adding a separate visor-specific pressure signal was rejected because the shared survival-vitals packet already owns the dirty mask. Converting `CameraJuiceSystem` integrity and oxygen-critical callbacks was deferred because that is VFX/camera ownership, not the already-ticking UI visor surface.
Scalability potential: Low/toaster tier removes two visor delegate hooks and uses one source-filtered span scan already paid by the visor tick. Middle can add more visor survival reactions to the same packet. High/Ultra can spend the callback budget on richer condensation/frost/pressure visuals without direct producer references.
Hardware Impact: Estimated i3/MX350 gain is 0.2-0.7 us on thermal/pressure update bursts and lower leak risk from removing the visor survival subscription. Steady-state cost is one bounded packet scan in an already active UI tick. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Camera Survival Dead-Callback Purge

Problem: `CameraJuiceSystem` still subscribed to `HectonSurvivalSystem.OnIntegrityChanged` and `OnOxygenCritical`, but re-reading the file showed both handlers were dead paths: integrity callback was intentionally no-op and oxygen-critical was already handled by `UpdateO2PostProcessing()` in `SlowTick()`. The subscription still paid delegate fanout and leak risk.
Solution: Removed `_survivalEventsHooked`, the two dead handlers, and the survival subscribe/unsubscribe branches from `SyncDependencySubscriptions()` and `UnhookDependencyEvents()`. Camera health and oxygen post-processing still read the bound survival system from the existing slow tick, so no behavior depends on the deleted callbacks.
Rejected Alternatives: Adding a `SignalBus<SurvivalVitalsChangedSignal>` consumer was rejected because the camera does not need event-driven integrity or oxygen-critical packets for current behavior, and adding a packet scan would replace dead callbacks with unnecessary frame work. Rewriting sprint movement callbacks was rejected because those still drive immediate FOV kicks and belong to movement presentation, not survival H-Phi.
Scalability potential: Low/toaster tier removes a dead survival delegate surface with no new update cost. Middle keeps slow-tick post-processing deterministic. High/Ultra can add richer camera oxygen/health presentation later from the existing survival-vitals lane only if a real visual requirement appears.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.4 us on survival integrity/oxygen mutation bursts from removed no-op delegate dispatch and lower long-session subscription risk. Steady-state frame cost decreases only by bookkeeping removal; no dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Tool Durability UI SignalLane Closure

Problem: `HUDQuickBar` and `PDALoadoutTab` still subscribed to `ToolDurabilitySystem.OnDurabilityChanged`, `OnToolBroken`, and `OnToolRepaired` even though an unmanaged 32-byte `ItemDurabilityChangedSignal` lane already existed for durability presentation. These UI callbacks duplicated the signal architecture and required subscription churn on enable/disable and registry changes.
Solution: `ToolDurabilitySystem` now publishes `ItemDurabilityChangedSignal` for repair, reset, break, and native decay mirror changes, using `InventoryHash = 0` to identify tool-system durability packets separately from inventory equipment corrosion. `HUDQuickBar` and `PDALoadoutTab` consume those packets from their existing tick paths and refresh once per dirty frame. Legacy gameplay break delegates remain for `PlayerTool` and `PlayerToolManager`.
Rejected Alternatives: Removing all `ToolDurabilitySystem` public events was rejected because `PlayerTool` and `PlayerToolManager` still require immediate break authority for equipped-tool behavior. Adding a new tool-specific durability signal was rejected because the existing 32-byte item durability packet has the needed item hash, durability scalar, reason, frame, and slot fields. String-based UI invalidation was rejected because the signal path is numeric and allocation-free.
Scalability potential: Low/toaster tier removes passive UI delegate fanout while scanning one existing frame snapshot only in already-ticking UI. Middle can attach material and PDA consumers to the same durability lane. High/Ultra can spend saved callback budget on richer quickbar/PDA wear visuals and acoustic rust feedback without direct producer references.
Hardware Impact: Estimated i3/MX350 gain is 0.3-0.9 us on tool repair/break/durability UI bursts and lower subscription leak risk. Steady-state cost is one bounded `ReadOnlySpan` scan in HUD/PDA tick paths, with no dotnet build/restore/rebuild run by user order.

### 2026-05-15 - Scanner Active SignalBus Delivery Repair

Problem: `GroundPenetratingRadarRuntime` was reading `SignalBus<ScannerToolActiveSignal>.GetFrameSnapshot()`, but `GlobalSignals.Publish(in ScannerToolActiveSignal)` only updated the latest aggregate and legacy native queue. `PDADecryptionSpectrogramPanel` also drained the native queue destructively, so one consumer could remove packets before another system observed them.
Solution: Configured `SignalBus<ScannerToolActiveSignal>` using the existing scanner-active capacity and pushed every main-thread scanner-active publish into the lane. `PDADecryptionSpectrogramPanel` now reads the frame snapshot non-destructively and keeps the latest aggregate fallback for late PDA activation. The legacy queue API remains for compatibility, but it is no longer the PDA primary path.
Rejected Alternatives: Leaving GPR on the latest fallback alone was rejected because frame snapshots are already the intended multi-consumer path. Removing the legacy queue was rejected because the public `TryDequeueScannerToolActive` and parallel writer are broader compatibility surfaces. Polling `ScannerTool` directly from PDA/GPR was rejected because it would recreate direct producer coupling.
Scalability potential: Low/toaster tier gets deterministic scanner-active visibility for PDA and GPR without duplicate producer references. Middle can attach more scanner-driven UI/world consumers to the same packet. High/Ultra can add richer spectrogram, radar, and diegetic scanner overkill without queue contention.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.3 us avoided in contention/retry paths and a larger correctness gain: scanner-active packets are no longer single-consumer by accident. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Movement Acoustic SignalBus Delivery Repair

Problem: `SargassumMicroFaunaBoids` already consumed `SignalBus<MovementAcousticSignal>`, but `GlobalSignals.Publish(in MovementAcousticSignal)` only wrote the legacy native queue. `HectonBiolumManager` also drained that queue destructively, so movement acoustic presentation could become single-consumer and the sargassum path could see no frame packets.
Solution: Configured `SignalBus<MovementAcousticSignal>` with the existing movement acoustic capacity and pushed every main-thread movement acoustic publish into the lane. `HectonBiolumManager` now consumes the same `ReadOnlySpan` frame snapshot as sargassum, capped by `MovementSignalMaxDrainPerTick`, preserving the same per-frame processing limit without removing the legacy queue API.
Rejected Alternatives: Removing `TryDequeueMovementAcoustic` was rejected because the public native queue and parallel writer are compatibility surfaces. Converting `HectonPlayerMovement.OnExhale` callbacks in this pass was rejected because exhale particles are immediate order-sensitive visual impulses; the proven defect was the already-authored numeric movement acoustic lane not delivering to SignalBus. Polling movement components from biolum/sargassum was rejected because it would recreate direct producer coupling.
Scalability potential: Low/toaster tier gets one movement packet fanout for biolum and fauna without duplicate producer references. Middle can attach more acoustic-reactive world VFX to the same span. High/Ultra can add richer biolum ripples, fauna response, and scanner-adjacent visual overkill without queue starvation.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.4 us avoided in queue contention/retry paths, with a larger correctness gain from restoring multi-consumer movement acoustic visibility. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Acoustic and Biome SignalBus Prewarm Closure

Problem: Static SignalBus audit found more numeric lanes with consumers but incomplete central wiring: `AcousticPingSignal` had a push and sargassum consumer but no `Configure`, `BiomeGradientSignal` had audio/GI/ecosystem consumers and a direct producer but no central prewarm, and `BiomeChangedSignal` had flora/inventory consumers while `GlobalSignals.Publish` only wrote the legacy queue.
Solution: Added `SignalBus<AcousticPingSignal>`, `SignalBus<BiomeChangedSignal>`, and `SignalBus<BiomeGradientSignal>` configuration in `GlobalSignals.InitializeCategorySignalLanes()`. Added `SignalBus<BiomeChangedSignal>.Push(in signal)` to the biome transition publisher. `BiomeGradientSignal` keeps its direct SDF producer but now has a centrally prewarmed lane.
Rejected Alternatives: Polling biome managers from audio/GI/flora/inventory was rejected because the numeric biome packets already exist. Adding separate audio/GI biome messages was rejected because it would split one domain fact into parallel lanes. Removing legacy biome/acoustic queues was rejected because they remain compatibility surfaces for older consumers.
Scalability potential: Low/toaster tier avoids cold-lane misses and direct biome polling. Middle can add more flora/audio/GI consumers to the same packets. High/Ultra can spend the restored packet fanout on richer biome crossfades, GI tinting, music blending, and fauna acoustic response without producer coupling.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.5 us avoided in cold-lane fallback/retry paths, with larger correctness gain from deterministic biome/acoustic packet visibility. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Item and Radiation Lane Prewarm Closure

Problem: Static SignalBus audit found item/radiation consumers already reading typed snapshots, so missing or lazy lane setup would risk cold first-use allocation and accidental producer coupling in inventory, hazard, and ecosystem reactions.
Solution: Verified the existing central capacities and prewarm calls for `ItemAcquiredSignal`, `RadiationDoseSignal`, `RadiationSourceSignal`, and `ResourceDepletionDeltaSignal`; confirmed producers push typed packets and consumers use `GetFrameSnapshot()` instead of owner polling.
Rejected Alternatives: Polling `PlayerInventory` or `RadiationHazardGrid` directly from ecosystem/hazard consumers was rejected because it recreates direct dependencies. Removing legacy queues was rejected because compatibility consumers still exist.
Scalability potential: Low/toaster tier gets deterministic packet lanes and no lazy first-use allocation spikes. Middle tier can attach more hazard, UI, and ecosystem consumers to the same spans. High/Ultra can spend the saved coupling budget on richer Geiger, bloom, and ecology reactions without new producer references.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.4 us avoided in cold-lane fallback and item/radiation burst contention paths. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Flood and Voxel Carve Lane Boundary Closure

Problem: Flood and voxel carving lanes cross important visual/physics boundaries: ballast reads `SubmarineFloodStateSignal` snapshots, debris reads `VoxelCarveEvent` snapshots, and both must be visible without lazy allocation or Core-to-Caves dependency inversion.
Solution: Verified `SubmarineFloodStateSignal` is configured centrally in `GlobalSignals` and `VoxelCarveEvent` is configured owner-locally in `VoxelDeltaProcessor` before any queued carve push. The voxel lane remains in the Caves owner path and uses SignalBus default type hashing instead of widening private Core hash helpers.
Rejected Alternatives: Adding `using Hecton8.Caves` to `GlobalSignals` was rejected as domain inversion. Moving voxel carve prewarm into the debris renderer was rejected because the producer/owner should own lane capacity. Replacing carve debris with direct processor polling was rejected because the typed packet lane already exists.
Scalability potential: Low/toaster tier avoids first-carve/flood hitches and keeps debris visuals bounded. Middle tier can add more carve/flood presentation from the same packet spans. High/Ultra can spend stable packet fanout on richer debris, hull seepage, and diagnostic overlays without touching producers.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.4 us avoided on first flood/carve packet plus deterministic snapshot visibility. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Drone Docking Lane Owner Prewarm

Problem: `DroneFleetManager` consumed `DockingRequestSignal` snapshots and pushed completion/failure packets, but the three docking lanes were not owner-configured before use. Lazy default SignalBus initialization on the first docking burst is small but violates the prewarm discipline used elsewhere.
Solution: Added `DockingSignalCapacity = HeadlessDroneCapacity`, a one-shot `EnsureDockingSignalLanes()` helper, and owner-local `Configure()` / `EnsureInitialized()` for `DockingRequestSignal`, `DockingCompleteSignal`, and `DockingFailedSignal` before fleet native memory allocation and request scans.
Rejected Alternatives: Centralizing vehicle automation lanes in `GlobalSignals` was rejected because Core should not own vehicle docking semantics. Using generated hash constants was rejected because SignalBus already computes stable type hashes when no lane hash is supplied. Leaving lazy first-push initialization was rejected because docking diagnostics should have stable telemetry before the first failure or completion.
Scalability potential: Low/toaster tier gets deterministic 64-drone docking packet capacity and no first-dock allocation spike. Middle tier can add hangar UI or submarine OS consumers to complete/failed packets. High/Ultra can spend saved certainty on richer docking corridor visualization, warning audio, and fleet diagnostics.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.3 us avoided on first docking burst and lower telemetry ambiguity. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Discrete Input Command SignalLane Expansion

Problem: `PlayerInputSignal` existed but only carried inventory toggles, while PDA and fabricator UI still depended on managed input callback subscriptions for PDA, cancel, and tab commands. That left passive UI systems tied to input-service delegate lists even though they already tick inside the UI layer.
Solution: Expanded `PlayerInputSignalCommands` to cover PDA, cancel, tab navigation, interact, primary/secondary, and tool slots. `InputDispatcher` now publishes one numeric command packet for every discrete button path while keeping legacy events for authority consumers. `PlayerPDA` consumes the signal lane and removed five `IInputService` subscriptions. `HectonFabricatorUI` consumes cancel/tab commands from the signal lane, baselines command sequence when the menu opens, and keeps only native navigate/submit callbacks that still require vector/submit semantics.
Rejected Alternatives: Removing all `IInputService` events was rejected because gameplay authority owners still depend on immediate input semantics. Publishing managed command DTOs was rejected because the existing 32-byte unmanaged lane is sufficient. Converting fabricator navigate/submit was rejected because those are not yet represented by the numeric command lane.
Scalability potential: Low/toaster tier removes passive PDA/fabricator delegate churn and keeps one 32-byte command stream. Middle tier can migrate more UI panels to the same signal without adding input references. High/Ultra can attach diegetic input hints, cockpit diagnostics, and haptics to the command stream without widening producer contracts.
Hardware Impact: Estimated i3/MX350 gain is 0.2-0.8 us on UI input bursts from removed PDA and fabricator delegate dispatch/subscription surfaces, with no new managed allocation. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Pause Menu Cancel Signal Consumption

Problem: `PauseMenuController` already ticks in the UI layer but still subscribed to native cancel input. That kept one more passive UI callback surface alive after the command signal lane carried cancel packets.
Solution: `PauseMenuController` now consumes source-filtered `PlayerInputSignalCommands.Cancel` snapshots and removed its native cancel subscribe/unsubscribe path. The native pause callback remains because pause-open authority is not yet represented by the command lane and must stay immediate.
Rejected Alternatives: Converting pause-open in the same pass was rejected because the command lane does not model pause yet. Converting non-ticking controls panels was rejected because adding permanent ticks just to remove callbacks would cost more than it saves.
Scalability potential: Low/toaster tier removes passive cancel delegate churn from pause UI. Middle/High/Ultra can route more pause-panel commands through the same signal once those panels have existing tick ownership or an event bridge.
Hardware Impact: Estimated i3/MX350 gain is 0.05-0.2 us on cancel bursts and lower subscription leak risk. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Tool Slot Command Signal Consumption

Problem: `PlayerToolManager` already ticks every player frame but still refreshed four tool-slot input event subscriptions and received slot presses through managed callbacks. This contradicted its own tick-owned input model and added per-frame subscription checks.
Solution: Removed the tool-slot input subscriptions and consumed `PlayerInputSignalCommands.ToolSlot1..4` from the shared command lane inside `Tick()`. Continuous primary/secondary tool use still reads `IInputService.GetState()` because held-action state is not a discrete command packet.
Rejected Alternatives: Converting primary/secondary held use to command packets was rejected because those are continuous action states and need current state reads. Keeping subscription refresh in `Tick()` was rejected because the signal lane now carries the same slot intent without delegate churn.
Scalability potential: Low/toaster tier removes four delegate bindings and one subscription refresh branch from the player tool loop. Middle tier can migrate more discrete tool commands onto the same stream. High/Ultra can attach richer quick-slot animation, cockpit hints, and haptics without adding input references.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.4 us on slot-switch frames and a small steady-state gain from removing subscription refresh checks. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Builder Tool Edge Command Signal Consumption

Problem: `PlayerBuilder` subscribed to primary, secondary, interact, and tab input callbacks while equipped, even though builder placement, rotate, deconstruct, and catalog cycling are discrete edge commands now present in `PlayerInputSignal`.
Solution: Removed builder input subscribe/unsubscribe lifecycle and consumed `PlayerInputSignalCommands.PrimaryAction`, `SecondaryAction`, `Interact`, `TabNext`, and `TabPrevious` from `ToolTick()` while the builder is equipped. On equip, the builder baselines the latest command sequence so a pre-equip edge cannot place or rotate immediately.
Rejected Alternatives: Converting mounted transport and player interaction in the same pass was rejected because those are immediate authority paths with separate timing contracts. Polling input actions for builder edges was rejected because the command lane already preserves edge intent.
Scalability potential: Low/toaster tier removes five builder delegate bindings and avoids resubscribe churn on equip/unequip. Middle/High/Ultra can attach richer builder hologram, sound, and haptic reactions to the same command packets.
Hardware Impact: Estimated i3/MX350 gain is 0.1-0.5 us on builder command bursts and less equip/unequip subscription churn. No dotnet build/restore/rebuild was run by user order.

### 2026-05-15 - Main Menu Cancel Signal Consumption

Problem: `MainMenuController` already ticks through the UI update layer but still subscribed directly to native cancel input. This kept the main-menu close/back path on a managed callback even though the bootstrap-owned `InputDispatcher` publishes the same cancel edge into `PlayerInputSignal`.
Solution: Added source-filtered `PlayerInputSignalCommands.Cancel` snapshot consumption inside the existing main-menu tick, baselined command sequence on enable/input-manager binding, and removed the native cancel subscribe/unsubscribe plus callback method. The controller still binds `GlobalRegistry.NativeInputManager` only to keep UI action-map routing intact.
Rejected Alternatives: Removing native input-manager binding was rejected because the menu still needs action-map routing. Converting `PauseControlsPanel` and `PDAControlsRebindUI` was rejected because they are non-ticking interactive rebinding panels; adding permanent update registration for cancel/tab would spend idle frame time to remove cold callbacks. Converting mounted transport or player interaction was rejected because those remain immediate gameplay authority paths.
Scalability potential: Low/toaster tier removes one long-lived menu delegate and keeps cancel as a compact command packet. Middle can migrate more already-ticking menu surfaces onto the same lane. High/Ultra can layer richer menu haptics, transition audio, and input diagnostics from the command stream without widening producer contracts.
Hardware Impact: Estimated i3/MX350 gain is 0.05-0.2 us on main-menu cancel bursts and lower leak risk from one less native-input subscription. Steady-state cost is a bounded snapshot scan in an already active UI tick. No dotnet build/restore/rebuild was run by user order.
