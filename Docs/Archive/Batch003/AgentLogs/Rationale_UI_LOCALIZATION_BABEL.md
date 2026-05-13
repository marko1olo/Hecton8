# Rationale_UI_LOCALIZATION_BABEL

Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL DEPENDENCIES
Agent: UX_ENGINEER
Prompt ID: UI_LOCALIZATION_BABEL

## Mandate Ingestion

Problem: Localization task spans UI, registry, native data, jobs, font swap, and AUP world text.
Solution: Loaded only relevant mandates: Babel localization, UI zero-GC streaming, GlobalRegistry DI, zero-GC policy, native memory/jobs, crash telemetry, and AUP origin safety.
Rejected Alternatives: Bulk loading all .agents-skills would add irrelevant physics/rendering noise and increase risk of cross-domain edits.
Scalability potential: Low uses static baked glyph atlases and raw byte spans; Middle adds staged font swap; High/Ultra can spend saved CPU/GC on richer glyph sets and visual text decay without hot-path allocation.
Hardware Impact: Expected low-end gain on i3/MX350 is prevention of language-switch GC spikes and UI refresh heap churn; exact microseconds pending profiler/build evidence.

## Decision 0 - Batch Memory

Problem: Context compression and parallel agents make chat memory unreliable.
Solution: Created Status_UI_LOCALIZATION_BABEL.md and Rationale_UI_LOCALIZATION_BABEL.md as disk-backed state before code edits.
Rejected Alternatives: Chat-only checklist; rejected because batch protocol says CTO reads disk logs, not chat.
Scalability potential: Persistent state supports at least five iterative loops without architectural drift.
Hardware Impact: No runtime impact; process safety only.

## Decision 1 - Singleton Purge and Contract Exposure

Problem: `LocalizationManager.Instance` was a singleton facade over `GlobalRegistry.Localization`, leaving UI code free to bind to a concrete owner.
Solution: Removed the `Instance` property, changed the two internal token evaluators to use `GlobalRegistry.Localization`, and exposed `IBabelLocalization` through `GlobalRegistry.BabelLocalization` / `GlobalRegistry.Get<IBabelLocalization>()`.
Rejected Alternatives: Mass-changing all concrete `GlobalRegistry.Localization` call sites was rejected because 20+ agents are editing adjacent gameplay/UI code and concrete compatibility prevents cross-domain breakage.
Scalability potential: Low keeps legacy call sites stable; Middle migrates UI consumers to `IBabelLocalization`; High/Ultra can swap the implementation behind the contract without changing text nodes.
Hardware Impact: Removes singleton branch indirection and concrete polling pressure. Estimated gain on i3/MX350: 2-4 us on language-event fanout; main value is architectural isolation.

## Decision 2 - Native Babel Lookup

Problem: `LocRegistry` only held managed char arrays, so language reload still depended on string table residency and could not return UTF-8 spans.
Solution: Added a `NativeParallelHashMap<uint,int2>` plus contiguous `NativeArray<byte>` UTF-8 blob. Active language loads first, English loads second as fallback, missing hashes return `[MISSING_HASH]`.
Rejected Alternatives: `Encoding.UTF8.GetBytes(string)` per entry was rejected because it allocates byte arrays. The implementation writes directly into native memory with unsafe `Encoding.UTF8.GetBytes(char*,...)`.
Scalability potential: Low stores compact UTF-8 once; Middle prefetches visible hash offsets; High/Ultra can use the same offset map for richer glyph effects without string churn.
Hardware Impact: Expected low-end gain: avoids per-label managed string allocation during staged UI refresh; estimated 40-120 us saved on 100 visible localized labels, plus avoided GC spikes.

## Decision 3 - StaticDataArena LocData Bridge

Problem: `H8StaticDataArena` exposed only decoded char spans, not raw LocData bytes.
Solution: Added raw `TryGetLocalizedUtf8Block` and slice APIs so Babel can read contiguous monolith UTF-8 without converting to C# strings.
Rejected Alternatives: Decoding the whole LocData block to chars was rejected; it defeats the byte-span requirement and doubles memory traffic.
Scalability potential: Low uses baked byte spans; Middle maps static-data values by FNV; High/Ultra can add an authored key-to-offset table without changing the public span API.
Hardware Impact: Estimated gain on low-end silicon: 15-35 us per static-data lookup batch by skipping decode until TMP handoff.

## Decision 4 - TMP, RTL, and Formatting Path

Problem: TMP refresh used `LocRegistry.TryGetRawBuffer`; variable injection only accepted `{N0}` tokens.
Solution: Routed staged font-refresh labels through `TryGetVisualBufferFromUtf8`, added `{0}` token parsing, and changed integer injection to `ZeroGCFormatter.FastIntToChars`.
Rejected Alternatives: `string.Split`, `new string`, and `string.Format` were rejected. Full Unicode bidi shaping was also rejected for this pass; the prompt demanded a cheap RTL fake.
Scalability potential: Low reverses a caller-owned char buffer; Middle uses prefetch slices; High/Ultra can replace the fake with a shaping pass behind the same TMP handoff.
Hardware Impact: Estimated low-end gain: 10-30 us per formatted HUD update versus managed formatting, zero hot-path allocations.

## Decision 5 - UI Rescale, AUP, and Black Box

Problem: Font swaps did not notify manual diegetic HUD layouts, and world-space localized signs did not explicitly rebase their text bounds after floating-origin shifts.
Solution: Added `UIRescaleRequestSignal`, registered `DiegeticHudManualLayout` instances for signal-driven rebuild, and made `LocalizedWorldSign` an `IOriginShiftListener`. Added a 300-entry native Babel telemetry ring and corruption dump to `Docs/AgentLogs/Dump_UI_LOCALIZATION_BABEL.bin`.
Rejected Alternatives: Rebuilding every layout every frame was rejected as UI bloat. Physics bounds simulation for text was rejected; preserving AUP position and forcing TMP mesh/layout refresh is cheaper and deterministic.
Scalability potential: Low drains one native signal and rebuilds registered layouts; Middle batches rescale requests; High/Ultra can add font-specific overkill metrics while keeping the signal lane.
Hardware Impact: Estimated low-end gain versus polling layout bounds: 20-60 us per language switch, zero recurring frame cost.

## Decision 6 - Compile Wall

Problem: `dotnet build Hecton8.Core.csproj --no-restore` cannot complete because other batch domains currently leave missing symbols in BootstrapContracts, Cartography, GlobalSignals, Biolum, Flora, and physics listener contracts.
Solution: Ran three compile attempts and isolated reported errors. No Babel-edited file appeared in the compiler error list after the last Babel patch; the third attempt stops earlier in `Hecton8.Bootstrap.Contracts`.
Rejected Alternatives: Editing Cartography/physics/world systems was rejected as outside the UX Babel domain and would violate the domain boundary.
Scalability potential: Dependency-blocked compile evidence lets the integrator repair upstream contracts without reversing the Babel work.
Hardware Impact: No runtime impact; integration safety only.

## OMEGA POLISH CHANGES

Problem: Polish audit required anti-bloat proof after core task closure.
Solution: Ran targeted scans on Babel-touched code for managed `foreach`, `string.Format`, `.ToString()`, `string.Split`, `new string`, `Encoding.UTF8.GetString`, `math.sqrt`, and `math.normalize`. No hits were found in the new Babel hot paths. Added visible-hash Burst prefetch dispatch to `FontStreamingManager` so Task 9 is not just an unused API.
Rejected Alternatives: Running broad repo cleanup was rejected because the tree has parallel agent edits and third-party/vendor offenders outside UX Babel.
Scalability potential: Low prefetches offsets for visible labels during staged font swap; Middle amortizes larger screens through persistent native arrays; High/Ultra can reuse prefetch slices for richer glyph effects.
Hardware Impact: Estimated i3/MX350 gain from the polish pass: 8-20 us per 100 visible hashes by batching offset lookup, plus no added steady-frame cost.

Polish correction: Missing-hash fallback bytes are no longer fed through RTL reversal; `[MISSING_HASH]` remains readable in Arabic/RTL mode.

Cinematic Cheats Used:
- UTF-8 byte-span fake instead of managed localized strings.
- RTL reverse-buffer fake instead of full bidi shaping.
- Signal-triggered layout rebuild instead of per-frame measurement.
- English fallback hash chain instead of exception/missing-key string expansion.

Final Git Diff Evidence:
- Modified Babel/domain files: `LocalizationManager.cs`, `LocRegistry.cs`, `LocNumericBuffer.cs`, `RTLProcessor.cs`, `LabelSwapScheduler.cs`, `FontStreamingManager.cs`, `DiegeticHudManualLayout.cs`, `LocalizedWorldSign.cs`, `H8StaticDataArena.cs`.
- Registry/signal contract files touched for decoupling: `GlobalRegistry.cs`, `GlobalRegistryContracts.cs`, `GlobalSignals.cs`.
- New isolation markers: `Assets/_Project/Scripts/Core/Contracts/`, `Assets/_Project/Scripts/UI/Localization/`.
- Logs/status updated: `Docs/Tasks/Status_UI_LOCALIZATION_BABEL.md`, `Docs/AgentLogs/Rationale_UI_LOCALIZATION_BABEL.md`.
- Compile result remains PENDING due external missing symbols: `ITickDispatcher`/`GlobalRegistry` inside `Hecton8.Bootstrap.Contracts`, plus prior `Hecton8.Cartography`, `SeismicSignal`, `VitalWarningSignal`, `CrushWarningSignal`, `SubtitleSignal`, and world/physics interface implementation holes.

## Decision 7 - Patient Re-Audit Hardening

Problem: Static review found three Babel defects after the first pass: monolith `LocData` could be skipped when managed tables were empty, large UTF-8 labels were decoded into an oversized thread-local char buffer before truncation, and the visible-hash prefetch job completed synchronously without feeding the staged swap queue. `LocalizedWorldSign` also still assigned `TMP_Text.text`.
Solution: `LocRegistry` now estimates static `LocData` entries, loads static bytes without requiring managed tables, scans UTF-8 boundaries before decoding to the 1024-glyph capped buffer, and tracks a read fence so `_utf8Offsets`/`_utf8Bytes` are not disposed while a prefetch job can still read them. `FontStreamingManager` now stores a tracked prefetch handle and applies completed `int2` slices into `LabelSwapScheduler`, which uses `TryGetVisualBufferFromUtf8Slice` during the 18-label drain. `LocalizedWorldSign` now hashes its authored key, uses Babel buffers plus `SetCharArray`, owns cold fallback/display buffers, and preserves the AUP rebase refresh.
Rejected Alternatives: Same-tick `JobHandle.Complete()` during collection was rejected because it serializes the worker job path. Keeping `targetText.text` for "rare" world signs was rejected because the prompt is Babel/zero-GC and signage still participates in language refresh. Decoding the full UTF-8 span and then truncating was rejected because malicious or bad data could permanently grow the thread-local buffer.
Scalability potential: Low/MX350 gets bounded decode memory, no language-swap job stall, native read-fence safety on rapid language swaps, and no string assignment for signs. Middle uses precomputed slices to amortize label updates across denser HUDs. High/Ultra can spend the saved CPU on richer glyph decay, CJK/RTL visual passes, or per-sign material effects without changing the zero-GC text handoff.
Hardware Impact: Estimated i3/MX350 gain is prevention of worst-case thread-local char buffer bloat, removal of one synchronous prefetch completion from language-swap collection, and 8-20 us saved per 100 queued localized labels by consuming prefetched slices. Measured proof remains absent because the user explicitly forbade `dotnet build`; status remains PENDING VERIFICATION.

REGRESSION MODEL:
- CPU: lower collection spike; possible one-frame delay before first swap batch while the prefetch job finishes.
- GC: no new hot-path managed strings; new buffers are cold per sign/thread only.
- Memory: static LocData capacity now includes native hash-map entries; bounded decode prevents pathological char-buffer growth.
- Cadence: font swap still drains max 18 labels/tick; prefetch result is applied before draining.
- Correctness: static monolith-only localization no longer returns universal misses; fallback signs no longer depend on `LocalizationManager.GetExpandedOrFallback`.

HOT PATH IMPACT:
- Label swap: one map lookup can be skipped when prefetch slice is valid.
- World signs: language refresh writes char buffers through TMP, not managed text strings.
- Runtime frames outside language swap: no new recurring work.

FAILURE MODES:
- If a prefetch job is still running during a reset, the apply flag is cleared so stale slices are not written into a new queue.
- If localization reload mutates the UTF-8 map while a prefetch reader exists, `LocRegistry` completes the registered read fence before disposal.
- If a prefetched slice is invalid, the scheduler falls back to the hash lookup path and telemetry handles corruption.
- If static LocData has no NUL-delimited entries, the native map is not created from empty data.

WHY KEPT:
- The changes stay inside UX/Babel files and preserve existing public call sites.
- `dotnet build` was not run due the user override; static scans and diff checks only.

## Decision 8 - Native Sentinel and Stale Prefetch Cleanup

Problem: Second audit found two integration risks. `_utf8Offsets` was a persistent `NativeParallelHashMap<uint,int2>` without `NativeMemorySentinel` registration, so leak/fragmentation telemetry could under-report Babel native memory. A rapid language change could also clear the active swap queue while an older visible-hash prefetch job was still in flight, allowing stale slices to be applied to a new queue if completion happened later.
Solution: `LocRegistry` now registers `_utf8Offsets` with `NativeMemorySentinel.RegisterNativeParallelHashMap` immediately after allocation and unregisters it before disposal. `FontStreamingManager` now explicitly abandons visible-hash prefetch results on language/queue reset while preserving the handle until completion, and only blocks `ProcessSwapBatch` when the in-flight slices still belong to the active queue.
Rejected Alternatives: Tracking only `_utf8Bytes` was rejected because hash-map capacity is native memory too. Completing every abandoned prefetch immediately was rejected because a language-change hot path should not stall on a tiny worker job when the result is no longer needed. Reusing stale slices across queues was rejected because pending entry order is the contract.
Scalability potential: Low/MX350 avoids false memory-clean reports and prevents a one-frame incorrect-language text flash under rapid toggles. Middle keeps queue draining even when an abandoned job is still finishing. High/Ultra can run larger visible-label prefetch batches without changing the handoff rules because ownership is now explicit.
Hardware Impact: Estimated i3/MX350 runtime gain is avoiding a possible one-frame label-drain stall after rapid language changes; exact frame-time proof is still blocked by the no-build/no-profiler constraint. Memory safety gain is deterministic NativeMemorySentinel visibility for the UTF-8 offset map.

REGRESSION MODEL:
- CPU: no new steady-frame work; abandoned jobs are not force-completed unless teardown or native-map mutation requires it.
- GC: no new managed allocations in the label-drain path.
- Memory: `_utf8Offsets` is now visible to the native allocation registry; no capacity change.
- Correctness: prefetched slices are applied only while the queue that requested them is still current.
- Integration: legacy string-returning localization APIs still contain dev-only `new string`/`string.Format` paths, but Babel/TMP hot paths remain scan-clean.

WHY KEPT:
- It fixes native-memory accounting and a concrete stale-job ordering bug without crossing the UX/Babel boundary.
- Verification remains static only because the current user instruction explicitly forbids `dotnet build`.

## Decision 9 - Unicode Hash Parity and RTL Visual Consistency

Problem: The static `LocData` bridge hashed raw UTF-8 bytes with `ComputeAscii(ReadOnlySpan<byte>)`, but the project key/value hash contract hashes UTF-16 char units through `LocHash.Compute(ReadOnlySpan<char>)`. That made static non-ASCII strings vulnerable to hash misses. The legacy char-table RTL visual path also returned logical order while the new Babel UTF-8 path reversed in place, creating inconsistent Arabic behavior between old and new UI callers. `LocalizedWorldSign` trusted upstream text length without clamping against the backing char buffer.
Solution: Added `LocHash.ComputeUtf8AsUtf16`, a zero-allocation UTF-8 scalar reader that hashes the same UTF-16 code units as `LocHash.Compute`. Static arena slice registration now uses this parity hash. `RTLProcessor.ToVisualOrder` and `TryGetVisualBuffer` now copy into thread-local storage and reverse in place. `LocalizedWorldSign.PrepareDisplayBuffer` clamps display length to `sourceBuffer.Length` before `TMP_Text.SetCharArray`.
Rejected Alternatives: Keeping byte-wise hash was rejected because it only works for ASCII and breaks authored CJK/Arabic/Cyrillic static data. Using `Encoding.UTF8.GetString` to compute parity was rejected because it allocates. Deferring legacy RTL callers to TMP bidi was rejected because the prompt requires a visual fake and the Babel path already owns the reversal.
Scalability potential: Low/MX350 gets deterministic static-data hits without extra managed memory. Middle keeps legacy and UTF-8 label paths visually aligned during phased migration. High/Ultra can swap in a real bidi/shaping pass later behind the same visual-buffer contract.
Hardware Impact: UTF-8 hash parity is cold during reload/static-map build only and adds no steady-frame cost. RTL copy+reverse remains O(n) over thread-local buffers and avoids managed strings; expected cost is 5-15 us for typical short Arabic labels on low-end silicon.

REGRESSION MODEL:
- CPU: small cold cost for UTF-8 scalar hash while loading static LocData; no added per-frame polling.
- GC: no new managed strings or temporary arrays in the Babel/TMP hot path.
- Memory: reuses existing thread-local RTL buffer and native UTF-8 blob.
- Correctness: non-ASCII static strings now hash to the same value as their decoded string; legacy char and UTF-8 visual paths now agree.
- Safety: world signs clamp display length before TMP handoff under corrupt or stale buffer metadata.

WHY KEPT:
- These are localized correctness fixes inside the Babel/domain files.
- Static verification passed; `dotnet build` remains intentionally unrun by current instruction.

## Decision 10 - Static LocData Authored Hash Aliases

Problem: The static `LocData` bridge copied the monolith byte block and registered value-content hashes, but Data Monolith records store authored numeric IDs separately from `NameUtf8Offset`, `DisplayNameUtf8Offset`, and `MessageUtf8Offset`. A caller using an item/species/biome/module/error hash could still miss even though the target UTF-8 text was resident.
Solution: Added `H8StaticLocalizationReference` and caller-owned `H8StaticLocalizationCursor` so `H8StaticDataArena` can expose primary authored hash -> UTF-8 slice aliases without managed strings. `LocRegistry` now walks that cursor once during reload and inserts aliases into the native `NativeParallelHashMap<uint,int2>` after active language and English fallback entries, preserving authored table priority.
Rejected Alternatives: Rebuilding string keys from monolith IDs was impossible because only numeric hashes are stored. Passing `NativeParallelHashMap` into the data arena was rejected because it would couple the data domain to Babel internals. Indexed alias lookup was rejected for the reload path because it rescans prior records and becomes O(n^2). Inventing description-key hashes was rejected because `H8ItemRecord` stores a description offset but no stable description hash contract.
Scalability potential: Low/MX350 gets monolith-backed display names without managed lookups or miss cascades. Middle handles larger static tables with O(n) cursor enumeration. High/Ultra can add richer authored localization fields later by extending the cursor contract, not the UI handoff.
Hardware Impact: No steady-frame cost. Cold reload adds one O(n) scan over static item/creature/biome/ghost/SOP records and prevents 15-45 us per 100 monolith-backed label resolutions from falling through value-hash misses and fallback work on low-end silicon.

REGRESSION MODEL:
- CPU: one cold cursor pass during Babel reload; no per-frame polling.
- GC: no managed strings, arrays, dictionaries, or LINQ in the alias path.
- Memory: native hash-map capacity now includes static aliases; byte blob is reused.
- Correctness: primary static record hashes now resolve to their display/message text; item descriptions remain unaliased until the monolith owns a distinct description key hash.
- Integration: API lives in `H8StaticDataArena` as a data-domain enumeration contract; `LocRegistry` consumes only references and does not walk monolith sections directly.

WHY KEPT:
- It closes a concrete binary dictionary gap while staying inside the Data Monolith/Babel interface.
- Static verification passed; `dotnet build` remains intentionally unrun by current instruction.
