# [ARCHIVE] Pre-Line-Split Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SURFACE_STORM_ABYSSAL_PROPAGATION_SHINOBU_234.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_234 Surface Storm Abyssal Propagation



Owner: Atmosphere / storm propagation.



Route:



Optional `ShinobuOceanWeatherState` or SHINOBU-owned emergency `MockHurricaneStateDTO` -> `CalculateStormAttenuationJob` -> hidden `ShinobuStormPropagationWriteState` snapshot -> late-frame `ShinobuStormPropagationState` + scalar row publication.



Startup:



`ShinobuStormPropagationRuntime` creates one scene-local host after scene load if no instance has claimed the route. It does not call `DontDestroyOnLoad`; claim is released on disable.



Phase record:



- Owner assembly/domain: `Hecton8.Atmosphere.StormPropagation.Runtime` / `Hecton8.Atmosphere`.



- Schedule lane: Environment `IUpdatable` admission with fixed `SimulationTickDeltaSeconds = 1/60` for cadence, `DeltaTime`, and deterministic phase time (`_frame * SimulationTickDeltaSeconds`), cold owned-buffer creation on enable/DataVault rebind, optional existing weather descriptor adoption via `TryGetGenerationHandle`, completed job publication on `ILateFrameTickable`, deferred fault dump fallback on `ISlowTickable`.



- Producer job: `CalculateStormAttenuationJob`, Burst deterministic.



- Publication: only the job-visible rows are Vault-locked before schedule/resolve; public scalar rows are not pinned for the worker lifetime. The job writes only the hidden 96-byte write snapshot. `ILateFrameTickable` first fails closed under an active Vault compaction fence, otherwise locks the four scalar rows only during publication, then locks the stable state row, resolves every target row, copies the completed state with a 32-byte `UnsafeUtility.MemCpy`, publishes all four scalar `float4` rows in the same owner window, and stamps producer proof bits into telemetry. If any publication lock/resolve fails, previous public rows remain visible and no scalar proof bits are stamped.



- Proof status: static-source only; Unity compile, Burst Inspector, Play Mode, profiler, GCMonitor, and visual capture are pending.



Data lanes:



- `StormPropagationDTO`: 32 bytes, explicit layout, `float3 SurgeVector`, turbidity, acoustic muffling, biolum stimulus.



- `StormPropagationWriteSnapshotDTO`: 96 bytes, explicit hidden write layout, 32-byte state plus flow/audio/biolum/fog scalar snapshots.



- `ShinobuStormPropagationFlowScalar`: `float4(surge.xyz, attenuatedEnergy)`.



- `ShinobuStormPropagationAudioScalar`: `float4(muffling, lowPassHz, attenuatedEnergy, depthMeters)`.



- `ShinobuStormPropagationFogScalar`: `float4(densityMultiplier, extinctionMultiplier, flowAdvection, attenuatedEnergy)`.



- `ShinobuStormPropagationBiolumScalar`: `float4(stimulus, pulseMultiplier, attenuatedEnergy, depthMeters)`.



Ownership:



SHINOBU_234 owns only the storm propagation buffers above. It does not mutate `ShinobuVolumetricFogParams`, `BiolumMockWeatherSignal`, `BiolumPulseStateDTO`, or `ShinobuOceanSurfaceSwell`; downstream VFX/Ocean/Audio/Flow owners are intended to consume SHINOBU scalar lanes in their own phase. The current source surface has no external consumer for `71721..71724`.



Math:



Depth is currently resolved from sector/floating-origin AUP and sea-level AUP in double precision before float attenuation:



`Energy = SurfaceIntensity * exp(-DepthMeters * DecayConstant)`.



Task 13's camera-AUP requirement is blocked until a pure owner-published camera/player AUP snapshot lane exists; SHINOBU does not hot-poll `GlobalRegistry.Player`. Rejected near misses are `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot` because its read path can sync scene/player context, `CameraPositionSignal` because it is float runtime position rather than AUP, `PlayerStateSignal` because it is a contextual adapter lane, and `PlayerKinematicState` because it is player body state rather than camera AUP.



Scalability:



`GlobalQualityWeight` continuously controls noise richness, cached cadence, and decorative intensity. It does not alter DTO layout, authority route, save identity, or gameplay ownership.



Rollback:



`SurgeVector` is the physical/environmental scalar that can be re-evaluated deterministically. Fog turbidity, biolum panic, and audio muffling are presentation lanes and remain outside rollback Merkle state.



Verification boundary:



No runtime readiness is claimed from this document. Current evidence is source inspection and local static commands only.



## Global Authority Route Card



Route ID: SHINOBU_234_SURFACE_STORM_ABYSSAL_PROPAGATION



Date: 2026-05-21



Owner: SHINOBU_234



Owner domain: ECHELON 7 ATMOSPHERE & CELESTIAL / Weather & Wind Director



Owning file/system: `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs`



Fact:



Depth-attenuated surface storm propagation scalars: surge vector, turbidity, acoustic muffling, biolum stimulus, and derived scalar bridge lanes.



Route:



Optional existing `ShinobuOceanWeatherState` or SHINOBU-owned emergency mock hurricane row + `SampleAup` currently supplied by the floating-origin fallback + sea-level AUP + tuning/profile rows -> `CalculateStormAttenuationJob` -> hidden `ShinobuStormPropagationWriteState` snapshot -> late-frame `ShinobuStormPropagationState` + `FlowScalar`/`AudioScalar`/`BiolumScalar`/`FogScalar`.



Problem:



Surface storm energy must affect deep turbidity, current pressure, biolum panic, and audio masking without managed per-entity weather listeners or deep-water Rigidbody forces.



Why owner-local data is insufficient:



The output is intended for cross-domain presentation and flow owners, but current static proof found no external consumer yet. The attenuation job must still survive Vault relocation, scene load, crash dump, and future late consumer phases.



Why direct caller/owner interface is insufficient:



Consumers are phase-separated and may be absent during bootstrap; a direct interface would either create sibling assembly coupling or force hot GlobalRegistry polling.



Producer/consumer phase: environment update admission schedules `CalculateStormAttenuationJob`; `ILateFrameTickable` publishes completed state plus scalar rows -> downstream flow, fog, audio, and biolum owners read scalar rows in their owner phases when integration exists.



Cadence/capacity: continuous `GlobalQualityWeight` cadence from 5Hz to configured publication cadence, clamped 5Hz..60Hz; one 32-byte state row, one hidden 96-byte write snapshot, and bounded profile/telemetry rows.



Instrument:



- GlobalDataVault / IDataVault



- Black-box/telemetry route



Producer phase:



Environment update admission schedules `CalculateStormAttenuationJob`; `ILateFrameTickable` publishes completed state after `JobHandle.IsCompleted`.



Upstream producer dependency:



`ShinobuOceanWeatherState` is adopted through an existing generation handle when present. Its absence no longer blocks the SHINOBU-owned emergency mock path or calm fallback publication, and SHINOBU never creates or mutates the upstream weather row. The current weather owner does not expose a first-party immutable snapshot fence or producer `JobHandle` for SHINOBU to chain. `TryLockBuffer` is used only as relocation pinning, not writer-completion proof. This remains an upstream route block before GREEN approval for live weather integration.



Consumer phase:



Downstream flow, fog, audio, and biolum owners are expected to read SHINOBU-owned scalar rows in their own owner phases. Current static scan found no downstream consumer outside SHINOBU yet, so this route is producer-side implemented and cross-owner integration remains pending. SHINOBU does not mutate downstream DTOs.



Known downstream landing zones, not owned by SHINOBU:



- Flow: `VegetationFlowFieldIntegrator` / fluid owner should fold `FlowScalar` into the published abyssal flow surface.



- Fog: VFX/fog owner can map `FogScalar` into `FogConstantsDTO.FlowAdvection` and density/extinction fields.



- Biolum: biolum owner can consume `BiolumScalar` during pulse sync/global shader publication.



- Audio: acoustic owner can consume `AudioScalar` during acoustic zone graph blending.



Direct SHINOBU calls into these systems are rejected because they create sibling assembly coupling and owner-phase mutation.



Cadence:



Continuous `GlobalQualityWeight` drives cadence from 5Hz to configured `PublicationCadenceHz` (default 30Hz), clamped between 5Hz and 60Hz admission windows. Cadence accumulation advances by the locked 1/60 simulation tick, not variable dispatcher frame delta.



Expected max events/reads per frame:



At most one scheduled attenuation job, one hidden 96-byte write-snapshot write, one stable 32-byte state publication, and four late-frame scalar `float4` row publications per admission interval. Public scalar rows are not written by the worker job and are locked only for the late-frame all-or-nothing publication window.



GlobalQualityWeight behavior:



Quality scales cadence, noise richness, pulse multiplier, and surge gain through `math.lerp`, `math.smoothstep`, and polynomial smoothing. It does not change DTO layout, BufferID identity, ownership, or rollback route.



Accessor purity:



- No Get/TryGet/Resolve/Read API publishes signals.



- No Get/TryGet/Resolve/Read API syncs scene state.



- No Get/TryGet/Resolve/Read API allocates/grows buffers.



- No Get/TryGet/Resolve/Read API completes jobs.



- No Get/TryGet/Resolve/Read API mutates global state.



- No Get/TryGet/Resolve/Read API searches the scene.



Payload/data shape:



Managed fields present: no.



UnityEngine.Object fields present: no.



Layout proof: `StormPropagationDTO` is explicit 32 bytes: offset 0 `float3 SurgeVector` (12), 12 `float TurbidityScalar` (4), 16 `float AcousticMuffling` (4), 20 `float BioluminescenceStimulus` (4), 24-31 explicit padding bytes. `StormPropagationWriteSnapshotDTO` is explicit 96 bytes: 0 `StormPropagationDTO State` (32), 32 `float4 FlowScalar` (16), 48 `float4 AudioScalar` (16), 64 `float4 BiolumScalar` (16), 80 `float4 FogScalar` (16).



Capacity:



- `ShinobuStormPropagationState = 71712`: 1 x 32 bytes.



- `ShinobuStormPropagationWriteState = 71713`: 1 x 96-byte hidden write snapshot.



- `ShinobuStormPropagationTuning = 71714`: 1 x 64 bytes.



- `ShinobuStormPropagationTelemetryRing = 71715`: 300 x 64 bytes.



- `ShinobuStormPropagationTelemetryCursor = 71716`: 1 x int.



- `ShinobuStormPropagationMockWeather = 71717`: 1 x 32 bytes.



- `ShinobuStormPropagationImpactProfiles = 71718`: 16 x 32 bytes.



- `ShinobuStormPropagationCsvScratch = 71719`: 16 KiB cold scratch.



- `ShinobuStormPropagationDumpScratch = 71720`: 19232 bytes cold fault dump scratch.



- `ShinobuStormPropagationFlowScalar = 71721`, `AudioScalar = 71722`, `BiolumScalar = 71723`, `FogScalar = 71724`: 1 x `float4` each.



BufferID collision note:



Earlier draft IDs `71680..71692` are explicitly superseded. That block is occupied by `ProceduralBoneBlenderBufferIds` and must not be reused by SHINOBU storm propagation.



CSV profile storage:



Task 17's requested `NativeHashMap` is reconciled as a fixed-capacity Vault array: `ShinobuStormPropagationImpactProfiles` stores 16 hashed profile rows and the Burst job performs a bounded contiguous scan. Profile rows are not mixed blindly: `gale`, `hurricane`, and `abyssal_hurricane` rows are weighted by `WeatherStateDTO.StateMask` plus continuous storm intensity, then multiplied by smooth depth-band weights. A private runtime-owned `NativeHashMap` is rejected because it violates Vault ownership, relocation, and allocator control.



Overflow/failure:



- Telemetry wraps modulo 300 with signed-overflow-safe cursor helpers.
- Missing Vault, missing weather owner row, stale handle, compaction fence, missing CSV, oversized CSV, or short CSV reads fail closed and reuse stable rows.
- Schedule-time lock/resolve failure clears cached Vault handles after unlock so `SlowTick` can cold-rebind instead of spinning on stale handles.
- `SampleAup` is currently the core floating-origin fallback, not a player/camera accessor, so hot propagation does not depend on player-context availability and Task 13 remains blocked for literal camera-AUP depth.
- Non-finite attenuation caches telemetry flags during publication and defers one fail-closed file export attempt to slow tick at `Docs/AgentLogs/Dump_SHINOBU_234.bin`; full async exporter handoff is still absent.


Telemetry fields:



Frame, flags, surface intensity, depth, attenuated energy, turbidity, acoustic muffling, biolum stimulus, surge vector, quality, schedule-to-publish latency microseconds, previous intensity, state hash, and noise octave count. Flags include non-finite, emergency mock weather, and producer-lane proof bits for flow, audio, biolum, and fog scalar writes. This is a dispatch/publication latency stamp, not Burst kernel profiler proof.



Black-box fields:



`StormPropagationDumpHeader` plus oldest-to-newest 300 telemetry rows. Dump publication writes `Dump_SHINOBU_234.bin.tmp`, validates byte length, then replaces `Dump_SHINOBU_234.bin` with `.bak` preservation when an older dump exists.



Profiler marker:



Not present. Required before GREEN runtime approval.



GC proof required:



Unity Profiler/GCMonitor hot-path proof at 0 B/frame. Static scan is not sufficient.



Shutdown/disposal:



`OnDisable` and `Dispose` complete scheduled jobs only for teardown, unregister tick/late/slow/hot-swap routes, release the scene-local runtime claim, and unlock any job-locked Vault buffers.



Scene unload behavior:



Runtime host is scene-local and not `DontDestroyOnLoad`. Runtime claim is reset on subsystem registration and released on disable.



Stale-handle behavior:



DataVault rebind drains scheduled jobs, clears all cached generation handles, marks Vault not ready, and re-creates handles only in cold setup/rebind. Schedule-time lock/resolve failure also clears cached handles after unlocking when no attenuation job is scheduled.



Editor tooling boundary:



`ShinobuStormPropagationDebugGizmo` is fully `UNITY_EDITOR`-guarded. The editor gizmo fails closed during active Vault compaction fences, locks the stable storm state row, copies one DTO, unlocks, then draws from `Camera.current` with transform fallback. It does not define a player-build component type and is not part of runtime authority.



Rejected alternatives:



- Owner-local field: rejected because cross-domain job-visible scalar state requires relocation-safe Vault rows.



- Cached owner interface: rejected for fan-out consumers and absent downstream systems.



- Existing SignalBus lane: rejected because consumers need stable scalar rows, not event bursts.



- Existing Vault buffer: rejected for downstream-owned DTOs; SHINOBU uses its own scalar lanes.



- Private persistent NativeHashMap: rejected for CSV profiles because GlobalDataVault owns cross-domain native memory; fixed profile rows keep ownership, capacity, and compaction behavior explicit.



- Cold HectonEventBus hook: rejected because this is first-party hot gameplay/presentation data.



- No global route needed: rejected because flow/fog/audio/biolum owner phases must consume the result.



Why this does not increase global monolith risk:



The route owns one narrow fact: depth-attenuated storm propagation scalars. It does not own fog, audio, biolum, ocean swell, flow-grid simulation, or weather truth beyond a cold mock fallback row.



H-Phi impact expected:



Low. This adds bounded fixed-capacity Vault rows for real cross-domain/job/telemetry data; no global heap or speculative absent-system buffers are introduced.



Data Monolith status:



`Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists in the current X_012 scan; route-specific boot proof remains pending. `storm_depth_impact_profiles.csv` now lives under `Assets/_SourceData/Atmosphere` as editor/source input only; player builds do not read it from `StreamingAssets`. It remains not runtime Data Monolith readiness proof until baked into an Atmosphere-domain `.h8bin` or `static_data.h8bin` section.



CSV profile index status:



The current implementation stores parsed profile rows in fixed-capacity Vault-backed `StormDepthImpactProfileDTO[]` entries keyed by `ProfileHash`; it does not allocate a persistent `NativeHashMap`. The attenuation job weights matching profile rows by weather mask, storm intensity, and depth band. This is an intentional deviation until a first-party Vault hash-map ownership contract exists. The rejected alternative is private persistent map ownership inside SHINOBU.



Proof required before GREEN:



Unity import, Unity Console clean, Burst compile/Inspector artifact, Play Mode 10-minute soak, Profiler/GCMonitor 0 B hot-path proof, Memory Profiler no retained growth, Frame Debugger/visual gizmo capture, downstream owner consumers for `71721..71724`, pure camera/player AUP snapshot owner route if Task 13 remains literal, Data Monolith import/bake/boot validation if CSV migrates into `static_data.h8bin`, and compile with the external missing Gameplay scanner file restored.



Proof artifact:



ABSENT. Current artifacts are static-source scans and documentation only; the one compiler attempt failed on external `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs`.



Reviewer:



Primary agent self-review only.



Review disposition:



YELLOW



Status:



BLOCKED BY EXTERNAL COMPILE DEPENDENCY / PENDING RUNTIME PROOF

