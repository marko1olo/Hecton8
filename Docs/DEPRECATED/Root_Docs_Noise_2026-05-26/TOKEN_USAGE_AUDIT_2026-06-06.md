# TOKEN USAGE AUDIT FAST REFRESH 2026-06-06

Generated UTC: 2026-06-05T20:18:24.605539+00:00
Generated Samara: 2026-06-06T00:18:24.605539+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 1,384 |
| sessions_with_usage | 3,457 |
| input_tokens | 136,283,052,806 |
| cached_input_tokens | 131,056,650,496 |
| output_tokens | 474,903,190 |
| reasoning_output_tokens | 145,418,333 |
| total_tokens | 136,758,989,596 |
| GPT-5.5 standard under-272K API-equivalent | $105,907.43 |
| GPT-5.5 long-context sensitivity upper bound | $204,691.32 |
| GPT-5.5 long-context + regional sensitivity upper bound | $225,160.45 |
| GPT-5.5 regional +10% sensitivity | $116,498.18 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 102,569,242,197 |
| all-time 500-word printed pages | 205,138,484 |
| all-time 80k-word books | 1,282,115 |
| continuous reading at 250 wpm | 780.59 years |
| 8h/day reading at 250 wpm | 2,341.76 years |
| all-time $60 game equivalents | 1,765.12 |
| all-time $2k workstation equivalents | 52.95 |
| cached input share | 96.17% |
| since previous snapshot approximate words | 242,327,149 |
| since previous snapshot 500-word pages | 484,654 |
| current burn approximate words / second | 32,719.58 |
| current burn pages / hour | 235,580.96 |
| current burn GPT-5.5 standard $ / day | $3,170.71 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-06-05.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-06-05T18:14:58.747000+00:00`
Changed JSONL files scanned: 56
Increment events after cutoff: 2,255
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 323,102,866 |
| input_tokens | 321,670,923 |
| cached_input_tokens | 306,560,128 |
| output_tokens | 1,431,943 |
| reasoning_output_tokens | 425,553 |
| GPT-5.5 standard $ | $271.79 |
| primary C# lines | 495 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 157,053,973.91 |
| total tokens / second | 43,626.10 |
| GPT-5.5 standard $ / hour | $132.11 |
| primary C# lines / hour | 240.61 |
| tokens / net primary C# line | 652,733.06 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
