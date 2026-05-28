# Token Usage Apex Verification 2026-05-28

Generated Samara: `2026-05-28T12:52:08.568402+04:00`
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

## Token Headline

| Metric | Value |
|---|---:|
| total_tokens | 110775514778 |
| input_tokens | 110390552320 |
| cached_input_tokens | 106072714368 |
| output_tokens | 383928858 |
| reasoning_output_tokens | 120444690 |
| sessions_with_usage | 2871 |
| gpt_5_5_standard_api_equivalent_usd | 86143.412684 |
| delta_total_tokens | 2531127235 |
| tokens_per_hour | 184544812.69385818 |
| tokens_per_second | 51262.44797051616 |
| gpt_5_5_standard_usd_per_hour | 142.53338834028025 |

## Pricing Sensitivity

| Metric | Value |
|---|---:|
| long_context_trigger_input_tokens | 272000 |
| gpt_5_5_long_context_upper_bound_usd | 166527.892498 |
| gpt_5_5_long_context_upper_bound_delta_usd | 80384.479814 |
| gpt_5_5_regional_10pct_usd | 94757.7539524 |
| gpt_5_5_regional_10pct_delta_usd | 8614.341268400007 |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.json` | `83c30566ea02958be6d73dc96f9675c10376854cac8d84651872827b3714a3e3` | 563279 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.md` | `b28be71f661ef8f655b64bb907adf63ac9c844bbc5e0eea32255ba4916d00b6a` | 2033 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.json` | `0b571bda9b5a6674019c884750fdbbc3446ef10ddc068b3b51ee641b1259063d` | 71344 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.md` | `30e6abfa868e97895c3f8b36b6220116d9c25c8d2f597210dbf6a11869ffba51` | 6904 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `7f1feafba627ba95439a1b0730f71ed10aa6ed50d9d16d8953c4e03f01d96cc3` | 1565 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `e0ad0fcb2bbc21e0f3f7fbb693a0e7875881fa8beabdc873fc95a900c15128c7` | 473 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `86da3fd3f90bf6e0d51897f6cdfdf48900b0ec9a08b518d82dc5c50551eb1ea0` | 493 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `239d8ec0acc23b225626d80890a732718294f3575938de53b68ff3c37e8176fc` | 395 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-28 report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
