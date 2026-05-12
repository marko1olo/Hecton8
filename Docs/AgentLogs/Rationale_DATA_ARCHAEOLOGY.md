# Rationale_DATA_ARCHAEOLOGY

Status: PENDING VERIFICATION
Agent: DATA_ARCHAEOLOGY

## Bootstrap

Problem: Scanner UI prompt demands zero-GC work across scanner, UI, lore, save, AUP, and signals while multiple agents may be editing adjacent systems.
Solution: Treat existing scanner/data archaeology runtime as the owner, add only decoupled GlobalSignals/registry hooks, and keep runtime data in preallocated native/fixed buffers.
Rejected Alternatives: A new scanner subsystem would duplicate ownership and risk desync. Runtime prefab instantiation or TMP string assignment violates UI and GC mandates.
Scalability potential: Low tier uses plain text and no scan shader mask; Middle uses char scrambling at controlled cadence; High/Ultra use richer shader mask/audio feedback with the same authority data.
Hardware Impact: MX350 avoids per-frame string/UI allocation and prefab stalls; expected saving is from eliminating GC spikes, not from fake profiler numbers.

Problem: Required status and rationale files were missing.
Solution: Create them before code changes and keep all task state disk-backed.
Rejected Alternatives: Chat-only progress tracking cannot survive context compression.
Scalability potential: Not a runtime feature; prevents integration ambiguity.
Hardware Impact: No runtime cost.

## Decision Ledger

- Native scan state implementation: added persistent `NativeParallelHashMap<int, byte>` in `DataArchaeologyRuntime`.
- RaycastCommand integration: used `SystemDispatcher.QueueDispatcherRaycast` instead of a second tool-local scheduler.
- Zero-GC PDA decryption label: added `PDADataArchaeologyDecryptLabel` using `CharBufferPool`, `Span<char>`, `LocRegistry.GetLength`, and TMP `SetCharArray`.
- Runtime lore bitmask: added `_unlockedLoreWords` NativeArray and save/load mirror.
- AUP handling: registered with `HectonFloatingOrigin` listener instead of draining shared `AupShiftSignal`.
- Scan presentation: gated `_HectonScannerPoints` and text scramble on Low/MX350.
- Save/state persistence bridge: added SaveData v66 key/value scan state arrays and codec round trip for NativeParallelHashMap state.

## Loop 1 - Tasks 1-5

Problem: Scanner target state needed to survive across scanner ticks without managed collections.
Solution: Added `_scanStates` as `NativeParallelHashMap<int, byte>` in the existing data archaeology owner and mirrored hash-only target progress through fixed partial arrays.
Rejected Alternatives: `Dictionary<uint, ScanState>` or a `List` of unlocked entries; both create managed ownership pressure and contradict the prompt.
Scalability potential: Low/MX350 pays only scalar and map writes; High/Ultra can consume the same state for richer PDA/shader/audio response.
Hardware Impact: Removes managed state churn from scan hold path; expected frame win is hitch prevention, not steady-state fake numbers.

Problem: Prompt required `RaycastCommand`, but adding another raycast scheduler would duplicate core ownership.
Solution: `ScannerTool` now implements dispatcher raycast receiver and queues a `RaycastCommand` into `SystemDispatcher`.
Rejected Alternatives: `Physics.Raycast`, `RaycastAll`, or tool-local NativeArray scheduler. Synchronous physics is a hot-path stall; a second scheduler duplicates global batching.
Scalability potential: Dispatcher budget can throttle globally on Low while High/Ultra can accept more deferred ray slots.
Hardware Impact: Avoids per-tool job/container overhead and keeps MX350 raycasts in one batch lane.

Problem: Completion needed to notify crafting/HUD without direct references.
Solution: Added unmanaged `BlueprintUnlockedSignal`, `ToolAcousticSignal`, and `HUDNotificationSignal` to `GlobalSignals`, plus hash-only publishes from completion and scan progress.
Rejected Alternatives: Concrete Crafting/HUD references or string event names. That would violate parallel-agent decoupling and EventBus hash discipline.
Scalability potential: Low can ignore cosmetic consumers; High/Ultra can layer audio/UI/visual overkill from the same packets.
Hardware Impact: NativeQueue enqueue is bounded and avoids managed delegate/event allocation.

Problem: PDA decryption visual had to display names without string creation.
Solution: Added `LocRegistry.GetLength` and `PDADataArchaeologyDecryptLabel`; it leases `CharBufferPool`, uses `Span<char>` scramble, then writes TMP via `SetCharArray`.
Rejected Alternatives: `TMP_Text.text`, `string.Create`, runtime UI prefab instantiation, or private char array resize. All are allocation risks in hot UI.
Scalability potential: Low disables scramble; Middle updates hash labels with controlled effect; High/Ultra uses fast scramble cadence for richer PDA feedback.
Hardware Impact: Converts string/prefab stall into pooled char copy; expected cost 10-35 us per active label update, measured proof absent.

Compile Evidence: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 errors. Warnings were existing CS0649 fields in audio/world files, not new scanner/UI code.

## Loop 2 - Tasks 6-10

Problem: The prompt required hash-to-text without strings.
Solution: `LocRegistry.GetLength(int/uint)` exposes raw text length and `PDADataArchaeologyDecryptLabel` uses the existing registry buffers plus a `CharBufferPool` lease.
Rejected Alternatives: `new string`, TMP `.text`, and any per-label resize buffer. They generate managed memory or UI rebuild risk.
Scalability potential: Low reads stable text; Middle/High/Ultra can scramble more often without changing data ownership.
Hardware Impact: Prevents text allocation hitches; update cost becomes bounded char copy.

Problem: Lore unlock authority was managed-only at runtime.
Solution: Added `NativeArray<ulong>` `_unlockedLoreWords` and mirrors to managed save words only at save/load boundaries.
Rejected Alternatives: Replacing the save format immediately. That risks migration breakage during parallel agent work.
Scalability potential: Low pays one bit op; High/Ultra can use the same bitmask for databank visual overkill and direct native queries.
Hardware Impact: Native bit set is sub-microsecond; managed copy is cold save/load only.

Problem: AUP rebasing must not consume a shared global signal lane.
Solution: Implemented `IOriginShiftListener` and registered with `HectonFloatingOrigin`; cached scan positions and hologram matrices rebase on committed shift.
Rejected Alternatives: Draining `GlobalSignals.TryDequeueAupShift` in this scanner runtime, because that can steal shift packets from world streaming consumers.
Scalability potential: Low has no steady-frame cost; High/Ultra can preserve scanner hologram precision across large worlds.
Hardware Impact: 0 us normal frames; shift-only O(discovered scans + holograms).

Problem: Scan grid visual needed AUP shader data without low-tier cost.
Solution: Publish one preallocated `_HectonScannerPoints` array and `_HectonScannerPointCount` only when position/progress changes and only above Low/MX350.
Rejected Alternatives: MPB per target, renderer material mutation, or shader upload every tick. These break SRP batching or waste CPU.
Scalability potential: Low disables the mask; Middle gets one point grid; High/Ultra can consume up to the four-point array for richer surface grids later.
Hardware Impact: 0 us on Low; dirty upload only on non-low tiers, measured proof absent.

Problem: Scanner audio feedback needed progress pitch without direct audio ownership.
Solution: Emit `ToolAcousticSignal(Scanning)` with target hash, progress, pitch, intensity.
Rejected Alternatives: direct `GlobalRegistry.Audio` call from the new scan path. Existing legacy calls remain, but new data archaeology signal path is decoupled.
Scalability potential: Low can reduce or ignore consumers; High/Ultra can layer granular audio from the same signal.
Hardware Impact: NativeQueue enqueue only; no AudioSource allocation.

Compile Evidence: second `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 warnings and 0 errors.

## Loop 3 - Tasks 11-15

Problem: Scanner unlocks needed UI/crafting notifications without text allocation or direct subsystem dependency.
Solution: Completion publishes `BlueprintUnlockedSignal`, `ScanCompleteSignal`, and `HUDNotificationSignal` using the same FNV/entity hash key.
Rejected Alternatives: Direct HUD string display or direct crafting manager call. Those create ownership coupling and string work inside scanner completion.
Scalability potential: Low can consume only the signal bit; High/Ultra can turn the same hash into richer PDA unlock animation and notification layering.
Hardware Impact: NativeQueue enqueue only; estimated 1-3 us per completion, measured proof absent.

Problem: Math LOD had to remove cosmetic scanner work on weak hardware.
Solution: Gate PDA scrambling and `_HectonScannerPoints` shader uploads on Low/Unknown/MX350 presentation tiers.
Rejected Alternatives: Lowering effect frequency. Frequency reduction still pays branch/text/shader work and violates the explicit Low Tier disable requirement.
Scalability potential: Low gets stable readable text and no mask; Middle gets hash scramble and one shader point; High/Ultra can expand visual overkill from the same fixed four-point array.
Hardware Impact: 0 us for disabled effects on MX350-class path; non-low tiers pay only dirty uploads.

Problem: Native scan states needed persistence beyond lore-complete bitmasks.
Solution: Added `SaveData` version 66 scan-state key/value arrays and `SaveBinaryPayloadCodec` read/write helpers; `DataArchaeologyRuntime` copies NativeParallelHashMap key/value pairs only in cold save/load.
Rejected Alternatives: Only saving the lore bitmask. That would drop `Scanning` states and make the prompt's native map non-authoritative across sessions.
Scalability potential: Low/Middle/High/Ultra all share the same compact byte state payload; richer databank UI can use the state later without changing save shape.
Hardware Impact: Cold save O(n) copy only; 0 us normal frame cost.

Problem: Existing `ScannerTool.cs` contains legacy string-heavy reporting paths, so a blunt grep could falsely condemn the new path.
Solution: Logged full recon counts and separately audited the hot `ToolTick` -> `UpdateScientificScanning` slice. The hot slice has zero matches for `Instantiate`, `string`, `new string`, `string.Create`, `.ToString(`, or `Format(`.
Rejected Alternatives: Rewriting all legacy scanner presentation strings in this pass. That is outside the XML tag and risks unrelated regressions.
Scalability potential: Low keeps the new hot path hash/float-only; High/Ultra can still use existing cold reporting until a separate prompt owns it.
Hardware Impact: New data archaeology scan update path avoids string allocation hitches; legacy reporting remains documented.

Problem: Self-review found that releasing the trigger before accumulating progress could write a meaningless zero-progress partial.
Solution: Guarded `StopScientificRaycastTargetScan` so it only persists partial state when `_activeScientificEntityProgress > 0f`.
Rejected Alternatives: Letting zero-progress entries enter the native map. That adds noise to save/recon data without player-visible state.
Scalability potential: Keeps Low save payload small and High/Ultra state maps clean for richer overlays.
Hardware Impact: Removes a cold useless map write; microsecond impact is negligible but state quality improves.

Compile Evidence: post-save-bridge `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 errors and one existing CS0649 warning in `WorldSpatialHashGrid.cs`. Final compile after self-review patch remains pending.

## OMEGA POLISH CHANGES

Problem: OMEGA audit required a final pass for math bloat, hot allocations, string generation, domain leaks, and build health.
Solution: Reran hot-slice scanner grep, PDA/runtime string-allocation grep, targeted `new`/math scans, scoped diff hygiene, prompt extraction, and final compile. The only code change from self-review was the zero-progress guard in `StopScientificRaycastTargetScan`.
Rejected Alternatives: Adding the optional 5% "Data Corrupted" restart branch. That would require a deterministic failure-state contract between scanner/runtime/UI and is not in the 15 primary objectives; a random failure in the hot scanner loop would violate predictable UX without authored design data.
Scalability potential: Low/Unknown/MX350 disables PDA scrambling and shader mask entirely; Middle uses pooled char scrambling and one global shader point; High/Ultra can consume the fixed four-point shader array and signal lanes for stronger presentation without changing authority data.
Hardware Impact: Low-tier avoids char mutation and shader global uploads; final hot scanner update remains hash/float/NativeQueue work. Measured profiler proof is absent.

Problem: Honest expensive calculations could have leaked into the scan presentation path.
Solution: No physical simulation was touched. The implementation uses cinematic cheats instead of honest surface simulation: one capped `_HectonScannerPoints` shader array, progress scalar pitch, FNV/entity hashes, bitmasks, and AUP rebase only on actual origin shift.
Rejected Alternatives: Per-object material mutation, per-renderer scan overlays, or surface-wide CPU distance grids. These would burn CPU/GPU bandwidth for a visual that can be shader-faked.
Scalability potential: Low path is stable text and no grid; High/Ultra can spend saved CPU on denser shader-side visual response.
Hardware Impact: 0 us shader-mask work on Low; dirty global upload only on non-low tiers.

Problem: Hidden managed allocation patterns had to be separated from existing legacy scanner strings.
Solution: Recon shows `ScannerTool.cs` has 0 `Instantiate`, 256 `string` matches, but 0 matches in the `UpdateScientificScanning` hot slice for `Instantiate`, `string`, `new string`, `string.Create`, `.ToString(`, or `Format(`. `PDADataArchaeologyDecryptLabel` and `DataArchaeologyRuntime` have no `new char[`, `new string`, `.text =`, `SetText(`, `string.Create`, or `ToString(` hits.
Rejected Alternatives: Rewriting all legacy scanner reports. That is a separate scope and would risk unrelated presentation regressions.
Scalability potential: New data archaeology path is clean for Low; existing cold/legacy strings remain documented for a future scanner-presentation pass.
Hardware Impact: Prevents new scanner hold path GC spikes; legacy reporting debt remains outside this XML task.

Problem: Domain boundary leaked into save/core files.
Solution: Cross-domain edits were limited to interfaces/data lanes needed by the XML: `GlobalSignals` unmanaged signal lanes, `SaveData`/`SaveBinaryPayloadCodec` persistence payload, and `LocRegistry.GetLength` hash text length access.
Rejected Alternatives: Direct Crafting/HUD/Narrative concrete references or a new scanner-specific event framework.
Scalability potential: EventBus consumers can scale down or up by tier without scanner ownership changes.
Hardware Impact: NativeQueue enqueue and cold save payload copy only.

Final Git Diff:
- Tracked diff stat: `LocRegistry.cs` 41 lines, `SaveBinaryPayloadCodec.cs` 153 lines, `SaveData.cs` 46 lines, `ScannableTarget.cs` 23 lines, `ScannerTool.cs` 193 lines; total 436 insertions / 20 deletions.
- Untracked/new files in this working tree: `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs`, `Assets/_Project/Scripts/UI/PDADataArchaeologyDecryptLabel.cs`, `Docs/AgentLogs/RECON_DATA_ARCHAEOLOGY.md`, `Docs/AgentLogs/Rationale_DATA_ARCHAEOLOGY.md`, `Docs/Tasks/Status_DATA_ARCHAEOLOGY.md`.
- Scoped `git diff --check -- <DATA_ARCHAEOLOGY paths>` passed; repository-wide `git diff --check` still reports unrelated pre-existing whitespace in AGENTS/docs/meta files.

Build Evidence: final `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 warnings and 0 errors.

## Continuation Audit - 2026-05-12

Problem: Re-audit found the scanner label hash-to-text path had preserved raw localized buffers but risked losing RTL visual-buffer behavior.
Solution: Restored `LocRegistry.ResolveVisual` and `TryGetVisualBuffer` RTL routing through `RTLProcessor` while keeping `GetLength` and pooled char output for the DATA_ARCHAEOLOGY label.
Rejected Alternatives: Returning only raw buffers for every language would be faster by a branch but would regress Arabic/Hebrew presentation. Allocating per-call visual strings would violate the zero-GC mandate.
Scalability potential: Low tier still gets stable no-scramble text; Middle/High/Ultra can use the same visual buffer for richer decrypt effects without changing localization ownership.
Hardware Impact: One language-tier branch and raw buffer copy; no new hot managed allocation. MX350 cost remains bounded by label length.

Problem: The PDA decryption label could remain registered after a zero hash or missing TMP target, and completed labels continued consuming UI-lane ticks.
Solution: Added zero-hash `Clear()`, zero-length TMP `SetCharArray` with a static empty char buffer, disabled TMP raycast targeting, and unregister-on-complete/unavailable-target logic.
Rejected Alternatives: Assigning `targetText.text = string.Empty` or destroying the UI object. String assignment allocates/rebuilds through TMP; object destruction would create lifecycle churn and prefab risk.
Scalability potential: Low tier reads stable text and exits the UI tick lane after completion; High/Ultra spends UI work only while the decrypt presentation is active.
Hardware Impact: Removes persistent per-frame UI tick checks after completion; expected saving is sub-microsecond per inactive label plus avoided UI raycast participation.

Problem: Scanner raycast reacquisition could keep already-scanned entities in the active raycast target path and reset visual progress intent.
Solution: `DataArchaeologyRuntime.RegisterRaycastTarget` now returns `false` for zero or already-scanned hashes, and `ScannerTool.ConsumeScientificRaycastHit` only activates entity progress when registration succeeds.
Rejected Alternatives: Letting already-scanned hits flow through and filtering only at completion. That burns scalar/map work every hold tick and creates confusing presentation resets.
Scalability potential: Low tier skips redundant work; High/Ultra keeps shader/audio overkill reserved for active scan state only.
Hardware Impact: Avoids repeated map writes and shader-point dirtying for completed targets; estimated 2-5 us avoided per rejected reacquisition.

Problem: Old migrated saves could have data archaeology bit words and partial arrays but lack the v66 scan-state key/value arrays.
Solution: Added migration guards in `EnsureLoreSystems` to allocate fixed scan-state arrays, reset invalid counts, and clamp counts to actual array capacity.
Rejected Alternatives: Depending only on binary payload reads to initialize the arrays. Legacy/non-binary paths could still enter runtime with null arrays.
Scalability potential: All tiers share one compact byte-state save shape; future PDA/databank overlays can read state without save-format churn.
Hardware Impact: Cold migration only. Normal frame cost remains 0 us.

Problem: Verification had contradictory results: standalone dotnet compile passed, while Unity editor console surfaced an outside-domain compile error.
Solution: Reran scoped hot allocation scans, scoped diff hygiene, and `dotnet build`; all DATA_ARCHAEOLOGY evidence passed. Unity refresh then timed out, and the last available console error pointed to `SubmarineStructuralGrid.cs` missing `ILateFrameTickable.LateFrameTick()`, outside the DATA_ARCHAEOLOGY domain.
Rejected Alternatives: Editing `SubmarineStructuralGrid.cs` from the UX scanner prompt. That would violate domain ownership unless the user explicitly authorizes an integrator/compiler-fix pass.
Scalability potential: Not a runtime feature; keeps the scanner pass evidence isolated for integration.
Hardware Impact: No runtime cost. Editor verification remains blocked by outside-domain compile state, not by the scanner implementation.

## Continuation Audit - MMF Flush Cadence and Lifecycle

Problem: Partial scan progress could mark the MMF sidecar dirty every scan frame and `LateFrameTick` could synchronously rewrite the sidecar in-frame. `Dispose()` also did not unregister render/save/late-frame registrations when called directly.
Solution: Added explicit MMF dirty scheduling: partial progress flushes no faster than 4 seconds, completion/removal/interruption flushes after 0.25 seconds, clean states skip disk writes, and disable/dispose now share `UnregisterRuntime()`.
Rejected Alternatives: Writing the sidecar every dirty frame or removing MMF persistence. Per-frame file I/O risks scan hitches; removing the cache would regress cold PDA/data archaeology continuity.
Scalability potential: Low/MX350 gets bounded file I/O outside the hot scan hold path; High/Ultra keeps the same state surface for stronger PDA/databank visuals without changing persistence shape.
Hardware Impact: Removes a potential multi-millisecond filesystem stall from the scan frame. Normal scan-frame cost returns to scalar/map work plus dirty timestamp checks; measured profiler proof remains absent.

Problem: The project build was blocked by an outside-domain dirty `HectonPlayerMovement.cs` change that introduced scheduled drag-job methods but omitted the backing fields.
Solution: Added only `_playerKinematicsDragScheduled` and `_playerKinematicsDragJobHandle` beside the existing player kinematics state fields.
Rejected Alternatives: Reverting the other agent's movement work or redesigning the player kinematics job path. Both would violate shared-worktree ownership and exceed the DATA_ARCHAEOLOGY prompt.
Scalability potential: Not a scanner feature; restores compile so the UX scanner work can be verified in the full project.
Hardware Impact: The two fields add negligible per-player memory. Runtime behavior remains whatever the existing movement job code intended; this patch is compile-only.

## Continuation Audit - MMF Authority and AUP Persistence

Problem: MMF cold-load restored fragment positions and partial arrays but did not restore completed lore bits or partial scan states into the native scan-state map. This violated the native map authority rule after sidecar-only recovery.
Solution: Completed MMF fragment records now set the lore bit and scanned state; partial MMF records deduplicate by hash and set the native scan state to Scanning unless the hash is already Scanned.
Rejected Alternatives: Trusting the binary SaveData payload alone. The prompt explicitly requires an MMF/state pipeline, so sidecar recovery must not produce half-authoritative state.
Scalability potential: Low/MX350 gets correct native state without managed repair passes; High/Ultra PDA/databank overlays can trust one native map for richer presentation.
Hardware Impact: Cold-load O(fragment + partial count) only. Normal scan-frame cost remains unchanged.

Problem: `PopulateScanStateSaveData` used `GetKeyValueArrays(Allocator.Temp)` to snapshot scan states before copying them to save arrays.
Solution: Replaced the temp native allocation with direct `NativeParallelHashMap` enumeration into preallocated save arrays.
Rejected Alternatives: Keeping the cold temp snapshot. It compiled and was cold, but it was unnecessary memory churn in a system claiming zero-GC/low allocation discipline.
Scalability potential: Low-tier saves avoid transient native allocation; High/Ultra state capacity remains fixed and predictable.
Hardware Impact: Removes one cold native temp allocation per save. Microsecond gain is save-path only and not frame-time relevant.

Problem: AUP origin shifts rebased persisted fragment mirrors in memory but did not dirty the MMF sidecar, allowing stale runtime positions to survive a quit after a shift.
Solution: `RebaseRuntimePositions` now marks the sidecar dirty on the bounded 4-second cadence when persisted fragment mirrors change.
Rejected Alternatives: Writing the sidecar immediately during the shift event or ignoring the persistence mismatch. Immediate disk I/O risks a shift hitch; ignoring it breaks persistence correctness.
Scalability potential: Low tier keeps shift work scalar and delayed; High/Ultra keeps precise scanner hologram/databank positions after long-world traversal.
Hardware Impact: 0 us normal frames; shift-only dirty flag and later cold sidecar flush.

## Continuation Audit - UI Retry and Stale Partial Cleanup

Problem: The PDA decrypt label could fail to render when `CharBufferPool` had no lease available, then still mark itself clean or unregister on a completed scan. That could leave stale or empty text after transient UI buffer contention.
Solution: `RenderHash` now returns `bool`; failed render keeps `_dirty = true` and leaves the label registered for a later tick retry. Render copy length is clamped by both localization buffer capacity and pooled slot capacity before `AsSpan`. Successful completed labels still unregister after writing their final char buffer.
Rejected Alternatives: Falling back to `TMP_Text.text`, `SetText(string)`, or allocating an emergency `char[]`. Those would hide the failure by violating the zero-GC UI mandate.
Scalability potential: Low tier has the same retry correctness with no scramble; Middle/High/Ultra keep richer decrypt presentation without stale-label risk under pooled-buffer pressure.
Hardware Impact: Adds one branch and no allocation. MX350 avoids stale UI while preserving the same pooled `SetCharArray` path.

Problem: Explicit Unscanned or Scanned state could coexist with partial progress loaded from old SaveData/MMF records, and SaveData partial load could preserve duplicate hashes.
Solution: Non-Scanning explicit state now wins after scan-state load via `RemoveNonScanningPartials(false)`, MMF partial load skips Scanned hashes, both SaveData/MMF partial cold-load paths use `InsertOrUpgradePartialCold` to deduplicate hashes and keep highest progress without marking the sidecar dirty, and `SetScanState(..., ScanStateUnscanned)` removes the native map key instead of persisting zero-state authority.
Rejected Alternatives: Trusting array order, repairing only in UI, or allowing duplicate partials until the next scan. Those leave native authority ambiguous and can show regressed scan progress.
Scalability potential: Low/MX350 receives compact, deterministic cold state; High/Ultra PDA/databank overlays can trust one partial record per hash for richer state visualization.
Hardware Impact: Cold load only. Normal scanner frames remain unchanged; no `MarkMmfDirty` or file I/O is triggered by load-time dedupe.

Problem: The newest verification pass could not use full compile because the user explicitly blocked `dotnet build`.
Solution: Ran static allocation greps, scoped diff hygiene, and targeted code readback only; status remains `PENDING VERIFICATION`.
Rejected Alternatives: Ignoring the user and running a build anyway. That violates the newest instruction and would contaminate the evidence chain.
Scalability potential: Not a runtime feature; keeps evidence honest until Unity/profiler proof is available.
Hardware Impact: No runtime cost.
