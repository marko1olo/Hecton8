# TOKEN USAGE AUDIT FAST REFRESH 2026-06-05

Generated UTC: 2026-06-05T18:14:58.423520+00:00
Generated Samara: 2026-06-05T22:14:58.423520+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 1,339 |
| sessions_with_usage | 3,412 |
| input_tokens | 135,961,381,883 |
| cached_input_tokens | 130,750,090,368 |
| output_tokens | 473,471,247 |
| reasoning_output_tokens | 144,992,780 |
| total_tokens | 136,435,886,730 |
| GPT-5.5 standard under-272K API-equivalent | $105,635.64 |
| GPT-5.5 long-context sensitivity upper bound | $204,169.21 |
| GPT-5.5 long-context + regional sensitivity upper bound | $224,586.13 |
| GPT-5.5 regional +10% sensitivity | $116,199.20 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 102,326,915,047 |
| all-time 500-word printed pages | 204,653,830 |
| all-time 80k-word books | 1,279,086 |
| continuous reading at 250 wpm | 778.74 years |
| 8h/day reading at 250 wpm | 2,336.23 years |
| all-time $60 game equivalents | 1,760.59 |
| all-time $2k workstation equivalents | 52.82 |
| cached input share | 96.17% |
| since previous snapshot approximate words | 1,237,616,704 |
| since previous snapshot 500-word pages | 2,475,233 |
| current burn approximate words / second | 7,084.47 |
| current burn pages / hour | 51,008.16 |
| current burn GPT-5.5 standard $ / day | $696.92 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-06-03.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-06-03T17:43:54.960000+00:00`
Changed JSONL files scanned: 290
Increment events after cutoff: 11,242
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 1,650,155,606 |
| input_tokens | 1,642,088,854 |
| cached_input_tokens | 1,565,183,744 |
| output_tokens | 8,066,752 |
| reasoning_output_tokens | 2,014,374 |
| GPT-5.5 standard $ | $1,409.12 |
| primary C# lines | 15,020 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 34,005,437.19 |
| total tokens / second | 9,445.95 |
| GPT-5.5 standard $ / hour | $29.04 |
| primary C# lines / hour | 309.52 |
| tokens / net primary C# line | 109,863.89 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
