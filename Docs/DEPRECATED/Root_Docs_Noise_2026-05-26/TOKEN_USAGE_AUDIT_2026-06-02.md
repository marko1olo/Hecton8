# TOKEN USAGE AUDIT FAST REFRESH 2026-06-02

Generated UTC: 2026-06-02T12:11:04.960407+00:00
Generated Samara: 2026-06-02T16:11:04.960407+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,229 |
| sessions_with_usage | 3,080 |
| input_tokens | 129,184,482,782 |
| cached_input_tokens | 124,245,990,784 |
| output_tokens | 446,464,992 |
| reasoning_output_tokens | 137,637,121 |
| total_tokens | 129,631,981,374 |
| GPT-5.5 standard under-272K API-equivalent | $100,209.41 |
| GPT-5.5 long-context sensitivity upper bound | $193,721.84 |
| GPT-5.5 long-context + regional sensitivity upper bound | $213,094.02 |
| GPT-5.5 regional +10% sensitivity | $110,230.35 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 97,223,986,030 |
| all-time 500-word printed pages | 194,447,972 |
| all-time 80k-word books | 1,215,299 |
| continuous reading at 250 wpm | 739.91 years |
| 8h/day reading at 250 wpm | 2,219.73 years |
| all-time $60 game equivalents | 1,670.16 |
| all-time $2k workstation equivalents | 50.10 |
| cached input share | 96.18% |
| since previous snapshot approximate words | 197,399,146 |
| since previous snapshot 500-word pages | 394,798 |
| current burn approximate words / second | 16,916.60 |
| current burn pages / hour | 121,799.55 |
| current burn GPT-5.5 standard $ / day | $1,486.97 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-06-02.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-06-02T08:56:35.485000+00:00`
Changed JSONL files scanned: 7
Increment events after cutoff: 1,563
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 263,198,862 |
| input_tokens | 262,232,733 |
| cached_input_tokens | 253,182,592 |
| output_tokens | 966,129 |
| reasoning_output_tokens | 196,666 |
| GPT-5.5 standard $ | $200.83 |
| primary C# lines | 573 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 81,199,699.19 |
| total tokens / second | 22,555.47 |
| GPT-5.5 standard $ / hour | $61.96 |
| primary C# lines / hour | 176.78 |
| tokens / net primary C# line | 459,334.84 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
