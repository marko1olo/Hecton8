# Codex Token Usage Ledger

Date: 2026-05-27 13:47 Europe/Samara
Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT / NOT PROJECT ENGINEERING AUTHORITY

This file is the local token accounting surface archived out of active project docs. The detailed current report is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.md`; machine-readable data is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.json`.

## Current Total

Scope: current `C:\Users\danat\.codex\sessions`, current `C:\Users\danat\.codex\archived_sessions`, and backup `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850`.

Accounting rule: parse JSONL `session_meta`/`token_count`, take the final per-session `payload.info.total_token_usage`, dedupe by `session_meta.id`, and keep the highest final `total_tokens` for duplicate records. Day/week/month stats in the dated report use positive in-session deltas.

| Metric | Value |
|---|---:|
| unique_session_or_path_keys | 2,814 |
| sessions_with_usage | 2,788 |
| sessions_without_usage | 26 |
| duplicate_records_removed | 107 |
| files_missing_session_id | 2 |
| First selected timestamp UTC | 2026-04-03T17:11:28.591000+00:00 |
| Last selected timestamp UTC | 2026-05-27T09:49:31.931000+00:00 |
| input_tokens | 105,501,553,097 |
| cached_input_tokens | 101,372,633,216 |
| output_tokens | 367,050,571 |
| reasoning_output_tokens | 115,519,808 |
| total_tokens | 105,869,637,268 |
| Uncached input tokens | 4,128,919,881 |
| Cached-input ratio | 96.086389% |

## Change Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-26.json`.
Previous generated Samara: `2026-05-26T21:33:27.098408+04:00`.
Elapsed hours: 16.24

| Metric | Delta |
|---|---:|
| file_count | 44 |
| sessions_with_usage | 39 |
| input_tokens | 3,428,623,438 |
| cached_input_tokens | 3,292,579,712 |
| output_tokens | 11,225,743 |
| reasoning_output_tokens | 3,227,306 |
| total_tokens | 3,439,849,181 |
| GPT-5.5 standard API-equivalent $ | $2,663.28 |
| GPT-5.5 priority API-equivalent $ | $6,658.20 |
| gpt-5.3-codex standard comparison $ | $971.44 |
| top model-effort tokens | 3,410,901,203 |
| top model-effort sessions | 30 |
| top model-effort standard $ | $2,638.44 |
| tokens / primary code line | 1,099.54 |
| tokens / 1k primary code chars | 26,096.69 |

`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.

## API-Equivalent Cost Snapshot

Local Codex telemetry is not an invoice. The primary estimate uses official `gpt-5.5` standard short-context API-equivalent rates checked on 2026-05-27: input $5.00/1M, cached input $0.50/1M, output $30.00/1M. `xhigh` is a reasoning-effort setting; it changes observed token shape, not the public rate row.

| Scenario | Total | No-cache upper bound |
|---|---:|---:|
| gpt-5.5 standard short-context API-equivalent | $82,342.43 | $538,519.28 |
| gpt-5.5_priority_short_context_equivalent | $205,856.08 | $1,346,298.21 |
| gpt-5.5_batch_short_context_equivalent | $41,171.22 | $269,259.64 |
| gpt-5.5_flex_short_context_equivalent | $41,171.22 | $269,259.64 |
| gpt-5.4_standard_short_context_equivalent | $41,171.22 | $269,259.64 |
| gpt-5.3-codex_standard_api_equivalent | $30,104.53 | $189,766.43 |
| gpt-5.3-codex_priority_api_equivalent | $60,209.06 | $379,532.85 |

## Model Attribution

Exact model labels are available only where JSONL contains structured `turn_context` model fields. Unknown sessions are not guessed in the main total.

| Model | Sessions | Total tokens | Standard cost if rate known |
|---|---:|---:|---:|
| gpt-5.5 | 2,543 | 94,202,563,511 | $72,880.85 |
| gpt-5.4 | 232 | 11,564,030,598 | $4,680.91 |
| gpt-5.2-codex | 3 | 85,512,992 | unpriced |
| gpt-5.1-codex-mini | 3 | 13,472,930 | unpriced |
| gpt-5.4-mini | 1 | 2,818,965 | $0.39 |
| gpt-5.3-codex | 3 | 1,096,113 | $0.63 |
| gpt-5.2 | 3 | 142,159 | unpriced |

Model-specific cost bounds:

- Known model standard cost only: $77,562.78
- Unpriced known-model tokens: 99,128,081
- Known + unpriced as gpt-5.3-codex standard: $77,598.17
- Known + unpriced as gpt-5.5 standard: $77,658.22

## Interpretive Snapshot

| Metric | Value |
|---|---:|
| active_days | 55.0000 |
| mean_tokens_per_active_day | 1,924,902,495.7818 |
| median_tokens_per_active_day | 1,099,097,709.0000 |
| session_gini_total_tokens | 0.7815 |
| top_1_percent_sessions_share | 17.7721% |
| top_10_percent_sessions_share | 61.7569% |
| equivalent_full_258400_context_windows | 409,712.2185 |
| tokens_per_primary_code_character | 1,328.0510 |
| tokens_per_primary_code_non_ws_character | 1,900.6028 |
| tokens_per_primary_code_alphanumeric_character | 2,157.0367 |
| xhigh_final_sessions_share | 68.6155% |
| xhigh_final_tokens_share | 90.6210% |
| gpt_5_5_standard_xhigh_final_cost_usd | $74,034.98 |
| gpt_5_5_standard_cost_per_xhigh_final_session_usd | $38.70 |
| reasoning_tokens_per_1m_xhigh_final_tokens | 1,062.9387 |
| gpt_5_5_standard_cache_discount_saved_usd | $456,176.85 |
| gpt_5_5_standard_cost_per_1k_primary_loc_usd | $44.46 |
| gpt_5_3_codex_standard_cache_discount_saved_usd | $159,661.90 |
| gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd | $16.25 |
| priced_model_effort_final_standard_cost_usd | $77,562.78 |
| unpriced_model_effort_final_tokens | 99,128,081.0000 |
| unpriced_model_effort_final_tokens_share | 0.0936% |
| top_model_effort_final_tokens_share | 87.9583% |
| top_model_effort_final_cost_usd | $71,695.32 |
| gpt_5_5_xhigh_exact_final_tokens | 93,121,134,702.0000 |
| gpt_5_5_xhigh_exact_final_tokens_share | 87.9583% |
| gpt_5_5_xhigh_exact_sessions | 1,810.0000 |
| gpt_5_5_xhigh_exact_standard_cost_usd | $71,695.32 |
| gpt_5_5_xhigh_exact_cache_savings_usd | $401,775.90 |
| gpt_5_5_xhigh_exact_cost_per_session_usd | $39.61 |
| gpt_5_5_xhigh_exact_reasoning_tokens_per_1m | 1,024.9817 |
| observed_model_high_bound_cost_per_1k_primary_loc_usd | $41.93 |

## Input Output Snapshot

| Metric | Value |
|---|---:|
| input_to_output_ratio | 28743.0565% |
| uncached_input_to_output_ratio | 1124.8913% |
| cached_input_to_output_ratio | 27618.1652% |
| output_to_total_tokens_ratio | 0.3467% |
| reasoning_to_output_ratio | 31.4725% |
| paid_input_to_all_input_ratio | 3.9136% |
| cached_input_to_uncached_input_ratio | 2455.1853% |
| gpt_5_5_standard_input_side_cost_usd | $71,330.92 |
| gpt_5_5_standard_output_side_cost_usd | $11,011.52 |
| gpt_5_5_standard_output_cost_share | 13.3728% |
| gpt_5_5_standard_effective_usd_per_1m_output_tokens | $224.34 |
| gpt_5_5_standard_reasoning_output_cost_usd | $3,465.59 |

## Code Density Snapshot

| Scope | Lines | Characters | Tokens / line | Tokens / 1k chars | Output tokens / 1k chars | GPT-5.5 $ / 1k lines | GPT-5.5 $ / 1k chars |
|---|---:|---:|---:|---:|---:|---:|---:|
| first_party_assets_project_cs | 1,852,195 | 79,718,053 | 57,159.01 | 1,328,050.97 | 4,604.36 | $44.46 | $1.03 |
| first_party_scripts_cs | 1,823,775 | 78,482,965 | 58,049.73 | 1,348,950.53 | 4,676.82 | $45.15 | $1.05 |
| all_repo_cs_excluding_generated | 2,907,175 | 121,125,558 | 36,416.67 | 874,048.71 | 3,030.33 | $28.32 | $0.68 |
| all_repo_source_broad | 3,249,321 | 136,412,713 | 32,582.08 | 776,098.03 | 2,690.74 | $25.34 | $0.60 |
| docs_markdown_text | 202,327 | 18,979,195 | 523,260.06 | 5,578,194.29 | 19,339.63 | $406.98 | $4.34 |

## Chat Concentration Snapshot

| Group | Key | Total tokens | Output tokens |
|---|---|---:|---:|
| cwd | `c:\hades` | 88,095,092,048 | 302,609,785 |
| source | `vscode` | 102,313,611,456 | 349,426,174 |
| cli | `0.131.0-alpha.9` | 58,256,017,828 | 206,952,797 |

## Root Breakdown

| Root | JSONL files | Files with usage | Selected sessions with usage | Selected total tokens |
|---|---:|---:|---:|---:|
| backup_cleanup_20260521_194850 | 1,048 | 1,029 | 1,001 | 57,856,335,910 |
| current_archived_sessions | 1 | 1 | 1 | 157,103 |
| current_sessions | 1,872 | 1,865 | 1,786 | 48,013,144,255 |

## Evidence Boundary

Evidence class: static local filesystem telemetry. This is not billing-provider proof, Unity runtime proof, or profiler proof.
