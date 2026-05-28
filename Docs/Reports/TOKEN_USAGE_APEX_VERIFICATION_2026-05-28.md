# Token Usage Apex Verification 2026-05-28

Generated Samara: `2026-05-28T13:55:58.884213+04:00`
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
| Chart manifest exact match | `True` | dashboard paths vs disk paths |

## Token Headline

| Metric | Value |
|---|---:|
| total_tokens | 111059394546 |
| input_tokens | 110673421924 |
| cached_input_tokens | 106343610752 |
| output_tokens | 384939022 |
| reasoning_output_tokens | 120731657 |
| sessions_with_usage | 2878 |
| gpt_5_5_standard_api_equivalent_usd | 86369.031896 |
| delta_total_tokens | 129102934 |
| tokens_per_hour | 282851878.21349716 |
| tokens_per_second | 78569.96617041588 |
| gpt_5_5_standard_usd_per_hour | 220.95441371068515 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 166963.978462 |
| gpt_5_5_long_context_upper_bound_delta_usd | 80594.946566 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 183660.3763082 |
| gpt_5_5_regional_10pct_usd | 95005.93508560001 |
| gpt_5_5_regional_10pct_delta_usd | 8636.903189600009 |
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
| dotnet_or_csc_process_count | `2` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.json` | `1bd5fe9b1cc5c22fe82a8b97a26216af25c0f82b388f91819274a95d8a04afa9` | 564624 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.md` | `7d83f4d6234ae89a5159a4442f612ccc6fb88020318e49582afed473958f31d8` | 2361 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.json` | `6c0cdbbdc4ad360c1e4d8d1db73136b240f06d0d61ee6a74db8455cd6f4aa7f8` | 72284 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.md` | `e8f771905e1b809f9fc53c3492ccebe2f4dc78a4e08950fe41b01586d7aaa7c4` | 7363 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `43acce49130ac370c429dfedc655e77bf67b53245ece8abef617e2522874e685` | 501 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `a24a683676d0519f86e211c5ab415982cc9b5a2d34de9f999524ced3a869bacc` | 498 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `c6e91e6b54a90031aae54554d24e84ce9be43f6fa05d1cd252f970fb47559314` | 444 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-28 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
