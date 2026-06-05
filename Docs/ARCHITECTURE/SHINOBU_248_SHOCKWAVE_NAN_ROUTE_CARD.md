# SHINOBU_248 Shockwave NaN Route Card

Date: 2026-05-21

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING

Owner: SHINOBU_248 / SHOCKWAVE_NAN_AUDITOR_AND_LINK

Evidence: STATIC_SOURCE only. Unity import, Burst Inspector, Play Mode, profiler, GCMonitor, shader render, and player-build proof remain pending.

## Authority

One fact: explosive shockwave force and cavitation presentation scalars.

One owner: `AbyssalCavitationRuntime` under `SystemID.VehiclesPhysics`.

Prior `SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD` is historical for original buffer range and is archived at `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Architecture/Superseded_Route_Cards/SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD.md`; this card supersedes live NaN/cavitation route delta.

One route: GlobalDataVault DTO rows for shockwave/input/force/visual/telemetry, then `PhysicsApplySystem` drain and typed `SignalBus`.

Required proof route before GREEN: 300-entry `ShockwaveTelemetryEntry` ring, staged `Docs/AgentLogs/Dump_SHINOBU_248.bin` black-box dump path, and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`; static source visibility alone is not runtime proof.

| Field | Value |

|---|---|

| Route ID | `SHINOBU_248_SHOCKWAVE_NAN` |

| Owner | `SHINOBU_248 / SHOCKWAVE_NAN_AUDITOR_AND_LINK` with runtime owner `AbyssalCavitationRuntime` under `SystemID.VehiclesPhysics`. |

| Instrument | `GlobalDataVault` DTO rows for shockwave/input/force/visual/telemetry, existing `PhysicsApplySystem` force-packet drain, and typed `SignalBus` broadcasts. |

| Producer phase | `AbyssalCavitationRuntime.ScheduleSimulation(inputDependency)` after shockwave event ingestion. |

| Consumer phase | `PhysicsApplySystem.DrainCavitationForcePackets` after the job fence; shader consumers read visual sphere DTO/scalar output. |

| Cadence/capacity | Simulation-tick scheduled batch; capacity bounded by the Vault buffers listed below. |

| Overflow/failure | Saturation, non-finite math, or NaN guard hits increment counters, clamp through the math guard, and must be recorded into telemetry before GREEN. |

| Shutdown/disposal | Owning runtime/vault releases or clears buffers; this route card does not authorize private persistent native ownership. |

| Proof required before GREEN | Fresh Unity import, clean Console, Play Mode, profiler/GCMonitor, player build, 300-frame telemetry fault-path artifact, and fresh `Tools/Division_By_Zero_Scanner.py` output. |

| Review disposition | PENDING VERIFICATION / STATIC_SOURCE only under static-source review. |

## Vault Buffers

- `71560` `ShockwaveEvents`

- `71561` `ShockwaveCounters`

- `71562` `EntitySnapshots`

- `71563` `ForcePackets`

- `71571` `ForceTransportPackets`

- `71564` `VisualSpheres`

- `71565` `TelemetryRing`

- `71566` `OrdnanceProfiles`

- `71567` `CsvScratch`

- `71568` `Tuning`

- `71569` `SdfDescriptor`

- `71570` `SdfVoxels`

- All buffers are acquired from GlobalDataVault with `NativeArrayOptions.UninitializedMemory`.
- Runtime persists `VaultGenerationHandle<T>` descriptors only.
- Method-local views open through `IDataVault.TryResolveHandle(...)`.
- No private persistent native collection is introduced.
- No pointer-bearing `VaultBufferHandle<T>` handle is introduced.

## Hot Route

`ScheduleSimulation(inputDependency)` schedules:

1. `PropagateShockwavesJob`

2. `CompactShockwavesJob`

3. `EvaluateSanitizedShockwaveJob`

4. `UpdateCavityShaderParamsJob`

5. `RecordShockwaveTelemetryJob`

Scheduled handle is returned to callers and registered with H8Memory.

Hot simulation entry points fail closed through `IsRuntimeReady`; cold owner phases initialize Vault.

Main-thread force application stays in `PhysicsApplySystem.DrainCavitationForcePackets` after job fence completion.

Public writer entry points fail closed through `IsRuntimeReady`: `TryApplyTuning`, `TryWriteSdfVolume`, `TryClearSdfVolume`.

They do not cold-bootstrap Vault ownership. Residual `EnsureInitialized` calls are cold owner lifecycle, cold CSV load, editor refresh/mutator, or mock harness surfaces.

`DrainCavitationForcePackets` resolves `GlobalPhysicsStateManager` once per drain.

It tries packet `RigidbodySlot` first, then folded entity hash only when slot is stale/absent.

`PhysicsApplySystem.EnsureRuntimeInstance()` remains integrator debt; replacement needs force-sink injection API outside SHINOBU_248.

## Math Guard

The inverse-square denominator is:

`distanceSq = math.max(math.select(0f, rawDistanceSq, math.isfinite(rawDistanceSq)), tuning.EpsilonClampValue)`

Direction uses `delta * math.rsqrt(math.max(distanceSq, epsilon))` when radial vector is valid.

Exact-overlap epsilon-clamped cases use deterministic hash unit vector from entity hash, source hash, frame index, and SHINOBU_248 salt. Epsilon path increments `EpsilonClampCount`.

Non-finite force response:

- Clear force vector.
- Clear `forceSq`.
- Run cleanup before the active-packet gate.
- Prevent stale NaN comparison state from reaching the drain.

Shockwave active checks reject non-finite radius, max radius, peak pressure, expansion speed, and epicenter AUP before propagation/evaluation/visual upload.

## Dear Lie

Visual cavitation bubble is not fluid simulation. CPU writes `CavitationVisualSphereDTO` rows to shader buffer.

`Hecton8_UberNoir` consumes radius, pressure intensity, age, quality, and phase to fake refraction/collapse.

## Black-Box Dump

Editor/development builds register a cold `Application.logMessageReceived` fault hook.

Exceptions/errors/asserts attempt one reentrant-guarded dump when no writer job is active. `TryDumpBlackBox` writes `.tmp`, then replaces/moves final artifact with delete+move fallback.

## Compile Wall

Asmdef boundary:

- No new asmdef.
- No direct sibling runtime dependency.
- Current code uses existing Core/World AUP contracts on the monolithic project surface.
- If Cavitation becomes a dedicated asmdef, AUP conversion must move behind a Contracts DTO or cached owner interface before approval.

## Verification

Latest static scanner: `Tools/Division_By_Zero_Scanner.py`

- Errors: `0`
- Out-of-domain warnings: `68`
- Info: `62`
- Cavitation runtime errors: `0`
- Focused descriptor scan: no `VaultBufferHandle`, `GetBufferHandle`, `TryGetBufferHandle`, `.Resolve(...)`, `.ptr`, `ResolvePointer`, `GetElementAsRef`, or standalone `GenerationID` residue in Cavitation/editor scope.

Compile was not launched because the latest CPU gate sampled `99%`, above the project limit of `50%`; no `dotnet`/`csc` process was active at that sample.
