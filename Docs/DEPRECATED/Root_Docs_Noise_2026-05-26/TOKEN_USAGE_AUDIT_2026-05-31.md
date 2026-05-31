# TOKEN USAGE AUDIT FAST REFRESH 2026-05-31

Generated UTC: 2026-05-31T12:13:49.075812+00:00
Generated Samara: 2026-05-31T16:13:49.075812+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,173 |
| sessions_with_usage | 3,027 |
| input_tokens | 124,095,978,723 |
| cached_input_tokens | 119,334,398,080 |
| output_tokens | 429,060,625 |
| reasoning_output_tokens | 132,838,116 |
| total_tokens | 124,526,072,948 |
| GPT-5.5 standard under-272K API-equivalent | $96,346.92 |
| GPT-5.5 long-context sensitivity upper bound | $186,257.93 |
| GPT-5.5 long-context + regional sensitivity upper bound | $204,883.73 |
| GPT-5.5 regional +10% sensitivity | $105,981.61 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 93,394,554,711 |
| all-time 500-word printed pages | 186,789,109 |
| all-time 80k-word books | 1,167,431 |
| continuous reading at 250 wpm | 710.77 years |
| 8h/day reading at 250 wpm | 2,132.30 years |
| all-time $60 game equivalents | 1,605.78 |
| all-time $2k workstation equivalents | 48.17 |
| cached input share | 96.16% |
| since previous snapshot approximate words | 15,624,452 |
| since previous snapshot 500-word pages | 31,248 |
| current burn approximate words / second | 15,537.08 |
| current burn pages / hour | 111,866.95 |
| current burn GPT-5.5 standard $ / day | $1,407.16 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-31.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-05-31T11:57:03.020000+00:00`
Changed JSONL files scanned: 4
Increment events after cutoff: 137
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 20,832,603 |
| input_tokens | 20,770,056 |
| cached_input_tokens | 19,855,232 |
| output_tokens | 62,547 |
| reasoning_output_tokens | 18,298 |
| GPT-5.5 standard $ | $16.38 |
| primary C# lines | 45 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 74,577,964.61 |
| total tokens / second | 20,716.10 |
| GPT-5.5 standard $ / hour | $58.63 |
| primary C# lines / hour | 161.09 |
| tokens / net primary C# line | 462,946.73 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
