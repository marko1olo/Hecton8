# Rationale_SHINOBU_160

Evidence State: PENDING_VERIFICATION / ACTIVE_POLISH / COMPILE_NOT_LAUNCHED_THIS_PASS

## Decision 2026-05-20-01 - Active Memory Reconstruction

Problem: Active status/rationale/log files for SHINOBU_160 were absent after archival while active source and route docs still exist.
Solution: Recreate active files under `C:\hades\Hecton8\Docs`, treating current source and active batch prompt as authority.
Rejected Alternatives: Trust chat summary; copy archive as current proof; ignore missing active logs.
Scalability potential: Low/Middle/High/Ultra behavior unchanged; process truth restored.
Hardware Impact: 0 us runtime.

## Decision 2026-05-20-02 - Frame Domain Repair

Problem: Runtime used `Time.frameCount` in mock seed, queue processing frame index, telemetry frame, and dump throttle.
Solution: Use `DispatcherTimingDTO.FrameId`; if zero, advance an owner-local fallback counter. Session timestamp advances from dispatcher `FrameDelta`; background worker may stamp wall-clock payload header off the main thread.
Rejected Alternatives: Keep Unity global frame reads; use main-thread wall-clock every POST_SIMULATION; change dispatcher contracts.
Scalability potential: Low avoids unnecessary Unity time read; High/Ultra retain the same event density controls.
Hardware Impact: Static estimate <1 us/frame; determinism hygiene is the primary gain. Profiler proof pending.

## Decision 2026-05-20-03 - Deterministic Mock RNG

Problem: Mock analytics used custom LCG seeded from Unity frame count, not the mandated deterministic RNG route.
Solution: Use `Unity.Mathematics.Random` seeded by `SystemHash ^ SectorHash ^ SimulationFrame`, with sector hash derived from mock AUP sector.
Rejected Alternatives: Keep LCG; use `UnityEngine.Random`; remove CI/editor mock fallback.
Scalability potential: Mock remains opt-in; Ultra stress can increase event density without gameplay truth dependence.
Hardware Impact: Mock-only path; no normal gameplay cost.

## Decision 2026-05-20-04 - Fail-Closed Ingress Ownership

Problem: `TryRecordEvent` could reach the static ingress queue when no active exporter owned it.
Solution: Return false immediately when `s_active == null`; active route still owner-thread gates and pressure-culls.
Rejected Alternatives: Static queue accepts stale writes; public `ParallelWriter` without producer fence.
Scalability potential: All tiers fail closed during teardown.
Hardware Impact: 0 us steady-state beyond existing branch; removes stale native write risk.
