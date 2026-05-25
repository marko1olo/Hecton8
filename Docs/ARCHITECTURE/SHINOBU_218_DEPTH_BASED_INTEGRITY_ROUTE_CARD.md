Route ID: SHINOBU_218_DEPTH_BASED_INTEGRITY_SOLVER

Date: 2026-05-20

Owner: SHINOBU_218

Owner domain: Habitat & Vehicles / Structural Integrity Math

Owning file/system: Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs

Status: YELLOW / STATIC SOURCE PATCHED / HABITAT DEFORMATION GENERATION HANDLE ROUTE PATCHED / CONTINUOUS HEALTH-PRESSURE QUALITY PATCHED / HULL JOB DETERMINISM PATCHED / UNITY RUNTIME PROOF PENDING

## R48 Exact Route Field Normalization

Route ID: SHINOBU_218_DEPTH_BASED_INTEGRITY_ROUTE_CARD

Owner: SHINOBU_218

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

Problem:

Legacy flat base-strength summation cannot model depth pressure, local buckling, support loss, or deterministic cascade failure.

Why owner-local data is insufficient:

Structural state is persistent, rollback-relevant, job-visible, cross-domain, and must feed UI/audio/fluid/presentation consumers without scene hierarchy traversal.

Why direct caller/owner interface is insufficient:

Multiple first-party consumers need breach/collapse notification, telemetry, and shader-visible snapshots across phase boundaries. A single direct caller interface would either poll or create concrete sibling references.

Instrument:

  [x] GlobalRegistry cold service/interface

  [x] SignalBus<T> first-party broadcast

  [ ] GlobalSignals bridge/direct queue

  [ ] HectonEventBus mod/API/cold event

  [x] GlobalDataVault / IDataVault

  [x] Black-box/telemetry route

Producer phase:

Simulation tick schedules Burst structural jobs. Visual sync completes ready fences through DispatcherJobFence and uploads shader data.

Consumer phase:

Signal consumers: downstream phase-specific consumers of `SignalBus<BaseIntegrityEventPayload>`, `SignalBus<FluidIncursionSignal>`, and `SignalBus<BaseModuleCompromisedSignal>`.

Shader consumers: visual sync / render.

Editor consumers: UI Toolkit tuner and SceneView gizmo only.

Cadence:

`framesBetweenUpdates = clamp((int)math.lerp(1, 30, 1.0f - GlobalQualityWeight), 1, 30)`.

Expected max events/reads per frame:

Max structural nodes: 4096.

Max directed CSR edges: 16384.

Max structural state upload: 4096 rows * 32 bytes, dirty-gated by telemetry state hash.

Max telemetry writes: 1 ring entry per scheduled solve.

Max signal pushes: at most one threshold transition per node per solve for each relevant structural signal.

Signal lane capacity:

- `BaseIntegrityEventPayload`: 64 expected / 256 max-frame / 32 survival-frame on lane hash `SIC1`.
- `FluidIncursionSignal`: 64 / 128 / 16 on FNV32("FluidIncursionSignal").
- `BaseModuleCompromisedSignal`: 64 / 64 / 16 on FNV32("BaseModuleCompromisedSignal").

GlobalQualityWeight behavior:

Low quality increases solve interval toward 30 frames, keeps nearest/fallback SDF anchor, and suppresses SDF cross taps through smoothstep.

Middle quality blends SDF cross taps gradually.

High/Ultra runs more frequent solves and full shader-visible buckling response.

Homeostasis pressure uses the same continuous quality route: `SystemHealthIndexSignal.Pressure01` shapes warning/critical ceilings through `math.smoothstep` and `math.lerp`; warning/critical states are fallback floors, not tier switches.

Payload/data shape:

`IntegrityStateDTO`: explicit 32 bytes, unmanaged, NativeArray/Vault/GPU upload.

`StructuralTuningDTO`: explicit 96 bytes, unmanaged, Vault tuning.

`StructuralTelemetryEntry`: explicit 64 bytes, unmanaged, 300-entry Vault ring.

`BaseIntegrityEventPayload`: explicit 64 bytes, unmanaged SignalBus payload.

Vault handle policy:

- Runtime persists only 16-byte `VaultGenerationHandle<T>` descriptors for structural buffers `70488-70497`.
- Each execution phase resolves method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`.
- No legacy pointer-bearing `VaultBufferHandle<T>` is retained across frames.
- No persistent `NativeArray<T>`, `NativeSlice<T>`, or raw Vault pointer is retained across frames.

Adjacent habitat-deformation cleanup:

- `HullIntegrityRuntime` now follows the same descriptor rule for its hull dent/deformation, breach jet, material strength, CSV scratch, telemetry, and pressure mirror lanes.
- It resolves phase-local views, validates capacities during boot, releases descriptors on failed boot/shutdown, and registers scheduled/cold clear handles through `H8Memory.RegisterActiveJob`.
- Scoped static scan summary is clean text only for legacy Vault handle/pointer patterns.
- Scope: `Assets/_Project/Scripts/Habitat/Deformation`.
- Required before current proof: artifact path, command/tool, timestamp, environment, output.

Adjacent quality/cache cleanup:

- `HullIntegrityRuntime` uses continuous health pressure before dent hysteresis.
- It sheds dent tracking and shader dent rows.
- Breach-jet camera refresh reads boot/hot-swap cached `IPlayerRuntimeContext`.
- Refresh method does not poll `GlobalRegistry.Player`.

Adjacent cold/debug cleanup:

Player builds do not implement/register/unregister structural or hull runtime on the cold dispatcher lane.

CSV tuning reload and CSV parser/file polling are editor-only. Black-box fault dump I/O remains available in player because it is fault-triggered only.

Adjacent Burst determinism cleanup:

All `HullIntegrityTypes.cs` jobs now use deterministic Burst mode. This covers SIP mutation, breach flags, deformation rows, pressure buckling dents, breach jet args, clear/copy utilities, and telemetry-affecting jobs.

Adjacent layout reflection cleanup:

`HullIntegrityRuntime.ValidateLayouts()` keeps `UnsafeUtility.SizeOf<T>()` DTO size checks in every build. Exact reflection-backed field offset checks compile under `UNITY_EDITOR` only, removing player boot metadata traversal while retaining editor fail-fast offset proof.

Managed fields present: no

UnityEngine.Object fields present: no

Layout proof:

`StructuralIntegrityLayout.Validate()` checks state, tuning, telemetry, material, dump header, event payload, and Core AUP sizes in every build.

Exact offset reflection is editor-only. Runtime boot fails closed on size drift; editor boot also fails closed on offset drift.

Capacity:

Vault buffers:

70488 StructuralIntegrityStates: 4096

70489 StructuralIntegrityNodeAups: 4096

70490 StructuralIntegrityCsrOffsets: 4097

70491 StructuralIntegrityCsrDestinations: 16384

70492 StructuralIntegrityEdgeFlags: 16384

70493 StructuralIntegrityTelemetryRing: 300

70494 StructuralIntegrityTelemetryCursor: 1

70495 StructuralIntegrityTuning: 1

70496 StructuralIntegrityMaterialStrengths: 32

70497 StructuralIntegrityCsvScratch: 16384 bytes

Collision note:

Structural buffer IDs moved from historical `70110-70119` to `70488-70497`.

- Reason: static audit found `HectonSeismicTideDirector` still owns raw celestial constants at `70110-70116`.
- Fix location: `H8Memory.cs`.
- No Environment source edited.

Overflow/failure:

Node and edge counts are clamped to Vault capacities.

Non-finite structural math sets `StateFlagNonFinite`, produces finite collapse-safe scalars, marks telemetry, and triggers dump.

Mass collapse marks telemetry and triggers dump.

Telemetry cursor is normalized before ring access.

Dirty upload fallback without telemetry does not seed the skip cache.

Telemetry fields:

Fields:

- frame, state hash, max pressure, max stress, active node count, edge count;
- critical node count, collapsed node count, quality, frames between updates;
- estimated microseconds, fault flags, weakest node hash, weakest buckling scalar, base hash, sequence.

Black-box fields:

Telemetry package: 300 `StructuralTelemetryEntry` rows, cursor, dump header, fault flags, state hash.

- Primary planned/generated-on-fault dump: `Docs/AgentLogs/Dump_SHINOBU_218.bin`.
- Secondary mirror: `Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin` for integrator crash triage.
- No artifact implied without timestamped trigger and output.

Profiler marker:

`H8.Habitat.StructuralIntegrity.Tick`

`H8.Habitat.StructuralIntegrity.LateFrame`

GC proof required:

Profiler/GCMonitor proof: `0 B/frame` on structural Tick and LateFrameTick steady state.

Player `ColdTick` is not registered and is compile-time no-op. Editor-only CSV polling and material CSV boot reads stay outside player hot-path proof.

Shutdown/disposal:

OnDisable forces scheduled handle completion through `DispatcherJobFence`, unregisters tickables, releases double-buffered GraphicsBuffers, releases structural `VaultGenerationHandle<T>` descriptors through `IDataVault.ReleaseBuffer`, resets upload cache, and clears active runtime.

Scene unload behavior:

Vault owns native buffers. Runtime releases its generation descriptors and GPU buffers during owner shutdown; DataVault invalidates stale generations and handles native memory lifetime.

Stale-handle behavior:

Runtime resolves generation descriptors after boot acquisition, validates lengths, and locks buffers before reads/writes. Failed boot calls the same release path. DataVault stale-handle proof remains runtime-pending.

Rejected alternatives:

  [x] owner-local field

  [x] cached owner interface

  [x] existing SignalBus lane

  [x] existing Vault buffer

  [x] cold HectonEventBus hook

  [ ] no global route needed

Rejected detail:

Owner-local fields are insufficient for rollback/job-visible state.

Direct Construction/BaseModule references are rejected to avoid sibling runtime coupling.

HectonEventBus is rejected because this is hot first-party gameplay.

Per-renderer MaterialPropertyBlock traversal is rejected for standard geometry; the route uses a global structured buffer.

SHINOBU_210 owns baked damage mesh state selection and intentionally keeps Stressed/Ruptured/Collapsed states reachable. SHINOBU_218 does not call that pressure-to-mesh resolver; pre-collapse structural deformation remains `BucklingScalar` plus the structural shader buffer.

Why this does not increase global monolith risk:

No new GlobalRegistry slot was added.

- GlobalRegistry use: cold DataVault discovery and tickable registration only.
- Cross-domain state: existing typed SignalBus lanes and existing Vault BufferID entries.
- Solver assembly has no direct sibling runtime references.

H-Phi impact expected:

Positive only by removing structural truth from scene hierarchy/PhysX and keeping one Vault-backed owner route. H-Phi is not used as acceptance proof.

Proof required before GREEN:

Unity import/Console clean for current workspace.

Play Mode bootstrap/world smoke.

Profiler capture showing structural Tick/LateFrame under 0.1 ms at 4096 nodes / 16384 edges or documented quality shedding.

GCMonitor proof of 0 B/frame on player hot path.

Frame Debugger/shader proof that `_HectonStructuralIntegrityStateBuffer` drives deformation without MPB/material instantiation.

Required fault-injection proof before GREEN: non-finite or mass collapse must write `Dump_SHINOBU_218.bin` and the structural-surgeon mirror, with artifact path, trigger, timestamp, environment, and output.

Global authority review:

Result: YELLOW

Owner: SHINOBU_218

Instrument: GlobalDataVault + SignalBus<T> + shader structured buffer + black-box telemetry.

Reason: Route is narrow and static-source documented, but runtime/Unity/profiler proof is still blocked by CPU/build gate.

Required fixes: collect compile/import/profiler/GC/shader/fault-injection artifacts when the build gate opens.

Proof still missing: Unity import, Play Mode, profiler, GCMonitor, Frame Debugger, player build.

Reviewer: SHINOBU_218 self-review, requires independent integrator review before GREEN.

Review disposition: `YELLOW / STATIC_SOURCE_ONLY`.
