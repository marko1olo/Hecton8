# Buoyancy Sleep State - SHINOBU_249

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
