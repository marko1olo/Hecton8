# Token Usage Apex Verification 2026-06-02

Generated Samara: `2026-06-02T16:16:08.602807+04:00`
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
| total_tokens | 129631981374 |
| input_tokens | 129184482782 |
| cached_input_tokens | 124245990784 |
| output_tokens | 446464992 |
| reasoning_output_tokens | 137637121 |
| sessions_with_usage | 3080 |
| gpt_5_5_standard_api_equivalent_usd | 100209.405142 |
| delta_total_tokens | 263198862 |
| tokens_per_hour | 81199699.19248042 |
| tokens_per_second | 22555.471997911227 |
| gpt_5_5_standard_usd_per_hour | 61.956956011719164 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 193721.835404 |
| gpt_5_5_long_context_upper_bound_delta_usd | 93512.43026200001 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 213094.01894440004 |
| gpt_5_5_regional_10pct_usd | 110230.34565619999 |
| gpt_5_5_regional_10pct_delta_usd | 10020.940514199989 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |
| post_cutoff_long_context_event_evidence_class | LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION |

## Compilation Resource Throttling

| Metric | Value |
|---|---|
| dotnet_build_invoked_by_token_usage_audit | `False` |
| unity_build_invoked_by_token_usage_audit | `False` |
| final_compile_check | `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION` |
| cpu_total_percent | `11` |
| dotnet_or_csc_process_count | `2` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-02.json` | `2cb68f16673b08287239e798f20dc1626761faf42a9bbf0d740c6e8aca448486` | 569465 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-02.md` | `2c3670912d1917af9d943dad61994d9154c8c90689694e98d5b3388faf40c343` | 3206 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-02.json` | `dd7161171695c321238744f2b0d515e16b708372fd7312f026a269092d5d7370` | 176930 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-02.md` | `6c6983069acce85c5e2dc0514fced221c9ef3700883cbc5c67db69fad8dd2859` | 29078 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `6169f6f3b5a8155d50f1261aec5f0abd26744831e1c419f784bd2f110c31f5fd` | 582 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `9fb5a0ed25888dacc7f111b7a2dc9ac4ad9611e8cef4882600cdb6150128947a` | 892 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `a7b6d7778f242dfe6035c1bb86ad6905408ab14c1d4253f036b019a70463e05d` | 508 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-06-02 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
