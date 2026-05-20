# Headless Ecosystem Simulation
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not headless sim correctness, persistence drift safety, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs`
- `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`
- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs`

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current static/tool boundary is R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) (R46 prior interior-authority/route-field/proof-language correction; R45 prior R43/R44 residue/proof-artifact/source-counter correction); R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; AtlasCheck fails `ATLAS_CHECK_FAIL references=6781 missing=61` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, missing HectonMaskChannelPacker/HectonMaterialChannelPackValidator editor source refs, and missing HabitatDamageBakePipeline source ref in the current atlas); Mod API static validation passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
Historical 2026-05-04 boundary:

- This is the headless ecosystem implementation contract, not proof of live headless correctness.
- Historical project-state orientation previously started at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Headless fauna, AUP route ownership, hibernation catch-up, and save-sector reconciliation remain high-risk until runtime tests prove no presentation dependency and no persistence drift.

Pacing prerequisite: `Docs/ARCHITECTURE/AI_PACING_MODEL.md` now anchors the static pacing contract against `EcosystemDirector`, `FaunaDirector`, `HectonDirectorAI`, `SystemDispatcher`, `HomeostasisBrain`, and `PersistentWorldRegistry`. It is contract orientation only, not runtime proof.

## Purpose

Headless ecosystem simulation keeps Tier 2 fauna alive, hungry, migrating, reproducing, dying, and influencing spawn weights while no Unity GameObject exists. Runtime presentation is disposable. Persistent `EntityDataRecord` state is authoritative for hibernated creatures.

## Simulation Tiers

- Tier 0, `<40m`: near-field full Unity presentation, physics, cognition, and scanning hooks.
- Tier 1, `40m-150m`: data-only fauna slot updated by the Burst data-only LOD job, with presentation and colliders disabled.
- Tier 2, `>150m`: hibernated indexed save-sector record. No active `FaunaBrain`, no collider, no animator, no spatial hash runtime entry.

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
