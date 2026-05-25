# SHINOBU_229 Auxiliary Equipment Global Authority Route Card

Date: 2026-05-20

Owner: SHINOBU_229

Owner domain: AUXILIARY_EQUIPMENT_ROUTER

Owning file/system: `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs`

Status: BLOCKED UNTIL UNITY IMPORT / CONSOLE / PROFILER PROOF

Review disposition: YELLOW

## Problem

Flares, gravity tethers, and scanner pings were object-owned Unity behavior.

They carried component lifetimes, pulse drawers, local light/physics state, and per-object updates. That creates multiple owners and blocks rollback/memcpy proof.

Owner-local data is insufficient: lighting, sonar/VFX, and tether physics are separate consumers. Direct owner interface fails because Burst producers and consumers use different phases.

## First 20 Minutes Route Impact

First 20 Minutes moment: Tool / Hazard / Proof.

Route impact: flare darkness response, scanner active pulse feedback, and gravity tether support now route as data packets.

They are not Unity-object effect owners. This supports scanner P1 extension and hazard/tool proof without claiming full playable-route proof.

- Proof required: Unity import and clean Console for SHINOBU_229 files;
- Play Mode deploy smoke for flare,
- sensor ping,
- and gravity tether;
- Profiler or GCMonitor 0 B/frame capture;
- SignalBus telemetry under mock stress;
- Frame Debugger proof for the double-buffered VFX upload.

Parked work rejected: scanner lore/UI zero-GC rewrite, downstream consumer migration, tether pool removal, Data Monolith h8bin migration, and visual implementation remain owner-specific.

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

Cadence/capacity: continuous `GlobalQualityWeight` curve, `15..60 Hz`.

Caps: `1024` auxiliaries, `1024` anchors, `300` telemetry entries, `64` profiles, `16384` CSV bytes, `1024` prewarmed lane slots.

- Expected max events per frame: 1024 flare, 1024 sonar, 1024 tether configured maximum-quality lane maxima.
- Each lane prewarms 1024 queue slots to match the maximum one-signal-per-active-slot producer ceiling.
- Minimum-quality SignalBus flush caps shed visual/effect bandwidth at 64 flare, 32 sonar, 16 tether signals per frame; deployment truth stays in Vault, drops/coalescing in telemetry.
- GlobalQualityWeight behavior: cadence, flare range, sonar expansion rate, and VFX scale lerp continuously; gameplay truth and DTO layout do not change.

## Accessor Purity

- `[x] No Get/TryGet/Resolve/Read API publishes signals`

- `[x] No Get/TryGet/Resolve/Read API syncs scene state`

- `[x] No Get/TryGet/Resolve/Read API allocates/grows buffers`

- `[x] No Get/TryGet/Resolve/Read API completes jobs`

- `[x] No Get/TryGet/Resolve/Read API mutates global state`

- `[x] No Get/TryGet/Resolve/Read API searches the scene`

Read APIs use existing Vault generation handles only.

Buffer acquisition is limited to bootstrap, explicit initialization, and mock generation. `TryReadDeployments` fails closed while lifecycle job is active to avoid writer/read collision.

## Payload And Capacity

Managed fields present: no.

UnityEngine.Object fields present: no.

Layout proof: `DeployedAuxiliaryDTO` is 64 bytes; signals are 64 bytes; state/counters are 16 bytes; tuning/telemetry/VFX matrices are 64 bytes.

Per-tether anchor proof: `AuxiliaryTetherAnchorDTO` is 32 bytes and stores one anchor AUP per deployment slot without changing the deployment ABI.

Capacity: 1024 deployed auxiliaries, 1024 tether anchors, 300 telemetry entries, 64 profiles, 16384 byte CSV scratch.

- Overflow/failure: route queues rely on `SignalBus<T>` bounded drop/coalesce behavior; router fail-closes when Vault handles are absent or a job is active.
- Producer jobs open lanes through `SignalBus<T>.OpenParallelWriter()` and sanitize non-finite route scalars before enqueue.
- Per-slot route counters are attempted enqueue counts; telemetry records last-flush SignalBus dropped/corrupted/peak-queued counters separately, including quality-driven minimum-budget visual/effect shedding.

## Telemetry And Black Box

Telemetry fields:

- frame; active count; flare/ping/tether attempted route counts
- cadence; schedule-to-finalize wall microseconds; quality weight
- fault flags; snapshot hash; dropped slots/signals; corrupted signals; peak queued signals

Black-box fields: same telemetry ring, last 300 frames.

Planned/generated-on-fault dump target: `Docs/AgentLogs/Dump_SHINOBU_229.bin`; no existing dump artifact is implied unless linked with command, timestamp, environment, trigger, and output.

Profiler marker: pending; static source only. `CpuMicroseconds` is schedule-to-finalize wall time until profiler proof exists.

GC proof required: Unity Profiler or GCMonitor capture in Play Mode.

Generated project shield: `Directory.Build.targets` prunes the deleted `HectonScannerProjectionState.cs` compile item and conditionally includes SHINOBU_229 runtime/editor auxiliary sources without editing generated `.csproj` files.

## Shutdown And Stale Handles

Shutdown/disposal: `OnDisable` completes pending job through dispatcher teardown path, unregisters tick hooks, releases all owned Vault generation handles.

Scene unload behavior: router releases Vault handles and clears readiness.

Stale-handle behavior: hot tick/deploy/read paths use existing generation handles; absent handles fail closed rather than reacquiring/growing buffers.

- Job lock fence covers lifecycle/VFX scheduling, deploy, cancel, and mock generation.
- Locked buffers: `Deployments`, `States`, `TetherAnchors`, `ActiveCount`, `RouteCounters`, `VfxMatrices`, `TelemetryRing`, `TelemetryCursor`, `ActiveEquipmentState`.
- NativeArray views re-resolve under lock; unlock occurs only after pending job finalization and post-fence telemetry proof.
- Mock generation schedules behind the same pending fence and does not force-complete from `Tick`.

Tuning write fence: editor tuning mutation locks `ShinobuAuxiliaryTuning`, resolves only the tuning handle, writes one DTO, and unlocks immediately. Jobs receive tuning by value.

## Cold Tuning Bridge

CSV source: `Assets/_SourceData/Equipment/Auxiliary/auxiliary_equipment_profiles.csv`.

Route: file bytes -> `ShinobuAuxiliaryCsvScratch` -> `AuxiliaryProfileDTO[]` -> `AuxiliaryTuningDTO`.

Fallback: deterministic unmanaged profiles for flare, sensor ping, and gravity tether when CSV is absent.

Data Monolith caveat: this CSV route is editor/source-data input and static fallback only.

Player runtime must use deterministic fallback rows or baked equipment/Data Monolith binary route, not `StreamingAssets` text.

This does not claim `static_data.h8bin` presence or complete h8bin migration.

## Presentation Handoff

Source: `ShinobuAuxiliaryVfxMatrices`.

GPU handoff: double-buffered persistent structured `GraphicsBuffer` pages exposed via `AuxiliaryEquipmentRouterRuntime.TryReadVfxGraphicsBuffer`.

Upload discipline:

- Post-fence upload hashes active deployment slots and compares active count, snapshot hash, camera AUP, and `GlobalQualityWeight`.
- Unchanged frames skip `UploadNativeArray` and keep the previous read buffer.
- Uploads use `GraphicsBufferUploadUtility.CreateStructuredLockBuffer`, `LockBufferForWrite`, and `UnsafeMemoryCopyGuard.TryMemCpy`.
- No auxiliary VFX path uses `SetData`.

Draw ownership: downstream presentation systems; auxiliary router does not call `DrawProceduralIndirect` directly.

- Scanner projection: `HectonScannerProjectionFeature` reads `AuxiliarySonarRequestSignal` snapshots.
- AUP path: subtract `HectonFloatingOrigin.CurrentTotalOffsetDouble` before float upload; shader uses local `worldPos - localOrigin`.
- Age path: derive presentation age from `CurrentRadius / MaxRadius`.
- `ScannerTool` no longer publishes `HectonScannerProjectionState`; unused static shadow-state file and `.meta` were deleted.
- No projection wall-clock state (`Time.time`, `StartTime`, or `Duration`) remains in the owned route.

Scanner audio:

- active pulse audio no longer calls `IAudioService.PlayAtPoint`;
- no `AudioClip` asset field dependency;
- publishes `AcousticPingSignal` through `SignalBus` with active-sonar flags.

`ScanEvents.RaiseScanTriggered` remains scanner-log/progression legacy route.

## Domain Isolation

Auxiliary active mirror: `AuxiliaryActiveEquipmentDTO[1024]`, local to `Hecton8.Equipment.Auxiliary`; the router no longer imports `Hecton8.Tools` or reuses sibling `ActiveEquipmentDTO`.

Runtime-position to AUP conversion: direct add against `HectonFloatingOrigin.CurrentTotalOffsetDouble`; the auxiliary runtime no longer imports `Hecton8.World`.

- Scanner residual boundary: `ScannerTool` scientific/lore discovery strings and broad scanner knowledge/UI coupling remain outside this route card.
- The `ScannerToolActiveSignal` producer no longer calls `GlobalSignals.Publish`; it pushes directly to `SignalBus<ScannerToolActiveSignal>` from `LateFrameTick`.
- Existing downstream `GlobalSignals.TryGetLatestScannerToolActiveSignal` fallback readers are legacy consumer bridge debt for their owners.
- The owned radar pulse lifecycle and projection route remain `AuxiliarySonarRequestSignal` only.

Flare facade boundary: `DeployableFlare` keeps no local `_state` or countdown mirror; public compatibility reads are derived from the router/Vault record only.

## Rejected Alternatives

- `[x] owner-local field`

- `[x] cached owner interface`

- `[x] existing SignalBus lane`

- `[x] existing Vault buffer`

- `[x] cold HectonEventBus hook`

- `[x] no global route needed`

Rejected because auxiliary effects need specialized consumers, job visibility, and rollback/telemetry visibility.

Existing lanes lacked flare/sonar/tether payload layout with AUP and per-route counters.

## Monolith Risk

This route does not add a `GlobalRegistry` service slot and does not use `HectonEventBus`.

It adds typed unmanaged bounded payloads plus named Vault buffers.

It removes object-local effect ownership from flares, gravity tethers, and scanner pulses; no catch-all gameplay pipe is added.

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
