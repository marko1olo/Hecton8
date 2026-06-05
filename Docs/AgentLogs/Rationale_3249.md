# Rationale 3249

Task decision:
- Created a spoiler-gated public/archive packet warning that public evidence can be exploited after release.
- Kept the canon line: public proof makes erasure expensive, not salvation automatic.
- Connected Tau Ceti ledger release, redaction header, caption chain, witness hash, and Deep Reach cleanup risk.
- Separated surface voices: public archive, in-game dossier, terminal warning stamp, Marauder annotation.
- Added proposed LocIDs without touching CSV, binding maps, generated pages, h8bin, Unity assets, or runtime scripts.

Authority basis:
- Canon_Locks.md and Lore_Bible.md: Tau Ceti can make evidence public after delay; Luyten authenticates custody; Deep Reach/Recovery Compliance pressure remains procedural; public ledger release prevents clean erasure but loses control over consequence.
- writing.md and narrative.md: evidence before exposition; surface voices must be distinct; text must be artifact prose, not design notes.
- localization.md and Lore_Localization_Model.md: 15 locale rows, English authority row, non-English draft rows, stable LocIDs, honest status.
- data.md and authoring.md: lore markdown is authoring input only; runtime consumes baked stable IDs, not free-form packet text.
- quality.md and QA_Evidence_Text_Filter_Audit.txt: static text is not runtime, import, profiler, public, or player-build evidence.

Mandates followed:
- QA_Evidence_Text_Filter_Audit: claims restricted to STATIC_DOC file work.
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc: locale rows keep stable IDs and note RTL/CJK risk.
- DATA_Runtime_Struct_Layout_ARM64: no DTO or runtime layout changed.
- TOOL_Designer_Facades_CSV_Binary_Bridge: no source CSV/binary bridge changed.
- OPT_Zero_GC_Policy_AllocFree_Mandate: no runtime or hot-path code changed.

GlobalQualityWeight consequence:
- Low/Compact: shorter warning surface with core custody chain visible.
- Middle: fuller archive/dossier warning.
- High: Marauder annotation and misuse-lane context.
- Ultra: denser archive sidebars without changing truth, IDs, custody, ending state, or public claim state.

Validation:
- UTF-8 strict read: PASS.
- Locale headings: 15 total, 15 unique, no missing, no extra.
- Locale status rows: source count 1, draft count 14.
- U+FFFD count: 0.
- Mojibake marker/codepoint scan: 0.
- Bracketed locale/status heading scan: 0.
- Positive readiness claim phrase scan: 0.
- Scoped `git diff --check`: no output.
- Scoped `git status --short`: four new assigned files only.
- Protected prior-packet token scan in P496 packet: no hits.
