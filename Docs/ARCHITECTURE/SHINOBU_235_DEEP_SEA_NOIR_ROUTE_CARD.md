# SHINOBU_235 Deep Sea Noir Route Card

Date: 2026-05-21

Status: PENDING VERIFICATION

Evidence class: STATIC_SOURCE / FILESYSTEM only. Unity import, script compile, shader import, RenderGraph Viewer, Frame Debugger, profiler, GCMonitor, XR validation, and player-build proof remain absent.

## Route Card

```text

Route ID: SHINOBU_235_DEEP_SEA_NOIR_POST_PROCESSOR

Date: 2026-05-21

Owner: SHINOBU_235

Owner domain: Echelon 8 Presentation & UX / Deep Sea Noir post-processing

Vault allocation owner tag:

- Owner: SystemID.GraphicsScalability.
- Reason: current SystemID enum has no Echelon 8 or Presentation rendering owner.
- Existing owner role: native memory for GPU-scalability payloads.
- SHINOBU_235 owns route contract, shader, docs, and proof artifacts.
- DataVault lock/release calls consistently use SystemID.GraphicsScalability.

Owning file/system: Hecton8.Visor.HectonVisorUberPostFeature deepSeaNoirUnifiedPass branch

Problem:

Unity Volume/PostProcessVolume mutation and string shader parameter updates are managed, profile-oriented, and not acceptable for the abyss visor post effect.

Why owner-local data is insufficient:

Stress, toxicity, depth, quality, tuning, color profiles, and telemetry must be visible to the RenderGraph pass, editor tuner, static validator, and crash dump without duplicating managed state.

Why direct caller/owner interface is insufficient:

RenderGraph needs a stable GPU CBuffer row and telemetry ring. Player movement/survival facts are cached owner snapshots; post processor does not own them.

Instrument:

  [x] GlobalRegistry cold service/interface

  [ ] SignalBus<T> first-party broadcast

  [ ] GlobalSignals bridge/direct queue

  [ ] HectonEventBus mod/API/cold event

  [x] GlobalDataVault / IDataVault

  [x] Black-box/telemetry route

Producer phase: VISUAL_SYNC / renderer feature AddRenderPasses, after cold Create/hot-swap dependency binding.

Consumer phase: URP RenderGraph fullscreen raster pass and Hecton_VisorGlitchACES shader.

Cadence/capacity:

NoirPostProcessDTO: 1 row, dirty-upload only.

NoirPostProcessInputDTO: 1 row, written each active frame from owner snapshots or mock.

NoirPostProcessTuningDTO: 1 row, editor/cold tuning row.

NoirTelemetryEntry: 300 rows, circular ring.

NoirColorProfileDTO: 32 rows, cold CSV profile table.

NoirCsvScratch: 16384 bytes, cold CSV scratch.

Expected max events/reads per frame:

One CBuffer row resolve/write, one tuning row write/read, one input row write/read, one telemetry row write, one optional profile lookup on quality-scaled cadence.

GlobalQualityWeight behavior:

- 0..1 continuous scalar controls grain strength/scale/speed, chroma, X/Y glitch offsets, shader detail masks.
- It also controls profile refresh cadence and late player-context retry cadence.
- It does not change DTO layout, save identity, rollback ownership, or route ownership.

Accessor purity:

  [x] No Get/TryGet/Resolve/Read API publishes signals

  [x] No Get/TryGet/Resolve/Read API syncs scene state

  [x] No Get/TryGet/Resolve/Read API allocates/grows buffers

  [x] No Get/TryGet/Resolve/Read API completes jobs

  [x] No Get/TryGet/Resolve/Read API mutates global state

  [x] No Get/TryGet/Resolve/Read API searches the scene

Payload/data shape:

Managed fields present: no

UnityEngine.Object fields present: no

Layout proof:

NoirPostProcessDTO = 64 bytes.

| Field | Offset | Size |
| --- | ---: | ---: |
| GrainParams | 0 | 16 |
| AberrationParams | 16 | 16 |
| ColorGrading | 32 | 16 |
| QualityAndLimits | 48 | 16 |

Runtime layout guard checks SizeOf and offsets before CBuffer use.

- NoirPostProcessInputDTO = 64 bytes.
- Offsets: Stress01 0, DepthMeters 4, Toxicity01 8, Narcosis01 12.
- Offsets: Supersaturation01 16, GlobalQualityWeight01 20, TimeSecondsWrapped 24, FrameIndex 28.
- Offsets: AbSplit01 32, VignetteOverride01 36, Flags 40, SourceHash 44.
- Padding offsets: 48, 52, 56, 60; size 4 each.

NoirPostProcessTuningDTO = 64 bytes. BaseParams offset 0 size 16; GradeParams offset 16 size 16; StressResponse offset 32 size 16; ProfileParams offset 48 size 16.

- NoirTelemetryEntry = 64 bytes.
- Offsets: Frame 0, Flags 4, Stress01 8, DepthMeters 12, Toxicity01 16.
- Offsets: GlobalQualityWeight01 20, Grain01 24, Glitch01 28, Vignette01 32, AbSplit01 36.
- Offsets: WrappedTimeSeconds 40, ParameterHash 44, EstimatedGpuCostMs 48, ActiveFeatureFlags 52.
- Padding offsets: 56, 60; size 4 each.

- NoirColorProfileDTO = 64 bytes.
- Offsets: ProfileHash 0, Flags 4, DepthMinMeters 8, DepthMaxMeters 12.
- Offsets: StressMin01 16, StressMax01 20, GradeParams 24 size 16, ResponseParams 40 size 16.
- Padding offsets: 56, 60; size 4 each.

Overflow/failure:

Invalid or missing player snapshots route to deterministic mock input. Non-finite parameter output writes failsafe constants, marks NoirFlagInvalidMath, records telemetry, and attempts one black-box dump.

Telemetry fields:

Frame, Flags, Stress01, DepthMeters, Toxicity01, GlobalQualityWeight01, Grain01, Glitch01, Vignette01, AbSplit01, WrappedTimeSeconds, ParameterHash, EstimatedGpuCostMs, ActiveFeatureFlags.

Black-box fields:

Planned/generated-on-fault dump target: raw 300-row NoirTelemetryEntry ring to `Docs/AgentLogs/Dump_SHINOBU_235.bin` on first invalid-math detection; no existing artifact is implied unless a timestamped trigger/output is linked.

Profiler marker:

Hecton Deep Sea Noir Post.

GC proof required:

Required proof: Unity Profiler and GCMonitor `0 B/frame` for active deepSeaNoirUnifiedPass.

Static source shows no per-frame managed collection allocation in active route, but measured proof is absent.

Shutdown/disposal:

Dispose unregisters the GlobalRegistry hot-swap listener, clears raw-color history requests, releases Noir Vault handles through IDataVault.ReleaseBuffer, releases double GraphicsBuffers, and clears cached snapshot/scaler references.

Scene unload behavior:

Renderer feature disposal owns release. Recreated scene must cold-create handles/buffers again through Create and hot-swap refresh; no persistent private NativeArray survives unload.

Stale-handle behavior:

DataVault replacement releases old handles and reacquires the Noir handles in the new Vault. Phase-local TryResolveHandle failures fail closed and skip pass enqueue.

Rejected alternatives:

  [x] owner-local field

  [x] cached owner interface

  [ ] existing SignalBus lane

  [x] existing Vault buffer

  [ ] cold HectonEventBus hook

  [ ] no global route needed

Why this does not increase global monolith risk:

- Route adds six fixed presentation-only Vault lanes with explicit BufferIDs.
- It adds no broad service.
- It consumes cached owner snapshots.
- It writes one proof artifact ring.
- It creates no generic global heap or runtime latest-created fallback.

H-Phi impact expected:

Small Vault surface increase, bounded by fixed capacities and explicit ownership. No gameplay truth migration.

Proof required before GREEN:

Pending proof: Unity import, script compile, shader import, RenderGraph order, Frame Debugger, Profiler/GCMonitor 0 B/frame, GPU timings, dump readback, player build.

Reviewer: pending integrator

Review disposition: YELLOW / STATIC_SOURCE_ONLY

Status: ACCEPTED_STATIC_SOURCE / RUNTIME_PENDING

```

## Linked Artifacts

- Binary ledger: `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`

- Architecture note: `Docs/ARCHITECTURE/DEEP_SEA_NOIR_POST_PROCESSOR_SHINOBU_235.md`

- Status: `Docs/Tasks/Status_SHINOBU_235.md`

- Rationale: `Docs/AgentLogs/Rationale_SHINOBU_235.md`

- CTO log: `Docs/AgentLogs/LOG_SHINOBU_235.md`

- Static report: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`

- Dump target: planned/generated-on-fault `Docs/AgentLogs/Dump_SHINOBU_235.bin`; no existing artifact is implied unless a timestamped trigger/output is linked.
