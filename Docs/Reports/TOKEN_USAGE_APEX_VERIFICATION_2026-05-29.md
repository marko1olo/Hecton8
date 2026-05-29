# Token Usage Apex Verification 2026-05-29

Generated Samara: `2026-05-29T14:33:22.553293+04:00`
Evidence class: `STATIC_SOURCE_AND_STATIC_DOC_CPU_THROTTLE_NO_COMPILE`

## Verdict

| Claim | Status | Evidence |
|---|---|---|
| Runtime hot-path changed | `False` | owned runtime C# file list is empty |
| Runtime 0 B/frame | `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM` | no profiler/GCMonitor run |
| C# hot forbidden text hits in owned tooling | `0` | regex scan |
| DataVault migration | `False` | route scan |
| Chart count | `41` | PNG scan |
| PNG signatures ok | `True` | binary signature check |
| Chart manifest exact match | `True` | dashboard paths vs disk paths |

## Token Headline

| Metric | Value |
|---|---:|
| total_tokens | 116242717214 |
| input_tokens | 115838396673 |
| cached_input_tokens | 111327371904 |
| output_tokens | 403286941 |
| reasoning_output_tokens | 125822605 |
| sessions_with_usage | 2944 |
| gpt_5_5_standard_api_equivalent_usd | 90317.41802699999 |
| delta_total_tokens | 2950209170 |
| tokens_per_hour | 177387252.92792442 |
| tokens_per_second | 49274.23692442345 |
| gpt_5_5_standard_usd_per_hour | 132.47511662970234 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 174585.53193899998 |
| gpt_5_5_long_context_upper_bound_delta_usd | 84268.11391199999 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 192044.08513289999 |
| gpt_5_5_regional_10pct_usd | 99349.15982970002 |
| gpt_5_5_regional_10pct_delta_usd | 9031.741802700024 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |
| post_cutoff_long_context_event_evidence_class | LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION |

## Compilation Resource Throttling

| Metric | Value |
|---|---|
| dotnet_build_invoked_by_token_usage_audit | `False` |
| unity_build_invoked_by_token_usage_audit | `False` |
| final_compile_check | `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION` |
| cpu_total_percent | `83` |
| dotnet_or_csc_process_count | `0` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-29.json` | `0ad8e772f92469f0764308b9f3f4c1a55e3fd6b67343407ca12b9b80ac18001b` | 565229 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-29.md` | `f9944b83de7ad94ab8e34284383041b9353febe5e78aac2732333f312b75242f` | 2374 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-29.json` | `d736cf24b9e0dc783845399238b22b2398a580ec12295bc7b761976203f7eaf9` | 76223 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-29.md` | `f937d6ba94a7757cae50b5f3bb89b5bc9d2feebb58b53bb5cc7c8e7c12197343` | 11131 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `43acce49130ac370c429dfedc655e77bf67b53245ece8abef617e2522874e685` | 501 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `158320d8de281cc6973bbead5128120c5ce89d6177ec7f1a27f7cf45d81aedfe` | 615 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `32b853a1fd3383c7ac3d82715d4c97a324df366e4f173356b20e1324ac4450d8` | 459 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-29 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
