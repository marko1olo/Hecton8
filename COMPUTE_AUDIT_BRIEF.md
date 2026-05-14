# COMPUTE AUDIT BRIEF

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T03:02:21+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Full report: `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`
Status/Rationale/Log:
- `Docs/Tasks/Status_COMPUTE_LOGISTICS_AUDITOR.md`
- `Docs/AgentLogs/Rationale_COMPUTE_LOGISTICS_AUDITOR.md`
- `Docs/AgentLogs/LOG_COMPUTE_LOGISTICS_AUDITOR.md`

## Hard Numbers

| Metric | Value |
|---|---:|
| Script files, `Assets/_Project/Scripts/**/*.cs` | 1,501 |
| Script physical LOC | 946,341 |
| Script meaningful LOC | 775,435 |
| Script logic density | 81.94% |
| All `Assets/**/*.cs` physical LOC | 1,581,522 |
| Latest JSONL final tokens | 43,778,987,916 |
| Input tokens | 43,630,634,851 |
| Cached input tokens | 41,886,807,040 |
| Output tokens | 148,094,665 |
| Cached-input ratio | 96.0032% |
| Cache-aware current API estimate | USD 29,135.37 |
| No-cache equivalent | USD 191,832.08 |
| Cache avoided | USD 162,696.72 |
| Whole-period average | 12,291.36 tokens/sec |
| Whole-period minute rate | 737,481.59 tokens/min |
| Whole-period hour rate | 44,248,895.33 tokens/hour |
| Whole-period day rate | 1,061,973,487.91 tokens/day |
| Last 6h rate | 61,645.10 tokens/sec |
| Tokens per meaningful LOC | 56,457.33 |
| Tokens per script source byte | 1,055.433 |
| Cache-aware cost per meaningful LOC | USD 0.037573 |
| Energy by prompt constant | 2,188.95 MWh |

## Evidence Rules

- Evidence classes used: FILESYSTEM, STATIC_DOC, SQLITE, JSONL, WEB_OFFICIAL, CALC.
- `.codex` is live. Old token snapshots are still valid captures, not eternal truth.
- JSONL `last_token_usage` events repeat. Do not sum every event. Use final `total_token_usage` per session or positive deltas.
- Reasoning output is treated as part of output, not charged twice.
- This is not an OpenAI invoice. It is local ledger accounting plus official public pricing.
- H-Phi/token correlation is NOT PROVEN. There is no valid join key from token burn to H-Phi delta.
- "Compute Thief" convictions are NOT PROVEN. High-burn `.codex` threads need diff, LOC delta, compile result, and H-Phi attribution.

## Current Verdict

The codebase is not 1.63M meaningful first-party LOC. It is 775,435 meaningful script LOC and 1.58M physical all-Assets C# LOC.

The economic anomaly is context recursion: 43.78B total tokens against 775,435 meaningful LOC equals 56,457 tokens per meaningful line. Cache pricing makes the bill survivable; it does not make the workflow clean.

Next audit target: top 100 `.codex` threads. They hold about half of the recorded token mass. Broad scanning below that is low-yield.
