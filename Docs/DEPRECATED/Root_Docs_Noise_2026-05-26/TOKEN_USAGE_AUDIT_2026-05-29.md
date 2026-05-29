# TOKEN USAGE AUDIT FAST REFRESH 2026-05-29

Generated UTC: 2026-05-29T10:26:26.695518+00:00
Generated Samara: 2026-05-29T14:26:26.695518+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,087 |
| sessions_with_usage | 2,944 |
| input_tokens | 115,838,396,673 |
| cached_input_tokens | 111,327,371,904 |
| output_tokens | 403,286,941 |
| reasoning_output_tokens | 125,822,605 |
| total_tokens | 116,242,717,214 |
| GPT-5.5 standard under-272K API-equivalent | $90,317.42 |
| GPT-5.5 long-context sensitivity upper bound | $174,585.53 |
| GPT-5.5 long-context + regional sensitivity upper bound | $192,044.09 |
| GPT-5.5 regional +10% sensitivity | $99,349.16 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-28.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-05-28T17:48:53.426000+00:00`
Changed JSONL files scanned: 52
Increment events after cutoff: 17,586
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 2,950,209,170 |
| input_tokens | 2,940,168,651 |
| cached_input_tokens | 2,844,178,688 |
| output_tokens | 10,040,519 |
| reasoning_output_tokens | 2,877,482 |
| GPT-5.5 standard $ | $2,203.25 |
| primary C# lines | 16,902 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 177,387,252.93 |
| total tokens / second | 49,274.24 |
| GPT-5.5 standard $ / hour | $132.48 |
| primary C# lines / hour | 1,016.27 |
| tokens / net primary C# line | 174,547.93 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
