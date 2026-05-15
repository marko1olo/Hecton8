# COMPUTE CORRECTED ROLLING RATES

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T17:18:23+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `.codex` JSONL positive deltas + `state_5.sqlite` path-or-UUID model matching

## Boundary

This supersedes rolling-window cost rows from `COMPUTE_TOKEN_BURN_RATE_LEDGER.md`.

Reason: the earlier rolling ledger used exact path matching and therefore priced several fresh windows through an `unknown` proxy. This pass uses the corrected path-or-UUID model attribution from `COMPUTE_MODEL_BUCKET_RECONCILIATION.md`.

It is still not an invoice. It is local telemetry plus public API pricing/proxy rates.

## Ledger

| Metric | Value |
|---|---:|
| Session files scanned | 766 |
| SQLite threads | 766 |
| SQLite `threads.tokens_used` | 45,758,254,570 |
| Parsed token-count rows | 368,469 |
| Parse errors | 0 |
| Final sessions matched by path | 736 |
| Final sessions matched by UUID fallback | 12 |
| First token timestamp UTC | 2026-04-03T17:10:35.129Z |
| Last token timestamp UTC | 2026-05-15T13:17:53.238Z |
| JSONL final total tokens | 45,771,499,116 |
| Positive-delta token flow | 45,761,631,790 |
| Input tokens | 45,615,641,500 |
| Cached input tokens | 43,793,799,808 |
| Output tokens | 155,599,216 |
| Reasoning output tokens | 53,751,246 |
| Cached-input ratio | 96.00610% |

## Corrected Cost

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided |
|---|---:|---:|---:|
| Model-aware corrected | USD 30,704.36 | USD 201,983.02 | USD 171,278.65 |
| All tokens as GPT-5.5 standard | USD 35,674.08 | USD 232,746.18 | USD 197,072.10 |
| All tokens as GPT-5.5 long-context | USD 69,014.18 | USD 463,158.38 | USD 394,144.20 |

## Corrected Rolling Windows

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equiv | Cache-aware cost | USD/min | USD/hour | USD/day equiv | No-cache cost |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Last 1h | 195,974,142 | 54,437.26 | 3,266,235.70 | 195,974,142.00 | 4,703,379,408.00 | USD 150.02 | USD 2.50 | USD 150.02 | USD 3,600.49 | USD 995.36 |
| Last 6h | 845,618,668 | 39,149.01 | 2,348,940.74 | 140,936,444.67 | 3,382,474,672.00 | USD 647.33 | USD 1.80 | USD 107.89 | USD 2,589.32 | USD 4,294.95 |
| Last 24h | 3,398,780,549 | 39,337.74 | 2,360,264.27 | 141,615,856.21 | 3,398,780,549.00 | USD 2,601.80 | USD 1.81 | USD 108.41 | USD 2,601.80 | USD 17,262.61 |
| Last 7d | 20,296,429,548 | 33,558.91 | 2,013,534.68 | 120,812,080.64 | 2,899,489,935.43 | USD 15,537.13 | USD 1.54 | USD 92.48 | USD 2,219.59 | USD 103,086.78 |
| Last 14d | 29,912,591,279 | 24,729.32 | 1,483,759.49 | 89,025,569.28 | 2,136,613,662.79 | USD 22,898.41 | USD 1.14 | USD 68.15 | USD 1,635.60 | USD 151,927.84 |
| Last 30d | 42,821,381,540 | 16,520.59 | 991,235.68 | 59,474,141.03 | 1,427,379,384.67 | USD 29,550.86 | USD 0.68 | USD 41.04 | USD 985.03 | USD 194,834.23 |
| Whole observed | 45,761,631,790 | 12,659.39 | 759,563.17 | 45,573,790.44 | 1,093,770,970.49 | USD 30,696.37 | USD 0.51 | USD 30.57 | USD 733.69 | USD 201,930.34 |

Direct corrected answer for latest rolling day: 3.399B tokens, USD 2,601.80 cache-aware, USD 17,262.61 no-cache equivalent, USD 1.81/min average.

## Recent UTC Days

| Day UTC | Tokens | Cache-aware cost | No-cache cost | Cache avoided | Dominant model |
|---|---:|---:|---:|---:|---|
| 2026-05-06 | 1,713,970,292 | USD 1,312.06 | USD 8,705.36 | USD 7,393.29 | `gpt-5.5` |
| 2026-05-07 | 2,196,054,409 | USD 1,681.10 | USD 11,153.89 | USD 9,472.79 | `gpt-5.5` |
| 2026-05-08 | 3,187,620,273 | USD 2,440.16 | USD 16,190.11 | USD 13,749.96 | `gpt-5.5` |
| 2026-05-09 | 2,352,879,610 | USD 1,801.15 | USD 11,950.42 | USD 10,149.26 | `gpt-5.5` |
| 2026-05-10 | 1,692,316,233 | USD 1,295.49 | USD 8,595.38 | USD 7,299.89 | `gpt-5.5` |
| 2026-05-11 | 2,608,576,016 | USD 1,996.89 | USD 13,249.11 | USD 11,252.22 | `gpt-5.5` |
| 2026-05-12 | 2,414,507,314 | USD 1,848.33 | USD 12,263.43 | USD 10,415.10 | `gpt-5.5` |
| 2026-05-13 | 3,948,640,246 | USD 3,022.73 | USD 20,055.38 | USD 17,032.65 | `gpt-5.5` |
| 2026-05-14 | 2,692,719,493 | USD 2,061.31 | USD 13,676.48 | USD 11,615.18 | `gpt-5.5` |
| 2026-05-15 partial | 1,515,260,685 | USD 1,159.95 | USD 7,696.10 | USD 6,536.15 | `gpt-5.5` |

## Peak Hours

| Hour UTC | Tokens | Cache-aware cost | No-cache cost |
|---|---:|---:|---:|
| 2026-05-14T23:00Z | 402,358,823 | USD 308.01 | USD 2,043.60 |
| 2026-05-15T00:00Z | 390,152,610 | USD 298.67 | USD 1,981.61 |
| 2026-05-14T21:00Z | 385,893,919 | USD 295.41 | USD 1,959.98 |
| 2026-05-09T01:00Z | 355,172,758 | USD 271.89 | USD 1,803.94 |
| 2026-05-14T20:00Z | 355,050,557 | USD 271.79 | USD 1,803.32 |

## Verdict

The prior rolling-day cost was under-attributed. Corrected path-or-UUID matching puts the latest 24h entirely in `gpt-5.5`: 3.399B tokens and USD 2.60k cache-aware.

Cache still prevents the bill from becoming grotesque: same 24h no-cache equivalent is USD 17.26k.

STATUS: AUDIT COMPLETE.
