# TOKEN USAGE AUDIT FAST REFRESH 2026-05-28

Generated UTC: 2026-05-28T09:19:19.035628+00:00
Generated Samara: 2026-05-28T13:19:19.035628+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,014 |
| sessions_with_usage | 2,874 |
| input_tokens | 110,544,782,904 |
| cached_input_tokens | 106,219,997,056 |
| output_tokens | 384,475,108 |
| reasoning_output_tokens | 120,609,066 |
| total_tokens | 110,930,291,612 |
| GPT-5.5 standard under-272K API-equivalent | $86,268.18 |
| GPT-5.5 long-context sensitivity upper bound | $166,769.24 |
| GPT-5.5 long-context + regional sensitivity upper bound | $183,446.16 |
| GPT-5.5 regional +10% sensitivity | $94,895.00 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-28.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-05-28T09:04:41.985000+00:00`
Changed JSONL files scanned: 22
Increment events after cutoff: 429
Post-cutoff long-context-like increment events: 0

| Metric | Delta |
|---|---:|
| total_tokens | 69,876,659 |
| input_tokens | 69,628,909 |
| cached_input_tokens | 66,866,048 |
| output_tokens | 247,750 |
| reasoning_output_tokens | 73,921 |
| GPT-5.5 standard $ | $54.68 |
| primary C# lines | 487 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 286,959,768.74 |
| total tokens / second | 79,711.05 |
| GPT-5.5 standard $ / hour | $224.55 |
| primary C# lines / hour | 1,999.94 |
| tokens / net primary C# line | 143,483.90 |
| post-cutoff long-context surcharge delta | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the report includes a separate upper-bound sensitivity.
