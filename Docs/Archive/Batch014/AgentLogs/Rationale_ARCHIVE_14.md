# Rationale_ARCHIVE_14

## Decision 2026-05-30-001: Archive Mode
Problem: User requested Batch014 archival while 20+ agents may have active docs/logs and explicitly said `CURRENT_BATCH.md` must not be touched.
Solution: Repeat Batch013 copy-only pattern: create `Docs/Archive/Batch014/{Tasks,AgentLogs,Summaries}`, copy source artifacts intact, exclude `Docs/Tasks/CURRENT_BATCH.md`, preserve source files.
Rejected Alternatives: Move/delete active files; wipe `Docs/Tasks`; copy `CURRENT_BATCH.md`; invent a new archive structure.
Scalability potential: Low/Middle/High/Ultra unaffected. This is process containment, not runtime logic.
Hardware Impact: 0 runtime us. Filesystem operation only; no Unity or game frame impact.

## Decision 2026-05-30-002: Summary Compression
Problem: User needs summaries suitable for another model with total summary budget <= 2.5 MB, while raw logs/tasks contain boilerplate, XML, repeated prompts, and noisy punctuation.
Solution: Generate focused summaries per bucket. Keep task IDs, blockers, evidence claims, file paths, decisions, proof artifacts, and unresolved risks. Collapse exact duplicates by SHA256. Strip blank churn, markdown fences, XML wrappers, long separators, and repeated protocol boilerplate.
Rejected Alternatives: Raw concatenation; full article-style summaries; destructive source cleanup; summarizing from chat memory.
Scalability potential: Faster downstream context loading across cheap and high-end machines; no gameplay behavior change.
Hardware Impact: 0 runtime us. Reduces later token/memory cost for analysis agents, not game execution.

## Decision 2026-05-30-003: Evidence Class
Problem: Archive creation can prove file presence and counts but cannot prove compile, Unity import, runtime GC, profiler, or frame-time status.
Solution: Label all claims as STATIC_FILESYSTEM/STATIC_DOC. Verification JSON records compileOrUnityRun as NOT RUN.
Rejected Alternatives: Claiming runtime readiness from docs; launching dotnet/Unity without need; fabricating microsecond savings.
Scalability potential: Keeps future agents from mistaking archive hygiene for runtime proof.
Hardware Impact: 0 runtime us. Avoided unnecessary build contention on shared machine.

## Decision 2026-05-30-004: Batch14 Selection Filter
Problem: Source folders still contain Batch013 leftovers because previous archive was copy-only. Copying every stale file into Batch014 would pollute the handoff and inflate summaries.
Solution: Select files changed after `Docs/Archive/Batch013` creation or files whose relative path explicitly matches `14`, `1400-1427`, or `ARCHIVE_14`. Preserve raw selected files and summarize only selected files.
Rejected Alternatives: Copy all historical leftovers; delete old 13 files; rely only on filename prefix and miss active unnumbered audit files.
Scalability potential: Reduces future model context load while preserving Batch014 evidence.
Hardware Impact: 0 runtime us. Tooling byte reduction only.

## Decision 2026-05-30-005: PowerShell Compatibility Fix
Problem: First copy attempt used `[System.IO.Path]::GetRelativePath`, unavailable in this host runtime.
Solution: Replace with a local substring-based `Get-RelPath` after resolving source and base paths. No destructive cleanup was needed because the failed pass only created empty archive directories.
Rejected Alternatives: Switch to Python; delete/recreate archive root; stop after first tooling fault.
Scalability potential: Script stays compatible with older Windows PowerShell hosts.
Hardware Impact: 0 runtime us. Avoided repeated failed passes.
