# Rationale_ARCHIVE015

Problem: Active Docs/AgentLogs and Docs/Tasks contain post-Batch014 files; stale batch state contaminates new work.
Solution: Archive files whose LastWriteTime is at or after Docs/Archive/Batch014 LastWriteTime into Docs/Archive/Batch015 with AgentLogs/Tasks layout preserved. Generate compact summaries instead of raw concatenation.
Rejected Alternatives: Filename prefix filtering misses late 14xx files and nonnumeric audit files. Raw concatenation exceeds requested size and preserves repeated boilerplate.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; process hygiene lowers context load only.
Hardware Impact: Runtime gain 0 us on i3/MX350; disk/context reduction only.
Evidence Class: STATIC_DOC / FILESYSTEM.
Residual risk: LastWriteTime cutoff can include late Batch014 artifacts; user explicitly allowed that.

Problem: Master prompt extraction for ARCHIVE015 returned no XML tag in Docs/Tasks/CURRENT_BATCH.md.
Solution: Treat direct user request as primary directive; archive by Batch014 timestamp.
Rejected Alternatives: Guessing another agent ID from CURRENT_BATCH would contaminate scope.
Scalability potential: Runtime unchanged across Low/Middle/High/Ultra; context load reduced by summary artifacts.
Hardware Impact: 0 us runtime; filesystem-only cleanup.

Problem: Initial summaries were too lossy: 130183 bytes from 10.29 MB source kept only first signal lines per file.
Solution: Regenerated summaries with larger per-file budgets and stronger signal retention: problem/solution/rejected/verification, checkboxes, paths, errors/build, manifests, risk/result, final tails.
Rejected Alternatives: Raw concatenation exceeds 2 MB. Keeping only first hits hides late decisions and verification tails.
Scalability potential: Runtime unchanged; Low/Middle/High/Ultra unaffected. Human context fidelity increased while still below requested storage cap.
Hardware Impact: 0 runtime us; summary bytes now 6502373.

Problem: The first rich-regeneration exceeded the 2 MB cap because high-priority lines bypassed per-file budget.
Solution: Rebuilt selector with hard budget checks for every line. New summary bytes: 690180.
Rejected Alternatives: Keeping 6.5 MB summaries violates direct user cap. Returning to 130 KB loses too much file-level detail.
Scalability potential: Runtime unchanged; context fidelity bounded under cap.
Hardware Impact: 0 runtime us.


Problem: 690 KB summaries were valid but still below useful density for 10.29 MB source.
Solution: Increased hard budgets while preserving 2 MB cap. New summary bytes: 1352935.
Rejected Alternatives: 130 KB first-pass and 690 KB second-pass both risk hiding useful agent evidence.
Scalability potential: Runtime unchanged; archive readability improved.
Hardware Impact: 0 runtime us.


Problem: Summary sample exposed raw Unity YAML recovery-prefab content; informative value low.
Solution: Treat .prefab/.unity/.asset as structured artifacts and summarize as path/type/size only. Reallocated budget to text logs. New summary bytes: 1428389.
Rejected Alternatives: Keeping YAML snippets wastes cap; deleting raw recovery files would destroy evidence.
Scalability potential: Runtime unchanged; archive signal density improved.
Hardware Impact: 0 runtime us.


Problem: Active Docs/AgentLogs and Docs/Tasks still contained pre-Batch015/old Batch013-Batch014 files plus mixed 15/16 debris.
Solution: Moved old files by filename/timestamp target: 13xx to Batch013, 14xx/older audit debt to Batch014, 15xx/16xx to Batch015. Left CURRENT_BATCH.md and files modified after 2026-06-02T12:01:24.2719739+04:00. Restored CURRENT_BATCH.md from Batch015 archive copy if missing.
Rejected Alternatives: Moving every file regardless of LastWriteTime could steal actively updated logs. Deleting duplicates would lose evidence. Raw flattening rejected because archive paths must preserve source layout.
Scalability potential: Runtime unchanged. Context and file-scan load reduced for all lanes.
Hardware Impact: 0 runtime us; filesystem hygiene only.
