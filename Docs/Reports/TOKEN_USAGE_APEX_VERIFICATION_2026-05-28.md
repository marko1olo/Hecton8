# Token Usage Apex Verification 2026-05-28

Generated Samara: `2026-05-28T12:19:24.203066+04:00`
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
| total_tokens | 110159445798 |
| input_tokens | 109776871191 |
| cached_input_tokens | 105482603520 |
| output_tokens | 381541007 |
| reasoning_output_tokens | 119780195 |
| sessions_with_usage | 2856 |
| gpt_5_5_standard_api_equivalent_usd | 85658.870325 |
| delta_total_tokens | 1915058255 |
| tokens_per_hour | 297297721.32858646 |
| tokens_per_second | 82582.70036905179 |
| gpt_5_5_standard_usd_per_hour | 228.26438423437068 |

## Artifact Hashes

| Path | SHA-256 | Bytes |
|---|---|---:|
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.json` | `9dabab3032221ffb823c42c181635b51606de54f1b8b0aec4049f1741856b674` | 561331 |
| `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.md` | `03403865e41716912b1a2cfc0d7b4ab3a0f25ec3fce0c08a5c8f6b94405aaeff` | 1753 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.json` | `4c68107ee801033f4464640515a4217cbf0fc3813f380ea1564da0374cafb156` | 70419 |
| `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.md` | `495d623b484d07b7df3e16ca21cf7f5960890fed81c9302835635550229e4e4d` | 6701 |
| `Tools/CodexTokenUsageAudit_20260525.py` | `0deb8a5ec931ca980b7761c966aa1d78698d92d593f53617d5cb088260b0153d` | 1543 lines |
| `Tools/CodexTokenUsageFastRefresh_20260528.py` | `a3c101bc996b9d05c8840d3407fb7f187383ce6e5805d5b249f39cf13918256c` | 446 lines |
| `Tools/ProjectMetricsDashboard_20260528.py` | `a0500b863c016fe8b6941e15e1c5403ce1a4e364926a21b6cb787ea0d8c44d3f` | 488 lines |
| `Tools/TokenUsageApexVerification_20260528.py` | `bd18bd61bdf07d6a3b04f4100a38e4afad228532a39499e01795bf3bff9e4258` | 370 lines |

## Known Faults

- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Full all-time token replay exceeded 20 minutes under live parallel-agent churn; 2026-05-28 report uses fast incremental evidence from the 2026-05-27 full snapshot plus post-cutoff JSONL deltas.
- Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.
