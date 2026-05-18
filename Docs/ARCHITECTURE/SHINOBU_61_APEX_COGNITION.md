# SHINOBU_61 Apex Cognition Boundary

Date: 2026-05-19
Owner: SHINOBU_61 / Predictive Apex Aggression Director
Status: LOOP 17 STATIC SOURCE NOTES / ROSLYN RECHECK SKIPPED BY CPU GUARD / UNITY RUNTIME PENDING. Unity import, Play Mode, Burst Inspector, and profiler proof pending.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, Burst Inspector, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Runtime Authority

`Assets/_Project/Scripts/AI/Cognition` owns the leviathan cognition math only. It produces `DesiredVelocity`, local `IK_BiteTarget`, predictive intercept telemetry, and unmanaged signal DTO rows. It does not own animation, WFC deformation, audio presentation, HUD distortion, ecosystem fish scatter, or Player Kinematics.

Runtime dependencies stay routed through:

- `Hecton8.Core.Contracts`
- `Hecton8.Core.Memory`
- Unity Burst, Collections, Jobs, Mathematics

The runtime AI assembly must not reference `Hecton8.Core` directly. SignalBus/core owners can attach `NativeQueue<T>.ParallelWriter` lanes through `ApexBrainVault.TryScheduleWithSignalWriters(...)`; SHINOBU_61 then enqueues unmanaged signals from Burst without widening the compile wall. The default schedule path leaves queue writes disabled; enqueue access is gated by `EnableSignalQueueWrites`.

## Vault Buffers

Reserved local IDs:

| ID | Buffer |
|---:|---|
| 70609 | `ApexStateDTO[10]` |
| 70610 | `MockPlayerAUP[10]` |
| 70611 | `AcousticEchoTap[32]` |
| 70612 | `ApexBrainTuning[1]` |
| 70613 | `ApexEmergencyStats[1]` |
| 70614 | `MockWorldSampler[1]` |
| 70615 | `ApexBrainOutputDTO[10]` |
| 70616 | `ApexProximitySignal[10]` |
| 70617 | `MockCombatDamageSignal[10]` |
| 70618 | `GlobalPanicSignal[10]` |
| 70619 | `ApexInfluenceNode[160]` |
| 70626 | `ApexTelemetryEntry[3000]` |
| 70627 | `int[1]` telemetry cursor |
| 70628 | `byte[4096]` CSV scratch |
| 70629 | `float3[160]` ambush node scratch |

`ApexStateDTO` is an explicit 64-byte cache-line record. The authority math converts `double3` AUP deltas to local `float3` before dot products, SDF steering, spatial hashing, and predictive intercept.

Parallel-written rows are padded to cache-line multiples to avoid worker false sharing:

- `MockPlayerAUP`: 128B
- `ApexBrainOutputDTO`: 192B
- `ApexInfluenceNode`: 64B
- `ApexTelemetryEntry`: 128B
- legacy `AlphaLeviathanCognitionState`: 192B
- legacy `AlphaLeviathanSteeringOutput`: 128B

## Dear-Lie LOS

No `NavMeshAgent`, physics ray fan, or body capsule fitting is used. Line-of-sight pressure is faked from:

- player-forward dot product against player-to-leviathan vector
- distance visibility falloff
- center SDF wall shadow
- spatial-hash canyon bias

The SDF slither lie samples center/head always, midsection after quality `0.25`, and tail after quality `0.55`. `GlobalQualityWeight` interpolates ambush node count from 2 to 16 and collapses low-quality work without binary device-tier branches. Acoustic memory now uses the same continuum: the tap scan window lerps from 4 taps at survival quality to 32 taps at full quality. Scheduler cadence also comes from quality: `math.lerp(5f, 60f, Smooth01(...))` drives a deterministic 60-frame mask used by `TrySchedule(...)` and `TryScheduleWithSignalWriters(...)`.

Loop 10 removed per-node `math.sincos` from ambush placement. Candidate nodes are deterministic octant-lattice lanes with spatial-hash radial jitter, so high-quality 16-node overkill does not pay trig cost on Quest-class CPUs.

Inactive target rows now early-out as Dormant before SDF/acoustic/ambush work. First fallback hydration clears all uninitialized runtime rows and scratch buffers with `UnsafeUtility.MemClear` before emergency mock tuning is installed, preventing random cold memory from spawning phantom predators.

The allocation-locked vault path performs the same existing-handle validation and emergency hydration without requesting new buffers. This keeps post-boot integrations from bypassing fallback stats.

`ApexTelemetryEntry.ActiveLeviathans` is row-local truth: active authority slots write `1`, Dormant slots write `0`. It is not the scheduled array capacity.

Rollback-relevant jobs use `FloatMode.Deterministic` with `FloatPrecision.Standard`. Dormant and faulted rows clear their 16-slot ambush scratch/influence span so stale predator intent cannot leak into gizmos, animation bridges, or downstream consumers.

Loop 16 hardened NaN vaccination for non-finite inputs: non-finite state/target AUP or velocity writes a fault row and returns before AUP delta downcast, SDF sampling, dot-product LOS, or spatial hashing. Loop 17 applies the same quarantine to computed SDF/LOS faults, returning before biome, aggro, ambush nodes, signals, telemetry construction, or `HashSpatial(interceptLocal)` can consume poisoned scalars. Optional NativeQueue signal writers are still gated by `EnableSignalQueueWrites`, now with inline three-paragraph safety justifications at each `NativeDisableContainerSafetyRestriction` declaration.

## Forensics

The hot job writes a 300-frame `ApexTelemetryEntry` ring. Cold bridge dump helpers write:

- `Docs/AgentLogs/Dump_SHINOBU_61.bin`
- `Docs/AgentLogs/Dump_LEVIATHAN_CORTEX.bin`

Dump headers include magic, endian marker `0x01020304`, version, telemetry frame count, leviathan capacity, ring length, and cursor.
