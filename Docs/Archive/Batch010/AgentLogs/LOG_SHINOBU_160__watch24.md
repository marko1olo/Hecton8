# LOG_SHINOBU_160

## 2026-05-20 - Active Polish Re-Entry

What was wrong:
- Active SHINOBU status/rationale/log files were absent after Batch010 archival.
- Current source still had dispatcher-domain rot: `Time.frameCount` in mock seed, telemetry frame, process job frame, and dump throttle.
- Emergency mock events used a custom LCG instead of the mandated `Unity.Mathematics.Random`.
- The public facade could attempt static ingress enqueue when no active exporter owned the queue.

What was done:
- Recreated active SHINOBU_160 status/rationale/log files for the current batch state.
- Patched frame identity to use `DispatcherTimingDTO.FrameId` with owner-local fallback.
- Patched mock RNG to `Unity.Mathematics.Random` with `SystemHash ^ SectorHash ^ SimulationFrame`.
- Patched `TryRecordEvent` to fail closed when `s_active == null`.
- Replaced DTO enqueue object initializers with `default` field assignment.

Cinematic Cheats used:
- Analytics remains a Dear Lie: batched external observation, not gameplay truth. Server stream is simulated by fixed-size background chunks.

Exact Microseconds saved:
- Frame-domain repair: estimated <1 us/frame static; profiler proof absent.
- Fail-closed ingress: 0 us steady-state, prevents stale native write during teardown.
- Main-thread network/compression saving remains the original architectural target: 100-5000 us avoided per telemetry burst versus JSON/web on main thread, pending profiler proof.

Verification:
- Pending static scans after patch.
- Compile/import not launched in this pass.
