# SHINOBU_251 Submarine Added Mass Route Card

Date: 2026-05-21

Owner: SHINOBU_251 / SUBMARINE_ADDED_MASS_SOLVER

Status: YELLOW / STATIC SOURCE WIRED / COMPILE BLOCKED BY EXISTING PROJECT FILE GAP

## Boundary

`SubmarineDynamicsRuntime` owns the added-mass tensor route for large submarine inertia.

It does not own player input, flood truth, water rendering, Rigidbody presentation, vehicle damage, or habitat fluid incursion.

Those systems provide scalar snapshots or signals. This route applies hydrodynamic inertia in the submarine simulation lane.

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

`Shinobu251CsvScratch` / `71733` is reserved for cold CSV scratch. Runtime reuses existing submarine CSV byte route and allocates no private persistent native array.

## Human Tuning / Import

- `Data/Physics/vehicle_hull_profiles.csv` is the literal designer hull profile source for this route.
- Runtime reads it only when no simulation job or Vault lock is active.
- It stages bytes on stack as bounded `ReadOnlySpan<byte>`.
- Profile name hash: FNV-1a.
- Output: `SubmarineHullProfileDTO` rows into `Shinobu251HullProfiles`.

Tensor fidelity call sites consume the quality/LOD overload directly.

No `ResolveTensorBlend` overload accepts `HardwareTier`. `HardwareTier` remains fixed-offset compatibility fields in old submarine DTOs and is not consumed by SHINOBU_251.

Runtime handle model:

- No pointer-bearing `VaultBufferHandle<T>` descriptors.
- Added-mass, telemetry, hull profile, tuning, kinematic state, config, force, PID, and borrowed damage lanes use `VaultGenerationHandle<T>`.
- Hot fixed-phase jobs receive method-local `NativeArray<T>` views resolved immediately before scheduling.
- Editor reads use `TryReadHandle`.
- Raw pointers are not kept across frames.

Runtime writer fences use generation-handle `IDataVault.TryAcquireWriteLock` and `ReleaseWriteLock`.

No SHINOBU runtime path uses raw `TryLockBuffer` / `TryUnlockBuffer` after descriptor migration.

Density and cavitation acoustic hot paths use typed `SignalBus` lanes, not `GlobalSignals` latest-state/publish bridges.

Boot/default initialization follows the same fence rule.

`EnsureVaultBuffers` reads config/tuning state. Default tuning/profile writes happen only inside generation write-lock helpers.

`AddedMassProfileDTO` and `SubmarineHydrodynamicsTelemetry` buffers are not touched during boot; their `UninitializedMemory` lanes are job-written.

Volcanic updraft force injection is not a SHINOBU-owned fact route.

Direct World-domain call was removed from `SubmarineDynamicsRuntime`. World needs typed SignalBus/DataVault bridge before reintroducing updraft forces.

The editor facade and scanner are isolated in `Hecton8.Physics.Vehicles.Editor.asmdef` with `includePlatforms: Editor`, preventing `UnityEditor` references from entering `Hecton8.Core` player/runtime compilation.

Scanner sidecar:

- Primary artifact: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json`.
- Shared report update: replace/append one top-level `shinobu251SubmarineAddedMassScanner` object.
- Rejected: overwriting another agent's report body.
- Scanner core: Roslyn AST assignment/prefix/postfix analysis plus comment/string-aware token fallback.
- Roslyn DLL refs stay isolated to `Hecton8.Physics.Vehicles.Editor.asmdef`.

## Layout Proof

`AddedMassProfileDTO` is explicit `128` bytes: `LinearAddedMass` offset `0`, `AngularAddedMass` offset `64`.

Each field is one `float4x4` cache line. No properties or managed references are present.

`SubmarineHydrodynamicsTelemetry` is explicit 128 bytes. It holds AUP, depth, mass, traces, quality, damping, frame, flags, hashes, `BurstElapsedUs@88`, density scalar `@92`, and padding.

- Fault dump route writes a 16-byte `AM25` unmanaged header.
- It then writes raw `SubmarineHydrodynamicsTelemetry` row bytes through `ReadOnlySpan<byte>`.
- SHINOBU_251 no longer writes legacy SHINOBU_11 artifacts from this runtime path.
- It no longer writes `Dump_SUB_KINEMATICS` artifacts here.

`SubmarineAddedMassTuningDTO` and `SubmarineHullProfileDTO` are explicit 64-byte DTOs. They are snapshot/tuning surfaces, not gameplay authority owners.

## Dear Lie

Dear Lie scope:

- Rejected: water particles.
- Rejected: hull skin friction integrals.
- Rejected: Navier-Stokes volume fields.
- Calculates analytical added-mass tensors from hull volume, depth scalar, flood mass, and orientation.
- Angular damping derives from tensor trace.
- Complexity: `O(n)` for `n` submarines, not `O(n * fluid samples)`.

## Verification Boundary

Static source and docs are wired.

Previous guarded compile stopped early because `Hecton8.Core.csproj` references missing `IBuildPlacementRule.cs`. No runtime/profiler/GC/import/player proof is claimed.
