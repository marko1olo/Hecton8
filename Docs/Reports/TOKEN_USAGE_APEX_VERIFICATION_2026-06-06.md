# Token Usage Apex Verification 2026-06-06

Generated Samara: `2026-06-06T11:47:16.462616+04:00`
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
| total_tokens | 138546616477 |
| input_tokens | 138063786512 |
| cached_input_tokens | 132756527360 |
| output_tokens | 481796365 |
| reasoning_output_tokens | 147451139 |
| sessions_with_usage | 3608 |
| gpt_5_5_standard_api_equivalent_usd | 107368.45039 |
| delta_total_tokens | 2110729747 |
| tokens_per_hour | 158533006.67123663 |
| tokens_per_second | 44036.94629756573 |
| gpt_5_5_standard_usd_per_hour | 130.1481701843761 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 207509.95530499998 |
| gpt_5_5_long_context_upper_bound_delta_usd | 100141.50491499998 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 228260.9508355 |
| gpt_5_5_regional_10pct_usd | 118105.29542900002 |
| gpt_5_5_regional_10pct_delta_usd | 10736.845039000022 |
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
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-06.json` | `ee6f1c6ca20636ea69124ee06a884887627e2e0e642ad0653079295d89dc18e0` | 565704 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-06.md` | `5e46df84594d436879a6da597d0b4a7f97d501e7ddb7ff2b5e6e1127aa158bef` | 3224 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-06.json` | `788e8d7c050d04363c139a7b4663c577a109cb7afe70132c371b40ff20b1daed` | 149117 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-06.md` | `925fba83006a1c8e9c141991f1517d0dc0555545c6e7ceb45fb6ca217bbf6f44` | 29080 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `dae45fcd7cb69f19ff5677ae8b01535f31a43a61cf6108c81f65204e460126de` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `6169f6f3b5a8155d50f1261aec5f0abd26744831e1c419f784bd2f110c31f5fd` | 582 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `9fb5a0ed25888dacc7f111b7a2dc9ac4ad9611e8cef4882600cdb6150128947a` | 892 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `be8115b9c34e108aa081e78cd320f7cffee802dc5caeb345fd9f19380c8474f5` | 508 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-06-06 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
