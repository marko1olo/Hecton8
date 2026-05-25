# LOG X_011 - VOCAL_WARNING_AND_SUBTITLE_STREAMLINER

## 2026-05-23 - Phase 0 Through Static Proof Pass

What was wrong:
- VocalWarningSystem retained a heap-style priority route for a five-alarm domain. The route had more state mutation than needed and made low-count scheduling look like a general OS priority queue problem.
- SubtitleManager retained a managed string lane: SubtitleRequest, _stringQueue, _currentMessage, _lastEnqueuedMessage, ShowImmediate(string), and string-based display corruption. Public string callers could feed the route into retained managed strings.
- SubtitleCueSignal was 16 bytes and did not satisfy the 64-byte ARM64 signal contract requested for the VWS/subtitle lane.
- Existing proof state was prose-heavy. No X_011 scanner, target JSON, or deterministic 50-trigger storm report existed.

What was done:
- Replaced the VWS queue state with VocalWarningPriorityState.VwsPriorityWord plus one VocalWarningDTO slot per bit index. Canonical warnings map to bits 63..59. Highest priority resolves by high-bit scan, not heap sift.
- Rewired VWS dispatch to publish SignalBus<VocalCueSignal> and SignalBus<SubtitleCueSignal> from the resolved priority-word candidate. Rejection marks the priority-state fault flags and can trigger telemetry dump.
- Expanded SubtitleCueSignal to explicit 64-byte layout with TokenHash, SourceHash, StartAudioFrame, duration, priority, flags, latency, and padding. BabelSubtitleSyncRuntime validates SizeOf<SubtitleCueSignal>() == 64.
- Removed SubtitleManager's legacy managed string queue/current-message path. Public DisplaySubtitle(string) and notification strings now copy immediately to the pooled ReadOnlySpan<char> / BufferedSubtitleRequest route. Rendering remains ApplySubtitleBuffer -> TMP SetCharArray.
- Added OOP_Voice_Scanner_X_011 and report Docs/Reports/UX_OPTIMIZATION_REPORT_X_011.json. Static forbidden hot-route findings: none for NativeMinHeap, VocalWarningHeapOps, managed subtitle string queue, direct .text writes, new string, or string.Format in the VWS/subtitle files.
- Added VocalWarningStormTorture_X_011 and report Docs/Reports/UX_VWS_STORM_TORTURE_X_011.json. Static deterministic storm: 50 triggers collapse to 5 active bits, priorityWordHex 0xF800000000000000, highestBit 63.
- Added/updated Docs/Tasks/Status_X_011.md and Docs/AgentLogs/Rationale_X_011.md with DOD, rejected alternatives, scalability, and build-gate status.
- Restored Assembly-CSharp project assets under guard and verified dotnet build Assembly-CSharp.csproj with 0 warnings and 0 errors.

Cinematic Cheats used:
- Priority simulation was collapsed to one ulong and canonical bit ranking. This is a deliberate game-system fake: predictable alarm dominance over generic scheduler realism.
- Subtitle synchronization uses audio-frame timestamps and fixed char buffers. Text polish scales by quality tier; text ownership and DTO identity do not change.
- VWS high-tier budget should buy richer radio distortion, spatial blend, and subtitle presentation, not a more complicated scheduler.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. Unity profiler/GCMonitor/player proof not run.
- Build gate: first build attempt failed before compile with NETSDK1004 because Temp/obj/Assembly-CSharp/project.assets.json was missing. Guarded restore generated it. dotnet build Assembly-CSharp.csproj then passed with 0 warnings and 0 errors.
- Static operation delta: heap sift/sort path removed from VWS hot route; priority fetch is one 64-bit scan over VwsPriorityWord. Exact frame-time value remains PENDING PROFILER.

Verification artifacts:
- Docs/Reports/UX_OPTIMIZATION_REPORT_X_011.json
- Docs/Reports/UX_VWS_STORM_TORTURE_X_011.json
- Static scan command found no forbidden hot-route tokens in VocalWarningSystem.cs, SubtitleManager.cs, and BabelSubtitleSyncRuntime.cs.
- git diff --check on touched files returned no whitespace errors; line-ending warnings only.
- dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false: PASS, 0 warnings, 0 errors.

Residual risk:
- No Unity editor Play Mode, player build, profiler, or GCMonitor evidence exists from this session.
- New Editor scanner/harness files may require Unity project-file regeneration before they appear in generated csproj compile coverage.

## 2026-05-23 - Phase 0 Full-Tree Archaeology Delta

What was wrong:
- The first UX optimization JSON was accurate for owned hot-route files, but too narrow for the literal Phase 0 order to parse Assets/_Project/Scripts and build a source-backed audio/subtitle route graph.
- Full-tree searches produce noisy findings: heap keywords still exist in AI, construction, economy, world, and editor harness files. Those are not VWS/subtitle runtime ownership, but leaving them unclassified would make the report ambiguous.

What was done:
- Re-scanned 2379 C# files under Assets/_Project/Scripts.
- Recorded 25 VWS reference files, 84 subtitle reference files, 8 heap-keyword files, and 536 string-risk files in Docs/Reports/UX_OPTIMIZATION_REPORT_X_011.json.
- Added owned hot-route files and source-backed route map: VocalWarningSystem signal snapshots -> VwsPriorityWord -> SignalBus<VocalCueSignal> and SignalBus<SubtitleCueSignal> -> BabelSubtitleSyncRuntime/SubtitleManager.
- Classified external heap findings:
  - Assets/_Project/Scripts/AI/Pathfinding/VoxelAStarJobs.cs
  - Assets/_Project/Scripts/AI/Pathfinding/VoxelAStarContracts.cs
  - Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs
  - Assets/_Project/Scripts/Construction/DroneFleetManager.cs
  - Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs
  - Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs
  - Assets/_Project/Scripts/World/HectonAnomalyEngine.cs
  - Assets/_Project/Scripts/Audio/Editor/OOP_Voice_Scanner_X_011.cs
- Reconfirmed: owned VWS/subtitle hot-route forbidden findings remain empty.

Cinematic Cheats used:
- No new runtime cheat was introduced in this delta. The existing cheat remains the single 64-bit priority word for five alarms, with pooled subtitle buffers and audio-frame token sync.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed.
- This delta was documentation/reporting only; no runtime code changed.
- Runtime build proof remains the prior guarded pass: dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false, 0 warnings, 0 errors.

Residual risk:
- Full-tree string-risk count is intentionally broad and includes unrelated UI/editor `.text`, `.ToString()`, and string creation patterns. It is not a hot-route GC proof.
- Unity profiler, GCMonitor, Play Mode, and player-build proof remain pending.

## 2026-05-23 - T.A.R.S. Paranoid Zero-GC Audit

What was wrong:
- BabelSubtitleSyncRuntime still used Time.frameCount for presentation-frame dedupe and localization telemetry Frame.
- SubtitleManager still used Time.frameCount for global subtitle signal dedupe.
- LocalizationManager and LocRegistry still used Time.frameCount in text-route frame caches/telemetry.
- VocalBankPlaybackRuntime had cold runtime exception log concatenation with ex.Message.
- VWS highest-bit selection still had a high-half branch after the zero-word guard.

What was done:
- BabelSubtitleSyncRuntime now resolves presentation frames from SystemDispatcher.ReadPublishedDispatcherFrameId(), then audio-frame clock, then a local non-zero fallback. Telemetry writes the same owner frame.
- SubtitleManager drains global subtitle signals using BabelSubtitleSyncRuntime.CurrentPresentationFrame/CurrentAudioFrame. No Time.frameCount remains in the X_011 subtitle route.
- LocalizationManager cache gates and LocRegistry telemetry now use dispatcher/audio-frame owner ids instead of Unity frameCount.
- VocalBankPlaybackRuntime warning logs now use constant messages in catch paths; no exception-message string concatenation remains there.
- SubtitleManager cold waveform bootstrap names now use a predeclared string table instead of "Bar_" + i.
- VocalWarningSystem priority-word high/low half selection now uses math.select(low, high, useHigh) and lzcnt(selected). Pop/expiry still clear bits through state.VwsPriorityWord &= ~bitMask, scanWord &= ~bitMask, and activeWord &= ~bitMask.
- Added Docs/Reports/UX_ZERO_GC_AUDIT_X_011.json.

Cinematic Cheats used:
- Alarm scheduling remains a single priority word instead of a general scheduler. That is the correct fake for five canonical alarms.
- Subtitle time remains audio-frame based; no coroutine timing or Unity frame counter remains in the owned subtitle route.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed.
- Static hot-route scan: no Time.frameCount, WaitForSeconds, StartCoroutine, IEnumerator, yield return, ToString, string.Format, StringBuilder, TMP .text assignment, TMP SetText, string.Concat, NativeMinHeap, VocalWarningHeapOps, SubtitleRequest, or _stringQueue tokens in the owned VWS/subtitle route files.
- Compile status: PENDING after this patch. Guard checks saw CPU 50.32%, then CPU 67.91% with csc.exe/dotnet active, then CPU 99.23% with 9 compiler/dotnet processes, then CPU 71.4%, then CPU 97.67% with dotnet active. dotnet build was not launched.

Residual risk:
- Entire audio/UI/localization codebase is not globally managed-allocation-free. Legacy LocalizationManager string APIs still create strings and use Regex.Replace. They are not on the X_011 SignalBus -> Babel span -> SubtitleManager SetCharArray hot route.
- Broader immediate audio/UI/narrative/localization scan still reports 94 Time.frameCount tokens and 1 coroutine-timing token outside the owned X_011 route. Those are separate domain-owner cleanup items, not VWS/subtitle hot-route findings.
- Unity profiler/GCMonitor/player proof remains absent. Static clean route is not a measured 0 B GC claim.

## 2026-05-23 - T.A.R.S. Broad Audio/Text Re-Sweep

What was wrong:
- Runtime Audio/UI/Narrative still contained 110 direct Time.frameCount / UnityEngine.Time.frameCount reads outside the already-clean X_011 VWS/subtitle lane.
- Runtime audio/UI fault paths still had '+ ex.Message' or direct ex.Message logging in AdaptiveStemAudioMixer, DynamicMusicGranularSynthesizer, and WristHologramHudRuntime.
- LocalizationManager still hid managed text materialization behind legacy string APIs: Babel binary -> new string, development corruption -> new string, and FormatLocalized -> string.Create.
- SubtitleManager still had the stale type name BufferedSubtitleRequest, which was not the removed legacy struct but polluted forbidden-token scans.

What was done:
- Re-extracted Docs/Tasks/CURRENT_BATCH.md AGENT_PROMPT id X_011 by CLI. Prompt found, length 11417, task labels Task 01 through Task 10.
- Added SystemDispatcher.CurrentFrameIndex as the dispatcher-owned int frame accessor.
- Replaced 110 direct Time.frameCount reads in runtime Audio/UI/Narrative scope with Hecton8.Core.SystemDispatcher.CurrentFrameIndex.
- Removed runtime '+ ex.Message' and direct exception-message debug output in AdaptiveStemAudioMixer, DynamicMusicGranularSynthesizer, and WristHologramHudRuntime.
- Hardened LocalizationManager: Babel legacy string API now publishes telemetry and returns false instead of allocating a string; string corruption API warns and returns original text; FormatLocalized warns and returns the template instead of string.Create.
- Renamed BufferedSubtitleRequest to BufferedSubtitleCue.
- Updated Docs/Reports/UX_ZERO_GC_AUDIT_X_011.json and Docs/Reports/UX_OPTIMIZATION_REPORT_X_011.json with new broad scan numbers.

Cinematic Cheats used:
- Frame identity is routed through one dispatcher-owned integer facade instead of Unity frame reads scattered through presentation/audio code.
- Legacy string APIs degrade to telemetry/fallback/template instead of materializing strings; zero-GC buffer/span APIs are the production lane.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed.
- Static owned-route scan: 0 hits for Time.frameCount, UnityEngine.Time.frameCount, WaitForSeconds, StartCoroutine, yield return, ToString, string.Format, StringBuilder, .text assignment, SetText, string.Concat, interpolation, new string, string.Create, + ex.Message, NativeMinHeap, VocalWarningHeapOps, SubtitleRequest, _stringQueue, ShowImmediate(string), CopyStringToRenderBuffer, ResolveDisplayMessage.
- Broad runtime Audio/UI/Narrative/Localization scan: 0 Time.frameCount, 0 coroutine timing tokens, 0 '+ ex.Message'.
- Broad residual managed string risk remains outside Betty/subtitle route: ToString 2, StringBuilder 11, direct .text assignment 1, SetText 52, string.Concat 18, interpolation 60, new string 22, string.Create 4.
- Compile status: RESULT UNKNOWN. A guarded dotnet build launched, but the shell call timed out before output was captured. Follow-up wait confirmed compiler processes drained. Re-run was blocked by latest guard CPU=66.25%, CompilerProcessCount=0.

Residual risk:
- Whole-project UI/text is still not globally zero-GC. Settings/PDA/Pause/debug text surfaces retain managed string composition and require owner-led migration.
- Runtime compile proof is pending after this broad patch. Prior build pass predates these new edits.
- Unity profiler/GCMonitor/player proof remains absent; static clean route is not a measured 0 B GC claim.

## 2026-05-23 - T.A.R.S. Second Broad String Sweep

What was wrong:
- Broad audio/UI/narrative text scan still contained runtime debug interpolation and exception-message materialization after the first broad sweep.
- BaseIntegrityHUD constructed per-percent notification strings with string.Create/new string on warning cache misses.
- PDADataLogTab still concatenated several cold display strings unrelated to the Betty/subtitle route.

What was done:
- BaseIntegrityHUD now registers the stable notification format key instead of materializing a per-percent message string.
- Removed debug interpolation and exception-message materialization in SettingsManager, SettingsPanelProfiler, SubnauticaSystemsDebugUI, PDAIntrusionManager, PauseMenuController, PauseControlsPanel, TopographicalSonarSynthesizer, MetaCampaignService, CorporateOrderSystem, and LoreDatabaseManager.
- Reduced PDADataLogTab cold concat usage for row names, author lines, summary prefix returns, and empty-state text.
- Re-scanned owned X_011 route: forbidden total remains 0.
- Re-scanned broad runtime Audio/UI/Narrative/Localization: timing/coroutine hits remain 0, exception-message hits now 0.

Cinematic Cheats used:
- Percent warning text now favors deterministic stable warning identity over runtime-composed percentage prose. The severity and event route remain intact; the presentation string no longer spends allocation budget.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed.
- Static broad residual after sweep: ToString 1, StringBuilder 11, direct text false-positive 1, SetText 52, string.Concat 8, interpolation 10, new string 21, string.Create 3.
- Build not rerun: guard observed CPU=100.00% with 9 compiler processes, then waited 6 minutes; 7 dotnet processes remained active, so no overlapping build was launched.

Residual risk:
- Remaining broad string-risk hits are cold UI construction/caches or editor-only lore rebake, not Betty/subtitle hot lane.
- Whole-project UI/text zero-GC remains false until ModalWindow, PDA corruption surface keys, HectonOSBootManager, PDADeathMemoryDump, SettingsPanel caches, and remaining legacy panels get owner-led span-buffer APIs.

## 2026-05-23 - T.A.R.S. Modal Buffer Closure And Runtime String Tightening

What was wrong:
- PauseControlsPanel assembled conflict modal text in a char buffer, then materialized it with `new string(...)` because ModalWindow only exposed a string message API.
- SettingsPanel numeric display caches still used string.Create; UISliderValueDisplay still used string.Concat for template/suffix composition.
- Selected runtime audio/text diagnostics still had interpolation/ToString/string concatenation in AudioLogSystem, HectonNarrativeDirector, UIRuntimeSmokeTester, and ContentLoreBinaryProvider.

What was done:
- Added ModalWindow.Show(string, char[], int, Action, Action) and a char-buffer ShowInternal path.
- PauseControlsPanel now passes the pooled modal message buffer directly; ModalWindow renders it with TMP SetCharArray.
- SettingsPanel numeric labels now use cold CachedTextLabel char buffers and TMP SetCharArray.
- UISliderValueDisplay template cache now uses char[] and ReadOnlySpan<char>, not string.Concat.
- Collapsed selected runtime diagnostic interpolation/ToString/concat paths to constant messages.
- Re-scanned owned X_011 route: 0 forbidden hits.
- Re-scanned adjacent audio/text timing perimeter: 0 Time.frameCount, 0 coroutine timing, 0 exception-message hits.
- Re-scanned patched UI/modal target files: 0 string.Concat, 0 string.Create, 0 ToString, 0 interpolation, 0 new string, 0 exception-message hits.

Cinematic Cheats used:
- Modal conflict text now remains a transient char-buffer payload instead of becoming a managed object.
- Numeric settings/slider presentation uses precomputed buffer views; no formatting work is spent during refresh.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed.
- Static runtime broad scan excluding Editor: Time.frameCount 0, coroutine timing 0, exception-message 0, ToString 2, StringBuilder 15, direct .text assignment 1, SetText 50, string.Concat 7, interpolation 10, new string 4, string.Create 0.
- Build not launched: final 3-minute guard loop saw CPU 57.00-100.00% and active dotnet/csc process count 0 rising to 7-9, then ending at 7; project rule forbids dotnet build while CPU >50% or another compiler process is active.

Residual risk:
- Whole-project UI/text zero-GC remains false. Remaining non-editor runtime string risks are outside Betty/subtitle route: AudioLog markup stripping, PDA exchange save summary, HudNumericStringCache cold cache, PDAControlsRebindUI cold hierarchy naming/lookups, PDADataLogTab cached surface keys, and PDA shell/death/debug dump string-return APIs.
- Unity profiler/GCMonitor/player proof remains absent; static clean route is not a measured 0 B GC claim.

## 2026-05-23 - T.A.R.S. Residual Runtime String Sweep

What was wrong:
- Runtime audio/UI/text scan still had allocation-prone sites outside Betty/subtitle: HectonMusicDirector debug reason concat, AudioLogPickup prompt concat, AudioLogSystem notification concat, ContentLoreBinaryProvider diagnostic concat, HudNumericStringCache cold `new string`, PDAControlsRebindUI interpolation, PDAConstructionTab/PDALoadoutTab hierarchy-name concat, SubnauticaSystemsDebugUI snapshot interpolation/new string, PDAShellChrome template/material-name strings, and SuitHUDV4CanvasOverlay cold hierarchy-name concat.

What was done:
- HudNumericStringCache now stores `char[][]` and exposes `ReadOnlySpan<char>` via `GetIntSpan`; PDASpectrumTab appends spans into its line buffer.
- AudioLogPickup no longer composes title-bearing interact strings; AudioLogSystem uses the stable fallback discovery notification.
- HectonMusicDirector, ContentLoreBinaryProvider, PlayerPDA, PauseMenuController, FontAssetRecovery, RelayHUDRuntimeBootstrap, SubnauticaSystemsDebugUI, PDAShellChrome, PDAControlsRebindUI, PDAConstructionTab, PDALoadoutTab, and SuitHUDV4CanvasOverlay had safe concat/interpolation/new-string sites collapsed to constants or char-buffer/span routes.
- Re-scanned owned X_011 route: 0 forbidden hits.
- Re-scanned runtime audio/UI/text excluding Editor: Time.frameCount 0, coroutine timing 0, exception-message 0, interpolation 0, string.Create 0.

Cinematic Cheats used:
- Audio log discovery/interact text now favors stable notification/verb labels over per-log runtime-composed prose.
- Debug snapshots and hierarchy object names now use constants; no gameplay truth is carried by those strings.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed.
- Static runtime broad residual after this pass: ToString 3, StringBuilder 16, direct .text assignment 1, TMP SetText 50, string.Concat 7, new string 2, string.Create 0, interpolation 0.
- Build not launched: guard observed CPU=99.00% with 0 dotnet/csc processes; project rule forbids dotnet build while CPU >50%.

Residual risk:
- Whole-runtime audio/UI/text zero-GC remains false. Remaining real string-contract blockers: AudioLogData timecode stripping returns string, PDAExchangeSystem save summary returns string, SteamCloudSaveConflictResolver modal prompt returns string, PDADataLogTab lore surface keys use string identity, PDADeathMemoryDump and PDAShellChrome still contain string-return contract paths, and LocalizationManager legacy plural fallback composes `_OTHER`.
- Betty/subtitle hot route remains statically clean; broad legacy UI/text does not.
## 2026-05-23 - T.A.R.S. Final Residual Classification

What was wrong:
- Previous residual report was stale after PDAShellChrome and HudNumericStringCache patches.
- Raw scanner `.Message` hit was PauseMenuController payload.Message, not exception-message materialization.
- SystemDispatcher still has direct Time.frameCount reads as the core frame owner; this must not be misreported as a VWS/subtitle consumer violation.

What was done:
- Reran 163-file runtime audio/UI/text scan excluding Editor directories.
- Reran VWS/Babel/SubtitleManager consumer-route forbidden-token scan.
- Updated Status_X_011.md, Rationale_X_011.md, UX_ZERO_GC_AUDIT_X_011.json, and UX_OPTIMIZATION_REPORT_X_011.json with corrected counts.

Cinematic Cheats used:
- None in this delta. This was a static audit/report correction pass.

Exact Microseconds saved:
- 0 us claimed. No post-patch profiler or GCMonitor run completed.

Verification:
- VWS/subtitle consumer route: 0 forbidden static hits.
- Runtime audio/UI/text perimeter: Time.frameCount 0, coroutine timing 0, real exception-message materialization 0, interpolation 0, string.Create 0, new string 1, string.Concat 7, ToString 3, StringBuilder 16, direct .text assignment 1, TMP SetText 49.
- Raw StringPlus=27; manual classification leaves one real string `+` concat: LocalizationManager plural fallback.
- Remaining managed-string blockers outside Betty/subtitle: AudioLogData markup strip string return, PDAExchangeSystem save summary, SteamCloud modal prompt, PDADataLogTab lore surface-key strings, PDADeathMemoryDump cold line-library strings, LocalizationManager plural-key fallback.
- Build skipped by project guard: CPU=100.00%, dotnet/csc process count=9.

## 2026-05-24 - T.A.R.S. APEX Audio/Text Frame And String Sweep

What was wrong:
- The prior clean claim was too narrow. ModalWindow still had a nested `SetText` fallback, SaveSlotUI still formatted DateTime through `ToString("g")`, PDADataLogTab still used a string-return corruption path for row/detail lore text, and PDADeathMemoryDump retained an unused line-string staging allocator.
- The wider audio/UI/narrative/text filename scan found missed direct `Time.frameCount` reads in AcousticZoneController, SoundscapeSystem, LocalizationEvents, BabelDictionaryStore, QueryCacheContext, PlayerRuntimeContext, ContextualPhysicalIk bridge files, and an editor Narrative DAG inspector.
- ContentRuntimeServices diagnostics still had `Time.frameCount`, `ToString("X8")`, and `exception.Message` string construction in diagnostic paths.

What was done:
- Removed ModalWindow's local `SetText` helper; labels now route through shared `Hecton8.UI.TmpTextNoAlloc`.
- Added `TmpTextNoAlloc` span sink that copies caller spans through CharBufferPool leases and renders TMP via `SetCharArray`.
- SaveSlotUI now writes timestamps into fixed `char[32]` with manual digits, no DateTime.ToString.
- PDADataLogTab row/detail title/author/date now flow through `ReadOnlySpan<char>` and `TryApplyPdaLoreCorruptionIfNeeded` into caller-owned buffers.
- PDADeathMemoryDump dead line-string staging path was removed.
- ContentRuntimeServices now reads `SystemDispatcher.CurrentFrameIndex` and logs constant diagnostics instead of dynamic string composition.
- Acoustic/soundscape/localization/Babel/query/player-context/IK/narrative-inspector frame reads now use `SystemDispatcher.CurrentFrameIndex`.
- VWS priority highest-bit read now uses `math.select` over low/high 32-bit halves and final select for the zero-word case; Pop and expired-discard clear bits by mask AND.

Cinematic Cheats used:
- UI text presentation uses fixed char buffers and stable labels instead of runtime prose assembly.
- VWS uses a single priority bit word for five canonical alarms instead of heap scheduling.
- Content/audio timing consumers read one dispatcher frame owner instead of Unity global frame state.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch build/profiler/GCMonitor run completed.
- Static VWS/Babel/Subtitle route scan: 0 hits for Time.frameCount, WaitForSeconds, StartCoroutine, yield return, SetText, string.Concat, new string, string.Create, ToString, NativeMinHeap, VocalWarningHeapOps, BufferedSubtitleRequest, _stringQueue, _currentMessage, exception.Message, ex.Message.
- Static 235-file audio/UI/narrative/text matched scan: Time.frameCount 0, WaitForSeconds 0, StartCoroutine 0, yield return 0, SetText 0, string.Concat 0, new string 0, exception-message construction 0.
- `git diff --check` on touched X_011 files returned no whitespace errors; only line-ending warnings.
- Build skipped by project guard: latest CPU LoadPercentage=94 and 7 active dotnet processes. Project rule forbids dotnet build while CPU >50% or another dotnet/csc process is active.

Residual risk:
- Full repository zero `Time.frameCount` is still false because many non-X_011 systems own frame telemetry and were not part of this route.
- Whole-project UI/text zero-GC is still false. Residual managed strings remain in editor/debug/save-identity surfaces such as LoreDatabaseManager editor seed rebake, PDALogbook debug strings, PDAExchange save summary, PDAMarker save id, diagnostics, and WAL/save diagnostics.
- Runtime 0 B/frame is not measured. Static clean route is not profiler proof.

## 2026-05-24 - Quest/PDA Timing And Debug String Closure

What was wrong:
- Expanded Audio/AudioLog/UI/Narrative/Quest/PDA/Core-Content/Localization scan still found direct `Time.frameCount` telemetry throttles in `QuestGraphEvaluator`, `QuestEvents`, and `MissionMarkerSystem`.
- `QuestStateManager` still materialized `exception.Message` in a quest audit append warning.
- `PDALogbookEntry.Title`, `Message`, and `OriginKey` still reconstructed debug strings through `ReadOnlySpan<char>.ToString()` and `int.ToString("X8")`.

What was done:
- Quest narrative/event/marker throttles now use `SystemDispatcher.CurrentFrameIndex`.
- Quest audit append warning is a constant log; no exception-message string composition remains in that target.
- PDALogbook legacy string properties now return `string.Empty`; non-allocating `GetTitleSpan`, `GetMessageSpan`, `TryGetTitleBuffer`, `TryGetMessageBuffer`, and `TryWriteOriginKey` remain the supported routes.
- Updated `Status_X_011.md`, `Rationale_X_011.md`, `UX_OPTIMIZATION_REPORT_X_011.json`, and `UX_ZERO_GC_AUDIT_X_011.json`.

Cinematic Cheats used:
- Debug string reconstruction was removed instead of reformatted. PDA/logbook presentation must use existing span/buffer surfaces.
- Quest timing now uses the same dispatcher-owned frame integer as audio/subtitle timing.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Target route scan: VWS/subtitle plus patched quest/PDA files returned 0 hits for `Time.frameCount`, coroutine timing, `SetText`, `string.Concat`, `new string`, `string.Create`, `ToString`, heap queue tokens, legacy subtitle queue tokens, `exception.Message`, and `ex.Message`.
- JSON reports parse through `ConvertFrom-Json`.
- `git diff --check` on the touched X_011 files returned no whitespace errors; only line-ending warnings.
- Build skipped by project guard: latest gate CPU LoadPercentage=79 with 8 `dotnet` processes, 1 `csc`, and 1 `VBCSCompiler` active.

Residual risk:
- Whole-project zero-GC remains false. Current expanded cold residuals outside Betty/subtitle: `PDAMarkerRegistry` save-id string creation, `QuestStateManager` compile/audit string contracts, `LoreDatabaseManager` editor seed rebake, `NarrativeDagInspectorWindow` editor-only formatting, save/WAL diagnostics, and non-X_011 owners.
- Runtime 0 B/frame is not measured. Static clean route is not profiler proof.

## 2026-05-24 - Main Menu Text Sink Closure

What was wrong:
- `MainMenuController` still called TMP `SetText` for loading percent text.
- `MainMenuController` still converted `FixedString128Bytes` save/load error text through `ToString`.
- `MainMenuController` still had explicit `string.Concat` fallback display strings.
- `DiegeticHudTextNode` exposed a span-only method named `SetText`, leaving a misleading forbidden token in UI scans.

What was done:
- Loading percent now formats into a persistent `char[32]` buffer and writes through `SetCharArray`.
- Save/load error UI uses the stable fallback message instead of materializing fixed-string payload text.
- Manual save slot display resolves to stable display constants and no longer calls `string.Concat`.
- `DiegeticHudTextNode.SetText(ReadOnlySpan<char>)` was renamed to `SetSpan(ReadOnlySpan<char>)`; scan found no call sites.

Cinematic Cheats used:
- Save slot modal fallback text now favors stable slot labels over localized runtime concatenation.
- Loading progress avoids TMP formatting entirely.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Patched target scan including MainMenuController and DiegeticHudTextNode: 0 hits for `Time.frameCount`, coroutine timing, `SetText`, `string.Concat`, `new string`, `string.Create`, `ToString`, `exception.Message`, and `ex.Message`.
- `git diff --check` on those two files returned no whitespace errors; only line-ending warnings.

Residual risk:
- Full UI zero-GC remains false outside the patched target route. Current expanded cold residuals are still save identity, quest compile/audit diagnostics, and editor-only formatting.
- Build remains pending under CPU/compiler guard: latest gate CPU LoadPercentage=79 with 8 `dotnet` processes, 1 `csc`, and 1 `VBCSCompiler` active.

## 2026-05-24 - Main Menu Fallback Interpolation Closure

What was wrong:
- `MainMenuController` still had interpolated fallback strings for missing save files, scene-load failure, backup recovery, and save/load errors.
- `MainMenuController` still had dynamic save/load development logs that resolved slot names and error strings only for logging.

What was done:
- Collapsed those fallback strings and dev logs to stable constant messages.
- Re-ran selected non-editor timing/coroutine/text-sink scans across Audio, AudioLog, UI, Narrative, Quest, PDA, Core/Content, MainMenu, ModalWindow, SaveSlotUI, SteamCloud conflict UI, LocalizationManager, LocRegistry, and LocalizationEvents.
- Updated `Status_X_011.md`, `Rationale_X_011.md`, `UX_OPTIMIZATION_REPORT_X_011.json`, and `UX_ZERO_GC_AUDIT_X_011.json`.

Cinematic Cheats used:
- Fallback UI uses constant failure wording instead of runtime dynamic prose. Localized paths remain richer when the localization service is available.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- `MainMenuController` scan: 0 hits for interpolation, `string.Concat`, `new string`, `string.Create`, `ToString`, `SetText`, `exception.Message`, and `ex.Message`.
- Selected non-editor timing/coroutine scan: 0 hits for `Time.frameCount`, `WaitForSeconds`, `StartCoroutine`, `yield return`, `NativeMinHeap`, `VocalWarningHeapOps`, legacy subtitle queue tokens, and direct `.text =`.
- Remaining selected string tokens are outside the Betty/subtitle render route: `QuestStateManager` compile/audit labels and `PDAMarkerRegistry` save-id `string.Create`; `LoreDatabaseManager` and `NarrativeDagInspectorWindow` formatting is editor-gated.
- Build skipped by project guard: latest gate CPU LoadPercentage=85 with no active `dotnet`, `csc`, or `VBCSCompiler` process; CPU still exceeded the 50% limit.

Residual risk:
- Runtime 0 B/frame is still unmeasured. Static clean scans are not profiler proof.
- Whole-project UI/text zero-GC remains false because save identity, quest diagnostics, editor authoring, and cross-owner APIs still use managed strings.

## 2026-05-24 - Wrist HUD Legacy IO Fence

What was wrong:
- `WristHologramHudRuntime` did not contain a managed coroutine, but its font-atlas boot path could still enter Docs/Archive legacy font discovery and file reads from player runtime.
- The old source used explicit managed file enumeration for legacy font metrics discovery. That is not acceptable in the production UI text boot path.

What was done:
- `TryLoadLegacyFontMetrics`, first-file discovery, binary file reads, and CSV parsing are now editor-only behind `#if UNITY_EDITOR`.
- Player runtime keeps the generated font atlas path and no longer calls the legacy Docs/Archive discovery path from `EnsureNativeBuffers`.
- Removed the explicit `Directory.EnumerateFiles(...).GetEnumerator()` pattern before fencing the legacy loader.

Cinematic Cheats used:
- Runtime text atlas fallback now uses deterministic generated glyph metrics instead of filesystem archaeology.
- Editor tooling remains available for authoring CSV/font overrides without shipping that work into player startup.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Selected non-editor Audio/UI/PDA/AudioLog/Narrative/MainMenu/Modal/SaveSlot scan: 0 hits for `Time.frameCount`, `WaitForSeconds`, `StartCoroutine`, `yield return`, `IEnumerator`, direct `.text =`, `SetText`, `string.Concat`, interpolation, `new string`, `string.Create`, `ToString`, `exception.Message`, and `ex.Message`.
- `git diff --check` on `WristHologramHudRuntime.cs` returned no whitespace errors; only the existing LF-to-CRLF warning.
- Build skipped by project guard: latest CPU LoadPercentage=99 with no active `dotnet`, `csc`, or `VBCSCompiler` process.

Residual risk:
- Runtime 0 B/frame is still unmeasured. Static clean scans are not profiler proof.
- Raw file IO remains in editor tooling, blackbox dumps, and non-X_011 data streamer owners. That is not claimed clean.

## 2026-05-24 - Boot And Death Dump Text Builder Removal

What was wrong:
- `HectonOSBootManager` used a managed `StringBuilder` to assemble boot-console text before copying to a TMP char buffer.
- `PDADeathMemoryDump` used a managed `StringBuilder` to assemble fatal-pressure dump text and cold line-library entries.
- Both were presentation-layer text builders. Cold or rare does not make them zero-GC proof.

What was done:
- `HectonOSBootManager` now writes directly into `_sequencePayloadBuffer` through `BootTextWriter`, formats numbers manually, pulls localized labels through `GetRawSpanOrFallback`, and renders with `SetCharArray`.
- `PDADeathMemoryDump` now writes directly into `_dumpPayloadBuffer` through `DumpTextWriter`, formats hex/int values manually, tracks reveal thresholds from writer length, and renders with `SetCharArray`.
- The final death-dump localized line now uses `GetRawSpanOrFallback` instead of string-return localization.

Cinematic Cheats used:
- Boot and death overlays keep the same visual payload, but string assembly is reduced to fixed-buffer writes.
- Cold line-library entries remain reusable char arrays rather than dynamic text construction during death playback.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Selected non-editor Audio/UI/PDA/AudioLog/Narrative/MainMenu/Modal/SaveSlot scan: 0 hits for `Time.frameCount`, coroutine tokens, `IEnumerator`, direct `.text =`, `SetText`, `string.Concat`, interpolation, `new string`, `string.Create`, `ToString`, `exception.Message`, `ex.Message`, `StringBuilder`, `string.Format`, `AppendFormat`, and `Regex.Replace`.
- `git diff --check` on `HectonOSBootManager.cs`, `PDADeathMemoryDump.cs`, and `WristHologramHudRuntime.cs` returned no whitespace errors; only existing LF-to-CRLF warnings.
- Build skipped by project guard: latest CPU LoadPercentage=93 with no active `dotnet`, `csc`, or `VBCSCompiler` process.

Residual risk:
- Runtime 0 B/frame is still unmeasured. Static clean scans are not profiler proof.
- Cold `char[]` line-library allocation remains in `PDADeathMemoryDump`; it is prebuilt reusable payload storage, not a per-frame/dialogue builder.

## 2026-05-24 - Quest CSV, Corporate Slow Tick, AudioLog Enumerator Closure

What was wrong:
- `QuestDagCsvOverrideIngestor` used a full-file managed CSV string before parsing override rows.
- `CorporateOrderSystem.SlowTick` copied pending dictionary keys into growable `List<string>` buffers before decrementing timers and delivering orders.
- `AudioLogSystem` exposed an unused public `HashSet<uint>.Enumerator` method. The enumerator was a struct, but the API invited runtime hash-set walking.

What was done:
- Quest override CSV now reads into a fixed `char[64k]` scratch buffer and passes `ReadOnlySpan<char>` to the existing row parser.
- Corporate order slow tick now scans the authored `CorporateOrder[]` and updates/removes dictionary entries directly; key-buffer and delivery-buffer lists were removed.
- Deleted `AudioLogSystem.GetDiscoveredHashEnumerator`; repository search found no call sites.

Cinematic Cheats used:
- Corporate order notification timing uses authored order order as the deterministic scan surface instead of runtime key copying.
- Quest CSV remains editor/cold tooling; player dialogue/VWS routes do not ingest full-file strings.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Selected non-editor Audio/UI/PDA/AudioLog/Narrative/Quest/Core/Content/MainMenu/SteamCloud/Localization scan: 0 hits for `Time.frameCount`, `WaitForSeconds`, `StartCoroutine`, `yield return`, `IEnumerator`, direct `.text =`, `SetText`, `string.Concat`, interpolation, `new string`, `string.Create`, `ToString`, `exception.Message`, `ex.Message`, `StringBuilder`, `string.Format`, `AppendFormat`, and `Regex.Replace`.
- Raw string `+` concat scans over the same selected scope returned 0 hits.
- File-IO/GetEnumerator residuals in the selected scope are editor-only authoring tools: Lore/Narrative inspector line reads and Wrist HUD legacy font discovery behind `UNITY_EDITOR`.
- Build skipped by project guard: CPU LoadPercentage=83 after a 20-second cooldown with no active `dotnet`, `csc`, or `VBCSCompiler` process; CPU still exceeded the 50% limit.

Residual risk:
- Runtime 0 B/frame is still unmeasured. Static clean scans are not profiler proof.
- Full project still has unrelated `Time.frameCount` and async wait tokens in non-X_011 domains; those are not claimed clean.

## 2026-05-24 - CharBufferPool Registry Probe Closure

What was wrong:
- `CharBufferPool.GetBabelSpan` could retry `GlobalRegistry.DataVault` lookup every subtitle when the native Babel arena was not bound.
- That is not necessarily a heap allocation, but it is hot registry polling in a text read route.

What was done:
- Added `s_babelArenaProbeCompleted`.
- Normal subtitle span access now probes the DataVault arena once and then stays on the preallocated TMP bridge fallback.
- `Prewarm()` remains the explicit cold retry point for late native arena binding.

Cinematic Cheats used:
- Low tier uses preallocated char[] TMP bridge buffers without registry archaeology.
- Higher tiers can still bind the native Babel arena during cold prewarm and spend saved stability on richer subtitle treatment.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- `CharBufferPool.cs` diff-check returned no whitespace errors; only the existing LF-to-CRLF warning.
- Selected non-editor Audio/UI/PDA/AudioLog/Narrative/Quest/Core/Content/MainMenu/SteamCloud/Localization scan remained 0 hits for timing, coroutine, TMP string sink, managed string materialization, and raw string-plus concat tokens.
- Build skipped by project guard: CPU LoadPercentage=68 with active `dotnet`, `csc`, and `VBCSCompiler` processes.

Residual risk:
- TMP still requires the final pooled managed `char[]` bridge for `SetCharArray`. This is zero per-dialogue allocation after prewarm, but it is not direct native span rendering into TMP.

## 2026-05-24 - Bootstrap/Fabricator Timing And Interaction Prompt Span Route

What was wrong:
- Bootstrap/fabricator/terminal presentation code still had direct `Time.frameCount`, `Awaitable.WaitForSecondsAsync`, `UnityEngine.UI.Text.text`, boot text builders, and exception-message/string-plus formatting in the audited route.
- Fabricator interaction prompt output still materialized a managed string from a fixed char buffer because `IInteractable.GetInteractText()` is a string contract.

What was done:
- Patched `GameBootstrapper`, `BootstrapEvents`, `HectonFabricatorUI`, `Fabricator`, and `MessageTerminal` to use dispatcher-owned frame ids, TMP/`TmpTextNoAlloc`, stable boot/fatal messages, and next-frame delay loops instead of wait-for-seconds helpers.
- Added `IInteractableTextProvider.TryCopyInteractText(Span<char>, out int)`.
- `Fabricator` now serves its localized prompt from a fixed `char[96]` buffer; both interaction UI routes and `PlayerInteraction` consume the span route first.
- `PlayerLookTargetPromptCache` can now store `ReadOnlySpan<char>` directly, so look-target tooltip prompt hashing/cache fill no longer needs a managed prompt string for migrated providers.

Cinematic Cheats used:
- Fabricator prompt correctness is preserved through fixed text staging, not runtime string identity.
- Legacy interactables keep cached-string fallback until their owners migrate; no fake full-project zero-GC claim.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Target scans over Bootstrap/Fabricator/Terminal and Interaction/Fabricator prompt-provider files report 0 hits for `Time.frameCount`, `WaitForSeconds`, coroutine tokens, direct `.text =`, `SetText`, `StringBuilder`, `ToString`, `exception.Message`, `ex.Message`, `string.Concat`, `new string`, `string.Create`, `string.Format`, `Regex.Replace`, and raw string-plus concat.
- `git diff --check` on the patched prompt-provider files returned no whitespace errors; only existing LF-to-CRLF warnings.
- Build skipped by project guard: latest gate saw CPU LoadPercentage=100 with active `csc` and `dotnet`.

Residual risk:
- Runtime 0 B/frame is still unmeasured. Static clean scans are not profiler proof.
- Non-migrated `IInteractable.GetInteractText()` providers still return cached strings. Fabricator is clean; whole-project interact prompts are not yet fully migrated.

## 2026-05-24 - Interaction Provider Expansion And Timing Residue Closure

What was wrong:
- Runtime interaction owners still depended on the legacy `IInteractable.GetInteractText()` string contract even after Fabricator gained a span route.
- `NarrativeDiscovery` assembled prompt text with managed string concatenation.
- `HarvestableOutcrop`, `InteractionEvents`, and `PhysicalSnapSwitch` still read `Time.frameCount` in interaction-adjacent runtime logic.

What was done:
- Migrated the remaining found runtime `IInteractable` owners to `IInteractableTextProvider`.
- `NarrativeDiscovery` now writes prompt text into a fixed `char[128]` buffer and exposes it through `TryCopyInteractText`.
- `UI.InteractionUI`, `InteractionUI`, and `PlayerInteraction` consume provider spans for prompt display/hash routes and no longer need the legacy string fallback for migrated owners.
- Item pickup and Hecton item quantity prompts now use manual digit writing through `InteractableTextCopy.TryCopyWithQuantity`.
- Replaced the remaining interaction-adjacent `Time.frameCount` reads with `SystemDispatcher.CurrentFrameIndex`.

Cinematic Cheats used:
- Prompt identity is treated as copied text payload, not managed string object identity.
- Quantity suffix is written as digits into the caller buffer instead of using `ToString`, interpolation, or `+ " x" + quantity`.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- 26 runtime files changed in this package; `git diff --check` returned no whitespace errors, only existing LF-to-CRLF warnings.
- All found runtime `IInteractable` class declarations now include `IInteractableTextProvider`.
- Selected non-editor audio/UI/narrative/interaction timing scan: 0 hits for `Time.frameCount`, `WaitForSeconds`, `WaitForSecondsRealtime`, `StartCoroutine`, `yield return`, and runtime `IEnumerator`.
- Patched prompt-route scan: 0 hits for raw string-plus concat, direct `.text =`, `SetText`, `ToString`, `new string`, `string.Concat`, `string.Format`, `StringBuilder`, `Regex.Replace`, `exception.Message`, and `ex.Message`.
- Build skipped by project guard: CPU LoadPercentage=100 with no active `dotnet`, `csc`, or `VBCSCompiler`; CPU still exceeded the 50% limit.

Residual risk:
- `DeployableBeacon.CreateDeterministicBeaconId()` still uses `string.Create` for a cold save/registry ID if serialized `beaconId` is empty. This is outside Betty/subtitle/prompt hot rendering and needs a beacon registry/network fixed-string migration, not a local fake.
- `LocalizationManager.ExpandText()` still uses managed `Regex.Replace`. Migrated prompt paths avoid it, but whole-project text expansion is not zero-GC.
- Runtime 0 B/frame remains unmeasured until Unity profiler/GCMonitor can run after a successful compile.

## 2026-05-24 - Localization Token Expander And Quest Diagnostic String Closure

What was wrong:
- `LocalizationManager` legacy runtime token expansion used compiled Regex, MatchEvaluator delegates, and Regex.Replace over managed strings.
- `LocalizedInlineIconResolver` normalized tokens with managed string trim/lowercase paths and could concatenate markup/display names.
- `QuestStateManager` compile/audit diagnostics still composed managed strings with `string.Concat` and `string.Create`.

What was done:
- Removed Regex and MatchEvaluator token expansion from `LocalizationManager`.
- Added `TryExpandText(ReadOnlySpan<char>, char[], out int)` and `TryExpandNarrativeText(...)` for button/item/status/key/tech token expansion into caller-owned buffers.
- `UI.InteractionUI.UpdatePrompt` now calls the buffer expander and renders through TMP `SetCharArray`.
- `LocalizedInlineIconResolver` now resolves item/status tokens from spans and exposes a char-buffer item-display writer.
- `QuestStateManager` dynamic compile/audit labels were collapsed to stable constants.

Cinematic Cheats used:
- Runtime localization richness now rides on fixed buffers and predeclared token chips instead of Regex.
- Quest diagnostics trade verbose generated strings for deterministic constants; detailed compile context must come from structured hashes/log route, not managed text assembly.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Patched target scan over localization/inline-icon/UI/quest files reports 0 hits for `Regex`, `MatchEvaluator`, `Regex.Replace`, `StringBuilder`, `string.Concat`, `new string`, `string.Create`, `string.Format`, `AppendFormat`, `ToString`, `SetText`, direct `.text =`, `exception.Message`, and `ex.Message`.
- Expanded non-editor audio/UI/narrative/PDA/quest/interaction timing scan reports 0 hits for `Time.frameCount`, `WaitForSeconds`, `WaitForSecondsRealtime`, `StartCoroutine`, `yield return`, and runtime `IEnumerator`.
- `git diff --check` on the patched localization/inline-icon/UI/quest files returned no whitespace errors, only existing LF-to-CRLF warnings.
- `dotnet build Assembly-CSharp.csproj` passed after CPU/compiler guard cleared: 0 warnings, 0 errors, elapsed 00:02:50.13.

Residual risk:
- Legacy string `ExpandText` / `ExpandNarrativeText` no longer expands inline runtime tokens; callers that still depend on string-return token expansion will see raw token text until migrated to the buffer APIs.
- `PDAMarkerRegistry.BuildNextMarkerId()` still uses `string.Create` for save identity. That is outside Betty/subtitle/prompt rendering and needs a save-contract migration, not an X_011 text patch.
- `LoreDatabaseManager` and `NarrativeDagInspectorWindow` raw string hits are editor-gated authoring code.

## 2026-05-24 - Manta/Consumable Text Sweep And Presentation Frame Recheck

What was wrong:
- `ConsumableItem` used a cached `Dictionary<ItemData,string>` plus `StringBuilder.ToString()` for effect descriptions.
- `MantaScooter` kept managed localized warning/summary/directive strings and previously generated percent summary variants with `string.Create`.
- Selected presentation/narrative telemetry files still had direct `Time.frameCount` reads adjacent to text/presentation state.

What was done:
- `ConsumableItem` now exposes `TryWriteEffectDescription(ItemData, Span<char>, out int)` and writes `+value label` segments into caller memory with `TryFormat`.
- `MantaScooter` now stores localized labels/templates in `FixedCharBuffer`, publishes tool warnings from `ReadOnlySpan<char>`, and expands `{0}` battery percent directly into the output buffer.
- Replaced selected presentation/narrative `Time.frameCount` reads in `EndingSystem`, `FirstHourDirector`, `PlayerSwimPresentationController`, `HectonPlayerCameraRig`, `HectonScanRenderRegistry`, and `DataArchaeologyRuntime` with `SystemDispatcher.CurrentFrameIndex`.

Cinematic Cheats used:
- Manta HUD text is treated as a fixed staged payload, not a managed string identity.
- Consumable tooltip detail is written into caller buffers; legacy string API is fenced as an obsolete empty bridge instead of silently allocating.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Targeted scan over Manta/Consumable and the six selected presentation files reports 0 hits for `Time.frameCount`, `WaitForSeconds`, `StartCoroutine`, `yield return`, `IEnumerator`, `.text =`, `SetText`, `ToString`, `new string`, `string.Create`, `string.Concat`, `StringBuilder`, `string.Format`, `exception.Message`, and `ex.Message`.
- Selected Audio/AudioLog/UI/Narrative/PDA/Quest/Interaction/Manta/Consumable scan reports one non-editor managed string constructor: `PDAMarkerRegistry.BuildNextMarkerId()` `string.Create`, classified as save/registry identity outside subtitle/VWS/prompt render.
- Build skipped by project guard after this sweep: CPU LoadPercentage=100 with 9 active `dotnet`/`csc` processes.

Residual risk:
- Current post-Manta patch is not compile-verified. Last successful compile predates this sweep.
- `PDAMarkerRegistry` and beacon save/registry string IDs remain managed by design until a save-contract migration exists.
- Full-project zero-GC remains false; the owned VWS/subtitle/prompt/Manta/consumable selected routes are static-clean only.

## 2026-05-24 - AudioLog Subtitle Span Preview

What was wrong:
- `AudioLogData.TryWriteVisibleSubtitleOrFallback` still resolved audio-log subtitle content through `SubtitleOrFallback` as a managed string before copying into the PDA preview buffer.
- `HasSubtitleText` and `HasArchiveSummary` also used string-return properties for visibility checks.

What was done:
- Added `LocalizedTextReference.ResolveSpanOrFallback`, `TryCopyResolvedOrFallback`, and `HasResolvedOrFallbackText`.
- `AudioLogData.HasSubtitleText` and `HasArchiveSummary` now check visible text through span resolution.
- `TryWriteVisibleSubtitleOrFallback` now resolves subtitle content as `ReadOnlySpan<char>`, expands tokens into the caller buffer, and strips `[time]` markers through a span overload.

Cinematic Cheats used:
- Audio-log preview treats localized text as copied payload, not string identity.
- Legacy string properties remain for older PDA/quest/codex callers; the subtitle preview route no longer depends on them.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No post-patch profiler or GCMonitor run completed.
- Targeted scan over `AudioLogData.cs`, `LocalizedTextReference.cs`, `MantaScooter.cs`, and `ConsumableItem.cs` reports 0 hits for `ToString`, `new string`, `StringBuilder`, `string.Create`, `string.Concat`, `Regex.Replace`, `MatchEvaluator`, `SetText`, `.text =`, `exception.Message`, and `ex.Message`.
- Build skipped by project guard after this sweep: CPU LoadPercentage=100 with 9 active `dotnet`/`csc` processes.

Residual risk:
- Current post-AudioLog patch is not compile-verified.
- Legacy `LocalizedTextReference.ResolveOrFallback` string APIs remain for compatibility; only migrated buffer/span consumers are clean.

## 2026-05-24 - PDA AudioLog Archive Span Buffer Closure

What was wrong:
- PDA audio-log rows/details still consumed `DisplayTitleOrFallback`, `AuthorOrFallback`, `RecordDateOrFallback`, and `ArchiveSummaryOrFallback` string properties.
- Summary decrypt presentation stored `_resolvedSummaryBaseText` as a managed string.
- Localization inline item/status chip expansion still routed constant markup through `out string`.

What was done:
- Added `AudioLogData.TryWriteDisplayTitleOrFallback`, `TryWriteAuthorOrFallback`, `TryWriteArchiveSummaryOrFallback`, and `TryWriteRecordDateOrFallback`.
- `PDADataLogTab` now renders audio-log title/author/date/summary from caller-owned char buffers, stores summary decrypt base text in `_resolvedSummaryBaseBuffer`, and builds decrypt hex text from `ReadOnlySpan<char>`.
- Removed `_catalogAuthorLines` and `_catalogSummaryLines` string caches from PDA audio-log cache rebuild.
- Added span-return chip APIs in `LocalizedInlineIconResolver`; `LocalizationManager.TryExpandText` now appends chip spans directly.

Cinematic Cheats used:
- Archive display text is treated as staged payload in fixed buffers, not identity strings.
- Save/registry IDs remain outside this patch; breaking them would be fake optimization.

Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. No Unity profiler or GCMonitor run completed.
- Targeted scan over `PDADataLogTab.cs`, `AudioLogData.cs`, `LocalizedTextReference.cs`, `LocalizedInlineIconResolver.cs`, and `LocalizationManager.cs` reports 0 hits for `ToString`, `new string`, `StringBuilder`, `string.Create`, `string.Concat`, `Regex`, `MatchEvaluator`, `SetText`, direct `.text`, and exception-message tokens.
- Expanded selected audio/UI/PDA/narrative/quest/interaction scan still reports only `PDAMarkerRegistry.BuildNextMarkerId()` cold save/registry ID `string.Create` and editor-gated `NarrativeDagInspectorWindow` formatting.
- Build passed after guard cleared: `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`, 0 warnings, 0 errors, elapsed 00:03:34.55.

Residual risk:
- Legacy string properties on `AudioLogData` still exist for external compatibility. Migrated PDA archive display no longer uses them.
- `StripTimecodedSubtitleMarkup(string)` is a compatibility stub; the functional no-allocation route is `TryStripTimecodedSubtitleMarkup(ReadOnlySpan<char>, char[], out int)` / `TryWriteVisibleSubtitleOrFallback`.

## 2026-05-24 - Guarded Build Proof

What was wrong:
- Latest static-clean code was not compile-verified after the Manta/Consumable/AudioLog/PDA archive sweeps.
- CPU/compiler guard blocked immediate builds for several minutes.

What was done:
- Ran a 32-attempt guarded build loop.
- Attempts 1-15 were skipped due CPU 58-100 or active `dotnet/csc/VBCSCompiler`.
- Attempt 16 had `CpuLoad=15`, `CompilerProcessCount=0`; build executed.

Cinematic Cheats used:
- None. This is a proof gate, not runtime presentation work.

Exact Microseconds saved:
- 0 us claimed.
- Build result: 0 warnings, 0 errors, elapsed 00:03:34.55.

Residual risk:
- Unity Play Mode, player build, runtime profiler, and GCMonitor have not been run.

## 2026-05-24 - Notification Span Registry And Residual Text Closure

What was wrong:
- Quest objective notifications, suit upgrade notifications, SaveStation prompt text, InteractionUI fallback prompt templates, and LocalizationManager plural keys still had managed string assembly in the broader audio/UI/text perimeter.
- SubtitleManager consumed notification text through `TryResolveMessage(... out string)`, which kept the notification subtitle path on the wrong ownership boundary.

What was done:
- Added fixed-storage `NotificationEvents.RegisterMessage(ReadOnlySpan<char>)` and `TryResolveMessageSpan`.
- Routed SubtitleManager and HUDNotification notification display through span resolution.
- Rebuilt quest and suit notification registration with caller-owned char buffers.
- Replaced SaveStation interact text concat with fixed-buffer `IInteractableTextProvider`.
- Removed InteractionUI `inputPrefix + "{0} {1}"` fallback construction.
- Replaced plural-key concatenation with `TryCopyPlural` using stackalloc key assembly and raw Babel lookup.

Cinematic Cheats used:
- Notification payload remains a 32-bit hash; text richness is bought later at display time from fixed buffers instead of shipping managed strings through the hot lane.

Exact Microseconds saved:
- 0 us measured; profiler not run.
- Static result: patched target scan 0 hits for timing/coroutine/TMP string sink/managed string materialization/exception-message/VWS heap/legacy subtitle queue tokens.
- Guarded build result: skipped. Attempts 1-12 stayed CPU 80-100 with 0 compiler processes, so project rule blocked `dotnet build`.

Residual risk:
- Fresh compile after this patch is pending.
- Remaining real selected-scan materialization: editor-only Lore/Narrative inspector formatting plus `PDAMarkerRegistry` save ID `string.Create`, outside VWS/subtitle/prompt render ownership.

## 2026-05-24 - Progression Notification Residue Closure

What was wrong:
- Achievement notifications/logbook hashes built text through string concatenation.
- Contextual PDA advisories registered localized strings through legacy string APIs.
- Achievement telemetry throttling still read `Time.frameCount` directly.

What was done:
- `PlayerAchievementRegistry` now stages achievement notification/logbook text in fixed char buffers, hashes spans, and registers notification spans.
- `PDAContextualAdvisorySystem` now resolves advisory text as `ReadOnlySpan<char>` and publishes unregistered fallback advisories through span-registered notification hashes.
- Achievement telemetry frame reads now use `SystemDispatcher.CurrentFrameIndex`.

Cinematic Cheats used:
- None. This is notification plumbing cleanup: hash payload stays unchanged, message body stays in fixed buffers.

Exact Microseconds saved:
- 0 us measured.
- Static result: selected Audio/AudioLog/UI/Narrative/Quest/PDA/Interaction/Progression/Localization scan has TimeFrame 0, Coroutine 0, NotificationConcat 0, VwsHeap 0, ExceptionMessage 0.
- Compile result: pending; latest build guard had CPU 100 and 2 compiler processes.

Residual risk:
- Fresh compile and Unity profiler/GCMonitor still pending.

## 2026-05-24 - Advisory Legacy String Dead Path Removal

What was wrong:
- `PDAContextualAdvisorySystem` still contained an unused private legacy string localization helper after the span migration.

What was done:
- Removed the dead helper.
- Reran targeted progression forbidden-token scan: no timing/coroutine/string-materialization/TMP string sink/exception-message/notification-concat hits.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- 0 us measured.
- Guarded build retry skipped: attempts 1-8 saw CPU 86-100 and compiler process count 0-2.

Residual risk:
- Fresh compile and Unity profiler/GCMonitor still pending.

## 2026-05-24 - Span Notification Exactness

What was wrong:
- `NotificationEvents.RegisterMessage(ReadOnlySpan<char>)` could hash a full caller span but store only the first 512 chars.
- Same-hash different-span registration was not safely representable because the runtime notification payload carries only the hash.
- An earlier all-script scan command hit the Windows argument-length limit, and a selected-file regex accidentally included Fluid files via `ui` inside `fluid`.

What was done:
- Span notification registration now refuses bodies longer than the fixed slot.
- Same-hash different-span registration now fails fast with hash 0.
- Re-ran scoped non-editor audio/text scans using path roots instead of giant argument lists.

Cinematic Cheats used:
- Fixed 512-char notification slots keep the payload as a hash and avoid heap growth; longer prose must be authored/baked differently, not allocated at runtime.

Exact Microseconds saved:
- 0 us measured.
- Static result: `NotificationEvents.cs` target scan has 0 forbidden hits.
- Scoped non-editor audio/text perimeter: TimeFrame 0, Coroutine 0, TMP string sink 0, ExceptionMessage 0, NotificationConcat 0, VwsHeap 0.

Residual risk:
- Fresh compile after loops 31-34 remains pending.
- Build guard skipped at CPU 62 and then CPU 100 with no compiler processes.
- Remaining scoped materialization hits are editor Lore/Narrative formatting and `PDAMarkerRegistry` save-id `string.Create`, outside VWS/subtitle render ownership.

## 2026-05-24 - Post-Notification Compile Proof

What was wrong:
- Loops 31-34 changed runtime code after the previous successful build.

What was done:
- Ran guarded build wait.
- Attempts 1-2 skipped: CPU 100 and 12 compiler processes.
- Attempt 3 passed guard: CPU 45 and 0 compiler processes.
- Executed `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.

Cinematic Cheats used:
- None. Proof gate only.

Exact Microseconds saved:
- 0 us measured.
- Build result: 0 warnings, 0 errors, elapsed 00:05:32.03.

Residual risk:
- Unity Play Mode, runtime profiler, and GCMonitor proof still not executed in this CLI session.

## 2026-05-24 - Hidden Allocation Backstop Sweep

What was wrong:
- PDA marker title creation used `Trim()`.
- Player critical procedural audio reverb mixer binding used `Trim()`.
- LocalizationManager still carried a dead `Replace/Trim` helper.
- PDA shell intrusion hint cache used `Substring()`.
- Pause menu uppercased language labels by allocating cached strings.
- Quest/interaction diagnostics still had interpolation or literal string concat in selected presentation-adjacent files.

What was done:
- Replaced PDA marker title normalization with fallback-or-original string ownership.
- Replaced mixer parameter trimming with no-alloc validation: whitespace-padded names fail instead of allocating a cleaned name.
- Deleted the unused localization normalizer.
- Cached PDA shell intrusion hint prefix/suffix in fixed char buffers.
- Uppercased pause language labels directly into the caller char buffer.
- Collapsed selected debug string formatting to constants.

Cinematic Cheats used:
- Fixed char prefix/suffix caches and ASCII-only direct uppercase avoid allocating clean display copies.

Exact Microseconds saved:
- 0 us measured.
- Static proof: VWS/subtitle consumer route scan has 0 forbidden-token hits.
- Static proof: selected Audio/UI/PDA/AudioLog/Narrative/Quest/Interaction/Progression timing scan has 0 `Time.frameCount`, 0 coroutine tokens, 0 `WaitForSeconds`.

Residual risk:
- `PDAMarkerRegistry.BuildNextMarkerId()` still uses `string.Create` for save/registry identity, intentionally not rewritten without migration.
- TMP still consumes preallocated managed `char[]` through `SetCharArray`; there is no direct unmanaged-span TMP sink in this route.
- Unity profiler/GCMonitor proof is still pending.

## 2026-05-24 - Compile Dependency Repair Attempt

What was wrong:
- Guarded build after the backstop sweep failed in non-X_011 modified dependencies: `InputManager`, `ConstructionRuntimeProxyFactory`, then `SubmarineAtmosphereSystem`, `FaunaBrain`, and `HectonPlayerMotor`.

What was done:
- Fixed `InputManager` generated action fallback without relying on interface-only `.asset` / `.Dispose()`.
- Fixed construction proxy material property IDs without referencing the Graphics assembly from Core.
- Fixed the `in` property ref error in atmosphere impact AUP resolution.
- Fixed missing physics namespace and finite float3 sanitizer in `FaunaBrain`.
- Fixed KCC local name shadowing in `HectonPlayerMotor`.

Cinematic Cheats used:
- None. This was compile hygiene, not presentation.

Exact Microseconds saved:
- 0 us measured.
- Build attempt 1 failed with 4 errors in external dependencies.
- Build attempt 2 failed with 6 different external dependency errors, then those were patched.
- Final build guard attempts 1-24 stayed red; no illegal overlapping build was launched.

Residual risk:
- No compile proof exists after the final dependency repairs because CPU/compiler guard never went green.

## 2026-05-24 - Compile Closure And Late UI Text Residuals

What was wrong:
- `BaseAirlockEvents.PrimeQueueStorage()` wrote to a non-existent `pendingCount`.
- Loading tips still cached resolved managed strings before copying them into TMP.
- PDA loadout category labels used a managed `string[]` and string parameter in the body writer.
- Editor build was blocked by a missing `Hecton8.Graphics.Materials` reference and ambiguous `Object` references.

What was done:
- Removed the invalid airlock queue counter write.
- Loading tips now resolve raw localization spans and copy directly into the fixed `_tipBuffer`.
- PDA loadout category labels now resolve as `ReadOnlySpan<char>` switch labels and feed the pooled char-buffer writer.
- Editor blockers were repaired with local shader property IDs and qualified `UnityEngine.Object`.

Cinematic Cheats used:
- Span-to-buffer text staging; no heap-backed resolved tip/category cache on the patched presentation path.

Exact Microseconds saved:
- 0 us measured.
- Guarded build passed: 0 errors, 2 warnings, elapsed 00:00:29.56.
- Static proof: VWS/subtitle target scan has 0 forbidden-token hits.
- Static proof: selected non-editor audio/text timing scan has 0 `Time.frameCount`, 0 coroutine tokens, 0 `WaitForSeconds`.

Residual risk:
- Build still reports 2 external `Hecton8.Input.csproj` missing-reference warnings.
- Remaining selected string materialization hits are cold/save/editor contracts: pause save-slot repair array, PDA data-log category string cache, PDA marker save ID, cartography dump path, lore/editor rebake diagnostics, and quest title/description caches.
- Unity Play Mode, runtime profiler, and GCMonitor proof still not executed in this CLI session.
## 2026-05-24 - PDA Quest Atlas Text Cache Hardening

What was wrong:
- `PDADataLogTab` still cached audio-log category labels as `string[5]`.
- `LoadingTipsDisplay` still carried a dead `LoadTips()` method with stale string-cache residue.
- `QuestStateManager` still cached quest titles/descriptions as `string[]`, and Atlas-6 scarcity directive notifications read the title through a managed string output.
- Selected AtlasSignal telemetry/cache paths still read `Time.frameCount` directly.

What was done:
- Replaced PDA category strings with fixed char buffers and `ReadOnlySpan<char>` category lookup.
- Deleted the dead loading-tip string-cache method.
- Replaced quest presentation `string[]` caches with fixed char buffer tables and length arrays.
- Added `QuestData.TryWriteDescriptionOrFallback(...)` and `QuestManager.TryCopyQuestPresentation(...)`.
- Routed Atlas-6 scarcity directive notifications through `NotificationEvents.RegisterMessage(ReadOnlySpan<char>)` plus `PushRegisteredWarning`.
- Replaced AtlasSignal/SignalBeacon direct frame reads with `SystemDispatcher.CurrentFrameIndex`.

Cinematic cheats used:
- None. This pass is text ownership and dispatcher-frame hygiene only.

Exact microseconds saved:
- 0 measured. Static expected gain is removal of managed category/quest title cache materialization from selected presentation routes. Runtime profiler/GCMonitor proof is still absent.

Verification:
- Patched-file scans: 0 hits for removed string caches, `new string`, `StringBuilder`, `string.Create`, `string.Concat`, `string.Format`, `.ToString(`, exception-message tokens, TMP string sinks, coroutine tokens, `WaitForSeconds`, and direct `Time.frameCount`.
- Expanded selected non-editor Audio/UI/PDA/AudioLog/Narrative/Quest/Interaction/Progression/AtlasSignal timing scan: 0 hits.
- Expanded selected string scan residuals: `PDAMarkerRegistry` save-ID `string.Create`, `PauseMenuController` cold save-slot repair array, and editor/source-rebake formatting.
- `git diff --check` on touched files: only LF to CRLF warnings.
- Compile: pending. Guarded build skipped after 24 attempts because 7 compiler processes remained active.
## 2026-05-24 - X_011 Loop 40 Waveform/Notification Closure

What was wrong:
- Subtitle waveform bootstrap still had a local `RectTransform[4]` allocation in `SubtitleManager`.
- `AudioWaveformAnimator` could allocate fallback `new[]` and dynamic `float[count]` scale caches during target initialization.
- Quest compile summary used managed string concatenation for multi-error accumulation.
- PDA construction/loadout notification literals used HUDNotification string overloads instead of fixed-buffer routes.

What was done:
- Replaced waveform target setup with fixed four-slot runtime caches and a four-argument configure path.
- Removed dynamic waveform fallback/scale arrays; retained serialized `RectTransform[] waveformBars` only as inspector authoring input.
- Changed quest compile diagnostics to first-error plus `CompileErrorCount`.
- Routed PDA construction/loadout notification literals through `ReadOnlySpan<char>`/`FixedCharBuffer` helpers.

Cinematic Cheats used:
- Bounded four-bar waveform proxy instead of dynamic collection setup.
- First-error compile summary plus count instead of fully formatted multi-line managed diagnostics.

Exact Microseconds saved:
- Static estimate only: ~170 us avoided during subtitle waveform cold bootstrap and PDA notification emission spikes. Runtime microseconds and GC bytes are not claimed; Unity profiler/GCMonitor did not run.

Verification:
- Pre-Loop40 guarded build succeeded: 0 errors, 6 warnings, 00:01:31.62.
- Post-Loop40 targeted scan clean except serialized inspector `RectTransform[] waveformBars`.
- Expanded selected runtime scan clean for timing/coroutine/TMP-string/string-concat/VWS-heap tokens; residuals are save/editor/cold repair surfaces.
- Post-Loop40 compile not run: 42 guarded attempts skipped because compiler processes stayed active (7-10, ending at 8).

## 2026-05-24 - X_011 Loop 41-42 Notification Span Lane And Casing Closure

What was wrong:
- Adjacent HUD notification producers still used managed string paths or string-return localization even after the VWS/subtitle hot route was clean.
- `PlayerExpressionManager` and `HectonDiscoveryManager` built notification text through interpolation / `string.Create` / uppercase string work.
- `UITooltip` cached current text as a managed string instead of comparing span payloads.
- Selected runtime UI/token code still used `char.ToUpperInvariant`/`char.ToLowerInvariant`; those are not heap allocations, but they were unnecessary for ASCII technical tokens and HUD labels.

What was done:
- Added `NotificationEvents.PushInfo/PushWarning/PushCritical(ReadOnlySpan<char>)` and a span publish route through the fixed notification message registry.
- Routed SaveStation, SuitAdvisory, PlayerExpression, HectonDiscovery, HazardExposureNotifier, CorporateOrderSystem, ProceduralLoreDirector, UITooltip, and PDAExchange notification paths through spans/fixed buffers.
- Replaced selected presentation casing calls in LocalizationManager, BuilderStatusOverlay, HectonOSBootManager, PDAConstructionTab, and PDALoadoutTab with ASCII-only helpers.
- Left `PDAExchangeSystem` save-summary `new string`, `PDAMarkerRegistry` save-ID `string.Create`, `PauseMenuController` cold save-slot repair array, and editor/source-rebake formatting classified as non-VWS/non-subtitle residuals instead of fake-fixing unrelated contracts.

Cinematic Cheats used:
- Hash-registered fixed notification text payloads instead of managed notification strings.
- ASCII technical-token casing instead of runtime culture casing.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of managed notification text construction and reduced casing cost in selected runtime presentation paths. Unity profiler/GCMonitor was not run, so no runtime GC-byte claim is made.

Verification:
- Changed 15-file target scan: 0 `Time.frameCount`, coroutine/WaitForSeconds, TMP string sinks, `.ToString`, `string.Create`, concat/format/StringBuilder/Regex/interpolation/exception-message/direct HUD string sink/`ToUpperInvariant`/VWS heap tokens; only `PDAExchangeSystem` save-summary `new string` remains by contract.
- Expanded selected non-editor Audio/UI/Narrative/PDA/Quest/Interaction/Progression/AtlasSignal scan: 0 `Time.frameCount`, 0 coroutine/WaitForSeconds, 0 TMP string sinks, 0 string.Concat/StringBuilder/interpolation, 0 exception-message, 0 direct HUD string sink, 0 `ToUpperInvariant`, 0 VWS heap.
- Residual selected materialization hits: `PDAMarkerRegistry` save-ID `string.Create`, `PDAExchangeSystem` save-summary `new string`, `PauseMenuController` cold save-slot repair array, `NarrativeDagInspectorWindow` and `LoreDatabaseManager` editor/source-rebake formatting.
- `git diff --check` reports only LF->CRLF warnings on five touched files.
- Compile: not launched after Loop 42. Guarded retry made 24 attempts over ~389.6 s; CPU/compiler rule stayed red, ending with 7 active compiler processes.

## 2026-05-24 - X_011 Loop 43 AudioLog/Atlas/BaseIntegrity/Settings Span Closure

What was wrong:
- `AudioLogPickup` still cached prompt text as a managed string and resolved localization through `GetOrFallback`.
- `CorporateOrderSystem` had one remaining static warning pushed through the string notification overload.
- Atlas/BaseIntegrity warning paths still had string-return localization or percent-format registration residue.
- `SettingsPanel` refreshed runtime labels and option values through localized managed strings despite having span-capable TMP sinks.

What was done:
- Replaced `AudioLogPickup` runtime prompt cache with a fixed 96-char buffer and raw localization spans.
- Kept `AudioLogPickup.GetInteractText()` as legacy fallback/custom-string compatibility; the render path uses `TryCopyInteractText`.
- Routed the remaining corporate warning through `.AsSpan()`.
- Routed Atlas and BaseIntegrity warning display text through `ReadOnlySpan<char>`.
- Reworked BaseIntegrity percent notifications so `{0}` is expanded into a reusable char buffer and registered as the exact span payload.
- Reworked `SettingsPanel` label/value localization through `ResolveLocalizedSpan` and `TmpTextNoAlloc.Set(ReadOnlySpan<char>)`, replacing cached previous strings with span hashes.

Cinematic Cheats used:
- Fixed prompt/notification/settings buffers instead of managed text assembly.
- Exact hash-registered notification spans instead of dynamic formatted warning strings.

Exact Microseconds saved:
- 0 measured. Static estimate is removal of managed prompt, warning, and settings-label refresh churn; runtime profiler and GCMonitor proof are still absent.

Verification:
- Targeted scan over `HUDNotification`, `AtlasSignalSystem`, `Atlas6DirectiveSystem`, `BaseIntegrityHUD`, `AudioLogPickup`, `CorporateOrderSystem`, and `SettingsPanel`: 0 hits for `Time.frameCount`, `WaitForSeconds`, coroutine tokens, `new string`, `string.Create`, `string.Concat`, `string.Format`, `StringBuilder`, `.ToString(`, `GetOrFallback(`, `ResolveLocalized(`, `ToUpperInvariant`, `ToLowerInvariant`, `NativeMinHeap`, `VocalWarningHeapOps`, and `BufferedSubtitleRequest`.
- Non-editor notification producer scan: only `NotificationEvents` overload definitions remain, no runtime string-argument warning/info/critical producers.
- `git diff --check` on the seven edited files: only LF->CRLF warnings.
- Compile: not launched. Guarded build attempts 1-24 stayed blocked by CPU/compiler rule; final state CPU=94 and compilerCount=0.

## 2026-05-24 - X_011 Loop 44 Notification Literal And Frame-Read Correction

What was wrong:
- The corrected non-editor scan still found direct string-overload notification calls in `BaseModule`, `RandomEventSystem`, `EcosystemDirector`, `ObjectPoolDiagnostics`, `HectonPlayerHealth`, and `AudioLogSystem`.
- The same touched perimeter still had direct `Time.frameCount` reads in `BaseModule` and `ObjectPoolDiagnostics`.
- Loop 43's notification producer statement was too narrow; the corrected scan proved residuals outside that seven-file set.

What was done:
- Converted the found notification/register calls to `ReadOnlySpan<char>` overloads.
- Changed `RandomEventSystem` notification localization from managed `ResolveLocalized`/`GetOrFallback` to `ResolveLocalizedSpan`/`GetRawSpanOrFallback`.
- Replaced found `BaseModule` and `ObjectPoolDiagnostics` frame reads with `SystemDispatcher.CurrentFrameIndex`.

Cinematic Cheats used:
- Fixed span notification routing instead of managed string overloads.
- Dispatcher-owned frame ID instead of local Unity frame polling.

Exact Microseconds saved:
- 0 measured. Static expected gain is boundary hardening and avoiding managed-string notification entry points. Unity profiler/GCMonitor was not run.

Verification:
- Non-editor direct literal notification scan: 0 direct `NotificationEvents.Push*/RegisterMessage` and `HUDNotification.*` string-literal calls.
- Six-file timing scan: 0 `Time.frameCount`, 0 `WaitForSeconds`, 0 coroutine tokens.
- Six-file string scan: only `ObjectPoolDiagnostics.GenerateReport()` cold pooled `StringBuilder`/`ToString()` report generation remains.
- `git diff --check` on the six edited files: only LF->CRLF warnings.
- Compile: not launched. Guarded build attempts 1-24 stayed blocked by CPU/compiler rule; CPU ranged 63-100 after the initial compiler process cleared and compilerCount later rose again to 1-8.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 45 PDA/Settings Localization String-Cache Closure

What was wrong:
- `SaveSlotHoverPreview`, `PauseMenuController`, `PDAShellChrome`, and `PDADataLogTab` still used `ResolveLocalized` / `GetOrFallback` string-return paths for UI labels that ultimately went into TMP char buffers.
- `InteractionUI` kept a dead `ResolveLocalized` helper.
- This was not VWS priority-word logic, but it was still presentation text allocation surface.

What was done:
- `SaveSlotHoverPreview`: slot prefix, no-data, scene, and integrity labels now resolve as spans and write directly into preview buffers.
- `PauseMenuController`: settings language hint/button/status now use `ResolveLocalizedSpan`; modal button labels no longer call localized string helpers.
- `PDAShellChrome`: localized PDA title/tab/footer/mech-mode caches are fixed char buffers; footer numeric templates are span-backed.
- `PDADataLogTab`: active archive/encrypted/unknown/play-button/empty-state localized labels are fixed char buffers and use span stress-corruption path.
- `InteractionUI`: removed unused string-return localization helper.

Cinematic Cheats used:
- Fixed PDA/UI label buffers instead of managed localized string caches.
- Stable modal labels instead of pulling cold managed localized strings for retry/OK buttons.

Exact Microseconds saved:
- 0 measured. Static expected gain is reduced language-refresh/UI-rebuild allocation risk. Unity profiler/GCMonitor was not run.

Verification:
- Selected runtime Audio/AudioLog/UI/Narrative/Quest/PDA/Interaction/Progression/AtlasSignal scan: 0 `Time.frameCount`, 0 `WaitForSeconds`, 0 coroutine tokens, 0 `IEnumerator`, 0 `GetOrFallback`, 0 `ResolveLocalized`, 0 TMP string sinks, 0 `string.Concat`, 0 `string.Format`, 0 `StringBuilder`, 0 interpolation, 0 exception-message concat, 0 `ToUpperInvariant`.
- Remaining selected hits are classified non-hot: `AudioLogData.OnValidate` editor-only lower/replace, `NarrativeDagInspectorWindow` editor strings, `LoreDatabaseManager` editor hash rewrite, `PDAMarkerRegistry` save-ID `string.Create`, and `PauseMenuController` stale serialized `saveSlots` repair array.
- `git diff --check` on patched UI/PDA files: only LF->CRLF warnings.
- Compile: first guarded build opened but failed on missing `Temp/obj/Assembly-CSharp/project.assets.json` (`NETSDK1004`). Guarded restore/build retry made 18 attempts and did not launch because CPU/compiler guard stayed red.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 46 Modal/MainMenu And Notification Span Boundary Closure

What was wrong:
- `PauseMenuController`, `MainMenuController`, `SettingsPanel`, and `ModalWindow` still had modal/menu paths that could materialize localized/formatted managed strings before writing to TMP.
- `PauseMenuController` still had a stale runtime save-slot repair allocation (`new string[SaveEvents.ManualSlotCount]`).
- Several adjacent notification/prompt producers still entered `NotificationEvents` through managed string variables or `GetOrFallback` localization routes.
- `EndingTerminalInteractable` and `EmergencyServiceRelay` still had interactive presentation text cached or assembled as managed strings despite having span-capable consumers.
- Touched gameplay/presentation files still had direct `Time.frameCount` reads.

What was done:
- Removed PauseMenu save-slot runtime array repair; save-slot display now resolves configured labels without growing serialized arrays.
- Added `ModalWindow.ShowWithCustomLabels(string, char[], int, ...)`; routed PauseMenu/MainMenu/Settings modal bodies through fixed char buffers.
- Moved modal default labels to raw localization spans and `TmpTextNoAlloc`.
- Converted MainMenu loading percent template and save/load/error modal messages to fixed buffers/raw spans.
- Updated the editor smoke tester expectations for the removed save-slot repair/cache route.
- Converted notification producers in ScanLog, SaveManager, PersistentWorldRegistry, HectonSurvival, WorldReadability, EmergencyRelay, Spectrum, HectonPlayerMovement, Eclipse, Ending, EndingTerminal, HectonPlayerHealth, MountableTransport, DepthZone, and FirstHour to span overloads where safe.
- Replaced localized notification fallback strings in Eclipse, Ending, FirstHour, Spectrum, and pressure warning with `GetRawSpanOrFallback`.
- Converted `EndingTerminalInteractable` prompt/data-loaded notification text to fixed char buffers.
- Converted `EmergencyServiceRelay` interact prompt and lore/reward notification route to fixed buffers/spans; public route-message string contracts remain compatibility boundaries.
- Added relay guidance span contracts and switched `EmergencyServiceRelayDirector` / `FirstHourDirector` HUD guidance consumers away from string route messages.
- Replaced touched direct `Time.frameCount` reads in `HectonPlayerMovement` and `SpectrumSystem` with `SystemDispatcher.CurrentFrameIndex`.

Cinematic Cheats used:
- Fixed modal/notification/prompt buffers instead of managed formatting.
- Hash/span notification payloads instead of string overload entry points.
- Dispatcher-owned frame ID instead of local Unity global frame polling in touched routes.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of modal/notification/prompt managed allocation surfaces; Unity profiler/GCMonitor was not run.

Verification:
- Patched target scans: 0 direct `Time.frameCount`, 0 coroutine timing, 0 string materializers, 0 TMP string sinks, and 0 VWS heap tokens in edited runtime notification/prompt/modal paths; remaining `GetOrFallback` hits are classified relay public string compatibility contracts.
- Selected non-editor Audio/AudioLog/UI/Narrative/Quest/PDA/Interaction/Progression/AtlasSignal scans: 0 direct `Time.frameCount`, 0 `WaitForSeconds`/coroutine tokens, 0 direct TMP `.text =`, 0 VWS heap/legacy subtitle queue tokens.
- `git diff --check` on edited files: only LF->CRLF warnings.
- Compile/restore: not launched. `Temp/obj/Assembly-CSharp/project.assets.json` is still missing and the build guard is red: CPU=100 with active dotnet processes.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 47 Relay HUD Label Span Contract Closure

What was wrong:
- `RelayHUDElement` still consumed the active relay label through a managed `RelayLabel` property before writing to TMP.
- `EmergencyServiceRelay` and `EmergencyServiceRelayDirector` still exposed unused public string guidance APIs that could re-open route-message managed text paths.
- Relay descriptor semantics still used a cold `GetOrFallback` string lookup even though relay descriptor text is not the HUD/subtitle/VWS render path.

What was done:
- Added `EmergencyServiceRelay.ResolveRelayLabelSpan()`.
- Converted `RelayHUDElement.UpdateLabel`, label truncation, label hash, label buffer comparison, and buffer write helpers from `string` to `ReadOnlySpan<char>`.
- Deleted unused project-local string guidance APIs: `BuildInitialRouteMessage()`, `BuildBreadcrumbMessage()`, `BuildDownloadedRouteMessage(...)`, and `TryBuildContextualGuidanceMessage(out string)`.
- Removed relay descriptor-note runtime `GetOrFallback`; descriptor now uses authored text or stable fallback without managed localization lookup.

Cinematic Cheats used:
- Bounded 96-char relay label buffer with hash compare and ellipsis truncation.
- Span source to TMP `SetCharArray`; no route label materialization.
- Stable authored descriptor fallback instead of runtime descriptor string localization.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of one HUD label managed-string consumer boundary and the dead public string guidance surface. Unity profiler/GCMonitor was not run.

Verification:
- Targeted relay scan: 0 old string route APIs, 0 `RelayLabel` string route, 0 `GetOrFallback`, 0 `ResolveLocalized`, 0 `ToString`, 0 `new string`, 0 `string.Create`, 0 `string.Concat`, 0 `string.Format`, 0 `StringBuilder`, 0 exception-message construction, 0 TMP string sinks, 0 VWS heap tokens, 0 legacy subtitle queue tokens.
- Selected non-editor Audio/AudioLog/UI/Narrative/Quest/PDA/Interaction/Progression/AtlasSignal scan: 0 `Time.frameCount`, 0 `WaitForSeconds`, 0 coroutine tokens, 0 `IEnumerator`, 0 direct TMP `.text =`, 0 TMP `SetText`, 0 VWS heap tokens, 0 legacy subtitle queue tokens.
- Expanded selected string scan residuals: editor-gated `NarrativeDagInspectorWindow` / `LoreDatabaseManager` formatting and `PDAMarkerRegistry` save-ID `string.Create`.
- `git diff --check` on touched relay files: only LF->CRLF warnings.
- Compile/restore: not launched. `Temp/obj/Assembly-CSharp/project.assets.json` is missing; guard is red (`CpuLoad=100`, active Unity `VBCSCompiler.dll` under `dotnet.exe` PID 19092).
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 48 Notification Registered-String Store Removal

What was wrong:
- `NotificationEvents` still retained managed strings in `RegisteredMessageSlot.Message`.
- `HUDNotification.ShowInfo/ShowWarning/ShowCritical(string)` and `NotificationEvents.Push*/RegisterMessage(string)` could still feed that managed string store.
- Most runtime producers already used spans, but the compatibility route was still a heap-retention trap.

What was done:
- `NotificationEvents.RegisterMessage(string)` now calls the span registration path and copies into fixed backing storage.
- `NotificationEvents.Publish(string, ...)` now dispatches through the span path.
- Removed `RegisteredMessageSlot`, `_messagesByHash`, `_messageSlotCount`, `TryRegisterMessageSlot`, `TryResolveRegisteredMessage`, `TryFindRegisteredMessage`, and `ClearRegisteredMessages`.
- Kept public string overloads for ModdingAPI/tool compatibility, but they no longer retain message strings.
- Kept `TryResolveMessage(uint,out string)` as a cache-presence probe; span-backed messages return `string.Empty`, and repo call sites use `out _`.

Cinematic Cheats used:
- One span message registry and fixed char backing store for both span and string compatibility callers.
- Hash-only event payloads; display resolves message spans at the HUD sink.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of retained HUD notification managed strings and one duplicate registry path. Unity profiler/GCMonitor was not run.

Verification:
- Core scan over `VocalWarningSystem`, `SubtitleManager`, `BabelSubtitleSyncRuntime`, `HUDNotification`, and `NotificationEvents`: 0 `ToString`, 0 `new string`, 0 `string.Create`, 0 `string.Concat`, 0 `string.Format`, 0 `StringBuilder`, 0 `GetOrFallback`, 0 `ResolveLocalized`, 0 TMP string sinks, 0 `WaitForSeconds`, 0 coroutine tokens, 0 direct `Time.frameCount`, 0 `NativeMinHeap`, 0 `VocalWarningHeapOps`, 0 legacy subtitle queue tokens.
- Targeted notification scan: no managed registered-message storage symbols remain; public string overload declarations remain and route to span storage.
- `git diff --check` on `NotificationEvents.cs` / `HUDNotification.cs`: only LF->CRLF warnings.
- Compile/restore: not launched. `Temp/obj/Assembly-CSharp/project.assets.json` is missing; guard is red (`CpuLoad=100`, no compiler process at final probe).
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 49 PDA Inventory Description Span Closure

What was wrong:
- `PDAInventoryTab.SetSelectedDescriptionText` still consumed `_selectedItem.description`, which goes through `ItemData`'s managed string cache.
- Missing-description fallback still used `ResolveLocalized`/`GetOrFallback`.
- PDA stress corruption still used string-return `ApplyPdaLoreCorruptionIfNeeded`.

What was done:
- Added `ItemData.GetDescriptionSpan(LocalizationManager)` for caller-owned UI buffers.
- Converted selected PDA item description rendering to `ReadOnlySpan<char>`.
- Added `_descriptionTextBuffer` and routed stress corruption through `TryApplyPdaLoreCorruptionIfNeeded(...)`.
- Kept final detail composition on `_detailTextBuffer` and TMP `SetCharArray`.
- Converted the remaining PDA drop-failure literal warning helper to `ReadOnlySpan<char>` and removed unused string info helper.

Cinematic Cheats used:
- Fixed description corruption buffer with bounded truncation before the detail panel write.
- Span localization fallback through `GetRawSpanOrFallback`.
- Hash-based PDA lore corruption source selection using description table hash or persistent item hash.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of selected-item description string cache/fallback/corruption materialization during PDA refresh. Unity profiler/GCMonitor was not run.

Verification:
- Core scan over `VocalWarningSystem`, `SubtitleManager`, `BabelSubtitleSyncRuntime`, `HUDNotification`, `NotificationEvents`, and `PDAInventoryTab`: 0 `ToString`, 0 `new string`, 0 `string.Create`, 0 `string.Concat`, 0 `string.Format`, 0 `StringBuilder`, 0 `GetOrFallback`, 0 TMP string sinks, 0 `WaitForSeconds`, 0 coroutine tokens, 0 direct `Time.frameCount`, 0 `NativeMinHeap`, 0 `VocalWarningHeapOps`, 0 legacy subtitle queue tokens.
- PDA-specific scan: no `_selectedItem.description`, `string desc`, `GetOrFallback`, or string-return corruption path remains; only `TryApplyPdaLoreCorruptionIfNeeded(...)` span-buffer route remains.
- `git diff --check` on `PDAInventoryTab.cs` / `ItemData.cs`: only LF->CRLF warnings.
- Compile: not launched. `Temp/obj/Assembly-CSharp/project.assets.json` now exists, but guard is red with 8 `dotnet` processes plus active `VBCSCompiler.exe`.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 50 Depth-Zone HUD Notification Span Closure

What was wrong:
- `DepthZoneDirector` still cached zone-entry, hull-warning, and route-cue HUD messages as managed `string[]`.
- `RebuildZoneMessageCache()` still used `GetFormatted`, `GetOrFallback`, `ToUpperInvariant`, string concatenation, and `DepthZoneProfile.cachedHudLabel`.
- Dev zone-enter logging still forced `DepthZoneProfile.DisplayNameOrFallback`, a string-return compatibility property.

What was done:
- Removed `DepthZoneProfile.cachedHudLabel`.
- Added `DepthZoneProfile.ResolveDisplayNameSpan(ILocalizationTextReadModel)` and `ResolveDescriptionSpan(ILocalizationTextReadModel)`.
- Reduced the director cache to zone identity only.
- Built zone-enter and hull-warning notification messages into `_zoneNotificationBuffer` / `_zoneAuxBuffer`.
- Published messages through `NotificationEvents.PushInfo(ReadOnlySpan<char>)` and `PushWarning(ReadOnlySpan<char>)`.
- Replaced managed uppercase with bounded ASCII uppercase copy into caller-owned buffers.
- Collapsed dev zone-enter log to a stable literal so it no longer reads a string display-name route.

Cinematic Cheats used:
- Fixed 256-char notification buffer plus 192-char aux buffer instead of preformatted per-zone message strings.
- Single `{0}` span-template writer for zone labels.
- `LocNumericBuffer` / `ZeroGCFormatter` for hull-tier numeric insertion.
- Identity-only zone cache; display text is late-frame span materialization.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of depth-transition HUD string formatting/caching churn and retained per-zone HUD strings. Unity profiler/GCMonitor was not run.

Verification:
- Targeted DepthZone forbidden scan: 0 hits for `cachedHudLabel`, cached HUD string arrays, `GetFormatted`, `GetOrFallback`, `ToUpperInvariant`, `string.Concat`, `string.Format`, `new string`, `string.Create`, `StringBuilder`, `.ToString`, interpolation, direct `Time.frameCount`, coroutine timing, TMP string sinks, `NativeMinHeap`, `VocalWarningHeapOps`, and legacy subtitle queue tokens.
- Old helper scan: 0 hits for `GetZoneEnterMessage`, `GetHullWarningMessage`, `ResolveUnknownZoneLabel`, `ResolveZoneEnterFallback`, and `ResolveZoneRouteCue`.
- `git diff --check` on `DepthZoneDirector.cs` / `DepthZoneProfile.cs`: only LF->CRLF warnings.
- Compile: not launched. Build guard was red: `CpuLoad=63`, `CompilerProcessCount=7`.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 51 PDA Inventory ASCII Casing Cleanup

What was wrong:
- `PDAInventoryTab` still used `char.ToUpperInvariant` in item detail/title notification paths.
- This was not a managed allocation, but it was still culture-aware casing inside the PDA presentation refresh perimeter.

What was done:
- Replaced the two `char.ToUpperInvariant` calls with local `ToUpperAscii(char)`.
- Renamed `AppendUpperInvariant` to `AppendUpperAscii`.
- Kept all writes on existing caller-owned buffers and TMP `SetCharArray` routes.

Cinematic Cheats used:
- ASCII-only casing for technical PDA item labels.
- One-char stack scratch retained for `FixedCharBuffer` appends.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of culture-aware casing in PDA label/notification refresh. Unity profiler/GCMonitor was not run.

Verification:
- `PDAInventoryTab` selected scan: 0 hits for `ToUpperInvariant`, `ToLowerInvariant`, `GetOrFallback`, `GetFormatted`, `new string`, `string.Create`, `string.Concat`, `string.Format`, `StringBuilder`, TMP string sinks, `Time.frameCount`, coroutine timing, `NativeMinHeap`, `VocalWarningHeapOps`, and legacy subtitle queue tokens.
- Expanded PDA/VWS/subtitle/localization scan residuals are classified: legacy localization string APIs, `PDAMarkerRegistry` save-ID `string.Create`, and `PDAExchangeSystem` save-summary `new string`.
- `git diff --check` on `PDAInventoryTab.cs`: only LF->CRLF warnings.
- Compile: not launched. A 12-attempt guarded loop never reached `CpuLoad <= 50` with zero compiler processes; final attempt was `CpuLoad=70`, `CompilerProcessCount=1`.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 52 VWS Renderer Priority Coherence Repair

What was wrong:
- `VocalWarningSystem` selected queued warnings by canonical bit order in `VwsPriorityWord`.
- `VocalBankPlaybackRuntime` then applied an independent score-only gate: a dispatched VWS preempt could be ignored when its numeric cue priority was lower than the currently playing codec priority.
- This was a real suppression risk. The 64-bit priority word was correct, but the downstream renderer could contradict it.
- Subtitle memory path is Zero-GC by static route, but not literally pure unmanaged UI memory: TMP still requires preallocated managed `char[]` bridge buffers.

What was done:
- Changed VWS cue and subtitle priorities to use non-overlapping canonical bands derived from the selected priority bit index.
- Kept severity/score as an intra-band tiebreaker instead of letting it reorder canonical warning classes.
- Added `VwsPreemptedFlag = 1u << 5` in `VocalBankPlaybackRuntime`.
- Changed renderer gating so VWS preempted cues bypass the stale numeric reject.

Cinematic Cheats used:
- Five fixed numeric bands inside byte priority instead of a scheduler or heap.
- One bit index drives both queue order and renderer priority.
- Preempt flag is a single bit already carried in `VocalCueSignal.Flags`.

Exact Microseconds saved:
- 0 measured. Static expected gain is correctness, not speed: no extra allocation and no extra search structure, just arithmetic on the existing bit index. Unity profiler/GCMonitor was not run.

Verification:
- Focused scan over `VocalWarningSystem`, `VocalBankPlaybackRuntime`, `SubtitleManager`, `BabelSubtitleSyncRuntime`, `AudioLogSystem`, and `AudioLogData`: 0 hits for `ToString`, `new string`, `string.Create`, `string.Concat`, `string.Format`, `StringBuilder`, interpolation, TMP string sinks, direct `Time.frameCount`, coroutine timing, `NativeMinHeap`, `VocalWarningHeapOps`, `BufferedSubtitleRequest`, `GetOrFallback`, and `GetFormatted`.
- Selected non-editor Audio/AudioLog/UI/Narrative/Quest/PDA scan: 0 `Time.frameCount`, 0 `WaitForSeconds`, 0 coroutine tokens, 0 TMP string sinks, 0 string concat/format/builder/interpolation, 0 exception-message construction, 0 VWS heap tokens.
- Representative static priority simulation: Power bit59 priority 19, Radiation bit60 81, Oxygen bit61 146, Hull bit62 203, Crush bit63 239. Lower canonical alarms cannot numerically outrank higher canonical alarms in renderer priority.
- `git diff --check` on `VocalWarningSystem.cs` and `VocalBankPlaybackRuntime.cs`: only LF->CRLF warnings.
- Compile: not launched. Build guard was red: `CpuLoad=100`, `CompilerProcessCount=8`.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-24 - X_011 Loop 53 Compile Dependency Closure And SaveEvent Frame Ownership

What was wrong:
- The VWS priority repair was statically clean, but the first guarded build surfaced external compile blockers in UI/save/world-adjacent code.
- Most blocker files changed under the shared worktree during the build window; source re-read showed the stale save/menu/migration/seam/PDA-map errors were already corrected in current files.
- `SaveEvents.cs` still had two real current defects: missing `Hecton.Localization` import for `LocHash`, and `DrainQueueWithoutBudget` dequeued into `_` before releasing `payload.MessageSlot`.
- `SaveEvents.cs` also still read `UnityEngine.Time.frameCount` directly in four telemetry throttles.

What was done:
- Replaced direct frame reads in `LockstepStateValidator` and `PersistentWorldRegistry` with `SystemDispatcher.CurrentFrameIndex`.
- Added `using Hecton.Localization;` to `SaveEvents.cs`.
- Changed `DrainQueueWithoutBudget` to dequeue into `SaveEventPayload payload` before releasing the message slot.
- Changed four `SaveEvents` telemetry frame reads to `SystemDispatcher.CurrentFrameIndex`.
- Re-ran targeted compile-blocker scans against current source.

Cinematic Cheats used:
- Kept save event DTOs hash/slot based instead of restoring managed `Message` / `SlotName` strings.
- Dispatcher-owned frame id reused as the single frame source.
- No heap or coroutine route added.

Exact Microseconds saved:
- 0 measured. This was compile and ownership repair. Static expected gain is removal of four direct Unity frame reads and avoidance of reintroducing string payloads into save events.

Verification:
- Final guarded `dotnet build Assembly-CSharp.csproj --no-restore` succeeded after the `SaveEvents` frame-owner patch: 0 errors, 2 warnings for missing external `Hecton8.Input.csproj`, elapsed 00:00:51.08.
- Earlier dependency-closure build also succeeded before the final frame-owner patch: 0 errors, 2 same warnings, elapsed 00:01:53.13.
- Post-`SaveEvents` frame-owner patch X_011 route scan: 0 hits for direct frame reads, coroutine timing, TMP string sinks, string materializers, exception-message construction, VWS heap tokens, and legacy subtitle queue tokens.
- Compile-adjacent scan: remaining hits are false positives on shader property IDs and `payload.MessageHash/MessageSlot`; no old removed buffer names or `payload.Message` / `payload.SlotName` remain.
- `git diff --check` on touched files: only LF->CRLF warnings.
- Post-frame-owner compile rerun: passed after guard opened on attempt 7 (`CpuLoad=27`, `CompilerProcessCount=0`).
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 54 Interaction Prompt Provider Span Expansion

What was wrong:
- VWS/subtitle core remained statically clean, but nearby interactable prompt producers still had managed localized string cache/resolve paths.
- Some user-facing or presentation-adjacent frame reads still went directly through `Time.frameCount`.
- A whole-project Zero-GC claim would still be false without runtime profiler proof and because TMP uses preallocated managed `char[]` bridge buffers.

What was done:
- Added/expanded `InteractableTextCopy` span helpers for localized/configured prompt copying.
- Migrated 24 touched prompt/presentation files to fixed buffers or `ReadOnlySpan<char>` routes where active UI text is produced.
- Patched direct frame reads in `PlayerActionController`, `MantaEmergencyWreck`, and `DebrisManager` to `SystemDispatcher.CurrentFrameIndex`.
- Left `DeployableBeacon.CreateDeterministicBeaconId()` intact because it creates persistent beacon identity, not render text.

Cinematic Cheats used:
- Stable fallback strings only on legacy compatibility APIs.
- Fixed-size char buffers for prompt/UI text instead of localized string caches.
- Dispatcher-owned frame id as the single frame source.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of localized prompt string refresh churn and three direct Unity frame reads in presentation-adjacent logic. No Unity profiler/GCMonitor run.

Verification:
- Focused scan over `VocalWarningSystem`, `VocalBankPlaybackRuntime`, `BabelSubtitleSyncRuntime`, `SubtitleManager`, `AudioLogSystem`, and `AudioLogPickup`: 0 hits for `Time.frameCount`, `WaitForSeconds`, coroutine tokens, TMP string sinks, string materializers, exception-message construction, `NativeMinHeap`, `VocalWarningHeapOps`, `BufferedSubtitleRequest`, `GetOrFallback`, and `ResolveLocalized`.
- Touched-file scan over 24 patched files: one residual `string.Create` at `DeployableBeacon.CreateDeterministicBeaconId()`, classified as persistent save identity.
- `git diff --check` on 24 touched files: exit 0; one LF->CRLF warning on `HectonOSBootManager.cs`.
- Compile: not launched after Loop 54. Build guard was red: `CpuLoad=100`, `CompilerProcessCount=2`.
- Last compile proof: Loop 53 `dotnet build Assembly-CSharp.csproj --no-restore` passed with 0 errors and 2 external `Hecton8.Input.csproj` warnings, elapsed 00:00:51.08.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 55 Beacon/Scanner/Tool HUD-Adjacent String Backstop

What was wrong:
- Core VWS/subtitle/audio-log scan stayed clean, but adjacent player-facing text systems still had runtime string materialization patterns.
- `BeaconRuntime` and `BeaconNetworkSystem` used fake "zero-GC" uppercase caches backed by `ToUpperInvariant()` strings.
- `BeaconNetworkSystem` pulled the beacon prefix through `GetOrFallback` before creating labels.
- `FieldOperationLogSystem` normalized source/title/summary/severity with `Trim().ToUpperInvariant()` and base `PlayerTool` legacy APIs materialized `FixedCharBuffer` through `ToString()`.
- `ScannableTarget` runtime configure/refresh uppercased and trimmed scan titles/categories/summaries.

What was done:
- Removed `BeaconRuntime` uppercase cache and localized string fallback from runtime label configuration; empty labels now use the stable `"BEACON"` literal.
- Changed `BeaconNetworkSystem` prefix resolution to `GetRawSpanOrFallback` into a fixed prefix buffer before creating the persistent beacon label string.
- Collapsed beacon trim-log fallback text to stable literals instead of managed localization string calls.
- Removed field-operation source/title/summary trim/uppercase materialization; severity now uses a span ASCII compare and returns stable constants.
- Changed base `PlayerTool` legacy string APIs to return stable strings/cached authored tool names directly and removed the unused legacy scratch buffer.
- Removed runtime `ScannableTarget` uppercase/trim materialization for configured/resolved titles/categories/summaries.

Cinematic Cheats used:
- Stable literals and fixed prefix buffers for presentation text.
- ASCII severity classification instead of culture/globalization string normalization.
- Persistent identity strings kept only where save/network identity requires them.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of uppercase/trim/cache churn in beacon/scanner/tool HUD-adjacent presentation and field-operation record normalization. Unity profiler/GCMonitor was not run.

Verification:
- Focused scan over `VocalWarningSystem`, `VocalBankPlaybackRuntime`, `BabelSubtitleSyncRuntime`, `SubtitleManager`, `AudioLogSystem`, and `AudioLogPickup`: 0 hits for `Time.frameCount`, coroutine timing, TMP string sinks, string materializers, exception-message construction, VWS heap tokens, legacy subtitle queue tokens, `GetOrFallback`, and `ResolveLocalized`.
- Patched 5-file scan over `BeaconRuntime`, `BeaconNetworkSystem`, `FieldOperationLogSystem`, `PlayerTool`, and `ScannableTarget`: remaining hits are classified as persistent beacon id/label `string.Create`, persistent field-operation log buffer-to-string storage, and editor-only `ScannableTarget.OnValidate`.
- `git diff --check` on patched files: only LF->CRLF warnings.
- Build: not launched. Guard remained red after wait: `CpuLoad=85`, `CompilerProcessCount=0`; prior probe was `CpuLoad=61`, `CompilerProcessCount=1` with Unity dotnet active.
- Residual honesty: `BeaconDeployerTool` still has a managed localized assessment/log route (`ResolveLocalized`, `GetOrFallback`, one legacy `new string`). It is outside the Betty/subtitle lane but not zero-GC and needs a separate span-struct migration.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 56 Tool Operational Text And Archive Boundary Cleanup

What was wrong:
- The core Betty/subtitle/audio-log route was still statically clean, but tool-level operational summaries/directives and diagnostics still contained legacy string-localization and buffer-to-string materialization routes.
- `BeaconDeployerTool`, `RepairTool`, `KnifeTool`, `SalvageSamplerTool`, `StunPistolTool`, `EnvironmentalAnalyzerTool`, `BuilderTool`, `HarpoonLauncherTool`, `PropulsionTool`, `LaserCutter`, and `ScannerTool` had stale `ResolveLocalized`/`GetOrFallback`, `CreateLegacyString`, Babel buffer `new string`, or compile-time string-plus noise.
- `ScannerTool` used `FixedCharBuffer.Append("+")` for a one-character sign marker.

What was done:
- Collapsed active legacy operational summary/directive APIs in the touched tools to stable fallback-only text rather than managed localized string construction.
- Removed legacy buffer-to-string helpers from HUD refresh paths; sampler/analyzer remaining `new string` helpers are now explicitly named persistent archive helpers.
- Removed dynamic sampler resource/recovery HUD strings in favor of stable fallback labels.
- Added `FixedCharBuffer.Append(char)` and changed the scanner signed-component plus marker to a direct char append.
- Removed compile-time tooltip/dev-log string-plus noise in `BuilderTool` and `LaserCutter` so the scanner output is not polluted by constant literals.

Cinematic Cheats used:
- Stable fallback text in legacy string APIs instead of runtime localization materialization.
- Fixed char buffers and span copying for presentation text.
- Persistent archive/save strings left only at explicit identity/archive boundaries.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of managed localization/buffer-to-string churn from tool operational refresh and one-character string append calls. No Unity profiler/GCMonitor run.

Verification:
- Focused scan over `VocalWarningSystem`, `VocalBankPlaybackRuntime`, `BabelSubtitleSyncRuntime`, `SubtitleManager`, `AudioLogSystem`, and `AudioLogPickup`: 0 hits for direct `Time.frameCount`, coroutine timing, TMP string sinks, managed string materializers, exception-message construction, VWS heap tokens, legacy subtitle queue tokens, localization string APIs, interpolation, and string-plus concat.
- Tool scan over the 11 touched tool files plus `FixedCharBuffer`: 0 string-plus concat hits. Materializer scan leaves exactly three classified residuals: `SalvageSamplerTool.CreatePersistentArchiveString`, `EnvironmentalAnalyzerTool.CreatePersistentArchiveString`, and `FixedCharBuffer.ToString()`.
- Selected non-editor audio/UI/narrative/quest/PDA/interaction/progression scan over 216 files: 0 direct frame reads, 0 coroutine timing, 0 TMP string sinks, 0 localization string APIs, 0 VWS heap tokens, 0 exception-message construction, and 0 interpolation. Remaining managed materializers are editor-only DAG/lore tooling and `PDAMarkerRegistry` save identity.
- `git diff --check` on touched files: only LF->CRLF warnings.
- Build: not launched. Guard was red: `CpuLoad=96`, `CompilerProcessCount=3` (`dotnet`, `dotnet`, `VBCSCompiler`).
- Last compile proof remains Loop 53: `dotnet build Assembly-CSharp.csproj --no-restore` passed with 0 errors and 2 external `Hecton8.Input.csproj` warnings, elapsed 00:00:51.08.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 57 Build Guard Retry

What was wrong:
- Loop 56 touched runtime source, so compile verification was needed.
- The local build guard did not allow a safe build launch because CPU/compiler state remained red.

What was done:
- Ran 24 guard attempts before build.
- Did not launch `dotnet build` because the guard never opened.

Cinematic Cheats used:
- None. This was verification gating only.

Exact Microseconds saved:
- 0 measured. Wall-clock guard wait was about 151400000 us.

Verification:
- Guard attempts 1-24: CPU ranged 51-100; compiler process count ranged 0-2.
- Attempt 14 had `CompilerProcessCount=0` but `CpuLoad=100`, so build still stayed blocked.
- Final sample: `CpuLoad=100`, `CompilerProcessCount=2`.
- Last compile proof remains Loop 53: `dotnet build Assembly-CSharp.csproj --no-restore` passed with 0 errors and 2 external `Hecton8.Input.csproj` warnings, elapsed 00:00:51.08.
- Runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 58 Input/UI/Core String Perimeter Hardening

What was wrong:
- `InputManager` still had an actual managed fallback: `controlName.ToString().Trim().ToUpperInvariant()` and interpolated fallback glyph chips.
- `ZeroGCStringCache` was not zero-GC on misses; it uppercased into a fresh managed string.
- `MemoryBudgetTracker`, `RebindingManager`, and `UserOptionsPersistence` had development diagnostics that concatenated strings and exception messages.
- `GameStartContext` persisted ticks with `long.ToString()` and wrote diagnostic `Current.ToString()`.
- `InteractionUI` refreshed prompt caches through `GetExpandedOrFallback`, a legacy managed localization string API.
- `PlayerExplorationTracker` had a raw string-plus dump path in a blackbox boundary.

What was done:
- Replaced the unknown binding-display fallback with the existing binding path reference and kept hot display writes on caller-owned char buffers.
- Replaced InputManager culture casing with ASCII char transforms and stable fallback glyph chip literals.
- Replaced fake uppercase string caching with `ZeroGCStringCache.TryWriteUpperAscii`.
- Collapsed memory-budget, rebind, and options diagnostics to stable literal logs.
- Packed game-start handoff ticks into two `PlayerPrefs.SetInt` values; kept legacy string-read fallback only for old handoff keys.
- Made `InteractionUI.ResolveLocalizedExpanded` return authored fallback strings and rely on the existing `TryExpandText(..., char[])` render path for button-token expansion.
- Replaced PDA cartography blackbox raw string-plus path with `Path.Combine`/`Path.GetFullPath`.

Cinematic Cheats used:
- Stable authored prompt strings instead of managed localized prompt cache materialization.
- ASCII casing for technical binding labels.
- Packed integer timestamp persistence instead of decimal text persistence.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of binding-display, prompt-cache, and diagnostic heap churn. No Unity profiler/GCMonitor run.

Verification:
- Focused Betty/subtitle/audio-log scan: 0 hits for direct frame reads, coroutine timing, TMP string sinks, managed string materializers, exception-message construction, VWS heap tokens, legacy subtitle queue tokens, localization string APIs, interpolation, and string-plus concat.
- Touched 8-file scan: 0 hits for `ToString`, `new string`, `string.Create`, `string.Concat`, interpolation, raw string-plus concat, TMP string sinks, `GetOrFallback`, `ResolveLocalized`, exception-message construction, `ToUpperInvariant`, and `ToLowerInvariant`.
- Selected non-editor audio/UI/narrative/quest/PDA/input/interaction timing scan: 0 hits for `Time.frameCount`, `WaitForSeconds`, coroutine tokens, and `IEnumerator`.
- Expanded selected text scan residuals are classified: editor-gated DAG/lore tooling, `PDAMarkerRegistry` save ID, `PDAExchangeSystem` save summary, legacy `LocalizationManager` string API declarations, and literal plus signs written into char buffers.
- `git diff --check` on touched files: only LF->CRLF warnings.
- Build: guarded `dotnet build --no-restore` launched once at `CpuLoad=38`, `CompilerProcessCount=0`, but failed with NETSDK1004 because `Temp/obj/Assembly-CSharp/project.assets.json` is missing. Guarded restore then made 18 attempts and did not launch because CPU/compiler state stayed red; final sample `CpuLoad=79`, `CompilerProcessCount=8`.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 59 Betty Playback Proof And Field Operation Span Readback

What was wrong:
- The VWS/subtitle route itself stayed clean, but a fresh static pass needed to prove the vocal playback renderer could not contradict the 64-bit priority word.
- `FieldOperationLogSystem` had fixed-buffer writes but only legacy string snapshot readback for consumers that wanted recent operation text.
- Two allocation tokens remained in the selected perimeter: editor-only `AudioLogData` ID generation and save/export `FieldOperationLogSystem` string materialization.

What was done:
- Re-read `VocalWarningSystem`: insert sets a warning bit in `VwsPriorityWord`; pop clears that bit; expired discard clears stale bits from the active word; highest-priority selection uses `math.lzcnt` over selected high/low halves.
- Re-read `VocalBankPlaybackRuntime`: the renderer checks `VwsPreemptedFlag` before rejecting lower numeric priority, so canonical VWS preemption survives playback-layer filtering.
- Re-read `BabelSubtitleSyncRuntime` and `SubtitleManager`: subtitle timing is audio-frame/dispatcher-frame based and rendering is `SetCharArray` from preallocated buffers, not managed coroutine/string timing.
- Added/kept `FieldOperationLogSystem.TryCopyRecentEntry` and `TryCopyLatestEntry` so HUD/log consumers can copy recent source/title/summary/severity into caller-owned spans without `new string`.

Cinematic Cheats used:
- One 64-bit priority word instead of heap/sort scheduling for five canonical Betty alarms.
- Canonical non-overlapping numeric priority bands for playback compatibility.
- Preallocated TMP char-array bridge for subtitles; no managed text composition in the hot route.

Exact Microseconds saved:
- 0 measured. Static expected gain is avoided heap traversal and avoided string readback when consumers use the new field-operation span API.

Verification:
- Focused hot-route scan over VWS, vocal playback, Babel subtitle sync, SubtitleManager, audio-log system/pickup, notifications, HUD notification, base/life-support text, player tool manager, and options persistence: 0 direct frame reads, 0 coroutine timing, 0 TMP string sinks, 0 managed materializers, 0 localization string APIs, 0 culture casing, 0 exception-message concat, 0 VWS heap tokens, 0 legacy subtitle queue tokens.
- Boundary scan residuals: `AudioLogData.CreateEditorDefaultLogId` editor-only `string.Create`; `FieldOperationLogSystem.CreatePersistentString` save/export/legacy snapshot `new string`.
- `git diff --check` on touched/inspected X_011 files: only LF->CRLF warnings.
- Build guard: 12 attempts, assets json present, but two compiler processes stayed active and CPU ranged 37-99. No overlapping build launched.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 60 Quest Marker String Readback Closure

What was wrong:
- `MissionMarkerSystem.TryResolveMarkerCache` asked `QuestManager.TryGetQuestPresentation(...)` for title and description, then discarded both values with `out _`.
- The callee could still materialize managed quest title/description strings through fallback presentation properties before the caller discarded them.
- This was not a subtitle renderer bug, but it was a real hidden text allocation risk in a HUD/marker presentation-adjacent refresh path.

What was done:
- Changed marker refresh to call `QuestManager.TryCopyQuestPresentation(...)` with `null` title/description buffers.
- Removed unused legacy string-return `TryGetQuestPresentation(...)` methods from `QuestManager` and `QuestStateManager`.
- Verified the adjacent Atlas marker route reads through `IAtlasSignalReadModel.TryReadAtlasSignalCoreAup` instead of a concrete runtime object.

Cinematic Cheats used:
- Reused the existing span-copy quest presentation route instead of inventing another cache.
- Passed `null` text buffers where marker logic only needs target, world position, and height offset.

Exact Microseconds saved:
- 0 measured. Static expected gain is avoided managed quest title/description fallback work during marker cache refresh.

Verification:
- Focused VWS/subtitle/audio-log/quest scan: 0 direct frame reads, 0 coroutine timing, 0 TMP string sinks, 0 managed materializers, 0 localization string APIs, 0 culture casing, 0 exception-message concat, 0 VWS heap tokens, 0 legacy subtitle queue tokens, 0 legacy quest string API calls.
- Expanded non-editor audio/UI/narrative/quest/PDA/input/interaction materializer scan residuals: editor-only `AudioLogData` ID generation, editor-only `NarrativeDagInspectorWindow`, editor-only `LoreDatabaseManager` rebake formatting, and `PDAMarkerRegistry` save identity.
- `git diff --check` on quest files: only LF->CRLF warnings.
- Build guard: no build launched because `CpuLoad=93`, `CompilerProcessCount=2`, assets json present.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 61 PDA Description Span Closure

What was wrong:
- `PDAConstructionTab.WriteCardBody` checked and wrote module notes through string-oriented description helpers.
- `PDALoadoutTab.AppendPresetBrief` copied `preset.description` to a local `string` and then scanned/truncated it.
- These were not fresh allocations from the field read, but they preserved a string-shaped PDA presentation route next to the VWS/subtitle text surface.

What was done:
- `PDAConstructionTab` now converts `data.description` to `ReadOnlySpan<char>` once and sends that span into the NOTES writer.
- `TryAppendTrimmedUpperForCard` now accepts `ReadOnlySpan<char>` and uses a local no-allocation whitespace scanner.
- `PDALoadoutTab` now trims/truncates preset descriptions as spans and appends the span slice into the existing char buffer.

Cinematic Cheats used:
- Small span scanners instead of `Trim`, `Substring`, or string-local staging.
- Existing pooled PDA char buffers and TMP `SetCharArray` sinks reused.

Exact Microseconds saved:
- 0 measured. Static expected gain is reduced PDA card text heap risk and removal of string-shaped description helper signatures.

Verification:
- Target scan over VWS/subtitle/audio-log/quest/PDA construction/loadout: 0 direct frame reads, 0 coroutine timing, 0 TMP string sinks, 0 managed materializers, 0 localization string APIs, 0 culture casing, 0 exception-message concat, 0 VWS heap tokens, 0 legacy subtitle queue tokens, 0 legacy quest string API calls, 0 local description-string patterns.
- Expanded non-editor audio/UI/narrative/quest/PDA/input/interaction materializer scan residuals remain classified: editor-only `AudioLogData`, editor-only `NarrativeDagInspectorWindow`, editor-only `LoreDatabaseManager`, and `PDAMarkerRegistry` save identity.
- `git diff --check` on the two PDA files: only LF->CRLF warnings.
- Build guard: no build launched because latest sample was `CpuLoad=70`, `CompilerProcessCount=0`, assets json present.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 62 Music Context Local String Cleanup

What was wrong:
- Audio sweep found one remaining local `string description = matrixProfile.shortDescription` in `HectonMusicDirector.ResolveMatrixBiomeMusicProfile`.
- It was not a heap allocation, but it preserved unnecessary string-shaped staging in audio-context selection.

What was done:
- Removed local biome/description/family string staging.
- Token checks now read the authored matrix profile fields directly.

Cinematic Cheats used:
- No new cache. No new matcher. Removed the staging instead.

Exact Microseconds saved:
- 0 measured. Static expected gain is reduced audio text-path surface and less scan noise.

Verification:
- Focused HectonMusicDirector/PDA/quest scan: 0 direct frame reads, 0 coroutine timing, 0 TMP string sinks, 0 managed materializers, 0 localization string APIs, 0 culture casing, 0 exception-message concat, 0 local description-string patterns.
- Wider non-editor string-description scan residual: only serialized `ToolLoadoutPreset.description` field declaration.
- `git diff --check` on `HectonMusicDirector.cs` plus PDA files: only LF->CRLF warnings.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 63 Restore And Build Guard Hold

What was wrong:
- Runtime source changed in Loops 60-62 and needs compile verification.
- `Temp/obj/Assembly-CSharp/project.assets.json` disappeared during concurrent work before a build could launch.

What was done:
- Re-ran focused post-cleanup scan: 0 direct frame reads, 0 coroutine timing, 0 TMP string sinks, 0 managed materializers, 0 interpolation, 0 localization string APIs, 0 culture casing, 0 exception-message concat, 0 VWS heap tokens, 0 legacy subtitle queue tokens, 0 legacy quest string API calls, 0 local description-string patterns, 0 Trim/Substring tokens in the focused set.
- Polled guarded build/restore state. Build was not launched because assets json was missing. Restore was not launched because CPU/compiler guard stayed red.

Cinematic Cheats used:
- None. Verification guard only.

Exact Microseconds saved:
- 0 measured. Guard wait consumed about 130000000 us wall clock.

Verification:
- Static focused scan: clean.
- Restore/build: blocked by missing assets json plus CPU/compiler guard. Last restore attempt samples included CPU 39 with 2 compiler processes and later CPU 100 with 2 compiler processes.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 64 VWS Priority State Branch Shape Tightening

What was wrong:
- `Pop` and `DiscardExpired` cleared warning bits with masks, but state publication still used a ternary for empty-word `HighestPriorityBitIndex`.
- No allocation was present, but the code shape was weaker than the branch-minimal priority-word contract.

What was done:
- Added `ResolveHighestPriorityBitIndexOrMax(ulong)`.
- Replaced post-pop and post-expiry `highestBitIndex >= 0 ? ... : uint.MaxValue` with `math.select`.

Cinematic Cheats used:
- Single-word bit scan and mask clear retained; no heap, no table, no job.

Exact Microseconds saved:
- 0 measured. Static expected gain is negligible; this is determinism/code-shape hardening.

Verification:
- Focused VWS/subtitle/audio/PDA/quest scan: 0 direct frame reads, 0 coroutine timing, 0 TMP string sinks, 0 managed materializers, 0 interpolation, 0 localization string APIs, 0 culture casing, 0 exception-message concat, 0 VWS heap tokens, 0 legacy subtitle queue tokens, 0 legacy quest string API calls, 0 local description-string patterns, 0 Trim/Substring tokens.
- VWS grep: no `highestBitIndex >= 0` branch remains in priority state update.
- Build/restore: not launched because `Temp/obj/Assembly-CSharp/project.assets.json` is missing and latest guard is `CpuLoad=99`, `CompilerProcessCount=0`.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 65 LocalizedTextReference Legacy String Fallback Hardening

What was wrong:
- `LocalizedTextReference.Resolve()` and `Resolve(LocalizationManager)` could still reach `ExpandText(...)` and return managed strings.
- Hot UI uses span/copy APIs, but the legacy string API shape was still an accidental allocation trap.

What was done:
- `Resolve()` now routes through `GlobalRegistry.LocalizationText` read-model fallback resolution.
- `Resolve(LocalizationManager)` now routes through the read-model overload.
- `Resolve(GameLanguage, LocalizationManager)` returns only inline text, fallback text, or table key. Caller-owned `TryCopyResolvedOrFallback(...)` keeps char-buffer expansion.

Cinematic Cheats used:
- Legacy string APIs now degrade to stable fallback/table-key text; rich localization stays on caller-owned span/buffer routes.

Exact Microseconds saved:
- 0 measured. Static expected gain is removal of accidental managed text expansion when legacy string properties are touched.

Verification:
- Selected localized data/string API scan: 0 timing tokens, 0 text sinks.
- Remaining selected materializer: editor-only `AudioLogData.CreateEditorDefaultLogId`.
- Expansion hits are char-buffer `TryExpandText` paths; culture casing hit is editor-only `SuitUpgradeData.OnValidate`.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 66 CLI Voice Scanner And Priority Proof

What was wrong:
- The existing editor scanner was narrower than the current re-audit. It did not provide CLI proof for all `Assets/_Project/Scripts` C# files and did not embed the priority-word source proof or storm simulation in the report.
- Manual grep output was not enough as a persistent artifact.

What was done:
- Added `Tools/OOP_Voice_Scanner_X_011.py`.
- Regenerated `Docs/Reports/UX_OPTIMIZATION_REPORT_X_011.json`.
- Scanner now classifies owned VWS/subtitle hot-route findings separately from editor/save/diagnostic/out-of-domain findings.
- Scanner embeds source evidence for explicit 64-byte `VocalCueSignal`, `VwsPriorityWord`, bit set/clear, `math.lzcnt`, `math.select`, `ResolveHighestPriorityBitIndexOrMax`, playback `VwsPreemptedFlag`, explicit 64-byte `SubtitleCueSignal`, audio-frame subtitle fields, and TMP `SetCharArray`.
- Scanner runs a deterministic 50-trigger storm simulation.

Cinematic Cheats used:
- Keep VWS authority as one `ulong`; verify with bit masks and high-bit scan instead of restoring heap scheduling.

Exact Microseconds saved:
- 0 measured. Static verification consumed about 312700000 us wall clock including one timed-out first scanner run and three completed scanner runs.

Verification:
- `python Tools/OOP_Voice_Scanner_X_011.py`: `PASS_STATIC_HOT_ROUTE`, `PASS_STATIC_PRIORITY_PROOF`, `filesScanned=2406`, `ownedHotFatalCount=0`, `focusedTextFatalCount=0`, `focusedTextManagedMaterializerCount=0`.
- Storm simulation: priority word `0xF800000000000000`, active bits `[63,62,61,60,59]`, highest bit `63`, accepted `5`, replaced `8`, rejected `37`.
- Manual owned-route grep: 0 direct `Time.frameCount`, 0 coroutine timing, 0 TMP string sinks, 0 VWS heap tokens, 0 legacy localization calls, 0 culture casing, 0 exception-message concat. One materializer token remains and is editor-only: `AudioLogData.CreateEditorDefaultLogId`.
- `git diff --check` on touched X_011 files: LF->CRLF warnings only.
- Build not launched: guard sample `CpuLoad=100`, `CompilerProcessCount=0`, assets json present.
- Last compile proof remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 67 Compile Wall RecipeData Contract Import

What was wrong:
- Guard opened and a full `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` was launched.
- Build failed in `Hecton8.Core.csproj`, not in X_011 VWS/subtitle code:
  - `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs(3502,39): CS0246 RecipeData could not be found`
  - `Assets/_Project/Scripts/Economy/ResourceScarcityDirector.cs(27,93): CS0535 ResourceScarcityDirector does not implement IResourceScarcityReadModel.GetCraftPowerMultiplier(RecipeData)`
- `RecipeData` is `Hecton8.Crafting.RecipeData`; implementation already imports `Hecton8.Crafting`, contract file did not.

What was done:
- Added `using Hecton8.Crafting;` to `GlobalRegistryContracts.cs`.

Cinematic Cheats used:
- None. Compile wall fix only.

Exact Microseconds saved:
- 0 runtime. Failed build consumed about 330860000 us; source fix took about 10000 us; retry guard consumed about 51900000 us.

Verification:
- Source grep confirms the contract and implementation both import/use `RecipeData`.
- `git diff --check` on touched loop files: LF->CRLF warnings only.
- Retry build not launched: 8 guard attempts stayed red (`CpuLoad` 77-100, compiler processes 1-2); latest sample `CpuLoad=85`, `CompilerProcessCount=2`, assets json present.
- X_011 scanner remains `PASS_STATIC_HOT_ROUTE` / `PASS_STATIC_PRIORITY_PROOF`.
- Last successful compile remains Loop 53; runtime profiler/GCMonitor proof: not run.

## 2026-05-25 - X_011 Loop 68 Post-Fix Build Guard Retry

What was wrong:
- The `RecipeData` contract import fix still needed build verification.

What was done:
- Polled the build guard for 12 samples after Loop 67.

Cinematic Cheats used:
- None. Guard only.

Exact Microseconds saved:
- 0 measured. Guard wait consumed about 74600000 us.

Verification:
- Build not launched. CPU stayed above 50 on every sample: 68, 70, 76, 100, 80, 91, 91, 88, 100, 100, 100, 100.
- Compiler process count was 0 and assets json was present, but CPU guard alone blocked the retry.
- X_011 scanner remains `PASS_STATIC_HOT_ROUTE` / `PASS_STATIC_PRIORITY_PROOF`.
- Last successful compile remains Loop 53; runtime profiler/GCMonitor proof: not run.
