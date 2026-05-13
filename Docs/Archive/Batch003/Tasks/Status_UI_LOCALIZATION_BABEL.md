# Status_UI_LOCALIZATION_BABEL

Agent: UX_ENGINEER
Prompt ID: UI_LOCALIZATION_BABEL
Domain: ECHELON 8 PRESENTATION & UX / Zero-GC Subtitles (Babel)
Task Count: 18
Status: PENDING VERIFICATION - CORE TASKS IMPLEMENTED, COMPILE BLOCKED BY EXTERNAL DEPENDENCIES

Mandates loaded:
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt

Loop 0 - Bootstrap
- [x] Extract prompt from CURRENT_BATCH.md | DOD: CLI regex extraction from Docs/Tasks/CURRENT_BATCH.md, full tag only | Rejected: IDE tab memory / neighboring prompts | Estimate: 35 us
- [x] Read domain authority | DOD: Docs/Actual Domains of Project.txt inspected before edits | Rejected: guessing UX domain from prompt name | Estimate: 20 us
- [x] Read task mandates | DOD: 7 relevant .agents-skills files loaded | Rejected: bulk registry load noise | Estimate: 90 us

Loop 1 - Tasks 1-5
- [x] Task 1 - SINGLETON ERADICATION | DOD: `LocalizationManager.Instance` removed; internal token evaluators now use `GlobalRegistry.Localization`; `IBabelLocalization` exposed by registry | Rejected: mass rewrite of concrete non-UI callsites during parallel batch | Estimate: 4 us saved per language event
- [x] Task 2 - SIGNAL MIGRATION | DOD: existing NativeQueue-backed `LocalizationEvents.PublishLanguageChanged` retained; `FontStreamingManager` and signs consume listener callbacks, not polling | Rejected: per-frame language polling on TMP nodes | Estimate: 20 us saved per 100 labels per frame avoided
- [x] Task 3 - ASMDEF ISOLATION | DOD: created `Hecton8.Core.Contracts` and `Hecton8.UI.Localization` asmdefs; UI localization asmdef references only Core.Contracts and Unity.Collections | Rejected: moving live monolith files into new asmdef mid-batch | Estimate: 0 runtime us, compile boundary only
- [x] Task 4 - DEAD CODE HUNT | DOD: `JsonUtility.FromJson` scan found no runtime translation loader; existing hits are options/modding/editor/static-data, not UI translations | Rejected: deleting non-translation persistence/editor code | Estimate: 0 runtime us
- [x] Task 5 - BINARY LOC DICTIONARY | DOD: `H8StaticDataArena.TryGetLocalizedUtf8Block/TryGetLocalizedUtf8Span` added; LocData block exposed as raw UTF-8 bytes | Rejected: full-block managed decode | Estimate: 15-35 us saved per static-data lookup batch
- [BLOCKED BY DEPENDENCY] Loop 1 compile check | DOD: `dotnet build Hecton8.Core.csproj --no-restore` attempted | Rejected: editing Cartography/physics/world dependencies outside UX domain | Estimate: blocked by missing external symbols

Loop 2 - Tasks 6-10
- [x] Task 6 - HASH-BASED OFFSET LUT | DOD: `NativeParallelHashMap<uint,int2>` maps FNV hash to UTF-8 offset/length; active language wins, English fallback fills gaps | Rejected: managed `Dictionary<uint,string>` | Estimate: 40-120 us saved on 100 visible labels
- [x] Task 7 - ZERO-GC DECODE | DOD: `TryGetLocalizedSpan(uint,out ReadOnlySpan<byte>)` returns raw byte spans or `[MISSING_HASH]` without C# string conversion | Rejected: `Encoding.UTF8.GetString` | Estimate: avoids 100 managed string allocations per 100 labels
- [x] Task 8 - TMP INTEGRATION | DOD: `LabelSwapScheduler` now calls UTF-8 span decode into reusable char buffer then `TMP_Text.SetCharArray` | Rejected: assigning `TMP_Text.text` | Estimate: 10-30 us saved per staged label batch
- [x] Task 9 - MULTI-THREADED PRE-CACHING | DOD: Burst `BabelVisibleTextOffsetPrefetchJob` added and dispatched from `FontStreamingManager.CollectSwapQueue` for visible hashes | Rejected: resolving offsets individually during TMP write | Estimate: 8-20 us saved on 100 hashes
- [x] Task 10 - FONT SWAPPING | DOD: existing LanguageChanged listener staged font swaps across registered TMP nodes; Babel refresh happens inside the 18-label drain budget | Rejected: all-label instant swap due 0.1 ms frame-time suspicion | Estimate: spike converted to bounded 18 labels/tick

Loop 3 - Tasks 11-15
- [x] Task 11 - RTL FAKE | DOD: RTL visual buffer reversal added plus Burst `BabelRtlReverseJob` for native char buffers | Rejected: full bidi shaping pass | Estimate: 5-15 us saved per Arabic label versus shaping
- [x] Task 12 - VARIABLE INJECTION | DOD: `{0}` token parser added; integer path uses `ZeroGCFormatter.FastIntToChars`; no `string.Split` | Rejected: `string.Format` and `Split` | Estimate: 10-30 us per formatted HUD write
- [x] Task 13 - PLURALIZATION MATH | DOD: `IBabelLocalization.ResolvePluralHash` uses `value == 1 ? singular : plural` | Rejected: rule table allocation | Estimate: sub-1 us branch
- [x] Task 14 - GLYPH BOUNDARY VALIDATION | DOD: UTF-8 decode clamps to 1024 glyphs and appends `...` without new string; surrogate boundary guarded | Rejected: unbounded TMP label writes | Estimate: prevents pathological layout tear, no recurring cost
- [x] Task 15 - DYNAMIC UI RESCALE | DOD: `UIRescaleRequestSignal` added; font swap completion publishes signal and `DiegeticHudManualLayout` drains/rebuilds registered layouts | Rejected: per-frame layout polling | Estimate: 20-60 us saved per language switch

Loop 4 - Tasks 16-18
- [x] Task 16 - FALLBACK HASH | DOD: active language loads first, English fallback fills missing hashes, final miss returns `[MISSING_HASH]` bytes | Rejected: returning null/empty string | Estimate: avoids exception/log cascade
- [x] Task 17 - AUP SAFE | DOD: `LocalizedWorldSign` listens to origin shifts, restores cached AUP runtime position, and forces TMP mesh/layout refresh | Rejected: simulated text physics bounds | Estimate: 3-8 us per shifted sign
- [BLOCKED BY DEPENDENCY] Task 18 - OMEGA COMPILE CHECK | DOD: three `dotnet build Hecton8.Core.csproj --no-restore` attempts; current errors are external BootstrapContracts plus prior Cartography/GlobalSignals/World/Physics contract holes | Rejected: cross-domain repair outside UX Babel | Estimate: blocked

Loop 5 - Recursive Verification
- [x] Re-read prompt after tasks | DOD: corrected CLI regex for tags with role/chat_name attributes; full UI_LOCALIZATION_BABEL block extracted | Rejected: exact-tag regex that missed attributes | Estimate: 15 us process cost
- [x] Re-audit variable injection for string.Split/new string | DOD: scan found no `string.Split`; only unrelated dev-only corruption `new string` remains outside variable injection | Rejected: deleting unrelated dev corruption API | Estimate: 0 runtime us
- [x] Read own code for missed zero-GC violations | DOD: scanned Babel paths for `new string`, `GetString`, `Split`, temp byte arrays; cold native/thread-local allocations are labeled | Rejected: hot-path arrays | Estimate: avoids GC spikes
- [x] Read POLISH_MANDATE after task closure | DOD: CLI extracted `<POLISH_MANDATE>` only after Tasks 1-18 were done/blocked; targeted anti-bloat scans complete | Rejected: broad repo cleanup outside UX Babel | Estimate: 8-20 us saved per 100 visible hashes from prefetch polish

Loop 6 - Patient Re-Audit / User No-Build Override
- [x] Re-read AGENTS, status, rationale, current prompt, and 7 mandates | DOD: CLI-only extraction and targeted mandate reload before edits | Rejected: chat memory and broad registry reread | Estimate: 80 us process cost
- [x] Harden monolith LocData path | DOD: static arena entries now count toward UTF-8 map capacity; static LocData can load even when managed language tables are empty | Rejected: table-only Babel startup | Estimate: prevents 100% monolith miss case
- [x] Bound UTF-8 decode before char-buffer growth | DOD: long UTF-8 spans scan to a safe byte boundary before decode and append native ellipsis | Rejected: decode-full-then-clamp allocation growth | Estimate: avoids large thread-local buffer expansion on bad/long strings
- [x] Make prefetch job real and non-blocking | DOD: visible hash job result is applied to queued swaps; staged drain waits for completion instead of same-tick completion | Rejected: schedule+complete in collection path | Estimate: 8-20 us saved per 100 queued localized labels; no worker stall unless teardown
- [x] Guard UTF-8 map disposal against active prefetch readers | DOD: `LocRegistry` tracks the active prefetch read fence and completes it before map/blob mutation or disposal | Rejected: disposing `_utf8Offsets` while a job can still read it | Estimate: avoids native use-after-free on rapid language swaps
- [x] Purge world-sign string assignment | DOD: `LocalizedWorldSign` now hashes its key once, uses Babel/TMP `SetCharArray`, owns fallback/display char buffers, and keeps AUP shift refresh | Rejected: `targetText.text` and uppercase string cache | Estimate: avoids one managed string assignment per language refresh/sign
- [x] Static verification without `dotnet build` | DOD: `git diff --check` and targeted `rg` scans run; no build launched per user command | Rejected: prior compile loop and any dotnet build invocation | Estimate: PENDING VERIFICATION

Loop 7 - Native Sentinel / Prefetch Staleness Audit
- [x] Re-extract UI_LOCALIZATION_BABEL prompt | DOD: CLI regex pulled full tag with attributes from `Docs/Tasks/CURRENT_BATCH.md` | Rejected: stale chat assignment | Estimate: 12 us process cost
- [x] Register Babel UTF-8 hash map with NativeMemorySentinel | DOD: `_utf8Offsets` now calls `RegisterNativeParallelHashMap` on allocation and unregisters before dispose | Rejected: tracking only the byte blob | Estimate: 0 runtime us; leak/audit safety
- [x] Prevent stale visible-hash slices from touching a new queue | DOD: `FontStreamingManager` abandons old prefetch results on language/queue reset while keeping the native job handle alive until completion | Rejected: blocking every new queue on abandoned work | Estimate: avoids one-frame stale-slice correctness failure; 0-16 ms UX stall avoided in worst case
- [x] Let abandoned prefetch jobs finish without gating label drain | DOD: `ProcessSwapBatch` only waits when slices are still intended for the active queue | Rejected: global wait on any in-flight prefetch handle | Estimate: saves up to one label-drain frame after rapid language changes
- [x] Re-audit modified Babel hot paths | DOD: targeted `rg` found no `LocalizationManager.Instance`, `targetText.text`, `.text =`, `SetText(`, `new string`, `Encoding.UTF8.GetString`, `string.Split`, `string.Format`, `JsonUtility.FromJson` in LocRegistry/FontStreamingManager/LabelSwapScheduler/LocalizedWorldSign | Rejected: broad unrelated repo cleanup | Estimate: PENDING VERIFICATION
- [x] No-build static verification pass | DOD: `git diff --check` passed with line-ending warnings only; `dotnet build` was not launched | Rejected: violating user no-build override | Estimate: PENDING VERIFICATION

Loop 8 - Unicode Hash / RTL Consistency Audit
- [x] Re-read Babel and zero-GC mandates plus prompt | DOD: CLI loaded localization mandate, zero-GC mandate, and full `UI_LOCALIZATION_BABEL` prompt | Rejected: relying on prior loop memory | Estimate: 55 us process cost
- [x] Fix static LocData non-ASCII hash parity | DOD: added `LocHash.ComputeUtf8AsUtf16` and switched static arena slices from raw-byte hash to UTF-16-equivalent FNV hash | Rejected: ASCII-only byte hashing for CJK/Arabic/Cyrillic strings | Estimate: correctness win; 0 allocation
- [x] Make legacy RTL visual path match Babel UTF-8 path | DOD: `RTLProcessor.ToVisualOrder` and `TryGetVisualBuffer` now copy into thread-local storage and reverse in place | Rejected: returning logical order from a method named visual | Estimate: 5-15 us saved versus later corrective reshaping; no string allocation
- [x] Clamp world-sign display length to backing buffer | DOD: `LocalizedWorldSign.PrepareDisplayBuffer` caps display length to `sourceBuffer.Length` before TMP handoff | Rejected: trusting upstream length under corrupt data | Estimate: prevents bad buffer read; 0 steady-frame cost
- [x] No-build verification pass | DOD: `git diff --check` on touched Babel files passed with line-ending warnings only; targeted forbidden-pattern `rg` scan found no hot-path violations | Rejected: `dotnet build` per user override | Estimate: PENDING VERIFICATION

Loop 9 - Static LocData Authored Hash Alias Audit
- [x] Re-audit static Data Monolith record mapping | DOD: inspected `H8ItemRecord`, `H8CreatureTraitRecord`, `H8BiomeRecord`, `H8GhostModuleRecord`, and `H8SopErrorRecord` hash/UTF-8 offset fields | Rejected: assuming value-content hashes are enough for authored IDs | Estimate: 60 us process cost
- [x] Add zero-GC static localization reference cursor | DOD: `H8StaticLocalizationReference` plus `H8StaticLocalizationCursor` expose primary authored hash -> LocData slice aliases with caller-owned cursor state | Rejected: managed dictionaries, string key rebuilds, and O(n^2) indexed scans during Babel reload | Estimate: saves cold reload scan time on large monoliths; 0 hot-path us
- [x] Map authored monolith hashes into Babel UTF-8 LUT | DOD: `LocRegistry.TryLoadStaticArenaReferenceAliases` registers item/species/biome/module/error hashes to copied static byte slices without overriding active language table keys | Rejected: duplicating unsafe section pointer walks in UI code and inventing description hashes without a stored key | Estimate: avoids static record display-name misses; 15-45 us saved per 100 monolith-backed label resolutions
- [x] Static verification without build | DOD: `git diff --check` passed with line-ending warnings only; targeted forbidden-pattern `rg` scan found no hot-path violations | Rejected: `dotnet build` per user override | Estimate: PENDING VERIFICATION
