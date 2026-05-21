# COMPUTE LAST 6H PROMPT TOKEN AUDIT

Status: AUDIT COMPLETE
Scope: HECTON-8 `.codex` session JSONL only. Timaert excluded.
Invoice status: NOT AN INVOICE. This is local telemetry accounting.

## Token Window

Window: 2026-05-16T09:55:54+04:00 to 2026-05-16T15:55:54+04:00.

Method: scan `.codex\sessions` JSONL files modified near the window, parse `event_msg.token_count`, use cumulative `total_token_usage` deltas with a pre-window baseline, and fall back to `last_token_usage` only when no baseline exists.

| Metric | Value |
|---|---:|
| JSONL files scanned | 47 |
| JSONL bytes scanned | 335,002,125 |
| Token rows seen | 26,348 |
| Token rows inside window | 8,388 |
| Negative cumulative deltas | 0 |
| No-baseline fallback rows | 2 |
| Parse errors | 0 |
| Model bucket | `gpt-5.5` |

## Last 6H Tokens

| Metric | Value |
|---|---:|
| Total tokens | 757,394,868 |
| Input tokens | 754,722,691 |
| Cached input tokens | 721,504,896 |
| Non-cached input tokens | 33,217,795 |
| Output tokens | 2,672,177 |
| Reasoning output tokens | 857,278 |
| Cached-input ratio | 95.599% |
| Output/input ratio | 0.354% |

## Last 6H Rates

| Metric | Value |
|---|---:|
| Tokens/sec | 35,064.58 |
| Tokens/min | 2,103,874.63 |
| Tokens/hour | 126,232,478.00 |
| Tokens/day equivalent | 3,029,579,472.00 |
| Peak minute | 15,133,220 tokens at 2026-05-16T10:13+04:00 |
| Peak hour | 239,443,502 tokens at 2026-05-16T10:00-11:00+04:00 |

## Last 6H Cost

Rates used for `gpt-5.5`: input USD 5.00/M, cached input USD 0.50/M, output USD 30.00/M.

| Metric | Cache-aware | No-cache equivalent |
|---|---:|---:|
| 6-hour cost | USD 607.01 | USD 3,853.78 |
| USD/min | USD 1.69 | USD 10.70 |
| USD/hour | USD 101.17 | USD 642.30 |
| USD/day equivalent | USD 2,428.03 | USD 15,415.12 |

Cache avoided about USD 3,246.77 during this 6-hour window.

## Prompt Cadence Window

Prompt cadence was measured separately from the same recent JSONL surface.
Window: 2026-05-16T09:51:24+04:00 to 2026-05-16T15:51:24+04:00.

| Metric | `event_msg.user_message` | `response_item role:user` |
|---|---:|---:|
| Rows | 146 | 155 |
| Average rows/sec | 0.00676 | 0.00718 |
| Average rows/min | 0.4056 | 0.4306 |
| Average rows/hour | 24.33 | 25.83 |
| Day equivalent | 584.0 | 620.0 |
| Peak second | 6 rows at 2026-05-16T14:24:52+04:00 | 6 rows at 2026-05-16T14:24:52+04:00 |
| Peak minute | 15 rows at 2026-05-16T14:24+04:00 | 15 rows at 2026-05-16T14:24+04:00 |
| Peak hour | 99 rows at 2026-05-16T14:00-15:00+04:00 | 107 rows at 2026-05-16T14:00-15:00+04:00 |

## Token Per Prompt Proxy

This is a workflow-amplification proxy, not a claim about a single human prompt.

| Denominator | Tokens per row | Cache-aware USD per row | No-cache USD per row |
|---|---:|---:|---:|
| 146 `event_msg.user_message` rows | 5,187,636.08 | USD 4.16 | USD 26.40 |
| 155 `response_item role:user` rows | 4,886,418.50 | USD 3.92 | USD 24.86 |

## Verdict

The six-hour average is lower than the 14:57 60-second live pulse: 35.1K tokens/sec vs 93.2K tokens/sec. That means the live pulse was a burst, not the six-hour baseline.

The six-hour window still burned 757.4M tokens and USD 607.01 cache-aware. The cache ratio stayed high at 95.599%, but the no-cache equivalent was USD 3,853.78.

STATUS: AUDIT COMPLETE.
