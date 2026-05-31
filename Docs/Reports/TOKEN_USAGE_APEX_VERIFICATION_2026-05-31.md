# Token Usage Apex Verification 2026-05-31

Generated Samara: `2026-05-31T16:30:36.853742+04:00`
Evidence class: `STATIC_SOURCE_AND_STATIC_DOC_CPU_THROTTLE_NO_COMPILE`

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
| total_tokens | 124526072948 |
| input_tokens | 124095978723 |
| cached_input_tokens | 119334398080 |
| output_tokens | 429060625 |
| reasoning_output_tokens | 132838116 |
| sessions_with_usage | 3027 |
| gpt_5_5_standard_api_equivalent_usd | 96346.92100500001 |
| delta_total_tokens | 20832603 |
| tokens_per_hour | 74577964.61222722 |
| tokens_per_second | 20716.101281174226 |
| gpt_5_5_standard_usd_per_hour | 58.631597443780564 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 186257.932635 |
| gpt_5_5_long_context_upper_bound_delta_usd | 89911.01163 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 204883.7258985 |
| gpt_5_5_regional_10pct_usd | 105981.61310550002 |
| gpt_5_5_regional_10pct_delta_usd | 9634.692100500004 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |
| post_cutoff_long_context_event_evidence_class | LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION |

## Compilation Resource Throttling

| Metric | Value |
|---|---|
| dotnet_build_invoked_by_token_usage_audit | `False` |
| unity_build_invoked_by_token_usage_audit | `False` |
| final_compile_check | `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION` |
| cpu_total_percent | `100` |
| dotnet_or_csc_process_count | `1` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-31.json` | `5a338f37ef4be0ed31bc3147205fb47f41766e37103f0c78d7903ea72fd7bcc5` | 566827 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-31.md` | `02b2d954389dc121ee2ea9286311299fdf349587b8aeddc44e782d808a015fa0` | 3194 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-31.json` | `6500e501e6eff5c778631a24c3b0eaee17ad1c997996bc6eeb3bd2fb9685a1c0` | 168786 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-31.md` | `4e199d27f33c1621f343680a90192398d69fdd70d22c7614b583ea23ea62b986` | 29077 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `d6d3164de56ef0c16029135b969a1201438def79f5c565b52a52128a3e6bda1a` | 589 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `9fb5a0ed25888dacc7f111b7a2dc9ac4ad9611e8cef4882600cdb6150128947a` | 892 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `a7b6d7778f242dfe6035c1bb86ad6905408ab14c1d4253f036b019a70463e05d` | 508 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-31 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
