# Headless Ecosystem Simulation

Status: implementation contract for hibernated fauna sectors.
Verification: PENDING VERIFICATION

2026-05-02 current-state boundary:

- This is the headless ecosystem implementation contract, not proof of live headless correctness.
- Current project-state orientation starts at `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Headless fauna, AUP route ownership, hibernation catch-up, and save-sector reconciliation remain high-risk until runtime tests prove no presentation dependency and no persistence drift.

Missing prerequisite: `Docs/ARCHITECTURE/AI_PACING_MODEL.md` was requested by the operation prompt but is not present in this repository. This document anchors the current implementation against `EcosystemDirector`, `FaunaDirector`, and `PersistentWorldRegistry`.

## Purpose

Headless ecosystem simulation keeps Tier 2 fauna alive, hungry, migrating, reproducing, dying, and influencing spawn weights while no Unity GameObject exists. Runtime presentation is disposable. Persistent `EntityDataRecord` state is authoritative for hibernated creatures.

## Simulation Tiers

- Tier 0, `<40m`: near-field full Unity presentation, physics, cognition, and scanning hooks.
- Tier 1, `40m-150m`: data-only fauna slot updated by the Burst data-only LOD job, with presentation and colliders disabled.
- Tier 2, `>150m`: hibernated MMF-sector record. No active `FaunaBrain`, no collider, no animator, no spatial hash runtime entry.

Tier 2 handoff writes the compact hibernation record before destroying the runtime instance. Rehydration consumes the saved record, applies metabolic catch-up, then returns the creature to a Tier 1 slot before it can enter full simulation.

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

If hunger reaches the starvation threshold, the restored brain is forced into starving/hunt behavior. If health drains below zero, no live brain is restored and the sector produces an organic corpse/whale-fall path instead.

## Lotka-Volterra ColdTick

`EcosystemDirector` owns the headless population solve. ColdTick cadence is one minute, and the Burst job operates on unmanaged sector arrays:

```text
interaction = prey * predator
dx/dt = alpha * prey - beta * interaction
dy/dt = delta * interaction * predatorGainPerPrey - gamma * predator

preyNext = clamp(prey + dx/dt * dtMinutes, 0, maxPrey)
predatorNext = clamp(predator + dy/dt * dtMinutes, 0, maxPredators)
```

The current implementation also applies bounded suppression, harvest crash, temperature adaptation, and migration diffusion before clamping. After the Burst solve completes in the scheduled ColdTick path, `PersistentWorldRegistry.ReconcileFaunaHibernationSectorPopulation` adjusts the saved hibernation records toward the solved prey/predator counts.

## Hibernated Predation

Sector-local predation is resolved during record reconciliation. If a saved large predator record and a saved non-predator fauna record share the same hibernated sector, a deterministic combat score is computed:

```text
roll01 = hash(sectorHash, apexUid, preyUid) / 65535
predatorPressure01 = predatorPopulation / max(1, preyPopulation + predatorPopulation)
apexPower = 0.65 + predatorPressure01 * 0.25 + roll01 * 0.10
preyEscapePower = 0.15 + (1 - predatorPressure01) * 0.20
victim dies if apexPower >= preyEscapePower
```

For apex-versus-herbivore sectors this intentionally resolves as a kill while remaining deterministic. The prey record is removed from the sector block, its UID is removed from the in-memory registry, and `TryRegisterFaunaTombstone` writes the death into the persistent tombstone set so the same individual is not rehydrated later.

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

`EcosystemDirector.ResolveCombinedCorpseSpawnInfluence01` combines live corpse influence from `DestructibleOrganicManager.ResolveCorpseSpawnInfluence01` with persistent whale-fall POIs from `PersistentWorldRegistry.ResolveWhaleFallSpawnInfluence01`. Scavenger and carcass-consumer spawn weights are multiplied inside the 100 m influence radius until the live corpse depletes or the persistent POI expires.

## Spawn Gating

Predator spawning is bottom-up. Before predator or apex generation proceeds, `EcosystemDirector.CanSupportPredatorSpawn` performs a non-alloc spatial hash query through `FaunaSpatialHashRegistry.CollectContactsNonAlloc` using the preallocated `_predatorSpawnValidationHits` buffer. A spawn is accepted only if at least one nearby fauna `PreyMaskBits` overlaps the candidate predator `DietMaskBits`, or if carcass diet rules are satisfied by corpse influence.

No managed list is created in this validation path.

## Discovery Hooks

Feeding observation is event-driven. `FaunaBrain` raises `ScanEvents.RaiseFaunaFeedingObserved`; `HectonDiscoveryManager` records fauna interaction counts and unlocks ecological PDA milestones at the configured thresholds.

## Allocation Contract

- ColdTick math runs in Burst-compatible unmanaged arrays owned by `EcosystemDirector`.
- Sector reconciliation reuses `_entityStateScratch`; no LINQ and no scene searches are introduced.
- Runtime spawn gating uses caller-owned non-alloc buffers.
- Persistent hibernation data is stored as fixed records, not managed serialized graphs.

Measured profiler proof is not embedded in this document. GC status remains pending runtime profiler or GCMonitor capture.
