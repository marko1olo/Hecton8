# Rationale_SHINOBU_160

Evidence State: PENDING_VERIFICATION / ACTIVE_POLISH / COMPILE_NOT_LAUNCHED_THIS_PASS

## Decision 2026-05-20-01 - Active Memory Reconstruction

Problem: Active `Docs/Tasks/Status_SHINOBU_160.md` and `Docs/AgentLogs/Rationale_SHINOBU_160.md` were absent after Batch010 archival, while active source files and route docs remain in the workspace.
Solution: Treat current source and active `CURRENT_BATCH.md` as authority, recreate active status/rationale, and use archive only as historical evidence boundary already referenced by active architecture docs.
Rejected Alternatives: Trust chat summary; copy archived status as if it were active current proof; ignore missing active logs.
Scalability potential: Low/Middle/High/Ultra behavior unchanged; this protects process correctness.
Hardware Impact: 0 us runtime. Prevents false verification state.

## Decision 2026-05-20-02 - Frame Domain Repair

Problem: SHINOBU runtime used `Time.frameCount` in mock seed, telemetry frame, process job frame, and dump throttle. That leaks Unity global frame state into an otherwise dispatcher-owned route.
Solution: Use `DispatcherTimingDTO.FrameId` as the primary frame identity and an owner-local fallback counter only when the dispatcher sends zero. Session timestamp advances from `FrameDelta`; worker raw payload can still stamp wall-clock time on the background thread.
Rejected Alternatives: Keep `Time.frameCount`; use wall-clock `DateTimeOffset.UtcNow` on the main thread every POST_SIMULATION; change dispatcher contracts.
Scalability potential: Low devices avoid a Unity time read in the exporter frame path; High/Ultra retain identical analytics density controls.
Hardware Impact: Static estimate: sub-microsecond per POST_SIMULATION frame saved, but main gain is determinism and rollback-fence hygiene. Profiler proof pending.

## Decision 2026-05-20-03 - Deterministic Mock RNG

Problem: Emergency mock events used a custom LCG seeded with `Time.frameCount`, which did not literally satisfy the deterministic RNG mandate requiring `Unity.Mathematics.Random` and simulation-frame seed.
Solution: Use `Unity.Mathematics.Random` seeded by `SystemHash ^ SectorHash ^ SimulationFrame`, where sector hash is derived from mock AUP sector coordinates. Keep mock output unmanaged and Burst-compatible.
Rejected Alternatives: Keep LCG; use `UnityEngine.Random`; remove mock generator and leave CI/editor fallback uncovered.
Scalability potential: Low uses mock only when explicitly enabled; High/Ultra can stress richer export density without corrupting gameplay truth.
Hardware Impact: Mock-only path. No gameplay hot-path cost unless designer enables mock analytics.

## Decision 2026-05-20-04 - Fail-Closed Ingress Ownership

Problem: `TryRecordEvent` could still call the static ingress queue when `s_active == null`, which is a stale-owner hole if a queue survives an abnormal worker shutdown.
Solution: Return false immediately without active exporter ownership. Active exporter still applies owner-thread gate, continuous pressure cull, and atomics.
Rejected Alternatives: Allow global static queue writes without active owner; expose `ParallelWriter` without producer fence.
Scalability potential: All tiers fail closed under teardown. No analytics data is worth a stale native write.
Hardware Impact: 0 us normal path beyond a null branch already present; prevents shutdown corruption.
