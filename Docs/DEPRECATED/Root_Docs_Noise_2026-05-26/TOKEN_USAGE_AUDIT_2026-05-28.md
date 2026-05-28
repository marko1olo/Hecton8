# TOKEN USAGE AUDIT FAST REFRESH 2026-05-28

Generated UTC: 2026-05-28T09:46:42.194462+00:00
Generated Samara: 2026-05-28T13:46:42.194462+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,018 |
| sessions_with_usage | 2,878 |
| input_tokens | 110,673,421,924 |
| cached_input_tokens | 106,343,610,752 |
| output_tokens | 384,939,022 |
| reasoning_output_tokens | 120,731,657 |
| total_tokens | 111,059,394,546 |
| GPT-5.5 standard under-272K API-equivalent | $86,369.03 |
| GPT-5.5 long-context sensitivity upper bound | $166,963.98 |
| GPT-5.5 long-context + regional sensitivity upper bound | $183,660.38 |
| GPT-5.5 regional +10% sensitivity | $95,005.94 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-28.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-05-28T09:19:18.799000+00:00`
Changed JSONL files scanned: 24
Increment events after cutoff: 824
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 129,102,934 |
| input_tokens | 128,639,020 |
| cached_input_tokens | 123,613,696 |
| output_tokens | 463,914 |
| reasoning_output_tokens | 122,591 |
| GPT-5.5 standard $ | $100.85 |
| primary C# lines | 818 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 282,851,878.21 |
| total tokens / second | 78,569.97 |
| GPT-5.5 standard $ / hour | $220.95 |
| primary C# lines / hour | 1,792.16 |
| tokens / net primary C# line | 157,827.55 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
