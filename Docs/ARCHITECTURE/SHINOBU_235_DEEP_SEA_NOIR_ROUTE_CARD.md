# SHINOBU_235 Deep Sea Noir Route Card

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Date: 2026-05-21
Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / FILESYSTEM only. Unity import, script compile, shader import, RenderGraph Viewer, Frame Debugger, profiler, GCMonitor, XR validation, and player-build proof remain absent.

## Route Card

```text
Route ID: SHINOBU_235_DEEP_SEA_NOIR_POST_PROCESSOR
Date: 2026-05-21
Owner: SHINOBU_235
Owner domain: Echelon 8 Presentation & UX / Deep Sea Noir post-processing
Vault allocation owner tag: SystemID.GraphicsScalability. The current SystemID enum has no Echelon 8 or Presentation rendering owner; GraphicsScalability is the existing native-memory owner for GPU-scalability payloads. SHINOBU_235 owns the route contract, shader, docs, and proof artifacts; DataVault lock/release calls use SystemID.GraphicsScalability consistently.
Owning file/system: Hecton8.Visor.HectonVisorUberPostFeature deepSeaNoirUnifiedPass branch

Problem:
Unity Volume/PostProcessVolume mutation and string shader parameter updates are managed, profile-oriented, and not acceptable for the abyss visor post effect.

Why owner-local data is insufficient:
Stress, toxicity, depth, quality, tuning, color profiles, and telemetry must be visible to the RenderGraph pass, editor tuner, static validator, and crash dump without duplicating managed state.

Why direct caller/owner interface is insufficient:
The RenderGraph pass needs a stable GPU CBuffer row and telemetry ring across frames. Player movement/survival facts are consumed as cached owner snapshots only; the post processor does not own those facts.

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
0..1 continuous scalar controls grain strength/scale/speed, chroma, X/Y glitch offsets, shader detail masks, profile refresh cadence, and late player-context retry cadence. It does not change DTO layout, save identity, rollback ownership, or route ownership.

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
NoirPostProcessDTO = 64 bytes. GrainParams offset 0 size 16; AberrationParams offset 16 size 16; ColorGrading offset 32 size 16; QualityAndLimits offset 48 size 16. Runtime layout guard checks SizeOf and offsets before CBuffer use.
NoirPostProcessInputDTO = 64 bytes. Stress01 offset 0 size 4; DepthMeters offset 4 size 4; Toxicity01 offset 8 size 4; Narcosis01 offset 12 size 4; Supersaturation01 offset 16 size 4; GlobalQualityWeight01 offset 20 size 4; TimeSecondsWrapped offset 24 size 4; FrameIndex offset 28 size 4; AbSplit01 offset 32 size 4; VignetteOverride01 offset 36 size 4; Flags offset 40 size 4; SourceHash offset 44 size 4; padding offsets 48,52,56,60 size 4 each.
NoirPostProcessTuningDTO = 64 bytes. BaseParams offset 0 size 16; GradeParams offset 16 size 16; StressResponse offset 32 size 16; ProfileParams offset 48 size 16.
NoirTelemetryEntry = 64 bytes. Frame offset 0 size 4; Flags offset 4 size 4; Stress01 offset 8 size 4; DepthMeters offset 12 size 4; Toxicity01 offset 16 size 4; GlobalQualityWeight01 offset 20 size 4; Grain01 offset 24 size 4; Glitch01 offset 28 size 4; Vignette01 offset 32 size 4; AbSplit01 offset 36 size 4; WrappedTimeSeconds offset 40 size 4; ParameterHash offset 44 size 4; EstimatedGpuCostMs offset 48 size 4; ActiveFeatureFlags offset 52 size 4; padding offsets 56,60 size 4 each.
NoirColorProfileDTO = 64 bytes. ProfileHash offset 0 size 4; Flags offset 4 size 4; DepthMinMeters offset 8 size 4; DepthMaxMeters offset 12 size 4; StressMin01 offset 16 size 4; StressMax01 offset 20 size 4; GradeParams offset 24 size 16; ResponseParams offset 40 size 16; padding offsets 56,60 size 4 each.
Overflow/failure:
Invalid or missing player snapshots route to deterministic mock input. Non-finite parameter output writes failsafe constants, marks NoirFlagInvalidMath, records telemetry, and attempts one black-box dump.

Telemetry fields:
Frame, Flags, Stress01, DepthMeters, Toxicity01, GlobalQualityWeight01, Grain01, Glitch01, Vignette01, AbSplit01, WrappedTimeSeconds, ParameterHash, EstimatedGpuCostMs, ActiveFeatureFlags.

Black-box fields:
Planned/generated-on-fault dump target: raw 300-row NoirTelemetryEntry ring to `Docs/AgentLogs/Dump_SHINOBU_235.bin` on first invalid-math detection; no existing artifact is implied unless a timestamped trigger/output is linked.

Profiler marker:
Hecton Deep Sea Noir Post.

GC proof required:
Unity Profiler and GCMonitor proof of 0 B/frame in active deepSeaNoirUnifiedPass path. Static source currently shows no per-frame managed collection allocation in the active route, but measured proof is absent.

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
The route adds six fixed, presentation-only Vault lanes with explicit BufferIDs and no broad service. It consumes cached owner snapshots and writes one proof artifact ring; it does not create a generic global heap or runtime latest-created fallback.

H-Phi impact expected:
Small Vault surface increase, bounded by fixed capacities and explicit ownership. No gameplay truth migration.

Proof required before GREEN:
Unity import, script compile, shader import, RenderGraph Viewer pass order, Frame Debugger one-pass proof, Profiler/GCMonitor 0 B/frame proof, GPU timing capture on low/mid/high quality, black-box dump readback test, and player-build proof.

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
