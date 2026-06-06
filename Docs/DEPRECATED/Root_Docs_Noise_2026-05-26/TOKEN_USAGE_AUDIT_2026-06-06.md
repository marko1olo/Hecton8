# TOKEN USAGE AUDIT FAST REFRESH 2026-06-06

Generated UTC: 2026-06-06T10:13:50.605808+00:00
Generated Samara: 2026-06-06T14:13:50.605808+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 1,563 |
| sessions_with_usage | 3,635 |
| input_tokens | 138,427,944,497 |
| cached_input_tokens | 133,102,804,608 |
| output_tokens | 483,264,799 |
| reasoning_output_tokens | 147,905,141 |
| total_tokens | 138,912,242,896 |
| GPT-5.5 standard under-272K API-equivalent | $107,675.05 |
| GPT-5.5 long-context sensitivity upper bound | $208,101.12 |
| GPT-5.5 long-context + regional sensitivity upper bound | $228,911.23 |
| GPT-5.5 regional +10% sensitivity | $118,442.55 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 104,184,182,172 |
| all-time 500-word printed pages | 208,368,364 |
| all-time 80k-word books | 1,302,302 |
| continuous reading at 250 wpm | 792.88 years |
| 8h/day reading at 250 wpm | 2,378.63 years |
| all-time $60 game equivalents | 1,794.58 |
| all-time $2k workstation equivalents | 53.84 |
| cached input share | 96.15% |
| since previous snapshot approximate words | 274,219,814 |
| since previous snapshot 500-word pages | 548,439 |
| current burn approximate words / second | 28,560.70 |
| current burn pages / hour | 205,637.04 |
| current burn GPT-5.5 standard $ / day | $2,758.98 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-06-06.json`
Previous snapshot mode: `same_day_existing_report`
Cutoff UTC: `2026-06-06T07:34:08.066000+00:00`
Changed JSONL files scanned: 44
Increment events after cutoff: 2,508
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 365,626,419 |
| input_tokens | 364,157,985 |
| cached_input_tokens | 346,277,248 |
| output_tokens | 1,468,434 |
| reasoning_output_tokens | 454,002 |
| GPT-5.5 standard $ | $306.60 |
| primary C# lines | 327 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 137,091,361.38 |
| total tokens / second | 38,080.93 |
| GPT-5.5 standard $ / hour | $114.96 |
| primary C# lines / hour | 122.61 |
| tokens / net primary C# line | 1,118,123.61 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
