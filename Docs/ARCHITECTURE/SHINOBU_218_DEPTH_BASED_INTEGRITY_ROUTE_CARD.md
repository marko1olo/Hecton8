Route ID: SHINOBU_218_DEPTH_BASED_INTEGRITY_SOLVER
Date: 2026-05-20
Owner: SHINOBU_218
Owner domain: Habitat & Vehicles / Structural Integrity Math
Owning file/system: Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs
Status: YELLOW / STATIC SOURCE PATCHED / HABITAT DEFORMATION GENERATION HANDLE ROUTE PATCHED / CONTINUOUS HEALTH-PRESSURE QUALITY PATCHED / HULL JOB DETERMINISM PATCHED / UNITY RUNTIME PROOF PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R45): `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` is the latest local static root/architecture R43/R44 residue, proof-artifact wording, source-counter, and atlas-boundary correction. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
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
Signal lane capacity: `BaseIntegrityEventPayload` 64 expected / 256 max-frame / 32 survival-frame on lane hash `SIC1`; `FluidIncursionSignal` 64 / 128 / 16 on FNV32("FluidIncursionSignal"); `BaseModuleCompromisedSignal` 64 / 64 / 16 on FNV32("BaseModuleCompromisedSignal").

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
Runtime persists only 16-byte `VaultGenerationHandle<T>` descriptors for structural buffers `70488-70497`. Every execution phase resolves method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`; no legacy pointer-bearing `VaultBufferHandle<T>`, persistent `NativeArray<T>`, `NativeSlice<T>`, or raw Vault pointer is retained across frames.

Adjacent habitat-deformation cleanup:
`HullIntegrityRuntime` now follows the same descriptor rule for its hull dent/deformation, breach jet, material strength, CSV scratch, telemetry, and pressure mirror lanes. It resolves phase-local views, validates capacities during boot, releases descriptors on failed boot/shutdown, and registers scheduled/cold clear handles through `H8Memory.RegisterActiveJob`. The scoped static scan summary is recorded as clean text only for legacy Vault handle/pointer patterns across `Assets/_Project/Scripts/Habitat/Deformation`; link artifact path, command/tool, timestamp, environment, and output before treating it as current proof.

Adjacent quality/cache cleanup:
`HullIntegrityRuntime` now uses continuous health pressure to shed dent tracking and shader dent rows before dent hysteresis. Breach-jet camera refresh reads a boot/hot-swap cached `IPlayerRuntimeContext`; the refresh method does not poll `GlobalRegistry.Player`.

Adjacent cold/debug cleanup:
Player builds do not implement/register/unregister the structural or hull runtime on the cold dispatcher lane. CSV tuning hot reload and CSV parser/file polling code are editor-only; black-box fault dump I/O remains available in player because it is only triggered on fault.

Adjacent Burst determinism cleanup:
All `HullIntegrityTypes.cs` jobs now use deterministic Burst mode. This covers SIP mutation, breach flags, deformation rows, pressure buckling dents, breach jet args, clear/copy utilities, and telemetry-affecting jobs.

Adjacent layout reflection cleanup:
`HullIntegrityRuntime.ValidateLayouts()` keeps `UnsafeUtility.SizeOf<T>()` DTO size checks in every build. Exact reflection-backed field offset checks compile under `UNITY_EDITOR` only, removing player boot metadata traversal while retaining editor fail-fast offset proof.

Managed fields present: no
UnityEngine.Object fields present: no

Layout proof:
`StructuralIntegrityLayout.Validate()` checks state, tuning, telemetry, material, dump header, event payload, and Core AUP sizes in every build. Exact offset reflection is editor-only. Runtime boot fails closed on size drift; editor boot also fails closed on offset drift.

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
Structural buffer IDs moved from the historical `70110-70119` range to `70488-70497` after static audit found `HectonSeismicTideDirector` still owns raw celestial constants at `70110-70116`. This is a critical cross-domain interface fix in `H8Memory.cs`; no Environment source was edited.

Overflow/failure:
Node and edge counts are clamped to Vault capacities.
Non-finite structural math sets `StateFlagNonFinite`, produces finite collapse-safe scalars, marks telemetry, and triggers dump.
Mass collapse marks telemetry and triggers dump.
Telemetry cursor is normalized before ring access.
Dirty upload fallback without telemetry does not seed the skip cache.

Telemetry fields:
Frame, state hash, max pressure, max stress, active node count, edge count, critical node count, collapsed node count, quality, frames between updates, estimated microseconds, fault flags, weakest node hash, weakest buckling scalar, base hash, sequence.

Black-box fields:
300 `StructuralTelemetryEntry` rows, cursor, dump header, fault flags, state hash. Primary owner dump: `Docs/AgentLogs/Dump_SHINOBU_218.bin`. Secondary structural-surgeon mirror: `Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin` for integrator crash triage.

Profiler marker:
`H8.Habitat.StructuralIntegrity.Tick`
`H8.Habitat.StructuralIntegrity.LateFrame`

GC proof required:
Profiler/GCMonitor proof of 0 B/frame on structural Tick and LateFrameTick steady state. Player `ColdTick` is not registered and is a compile-time no-op; editor-only cold CSV polling and structural material CSV boot reads remain excluded from player hot-path proof.

Shutdown/disposal:
OnDisable forces scheduled handle completion through `DispatcherJobFence`, unregisters tickables, releases double-buffered GraphicsBuffers, releases structural `VaultGenerationHandle<T>` descriptors through `IDataVault.ReleaseBuffer`, resets upload cache, and clears active runtime.

Scene unload behavior:
Vault owns native buffers. Runtime releases its generation descriptors and GPU buffers during owner shutdown; DataVault invalidates stale generations and handles native memory lifetime.

Stale-handle behavior:
Runtime resolves generation descriptors only after boot acquisition, validates required lengths, and locks buffers before reads/writes. Failed boot after handle acquisition calls the same release path. DataVault generation/stale-handle proof remains runtime-pending.

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
Pressure-driven Stressed/Ruptured mesh selection is rejected before collapse; `ResolveVisualBuckling01`/`BucklingScalar` carry the pre-collapse Dear Lie.

Why this does not increase global monolith risk:
No new GlobalRegistry slot was added. GlobalRegistry is used only for cold DataVault discovery and tickable registration. Cross-domain state uses existing typed SignalBus lanes and existing Vault BufferID entries. The solver assembly has no direct sibling runtime references.

H-Phi impact expected:
Positive only by removing structural truth from scene hierarchy/PhysX and keeping one Vault-backed owner route. H-Phi is not used as acceptance proof.

Proof required before GREEN:
Unity import/Console clean for current workspace.
Play Mode bootstrap/world smoke.
Profiler capture showing structural Tick/LateFrame under 0.1 ms at 4096 nodes / 16384 edges or documented quality shedding.
GCMonitor proof of 0 B/frame on player hot path.
Frame Debugger/shader proof that `_HectonStructuralIntegrityStateBuffer` drives deformation without MPB/material instantiation.
Fault injection proof that non-finite or mass collapse writes `Dump_SHINOBU_218.bin` and the structural-surgeon mirror.

Global authority review:
Result: YELLOW
Owner: SHINOBU_218
Instrument: GlobalDataVault + SignalBus<T> + shader structured buffer + black-box telemetry.
Reason: Route is narrow and static-source documented, but runtime/Unity/profiler proof is still blocked by CPU/build gate.
Required fixes: collect compile/import/profiler/GC/shader/fault-injection artifacts when the build gate opens.
Proof still missing: Unity import, Play Mode, profiler, GCMonitor, Frame Debugger, player build.
Reviewer: SHINOBU_218 self-review, requires independent integrator review before GREEN.
Review disposition: `YELLOW / STATIC_SOURCE_ONLY`.
