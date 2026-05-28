# TOKEN USAGE AUDIT FAST REFRESH 2026-05-28

Generated UTC: 2026-05-28T09:04:42.411098+00:00
Generated Samara: 2026-05-28T13:04:42.411098+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,013 |
| sessions_with_usage | 2,873 |
| input_tokens | 110,475,153,995 |
| cached_input_tokens | 106,153,131,008 |
| output_tokens | 384,227,358 |
| reasoning_output_tokens | 120,535,145 |
| total_tokens | 110,860,414,953 |
| GPT-5.5 standard under-272K API-equivalent | $86,213.50 |
| GPT-5.5 long-context sensitivity upper bound | $166,663.59 |
| GPT-5.5 long-context + regional sensitivity upper bound | $183,329.95 |
| GPT-5.5 regional +10% sensitivity | $94,834.85 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-27.json`
Cutoff UTC: `2026-05-27T19:04:43.056000+00:00`
Changed JSONL files scanned: 80
Increment events after cutoff: 16,265
Post-cutoff long-context-like increment events: 0

| Metric | Delta |
|---|---:|
| total_tokens | 2,616,027,410 |
| input_tokens | 2,606,325,783 |
| cached_input_tokens | 2,510,593,408 |
| output_tokens | 9,701,627 |
| reasoning_output_tokens | 2,836,269 |
| GPT-5.5 standard $ | $2,025.01 |
| primary C# lines | 23,883 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 186,601,807.35 |
| total tokens / second | 51,833.84 |
| GPT-5.5 standard $ / hour | $144.44 |
| primary C# lines / hour | 1,703.58 |
| tokens / net primary C# line | 109,535.13 |
| post-cutoff long-context surcharge delta | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the report includes a separate upper-bound sensitivity.
