# Token Usage Apex Verification 2026-05-28

Generated Samara: `2026-05-28T13:10:26.935550+04:00`
Evidence class: `STATIC_SOURCE_AND_STATIC_DOC_PLUS_PYTHON_BYTECODE_COMPILE`

## Verdict

| Claim | Status | Evidence |
|---|---|---|
| Runtime hot-path changed | `False` | owned runtime C# file list is empty |
| Runtime 0 B/frame | `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM` | no profiler/GCMonitor run |
| C# hot forbidden text hits in owned tooling | `0` | regex scan |
| DataVault migration | `False` | route scan |
| Chart count | `29` | PNG scan |
| PNG signatures ok | `True` | binary signature check |

## Token Headline

| Metric | Value |
|---|---:|
| total_tokens | 110860414953 |
| input_tokens | 110475153995 |
| cached_input_tokens | 106153131008 |
| output_tokens | 384227358 |
| reasoning_output_tokens | 120535145 |
| sessions_with_usage | 2873 |
| gpt_5_5_standard_api_equivalent_usd | 86213.501179 |
| delta_total_tokens | 2616027410 |
| tokens_per_hour | 186601807.34837893 |
| tokens_per_second | 51833.8353745497 |
| gpt_5_5_standard_usd_per_hour | 144.44421997903413 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 166663.591988 |
| gpt_5_5_long_context_upper_bound_delta_usd | 80450.090809 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 183329.95118680003 |
| gpt_5_5_regional_10pct_usd | 94834.85129690001 |
| gpt_5_5_regional_10pct_delta_usd | 8621.350117900016 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.json` | `5d9ebf158a8d232aebc4bc170713f6511f83d8a2d438d34bea4c6f317822099d` | 564332 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.md` | `92a9ca02f8f29daeb4170da1bc3556991dc1e17fb6939d766bf02aa1d0f801b0` | 2213 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.json` | `cb569ca85d09b2cc6d15c1371d646f60850ee1ef52829fd7631e9c8f6d43ed17` | 72060 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.md` | `babf47b89dcdf9bdca7c534065cbe1b3c42834693045d1e71fab87d50cbb82eb` | 7067 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `ab25b7406a38f27b454b585b0bc15b11f684cc57fd05e167db9a2151dddd790d` | 494 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `06c9c29e4e8dbd75b004f1eecc893355713163ccf8b0ed60e6058b15c0a8e330` | 496 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `e3fa9102be2294c56902a38cb82af92ce3a7b11e59ed5edc1af5d52a87c3df84` | 398 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-28 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
