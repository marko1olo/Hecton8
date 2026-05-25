# SHINOBU_138 Chemical Influence Grid Route Card

Owner: `SHINOBU_138`

Domain: `CHEMICAL_INFLUENCE_GRID_TRACKER`

Runtime authority: `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`

## R48 Exact Route Field Normalization

Route ID: SHINOBU_138_CHEMICAL_INFLUENCE_GRID_ROUTE_CARD

Owner: `SHINOBU_138`

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity import, chemical-grid runtime diffusion, predator AI consumption, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`

## Route

Chemical truth is a sliding 3D AUP-local grid around the player.

Emitters write blood, pheromone, and toxin scalars into Vault-backed `ChemicalCellDTO` buffers.

A deterministic Burst Jacobi solver diffuses/dissipates the field and publishes normalized `float4` samples.

DataVault-backed global-authority route with owner-local source ownership.

External systems enqueue chemical events through existing `ChemicalInfluenceGrid` methods or consume published snapshot.

They do not own Vault buffers. Predator cognition samples published grid first and uses legacy breadcrumbs only as bounded compatibility fallback.

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

- Prior static forbidden-pattern scan text is documentation/source orientation only.
- At capture time it claimed no scent `OnTriggerStay`, distance/format/LINQ, `Pack=1`, random/time/camera, source-level `Hecton8.Gameplay`, or private persistent `NativeArray` in owned files.
- Remaining orientation matches are `ResolveArray<T>` as a method-return false-positive and editor-only `Marshal.OffsetOf(typeof(T), fieldName)` inside `#if UNITY_EDITOR`.
- Build/import was intentionally not launched because host CPU telemetry reportedly violated the project build gate.
- Fresh Unity import, Console, Play Mode, profiler, GCMonitor, player build, AI consumption, and visual proof remain pending.
