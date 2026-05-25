# Rationale X_012

## Decision 01 - Source Beats Prompt Constants

Problem: The X_012 prompt says `SignalBusRegistry` capacity should be `256`, while current source defines `LaneCapacity = 512` in `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`.
Solution: Treat source as authority and patch docs to `512`.
Rejected Alternatives: Repeating prompt value would preserve stale documentation and violate the prompt's own source-synchronization rule.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; accurate lane capacity prevents false migration pressure.
Hardware Impact: 0 us runtime; process gain only on i3/MX350.

## Decision 02 - Data Monolith Presence Reclassified

Problem: Active docs still state `static_data.h8bin` is absent and H8DM header is `16` bytes. Current filesystem has `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` at `1,064,384` bytes; source defines `H8DataLayoutConstants.HeaderSizeBytes = 64`.
Solution: Patch central authority docs to present/baked/static-source status and keep Unity/player/profiler proof as pending.
Rejected Alternatives: Marking full runtime readiness from file presence; preserving stale absence claims.
Scalability potential: Low/Middle/High/Ultra static data path is now real, but runtime proof still gates platform claims.
Hardware Impact: 0 us runtime claimed; avoids false blocker reports on cheap devices.

## Decision 03 - Root Bloat Must Be Preserved Then Compressed

Problem: Root policy allows only three text anchors, but two anchors are large historical ledgers (`MASTER_RELEASE_WORK_PLAN.md` and `BUILD_PLAYTEST_ISSUES.md`) that overload root context.
Solution: Preserve full originals under `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/`, then replace root files with concise active summaries.
Rejected Alternatives: Deleting historical text; leaving root bloat active; moving anchors without replacements.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; future agents load fewer tokens and find current contracts faster.
Hardware Impact: 0 us runtime; documentation-context reduction only.

## Decision 04 - Historical Reports Leave Active Corpus

Problem: Active `Docs/Reports` markdown held historical evidence snapshots and generated queues that dominated active word count while not serving as current contracts.
Solution: Move 160 top-level report markdown/txt files to `Docs/_Archive/Reports_X_012_2026-05-23/`, keep current JSON proof artifacts and report index in `Docs/Reports`, and rewrite references to archived markdown paths.
Rejected Alternatives: Deleting reports; leaving historical reports active; moving current JSON proof artifacts permanently.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; future agents load less stale evidence before finding current contracts.
Hardware Impact: 0 us runtime; context reduction only.

## Decision 05 - Active Validation Is Scripted, Not Narrative

Problem: Manual claims about "clean docs" are not reproducible under parallel-agent churn.
Solution: Extend `Tools/VerifyDocStructure.py` and `Tools/OOP_Doc_Scanner.py` to emit proof JSON for root policy, stale parameters, encoding, fences, links, source constants, and word reduction.
Rejected Alternatives: Chat-only report; one-off PowerShell counts without persisted artifacts.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; documentation quality gate becomes repeatable on cheap devices.
Hardware Impact: 0 us runtime; latest offline scanner run took 131.3 s.

## Decision 06 - Current Source Constants Remain Static Proof Only

Problem: `static_data.h8bin` exists and source constants align, but runtime/import/player proof was not part of X_012's markdown-only scope.
Solution: Mark Data Monolith payload presence as STATIC_FILESYSTEM/STATIC_SOURCE and keep Unity/player/profiler readiness as PENDING VERIFICATION.
Rejected Alternatives: Upgrading Data Monolith readiness from file presence alone; launching a build for documentation-only edits.
Scalability potential: Low/Middle/High/Ultra route can now rely on correct static facts, but runtime behavior still needs separate proof.
Hardware Impact: 0 us runtime claimed; avoids false readiness reports on i3/MX350 and high-end targets alike.

## Decision 07 - Report Archive Header Instead Of Content Rewrite

Problem: Historical report contents may contain stale constants, but rewriting each report would corrupt evidence provenance.
Solution: Prefix archived markdown/txt with an archive header and move them out of active validation scope.
Rejected Alternatives: Editing historical report internals to look current; deleting them; leaving them indexed as active.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; active docs remain concise while evidence remains recoverable.
Hardware Impact: 0 us runtime; documentation search/load reduction only.

## Decision 08 - Architecture Run Logs Are Not Active Specs

Problem: `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` were carrying full historical prose inside active architecture scope.
Solution: Archive full snapshots under `Docs/_Archive/Architecture_X_012_APEX_2026-05-23/` and keep active files as compact source-fact ledgers with proof links.
Rejected Alternatives: Hard deletion; leaving full changelogs active; trimming random paragraphs without a recoverable archive.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; documentation search/load cost drops while source constants remain explicit.
Hardware Impact: 0 us runtime; active architecture text reduced by offline documentation surgery only.

## Decision 09 - Machine-Readable Payload Facts Beat Prose Preservation

Problem: The binary payload ledger contained hundreds of boundary records embedded in narrative text.
Solution: Extract `288` payload records into `Docs/Reports/BINARY_PAYLOAD_LEDGER_CONCISION_X_012.json` and make the active architecture file point to JSON plus source constants.
Rejected Alternatives: Keeping the 117k-word ledger active; rewriting every record as markdown prose.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; future tooling can parse JSON directly instead of scraping prose.
Hardware Impact: 0 us runtime; context and audit overhead only.

## Decision 10 - Paragraph-Length Gate Is Part Of Architecture Quality

Problem: A new `GLOBAL_AUTHORITY_BOUNDARIES.md` note arrived as a 324-word paragraph and broke the APEX architecture pass.
Solution: Convert the note to short sections and bullets while preserving every route fact, compile boundary, and missing-proof statement.
Rejected Alternatives: Ignoring the paragraph because another agent added it; deleting it; weakening the scanner.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture remains scannable under parallel-agent churn.
Hardware Impact: 0 us runtime; latest active-doc proof reports `48.08091541745749%` reduction and `0` long architecture paragraphs over 180 words.

## Decision 11 - Strict Architecture Paragraph Threshold

Problem: The `180` word paragraph gate still allowed dense academic prose in active architecture files.
Solution: Add a `90` word unstructured-paragraph gate plus tutorial-marker scan to `Tools/OOP_Doc_Scanner.py`, then rewrite every failing architecture paragraph into list structure.
Rejected Alternatives: Manual spot review; claiming prose is concise because the old threshold passed; weakening the gate for numbered lists without checking tutorial/stale markers.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active specs become deterministic scan artifacts instead of narrative context dumps.
Hardware Impact: 0 us runtime; current proof reports `48.02898547982247%` reduction, `0` strict unstructured architecture paragraphs over `90` words, and `0` tutorial markers.

## Decision 12 - Archive Before Bulk Paragraph Surgery

Problem: Mechanical paragraph rewriting touched 39 active architecture files and could obscure prior wording if no snapshot existed.
Solution: Copy pre-strict versions to `Docs/_Archive/Architecture_X_012_APEX_2026-05-24/` with archive headers and manifest before writing active replacements.
Rejected Alternatives: Hard delete, silent rewrite, or duplicating old versions inside active docs.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; forensic documentation recovery remains available without loading stale prose as active doctrine.
Hardware Impact: 0 us runtime; archive affects disk only, not player behavior.

## Decision 13 - Structured Lines Can Still Be Bloated

Problem: The paragraph gate passed while long bullet/table lines still carried up to 478 words in one active architecture item.
Solution: Add a `70` word structured-line gate to `Tools/OOP_Doc_Scanner.py`, split long list items, and convert the long Data Monolith handoff table row into a compact subsection table.
Rejected Alternatives: Treating bullets as automatically concise; raising the threshold; deleting facts.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active specs now fail on both narrative bloat and list-item bloat.
Hardware Impact: 0 us runtime; current proof reports `47.94267094824329%` active-text reduction and `0` strict structured-line offenders.

## Decision 14 - Parallel Report Churn Is Not A Reason To Ship Red Proof

Problem: Other agents created or rewrote active report files during validation, causing active count and UTF-8-SIG drift between OOP and structure passes.
Solution: Normalize the new live report encodings, rerun both validators, and record the final active doc count as `581`.
Rejected Alternatives: Reporting an older green count; ignoring encoding drift because it came from another agent.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; validator evidence remains current under concurrent agent writes.
Hardware Impact: 0 us runtime; documentation validation wall time only.

## Decision 15 - File-Scale Bloat Requires A Hard Gate

Problem: Paragraph and structured-line gates passed while six active architecture files still exceeded `2500` words and kept historical ledger mass in active specs.
Solution: Add a `2500` word file cap to `Tools/OOP_Doc_Scanner.py`, archive the six full snapshots, and replace active files with current-contract summaries.
Rejected Alternatives: Raising paragraph/line thresholds, leaving bloated files because line-local checks passed, or deleting historical text without archive.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now has file-level bounds for faster source-of-truth review.
Hardware Impact: 0 us runtime; documentation read/load overhead only.

## Decision 16 - Archive History, Keep Active Specs Current

Problem: Historical ledgers contain useful forensic context but are not active architecture contracts.
Solution: Move full pre-cap copies to `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/` and index the archive plus JSON proof from active README files.
Rejected Alternatives: Duplicating history inside active specs, unindexed archive moves, or rewriting history to look current.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; agents can recover history without loading it by default.
Hardware Impact: 0 us runtime; final proof target remains documentation-only.

## Decision 17 - Diff Provenance Is Not Active Architecture

Problem: Two `.diff` provenance files remained directly under `Docs/ARCHITECTURE/` and carried `150060` words of patch evidence outside the `.md/.txt` active-doc gates.
Solution: Move them to `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_RESIDUAL_PROSE/diff_provenance/` with `ARCHIVE_` filename prefixes and make `OOP_Doc_Scanner` fail on active non-contract architecture text files.
Rejected Alternatives: Leaving them active because the previous prompt scoped only `.md/.txt`; deleting patch evidence; counting them as current architecture specs.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; agents no longer load patch bodies as active design contracts.
Hardware Impact: 0 us runtime; documentation-context reduction only.

## Decision 18 - Residual Prose Needs Sentence-Level Gates

Problem: A `90` word paragraph cap still allowed dense 60-80 word prose blocks and long single-sentence route-card facts.
Solution: Tighten architecture gates to `55` words per unstructured paragraph and `35` words per unstructured sentence, then convert offenders to bullets while preserving parameters.
Rejected Alternatives: Excluding ```text route-card blocks from review; deleting constants to satisfy length; lowering proof quality by marking narrative text as acceptable because file caps passed.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; current docs become faster to parse under parallel-agent churn.
Hardware Impact: 0 us runtime; final proof remains STATIC_DOC / STATIC_SOURCE.

## Decision 19 - Source Routes Must Match Disk Paths

Problem: The active actuality ledger had correct constants but stale source routes for `SaveBinaryStorage.cs` and `H8DataMonolithTypes.cs`.
Solution: Patch the ledger to `Assets/_Project/Scripts/SaveBinaryStorage.cs` and `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs`, then rerun both validators.
Rejected Alternatives: Leaving stale route text because values were correct; editing generated JSON without fixing the active ledger.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; documentation route ownership is now auditable by path.
Hardware Impact: 0 us runtime; latest proof remains docs-only.

## Decision 20 - Manual Rewrite Constraint

Problem: The user rejected script-driven prose rewriting and required manual rework for any remaining bloated active specifications.
Solution: Use scripts only for discovery/validation; perform all text rewrites with explicit `apply_patch` edits after reading target lines.
Rejected Alternatives: Bulk regex replacement; weakening scanner thresholds; claiming prior green reports without a manual marker sweep.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; future agents load less document-voice boilerplate before reaching facts.
Hardware Impact: 0 us runtime; documentation-context reduction only.

## Decision 21 - Transient Report Output Exclusion

Problem: `Docs/Reports/X007_scanner_stdout.txt` and `X007_scanner_stderr.txt` were zero-byte live outputs locked by another Python process, causing encoding validation red despite not being documentation contracts.
Solution: Exclude `Docs/Reports/*_stdout.txt` and `*_stderr.txt` from active doc validation in `VerifyDocStructure.py` and `OOP_Doc_Scanner.py`.
Rejected Alternatives: Killing another agent's process; force-moving locked files; weakening BOM checks for real `.md/.txt` specifications.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active docs gate no longer depends on transient live process outputs.
Hardware Impact: 0 us runtime; validator stability only.

## Decision 22 - Near-Threshold Density Gate

Problem: The hard scanner gate at `70` words per structured line still allowed dense `60..69` word bullets/table rows in active architecture specs.
Solution: Run a stricter manual density audit at `>=60` structured-line words, inspect offenders, and rewrite facts by hand into short lists/tables.
Rejected Alternatives: Lowering scanner thresholds before proof, bulk regex rewriting, or claiming the prior green gate was sufficient.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; architecture specs load faster and expose route facts without prose parsing.
Hardware Impact: 0 us runtime; final proof reports `55.06936371646136%` active text reduction and `0` structured lines at `>=60` words in the targeted audit.

## Decision 23 - Encoding Drift Is A Gate Failure, Not A Report Excuse

Problem: Two current report markdown files from parallel agents lacked UTF-8 BOM and kept `VerifyDocStructure.py` red after X_012 edits were complete.
Solution: Normalize only file encoding for `Docs/Reports/KCC_APEX_AUDIT_X_005.md` and `Docs/Reports/SIGNAL_QUEUE_INGRESS_BUDGET_CLOSURE_X_001.md`; preserve text content byte-for-byte after decoding.
Rejected Alternatives: Ignoring the red validator because another agent caused it, weakening the BOM gate, or archiving unrelated current reports.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; documentation validation stays deterministic under concurrent report writes.
Hardware Impact: 0 us runtime; final structure gate reports `encodingWithoutUtf8Sig=0`.

## Decision 24 - Micro-Density Gate

Problem: Loop 12 proved zero architecture structured lines at `>=60` words, but a fresh Loop 13 audit still found unstructured paragraphs at `55` words and new dense structured rows introduced by parallel documentation churn.
Solution: Manually split every confirmed architecture paragraph `>=55` words and structured line `>=60` words into tables/lists while preserving route facts and constants.
Rejected Alternatives: Bulk regex rewriting, raising thresholds, or claiming the official `70` word structured-line gate was enough.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; future agents read fewer dense prose blocks before reaching active facts.
Hardware Impact: 0 us runtime; final micro-audit reports `paragraphsGe55=0`, `structuredLinesGe60=0`, and max architecture file `2481` words.

## Decision 25 - Prompt Example Capacity Remains Non-Authoritative

Problem: The repeated APEX prompt cites SignalBus capacity `256`, while current C# source still defines the generic runtime lane capacity as `512`.
Solution: Keep active documentation and proof JSON aligned to `SignalBusRuntime.LaneCapacity = 512`; explicitly mark prompt `256` as stale example text in the Loop 13 proof.
Rejected Alternatives: Regressing documentation to prompt text or documenting both values as equal authority.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; source-owned capacity avoids false queue-pressure documentation.
Hardware Impact: 0 us runtime; source-sync validator remains `true`.

## Decision 26 - Ultra-Density Manual Gate

Problem: Loop 13 left no paragraphs `>=55` words and no structured lines `>=60` words, but a tighter Loop 14 probe still exposed 50+ word architecture blocks and one document-voice marker.
Solution: Manually split confirmed offenders into compact bullets/tables with `apply_patch`, then write `ARCHITECTURE_ULTRA_DENSITY_AUDIT_X_012.json` proving paragraphs `>=50` words `0`, structured lines `>=50` words `0`, marker hits `0`, source sync `true`, and active-text reduction `55.00994039112259%`.
Rejected Alternatives: Bulk regex rewriting; lowering proof to the old `55/60` threshold; changing source-owned constants to match prompt examples.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; future agents parse source facts faster without losing route constants.
Hardware Impact: 0 us runtime; documentation-context reduction only.

## Decision 27 - 45-Word APEX Density Gate

Problem: Loop 14 proved zero active architecture blocks at `>=50` words, but a stricter Loop 15 probe still found `63` unstructured paragraphs and `66` structured lines at `45..49` words.
Solution: Manually split every confirmed `>=45` word block into tighter bullets, tables, or short paragraphs with `apply_patch`, then write `ARCHITECTURE_45WORD_DENSITY_AUDIT_X_012.json` proving `paragraphsGe45=0`, `structuredLinesGe45=0`, `markerHits=0`, source sync `true`, structure pass `true`, and active-text reduction `54.958879096936975%`.
Rejected Alternatives: Bulk regex rewriting; pretending `>=50` was enough; changing source-owned constants to satisfy prompt examples; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture docs now force dense route facts into short parse units.
Hardware Impact: 0 us runtime; documentation-context reduction only.

## Decision 28 - 40-Word APEX Density Gate

Problem: Loop 15 proved zero active architecture blocks at `>=45` words, but a stricter Loop 16 probe still found `204` active architecture blocks at `40..44` words.
Solution: Manually split confirmed `>=40` word paragraphs, bullets, and table rows with `apply_patch`, then write `ARCHITECTURE_40WORD_DENSITY_AUDIT_X_012.json` proving `finalOffenderCount=0`, source constants, manual rewrite scope, and prompt-stale SignalBus `256` correction.
Rejected Alternatives: Bulk regex rewriting; lowering the target back to `45`; treating dense table rows as already concise; changing source-owned `512` to prompt example `256`.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture docs now expose route facts in smaller parse units for every future agent.
Hardware Impact: 0 us runtime; final proof reports `54.95944570925487%` active text reduction and `0` architecture blocks at `>=40` words.

## Decision 29 - 35-Word APEX Density Gate

Problem: Loop 16 proved zero active architecture blocks at `>=40` words, but the user required a stricter "army manual" density pass and current active docs still held `319` blocks at `35..39` words.
Solution: Manually split every confirmed `>=35` word paragraph, bullet, and table row with `apply_patch`; then harden `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>34` words.
Rejected Alternatives: Bulk regex rewriting; treating table/list rows as acceptable; preserving stale `static_data.h8bin` missing text; changing source-owned `512` SignalBus capacity to the prompt example `256`.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; future agents reach source-owned facts faster and no gameplay truth, DTO layout, save identity, or authority route changed.
Hardware Impact: 0 us runtime; final proof reports `54.96035445616248%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=35` words.

## Decision 30 - 34-Word APEX Density Gate

Problem: Loop 17 proved zero active architecture blocks at `>=35` words, but a stricter audit still found `58` blocks at `>=34` words and one residual document-voice marker.
Solution: Manually split every confirmed `>=34` word paragraph, bullet, and table row with `apply_patch`; then harden `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>33` words.
Rejected Alternatives: Bulk regex rewriting; accepting 34-word list rows as concise; changing source-owned `512` SignalBus capacity to prompt example `256`; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now forces route facts into smaller parse units without changing gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: 0 us runtime; final proof reports `54.908927175155206%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=34` words.

## Decision 31 - 33-Word APEX Density Gate

Problem: Loop 18 proved zero active architecture blocks at `>=34` words, but a stricter audit still found `88` blocks at exactly `33` words.
Solution: Manually split every confirmed `>=33` word paragraph, bullet, numbered line, and table row with `apply_patch`; then harden `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>32` words.
Rejected Alternatives: Bulk regex rewriting; preserving 33-word route facts as concise enough; changing source-owned `512` SignalBus capacity to prompt example `256`; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now enforces even smaller parse units while preserving source-owned constants and route identity.
Hardware Impact: 0 us runtime; final proof reports `54.90493032456174%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=33` words.

## Decision 32 - 32-Word APEX Density Gate

Problem: Loop 19 proved zero active architecture blocks at `>=33` words, but the scanner word function still found `30` blocks at exactly `32` words.
Solution: Manually split every confirmed `>=32` word paragraph/sentence with `apply_patch`; then harden `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>31` words.
Rejected Alternatives: Bulk regex rewriting; treating 32-word route facts as concise enough; changing source-owned `512` SignalBus capacity to prompt example `256`; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now enforces smaller parse units while preserving source-owned constants and route identity.
Hardware Impact: 0 us runtime; final proof reports `54.89104336635201%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=32` words.

## Decision 33 - 31-Word APEX Density Gate

Problem: Loop 20 proved zero active architecture blocks at `>=32` words, but a stricter scan still found `96` blocks at `31` words.
Solution: Manually split confirmed `>=31` word paragraphs, sentences, bullets, numbered lines, and table rows with `apply_patch`; then harden `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>30` words.
Rejected Alternatives: Bulk regex rewriting; preserving 31-word route facts as concise enough; changing source-owned `512` SignalBus capacity to prompt example `256`; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now enforces smaller parse units without changing gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: 0 us runtime; final proof reports `54.87674634739819%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=31` words.

## Decision 34 - 30-Word APEX Density Gate

Problem: Loop 21 proved zero active architecture blocks at `>=31` words, but a stricter scan still found `107` blocks at `30` words.
Solution: Manually split confirmed `>=30` word paragraphs, sentences, bullets, numbered lines, and table rows with `apply_patch`; then harden `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>29` words.
Rejected Alternatives: Bulk regex rewriting; preserving 30-word route facts as concise enough; changing source-owned `512` SignalBus capacity to prompt example `256`; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now enforces smaller parse units without changing gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: 0 us runtime; final proof reports `54.727998480643194%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=30` words.

## Decision 35 - 29-Word APEX Density Gate

Problem: Loop 22 proved zero active architecture blocks at `>=30` words, but a stricter scan still found `122` blocks at exactly `29` words.
Solution: Manually split every confirmed `>=29` word paragraph, sentence, bullet, numbered line, and table row with `apply_patch`; then harden `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>28` words.
Rejected Alternatives: Bulk regex rewriting; preserving 29-word route facts as concise enough; changing source-owned `512` SignalBus capacity to prompt example `256`; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now enforces smaller parse units while preserving source-owned constants and route identity.
Hardware Impact: 0 us runtime; final proof reports `54.71277175135032%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=29` words.

## Decision 36 - 28-Word APEX Density Gate

Problem: Loop 23 proved zero active architecture blocks at `>=29` words, but a stricter scan still found `104` blocks at exactly `28` words across `69` files.
Solution: Manually split or trimmed every confirmed `>=28` word paragraph, sentence, bullet, numbered line, and table row with `apply_patch`; then hardened `Tools/OOP_Doc_Scanner.py` so architecture paragraphs, sentences, and structured lines fail at `>27` words.
Rejected Alternatives: Bulk regex rewriting; preserving 28-word route facts as concise enough; changing source-owned `512` SignalBus capacity to prompt example `256`; launching a build for docs-only edits.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; active architecture now enforces smaller parse units while preserving source-owned constants and route identity.
Hardware Impact: 0 us runtime; final proof reports `54.63695643676832%` active text reduction, `0` stale parameter files, and `0` architecture blocks at `>=28` words.
