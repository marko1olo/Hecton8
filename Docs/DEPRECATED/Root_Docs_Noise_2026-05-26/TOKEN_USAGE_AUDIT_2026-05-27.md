# TOKEN USAGE AUDIT 2026-05-27

Generated UTC: 2026-05-27T19:03:32.917743+00:00
Generated Samara: 2026-05-27T23:03:32.917743+04:00
Evidence class: STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Not billing-provider proof.

## Scope
- current_sessions: `C:\Users\danat\.codex\sessions` exists=True
- current_archived_sessions: `C:\Users\danat\.codex\archived_sessions` exists=True
- backup_cleanup_20260521_194850: `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850` exists=True

Accounting: all-time totals use final per-session `total_token_usage`, deduped by `session_meta.id`. Day/week/month stats use positive deltas between token_count snapshots inside selected sessions.

## Totals
| Metric | Value |
|---|---:|
| file_count | 2,940 |
| unique_session_or_path_keys | 2,830 |
| sessions_with_usage | 2,804 |
| sessions_without_usage | 26 |
| duplicate_records_removed | 110 |
| files_missing_session_id | 2 |
| parse_errors_first_pass | 0 |
| parse_errors_increment_pass | 0 |
| day_span | 55 |
| first_selected_timestamp_utc | 2026-04-03T17:11:28.591000+00:00 |
| last_selected_timestamp_utc | 2026-05-27T19:04:43.056000+00:00 |
| input_tokens | 107,868,828,212 |
| cached_input_tokens | 103,642,537,600 |
| output_tokens | 374,525,731 |
| reasoning_output_tokens | 117,698,876 |
| total_tokens | 108,244,387,543 |
| uncached_input_tokens | 4,226,290,612 |
| cache_ratio | 96.082009% |
| output_ratio | 0.346000% |
| reasoning_output_ratio_of_output | 31.426112% |

## Change Since Previous Snapshot
Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-26.json`
Previous generated Samara: `2026-05-26T21:33:27.098408+04:00`
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
| top model-effort key | `gpt-5.5::xhigh` -> `gpt-5.5::xhigh` |
| top model-effort tokens | 5,769,241,956 |
| top model-effort sessions | 40 |
| top model-effort standard $ | $4,470.28 |
| primary code lines | 42,022 |
| primary code characters | 1,767,377 |
| tokens / primary code line | 1,850.47 |
| tokens / 1k primary code chars | 43,678.53 |
| GPT-5.5 $ / 1k primary LOC | $1.43 |
| GPT-5.5 $ / 1k primary code chars | $0.03 |

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

## API-Equivalent Price Scenarios
Actual Codex billing cannot be proven from local JSONL. These are API-equivalent estimates using official OpenAI rates checked on 2026-05-27. Cached input is charged at cached-input rate; reasoning output is an output subcounter, not added twice.

| Scenario | Uncached input | Cached input | Output | Total | No-cache upper bound |
|---|---:|---:|---:|---:|---:|
| gpt-5.3-codex_standard_api_equivalent | $7,396.01 | $18,137.44 | $5,243.36 | $30,776.81 | $194,013.81 |
| gpt-5.3-codex_priority_api_equivalent | $14,792.02 | $36,274.89 | $10,486.72 | $61,553.63 | $388,027.62 |
| gpt-5.4_standard_short_context_equivalent | $10,565.73 | $25,910.63 | $5,617.89 | $42,094.25 | $275,289.96 |
| gpt-5.5_standard_short_context_equivalent | $21,131.45 | $51,821.27 | $11,235.77 | $84,188.49 | $550,579.91 |
| gpt-5.5_batch_short_context_equivalent | $10,565.73 | $25,910.63 | $5,617.89 | $42,094.25 | $275,289.96 |
| gpt-5.5_flex_short_context_equivalent | $10,565.73 | $25,910.63 | $5,617.89 | $42,094.25 | $275,289.96 |
| gpt-5.5_priority_short_context_equivalent | $52,828.63 | $129,553.17 | $28,089.43 | $210,471.23 | $1,376,449.78 |
| gpt-5.4_mini_standard_equivalent | $3,169.72 | $7,773.19 | $1,685.37 | $12,628.27 | $82,586.99 |

## Input Output Economics
This section separates prompt mass, cache leverage, visible output, and hidden reasoning output. Cost shares use the primary GPT-5.5 standard scenario.

| Metric | Value |
|---|---:|
| input_to_output_ratio | 28801.4465% |
| uncached_input_to_output_ratio | 1128.4380% |
| cached_input_to_output_ratio | 27673.0086% |
| output_to_total_tokens_ratio | 0.3460% |
| reasoning_to_output_ratio | 31.4261% |
| reasoning_to_total_tokens_ratio | 0.1087% |
| non_reasoning_output_tokens | 256,826,855 |
| non_reasoning_output_to_output_ratio | 68.5739% |
| paid_input_to_all_input_ratio | 3.9180% |
| cached_input_to_uncached_input_ratio | 2452.3287% |
| output_tokens_per_session | 133,568.3777 |
| input_tokens_per_session | 38,469,624.8973 |
| uncached_input_tokens_per_session | 1,507,236.3096 |
| reasoning_output_tokens_per_session | 41,975.3481 |
| gpt_5_5_standard_uncached_input_cost_share | 25.1002% |
| gpt_5_5_standard_cached_input_cost_share | 61.5539% |
| gpt_5_5_standard_output_cost_share | 13.3460% |
| gpt_5_5_standard_input_side_cost_usd | $72,952.72 |
| gpt_5_5_standard_output_side_cost_usd | $11,235.77 |
| gpt_5_5_standard_effective_usd_per_1m_total_tokens | 0 |
| gpt_5_5_standard_effective_usd_per_1m_output_tokens | 224 |
| gpt_5_5_standard_reasoning_output_cost_usd | $3,530.97 |
| gpt_5_5_standard_non_reasoning_output_cost_usd | $7,704.81 |
| gpt_5_5_standard_reasoning_output_cost_share | 4.1941% |
| top_output_day | 2026-05-21 |
| top_output_day_output_tokens | 40,439,316 |
| top_reasoning_day | 2026-05-21 |
| top_reasoning_day_reasoning_tokens | 12,217,203 |

## Model Forensics
Model evidence comes from structured `turn_context.payload.model` / `collaboration_mode.settings.model` fields when present. Sessions without that field are `unknown_model`; local JSONL still does not expose invoice SKU, priority mode, or contractual billing plan.

### Final Session Model Attribution
| Model | Sessions | Total tokens | Input | Cached input | Output | Reasoning output | Standard cost if rate known |
|---|---:|---:|---:|---:|---:|---:|---:|
| gpt-5.5 | 2,559 | 96,577,313,786 | 96,248,592,200 | 92,521,485,568 | 327,687,986 | 99,135,314 | $74,726.92 |
| gpt-5.4 | 232 | 11,564,030,598 | 11,517,784,890 | 11,025,437,696 | 46,245,708 | 18,360,012 | $4,680.91 |
| gpt-5.2-codex | 3 | 85,512,992 | 85,044,900 | 79,787,648 | 468,092 | 157,917 | unpriced |
| gpt-5.1-codex-mini | 3 | 13,472,930 | 13,374,833 | 12,237,952 | 98,097 | 32,307 | unpriced |
| gpt-5.4-mini | 1 | 2,818,965 | 2,801,127 | 2,652,800 | 17,838 | 12,029 | $0.39 |
| gpt-5.3-codex | 3 | 1,096,113 | 1,088,533 | 879,744 | 7,580 | 1,132 | $0.63 |
| gpt-5.2 | 3 | 142,159 | 141,729 | 56,192 | 430 | 165 | unpriced |

### Temporal Delta Model Attribution
This table assigns each token delta to the latest prior `turn_context` model in the same JSONL file. It is useful for trend analysis, but all-time totals above remain final-session authority.

| Model | Delta total | Delta input | Delta cached input | Delta output | Delta reasoning output |
|---|---:|---:|---:|---:|---:|
| gpt-5.5 | 95,247,607,213 | 94,924,861,585 | 91,251,557,504 | 322,745,628 | 97,333,216 |
| gpt-5.4 | 13,002,550,593 | 12,950,641,924 | 12,396,688,640 | 51,908,669 | 20,348,155 |
| gpt-5.2-codex | 31,468,079 | 31,351,204 | 28,951,680 | 116,875 | 50,048 |
| gpt-5.3-codex | 22,822,547 | 22,773,200 | 21,537,152 | 49,347 | 20,889 |
| gpt-5.4-mini | 5,851,626 | 5,821,586 | 5,453,824 | 30,040 | 15,652 |
| gpt-5.1-codex-mini | 995,678 | 991,379 | 583,168 | 4,299 | 2,368 |
| unknown_model | 570,802 | 566,451 | 506,496 | 4,351 | 1,697 |
| gpt-5.2 | 142,159 | 141,729 | 56,192 | 430 | 165 |

### Reasoning Effort Attribution
Effort cost uses gpt-5.5 standard short-context API-equivalent. `xhigh` is a cost driver, not a separate official price row.

| Effort | Sessions | Total tokens | Input | Cached input | Output | Reasoning output | GPT-5.5 standard $ | $ / session |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| xhigh | 1,923 | 98,298,473,431 | 97,961,681,986 | 94,225,404,928 | 335,757,845 | 104,148,577 | $75,866.82 | $39.45 |
| high | 251 | 8,592,719,908 | 8,559,979,733 | 8,183,280,256 | 32,740,175 | 11,910,695 | $6,957.34 | $27.72 |
| medium | 619 | 1,346,643,132 | 1,340,655,010 | 1,228,428,672 | 5,988,122 | 1,635,697 | $1,354.99 | $2.19 |
| low | 11 | 6,551,072 | 6,511,483 | 5,423,744 | 39,589 | 3,907 | $9.34 | $0.85 |

### Exact Model Plus Effort Final Cost Matrix
Final-session totals are the authoritative all-time local spend slice. `reasoning_effort` has no separate public multiplier; cost is produced by the model rate and observed input/cached/output tokens. Unknown model rows are left unpriced.

| Model | Effort | Sessions | Total | Share | Input / output | Paid input / output | Cached / output | Cache hit | Output / total | Reasoning / output | Output cost share | Input $/1M | Cached $/1M | Output $/1M | Standard model $ | Cache saved $ | $ / session |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| gpt-5.5 | xhigh | 1,820 | 95,479,475,455 | 88.2073% | 295.28 | 11.23 | 284.05 | 96.1978% | 0.3375% | 30.2917% | 13.1485% | $5.000 | $0.500 | $30.00 | $73,527.16 | $411,921.50 | $40.40 |
| gpt-5.4 | high | 124 | 8,347,373,581 | 7.7116% | 264.64 | 11.33 | 253.32 | 95.7202% | 0.3764% | 36.2339% | 14.0654% | $2.500 | $0.250 | $15.00 | $3,351.13 | $17,910.09 | $27.03 |
| gpt-5.4 | xhigh | 103 | 2,818,997,976 | 2.6043% | 207.82 | 8.76 | 199.06 | 95.7867% | 0.4789% | 48.3790% | 17.3098% | $2.500 | $0.250 | $15.00 | $1,169.83 | $6,046.41 | $11.36 |
| gpt-5.5 | medium | 605 | 846,154,750 | 0.7817% | 206.69 | 21.42 | 185.27 | 89.6363% | 0.4815% | 24.2828% | 13.0581% | $5.000 | $0.500 | $30.00 | $935.98 | $3,396.64 | $1.55 |
| gpt-5.4 | medium | 5 | 397,659,041 | 0.3674% | 299.65 | 13.79 | 285.87 | 95.3992% | 0.3326% | 33.4988% | 12.4035% | $2.500 | $0.250 | $15.00 | $159.95 | $850.73 | $31.99 |
| gpt-5.5 | high | 126 | 245,273,572 | 0.2266% | 185.29 | 15.77 | 169.52 | 91.4902% | 0.5368% | 39.8507% | 15.4958% | $5.000 | $0.500 | $30.00 | $254.90 | $1,004.39 | $2.02 |
| gpt-5.2-codex | medium | 3 | 85,512,992 | 0.0790% | 181.68 | 11.23 | 170.45 | 93.8183% | 0.5474% | 33.7363% | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced |
| gpt-5.1-codex-mini | medium | 3 | 13,472,930 | 0.0124% | 136.34 | 11.59 | 124.75 | 91.4998% | 0.7281% | 32.9337% | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced |
| gpt-5.5 | low | 8 | 6,410,009 | 0.0059% | 162.53 | 25.58 | 136.95 | 84.2623% | 0.6115% | 9.6740% | 13.2528% | $5.000 | $0.500 | $30.00 | $8.87 | $24.16 | $1.11 |
| gpt-5.4-mini | medium | 1 | 2,818,965 | 0.0026% | 157.03 | 8.32 | 148.72 | 94.7047% | 0.6328% | 67.4347% | 20.5572% | $0.750 | $0.075 | $4.50 | $0.39 | $1.79 | $0.39 |
| gpt-5.3-codex | medium | 2 | 1,024,454 | 0.0009% | 137.38 | 24.05 | 113.34 | 82.4973% | 0.7226% | 14.6157% | 18.4419% | $1.750 | $0.175 | $14.00 | $0.56 | $1.32 | $0.28 |
| gpt-5.2 | high | 1 | 72,755 | 0.0001% | 335.83 | 144.42 | 191.41 | 56.9955% | 0.2969% | 46.2963% | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced |
| gpt-5.3-codex | low | 1 | 71,659 | 0.0001% | 403.85 | 173.89 | 229.97 | 56.9430% | 0.2470% | 28.2486% | 3.9047% | $1.750 | $0.175 | $14.00 | $0.06 | $0.06 | $0.06 |
| gpt-5.2 | low | 2 | 69,404 | 0.0001% | 323.32 | 253.93 | 69.38 | 21.4597% | 0.3083% | 30.3738% | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced | unpriced |

### Model Plus Effort Delta Cost Matrix
This assigns each token delta to the latest prior `turn_context` model and effort in the same JSONL file. Use it for temporal trend shape, not as the all-time authority.

| Model | Effort | Delta events | Delta total | Share | Input / output | Paid input / output | Cached / output | Cache hit | Output / total | Reasoning / output | Output cost share | Standard model $ | Cache saved $ | $ / delta event |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| gpt-5.5 | xhigh | 611,217 | 94,160,353,853 | 86.9344% | 295.67 | 11.23 | 284.44 | 96.2013% | 0.3371% | 30.1954% | 13.1362% | $72,484.70 | $406,251.87 | $0.12 |
| gpt-5.4 | high | 63,032 | 9,901,523,177 | 9.1417% | 267.46 | 11.40 | 256.06 | 95.7374% | 0.3725% | 35.8855% | 13.9512% | $3,965.49 | $21,249.34 | $0.06 |
| gpt-5.4 | xhigh | 19,172 | 2,946,820,824 | 2.7207% | 204.86 | 8.65 | 196.21 | 95.7783% | 0.4858% | 48.6115% | 17.5081% | $1,226.40 | $6,319.58 | $0.06 |
| gpt-5.5 | medium | 7,614 | 835,569,779 | 0.7714% | 208.01 | 21.71 | 186.30 | 89.5652% | 0.4784% | 24.1859% | 12.9490% | $926.20 | $3,351.60 | $0.12 |
| gpt-5.5 | high | 2,031 | 245,273,572 | 0.2265% | 185.29 | 15.77 | 169.52 | 91.4902% | 0.5368% | 39.8507% | 15.4958% | $254.90 | $1,004.39 | $0.13 |
| gpt-5.4 | medium | 1,074 | 154,206,592 | 0.1424% | 215.60 | 13.57 | 202.03 | 93.7051% | 0.4617% | 21.6684% | 15.0847% | $70.79 | $323.62 | $0.07 |
| gpt-5.2-codex | medium | 206 | 31,468,079 | 0.0291% | 268.25 | 20.53 | 247.71 | 92.3463% | 0.3714% | 42.8218% | unpriced | unpriced | unpriced | unpriced |
| gpt-5.3-codex | high | 127 | 21,746,817 | 0.0201% | 476.72 | 18.88 | 457.84 | 96.0403% | 0.2093% | 44.5060% | 11.0100% | $5.79 | $32.83 | $0.05 |
| gpt-5.5 | low | 85 | 6,410,009 | 0.0059% | 162.53 | 25.58 | 136.95 | 84.2623% | 0.6115% | 9.6740% | 13.2528% | $8.87 | $24.16 | $0.10 |
| gpt-5.4-mini | medium | 55 | 5,851,626 | 0.0054% | 193.79 | 12.24 | 181.55 | 93.6828% | 0.5134% | 52.1039% | 16.4846% | $0.82 | $3.68 | $0.01 |
| gpt-5.3-codex | medium | 11 | 1,004,071 | 0.0009% | 274.24 | 94.84 | 179.40 | 65.4187% | 0.3633% | 15.8717% | 6.6239% | $0.77 | $1.03 | $0.07 |
| gpt-5.1-codex-mini | medium | 23 | 922,147 | 0.0009% | 239.21 | 89.93 | 149.27 | 62.4035% | 0.4163% | 53.3472% | unpriced | unpriced | unpriced | unpriced |
| unknown_model | unknown | 7 | 570,802 | 0.0005% | 130.19 | 13.78 | 116.41 | 89.4157% | 0.7623% | 39.0025% | unpriced | unpriced | unpriced | unpriced |
| gpt-5.1-codex-mini | high | 1 | 73,531 | 0.0001% | 158.85 | 136.87 | 21.98 | 13.8386% | 0.6256% | 69.5652% | unpriced | unpriced | unpriced | unpriced |
| gpt-5.2 | high | 2 | 72,755 | 0.0001% | 335.83 | 144.42 | 191.41 | 56.9955% | 0.2969% | 46.2963% | unpriced | unpriced | unpriced | unpriced |
| gpt-5.3-codex | low | 2 | 71,659 | 0.0001% | 403.85 | 173.89 | 229.97 | 56.9430% | 0.2470% | 28.2486% | 3.9047% | $0.06 | $0.06 | $0.03 |
| gpt-5.2 | low | 2 | 69,404 | 0.0001% | 323.32 | 253.93 | 69.38 | 21.4597% | 0.3083% | 30.3738% | unpriced | unpriced | unpriced | unpriced |

### Model-Specific Cost Bounds
| Bound | USD |
|---|---:|
| known_models_only_standard_usd | $79,408.84 |
| unpriced_known_model_total_tokens | 99,128,081 tokens |
| unpriced_as_gpt_5_3_codex_standard_usd | $35.39 |
| unpriced_as_gpt_5_5_standard_usd | $95.44 |
| known_plus_unpriced_as_gpt_5_3_codex_standard_usd | $79,444.23 |
| known_plus_unpriced_as_gpt_5_5_standard_usd | $79,504.28 |

## Interpretive Stats
These are derived diagnostics, not billing proof. They are useful for waste shape, concentration, and cache economics.

| Metric | Value |
|---|---:|
| active_days | 55.0000 |
| calendar_day_span | 55.0000 |
| mean_tokens_per_active_day | 1,968,079,773.5091 |
| median_tokens_per_active_day | 1,099,097,709.0000 |
| peak_day_tokens | 11,101,068,200.0000 |
| peak_day_vs_mean_active_day | 5.6406 |
| session_gini_total_tokens | 0.7834 |
| top_1_percent_sessions_share | 17.9738% |
| top_5_percent_sessions_share | 44.2522% |
| top_10_percent_sessions_share | 62.1379% |
| largest_session_share | 1.8049% |
| equivalent_full_258400_context_windows | 418,902.4286 |
| equivalent_full_270k_context_windows | 400,905.1390 |
| gpt_5_5_standard_cache_discount_saved_usd | $466,391.42 |
| gpt_5_5_standard_cost_per_primary_loc_usd | $0.05 |
| gpt_5_5_standard_cost_per_1k_primary_loc_usd | $45.04 |
| gpt_5_5_standard_cost_per_primary_code_character_usd | $0.00 |
| gpt_5_3_codex_standard_cache_discount_saved_usd | $163,237.00 |
| gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd | $16.47 |
| observed_model_high_bound_cost_per_1k_primary_loc_usd | $42.53 |
| tokens_per_primary_code_character | 1,345.6328 |
| tokens_per_primary_code_non_ws_character | 1,926.2965 |
| tokens_per_primary_code_alphanumeric_character | 2,186.0617 |
| tokens_per_dollar_gpt_5_5_standard | 1,285,738.4979 |
| tokens_per_dollar_gpt_5_3_codex_standard | 3,517,075.9217 |
| xhigh_final_sessions_share | 68.5806% |
| xhigh_final_tokens_share | 90.8116% |
| xhigh_delta_tokens_share | 89.6550% |
| gpt_5_5_standard_xhigh_final_cost_usd | $75,866.82 |
| gpt_5_5_standard_cost_per_xhigh_final_session_usd | $39.45 |
| reasoning_tokens_per_1m_xhigh_final_tokens | 1,059.5137 |
| output_tokens_per_1m_xhigh_final_tokens | 3,415.6974 |
| top_model_effort_final_tokens_share | 88.2073% |
| top_model_effort_final_cost_usd | $73,527.16 |
| priced_model_effort_final_standard_cost_usd | $79,408.84 |
| unpriced_model_effort_final_tokens | 99,128,081.0000 |
| unpriced_model_effort_final_tokens_share | 0.0916% |
| gpt_5_5_xhigh_exact_final_tokens | 95,479,475,455.0000 |
| gpt_5_5_xhigh_exact_final_tokens_share | 88.2073% |
| gpt_5_5_xhigh_exact_sessions | 1,820.0000 |
| gpt_5_5_xhigh_exact_standard_cost_usd | $73,527.16 |
| gpt_5_5_xhigh_exact_cache_savings_usd | $411,921.50 |
| gpt_5_5_xhigh_exact_cost_per_session_usd | $40.40 |
| gpt_5_5_xhigh_exact_reasoning_tokens_per_1m | 1,022.3930 |
| output_tokens_per_1m_total_tokens | 3,460.0014 |
| reasoning_tokens_per_1m_total_tokens | 1,087.3439 |

## Root Breakdown
| Root | JSONL files | Files with usage | Selected sessions | Selected with usage | Selected total tokens |
|---|---:|---:|---:|---:|---:|
| backup_cleanup_20260521_194850 | 1,048 | 1,029 | 1,020 | 1,001 | 57,856,335,910 |
| current_archived_sessions | 1 | 1 | 1 | 1 | 157,103 |
| current_sessions | 1,891 | 1,884 | 1,809 | 1,802 | 50,387,894,530 |

## Codebase Density And Economics
| Scope | Files | Lines | Nonblank lines | Characters | Non-ws chars | Tokens / line | Tokens / 1k chars | Output tokens / 1k chars | Tokens / 1k non-ws chars | GPT-5.5 $ / 1k lines | GPT-5.5 $ / 1k chars | gpt-5.3-codex $ / 1k chars | Observed high $ / 1k chars |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| first_party_assets_project_cs | 2,533 | 1,869,185 | 1,637,505 | 80,441,252 | 56,193,003 | 57,909.94 | 1,345,632.80 | 4,655.89 | 1,926,296.55 | $45.04 | $1.05 | $0.38 | $0.99 |
| first_party_scripts_cs | 2,442 | 1,838,970 | 1,611,161 | 79,093,073 | 55,210,822 | 58,861.42 | 1,368,569.76 | 4,735.25 | 1,960,564.68 | $45.78 | $1.06 | $0.39 | $1.01 |
| all_repo_cs_excluding_generated | 6,413 | 3,035,728 | 2,651,652 | 126,465,126 | 89,232,648 | 35,656.81 | 855,922.82 | 2,961.49 | 1,213,058.11 | $27.73 | $0.67 | $0.24 | $0.63 |
| all_repo_source_broad | 7,761 | 3,379,636 | 2,953,121 | 141,859,653 | 101,284,924 | 32,028.42 | 763,038.58 | 2,640.11 | 1,068,711.74 | $24.91 | $0.59 | $0.22 | $0.56 |
| tools_scripts | 229 | 115,688 | 102,788 | 5,104,357 | 4,023,155 | 935,657.87 | 21,206,272.90 | 73,373.73 | 26,905,348.55 | $727.72 | $16.49 | $6.03 | $15.58 |
| docs_markdown_text | 953 | 261,405 | 214,969 | 26,175,545 | 23,337,581 | 414,086.91 | 4,135,325.07 | 14,308.23 | 4,638,200.83 | $322.06 | $3.22 | $1.18 | $3.04 |

## Chat And Client Breakdowns
Top groups use final per-session totals after dedupe. `key` is raw local telemetry.

### Top CWDs
| Key | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| `c:\hades` | 90,469,842,323 | 90,158,723,778 | 86,643,884,672 | 310,084,945 | 93,417,736 |
| `c:\hades\Hecton8` | 17,735,682,299 | 17,671,512,163 | 16,964,203,008 | 64,170,136 | 24,214,929 |
| `C:\Users\danat\Downloads` | 38,862,921 | 38,592,271 | 34,449,920 | 270,650 | 66,211 |

### Top Sources
| Key | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| `vscode` | 104,643,530,001 | 104,285,728,998 | 100,341,474,304 | 356,767,403 | 110,577,634 |
| `cli` | 38,862,921 | 38,592,271 | 34,449,920 | 270,650 | 66,211 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e54e5-7e0f-7d61-b2a8-7d3cf00593f8', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Newton', 'agent_role': 'explorer'}}}` | 20,498,010 | 20,434,334 | 19,412,864 | 63,676 | 16,949 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e54e5-7e0f-7d61-b2a8-7d3cf00593f8', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Russell', 'agent_role': 'explorer'}}}` | 12,377,278 | 12,348,724 | 12,104,064 | 28,554 | 6,276 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e5ab5-ddb8-7003-be49-1c6f53d16cee', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Huygens', 'agent_role': None}}}` | 11,582,832 | 11,564,011 | 11,340,544 | 18,821 | 5,550 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e67d8-17cf-77c0-949a-a496b94302ec', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Hooke', 'agent_role': 'explorer'}}}` | 11,564,323 | 11,542,056 | 11,233,920 | 22,267 | 9,783 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e3d06-345b-75f2-a782-aff05fceab66', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Sartre', 'agent_role': 'explorer'}}}` | 11,394,421 | 11,377,375 | 11,118,848 | 17,046 | 6,472 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e508a-fac6-72a2-8677-a7e5b6e41e5b', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Gauss', 'agent_role': 'explorer'}}}` | 11,304,494 | 11,286,790 | 11,047,168 | 17,704 | 10,188 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e5134-7b8c-74c3-b1f5-c7ec0d2b82d5', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Hypatia', 'agent_role': 'explorer'}}}` | 10,818,198 | 10,799,250 | 10,551,680 | 18,948 | 7,246 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e42fa-4ec0-7e32-8384-f0756a3470c0', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Bohr', 'agent_role': 'explorer'}}}` | 10,097,084 | 10,065,223 | 9,544,832 | 31,861 | 13,815 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e3974-b286-7191-a24e-53679b314dd5', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Poincare', 'agent_role': 'explorer'}}}` | 9,979,355 | 9,923,785 | 9,401,344 | 55,570 | 23,565 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e508a-fac6-72a2-8677-a7e5b6e41e5b', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Nietzsche', 'agent_role': None}}}` | 9,854,783 | 9,843,797 | 9,609,984 | 10,986 | 2,790 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e42fa-4ec0-7e32-8384-f0756a3470c0', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Sagan', 'agent_role': 'explorer'}}}` | 9,569,045 | 9,529,813 | 8,734,336 | 39,232 | 14,045 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e54e5-7e0f-7d61-b2a8-7d3cf00593f8', 'depth': 1, 'agent_path': None, 'agent_nickname': 'McClintock', 'agent_role': 'explorer'}}}` | 9,549,157 | 9,525,158 | 9,286,528 | 23,999 | 7,174 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e44f9-af16-7993-94a1-231c08c2772e', 'depth': 1, 'agent_path': None, 'agent_nickname': 'James', 'agent_role': 'explorer'}}}` | 9,330,334 | 9,310,472 | 9,050,368 | 19,862 | 9,080 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e44f9-af16-7993-94a1-231c08c2772e', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Hypatia', 'agent_role': 'explorer'}}}` | 9,227,755 | 9,207,290 | 8,843,904 | 20,465 | 7,974 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e42fa-4ec0-7e32-8384-f0756a3470c0', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Boyle', 'agent_role': 'explorer'}}}` | 9,209,643 | 9,179,119 | 8,553,984 | 30,524 | 10,512 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e3974-b286-7191-a24e-53679b314dd5', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Herschel', 'agent_role': 'explorer'}}}` | 8,939,006 | 8,897,360 | 8,552,320 | 41,646 | 17,307 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e41a1-24c9-7bd1-b909-9d7c1f2dd16d', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Euler', 'agent_role': 'explorer'}}}` | 8,899,768 | 8,877,824 | 8,624,768 | 21,944 | 13,858 |
| `{'subagent': {'thread_spawn': {'parent_thread_id': '019e42fa-4ec0-7e32-8384-f0756a3470c0', 'depth': 1, 'agent_path': None, 'agent_nickname': 'Lorentz', 'agent_role': 'explorer'}}}` | 8,684,991 | 8,657,752 | 8,063,744 | 27,239 | 9,763 |

### Top Originators
| Key | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| `codex_vscode` | 108,205,524,622 | 107,830,235,941 | 103,608,087,680 | 374,255,081 | 117,632,665 |
| `codex-tui` | 38,862,921 | 38,592,271 | 34,449,920 | 270,650 | 66,211 |

### Top Plan Types
| Key | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| `free` | 107,722,904,679 | 107,349,192,171 | 103,147,536,640 | 372,678,908 | 117,032,618 |
| `team` | 430,146,454 | 428,672,772 | 408,039,808 | 1,473,682 | 556,691 |
| `unknown` | 91,336,410 | 90,963,269 | 86,961,152 | 373,141 | 109,567 |

### Top CLI Versions
| Key | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| `0.131.0-alpha.9` | 60,630,768,103 | 60,415,564,946 | 58,052,968,320 | 214,427,957 | 62,855,409 |
| `0.130.0-alpha.5` | 14,337,281,085 | 14,289,475,888 | 13,766,278,784 | 47,546,797 | 14,804,881 |
| `0.128.0-alpha.1` | 12,731,786,222 | 12,692,996,370 | 12,188,216,832 | 38,789,852 | 12,496,247 |
| `0.125.0-alpha.3` | 9,670,604,755 | 9,637,610,872 | 9,273,073,536 | 32,993,883 | 12,000,325 |
| `0.129.0-alpha.15` | 2,770,006,913 | 2,760,686,574 | 2,636,420,736 | 9,320,339 | 3,261,199 |
| `0.119.0-alpha.28` | 2,515,214,682 | 2,506,391,567 | 2,397,194,496 | 8,823,115 | 3,016,217 |
| `0.122.0-alpha.1` | 1,703,795,744 | 1,696,746,630 | 1,627,171,840 | 7,049,114 | 2,890,165 |
| `0.122.0-alpha.13` | 1,631,271,567 | 1,623,650,731 | 1,556,021,248 | 7,620,836 | 3,479,306 |
| `0.118.0-alpha.2` | 1,513,496,448 | 1,508,502,426 | 1,437,568,896 | 4,994,022 | 1,745,773 |
| `0.124.0-alpha.2` | 406,767,288 | 404,976,380 | 391,651,968 | 1,790,908 | 789,360 |
| `0.119.0-alpha.11` | 294,531,815 | 293,633,557 | 281,521,024 | 898,258 | 293,783 |
| `0.118.0` | 38,862,921 | 38,592,271 | 34,449,920 | 270,650 | 66,211 |


## Daily Stats
| Date Samara | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| 2026-04-03 | 46,914,153 | 46,800,020 | 44,751,232 | 114,133 | 48,190 |
| 2026-04-04 | 217,726,420 | 217,069,509 | 208,059,520 | 656,911 | 300,112 |
| 2026-04-05 | 147,145,632 | 146,568,964 | 139,083,648 | 576,668 | 224,979 |
| 2026-04-06 | 67,295,113 | 67,012,995 | 63,155,968 | 282,118 | 117,563 |
| 2026-04-07 | 231,980,232 | 231,129,121 | 221,723,648 | 851,111 | 275,631 |
| 2026-04-08 | 365,440,905 | 364,224,654 | 344,372,608 | 1,216,251 | 362,385 |
| 2026-04-09 | 537,602,734 | 535,920,945 | 510,756,352 | 1,681,789 | 542,534 |
| 2026-04-10 | 120,758,983 | 120,219,221 | 112,022,016 | 539,762 | 132,393 |
| 2026-04-11 | 161,493,346 | 160,993,566 | 155,746,688 | 499,780 | 191,242 |
| 2026-04-12 | 180,617,991 | 179,746,201 | 169,569,664 | 871,790 | 323,436 |
| 2026-04-13 | 217,604,650 | 216,856,479 | 205,986,816 | 748,171 | 248,235 |
| 2026-04-14 | 272,823,843 | 272,002,972 | 258,142,208 | 820,871 | 308,844 |
| 2026-04-15 | 462,585,355 | 461,073,509 | 440,721,792 | 1,511,846 | 540,429 |
| 2026-04-16 | 517,255,910 | 515,457,159 | 493,700,608 | 1,798,751 | 605,000 |
| 2026-04-17 | 660,349,124 | 658,271,205 | 635,097,728 | 2,077,919 | 701,678 |
| 2026-04-18 | 489,036,261 | 487,172,418 | 467,197,056 | 1,863,843 | 785,244 |
| 2026-04-19 | 338,544,554 | 337,184,425 | 324,000,896 | 1,360,129 | 594,235 |
| 2026-04-20 | 491,688,782 | 489,332,881 | 468,158,208 | 2,355,901 | 880,188 |
| 2026-04-21 | 670,066,703 | 667,417,339 | 643,610,112 | 2,649,364 | 1,034,410 |
| 2026-04-22 | 589,417,352 | 587,150,956 | 560,861,952 | 2,266,396 | 823,033 |
| 2026-04-23 | 753,420,707 | 749,344,244 | 716,932,352 | 4,076,463 | 2,027,319 |
| 2026-04-24 | 574,647,336 | 571,997,731 | 551,393,664 | 2,649,605 | 1,212,386 |
| 2026-04-25 | 556,431,838 | 553,985,815 | 532,980,608 | 2,446,023 | 1,203,739 |
| 2026-04-26 | 279,605,255 | 278,028,301 | 261,297,792 | 1,576,954 | 794,589 |
| 2026-04-27 | 540,705,543 | 538,398,669 | 512,477,184 | 2,306,874 | 1,011,603 |
| 2026-04-28 | 630,972,538 | 628,248,279 | 601,294,720 | 2,724,259 | 1,078,469 |
| 2026-04-29 | 2,371,242,522 | 2,361,791,317 | 2,264,046,208 | 9,451,205 | 3,341,938 |
| 2026-04-30 | 1,934,301,823 | 1,928,793,392 | 1,864,962,304 | 5,508,431 | 1,908,761 |
| 2026-05-01 | 1,658,252,483 | 1,654,281,664 | 1,603,181,312 | 3,970,819 | 1,255,115 |
| 2026-05-02 | 600,356,890 | 598,655,185 | 576,509,184 | 1,701,705 | 510,010 |
| 2026-05-03 | 1,137,400,165 | 1,133,992,954 | 1,093,655,552 | 3,407,211 | 931,949 |
| 2026-05-04 | 1,099,097,709 | 1,096,107,341 | 1,055,105,792 | 2,990,368 | 1,005,607 |
| 2026-05-05 | 1,877,396,348 | 1,871,704,737 | 1,778,847,232 | 5,691,611 | 1,909,201 |
| 2026-05-06 | 1,499,172,953 | 1,495,181,228 | 1,428,096,256 | 3,991,725 | 1,387,800 |
| 2026-05-07 | 2,454,657,515 | 2,447,960,532 | 2,361,763,968 | 6,696,983 | 2,182,677 |
| 2026-05-08 | 2,251,279,157 | 2,243,508,273 | 2,151,188,736 | 7,770,884 | 2,489,958 |
| 2026-05-09 | 3,680,140,530 | 3,667,936,699 | 3,537,070,336 | 12,203,831 | 3,727,560 |
| 2026-05-10 | 1,107,882,179 | 1,104,049,311 | 1,057,653,504 | 3,832,868 | 1,390,915 |
| 2026-05-11 | 2,534,804,277 | 2,525,927,322 | 2,404,871,424 | 8,876,955 | 2,955,420 |
| 2026-05-12 | 2,022,348,386 | 2,015,898,746 | 1,933,376,256 | 6,449,640 | 2,023,604 |
| 2026-05-13 | 4,316,279,171 | 4,303,116,896 | 4,168,047,360 | 13,162,275 | 4,084,874 |
| 2026-05-14 | 1,909,997,371 | 1,903,285,458 | 1,827,978,240 | 6,711,913 | 2,217,603 |
| 2026-05-15 | 4,068,018,530 | 4,052,913,416 | 3,901,611,520 | 15,105,114 | 4,582,586 |
| 2026-05-16 | 3,057,382,237 | 3,046,891,842 | 2,921,180,800 | 10,490,395 | 3,372,832 |
| 2026-05-17 | 2,825,460,950 | 2,815,484,794 | 2,708,110,976 | 9,976,156 | 3,066,818 |
| 2026-05-18 | 3,135,985,060 | 3,123,214,081 | 2,987,810,176 | 12,770,979 | 4,073,419 |
| 2026-05-19 | 6,373,573,208 | 6,349,219,636 | 6,065,698,176 | 24,353,572 | 7,357,653 |
| 2026-05-20 | 7,029,317,551 | 7,003,018,861 | 6,732,588,416 | 26,298,690 | 7,229,638 |
| 2026-05-21 | 11,101,068,200 | 11,060,628,884 | 10,592,076,288 | 40,439,316 | 12,217,203 |
| 2026-05-22 | 4,536,739,481 | 4,521,106,542 | 4,352,767,488 | 15,632,939 | 4,671,535 |
| 2026-05-23 | 4,820,332,622 | 4,804,177,372 | 4,627,110,272 | 16,155,250 | 4,746,761 |
| 2026-05-24 | 4,895,972,931 | 4,881,900,914 | 4,747,163,776 | 14,072,017 | 3,566,006 |
| 2026-05-25 | 4,655,452,664 | 4,639,883,798 | 4,476,006,016 | 15,568,866 | 4,208,630 |
| 2026-05-26 | 4,344,417,825 | 4,328,992,452 | 4,170,849,280 | 15,425,373 | 4,423,936 |
| 2026-05-27 | 4,891,546,699 | 4,875,917,833 | 4,672,912,768 | 15,628,866 | 4,564,315 |

## Daily Cost Stats
| Date Samara | gpt-5.5 standard $ | gpt-5.3-codex secondary $ | Observed-model low bound $ | Observed-model high bound $ | Unpriced tokens |
|---|---:|---:|---:|---:|---:|
| 2026-04-03 | $36.04 | $13.01 | $17.72 | $19.07 | 645,179 |
| 2026-04-04 | $168.79 | $61.37 | $84.39 | $84.39 | 0 |
| 2026-04-05 | $124.27 | $45.51 | $62.13 | $62.13 | 0 |
| 2026-04-06 | $59.33 | $21.75 | $29.59 | $29.94 | 425,524 |
| 2026-04-07 | $183.42 | $67.18 | $91.71 | $91.71 | 0 |
| 2026-04-08 | $307.93 | $112.03 | $151.71 | $151.71 | 0 |
| 2026-04-09 | $431.65 | $156.97 | $213.42 | $224.46 | 17,964,673 |
| 2026-04-10 | $113.19 | $41.51 | $56.41 | $57.23 | 1,080,270 |
| 2026-04-11 | $119.10 | $43.43 | $59.16 | $61.14 | 2,650,758 |
| 2026-04-12 | $161.82 | $59.69 | $80.23 | $83.01 | 4,516,077 |
| 2026-04-13 | $179.79 | $65.54 | $87.79 | $87.79 | 0 |
| 2026-04-14 | $223.00 | $80.92 | $110.98 | $113.33 | 5,181,276 |
| 2026-04-15 | $367.47 | $133.91 | $183.74 | $183.74 | 0 |
| 2026-04-16 | $409.60 | $149.65 | $204.80 | $204.80 | 0 |
| 2026-04-17 | $495.75 | $180.79 | $247.88 | $247.88 | 0 |
| 2026-04-18 | $389.39 | $142.81 | $194.70 | $194.70 | 0 |
| 2026-04-19 | $268.72 | $98.81 | $134.36 | $134.36 | 0 |
| 2026-04-20 | $410.63 | $151.97 | $205.29 | $205.41 | 72,755 |
| 2026-04-21 | $520.32 | $191.39 | $260.16 | $260.16 | 0 |
| 2026-04-22 | $479.87 | $175.89 | $239.90 | $239.90 | 0 |
| 2026-04-23 | $642.82 | $239.25 | $321.41 | $321.41 | 0 |
| 2026-04-24 | $458.21 | $169.65 | $229.10 | $229.10 | 0 |
| 2026-04-25 | $444.90 | $164.28 | $222.45 | $222.45 | 0 |
| 2026-04-26 | $261.61 | $97.08 | $130.79 | $130.87 | 32,240 |
| 2026-04-27 | $455.05 | $167.34 | $227.50 | $227.60 | 37,164 |
| 2026-04-28 | $517.14 | $190.53 | $258.57 | $258.57 | 0 |
| 2026-04-29 | $1,904.28 | $699.58 | $952.14 | $952.14 | 0 |
| 2026-04-30 | $1,416.89 | $515.19 | $1,192.85 | $1,192.85 | 0 |
| 2026-05-01 | $1,176.22 | $425.57 | $1,176.22 | $1,176.22 | 0 |
| 2026-05-02 | $450.04 | $163.47 | $450.04 | $450.04 | 0 |
| 2026-05-03 | $850.73 | $309.68 | $850.73 | $850.73 | 0 |
| 2026-05-04 | $822.27 | $298.26 | $822.27 | $822.27 | 0 |
| 2026-05-05 | $1,524.46 | $553.48 | $1,524.46 | $1,524.46 | 0 |
| 2026-05-06 | $1,169.22 | $423.20 | $1,169.22 | $1,169.22 | 0 |
| 2026-05-07 | $1,812.77 | $657.91 | $1,812.77 | $1,812.77 | 0 |
| 2026-05-08 | $1,770.32 | $646.81 | $1,770.32 | $1,770.32 | 0 |
| 2026-05-09 | $2,788.98 | $1,018.86 | $2,788.98 | $2,788.98 | 0 |
| 2026-05-10 | $875.79 | $319.94 | $875.79 | $875.79 | 0 |
| 2026-05-11 | $2,074.02 | $756.98 | $2,074.02 | $2,074.02 | 0 |
| 2026-05-12 | $1,572.79 | $573.05 | $1,572.79 | $1,572.79 | 0 |
| 2026-05-13 | $3,154.24 | $1,150.05 | $3,154.24 | $3,154.24 | 0 |
| 2026-05-14 | $1,491.88 | $545.65 | $1,491.88 | $1,491.88 | 0 |
| 2026-05-15 | $3,160.47 | $1,159.03 | $3,160.47 | $3,160.47 | 0 |
| 2026-05-16 | $2,403.86 | $878.07 | $2,403.86 | $2,403.86 | 0 |
| 2026-05-17 | $2,190.21 | $801.49 | $2,190.21 | $2,190.21 | 0 |
| 2026-05-18 | $2,554.05 | $938.62 | $2,554.05 | $2,554.05 | 0 |
| 2026-05-19 | $5,181.06 | $1,898.61 | $5,181.06 | $5,181.06 | 0 |
| 2026-05-20 | $5,507.41 | $2,019.64 | $5,507.41 | $5,507.41 | 0 |
| 2026-05-21 | $8,851.98 | $3,239.73 | $8,851.98 | $8,851.98 | 0 |
| 2026-05-22 | $3,487.07 | $1,275.19 | $3,487.07 | $3,487.07 | 0 |
| 2026-05-23 | $3,683.55 | $1,345.79 | $3,683.55 | $3,683.55 | 0 |
| 2026-05-24 | $3,469.43 | $1,263.55 | $3,469.43 | $3,469.43 | 0 |
| 2026-05-25 | $3,524.46 | $1,288.05 | $3,524.46 | $3,524.46 | 0 |
| 2026-05-26 | $3,338.90 | $1,222.60 | $3,338.90 | $3,338.90 | 0 |
| 2026-05-27 | $3,820.35 | $1,391.82 | $3,819.92 | $3,820.35 | 570,802 |

## Weekly Stats
| ISO Week Samara | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| 2026-W14 | 411,786,205 | 410,438,493 | 391,894,400 | 1,347,712 | 573,281 |
| 2026-W15 | 1,665,189,304 | 1,659,246,703 | 1,577,346,944 | 5,942,601 | 1,945,184 |
| 2026-W16 | 2,958,199,697 | 2,948,018,167 | 2,824,847,104 | 10,181,530 | 3,783,665 |
| 2026-W17 | 3,915,277,973 | 3,897,257,267 | 3,735,234,688 | 18,020,706 | 7,975,664 |
| 2026-W18 | 8,873,231,964 | 8,844,161,460 | 8,516,126,464 | 29,070,504 | 10,037,845 |
| 2026-W19 | 13,969,626,391 | 13,926,448,121 | 13,369,725,824 | 43,178,270 | 14,093,718 |
| 2026-W20 | 20,734,290,922 | 20,663,518,474 | 19,865,176,576 | 70,772,448 | 22,303,737 |
| 2026-W21 | 41,892,989,053 | 41,743,266,290 | 40,105,214,592 | 149,722,763 | 43,862,215 |
| 2026-W22 | 13,891,417,188 | 13,844,794,083 | 13,319,768,064 | 46,623,105 | 13,196,881 |

## Weekly Cost Stats
| ISO Week Samara | gpt-5.5 standard $ | gpt-5.3-codex secondary $ | Observed-model low bound $ | Observed-model high bound $ | Unpriced tokens |
|---|---:|---:|---:|---:|---:|
| 2026-W14 | $329.10 | $119.90 | $164.25 | $165.60 | 645,179 |
| 2026-W15 | $1,376.45 | $502.56 | $682.24 | $699.20 | 26,637,302 |
| 2026-W16 | $2,333.72 | $852.44 | $1,164.25 | $1,166.59 | 5,181,276 |
| 2026-W17 | $3,218.35 | $1,189.50 | $1,609.09 | $1,609.30 | 104,995 |
| 2026-W18 | $6,770.35 | $2,471.37 | $5,108.05 | $5,108.15 | 37,164 |
| 2026-W19 | $10,763.82 | $3,918.46 | $10,763.82 | $10,763.82 | 0 |
| 2026-W20 | $16,047.47 | $5,864.32 | $16,047.47 | $16,047.47 | 0 |
| 2026-W21 | $32,734.55 | $11,981.12 | $32,734.55 | $32,734.55 | 0 |
| 2026-W22 | $10,683.71 | $3,902.48 | $10,683.28 | $10,683.71 | 570,802 |

## Monthly Stats
| Month Samara | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| 2026-04 | 14,427,675,605 | 14,372,192,287 | 13,772,103,552 | 55,483,318 | 21,618,565 |
| 2026-05 | 93,884,333,092 | 93,564,956,771 | 89,933,231,104 | 319,376,321 | 96,153,625 |

## Monthly Cost Stats
| Month Samara | gpt-5.5 standard $ | gpt-5.3-codex secondary $ | Observed-model low bound $ | Observed-model high bound $ | Unpriced tokens |
|---|---:|---:|---:|---:|---:|
| 2026-04 | $11,550.99 | $4,237.04 | $6,250.89 | $6,271.86 | 32,605,916 |
| 2026-05 | $72,706.53 | $26,565.10 | $72,706.10 | $72,706.53 | 570,802 |

## Top 20 Days
| Date Samara | Total tokens |
|---|---:|
| 2026-05-21 | 11,101,068,200 |
| 2026-05-20 | 7,029,317,551 |
| 2026-05-19 | 6,373,573,208 |
| 2026-05-24 | 4,895,972,931 |
| 2026-05-27 | 4,891,546,699 |
| 2026-05-23 | 4,820,332,622 |
| 2026-05-25 | 4,655,452,664 |
| 2026-05-22 | 4,536,739,481 |
| 2026-05-26 | 4,344,417,825 |
| 2026-05-13 | 4,316,279,171 |
| 2026-05-15 | 4,068,018,530 |
| 2026-05-09 | 3,680,140,530 |
| 2026-05-18 | 3,135,985,060 |
| 2026-05-16 | 3,057,382,237 |
| 2026-05-17 | 2,825,460,950 |
| 2026-05-11 | 2,534,804,277 |
| 2026-05-07 | 2,454,657,515 |
| 2026-04-29 | 2,371,242,522 |
| 2026-05-08 | 2,251,279,157 |
| 2026-05-12 | 2,022,348,386 |

## Top Output Days
| Date Samara | Output tokens | Total tokens | Reasoning output | Output / total | Reasoning / output |
|---|---:|---:|---:|---:|---:|
| 2026-05-21 | 40,439,316 | 11,101,068,200 | 12,217,203 | 0.3643% | 30.2112% |
| 2026-05-20 | 26,298,690 | 7,029,317,551 | 7,229,638 | 0.3741% | 27.4905% |
| 2026-05-19 | 24,353,572 | 6,373,573,208 | 7,357,653 | 0.3821% | 30.2118% |
| 2026-05-23 | 16,155,250 | 4,820,332,622 | 4,746,761 | 0.3351% | 29.3822% |
| 2026-05-22 | 15,632,939 | 4,536,739,481 | 4,671,535 | 0.3446% | 29.8826% |
| 2026-05-27 | 15,628,866 | 4,891,546,699 | 4,564,315 | 0.3195% | 29.2044% |
| 2026-05-25 | 15,568,866 | 4,655,452,664 | 4,208,630 | 0.3344% | 27.0323% |
| 2026-05-26 | 15,425,373 | 4,344,417,825 | 4,423,936 | 0.3551% | 28.6796% |
| 2026-05-15 | 15,105,114 | 4,068,018,530 | 4,582,586 | 0.3713% | 30.3380% |
| 2026-05-24 | 14,072,017 | 4,895,972,931 | 3,566,006 | 0.2874% | 25.3411% |
| 2026-05-13 | 13,162,275 | 4,316,279,171 | 4,084,874 | 0.3049% | 31.0347% |
| 2026-05-18 | 12,770,979 | 3,135,985,060 | 4,073,419 | 0.4072% | 31.8959% |
| 2026-05-09 | 12,203,831 | 3,680,140,530 | 3,727,560 | 0.3316% | 30.5442% |
| 2026-05-16 | 10,490,395 | 3,057,382,237 | 3,372,832 | 0.3431% | 32.1516% |
| 2026-05-17 | 9,976,156 | 2,825,460,950 | 3,066,818 | 0.3531% | 30.7415% |
| 2026-04-29 | 9,451,205 | 2,371,242,522 | 3,341,938 | 0.3986% | 35.3599% |
| 2026-05-11 | 8,876,955 | 2,534,804,277 | 2,955,420 | 0.3502% | 33.2932% |
| 2026-05-08 | 7,770,884 | 2,251,279,157 | 2,489,958 | 0.3452% | 32.0421% |
| 2026-05-14 | 6,711,913 | 1,909,997,371 | 2,217,603 | 0.3514% | 33.0398% |
| 2026-05-07 | 6,696,983 | 2,454,657,515 | 2,182,677 | 0.2728% | 32.5919% |

## Top Reasoning Days
| Date Samara | Reasoning output | Output tokens | Total tokens | Reasoning / output |
|---|---:|---:|---:|---:|
| 2026-05-21 | 12,217,203 | 40,439,316 | 11,101,068,200 | 30.2112% |
| 2026-05-19 | 7,357,653 | 24,353,572 | 6,373,573,208 | 30.2118% |
| 2026-05-20 | 7,229,638 | 26,298,690 | 7,029,317,551 | 27.4905% |
| 2026-05-23 | 4,746,761 | 16,155,250 | 4,820,332,622 | 29.3822% |
| 2026-05-22 | 4,671,535 | 15,632,939 | 4,536,739,481 | 29.8826% |
| 2026-05-15 | 4,582,586 | 15,105,114 | 4,068,018,530 | 30.3380% |
| 2026-05-27 | 4,564,315 | 15,628,866 | 4,891,546,699 | 29.2044% |
| 2026-05-26 | 4,423,936 | 15,425,373 | 4,344,417,825 | 28.6796% |
| 2026-05-25 | 4,208,630 | 15,568,866 | 4,655,452,664 | 27.0323% |
| 2026-05-13 | 4,084,874 | 13,162,275 | 4,316,279,171 | 31.0347% |
| 2026-05-18 | 4,073,419 | 12,770,979 | 3,135,985,060 | 31.8959% |
| 2026-05-09 | 3,727,560 | 12,203,831 | 3,680,140,530 | 30.5442% |
| 2026-05-24 | 3,566,006 | 14,072,017 | 4,895,972,931 | 25.3411% |
| 2026-05-16 | 3,372,832 | 10,490,395 | 3,057,382,237 | 32.1516% |
| 2026-04-29 | 3,341,938 | 9,451,205 | 2,371,242,522 | 35.3599% |
| 2026-05-17 | 3,066,818 | 9,976,156 | 2,825,460,950 | 30.7415% |
| 2026-05-11 | 2,955,420 | 8,876,955 | 2,534,804,277 | 33.2932% |
| 2026-05-08 | 2,489,958 | 7,770,884 | 2,251,279,157 | 32.0421% |
| 2026-05-14 | 2,217,603 | 6,711,913 | 1,909,997,371 | 33.0398% |
| 2026-05-07 | 2,182,677 | 6,696,983 | 2,454,657,515 | 32.5919% |

## Distributions
| Metric | Value |
|---|---:|
| tokens_per_day_span | 1,968,079,773.51 |
| tokens_per_session_with_usage | 38,603,561.89 |
| output_tokens_per_session_with_usage | 133,568.38 |
| median_tokens_per_session | 3,555,385.00 |
| p90_tokens_per_session | 108,828,922.00 |
| p95_tokens_per_session | 178,487,644.00 |
| p99_tokens_per_session | 417,444,229.00 |
| max_tokens_per_session | 1,953,701,850.00 |

Context window counts:
- 258400: 2,804

Plan type counts:
- free: 2,792
- team: 10
- unknown: 2

## Top 25 Sessions
| Rank | Session | Model | Effort | Root | Final UTC | Total | Input | Cached | Output | I/O | Output / total | Reasoning / output | Primary $ | Model $ | CWD |
|---:|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | `019e42fa-4ec0-7e32-8384-f0756a3470c0` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:25:54.292000+00:00 | 1,953,701,850 | 1,949,760,725 | 1,896,275,456 | 3,941,125 | 494.72 | 0.2017% | 29.5558% | $1,333.80 | $1,333.80 | `c:\hades` |
| 2 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T01:19:43.715000+00:00 | 1,305,764,480 | 1,302,806,591 | 1,262,117,376 | 2,957,889 | 440.45 | 0.2265% | 28.2381% | $923.24 | $923.24 | `c:\hades` |
| 3 | `019e3dbf-eddb-7ab0-84b6-aa5b097a2b68` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T18:58:24.572000+00:00 | 1,300,668,055 | 1,296,761,551 | 1,261,152,128 | 3,906,504 | 331.95 | 0.3003% | 24.7620% | $925.82 | $925.82 | `c:\hades` |
| 4 | `019e42c1-57ec-7701-a1d7-7b5fbb073503` | gpt-5.5 | xhigh | current_sessions | 2026-05-23T01:11:23.296000+00:00 | 1,167,862,097 | 1,163,767,930 | 1,128,039,680 | 4,094,167 | 284.25 | 0.3506% | 24.5933% | $865.49 | $865.49 | `c:\hades` |
| 5 | `019e54e3-0619-7d00-bd04-709b7ec1949e` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T09:51:02.089000+00:00 | 892,421,675 | 890,196,376 | 866,640,512 | 2,225,299 | 400.03 | 0.2494% | 27.2065% | $617.86 | $617.86 | `c:\hades` |
| 6 | `019e54e5-7e0f-7d61-b2a8-7d3cf00593f8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T09:48:09.286000+00:00 | 822,302,475 | 820,019,618 | 799,859,200 | 2,282,857 | 359.21 | 0.2776% | 25.2734% | $569.22 | $569.22 | `c:\hades` |
| 7 | `019e6137-88ea-7683-aec2-12061e8157bf` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T17:01:44.226000+00:00 | 755,622,806 | 753,403,283 | 732,507,136 | 2,219,523 | 339.44 | 0.2937% | 25.7552% | $537.32 | $537.32 | `c:\hades` |
| 8 | `019e54e1-c11c-71b0-9b8a-21b8efdcde8c` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:02:22.353000+00:00 | 711,109,922 | 709,268,154 | 692,436,480 | 1,841,768 | 385.10 | 0.2590% | 25.6240% | $485.63 | $485.63 | `c:\hades` |
| 9 | `019e558a-e142-7303-8f57-df6d1d2d75d4` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:25:51.533000+00:00 | 690,288,484 | 688,402,127 | 669,142,528 | 1,886,357 | 364.94 | 0.2733% | 26.7878% | $487.46 | $487.46 | `c:\hades` |
| 10 | `019e558b-99b3-73a2-b1b0-744e2c7adaf8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T02:21:00.701000+00:00 | 621,711,391 | 620,254,496 | 605,311,232 | 1,456,895 | 425.74 | 0.2343% | 27.9349% | $421.08 | $421.08 | `c:\hades` |
| 11 | `019e5249-c70f-7263-880f-3531b581aad4` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T00:17:04.800000+00:00 | 610,793,317 | 608,951,910 | 594,295,296 | 1,841,407 | 330.70 | 0.3015% | 20.1130% | $425.67 | $425.67 | `c:\hades` |
| 12 | `019e5d19-2023-72e3-b282-e1a65f86f339` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T19:04:30.489000+00:00 | 579,264,651 | 577,359,818 | 556,203,904 | 1,904,833 | 303.10 | 0.3288% | 27.2164% | $441.03 | $441.03 | `c:\hades` |
| 13 | `019e54df-35af-7c60-8da3-67d86a082648` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:41:44.391000+00:00 | 564,611,473 | 562,927,882 | 546,199,040 | 1,683,591 | 334.36 | 0.2982% | 28.2493% | $407.25 | $407.25 | `c:\hades` |
| 14 | `019e42d0-0f2a-72c2-a688-9241371dd6e4` | gpt-5.5 | xhigh | current_sessions | 2026-05-20T23:54:50.549000+00:00 | 548,201,085 | 546,656,842 | 535,127,040 | 1,544,243 | 354.00 | 0.2817% | 21.9045% | $371.54 | $371.54 | `c:\hades` |
| 15 | `019e480b-c231-7d60-ac6c-130c5f52e788` | gpt-5.5 | xhigh | current_sessions | 2026-05-22T09:05:28.159000+00:00 | 528,011,366 | 526,558,944 | 512,782,336 | 1,452,422 | 362.54 | 0.2751% | 25.0314% | $368.85 | $368.85 | `c:\hades` |
| 16 | `019e42d6-563e-7d31-ad70-d983432fe8d1` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T15:23:03.424000+00:00 | 523,659,412 | 521,826,682 | 502,747,776 | 1,832,730 | 284.73 | 0.3500% | 26.1494% | $401.75 | $401.75 | `c:\hades` |
| 17 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-14T10:56:23.755000+00:00 | 518,697,166 | 517,631,477 | 503,886,080 | 1,065,689 | 485.72 | 0.2055% | 30.6934% | $352.64 | $352.64 | `c:\hades` |
| 18 | `019e4328-e29d-7163-b59e-f2841cce7c18` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T16:09:09.486000+00:00 | 517,149,254 | 515,515,364 | 496,384,640 | 1,633,890 | 315.51 | 0.3159% | 28.1066% | $392.86 | $392.86 | `c:\hades` |
| 19 | `019e559e-fe08-72d2-8a16-4deb504b9a8f` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:35:55.207000+00:00 | 512,144,428 | 510,572,140 | 496,116,608 | 1,572,288 | 324.73 | 0.3070% | 27.6906% | $367.50 | $367.50 | `c:\hades` |
| 20 | `019e4b4d-3686-7c23-b2a3-a04e2101ce5c` | gpt-5.5 | xhigh | current_sessions | 2026-05-22T07:38:58.454000+00:00 | 508,129,959 | 506,761,687 | 494,648,448 | 1,368,272 | 370.37 | 0.2693% | 24.4260% | $348.94 | $348.94 | `c:\hades` |
| 21 | `019e3700-e461-7b83-b037-ecaceb36f169` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T22:25:39.731000+00:00 | 504,011,103 | 502,231,620 | 479,605,632 | 1,779,483 | 282.23 | 0.3531% | 27.3197% | $406.32 | $406.32 | `c:\hades` |
| 22 | `019e5488-d058-7152-8dda-15c6e68fd5d5` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T01:31:58.063000+00:00 | 502,547,222 | 500,775,886 | 483,416,704 | 1,771,336 | 282.71 | 0.3525% | 23.1173% | $381.64 | $381.64 | `c:\hades` |
| 23 | `019e559a-8add-70d0-a8b6-d80ae1bce573` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T02:44:34.994000+00:00 | 501,605,986 | 500,149,065 | 486,301,824 | 1,456,921 | 343.29 | 0.2905% | 24.1905% | $356.09 | $356.09 | `c:\hades` |
| 24 | `019d6329-de82-74e2-83ca-450539a61cec` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-09T13:02:36.778000+00:00 | 490,407,394 | 488,890,828 | 466,945,664 | 1,516,566 | 322.37 | 0.3092% | 28.0769% | $388.70 | $194.35 | `c:\hades\Hecton8` |
| 25 | `019e3974-b286-7191-a24e-53679b314dd5` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-21T00:37:51.363000+00:00 | 469,509,921 | 467,091,035 | 445,224,192 | 2,418,886 | 193.10 | 0.5152% | 19.8737% | $404.51 | $404.51 | `c:\hades` |

## Top 25 Output Sessions
| Rank | Session | Model | Effort | Root | Final UTC | Output | Reasoning output | Total | I/O | Paid I/O | Cached / output | Output cost share | Primary $ | CWD |
|---:|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | `019e42c1-57ec-7701-a1d7-7b5fbb073503` | gpt-5.5 | xhigh | current_sessions | 2026-05-23T01:11:23.296000+00:00 | 4,094,167 | 1,006,889 | 1,167,862,097 | 284.25 | 8.73 | 275.52 | 14.1914% | $865.49 | `c:\hades` |
| 2 | `019e42fa-4ec0-7e32-8384-f0756a3470c0` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:25:54.292000+00:00 | 3,941,125 | 1,164,831 | 1,953,701,850 | 494.72 | 13.57 | 481.15 | 8.8644% | $1,333.80 | `c:\hades` |
| 3 | `019e3dbf-eddb-7ab0-84b6-aa5b097a2b68` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T18:58:24.572000+00:00 | 3,906,504 | 967,330 | 1,300,668,055 | 331.95 | 9.12 | 322.83 | 12.6585% | $925.82 | `c:\hades` |
| 4 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T01:19:43.715000+00:00 | 2,957,889 | 835,253 | 1,305,764,480 | 440.45 | 13.76 | 426.70 | 9.6114% | $923.24 | `c:\hades` |
| 5 | `019e3974-b286-7191-a24e-53679b314dd5` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-21T00:37:51.363000+00:00 | 2,418,886 | 480,721 | 469,509,921 | 193.10 | 9.04 | 184.06 | 17.9393% | $404.51 | `c:\hades` |
| 6 | `019e54e5-7e0f-7d61-b2a8-7d3cf00593f8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T09:48:09.286000+00:00 | 2,282,857 | 576,956 | 822,302,475 | 359.21 | 8.83 | 350.38 | 12.0316% | $569.22 | `c:\hades` |
| 7 | `019e54e3-0619-7d00-bd04-709b7ec1949e` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T09:51:02.089000+00:00 | 2,225,299 | 605,425 | 892,421,675 | 400.03 | 10.59 | 389.45 | 10.8049% | $617.86 | `c:\hades` |
| 8 | `019e6137-88ea-7683-aec2-12061e8157bf` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T17:01:44.226000+00:00 | 2,219,523 | 571,642 | 755,622,806 | 339.44 | 9.41 | 330.03 | 12.3922% | $537.32 | `c:\hades` |
| 9 | `019e4024-b80b-7c02-8089-6efcb541ca5b` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T00:37:35.372000+00:00 | 1,953,484 | 259,969 | 417,444,229 | 212.69 | 6.69 | 206.00 | 18.0247% | $325.13 | `c:\hades` |
| 10 | `019e5d19-2023-72e3-b282-e1a65f86f339` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T19:04:30.489000+00:00 | 1,904,833 | 518,427 | 579,264,651 | 303.10 | 11.11 | 292.00 | 12.9573% | $441.03 | `c:\hades` |
| 11 | `019e558a-e142-7303-8f57-df6d1d2d75d4` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:25:51.533000+00:00 | 1,886,357 | 505,313 | 690,288,484 | 364.94 | 10.21 | 354.73 | 11.6093% | $487.46 | `c:\hades` |
| 12 | `019e63cd-1665-7e03-8f44-597caea9da15` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T18:21:13.980000+00:00 | 1,846,982 | 509,135 | 458,053,290 | 247.00 | 9.62 | 237.38 | 15.2445% | $363.47 | `c:\hades` |
| 13 | `019e54e1-c11c-71b0-9b8a-21b8efdcde8c` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:02:22.353000+00:00 | 1,841,768 | 471,934 | 711,109,922 | 385.10 | 9.14 | 375.96 | 11.3776% | $485.63 | `c:\hades` |
| 14 | `019e5249-c70f-7263-880f-3531b581aad4` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T00:17:04.800000+00:00 | 1,841,407 | 370,362 | 610,793,317 | 330.70 | 7.96 | 322.74 | 12.9776% | $425.67 | `c:\hades` |
| 15 | `019e42d6-563e-7d31-ad70-d983432fe8d1` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T15:23:03.424000+00:00 | 1,832,730 | 479,247 | 523,659,412 | 284.73 | 10.41 | 274.32 | 13.6856% | $401.75 | `c:\hades` |
| 16 | `019e3700-e461-7b83-b037-ecaceb36f169` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T22:25:39.731000+00:00 | 1,779,483 | 486,149 | 504,011,103 | 282.23 | 12.71 | 269.52 | 13.1386% | $406.32 | `c:\hades` |
| 17 | `019e5488-d058-7152-8dda-15c6e68fd5d5` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T01:31:58.063000+00:00 | 1,771,336 | 409,485 | 502,547,222 | 282.71 | 9.80 | 272.91 | 13.9240% | $381.64 | `c:\hades` |
| 18 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-29T22:03:49.658000+00:00 | 1,686,324 | 528,007 | 408,633,638 | 241.32 | 7.55 | 233.77 | 16.2487% | $311.35 | `c:\hades\Hecton8` |
| 19 | `019e54df-35af-7c60-8da3-67d86a082648` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:41:44.391000+00:00 | 1,683,591 | 475,603 | 564,611,473 | 334.36 | 9.94 | 324.43 | 12.4021% | $407.25 | `c:\hades` |
| 20 | `019e4328-e29d-7163-b59e-f2841cce7c18` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T16:09:09.486000+00:00 | 1,633,890 | 459,231 | 517,149,254 | 315.51 | 11.71 | 303.81 | 12.4768% | $392.86 | `c:\hades` |
| 21 | `019e559e-fe08-72d2-8a16-4deb504b9a8f` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:35:55.207000+00:00 | 1,572,288 | 435,376 | 512,144,428 | 324.73 | 9.19 | 315.54 | 12.8348% | $367.50 | `c:\hades` |
| 22 | `019e42d0-0f2a-72c2-a688-9241371dd6e4` | gpt-5.5 | xhigh | current_sessions | 2026-05-20T23:54:50.549000+00:00 | 1,544,243 | 338,259 | 548,201,085 | 354.00 | 7.47 | 346.53 | 12.4690% | $371.54 | `c:\hades` |
| 23 | `019d6329-de82-74e2-83ca-450539a61cec` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-09T13:02:36.778000+00:00 | 1,516,566 | 425,805 | 490,407,394 | 322.37 | 14.47 | 307.90 | 11.7050% | $388.70 | `c:\hades\Hecton8` |
| 24 | `019e559a-8add-70d0-a8b6-d80ae1bce573` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T02:44:34.994000+00:00 | 1,456,921 | 352,437 | 501,605,986 | 343.29 | 9.50 | 333.79 | 12.2742% | $356.09 | `c:\hades` |
| 25 | `019e558b-99b3-73a2-b1b0-744e2c7adaf8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T02:21:00.701000+00:00 | 1,456,895 | 406,982 | 621,711,391 | 425.74 | 10.26 | 415.48 | 10.3797% | $421.08 | `c:\hades` |

## Top 25 Reasoning Sessions
| Rank | Session | Model | Effort | Root | Final UTC | Output | Reasoning output | Total | I/O | Paid I/O | Cached / output | Output cost share | Primary $ | CWD |
|---:|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | `019e42fa-4ec0-7e32-8384-f0756a3470c0` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:25:54.292000+00:00 | 3,941,125 | 1,164,831 | 1,953,701,850 | 494.72 | 13.57 | 481.15 | 8.8644% | $1,333.80 | `c:\hades` |
| 2 | `019e42c1-57ec-7701-a1d7-7b5fbb073503` | gpt-5.5 | xhigh | current_sessions | 2026-05-23T01:11:23.296000+00:00 | 4,094,167 | 1,006,889 | 1,167,862,097 | 284.25 | 8.73 | 275.52 | 14.1914% | $865.49 | `c:\hades` |
| 3 | `019e3dbf-eddb-7ab0-84b6-aa5b097a2b68` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T18:58:24.572000+00:00 | 3,906,504 | 967,330 | 1,300,668,055 | 331.95 | 9.12 | 322.83 | 12.6585% | $925.82 | `c:\hades` |
| 4 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T01:19:43.715000+00:00 | 2,957,889 | 835,253 | 1,305,764,480 | 440.45 | 13.76 | 426.70 | 9.6114% | $923.24 | `c:\hades` |
| 5 | `019e54e3-0619-7d00-bd04-709b7ec1949e` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T09:51:02.089000+00:00 | 2,225,299 | 605,425 | 892,421,675 | 400.03 | 10.59 | 389.45 | 10.8049% | $617.86 | `c:\hades` |
| 6 | `019e54e5-7e0f-7d61-b2a8-7d3cf00593f8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T09:48:09.286000+00:00 | 2,282,857 | 576,956 | 822,302,475 | 359.21 | 8.83 | 350.38 | 12.0316% | $569.22 | `c:\hades` |
| 7 | `019e6137-88ea-7683-aec2-12061e8157bf` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T17:01:44.226000+00:00 | 2,219,523 | 571,642 | 755,622,806 | 339.44 | 9.41 | 330.03 | 12.3922% | $537.32 | `c:\hades` |
| 8 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-29T22:03:49.658000+00:00 | 1,686,324 | 528,007 | 408,633,638 | 241.32 | 7.55 | 233.77 | 16.2487% | $311.35 | `c:\hades\Hecton8` |
| 9 | `019e5d19-2023-72e3-b282-e1a65f86f339` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T19:04:30.489000+00:00 | 1,904,833 | 518,427 | 579,264,651 | 303.10 | 11.11 | 292.00 | 12.9573% | $441.03 | `c:\hades` |
| 10 | `019e63cd-1665-7e03-8f44-597caea9da15` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T18:21:13.980000+00:00 | 1,846,982 | 509,135 | 458,053,290 | 247.00 | 9.62 | 237.38 | 15.2445% | $363.47 | `c:\hades` |
| 11 | `019e558a-e142-7303-8f57-df6d1d2d75d4` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:25:51.533000+00:00 | 1,886,357 | 505,313 | 690,288,484 | 364.94 | 10.21 | 354.73 | 11.6093% | $487.46 | `c:\hades` |
| 12 | `019d67a6-6823-7b82-94f9-a3167b8e0286` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-09T11:13:03.573000+00:00 | 1,410,960 | 489,347 | 429,064,399 | 303.09 | 14.11 | 288.99 | 12.2435% | $345.73 | `c:\hades\Hecton8` |
| 13 | `019e3700-e461-7b83-b037-ecaceb36f169` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T22:25:39.731000+00:00 | 1,779,483 | 486,149 | 504,011,103 | 282.23 | 12.71 | 269.52 | 13.1386% | $406.32 | `c:\hades` |
| 14 | `019e3974-b286-7191-a24e-53679b314dd5` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-21T00:37:51.363000+00:00 | 2,418,886 | 480,721 | 469,509,921 | 193.10 | 9.04 | 184.06 | 17.9393% | $404.51 | `c:\hades` |
| 15 | `019e42d6-563e-7d31-ad70-d983432fe8d1` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T15:23:03.424000+00:00 | 1,832,730 | 479,247 | 523,659,412 | 284.73 | 10.41 | 274.32 | 13.6856% | $401.75 | `c:\hades` |
| 16 | `019e54df-35af-7c60-8da3-67d86a082648` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:41:44.391000+00:00 | 1,683,591 | 475,603 | 564,611,473 | 334.36 | 9.94 | 324.43 | 12.4021% | $407.25 | `c:\hades` |
| 17 | `019e54e1-c11c-71b0-9b8a-21b8efdcde8c` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T10:02:22.353000+00:00 | 1,841,768 | 471,934 | 711,109,922 | 385.10 | 9.14 | 375.96 | 11.3776% | $485.63 | `c:\hades` |
| 18 | `019e4328-e29d-7163-b59e-f2841cce7c18` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T16:09:09.486000+00:00 | 1,633,890 | 459,231 | 517,149,254 | 315.51 | 11.71 | 303.81 | 12.4768% | $392.86 | `c:\hades` |
| 19 | `019e559e-fe08-72d2-8a16-4deb504b9a8f` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:35:55.207000+00:00 | 1,572,288 | 435,376 | 512,144,428 | 324.73 | 9.19 | 315.54 | 12.8348% | $367.50 | `c:\hades` |
| 20 | `019d6329-de82-74e2-83ca-450539a61cec` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-09T13:02:36.778000+00:00 | 1,516,566 | 425,805 | 490,407,394 | 322.37 | 14.47 | 307.90 | 11.7050% | $388.70 | `c:\hades\Hecton8` |
| 21 | `019e5488-d058-7152-8dda-15c6e68fd5d5` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T01:31:58.063000+00:00 | 1,771,336 | 409,485 | 502,547,222 | 282.71 | 9.80 | 272.91 | 13.9240% | $381.64 | `c:\hades` |
| 22 | `019e558b-99b3-73a2-b1b0-744e2c7adaf8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T02:21:00.701000+00:00 | 1,456,895 | 406,982 | 621,711,391 | 425.74 | 10.26 | 415.48 | 10.3797% | $421.08 | `c:\hades` |
| 23 | `019e5f29-93db-7422-93dd-94610846c858` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T17:26:22.168000+00:00 | 1,438,933 | 406,134 | 421,443,562 | 291.89 | 9.80 | 282.09 | 13.6340% | $316.62 | `c:\hades` |
| 24 | `019e63ba-0634-7250-97fd-12d424b6d428` | gpt-5.5 | xhigh | current_sessions | 2026-05-27T17:26:00.614000+00:00 | 1,302,728 | 385,811 | 415,487,729 | 317.94 | 12.87 | 305.07 | 12.1523% | $321.60 | `c:\hades` |
| 25 | `019dcffb-b2d1-7772-81c9-a6715b38b0cd` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-29T19:58:22.258000+00:00 | 948,530 | 378,862 | 208,914,865 | 219.25 | 9.81 | 209.44 | 16.3242% | $174.32 | `c:\hades\Hecton8` |

## Price Sources
- https://developers.openai.com/api/docs/pricing checked 2026-05-27; lines 700-708 list GPT-5.5/GPT-5.4/GPT-5.4-mini standard short-context rates and regional uplift
- https://developers.openai.com/api/docs/pricing checked 2026-05-27; lines 740-745 list GPT-5.5/GPT-5.4/GPT-5.4-mini priority short-context rates
- https://developers.openai.com/api/docs/pricing checked 2026-05-27; lines 865-881 list specialized gpt-5.3-codex standard and priority rates
- https://developers.openai.com/api/docs/guides/prompt-caching checked 2026-05-27; lines 741-757 define GPT-5.5 cache retention default and cached token reporting
- https://developers.openai.com/api/docs/guides/reasoning checked 2026-05-27; lines 813-829 define reasoning effort, including xhigh, and lines 837-842 define reasoning tokens as output-billed
- All dollar values are API-equivalent estimates, not local invoice proof

## Residual Risk
- Local JSONL is not provider billing. It lacks invoice ids and does not expose whether a Codex request used standard, priority, enterprise, subscription, or internal billing.
- `cached_input_tokens` is treated as a priced subcounter of input tokens, not additional total tokens.
- Model labels are exact only where structured `turn_context` fields exist. Older sessions without model fields remain `unknown_model`.
- Daily/week/model delta allocation is reconstructed from telemetry deltas; all-time final per-session total remains authoritative for this local audit.
