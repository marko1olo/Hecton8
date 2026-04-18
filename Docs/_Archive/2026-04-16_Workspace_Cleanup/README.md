**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Workspace Cleanup Archive

Date: `2026-04-16`
Status: `PENDING VERIFICATION`

This archive was created to remove stale one-shot documentation from the active workspace without deleting history.

Archive rules used for this pass:
- Older than roughly `4-5` days and clearly a report, handoff, prompt, chat dump, migration walkthrough, or status snapshot.
- Not treated as active source-of-truth design/spec documentation.
- Safe to move because newer `/Docs` execution docs or cleaner active ledgers already exist.

Structure:
- `root/feature_migration_docs` - one-off migration task packs, plans, walkthroughs.
- `root/reports_and_findings` - stale findings, issue dumps, work-completed reports.
- `root/legacy_prompts_and_chat_dumps` - prompt payloads, duplicate agent rules, chat/news dumps.
- `root/procedural_status_snapshots` - point-in-time procedural status ledgers and transfer snapshots.
- `root/legacy_flora_sessions` - raw and optimized old coral/seaweed session docs.
- `root/legacy_plans` - short-lived sprint or task planning docs.
- `root/shell_reports` - stale shell/settings completion summaries and readiness checklists.
- `folders/legacy_agent_drops` - whole archived agent-output folders moved out of repo root.
- `folders/ai_findings` - older findings subfolders moved out of mixed active findings space.
- `docs/session_audits` - archived audit bundles that no longer belong in active `/Docs`.
- `docs/legacy_external_reviews` - imported Gemini/chat review dumps no longer used as active source of truth.
- `docs/cleanup_ledgers` - one-shot cleanup bookkeeping docs kept only for traceability.

Kept active in root:
- `AGENTS.md`
- live roadmap/spec/reference docs
- recent shell/settings docs from `2026-04-14+`
- current design/spec/lore anchors

Verification: file moves only. No runtime code or project settings changed.
