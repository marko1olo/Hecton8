# TOKEN USAGE AUDIT FAST REFRESH 2026-06-06

Generated UTC: 2026-06-06T07:33:49.307118+00:00
Generated Samara: 2026-06-06T11:33:49.307118+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 1,536 |
| sessions_with_usage | 3,608 |
| input_tokens | 138,063,786,512 |
| cached_input_tokens | 132,756,527,360 |
| output_tokens | 481,796,365 |
| reasoning_output_tokens | 147,451,139 |
| total_tokens | 138,546,616,477 |
| GPT-5.5 standard under-272K API-equivalent | $107,368.45 |
| GPT-5.5 long-context sensitivity upper bound | $207,509.96 |
| GPT-5.5 long-context + regional sensitivity upper bound | $228,260.95 |
| GPT-5.5 regional +10% sensitivity | $118,105.30 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 103,909,962,357 |
| all-time 500-word printed pages | 207,819,924 |
| all-time 80k-word books | 1,298,874 |
| continuous reading at 250 wpm | 790.79 years |
| 8h/day reading at 250 wpm | 2,372.37 years |
| all-time $60 game equivalents | 1,789.47 |
| all-time $2k workstation equivalents | 53.68 |
| cached input share | 96.16% |
| since previous snapshot approximate words | 1,583,047,310 |
| since previous snapshot 500-word pages | 3,166,094 |
| current burn approximate words / second | 33,027.71 |
| current burn pages / hour | 237,799.51 |
| current burn GPT-5.5 standard $ / day | $3,123.56 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-06-05.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-06-05T18:14:58.747000+00:00`
Changed JSONL files scanned: 208
Increment events after cutoff: 14,331
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 2,110,729,747 |
| input_tokens | 2,102,404,629 |
| cached_input_tokens | 2,006,436,992 |
| output_tokens | 8,325,118 |
| reasoning_output_tokens | 2,458,359 |
| GPT-5.5 standard $ | $1,732.81 |
| primary C# lines | 5,416 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 158,533,006.67 |
| total tokens / second | 44,036.95 |
| GPT-5.5 standard $ / hour | $130.15 |
| primary C# lines / hour | 406.79 |
| tokens / net primary C# line | 389,721.15 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
