# Status_DATA_ARCHAEOLOGY

Status: PENDING VERIFICATION
Agent: DATA_ARCHAEOLOGY
Role: UX_ENGINEER
Domain: ECHELON 8 PRESENTATION & UX (Interaction and Perception)
Task Count: 15
Target Hardware: i3 / MX350

## Mandates Loaded

- UI_Data_Streaming_ZeroGC_Optimization.txt
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- CORE_Tools_Equipment_Interaction_Raycast_Heat.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Prompt Extraction Log

- [x] Extracted `<AGENT_PROMPT id="DATA_ARCHAEOLOGY">` from `Docs/Tasks/CURRENT_BATCH.md` via PowerShell regex. DOD: cover-to-cover XML extraction. Rejected: MCP-only read because batch files can truncate. Estimate: 25 us cold CLI scan.
- [x] Re-extracted `<AGENT_PROMPT id="DATA_ARCHAEOLOGY" role="UX_ENGINEER">` with an attribute-aware PowerShell regex after the strict id-only regex failed on XML attributes. DOD: exact DATA_ARCHAEOLOGY block isolated; primary objective count confirmed as 15. Rejected: relying on previous chat/task memory. Estimate: 900 us cold CLI scan.
- [x] Loaded domain definition from `Docs/Actual Domains of Project.txt`. DOD: boundary confirmed before edits. Rejected: editing gameplay ownership without cross-domain justification. Estimate: 20 us cold read.
- [x] Initialized status/rationale tracking. DOD: disk-backed state for anti-amnesia. Rejected: chat-only task memory. Estimate: 50 us cold file creation.

## Tasks

1. [x] Native scan targets: Maintain `NativeParallelHashMap<int, byte>` for AUP/entity hash scan state. Justification: `DataArchaeologyRuntime` owns a persistent native map keyed by `unchecked((int)hash)` and stores Unscanned/Scanning/Scanned bytes. DOD: zero-GC O(1) state lookup/update. Alternative rejected: managed `Dictionary`/`List` state, because scanner hold path is hot. Estimate: 2-5 us per lookup/update on MX350-class CPU.
2. [x] Raycast batch scanner: Scanner Tool fires `RaycastCommand` and reads `ScannableTarget.EntityHash`. Justification: `ScannerTool` queues through dispatcher-owned `RaycastCommand` lane and consumes `ScannableTarget.EntityHash` without new scheduler allocation. DOD: batched physics lane, no `Physics.Raycast` sync path. Alternative rejected: per-tool `NativeArray<RaycastCommand>` ownership, because `SystemDispatcher` already owns the global batch lane. Estimate: 5-12 us submit overhead; physics work deferred to batch.
3. [x] Progress accumulator: held trigger increments local progress and flips state to scanned. Justification: `ScannerTool` keeps `_activeScientificEntityProgress` as local float seconds and `DataArchaeologyRuntime.UpdateRaycastTargetProgress` flips state after `> 1f`. DOD: scalar local progress, no list write, no string. Alternative rejected: storing per-frame progress in UI objects. Estimate: <1 us for scalar accumulation; 2-5 us with map write.
4. [x] Unlock signal: emit `BlueprintUnlockedSignal(EntityHash)` through `GlobalSignals`. Justification: completion emits `BlueprintUnlockedSignal`, `ScanCompleteSignal`, and hash-only HUD signal. DOD: unmanaged 32-byte signal payload, NativeQueue lane. Alternative rejected: direct Crafting concrete reference. Estimate: 1-3 us enqueue.
5. [x] Zero-GC decryption UI: PDA name display scrambles TMP char buffer via `Span<char>`. Justification: `PDADataArchaeologyDecryptLabel` resolves hash text into a `CharBufferPool` lease, scrambles unrevealed chars through `Span<char>`, and calls `TMP_Text.SetCharArray`. DOD: no runtime `new char[]`, no TMP `.text`. Alternative rejected: prefab instantiation/string assignment. Estimate: 10-35 us per updated label depending text length.
6. [x] Hash-to-text: `LocRegistry.GetLength(EntityHash)` and preallocated char buffer. Justification: `LocRegistry.GetLength(int/uint)` returns raw char length and PDA label uses `CharBufferPool` lease of that length cap. DOD: no string materialization. Alternative rejected: `Resolve(...).ToString()` or private `new char[]`. Estimate: 2-6 us lookup plus bounded char copy.
7. [x] Databank SOA: unlocked lore stored in `NativeArray<ulong>` bitmask. Justification: `DataArchaeologyRuntime` owns `_unlockedLoreWords` and mirrors it to the save-compatible managed bit words only during save/load. DOD: native SOA authority during runtime. Alternative rejected: managed `long[]` as only runtime authority. Estimate: <1 us bit set.
8. [x] AUP origin shift: scanner target validation respects AUP shift. Justification: runtime implements `IOriginShiftListener`, registers with `HectonFloatingOrigin`, and rebases cached scan positions/hologram matrices by shift delta. DOD: no drain of shared `AupShiftSignal` queue, no stolen broadcasts. Alternative rejected: polling/dequeueing `GlobalSignals.TryDequeueAupShift` from scanner. Estimate: O(n discovered scan positions) only on shift, 0 us normal frames.
9. [x] Emissive scan mask: publish hit AUP to `_HectonScannerPoints`. Justification: scan progress writes preallocated `Vector4[4]` to global shader array when changed and not Low/MX350 tier. DOD: no per-update array allocation, dirty threshold, tier gate. Alternative rejected: material property blocks or per-renderer material mutation. Estimate: 10-40 us when dirty on non-low tiers; 0 us on Low.
10. [x] Audio sync: emit `ToolAcousticSignal(Scanning)` with pitch tied to progress. Justification: scan progress publishes unmanaged `ToolAcousticSignal` with pitch/intensity derived from progress/match. DOD: NativeQueue signal, no direct audio manager call. Alternative rejected: direct `AudioSource` or concrete audio owner call. Estimate: 1-3 us enqueue.
11. [x] HUD notification: push `HUDNotificationSignal` using FNV-1a hash. Justification: completion publishes hash-only `HUDNotificationSignal` through `GlobalSignals`, using the entity/discovery FNV hash as the message key. DOD: unmanaged payload, no localized string generation in scanner completion. Alternative rejected: direct HUD text call. Estimate: 1-3 us enqueue.
12. [x] Math LOD: Low Tier disables UI scrambling and scanner shader mask. Justification: `PDADataArchaeologyDecryptLabel.ShouldScramble` disables scrambling on Low/Unknown/MX350, and `DataArchaeologyRuntime.PublishScannerShaderPoint` returns before shader upload on the same tier gate. DOD: toaster path skips char mutation and shader global upload. Alternative rejected: always-on effect with lower frequency. Estimate: 0 us for disabled presentation work on Low.
13. [x] MMF save pipeline: scanner states blitted into binary save payload or documented existing equivalent. Justification: `SaveData` v66 carries scan state key/value arrays; `SaveBinaryPayloadCodec` writes/reads them; `DataArchaeologyRuntime.PopulateScanStateSaveData` blits `NativeParallelHashMap` key/value pairs on cold save. DOD: native state survives payload round trip without hot-frame managed collections. Alternative rejected: relying only on lore bitmask, because Scanning partial states would be lost. Estimate: cold save O(n), 0 us normal frames.
14. [x] Recon: scan `ScannerTool.cs` for `Instantiate` or `string`; log to `RECON_DATA_ARCHAEOLOGY.md`. Justification: recon file records 0 `Instantiate`, 256 `string` matches, and 0 hot-slice matches in `ToolTick` -> `UpdateScientificScanning`. DOD: evidence logged to disk. Alternative rejected: chat-only recon. Estimate: 180-300 us cold CLI scan.
15. [x] Omega compile check: verify Scanner Tool hot update/tick path does not generate strings. Justification: hot slice grep from `UpdateScientificScanning` to `TryResolveScientificSpatialContact` returned 0 matches for string/format/instantiate patterns; new PDA/runtime zero-GC grep also returned 0 matches. DOD: textual allocation audit plus final dotnet compile rerun clean. Alternative rejected: trusting review without grep. Estimate: 0 us runtime change.

## Iteration Loop Ledger

- Loop 0 Bootstrap: prompt/domain/mandates loaded; tracking files created.
- Loop 1 Tasks 1-5: COMPLETE; dotnet compile green with unrelated existing CS0649 warnings.
- Loop 2 Tasks 6-10: COMPLETE; dotnet compile green with 0 warnings/0 errors.
- Loop 3 Tasks 11-15: COMPLETE; recon logged and save payload bridge added.
- Loop 4 Self-review: COMPLETE; zero-progress partial write guarded and hot allocation scans rerun.
- Loop 5 Polish mandate: COMPLETE; OMEGA audit recorded in rationale/log, code compile clean, scoped diff check clean.
- Loop 6 Continuation audit: COMPLETE; restored RTL visual buffer behavior, disabled PDA label raycasts, added stable completion/unbind unregister path, blocked already-scanned raycast target reacquisition, and added save migration guards for v66 data archaeology arrays.
- Loop 7 MMF/lifecycle/build audit: COMPLETE; bounded partial MMF flushes to a 4s cadence, kept completion/removal flush urgent at 0.25s, made disable/dispose share runtime unregister, skipped MMF writes when clean, and applied a minimal outside-domain compile unblock for missing `HectonPlayerMovement` drag-job backing fields.
- Loop 8 MMF authority audit: COMPLETE; MMF cold-load now restores completed lore bits, restores completed/partial scan states into the native map, deduplicates partial records, enumerates scan-state saves without `GetKeyValueArrays(Allocator.Temp)`, and dirties the MMF sidecar after AUP rebases of persisted fragment mirrors.
- Loop 9 UI retry / stale partial cleanup audit: COMPLETE; PDA label render failures keep the label dirty and registered for retry, PDA render copy length is clamped by localization buffer capacity and pooled slot capacity, explicit non-Scanning state removes stale partial progress after load, Unscanned removes native map keys instead of persisting zero-state entries, MMF partial load skips Scanned hashes, and cold partial loaders now deduplicate save/MMF records through one bounded helper without dirtying the sidecar.

## Verification

- Compile after Tasks 1-5: PASS via `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`; 0 errors, 3 existing CS0649 warnings outside DATA_ARCHAEOLOGY changes.
- Compile after Tasks 6-10: PASS via `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`; 0 warnings, 0 errors.
- Compile after Tasks 11-15: PASS via `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`; 0 errors, 1 existing CS0649 warning in `WorldSpatialHashGrid.cs`.
- Final compile: PASS via `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`; 0 warnings, 0 errors.
- Continuation compile: PASS via `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`; 0 warnings, 0 errors.
- Continuation hot allocation audit: PASS. `UpdateScientificScanning` -> `TryResolveScientificSpatialContact` slice returned `HOT_SLICE_MATCHES=0` for `Instantiate`, `string`, `new string`, `string.Create`, `.ToString(`, and `Format(`.
- Continuation PDA/runtime allocation audit: PASS. `PDADataArchaeologyDecryptLabel.cs` and `DataArchaeologyRuntime.cs` returned `PDA_RUNTIME_ALLOC_TEXT_MATCHES=0` for `new char[]`, `new string`, TMP `.text`, `SetText`, `string.Create`, `.ToString(`, and `Format(`.
- Scoped diff hygiene: PASS for DATA_ARCHAEOLOGY-touched files via `git diff --check -- <touched paths>`; repository-wide `git diff --check` remains blocked by unrelated pre-existing trailing whitespace in AGENTS/docs/meta files.
- Unity console / profiler / GC proof: PENDING VERIFICATION; measured proof absent. Unity refresh timed out after 60 seconds and subsequent console read failed with `no_unity_session`; the last available Unity console entry before timeout was an outside-domain compile error in `Assets/_Project/Scripts/SubmarineStructuralGrid.cs(53,117)` for missing `ILateFrameTickable.LateFrameTick()`.
- Loop 7 hot allocation audit: PASS. Scanner hot slice remains `HOT_SLICE_MATCHES=0`; PDA/runtime allocation-text scan remains `PDA_RUNTIME_ALLOC_TEXT_MATCHES=0`.
- Loop 7 scoped diff hygiene: PASS for `DataArchaeologyRuntime.cs` and the outside-domain `HectonPlayerMovement.cs` compile unblock; only LF-to-CRLF warnings reported.
- Loop 7 compile: PASS via `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`; 0 warnings, 0 errors after reapplying the outside-domain field unblock.
- Outside-domain unblock note: `HectonPlayerMovement.cs` was already dirty from another agent and referenced `_playerKinematicsDragScheduled` / `_playerKinematicsDragJobHandle` without declarations. Added only those two fields to restore compilation; no movement behavior refactor was performed.
- Unity console retry: PENDING VERIFICATION. `read_console` failed with `Unity session not ready for 'read_console' (ping not answered)`.
- Loop 8 prompt extraction: PASS. Attribute-aware CLI extraction reconfirmed primary objective count = 15.
- Loop 8 hot allocation audit: PASS. Scanner hot slice remains `HOT_SLICE_MATCHES=0`; PDA/runtime allocation-text scan remains `PDA_RUNTIME_ALLOC_TEXT_MATCHES=0`; scan-state save audit reports `RUNTIME_GETKEYVALUEARRAYS=0`.
- Loop 8 compile: PASS via `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`; final rerun 0 warnings, 0 errors.
- Loop 8 scoped diff hygiene: PASS for all DATA_ARCHAEOLOGY-touched files plus the `HectonPlayerMovement.cs` compile unblock; no diff-check errors.
- Loop 8 Unity console retry: PENDING VERIFICATION. `read_console` failed with `Unity session not available; reason=no_unity_session`.
- Loop 9 build/compile: SKIPPED BY USER REQUEST. User explicitly instructed: `do not build or run dotnet build`.
- Loop 9 scoped diff hygiene: PASS for `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs`; only LF-to-CRLF warning reported.
- Loop 9 static allocation audit: PASS. `DataArchaeologyRuntime.cs` and `PDADataArchaeologyDecryptLabel.cs` returned no matches for `GetKeyValueArrays`, `new char[]`, `new string`, `string.Create`, TMP `.text =`, `SetText(`, `.ToString(`, or `Format(`; `RenderHash` clamps copy length before `AsSpan`.
- Loop 9 partial-load audit: PASS by static read. `LoadFromSaveData` and `TryLoadMmfCold` now call `InsertOrUpgradePartialCold`, which deduplicates by hash and keeps highest progress without calling `MarkMmfDirty`; explicit Unscanned/Scanned states remove matching partial records through `RemoveNonScanningPartials(false)`, and `SetScanState(..., ScanStateUnscanned)` removes the key instead of saving a zero-state entry.
- Loop 9 Unity console / profiler / GC proof: PENDING VERIFICATION. `read_console` failed with MCP transport error to `http://127.0.0.1:8088/mcp`; no live Unity session proof was available in this pass.
