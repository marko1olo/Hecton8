# SHINOBU_138 Chemical Influence Grid Route Card

Owner: `SHINOBU_138`
Domain: `CHEMICAL_INFLUENCE_GRID_TRACKER`
Runtime authority: `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`

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

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity import, chemical-grid runtime diffusion, predator AI consumption, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`

## Route

Chemical truth is a sliding 3D AUP-local grid around the player. Emitters write blood, pheromone, and toxin scalars into Vault-backed `ChemicalCellDTO` buffers. A deterministic Burst Jacobi solver diffuses/dissipates the field and publishes normalized `float4` samples for predator AI and editor diagnostics.

This is a DataVault-backed global-authority route with owner-local source ownership. External systems enqueue chemical events through existing `ChemicalInfluenceGrid` static methods or consume the published snapshot; they do not own the Vault buffers. Predator cognition samples the published grid first and only uses legacy breadcrumbs as a bounded compatibility fallback.

## Current Route Review Disposition

| Field | Value |
|---|---|
| Route ID | `SHINOBU_138_CHEMICAL_INFLUENCE_GRID` |
| Review disposition | YELLOW / STATIC_SOURCE_ONLY |
| Owner | SHINOBU_138 / `ChemicalInfluenceGrid` |
| Instrument | GlobalDataVault chemical grid buffers `71150..71168`, published snapshot buffer, compatibility static ingress, and black-box telemetry/fault-dump route |
| Producer phase | Chemical emitters enqueue before simulation; diffusion/publish runs during owner simulation phase |
| Consumer phase | Post-simulation published-grid reads by predator cognition, overlay diagnostics, and compatibility fallback consumers |
| Consumers | Predator cognition, diagnostics overlay, and compatibility breadcrumb fallback consumers |
| Cadence | Dirty emitter commit plus deterministic diffusion/publish tick |
| Capacity | Front/back/published/overlay grids at `36864` cells; telemetry ring fixed at 300 entries; emitter/profile capacities are Vault-buffer bounded |
| Overflow/failure | Pending emitter count and active emitter count bound write extents; non-owned systems consume published snapshots only; legacy breadcrumbs remain bounded fallback |
| Shutdown/disposal | Vault buffers remain owner-controlled; scheduled jobs complete at boot/teardown boundaries or normal non-blocking finalization |
| Telemetry fields | Frame, active/pending emitters, diffusion step, published hash, overlay hash, fault flags, quality, and estimated compute time; exact field list remains source-oriented until artifact-backed layout output is linked |
| Black-box fields | 300-entry chemical telemetry ring, cursor, state hash, emitter counts, diffusion counters, and fault flags |
| Fault dump target | `Docs/AgentLogs/Dump_SHINOBU_138.bin` is a planned/generated fault target; no existing artifact is implied unless linked with runtime trigger evidence |
| Proof required before GREEN | Fresh compile/import artifact, AI consumption proof, profiler/GC proof, grid stress proof, and linked output path with command, timestamp, environment, and result |

## Vault Buffers

`71150` front `ChemicalCellDTO[36864]`
`71151` back `ChemicalCellDTO[36864]`
`71152` published `float4[36864]`
`71153` overlay `float4[36864]`
`71154` breadcrumbs
`71155` pending emitters
`71156` pending emitter count
`71157` active emitters
`71158` active emitter count
`71159` mock emitters
`71160` mock emitter count
`71161` tuning DTO
`71162` telemetry ring `ChemicalTelemetryEntry[300]`
`71163` telemetry cursor
`71164` 64B atomic counters
`71165` defoliant zones
`71166` CSV scratch
`71167` emitter profile table
`71168` profile count

## DTO Layout

`ChemicalCellDTO` is `[StructLayout(LayoutKind.Explicit, Size = 16)]`.

Offset 0: `float BloodConcentration`
Offset 4: `float PheromoneConcentration`
Offset 8: `float ToxinConcentration`
Offset 12: `uint Flags`

Four cells fit into one 64-byte cache line. `ChemicalAtomicCounterDTO`, `ChemicalTelemetryEntry`, emitters, profiles, sample requests, and sample results are fixed 64-byte DTOs.

## Job Graph

Optional `ShiftChemicalGridJob` -> optional `CopyChemicalGridJob` -> `PrepareChemicalFrameJob` -> `CommitPendingEmittersJob` -> `GenerateMockScentSourcesJob` -> `ChemicalInjectionJob` -> `ChemicalDiffusionSolverJob` x `(int)math.lerp(1, 6, GlobalQualityWeight)` -> `ChemicalPublishGridJob` -> `ChemicalTelemetryWriteJob`.

The system registers the final handle through `H8Memory.RegisterActiveJob(SystemID.AISensory, handle)` and finalizes with non-blocking completion during normal frame flow. Forced completion is limited to boot/teardown boundaries.

## Verification

Prior static forbidden-pattern scan text is documentation/source orientation only: no scent `OnTriggerStay`, `Vector3.Distance`, `string.Format`, LINQ, `Pack=1`, `UnityEngine.Random`, `Time.deltaTime`, `Time.frameCount`, `Time.time`, `Camera.main`, source-level `Hecton8.Gameplay` symbol, or private persistent `NativeArray` field was claimed for owned runtime/editor/data files at capture time. Remaining orientation matches are `ResolveArray<T>` as a method-return false-positive and editor-only `Marshal.OffsetOf(typeof(T), fieldName)` inside `#if UNITY_EDITOR`. Build/import was intentionally not launched because host CPU telemetry reportedly violated the project build gate. Fresh Unity import, Console, Play Mode, profiler, GCMonitor, player build, AI consumption, and visual proof remain pending.


