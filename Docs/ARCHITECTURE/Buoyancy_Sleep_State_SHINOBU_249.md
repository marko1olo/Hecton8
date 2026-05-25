# Buoyancy Sleep State - SHINOBU_249

Owner: Physics / Buoyancy.

Authority route:
- Runtime truth lives in Vault buffers owned by `BuoyancyDisplacementRuntime`.
- Hot sleep decisions are Burst jobs over unmanaged DTO rows only.
- Wake requests use `SignalBus<WakeRequestSignal>` snapshots. No hot `GlobalRegistry` polling.
- `KinematicStateDTO` sleep jobs are KCC-owned kernel artifacts for the prompt contract; the active buoyancy route does not schedule them or write KCC state.

Buffers:

- `ShinobuBuoyancyStates`: 50,000 `BuoyancyStateDTO` rows, 64 bytes each.

- `ShinobuBuoyancySleepSdfDensity`: signed byte SDF contact samples.

- `ShinobuBuoyancySleepSdfConfig`: one 64-byte SDF/contact config row.
- `ShinobuBuoyancySleepTelemetryRing`: 300 `SleepStateTelemetryEntry` rows.
- `ShinobuBuoyancyMaterialSettlingProfiles`: cold-loaded material sleep thresholds.
- Cold source file: `Assets/_Project/Data/Physics/material_settling_profiles.csv`; runtime Burst jobs read only the Vault profile rows.

Rules:
- Sleep is a bit flag, not an active/inactive array move.

- SDF sampling subtracts grid `double3` AUP from entity `double3` AUP before float local sampling and requires `abs(signedDistance) <= contactEpsilon`.
- Angular settling uses `BuoyancyStateDTO.AngularSpeedSq` at offset 56; the state remains 64 bytes.
- `GlobalQualityWeight` continuously scales sleep aggression and current polling cadence.
- Authored flow samples are optional. Missing flow data falls back to deterministic analytic flow instead of aborting the evaluator.
- Deep sleep raises `FlagStaticPromotionPending`; render ownership must demote before any wake-driven dynamic update.
- Fixed-tick gameplay phases fail closed when Vault handles are not already cold-booted; no descriptor recovery/allocation runs in the simulation tick.
