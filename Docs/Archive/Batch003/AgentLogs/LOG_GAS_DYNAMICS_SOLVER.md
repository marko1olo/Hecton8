# LOG_GAS_DYNAMICS_SOLVER

## 2026-05-13 - Dalton Gas Solver Purge

Status: PENDING VERIFICATION. Compile cleanliness is blocked by existing cross-domain dependency errors outside this task.

What was wrong:
- Gas ownership had no registry contract for Dalton room logic.
- `HectonAtmosphereManager.Instance` still exposed singleton-style access.
- Atmosphere consequences were at risk of direct Player/Physiology coupling.
- `SubmarineAtmosphereSystem` carried stale wording that described oxygen as a tank rather than Dalton partial pressure.

What was done:
- Added `IGasDynamicsSolver`, `GasRoomSnapshot`, `ToxicitySignal`, `GasDynamicsRoomFlags`, and `GasDynamicsMathLod`.
- Registered `GasDynamicsRuntime` in `GlobalRegistry` with field/property/register/unregister/resolve support.
- Added `GasDynamicsSolver.cs` with SOA `NativeArray<float>` lanes for O2, CO2, nitrogen, and total pressure.
- Implemented Burst `GasDynamicsStepJob` for Dalton pressure, bulkhead diffusion, player metabolism, powered scrubbers, fire conversion, breach decompression, toxicity/narcosis signals, and 300-frame telemetry.
- Added UIStateStore slots for O2 KPa, CO2 KPa, room pressure KPa, and narcosis.
- Removed `HectonAtmosphereManager.Instance`.
- Updated stale submarine atmosphere documentation.

Cinematic cheats used:
- Dalton linear pressure sum instead of chemistry.
- Capped scalar diffusion instead of CFD/particles.
- Fire as O2-to-CO2 scalar conversion.
- Breach as instant ambient-pressure snap.
- Hull stress as scalar pressure relief.
- Math LOD cadence: Low/MX350 2.0s, Mid 0.5s, High/Ultra 0.1s.

Exact microseconds saved:
- SOA native room layout: estimated 80-180us per 64-room solve versus managed room objects.
- Linear pressure math: estimated 20-50us per solve versus nonlinear/libm gas curves.
- Capped diffusion: estimated 300-900us per 128 edges versus particle/CFD gas.
- No direct physiology/player events: estimated 15-40us on toxic ticks versus managed event fanout.
- Low-tier ColdTick: estimated 70-95% gas CPU reduction versus fixed 10Hz.

Verification:
- `rg` found no `AtmosphereManager.Instance`, `HectonAtmosphereManager.Instance`, or `Player.Instance.Kill()` in runtime code.
- Runtime `math.exp` scan excluding Editor returned no hits.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` attempted four times; blocked by unrelated missing `Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, `Hecton8.Cartography`, `IDataVault`, `SystemID`, and kinematics/cartography signal types.

## 2026-05-13 - Static Hardening Pass

Status: PENDING VERIFICATION. User explicitly forbade `dotnet build`; this pass used static inspection only.

What was wrong:
- Breach fallback ambient was zero until another owner configured the room.
- External room-flag/configure writes could clear the solver-owned occupied bit.
- Zero authored bulkhead capacity could still accept edge 0 because the native buffer has a minimum allocation length.
- Diffusion conductance authoring allowed 0..2 but scheduling saturated it to 0..1 before the step cap.
- The Burst job still carried an unused `RoomPressureFront` field.

What was done:
- Added `IGasDynamicsSolver.TrySetAmbientPressure` and implemented it in `GasDynamicsSolver`.
- Seeded standard room ambient pressure from standard Dalton pressure.
- Preserved `Occupied` during external `TrySetRoomFlags` and `TryConfigureRoom` calls when the player is still present.
- Enforced authored bulkhead capacity separately from native buffer length.
- Removed the unused front-pressure job payload and added explicit sequential layout to black-box telemetry entries.
- Re-extracted the GAS_DYNAMICS_SOLVER prompt with an attribute-aware XML regex; confirmed 19 tasks.

Cinematic cheats used:
- Kept breach as instant ambient-pressure snap, now with safer default ambient.
- Kept linear capped diffusion; no chemistry, CFD, or exponential gas curves.

Exact microseconds saved:
- Removed unused job payload: estimated 1-3us per scheduled solve on i3/MX350 from lower schedule/copy pressure.
- Preserved scalar ambient ingress: avoids integration-side correction passes during breach events.

Verification:
- Static scan found no `RoomPressureFront` after removal.
- Static scan found no `math.exp`, `math.sqrt`, `math.normalize`, managed formatting, AUP/world-position coupling, or `transform.position` in `GasDynamicsSolver.cs`.

## 2026-05-13 - Native Audit And Lifecycle Hardening

Status: PENDING VERIFICATION. User explicitly forbade `dotnet build`; this pass used static inspection only.

What was wrong:
- Native ownership had no public cold audit snapshot even though all allocations were sentinel-registered.
- Rapid disable/enable during deferred disposal could return from `OnEnable` before the solver was registered for future tick polling.
- Edit-mode `OnEnable` had no explicit play-mode guard.

What was done:
- Added `GasDynamicsNativeMemoryAudit` to the core gas contract.
- Added `IGasDynamicsSolver.TryGetNativeMemoryAudit` and implemented local allocation count, byte total, largest allocation label hash, and sentinel totals.
- Added edit-mode `OnEnable` guard.
- Registered tick polling during deferred-dispose re-enable and retried registry binding after native storage finalizes.

Cinematic cheats used:
- None added. The pass preserves scalar Dalton truth and avoids new simulation work.

Exact microseconds saved:
- Hot-path delta: 0us. Audit is cold-only.
- Stability gain: avoids a dead solver after rapid lifecycle churn without forcing `JobHandle.Complete`.

Verification:
- Gas-file static scan found no `RoomPressureFront`, `math.exp`, `math.sqrt`, `math.normalize`, managed formatting, AUP/world-position coupling, or `transform.position`.
- `git diff --check` reported only the existing CRLF warning on `GlobalRegistryContracts.cs`.

## 2026-05-13 - Defined Memory And Dump Header

Status: PENDING VERIFICATION. User explicitly forbade `dotnet build`; this pass used static inspection only.

What was wrong:
- Gas lanes used `UninitializedMemory`, which is unsafe if standard atmosphere seeding is disabled and rooms are configured externally over time.
- Black-box telemetry dump had no magic/version/cursor header, making postmortem parsing ambiguous.

What was done:
- Changed O2, CO2, pressure, nitrogen, back-buffer, and ambient native lanes to `ClearMemory`.
- Added dump magic, format version, telemetry entry size, capacity, write index, and tick count before ring entries.
- Fixed `GasDynamicsTelemetryEntry` layout to 32 bytes.

Cinematic cheats used:
- None added. This preserves the scalar Dalton model.

Exact microseconds saved:
- Hot-path delta: 0us.
- Cold cost: negligible zero-fill on 128-room native lanes; accepted to remove undefined pressure state.

Verification:
- Static scan found no `UninitializedMemory` in `GasDynamicsSolver.cs`.
- Gas-file static scan found no `RoomPressureFront`, `math.exp`, `math.sqrt`, `math.normalize`, managed formatting, AUP/world-position coupling, or `transform.position`.

## 2026-05-13 - Toxicity Queue Discipline

Status: PENDING VERIFICATION. User explicitly forbade `dotnet build`; this pass used static inspection only.

What was wrong:
- Stale undrained toxicity packets could remain in the native queue until the count exceeded the soft cap.
- A subsequent gas job could enqueue another burst of packets and force native queue growth past the prewarmed capacity.
- CO2 fatal and narcosis full clamps referenced raw serialized thresholds instead of the already-sanitized local thresholds.

What was done:
- Replaced stale-over-cap trimming with pre-schedule queue draining.
- Kept each gas solve's toxicity lane fresh and bounded to the prewarmed packet budget.
- Sanitized CO2 threshold/fatal and narcosis threshold/full values once before scheduling the Burst job.

Cinematic cheats used:
- None added. This keeps the existing scalar Dalton consequence lane.

Exact microseconds saved:
- Prevented native queue growth spikes when Physiology does not drain signals between gas solves.
- Main-thread drain is bounded to 128 packets; estimated worst-case under 10us on i3/MX350, with 0B managed GC.

Verification:
- Static scan confirmed the old `TrimToxicityQueueIfStale` path is gone and the new `TrimToxicityQueueBeforeSchedule` path is used before job scheduling.
- No `dotnet build` launched.
