# Vehicle Component Damage Router

Owner: SHINOBU_152
Domain: Echelon 5 vehicle localized damage truth

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Global Authority Route Card

Evidence class: STATIC_SOURCE route-card completion only. This block does not prove Unity import, Burst compile, Play Mode, profiler, GCMonitor, player build, or vehicle damage correctness.

| Field | Value |
|---|---|
| Owner | SHINOBU_152 / Vehicle component damage router |
| First-20-minutes route moment | Hazard damage feedback only if the Copper Wire route reaches vehicle/submarine integrity; otherwise parked |
| Authority surface | GlobalDataVault buffers `71640`-`71649`; combat AUP impacts enter through signal payloads and publish `VehicleDamageStateDTO` |
| Producer phase | `SIMULATION` for damage grid writes and component-state solve |
| Consumer phase | `POST_SIMULATION` for state publication; downstream hydrodynamics consumes the DTO after publication |
| Cadence | Fixed simulation cadence for damage truth; editor tuner is cold/editor-only |
| Capacity | Default `16 x 6 x 8` cell grid, bounded impact buffer, telemetry ring `300` |
| Overflow policy | Bounded impact ingestion; excess or invalid impacts must fail closed with telemetry rather than grow managed collections |
| Failure mode | Non-finite AUP math, missing kinematic root snapshot, or invalid grid state sets telemetry flags and dumps `Docs/AgentLogs/Dump_VEHICLE_SURGEON.bin` |
| Shutdown | Damage owner releases/clears Vault buffers and tuner state; consumers never own grid teardown |
| Review disposition | `YELLOW / STATIC_SOURCE_ONLY` until route-card review, guarded compile, Unity import, profiler, and player-build artifacts exist |
| Proof required | Artifact path, command/tool, timestamp, environment, output tuple for compile/import/runtime/profiler claims |

## Runtime Truth

`VehicleComponentDamageRuntime` consumes `CombatDamageSignal` AUP impacts and writes a Vault-owned component grid. The grid is not a health bar and not a hierarchy of component scripts.

Authoritative cell layout:

- `VehicleGridCellDTO`, 16 bytes, explicit offsets: `Integrity01` 0, `ComponentHash` 4, `StatusFlags` 8, `ArmorValue` 12.
- Default grid: 16 x 6 x 8 cells.
- Write/read buffers are separate; readers consume only the read buffer after `PublishVehicleDamageStateJob`.

## Vault Buffers

- `71640` write grid
- `71641` read grid
- `71642` impact signal buffer
- `71643` mock impact signal buffer
- `71644` write state
- `71645` read state
- `71646` tuning
- `71647` 300-frame telemetry ring
- `71648` telemetry cursor
- `71649` CSV scratch

## Mapping Rule

Impact mapping must subtract root AUP in double precision first:

`local = inverse(rootRotation) * (float3)(impactAup - rootAup)`

Absolute AUP is never cast to `float3` before subtraction.

Runtime `FixedTick` consumes only a cached root pose snapshot. That snapshot is refreshed in cold boot/LateFrame from `BufferID.SubmarineKinematicConfig.LocalOriginAup` plus the last completed vehicle local pose/rotation already published to the transform by the kinematic owner. The damage router does not read live `SubmarineKinematicStates` while the integrator can be scheduled. The only transform-only fallback is guarded to `UNITY_EDITOR || DEVELOPMENT_BUILD` for isolated mock profiling; player builds fail closed until a kinematic config-backed snapshot exists.

## Output Contract

`VehicleDamageStateDTO` publishes thrust, buoyancy, sensor, drag, flood, fire, and structure scalars. `SubmarineDynamicsRuntime` consumes only this DTO and applies hydrodynamic penalties. It does not read grid internals.

## Component Hash Contract

Canonical component hashes are lowercase FNV-1a values: `hull=0x6EA478B6`, `engine=0xEE05D83B`, `ballast=0x16368F10`, `sensors=0x5FD70E98`, `power=0xF54F2346`. The CSV bridge folds common aliases (`sensor`, `sonar`, `engines`, `reactor`, `battery`) into those canonical hashes and ORs component-derived critical/flammable flags with existing initialized flags. CSV rows cannot accidentally erase `OuterHull` breach semantics by omitting a flags column.

## Debug And Tools

`Vehicle Integrity Tuner` is editor-only UI Toolkit. It calls editor-only runtime snapshot methods that refuse to read while a damage job is pending, use short Vault locks, expose numeric fields without per-refresh `.ToString()` formatting, and write `VehicleDamageTuningDTO` directly in the Vault using editor override flags. CSV layout hot-reload is compiled only for `UNITY_EDITOR || DEVELOPMENT_BUILD`; shipping player builds do not poll `vehicle_component_layouts.csv` from `SlowTick`. `OnDrawGizmosSelected` samples the read grid only. Fatal NaN state dumps `Docs/AgentLogs/Dump_VEHICLE_SURGEON.bin`.

## Collision Boundary

`SubmarineStructuralGrid` no longer owns an `OnCollisionEnter` damage ingress for SHINOBU_152. Heavy hull/component damage must arrive as AUP damage signals or through the existing explicit local-impact API. This prevents Unity contact callbacks from becoming a second damage truth.


