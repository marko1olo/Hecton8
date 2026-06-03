# TOKEN USAGE AUDIT FAST REFRESH 2026-06-03

Generated UTC: 2026-06-03T17:43:24.001916+00:00
Generated Samara: 2026-06-03T21:43:24.001916+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,290 |
| sessions_with_usage | 3,141 |
| input_tokens | 134,319,293,029 |
| cached_input_tokens | 129,184,906,624 |
| output_tokens | 465,404,495 |
| reasoning_output_tokens | 142,978,406 |
| total_tokens | 134,785,731,124 |
| GPT-5.5 standard under-272K API-equivalent | $104,226.52 |
| GPT-5.5 long-context sensitivity upper bound | $201,471.97 |
| GPT-5.5 long-context + regional sensitivity upper bound | $221,619.17 |
| GPT-5.5 regional +10% sensitivity | $114,649.17 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 101,089,298,343 |
| all-time 500-word printed pages | 202,178,596 |
| all-time 80k-word books | 1,263,616 |
| continuous reading at 250 wpm | 769.32 years |
| 8h/day reading at 250 wpm | 2,307.97 years |
| all-time $60 game equivalents | 1,737.11 |
| all-time $2k workstation equivalents | 52.11 |
| cached input share | 96.18% |
| since previous snapshot approximate words | 3,865,312,312 |
| since previous snapshot 500-word pages | 7,730,624 |
| current burn approximate words / second | 36,348.95 |
| current burn pages / hour | 261,712.43 |
| current burn GPT-5.5 standard $ / day | $3,263.89 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-06-02.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-06-02T12:11:04.636000+00:00`
Changed JSONL files scanned: 67
Increment events after cutoff: 30,996
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 5,153,749,750 |
| input_tokens | 5,134,810,247 |
| cached_input_tokens | 4,938,915,840 |
| output_tokens | 18,939,503 |
| reasoning_output_tokens | 5,341,285 |
| GPT-5.5 standard $ | $4,017.12 |
| primary C# lines | 66,353 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 174,474,951.41 |
| total tokens / second | 48,465.26 |
| GPT-5.5 standard $ / hour | $136.00 |
| primary C# lines / hour | 2,246.31 |
| tokens / net primary C# line | 77,671.69 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
