# TOKEN USAGE AUDIT FAST REFRESH 2026-06-02

Generated UTC: 2026-06-02T08:56:36.002115+00:00
Generated Samara: 2026-06-02T12:56:36.002115+04:00
Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.

## Totals

| Metric | Value |
|---|---:|
| file_count | 3,229 |
| sessions_with_usage | 3,080 |
| input_tokens | 128,922,250,049 |
| cached_input_tokens | 123,992,808,192 |
| output_tokens | 445,498,863 |
| reasoning_output_tokens | 137,440,455 |
| total_tokens | 129,368,782,512 |
| GPT-5.5 standard under-272K API-equivalent | $100,008.58 |
| GPT-5.5 long-context sensitivity upper bound | $193,334.68 |
| GPT-5.5 long-context + regional sensitivity upper bound | $212,668.14 |
| GPT-5.5 regional +10% sensitivity | $110,009.44 |

## Scale For Non-Specialists

These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.

| Metric | Value |
|---|---:|
| all-time approximate words | 97,026,586,884 |
| all-time 500-word printed pages | 194,053,173 |
| all-time 80k-word books | 1,212,832 |
| continuous reading at 250 wpm | 738.41 years |
| 8h/day reading at 250 wpm | 2,215.22 years |
| all-time $60 game equivalents | 1,666.81 |
| all-time $2k workstation equivalents | 50.00 |
| cached input share | 96.18% |
| since previous snapshot approximate words | 3,632,032,173 |
| since previous snapshot 500-word pages | 7,264,064 |
| current burn approximate words / second | 22,563.84 |
| current burn pages / hour | 162,459.66 |
| current burn GPT-5.5 standard $ / day | $1,965.42 |

## Increment Since Previous Snapshot

Previous report: `C:\hades\Hecton8\Docs\DEPRECATED\Root_Docs_Noise_2026-05-26\TOKEN_USAGE_AUDIT_2026-05-31.json`
Previous snapshot mode: `prior_dated_report`
Cutoff UTC: `2026-05-31T12:13:48.703000+00:00`
Changed JSONL files scanned: 61
Increment events after cutoff: 29,719
Post-cutoff long-context delta events detected (lower-bound): 0

| Metric | Delta |
|---|---:|
| total_tokens | 4,842,709,564 |
| input_tokens | 4,826,271,326 |
| cached_input_tokens | 4,658,410,112 |
| output_tokens | 16,438,238 |
| reasoning_output_tokens | 4,602,339 |
| GPT-5.5 standard $ | $3,661.66 |
| primary C# lines | 74,196 |

## Velocity

| Metric | Value |
|---|---:|
| total tokens / hour | 108,306,438.04 |
| total tokens / second | 30,085.12 |
| GPT-5.5 standard $ / hour | $81.89 |
| primary C# lines / hour | 1,659.38 |
| tokens / net primary C# line | 65,269.15 |
| post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |

## Residual Risk

- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.
- It inherits older all-time dimensions from the previous full snapshot.
- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.
- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.
