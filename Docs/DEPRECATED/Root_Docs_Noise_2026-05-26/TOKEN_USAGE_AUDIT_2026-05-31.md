# TOKEN USAGE AUDIT FAST REFRESH 2026-05-31

Generated UTC: 2026-05-31T11:57:03.452084+00:00
Generated Samara: 2026-05-31T15:57:03.452084+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,173 |
| sessions_with_usage | 3,027 |
| input_tokens | 124,075,208,667 |
| cached_input_tokens | 119,314,542,848 |
| output_tokens | 428,998,078 |
| reasoning_output_tokens | 132,819,818 |
| total_tokens | 124,505,240,345 |
| GPT-5.5 standard under-272K API-equivalent | $96,330.54 |
| GPT-5.5 long-context sensitivity upper bound | $186,226.11 |
| GPT-5.5 long-context + regional sensitivity upper bound | $204,848.73 |
| GPT-5.5 regional +10% sensitivity | $105,963.60 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 93,378,930,258 |
| all-time 500-word printed pages | 186,757,860 |
| all-time 80k-word books | 1,167,236 |
| continuous reading at 250 wpm | 710.65 years |
| 8h/day reading at 250 wpm | 2,131.94 years |
| all-time $60 game equivalents | 1,605.51 |
| all-time $2k workstation equivalents | 48.17 |
| cached input share | 96.16% |
| since previous snapshot approximate words | 777,650,425 |
| since previous snapshot 500-word pages | 1,555,300 |
| current burn approximate words / second | 13,053.25 |
| current burn pages / hour | 93,983.42 |
| current burn GPT-5.5 standard $ / day | $1,129.91 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-30.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-05-30T19:25:04.432000+00:00`
Changed JSONL files scanned: 25
Increment events after cutoff: 6,394
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 1,036,867,234 |
| input_tokens | 1,033,671,384 |
| cached_input_tokens | 996,694,784 |
| output_tokens | 3,195,850 |
| reasoning_output_tokens | 919,009 |
| GPT-5.5 standard $ | $779.11 |
| primary C# lines | 9,412 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 62,655,612.92 |
| total tokens / second | 17,404.34 |
| GPT-5.5 standard $ / hour | $47.08 |
| primary C# lines / hour | 568.75 |
| tokens / net primary C# line | 110,164.39 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
