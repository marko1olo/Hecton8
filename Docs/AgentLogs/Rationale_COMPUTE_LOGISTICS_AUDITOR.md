# Rationale_COMPUTE_LOGISTICS_AUDITOR

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00

## Decision Ledger

Problem: Prior compute numbers were from 2026-05-15 and `.codex` is live.
Solution: Treat previous reports as historical snapshots and create a new 2026-05-16 bundle.
Rejected Alternatives: Overwriting old reports would erase drift evidence.
Scalability potential: Cheap machine needs small static scans and SQLite summaries; high-end machine can run full 8.49GB JSONL pass.
Hardware Impact: Full JSONL scan took minutes but did not touch Unity runtime or force asset import.

Problem: SQLite gives fast `tokens_used` but not reliable input/cached/output billing split.
Solution: Use SQLite for live tail and model/cwd split, JSONL final usage for input/cache/output and rolling positive deltas.
Rejected Alternatives: Estimating cost from SQLite-only totals would hide cache and output ratio.
Scalability potential: Future pass can cache per-file JSONL fingerprints to avoid full rescans.
Hardware Impact: CPU/disk heavy once; no MX350/Unity frame impact.

Problem: "tokens per byte of code" can be misread.
Solution: Report both source text proxy (~0.25 tokens/byte at bytes/4) and historical burn per script byte (1,100.57 tokens/byte).
Rejected Alternatives: A single ratio would mix tokenizer density with workflow amplification.
Scalability potential: Keeps future audits honest when LOC changes.
Hardware Impact: None.

Problem: Energy conversion was easy to mislabel by 1000x.
Solution: Formula fixed explicitly: `tokens / 1000 * 0.05 kWh`, then convert kWh to MWh/GWh.
Rejected Alternatives: Carrying forward a raw MWh label without unit check.
Scalability potential: Prevents future report drift.
Hardware Impact: None.

Problem: User asked to continue after the full JSONL scan, but re-running 8.49GB JSONL immediately would waste time for only tail drift.
Solution: Use a 30-second SQLite live-tail sample to measure current active burn, then document it as SQLite-only evidence.
Rejected Alternatives: Pretending the full 03:56 JSONL snapshot remained current; re-running full JSONL for every short prompt.
Scalability potential: Cheap devices use SQLite tail checks; expensive offline audits can later rerun JSONL.
Hardware Impact: 30-second wait plus two SQLite reads; no Unity runtime impact.

Problem: `logs_2.sqlite` is 3.57GB plus WAL and full grouping timed out.
Solution: Record DB metadata and a latest-5,000-row sample instead of pretending to have complete grouped evidence.
Rejected Alternatives: Running unindexed global grouping until it blocks the agent; treating log rows as token/billing rows.
Scalability potential: Future offline job can export/index logs by thread and target.
Hardware Impact: Metadata query was acceptable; full grouping is too expensive for interactive loop.

Problem: The 03:56 JSONL scan and 05:19 live tail were stale while other HECTON agents kept running.
Solution: Run another bounded 60-second SQLite tail and re-scan current first-party LOC, then append a continuation rebase instead of overwriting the original snapshot.
Rejected Alternatives: Re-running the full 8.49GB JSONL pass for every user nudge; pretending the old snapshot is still current.
Scalability potential: SQLite tail is cheap enough for repeated live accounting; full JSONL remains the slower invoice-model pass.
Hardware Impact: 60-second wait plus read-only SQLite queries and a source LOC scan; no Unity import or runtime cost.

Problem: The log DB continuation query initially assumed a `timestamp` column.
Solution: Inspect `pragma table_info(logs)` and use the actual `ts`/`ts_nanos` indexed order.
Rejected Alternatives: Forcing a broken schema assumption or treating the failed query as evidence.
Scalability potential: Correct schema use keeps future tail samples cheap and reproducible.
Hardware Impact: Indexed latest-5,000-row query completed interactively.

Problem: SQLite live totals show burn but cannot prove input/cache/output split for the last six hours.
Solution: Parse recent JSONL `event_msg.token_count` rows, use cumulative deltas with pre-window baselines, and price the actual `gpt-5.5` input/cache/output split.
Rejected Alternatives: Extending the 60-second blended SQLite pulse to a six-hour invoice estimate; scanning the entire 8.49GB session ledger again.
Scalability potential: Recent-file JSONL pass gives a middle tier between quick SQLite tail and full historical rescan.
Hardware Impact: Read about 335MB of JSONL; no Unity runtime impact.

Problem: User requested H-Phi in addition to token counting, but prior compute reports correctly said H-Phi/token correlation was not proven.
Solution: Run the authoritative `Tools/Architecture/HectonPhiAudit.ps1` static scan, then parse historical H-Phi artifacts with UTF-8/UTF-16 autodetection and join them to `.codex` token deltas by timestamp.
Rejected Alternatives: Inventing a new H-Phi formula; treating UTF-16 H-Phi JSON artifacts as invalid; claiming causation from sparse artifact timestamps.
Scalability potential: The H-Phi current scan is one static pass; the timeseries extractor can be rerun without Unity import or build.
Hardware Impact: H-Phi scan took about 57 seconds; token timeseries scan read JSONL only and did not touch Unity runtime.

