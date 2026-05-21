# SHINOBU_251 Submarine Added Mass Route Card

Date: 2026-05-21
Owner: SHINOBU_251 / SUBMARINE_ADDED_MASS_SOLVER
Status: YELLOW / STATIC SOURCE WIRED / COMPILE BLOCKED BY EXISTING PROJECT FILE GAP

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

## Boundary

`SubmarineDynamicsRuntime` owns the added-mass tensor route for large submarine inertia. It does not own player input, flood compartment truth, water rendering, rigidbody presentation, vehicle damage, or habitat fluid incursion. Those systems provide scalar snapshots or signals; this route applies hydrodynamic inertia in the submarine vehicle simulation lane.

## Route

| Field | Value |
|---|---|
| Route ID | `SHINOBU_251_SUBMARINE_ADDED_MASS` |
| Instrument | `GlobalDataVault` buffers plus 300-frame black-box telemetry |
| Producer phase | `FixedTick` schedules `CalculateAddedMassTensorJob` before `Submarine6DIntegratorJob` |
| Consumer phase | same fixed simulation chain; editor-only tuner/gizmo read after jobs are not pending |
| Cadence/capacity | fixed simulation cadence; capacity follows `vehicleCapacity`; telemetry is `vehicleCapacity * 300` entries |
| Job granularity | runtime simulation chain uses batched jobs only; optional mock flood signal uses quality-weighted deterministic `SignalBus<MockFloodSignal>.TryPush` and does not schedule a tiny producer job |
| Signal frame limits | local lanes configure survival/max capacities; core `SignalBus` interpolates frame limits from `GlobalQualityWeight`, not a hardware-tier branch |
| Slow-solver cadence | `GlobalQualityWeight` maps through smoothstep to a 0.25..1.0 update fraction; deterministic frame/entity hash dither decides skipped dead-reckoning frames; tensor LOD hold targets `lerp(2,0,updateFraction)` |
| Optional mock/telemetry cadence | mock flood probability maps through smoothstep from 1/96 to 1/16 per frame; local Vault telemetry stride lerps 4..1 and frame-dithers floor/ceil |
| GlobalQualityWeight behavior | continuous tensor blend: low quality uses diagonal division, high quality blends toward full matrix inverse; payload layout and authority route do not change |
| Flood scalar behavior | `FloodVolumeScalar` is a finite 0..3 tuning gate; value `0` disables flood-volume tensor inflation without changing DTO layout or flood fact ownership |
| Overflow/failure | non-finite tensors fall back to deterministic diagonal matrices; generated-on-fault dump target is the single SHINOBU proof artifact `Docs/AgentLogs/Dump_SHINOBU_251.bin` |
| Shutdown/disposal | Vault owns native memory; runtime unlocks handles and unregisters ticks on disable |
| Vault descriptor policy | runtime persists `VaultGenerationHandle<T>` descriptors only; each phase resolves method-local `NativeArray<T>` views through `TryResolveHandle` / `TryReadHandle`; write fences use generation-handle `TryAcquireWriteLock` / `ReleaseWriteLock` |
| Signal route | fluid density is consumed through `SignalBus<FluidDensityChangedSignal>.GetFrameSnapshot()`; cavitation acoustic pings publish through `SignalBus<AcousticPingSignal>.TryPush` with `AM25` source id |
| Dependency blocker | volcanic updraft force injection is `[BLOCKED BY DEPENDENCY]`; SHINOBU runtime has no direct `Hecton8.World` reference and waits for a World-owned typed SignalBus/DataVault bridge |
| Proof required before GREEN | Unity import, compile after project-file blocker is resolved, Play Mode smoke, Burst/GC profiler proof, dump readback |
| Review disposition | PENDING |

## Vault Buffers

| BufferID | Element | Capacity | Options |
|---|---|---:|---|
| `Shinobu251AddedMassProfiles` / `71730` | `AddedMassProfileDTO` | `vehicleCapacity` | `UninitializedMemory`; fully overwritten by tensor job |
| `Shinobu251HydrodynamicsTelemetry` / `71731` | `SubmarineHydrodynamicsTelemetry` | `vehicleCapacity * 300` | `UninitializedMemory`; ring entries overwritten per frame |
| `Shinobu251HullProfiles` / `71732` | `SubmarineHullProfileDTO` | `vehicleCapacity` | `ClearMemory`; cold CSV/default profile lane |
| `Shinobu251AddedMassTuning` / `71734` | `SubmarineAddedMassTuningDTO` | `1` | `ClearMemory`; editor/cold tuning lane |

`Shinobu251CsvScratch` / `71733` is reserved for the cold CSV scratch lane. The current runtime reuses the existing submarine CSV byte route and does not allocate a private persistent native array for it.

## Human Tuning / Import

`Data/Physics/vehicle_hull_profiles.csv` is the literal designer hull profile source for this route. The runtime reads it only when no simulation job or Vault lock is active, stages the bytes on the stack as a bounded `ReadOnlySpan<byte>`, hashes the profile name with FNV-1a, and writes `SubmarineHullProfileDTO` rows into `Shinobu251HullProfiles`.

Tensor fidelity call sites consume the quality/LOD overload directly. No `ResolveTensorBlend` overload accepts `HardwareTier`; `HardwareTier` remains only as fixed-offset compatibility fields in pre-existing submarine DTOs and is not assigned or consumed by the SHINOBU_251 solve route.

The runtime no longer stores pointer-bearing `VaultBufferHandle<T>` descriptors. Added-mass, hydrodynamic telemetry, hull profile, tuning, kinematic state, config, force, PID, and borrowed vehicle-damage lanes are represented as `VaultGenerationHandle<T>` fields. Hot fixed-phase jobs receive method-local `NativeArray<T>` views resolved immediately before scheduling; editor reads use `TryReadHandle` and never keep raw pointers across frames.

Runtime writer fences are acquired through generation-handle `IDataVault.TryAcquireWriteLock` and released through `ReleaseWriteLock`; no SHINOBU runtime path uses raw `TryLockBuffer` / `TryUnlockBuffer` after the descriptor migration. The density and cavitation acoustic hot paths use typed `SignalBus` lanes rather than `GlobalSignals` latest-state or publish bridges.

Boot/default initialization follows the same fence rule: `EnsureVaultBuffers` reads config/tuning state, then default tuning/profile writes are performed only inside generation write-lock helpers. `AddedMassProfileDTO` and `SubmarineHydrodynamicsTelemetry` buffers are not touched during boot initialization because their `UninitializedMemory` lanes are fully written by the scheduled owner jobs.

Volcanic updraft force injection is not a SHINOBU-owned fact route. The direct World-domain call was removed from `SubmarineDynamicsRuntime`; World must expose a typed SignalBus/DataVault bridge before updraft forces can be reintroduced without breaking compile-wall isolation.

The editor facade and scanner are isolated in `Hecton8.Physics.Vehicles.Editor.asmdef` with `includePlatforms: Editor`, preventing `UnityEditor` references from entering `Hecton8.Core` player/runtime compilation.

The scanner sidecar is `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json`. When the editor scanner/audit is executed, it updates the shared `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` by replacing or appending one top-level `shinobu251SubmarineAddedMassScanner` object rather than overwriting another agent's report body. The scanner core uses Roslyn AST assignment/prefix/postfix analysis with a comment/string-aware token fallback; Roslyn DLL references are isolated to `Hecton8.Physics.Vehicles.Editor.asmdef`.

## Layout Proof

`AddedMassProfileDTO` is explicit 128 bytes: `LinearAddedMass` at offset `0`, `AngularAddedMass` at offset `64`. Each field is a `float4x4` occupying one 64-byte cache line. No properties or managed references are present.

`SubmarineHydrodynamicsTelemetry` is explicit 128 bytes and holds AUP, depth, density, displaced/flood mass, diagonal traces, quality blend, damping, frame, flags, state/tensor hashes, `BurstElapsedUs` at offset 88, density scalar at offset 92, and padding.

The fault dump route writes a 16-byte `AM25` unmanaged header followed by raw `SubmarineHydrodynamicsTelemetry` row bytes through `ReadOnlySpan<byte>`. SHINOBU_251 no longer writes legacy SHINOBU_11 or `Dump_SUB_KINEMATICS` artifacts from this runtime path.

`SubmarineAddedMassTuningDTO` and `SubmarineHullProfileDTO` are explicit 64-byte DTOs. They are snapshot/tuning surfaces, not gameplay authority owners.

## Dear Lie

The route does not simulate water particles, hull skin friction integrals, or Navier-Stokes volume fields. It calculates analytical added-mass tensors from hull volume, depth scalar, flood mass, and orientation, then uses exponential angular damping derived from tensor trace. Complexity stays `O(n)` for `n` submarines instead of `O(n * fluid samples)`.

## Verification Boundary

Static source and docs are wired. The previous guarded compile attempt stopped before changed source compilation because `Hecton8.Core.csproj` references missing `Assets/_Project/Scripts/IBuildPlacementRule.cs`. No runtime, profiler, GCMonitor, Unity import, or player-build proof is claimed by this route card.
