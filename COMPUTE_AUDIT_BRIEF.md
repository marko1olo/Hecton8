# COMPUTE AUDIT BRIEF

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Scope: HECTON-8 only. Timaert ignored.
Evidence: local filesystem, `.codex` SQLite, `.codex` JSONL, static LOC scanner, official OpenAI pricing page.
Invoice status: NOT AN INVOICE. This is local telemetry accounting.

## Current Hard Numbers

| Metric | Value |
|---|---:|
| `Assets/_Project/Scripts/**/*.cs` files | 1,538 |
| Script physical LOC | 985,864 |
| Script meaningful LOC | 809,871 |
| Logic density | 82.15% |
| All `Assets/**/*.cs` physical LOC | 1,543,550 |
| `Packages/**/*.cs` physical LOC | 140,868 |
| `.codex` JSONL files | 809 |
| `.codex` JSONL bytes | 8,493,635,444 |
| JSONL files with final usage | 791 |
| JSONL final total tokens | 47,456,271,437 |
| JSONL input tokens | 47,294,226,243 |
| JSONL cached input tokens | 45,410,520,576 |
| JSONL output tokens | 161,786,794 |
| JSONL reasoning output tokens | 55,602,954 |
| Cached-input ratio | 96.017% |
| SQLite thread tokens, 03:56 local | 47,465,726,066 |
| JSONL vs SQLite drift | ~9.45M tokens live tail |
| Model-aware cache-aware estimate | USD 32,007.67 |
| Model-aware no-cache equivalent | USD 210,561.57 |
| Cache avoided | USD 178,553.90 |
| Long-context surcharge scenario | USD 32,015.49 |
| Last 24h tokens | 3,240,421,310 |
| Last 24h cache-aware cost | USD 2,525.79 |
| Last 24h no-cache equivalent | USD 16,500.45 |
| Last 24h average | 37,504.88 tokens/sec |
| Tokens per meaningful LOC | 58,597.32 |
| Historical burn per script byte | 1,100.57 tokens/byte |
| Script text proxy tokens | ~10.78M tokens at bytes/4 |
| Energy at 0.05 kWh / 1K tokens | 2,372.81 MWh |
| Energy in common units | 2.373 GWh; 2,372,814 kWh; 79,094 home-days at 30 kWh/day |

## Current Verdict

The old "1.63M LOC" claim is still not meaningful first-party logic. Current first-party script surface is 809,871 meaningful LOC. The broader all-Assets C# physical count is 1.544M LOC and includes non-first-party/vendor surface.

The economic anomaly is not raw output volume. It is repeated long-context recursion: 47.456B local ledger tokens against 809,871 meaningful script LOC, or 58.6K tokens burned per meaningful line.

Cache is carrying the bill. At current model-aware public-price assumptions, cache reduces the local estimate from USD 210.56K to USD 32.01K. That is an 84.8% avoided-cost effect. It is still not clean engineering economics.

## Canonical Files

- Detailed 2026-05-16 ledger: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`
- Index: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`
- Historical long report with addendum: `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`

