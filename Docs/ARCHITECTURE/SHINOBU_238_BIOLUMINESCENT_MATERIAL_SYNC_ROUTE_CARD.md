# SHINOBU_238 Bioluminescent Material Sync Route Card

Date: 2026-05-21

Status: STATIC SOURCE / ROUTE YELLOW / RUNTIME PROOF PENDING

Evidence class: STATIC_DOC + STATIC_SOURCE. This is not Unity import, Play Mode, profiler, GCMonitor, Frame Debugger, shader variant warmup, or player-build proof.

First 20 Minutes moment: World load, Swim, and Hazard readability on the selected Copper Wire route.

Route impact: Makes abyss/coral/flora bioluminescence visible and scalable during the first swim without per-material mutation, and supports darkness/hazard readability through one GPU matrix route.

Shader variant boundary: SHINOBU_238 shader diffs add no new `#pragma`, `multi_compile`, or `shader_feature` lines.

Existing shader variant debt remains pre-existing and needs project warmup/import proof before GREEN.

Proof required: Unity import, selected-route Play Mode, Frame Debugger shader-global proof, GCMonitor/profiler capture, and visual capture across low/middle/high/ultra `GlobalQualityWeight`.

Parked work rejected: fauna/Leviathan shader rewrites, new global authority surface, second matrix/color buffer, and per-instance CPU glow animation.

## Route Card

Route ID: `SHINOBU_238_BIOLUM_MATRIX_SYNC`

Owner: `SHINOBU_238`

Owner domain: `BIOLUMINESCENT_MATERIAL_SYNC_ARCHITECT`

Owning file/system: `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`

Problem: Flora/coral/bio-structure glow must appear individually alive without per-renderer material mutation, per-object `Update`, or CPU-side neighbor/light simulation.

Why owner-local data is insufficient: Shader-visible phase state is consumed by instanced flora render paths and editor diagnostics; it must survive DataVault relocation and produce crash telemetry.

Why direct caller/owner interface is insufficient: The GPU shader path is the consumer. A C# interface cannot feed 100,000 visible instances without per-object traversal.

Instrument:

- GlobalRegistry cold service/interface: cached `ITickDispatcher` and `IDataVault` only.

- SignalBus first-party broadcast: consumes `AupShiftSignal`, `FrameTimeSignal`, and `AcousticPingSignal` snapshots.
- GlobalSignals bridge/direct queue: consumes legacy light/vitals/damage mirrors only as documented bridge input.
- GlobalDataVault / IDataVault: owns pulse, profile, sync pulse, mock signal, species, editor CSV scratch, black-box ring, and black-box dump scratch buffers.
- Black-box/telemetry route: 300-frame ring plus `Dump_SHINOBU_238.bin`.
- Fault frames copy clamped 9,616-byte snapshot into Vault-owned dump scratch.
- Fault frames signal a background writer.
- File I/O is outside `Tick`/`LateFrameTick`.
- Editor pulse trigger: editor-only `TryTriggerEditorGlobalPulse()` fails closed without an active runtime and localizes supplied `originAUP` through `AupPrecisionMath.LocalDeltaDouble` before any float hash/spatial-offset math.
- Legacy `HectonBiolumController` shader float publication: retired; it no longer owns `_BiolumPulseTime` or `_HectonLegacyBiolumIntensity`.
- Legacy `HectonBiolumManager` `_BiolumMasterPhase` and `_BiolumIntensity` publication: suppressed when `_GlobalBiolumParams.x > 0.5`; `BiolumPulseSyncRuntime` is the active owner of the biolum bridge/support vectors while the matrix route is live.
- Legacy `HectonBiolumManager` active-zone fallback registry: bounded fixed arrays and explicit counters; overflow is telemetry-visible and does not allocate/grow lists.
- Legacy `HectonBiolumManager` touch-ripple upload: bounded by continuous `GlobalQualityWeight`, not by binary `ScalabilityTier`; capacity ramps from 0 to 16 entries via `smoothstep`.
- Shader row ABI: `_GlobalBiolumDearLieGroups[row] = phase, frequency, amplitude, spatialOffset`; group hue is deterministic shader tint, not matrix payload.
- Indirect vegetation vertex prepass: global biolum vertex pulse now uses `animatedPositionWS - renderOriginWS` and a local deterministic seed for `ResolveSpatialHashPulseOffset`, not the absolute/stable AUP seed used for genetics.

- Producer/consumer phase:
  - `IUpdatable.Tick`: consumes cached signals, advances scalar support, schedules oscillator on cadence, records telemetry without shader publish.
  - `ILateFrameTickable.LateFrameTick`: finalizes completed oscillator jobs, publishes matrix plus scalar support globals.
- GPU shader consumers read the published globals; editor tuner copies snapshots in editor-owned UI refresh.

Cadence/capacity: continuous scalar cadence by `GlobalQualityWeight`; 4 shader matrix rows, 16 cold profile slots, 16 sync pulse rows, 300 telemetry rows, and bounded shader-global upload slots.

Review disposition: `YELLOW / STATIC_SOURCE_ONLY` until shader import, Play Mode, Frame Debugger, profiler, and dump-readback artifacts exist.

Producer phase: `IUpdatable.Tick` for signal consumption, support scalar update, scheduler decision, and telemetry; `ILateFrameTickable.LateFrameTick` for matrix/scalar shader publication.

Consumer phase: GPU vertex/fragment shader reads `_GlobalBiolumDearLieGroups`; editor tuner copies telemetry/profile rows in editor tooling only.

Assigned shader consumers: `Hecton_IndirectVegetation`, `Hecton_CoralMaster`, `Hecton_CoralMaster_GPUI`, `Hecton_KelpMaster`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, and `Hecton_ProceduralBio`.

Cadence: continuous scalar-cadenced by `GlobalQualityWeight`; overload may degrade toward 5 Hz cadence without changing gameplay truth.

Expected max events/reads per frame: one oscillator job, one telemetry row write, one matrix upload, up to 16 sync pulse rows scanned.

GlobalQualityWeight behavior:

- `ResolveUpdateCadenceSeconds()` lerps update cadence.
- Jobs apply small continuous amplitude gain.
- Shader blends vertex wave to per-pixel interference through `_GlobalBiolumParams.y`.
- Legacy touch-ripple fallback upload scales capacity continuously from 0 to 16.

## Accessor Purity

- No `Get*`/`TryGet*`/`Resolve*`/`Read*` API added for editor telemetry.
- Editor copy facades use `CopyEditor*` names because they lock Vault buffers and copy snapshots; these facades are compiled under `UNITY_EDITOR`.
- Shader publication happens only in owner publication methods, not from copy facades.
- No copy facade completes jobs, searches scenes, allocates/grows Vault buffers, or publishes signals.
- Touched legacy camera accessors are cached snapshot reads only: `HectonBiolumManager.GetCameraPosition()` and `GetCameraAup()` do not read live `Transform.position`, refresh scene/player state, or rebuild AUP; owner phases call `RefreshCameraSnapshotForOwnerPhase(...)`.
- Touched mutating helpers use `Update*`, `Sample*`, `Select*`, `TryOpen*`, `TryCache*`, or `TryBuild*` names; read-like `Resolve*` names are not used for cache mutation or buffer selection in this route.

## Payload And Layout

Managed fields present: no in DTO payloads.

UnityEngine.Object fields present: no in DTO payloads.

Layout proof:

- `BiolumPulseStateDTO` = 64 bytes, explicit.

- `Group1_Params` offset 0, size 16.

- `Group2_Params` offset 16, size 16.

- `Group3_Params` offset 32, size 16.

- `Group4_Params` offset 48, size 16.

- Shader row semantic layout: `.x phase`, `.y frequency`, `.z HDR amplitude/intensity source`, `.w spatial wave offset`.

- `SyncPulseDTO` = 32 bytes.

- `BiolumPulseTelemetryEntry` = 32 bytes.

Capacity:

- Pulse state: 1 DTO containing 4 `float4` matrix rows.

- Profile floats: 128 floats, 16 cold profile slots at 8 floats per slot.

- Sync pulses: 16 rows.

- Glow state/AUP mock rows: 50,000 rows.

- Species tuning: 150 rows.
- CSV scratch: 16 KB editor tooling bridge; player hot path does not poll CSV files.
- Black-box dump scratch: 9,616 bytes, Vault-owned `byte` buffer, overwritten per fault dump and never allocated as a private managed array.
- Telemetry ring: 300 rows.

Overflow/failure:

- Missing Vault handles fail closed.

- Sync pulse overflow overwrites bounded slots.

- Non-finite pulse rows are clamped to safe fallback rows.

- Job overrun or NaN triggers black-box dump.
- Missing binary profile falls back to deterministic default profiles.
- Legacy fallback active-zone overflow fails closed and sets the legacy manager telemetry overflow bit; it is not a PulseSync matrix overflow.

Telemetry fields: frame, active glowing instances, active sync pulse count, quality tier, flags, oscillator compute time, darkness scalar, group 0 phase, frequency, primary HDR amplitude.

Active glowing instances source: `_activeGlowingInstanceCount`, seeded from the fixed 50,000-row mock glow/AUP Vault buffers; this is not the four-row shader matrix count.

Black-box fields:

- 16-byte dump header;
- at most 300 raw 32-byte telemetry entries;
- fixed queued snapshot: `9,616` bytes even if source Vault ring is larger;
- storage: `BiolumBlackBoxDumpScratchBufferId = 70312`.

Profiler marker: `H8.VFX.BiolumPulseSync.Tick`, `H8.VFX.BiolumPulseSync.LateFrame`.

GC proof required: Profiler/GCMonitor capture remains pending.

Shutdown/disposal:

- Unregister dispatcher and hot-swap listeners.
- Finish teardown-only job fence.
- Clear shader globals.
- Signal/join black-box dump worker.
- Release cached handles only after confirmed writer shutdown.
- Stop editor-only CSV watcher when present.

Scene unload behavior: owner disables and clears global shader state; Vault handles are invalidated.

Stale-handle behavior: generation mismatch forces cached handle release and fail-closed reacquire through owner setup, not a hot latest-created fallback.

## Rejected Alternatives

- Owner-local native arrays: rejected; persistent state belongs to DataVault.

- Per-renderer `Material.SetFloat`/`MaterialPropertyBlock` pulse lanes: rejected; scales with flora count and breaks batching.

- Per-plant `Update`: rejected; C# callback cost is unbounded.

- Separate `CalculateDarknessScalarJob`: rejected as a tiny job; darkness scalar is inlined into the 4-row oscillator/mock kernels.

- Direct Celestial/Apex compile references: rejected; ambient/depth and mock predator bridge lanes keep compile wall intact.

- Gameplay rollback state inclusion: rejected; glow phase is presentation-only.

- Absolute-float shader selectors using `_GlobalBiolumAupOffset.x/z`: rejected; assigned shaders use local finite coordinates for matrix row selection and filament waves.
- Direct absolute-AUP float casts in editor pulse tooling: rejected; editor triggers now use the active runtime AUP reference and local delta downcast.
- Indirect vegetation vertex pulse from `stableAupSeed`: rejected; global pulse phase now uses local object/render-origin delta, while AUP seed remains only for deterministic genetics/shape hashing.
- Legacy `Shader.SetGlobalFloat` pulse route from `HectonBiolumController`: rejected; live shaders do not read it and matrix/vector publication is the single active route.
- Competing `_BiolumMasterPhase` / `_BiolumIntensity` publication from `HectonBiolumManager` while PulseSync is active: rejected by source guard `IsGlobalPulseSyncOwningLegacyBiolumGlobals()`.
- Legacy `List<HectonBiolumZone>` active-zone registries: rejected; fixed arrays keep the fallback route bounded and telemetry-visible on saturation.
- Binary `ScalabilityTier` touch-ripple gating in the legacy manager: rejected; continuous `GlobalQualityWeight` ramps upload capacity instead.
- Leviathan/fauna shader rewrites: rejected for this pass as external-domain consumers requiring owner route approval.

## Compile Guard

Runtime asmdef references only `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory`, and Unity packages. It does not reference AI, World, Environment, Celestial, or sibling VFX runtime assemblies.

Editor asmdef references the biolum runtime assembly plus Unity Collections/Mathematics.

Legacy bridge dependency note: `HectonBiolumManager` caches `DataVault`, `TickDispatcher`, `Fluid`, and `Player` during owner lifecycle.

It rebinds through `IGlobalRegistryHotSwapListener`; touched tick/sample/ensure helpers do not hot-poll those services.

`GlobalRegistry.CelestialRuntimeSnapshot` remains snapshot-only until Core exposes a typed service route.

## The Dear Lie

Before: apparent independent plant glow would be `O(N)` CPU work for `N` visible plants if driven by per-object updates/material writes.

After: CPU work is `O(1)` for four matrix rows plus bounded 16-pulse scan. Shaders use ALU-only local phase offsets for pseudo-individual propagation.

## Proof Required Before GREEN

- Unity import/console clean.

- Domain compile or Unity script compilation clean.

- Profiler/GCMonitor proof of 0 B/frame in pulse sync hot path.

- Frame Debugger proof of one matrix route and no per-material pulse writes.

- Endurance dump trigger test for NaN/overrun.

- Shader visual proof at low/middle/high/ultra `GlobalQualityWeight`.

## R48 Exact Route Field Normalization

Route ID: SHINOBU_238_BIOLUMINESCENT_MATERIAL_SYNC_ROUTE_CARD

Owner: `SHINOBU_238`

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.
