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
