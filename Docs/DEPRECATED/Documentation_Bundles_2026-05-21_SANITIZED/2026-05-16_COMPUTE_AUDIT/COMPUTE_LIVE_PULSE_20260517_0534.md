# COMPUTE LIVE PULSE 2026-05-17 05:34

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: SQLite live pulse + arithmetic from latest JSONL price ratios.
Invoice status: NOT AN INVOICE.

No H-Phi scan was run in this pass. Latest valid H-Phi artifact remains `COMPUTE_HPHI_CURRENT_20260517_040910.json` from 04:12.

## SQLite Pulse

Source: `C:\Users\danat\.codex\state_5.sqlite`.

| Metric | Value |
|---|---:|
| Start | 2026-05-17T05:34:08+04:00 |
| End | 2026-05-17T05:34:38+04:00 |
| Duration | 30.009129 sec |
| Start total tokens | 50,951,931,900 |
| End total tokens | 50,953,580,001 |
| Delta tokens | 1,648,101 |
| Tokens/sec | 54,919.99 |
| Tokens/min | 3,295,199.27 |
| Tokens/hour | 197,711,956.25 |
| Tokens/day equivalent | 4,745,086,950.04 |
| Active delta threads | 5 |

The pulse is hotter than 04:46 (24,895.30 tokens/sec) and cooler than the post-04:12 JSONL window average (106,127.83 tokens/sec).

## Cost Range

SQLite does not expose input/cache/output split. Cost is therefore a range:

- Low estimate: historical full-ledger blended cache-aware rate, USD 0.765893/M tokens.
- Hot estimate: latest post-04:12 JSONL blended cache-aware rate, USD 0.910227/M tokens.
- No-cache scenario: latest post-04:12 JSONL no-cache blended rate, USD 5.078341/M tokens.

| Metric | Low cache-aware | Hot cache-aware | No-cache scenario |
|---|---:|---:|---:|
| Sample cost | USD 1.26 | USD 1.50 | USD 8.37 |
| USD/min | USD 2.52 | USD 3.00 | USD 16.73 |
| USD/hour | USD 151.43 | USD 179.96 | USD 1,004.05 |
| USD/day equivalent | USD 3,634.23 | USD 4,319.11 | USD 24,097.17 |

Estimated current cache-aware total:

| Method | Value |
|---|---:|
| 04:45 total + historical full-ledger blended delta | USD 34,686.23 |
| 04:45 total + latest post-04:12 blended delta | USD 34,732.01 |

## Energy

Formula remains the prompt constant: `tokens / 1000 * 0.05 kWh`.

| Metric | Value |
|---|---:|
| Sample energy | 82.41 kWh |
| Current cumulative energy | 2,547.68 MWh |
| Day-equivalent energy at pulse rate | 237.25 MWh/day |

## Code Ratios

Using the latest first-party code denominator from the 04:46 scan: 839,069 meaningful LOC and 44,746,126 script bytes.

| Metric | Value |
|---|---:|
| Tokens / meaningful LOC | 60,726.33 |
| Tokens / script byte | 1,138.73 |

## Active Burners

| Rank | Thread title | Model | Delta tokens |
|---:|---|---|---:|
| 1 | Enforce DataVault statelessness | `gpt-5.5` | 460,086 |
| 2 | CONTENT_AUTHORITY_DICTATOR prompt thread | `gpt-5.5` | 404,169 |
| 3 | Move reports to batch006 | `gpt-5.5` | 328,033 |
| 4 | Build ballast PID | `gpt-5.5` | 284,567 |
| 5 | Improve bot memory and CRM | `gpt-5.5` | 171,246 |

## Model Split At 05:34

| Model | Threads | Tokens |
|---|---:|---:|
| `gpt-5.5` | 536 | 39,069,384,855 |
| `gpt-5.4` | 244 | 11,591,437,853 |
| `gpt-5.4-mini` | 25 | 192,533,099 |
| `gpt-5.2-codex` | 3 | 85,512,992 |
| `gpt-5.1-codex-mini` | 3 | 13,472,930 |
| `gpt-5.3-codex` | 3 | 1,096,113 |
| `gpt-5.2` | 3 | 142,159 |

Verdict: burn is still active and materially concentrated. This sample does not prove waste, but it identifies current live burners and updates the cumulative token/code/energy denominator.

STATUS: AUDIT COMPLETE.
