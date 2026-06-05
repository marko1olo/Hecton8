# Token Usage Apex Verification 2026-06-05

Generated Samara: `2026-06-05T22:24:11.226971+04:00`
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
| total_tokens | 136435886730 |
| input_tokens | 135961381883 |
| cached_input_tokens | 130750090368 |
| output_tokens | 473471247 |
| reasoning_output_tokens | 144992780 |
| sessions_with_usage | 3412 |
| gpt_5_5_standard_api_equivalent_usd | 105635.64016899999 |
| delta_total_tokens | 1650155606 |
| tokens_per_hour | 34005437.19172758 |
| tokens_per_second | 9445.954775479882 |
| gpt_5_5_standard_usd_per_hour | 29.038316671033346 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 204169.211633 |
| gpt_5_5_long_context_upper_bound_delta_usd | 98533.57146400001 |
| gpt_5_5_long_context_regional_10pct_upper_bound_usd | 224586.13279630002 |
| gpt_5_5_regional_10pct_usd | 116199.20418590002 |
| gpt_5_5_regional_10pct_delta_usd | 10563.564016900025 |
| post_cutoff_long_context_event_count | 0 |
| post_cutoff_long_context_event_surcharge_delta_usd | 0.0 |
| post_cutoff_long_context_event_evidence_class | LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION |

## Compilation Resource Throttling

| Metric | Value |
|---|---|
| dotnet_build_invoked_by_token_usage_audit | `False` |
| unity_build_invoked_by_token_usage_audit | `False` |
| final_compile_check | `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION` |
| cpu_total_percent | `91` |
| dotnet_or_csc_process_count | `0` |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-05.json` | `85a5b8180c0fa1a48e2343216495fed936b1842253cd4ce94eab843e9d7e921f` | 565348 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-05.md` | `7c35891e0564b59f35847519cd50a0d3a7595c22e76be5500f6264f3d3f609ba` | 3218 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-05.json` | `7c43ee7f68b51596b7768d508f2b34009a0c69e1d56d5d7102fd5f7d5113abdf` | 178898 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-05.md` | `7fbf7aea1b0ed66c4f2dfa2a06296944dbc073fb930ca6ceeb1c3ee518baa4f1` | 29078 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `dae45fcd7cb69f19ff5677ae8b01535f31a43a61cf6108c81f65204e460126de` | 1568 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `6169f6f3b5a8155d50f1261aec5f0abd26744831e1c419f784bd2f110c31f5fd` | 582 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `9fb5a0ed25888dacc7f111b7a2dc9ac4ad9611e8cef4882600cdb6150128947a` | 892 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `be8115b9c34e108aa081e78cd320f7cffee802dc5caeb345fd9f19380c8474f5` | 508 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-06-05 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
