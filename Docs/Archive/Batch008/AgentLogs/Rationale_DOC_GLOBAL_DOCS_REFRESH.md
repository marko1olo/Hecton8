# Rationale_DOC_GLOBAL_DOCS_REFRESH

Problem: The user requested a full documentation actuality pass across a large repository while other agents are concurrently editing docs and code.

Solution: Treat stable authority documents as the editable current brain, dated reports and archives as historical evidence, and asset-local README files as code-adjacent references. Inventory first, then patch only evidence-backed current docs and write a separate currency report.

Rejected Alternatives: Rewriting every historical report was rejected because it destroys evidence. Blind global find/replace was rejected because stale reports intentionally preserve past state. Staging unrelated concurrent changes was rejected because multiple agents are active.

Scalability potential: Low keeps doc lookup cheap by preserving stable authority indexes. Middle keeps historical evidence searchable without making it current policy. High/Ultra can consume generated indexes and reports without losing provenance.

Hardware Impact: Runtime impact is 0 us/frame. Documentation hygiene only.

Evidence Class: STATIC_DOC / STATIC_SOURCE / GIT_CLI. Runtime verification remains PENDING VERIFICATION.

## Decision 1: Stable Authority First

Problem: "All documentation" includes active docs, dated reports, archive evidence, third-party docs, and asset-local README files with different authority levels.

Solution: Update stable authority/index docs and create a current currency report; classify archives, deprecated bundles, third-party notices, and dated reports instead of overwriting their historical content.

Rejected Alternatives: Editing archive evidence was rejected because it falsifies past records. Ignoring non-Docs README files was rejected because code-adjacent docs can mislead implementation work.

Scalability potential: Low/Middle readers get current entry points. High/Ultra forensic review keeps historical deltas intact.

Hardware Impact: Runtime 0 us/frame.

## Decision 2: Header Normalization Scope

Problem: Active stable docs had missing `Date:` and/or `Status:` metadata, but dated reports and archives are historical evidence.

Solution: Normalize only tracked, clean, stable active `Docs` files outside reports, archives, deprecated folders, active AgentLogs/Tasks, and dated forensic bundles. Leave reports and archives intact and classify their status in the new currency report.

Rejected Alternatives: Bulk-editing every old report was rejected because it mutates evidence snapshots. Touching dirty/untracked concurrent files was rejected because other agents own those edits.

Scalability potential: Low/Middle agents can trust active stable docs as current entry points. High/Ultra review can still inspect historical reports without metadata churn.

Hardware Impact: Runtime 0 us/frame.

## Decision 3: Root Drift Classification

Problem: May 15 governance says root has three markdown anchors, but current filesystem scan sees `COMPUTE_AUDIT_BRIEF.md` in root.

Solution: Document `COMPUTE_AUDIT_BRIEF.md` as root drift in governance/reference/report files without moving it, because it was already modified by a concurrent worker.

Rejected Alternatives: Moving or staging the dirty compute file was rejected as cross-agent ownership collision. Treating it as a fourth root authority anchor was rejected because root authority remains intentionally narrow.

Scalability potential: Stable root governance remains simple while compute evidence remains findable through report bundles.

Hardware Impact: Runtime 0 us/frame.

## Decision 4: Narrow Commit And Push

Problem: The worktree still contains unrelated concurrent source/report changes while the documentation refresh needed to be committed and pushed.

Solution: Stage only DOC_GLOBAL_DOCS_REFRESH evidence files, stable header updates, and governance/report index patches. Commit `e4e42fad7`, push it, fetch remote, and verify divergence `0 0`. Then record this closeout in task-local evidence files only.

Rejected Alternatives: Staging the whole dirty tree was rejected. Force push was rejected. Moving dirty root `COMPUTE_AUDIT_BRIEF.md` was rejected because another worker had active changes there.

Scalability potential: Low/Middle agents get current docs without losing parallel work. High/Ultra forensic review can correlate the report, status, rationale, and Git commits.

Hardware Impact: Runtime 0 us/frame.

## Decision 5: Concurrent Delta Ledger Instead Of Ownership Theft

Problem: After the first pushed documentation refresh, the working tree contained a new wave of documentation and source deltas from other active agents. Treating those edits as this agent's final documentation update would erase ownership and make later blame/audit unreliable.

Solution: Generate `Docs/Reports/2026-05-17_DOCUMENTATION_CONCURRENT_DELTA_LEDGER.md` as a second-pass reconciliation artifact. The ledger records `71` documentation candidates visible before ledger creation, `8` dirty source/shader blockers, the active `.md` / `.txt` header gate (`150 / 150` clean), and the `16` JSON files intentionally excluded from Markdown header injection.

Rejected Alternatives: Staging every dirty documentation file was rejected because other agents own the content. Rewriting active AgentLogs/Tasks was rejected because they are evidence streams. Adding textual headers to JSON was rejected because it would corrupt schema/config files.

Scalability potential: Low/Middle readers get a current owner-action list instead of stale uncertainty. High/Ultra review can consume a precise path-level ledger and decide which owner commits, archives, or supersedes each delta.

Hardware Impact: Runtime 0 us/frame. Documentation reconciliation only.

## Decision 6: R3 Root Drift Closure And Index Integration

Problem: The repeated user directive left a concrete documentation drift unresolved: `COMPUTE_AUDIT_BRIEF.md` still lived in repository root, current architecture files were not fully indexed in `Docs/ARCHITECTURE/README.md`, and the new R2/Subnautica documentation reports were not all visible from stable navigation.

Solution: Move the concise compute brief into `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_BRIEF.md`, update root governance/reference files to restore the three-anchor root, update architecture/report/root docs indexes, and write `Docs/Reports/2026-05-17_DOCUMENTATION_INTEGRATION_R3.md` as the verification record.

Rejected Alternatives: Keeping the root drift was rejected because it contradicted governance. Moving or staging dirty source/shader files was rejected because the user requested documentation and those files are outside DOC_GLOBAL_DOCS_REFRESH authority. Leaving new architecture docs unindexed was rejected because undiscoverable docs become stale immediately.

Scalability potential: Low/Middle agents now start from stable indexes instead of root noise. High/Ultra review can trace root cleanup, report movement, architecture inventory, JSON validity, and dirty source boundaries from one R3 report.

Hardware Impact: Runtime 0 us/frame. Documentation routing only.

## Decision 7: R4 Interior Boundary Instead Of GitHub Work

Problem: The user explicitly rejected another GitHub-centered loop and demanded internal documentation updates, not just reports or indexes.

Solution: Run a local-only R4 pass that inserts a machine-findable actuality boundary inside every active stable `.md` / `.txt` document in scope. The boundary makes each document subordinate to `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts. Generated `obj` / `bin` text outputs under `Docs` stay outside active documentation scope.

Rejected Alternatives: Another push was rejected by user order. Editing archives/reports/tasks/logs in bulk was rejected because those are evidence streams. Adding Markdown banners to JSON/config files was rejected because it would corrupt structured data. Annotating generated `obj` file lists was rejected because it would create disposable build-output churn, not documentation currency. Normalizing batch-prompt trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md` was rejected because it would mutate task evidence to satisfy a whitespace gate.

Scalability potential: Low/Middle agents now see the proof boundary inside each active doc without needing to remember a chat instruction. High/Ultra review can grep the R4 marker and audit exactly which active docs carry the current boundary.

Hardware Impact: Runtime 0 us/frame. Documentation actuality annotation only.

## Decision 8: R5 Claim-Level Correction Instead Of More Sorting

Problem: The user rejected further sorting/index work and demanded internal documentation study and repair. R4 made stale claims subordinate, but several active docs still carried obsolete source-scale counters, root-scan wording, direct-interface counts, asmdef counts, and a modding runtime-vs-builder manifest mismatch.

Solution: Run a local-only R5 content pass. Recheck current source/config facts with local CLI scans, then patch the active docs that contained false current claims. Source-scale authority is now `1653` project C# files, `1602` script C# files, `1638` non-test C# files, `1065255` project C# physical lines, `1047015` script physical lines, `1061832` non-test physical lines, `288` interface declaration hits, `63` direct `GlobalRegistryContracts.cs` public interfaces, and `95` first-party asmdefs. Modding docs now state that runtime requires `RequiredAPIVersion` / `ModPriority`, while the SDK builder currently emits only `7` fields.

Rejected Alternatives: Another index cleanup was rejected by direct user order. Rewriting dated historical reports was rejected because they are evidence snapshots. Claiming builder-created mods are runtime-ready was rejected because `ModLoader` disables missing/non-positive `RequiredAPIVersion`.

Scalability potential: Low/Middle agents now get current source-scale and manifest-boundary facts in active docs. High/Ultra review can trace the exact R5 report and rerun the same static scans before making stronger claims.

Hardware Impact: Runtime 0 us/frame. Documentation claim correction only.

## Decision 9: R6 Schema And Active-Doc Interior Closure

Problem: After R5, open Modding docs still contained a source/schema mismatch. The validator first failed because current `GlobalSignals.cs` exposed `170` unique `ISignal` structs while the schema inventory still recorded `134`; after the top-level repair, the nested `staticValidation.lastKnownPass` block still carried stale `134 / 132`. Active docs also had metadata placement drift and two active atlas/X-Ray documents still carried stale asmdef-count interiors.

Solution: Update the Modding schema to revision `14`, align source inventory and last-known-pass to `170 / 2 / 168`, and strengthen `Validate_Mod_API_Static.ps1` so nested last-known-pass drift fails the static gate. Normalize active stable `Date:` / `Status:` placement, classify `Docs/takoi prompt dlya gemini.txt` as an encoding-damaged prompt dump without moving it, update `Docs/PROJECT_ATLAS.md` to `95` first-party asmdefs, and update `PROJECT_STATE_STATIC_XRAY.md` assembly counts to `141 / 95 / 91` with current nearest-asmdef counts.

Rejected Alternatives: Treating a partially repaired validator PASS as sufficient was rejected because stale nested evidence would keep misleading future agents. Moving the prompt dump was rejected because the user explicitly stopped sorting and asked for interior updates. Rewriting generated JSON/config and historical reports was rejected because the R6 fixes targeted active current documentation and schema truth only.

Scalability potential: Low/Middle agents now get a consistent Modding contract without reading contradictory schema blocks. High/Ultra review can rerun the validator and know that both top-level and nested signal counts must agree before the API is promoted.

Hardware Impact: Runtime 0 us/frame. Documentation/schema correction only.

## Decision 10: R7 Regenerate Atlas, Do Not Hand-Edit Generated Counts

Problem: `Docs/DEPENDENCY_GRAPH.md` was an active generated atlas from `2026-05-15` and still exposed stale current counters. The generator also emitted `ATLAS VERIFIED PENDING RUNTIME VERIFICATION` even though the required `Tools/AtlasCheck.py` reference gate had not passed.

Solution: Run `python Tools/BuildArchitectureAtlas.py` to regenerate `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json` from current disk. Patch `Tools/BuildArchitectureAtlas.py` so future output carries `Date:`, the R4 actuality boundary, non-verified status, matching JSON status, and an explicit statement that `AtlasCheck.py` must exit `0` before the atlas is considered verified. Run `py_compile` on the tools and run `AtlasCheck.py`; record its current failure instead of hiding it.

Rejected Alternatives: Hand-editing generated counts was rejected because the next generator run would recreate the old format. Claiming atlas verification was rejected because `AtlasCheck.py` exits `1` with `57` missing RealtimeCSG vendor image references. Moving or deleting vendor-reference text was rejected because this pass is documentation currency, not asset surgery.

Scalability potential: Low/Middle agents now see current atlas counts while still seeing the failed reference gate. High/Ultra review can fix the vendor asset/reference source or update the checker policy without trusting a false verified label.

Hardware Impact: Runtime 0 us/frame. Documentation/tool-output correction only.

## Decision 11: R8 Atlas Cache And Counter Correction

Problem: After R7, the atlas test still required the old `ATLAS VERIFIED` status, atlas Markdown/JSON timestamps could drift, the source cache reused stale line counts for changed files, active docs still promoted R5/R6 counters as current, active compile/H-Phi artifact paths pointed to files moved into archive, and `UNCLAIMED_FUTURE_SYSTEM_SEAMS.md` falsely described several claimed slots as absent.

Solution: Patch the atlas generator and tests instead of hand-editing generated output. The generator now shares one timestamp between Markdown and JSON, validates cache entries by size/mtime before use, and removes the false no-project-file compile sentence. Stable docs now carry the R8 volatile static-source snapshot, archived compile/H-Phi paths, corrected future-seam ownership, corrected tool metadata count, and downgraded verification language. R8 report path: `Docs/Reports/2026-05-17_DOCUMENTATION_ATLAS_AND_COUNTERS_R8_LOCAL.md`.

Rejected Alternatives: Claiming AtlasCheck success was rejected because `Tools/AtlasCheck.py` still exits `1` on `57` missing RealtimeCSG vendor image references. Updating only generated atlas output was rejected because the next generator run would recreate stale cache/test/status behavior. Leaving R5/R6 counts as "current" was rejected because current static scans saw `1716 / 1663 / 1699` C# counts and `104` first-party asmdefs.

Scalability potential: Low/Middle agents get current volatile counts and no false compile-proof paths. High/Ultra review can rely on the generator/test pair to keep atlas status honest while future asset/reference cleanup attacks the RealtimeCSG missing-reference gate.

Hardware Impact: Runtime 0 us/frame. Documentation/tooling correction only.

## Decision 12: R9 Evidence-Language And Archive-Path Correction

Problem: After R8, active docs still contained proof words above their evidence class, stale R8 counters, absent `Docs/AgentLogs` artifact paths for May 15 Core/H-Phi evidence, and missing R4 actuality boundaries in newly active docs.

Solution: Downgrade active `SOURCE VERIFIED`, `STATIC DESIGN VERIFIED`, `OFFLINE SIM VERIFIED`, and Python-fuzz `VERIFIED` wording to static/offline evidence language. Refresh current volatile source counters to `1729 / 1676 / 1712` C# files, `1127320 / 1108505 / 1123322` physical lines, `267` R8-compatible interface declaration hits, `63` direct `GlobalRegistryContracts.cs` interfaces, and `106` first-party asmdefs. Point May 15 Core/H-Phi evidence to `Docs/Archive/Batch007/AgentLogs/...` or `Docs/Archive/Batch006/AgentLogs/...`. Add R4 boundaries to newly active docs and record R9 in `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`.

Rejected Alternatives: Treating R8 as terminal was rejected because source and docs changed again. Trusting active `Docs/AgentLogs` paths was rejected because those files are absent and archived copies exist. Claiming `py_compile` success was rejected because the command hit a `Tools/__pycache__` permission wall; AST parsing was used only as syntax fallback, not bytecode proof.

Scalability potential: Low/Middle agents get current static counters and proof wording that does not overstate runtime. High/Ultra review can trace archive evidence without searching missing active-log paths.

Hardware Impact: Runtime 0 us/frame. Documentation/evidence correction only.
