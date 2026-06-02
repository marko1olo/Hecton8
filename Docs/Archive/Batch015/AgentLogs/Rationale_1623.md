# Rationale 1623 - ZERO_GC_SUBTITLES_AND_BABEL_LOCALIZATION_COMPILER

Status: STATIC_COMPLETE_BUILD_NOT_RUN

## Decision 00 - Missing XML Prompt

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="1623">`; the file lists 1623 only in coordinator prose and contains XML blocks for other agents.
Solution: Treat the user's direct 1623 assignment as bounded scope, but record XML extraction as blocked and avoid reading or applying neighboring prompts.
Rejected Alternatives: Using 1621, 1622, 1624, or 1625 prompt material would contaminate architecture decisions. Guessing a 20-task XML count would be false reporting.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process scope hygiene.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Decision 01 - Mandates Selected

Problem: Babel/subtitle work touches hot UI rendering, localization lookup, buffer layout, and signal discipline.
Solution: Use UI zero-GC, localization Babel, zero-GC, ARM64 struct layout, execution phase, registry, signal lane, performance budget, and evidence mandates before code.
Rejected Alternatives: Reading only AGENTS.md leaves TMP and localization-specific constraints ambiguous.
Scalability potential: Low uses cheap cadence and fixed 2K atlas; middle keeps full static labels; high/ultra spend saved time on VISUAL_SYNC glitch/presentation only.
Hardware Impact: Prevents per-frame GC spikes; estimated avoided hitch cost is unmeasured and remains STATIC_SOURCE only.

## Decision 02 - Hash Fallback Must Stay Span-Based

Problem: `DisplaySubtitle(int keyHash, ReadOnlySpan<char> fallback, float duration)` discarded the fallback whenever `keyHash` was nonzero, so a missing Babel token produced no subtitle.
Solution: Add `uint textHash + ReadOnlySpan<char>` overloads and route misses through `BabelLease.Span` plus `CopyToTmpBuffer`, then the existing `SetCharArray` swap path.
Rejected Alternatives: `new string(fallback)`, `TMP_Text.text`, and `SetText` would allocate or invoke managed formatting. Broad localization-manager rewrite was outside 1623 scope.
Scalability potential: Low uses the same 512-char lease with truncation telemetry; middle/high/ultra keep the same truth route and can show richer glyph content through existing rich-text LOD.
Hardware Impact: Removes a silent miss without heap churn. Missing-hash fallback copy is bounded by 512 chars; estimated low-end cost 6-22 us on i3/MX350 when a fallback is displayed.

## Decision 03 - Battery Glitch Uses SignalBus, Not PowerGrid Polling

Problem: User required subtitle glitches under battery/energy failure, but direct polling of `PowerGrid` or scene power components would violate domain boundaries and hot-path registry rules.
Solution: Read `SignalBus<BatteryLevelSignal>` and `SignalBus<SurvivalVitalsChangedSignal>` frame snapshots, smooth a continuous intensity, quantize refresh cadence, and mutate the existing subtitle render char array before TMP `SetCharArray`.
Rejected Alternatives: Direct power-system references, `FindObjectOfType`, per-character TMP tags, string rebuilding, or per-frame unconditional reflush. Those add dependencies, allocations, or uncontrolled frame cost.
Scalability potential: Low: slow cadence and ~1.8% mutation rate. Middle: moderate cadence through quality weight. High: faster refresh. Ultra: up to 11% deterministic glyph mutation for visual overkill, still visual-only.
Hardware Impact: Snapshot scan is bounded by existing signal capacities; render mutation is capped by subtitle length. Estimated low-end cost 2-5 us on refresh frames, high/ultra 5-35 us depending on line length and quality.

## Decision 04 - APEX Proof Belongs In C# Static Assertions

Problem: Chat claims do not protect the codebase from future drift, and runtime dumps were explicitly rejected.
Solution: Add editor static assertions in `ZeroGCSubtitleFormatter1423EditTests.cs` for the 1623 route: fallback spans, LateFrameTick phase, SignalBus battery/energy snapshots, no hot cold lookups, no managed text bridge tokens, and single-lock try/finally routes.
Rejected Alternatives: Markdown-only proof, JSON report, binary dump, or `dotnet build` while Unity csc processes are active.
Scalability potential: Low/middle/high/ultra unchanged at runtime; the test pins the continuous quality path and prevents future binary quality switches.
Hardware Impact: 0 us runtime; editor-only static test cost only when tests are run.

## Decision 05 - TMP Registry Must Fail Closed

Problem: `TMP_TextRegistry` still had dynamic managed array growth through `EnsureCapacity`, so an unexpected burst of TMP nodes could allocate and copy arrays after boot.
Solution: Replace grow-on-demand arrays with fixed 2048-entry readonly backing stores, expose `Capacity` and `OverflowCount`, and reject excess nodes by leaving `RegistryIndex = -1`.
Rejected Alternatives: Keeping cold resize with a comment, using `List<T>`, or scene rescans on overflow. All hide allocation risk and break deterministic text ownership.
Scalability potential: Low keeps a bounded registry and avoids a hitch; middle/high/ultra keep the same registry truth and can spend visuals elsewhere without changing text ownership.
Hardware Impact: Removes an unbounded O(N) copy/allocation spike. Estimated avoided low-end hitch is 0.2-2.0 ms during pathological UI registration bursts; steady-state cost remains O(1).

## Decision 06 - Font Swap Queue Must Match TMP Registry Capacity

Problem: `TMP_TextRegistry` could now stage 2048 text nodes, but `LabelSwapScheduler` still had a 512-entry pending ring; language swap could silently stop queuing after 512 candidates.
Solution: Raise `LabelSwapScheduler.MaxQueueCapacity` to 2048, allocate the fixed pending ring from that const, and expose an `OverflowCount` fail-closed diagnostic.
Rejected Alternatives: Letting `CollectSwapQueue` break silently, switching to `Queue<T>`, or growing the ring on demand. Those either leave stale fonts or reintroduce managed growth.
Scalability potential: Low drains the same 2-18 labels per frame through `GlobalQualityWeight`; middle/high/ultra gain full UI coverage without raising per-frame drain above the 18-label mandate.
Hardware Impact: Steady-state remains bounded by 18 labels/tick. Avoids stale-language presentation defects on large HUD/PDA scenes without adding hot-path allocation.

## Decision 07 - Madness Text Padding Is Primed, Not Rebuilt Per Frame

Problem: `LocalizedTextMadnessFx.ApplyActiveState` called `TMP_Text.UpdateMeshPadding()` every active `LateFrameTick`, converting a bounded visual glitch into repeated TMP geometry padding work.
Solution: Prime active mesh padding once with the maximum bounded underlay/glow values, then animate only material floats during the active frame loop.
Rejected Alternatives: Removing the effect, updating padding every frame, or replacing it with per-character text mutation. Those either reduce instrument stress feedback or spend CPU in the wrong place.
Scalability potential: Low keeps the same glitch belief with less CPU; middle/high/ultra keep the same material-driven visual overkill without changing text content or layout truth.
Hardware Impact: Removes repeated TMP padding recalculation from active effect frames. Estimated low-end saving is 3-20 us per affected label per frame, static estimate only.

## Decision 08 - Power Glitch Must Not Corrupt TMP Markup

Problem: Random subtitle glitch mutation could replace characters inside TMP rich-text tags, corrupting `<sprite>`, `<color>`, or other markup and forcing visual parse failures.
Solution: Count mutable glyph candidates with a Span-based pass that ignores `<...>` tag ranges and whitespace, then deterministically select a bounded mutation subset from those candidates only.
Rejected Alternatives: Stripping all rich text before glitch, allocating a tag mask, or keeping random index mutation. Stripping reduces authored presentation; a mask allocates; random mutation breaks markup.
Scalability potential: Low spends one short linear pass on typical subtitle lines; middle/high/ultra keep richer tag-driven subtitles while still adding stronger visual decay through `GlobalQualityWeight`.
Hardware Impact: Typical subtitle extra scan is estimated at 0.5-3 us on i3/MX350 refresh frames. Prevents expensive TMP fallback/reparse behavior caused by corrupted tags.

## Decision 09 - Auto-Size Repair Runs Once Per Pending Apply

Problem: `LocalizedTMPAutoSizer.LateFrameTick` called `RepairCollapsedRectHierarchy()` and then `ApplyConfiguration()`, which called the same repair again.
Solution: Keep rect repair ownership inside `ApplyConfiguration` and let `LateFrameTick` only drain the pending apply flag.
Rejected Alternatives: Removing repair entirely or keeping duplicate bounded walks. Removing repair risks collapsed localized labels; duplicates waste UI phase time.
Scalability potential: Low devices avoid redundant hierarchy walks during language/font changes; higher tiers keep the same adaptive localized label behavior.
Hardware Impact: Avoids one extra bounded 4-pass/4-depth rect walk per pending label apply. Exact microseconds require Unity profiler proof.

## Decision 10 - Prefetch Budget Counts Successes, Not Attempts

Problem: `FontStreamingManager.CollectSwapQueue` incremented `prefetchedCount` even when `TryResolveVisibleTextOffsetSlice` failed, wasting the visible-slice budget on misses.
Solution: Advance `prefetchedCount` only inside the success branch and enqueue `hasPrefetchedSlice` only for valid slices.
Rejected Alternatives: Keeping attempt-based budget or increasing budget size. Attempt-based budget loses cheap path coverage; larger budget masks the accounting bug.
Scalability potential: Low devices keep more labels on pre-resolved slice paths during language reboot; higher tiers preserve the same visual behavior with lower staged resolve cost.
Hardware Impact: No steady-state cost. During font swap, saves full UTF-8 resolve work for later labels when earlier registered labels miss slice prefetch. Exact microseconds depend on loc table distribution.
