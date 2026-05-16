# LOG_COMPUTE_LOGISTICS_AUDITOR

## 2026-05-16 Compute Continuation

What was wrong: The previous compute audit was stale by about 1.685B JSONL final tokens. The user redirected away from Timaert back to HECTON-8 token/cost accounting.

What was done:

- Re-read `AGENTS.md`.
- Re-scanned first-party script LOC.
- Re-scanned `Docs/AgentLogs`, `Docs/Tasks`, and `Docs/Reports`.
- Re-scanned `.codex` SQLite and full JSONL session ledger.
- Recomputed cache-aware cost, no-cache equivalent, rolling rates, prompt cadence, tokens/LOC, tokens/code-byte, and energy equivalents.
- Wrote `COMPUTE_AUDIT_BRIEF.md`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`.
- Appended the 2026-05-16 addendum to `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Not applicable. The useful saving is analytical: SQLite-only cost estimation was rejected because it would lose cache/output split; full JSONL scan avoided a fake cost model.

Key numbers:

- JSONL final tokens: 47,456,271,437.
- Cached input ratio: 96.017%.
- Cache-aware estimate: USD 32,007.67.
- No-cache equivalent: USD 210,561.57.
- Last 24h: 3,240,421,310 tokens; USD 2,525.79 cache-aware.
- Meaningful script LOC: 809,871.
- Tokens per meaningful LOC: 58,597.32.
- Energy: 2,372.81 MWh.

STATUS: AUDIT COMPLETE.

