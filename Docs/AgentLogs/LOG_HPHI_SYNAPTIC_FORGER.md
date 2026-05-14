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
