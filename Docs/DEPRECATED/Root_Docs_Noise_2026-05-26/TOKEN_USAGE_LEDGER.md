# Codex Token Usage Ledger

Date: 2026-05-27 23:03 Europe/Samara
Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT / NOT PROJECT ENGINEERING AUTHORITY

This file is the local token accounting surface archived out of active project docs. The detailed current report is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.md`; machine-readable data is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.json`.

## Current Total

Scope: current `C:\Users\danat\.codex\sessions`, current `C:\Users\danat\.codex\archived_sessions`, and backup `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850`.

Accounting rule: parse JSONL `session_meta`/`token_count`, take the final per-session `payload.info.total_token_usage`, dedupe by `session_meta.id`, and keep the highest final `total_tokens` for duplicate records. Day/week/month stats in the dated report use positive in-session deltas.

| Metric | Value |
|---|---:|
| unique_session_or_path_keys | 2,830 |
| sessions_with_usage | 2,804 |
| sessions_without_usage | 26 |
| duplicate_records_removed | 110 |
| files_missing_session_id | 2 |
| First selected timestamp UTC | 2026-04-03T17:11:28.591000+00:00 |
| Last selected timestamp UTC | 2026-05-27T19:04:43.056000+00:00 |
| input_tokens | 107,868,828,212 |
| cached_input_tokens | 103,642,537,600 |
| output_tokens | 374,525,731 |
| reasoning_output_tokens | 117,698,876 |
| total_tokens | 108,244,387,543 |
| Uncached input tokens | 4,226,290,612 |
| Cached-input ratio | 96.082009% |

## Change Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-26.json`.
Previous generated Samara: `2026-05-26T21:33:27.098408+04:00`.
Elapsed hours: 25.50

| Metric | Delta |
|---|---:|
| file_count | 63 |
| sessions_with_usage | 55 |
| input_tokens | 5,795,898,553 |
| cached_input_tokens | 5,562,484,096 |
| output_tokens | 18,700,903 |
| reasoning_output_tokens | 5,406,374 |
| total_tokens | 5,814,599,456 |
| GPT-5.5 standard API-equivalent $ | $4,509.34 |
| GPT-5.5 priority API-equivalent $ | $11,273.35 |
| gpt-5.3-codex standard comparison $ | $1,643.72 |
| top model-effort tokens | 5,769,241,956 |
| top model-effort sessions | 40 |
| top model-effort standard $ | $4,470.28 |
| tokens / primary code line | 1,850.47 |
| tokens / 1k primary code chars | 43,678.53 |

## Velocity Since Previous Snapshot
Speed and burn-rate are derived from previous-snapshot deltas. Code ratios use net primary C# code growth in the same window.

| Metric | Value |
|---|---:|
| Total tokens / hour | 228,009,054.25 |
| Total tokens / minute | 3,800,150.90 |
| Total tokens / second | 63,335.85 |
| Total tokens / day pace | 5,472,217,302.10 |
| Input tokens / hour | 227,275,732.00 |
| Cached input tokens / hour | 218,122,804.10 |
| Uncached input tokens / hour | 9,152,927.90 |
| Output tokens / hour | 733,322.26 |
| Reasoning output tokens / hour | 212,001.23 |
| Usage sessions / hour | 2.16 |
| JSONL files / hour | 2.47 |
| Primary C# code lines / hour | 1,647.82 |
| Primary C# code lines / day pace | 39,547.61 |
| Primary C# code chars / hour | 69,304.51 |
| Primary C# code chars / day pace | 1,663,308.21 |
| Tokens / net primary C# code line | 138,370.36 |
| Input tokens / net primary C# code line | 137,925.34 |
| Output tokens / net primary C# code line | 445.03 |
| Reasoning tokens / net primary C# code line | 128.66 |
| Tokens / 1k net primary C# code chars | 3,289,959.90 |
| Output tokens / 1k net primary C# code chars | 10,581.16 |
| GPT-5.5 standard $ / hour | $176.83 |
| GPT-5.5 standard $ / day pace | $4,243.82 |
| GPT-5.5 priority $ / hour | $442.06 |
| gpt-5.3-codex standard $ / hour | $64.46 |
| GPT-5.5 standard $ / net primary C# code line | $0.11 |
| GPT-5.5 standard $ / 1k net primary C# code chars | $2.55 |

`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.

## API-Equivalent Cost Snapshot

Local Codex telemetry is not an invoice. The primary estimate uses official `gpt-5.5` standard short-context API-equivalent rates checked on 2026-05-27: input $5.00/1M, cached input $0.50/1M, output $30.00/1M. `xhigh` is a reasoning-effort setting; it changes observed token shape, not the public rate row.

| Scenario | Total | No-cache upper bound |
|---|---:|---:|
| gpt-5.5 standard short-context API-equivalent | $84,188.49 | $550,579.91 |
| gpt-5.5_priority_short_context_equivalent | $210,471.23 | $1,376,449.78 |
| gpt-5.5_batch_short_context_equivalent | $42,094.25 | $275,289.96 |
| gpt-5.5_flex_short_context_equivalent | $42,094.25 | $275,289.96 |
| gpt-5.4_standard_short_context_equivalent | $42,094.25 | $275,289.96 |
| gpt-5.3-codex_standard_api_equivalent | $30,776.81 | $194,013.81 |
| gpt-5.3-codex_priority_api_equivalent | $61,553.63 | $388,027.62 |

## Model Attribution

Exact model labels are available only where JSONL contains structured `turn_context` model fields. Unknown sessions are not guessed in the main total.

| Model | Sessions | Total tokens | Standard cost if rate known |
|---|---:|---:|---:|
| gpt-5.5 | 2,559 | 96,577,313,786 | $74,726.92 |
| gpt-5.4 | 232 | 11,564,030,598 | $4,680.91 |
| gpt-5.2-codex | 3 | 85,512,992 | unpriced |
| gpt-5.1-codex-mini | 3 | 13,472,930 | unpriced |
| gpt-5.4-mini | 1 | 2,818,965 | $0.39 |
| gpt-5.3-codex | 3 | 1,096,113 | $0.63 |
| gpt-5.2 | 3 | 142,159 | unpriced |

Model-specific cost bounds:

- Known model standard cost only: $79,408.84
- Unpriced known-model tokens: 99,128,081
- Known + unpriced as gpt-5.3-codex standard: $79,444.23
- Known + unpriced as gpt-5.5 standard: $79,504.28

## Interpretive Snapshot

| Metric | Value |
|---|---:|
| active_days | 55.0000 |
| mean_tokens_per_active_day | 1,968,079,773.5091 |
| median_tokens_per_active_day | 1,099,097,709.0000 |
| session_gini_total_tokens | 0.7834 |
| top_1_percent_sessions_share | 17.9738% |
| top_10_percent_sessions_share | 62.1379% |
| equivalent_full_258400_context_windows | 418,902.4286 |
| tokens_per_primary_code_character | 1,345.6328 |
| tokens_per_primary_code_non_ws_character | 1,926.2965 |
| tokens_per_primary_code_alphanumeric_character | 2,186.0617 |
| xhigh_final_sessions_share | 68.5806% |
| xhigh_final_tokens_share | 90.8116% |
| gpt_5_5_standard_xhigh_final_cost_usd | $75,866.82 |
| gpt_5_5_standard_cost_per_xhigh_final_session_usd | $39.45 |
| reasoning_tokens_per_1m_xhigh_final_tokens | 1,059.5137 |
| gpt_5_5_standard_cache_discount_saved_usd | $466,391.42 |
| gpt_5_5_standard_cost_per_1k_primary_loc_usd | $45.04 |
| gpt_5_3_codex_standard_cache_discount_saved_usd | $163,237.00 |
| gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd | $16.47 |
| priced_model_effort_final_standard_cost_usd | $79,408.84 |
| unpriced_model_effort_final_tokens | 99,128,081.0000 |
| unpriced_model_effort_final_tokens_share | 0.0916% |
| top_model_effort_final_tokens_share | 88.2073% |
| top_model_effort_final_cost_usd | $73,527.16 |
| gpt_5_5_xhigh_exact_final_tokens | 95,479,475,455.0000 |
| gpt_5_5_xhigh_exact_final_tokens_share | 88.2073% |
| gpt_5_5_xhigh_exact_sessions | 1,820.0000 |
| gpt_5_5_xhigh_exact_standard_cost_usd | $73,527.16 |
| gpt_5_5_xhigh_exact_cache_savings_usd | $411,921.50 |
| gpt_5_5_xhigh_exact_cost_per_session_usd | $40.40 |
| gpt_5_5_xhigh_exact_reasoning_tokens_per_1m | 1,022.3930 |
| observed_model_high_bound_cost_per_1k_primary_loc_usd | $42.53 |

## Input Output Snapshot

| Metric | Value |
|---|---:|
| input_to_output_ratio | 28801.4465% |
| uncached_input_to_output_ratio | 1128.4380% |
| cached_input_to_output_ratio | 27673.0086% |
| output_to_total_tokens_ratio | 0.3460% |
| reasoning_to_output_ratio | 31.4261% |
| paid_input_to_all_input_ratio | 3.9180% |
| cached_input_to_uncached_input_ratio | 2452.3287% |
| gpt_5_5_standard_input_side_cost_usd | $72,952.72 |
| gpt_5_5_standard_output_side_cost_usd | $11,235.77 |
| gpt_5_5_standard_output_cost_share | 13.3460% |
| gpt_5_5_standard_effective_usd_per_1m_output_tokens | $224.79 |
| gpt_5_5_standard_reasoning_output_cost_usd | $3,530.97 |

## Code Density Snapshot

| Scope | Lines | Characters | Tokens / line | Tokens / 1k chars | Output tokens / 1k chars | GPT-5.5 $ / 1k lines | GPT-5.5 $ / 1k chars |
|---|---:|---:|---:|---:|---:|---:|---:|
| first_party_assets_project_cs | 1,869,185 | 80,441,252 | 57,909.94 | 1,345,632.80 | 4,655.89 | $45.04 | $1.05 |
| first_party_scripts_cs | 1,838,970 | 79,093,073 | 58,861.42 | 1,368,569.76 | 4,735.25 | $45.78 | $1.06 |
| all_repo_cs_excluding_generated | 3,035,728 | 126,465,126 | 35,656.81 | 855,922.82 | 2,961.49 | $27.73 | $0.67 |
| all_repo_source_broad | 3,379,636 | 141,859,653 | 32,028.42 | 763,038.58 | 2,640.11 | $24.91 | $0.59 |
| docs_markdown_text | 261,405 | 26,175,545 | 414,086.91 | 4,135,325.07 | 14,308.23 | $322.06 | $3.22 |

## Chat Concentration Snapshot

| Group | Key | Total tokens | Output tokens |
|---|---|---:|---:|
| cwd | `c:\hades` | 90,469,842,323 | 310,084,945 |
| source | `vscode` | 104,643,530,001 | 356,767,403 |
| cli | `0.131.0-alpha.9` | 60,630,768,103 | 214,427,957 |

## Root Breakdown

| Root | JSONL files | Files with usage | Selected sessions with usage | Selected total tokens |
|---|---:|---:|---:|---:|
| backup_cleanup_20260521_194850 | 1,048 | 1,029 | 1,001 | 57,856,335,910 |
| current_archived_sessions | 1 | 1 | 1 | 157,103 |
| current_sessions | 1,891 | 1,884 | 1,802 | 50,387,894,530 |

## Evidence Boundary

Evidence class: static local filesystem telemetry. This is not billing-provider proof, Unity runtime proof, or profiler proof.
