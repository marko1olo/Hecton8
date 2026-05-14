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
