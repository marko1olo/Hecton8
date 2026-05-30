# TOKEN USAGE AUDIT FAST REFRESH 2026-05-30

Generated UTC: 2026-05-30T19:24:08.230194+00:00
Generated Samara: 2026-05-30T23:24:08.230194+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,151 |
| sessions_with_usage | 3,005 |
| input_tokens | 123,041,537,283 |
| cached_input_tokens | 118,317,848,064 |
| output_tokens | 425,802,228 |
| reasoning_output_tokens | 131,900,809 |
| total_tokens | 123,468,373,111 |
| GPT-5.5 standard under-272K API-equivalent | $95,551.44 |
| GPT-5.5 long-context sensitivity upper bound | $184,715.84 |
| GPT-5.5 long-context + regional sensitivity upper bound | $203,187.42 |
| GPT-5.5 regional +10% sensitivity | $105,106.58 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-30.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-05-30T08:10:49.673000+00:00`
Changed JSONL files scanned: 40
Increment events after cutoff: 13,547
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 2,351,612,320 |
| input_tokens | 2,344,300,298 |
| cached_input_tokens | 2,271,746,688 |
| output_tokens | 7,312,022 |
| reasoning_output_tokens | 1,946,481 |
| GPT-5.5 standard $ | $1,718.00 |
| primary C# lines | 14,765 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 209,442,276.60 |
| total tokens / second | 58,178.41 |
| GPT-5.5 standard $ / hour | $153.01 |
| primary C# lines / hour | 1,315.02 |
| tokens / net primary C# line | 159,269.37 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
