# Token Usage Apex Verification 2026-06-02

Generated Samara: `2026-06-02T13:06:28.731063+04:00`
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
| total_tokens | 129368782512 |
| input_tokens | 128922250049 |
| cached_input_tokens | 123992808192 |
| output_tokens | 445498863 |
| reasoning_output_tokens | 137440455 |
| sessions_with_usage | 3080 |
| gpt_5_5_standard_api_equivalent_usd | 100008.579271 |
| delta_total_tokens | 4842709564 |
| tokens_per_hour | 108306438.04170771 |
| tokens_per_second | 30085.12167825214 |
| gpt_5_5_standard_usd_per_hour | 81.89241144349457 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 193334.675597 |
| gpt_5_5_long_context_upper_bound_delta_usd | 93326.096326 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 212668.1431567 |
| gpt_5_5_regional_10pct_usd | 110009.4371981 |
| gpt_5_5_regional_10pct_delta_usd | 10000.857927100005 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |
| post_cutoff_long_context_event_evidence_class | LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION |

## Compilation Resource Throttling

| Metric | Value |
|---|---|
| dotnet_build_invoked_by_token_usage_audit | `False` |
| unity_build_invoked_by_token_usage_audit | `False` |
| final_compile_check | `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION` |
| cpu_total_percent | `52` |
| dotnet_or_csc_process_count | `3` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-02.json` | `78d27465706265fe73c2ccf0c21e1cbd227d85f5fe7de94b80c329f157f59c76` | 569539 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-02.md` | `c20df2963e0d5eaa9c41c582d75194d9cebc173f45ec55ec8a0d03c6500c32c6` | 3224 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-02.json` | `16daeb1c4a42d53b90f9b29b9fcd520fea1e9e9a38f4fcdd769d09deae702984` | 172138 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-02.md` | `9b60536a57f783d81b76a3c8853af249e682f898ee9d1d541b8c7856700089ab` | 29081 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `6169f6f3b5a8155d50f1261aec5f0abd26744831e1c419f784bd2f110c31f5fd` | 582 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `9fb5a0ed25888dacc7f111b7a2dc9ac4ad9611e8cef4882600cdb6150128947a` | 892 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `a7b6d7778f242dfe6035c1bb86ad6905408ab14c1d4253f036b019a70463e05d` | 508 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-06-02 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
