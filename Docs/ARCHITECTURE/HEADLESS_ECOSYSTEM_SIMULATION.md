# Headless Ecosystem Simulation

Date: 2026-05-07

Status: PENDING VERIFICATION

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not headless sim correctness, persistence drift safety, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/World/EcosystemDirector.cs`

- `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs`

- `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`

- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`

- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs`

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

Historical 2026-05-04 boundary:

- This is the headless ecosystem implementation contract, not proof of live headless correctness.

- Historical project-state orientation previously started at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

- Headless fauna, AUP route ownership, hibernation catch-up, and save-sector reconciliation remain high-risk until runtime tests prove no presentation dependency and no persistence drift.

Pacing prerequisite: `Docs/ARCHITECTURE/AI_PACING_MODEL.md` now anchors the static pacing contract against `EcosystemDirector`, `FaunaDirector`, `HectonDirectorAI`, `SystemDispatcher`, `HomeostasisBrain`, and `PersistentWorldRegistry`. It is contract orientation only, not runtime proof.

## Purpose

- Headless ecosystem simulation keeps Tier 2 fauna alive, hungry, migrating, reproducing, dying.
- It also influences spawn weights while no Unity GameObject exists.
- Runtime presentation is disposable.
- Persistent `EntityDataRecord` state is authoritative for hibernated creatures.

## Simulation Tiers

- Tier 0, `<40m`: near-field full Unity presentation, physics, cognition, and scanning hooks.

- Tier 1, `40m-150m`: data-only fauna slot updated by the Burst data-only LOD job, with presentation and colliders disabled.

- Tier 2, `>150m`: hibernated indexed save-sector record. No active `FaunaBrain`, no collider, no animator, no spatial hash runtime entry.

Tier 2 handoff writes the compact hibernation record before destroying the runtime instance.

Rehydration consumes the saved record, applies metabolic catch-up, and returns the creature to Tier 1 before full simulation.

## Hibernation Record Layout

`PersistentWorldRegistry.EntityDataRecord` is used as the fixed save-delta carrier:

```text

InventoryHash high byte: 0xF9 = fauna hibernation state

InventoryHash low flags: bit 0 large threat, bit 1 predator

InventoryHash value bits: quantized sleepStartSeconds / 0.25 seconds

Quantity: speciesId

Position: AbsoluteUniversePosition aligned blit

Position.Reserved high 32 bits: health float bits

Position.Reserved low 32 bits: hunger01 float bits

Integrity01: legacy health fallback

InstanceUid: persistent unique fauna id

```

This is not JSON and does not require managed object graphs. The sector block is rewritten through the indexed sector override path after reconciliation.

## Metabolic Catch-Up

On rehydration:

```text

timeAsleep = max(0, currentTimeSeconds - sleepStartSeconds)

hunger01 = saturate(savedHunger01 + hungerRate * timeAsleep)

health = savedHealth - starvationDamageRate * max(0, hunger01 - starvationThreshold) * timeAsleep

```

- If hunger reaches starvation threshold, restored brain is forced into starving/hunt behavior.
- If health drains below zero, no live brain is restored.
- Sector produces organic corpse/whale-fall path instead.

## Lotka-Volterra ColdTick

`EcosystemDirector` owns the headless population solve. ColdTick cadence is one minute, and the Burst job operates on unmanaged sector arrays:

```text

interaction = prey * predator

dx/dt = alpha * prey - beta * interaction

dy/dt = delta * interaction * predatorGainPerPrey - gamma * predator

preyNext = clamp(prey + dx/dt * dtMinutes, 0, maxPrey)

predatorNext = clamp(predator + dy/dt * dtMinutes, 0, maxPredators)

```

Current implementation applies before clamp:

- bounded suppression;
- harvest crash;
- temperature adaptation;
- migration diffusion.

After the ColdTick Burst solve, `PersistentWorldRegistry.ReconcileFaunaHibernationSectorPopulation` adjusts saved hibernation records toward solved prey/predator counts.

## Hibernated Predation

Sector-local predation is resolved during record reconciliation. If a saved large predator record and a saved non-predator fauna record share the same hibernated sector, a deterministic combat score is computed:

```text

roll01 = hash(sectorHash, apexUid, preyUid) / 65535

predatorPressure01 = predatorPopulation / max(1, preyPopulation + predatorPopulation)

apexPower = 0.65 + predatorPressure01 * 0.25 + roll01 * 0.10

preyEscapePower = 0.15 + (1 - predatorPressure01) * 0.20

victim dies if apexPower >= preyEscapePower

```

## Thermal Apex Migration

`FaunaDirector` queries the thermal service through `GlobalRegistry.Thermodynamics.TryResolveApexMigrationThermalAttractor`. When a valid attractor exists, `PersistentWorldRegistry.MigrateApexFaunaHibernationStatesToward` shifts hibernated large predator AUPs within neighboring 1 km sectors toward the heat source.

The migration is applied to saved records, not transforms. Arrival pop-in is avoided because the rehydrated entity is instantiated from the migrated AUP.

## Whale-Fall Spawn Anchors

Large predator death creates a persistent whale-fall POI:

```text

InventoryHash high byte: 0xF8 = whale-fall POI

Quantity: source species id

Position: corpse AUP

Integrity01: expiry time in seconds

InstanceUid: source creature uid

```

`EcosystemDirector.ResolveCombinedCorpseSpawnInfluence01` combines:

- live corpse influence from `DestructibleOrganicManager.ResolveCorpseSpawnInfluence01`;
- persistent whale-fall POIs from `PersistentWorldRegistry.ResolveWhaleFallSpawnInfluence01`.

Scavenger/carcass-consumer weights multiply inside `100 m` until live corpse depletion or POI expiry.

## Spawn Gating

Predator spawn gate:

- Direction: bottom-up.
- Query: `EcosystemDirector.CanSupportPredatorSpawn`.
- Spatial route: `FaunaSpatialHashRegistry.CollectContactsNonAlloc`.
- Buffer: preallocated `_predatorSpawnValidationHits`.
- Accept if nearby fauna `PreyMaskBits` overlap candidate predator `DietMaskBits`.
- Accept if carcass diet rules are satisfied by corpse influence.

No managed list is created in this validation path.

## Discovery Hooks

Feeding observation is event-driven. `FaunaBrain` raises `ScanEvents.RaiseFaunaFeedingObserved`; `HectonDiscoveryManager` records fauna interaction counts and unlocks ecological PDA milestones at the configured thresholds.

## Allocation Contract

- ColdTick math runs in Burst-compatible unmanaged arrays owned by `EcosystemDirector`.

- Sector reconciliation reuses `_entityStateScratch`; no LINQ and no scene searches are introduced.

- Runtime spawn gating uses caller-owned non-alloc buffers.

- Persistent hibernation data is stored as fixed records, not managed serialized graphs.

Measured profiler proof absent. GC status remains pending runtime profiler or GCMonitor capture.
