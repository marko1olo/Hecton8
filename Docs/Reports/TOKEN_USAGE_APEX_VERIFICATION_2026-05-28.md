# Token Usage Apex Verification 2026-05-28

Generated Samara: `2026-05-28T21:59:08.095575+04:00`
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
| total_tokens | 113292508044 |
| input_tokens | 112898228022 |
| cached_input_tokens | 108483193216 |
| output_tokens | 393246422 |
| reasoning_output_tokens | 122945123 |
| sessions_with_usage | 2917 |
| gpt_5_5_standard_api_equivalent_usd | 88114.163298 |
| delta_total_tokens | 2233113498 |
| tokens_per_hour | 278065144.81320393 |
| tokens_per_second | 77240.31800366775 |
| gpt_5_5_standard_usd_per_hour | 217.30208359306567 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 170329.630266 |
| gpt_5_5_long_context_upper_bound_delta_usd | 82215.466968 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 187362.5932926 |
| gpt_5_5_regional_10pct_usd | 96925.5796278 |
| gpt_5_5_regional_10pct_delta_usd | 8811.416329800006 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |
| post_cutoff_long_context_event_evidence_class | LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION |

## Compilation Resource Throttling

| Metric | Value |
|---|---|
| dotnet_build_invoked_by_token_usage_audit | `False` |
| unity_build_invoked_by_token_usage_audit | `False` |
| final_compile_check | `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION` |
| cpu_total_percent | `96` |
| dotnet_or_csc_process_count | `2` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.json` | `bed3afef4bffa53cca3ed0edb93062431f95c5ea9476fc18695a15b02737b316` | 564658 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.md` | `db8a916352f8ab77b0153858177c4ec84ec17c54ac7894e4122e4aeefb1101c4` | 2379 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.json` | `1dff8d1bf4fd5568f775bfd1b1d674e43ccdb9c331072e39c3431d686b037e96` | 75958 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.md` | `b334ae7e36d4c85b9158df5add8a3e0236dd228562cd2563700a58feb8bf4ed0` | 11131 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `f333c8a0292ec5a9c6c6b1cace948f2d8c1804cacf68ddee3906fe7f7ca53f26` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `43acce49130ac370c429dfedc655e77bf67b53245ece8abef617e2522874e685` | 501 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `158320d8de281cc6973bbead5128120c5ce89d6177ec7f1a27f7cf45d81aedfe` | 615 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `32b853a1fd3383c7ac3d82715d4c97a324df366e4f173356b20e1324ac4450d8` | 459 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-28 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
