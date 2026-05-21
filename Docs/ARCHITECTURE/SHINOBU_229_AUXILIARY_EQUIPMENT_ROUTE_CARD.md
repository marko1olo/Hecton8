# SHINOBU_229 Auxiliary Equipment Global Authority Route Card

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

Date: 2026-05-20
Owner: SHINOBU_229
Owner domain: AUXILIARY_EQUIPMENT_ROUTER
Owning file/system: `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs`
Status: BLOCKED UNTIL UNITY IMPORT / CONSOLE / PROFILER PROOF
Review disposition: YELLOW

## Problem

Flares, gravity tethers, and scanner sensor pings were represented by object-owned Unity behavior: component lifetimes, pulse drawers, local light/physics state, and per-object update routes. That creates multiple owners for one auxiliary fact and blocks rollback/memcpy proof.

Owner-local data is insufficient because lighting, sonar/VFX, and tether physics are separate consumers. A direct owner interface is insufficient because the producer runs Burst jobs and consumers may drain in different phases.

## First 20 Minutes Route Impact

First 20 Minutes moment: Tool / Hazard / Proof.
Route impact: flare darkness response, scanner active pulse feedback, and gravity tether support now route as data packets instead of Unity-object effect owners. This supports the scanner P1 extension and hazard/tool proof without claiming the full playable route is proven.
Proof required: Unity import and clean Console for SHINOBU_229 files; Play Mode deploy smoke for flare, sensor ping, and gravity tether; Profiler or GCMonitor 0 B/frame capture; SignalBus telemetry under mock stress; Frame Debugger proof for the double-buffered VFX upload.
Parked work rejected: scanner lore/UI zero-GC rewrite, downstream audio/AI/cockpit radar consumer migration, tether-manager cold pool removal, Data Monolith h8bin migration, and downstream visual implementation remain owner-specific work outside this route card.

## Instruments

- `[x] SignalBus<T> first-party broadcast`
- `[x] GlobalDataVault / IDataVault`
- `[x] Black-box/telemetry route`
- `[ ] GlobalRegistry cold service/interface`
- `[ ] GlobalSignals bridge/direct queue`
- `[ ] HectonEventBus mod/API/cold event`

## Phase Contract

Producer phase: SIMULATION for lifecycle update, POST_SIMULATION for telemetry, VISUAL_SYNC staging for matrices.
Consumer phase: downstream lighting, sonar/VFX, and tether owners drain typed `SignalBus<T>` lanes in their owned phases.
Cadence: continuous `GlobalQualityWeight` curve, 15 Hz to 60 Hz.
Producer/consumer phase: SIMULATION lifecycle update, POST_SIMULATION telemetry, and VISUAL_SYNC matrix staging -> downstream lighting, sonar/VFX, and tether owners drain typed `SignalBus<T>` lanes in owned phases.
Cadence/capacity: continuous `GlobalQualityWeight` curve, 15 Hz to 60 Hz; 1024 deployed auxiliaries, 1024 tether anchors, 300 telemetry entries, 64 profiles, 16384-byte CSV scratch, and 1024 prewarmed slots per flare/sonar/tether lane.
Expected max events per frame: 1024 flare, 1024 sonar, 1024 tether configured maximum-quality lane maxima. Each lane prewarms 1024 queue slots to match the maximum one-signal-per-active-slot producer ceiling. Minimum-quality SignalBus flush caps intentionally shed visual/effect bandwidth at 64 flare, 32 sonar, and 16 tether signals per frame; deployment truth remains in Vault and lane drops/coalescing are recorded in telemetry.
GlobalQualityWeight behavior: cadence, flare range, sonar expansion rate, and VFX scale lerp continuously; gameplay truth and DTO layout do not change.

## Accessor Purity

- `[x] No Get/TryGet/Resolve/Read API publishes signals`
- `[x] No Get/TryGet/Resolve/Read API syncs scene state`
- `[x] No Get/TryGet/Resolve/Read API allocates/grows buffers`
- `[x] No Get/TryGet/Resolve/Read API completes jobs`
- `[x] No Get/TryGet/Resolve/Read API mutates global state`
- `[x] No Get/TryGet/Resolve/Read API searches the scene`

Read APIs use existing Vault generation handles only. Buffer acquisition is limited to bootstrap/explicit initialization/mock generation paths. `TryReadDeployments` fails closed while the lifecycle job is active so diagnostics cannot read the same deployment buffer that the scheduled writer mutates.

## Payload And Capacity

Managed fields present: no.
UnityEngine.Object fields present: no.
Layout proof: `DeployedAuxiliaryDTO` is 64 bytes; signals are 64 bytes; state/counters are 16 bytes; tuning/telemetry/VFX matrices are 64 bytes.
Per-tether anchor proof: `AuxiliaryTetherAnchorDTO` is 32 bytes and stores one anchor AUP per deployment slot without changing the deployment ABI.
Capacity: 1024 deployed auxiliaries, 1024 tether anchors, 300 telemetry entries, 64 profiles, 16384 byte CSV scratch.
Overflow/failure: route queues rely on `SignalBus<T>` bounded drop/coalesce behavior; router fail-closes when Vault handles are absent or a job is active. Producer jobs open lanes through `SignalBus<T>.OpenParallelWriter()` and sanitize non-finite route scalars before enqueue. Per-slot route counters are attempted enqueue counts; telemetry records last-flush SignalBus dropped/corrupted/peak-queued counters separately, including quality-driven minimum-budget visual/effect shedding.

## Telemetry And Black Box

Telemetry fields: frame, active count, flare/ping/tether attempted route counts, cadence, schedule-to-finalize wall microseconds, quality weight, fault flags, snapshot hash, dropped slots, dropped signals, corrupted signals, and peak queued signals.
Black-box fields: same telemetry ring, last 300 frames.
Planned/generated-on-fault dump target: `Docs/AgentLogs/Dump_SHINOBU_229.bin`; no existing dump artifact is implied unless linked with command, timestamp, environment, trigger, and output.
Profiler marker: pending; static source only. `CpuMicroseconds` is schedule-to-finalize wall time until profiler proof exists.
GC proof required: Unity Profiler or GCMonitor capture in Play Mode.
Generated project shield: `Directory.Build.targets` prunes the deleted `HectonScannerProjectionState.cs` compile item and conditionally includes SHINOBU_229 runtime/editor auxiliary sources without editing generated `.csproj` files.

## Shutdown And Stale Handles

Shutdown/disposal: `OnDisable` completes pending job through dispatcher teardown path, unregisters tick hooks, releases all owned Vault generation handles.
Scene unload behavior: router releases Vault handles and clears readiness.
Stale-handle behavior: hot tick/deploy/read paths use existing generation handles; absent handles fail closed rather than reacquiring/growing buffers.
Job lock fence: lifecycle/VFX scheduling, deploy, cancel, and mock generation lock `Deployments`, `States`, `TetherAnchors`, `ActiveCount`, `RouteCounters`, `VfxMatrices`, `TelemetryRing`, `TelemetryCursor`, and `ActiveEquipmentState` together, then re-resolve the NativeArray views under the lock; all are unlocked only after the pending job has finalized and post-fence telemetry proof has been recorded. Mock generation schedules behind the same pending fence and does not force-complete from `Tick`.
Tuning write fence: editor tuning mutation locks `ShinobuAuxiliaryTuning`, resolves only the tuning handle, writes one DTO, and unlocks immediately. Jobs receive tuning by value.

## Cold Tuning Bridge

CSV source: `Assets/_SourceData/Equipment/Auxiliary/auxiliary_equipment_profiles.csv`.
Route: file bytes -> `ShinobuAuxiliaryCsvScratch` -> `AuxiliaryProfileDTO[]` -> `AuxiliaryTuningDTO`.
Fallback: deterministic unmanaged profiles for flare, sensor ping, and gravity tether when CSV is absent.
Data Monolith caveat: this CSV route is editor/source-data input and static fallback only. Player runtime must use deterministic fallback rows or a baked equipment/Data Monolith binary route, not `StreamingAssets` text. It is not a claim that `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists or that h8bin migration is complete.

## Presentation Handoff

Source: `ShinobuAuxiliaryVfxMatrices`.
GPU handoff: double-buffered persistent structured `GraphicsBuffer` pages exposed via `AuxiliaryEquipmentRouterRuntime.TryReadVfxGraphicsBuffer`.
Upload discipline: post-fence upload hashes active deployment slots and compares active count, snapshot hash, camera AUP, and `GlobalQualityWeight`; unchanged frames skip `UploadNativeArray` and keep the previous read buffer. Uploads create `GraphicsBuffer.UsageFlags.LockBufferForWrite` buffers through `GraphicsBufferUploadUtility.CreateStructuredLockBuffer` and copy with `LockBufferForWrite` + `UnsafeMemoryCopyGuard.TryMemCpy`; no auxiliary VFX path uses `SetData`.
Draw ownership: downstream presentation systems; auxiliary router does not call `DrawProceduralIndirect` directly.
Scanner projection: `HectonScannerProjectionFeature` reads `AuxiliarySonarRequestSignal` snapshots, subtracts `HectonFloatingOrigin.CurrentTotalOffsetDouble` from the signal AUP before float upload, derives presentation age from `CurrentRadius / MaxRadius`, and the shader uses local `worldPos - localOrigin`; `ScannerTool` no longer publishes `HectonScannerProjectionState` directly, and the unused static shadow-state file was deleted with its `.meta`. No projection wall-clock state (`Time.time`, `StartTime`, or `Duration`) remains in the owned route.
Scanner audio: active scanner pulse audio no longer calls `IAudioService.PlayAtPoint` and no longer depends on `AudioClip` asset fields; it publishes `AcousticPingSignal` through `SignalBus` with active-sonar channel flags. `ScanEvents.RaiseScanTriggered` remains a scanner-log/progression legacy route, not an auxiliary light/physics/VFX effect route.

## Domain Isolation

Auxiliary active mirror: `AuxiliaryActiveEquipmentDTO[1024]`, local to `Hecton8.Equipment.Auxiliary`; the router no longer imports `Hecton8.Tools` or reuses sibling `ActiveEquipmentDTO`.
Runtime-position to AUP conversion: direct add against `HectonFloatingOrigin.CurrentTotalOffsetDouble`; the auxiliary runtime no longer imports `Hecton8.World`.
Scanner residual boundary: `ScannerTool` scientific/lore discovery strings and broad scanner knowledge/UI coupling remain outside this route card. The `ScannerToolActiveSignal` producer no longer calls `GlobalSignals.Publish`; it pushes directly to `SignalBus<ScannerToolActiveSignal>` from `LateFrameTick`. Existing downstream `GlobalSignals.TryGetLatestScannerToolActiveSignal` fallback readers are legacy consumer bridge debt for their owners. The owned radar pulse lifecycle and projection route remain `AuxiliarySonarRequestSignal` only.
Flare facade boundary: `DeployableFlare` keeps no local `_state` or countdown mirror; public compatibility reads are derived from the router/Vault record only.

## Rejected Alternatives

- `[x] owner-local field`
- `[x] cached owner interface`
- `[x] existing SignalBus lane`
- `[x] existing Vault buffer`
- `[x] cold HectonEventBus hook`
- `[x] no global route needed`

Rejected because auxiliary effects have multiple specialized consumers, must be job-visible, and must be rollback/telemetry visible. Existing lanes did not provide flare/sonar/tether payload layout with AUP and per-route counters.

## Monolith Risk

This route does not add a new `GlobalRegistry` service slot and does not use `HectonEventBus`. It adds typed, unmanaged, bounded payloads plus named Vault buffers. It removes object-local effect ownership from flares, gravity tethers, and scanner pulses instead of adding a catch-all global gameplay pipe.

H-Phi impact expected: positive only if Unity import confirms the router files compile and runtime proof shows stable Vault/SignalBus behavior.

Proof required before GREEN:
- Unity import with clean Console for SHINOBU_229 files.
- Play Mode smoke: deploy flare, sensor ping, gravity tether.
- Profiler or GCMonitor: 0 B/frame for router hot path.
- Signal lane telemetry under 500 mock deployments.
- Dump file trigger proof or forced telemetry dump validation.

Reviewer: Integrator / Global Authority reviewer.

## R48 Exact Route Field Normalization

Route ID: SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD
Owner: SHINOBU_229
Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.
Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.
Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.
Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.
Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.
Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.
Review disposition: YELLOW / STATIC_SOURCE_ONLY.
