# Rationale_COMPUTE_LOGISTICS_AUDITOR

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

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

Problem: The improved H-Phi score could be misreported as strict baseline gate success.
Solution: Attempt the old-budget H-Phi gate explicitly, then document the timeout and use current scan counters for an inferred budget verdict.
Rejected Alternatives: Pretending a timed-out command produced a valid `EXIT=0`; ignoring raw counter regressions because composite H-Phi improved.
Scalability potential: Future gate runs should write intermediate JSON before budget checks or use cached scan data.
Hardware Impact: Strict gate attempt ran for 244 seconds and timed out; no Unity runtime impact.

Problem: A full all-history JSONL usage scan was interrupted and therefore could not be used as evidence.
Solution: Run a bounded recent-file JSONL pass over files modified in the last 30 hours, then compute timestamp-windowed 1h/6h/24h usage, cache-aware cost, no-cache equivalent, prompt cadence, and long-context event count.
Rejected Alternatives: Claiming partial output from the interrupted all-history scan; rerunning another full 8+ GB pass interactively while other agents were active.
Scalability potential: Cheap pass reads about 991MB and gives current rate truth; high-end/offline pass can still run full-history reconciliation later.
Hardware Impact: Read-only JSONL scan took about 35 seconds and did not touch Unity runtime.

Problem: SQLite totals were current but did not include line/byte denominator drift from concurrent source edits.
Solution: Pair the 00:52 SQLite live pulse with a fresh static first-party script LOC/byte scan and report tokens per meaningful LOC, physical LOC, and script byte.
Rejected Alternatives: Reusing the 23:59 LOC denominator after source changed; treating SQLite token total as invoice-grade despite missing cache/output split.
Scalability potential: Repeated static LOC scans are cheap enough for ongoing accounting without forcing Unity import.
Hardware Impact: Read-only source scan took seconds; no runtime frame impact.

Problem: Aggregate live burn hides which concurrent threads are consuming the current token rate.
Solution: Run a short per-thread SQLite delta and list live burners by positive token increase.
Rejected Alternatives: Calling a thread a compute thief without joining LOC/H-Phi/value deltas; using end-token totals as if they were current-rate deltas.
Scalability potential: This method is cheap enough to repeat and can later be joined against changed files and H-Phi artifacts.
Hardware Impact: 20-second wait plus two read-only SQLite scans; no Unity runtime impact.

Problem: The latest H-Phi truth had moved after the 17:18 artifact, while token totals continued to burn.
Solution: Run a fresh `HectonPhiAudit.ps1 -Summary -Json` scan without strict budget args, validate the artifact shape, compare scores/counters against 17:18, and compute a timestamp-windowed JSONL token/cost slice between the two H-Phi artifacts.
Rejected Alternatives: Reusing stale H-Phi numbers; rerunning strict budget gate after the prior 244-second timeout; treating score improvement as budget compliance.
Scalability potential: Summary-only H-Phi scans are enough for trend measurement; strict gates should be reserved for offline or cached-scan runs.
Hardware Impact: H-Phi scan took 157,042 ms. JSONL window pass read 543,939,025 bytes. No Unity runtime/import/build was touched.

Problem: Marginal H-Phi efficiency can look good if only the old baseline jump is shown.
Solution: Calculate marginal tokens and USD per H-Phi delta for the 17:18 to 02:17 interval.
Rejected Alternatives: Reporting only cumulative correlation; hiding that +0.001 Runtime H-Phi risk now costs about 3.20B tokens in this interval.
Scalability potential: Future rebase sections can show whether architecture hygiene gains are becoming more or less token-efficient.
Hardware Impact: Arithmetic only after scans.

Problem: Cumulative H-Phi ROI and marginal H-Phi ROI tell different stories.
Solution: Add a cumulative baseline-to-current ROI table and explicitly state that cumulative ROI is better because the first DataVault migration jump was cheaper.
Rejected Alternatives: Presenting only the latest expensive interval; presenting only the cumulative average and hiding worsening marginal cost.
Scalability potential: Future audit can plot marginal H-Phi cost over time and detect diminishing returns.
Hardware Impact: Arithmetic only.

Problem: Token burn spiked again after the H-Phi scan and could not be represented by the 02:14 live pulse.
Solution: Run another short per-thread SQLite delta at 03:04 and record the top live burners plus current total at 03:15.
Rejected Alternatives: Treating the cooler 02:14 pulse as current; declaring burner threads waste without value/LOC/H-Phi joins.
Scalability potential: Short SQLite pulses provide cheap burn telemetry while full JSONL scans remain reserved for billing-split windows.
Hardware Impact: 20-second wait plus read-only SQLite queries; no Unity runtime impact.

Problem: H-Phi moved again after 02:17, but the score movement looked tiny relative to the ongoing token burn.
Solution: Run another summary-only H-Phi scan at 04:12, compute the JSONL token/cost window from 02:17 to 04:12, and report marginal efficiency separately from cumulative ROI.
Rejected Alternatives: Assuming 02:17 H-Phi was still current; hiding the plateau by only reporting cumulative baseline efficiency.
Scalability potential: Repeated H-Phi rebases expose diminishing returns and keep future architecture work honest.
Hardware Impact: H-Phi scan took 170,338 ms. JSONL window pass read 561,363,073 bytes. No Unity runtime/import/build was touched.

Problem: The latest H-Phi delta includes both useful ownership movement and fresh managed debt.
Solution: Record both sides: +4 DataVault refs and -20 owner-blocked NativeArray refs, but also +2 managed format surface and +2 PrimaryManagedRuntimeRisk.
Rejected Alternatives: Selling the tiny score increase as clean improvement.
Scalability potential: Shows that future score gates must include raw counter regressions, not only the aggregate H-Phi score.
Hardware Impact: Static accounting only.

Problem: The user asked to keep counting after 04:12, but a fresh H-Phi scan 25 minutes later would mostly re-spend the same 170-second static pass.
Solution: Treat 04:12 as the current H-Phi boundary, then run a bounded post-04:12 JSONL token window, current SQLite total, first-party LOC/byte scan, and 04:46 per-thread live burner sample.
Rejected Alternatives: Re-running `HectonPhiAudit.ps1` on every short continuation; inferring H-Phi movement from token movement; using the quiet 04:38 30-second SQLite pulse as if it represented the full post-04:12 interval.
Scalability potential: Cheap machines can repeat short SQLite and bounded JSONL windows; high-end/offline passes can run full JSONL and H-Phi scans less frequently.
Hardware Impact: Read-only JSONL/SQLite/source scans only. No Unity runtime, import, build, or gameplay frame impact.

Problem: The 04:38 SQLite pulse showed zero token delta while JSONL later showed a large 04:41 burst.
Solution: Record both facts with timestamps and explain the boundary: the zero 30-second pulse was quiet only for its exact window, not the whole post-H-Phi interval.
Rejected Alternatives: Calling the zero pulse proof of no burn; discarding it because a later burst existed.
Scalability potential: Future reports should always state exact windows when mixing SQLite instantaneous pulses and JSONL timestamp windows.
Hardware Impact: Arithmetic and timestamp reconciliation only.

Problem: The user asked to continue after the 04:46 rebase, but another full JSONL/H-Phi pass would be disproportionate for a short continuation.
Solution: Run a 30-second SQLite live pulse at 05:34, join it with the latest 04:46 code denominator, and price it as a range because SQLite lacks input/cache/output split.
Rejected Alternatives: Pretending SQLite can produce exact invoice split; rerunning H-Phi without evidence of a large source movement; publishing the full raw CONTENT_AUTHORITY XML prompt title into summary tables.
Scalability potential: Short pulse gives repeatable burn telemetry; full JSONL and H-Phi remain scheduled heavier passes.
Hardware Impact: 30-second wait plus read-only SQLite query. No Unity runtime, import, build, or scene impact.

Problem: By 11:38 the post-04:12 source movement was no longer small, so the old H-Phi score was stale.
Solution: Gate the expensive H-Phi scan on source drift first, then rerun summary H-Phi after confirming 113 modified C# files and 10.8MB touched since 04:12.
Rejected Alternatives: Continuing to quote the 04:12 H-Phi score after 16,979 runtime-line drift; rerunning H-Phi blindly every prompt without source movement proof.
Scalability potential: Source-drift gate prevents waste on quiet intervals and still catches real architecture movement.
Hardware Impact: H-Phi scan took 181,218 ms. Source and JSONL scans were read-only. No Unity runtime/import/build was touched.

Problem: The 11:42 H-Phi improvement included both native ownership gains and managed runtime regressions.
Solution: Report composite score deltas together with raw counter deltas, especially -162 owner-blocked NativeArray refs and +20 PrimaryManagedRuntimeRisk.
Rejected Alternatives: Calling the score lift a clean win; calling the managed debt a total failure while DataVault/ownership counters improved.
Scalability potential: Keeps H-Phi from becoming metric theater; future gates can weight raw debt counters separately.
Hardware Impact: Static accounting only.

Problem: H-Phi / ash-fi was scattered across reports under inconsistent spellings, making future search fragile.
Solution: Create a dedicated `COMPUTE_HPHI_SEARCH_INDEX_20260517.md` and add a standard keyword alias line to active H-Phi compute documents.
Rejected Alternatives: Renaming historical files; relying on one spelling only; adding tags to every ancient unrelated Metric Phi report and generating noisy churn.
Scalability potential: Future agents can `rg "ash-fi|H-Phi|token-H-Phi-ROI"` and find the canonical compute audit trail.
Hardware Impact: Documentation-only update.

