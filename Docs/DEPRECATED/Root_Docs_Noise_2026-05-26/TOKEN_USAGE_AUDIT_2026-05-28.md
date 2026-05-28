# TOKEN USAGE AUDIT FAST REFRESH 2026-05-28

Generated UTC: 2026-05-28T08:46:28.773466+00:00
Generated Samara: 2026-05-28T12:46:28.773466+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,011 |
| sessions_with_usage | 2,871 |
| input_tokens | 110,390,552,320 |
| cached_input_tokens | 106,072,714,368 |
| output_tokens | 383,928,858 |
| reasoning_output_tokens | 120,444,690 |
| total_tokens | 110,775,514,778 |
| GPT-5.5 standard under-272K API-equivalent | $86,143.41 |
| GPT-5.5 long-context sensitivity upper bound | $166,527.89 |
| GPT-5.5 regional +10% sensitivity | $94,757.75 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-27.json`
Cutoff UTC: `2026-05-27T19:04:43.056000+00:00`
Changed JSONL files scanned: 78
Increment events after cutoff: 15,772

| Metric | Delta |
|---|---:|
| total_tokens | 2,531,127,235 |
| input_tokens | 2,521,724,108 |
| cached_input_tokens | 2,430,176,768 |
| output_tokens | 9,403,127 |
| reasoning_output_tokens | 2,745,814 |
| GPT-5.5 standard $ | $1,954.92 |
| primary C# lines | 23,657 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 184,544,812.69 |
| total tokens / second | 51,262.45 |
| GPT-5.5 standard $ / hour | $142.53 |
| primary C# lines / hour | 1,724.83 |
| tokens / net primary C# line | 106,992.74 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the report includes a separate upper-bound sensitivity.
