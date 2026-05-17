# HECTON-8 Nightly Headless Build Report

- Generated UTC: `2026-05-16T22:47:17Z`
- Scenario JSON: `Tools/HeadlessScenarios.json`
- Evidence class: `DUMMY_PROCESS/FILESYSTEM`
- CI status: `PASS`
- Runner status: `RUNNER CONFIGURED`
- Catalog audit: `PASS`
- JSON artifact: `Docs/Reports/Nightly_Build_Report.json`
- Artifact manifest: `Docs/Reports/Nightly_Build_ArtifactManifest.json`

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
| 100_Days_Idle | 0 | PASS | 0 | 0.70 | 10 | 15.660 | 15.880 | 15.880 | -0.200000 | `43ab07bbb3557b5788866731ef6b8d12` | none |
| 100_Days_Idle | 1 | PASS | 0 | 1.19 | 10 | 15.660 | 15.880 | 15.880 | -0.200000 | `43ab07bbb3557b5788866731ef6b8d12` | none |

## Validation Suite

| Check | Severity | Status | Exit | Seconds | Artifact | Missing Output | Details |
|---|---|---|---:|---:|---|---|---|
| FNV_Hash_Catalog | fail | PASS | 0 | 20.62 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/FNV_Hash_Catalog.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifyH8HashCollisions.py --write-report Docs/Reports/H8_Hash_Catalog_Audit.md --write-json Docs/Reports/H8_Hash_Catalog_Audit.json |
| Data_Inquisition_Static | fail | PASS | 0 | 24.65 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Data_Inquisition_Static.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifyDataInquisition.py --report Docs/Reports/Headless_Data_Inquisition_Audit.json |
| Binary_Hygiene_Global | fail | PASS | 0 | 2.69 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Binary_Hygiene_Global.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifyBinaryHygiene.py --report Docs/Reports/Headless_Binary_Hygiene_Audit.json |
| Metric_Phi_Data_Truth | fail | PASS | 0 | 34.40 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Metric_Phi_Data_Truth.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifyMetricPhiDataTruth.py --json-output Docs/Reports/Headless_Metric_Phi_Data_Truth.json --markdown-output Docs/Reports/Headless_Metric_Phi_Data_Truth.md |
| Optics_Beer_Lambert_LUT | fail | PASS | 0 | 3.91 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Optics_Beer_Lambert_LUT.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifyOpticsBaker.py --report Docs/Reports/Headless_Optics_Audit.json |
| Lore_Blob_Contract | fail | PASS | 0 | 2.62 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Lore_Blob_Contract.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifyLore.py --check --verify-source --verify-manifest |
| Sabine_Acoustic_Physics | fail | PASS | 0 | 3.19 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Sabine_Acoustic_Physics.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifySabineBaker.py |
| VFX_VRAM_Binary_Budget | fail | PASS | 0 | 2.00 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/VFX_VRAM_Binary_Budget.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/VerifyVramBudgets.py |
| BlueNoise_Flow_Texture | fail | PASS | 0 | 4.74 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/BlueNoise_Flow_Texture.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/NoiseBaker/VerifyBlueNoiseSpectrum.py |
| Taxonomy_Lore_Binary | fail | PASS | 0 | 1.92 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Taxonomy_Lore_Binary.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/Taxonomy/verify_taxonomy.py |
| Replay_Hasher_Verifier_Guard | fail | PASS | 0 | 2.38 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Replay_Hasher_Verifier_Guard.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/Security/ValidateReplayHasherReferenceVerifier.py |
| Save_Master_Hash_CSharp | fail | PASS | 0 | 2.14 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Save_Master_Hash_CSharp.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/Security/ValidateSaveMasterHashCSharp.py |
| Binary_Alignment_Scan | fail | PASS | 0 | 0.83 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Binary_Alignment_Scan.log` | none | total=42 unaligned=0; binary_alignment_scan |
| QA_Source_Contract_Scan | fail | PASS | 0 | 0.28 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/QA_Source_Contract_Scan.log` | none | files=3 structEndianViolations=0 shellTrue=0 tempDirectoryUses=0; source_contract_scan |
| Verification_Tool_Inventory | fail | PASS | 0 | 0.00 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Verification_Tool_Inventory.log` | none | discovered=43 classified=43 unclassified=0 missingDirect=0; verification_inventory_scan |
| H_Phi_Domain_Map | fail | PASS | 0 | 655.12 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/H_Phi_Domain_Map.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/CalculateHPhi.py --workers 1 --json-output .codex-artifacts/headless-scenarios/HECTON_PHI_HEADLESS_AUDIT.json --graph-output .codex-artifacts/headless-scenarios/HECTON_PHI_HEADLESS_GRAPH.png --atlas .codex-artifacts/headless-scenarios/PROJECT_ATLAS_HEADLESS_AUDIT.md |
| Economy_MonteCarlo_MillionStep | fail | PASS | 0 | 33.47 | `.codex-artifacts/headless-scenarios/20260516T224717Z/validation/Economy_MonteCarlo_MillionStep.log` | none | expectedExitCodes=0; C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -B Tools/Economy/MonteCarloEconomySim.py --players 7000 --max-nodes 10000 |

## FrameTimeMs Graph

SVG artifact: `Docs/Reports/Nightly_Build_FrameTime.svg`

```text
  15.88 |   *   *     *   *
  15.84 |
  15.81 |
  15.77 |  *   *     *   *
  15.73 |
  15.70 |
  15.66 | *   *   * *   *   *
  15.62 |
  15.59 |
  15.55 |*   *   * *   *   *
        +--------------------
         samples=20 min=15.550 max=15.880
```

## Commands

- `100_Days_Idle` replay `0`: `C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -c <dummy-headless-process> C:\Hecton8\.codex-artifacts\headless-scenarios\20260516T224717Z\100_Days_Idle\replay_00\unity.log C:\Hecton8\.codex-artifacts\headless-scenarios\20260516T224717Z\100_Days_Idle\replay_00\telemetry.jsonl 0 100_Days_Idle 1212692808 100 C:\Hecton8\Docs\AgentLogs`
- `100_Days_Idle` replay `1`: `C:\Users\User\AppData\Local\Programs\Python\Python314\python.exe -c <dummy-headless-process> C:\Hecton8\.codex-artifacts\headless-scenarios\20260516T224717Z\100_Days_Idle\replay_01\unity.log C:\Hecton8\.codex-artifacts\headless-scenarios\20260516T224717Z\100_Days_Idle\replay_01\telemetry.jsonl 0 100_Days_Idle 1212692808 100 C:\Hecton8\Docs\AgentLogs`

## Telemetry Artifacts

- `100_Days_Idle` replay `0`: log `.codex-artifacts/headless-scenarios/20260516T224717Z/100_Days_Idle/replay_00/unity.log`, telemetry `.codex-artifacts/headless-scenarios/20260516T224717Z/100_Days_Idle/replay_00/telemetry.jsonl`, stdout `.codex-artifacts/headless-scenarios/20260516T224717Z/100_Days_Idle/replay_00/process_stdout.log`
- `100_Days_Idle` replay `1`: log `.codex-artifacts/headless-scenarios/20260516T224717Z/100_Days_Idle/replay_01/unity.log`, telemetry `.codex-artifacts/headless-scenarios/20260516T224717Z/100_Days_Idle/replay_01/telemetry.jsonl`, stdout `.codex-artifacts/headless-scenarios/20260516T224717Z/100_Days_Idle/replay_01/process_stdout.log`

## Crash Dumps

- none

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
