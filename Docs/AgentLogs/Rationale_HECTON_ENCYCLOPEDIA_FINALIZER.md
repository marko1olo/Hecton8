# Rationale - HECTON_ENCYCLOPEDIA_FINALIZER

## Decision 0 - Documentation Ownership Boundary

Problem: Final encyclopedia work can easily mutate reports, task logs, or other agents' active files while 20+ agents are editing the same tree.

Solution: Restrict first pass to stable documentation authority and this agent's own ledgers unless the extracted prompt requires a specific existing index/map file. Evidence class: STATIC_DOC.

Rejected Alternatives: Broad cleanup of `Docs/AgentLogs`, `Docs/Tasks`, or archived report folders was rejected because batch hygiene rules forbid consuming or rewriting previous-batch context without explicit instruction.

Scalability potential: Low tier gains no runtime cost because docs only; middle/high/ultra gain clearer routing for future systems without runtime mutation.

Hardware Impact: 0 us runtime gain on i3/MX350; expected effect is reduced engineering rework, not frame-time improvement.

## Decision 1 - Evidence Language

Problem: The prompt demands finalization, but QA evidence mandate forbids claiming runtime or Unity verification from text edits.

Solution: Use `PENDING VERIFICATION` for runtime/compiler/perf claims and reserve `ENCYCLOPEDIA VERIFIED` only for the documentation artifact after spellcheck/link-style audit succeeds.

Rejected Alternatives: Declaring 0 GC, compile health, or runtime readiness from documentation was rejected as false verification.

Scalability potential: Low/middle/high/ultra content can be documented, but no runtime tier claim is valid without profiler artifacts.

Hardware Impact: 0 us runtime gain; prevents bad engineering decisions caused by fake status.

## Decision 2 - 85 Domains Instead Of Stale 80-Domain Wording

Problem: The extracted prompt asks for an ASCII map of 80 domains, but `Docs/Actual Domains of Project.txt` defines domains `1..85`.

Solution: Update the architecture map with an ASCII 85-domain backbone and explicitly state that the prompt's 80-domain wording is stale. Evidence class: STATIC_DOC.

Rejected Alternatives: Trimming domains 81-85 or hiding the mismatch was rejected because it would sabotage current ownership mapping.

Scalability potential: Low/middle/high/ultra systems keep correct ownership routing; no tier-specific runtime path changes.

Hardware Impact: 0 us runtime gain on i3/MX350; reduces integration errors from false domain boundaries.

## Decision 3 - FAQ And Glossary As Separate Stable Docs

Problem: `Docs/README.md` already carried too much volatile audit text. Embedding FAQ/glossary there would recreate the same entry-point bloat.

Solution: Keep README as a navigation index and create `Docs/TECHNICAL_FAQ.md` plus `Docs/H8_GLOSSARY.md`, linked from the stable anchors. Evidence class: STATIC_DOC.

Rejected Alternatives: Writing a single monolithic README "bible" was rejected because it increases context load and makes future targeted reading worse.

Scalability potential: Low tier agents load less text; high/ultra feature owners still get direct links into full architecture contracts.

Hardware Impact: 0 us runtime gain; improves documentation lookup cost only.

## Decision 4 - Spellcheck Classification

Problem: A literal spellcheck over all `Docs/` surfaces includes archives, patches, transliterated Russian, GUID-like fragments, Unity type names, and code identifiers. Treating every suspicious token as a defect would be false cleanup.

Solution: Run a Python `pyspellchecker` audit over all text files in `Docs/`, report the corpus metrics, and classify owned-doc suspicious terms separately. Owned-doc suspicious terms were project/proper nouns and technical identifiers. Evidence class: PY_SPELLCHECK.

Rejected Alternatives: Bulk editing archive spellcheck hits was rejected because it would mutate historical evidence and create noise outside the prompt.

Scalability potential: Low/middle/high/ultra unaffected at runtime; future agents get a concrete spellcheck boundary instead of guessing.

Hardware Impact: 0 us runtime gain on i3/MX350; documentation-only audit.

## Decision 5 - Link Check As Static Proof Only

Problem: The rewritten README links many stable docs and reports. Broken relative links would make the index useless.

Solution: Run a Python filesystem link checker over owned encyclopedia markdown files. It checked 149 relative links with 0 missing targets. Evidence class: FILESYSTEM.

Rejected Alternatives: Visual/manual link review was rejected because it does not produce reproducible proof.

Scalability potential: Documentation navigation is stable for all feature tiers.

Hardware Impact: 0 us runtime gain; lower human lookup cost only.

## Decision 6 - Missing Polish Mandate

Problem: The workflow requires a `<POLISH_MANDATE>` tag after core tasks, but the current batch file has no such tag.

Solution: Record the missing tag and execute a local anti-bloat pass using available mandate checks: ASCII, false-verification phrase scan, link check, line count, and diff hygiene. Evidence class: STATIC_DOC / FILESYSTEM.

Rejected Alternatives: Inventing a polish mandate was rejected because the batch file is the authority.

Scalability potential: No runtime tier change; documentation remains concise enough for low-context future agents and complete enough for high-tier system owners.

Hardware Impact: 0 us runtime gain on i3/MX350; documentation-only final pass.

## Decision 7 - Isolated Commit

Problem: The worktree contains many unrelated active agent edits. A broad commit would capture other agents' work.

Solution: Stage only files owned by HECTON_ENCYCLOPEDIA_FINALIZER: README, global architecture map, FAQ, glossary, spellcheck report, status, rationale, and log.

Rejected Alternatives: `git add Docs` or committing all modified files was rejected as cross-agent contamination.

Scalability potential: Source-control hygiene keeps parallel work separable across low/middle/high/ultra feature owners.

Hardware Impact: 0 us runtime gain; repository hygiene only.

## Decision 8 - Continuation After Batch Rotation

Problem: On 2026-05-15 the current batch file no longer contains `<AGENT_PROMPT id="HECTON_ENCYCLOPEDIA_FINALIZER">`, so the original prompt cannot be re-extracted from the active file.

Solution: Continue from the already-created status/rationale/log files as long-term memory and record the batch rotation as evidence. Evidence class: STATIC_DOC.

Rejected Alternatives: Restarting under a different agent prompt or inventing a replacement prompt was rejected because it would violate the strict parsing rule.

Scalability potential: No runtime tier impact; protects multi-agent documentation state from batch churn.

Hardware Impact: 0 us runtime gain on i3/MX350; documentation continuity only.

## Decision 9 - Complete Tracked Direct Report Inventory

Problem: The README needed mechanical proof of coverage for every tracked direct `Docs/Reports/*.md` file.

Solution: Verify `Docs/README.md` contains a complete tracked direct report inventory and validate that all 84 tracked direct reports are linked. Nested deprecated snapshots remain excluded by governance.

Rejected Alternatives: Linking nested deprecated reports in the primary README was rejected because deprecated snapshots are not current project authority.

Scalability potential: Low-context agents get direct report routing; high-tier feature owners still use current stable docs first.

Hardware Impact: 0 us runtime gain; reduces documentation lookup cost only.
