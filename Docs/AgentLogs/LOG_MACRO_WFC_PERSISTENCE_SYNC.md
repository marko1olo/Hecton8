# MACRO_WFC_PERSISTENCE_SYNC Log

Status: PENDING VERIFICATION

## Run Report: Outpost Save Delta Sync
Status: PENDING VERIFICATION.

What was wrong:
- WFC outpost player mutations had no backend truth path. Looted datapads, door state, and restored power could be regenerated from seed and lose player changes.
- The persistence path had chunk/page primitives, MacroDB dirty payload support, DataVault native buffers, and signal lanes, but no WFC-specific delta contract.
- Full project verification is blocked by concurrent missing Audio Virtualization, AI Cognition/Fauna, and Outpost generation contracts.

What was done:
- Extended `IAsyncPersistenceService` with native-grid WFC persist/restore methods.
- Added WFC persistence contracts: exact 10x10x5 constants, four mutable bit planes, status enum, and mutable cell flags.
- Added `BufferID.WfcOutpostGrid` for DataVault-owned WFC grid storage.
- Added `WfcOutpostStateChangedSignal` as a 32-byte typed signal lane and bounded SaveManager drain.
- Added SaveManager WFC persist path: compare mutable bit transition, pack 500 cells into 32 `ulong` words, hash snapshot, skip unchanged sector payloads, RLE encode, `MacroDB.MarkDirty(sectorHash, payload)`, and queue background `TryAppendDirtyPayload`.
- Added hydrate path: bounded `SectorHydratedSignal` scan queries MacroDB by absolute sector hash and applies valid WFC payloads into the DataVault WFC grid before the World solver consumes it.
- Added `SaveBinaryPayloadCodec` WFC payload read/write with magic, version, dimensions, plane count, raw byte count, stored byte count, and RLE flag validation.
- Added corruption guard: length/header/grid mismatch rejects payload and leaves fresh deterministic generation path available.
- Added `WfcBytesSaved` telemetry through `GlobalTelemetryBus.PublishModTelemetry(WFCP, WFBS, savedBytes)`.
- Omega polish removed the written-only WFC append counter and `System.Threading` dependency, then replaced four branch tests per WFC cell with branchless masked `ulong` OR writes.

Cinematic cheats used:
- Exact mutable bitmask instead of replaying player interaction history.
- Byte-RLE over a 32-word bitmask instead of a full 500-byte mutable grid blob.
- Dirty-on-transition plus snapshot hash instead of writing every interaction signal.
- DataVault restore injection instead of simulating outpost power/loot history during hydration.

Microseconds saved:
- Verified measured savings: 0 us. No Unity profiler/GCMonitor session was available.
- Static estimate: branchless pack path removes 2,000 branch tests per full 500-cell pack. Target pack budget remains under 10 us on i3/MX350.
- Static estimate: unchanged sector snapshot skips MacroDB dirty write entirely; disk/MMF append moved off main thread via Unity Awaitable background thread.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` failed due project-wide missing dependencies, including `Hecton8.Audio.Virtualization`, `Hecton8.AI.Cognition`, `Hecton8.Environment.Fluids`, `IOutpostGenerationService`, and `H8BinaryWorldPager`.
- Initial Unity/Bee Roslyn response files passed for `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, `Hecton8.Core.Persistence.Paging`, and `Hecton8.Core.Database`; current recheck status is superseded below because concurrent Memory edits introduced new unrelated blockers.
- Unity/Bee `Hecton8.Core` response-file compile failed only on unrelated Audio Virtualization, AI Cognition/Fauna, and `IOutpostGenerationService` blockers. No WFC persistence, WFC codec, WFC DataVault, or WFC signal errors appeared.
- Unity MCP console check failed: HTTP transport to `127.0.0.1:8088/mcp` unavailable.

Blocked work:
- Superseded by recheck below: the current World outpost runtime now has a separate mutable-state grid wired through `GlobalRegistry.AsyncPersistence`; topology-grid overwrite was explicitly rejected.
- Actual Burst import verification remains blocked until Unity editor/session and project compile blockers are cleared.

Final Git Diff Stat:
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: 16 lines changed.
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`: 283 lines changed.
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`: 68 lines changed.
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`: 39 lines changed.
- `Assets/_Project/Scripts/SaveManager.cs`: 111 lines changed.
- `Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md`: 71 lines changed.
- `Docs/Tasks/Status_MACRO_WFC_PERSISTENCE_SYNC.md`: 47 lines changed.

## Recheck Report: Mutable Grid Integration
Status: PENDING VERIFICATION.

What was wrong:
- Recheck found an actual integration fault: the current World outpost `WfcGrid` uses the low nibble for topology kind and the high nibble for N/E/S/W adjacency. The backend mutable flags also use low-nibble bits. Direct restore into that grid would corrupt generated rooms/doors/datapads.
- The DataVault mutable grid is one buffer. If mutation signals for different sector hashes arrive, stale mutable cells could bleed into another sector payload unless the grid is reset/restored on sector switch.
- WFC payload read accepted unknown flag bits, which is unsafe for binary save compatibility.

What was done:
- Added World-side `_wfcMutableStateGrid` and restored it through `GlobalRegistry.AsyncPersistence.TryApplyWfcOutpostStateOverride` before solve scheduling.
- Passed the mutable grid into `MarauderOutpostMatrixExtractionJob`; extraction now merges mutable bits into shell/proxy metadata without overwriting topology/adjacency bytes.
- Tightened `IAsyncPersistenceService` XML documentation to warn callers not to pass a topology/adjacency-packed WFC cell grid.
- Added SaveManager sector tracking for the DataVault mutable grid: clear 500 cells, restore saved sector payload if present, then apply the incoming changed cell.
- Hardened WFC payload decode by rejecting unknown flags and invalid raw stored lengths.
- Renamed the restore helper to `UnpackWfcOutpostMutableStateGrid` and made restore unpack branchless.

Cinematic cheats used:
- Separate mutable truth grid instead of replaying interaction history or re-solving gameplay state.
- Metadata merge during extraction instead of topology mutation.
- Byte-RLE bitmask remains the disk truth; visuals can consume metadata per tier.

Exact microseconds saved:
- Measured savings: 0 us. Unity runtime/profiler remains unavailable.
- Static estimate: branchless restore removes 2,000 branch checks per 500-cell restore.
- Static estimate: sector switch guard costs one 500-byte clear only when sector hash changes, preventing bad payload writes instead of adding a managed cache.
- Static estimate: mutable grid extraction adds one byte read and one mask per solid outpost cell, cold generation only.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` using id-attribute regex.
- Static scans confirmed no remaining `UnpackWfcOutpostGrid` calls and confirmed mutable-grid path in World outposts.
- `Hecton8.Core.Contracts` Unity/Bee response-file check passes after the documentation/contract clarification.
- `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` failed on unrelated project-wide missing namespaces/types, including Audio Virtualization/Propagation, AI Ecology/Cognition, Environment Fluids, Physics CCD, World Terrain/GroundRadar/resources/outpost contracts, `H8BinaryWorldPager`, and `MacroSwarm`.
- Unity/Bee support compile now fails earlier in `Hecton8.Core.Memory` on unrelated `GlobalDataVault` defrag symbols (`DefragFlagStressBlocked`, `CompactionStressThreshold`, `VaultMemMoveJob`, etc.).
- `Hecton8.World.Outposts` Bee response-file compile is blocked by missing stale `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.ref.dll`.

Blocked work:
- Runtime save/load roundtrip, Unity import, Burst Inspector, PlayMode, profiler, and GCMonitor proof remain unavailable under the current compile wall.

## Recheck Report: Mutable-State Purity
Status: PENDING VERIFICATION.

What was wrong:
- `RestoreWfcMutableState` returned on `sectorHash == 0` before clearing `_wfcMutableStateGrid`; debug/invalid generation could reuse previous restored flags.
- SaveManager restore and mutation assignment still preserved high/non-mutable bits from the old topology-grid assumption.

What was done:
- World restore now clears the 500-byte mutable-state grid before rejecting zero sector hash or missing persistence.
- SaveManager changed-cell path now writes exact mutable flags to the grid.
- SaveManager restore unpack now writes exact four-plane flags per cell instead of preserving non-mutable bits.

Cinematic cheats used:
- Exact four-plane mutable truth remains separate from generated topology.
- Cold 500-byte clear replaces any managed per-sector cache or history replay.

Exact microseconds saved:
- Measured savings: 0 us. No profiler.
- Static estimate: one mask/OR removed per changed-cell write and per restored cell write.
- Static cost: one 500-byte clear on cold generation before restore lookup.

Verification:
- Static grep confirms no remaining `immutableMask` or old `UnpackWfcOutpostGrid` path in `SaveManager`.
- `Hecton8.Core.Contracts` Unity/Bee response-file check exits 0.
- Full `dotnet build Hecton8.Core.csproj` timed out at 132 s under the existing dependency wall; follow-up process scan showed no lingering `dotnet` or `MSBuild` process.

## Recheck Report: Binary Boundary And Extraction Cost
Status: PENDING VERIFICATION.

What was wrong:
- WFC payload decode accepted exact valid WFC data plus trailing bytes. That is unsafe for a fixed binary save record.
- Outpost matrix extraction paid an `IsCreated`/length branch for every solid cell when the service-owned mutable grid is always allocated with the topology grid.

What was done:
- `SaveBinaryPayloadCodec.TryReadWfcOutpostBitmaskPayload` now requires exact record length: header plus stored bytes, no trailing data.
- `MarauderOutpostMatrixExtractionJob` now reads `MutableGrid[cellIndex]` directly and masks the low mutable nibble.

Cinematic cheats used:
- Fixed tiny bitmask truth remains the authoritative save payload.
- One native mutable grid feeds renderer/proxy metadata; no history replay or managed per-cell state.

Exact microseconds saved:
- Measured savings: 0 us. No profiler.
- Static estimate: one branch and one length compare removed per solid extracted cell, up to 500 cells full tier / 75 low tier.
- Static cost: one integer equality check during payload restore.

Verification:
- Static grep confirms exact-length guard and no `MutableGrid.IsCreated` branch in outpost extraction.
- `Hecton8.Core.Contracts` Unity/Bee response-file check exits 0.
- Full runtime/compiler proof remains blocked by the existing project-wide dependency wall.

## Recheck Report: Signal Backpressure, Telemetry, And Drift
Status: PENDING VERIFICATION.

What was wrong:
- The current `SaveManager.cs` had drifted back to the old 8-entry WFC state-change cap while prior reports already described the full snapshot fix.
- The old cap could drop valid WFC mutable-state signals after index 7 in a 128-entry typed lane.
- Same-sector bursts packed and dirtied the 500-cell mutable grid once per signal instead of once per dirty sector group.
- `WfcBytesSaved` used packed-word bytes as the baseline and underreported real disk-byte savings versus the old 500-byte mutable grid.
- `Docs/Tasks/CURRENT_BATCH.md` has rotated and no longer contains `MACRO_WFC_PERSISTENCE_SYNC`; fresh prompt extraction is blocked by batch hygiene, not by parsing.

What was done:
- Reapplied `SaveManager.DrainWfcOutpostStateChangedSignals` full snapshot scanning.
- Added lazy DataVault mutable-grid resolve and contiguous dirty-sector batching.
- Removed the stale `MaxWfcOutpostStateSignalsPerTick` cap constant.
- Restored `WfcBytesSaved` telemetry to `CellCount - payloadBytes`, clamped at zero.
- Updated status and rationale logs to record both the batch-file rotation and the worktree drift.

Cinematic cheats used:
- Keep exact four-plane mutable bitmask truth instead of replaying outpost interaction history.
- Batch same-sector persistence writes instead of adding a managed per-sector cache.
- Use truthful disk-savings telemetry to justify richer restored-state presentation on high tiers without changing the save truth payload.

Exact microseconds saved:
- Measured savings: 0 us. No Unity profiler, Burst Inspector, GCMonitor, or runtime trace is available.
- Static estimate: a same-sector burst of 8 changes now performs 1 pack pass instead of 8, removing 7 redundant 500-cell pack scans.
- Static estimate: telemetry correction costs one integer subtraction and clamp on successful persist, below measurement noise.
- Static cost: worst-case alternating-sector bursts may still pack per sector group, bounded by signal lane capacity; correctness is preferred over silent state loss.

Verification:
- Static scan confirms the full `for (int i = 0; i < signals.Length; i++)` snapshot loop.
- Static scan confirms no `MaxWfcOutpostStateSignalsPerTick` reference remains.
- Static scan confirms `CellCount - payloadBytes` telemetry baseline and no old `PackedWordBytes - payloadBytes` baseline.
- Static scan confirms exact WFC payload length guard and direct `MutableGrid[cellIndex]` extraction read remain intact.
- `git diff --check` reports no whitespace errors for touched files.
- `Hecton8.Core.Contracts` Unity/Bee response-file compile exits 0.
- `Hecton8.Core` Unity/Bee response-file compile remains blocked by unrelated Audio Virtualization, AI Cognition/Fauna, Prologue, Outpost generation, WFC power boot, and World Ore missing symbols.
- Runtime save/load roundtrip, PlayMode, GCMonitor, Burst Inspector, and profiler proof remain blocked by the project compile wall.

## Recheck Report: Producer/Restore Closure And H-Phi Hygiene
Status: PENDING VERIFICATION.

What was wrong:
- SaveManager consumed `WfcOutpostStateChangedSignal`, but the gameplay closure needed concrete spawned producers and restore consumers for doors/datapads.
- A restored open door could be pushed back to locked state by a generic `Lock()` call.
- Datapad-like hybrid prefabs could publish duplicate same-cell `DatapadLooted` signals if both `MessageTerminal` and `AudioLogPickup` were configured.
- The static H-Phi audit did not complete; a numeric score would be fabricated evidence.

What was done:
- Preserved the typed WFC signal bridge on `SealedDoor`, `MessageTerminal`, and `AudioLogPickup`.
- Added a `SealedDoor.Lock()` open-state guard so restored/open truth is not overwritten by a lock call.
- Updated outpost datapad configuration to prefer `MessageTerminal` and return before falling back to `AudioLogPickup`.
- Recorded the batch-rotation limit and honored the user's no-`dotnet` rebuild instruction.

Cinematic cheats used:
- Four-plane bitmask truth replaces replaying full outpost interaction history.
- One fixed 32-byte typed signal represents a real mutable-state transition.
- Restored state is applied at component spawn/configuration time instead of running a simulation catch-up pass.

Exact microseconds saved:
- Measured savings: 0 us. No profiler, PlayMode, Burst Inspector, or GCMonitor proof is available.
- Static estimate: duplicate datapad producer avoidance removes one redundant 32-byte signal and one redundant same-cell dirty check on hybrid prefabs.
- Static estimate: door lock guard adds one branch only on lock calls and prevents a later corrective persistence write.
- Static H-Phi: audit timed out; no numeric H-Phi claim is made.

Verification:
- Static scans confirm `GlobalSignals.Publish(in signal)` producers exist in `SealedDoor`, `MessageTerminal`, and `AudioLogPickup`.
- Static scans confirm outpost spawn calls door/datapad WFC configuration and door power restore.
- Static scans confirm the datapad `return` after `MessageTerminal` configuration.
- `git diff --check` reports no whitespace errors for the touched code/log files, aside from Git CRLF normalization warnings.
- No `dotnet` rebuild was run after the user's explicit instruction.

## Recheck Report: Hot-Path Registry Decoupling
Status: PENDING VERIFICATION.

What was wrong:
- WFC Tick drains could still reach `GlobalRegistry.DataVault` or `GlobalRegistry.MacroDatabase` through lazy dependency refresh when cached services were missing.
- That kept a service-locator fallback in a hot persistence path and reduced H-Phi quality even though the signal lane itself was typed.

What was done:
- Added cached WFC dependency readiness state in `SaveManager`.
- Moved refresh to service initialization, public cold persistence calls, and `SlowTick`.
- Split public `TryPersistWfcOutpostStateSnapshot` from private `TryPersistWfcOutpostStateSnapshotInternal` so Tick batching persists through cached services only.
- Kept public restore/persist behavior robust for cold world-generation calls without changing `IAsyncPersistenceService`.

Cinematic cheats used:
- No simulation replay, no managed event bridge, no registry polling during WFC mutation drain.
- The same four-plane bitmask remains the only save truth.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static estimate: worst-case missing-cache Tick no longer performs `GlobalRegistry` dependency lookups; normal cached path is unchanged.
- Static cost: one SlowTick dependency-readiness branch group plus occasional refresh outside hot mutation drain.

Verification:
- Static scans confirm `GlobalRegistry.DataVault` and `GlobalRegistry.MacroDatabase` only appear in `RefreshWfcOutpostDependencies`.
- Static scans confirm `DrainWfcOutpostStateChangedSignals` calls `TryPersistWfcOutpostStateSnapshotInternal`, not the public refresh path.
- Static scans confirm public persist/restore still call `RefreshWfcOutpostDependencies` for cold callers.
- `git diff --check` reports no whitespace errors except Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Payload Checksum Hardening
Status: PENDING VERIFICATION.

What was wrong:
- WFC payloads had structural guards but no stored-byte checksum.
- A payload-byte bit flip could survive if the magic/version/dimensions/length fields stayed valid.

What was done:
- Packed a WFC-local checksum into the high 24 bits of the existing 32-bit flags field.
- Added a checksum flag in the low flag byte.
- Writer computes checksum after RLE/raw storage selection.
- Reader rejects checksum mismatches before RLE/raw decode.
- Legacy zero-checksum payloads remain readable; high checksum bits without the checksum flag reject.

Cinematic cheats used:
- No header expansion, no sidecar manifest, no full interaction-history replay.
- The four-plane bitmask remains compact; integrity rides inside the existing payload header word.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: checksum loops over <=256 stored payload bytes per WFC persist/restore.
- Static gain: corrupt WFC payloads fail before mutable-state grid injection, avoiding bad restored presentation and later corrective writes.

Verification:
- Static scans confirm `WfcOutpostPayloadFlagChecksum24`, checksum write, checksum read, and mismatch rejection paths.
- Static scans confirm `PayloadHeaderBytes` and `PayloadMaxBytes` constants are unchanged.
- `git diff --check` reports no whitespace errors except Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: Hydration Telemetry And Probe Fairness
Status: PENDING VERIFICATION.

What was wrong:
- Current source already warned on corrupt hydration payloads, so the compacted note was stale.
- The real hydration risk was first-four slot bias: `DrainWfcSectorHydratedSignals` ignored valid WFC-sized `SectorHydratedSignal` entries after index 3.

What was done:
- Kept the existing corrupt hydration warning path intact.
- Replaced the first-four signal slice with a full fixed-lane scan.
- Capped actual WFC-sized hydration probes at four to preserve bounded Tick cost.
- Kept DataVault WFC grid resolution lazy so frames without WFC-sized hydration candidates do no grid work.

Cinematic cheats used:
- No simulation replay and no world-generation catch-up.
- Hydration still injects the compact four-plane truth bitmask only when the database payload is valid.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static gain: removes a slot-order correctness loss that could force fresh outpost generation instead of restored visual truth.
- Static cost: up to 64 signal-struct inspections and at most four WFC restore probes per Tick.

Verification:
- Static scans confirm `MaxWfcSectorHydrationProbesPerTick`, full snapshot scanning, and no remaining `MaxWfcSectorHydratedSignalsPerTick`.
- Static scans confirm hydration decode failures call `PublishWfcCorruptPayloadWarning()`.
- `git diff --check` reports no whitespace errors except Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: Pooled Datapad Baseline Restore
Status: PENDING VERIFICATION.

What was wrong:
- `MessageTerminal` WFC restore marked all messages read for a looted datapad.
- A later pooled reuse with an unlooted WFC cell had no path to restore authored unread/baseline state.
- Null `MessageEntry` slots could still break persistence-adjacent message scans.

What was done:
- Added a cold `_initialReadStates` baseline for authored terminal read flags.
- Rebuilt `_readMessageIds` from current message state instead of duplicating read-set logic.
- Restored baseline message state when WFC config has no `DatapadLooted` bit.
- Added null-entry guards in WFC read-state loops, pending scan, playback start/completion, and editor duration refresh.

Cinematic cheats used:
- No per-cell datapad history replay.
- No managed runtime map; pooled proxy state resets from the authored local baseline plus the four-plane WFC save bit.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static gain: avoids cross-sector pooled state contamination and corrective persistence writes.
- Static cost: one cold `bool[messages.Length]` allocation per terminal instance; branch-only scans during cold configure/editor paths.

Verification:
- Static scans confirm `_initialReadStates`, baseline capture, baseline restore, and read-set rebuild helpers.
- Static scans confirm no direct unsafe `messages[i].isRead/messageId/audioClip` access remains.
- `git diff --check` reports no whitespace errors except Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: Pooled Datapad Transient Reset
Status: PENDING VERIFICATION.

What was wrong:
- `MessageTerminal.UpdateState()` intentionally avoids interrupting active playback.
- A pooled WFC datapad reused while still in `Playing` could keep stale playback/timer/blink state even after baseline read-state restore.

What was done:
- Added `ResetWfcOutpostTransientPlaybackState()`.
- WFC configuration now clears current message index, playback timer, blink timer/state, and converts `Playing` to `Idle` before applying restored flags.
- Kept the reset WFC-scoped, avoiding global `OnDisable` or generic terminal behavior changes.

Cinematic cheats used:
- No playback/history replay.
- Cold WFC configure resets local presentation state and then applies the one-bit looted truth.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: scalar writes only on WFC configure.
- Static gain: prevents stale pooled playback from forcing later corrective interaction/save behavior.

Verification:
- Static scans confirm the reset helper and call order before the looted/unlooted branch.
- `git diff --check` reports no whitespace errors except Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: AudioLog Datapad Active-State Restore
Status: PENDING VERIFICATION.

What was wrong:
- `AudioLogPickup` is used as the WFC datapad fallback producer.
- Looted WFC restore can deactivate a pooled pickup.
- A later unlooted WFC configuration needed a sector-local reset/reactivation path; otherwise global audio-log discovery could keep the pickup inactive or discovered.

What was done:
- Added WFC-scoped baseline restore for audio-log pickups.
- Invalid WFC config clears stale identity before any baseline reactivation.
- Valid unlooted WFC config resets `_alreadyDiscovered`, rebuilds cache, and can reactivate inactive pooled pickups.
- `OnEnable` now applies configured WFC state before consulting global `AudioLogs` discovery.

Cinematic cheats used:
- No collection-history replay.
- No managed per-cell pickup map.
- The physical outpost pickup follows the one-bit WFC looted truth and rebuilds only local presentation cache.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: cold scalar reset, cache rebuild, and possible Unity reactivation on pooled unlooted restore.
- Static gain: prevents stale pooled active/discovered state from forcing corrective interaction or save writes.

Verification:
- Static scans confirm `RestoreWfcOutpostDatapadBaselineState`, WFC-first `OnEnable`, and clear-before-invalid-reactivate ordering.
- Targeted `git diff --check` reports no whitespace errors.
- No `dotnet` rebuild was run.

## Recheck Report: MessageTerminal Invalid WFC Fail-Closed Reset
Status: PENDING VERIFICATION.

What was wrong:
- Valid WFC terminal config reset playback/read state.
- Invalid WFC config only cleared persistence identity.
- A pooled terminal could keep stale looted/read/playback state after an invalid hash or cell.

What was done:
- Invalid WFC config now resets transient playback first.
- It then clears WFC identity.
- It restores authored datapad baseline after identity is cleared, avoiding stale signal publication.

Cinematic cheats used:
- No interaction-history replay.
- No broad terminal lifecycle reset.
- The invalid WFC path snaps back to authored local baseline.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: cold scalar reset plus existing baseline scan only on invalid config.
- Static gain: prevents stale pooled terminal state from forcing corrective interaction or save writes.

Verification:
- Static scans confirm invalid branch order: reset, clear, restore.
- Targeted `git diff --check` reports only CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: SealedDoor Invalid WFC Fail-Closed Reset
Status: PENDING VERIFICATION.

What was wrong:
- Valid WFC door config can restore an opened/unlocked/powered door.
- Invalid WFC config only cleared persistence identity.
- A pooled door could keep stale opened or unlocked state after bad WFC binding.

What was done:
- Invalid WFC door config now clears WFC identity.
- It then calls `ResetState()` to restore authored baseline, collider state, and progress visuals.
- It avoids `ResetDoor()` so no fake mutation signal is published for an invalid cell.

Cinematic cheats used:
- No door-history replay.
- No persistence write for invalid cell state.
- The invalid path snaps the physical proxy back to authored local baseline.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: cold baseline reset plus optional animator rebind on invalid config only.
- Static gain: prevents stale pooled door state from forcing corrective power/door signals.

Verification:
- Static scans confirm invalid branch order: clear identity, reset baseline.
- Targeted `git diff --check` reports only CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Outpost Black Box
Status: PENDING VERIFICATION.

What was wrong:
- WFC outpost persistence had corrupt-payload telemetry but no agent-owned fixed 300-entry post-mortem dump.
- The generic async save dump did not contain WFC sector hash, payload hash, cell flags, or source signal context.
- Corrupt restore/hydration failures could explain "what failed" only at the warning level, not with the preceding WFC state trail.

What was done:
- Added `NativeArray<WfcOutpostTelemetryEntry>[300]` in `SaveManager`.
- Added one 64-byte WFC frame snapshot per Tick and richer records for WFC signal, persist, restore, hydration, and append-failure events.
- Added one-shot corrupt-payload dump to `Docs/AgentLogs/Dump_MACRO_WFC_PERSISTENCE_SYNC.bin`.
- Kept all public persistence contracts unchanged.

Cinematic cheats used:
- Binary sector/cell truth is logged directly instead of replaying door/datapad/power history.
- The ring stores packed hashes, mutable flags, and source hashes instead of verbose managed logs.
- Corruption writes one compact binary dump rather than spamming text output.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: one 64-byte NativeArray write per Tick plus fixed event writes on WFC activity.
- Static gain: post-mortem diagnosis avoids blind reruns and narrows corrupt-sector investigation to sector/payload/cell state.

Verification:
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs` reports no whitespace errors beyond Git CRLF normalization warning.
- Static scans confirm WFC ring allocation/disposal, per-Tick frame records, event records, `Dump_MACRO_WFC_PERSISTENCE_SYNC.bin`, and corrupt-payload dump hook.
- `Select-String` re-extraction found no `MACRO_WFC_PERSISTENCE_SYNC` tag in rotated `Docs/Tasks/CURRENT_BATCH.md`.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Black-Box Frame/Event Split
Status: PENDING VERIFICATION.

What was wrong:
- The WFC black-box ring mixed frame snapshots and event records.
- Dense WFC event bursts could evict frame snapshots before 300 frames elapsed.
- That weakened the literal "last 300 frames" forensic requirement.

What was done:
- Kept the existing 300-entry WFC ring as the frame ring.
- Added a separate 300-entry WFC event ring.
- Tick writes `FRAM` records only to the frame ring.
- Signal, persist, restore, hydration, and append records write to the event ring.
- Versioned the dump header as `WfcOutpostBlackBoxVersion = 2` and serialized both rings.

Cinematic cheats used:
- Frame truth and event breadcrumbs are stored as compact binary records, not gameplay-history replay.
- Corrupt-sector investigation gets hashes/flags/source IDs without managed log spam.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: +19.2 KB persistent native memory for the event ring.
- Static cost: unchanged one 64-byte frame-ring write per Tick; WFC event writes occur only on WFC activity.

Verification:
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs` reports no whitespace errors beyond Git CRLF normalization warning.
- Static scans confirm frame records and event records use separate rings and no old shared `RecordWfcOutpostBlackBox` call remains.
- `Select-String` re-extraction found no `MACRO_WFC_PERSISTENCE_SYNC` tag in rotated `Docs/Tasks/CURRENT_BATCH.md`.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Frame Dependency Bitfield
Status: PENDING VERIFICATION.

What was wrong:
- Frame black-box records carried sector and payload hashes but only a single snapshot-present flag.
- Dependency failures could look similar to clean missing-state frames in a binary dump.

What was done:
- Added `BuildWfcOutpostFrameBlackBoxFlags()`.
- Packed last-snapshot present, dependency-ready, WFC grid-created, MacroDB open, and DataVault cached bits into the existing frame `Flags` word.
- Kept `WfcOutpostTelemetryEntry` at 64 bytes.

Cinematic cheats used:
- Encoded dependency truth as a bitfield instead of adding verbose logs or replaying service-locator history.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static cost: five scalar flag checks and one uint write per Tick.
- Static gain: faster post-mortem classification of WFC dependency failures without widening records.

Verification:
- Static scans confirm `BuildWfcOutpostFrameBlackBoxFlags()` feeds the Tick frame record.
- Static scans confirm the WFC telemetry entry remains 64 bytes.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Dirty Append Failure Dump
Status: PENDING VERIFICATION.

What was wrong:
- WFC dirty append failure recorded a binary event and performance warning, but did not dump the WFC black-box rings.
- A failed append can lose player mutation truth while leaving only a shallow warning trail.

What was done:
- Added `PublishWfcWriteFailureWarning(frame)` in `SaveManager`.
- `FlushWfcOutpostDirtyPayloadAsync` now records the append rejection event first, then emits the warning and one-shot WFC black-box dump.
- Public persistence contracts and payload format stayed unchanged.

Cinematic cheats used:
- Append failure diagnosis uses compact sector/frame binary state instead of replaying interaction history or adding managed log spam.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Hot path cost: unchanged.
- Failure path cost: one existing warning plus one one-shot binary dump after an append rejection.

Verification:
- Static scans confirm append failure records the binary event before `PublishWfcWriteFailureWarning(frame)`, and the helper invokes `DumpWfcOutpostBlackBox()`.
- `git diff --check` reports no whitespace errors beyond Git CRLF normalization warnings.
- `Select-String` still finds no `MACRO_WFC_PERSISTENCE_SYNC` tag in the rotated current batch.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Validated Write Failure Dump Broadening
Status: PENDING VERIFICATION.

What was wrong:
- Codec write failure and MacroDB `MarkDirty` failure were validated WFC write-loss paths.
- They recorded rejection events but did not dump the WFC black-box rings.

What was done:
- Generalized the helper to `PublishWfcWriteFailureWarning(frame)`.
- Persist rejection after payload encode failure now records the event, then dumps.
- Persist rejection after `MarkDirty` failure now records the event, then dumps.
- Invalid input and service-unavailable exits remain event-only to avoid noisy dependency/startup dumps.

Cinematic cheats used:
- Failure diagnosis uses compact binary sector/hash/frame state instead of replaying interactions or emitting managed text logs.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Hot success path cost: unchanged.
- Failure path cost: existing warning plus one one-shot binary dump.

Verification:
- Static scans confirm codec write failure, `MarkDirty` failure, and append failure all record their WFC black-box event before `PublishWfcWriteFailureWarning(frame)`.
- Static scans confirm no stale append-specific helper name remains.
- `git diff --check` reports no whitespace errors beyond Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Black-Box Entry Size Correction
Status: PENDING VERIFICATION.

What was wrong:
- `WfcOutpostTelemetryEntry` was annotated as 64 bytes but statically totaled 68 bytes.
- The extra unused reserved `uint` invalidated the stated cache-line and dump-size contract.

What was done:
- Removed the unused `Reserved1` field.
- Removed the extra `Reserved1` binary writer output.
- Bumped `WfcOutpostBlackBoxVersion` from 2 to 3.

Cinematic cheats used:
- Kept compact binary forensic truth instead of verbose managed logs or replay history.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Static memory correction: each WFC ring returns to the intended 300 x 64-byte footprint.
- Hot path cost: unchanged one native frame-entry write per Tick.

Verification:
- Static scans confirm WFC version 3.
- Static scans confirm the WFC telemetry struct contains no `Reserved1`.
- Static scans confirm the WFC writer emits 13 fields and ends at `Reserved0`.
- `git diff --check` reports no whitespace errors beyond Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Black-Box Dump Latch Ordering
Status: PENDING VERIFICATION.

What was wrong:
- The WFC dump latch was set before directory creation and file serialization.
- A failed dump attempt could permanently suppress later WFC forensic dumps.

What was done:
- Moved `_wfcOutpostBlackBoxDumped = true` to after successful ring serialization.
- Kept the one-shot behavior after a successful dump.

Cinematic cheats used:
- No simulation or replay added. The fix preserves compact binary forensic export.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Hot path cost: unchanged.
- Failure path cost: unchanged except the latch now reflects successful write state.

Verification:
- Static scans confirm the early dump guard remains active.
- Static scans confirm `_wfcOutpostBlackBoxDumped = true` runs only after frame and event ring serialization completes.
- `git diff --check` reports no whitespace errors.
- `Select-String` still finds no `MACRO_WFC_PERSISTENCE_SYNC` tag in the rotated current batch.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Hydration Magic Prefilter
Status: PENDING VERIFICATION.

What was wrong:
- Hydration WFC restore candidates were filtered by byte length only.
- Any unrelated MacroDB payload sized 32-288 bytes could be treated as corrupt WFC and trigger the WFC black-box dump.

What was done:
- Added `SaveBinaryPayloadCodec.HasWfcOutpostBitmaskMagic()`.
- Hydration now checks the WFC magic before full decode.
- Non-WFC small payloads become missing-WFC hydration events, not corrupt WFC dumps.
- Pointer-null handles and WFC-magic malformed payloads still use the corrupt/dump path.

Cinematic cheats used:
- One fixed magic read replaces payload-type contract churn and avoids replay/simulation diagnostics.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Hydration candidate cost: four byte reads before decode.
- False-positive avoidance: skips one roughly 38.4 KB WFC dump plus header for unrelated small payload bursts.

Verification:
- Static scans confirm hydration checks magic before WFC decode.
- Static scans confirm public WFC restore still decodes directly and dumps corrupt WFC.
- `git diff --check` reports no whitespace errors beyond Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Valid Door Restore Baseline Reset
Status: PENDING VERIFICATION.

What was wrong:
- A stale-door restore risk was audited: valid WFC door configuration must not apply saved flags onto current pooled state.
- Without a baseline reset, a pooled door that was already opened could remain opened when restored with saved flags `0`.

What was done:
- Verified current `SealedDoor.ConfigureWfcOutpostPersistence()` already calls `ResetState()` before applying saved WFC flags.
- Verified current `ResetState()` stops cutting and open particle systems.
- Verified invalid WFC config still clears identity before reset and publishes no fake mutation.
- No `SealedDoor.cs` source delta was required in this pass.

Cinematic cheats used:
- Restored binary door truth drives presentation state directly; no replay of cutting, power, or opening history.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime Tick cost: unchanged.
- Cold configure path: one baseline reset plus optional particle stops.

Verification:
- Static scans confirm valid config resets before WFC identity assignment and flag application.
- Static scans confirm invalid config remains clear-then-reset.
- `git diff` shows no source delta for `SealedDoor.cs`.
- Targeted `git diff --check` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Producer Attribution Closure
Status: PENDING VERIFICATION.

What was wrong:
- Current worktree drift had one WFC door-power producer bypassing `GlobalSignals`.
- Terminal/audio-log datapad mutations reused the `WFCP` persistence source hash.

What was done:
- Restored door-power publication through `GlobalSignals.Publish(in signal)`.
- Split datapad source hashes to `WFCT` for `MessageTerminal` and `WFCA` for `AudioLogPickup`.

Cinematic cheats used:
- Metadata-only attribution fix; no simulation, storage format, or payload growth.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Cost: unchanged typed native enqueue path plus constant source metadata.

Verification:
- Static scan shows remaining direct WFC typed `SignalBus<T>.Push` calls are only inside `GlobalSignals`.
- Datapad source hashes no longer collide with `SaveManager` persistence `WFCP`.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Hydration Magic Unaligned Read Guard
Status: PENDING VERIFICATION.

What was wrong:
- The hydration magic prefilter used `ReadUInt(source, 0)`.
- That raw `uint*` load was unnecessary before payload type certainty.

What was done:
- Replaced the prefilter read with four guarded byte comparisons.
- Left full WFC payload decode and checksum validation unchanged.

Cinematic cheats used:
- Fixed magic-byte prefilter avoids payload-type contract churn and avoids replay diagnostics.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Hydration candidate cost: four byte reads instead of one raw 32-bit load.
- Runtime allocation: 0 B by static inspection.

Verification:
- Static scans confirm `HasWfcOutpostBitmaskMagic()` no longer calls `ReadUInt`.
- Static scans confirm full WFC decode still performs complete header validation.
- Targeted `git diff --check` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Payload Header Alignment Hardening
Status: PENDING VERIFICATION.

What was wrong:
- Full WFC payload header helpers still used raw scalar pointer casts.
- That made the fixed binary header depend on pointer alignment and host scalar layout.

What was done:
- Replaced WFC header scalar helpers with explicit little-endian byte stores/loads.
- Kept the 32-byte WFC header, checksum path, and payload decoder validation unchanged.

Cinematic cheats used:
- Fixed 32-byte byte-addressed header instead of scratch structs or payload contract churn.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Cost shift: fixed header byte ops replace six raw scalar stores/loads on WFC persist/restore.

Verification:
- Static scans find no raw pointer-cast scalar helpers left in the WFC payload codec path.
- Targeted `git diff --check` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Dependency Flag Reacquire Truth
Status: PENDING VERIFICATION.

What was wrong:
- A direct WFC DataVault grid reacquire could leave `_wfcOutpostDependenciesReady` stale.
- Black-box frame flags could then under-report actual cached readiness until SlowTick refreshed.

What was done:
- Successful `TryEnsureWfcOutpostGrid()` now recomputes readiness from cached MacroDB/DataVault state.
- Tick drains still avoid `GlobalRegistry` dependency refresh.

Cinematic cheats used:
- Cached-state truth bit instead of hot registry probing.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Cost: one cached MacroDB-open check plus one DataVault null check on cold grid reacquire.

Verification:
- Static scans confirm no `RefreshWfcOutpostDependencies()` call was added to Tick drains.
- `Select-String` still finds no rotated batch XML tag for this ID.
- Targeted `git diff --check` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Signal Facade Ownership
Status: PENDING VERIFICATION.

What was wrong:
- WFC producers pushed typed packets directly into `SignalBus<T>`.
- That scattered WFC lane ownership across gameplay, world, and power producers instead of using the global facade.

What was done:
- Routed WFC state-change, generated-grid, and door-power packets through `GlobalSignals.Publish(in signal)`.
- Left signal payload structs, lane capacities, and consumers unchanged.

Cinematic cheats used:
- Facade routing only; no new service, replay layer, or managed event bridge.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Cost: same typed native enqueue plus existing initialized guard.

Verification:
- Static scan shows remaining direct WFC typed `SignalBus<T>.Push` calls are only inside `GlobalSignals` facade overloads.
- `Select-String` still finds no rotated batch XML tag for this ID.
- Targeted `git diff --check` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Interleaved Sector Signal Drain
Status: PENDING VERIFICATION.

What was wrong:
- WFC state-change draining flushed on sector transitions, so A/B/A signal order could persist the same sector twice in one frame.
- That made correctness depend on immediate visibility of a just-dirtied MacroDB payload.

What was done:
- Signal black-box events still record in original snapshot order.
- Dirty sectors are collected in stack-only scratch and each sector is hydrated, patched, and persisted once.

Cinematic cheats used:
- Bounded snapshot batching instead of a managed per-sector map or new signal layer.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Normal path: <=256 stack sector entries; common case is one sector and two snapshot scans.
- Storm snapshots use the later no-allocation first-occurrence fallback.

Verification:
- Static scan confirms old contiguous flush state is gone and stack-only sector accumulation is present.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Hydration Sector Probe Deduplication
Status: PENDING VERIFICATION.

What was wrong:
- Duplicate WFC-sized hydration signals for one sector could consume the four-probe hydration cap.
- That repeated same-sector restore work and could delay unrelated WFC sector restores.

What was done:
- Hydration drain now collects unique sector hashes in stack-only scratch.
- Each unique sector is restored once, capped by `MaxWfcSectorHydrationProbesPerTick`.

Cinematic cheats used:
- Sector-level probe fairness instead of higher caps, managed maps, or new signal contracts.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Worst case remains bounded by <=64 hydration signals and <=4 restore attempts.

Verification:
- Static scan confirms old `probes++` duplicate-sector flow is gone.
- Static scan confirms `hydrationSectors` uses stack-only scratch.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Signal Storm Stack Guard
Status: PENDING VERIFICATION.

What was wrong:
- The prior unique-sector drain used `stackalloc ulong[signals.Length]`.
- `SignalBus<T>.Configure(128)` is expected queue capacity, not a hard snapshot cap, so storm snapshots can exceed the intended stack bound.

What was done:
- Added `MaxWfcDirtySectorStackEntries = 256` for the fast stack path.
- Added an exact no-allocation storm fallback that records events in snapshot order and persists each unique sector by first occurrence.

Cinematic cheats used:
- Bounded stack batching plus first-occurrence storm scan instead of managed maps or signal bus contract churn.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Common path stays stack-only; storm path trades extra scalar scans for bounded stack and no mutation loss.

Verification:
- Static scan confirms the storm fallback and shared event helper exist.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Per-Sector Snapshot Cache
Status: PENDING VERIFICATION.

What was wrong:
- WFC unchanged-payload suppression remembered only the last sector/hash.
- Interleaved A/B/A sector traffic could requeue an identical A payload after B replaced the one-slot cache.
- Dependency churn could leave cached sector identity stale unless explicitly reset.

What was done:
- Added a 256-entry native per-sector payload-hash cache in `SaveManager`.
- Registered/disposed the cache through `NativeMemorySentinel`.
- Reused the cache from persist, restore, and hydration paths.
- Reset snapshot and mutable-grid sector caches when MacroDB/DataVault dependencies change or are unavailable.

Cinematic cheats used:
- Fixed native hash cache instead of managed maps, DB contract changes, or replaying WFC interaction history.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Memory cost: 4096 bytes persistent native cache.
- Avoided work: duplicate 32-288 byte MacroDB dirty copies and append attempts for unchanged interleaved sectors.

Verification:
- Static scan confirms cache allocation/disposal and reset wiring.
- Static scan confirms `_hasLastWfcOutpostSnapshot = true` now flows through `RememberWfcOutpostSnapshotHash`.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs Docs/Tasks/Status_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/LOG_MACRO_WFC_PERSISTENCE_SYNC.md` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Hydration Magic Fairness And Reset Safety
Status: PENDING VERIFICATION.

What was wrong:
- Hydration collection counted any 32-288 byte MacroDB payload toward the four WFC restore probes.
- Known non-WFC small payloads could starve later real WFC hydrations in the same snapshot.
- Cache reset could clear a cached DataVault-owned `NativeArray` after the DataVault reference changed.

What was done:
- Added `IsWfcOutpostHydrationCandidate()` to prefilter known non-WFC payloads by WFC magic bytes before cap consumption.
- Preserved existing telemetry/dump behavior for missing, null, or structurally corrupt candidates by letting them pass to the restore path.
- Changed sector-cache reset to clear the mutable grid only when the DataVault owner is known to be unchanged.

Cinematic cheats used:
- Four-byte magic prefilter instead of signal contract churn or higher hydration caps.
- Ownership-aware cache reset instead of reconstructing persistence state globally.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Added work: one cached MacroDB lookup and four byte reads per WFC-sized hydration candidate when MacroDB is open.
- Avoided work: false WFC restore attempts and black-box noise for known non-WFC small payloads.

Verification:
- Static scan confirms pre-cap `IsWfcOutpostHydrationCandidate()` filtering.
- Static scan confirms `ResetWfcOutpostSectorCaches(bool clearMutableGrid)` call-site wiring.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs Docs/Tasks/Status_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/LOG_MACRO_WFC_PERSISTENCE_SYNC.md` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Dirty Append Retry
Status: PENDING VERIFICATION.

What was wrong:
- A successful `MarkDirty` followed by a failed background append could leave a WFC dirty payload dependent on later unrelated MacroDB eviction/compaction.
- The sector hash cache could then skip identical future payloads as unchanged without queuing another append.

What was done:
- Added append pending/in-flight bits to the fixed native WFC sector cache.
- MarkDirty success records pending; queueing records in-flight; append success clears; append failure keeps pending.
- `SlowTick()` retries up to two pending non-in-flight WFC dirty appends through the existing background append path.
- Append callbacks are payload-hash guarded so stale async completions do not mutate a newer sector cache entry.

Cinematic cheats used:
- Tiny native retry flags inside the existing sector cache instead of managed retry queues or MacroDB contract expansion.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Memory cost: +2048 bytes persistent native cache versus Loop 37.
- SlowTick work: scan <=256 cache entries, queue <=2 background append attempts.

Verification:
- Static scan confirms retry, in-flight, completed, and failed append flag transitions.
- Static scan confirms no same-frame retry and no Tick retry path.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs Docs/Tasks/Status_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/LOG_MACRO_WFC_PERSISTENCE_SYNC.md` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: MacroDB Dirty Cache Clean Handoff
Status: PENDING VERIFICATION.

What was wrong:
- MacroDB direct append can durably write a dirty payload but the DataVault cache handle must then stop carrying `PayloadDirtyFlag`.
- Direct append cleanup was present, but compaction dirty flush had been clearing cache metadata while using target temp-file offsets before `File.Replace`.
- A failed or delayed swap could therefore leave cache metadata inconsistent with the active database boundary.

What was done:
- Kept direct append cache cleanup after active-file `UpsertPayloadOffset`.
- Removed source-cache mutation from `FlushDirtyPayloadsIntoTargetLocked`.
- Added post-swap `MarkDirtyPayloadCacheCleanAfterSwapLocked()` after successful replace/reopen; it resolves offsets from the active B-tree, clears dirty flags, or removes stale cache entries if the active payload cannot be proven.

Cinematic cheats used:
- Cache metadata follows active-file ownership instead of carrying a separate compaction offset side channel.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Hot Tick cost: unchanged.
- Cold compaction cost: one active B-tree offset lookup per dirty payload during swap.

Verification:
- Static scan confirms `MarkPayloadCleanInCacheLocked` is called only from active append and post-swap handoff.
- Static scan confirms compaction target dirty flush no longer clears source cache with temp offsets.
- `git diff --check -- Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs Assets/_Project/Scripts/SaveManager.cs Docs/Tasks/Status_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md Docs/AgentLogs/LOG_MACRO_WFC_PERSISTENCE_SYNC.md` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Producer Facade Drift Recheck
Status: PENDING VERIFICATION.

What was wrong:
- `MarauderOutpostGenerationService` directly pushed `WfcOutpostGeneratedSignal`.
- `WfcOutpostPowerBootRuntime` directly pushed `WfcOutpostDoorPowerSignal`.
- WFC producers must publish through `GlobalSignals` so lane initialization and ownership stay centralized.

What was done:
- Replaced both producer direct pushes with `GlobalSignals.Publish(in signal)`.
- Left snapshot reads unchanged; consumers can still read the fixed frame lanes.

Cinematic cheats used:
- Existing signal facade reused instead of adding new bridge objects, metadata fields, or managed event wrappers.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Payload size: unchanged.

Verification:
- Static scan confirms remaining direct WFC typed `SignalBus<T>.Push` calls are only inside `GlobalSignals`.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Append Stale-Service Callback Guard
Status: PENDING VERIFICATION.

What was wrong:
- WFC dirty append captured `_macroDatabaseService`, ran append work off-thread, then returned to main thread without proving the captured service was still current/open.
- A stale callback could clear retry flags for a newer service boundary or produce a false write-failure dump during service shutdown/churn.

What was done:
- Captured the MacroDB service once before the background hop.
- Required `macroDatabase.IsOpen` before background append.
- On main thread, verified `ReferenceEquals(_macroDatabaseService, macroDatabase)` and `macroDatabase.IsOpen` before success/failure handling.
- Stale callbacks now record `ServiceUnavailable`, keep matching retry state pending, and skip the write-failure dump path.

Cinematic cheats used:
- Owner-bound async callback validation instead of registry refresh, service polling, or expanding MacroDB contracts.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Hot Tick cost: unchanged.
- Completion cost: one reference equality check and one `IsOpen` check.

Verification:
- Static scan confirms the stale-service guard in `FlushWfcOutpostDirtyPayloadAsync`.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs` reports only Git CRLF normalization warnings.
- No `dotnet` rebuild was run.

## Recheck Report: MacroDB Compaction Deferred Append Truth
Status: PENDING VERIFICATION.

What was wrong:
- `TryAppendDirtyPayload` returned true while compaction write-lock was active if the dirty payload handle was valid.
- WFC interpreted that true as durable append completion and cleared native retry flags.
- If compaction later faulted or stayed deferred, WFC no longer owned an active retry for the sector.

What was done:
- `H8MacroDatabaseService.TryAppendDirtyPayload` now returns false for dirty payloads blocked by compaction.
- `SaveManager` detects active MacroDB compaction around a false append result and records `DirtyQueued` instead of write failure.
- The matching WFC cache entry remains pending and can retry after compaction clears.

Cinematic cheats used:
- Reused the existing compaction snapshot and native retry flags instead of adding a new append-status contract.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Hot Tick cost: unchanged.
- Append-completion cost: one compaction-state read only on false append.

Verification:
- Static scan confirms MacroDB returns false for compaction-deferred dirty append.
- Static scan confirms SaveManager records `DirtyQueued` and skips write-failure dump on compaction-deferred append.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary` exited 0; core graph debt counts unchanged.
- No `dotnet` rebuild was run.

## Recheck Report: WFC Append Retry Cache Saturation
Status: PENDING VERIFICATION.

What was wrong:
- The fixed WFC snapshot cache used round-robin replacement even when the victim entry still had append pending or in-flight flags.
- A full-cache sector burst could delete the only SaveManager-owned retry record for an unresolved dirty payload.
- SlowTick retry also kept launching append attempts while MacroDB compaction was already write-locked.

What was done:
- Added `WfcOutpostSnapshotCacheFlagAppendAny`.
- Full-cache replacement now chooses only clean cache entries and skips replacement if every entry is unresolved.
- Append callback flag updates reinsert pending state when the callback finds its cache entry missing and a clean slot exists.
- SlowTick retry returns early while MacroDB compaction is in a write-locked state.

Cinematic cheats used:
- Fixed-size native cache policy instead of managed overflow queues, contract expansion, or extra bridge services.

Exact microseconds saved:
- Measured savings: 0 us. No profiler or runtime trace.
- Runtime allocation: 0 B by static inspection.
- Hot Tick cost: unchanged.
- Full-cache insert cost: <=256 native-entry scan.
- Compaction window savings: up to two avoided background append submissions per SlowTick.

Verification:
- Static scan confirms `WfcOutpostSnapshotCacheFlagAppendAny`, clean-slot replacement, callback pending reinsert, and compaction-gated retry.
- Static scan confirms remaining direct WFC typed `SignalBus<T>.Push` calls are only inside `GlobalSignals`.
- `git diff --check -- Assets/_Project/Scripts/SaveManager.cs` reports only Git CRLF normalization warnings.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary` exited 0 at 2026-05-15 17:04:19 +04:00; core graph debt counts unchanged.
- No `dotnet` rebuild was run.
