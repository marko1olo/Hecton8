# Status 1623 - ZERO_GC_SUBTITLES_AND_BABEL_LOCALIZATION_COMPILER

Status: STATIC_COMPLETE_BUILD_NOT_RUN
Domain: ECHELON 8 / Zero-GC Subtitles (Babel)
Current batch XML: missing `<AGENT_PROMPT id="1623">` in `Docs/Tasks/CURRENT_BATCH.md`

Relevant mandates read before code:
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `ARCH_Execution_Phases.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `QA_Evidence_Text_Filter_Audit.txt`

## Checklist

- [x] Task 00: Batch prompt extraction.
  - DOD practice: CLI regex extraction from `CURRENT_BATCH.md`, not MCP/truncated read.
  - Rejected alternative: infer tasks from neighboring XML blocks; that would violate strict parsing.
  - Estimate: 350 us file scan after OS cache.
  - Result: blocked XML route; user direct assignment retained as bounded domain scope.
- [x] Task 01: Map existing Babel/subtitle/TMP runtime code.
  - DOD practice: Static source map across `SubtitleManager`, `BabelSubtitleSyncRuntime`, `TmpTextNoAlloc`, `CharBufferPool`, `LocRegistry`, and 1423 editor tests.
  - Rejected alternative: Broad rewrite of all menu string bridges; many are cold UI/menu paths and not this hot subtitle domain.
  - Estimate: 900 us cached file scans.
- [x] Task 02: Identify managed-string hot path violations.
  - DOD practice: Literal and regex scans for `DisplaySubtitle(string`, `.text =`, `SetText(`, `string.Format`, `new string(`, `.ToString(`, `foreach (`, `Array.Resize`, interpolation in modified runtime path.
  - Rejected alternative: Claiming runtime zero-GC without profiler capture; evidence is static only.
  - Estimate: 1100 us cached source scan.
- [x] Task 03: Implement or harden zero-GC char buffer writer.
  - DOD practice: Added Babel hash + `ReadOnlySpan<char>` fallback route; missing hashes copy fallback into `BabelLease.Span`, then into TMP buffer without managed string construction.
  - Rejected alternative: `new string(fallback)` or `TMP_Text.text`; both violate Babel zero-GC policy.
  - Estimate: 6-22 us per missing-hash fallback, bounded by 512-char Babel lease.
- [x] Task 04: Implement or harden TMP direct char-array flush.
  - DOD practice: Preserved existing `SetCharArray` flush path; fallback and glitch paths mutate preallocated char buffers before existing `ApplySubtitleBuffer`.
  - Rejected alternative: TMP `SetText` formatting; managed bridge and parser variance.
  - Estimate: 0 extra TMP allocations; extra mutation cost only on dirty visual refresh.
- [x] Task 05: Integrate deterministic glitch decay for low-battery/stress text.
  - DOD practice: Consumes `SignalBus<BatteryLevelSignal>` and `SignalBus<SurvivalVitalsChangedSignal>` snapshots, smooths continuous intensity, quantizes visual refresh cadence, mutates existing render char array with deterministic glyph hash.
  - Rejected alternative: Direct `PowerGrid` polling or power-component references; illegal cross-domain dependency and scene traversal risk.
  - Estimate: low tier 2-5 us on refresh frames for 256 chars; high/ultra can spend 5-35 us for denser visual overkill.
- [x] Task 06: Static audit no `text =`, string `SetText`, interpolation, LINQ in modified runtime paths.
  - DOD practice: `Select-String` literal/regex scan returned no forbidden hits in `SubtitleManager.cs`.
  - Rejected alternative: Editor compile as proof; user forbids build for non-critical edits.
  - Estimate: 700 us cached scan.
- [x] Task 07: Manual compile-risk pass without `dotnet build`.
  - DOD practice: Line-range review of changed code, `git diff --check`, API lookup for `SignalBus<T>.GetFrameSnapshot`, `BatteryLevelSignal`, `SurvivalVitalsChangedSignal`, `TryRegisterImmediateCue`.
  - Rejected alternative: `dotnet build`; CPU-heavy and not critical after local static pass.
  - Estimate: 0 runtime; verification is static.
- [x] Task 08: Append final log.
  - DOD practice: Wrote `Docs/AgentLogs/LOG_1623.md` with wrong/done/cheats/microseconds/verification.
  - Rejected alternative: Chat-only report; CTO protocol requires disk log.
  - Estimate: 0 runtime.
- [x] Task 09: APEX dependency verification.
  - DOD practice: Brace-scoped static scan of hot `Tick`/`FixedUpdate`/`LateFrameTick`/`Execute` and 1623 helper bodies for `GlobalRegistry.Get<`, `GlobalRegistry.`, `GetComponent(`, `TryGetComponent(`, scene search, `Camera.main`.
  - Rejected alternative: Line grep only; it cannot prove method-body scope.
  - Estimate: 2.1 ms cached PowerShell scan.
- [x] Task 10: APEX phase/lock verification.
  - DOD practice: Added editor static assertion `SubtitleManager1623_ApexIntegratorRoute_IsPhaseSafeAndZeroGc`; verified presentation route through `LateFrameTick`, SignalBus snapshots, no text bridges, and one mutation guard per try/finally lock route.
  - Rejected alternative: Runtime JSON/binary dump; user rejected I/O reports and it would not prove source contracts.
  - Estimate: 0 runtime unless editor tests are executed.
- [x] Task 11: TMP registry fixed-capacity fail-closed hardening.
  - DOD practice: Removed dynamic `EnsureCapacity` growth from `TMP_TextRegistry`; fixed backing stores at 2048 entries and added overflow telemetry via `OverflowCount`.
  - Rejected alternative: Keeping cold resize comments or switching to `List<T>`; both preserve hidden managed allocation/copy paths.
  - Estimate: 0 us steady-state change, avoids 0.2-2.0 ms pathological resize/copy spikes on i3/MX350-class hardware.
- [x] Task 12: Font swap scheduler capacity parity.
  - DOD practice: Matched `LabelSwapScheduler` pending ring capacity to the 2048-entry TMP registry and added a fail-closed `OverflowCount`.
  - Rejected alternative: Silent 512-entry queue truncation or managed `Queue<T>` growth during language reboot.
  - Estimate: steady-state remains bounded by 2-18 labels per `LateFrameTick`; avoids stale-font coverage failure on UI scenes above 512 labels.
- [x] Task 13: Localized madness text padding priming.
  - DOD practice: Moved active TMP `UpdateMeshPadding()` out of per-frame `ApplyActiveState` and into a bounded worst-case `PrimeActiveMeshPadding` transition.
  - Rejected alternative: Rebuilding TMP mesh padding every active frame or mutating subtitle chars for this effect.
  - Estimate: avoids 3-20 us per affected label per active frame on i3/MX350-class hardware, static estimate only.
- [x] Task 14: Subtitle power-glitch rich-text guard.
  - DOD practice: Replaced random index mutation with a two-pass Span/char-buffer candidate filter that skips TMP rich-text tags and whitespace.
  - Rejected alternative: Stripping all rich text before glitch or allowing random mutation inside `<...>` tags; both damage presentation truth.
  - Estimate: typical subtitle 0.5-3 us extra on refresh frames; avoids broken TMP parse/rebuild spikes from corrupted markup.
- [x] Task 15: Localized TMP auto-size duplicate repair removal.
  - DOD practice: Removed duplicate `RepairCollapsedRectHierarchy()` call from `LateFrameTick`; the single owner repair remains inside `ApplyConfiguration`.
  - Rejected alternative: Leaving two bounded layout repair walks per pending localization apply.
  - Estimate: avoids one redundant 4-pass/4-depth rect walk per dirty label apply; cost depends on label hierarchy depth.
- [x] Task 16: Font streaming visible-slice prefetch budget correction.
  - DOD practice: `prefetchedCount` now advances only when `LocRegistry.TryResolveVisibleTextOffsetSlice` succeeds.
  - Rejected alternative: Spending prefetch budget on misses; that silently pushes later valid labels back to full UTF-8 resolve during staged font swap.
  - Estimate: preserves cheap slice path coverage during language reboot; runtime saving depends on registry miss distribution.

## Verification

- Compile: not run. User forbids `dotnet build` except critical necessity.
- Runtime/Profiler: absent.
- `git diff --check`: no whitespace errors; Git warned only that LF will become CRLF when Git touches modified C# files.
- Forbidden text API scan: no hits in `SubtitleManager.cs` for `DisplaySubtitle(string`, `.text =`, `SetText(`, `string.Format`, `new string(`, `.ToString(`, `foreach (`, `Array.Resize`, interpolation.
- APEX hot-body scan: no cold dependency hits in `SubtitleManager` hot/1623 bodies.
- Lock route count: `TryAcquireSubtitleMutationBuffer<T>`, `WriteFrameTelemetry`, and `RecordUIOptimizationFailure` each have one acquire, one release, and one `finally`.
- TMP registry resize scan: no hits for `EnsureCapacity`, `newCapacity`, `resizedNodes`, `resizedEntries`, `Array.Resize`, or resized backing-store assignment.
- Label swap scheduler scan: fixed `MaxQueueCapacity = 2048`, no `Queue<T>`, no `List<T>`, no `Array.Resize`, no `new PendingSwap[512]`.
- Localized madness FX scan: `ApplyActiveState` has no `UpdateMeshPadding`; `PrimeActiveMeshPadding` and `ApplyIdleState` each have one padding update.
- Subtitle power-glitch scan: `ApplyPowerTextGlitchIfNeeded` uses `CountPowerTextGlitchCandidates`, tracks `insideRichTextTag`, and no longer indexes by `seed % safeLength`.
- Localized auto-size scan: `LateFrameTick` calls only `ApplyConfiguration`; `ApplyConfiguration` owns one `RepairCollapsedRectHierarchy` call.
- Font streaming scan: `CollectSwapQueue` increments `prefetchedCount` only inside the successful visible-slice branch.
- Build throttling: `dotnet build` not launched; existing Unity/Roslyn `csc` processes were detected, so no build command was attempted.
- Evidence class so far: STATIC_SOURCE / STATIC_DOC only.
