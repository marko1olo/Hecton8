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

