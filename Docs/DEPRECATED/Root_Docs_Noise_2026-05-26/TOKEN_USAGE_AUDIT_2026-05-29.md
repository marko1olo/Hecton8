# TOKEN USAGE AUDIT FAST REFRESH 2026-05-29

Generated UTC: 2026-05-29T15:38:31.176031+00:00
Generated Samara: 2026-05-29T19:38:31.176031+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,097 |
| sessions_with_usage | 2,952 |
| input_tokens | 117,275,879,242 |
| cached_input_tokens | 112,724,686,720 |
| output_tokens | 407,875,827 |
| reasoning_output_tokens | 127,100,543 |
| total_tokens | 117,684,788,669 |
| GPT-5.5 standard under-272K API-equivalent | $91,354.58 |
| GPT-5.5 long-context sensitivity upper bound | $176,591.02 |
| GPT-5.5 long-context + regional sensitivity upper bound | $194,250.13 |
| GPT-5.5 regional +10% sensitivity | $100,490.04 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-29.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-05-29T10:26:45.384000+00:00`
Changed JSONL files scanned: 23
Increment events after cutoff: 8,526
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 1,442,071,455 |
| input_tokens | 1,437,482,569 |
| cached_input_tokens | 1,397,314,816 |
| output_tokens | 4,588,886 |
| reasoning_output_tokens | 1,277,938 |
| GPT-5.5 standard $ | $1,037.16 |
| primary C# lines | 7,819 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 277,255,074.41 |
| total tokens / second | 77,015.30 |
| GPT-5.5 standard $ / hour | $199.41 |
| primary C# lines / hour | 1,503.29 |
| tokens / net primary C# line | 184,431.70 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
