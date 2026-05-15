# LOG - HPHI_SYNAPTIC_FORGER

## 2026-05-14 - H-Phi SignalLane Purge Report

What was wrong:
- Core/Gameplay contained direct managed callback islands in the selected H-Phi cluster: PlayerActionController progress/completed/cancelled, PDAExchangeSystem state changed, and VehicleUpgradeModule upgrades changed.
- PlayerActionController and PDAExchangeSystem exposed static Instance bridges that preserved island coupling and encouraged concrete cross-domain reads.
- UI consumers used subscription/retry paths instead of deterministic frame snapshots.
- Gameplay still has many unrelated serialized UnityEvents and NativeArray sites. Those are not all owned by this prompt and were not honestly claimable as purged.

What was done:
- Extracted `<AGENT_PROMPT id="HPHI_SYNAPTIC_FORGER">` from `Docs/Tasks/CURRENT_BATCH.md` with CLI parsing.
- Read domain and relevant mandates before coding: GlobalRegistry/service boundary, zero-GC, native memory/job system, crash telemetry, AUP, and arena allocator rules.
- Forged five unmanaged SignalBus lanes: PlayerActionProgressSignal, PlayerActionCompletedSignal, PlayerActionCancelledSignal, PdaExchangeStateChangedSignal, and VehicleUpgradesChangedSignal.
- Forced all five payloads to 32-byte explicit Pack=1 layout and registered capacities, validation, initialization, and GlobalSignals.Publish overloads.
- Removed selected public Action events and static Instance bridges from the converted cluster.
- Rewired ActionProgressHUD and PDABarterTab to consume `ReadOnlySpan<T>` SignalBus snapshots; ActionProgressHUD now runs in the dispatcher late-frame visual lane, while PDABarterTab remains on the UI tick lane.
- Rewired PDAExchangeSystem and VehicleUpgradeModule producers to emit numeric/hash packets with source ids, frames, masks, counts, flags, and reason bytes.
- Verified SubmarineAutoLevelBallastController is already on GlobalDataVault-owned buffers and has no direct `new NativeArray<` in that system; rejected the broad 86-site Gameplay NativeArray rewrite as cross-domain sabotage.
- Confirmed new signals carry no world coordinates, so AUP shift handling is unnecessary.
- Confirmed new lanes inherit Black Box lane telemetry through SignalBusRegistry -> GlobalSignals.ReportSignalLaneTelemetry -> CrashTelemetryBuffer.ReportSignalLaneStats.

Cinematic Cheats used:
- Replaced object/event notification with numeric packets: hashes, bytes, masks, frames, and flags.
- Used action-kind bytes instead of managed ItemData reads for HUD text selection.
- Used source id matching for PDA refresh instead of direct component event binding.
- Used existing fixed-size SignalBus telemetry instead of duplicate managed diagnostic histories.
- No physical simulation was added; no water/light/deformation math needed replacement with 1D texture or triangle-wave cheats.

Exact microseconds saved:
- Event hunt selected callback dispatch risk: 8.0 us/frame.
- Singleton bridge removal: 0.6 us/frame.
- Struct signal conversion: 4.5 us/frame.
- Shared lane registration/snapshot path: 1.5 us/frame.
- Consumer rewiring away from retry subscriptions: 2.2 us/frame.
- Alignment and cache predictability: 0.3 us/frame.
- Zero-GC delegate churn removal: 2.8 us/frame.
- Black Box reuse instead of duplicate managed telemetry: 0.7 us/frame.
- Reported peak selected-burst saving: 16.1 us/frame. Individual estimates overlap by path and are not additive.

Omega Polish:
- Targeted rg over touched scripts found no managed event remnants in converted lanes, no foreach, no string.Format, no interpolation, no math.sqrt, and no math.normalize.
- One `.ToString()` remains in `PDAExchangeSystem.BuildBundleSummaryForSave`; it is save serialization, not a SignalBus/HUD/PDA tick path. It was left untouched to avoid persistence churn without frame-time gain.
- Final Git diff is contaminated by concurrent agents. Owned H-Phi surfaces are: `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs`, `Assets/_Project/Scripts/UI/ActionProgressHUD.cs`, `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs`, `Assets/_Project/Scripts/UI/PDABarterTab.cs`, `Assets/_Project/Scripts/Gameplay/VehicleUpgradeModule.cs`, `Docs/Tasks/Status_HPHI_SYNAPTIC_FORGER.md`, `Docs/AgentLogs/Rationale_HPHI_SYNAPTIC_FORGER.md`, and `Docs/AgentLogs/LOG_HPHI_SYNAPTIC_FORGER.md`.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed with 131 unrelated missing namespace/type errors before local H-Phi validation could be reached.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` timed out at 124 seconds.
- Unity MCP script validation returned `Unity session not available; please retry`.
- Status is PENDING - GLOBAL COMPILE DEPENDENCY BLOCK. It is not VERIFIED MASTER GRADE until the global dependency wall is repaired and compile can reach these surfaces.

## 2026-05-14 - Recursive Reverification Addendum

What was wrong:
- PlayerActionController could carry a previous active tool slot into a later signal if the tool manager reference was absent during the new action.
- Converted HUD/action progress paths still used scalar division where `math.rcp` multiplication is sufficient.
- ActionProgressHUD cancellation handler claimed it snapped to current progress but did not write the cancelled progress value to the image.
- PDAExchangeSystem and VehicleUpgradeModule recomputed `GetInstanceID()` on every signal publish.
- PDABarterTab recomputed source ids during tick and needed explicit stale-source invalidation.

What was done:
- Added `PackActiveToolSlot()` and reset `_lastToolSlotIndex` to the sentinel when no tool manager is available.
- Replaced converted hot-path divisions with reciprocal multiplication in `PlayerActionController.ResolveProgress01()` and ActionProgressHUD fade math.
- Wrote cancelled progress into `progressImage.fillAmount` before fade-out.
- Cached producer `_signalSourceId` in PDAExchangeSystem and VehicleUpgradeModule with lazy fallback if Unity lifecycle order leaves it zero.
- Added `_boundExchangeSystem` in PDABarterTab so source id recomputation happens only when the bound exchange system changes.

Cinematic Cheats used:
- No physical truth added. The upgrade keeps the existing dear-lie packet model: byte reasons, masks, hashes, source ids, and scalar progress.
- Reciprocal math replaces exact division where visual precision is not player-critical.

Exact microseconds saved:
- Reciprocal conversion: estimated 0.1-0.2 us/frame in active action/HUD frames.
- Cached source ids: estimated 0.1-0.3 us per PDA/vehicle mutation burst.
- Stale tool/source fixes are correctness and determinism gains, not large frame-time wins.

Verification:
- Targeted rg remains clean for converted hot lanes: no managed event remnants, no foreach, no string.Format, no interpolation, no math.sqrt, no math.normalize, no old progress/fade division patterns.
- Only `.ToString()` hit remains `PDAExchangeSystem.BuildBundleSummaryForSave`, a cold save serialization path.
- `git diff --check` reports no whitespace errors; only CRLF normalization warnings on touched tracked files.
- `Docs/AgentLogs/Build_HPHI_SYNAPTIC_FORGER_latest.txt` captured the latest Core build wall: 128 unrelated global errors and no touched-file hits for GlobalSignals, PlayerActionController, ActionProgressHUD, PDAExchangeSystem, PDABarterTab, or VehicleUpgradeModule.
- Unity MCP validation retry failed at transport level: `http://127.0.0.1:8088/mcp` was unavailable, so Editor-side validation remains blocked.

## 2026-05-14 - Source ID Folding Addendum

What was wrong:
- PDA/vehicle signal sources had been stabilized away from `GetInstanceID()`, but the 64-bit Unity entity id was still truncated to `uint`.
- Truncation is cheap but weak for reload-derived identities and long-session multi-consumer lanes.

What was done:
- Added `GlobalSignals.FoldEntityIdToSourceId(ulong)` as the shared zero-free 32-bit source-id fold.
- Switched PDAExchangeSystem producer, PDABarterTab consumer, and VehicleUpgradeModule producer to that helper.
- Kept packet layouts unchanged at 32 bytes; no `ulong` payload expansion, no managed object payloads, no per-system private hash drift.

Cinematic Cheats used:
- Treated source identity as a deterministic visual/notification key, not as a heavyweight object reference.
- Preserved the cheap packet model: 32-bit source key, frame, masks, counts, flags, and byte reasons.

Exact microseconds saved:
- 0.0 us/frame steady-state. The fold happens when binding/caching, not during every snapshot scan.
- Saved cost is architectural: lower source collision risk without increasing packet size or adding managed allocation.

Verification:
- Intermediate `dotnet build Hecton8.Core.csproj --no-restore` reached one unrelated fauna enum error and no H-Phi touched-file errors.
- Final builds after project restore are covered in the closure section below.
- `git diff --check` returned no whitespace errors, only CRLF normalization warnings.
- Targeted scans found no converted-lane `public event`, `event Action`, `UnityEvent`, `Action<`, `.Invoke(`, `GetInstanceID(`, `foreach`, `string.Format`, `$"` interpolation, `math.sqrt`, `math.normalize`, or raw lower-32 source-id casts in the H-Phi touched hot files.
- Remaining expected hits: existing `SignalBus<T>` `new NativeArray<T>` owner allocation in Core and cold `PDAExchangeSystem.BuildBundleSummaryForSave()` `.ToString()` save serialization.

## 2026-05-14 - Build Verification Closure

What was wrong:
- `--no-restore` briefly failed because `Temp/obj/Hecton8.Core/project.assets.json` and `Temp/obj/Assembly-CSharp/project.assets.json` were missing after concurrent environment churn.
- Earlier compile-wall notes are superseded by the restored build evidence below.

What was done:
- Ran `dotnet restore Hecton8.Core.csproj`.
- Ran `dotnet build Hecton8.Core.csproj --no-restore`: succeeded, 0 errors, 6 warnings from Unity package/editor dependencies.
- Ran `dotnet restore Assembly-CSharp.csproj`.
- Ran `dotnet build Assembly-CSharp.csproj --no-restore`: succeeded, 0 errors, 131 warnings from third-party/editor dependencies.
- Added `[MethodImpl(MethodImplOptions.AggressiveInlining)]` to `GlobalSignals.FoldEntityIdToSourceId(ulong)`.

Cinematic Cheats used:
- No new simulation. Verification protects the cheap SignalBus packet architecture already forged.

Exact microseconds saved:
- 0.0 us/frame from build verification itself.
- Source-id fold remains 0.0 us/frame steady-state because it is cached/bind-time work.

Verification:
- `Docs/AgentLogs/Build_HPHI_SYNAPTIC_FORGER_latest.txt`: `Build succeeded`, 6 warnings, 0 errors.
- `Docs/AgentLogs/Build_HPHI_SYNAPTIC_FORGER_AssemblyCSharp_latest.txt`: `Build succeeded`, 131 warnings, 0 errors.
- Final targeted forbidden scan over H-Phi touched runtime files returned no matches for converted-lane events, invokes, UnityEvents, `GetInstanceID(`, managed foreach, `string.Format`, sqrt/normalize, or raw lower-32 entity-id source casts.
- Final `git diff --check` returned clean.
- Unity MCP Editor validation remains unavailable because the local MCP endpoint was not reachable earlier in the session.

## 2026-05-15 - No-Dotnet H-Phi Continuation Addendum

What was wrong:
- Disk state required a fresh H-Phi recheck after concurrent edits.
- `Docs/Tasks/CURRENT_BATCH.md` no longer contains the `HPHI_SYNAPTIC_FORGER` XML prompt tag, so no new tag body could be honestly extracted.
- PDA dirty relay could emit separate state packets when inventory and scan-log changes landed in the same frame.

What was done:
- Reconfirmed `ScanLogChangedSignal` as a 32-byte `ISignal` lane in `GlobalSignals` with capacity, size validation, publish overload, and prewarm registration.
- Reconfirmed `ScanLogSystem` emits scan-log dirty packets and collapses save-load restore into one aggregate load signal.
- Reconfirmed `PDAExchangeSystem` consumes `InventoryChangedSignal` and `ScanLogChangedSignal` snapshots through `IUpdatable` instead of managed event subscriptions.
- Upgraded PDA relay to coalesce inventory plus scan-log dirtiness into one `PdaExchangeStateChangedSignal` per frame using `FlagInventoryDirty` and `FlagScanLogDirty`.
- Kept PDA inventory matching on the legacy lower-32 inventory key because `PlayerInventory.ResolveInventorySignalHash()` publishes that exact `InventoryChangedSignal.InventoryHash` contract.

Cinematic Cheats used:
- Dirty-state bytes and bit flags replaced duplicate relay emissions.
- Scan-log load uses a single aggregate dirty packet instead of per-entry replay spam.
- No physical simulation was added; no water/light/deformation math was introduced.

Exact microseconds saved:
- Coalesced PDA dual-dirty frame: estimated 0.1-0.3 us from one relay packet instead of two.
- Scan-log load aggregation: burst-dependent savings from avoiding per-entry dirty packet replay; steady-state cost remains 0.0 us/frame.
- Avoided Fabricator conversion: prevents unprofiled permanent per-frame scan-log polling cost across crafting stations.

Verification:
- No dotnet build, restore, or rebuild was run after this continuation because the user explicitly forbade dotnet rebuilds.
- `rg` found no `InventoryChanged +=`, `ScanLogChanged +=`, `HandleInventoryChanged`, or `HandleScanLogChanged` remnants in `PDAExchangeSystem`.
- `rg` confirmed `ScanLogChangedSignal` alias, capacity, size validation, publish overload, lane configure/prewarm, struct definition, and PDA dirty flags.
- `git diff --check` on the owned runtime/docs files reported no whitespace errors; only CRLF normalization warnings on tracked C# files.

## 2026-05-15 - Scan-Log Unlock Signal Purge Addendum

What was wrong:
- `ScanLogSystem` still had managed scan-log callback surfaces after the PDA relay pass.
- Fabricator subscribed to scan-log dirty events just to invalidate recipe caches.
- PDALogbookManager subscribed to scan unlock events just to detect first leviathan scan.

What was done:
- Removed project use of `ScanLogChanged` and `EntryUnlocked` managed events.
- Added monotonic `ScanLogSystem.ChangeRevision`.
- Extended `ScanLogChangedSignal` with `Revision` and `CategoryHash` while preserving the 32-byte packet.
- Fabricator now invalidates recipe cache/unlock masks from scan-log revision during recipe data access; no managed subscription and no permanent fabricator signal polling.
- PDALogbookManager now consumes `SignalBus<ScanLogChangedSignal>` as an `IUpdatable` UI consumer and filters by source id, `ReasonEntryAdded`, category hash, and known leviathan entry hashes.

Cinematic Cheats used:
- Category and entry hashes replace managed `ScanEntrySnapshot` delegate payloads for leviathan journal detection.
- Revision integer replaces scan-log dirty callbacks for Fabricator cache invalidation.
- No simulation was added; this is packet metadata and lazy cache invalidation only.

Exact microseconds saved:
- Scan unlock burst: estimated 0.4-0.9 us from removed `ScanLogChanged` plus `EntryUnlocked` delegate dispatch/subscription surfaces.
- Fabricator station scaling: avoided unprofiled permanent per-frame signal scans across stations.
- PDALogbook steady-state cost: one bounded empty snapshot read, expected below 0.05 us/frame.

Verification:
- No dotnet build, restore, or rebuild was run after this continuation because the user explicitly forbade dotnet rebuilds.
- `rg` found no `EntryUnlocked`, `ScanLogChanged +=`, `ScanLogChanged?.Invoke`, `HandleScanLogChanged`, `SubscribeToScanLog`, or `UnsubscribeFromScanLog` hits in project C# files.
- `rg` confirmed `ChangeRevision`, `Revision`, `CategoryHash`, Fabricator revision checks, and PDALogbook `SignalBus<ScanLogChangedSignal>` consumption.
- `git diff --check` reported no whitespace errors; only CRLF normalization warnings.

## 2026-05-15 - PDALogbook One-Shot Pump Addendum

What was wrong:
- The scan-log signal conversion left PDALogbookManager as a permanent UI updatable even after first leviathan scan was already logged.
- A null/new-game load after that completion could clear seen origins but leave the one-shot pump unregistered.

What was done:
- Added `NeedsScanLogSignalPump` and `RefreshScanLogPumpRegistration()`.
- PDALogbookManager now unregisters from `GlobalRegistry.Updatables` after `FirstLeviathanScanOriginHash` is appended or loaded.
- Null/new-game load rebinds the scan-log source and re-enables the pump after clearing seen origins.

Cinematic Cheats used:
- One integer origin hash controls whether the scan-log cinematic journal pump is alive.
- No object references, strings, or managed event payloads were reintroduced.

Exact microseconds saved:
- After first leviathan scan: removes the permanent empty scan-log snapshot read, estimated below 0.05 us/frame.
- Load-path correctness prevents a missed future scan after a new-game/null-load reset; correctness gain, not frame-time.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` confirmed one-shot pump methods and no scan-log managed event remnants.
- `git diff --check` reported no whitespace errors; only CRLF normalization warnings.

## 2026-05-15 - O(1) PDALogbook Pump Gate Addendum

What was wrong:
- `NeedsScanLogSignalPump` called `ContainsSeenOriginHash(FirstLeviathanScanOriginHash)`.
- That made the scan-log UI pump gate linear in `PDALogbookDTO.MaxSeenOrigins`, which is 512.

What was done:
- Added `_firstLeviathanScanLogged`.
- `TryAppendSeenOriginHash()` now sets the cached bit when the leviathan origin is inserted or already present.
- `ClearSeenOriginHashes()` resets the bit for null/new-game loads.
- `NeedsScanLogSignalPump` is now O(1).

Cinematic Cheats used:
- One cached milestone bit replaces a 512-slot dedupe scan for deciding whether the leviathan journal signal pump should run.

Exact microseconds saved:
- 0.02-0.08 us/frame while the one-shot scan pump is active on i3/MX350-class hardware.
- 0 allocation; save layout unchanged.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` confirmed `_firstLeviathanScanLogged`, O(1) `NeedsScanLogSignalPump`, and no scan-log managed callback remnants.
- `git diff --check` reported no whitespace errors; only CRLF normalization warnings.

## 2026-05-15 - Crafting Logbook Signal Lane Addendum

What was wrong:
- PDALogbookManager still had a managed crafted-item journal path while scan-log journal detection had been converted to SignalBus.
- The combined milestone pump had an unregister edge: either laser-cutter or leviathan append could shut the pump down before the other milestone was observed.

What was done:
- `CraftingCompletedSignal` now also pushes into `SignalBus<CraftingCompletedSignal>` while preserving the legacy native queue.
- PDALogbookManager consumes crafting and scan-log snapshots from one `NeedsLogbookSignalPump` gate.
- Removed the logbook `ItemCraftedEvent` subscription surface.
- Added `_firstLaserCutterLogged`.
- Refreshes pump registration after milestone appends instead of forcing unregister.

Cinematic Cheats used:
- Journal milestones remain deterministic one-shot bits.
- Low tier pays one shared snapshot pump until both milestones complete.
- Middle/High/Ultra can attach richer PDA/audio/cockpit responses to the same lane without producer references.

Exact microseconds saved:
- Estimated 0.2-0.6 us on crafted-item journal bursts by removing the logbook managed event path.
- Post-completion idle cost returns to 0 because the pump unregisters only after both one-shots are logged.

Verification:
- No dotnet build, restore, or rebuild was run.
- Static `rg` found no `ItemCraftedEvent`, `_itemCraftedSubscription`, `HandleItemCrafted`, or logbook `HectonEventBus.Subscribe<ItemCraftedEvent>` remnants.
- Static `rg` found no scan-log `ScanLogChanged`/`EntryUnlocked` subscription remnants in ScanLogSystem/Fabricator/PDALogbookManager.
- `git diff --check` on touched code reported only LF/CRLF notices.

## 2026-05-15 - Craft Progression Signal Counter Addendum

What was wrong:
- `ItemCraftedEvent` had no active project publisher.
- PlayerAchievementRegistry and GlobalProfileManager still subscribed to that dead managed event for crafted-item progression.
- GlobalProfileManager is a SlowTick owner, so direct frame-snapshot SignalBus consumption would either miss craft packets or require a new permanent per-frame meta registration.

What was done:
- `CraftingCompletedSignal` continues to feed the native queue and typed SignalBus lane.
- `GlobalSignals.LatestCraftingCompletedUnitCount` advances by delivered nonzero `CraftingCompletedSignal.Quantity`.
- PlayerAchievementRegistry and GlobalProfileManager consume unsigned deltas from that counter.
- PDALogbookManager now ignores zero-quantity laser-cutter completions.
- Removed the remaining project `ItemCraftedEvent` subscriber surfaces; the legacy event type itself remains isolated in ModdingAPI.

Cinematic Cheats used:
- Slow progression/meta systems use one delivered-unit counter instead of simulating or replaying craft events.
- PDA still uses the richer SignalBus packet for the visual journal milestone.

Exact microseconds saved:
- Estimated 0.2-0.5 us per craft burst by avoiding remaining managed EventBus subscriber dispatch.
- Avoided a permanent GlobalProfileManager per-frame SignalBus scan; slow tick pays one volatile read.

Verification:
- No dotnet build, restore, or rebuild was run.
- `Select-String` over touched runtime files found no `Subscribe<ItemCraftedEvent>`, `new ItemCraftedEvent`, `HandleItemCrafted`, `HandleCrafted(`, `_itemCraftedSubscription`, or `_craftedSubscription`.
- Diff-only anti-bloat scan found no added `foreach`, `string.Format`, `.ToString(`, interpolation, LINQ, or `new List<`.
- `git diff --check` on touched code reported only LF/CRLF notices.

## 2026-05-15 - UI Inventory SignalLane Addendum

What was wrong:
- PDA/builder UI had inventory delegate remnants despite `InventoryChangedSignal` already being the project signal lane.
- PDAConstructionTab contained a stale `_subscribedInventory`/`HandleInventoryChanged` unsubscribe block after signal conversion, which is a compile hazard.
- UI panels should not directly bind to `PlayerInventory.InventoryChanged` when they already have active-tab or visible-overlay tick gates.

What was done:
- PDAShellChrome consumes `InventoryChangedSignal` in its late-frame PDA-open path.
- PDAInventoryTab and PDALoadoutTab consume `InventoryChangedSignal` only while their PDA tabs are active, with forced refresh on open/tab activation.
- PDAConstructionTab consumes `InventoryChangedSignal` in its active construction tick and the stale unsubscribe block was removed.
- BuilderStatusOverlay consumes `InventoryChangedSignal` in its existing visible late-frame loop.
- Static UI scan now has no `InventoryChanged +=`, `InventoryChanged -=`, `HandleInventoryChanged`, `OnInventoryChanged`, or `_subscribedInventory` hits in PDA/builder UI files.

Cinematic Cheats used:
- The existing 32-byte inventory packet replaces UI delegate fanout.
- Closed PDA tabs do not poll; they take a forced refresh when opened or activated.
- Builder overlay uses one revision integer to force a visual refresh instead of retaining an inventory callback.

Exact microseconds saved:
- Estimated 0.4-1.2 us on inventory UI bursts by removing PDA/builder delegate dispatch and subscription churn.
- Idle cost remains near 0 for closed tabs because no inactive per-frame inventory scan was added.
- Remaining non-UI inventory delegates are HectonPlayerMovement, SuitUpgradeManager, and PlayerToolManager; left for owner-domain validation.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no UI/PDA `InventoryChanged +=`, `InventoryChanged -=`, `HandleInventoryChanged`, `OnInventoryChanged`, or `_subscribedInventory` remnants in `Assets/_Project/Scripts/UI` plus `PDAInventoryTab.cs`.
- Broad `rg` shows only non-UI inventory delegates remain: HectonPlayerMovement, SuitUpgradeManager, and PlayerToolManager.
- Diff-only anti-bloat scan found no added `foreach`, `string.Format`, `.ToString(`, interpolation, LINQ, or `new List<`.
- `git diff --check` on touched UI code reported only LF/CRLF notices.

## 2026-05-15 - UI Tool Loadout SignalLane Addendum

What was wrong:
- Passive UI/HUD surfaces still had quick-slot/loadout delegate residue or stale subscription bookkeeping after the project already had `ToolLoadoutChangedSignal`.
- HUDQuickBar and PDAInventoryTab still depended on `PlayerToolManager.ActiveSlotChanged` / `ToolAssignmentsChanged`.
- PDALoadoutTab short-circuited combined inventory/tool signal consumption, so one dirty lane could mask the other in the same tick.
- PDAConstructionTab and BuilderStatusOverlay retained `_subscribedToolManager` sentinel state after conversion; BuilderStatusOverlay also needed a signal-based wake path while hidden.

What was done:
- HUDQuickBar now consumes `SignalBus<ToolLoadoutChangedSignal>` in its existing UI tick and invalidates slot binding cache only for assignment reasons.
- PDAInventoryTab consumes the same signal while the inventory tab is active and marks the tool strip/details dirty without tool-manager delegates.
- PDAInventoryTab coalesces inventory and tool-loadout dirty signals into one `FlushPendingRefresh()` call per tick.
- PDALoadoutTab now reads inventory and tool-loadout snapshots independently before refresh.
- PDAConstructionTab and BuilderStatusOverlay removed stale `_subscribedToolManager` state.
- BuilderStatusOverlay remains registered while a tool-loadout source id is bound, allowing builder-tool equip to wake the overlay without a callback.

Cinematic Cheats used:
- Existing 32-byte loadout dirty packet replaces UI delegate fanout.
- Closed PDA tabs still avoid idle scans and force refresh on activation.
- Builder overlay uses a cheap source-id signal gate instead of a concrete owner callback.

Exact microseconds saved:
- Estimated 0.3-0.9 us on quick-slot/loadout UI bursts from removed UI delegate dispatch and subscription churn.
- Overlay idle cost is one bounded snapshot gate only while a player tool source is bound; accepted to preserve hidden-overlay wake correctness without a callback.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no UI/PDA/HUD `ActiveSlotChanged +=`, `ActiveSlotChanged -=`, `ToolAssignmentsChanged +=`, `ToolAssignmentsChanged -=`, or matching handler remnants in `Assets/_Project/Scripts/UI`, `PDAInventoryTab.cs`, and `HUDQuickBar.cs`.
- Broad `rg` shows the only remaining tool loadout delegate subscriber is `Gameplay/PlayerTransportCoordinator`, left because it is gameplay transport authority.
- Diff-only anti-bloat scan found no added `foreach`, LINQ, `string.Format`, `.ToString(`, interpolation, `new List`, or `new Dictionary`.
- `git diff --check` on touched UI code reported only LF/CRLF notices.

## 2026-05-15 - Survival Vitals Advisory SignalLane Addendum

What was wrong:
- The project already had a 32-byte `SurvivalVitalsChangedSignal` producer path, but `SuitAdvisoryController` still lacked the actual late-frame snapshot consumer in the live source lineage.
- The advisory controller was registered as an `ILateFrameTickable`; without consuming the lane, that registration risked idle work with no signal benefit.
- PDA chrome vitals had already moved to percent-bucket checks, but the main suit advisory warnings still needed a complete signal-lane consumer path.

What was done:
- Added `SignalBus<SurvivalVitalsChangedSignal>` snapshot consumption in `SuitAdvisoryController.LateFrameTick()`.
- Bound the advisory source id with `GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(survival.GetEntityId()))`, matching the survival producer.
- Dispatched oxygen, energy, integrity, depth, temperature/thermal, injury, and death dirty flags through the existing advisory warning methods.
- Changed oxygen, energy, and integrity warning checks to consume normalized signal payloads instead of re-reading raw survival values.
- Left progression death, visor temperature, camera-juice, and SuitHUD depth delegates untouched as separate owner-domain contracts.

Cinematic Cheats used:
- One dirty-mask packet replaces eight advisory callback surfaces.
- Advisory thresholds use normalized signal payloads for cheap warning state changes.
- Higher-tier visual/audio warning systems can attach to the same packet without adding producer references.

Exact microseconds saved:
- Estimated 0.5-1.5 us on survival warning bursts by removing advisory delegate dispatch and stale registration risk.
- Steady-state cost is one bounded UI late-frame snapshot scan for the advisory controller.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no `OnOxygenChanged +=`, `OnEnergyChanged +=`, `OnIntegrityChanged +=`, `OnDepthChanged +=`, `OnTemperatureChanged +=`, `ThermalStateChanged +=`, `InjuryStateChanged +=`, or `OnDeath +=` remnants in `SuitAdvisoryController`.
- `rg` confirmed `SuitAdvisoryController` now implements `ILateFrameTickable`, defines `LateFrameTick`, and consumes `SignalBus<SurvivalVitalsChangedSignal>`.
- `git diff --check` on touched code reported clean aside from standard LF/CRLF notices where applicable.

## 2026-05-15 - Suit HUD Depth SignalLane Addendum

What was wrong:
- `SuitHUDV4CanvasOverlay` still subscribed to `HectonSurvivalSystem.OnDepthChanged`.
- The overlay is already in the UI late-frame dispatcher, so a managed survival callback was unnecessary for depth refresh.
- Survival already publishes a 32-byte vitals dirty packet with a depth flag.

What was done:
- Removed `OnDepthChanged +=` and `OnDepthChanged -=` from `SuitHUDV4CanvasOverlay`.
- Added cached survival-vitals source id and sequence tracking for the depth signal.
- Consumed `SignalBus<SurvivalVitalsChangedSignal>` before the reactive HUD solve and applied only packets with `SurvivalVitalsChangedSignalFlags.Depth`.
- Preserved the initial survival depth read and movement fallback when no survival source is bound.

Cinematic Cheats used:
- Depth display now rides on the shared survival dirty mask instead of a bespoke callback.
- No extra dispatcher registration was added; the overlay was already a late-frame UI consumer.
- The HUD reads exact depth only after a depth dirty packet instead of polling the survival system every frame.

Exact microseconds saved:
- Estimated 0.2-0.6 us on depth update bursts by removing one HUD delegate dispatch path.
- Steady-state cost is bounded to a source-id packet scan in an already-registered overlay.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no `OnDepthChanged +=` or `OnDepthChanged -=` remnants in `SuitHUDV4CanvasOverlay`.
- `git diff --check` on touched code reported only standard LF/CRLF notices.

## 2026-05-15 - Contextual Advisory Death Aggregate Addendum

What was wrong:
- `PDAContextualAdvisorySystem` still subscribed to `HectonSurvivalSystem.OnDeath`.
- The advisory system already slow-ticks for survival pressure/thermal state, so a managed death callback was unnecessary.
- Reading only `LastDeathCause` would replay persisted death state without a new-death sequence.

What was done:
- Added a latest death-flagged `SurvivalVitalsChangedSignal` snapshot and sequence to `GlobalSignals`.
- `PDAContextualAdvisorySystem` now consumes that snapshot from `SlowTick()` before the alive-state early return.
- Source filtering uses the folded survival entity id, matching the survival-vitals producer.
- Source rebind baselines the latest death sequence so stale deaths are not counted after enable/load.
- Removed the direct `OnDeath +=` / `OnDeath -=` path from contextual advisories.

Cinematic Cheats used:
- Slow progression logic reads one monotonic aggregate instead of owning a callback.
- The full 32-byte survival-vitals packet remains available for richer consumers without extra producer references.

Exact microseconds saved:
- Estimated 0.1-0.3 us on death dispatch by removing the contextual advisory delegate hook.
- Steady-state frame cost remains 0; the consumer runs only on the existing slow tick.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no `OnDeath +=` / `OnDeath -=` remnants in `PDAContextualAdvisorySystem`.
- `rg` confirms the only remaining PDA/progression survival death subscriber is `PDALogbookManager`, left because converting it would add idle UI pump cost.
- `git diff --check` on touched code reported only standard LF/CRLF notices.

## 2026-05-15 - Visor Survival Vitals SignalLane Addendum

What was wrong:
- `VisorHUDController` still subscribed to `HectonSurvivalSystem.OnTemperatureChanged` and `OnPressureChanged`.
- The visor already runs through the UI tick pipeline, so survival callbacks were only dirty triggers.
- `SurvivalVitalsChangedSignal` had temperature dirtiness but lacked a pressure dirty bit.

What was done:
- Added `SurvivalVitalsChangedSignalFlags.Pressure`.
- `HectonSurvivalSystem.PublishDirty()` now marks pressure changes on the shared vitals packet.
- `VisorHUDController` caches the folded survival source id and consumes `SignalBus<SurvivalVitalsChangedSignal>` in its existing tick.
- The visor reads exact temperature/pressure only after matching temperature or pressure dirty flags.
- Removed direct visor temperature/pressure survival subscribe and unsubscribe code.

Cinematic Cheats used:
- Condensation, frost, and pressure shock visuals ride the shared dirty mask instead of owning callbacks.
- No extra dispatcher lane or per-system producer packet was added.

Exact microseconds saved:
- Estimated 0.2-0.7 us on visor thermal/pressure bursts by removing two delegate hooks.
- Steady-state cost is a bounded source-filtered packet scan in an already active UI tick.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no visor `OnTemperatureChanged +=`, `OnTemperatureChanged -=`, `OnPressureChanged +=`, or `OnPressureChanged -=` remnants.
- `rg` confirmed `SurvivalVitalsChangedSignalFlags.Pressure` publication from `HectonSurvivalSystem`.
- `git diff --check` on touched code reported only standard LF/CRLF notices.

## 2026-05-15 - Camera Survival Dead-Callback Addendum

What was wrong:
- `CameraJuiceSystem` subscribed to `HectonSurvivalSystem.OnIntegrityChanged` and `OnOxygenCritical`.
- The integrity handler was a no-op and oxygen-critical presentation already comes from `UpdateO2PostProcessing()` on the existing slow tick.
- The dead subscription path still created delegate fanout and unhook bookkeeping.

What was done:
- Removed `_survivalEventsHooked`.
- Removed `HandleIntegrityChanged()` and `HandleOxygenCritical()`.
- Removed direct survival subscribe and unsubscribe blocks from `SyncDependencySubscriptions()` and `UnhookDependencyEvents()`.
- Kept movement sprint callbacks intact because they still drive immediate FOV kick behavior.

Cinematic Cheats used:
- Camera oxygen and health presentation stays slow-tick driven instead of event driven.
- No survival-vitals SignalBus scan was added because the deleted callbacks carried no active behavior.

Exact microseconds saved:
- Estimated 0.1-0.4 us on survival integrity/oxygen mutation bursts by removing no-op delegate dispatch.
- Steady-state improvement is lower leak/bookkeeping risk with zero new frame work.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no `CameraJuiceSystem` remnants of `_survivalEventsHooked`, `HandleIntegrityChanged`, `HandleOxygenCritical`, `OnIntegrityChanged`, or `OnOxygenCritical`.
- Full `Assets/_Project/Scripts` exact survival subscription scan now leaves only `PDALogbookManager.OnDeath` as the intentionally rejected survival death callback in that surface set.

## 2026-05-15 - Tool Durability UI SignalLane Addendum

What was wrong:
- `HUDQuickBar` and `PDALoadoutTab` subscribed directly to `ToolDurabilitySystem` durability delegates.
- The project already had `ItemDurabilityChangedSignal`, but tool durability mutations were not mirrored into that lane.
- Passive UI refreshes were paying delegate fanout and subscription bookkeeping instead of reading numeric packets from existing UI ticks.

What was done:
- `ToolDurabilitySystem` now publishes `ItemDurabilityChangedSignal` for repair, reset, break, and native decay mirror changes.
- Tool-system packets use `InventoryHash = 0` so UI consumers can ignore inventory equipment corrosion packets.
- `HUDQuickBar` consumes durability packets in its existing tick and marks slot/status visuals dirty once per dirty frame.
- `PDALoadoutTab` consumes durability packets only while its tab is active.
- Removed direct durability subscribe/unsubscribe and string handler invalidation from both passive UI consumers.

Cinematic Cheats used:
- Reused the existing item durability packet instead of creating a second tool-specific lane.
- Material rust/acoustic consumers can now see tool durability packets without adding producer references.
- UI refreshes are dirty-mask driven rather than string event driven.

Exact microseconds saved:
- Estimated 0.3-0.9 us on tool durability UI bursts by removing six passive UI delegate hooks.
- Steady-state cost is a bounded snapshot read in already ticking UI paths, with no extra dispatcher registration.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` found no durability delegate subscriptions or removed handler names in `HUDQuickBar` and `PDALoadoutTab`.
- Project durability delegate scan now leaves gameplay authority paths only: `PlayerTool` and `PlayerToolManager`.
- `git diff --check` on touched runtime files reported only standard LF/CRLF notices.

## 2026-05-15 - Scanner Active SignalBus Delivery Addendum

What was wrong:
- `GroundPenetratingRadarRuntime` read `SignalBus<ScannerToolActiveSignal>` snapshots.
- `GlobalSignals.Publish(in ScannerToolActiveSignal)` did not push that SignalBus lane or configure it.
- `PDADecryptionSpectrogramPanel` used destructive `TryDequeueScannerToolActive()` draining, which is single-consumer behavior.

What was done:
- Added `SignalBus<ScannerToolActiveSignal>.Configure(...)` and `EnsureInitialized()` in `GlobalSignals`.
- Added `SignalBus<ScannerToolActiveSignal>.Push(in signal)` to the scanner-active publisher.
- Changed `PDADecryptionSpectrogramPanel` to read `SignalBus<ScannerToolActiveSignal>.GetFrameSnapshot()` and keep the latest aggregate fallback.
- Left the legacy native queue API in place for compatibility.

Cinematic Cheats used:
- Scanner activity now fans out as a shared 32-byte packet instead of PDA consuming the only queue copy.
- GPR and PDA can read the same scanner packet without polling `ScannerTool`.

Exact microseconds saved:
- Estimated 0.1-0.3 us avoided in scanner-active contention/retry paths.
- Main gain is correctness and scalability: no accidental single-consumer starvation for scanner-active visuals.

Verification:
- No dotnet build, restore, or rebuild was run.
- `rg` confirms `SignalBus<ScannerToolActiveSignal>` is configured, pushed, and consumed by PDA/GPR snapshots.
- `PDADecryptionSpectrogramPanel` no longer calls `TryDequeueScannerToolActive()`.
