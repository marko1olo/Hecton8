# HECTON-8 Nightly Headless Build Report

- Generated UTC: `2026-05-16T01:47:58Z`
- Scenario JSON: `Tools/HeadlessScenarios.json`
- Evidence class: `DUMMY_PROCESS/FILESYSTEM`
- CI status: `FAIL`
- Runner status: `RUNNER CONFIGURED`
- Catalog audit: `PASS`

## Catalog Inquisition

- Schema: `H8_HEADLESS_SCENARIO_CATALOG`
- Endianness: `little`
- Binary alignment: `16 bytes`
- Atlas family/domain: `QA and tests` / `84`
- FNV-1a scenario collisions: `0`
- Quality profiles: `TOASTER, MIDDLE, HIGH, RTX_OVERKILL`
- Data sovereignty: `stateless scenario records; runner passes flags and consumes telemetry artifacts`
- Catalog errors: `none`

| Scenario | FNV-1a 32 | Hex |
|---|---:|---|
| 100_Days_Idle | 241145585 | `0x0E5F96F1` |
| Ecology_Collapse | 3584909435 | `0xD5AD607B` |
| Max_Stress_Test | 2792713319 | `0xA6756C67` |

## Scenario Results

| Scenario | Replay | Status | Exit | Seconds | Frame Samples | P50 ms | P95 ms | Max ms | RAM slope MB/sample | Hash | Reasons |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| Max_Stress_Test | 0 | FAIL | 13 | 2.70 | 10 | 15.540 | 15.760 | 15.760 | -0.200000 | `cc6d483a938dd2b24b111179d44a4ca9` | PROCESS_EXIT_CODE_13 |

## FrameTimeMs Graph

SVG artifact: `Docs/Reports/Nightly_Build_Report_CrashDummy_FrameTime.svg`

```text
  15.76 |   *   *
  15.72 |
  15.69 |
  15.65 |  *   *
  15.61 |
  15.58 |
  15.54 | *   *   *
  15.50 |
  15.47 |
  15.43 |*   *   *
        +----------
         samples=10 min=15.430 max=15.760
```

## Commands

- `Max_Stress_Test` replay `0`: `C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -c <dummy-headless-process> C:\Hecton8\.codex-artifacts\headless-scenarios\20260516T014758Z\Max_Stress_Test\replay_00\unity.log C:\Hecton8\.codex-artifacts\headless-scenarios\20260516T014758Z\Max_Stress_Test\replay_00\telemetry.jsonl 13 Max_Stress_Test 1297306452 7 C:\Hecton8\Docs\AgentLogs`

## Telemetry Artifacts

- `Max_Stress_Test` replay `0`: log `.codex-artifacts/headless-scenarios/20260516T014758Z/Max_Stress_Test/replay_00/unity.log`, telemetry `.codex-artifacts/headless-scenarios/20260516T014758Z/Max_Stress_Test/replay_00/telemetry.jsonl`, stdout `.codex-artifacts/headless-scenarios/20260516T014758Z/Max_Stress_Test/replay_00/process_stdout.log`

## Crash Dumps

- `Docs/AgentLogs/Dump_HEADLESS_SCENARIO_RUNNER.bin`: bytes `16`, sha256 `18a14d05b4b7109ce6158b19de1f65b881575e11385852c30f5aad041ab8a91f`, aligned16 `True`, valid_header `True`, entries `0`, struct `64`, error `none`

## Regression Model

- CPU: process timeout guard kills hangs after the configured threshold; default is 300 seconds.
- GC: Unity hot-path GC is not proven by this external runner; missing GCMonitor artifacts remain PENDING VERIFICATION.
- Memory: RAM slope is computed across parsed samples. Any slope above configured tolerance is a CI failure.
- Cadence: `FrameTimeMs` percentiles and max are reported from player telemetry or dummy telemetry.
- Correctness: deterministic replay compares output hashes for scenarios with `determinism_replays >= 2`.
- Failure modes: player missing, timeout, non-zero exit, missing telemetry, positive RAM slope, and hash mismatch.

## Residual Risk

- This report does not prove Unity scene wiring, GCMonitor state, profiler state, or MX350 player performance without a real `Hecton8.exe` artifact and fresh telemetry.
- Blackbox dump parsing is metadata-only. Runtime dump field semantics remain owned by the Unity telemetry system.
