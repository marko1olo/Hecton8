# TOKEN USAGE AUDIT FAST REFRESH 2026-05-30

Generated UTC: 2026-05-30T08:10:27.525790+00:00
Generated Samara: 2026-05-30T12:10:27.525790+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,130 |
| sessions_with_usage | 2,985 |
| input_tokens | 120,697,236,985 |
| cached_input_tokens | 116,046,101,376 |
| output_tokens | 418,490,206 |
| reasoning_output_tokens | 129,954,328 |
| total_tokens | 121,116,760,791 |
| GPT-5.5 standard under-272K API-equivalent | $93,833.43 |
| GPT-5.5 long-context sensitivity upper bound | $181,389.52 |
| GPT-5.5 long-context + regional sensitivity upper bound | $199,528.47 |
| GPT-5.5 regional +10% sensitivity | $103,216.78 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-29.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-05-29T15:38:45.744000+00:00`
Changed JSONL files scanned: 49
Increment events after cutoff: 19,728
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 3,431,972,122 |
| input_tokens | 3,421,357,743 |
| cached_input_tokens | 3,321,414,656 |
| output_tokens | 10,614,379 |
| reasoning_output_tokens | 2,853,785 |
| GPT-5.5 standard $ | $2,478.85 |
| primary C# lines | 17,856 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 207,591,690.17 |
| total tokens / second | 57,664.36 |
| GPT-5.5 standard $ / hour | $149.94 |
| primary C# lines / hour | 1,080.07 |
| tokens / net primary C# line | 192,202.74 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
