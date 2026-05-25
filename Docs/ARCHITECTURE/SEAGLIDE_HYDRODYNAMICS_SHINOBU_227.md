# Seaglide Hydrodynamics SHINOBU_227

Authority:

- `MantaScooter` is a request producer only. It no longer stores or reads `Rigidbody`.
- `MantaScooter` publishes `SeaglidePropulsionRequestSignal` through typed `SignalBus<T>`; it does not call `SeaglideHydrodynamicsRuntime` directly.
- `MantaScooter` advances `_lastSeaglideAup` only after `SignalBus<SeaglidePropulsionRequestSignal>.TryPush` succeeds. Dropped or saturated signal lanes therefore do not replace the previous accepted AUP baseline.
- `MantaScooter` refreshes one movement snapshot at `UsePrimary`/`Tick` entry.
- Downstream Seaglide read helpers consume that cached snapshot.
- Snapshot source: cached `HectonPlayerMovement` AUP/runtime/depth data.
- It no longer calls `PlayerRuntimeContextService`.
- Manta previous-AUP fallback rewinds velocity inside a local AUP frame via `AupPrecisionMath.LocalDeltaDouble`.
- It rehydrates absolute double3 for the request payload; no direct absolute-AUP displacement subtraction.

- `SeaglideHydrodynamicsRuntime` owns Vault buffers, SignalBus snapshot ingestion, Burst scheduling, telemetry, cold body binding, and 1000-record mock generation.
- `SeaglideHydrodynamicsRuntime` does not auto-install through `RuntimeInitializeOnLoadMethod` or `AddComponent`.
- It must exist through physics-owner scene/prefab composition or explicit bootstrap wiring.
- `EnsureRuntimeInstance` only returns an existing runtime attached to the registered `PhysicsApplySystem`.
- Empty Seaglide request SignalBus snapshots clear live active request count before cadence math.
- Only exception: explicit editor/profiler mock generation window.
- Mock state is tracked by `_mockRequestsActive` and cleared after solver completion, disable, or Vault release.
- `OnEnable` does not auto-seed mock rows.
- `PhysicsApplySystem.SeaglideQueue` is the only bridge that queues force application.
- `SeaglideHydrodynamicsRuntime` caches `PhysicsApplySystem` and `GlobalPhysicsStateManager` during cold refresh/hotswap.
- Drain method does not poll `GlobalRegistry`, call `EnsureRuntimeInstance`, search by body hash, or mutate bindings.

Hot-path data:

- `SeaglideStateDTO`: 64 bytes, explicit layout, AUP at 0, velocity at 24, battery at 36, flags at 40.
- `SeaglidePropulsionRequestDTO`: 128 bytes, explicit layout, current/previous `double3` AUP for Doppler and origin-shift safety.
- `SeaglidePropulsionRequestSignal`: 192 bytes, explicit layout. Offsets: request DTO 0, velocity 128, battery 140, mass 144, added mass 148, target hash 152, frame 156, flags 160, padding 164..191.
- `SeaglideTelemetryEntry`: 64 bytes. `FrameAndRequestCountPacked` overlays bytes 0..7 to force 8-byte native alignment while preserving `FrameIndex` at byte 0 and `EvaluatedRequests` at byte 4.
- Layout proof is executable in source.
- Editor trap checks state/request/request-signal size and every Seaglide DTO alignment used by NativeArray/SignalBus lanes.
- `SeaglideHydrodynamicsLayout.ValidateInternal` checks full DTO alignment plus request DTO and signal offsets.
- Visual/audio/cavitation DTOs are separate from physical state and marked rollback-excluded.

Scalability:

- `HomeostasisBrain.GlobalQualityWeight` continuously blends cheap dominant-axis drag/current fakes toward full quadratic drag and trilinear current sampling.

- Battery metabolism cadence interpolates between slow and fixed cadence without binary tier switches.

- Thrust solve cadence interpolates from 20 Hz survival cadence to fixed-tick cadence.
- Emitted forces are scaled by accumulated solver delta before `PhysicsApplySystem` receives them.
- Cadence shedding does not replay stale input: empty live snapshot resets active request window.

Black box:

- `SeaglideTelemetryEntry` is 64 bytes.
- Stores: frame index, request count, packet count, non-finite count.
- Stores: thrust/drag/flow totals, max force, compute micros, quality, flags.
- Stores: last target hash, last flow force, last battery level.

- Heartbeat rows use the same 300-entry ring.
- Covered ticks: idle, cadence-shed, force-ready waiting, invalid-delta, Vault-resolve.
- Covered failures: lock failure and force-prepare failure.
- Solver completion overwrites with force totals when hydrodynamic solve runs.

- Budget faults set `FlagBudgetExceeded` and trigger the same dump path as non-finite math.

Force route:
- Literal `NativeQueue` route was rejected for this branch.
- Reason: `PhysicsApplySystem` owns a Vault-backed bounded packet bridge for external force ingress.
- Packet count is authoritative.
- Stale rows are ignored instead of cleared.
- The actual Seaglide bridge file is `Assets/_Project/Scripts/Physics/Seaglide/PhysicsApplySystem.SeaglideQueue.cs`. It remains in the Seaglide domain folder to avoid editing the root `PhysicsApplySystem.cs` during the multi-agent batch.
- `PhysicsApplySystem.SeaglideQueue` binds through `GlobalPhysicsStateManager` body resolution only. The previous player-runtime fallback was rejected because body identity belongs to the central physics owner, not the Seaglide producer.
- Body hash search is allowed only in `SeaglideHydrodynamicsRuntime.TryBindPlayerBodyCold` during cold dependency refresh.
- Cold pass pre-fills every Seaglide body-binding row with resolved player `RigidbodyIndex` and row-local `StateIndex`.
- PostFixed drain resolves the pre-bound index only and fails closed with `FlagBodyBindingUnresolved` telemetry when binding is absent or stale.

- Force drain scans the evaluated request window and consumes rows with `FlagForceQueued`, finite force vectors, and nonzero target hashes.
- Sparse valid rows remain reachable after skipped/invalid requests.

Presentation signal route:

- `CalculateSeaglideAudioParametersJob` computes AUP-safe speed/pitch/volume into a rollback-excluded DTO.

- During cold boot, `SeaglideHydrodynamicsRuntime` configures `ToolAcousticSignal` and `BubbleSpawnSignal` lanes with stable FNV lane hashes before `EnsureInitialized`.
- After job completion, `SeaglideHydrodynamicsRuntime` publishes `ToolAcousticSignal` and `BubbleSpawnSignal` through existing `SignalBus` lanes.
- Publish budget is continuous: one packet at survival quality, up to four at high quality.
- Publication scans evaluated request window and counts only successful `SignalBus.TryPush` calls.
- `ToolAcousticSignal.State` uses SHINOBU-local state byte `4` for Seaglide propeller output. It does not reuse `ToolAcousticSignal.StateLaserLoop`.
- `BubbleSpawnSignal.PositionAup` requires the `Hecton8.World.AbsoluteUniversePosition` contract type, so the runtime performs one payload conversion from the job's `double3` AUP. It does not import or poll World services.

- Manta headlight presentation configures and publishes `SubmarineLightsChangedSignal` through the typed `SignalBus<T>` lane instead of `GlobalSignals.Publish`. Tick only queues the presentation update; shader/light mutation runs from `ILateFrameTickable.LateFrameTick`.
- Manta has no Unity `AudioSource` motor fallback. If hydrodynamic DSP publication is absent, motor presentation fails silent instead of starting Unity audio playback.
- Manta headlight upsert/remove masks advance only after accepted `SignalBus<SubmarineLightsChangedSignal>.TryPush` calls. Dropped pushes leave previous published bits intact for retry and record bounded local drop state.
- Manta headlight shader global vector arrays are hash-gated; unchanged payloads do not call `Shader.SetGlobalVectorArray` again during dirty late-frame presentation.
- Manta power indicator no longer uses `MaterialPropertyBlock`, `GetPropertyBlock`, or `SetPropertyBlock`; only a compact state byte is cached on the producer.

Designer tuning bridge:

- Cold boot reads primary `Data/Physics/seaglide_performance_profiles.csv` into the Vault-owned CSV scratch buffer `71672`. Legacy `Data/Physics/seaglide_vehicle_profiles.csv` is accepted only as a fallback for older local worktrees.

- `SeaglideVehicleProfileCsv` parses the scratch bytes through `ReadOnlySpan<byte>`, FNV-1a profile hashing, and strict manual float parsing; accepted values mutate the Vault tuning DTO only during cold setup.

- Runtime fixed ticks do not perform file IO or managed CSV parsing.

Binary ledger:

- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now records Seaglide Vault IDs `71660..71672` as static source/docs coverage. This is not runtime proof.

Unity import hygiene:

- Stable `.meta` files exist for the Seaglide folder, editor folder, and six new C# scripts.

Editor scanner/report hygiene:

- `SeaglideRigidbodyAddForceScanner` writes a domain sidecar report to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_227.json`.
- The shared `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` is preserved; the scanner inserts or replaces only the top-level `shinobu227SeaglideScanner` property instead of overwriting other agents' evidence.
- Scanner scope includes `SeaglideHydrodynamicsJobs.cs` and checks for stale unguarded reciprocal patterns (`* math.rcp(cell)`, `math.rcp(safeFull - safeStart)`) plus stale laser-loop audio assignment in `SeaglideHydrodynamicsRuntime.cs`.
- Scanner/report flags cover missing Manta `AudioSource` fallback and missing power-indicator `MaterialPropertyBlock`.
- Also cover accepted-only headlight signal masks and hash-gated headlight global arrays.
- Also cover editor/development-only mock generation and removed parallel-for safety suppression.
- Also cover fixed `SeaglideAudioSignalDTO` padding sequence.
- Scanner proof is static source/editor proof only. It is not compile, Unity import, Play Mode, profiler, or GC proof.

Global Authority Route Card:

```text

Route ID: SHINOBU_227_SEAGLIDE_HYDRODYNAMICS

Date: 2026-05-20

Owner: SHINOBU_227

Owner domain: Echelon 4 Player, Kinematics & Tools / Scooter Kinematics

Owning file/system: Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs

Problem: Player handheld propulsion needed hydrodynamic thrust/drag/current math without local Rigidbody mutation or FixedUpdate ownership.

Why owner-local data is insufficient: PhysicsApplySystem, editor telemetry, mock profiler, DSP, and VFX consumers need unmanaged cross-phase data.

Why direct caller/owner interface is insufficient: The player tool producer must not mutate the central physics body directly; force application belongs to PhysicsApplySystem.

Instrument:

  [ ] GlobalRegistry cold service/interface

  [x] SignalBus<T> first-party broadcast

  [ ] GlobalSignals bridge/direct queue

  [ ] HectonEventBus mod/API/cold event

  [x] GlobalDataVault / IDataVault

  [x] Black-box/telemetry route

Producer/consumer phase: Player tool submit and FixedTick solver/heartbeat producers -> PostFixed PhysicsApplySystem drain, typed SignalBus presentation consumers, and editor-only x-ray.

- Cadence/capacity: 20 Hz to fixed-tick cadence by `GlobalQualityWeight`.
- Signal snapshots: 16 max; 4 minimum-quality survival signals.
- Vault rows: 1024 request rows; 1024 force packet rows; 300 telemetry rows.
- Presentation packets: 1-4 bounded SignalBus packets.
- Producer phase: Player tool publishes `SeaglidePropulsionRequestSignal`.
- FixedTick ingests the snapshot, clears empty/failed live request windows, schedules solver, or writes heartbeat rows.
- Explicit editor/profiler mock generation may seed one mock solve window.
- PostFixed/LateFrame finalizes completed solver and publishes bounded SignalBus presentation packets.

Consumer phase: PostFixed PhysicsApplySystem drain; typed SignalBus presentation consumers; Editor-only x-ray

Cadence: 20 Hz to fixed-tick cadence by GlobalQualityWeight

Expected max events/reads per frame: 1024 Vault request rows; 1024 force packet rows; 1-4 presentation SignalBus packets

GlobalQualityWeight behavior: Smooth cadence, drag precision, current force, metabolism cadence, and presentation publish budget; no authority-route change.

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

Layout proof: SeaglideStateDTO 64B, request/force 128B, counter/telemetry/audio/cavitation 64B explicit FieldOffset

Capacity: state/request/force/body/visual/audio/cavitation 1024; flow 64; telemetry 300; tuning/counter/cursor 1

Overflow/failure: force packets fail closed with FlagPacketOverflow/non-finite counters; presentation signals shed through SignalBus policy

Telemetry fields: request count, force packet count, non-finite count, thrust/drag/flow totals, max force, compute micros, quality, flags, last target, last flow, battery

Black-box fields: 300-entry SeaglideTelemetryEntry ring with idle/cadence/force-ready/invalid-delta/Vault-failure/lock-failure/prepare-failure heartbeat rows and solver rows, dumped to Docs/AgentLogs/Dump_SHINOBU_227.bin

Profiler marker: compute micros in telemetry; Unity profiler proof pending

GC proof required: Unity profiler/GCMonitor pending; static hot-path scan is clean

Native safety proof: `NativeDisableParallelForRestriction` is not used in the Seaglide hydrodynamics jobs; mutable rows are written through index-local `NativeArray[index]` paths with Unity parallel-for safety enabled.

Shutdown/disposal: Vault owns buffers; runtime releases generation handles and unregisters dispatcher ticks on disable/destroy

Scene unload behavior: OnDisable/OnDestroy completes pending solver teardown and unregisters tick/hot-swap hooks

Stale-handle behavior: generation handles are validated before resolve; failures fail closed

Rejected alternatives:

  [x] owner-local field

  [x] cached owner interface

  [ ] existing SignalBus lane

  [ ] existing Vault buffer

  [x] cold HectonEventBus hook

  [ ] no global route needed

Why this does not increase global monolith risk: Uses owner-scoped BufferID range 71660..71672 and existing typed SignalBus lanes; no new GlobalRegistry service and no HectonEventBus traffic.

H-Phi impact expected: lower than local native ownership because persistent state is Vault-owned and presentation lanes are bounded.

Proof required before GREEN: Unity import, compile with regenerated project files, Play Mode one-scooter smoke, profiler GC=0B hot path, telemetry dump readback.

Reviewer: Integrator / Global Authority reviewer

Review disposition: YELLOW static source; binary ledger row present; runtime proof pending; generated csproj currently omits new Seaglide sources

Status: PROPOSED

```
