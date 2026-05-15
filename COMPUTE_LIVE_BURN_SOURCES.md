# COMPUTE LIVE BURN SOURCES

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T16:17:32+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\state_5.sqlite`, table `threads`

## Boundary

This is a live SQLite delta sample, not a full JSONL accounting pass and not an OpenAI invoice.

Method:
- Take `threads.id`, `threads.model`, `threads.tokens_used`, `threads.updated_at_ms`, `threads.title`, `threads.cwd`.
- Sleep 90 seconds.
- Read the same columns again.
- Report only positive token deltas.
- Estimate cost using model blended cache-aware rates from `COMPUTE_TOKEN_BURN_RATE_LEDGER.md`.

The first attempted 120-second sample completed but failed at stdout due Windows codepage Unicode encoding. It was discarded. These numbers are from the successful UTF-8 90-second repeat.

## Sample Summary

| Metric | Value |
|---|---:|
| Sample start UTC | 2026-05-15T12:16:02.024Z |
| Sample end UTC | 2026-05-15T12:17:32.584Z |
| Elapsed | 90.559961 seconds |
| Threads at start | 766 |
| Threads at end | 766 |
| Active threads with positive delta | 11 |
| Total delta tokens | 2,725,800 |
| Tokens/sec | 30,099.39 |
| Tokens/min | 1,805,963.68 |
| Tokens/hour | 108,357,820.52 |
| Tokens/day equivalent | 2,600,587,692.39 |
| Model bucket | `gpt-5.5` only |
| Cache-aware cost | USD 2.08 |
| No-cache equivalent | USD 13.84 |
| Average cost/min | USD 1.38 |
| Average cost/hour | USD 82.70 |
| Average cost/day equivalent | USD 1,984.87 |

Interpretation: live burn is still high. The current 90-second slice is slightly faster than the earlier post-scan tail: 2.601B tokens/day equivalent versus 2.398B tokens/day equivalent.

## Active Thread Delta

| Rank | Thread ID | Model | Delta tokens | Tokens/sec | Cache cost | No-cache | Current thread tokens | Title label |
|---:|---|---|---:|---:|---:|---:|---:|---|
| 1 | `019e2592-efa1-7562-93d6-f671ff937574` | `gpt-5.5` | 718,524 | 7,934.23 | USD 0.548 | USD 3.648 | 80,399,940 | Implement base hibernation |
| 2 | `019e2098-4883-7440-9d71-44971d6192fd` | `gpt-5.5` | 660,381 | 7,292.20 | USD 0.504 | USD 3.353 | 170,844,633 | Check bot and documentation |
| 3 | `019e230e-0e12-7be2-8eb9-39df3a774cc6` | `gpt-5.5` | 382,909 | 4,228.24 | USD 0.292 | USD 1.944 | 133,591,835 | Forge SignalLanes |
| 4 | `019e27db-3780-7b80-900a-0aeb9a23f4de` | `gpt-5.5` | 219,246 | 2,421.00 | USD 0.167 | USD 1.113 | 63,928,604 | Form 10 agent prompts |
| 5 | `019e2804-f244-7ba0-a863-982e85d123fd` | `gpt-5.5` | 175,185 | 1,934.46 | USD 0.134 | USD 0.889 | 77,232,204 | Read batch prompt |
| 6 | `019e2805-5171-7393-9a26-2291c246bd72` | `gpt-5.5` | 169,735 | 1,874.28 | USD 0.130 | USD 0.862 | 83,000,245 | Read AGENT_PROMPT |
| 7 | `019e20d5-88cc-7260-b940-a986d7db8ec5` | `gpt-5.5` | 96,337 | 1,063.79 | USD 0.074 | USD 0.489 | 142,563,610 | Introduce H-Phi metric |
| 8 | `019e230d-8959-72f2-a88b-5e6576683819` | `gpt-5.5` | 84,046 | 928.07 | USD 0.064 | USD 0.427 | 105,024,667 | Contextual UX prompter |
| 9 | `019e2310-3a80-7962-849b-5f9327a7141f` | `gpt-5.5` | 82,837 | 914.72 | USD 0.063 | USD 0.421 | 131,286,666 | Build outpost save delta sync |
| 10 | `019e285e-2f6d-7313-a7e7-6a9e3a3d670a` | `gpt-5.5` | 68,754 | 759.21 | USD 0.052 | USD 0.349 | 83,247,891 | Fix assembly compile wall |
| 11 | `019e231f-2aa0-7b53-ac90-91f1e6f5f0c0` | `gpt-5.5` | 67,846 | 749.18 | USD 0.052 | USD 0.344 | 164,861,402 | Audit AUP precision leaks |

## Concentration

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 active thread | 718,524 | 26.36% |
| Top 2 active threads | 1,378,905 | 50.59% |
| Top 3 active threads | 1,761,814 | 64.63% |
| Top 5 active threads | 2,156,245 | 79.10% |
| Top 11 active threads | 2,725,800 | 100.00% |

Two active threads produced just over half of the 90-second burn. This is a practical throttle target.

## Verdict

Live burn remains active at about 30.1k tokens/sec and USD 1.38/min cache-aware. The active source set is small: 11 threads. This is not random background noise; it is concentrated concurrent agent work.

STATUS: AUDIT COMPLETE.
