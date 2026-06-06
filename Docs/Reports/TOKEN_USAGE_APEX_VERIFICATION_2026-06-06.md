# Token Usage Apex Verification 2026-06-06

Generated Samara: `2026-06-06T15:15:28.864060+04:00`
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
| total_tokens | 138912242896 |
| input_tokens | 138427944497 |
| cached_input_tokens | 133102804608 |
| output_tokens | 483264799 |
| reasoning_output_tokens | 147905141 |
| sessions_with_usage | 3635 |
| gpt_5_5_standard_api_equivalent_usd | 107675.04571899999 |
| delta_total_tokens | 365626419 |
| tokens_per_hour | 137091361.37707222 |
| tokens_per_second | 38080.933715853396 |
| gpt_5_5_standard_usd_per_hour | 114.9576968738132 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 208101.119453 |
| gpt_5_5_long_context_upper_bound_delta_usd | 100426.073734 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 228911.2313983 |
| gpt_5_5_regional_10pct_usd | 118442.5502909 |
| gpt_5_5_regional_10pct_delta_usd | 10767.504571900019 |
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
| dotnet_or_csc_process_count | `0` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-06.json` | `45d00b9f6c6374fd9e047be26eee1b91777424f0d55074219667afd8bf17c252` | 565665 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-06.md` | `f8be665e29fb08882ea8131a2e3dd92a0d3a94b9efcd689058fd50363bf9b246` | 3214 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-06.json` | `61af91c59a2537e841d95899ae73569318856214aaa32dc48060ffa00279eabe` | 150235 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-06.md` | `77b1a7e4e9d233dfe19c334dc002306da1613b57136a67297ab2f6a557171502` | 29080 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `dae45fcd7cb69f19ff5677ae8b01535f31a43a61cf6108c81f65204e460126de` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `6169f6f3b5a8155d50f1261aec5f0abd26744831e1c419f784bd2f110c31f5fd` | 582 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `9fb5a0ed25888dacc7f111b7a2dc9ac4ad9611e8cef4882600cdb6150128947a` | 892 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `be8115b9c34e108aa081e78cd320f7cffee802dc5caeb345fd9f19380c8474f5` | 508 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-06-06 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
