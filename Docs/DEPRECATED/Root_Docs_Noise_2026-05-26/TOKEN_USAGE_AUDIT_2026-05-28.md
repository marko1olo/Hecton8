# TOKEN USAGE AUDIT FAST REFRESH 2026-05-28

Generated UTC: 2026-05-28T17:48:33.436100+00:00
Generated Samara: 2026-05-28T21:48:33.436100+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,057 |
| sessions_with_usage | 2,917 |
| input_tokens | 112,898,228,022 |
| cached_input_tokens | 108,483,193,216 |
| output_tokens | 393,246,422 |
| reasoning_output_tokens | 122,945,123 |
| total_tokens | 113,292,508,044 |
| GPT-5.5 standard under-272K API-equivalent | $88,114.16 |
| GPT-5.5 long-context sensitivity upper bound | $170,329.63 |
| GPT-5.5 long-context + regional sensitivity upper bound | $187,362.59 |
| GPT-5.5 regional +10% sensitivity | $96,925.58 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-28.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-05-28T09:46:46.902000+00:00`
Changed JSONL files scanned: 63
Increment events after cutoff: 13,740
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 2,233,113,498 |
| input_tokens | 2,224,806,098 |
| cached_input_tokens | 2,139,582,464 |
| output_tokens | 8,307,400 |
| reasoning_output_tokens | 2,213,466 |
| GPT-5.5 standard $ | $1,745.13 |
| primary C# lines | 16,093 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 278,065,144.81 |
| total tokens / second | 77,240.32 |
| GPT-5.5 standard $ / hour | $217.30 |
| primary C# lines / hour | 2,003.88 |
| tokens / net primary C# line | 138,763.03 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
