# Codex Token Usage Ledger

Date: 2026-05-27 20:42 Europe/Samara
Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT / NOT PROJECT ENGINEERING AUTHORITY

This file is the local token accounting surface archived out of active project docs. The detailed current report is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.md`; machine-readable data is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.json`.

## Current Total

Scope: current `C:\Users\danat\.codex\sessions`, current `C:\Users\danat\.codex\archived_sessions`, and backup `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850`.

Accounting rule: parse JSONL `session_meta`/`token_count`, take the final per-session `payload.info.total_token_usage`, dedupe by `session_meta.id`, and keep the highest final `total_tokens` for duplicate records. Day/week/month stats in the dated report use positive in-session deltas.

| Metric | Value |
|---|---:|
| unique_session_or_path_keys | 2,827 |
| sessions_with_usage | 2,801 |
| sessions_without_usage | 26 |
| duplicate_records_removed | 107 |
| files_missing_session_id | 2 |
| First selected timestamp UTC | 2026-04-03T17:11:28.591000+00:00 |
| Last selected timestamp UTC | 2026-05-27T16:44:10.389000+00:00 |
| input_tokens | 107,399,653,529 |
| cached_input_tokens | 103,189,309,056 |
| output_tokens | 372,985,934 |
| reasoning_output_tokens | 117,263,392 |
| total_tokens | 107,773,673,063 |
| Uncached input tokens | 4,210,344,473 |
| Cached-input ratio | 96.079741% |

## Change Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-26.json`.
Previous generated Samara: `2026-05-26T21:33:27.098408+04:00`.
Elapsed hours: 23.15

| Metric | Delta |
|---|---:|
| file_count | 57 |
| sessions_with_usage | 52 |
| input_tokens | 5,326,723,870 |
| cached_input_tokens | 5,109,255,552 |
| output_tokens | 17,161,106 |
| reasoning_output_tokens | 4,970,890 |
| total_tokens | 5,343,884,976 |
| GPT-5.5 standard API-equivalent $ | $4,156.80 |
| GPT-5.5 priority API-equivalent $ | $10,392.01 |
| gpt-5.3-codex standard comparison $ | $1,514.94 |
| top model-effort tokens | 5,298,527,476 |
| top model-effort sessions | 37 |
| top model-effort standard $ | $4,117.74 |
| tokens / primary code line | 1,670.63 |
| tokens / 1k primary code chars | 39,454.91 |

`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.

## API-Equivalent Cost Snapshot

Local Codex telemetry is not an invoice. The primary estimate uses official `gpt-5.5` standard short-context API-equivalent rates checked on 2026-05-27: input $5.00/1M, cached input $0.50/1M, output $30.00/1M. `xhigh` is a reasoning-effort setting; it changes observed token shape, not the public rate row.

| Scenario | Total | No-cache upper bound |
|---|---:|---:|
| gpt-5.5 standard short-context API-equivalent | $83,835.95 | $548,187.85 |
| gpt-5.5_priority_short_context_equivalent | $209,589.89 | $1,370,469.61 |
| gpt-5.5_batch_short_context_equivalent | $41,917.98 | $274,093.92 |
| gpt-5.5_flex_short_context_equivalent | $41,917.98 | $274,093.92 |
| gpt-5.4_standard_short_context_equivalent | $41,917.98 | $274,093.92 |
| gpt-5.3-codex_standard_api_equivalent | $30,648.03 | $193,171.20 |
| gpt-5.3-codex_priority_api_equivalent | $61,296.07 | $386,342.39 |

## Model Attribution

Exact model labels are available only where JSONL contains structured `turn_context` model fields. Unknown sessions are not guessed in the main total.

| Model | Sessions | Total tokens | Standard cost if rate known |
|---|---:|---:|---:|
| gpt-5.5 | 2,556 | 96,106,599,306 | $74,374.38 |
| gpt-5.4 | 232 | 11,564,030,598 | $4,680.91 |
| gpt-5.2-codex | 3 | 85,512,992 | unpriced |
| gpt-5.1-codex-mini | 3 | 13,472,930 | unpriced |
| gpt-5.4-mini | 1 | 2,818,965 | $0.39 |
| gpt-5.3-codex | 3 | 1,096,113 | $0.63 |
| gpt-5.2 | 3 | 142,159 | unpriced |

Model-specific cost bounds:

- Known model standard cost only: $79,056.31
- Unpriced known-model tokens: 99,128,081
- Known + unpriced as gpt-5.3-codex standard: $79,091.69
- Known + unpriced as gpt-5.5 standard: $79,151.74

## Interpretive Snapshot

| Metric | Value |
|---|---:|
| active_days | 55.0000 |
| mean_tokens_per_active_day | 1,959,521,328.4182 |
| median_tokens_per_active_day | 1,099,097,709.0000 |
| session_gini_total_tokens | 0.7830 |
| top_1_percent_sessions_share | 17.9593% |
| top_10_percent_sessions_share | 62.1037% |
| equivalent_full_258400_context_windows | 417,080.7781 |
| tokens_per_primary_code_character | 1,341.4092 |
| tokens_per_primary_code_non_ws_character | 1,920.0998 |
| tokens_per_primary_code_alphanumeric_character | 2,179.0435 |
| xhigh_final_sessions_share | 68.5469% |
| xhigh_final_tokens_share | 90.7715% |
| gpt_5_5_standard_xhigh_final_cost_usd | $75,514.28 |
| gpt_5_5_standard_cost_per_xhigh_final_session_usd | $39.33 |
| reasoning_tokens_per_1m_xhigh_final_tokens | 1,060.1602 |
| gpt_5_5_standard_cache_discount_saved_usd | $464,351.89 |
| gpt_5_5_standard_cost_per_1k_primary_loc_usd | $44.91 |
| gpt_5_3_codex_standard_cache_discount_saved_usd | $162,523.16 |
| gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd | $16.42 |
| priced_model_effort_final_standard_cost_usd | $79,056.31 |
| unpriced_model_effort_final_tokens | 99,128,081.0000 |
| unpriced_model_effort_final_tokens_share | 0.0920% |
| top_model_effort_final_tokens_share | 88.1558% |
| top_model_effort_final_cost_usd | $73,174.62 |
| gpt_5_5_xhigh_exact_final_tokens | 95,008,760,975.0000 |
| gpt_5_5_xhigh_exact_final_tokens_share | 88.1558% |
| gpt_5_5_xhigh_exact_sessions | 1,817.0000 |
| gpt_5_5_xhigh_exact_standard_cost_usd | $73,174.62 |
| gpt_5_5_xhigh_exact_cache_savings_usd | $409,881.97 |
| gpt_5_5_xhigh_exact_cost_per_session_usd | $40.27 |
| gpt_5_5_xhigh_exact_reasoning_tokens_per_1m | 1,022.8748 |
| observed_model_high_bound_cost_per_1k_primary_loc_usd | $42.40 |

## Input Output Snapshot

| Metric | Value |
|---|---:|
| input_to_output_ratio | 28794.5587% |
| uncached_input_to_output_ratio | 1128.8212% |
| cached_input_to_output_ratio | 27665.7374% |
| output_to_total_tokens_ratio | 0.3461% |
| reasoning_to_output_ratio | 31.4391% |
| paid_input_to_all_input_ratio | 3.9203% |
| cached_input_to_uncached_input_ratio | 2450.8519% |
| gpt_5_5_standard_input_side_cost_usd | $72,646.38 |
| gpt_5_5_standard_output_side_cost_usd | $11,189.58 |
| gpt_5_5_standard_output_cost_share | 13.3470% |
| gpt_5_5_standard_effective_usd_per_1m_output_tokens | $224.77 |
| gpt_5_5_standard_reasoning_output_cost_usd | $3,517.90 |

## Code Density Snapshot

| Scope | Lines | Characters | Tokens / line | Tokens / 1k chars | Output tokens / 1k chars | GPT-5.5 $ / 1k lines | GPT-5.5 $ / 1k chars |
|---|---:|---:|---:|---:|---:|---:|---:|
| first_party_assets_project_cs | 1,866,854 | 80,343,622 | 57,730.10 | 1,341,409.19 | 4,642.38 | $44.91 | $1.04 |
| first_party_scripts_cs | 1,836,779 | 79,006,458 | 58,675.36 | 1,364,112.20 | 4,720.96 | $45.64 | $1.06 |
| all_repo_cs_excluding_generated | 3,020,479 | 125,824,576 | 35,680.99 | 856,539.13 | 2,964.33 | $27.76 | $0.67 |
| all_repo_source_broad | 3,363,667 | 141,173,460 | 32,040.53 | 763,413.13 | 2,642.04 | $24.92 | $0.59 |
| docs_markdown_text | 252,912 | 25,150,193 | 426,131.12 | 4,285,202.63 | 14,830.34 | $331.48 | $3.33 |

## Chat Concentration Snapshot

| Group | Key | Total tokens | Output tokens |
|---|---|---:|---:|
| cwd | `c:\hades` | 89,999,127,843 | 308,545,148 |
| source | `vscode` | 104,172,815,521 | 355,227,606 |
| cli | `0.131.0-alpha.9` | 60,160,053,623 | 212,888,160 |

## Root Breakdown

| Root | JSONL files | Files with usage | Selected sessions with usage | Selected total tokens |
|---|---:|---:|---:|---:|
| backup_cleanup_20260521_194850 | 1,048 | 1,029 | 1,001 | 57,856,335,910 |
| current_archived_sessions | 1 | 1 | 1 | 157,103 |
| current_sessions | 1,885 | 1,878 | 1,799 | 49,917,180,050 |

## Evidence Boundary

Evidence class: static local filesystem telemetry. This is not billing-provider proof, Unity runtime proof, or profiler proof.
