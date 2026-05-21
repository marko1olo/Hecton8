# COMPUTE LIVE BURN 5MIN FORECAST

Status: AUDIT COMPLETE / LIVE LEDGER MOVING
Snapshot: 2026-05-15T17:43:10+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\state_5.sqlite`, table `threads`

## Boundary

This is a live SQLite delta sample, not an invoice. SQLite only exposes total per-thread token mass. It does not expose input/cached-input/output split per delta, so live price uses the corrected global blended rates from `COMPUTE_CORRECTED_ROLLING_RATES.md`.

Pricing basis checked against official OpenAI pricing pages on 2026-05-15. The applied live blend is:

| Rate | Value |
|---|---:|
| Corrected cache-aware blend | USD 0.670788 / 1M tokens |
| Corrected no-cache blend | USD 4.413807 / 1M tokens |

## Current Totals

| Metric | Value |
|---|---:|
| Current SQLite total tokens | 45,857,878,991 |
| Previous live trend total, 17:29+04 | 45,817,071,457 |
| Delta since previous live trend | 40,807,534 |
| Seconds since previous live trend | 836.616907 |
| Rate since previous live trend | 48,776.85 tokens/sec |
| Corrected JSONL/SQLite snapshot, 17:18+04 | 45,758,254,570 |
| Delta since corrected snapshot | 99,624,421 |
| Seconds since corrected snapshot | 1,487.042222 |
| Rate since corrected snapshot | 66,995.02 tokens/sec |

## Five-Minute Sample

| Metric | Value |
|---|---:|
| Sample start UTC | 2026-05-15T13:38:10.153674Z |
| Sample finish UTC | 2026-05-15T13:43:10.616907Z |
| Sample duration | 300.463233 sec |
| Sample token delta | 16,694,405 |
| Tokens/sec | 55,562.22 |
| Tokens/min | 3,333,733.35 |
| Tokens/hour equivalent | 200,024,000.94 |
| Tokens/day equivalent | 4,800,576,022.56 |
| Cache-aware sample cost | USD 11.20 |
| Cache-aware USD/min | USD 2.236 |
| Cache-aware USD/hour | USD 134.17 |
| Cache-aware USD/day equivalent | USD 3,220.17 |
| No-cache sample cost | USD 73.69 |
| No-cache USD/min | USD 14.714 |
| No-cache USD/hour | USD 882.87 |
| No-cache USD/day equivalent | USD 21,188.82 |
| Active threads | 20 |
| Active model bucket | `gpt-5.5` only |
| Active CWD bucket | `C:/hades` only |

## Interval Volatility

| Interval | Tokens | Tokens/sec | Cache-aware USD/min | No-cache USD/min |
|---:|---:|---:|---:|---:|
| 1 | 5,583,992 | 92,721.00 | USD 3.73 | USD 24.56 |
| 2 | 1,156,846 | 19,260.72 | USD 0.78 | USD 5.10 |
| 3 | 3,960,035 | 65,976.47 | USD 2.66 | USD 17.47 |
| 4 | 2,629,323 | 43,800.23 | USD 1.76 | USD 11.60 |
| 5 | 3,364,209 | 55,953.24 | USD 2.25 | USD 14.82 |

Verdict: the burn did not stabilize. Minute 1 was 4.8x minute 2. Any single-minute extrapolation is noise; the five-minute average is the better short-window throttle number.

## Concentration

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 thread | 1,937,906 | 11.61% |
| Top 2 threads | 3,819,094 | 22.88% |
| Top 5 threads | 7,745,612 | 46.40% |
| Top 10 threads | 12,444,535 | 74.54% |
| Top 12 threads | 13,940,362 | 83.50% |

Top active threads:

| Rank | Thread ID | Delta tokens | Title label |
|---:|---|---:|---|
| 1 | `019e2098-4883-7440-9d71-44971d6192fd` | 1,937,906 | Check bot and documentation |
| 2 | `019e230e-0e12-7be2-8eb9-39df3a774cc6` | 1,881,188 | Forge SignalLanes |
| 3 | `019e2804-6d3c-7712-a927-0839fac1cc5e` | 1,500,157 | Read batch prompt |
| 4 | `019e2321-2f60-7fd3-8cd6-31ccbca84ce9` | 1,303,615 | Build Race Condition Hunter |
| 5 | `019e27db-3780-7b80-900a-0aeb9a23f4de` | 1,122,746 | Form 10 agent prompts |
| 6 | `019e2310-3a80-7962-849b-5f9327a7141f` | 1,094,264 | Build outpost save delta sync |
| 7 | `019e1dfe-8ab5-7970-bba2-f7b283b05d7b` | 1,033,695 | Check and update documentation |
| 8 | `019e2802-6cfe-7ed1-8f84-6c466293f707` | 970,599 | Timaert road/river terrain prompt |
| 9 | `019e285e-2f6d-7313-a7e7-6a9e3a3d670a` | 813,965 | Fix assembly compile wall |
| 10 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | 786,400 | Improve bot memory and CRM |
| 11 | `019e2805-5171-7393-9a26-2291c246bd72` | 769,228 | Read own AGENT_PROMPT |
| 12 | `019e2593-7f04-7c50-b5c8-3c16f805188f` | 726,599 | Fix compile wall |

Token concentration is high enough for targeted throttle. It is not enough for a waste conviction. Waste still requires joining the thread to final diff, meaningful LOC/quality delta, and validation result.

## Stop-Loss Projection

Projection uses the five-minute average, not the one-minute spike.

| Threshold | Cache-aware time at current rate | No-cache time at current rate |
|---|---:|---:|
| USD 100 | 44.72 min | 6.80 min |
| USD 1,000 | 7.45 h | 1.13 h |
| USD 10,000 | 3.11 d | 11.33 h |

Token thresholds:

| Threshold | Time at current rate |
|---|---:|
| 100M tokens | 30.00 min |
| 1B tokens | 5.00 h |
| 4.8B tokens | 24.00 h |

## Code Ratios With Live SQLite Total

| Metric | Value |
|---|---:|
| Meaningful script LOC baseline | 788,619 |
| Estimated script source bytes | 42,067,846 |
| Live tokens per meaningful LOC | 58,149.60 |
| Live tokens per script source byte | 1,090.093 |
| Live model-aware cost per meaningful LOC | USD 0.03901 |

## Verdict

The live tail is still material. The five-minute average is 55.56k tokens/sec and USD 2.236/min cache-aware. That is lower than the 17:29 one-minute spike, but still equals 4.80B tokens/day if sustained.

The operational risk is not the absolute total anymore. The risk is uncontrolled concurrent agent tails, where top 10 active threads can hold 74.54% of live burn in a five-minute window.
