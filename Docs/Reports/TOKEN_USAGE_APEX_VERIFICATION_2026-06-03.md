# Token Usage Apex Verification 2026-06-03

Generated Samara: `2026-06-03T21:49:53.829603+04:00`
Evidence class: `STATIC_SOURCE_AND_STATIC_DOC_PLUS_PYTHON_BYTECODE_COMPILE`

## Verdict

| Claim | Status | Evidence |
|---|---|---|
| Runtime hot-path changed | `False` | owned runtime C# file list is empty |
| Runtime 0 B/frame | `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM` | no profiler/GCMonitor run |
| C# hot forbidden text hits in owned tooling | `0` | regex scan |
| DataVault migration | `False` | route scan |
| Chart count | `112` | PNG scan |
| PNG signatures ok | `True` | binary signature check |
| Chart manifest exact match | `True` | dashboard paths vs disk paths |

## Token Headline

| Metric | Value |
|---|---:|
| total_tokens | 134785731124 |
| input_tokens | 134319293029 |
| cached_input_tokens | 129184906624 |
| output_tokens | 465404495 |
| reasoning_output_tokens | 142978406 |
| sessions_with_usage | 3141 |
| gpt_5_5_standard_api_equivalent_usd | 104226.520187 |
| delta_total_tokens | 5153749750 |
| tokens_per_hour | 174474951.4074727 |
| tokens_per_second | 48465.26427985353 |
| gpt_5_5_standard_usd_per_hour | 135.99534053328887 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 201471.972949 |
| gpt_5_5_long_context_upper_bound_delta_usd | 97245.45276199999 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 221619.1702439 |
| gpt_5_5_regional_10pct_usd | 114649.1722057 |
| gpt_5_5_regional_10pct_delta_usd | 10422.652018699999 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |
| post_cutoff_long_context_event_evidence_class | LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION |

## Compilation Resource Throttling

| Metric | Value |
|---|---|
| dotnet_build_invoked_by_token_usage_audit | `False` |
| unity_build_invoked_by_token_usage_audit | `False` |
| final_compile_check | `python -m py_compile Tools/CodexTokenUsageAudit_20260525.py Tools/CodexTokenUsageFastRefresh_20260528.py Tools/ProjectMetricsDashboard_20260528.py Tools/TokenUsageApexVerification_20260528.py` |
| cpu_total_percent | `12` |
| dotnet_or_csc_process_count | `0` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-03.json` | `ac39ab6c922f4aba16e025a77d1ca4eeaa242d56927146410db91ca2cf76e439` | 570995 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-03.md` | `9543f3271658182f78f20245967ffecb2c4112f14a5ad22d060d49fcecbd1bc7` | 3226 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-03.json` | `35459096b470b9c09abe58b2342cd7d7c1db77ebf4d1b13f293b2fe79121b427` | 177357 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-03.md` | `22292d6992fdc9837c332bb10214509880e8e2167fc23a9bba2805b1f720fc92` | 29082 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `6169f6f3b5a8155d50f1261aec5f0abd26744831e1c419f784bd2f110c31f5fd` | 582 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `9fb5a0ed25888dacc7f111b7a2dc9ac4ad9611e8cef4882600cdb6150128947a` | 892 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `a7b6d7778f242dfe6035c1bb86ad6905408ab14c1d4253f036b019a70463e05d` | 508 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-06-03 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
