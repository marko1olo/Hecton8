# COMPUTE MODEL BUCKET RECONCILIATION

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T16:39:22+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\sessions/**/*.jsonl` + `C:\Users\danat\.codex\state_5.sqlite`

## Boundary

This is a model-attribution correction pass, not a billing export.

Problem found: the earlier model-aware ledger matched JSONL files to SQLite only by exact `rollout_path`. Active/recent threads can have path drift or records that do not match exact path strings. That created a false `unknown` model bucket.

Correction used:
- First match JSONL file path to `threads.rollout_path`.
- If path does not match, extract UUID from `rollout-...UUID.jsonl`.
- Match UUID to `threads.id`.
- Use `threads.model`.
- Parse JSONL `token_count` rows for final `total_token_usage`.

## Result

| Metric | Value |
|---|---:|
| Session files scanned | 766 |
| SQLite threads | 766 |
| Parsed token-count rows | 366,921 |
| Parse errors | 0 |
| Final-usage sessions matched by exact path | 731 |
| Final-usage sessions matched by UUID fallback | 17 |
| Unmatched session files | 1 |
| Unmatched final-usage tokens | 0 |
| Files without final token usage | 18 |
| First token timestamp UTC | 2026-04-03T17:10:35.129Z |
| Last token timestamp UTC | 2026-05-15T12:39:16.847Z |

The previous `unknown` final-usage bucket is no longer valid as a model bucket. The only unmatched session has no token-count rows, so it contributes zero final usage.

## Corrected Totals

| Metric | Value |
|---|---:|
| JSONL final total tokens | 45,652,088,834 |
| SQLite `threads.tokens_used` | 45,644,663,325 |
| JSONL minus SQLite | 7,425,509 |
| JSONL/SQLite drift | 0.01627% |
| Input tokens | 45,496,680,026 |
| Cached input tokens | 43,678,873,216 |
| Non-cached input tokens | 1,817,806,810 |
| Output tokens | 155,150,408 |
| Reasoning output tokens | 53,615,041 |
| Cached-input ratio | 96.00453% |

## Corrected Cost

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided |
|---|---:|---:|---:|
| Model-aware corrected | USD 30,613.26 | USD 201,374.74 | USD 170,761.48 |
| All tokens as GPT-5.5 standard | USD 35,582.98 | USD 232,137.91 | USD 196,554.93 |
| All tokens as GPT-5.5 long-context | USD 68,838.71 | USD 461,948.57 | USD 393,109.86 |

Delta versus previous `COMPUTE_TOKEN_BURN_RATE_LEDGER.md` model-aware row: +USD 2,250.82 cache-aware. Causes:
- Live token growth since prior full JSONL pass.
- UUID fallback reclassified recent path-unmatched sessions to `gpt-5.5`.
- `unknown` proxy pricing was too cheap for those sessions.

## Corrected Model Split

| Model bucket | Sessions | Final tokens | Input | Cached input | Output | Cache ratio | Cost |
|---|---:|---:|---:|---:|---:|---:|---:|
| `gpt-5.5` | 476 | 33,766,761,807 | 33,659,739,377 | 32,367,221,248 | 106,764,030 | 96.160% | USD 25,849.12 |
| `gpt-5.4` | 237 | 11,592,726,837 | 11,546,273,790 | 11,051,701,632 | 46,453,047 | 95.717% | USD 4,696.15 |
| `gpt-5.4-mini` | 24 | 192,533,099 | 191,173,213 | 167,098,752 | 1,359,886 | 87.407% | USD 36.71 |
| `gpt-5.2` / Codex proxy | 6 | 85,655,151 | 85,186,629 | 79,843,840 | 468,522 | 93.728% | USD 29.88 |
| `gpt-5.1-codex-mini` | 2 | 13,315,827 | 13,218,484 | 12,128,000 | 97,343 | 91.750% | USD 0.77 |
| `gpt-5.3-codex` | 3 | 1,096,113 | 1,088,533 | 879,744 | 7,580 | 80.819% | USD 0.63 |

Corrected model share by final tokens:

| Model bucket | Share |
|---|---:|
| `gpt-5.5` | 73.965% |
| `gpt-5.4` | 25.394% |
| Other known buckets combined | 0.641% |
| `unknown` with final usage | 0.000% |

## Reconciliation Notes

The earlier `unknown` bucket was an attribution defect, not a real model class. Exact path matching was too brittle for active `.codex` sessions. UUID fallback is required for future scans.

The rolling window costs in `COMPUTE_TOKEN_BURN_RATE_LEDGER.md` remain valid as local window estimates at their original snapshot, but their model split should be treated as under-attributed where they used `unknown`. Future window scans must use path-or-UUID model matching.

STATUS: AUDIT COMPLETE.
