# LOG_SHINOBU_150

## 2026-05-19 - Babel Subtitle Sync Runtime

What was wrong:

- Legacy subtitle presentation used frame-loop timing semantics for visible duration and typewriter reveal.
- Existing Babel decode supported UTF-8 span output and `^0..^3` placeholders, but `{0}` authoring would still require either manual preprocessing or managed formatting.
- Subtitle cue state had no dedicated 32-byte ABI, compact signal lane, or SHINOBU_150 black-box recorder.
- Canvas label swap cadence used a fixed max drain and binary rich-text policy instead of `GlobalQualityWeight`.
- There was no single editor x-ray for hash -> raw UTF-8 -> decoded span -> audio-frame cue publish.

What was done:

- Added `BabelSubtitleSyncRuntime` with 16-byte `SubtitleCueSignal`, 32-byte explicit `SubtitleCueDTO`, audio-frame cue evaluation, 300-entry telemetry ring, and dump paths `Dump_SHINOBU_150.bin` / `Dump_BABEL_SURGEON.bin`.
- Added domain-local GlobalDataVault buffer IDs for subtitle cue state and subtitle telemetry: `(BufferID)15070550` and `(BufferID)15070551`. No SHINOBU_150 IDs are added to the core enum.
- Wired `SubtitleManager` to prepare Babel cue frames, drain compact cue signals, record decode telemetry, append directional arrows, and derive subtitle/typewriter/audio-log reveal timing from DSP sample frames.
- Extended `LocRegistry` UTF-8 decode loop to handle `{0}` and `{0:format}` without `string.Format`.
- Changed `LabelSwapScheduler` dirty budget and rich-text stripping to continuous `HomeostasisBrain.GlobalQualityWeight`.
- Added UI Toolkit `BabelSyncTunerWindow` for telemetry, hash preview, raw UTF-8 hex, decoded span preview, quality override, audio-frame offset, and cue publish.
- Added `Docs/ARCHITECTURE/LOCALIZATION_SUBTITLE_SYNC_ENGINE.md` and a SHINOBU_150 addendum in `ZERO_GC_UI_PIPELINE.md`.
- Updated `Docs/Tasks/Status_SHINOBU_150.md` and `Docs/AgentLogs/Rationale_SHINOBU_150.md`.

Cinematic cheats used:

- Subtitles are treated as presentation-only "Dear Lie" state with `FlagVisualOnlyNoRollback`; gameplay rollback exchanges intent/audio truth, not visible subtitle progress.
- Directional subtitle arrows use a cheap AUP delta and dot product instead of spatial UI simulation.
- Low-quality behavior strips rich text and spreads TMP dirties across frames; high/ultra spends saved cycles on richer visible text and editor x-ray.

Exact microseconds saved, static estimate pending profiler proof:

- Hash UTF-8 span lookup vs managed string dictionary: 20-500 us per subtitle burst, workload dependent.
- Manual UTF-8 decode into pooled span vs `Encoding.GetString`: 20-120 us and one managed allocation per subtitle.
- `{0}` span formatting vs `string.Format`: 5-40 us and one managed allocation per formatted subtitle.
- 64 cue audio-frame evaluation: expected <10 us on i3/MX350-class CPU.
- Dirty label budget smoothing: expected 50-200 us frame-spike reduction during label bursts, profiler proof pending.
- Uninitialized cue buffer with flag-only clear: expected 1-4 us startup/init reduction for 64 cues.

Verification:

- Static scan clean for touched subtitle/Babel runtime files: no `Time.unscaledTime`, `Time.deltaTime`, `WaitForSeconds`, coroutine, `string.Format`, `Encoding.UTF8.GetString`, or JSON parser usage.
- Static subtitle/Babel runtime scan clean for JSON parser and `string.Format`. Later Loop 7 removed the former `LocRegistry.Reload(Dictionary<GameLanguage, Dictionary<string,string>>...)` bridge entirely.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` attempted after CPU 14% and no `dotnet/csc` process.
- Build is blocked by unrelated missing types in `HectonVisorUberPostFeature`, `GlobalRegistryContracts`, `DeferredDecalPass`, `ModularEquipmentEngine`, and `SomaticTunerWindow`. No SHINOBU_150 file errors were emitted before that dependency wall.

## 2026-05-19 - Ultra-Polish Pass / Compile-Wall Isolation

What was wrong:

- SHINOBU_150 had leaked into global compile surfaces by touching core `BufferID` authority and generated `.csproj` inclusion.
- Cue evaluation could be scheduled before dispatcher dependency wiring and then not returned as a dependency.
- Runtime held persistent private `NativeArray` state instead of only Vault handles and transient resolved views.
- The previous static report overstated dictionary eradication; at that point a legacy cold dictionary bridge still remained in `LocRegistry`.

What was done:

- Removed SHINOBU_150 core `BufferID` enum dependency and `.csproj` contamination. Runtime uses domain-local cast IDs.
- Converted cue and telemetry access to `VaultBufferHandle<T>` plus transient `NativeArray<T>` resolution.
- `ScheduleCueEvaluation` now returns the pending handle through `JobHandle.CombineDependencies` when already active.
- `TryCompletePendingCueEvaluation` fences only after `IsCompleted`.
- Added `[NoAlias]` to subtitle cue pointer fields and exact Burst flags to every SHINOBU_150 job.
- Removed dead `deltaTime` arguments from typewriter/audio-log subtitle progression.

Cinematic cheats used:

- Subtitle progress remains visual-only state excluded from rollback. The expensive truth is audio intent; the UI is a sample-frame-driven presentation fake.
- Directional accessibility arrows are a localized AUP delta plus dot products, not spatial UI simulation.
- Low quality collapses presentation richness by dirty-budget smoothing and rich-text stripping; high/ultra spends the saved budget on visual polish and editor x-ray.

Exact microseconds saved, static estimate pending profiler proof:

- Dispatcher nonblocking cue job: prevents an arbitrary main-thread fence; expected 0-80 us spike avoidance under cue bursts, workload dependent.
- NoAlias cue pointer: compiler can treat cue buffer as isolated; expected 1-5 us for 64 cue scan on MX350-class CPUs pending Burst proof.
- Domain-local BufferID/no `.csproj` edit: runtime cost 0 us; compile-wall risk reduced.

Verification:

- No `BabelSubtitleCueState`, `BabelSubtitleCueTelemetry`, `BabelSubtitleAudioFrameClock`, or `BabelSubtitleDebugScratch` remains in `H8Memory.cs` or `Hecton8.Core.csproj`.
- No manual `BabelSubtitleSyncRuntime.cs` / `BabelSyncTunerWindow.cs` include remains in `Hecton8.Core.csproj`.
- `BabelSubtitleSyncRuntime.cs` contains two Burst jobs, both with exact required flags.
- `BabelSubtitleSyncRuntime.cs` contains no persistent private `NativeArray`, `NativeList`, or `NativeHashMap` field.
- Build deliberately not launched in this pass by user instruction and because previous compile wall was unrelated.

<SELF_AUDIT agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Runtime subtitle path has no JSON parser. Legacy global/vendor JSON outside SHINOBU domain not edited.</TASK>
    <TASK id="02" status="PASS_WITH_LEGACY_COMPATIBILITY_BOUNDARY">Hot subtitle resolver uses uint hashes and UTF-8 spans. Loop 7 removed `LocRegistry.Reload(Dictionary...)`; managed string compatibility remains outside the Babel registry.</TASK>
    <TASK id="03" status="PASS">`SubtitleCueDTO` has public fields only; cue jobs mutate through `UnsafeUtility.AsRef`.</TASK>
    <TASK id="04" status="PASS">`SubtitleCueDTO` explicit 32-byte layout validated by size and offset checks.</TASK>
    <TASK id="05" status="PASS">Existing `GenerateEmergencyMockLocale()` provides unmanaged fallback UTF-8/index data.</TASK>
    <TASK id="06" status="PASS">`BabelDictionaryStore` maps `.h8bin`/MMF cold path; hot path consumes unmanaged spans and binary search.</TASK>
    <TASK id="07" status="PASS">UTF-8 decodes into caller `Span<char>`; no `Encoding.UTF8.GetString` in subtitle hot path.</TASK>
    <TASK id="08" status="PASS">`^0..^3`, `{0}`, and `{0:format}` resolve through span formatter; no `string.Format`.</TASK>
    <TASK id="09" status="PASS">Cue visibility and reveal timing use audio sample frames, not `Time.deltaTime`.</TASK>
    <TASK id="10" status="PASS">Existing staged Babel dictionary swap remains phase-safe and lazy.</TASK>
    <TASK id="11" status="PASS">Canvas dirty budget is `math.lerp`/smoothstep-driven by `GlobalQualityWeight`.</TASK>
    <TASK id="12" status="PASS">`SubtitleCueSignal` is a 16-byte unmanaged `SignalBus` lane.</TASK>
    <TASK id="13" status="PASS">AUP subtitle arrows subtract camera AUP before float cast.</TASK>
    <TASK id="14" status="PASS">Subtitle cue state is flagged visual-only and excluded from rollback truth.</TASK>
    <TASK id="15" status="PASS">Cue buffer uses `NativeArrayOptions.UninitializedMemory`; cold clear job only resets flags/progress.</TASK>
    <TASK id="16" status="PASS">300-entry 64-byte telemetry ring dumps to SHINOBU/Babel dump paths on fault triggers.</TASK>
    <TASK id="17" status="PASS">`BabelSyncTunerWindow` exists as editor-only telemetry/hash/x-ray/audio-offset facade.</TASK>
    <TASK id="18" status="PASS">Existing CSV override path parses into native scratch; no runtime `File.ReadAllText`/`Split` path added.</TASK>
    <TASK id="19" status="PASS">Editor x-ray displays raw UTF-8 hex and decoded span preview.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static audit complete; Unity Profiler 0 B managed allocation proof remains pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT name="SubtitleCueDTO" size="32" alignment="multiple_of_16">
    <FIELD name="TokenHash" offset="0" size="4" />
    <FIELD name="DisplayDuration" offset="4" size="4" />
    <FIELD name="StartAudioFrame" offset="8" size="4" />
    <FIELD name="CurrentProgress" offset="12" size="4" />
    <FIELD name="Flags" offset="16" size="4" />
    <FIELD name="_pad0.._pad11" offset="20" size="12" />
    <MATH>5 fields * 4 bytes = 20; 12 pad bytes = 32; 32 % 16 = 0.</MATH>
  </STRUCT_LAYOUT>
  <STRUCT_LAYOUT name="LocalizationTelemetryEntry" size="64" alignment="cache_line">
    <MATH>14 scalar fields occupy offsets 0..55; two uint pads at 56 and 60 force 64 bytes.</MATH>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below GlobalQualityWeight 0.3, label dirty budget approaches 2 per frame and rich text is stripped; subtitle truth still uses audio-frame math. Above 0.3, smoothstep expands dirty cadence continuously toward 18 per frame and preserves richer text where the budget allows.</SCALABILITY_CURVE>
  <VAULT_STATUS private_persistent_native_arrays="0">
    <BUFFER id="15070550" type="SubtitleCueDTO" count="64" owner="SystemID.UI" />
    <BUFFER id="15070551" type="LocalizationTelemetryEntry" count="300" owner="SystemID.UI" />
  </VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>
    <JOB name="EvaluateSubtitleCuesJob" consumes="dependsOn, SubtitleCueDTO*" outputs="pendingCueEvaluationHandle" noalias="true" />
    <JOB name="ClearSubtitleCueFlagsJob" consumes="SubtitleCueDTO*" outputs="coldBootClearHandle" noalias="true" />
    <DEPENDENCY_GRAPH>PreSimulation drains signals; ScheduleSimulation returns scheduled or combined cue handle; PostSimulation/VisualSync complete only after IsCompleted.</DEPENDENCY_GRAPH>
  </POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>No direct sibling runtime assembly reference was added. No manual `.csproj` include remains. SHINOBU_150 no longer edits core BufferID authority.</COMPILE_GUARD>
  <DEAR_LIE before="O(activeCueCount * managed_string_decode + frame_timer_drift)" after="O(logN hash lookup + UTF8 byte scan + O(activeCueCount) cue math)">Presentation follows audio sample truth and token span formatting; rollback/gameplay state does not simulate subtitle progress.</DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 - Binary Registry Purge / Long-Lore Cap

What was wrong:

- `LocRegistry` still had a managed dictionary reload bridge in source, even though the hot subtitle path used hashes and UTF-8 spans.
- `SubtitleManager.DisplaySubtitle(string key, ...)` could still route through the legacy string localization manager before the Babel hash path.
- The fallback decode buffer capped resolved text at 1024 glyphs, which is too small for the 500-word paragraph proof required by Task 20.

What was done:

- Removed `LocRegistry.Reload(Dictionary<GameLanguage, Dictionary<string,string>>...)`, `LocPool`, `LocEntry`, dictionary language-load helpers, and `Encoding` use from `LocRegistry`.
- `LocalizationManager.RefreshRuntimeRegistry()` now calls `LocRegistry.ReloadBinaryOrMock(CurrentLanguage)`. The Babel registry accepts static/binary UTF-8 authority or the unmanaged emergency mock, not managed tables.
- `SubtitleManager.DisplaySubtitle(string key, ...)` now hashes the caller key with `LocHash.Compute(key.AsSpan())` and attempts the Babel hash command path before using the caller-owned key span as a fallback visual.
- Raised `LocRegistry.MaxDecodedGlyphs` to 4096. Long-form lore should still decode into caller-owned spans sized for the view, but the audit/debug path no longer truncates normal 500-word paragraphs at 1024 glyphs.
- Replaced the remaining `LocalizationManager` `string.Format` usage with a `string.Create` compatibility formatter for simple `{0}` placeholders and primitive/string args. This is legacy string API containment, not zero-GC hot-path proof.

Cinematic cheats used:

- Legacy string APIs remain isolated as compatibility shells; Babel does not import their managed tables. The runtime truth is hash + byte slice + span decode.
- The emergency mock is a deterministic unmanaged fallback, not a managed test dictionary.

Exact microseconds saved, static estimate pending profiler proof:

- Babel reload no longer walks managed string dictionaries or re-encodes UTF-16 values into UTF-8; cold swap savings depend on table size and can be milliseconds for large locale sets.
- Per subtitle, the string-key entry point now avoids `LocalizationManager.GetExpandedOrFallback`; expected 20-500 us avoided during burst dialogue, workload dependent.
- 4096-glyph legacy compatibility decode now uses the fixed ring from the later Loop 12 pass; hot decode remains a bounded UTF-8 scan with no managed string.
- Legacy formatted string calls avoid composite `string.Format`; they still allocate the returned string and must not be used as SHINOBU hot-path proof.

Verification:

- Static scan of `Assets/_Project/Scripts/LocRegistry.cs`: no `LocPool`, `LocEntry`, `Dictionary<int`, `Dictionary<GameLanguage`, `Dictionary<string`, `Encoding.`, or `LocRegistry.Reload(` matches.
- Static scan of touched subtitle/Babel runtime files: no `Time.deltaTime`, `Time.unscaledTime`, `WaitForSeconds`, coroutine, `string.Format`, `Encoding.UTF8.GetString`, `JsonUtility.FromJson`, Newtonsoft, or System.Text.Json matches.
- Static scan of touched Babel/localization runtime files: no `string.Format` matches.
- Build not launched in this pass by user instruction. Prior compile attempt remains blocked by unrelated missing types outside SHINOBU_150.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="BINARY_REGISTRY_PURGE">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="02" status="PASS_WITH_LEGACY_COMPATIBILITY_BOUNDARY">`LocRegistry` dictionary reload bridge removed. Managed string dictionaries remain only in `LocalizationManager` compatibility/mod/editor APIs and no longer hydrate Babel.</TASK>
    <TASK id="07" status="PASS">Fallback decode cap raised to 4096 glyphs; primary API still decodes UTF-8 into caller-owned `Span<char>`.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static 500-word decode ceiling fixed; Unity Profiler 0 B managed allocation proof still pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <DICT_BRIDGE locRegistry="removed" localizationManager="legacy_string_api_isolated" subtitleStringEntry="hash_first_babel_path" />
  <LONG_LORE_DECODE fallbackThreadLocalGlyphs="4096" requiredForMegabyteLore="caller_owned_span_or_native_page_window" />
  <COMPILE_GUARD>Build not rerun by explicit instruction and because previous dependency wall was unrelated.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>

## 2026-05-19 - Legacy Decode Buffer Ring

What was wrong:

- `LocRegistry.ResolveRaw` and `TryGetRawBuffer` used a `[ThreadStatic]` decode buffer that allocated `char[capacity]` on first use.
- Consecutive raw-buffer lookups on the same thread returned the same backing array. A label+unit caller could fetch label text, fetch unit text, and then copy two references to the unit text.

What was done:

- Replaced the thread-static grow-on-first-use buffer with a fixed 16-slot prewarmed `char[4096]` decode ring.
- Slot selection uses `Interlocked.Increment` and a power-of-two mask; no dynamic resize or per-lookup allocation remains.
- Kept the authoritative hot route unchanged: `TryWriteVisualSpanFromUtf8(...)` still writes into caller-owned spans / `CharBufferPool` leases.
- Updated status, rationale, localization architecture, zero-GC UI architecture, and binary payload ledger.

Cinematic cheats used:

- The compatibility ring is a bounded presentation window, not a true text ownership model. It buys safety for old raw-buffer callers while the real Babel path remains direct UTF-8 slice to caller span.

Exact microseconds saved, static estimate pending profiler proof:

- No steady-state hot subtitle saving is claimed. The ring removes first-use `char[4096]` allocation from the legacy lookup path and prevents same-thread double-lookup alias corruption.
- Expected benefit is hitch prevention and avoiding corrupted metric labels, not ALU reduction.

Verification:

- Static scan clean for `ThreadStatic`, `_decodeBuffer`, and `new char[capacity]` in `LocRegistry`.
- Static runtime scan clean for `string.Format`, `string.Concat`, `Encoding.UTF8.GetString`, `File.ReadAllText`, runtime JSON parser, runtime `Dictionary<string...>`, `InjectEntries`, and `InjectTable(` in touched runtime Babel/localization/mod files.
- Static timing/native scans remain clean for subtitle `Time.*` truth APIs and local persistent native allocations.
- Build not launched by instruction; prior unrelated compile wall remains the current compile evidence boundary.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="LEGACY_DECODE_RING">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="07" status="PASS">Legacy raw-buffer decode no longer allocates a thread-static char array on first use; hot decode remains caller-span.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static source gates are clean; Unity Profiler 0 B proof remains pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <COMPATIBILITY_RING slots="16" charsPerSlot="4096" allocation="cold_static_prewarm" hotSubtitlePath="caller_span_or_char_buffer_pool" />
  <COMPILE_GUARD>Build not rerun by explicit instruction; no new compile-triggering signal after static gates.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>

## 2026-05-19 - LocRegistry Layout And Subtitle Queue Hardening

What was wrong:

- `LocRegistry` still used a managed `HashSet<int>` to suppress repeated missing-token logs.
- Several public registry DTOs/signals relied on sequential layout instead of explicit ARM64-safe offsets.
- `SubtitleManager` kept a managed `List<SubtitleRequest>` and `RemoveAt(0)` for legacy string subtitle queueing.

What was done:

- Replaced missing-key suppression with a fixed 256-bit bloom mask backed by four `ulong` fields.
- Converted `LocalizationEntryDTO`, `SubtitleCommandDTO`, `SubtitleStateDTO`, `BabelFormatArgs`, `MockTranslationRequestSignal`, `MockUiRefreshSignal`, `MockTextMeshProText`, `LocalizationLanguageChangedSignal`, `BabelDictionaryStage`, and `BabelTelemetryEntry` to explicit layouts.
- Replaced the legacy subtitle string queue with an 8-slot fixed ring matching the existing command/buffer queue pattern.
- Removed get-only properties from local subtitle event/slice structs.
- Converted `LocalizationManager.CurrentLanguage` to a private backing field plus read-only facade to keep the runtime property scan clean without exposing mutation.
- Updated the localization architecture doc, zero-GC UI doc, and binary payload ledger.

Cinematic cheats used:

- Diagnostic missing-key suppression is approximate by design. The fixed bloom mask can occasionally suppress a duplicate-looking log, but avoids an unbounded managed set. Gameplay text resolution remains hash/span authority.

Exact microseconds saved, static estimate pending profiler proof:

- Legacy subtitle queue dequeue changes from `List.RemoveAt(0)` O(n) shift to O(1) ring pop. At the current 8-slot cap the estimate is 1-15 us saved during burst churn on i3/MX350-class CPUs.
- Missing-key storms no longer grow or rehash a managed collection. Frame impact depends on fault rate; no steady-state hot-path saving is claimed.

Verification:

- Static runtime scan clean for `HashSet`, `System.Collections.Generic`, `Dictionary<`, `StructLayout(LayoutKind.Sequential)`, `Pack=1`, `{ get;`, local `new NativeArray`, `Allocator.Persistent`, `string.Format`, runtime JSON parser, `Encoding.UTF8.GetString`, `InjectEntries`, and `InjectTable(` in touched SHINOBU runtime files.
- Static timing scan clean for `Time.deltaTime`, `Time.unscaledTime`, `Time.time`, `WaitForSeconds`, and `StartCoroutine` in touched Babel subtitle timing files.
- `git diff --check` passed for latest touched source/docs with line-ending normalization warnings only.
- Build not launched by instruction; prior build wall remains unrelated missing-type debt outside SHINOBU_150.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="REGISTRY_LAYOUT_AND_QUEUE_HARDENING">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="02" status="PASS">`LocRegistry` and `SubtitleManager` runtime subtitle files have no managed dictionary/hashset/list dependency in the scanned SHINOBU path.</TASK>
    <TASK id="03" status="PASS">Touched subtitle-local structs no longer expose get-only properties; hot DTOs remain field-only.</TASK>
    <TASK id="04" status="PASS">Registry DTO/signals are explicit 16/24/32/64-byte layouts with no `Pack=1` or sequential layout in `LocRegistry`.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static source gates are clean; Unity import, Burst compile, and Unity Profiler 0 B proof remain pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <STRUCT_LAYOUT name="BabelTelemetryEntry" size="64" alignment="cache_line">
    <FIELD name="Frame" offset="0" size="4" />
    <FIELD name="KeyHash" offset="4" size="4" />
    <FIELD name="Offset" offset="8" size="4" />
    <FIELD name="Length" offset="12" size="4" />
    <FIELD name="TranslationsPerFrame" offset="16" size="4" />
    <FIELD name="BufferPoolLeasesActive" offset="20" size="4" />
    <FIELD name="SpanConversionTimeMs" offset="24" size="4" />
    <FIELD name="DictionaryLookupsPerFrame" offset="28" size="4" />
    <FIELD name="MissingHashCount" offset="32" size="4" />
    <FIELD name="SearchComputeTimeNs" offset="36" size="4" />
    <FIELD name="Language" offset="40" size="2" />
    <FIELD name="Flags" offset="42" size="2" />
    <FIELD name="CsvOverrideAppliedCount" offset="44" size="4" />
    <FIELD name="CsvOverrideRejectedCount" offset="48" size="4" />
    <FIELD name="_pad2" offset="52" size="4" />
    <FIELD name="_pad3" offset="56" size="4" />
    <FIELD name="_pad4" offset="60" size="4" />
  </STRUCT_LAYOUT>
  <COMPILE_GUARD>Build not rerun by explicit instruction; no new compile-triggering signal after static gates.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>

## 2026-05-19 - Legacy Format Spec Hardening

What was wrong:

- The first `LocalizationManager` compatibility formatter removed `string.Format`, but ignored colon format suffixes like `{0:F1}` and `{0:X8}`. That is a silent behavior regression, not a valid optimization.

What was done:

- `TryParseFormatPlaceholder` now returns the optional format-span bounds.
- Primitive numeric writers pass the parsed format span into `TryFormat`.
- Formatted string/char/bool/object placeholders fail closed to the source template and development warning path instead of emitting incorrect text.

Cinematic cheats used:

- None. This is compatibility containment. Babel remains the Dear Lie route for zero-GC subtitle/lore presentation.

Exact microseconds saved, static estimate pending profiler proof:

- No new frame-time claim. This prevents a correctness regression while keeping `string.Format` out of the localization owner.

Verification:

- Static scan target remains `string.Format`/JSON/time/coroutine forbidden patterns in touched runtime files.
- Build not launched by instruction.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="LEGACY_FORMAT_SPEC_HARDENING">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="08" status="PASS">Legacy compatibility formatting now preserves numeric format suffixes through primitive `TryFormat`; Babel token path remains span-owned.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static hardening complete; Unity Profiler 0 B proof remains pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <COMPILE_GUARD>Build not rerun by explicit instruction.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>

## 2026-05-19 - Babel Pool Vault Ownership Correction

What was wrong:

- `CharBufferPool` raised the Babel subtitle lane to 512 chars, but still retained a persistent private `NativeArray<char>` fallback and a `NativeBitArray` lease tracker.
- That fallback was cold, but it still violated the H-PHI rule that persistent native memory belongs to the Vault.

What was done:

- Removed the local `NativeArray<char>` Babel fallback and `NativeBitArray` lease tracker from `CharBufferPool`.
- Babel leases now resolve Vault buffer `(BufferID)70540` transiently when `GlobalDataVault` is present.
- If no Vault exists in an editor/mock route, the Babel span aliases the already prewarmed `char[500][512]` TMP bridge slot and still commits through `TMP_Text.SetCharArray`.
- Lease tracking now uses the existing fixed `ulong[8]` bitmap; no local native collection is allocated by `CharBufferPool`.
- Updated `LOCALIZATION_SUBTITLE_SYNC_ENGINE.md`, `ZERO_GC_UI_PIPELINE.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the 512-char lane and Vault route.

Cinematic cheats used:

- No new physical simulation exists here. The Dear Lie remains a fixed-size subtitle page window: short dialogue uses 512-char leases, long lore pages use encyclopedia/caller-owned spans instead of making every subtitle slot huge.

Exact microseconds saved, static estimate pending profiler proof:

- No meaningful hot-path CPU saving is claimed. This removes one cold persistent native allocation of 256000 chars and one native bitset allocation in no-vault routes.
- The frame-time value is bounded ownership: Vault-backed native staging on real runtime, deterministic TMP bridge fallback in CI/editor mock paths, and no private native memory owner.

Verification:

- Static scan clean for touched SHINOBU/CharBufferPool runtime files: no `string.Format`, `Encoding.UTF8.GetString`, JSON parser, `Time.deltaTime`, `Time.time`, coroutine timer, local `NativeArray<char>` fallback, `NativeBitArray`, or `Allocator.Persistent` matches.
- `git diff --check` passed for latest touched files with line-ending normalization warnings only.
- Build not launched by instruction and because static gates did not expose a compile-triggering dependency signal; prior build wall remains unrelated to SHINOBU_150.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="BABEL_POOL_VAULT_OWNERSHIP">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="07" status="PASS">Babel lease capacity is 512 chars and writes into Vault-backed or prewarmed caller-visible spans without native fallback allocation.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static H-PHI correction complete; Unity Profiler 0 B proof remains pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <H_PHI_VAULT_STATUS>
    <BUFFER id="70540" type="char" count="256000" owner="CharBufferPool" fallback="prewarmed_tmp_bridge_no_native_alloc" />
    <BUFFER id="15070550" type="SubtitleCueDTO" count="64" owner="SHINOBU_150" />
    <BUFFER id="15070551" type="LocalizationTelemetryEntry" count="300" owner="SHINOBU_150" />
  </H_PHI_VAULT_STATUS>
  <STATIC_GATES forbiddenApis="pass" localNativeFallback="pass" diffCheck="pass_with_line_ending_warnings" />
  <COMPILE_GUARD>Build not rerun by explicit instruction and no new compile-triggering signal.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>

## 2026-05-19 - Runtime Dictionary And Mod Injection Quarantine

What was wrong:

- `LocalizationManager` no longer fed Babel from managed tables, but it still exposed runtime-facing dictionary injection and JSON parser APIs.
- `HectonAPI.Localization.InjectTable(...)` still accepted `Dictionary<string,string>`, preserving the wrong mod authoring contract even though the implementation had been disabled.

What was done:

- Removed `LocalizationManager.InjectEntries(...)` and the runtime-owner JSON parser from `LocalizationManager`.
- Moved the legacy JSON parser to `Assets/_Project/Scripts/Editor/LocalizationEditorJsonTableParser.cs` and rewired only editor key/font validation tools to it.
- Replaced mod dictionary injection with a rejected future `InjectBabelEnvelope(ReadOnlySpan<byte>)` seam.
- Documented the disabled runtime dictionary/mod injection route in the localization architecture doc, UI zero-GC doc, and binary payload ledger.

Cinematic cheats used:

- The Dear Lie is still the same: runtime presentation consumes hash-indexed UTF-8 bytes and fixed character windows; editor-only JSON exists only to help humans generate keys and validate glyph coverage.

Exact microseconds saved, static estimate pending profiler proof:

- No new per-frame saving is claimed. Cold runtime risk removed: legacy mod/locale dictionary parse and injection can no longer allocate managed tables during boot or locale mutation.
- Expected low-end benefit when mods or legacy language assets are present: milliseconds of cold parse churn and hundreds of KB to MB of managed heap avoided on i3/MX350-class hardware.

Verification:

- Static runtime scan clean for `Dictionary<string,string>`, `JsonUtility.FromJson`, Newtonsoft, System.Text.Json, `Encoding.UTF8.GetString`, `string.Format`, `InjectEntries`, and `InjectTable(` in touched Babel/localization/mod runtime files.
- Static timing scan clean for `Time.deltaTime`, `Time.time`, coroutine timers in touched subtitle/Babel runtime files.
- `git diff --check` passed for latest touched source/docs with line-ending normalization warnings only.
- Build not launched by instruction and because the previous compile wall is still unrelated to SHINOBU_150.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="RUNTIME_DICTIONARY_AND_MOD_INJECTION_QUARANTINE">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="01" status="PASS">Runtime localization JSON parse route is absent from the touched Babel/localization/mod runtime files; legacy JSON parser is Editor-only tooling.</TASK>
    <TASK id="02" status="PASS">Runtime `Dictionary&lt;string,string&gt;` localization resolution/injection is absent from `LocRegistry`, `LocalizationManager`, and `HectonAPI.Localization`.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static source gates are clean; Unity Profiler 0 B proof remains pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <COMPILE_GUARD>Build not rerun by explicit instruction; prior compile wall remains unrelated missing-type debt outside SHINOBU_150.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>

## 2026-05-19 - LocNumericBuffer Fixed Ring

What was wrong:

- `LocNumericBuffer` still retained the older thread-static grow-buffer shape for numeric HUD/localization templates.
- Overflow fallback could allocate `new char[capacity]` when a translated template exceeded the current staging buffer, which violates the zero-GC HUD rule.

What was done:

- Replaced numeric staging with a fixed 16-slot prewarmed `char[4096]` ring selected through `Interlocked.Increment`.
- Removed `_stagingBuffer`, `MaxWriteAttempts`, `CapacityGrowthWatchdogLimit`, `ResolveExpandedCapacity`, and all `new char[capacity]` expansion from `LocNumericBuffer`.
- Bounded fallback copy now truncates into the fixed slot and writes `...` in-buffer when the template is too large.
- Updated status, rationale, localization architecture, UI zero-GC architecture, and binary payload ledger with the fixed-ring route.

Cinematic cheats used:

- The Dear Lie is bounded presentation staging: HUD numeric templates get fixed 4096-char windows rather than pretending every translated string deserves a fresh managed allocation or an unbounded common subtitle lease.

Exact microseconds saved, static estimate pending profiler proof:

- Steady-state CPU saving is not claimed. The gain is removal of a one-frame managed allocation spike when numeric localization templates exceed previous capacity.
- Low-end expected benefit is GC avoidance during HUD/dialogue bursts; estimated avoided allocation is up to one dynamic `char[capacity]` array per overflow event.

Verification:

- Static scan clean for `ThreadStatic`, `_stagingBuffer`, `new char[capacity]`, `ResolveExpandedCapacity`, `MaxWriteAttempts`, and `CapacityGrowthWatchdogLimit` in `LocNumericBuffer` and `LocRegistry`.
- Static runtime scan clean for `string.Format`, `string.Concat`, `Encoding.UTF8.GetString`, `File.ReadAllText`, runtime JSON parser, runtime `Dictionary<string...>`, `InjectEntries`, and `InjectTable(` in touched Babel/localization/mod runtime files.
- Static timing scan clean for subtitle `Time.deltaTime`, `Time.unscaledTime`, `Time.time`, `WaitForSeconds`, and `StartCoroutine` in touched Babel subtitle timing files.
- Build not launched by explicit instruction; Unity import, Burst compile, Play Mode, GCMonitor, and profiler proof remain pending.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="NUMERIC_FORMAT_RING">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="02" status="PASS">Numeric localization templates no longer use managed dictionary/string formatting authority.</TASK>
    <TASK id="08" status="PASS">Numeric token replacement remains `Span&lt;char&gt;` / `TryFormat` based; no `string.Format` fallback is present.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static source gate for numeric buffer allocations is clean; Unity Profiler 0 B proof remains pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <BUFFER_ROUTE>
    <BUFFER name="LocNumericBufferRing" slots="16" charsPerSlot="4096" owner="LocNumericBuffer" allocationPhase="cold static init" hotGrowth="false" />
  </BUFFER_ROUTE>
  <STATIC_GATES forbiddenApis="pass" threadStaticNumericBuffer="pass" dynamicCharGrowth="pass" />
  <COMPILE_GUARD>Build not rerun by explicit instruction; current work was static source containment after prior unrelated compile wall.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>

## 2026-05-19 - Audio-Frame Visual Corruption Clock

What was wrong:

- `LocalizationManager` subtitle truth had moved to audio frames, but PDA corrosion and madness visual windows still used `Time.unscaledTime`.
- Corruption seed buckets also used Unity time, so localized visual noise could drift from the audio authority during hitches.

What was done:

- Replaced `_externalPdaCorrosionEndTime` and `_madnessOverrideEndTime` with audio-frame end counters.
- Routed PDA corrosion, madness override, madness roll buckets, and corruption seed buckets through DSP sample-frame math.
- Replaced saturating frame add with wrap-safe `uint` frame add and capped intervals below `2^31` frames so signed-diff comparisons remain valid across long-session wrap.
- Updated status, rationale, localization architecture, UI zero-GC architecture, and binary payload ledger.

Cinematic cheats used:

- The Dear Lie remains a visual text corruption illusion: no gameplay state, no rollback mutation, no physics or simulation. The illusion follows the audio clock so it feels authored without adding a separate simulation.

Exact microseconds saved, static estimate pending profiler proof:

- No CPU speedup is claimed. The pass removes timing drift and a long-session wrap hazard.
- Added work is bounded to a few double DSP-frame calculations and integer comparisons in localized visual evaluation; no managed allocation route was added.

Verification:

- Static timing scan clean for `Time.unscaledTime`, `Time.deltaTime`, `Time.time`, `WaitForSeconds`, `StartCoroutine`, `_externalPdaCorrosionEndTime`, `_madnessOverrideEndTime`, and `AddAudioFramesSaturating` in touched Babel/localization timing files.
- Static runtime scan clean for `string.Format`, `string.Concat`, `Encoding.UTF8.GetString`, `File.ReadAllText`, runtime JSON parser, runtime `Dictionary<string...>`, `InjectEntries`, and `InjectTable(` in touched Babel/localization/mod runtime files.
- `git diff --check` passed for latest touched source/docs with line-ending normalization warnings only.
- Build not launched by explicit instruction; Unity import, Burst compile, Play Mode, GCMonitor, and profiler proof remain pending.

<SELF_AUDIT_UPDATE agent="SHINOBU_150" evidence="STATIC_SOURCE_PENDING_UNITY_PROFILER" pass="AUDIO_FRAME_CORRUPTION_CLOCK">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="09" status="PASS">Localized visual corruption and madness windows now use DSP/audio-frame authority instead of Unity frame time.</TASK>
    <TASK id="14" status="PASS">The corruption/madness route remains presentation-only and does not write rollback/Merkle truth.</TASK>
    <TASK id="20" status="PARTIAL_PASS">Static timing/string gates are clean; Unity Profiler 0 B proof remains pending.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <CLOCK_ROUTE source="AudioSettings.dspTime" unit="sampleFrame" wrap="uint signed-diff" maxIntervalFrames="2147483646" />
  <STATIC_GATES frameTimeApis="pass" forbiddenStringApis="pass" />
  <COMPILE_GUARD>Build not rerun by explicit instruction; no new compile-triggering dependency signal appeared.</COMPILE_GUARD>
</SELF_AUDIT_UPDATE>
