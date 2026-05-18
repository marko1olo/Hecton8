# Rationale_SHINOBU_39

Agent: SHINOBU_39
Domain: Presentation & UX / Zero-GC Localization and Subtitles
Status: LOOP 6 COMPLETE / STATIC AUDIT COMPLETE / FULL BUILD BLOCKED BY PRE-EXISTING COMPILE WALL

## Mandate Selection

Problem: The Babel system sits on the UI hot path where TMP string assignment and runtime key strings create GC spikes.
Solution: Use the localization, UI data streaming, zero-GC, native memory, signal-lane, and telemetry mandates as the active rule set before code changes.
Rejected Alternatives: Treating TextMeshPro as a managed string endpoint, or adding a broad UI framework beyond byte-to-char localization scope.
Scalability potential: Low uses fixed char slots and strips rich text; Middle uses full static atlas text; High keeps formatting telemetry; Ultra can spend saved CPU on richer SDF styling outside the hot conversion path.
Hardware Impact: Expected benefit on i3/MX350 is removing per-subtitle managed string churn; exact microseconds remain PENDING PROFILER PROOF.

## Decision 01 - Prompt Parsing

Problem: The exact `<AGENT_PROMPT id="SHINOBU_39">` regex failed because the tag carries extra attributes.
Solution: Re-read CURRENT_BATCH.md with an attribute-safe CLI regex and extracted only the SHINOBU_39 block.
Rejected Alternatives: Relying on chat text or reading neighboring prompts.
Scalability potential: Disk-backed task memory survives context compression and keeps the implementation scoped to Babel only.
Hardware Impact: Runtime impact 0 us; prevents architectural drift.

## Decision 02 - Babel Binary Authority

Problem: The prompt named legacy loc_strings_*.h8bin, but the live repository exposes Babel_Dictionary.h8bin and a zero-copy BabelDictionaryStore.
Solution: Treat Babel_Dictionary.h8bin as the active binary authority and parse its 32-byte header plus 16-byte hash/offset/length index.
Rejected Alternatives: Creating a second loc_strings loader or guessing an undocumented layout.
Scalability potential: Low/Mx350 reads only fixed slices; Mid/High/Ultra can prefetch visible hash offsets and spend saved CPU on richer TMP material styling.
Hardware Impact: Runtime path remains 0 file parsing during subtitle display; editor parsing is out of frame.

## Decision 03 - Hash Subtitle String Removal

Problem: SubtitleManager.DisplaySubtitle(int, fallback, duration) used char-backed legacy entries and other paths still supported string queues, leaving a route back to managed text churn.
Solution: Add SubtitleCommandDTO ring routing uint hashes through LocRegistry.TryWriteVisualSpanFromUtf8 into CharBufferPool leases, then copy once into the manager-owned render buffer and call TMP SetCharArray.
Rejected Alternatives: TMP_Text.text, SetText(string), string fallback formatting, and adding direct quest/localization dependencies.
Scalability potential: Low strips TMP rich tags during decode; Middle keeps full plain text; High and Ultra can keep tags and apply more SDF/material polish because the string allocation is gone.
Hardware Impact: Expected i3/MX350 win is removing one managed string allocation and UTF16 heap copy per hash subtitle, estimated 35-120 us depending subtitle length and GC pressure.

## Decision 04 - DTO Alignment

Problem: Cross-agent DTOs with auto-properties or byte packing cause CS1612 copy traps and unaligned ARM64 loads.
Solution: Add field-only sequential DTOs for LocalizationEntryDTO, SubtitleCommandDTO, SubtitleStateDTO, and mock signals; sizes for localization and subtitle command are exactly 16 bytes.
Rejected Alternatives: Pack=1 runtime structs, managed properties, and class wrappers.
Scalability potential: Same DTOs can be consumed by Burst queues or managed rings without per-tier type forks.
Hardware Impact: 0 us direct, but avoids alignment penalties and hidden defensive copies on low-end ARM/handheld devices.

## Decision 05 - Span UTF-8 Decode

Problem: The previous UTF-8 visual decode path used Encoding.UTF8.GetCharCount/GetChars into a thread-static char[] that could grow on demand.
Solution: Add a manual scalar decoder that writes to caller-owned Span<char>, handles malformed bytes with U+FFFD, surrogate pairs, ^0..^3 integer injection, optional rich-tag strip, and ellipsis truncation.
Rejected Alternatives: Encoding.UTF8.GetChars, new string, StringBuilder, and managed substring token replacement.
Scalability potential: Low/Mx350 strips rich tags and caps at pool capacity; Mid keeps plain full text; High/Ultra can preserve rich tags and spend the budget on TMP SDF polish.
Hardware Impact: Expected i3/MX350 gain is 10-80 us per subtitle depending text length and zero managed bytes on the hash path.

## Decision 06 - CharBufferPool Scale

Problem: Sixteen fixed char slots were too small for simultaneous subtitle, HUD, and localization swap bursts.
Solution: Expand to 500 fixed slots with 256-char physical slots, 128-char Babel target, free-mask scan without unchecked tzcnt, and NativeBitArray active lease tracking.
Rejected Alternatives: Dynamic char[] allocation, List<char>, stackalloc for cross-frame buffers, and lowering VR slot size to 128 which would break existing UI assumptions.
Scalability potential: Low uses the same pool with rich-tag strip; Middle/High/Ultra keep larger visible strings without changing allocation behavior.
Hardware Impact: Expected i3/MX350 benefit is avoiding burst allocations during 18-label swap drains and subtitle queues; exact profiler number blocked by existing compile wall.

## Decision 07 - Async Locale Swap

Problem: Language switches rebuild LocRegistry immediately, which can land in the same frame as visible UI event processing.
Solution: Add LocalizationManager.SetLanguageAsync that yields one frame through AwaitableDebtMonitor before calling the existing SetLanguage path.
Rejected Alternatives: New service locator dependency, coroutine string route, or background-thread mutation of Unity dictionaries.
Scalability potential: Low devices can amortize swap over a frame boundary; High/Ultra keep same API and can layer prefetch on top.
Hardware Impact: Direct hot-path impact 0 us; reduces same-frame spike risk during locale changes.

## Decision 08 - Dynamic Variable Injection

Problem: Subtitle variables like counts or depths normally push UI code toward string.Format or interpolation.
Solution: Decode ^0..^3 placeholders directly into the destination Span<char> using ZeroGCFormatter.FastIntToChars and BabelFormatArgs field DTO.
Rejected Alternatives: string.Format, interpolation, Regex, StringBuilder, and per-token managed dictionary replacement.
Scalability potential: Low uses integer-only substitution; Middle/High/Ultra can add richer non-hot authoring without changing the runtime primitive.
Hardware Impact: Expected i3/MX350 win is removing one formatted string allocation and 20-150 us on dynamic subtitle lines.

## Decision 09 - Missing Hash Contract

Problem: A missing Babel hash previously surfaced as `[MISSING_HASH]`; prompt requires deterministic `[ERR_LOC_HASH]` without exception or null.
Solution: Replace missing-hash UTF-8 fallback bytes and route misses through the same span decoder.
Rejected Alternatives: Throwing, returning null/empty text, or constructing a managed fallback string.
Scalability potential: Same error glyph path works on every tier and preserves telemetry for postmortem.
Hardware Impact: 0 managed bytes on miss; low-end cost is a 14-char copy.

## Decision 10 - Compile Wall

Problem: Hecton8.Core.csproj does not compile before SHINOBU_39 validation because cross-domain dispatcher, input, and world streaming DTOs are missing.
Solution: After three build attempts, record compile wall as dependency-blocked and continue SHINOBU_39 scope without editing foreign domains.
Rejected Alternatives: Creating fake dispatcher/input/world DTOs outside the localization domain or reverting SHINOBU_39 code that is not implicated by the errors.
Scalability potential: No runtime change; avoids architectural sabotage in another agent's ownership area.
Hardware Impact: 0 us; verification remains blocked until Integrator restores those DTO contracts.

## Decision 11 - Babel Blackbox Telemetry

Problem: A UTF-8 decoder fault or slow span conversion would otherwise vanish into the UI frame with no postmortem evidence.
Solution: Expand Babel telemetry to a 300-frame NativeArray ring containing frame, hash, slice, per-frame localization event count, active CharBufferPool leases, conversion time, language, and flags; dump both Dump_SHINOBU_39.bin and Dump_BABEL_SYSTEM.bin on corrupt slice or >0.5 ms conversion.
Rejected Alternatives: Debug.Log-only traces, managed List history, and exception-driven diagnostics.
Scalability potential: Low/Middle keep the recorder cheap and fixed-size; High/Ultra can add offline tooling around the binary dump without changing the hot path.
Hardware Impact: Steady-state write is estimated sub-2 us per localization event on i3/MX350; fault dump is outside the normal frame contract.

## Decision 12 - Editor Facade and CSV Override Scope

Problem: Designers need readable localization inspection and override save without recompiling C#, but runtime CSV polling would punish MicroSD and mobile storage.
Solution: Add Babel Localization Manager as an editor-only facade that parses .h8bin, polls loc_overrides.csv by write time, applies hash/value rows, saves aligned .h8bin output, and previews selected text with GUI.Label.
Rejected Alternatives: Runtime CSV parsing, reflection-heavy inspectors, and play-mode TMP preview objects.
Scalability potential: Low devices ship binary dictionaries only; Middle/High/Ultra gain authoring iteration in editor without runtime file I/O debt.
Hardware Impact: Runtime 0 us. Editor-only file parsing is intentionally allowed to allocate and does not touch subtitle Tick.

## Decision 13 - TMP Char Array Constraint

Problem: The prompt asks for a NativeArray<char> pool, but TextMeshPro's allocation-free SetCharArray endpoint accepts char[] rather than NativeArray<char>.
Solution: Own a literal NativeArray<char>[64000] Babel arena for UTF-8 decode slots, then copy at most 128 chars into a preallocated char[] TMP bridge because TMP_Text.SetCharArray only accepts char[].
Rejected Alternatives: Allocating strings, unsafe private TMP internals, or pretending TMP can consume NativeArray<char> directly.
Scalability potential: Low/Middle/High/Ultra all use the same fixed pool; Ultra can add richer TMP materials without changing text memory ownership.
Hardware Impact: Removes managed string allocation on the Babel path; the extra native-to-TMP bridge copy is bounded to 128 chars and estimated <3 us per subtitle on i3/MX350.

## Decision 14 - Dear Lie Text Presentation

Problem: A typewriter subtitle effect often rebuilds visible text every glyph, which is fake work with real GC pressure.
Solution: Upload the full char buffer once and advance TMP_Text.maxVisibleCharacters; Low/Mx350 additionally strips rich tags during decode instead of asking TMP to parse them.
Rejected Alternatives: Substring reveals, per-character SetCharArray, coroutine string concatenation, and runtime material keyword churn that can break SRP batching.
Scalability potential: Low strips effects; Middle preserves plain authored text; High/Ultra can preserve rich tags and spend saved CPU on visual-only TMP polish.
Hardware Impact: Estimated 15-60 us saved per reveal frame on low-end CPU by avoiding repeated text buffer rebuilds.

## Decision 15 - Dependency Guard

Problem: SHINOBU sits beside many active agents; direct references to quest/UI/world systems would amplify compile walls.
Solution: Use field DTOs, mock signals, GlobalRegistry tier/localization access, and existing TMP interfaces; no new sibling runtime asmdef dependency was added.
Rejected Alternatives: Referencing quest dialogue controllers, inventing local signal duplicates with behavioral ownership, or editing dispatcher/world contracts to force a build.
Scalability potential: Runtime behavior remains isolated and can be consumed by future SignalBus lanes without changing the Babel decode primitive.
Hardware Impact: 0 us direct; preserves iteration time and prevents unrelated recompilation cascades.

## Decision 16 - Native Babel Arena Correction

Problem: The first CharBufferPool pass removed GC but did not literally satisfy the 64,000 char NativeArray arena requirement.
Solution: Add a NativeArray<char>[64000] Babel arena split into 500 x 128 slots, expose slot-backed Span<char> for decoder writes, track lease occupancy with NativeBitArray, and retain char[500][128] only as the TMP API bridge.
Rejected Alternatives: Leaving managed-only char[][], copying through strings, or replacing broad VR HUD users with a risky pool contract change.
Scalability potential: Low tier caps Babel text at 128 chars and strips rich tags; Middle/High/Ultra keep the same arena and may route richer visual styling after SetCharArray without changing memory ownership.
Hardware Impact: Native arena is 128 KB session memory; bridge arrays are cold managed storage. Runtime GC remains 0 B; bridge copy is fixed and tiny.

## Decision 17 - Signal Corridor Integration

Problem: SubtitleCommandDTO local ring was zero-GC but did not consume the existing GlobalSignals subtitle lane, leaving cross-domain producers without the mandated NativeQueue route.
Solution: Drain SignalBus<SubtitleSignal>.GetFrameSnapshot() once per frame into SubtitleCommandDTO, cap reads at eight signals, and use priority/flag bits to trigger interrupt subtitles.
Rejected Alternatives: Creating a duplicate SHINOBU-only NativeQueue, using string UnityEvents, or referencing VocalWarningSystem/Quest classes directly.
Scalability potential: Low tier load-sheds through SignalBus policies; High/Ultra can raise lane limits without changing SubtitleManager.
Hardware Impact: Snapshot scan is at most eight structs per frame in the UI pump; expected cost is sub-1 us and 0 B GC.
