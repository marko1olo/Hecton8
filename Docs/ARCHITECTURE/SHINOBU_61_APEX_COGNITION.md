# SHINOBU_61 Apex Cognition Boundary



Date: 2026-05-19



Owner: SHINOBU_61 / Predictive Apex Aggression Director



Status: LOOP 23 STATIC SOURCE NOTES / HISTORICAL ROSLYN RECHECK TEXT / UNITY RUNTIME PENDING.

Pending proof: Unity import, Play Mode, Burst Inspector, profiler, current compile artifact.



## Source Anchors



Evidence: STATIC_SOURCE / FILESYSTEM.

Scope: cited local paths exist at capture time. No compile/import/Play/profiler/GC/player/save/platform/visual proof.



- `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs`



- `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs`



- `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs`



- `Assets/_Project/Scripts/AI/Cognition/LeviathanStalkJob.cs`



- `Assets/_Project/Scripts/AI/Cognition/Editor/LeviathanCortexTunerWindow.cs`



- `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef`



## Runtime Authority



- Owner path: `Assets/_Project/Scripts/AI/Cognition`.
- Owns leviathan cognition math only.
- Outputs: `DesiredVelocity`, local `IK_BiteTarget`, predictive intercept telemetry, unmanaged signal DTO rows.
- Excludes animation, WFC deformation, audio presentation, HUD distortion, ecosystem fish scatter, Player Kinematics.



Runtime dependencies stay routed through:



- `Hecton8.Core.Contracts`



- `Hecton8.Core.Memory`



- Unity Burst, Collections, Jobs, Mathematics



Runtime AI assembly must not reference `Hecton8.Core` directly.

SignalBus/core owners can attach `NativeQueue<T>.ParallelWriter` lanes through `ApexBrainVault.TryScheduleWithSignalWriters(...)`.

SHINOBU_61 can then enqueue unmanaged signals from Burst without widening compile wall. Default schedule leaves queue writes disabled; `EnableSignalQueueWrites` gates enqueue.



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



- The SDF slither lie samples center/head always, midsection after quality `0.25`, and tail after quality `0.55`.
- `GlobalQualityWeight` interpolates ambush node count from 2 to 16 and collapses low-quality work without binary device-tier branches.
- Acoustic memory now uses the same continuum: the tap scan window lerps from 4 taps at survival quality to 32 taps at full quality.
- Scheduler cadence also comes from quality: `math.lerp(5f, 60f, Smooth01(...))` drives a deterministic 60-frame mask used by `TrySchedule(...)` and `TryScheduleWithSignalWriters(...)`.



Loop 10 removed per-node `math.sincos` from ambush placement. Candidate nodes are deterministic octant-lattice lanes with spatial-hash radial jitter, so high-quality 16-node overkill does not pay trig cost on Quest-class CPUs.



- Inactive target rows early-out as Dormant before SDF/acoustic/ambush work.
- First fallback hydration clears runtime rows and scratch buffers with `UnsafeUtility.MemClear`.
- Emergency mock tuning installs only after clear.
- Goal: no phantom predators from random cold memory.



The allocation-locked vault path performs the same existing-handle validation and emergency hydration without requesting new buffers. This keeps post-boot integrations from bypassing fallback stats.



`ApexTelemetryEntry.ActiveLeviathans` is row-local truth: active authority slots write `1`, Dormant slots write `0`. It is not the scheduled array capacity.



Rollback-relevant jobs use `FloatMode.Deterministic` with `FloatPrecision.Standard`. Dormant and faulted rows clear their 16-slot ambush scratch/influence span so stale predator intent cannot leak into gizmos, animation bridges, or downstream consumers.



- Loop 16 hardened NaN vaccination for non-finite inputs: non-finite state/target AUP or velocity writes a fault row and returns before AUP delta downcast, SDF sampling, dot-product LOS, or spatial hashing.
- Loop 17 applies the same quarantine to computed SDF/LOS faults, returning before biome, aggro, ambush nodes, signals, telemetry construction, or `HashSpatial(interceptLocal)` can consume poisoned scalars.
- Optional NativeQueue signal writers are still gated by `EnableSignalQueueWrites`, now with inline three-paragraph safety justifications at each `NativeDisableContainerSafetyRestriction` declaration.



- Loop 18 sanitizes cold tuning and sampler inputs before authority math.
- Sanitized fields: head/mid/tail offsets, emergency stat `float4` rows, sampler origin, floor, ceiling, canyon bias.
- Additional sanitized fields: target noise, fallback target acoustic magnitude.
- The computed finite gate also checks pursuit vectors and intermediate LOS scalars.



Loop 19 hardens blind mock target generator used when Player Kinematics is absent.

Non-finite mock AUP resets before movement. Invalid deltas fall back to deterministic `1/30f`; velocity clamps to `120 m/s`; forward vectors must normalize finite.



Loop 20 bounds all authority-critical tuning and sampler scalars before node/SDF math. This prevents huge finite CSV or binary values from overflowing ambush candidates before `HashSpatial(candidate)`.



Loop 21 applies the same design envelopes to cold vault/CSV/editor tuning ingress.

`ApexBrainVault.SanitizeTuning()` caps unmanaged tuning rows before future cold consumers or editor views can observe absurd positives. Hot job clamp remains second-line guard.



- Loop 22 expands the zero-GC CSV bridge from a partial slider-adjacent surface to the gameplay-relevant `ApexBrainTuning` float surface.
- Optional/pending tuning source `apex_predator_stats.csv` is absent in the current checkout.
- When present, it can tune damage, deterministic tick delta, head/mid/tail SDF offsets, noise aggression, stamina, sweet-lie weights, ambush radius, visual-overkill gain, bite offset, and quality.
- Original fields remain: aggression, acoustic, turn, stalk, speed, radius, biome, strike.
- Simulation time, source hash, flags, and CSV metadata are not CSV-owned.
- Accepted values are still bounded by the vault sanitizer before any cold consumer reads them and by the job sanitizer before authority math.


- Loop 23 removes a sustained low-quality memory-write tax in the ambush scratch hygiene.
- The sweet-lie midpoint SDF probe was already genuinely gated, but low-quality ambush evaluation still walked all 16 lanes to clear 14 unevaluated rows every active frame.
- The resolver now reads the previous output row's `EvaluatedNodeCount`, evaluates only the current node count, and clears only the stale range when quality drops.
- Dormant/fault rows still erase all 16 lanes because those state transitions must remove predator intent immediately.



PROJECT_AUDIT binary-route correction: `apex_predator_stats.csv` is no longer resolved from runtime `StreamingAssets`.

- Cold tuning bridge checks editor/development source-data paths only.
- It returns `null` in production player builds until DataMonolith or Apex-owned `.h8bin` carries the table.
- Runtime defaults and emergency mock tuning remain production fallback.



## Forensics



The hot job writes a 300-frame `ApexTelemetryEntry` ring. Cold bridge dump helpers write:



- `Docs/AgentLogs/Dump_SHINOBU_61.bin`



- `Docs/AgentLogs/Dump_LEVIATHAN_CORTEX.bin`



Dump headers include magic, endian marker `0x01020304`, version, telemetry frame count, leviathan capacity, ring length, and cursor.



Date: 2026-05-19



Owner: SHINOBU_61 / Predictive Apex Aggression Director



Status: LOOP 23 STATIC SOURCE NOTES / HISTORICAL ROSLYN RECHECK TEXT / UNITY RUNTIME PENDING.

Pending proof: Unity import, Play Mode, Burst Inspector, profiler, current compile artifact.
