# SHINOBU_234 Surface Storm Abyssal Propagation



Owner: Atmosphere / storm propagation.



Route:



Optional `ShinobuOceanWeatherState` or SHINOBU-owned emergency `MockHurricaneStateDTO` -> `CalculateStormAttenuationJob` -> hidden `ShinobuStormPropagationWriteState` snapshot -> late-frame `ShinobuStormPropagationState` + scalar row publication.



Startup:



`ShinobuStormPropagationRuntime` creates one scene-local host after scene load if no instance has claimed the route. It does not call `DontDestroyOnLoad`; claim is released on disable.



Phase record:



- Owner assembly/domain: `Hecton8.Atmosphere.StormPropagation.Runtime` / `Hecton8.Atmosphere`.



- Schedule lane: Environment `IUpdatable`; fixed `SimulationTickDeltaSeconds = 1/60` drives cadence, `DeltaTime`, and deterministic phase time.
- Ownership lane: cold owned-buffer creation on enable/DataVault rebind, optional weather descriptor adoption via `TryGetGenerationHandle`.
- Publish lane: completed jobs publish on `ILateFrameTickable`; deferred fault dump fallback runs on `ISlowTickable`.



- Producer job: `CalculateStormAttenuationJob`, Burst deterministic.



- Publication: only the job-visible rows are Vault-locked before schedule/resolve; public scalar rows are not pinned for the worker lifetime.
- The job writes only the hidden 96-byte write snapshot.
- `ILateFrameTickable` publication path:
  - Fail closed under active Vault compaction.
  - Lock four scalar rows only during publication.
  - Lock stable state row.
  - Resolve every target row.
  - Copy completed state with a 32-byte `UnsafeUtility.MemCpy`.
  - Publish all four scalar `float4` rows in the same owner window.
  - Stamp producer proof bits into telemetry.
- If any publication lock/resolve fails, previous public rows remain visible and no scalar proof bits are stamped.



- Proof status: static-source only; Unity compile, Burst Inspector, Play Mode, profiler, GCMonitor, and visual capture are pending.



Data lanes:



- `StormPropagationDTO`: 32 bytes, explicit layout, `float3 SurgeVector`, turbidity, acoustic muffling, biolum stimulus.



- `StormPropagationWriteSnapshotDTO`: 96 bytes, explicit hidden write layout, 32-byte state plus flow/audio/biolum/fog scalar snapshots.



- `ShinobuStormPropagationFlowScalar`: `float4(surge.xyz, attenuatedEnergy)`.



- `ShinobuStormPropagationAudioScalar`: `float4(muffling, lowPassHz, attenuatedEnergy, depthMeters)`.



- `ShinobuStormPropagationFogScalar`: `float4(densityMultiplier, extinctionMultiplier, flowAdvection, attenuatedEnergy)`.



- `ShinobuStormPropagationBiolumScalar`: `float4(stimulus, pulseMultiplier, attenuatedEnergy, depthMeters)`.



Ownership:



SHINOBU_234 owns only the storm propagation buffers above.

It does not mutate `ShinobuVolumetricFogParams`, `BiolumMockWeatherSignal`, `BiolumPulseStateDTO`, or `ShinobuOceanSurfaceSwell`.

Downstream owners should consume SHINOBU scalar lanes in their own phase. Current source has no external consumer for `71721..71724`.



Math:



Depth is currently resolved from sector/floating-origin AUP and sea-level AUP in double precision before float attenuation:



`Energy = SurfaceIntensity * exp(-DepthMeters * DecayConstant)`.



- Task 13's camera-AUP requirement is blocked until a pure owner-published camera/player AUP snapshot lane exists; SHINOBU does not hot-poll `GlobalRegistry.Player`.
- Rejected near misses:
- `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`: read path can sync scene/player context.
- `CameraPositionSignal`: float runtime position, not AUP.
- `PlayerStateSignal`: contextual adapter lane.
- `PlayerKinematicState`: player body state, not camera AUP.



Scalability:



`GlobalQualityWeight` continuously controls noise richness, cached cadence, and decorative intensity. It does not alter DTO layout, authority route, save identity, or gameplay ownership.



Rollback:



`SurgeVector` is the physical/environmental scalar that can be re-evaluated deterministically. Fog turbidity, biolum panic, and audio muffling are presentation lanes and remain outside rollback Merkle state.



Verification boundary:



Runtime readiness: not claimed. Evidence: source inspection and local static commands only.



## Global Authority Route Card



Route ID: SHINOBU_234_SURFACE_STORM_ABYSSAL_PROPAGATION



Date: 2026-05-21



Owner: SHINOBU_234



Owner domain: ECHELON 7 ATMOSPHERE & CELESTIAL / Weather & Wind Director



Owning file/system: `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs`



Fact:



Depth-attenuated surface storm propagation scalars: surge vector, turbidity, acoustic muffling, biolum stimulus, and derived scalar bridge lanes.



Route:



Flow:

1. Optional `ShinobuOceanWeatherState` or SHINOBU-owned emergency mock hurricane row.
2. `SampleAup` from floating-origin fallback.
3. Sea-level AUP plus tuning/profile rows.
4. `CalculateStormAttenuationJob`.
5. Hidden `ShinobuStormPropagationWriteState` snapshot.
6. Late-frame `ShinobuStormPropagationState`.
7. Scalars: `FlowScalar`, `AudioScalar`, `BiolumScalar`, `FogScalar`.



Problem:



Surface storm energy must affect deep turbidity, current pressure, biolum panic, and audio masking without managed per-entity weather listeners or deep-water Rigidbody forces.



Why owner-local data is insufficient:



- Output target: cross-domain presentation and flow owners.
- Current static proof found no external consumer.
- Attenuation job must survive Vault relocation, scene load, crash dump, and future late consumer phases.



Why direct caller/owner interface is insufficient:



Consumers are phase-separated and may be absent during bootstrap; a direct interface would either create sibling assembly coupling or force hot GlobalRegistry polling.



Producer/consumer phase: environment update schedules `CalculateStormAttenuationJob`; `ILateFrameTickable` publishes state/scalars; downstream flow/fog/audio/biolum owners read rows in owner phases.



Cadence/capacity: continuous `GlobalQualityWeight` cadence from 5Hz to configured publication cadence, clamped 5Hz..60Hz; one 32-byte state row, one hidden 96-byte write snapshot, and bounded profile/telemetry rows.



Instrument:



- GlobalDataVault / IDataVault



- Black-box/telemetry route



Producer phase:



Environment update admission schedules `CalculateStormAttenuationJob`; `ILateFrameTickable` publishes completed state after `JobHandle.IsCompleted`.



Upstream producer dependency:



- `ShinobuOceanWeatherState` is adopted through an existing generation handle when present.
- Its absence no longer blocks the SHINOBU-owned emergency mock path or calm fallback publication, and SHINOBU never creates or mutates the upstream weather row.
- The current weather owner does not expose a first-party immutable snapshot fence or producer `JobHandle` for SHINOBU to chain.
- `TryLockBuffer` is used only as relocation pinning, not writer-completion proof.
- This remains an upstream route block before GREEN approval for live weather integration.



Consumer phase:



Downstream flow, fog, audio, and biolum owners read SHINOBU scalar rows in their own phases.

Current static scan found no downstream consumer outside SHINOBU. Route status: producer-side implemented; cross-owner integration pending. SHINOBU does not mutate downstream DTOs.



Known downstream landing zones, not owned by SHINOBU:



- Flow: `VegetationFlowFieldIntegrator` / fluid owner should fold `FlowScalar` into the published abyssal flow surface.



- Fog: VFX/fog owner can map `FogScalar` into `FogConstantsDTO.FlowAdvection` and density/extinction fields.



- Biolum: biolum owner can consume `BiolumScalar` during pulse sync/global shader publication.



- Audio: acoustic owner can consume `AudioScalar` during acoustic zone graph blending.



Direct SHINOBU calls into these systems are rejected because they create sibling assembly coupling and owner-phase mutation.



Cadence:



Continuous `GlobalQualityWeight` drives cadence from 5Hz to `PublicationCadenceHz` default 30Hz, clamped 5..60Hz. Accumulation uses locked 1/60 simulation tick.



Expected max events/reads per frame:



Per admission interval:

- One scheduled attenuation job.
- One hidden 96-byte write-snapshot write.
- One stable 32-byte state publication.
- Four late-frame scalar `float4` row publications.

Public scalar rows are not worker-written. They lock only during late-frame all-or-nothing publication.



GlobalQualityWeight behavior:



Quality scales cadence, noise richness, pulse multiplier, and surge gain through `math.lerp`, `math.smoothstep`, and polynomial smoothing. It does not change DTO layout, BufferID, ownership, or rollback route.



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



Layout proof:

| DTO | Bytes | Offsets |
|---|---:|---|
| `StormPropagationDTO` | 32 | 0 `float3 SurgeVector`; 12 `float TurbidityScalar`; 16 `float AcousticMuffling`; 20 `float BioluminescenceStimulus`; 24-31 padding |
| `StormPropagationWriteSnapshotDTO` | 96 | 0 `StormPropagationDTO State`; 32 `float4 FlowScalar`; 48 `float4 AudioScalar`; 64 `float4 BiolumScalar`; 80 `float4 FogScalar` |



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



- Task 17's requested `NativeHashMap` is reconciled as a fixed-capacity Vault array: `ShinobuStormPropagationImpactProfiles` stores 16 hashed profile rows and the Burst job performs a bounded contiguous scan.
- Profile rows are not mixed blindly: `gale`, `hurricane`, and `abyssal_hurricane` rows are weighted by `WeatherStateDTO.StateMask` plus continuous storm intensity, then multiplied by smooth depth-band weights.
- A private runtime-owned `NativeHashMap` is rejected because it violates Vault ownership, relocation, and allocator control.



Overflow/failure:



- Telemetry wraps modulo 300 with signed-overflow-safe cursor helpers.
- Missing Vault, missing weather owner row, stale handle, compaction fence, missing CSV, oversized CSV, or short CSV reads fail closed and reuse stable rows.
- Schedule-time lock/resolve failure clears cached Vault handles after unlock so `SlowTick` can cold-rebind instead of spinning on stale handles.
- `SampleAup` is the core floating-origin fallback, not a player/camera accessor.
- Hot propagation does not depend on player context; Task 13 remains blocked for literal camera-AUP depth.
- Non-finite attenuation caches telemetry flags during publication and defers one fail-closed file export attempt to slow tick at `Docs/AgentLogs/Dump_SHINOBU_234.bin`; full async exporter handoff is still absent.


Telemetry fields:



Telemetry row:

- Scalar fields: frame, flags, surface intensity, depth, attenuated energy, turbidity.
- Output fields: acoustic muffling, biolum stimulus, surge vector, quality.
- Timing/hash fields: schedule-to-publish latency microseconds, previous intensity, state hash, noise octave count.
- Flags: non-finite, emergency mock weather, producer-lane proof bits for flow/audio/biolum/fog scalar writes.
- Proof class: dispatch/publication latency stamp, not Burst kernel profiler proof.



Black-box fields:



Black-box dump:

- Header: `StormPropagationDumpHeader`.
- Rows: oldest-to-newest 300 telemetry rows.
- Temp path: `Dump_SHINOBU_234.bin.tmp`.
- Gate: byte-length validation.
- Final path: `Dump_SHINOBU_234.bin`.
- Existing dump: preserved as `.bak`.



Profiler marker:



Not present. Required before GREEN runtime approval.



GC proof required:



Unity Profiler/GCMonitor hot-path proof at 0 B/frame. Static scan is not sufficient.



Shutdown/disposal:



`OnDisable` and `Dispose` complete scheduled jobs only for teardown, unregister tick/late/slow/hot-swap routes, release the scene-local runtime claim, and unlock any job-locked Vault buffers.



Scene unload behavior:



Runtime host is scene-local and not `DontDestroyOnLoad`. Runtime claim is reset on subsystem registration and released on disable.



Stale-handle behavior:



- DataVault rebind drains scheduled jobs.
- It clears cached generation handles and marks Vault not ready.
- Handles re-create only in cold setup/rebind.
- Schedule-time lock/resolve failure clears cached handles after unlock when no attenuation job is scheduled.



Editor tooling boundary:



`ShinobuStormPropagationDebugGizmo` is fully `UNITY_EDITOR`-guarded.

Gizmo path: fail closed during Vault compaction, lock stable storm state, copy one DTO, unlock, draw from `Camera.current`. No player-build component or runtime authority.



Rejected alternatives:



- Owner-local field: rejected because cross-domain job-visible scalar state requires relocation-safe Vault rows.



- Cached owner interface: rejected for fan-out consumers and absent downstream systems.



- Existing SignalBus lane: rejected because consumers need stable scalar rows, not event bursts.



- Existing Vault buffer: rejected for downstream-owned DTOs; SHINOBU uses its own scalar lanes.



- Private persistent NativeHashMap: rejected for CSV profiles because GlobalDataVault owns cross-domain native memory; fixed profile rows keep ownership, capacity, and compaction behavior explicit.



- Cold HectonEventBus hook: rejected because this is first-party hot gameplay/presentation data.



- No global route needed: rejected because flow/fog/audio/biolum owner phases must consume the result.



Why this does not increase global monolith risk:



Route owns one fact: depth-attenuated storm propagation scalars.

It does not own fog, audio, biolum, ocean swell, flow-grid simulation, or weather truth beyond cold mock fallback row.



H-Phi impact expected:



Low. This adds bounded fixed-capacity Vault rows for real cross-domain/job/telemetry data; no global heap or speculative absent-system buffers are introduced.



Data Monolith status:



Current X_012 scan sees `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; route-specific boot proof remains pending.

- `storm_depth_impact_profiles.csv` is editor/source input at `Assets/_SourceData/Atmosphere`.
- Player builds do not read it from `StreamingAssets`.
- It is not runtime Data Monolith readiness proof.
- Required proof: bake into Atmosphere `.h8bin` or `static_data.h8bin` section.



CSV profile index status:



- The current implementation stores parsed profile rows in fixed-capacity Vault-backed `StormDepthImpactProfileDTO[]` entries keyed by `ProfileHash`; it does not allocate a persistent `NativeHashMap`.
- The attenuation job weights matching profile rows by weather mask, storm intensity, and depth band.
- This is an intentional deviation until a first-party Vault hash-map ownership contract exists.
- The rejected alternative is private persistent map ownership inside SHINOBU.



Proof required before GREEN:



- Unity import,
- Unity Console clean,
- Burst compile/Inspector artifact,
- Play Mode 10-minute soak,
- Profiler/GCMonitor 0 B hot-path proof,
- Memory Profiler no retained growth,
- Frame Debugger/visual gizmo capture,
- downstream owner consumers for `71721..71724`,
- pure camera/player AUP snapshot owner route if Task 13 remains literal,
- Data Monolith import/bake/boot validation if CSV migrates into `static_data.h8bin`,
- and compile with the external missing Gameplay scanner file restored.



Proof artifact:



ABSENT. Current artifacts are static-source scans and documentation only; the one compiler attempt failed on external `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs`.



Reviewer:



Primary agent self-review only.



Review disposition:



YELLOW



Status:



BLOCKED BY EXTERNAL COMPILE DEPENDENCY / PENDING RUNTIME PROOF
