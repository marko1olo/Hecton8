# SHINOBU_61 Apex Cognition Boundary

Date: 2026-05-19
Owner: SHINOBU_61 / Predictive Apex Aggression Director
Status: LOOP 23 STATIC SOURCE NOTES / HISTORICAL ROSLYN RECHECK TEXT / UNITY RUNTIME PENDING. Unity import, Play Mode, Burst Inspector, profiler, and current compile proof are pending unless a fresh artifact is linked.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, Burst Inspector, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.
- `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs`
- `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs`
- `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs`
- `Assets/_Project/Scripts/AI/Cognition/LeviathanStalkJob.cs`
- `Assets/_Project/Scripts/AI/Cognition/Editor/LeviathanCortexTunerWindow.cs`
- `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef`

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) (R45 prior R43/R44 residue/proof-artifact/source-counter correction) keeps this file as static source notes plus historical Roslyn text, not current compile, Burst Inspector, fauna runtime, profiler, or player-build proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`; R45 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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

Loop 18 moves more bad-data cases out of the fault path by sanitizing cold tuning and sampler inputs before authority math: head/mid/tail offsets, emergency stat `float4` rows, sampler origin/floor/ceiling/canyon bias, target noise, and fallback target acoustic magnitude. The computed finite gate also checks pursuit vectors and intermediate LOS scalars.

Loop 19 hardens the blind mock target generator used when Player Kinematics is absent. Non-finite mock AUP resets before movement, invalid deltas fall back to deterministic `1/30f`, velocity is clamped to 120 m/s, and forward vectors must normalize to finite output.

Loop 20 bounds all authority-critical tuning and sampler scalars before node/SDF math. This prevents huge finite CSV or binary values from overflowing ambush candidates before `HashSpatial(candidate)`.

Loop 21 applies those same design envelopes to cold vault/CSV/editor tuning ingress. `ApexBrainVault.SanitizeTuning()` now caps the unmanaged tuning row before any future cold consumer or editor view can observe absurd positive values; the hot job clamp remains as a second-line authority guard.

Loop 22 expands the zero-GC CSV bridge from a partial slider-adjacent surface to the gameplay-relevant `ApexBrainTuning` float surface. Optional/pending tuning source `apex_predator_stats.csv` is absent in the current checkout; when the artifact exists it can tune damage, deterministic tick delta, head/mid/tail SDF offsets, noise aggression, stamina recovery/cost, sweet-lie shadow/view-dot weights, ambush radius, visual-overkill gain, bite offset, and quality in addition to the original aggression/acoustic/turn/stalk/speed/radius/biome/strike fields. Simulation time, source hash, flags, and CSV metadata are not CSV-owned. Accepted values are still bounded by the vault sanitizer before any cold consumer reads them and by the job sanitizer before authority math.

Loop 23 removes a sustained low-quality memory-write tax in the ambush scratch hygiene. The sweet-lie midpoint SDF probe was already genuinely gated, but low-quality ambush evaluation still walked all 16 lanes to clear 14 unevaluated rows every active frame. The resolver now reads the previous output row's `EvaluatedNodeCount`, evaluates only the current node count, and clears only the stale range when quality drops. Dormant/fault rows still erase all 16 lanes because those state transitions must remove predator intent immediately.

## Forensics

The hot job writes a 300-frame `ApexTelemetryEntry` ring. Cold bridge dump helpers write:

- `Docs/AgentLogs/Dump_SHINOBU_61.bin`
- `Docs/AgentLogs/Dump_LEVIATHAN_CORTEX.bin`

Dump headers include magic, endian marker `0x01020304`, version, telemetry frame count, leviathan capacity, ring length, and cursor.

Date: 2026-05-19
Owner: SHINOBU_61 / Predictive Apex Aggression Director
Status: LOOP 23 STATIC SOURCE NOTES / HISTORICAL ROSLYN RECHECK TEXT / UNITY RUNTIME PENDING. Unity import, Play Mode, Burst Inspector, profiler, and current compile proof are pending unless a fresh artifact is linked.



