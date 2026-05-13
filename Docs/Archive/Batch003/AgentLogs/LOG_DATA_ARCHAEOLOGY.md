# LOG_DATA_ARCHAEOLOGY

## 2026-05-12 DATA_ARCHAEOLOGY Scanner UI Pass

Status: PENDING VERIFICATION
Domain: ECHELON 8 PRESENTATION & UX
Task Count: 15

What was wrong:
- Scanner/lore discovery path lacked a native scan-state authority keyed by entity/AUP hash.
- Scannable targets did not expose a stable hash for batched scanner raycasts.
- PDA scanner display needed hash-to-text length and char-buffer output instead of TMP string assignment.
- Completion notifications needed hash-only GlobalSignals, not concrete HUD/crafting calls.
- Data archaeology scan states had no explicit binary save payload.
- Legacy `ScannerTool.cs` still contains string-heavy reporting code, so the new hot path needed a separate audit.

What was done:
- Added `NativeParallelHashMap<int, byte>` scan state authority and `NativeArray<ulong>` lore bitmask in `DataArchaeologyRuntime`.
- Added dispatcher `RaycastCommand` path in `ScannerTool` and read `ScannableTarget.EntityHash`.
- Added hold-progress path that flips state to scanned at `progress > 1f`.
- Added `BlueprintUnlockedSignal`, `ToolAcousticSignal`, and `HUDNotificationSignal` lanes through `GlobalSignals`.
- Added `PDADataArchaeologyDecryptLabel` using `LocRegistry.GetLength`, `CharBufferPool`, `Span<char>`, and TMP `SetCharArray`.
- Added Low/MX350 presentation gates for PDA scrambling and `_HectonScannerPoints`.
- Added SaveData v66 scan state arrays and `SaveBinaryPayloadCodec` read/write helpers for the NativeParallelHashMap copy.
- Added `RECON_DATA_ARCHAEOLOGY.md`, `Status_DATA_ARCHAEOLOGY.md`, and `Rationale_DATA_ARCHAEOLOGY.md`.

Cinematic Cheats used:
- One capped global shader point array instead of per-object CPU scan-grid simulation.
- FNV/entity hash unlocks instead of localized strings in the scanner completion path.
- Bitmask lore authority instead of managed list scans.
- Tier gate: Low/Unknown/MX350 skips scrambling and shader-mask uploads; High/Ultra may spend saved cycles on stronger shader/audio presentation.
- AUP listener rebases cached scan data only on actual origin shift; no per-frame origin polling.

Exact Microseconds saved:
- Removed new hot scanner string generation: expected hitch prevention; exact profiler measurement absent.
- Native scan state lookup/update: estimated 2-5 us per target operation on MX350-class CPU.
- Signal enqueue for unlock/audio/HUD: estimated 1-3 us each.
- Low-tier disabled text scramble and shader upload: 0 us spent on those effects.
- Shader mask dirty upload on non-low tiers: estimated 10-40 us only when point/progress changes.
- PDA pooled char update: estimated 10-35 us per active label, bounded by text length; no measured GC proof.
- Save-state map blit: cold save O(n), 0 us normal-frame cost.

Verification:
- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` via PowerShell regex after core loops.
- Recon: `ScannerTool.cs` has 0 `Instantiate`, 256 `string` matches, and 0 hot-slice matches in `UpdateScientificScanning`.
- Final compile: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 warnings and 0 errors.
- Scoped diff check for DATA_ARCHAEOLOGY files passed.
- Repository-wide `git diff --check` is still blocked by unrelated pre-existing whitespace in AGENTS/docs/meta files.
- Unity console, runtime profiler, and in-build GC allocation proof remain PENDING VERIFICATION. Unity MCP console read failed with `no_unity_session`.

## 2026-05-12 DATA_ARCHAEOLOGY Continuation Audit

Status: PENDING VERIFICATION
Domain: ECHELON 8 PRESENTATION & UX

What was wrong:
- The active batch tag uses XML attributes after `id`; the strict id-only extractor fails and can create false prompt-loss reports.
- `LocRegistry` needed to keep RTL visual buffer routing while exposing allocation-free `GetLength`/buffer APIs.
- `PDADataArchaeologyDecryptLabel` could stay registered when rebound to hash `0`, could leave stale label text, and still participated in TMP raycasts.
- Already-scanned raycast targets could be reacquired by the scanner path instead of being rejected immediately.
- Legacy/migrated saves could miss v66 data archaeology scan-state key/value arrays.
- Unity editor verification is currently blocked by an outside-domain `SubmarineStructuralGrid.cs` interface error, while DATA_ARCHAEOLOGY dotnet compile is clean.

What was done:
- Re-extracted the DATA_ARCHAEOLOGY prompt with an attribute-aware CLI regex and confirmed the primary objective count is 15.
- Restored RTL-aware `LocRegistry.ResolveVisual` / `TryGetVisualBuffer` behavior.
- Added PDA label `Clear()` for zero hash, zero-length `SetCharArray` clearing via static empty char buffer, completed-label unregister, and `raycastTarget = false`.
- Made `DataArchaeologyRuntime.RegisterRaycastTarget` return `false` for zero/already-scanned hashes and made `ScannerTool` skip active target setup when registration fails.
- Added `SaveDataMigration.EnsureLoreSystems` guards for data archaeology discovery bit words, partial scan arrays, scan-state arrays, and count clamps.
- Reran scoped hot-path allocation scans, scoped diff hygiene, dotnet compile, Unity refresh, and Unity console read.

Cinematic Cheats used:
- Completed targets are rejected before shader/audio/UI presentation, keeping scan overkill only for active state.
- PDA text clearing uses TMP char-array path, not strings or object destruction.
- RTL handling stays as a buffer copy through `RTLProcessor`, avoiding per-language string construction.

Exact Microseconds saved:
- Already-scanned raycast rejection avoids an estimated 2-5 us map/progress path per reacquired target.
- Completed/cleared PDA labels unregister from `PriorityLayer.UI`; expected saving is sub-microsecond per inactive label per frame plus no TMP raycast participation.
- Low/MX350 remains 0 us for scrambling and shader-mask upload by tier gate.
- Save migration work is cold only; normal frame cost remains 0 us.

Verification:
- Attribute-aware prompt extraction succeeded from `Docs/Tasks/CURRENT_BATCH.md`.
- `HOT_SLICE_MATCHES=0` for scanner `UpdateScientificScanning` to `TryResolveScientificSpatialContact`.
- `PDA_RUNTIME_ALLOC_TEXT_MATCHES=0` for `PDADataArchaeologyDecryptLabel.cs` and `DataArchaeologyRuntime.cs`.
- Scoped `git diff --check -- <DATA_ARCHAEOLOGY paths>` passed with only LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 warnings and 0 errors.
- Unity refresh timed out after 60 seconds. Last available console error before session loss: `Assets\_Project\Scripts\SubmarineStructuralGrid.cs(53,117): error CS0535: 'SubmarineStructuralGrid' does not implement interface member 'ILateFrameTickable.LateFrameTick()'`.

## 2026-05-12 DATA_ARCHAEOLOGY MMF Flush Cadence Patch

Status: PENDING VERIFICATION
Domain: ECHELON 8 PRESENTATION & UX

What was wrong:
- Partial scan progress dirtied the MMF sidecar on hot scan updates, and the previous late-frame flush path could write the sidecar during active scanning.
- `DataArchaeologyRuntime.Dispose()` only removed the origin-shift listener when called directly; render/save/late-frame registrations were unregistered only by `OnDisable()`.
- A separate dirty `HectonPlayerMovement.cs` edit by another agent added scheduled drag-job methods without declaring `_playerKinematicsDragScheduled` and `_playerKinematicsDragJobHandle`, blocking full project compile.

What was done:
- Added `MmfPartialFlushCadenceSeconds = 4f` and `MmfUrgentFlushDelaySeconds = 0.25f`.
- Routed partial progress through non-urgent `MarkMmfDirty(false)` and completion/removal/interruption through urgent `MarkMmfDirty(true)`.
- Made `LateFrameTick` flush only after the scheduled dirty time and made `PersistMmfCold()` return immediately when the sidecar is clean.
- Added shared `UnregisterRuntime()` and called it from both `OnDisable()` and `Dispose()`.
- Added the two missing `HectonPlayerMovement` drag-job backing fields as a compile-only outside-domain unblock.

Cinematic Cheats used:
- The scan UI still uses scalar progress, one global shader-point lane, and hash-only signals; no physical scan-grid simulation or per-target material churn was introduced.
- MMF persistence is now a cold sidecar cadence, not part of the hot visual loop.

Exact Microseconds saved:
- Hot scanner allocation audit remains `HOT_SLICE_MATCHES=0`.
- PDA/runtime text-allocation audit remains `PDA_RUNTIME_ALLOC_TEXT_MATCHES=0`.
- Partial MMF writes are removed from the scan-frame path; exact filesystem savings are machine-dependent and unmeasured, but the former risk was a synchronous disk write during active scanning.
- Clean disable/dispose now performs 0 us of sidecar write work when `_mmfDirty == false`.

Verification:
- Scoped diff hygiene passed for `DataArchaeologyRuntime.cs` and the `HectonPlayerMovement.cs` compile unblock, with only LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` passed with 0 warnings and 0 errors after reapplying the outside-domain field unblock.
- Unity runtime/profiler/GC allocation proof remains PENDING VERIFICATION. `read_console` failed with `Unity session not ready for 'read_console' (ping not answered)`.

## 2026-05-12 DATA_ARCHAEOLOGY MMF Authority Patch

Status: PENDING VERIFICATION
Domain: ECHELON 8 PRESENTATION & UX

What was wrong:
- MMF cold-load restored completed fragment positions but did not set completed lore bits.
- MMF partial records restored only fixed arrays, not the native `NativeParallelHashMap<int, byte>` scan-state authority.
- Duplicate partial records could survive in the sidecar load path.
- Save-state copy used `GetKeyValueArrays(Allocator.Temp)`, an avoidable cold native allocation.
- AUP rebases changed persisted fragment mirrors in memory but did not dirty the MMF sidecar.

What was done:
- Completed MMF fragment records now call `SetNativeLoreBit(...)` and set scan state to Scanned.
- Partial MMF records deduplicate by hash and set scan state to Scanning unless already Scanned.
- Scan-state save copy now enumerates the native map directly into preallocated save arrays; `RUNTIME_GETKEYVALUEARRAYS=0`.
- AUP rebase marks the sidecar dirty on the existing bounded cadence when persisted fragment mirrors move.

Cinematic Cheats used:
- Persistence remains a fixed-record sidecar; no managed recovery dictionary, no text lookup, no full object reconstruction.
- AUP persistence uses a dirty flag and delayed flush, not immediate disk I/O in the shift event.

Exact Microseconds saved:
- Removed one cold native temp allocation from scan-state save.
- No hot-frame scanner cost added; MMF and scan-state repairs execute only on cold load/save or AUP shift.
- Shader/audio/PDA hot audits remain unchanged: `HOT_SLICE_MATCHES=0`, `PDA_RUNTIME_ALLOC_TEXT_MATCHES=0`.

Verification:
- Prompt extraction reconfirmed primary objective count = 15.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` final rerun passed with 0 warnings and 0 errors.
- Scoped diff hygiene passed with no diff-check errors.
- Unity runtime/profiler/GC allocation proof remains PENDING VERIFICATION. `read_console` failed with `Unity session not available; reason=no_unity_session`.

## 2026-05-12 DATA_ARCHAEOLOGY UI Retry and Stale Partial Patch

Status: PENDING VERIFICATION
Domain: ECHELON 8 PRESENTATION & UX

What was wrong:
- PDA decrypt labels could fail to acquire a pooled char buffer and still advance out of the dirty/render path, leaving stale or empty completed text.
- PDA render copy length trusted localization output length without also clamping against the backing buffer capacity.
- Old or malformed SaveData could carry duplicate partial scan hashes.
- Explicit Unscanned or Scanned state could coexist with stale partial progress after cold load.
- The user explicitly blocked `dotnet build`, so this pass cannot claim compile proof.

What was done:
- `PDADataArchaeologyDecryptLabel.RenderHash` now returns success/failure; failed renders keep `_dirty = true` and retry instead of unregistering.
- `RenderHash` clamps copy length by localization buffer capacity and `CharBufferPool.SlotCapacity` before building spans.
- `DataArchaeologyRuntime.LoadFromSaveData` now routes partial records through `InsertOrUpgradePartialCold`.
- `TryLoadMmfCold` uses the same cold helper, skips hashes already marked Scanned, and keeps the highest progress for duplicate partials.
- `RemoveNonScanningPartials(false)` keeps explicit Unscanned/Scanned state authoritative after save scan-state load.
- `SetScanState(..., ScanStateUnscanned)` now removes the native map key instead of persisting a zero-state entry.

Cinematic Cheats used:
- The PDA effect remains pooled char-buffer text and scalar reveal buckets, not runtime text prefab churn.
- Partial scan state remains fixed arrays plus native hash-state authority, not a managed recovery dictionary.
- No physical scanner simulation, per-target material mutation, or immediate load-time MMF rewrite was introduced.

Exact Microseconds saved:
- Duplicate partial load repair is cold only; normal scanner frame cost remains 0 us changed.
- Failed PDA render retry adds one branch and avoids string/array fallback allocation.
- PDA span bounds guard adds scalar min/null checks only; no allocation.
- Static allocation audit remains clean for the touched PDA/runtime text path.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs` passed with only LF-to-CRLF warning.
- Static allocation grep returned no matches for `GetKeyValueArrays`, `new char[]`, `new string`, `string.Create`, TMP `.text =`, `SetText(`, `.ToString(`, or `Format(` in `DataArchaeologyRuntime.cs` and `PDADataArchaeologyDecryptLabel.cs`.
- `rg` readback confirmed both `LoadFromSaveData` and `TryLoadMmfCold` use `InsertOrUpgradePartialCold`.
- `rg` readback confirmed `SetScanState` removes `ScanStateUnscanned` keys before save-state enumeration.
- Build/compile verification skipped by direct user instruction: `do not build or run dotnet build`.
- Unity runtime/profiler/GC allocation proof remains PENDING VERIFICATION; `read_console` failed with MCP transport error to `http://127.0.0.1:8088/mcp`.
