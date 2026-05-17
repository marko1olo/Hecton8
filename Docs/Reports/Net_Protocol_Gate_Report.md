# NET Protocol Gate Report

Date: 2026-05-15
Evidence class: OFFLINE_SIM / CLI_COMPILE / STATIC_DOC / STATIC_SOURCE
Status: NETWORK PROTOCOL READY

## Scenario Results

| Scenario | Status | Sent | Lost | Hash mismatches | Ring mismatches | Float audit |
|---|---|---:|---:|---:|---:|---|
| baseline | NETWORK PROTOCOL READY | 1296 | 78 | 0 | 0 | PASS |
| rollback_stress | NETWORK PROTOCOL READY | 1300 | 114 | 0 | 0 | PASS |
| four_client | NETWORK PROTOCOL READY | 7776 | 390 | 0 | 0 | PASS |

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
Ran 8 tests in 12.970s

OK
```

## Failures

- None

## Verification Boundary

This gate does not prove Unity import, Play Mode, profiler, GCMonitor, player build, scene wiring, or platform transport readiness.
Runtime network ownership remains pending.
