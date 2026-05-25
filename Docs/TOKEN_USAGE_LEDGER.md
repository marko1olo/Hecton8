# Codex Token Usage Ledger

Date: 2026-05-25 07:51 Europe/Samara
Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT

This file is the stable token accounting surface. The detailed current report is `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.md`; machine-readable data is `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.json`.

## Current Total

Scope: current `C:\Users\danat\.codex\sessions`, current `C:\Users\danat\.codex\archived_sessions`, and backup `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850`.

Accounting rule: parse JSONL `session_meta`/`token_count`, take the final per-session `payload.info.total_token_usage`, dedupe by `session_meta.id`, and keep the highest final `total_tokens` for duplicate records. Day/week/month stats in the dated report use positive in-session deltas.

| Metric | Value |
|---|---:|
| unique_session_or_path_keys | 2,648 |
| sessions_with_usage | 2,622 |
| sessions_without_usage | 26 |
| duplicate_records_removed | 97 |
| files_missing_session_id | 2 |
| First selected timestamp UTC | 2026-04-03T17:11:28.591000+00:00 |
| Last selected timestamp UTC | 2026-05-25T03:53:08.200000+00:00 |
| input_tokens | 95,520,333,024 |
| cached_input_tokens | 91,768,379,008 |
| output_tokens | 332,176,227 |
| reasoning_output_tokens | 105,571,322 |
| total_tokens | 95,853,026,051 |
| Uncached input tokens | 3,751,954,016 |
| Cached-input ratio | 96.072089% |

`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.

## API-Equivalent Cost Snapshot

Local Codex telemetry is not an invoice. The primary estimate uses official `gpt-5.3-codex` standard API-equivalent rates current on 2026-05-25: input $1.75/1M, cached input $0.175/1M, output $14/1M.

| Scenario | Total | No-cache upper bound |
|---|---:|---:|
| gpt-5.3-codex standard API-equivalent | $27,275.85 | $171,811.05 |
| gpt-5.3-codex_priority_api_equivalent | $54,551.71 | $343,622.10 |
| gpt-5.4_standard_short_context_equivalent | $37,304.62 | $243,783.48 |
| gpt-5.5_standard_short_context_equivalent | $74,609.25 | $487,566.95 |

## Model Attribution

Exact model labels are available only where JSONL contains structured `turn_context` model fields. Unknown sessions are not guessed in the main total.

| Model | Sessions | Total tokens | Standard cost if rate known |
|---|---:|---:|---:|
| gpt-5.5 | 2,377 | 84,185,952,294 | $65,147.67 |
| gpt-5.4 | 232 | 11,564,030,598 | $4,680.91 |
| gpt-5.2-codex | 3 | 85,512,992 | unpriced |
| gpt-5.1-codex-mini | 3 | 13,472,930 | unpriced |
| gpt-5.4-mini | 1 | 2,818,965 | $0.39 |
| gpt-5.3-codex | 3 | 1,096,113 | $0.63 |
| gpt-5.2 | 3 | 142,159 | unpriced |

Model-specific cost bounds:

- Known model standard cost only: $69,829.60
- Unpriced known-model tokens: 99,128,081
- Known + unpriced as gpt-5.3-codex standard: $69,864.98
- Known + unpriced as gpt-5.5 standard: $69,925.03

## Interpretive Snapshot

| Metric | Value |
|---|---:|
| active_days | 53.0000 |
| mean_tokens_per_active_day | 1,808,547,661.3396 |
| median_tokens_per_active_day | 753,420,707.0000 |
| session_gini_total_tokens | 0.7797 |
| top_1_percent_sessions_share | 17.9399% |
| top_10_percent_sessions_share | 61.6215% |
| equivalent_full_258400_context_windows | 370,948.2432 |
| gpt_5_3_codex_standard_cache_discount_saved_usd | $144,535.20 |
| gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd | $15.43 |

## Root Breakdown

| Root | JSONL files | Files with usage | Selected sessions with usage | Selected total tokens |
|---|---:|---:|---:|---:|
| backup_cleanup_20260521_194850 | 1,048 | 1,029 | 1,001 | 57,856,335,910 |
| current_archived_sessions | 1 | 1 | 1 | 157,103 |
| current_sessions | 1,696 | 1,689 | 1,620 | 37,996,533,038 |

## Evidence Boundary

Evidence class: static local filesystem telemetry. This is not billing-provider proof, Unity runtime proof, or profiler proof.
