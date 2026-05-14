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
- Rewired ActionProgressHUD and PDABarterTab to consume `ReadOnlySpan<T>` SignalBus snapshots during dispatcher ticks.
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
