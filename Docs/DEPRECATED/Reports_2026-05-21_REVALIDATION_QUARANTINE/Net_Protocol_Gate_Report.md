# NET Protocol Gate Report

Date: 2026-05-15
Evidence class: OFFLINE_SIM / CLI_COMPILE / STATIC_DOC / STATIC_SOURCE
Status: HISTORICAL OFFLINE NETWORK SIM SNAPSHOT / RUNTIME PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R13 Report Snapshot Boundary

This report file is a snapshot/provenance document. It is active only where it agrees with:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Historical `PASS`, `VERIFIED`, `current`, `latest`, counter, compile, runtime, 0-GC, frame-time, cost, and performance statements inside this report are not current proof unless the exact claim links a fresh artifact path, command/tool, timestamp, evidence class, and unresolved-error list. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this file alone.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scenario Results

| Scenario | Status | Sent | Lost | Hash mismatches | Ring mismatches | Float audit |
|---|---|---:|---:|---:|---:|---|
| baseline | HISTORICAL_OFFLINE_SIM_PASS | 1296 | 78 | 0 | 0 | PASS |
| rollback_stress | HISTORICAL_OFFLINE_SIM_PASS | 1300 | 114 | 0 | 0 | PASS |
| four_client | HISTORICAL_OFFLINE_SIM_PASS | 7776 | 390 | 0 | 0 | PASS |

## Unit Tests

Result: PASS
Tests: 8

```text
test_aup64_round_trips_boundaries_and_flags_overflow (test_net_jitter_sim.NetJitterSimTests.test_aup64_round_trips_boundaries_and_flags_overflow) ... ok
test_baseline_latency_loss_converges (test_net_jitter_sim.NetJitterSimTests.test_baseline_latency_loss_converges) ... ok
test_float_hash_crime_detector_rejects_float_math (test_net_jitter_sim.NetJitterSimTests.test_float_hash_crime_detector_rejects_float_math) ... ok
test_four_client_fanout_converges (test_net_jitter_sim.NetJitterSimTests.test_four_client_fanout_converges) ... ok
test_merkle_diff_indices_localize_changed_leaves (test_net_jitter_sim.NetJitterSimTests.test_merkle_diff_indices_localize_changed_leaves) ... ok
test_packet_schema_offsets_sizes_and_mtu_budget_are_locked (test_net_jitter_sim.NetJitterSimTests.test_packet_schema_offsets_sizes_and_mtu_budget_are_locked) ... ok
test_redundant_packet_records_clamp_to_available_ticks (test_net_jitter_sim.NetJitterSimTests.test_redundant_packet_records_clamp_to_available_ticks) ... ok
test_rollback_stress_corrects_predicted_inputs (test_net_jitter_sim.NetJitterSimTests.test_rollback_stress_corrects_predicted_inputs) ... ok

----------------------------------------------------------------------
Ran 8 tests in 2.333s

OK
```

## Failures

- None

## Verification Boundary

This gate does not prove Unity import, Play Mode, profiler, GCMonitor, player build, scene wiring, or platform transport readiness.
Runtime network ownership remains pending.
