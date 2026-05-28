# Token Usage Apex Verification 2026-05-28

Generated Samara: `2026-05-28T13:23:51.945932+04:00`
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
| total_tokens | 110930291612 |
| input_tokens | 110544782904 |
| cached_input_tokens | 106219997056 |
| output_tokens | 384475108 |
| reasoning_output_tokens | 120609066 |
| sessions_with_usage | 2874 |
| gpt_5_5_standard_api_equivalent_usd | 86268.181008 |
| delta_total_tokens | 69876659 |
| tokens_per_hour | 286959768.73930275 |
| tokens_per_second | 79711.04687202854 |
| gpt_5_5_standard_usd_per_hour | 224.5515356500487 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 166769.23539599997 |
| gpt_5_5_long_context_upper_bound_delta_usd | 80501.05438799998 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 183446.1589356 |
| gpt_5_5_regional_10pct_usd | 94894.99910880001 |
| gpt_5_5_regional_10pct_delta_usd | 8626.81810080001 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.json` | `a239c3d7e8fd2ffcdc3f8a62c296c9f2eae94fcfe45f0ddc7a2abd153ecceb2f` | 564418 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.md` | `9a3ac695b4d2e81dabf7499eace39238f9352e0de8f764935c017a8212cb1fd2` | 2242 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.json` | `6f3266df1c926a938c0e86341a0d2abb621c012cdf36ead386e9596cb5211a8d` | 72072 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.md` | `f5a632c7b417967228add9fe4cafa0370184506f62c8de9a0bea4c2b002c8766` | 7067 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `c8b1fc587fc1467c2cb2581015982ff454dfc64e72abe9732b282581b95b5c81` | 500 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `06c9c29e4e8dbd75b004f1eecc893355713163ccf8b0ed60e6058b15c0a8e330` | 496 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `e3fa9102be2294c56902a38cb82af92ce3a7b11e59ed5edc1af5d52a87c3df84` | 398 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-28 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
