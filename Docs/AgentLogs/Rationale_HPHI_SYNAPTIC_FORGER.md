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
