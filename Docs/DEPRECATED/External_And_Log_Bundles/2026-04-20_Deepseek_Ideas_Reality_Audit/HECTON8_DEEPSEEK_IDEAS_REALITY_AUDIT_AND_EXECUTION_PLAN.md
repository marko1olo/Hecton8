# HECTON-8 Deepseek Ideas Reality Audit And Execution Plan

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Date: `2026-04-20`

## 2026-04-21 Execution Update - Ecosystem Backend, Genetics, Infection Pressure, Migration

### What was corrected

- `DynamicDifficultyDirector` exported predator aggression pressure, but fauna did not consume it
- fauna variants still depended on authored prefab count instead of deterministic runtime mutation
- ecological debt had no physical world manifestation
- biome migration pressure was static, so water composition did not breathe over time

### Implemented now

- `Assets/_Project/Scripts/Ecosystem/FaunaBrain.Ecosystem.cs`
  - runtime overlay partial for:
    - aggression bridge
    - deterministic genetics application
    - infected-state visuals
    - toxicity hazard registration
- `Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs`
  - persisted `worldSeed` owner
  - deterministic trait generation from:
    - biome
    - spawn position
    - creature ID
  - mod mutation overlay merge via `ModEcosystemRegistry`
- `Assets/_Project/Scripts/Ecosystem/EcosystemHealthDirector.cs`
  - save-backed infected-zone owner driven by ecological strain
  - uses explored chunk data from `PlayerExplorationTracker`
  - configures spawned fauna infection state on entry
- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`
  - daily deterministic spawn-weight bias for ambient / territorial fauna by biome
- `Assets/_Project/Scripts/FaunaDirector.cs`
  - now applies:
    - genetics
    - infection state
    - migration pressure
  on normal and horde spawn paths
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
  - now consumes toxicity hazard intensity as another continuous body threat
- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
  - new supported facade:
    - `HectonAPI.Ecosystem.RegisterBiomeMutation(...)`
- `Assets/_Project/Scripts/SaveData.cs`
  - new `EcosystemStateDTO` with:
    - persisted `worldSeed`
    - infected chunk keys
    - infected-zone severity
  - `SaveData.CurrentVersion` is now `v32`

### Why this owner model was kept

- predator pressure now lands in the actual fauna runtime owner instead of forking another hidden combat modifier path
- deterministic genetics avoids prefab explosion and keeps variation math-driven, save-stable, and mod-overlay friendly
- infection zones are local world state, so they belong to slot save data, not the global profile
- migration stays deterministic from day index and seed, so there is no need to persist another heavy per-biome population snapshot

## 2026-04-21 Execution Update - Economy Loop, Recycling, Scarcity, Ecological Stress

### What was corrected

- there was no official dismantle/recycling owner
- the world did not remember extraction pressure for fabrication energy cost
- pollution had no saved gameplay consequence, so waste carried no systemic penalty

### Implemented now

- `Assets/_Project/Scripts/Economy/RecyclingRegistry.cs`
  - runtime-only recycling overlay store for first-party and mod-owned recycle yields
- `Assets/_Project/Scripts/Economy/ScrapManager.cs`
  - official dismantle owner
  - uses explicit recycle overlays first, then derives fallback yield from official fabricator recipes
- `Assets/_Project/Scripts/Economy/ResourceScarcityDirector.cs`
  - save-backed extraction tracker keyed by stable item ID
  - exposes recipe-level fabrication power multipliers consumed by `Fabricator`
- `Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs`
  - save-backed ecological debt owner
  - receives `ItemRecycledEvent` and `ItemDiscardedEvent`
  - exports predator-aggression pressure into `DynamicDifficultyDirector`
- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
  - new supported facades for recycle-yield registration and official recycle execution
- `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs`
  - new marathon goal for cumulative recycling across all runs
- `Assets/_Project/Scripts/Meta/MetaUpgradeRegistry.cs`
  - new permanent upgrades:
    - `GreenTech`
    - `EfficiencyExpert`

### Why this owner model was kept

- recycle yields stay runtime-only and do not mutate authored `RecipeData` or `ItemData`
- scarcity remains local-save world state because depletion belongs to a run, not to the account profile
- ecological debt feeds the existing hidden-difficulty layer instead of forking a second predator system
- deliberate discard is currently the only reliable first-party owner-path for waste telemetry; inventing a fake global despawn authority would be architecture drift

## 2026-04-21 Execution Update - Global Profile, Meta Currency, Dynamic Difficulty

### What was corrected

- all progression still died with a slot save
- there was no global hall-of-fame owner for cross-run records
- dynamic difficulty did not exist as a measured, hidden telemetry layer

### Implemented now

- `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs`
  - new slot-independent meta owner backed by `profile.json`
  - tracks:
    - max depth across all runs
    - longest life without death
    - highest biome discovery count reached in a run
    - fastest unlock time for each internal achievement
  - grants persistent `Explorer Points` on first-time global achievement unlocks
- `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs`
  - new `ISlowTickable` scene-level difficulty director
  - reads:
    - repeated deaths in a rolling 30-minute window
    - recent contextual advisories
    - recent achievement unlock momentum
    - no-damage biome streaks
  - produces `DifficultyModifierData` with:
    - `DamageMultiplier`
    - `OxygenDepletionRate`
    - `PredatorAggressionScale`
- `Assets/_Project/Scripts/Meta/MetaRuntimeInstaller.cs`
  - cold-path installer for scene-level meta systems
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
  - now consumes dynamic difficulty modifiers in:
    - oxygen depletion
    - pressure damage
    - direct damage intake

### Why this owner model was kept

- global profile data does not belong in `SaveData`; slot files are the wrong authority for meta progression
- `GlobalProfileManager` stays scene-level and file-backed, so the data survives new games without forcing another `DontDestroyOnLoad` singleton into the architecture
- difficulty logic remains analytical and hidden; it does not mutate authored `SurvivalStats` assets and does not fork the survival simulation into a second ruleset

## Purpose

This document converts `Docs/DEPRECATED/External_And_Log_Bundles/mnogo idey ot dipsika/idei dipsika.txt` from a raw idea dump into an evidence-based execution plan.

This file does not replace repository authority.

Authority remains:

- `AGENTS.md`
- `Docs/README.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `MASTER_RELEASE_WORK_PLAN.md`

This audit answers five questions with evidence:

1. what Deepseek proposed
2. what already exists in repo
3. what is missing
4. what is dangerous, stale, or architecture-breaking
5. what should be implemented first without waiting for another fantasy rewrite

---

## Evidence Base

Reviewed sources and live repo state:

- `AGENTS.md`
- `Docs/README.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `Docs/2026-04-19_Gemini_Reality_Audit/HECTON8_GEMINI_REALITY_AUDIT_AND_EXECUTION_PLAN.md`
- `Docs/DEPRECATED/External_And_Log_Bundles/mnogo idey ot dipsika/idei dipsika.txt`
- first-party scripts under `Assets/_Project/Scripts`
- current worktree state via `git status`

No Unity runtime proof was captured in this pass.

Status remains `PENDING VERIFICATION`.

---

## Atmospheric Polish Slice

Target:

- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
- `Assets/_Project/Scripts/Audio/DeepPsychosisController.cs`
- `Assets/_Project/Scripts/Audio/AtmosphericAudioRuntimeInstaller.cs`
- `Assets/_Project/Scripts/Visor/PlayerStressVFX.cs`
- `Assets/_Project/Scripts/Visor/CausticsProjectorManager.cs`
- `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs`
- `Assets/_Project/Scripts/SceneBootstrap.cs`

Result required:

- dynamic mixer-layer routing for rhythm, bass, atmosphere, and danger inside the real `HectonMusicDirector`
- predator proximity sampled through existing spatial registry instead of scene scans
- deep hallucination cue owner driven by depth, oxygen pressure, and ecological strain
- dedicated critical-state visor pulse owner using a runtime-only post-process profile
- camera-local URP caustics projector around the player using an authored decal material only
- bootstrap wiring through the existing player-runtime publication path

Constraints:

- no `SaveData` expansion in this slice
- no new physics systems in this slice
- no runtime material clones for third-party stacks
- no authored `VolumeProfile` mutation for the new stress pulse; runtime-only profile owner required
- no `FindObjects*` / hot-path scene scans for predator checks; must route through existing registry

Current pass result:

- aggressive-fauna proximity query added to `WorldSpatialHashGrid`
- `HectonMusicDirector` now owns mixer-layer routing from depth / oxygen / predator / storm pressure
- player-owned `DeepPsychosisController`, `PlayerStressVFX`, and `CausticsProjectorManager` added
- `SceneBootstrap` now installs the atmospheric slice on the active player
- compile/runtime proof still missing in current environment

Regression model:

- CPU:
  - low risk in hot path
  - predator sampling runs on `ISlowTickable`
  - per-frame work limited to scalar interpolation, mixer writes, and volume/decal parameter updates
- GC:
  - no intentional frame allocations added
  - measured proof absent
- memory:
  - one runtime `VolumeProfile`
  - one runtime `Volume`
  - one runtime `DecalProjector`
  - bounded clip arrays only
- cadence:
  - music layers updated every tick
  - predator/oxygen/storm threat snapshot refreshed on slow tick
  - psychosis cues timer-driven, not coroutine-driven
- correctness risk:
  - mixer parameter names may not exist in authored mixer
  - caustics projector may visually overlap existing shallow caustics owner
  - psychosis cue quality depends on authored clip palette

Status remains `PENDING VERIFICATION`.

---

## Execution Slice - Meta Shop, Hardcore Modes, Marathon Goals

Target:

- `Assets/_Project/Scripts/Meta/MetaUpgradeRegistry.cs`
- `Assets/_Project/Scripts/Meta/MetaBuffInjector.cs`
- `Assets/_Project/Scripts/Meta/RunModifierController.cs`
- `Assets/_Project/Scripts/Meta/GlobalProfileData.cs`
- `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs`
- `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs`
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/SaveData.cs`
- `Assets/_Project/Scripts/SaveDataMigration.cs`

Result required:

- permanent explorer-point sink with profile-persisted upgrade levels
- runtime-only buff injection for future runs without mutating authored `SurvivalStats` or `SuitData`
- slot-scoped run modifiers for:
  - permadeath
  - nightmare mode
  - daily-seed identity
- physical slot deletion path on permadeath death events when a slot exists
- nightmare override path that disables adaptive easing and forces hard values
- global marathon-goal backend paying explorer points off cumulative all-run totals

Implemented:

- `MetaUpgradeRegistry` defines permanent upgrades:
  - oxygen-capacity boost
  - starting resource cache
  - swim-speed boost
- `GlobalProfileManager` now owns:
  - upgrade purchasing
  - upgrade-level lookup
  - marathon-goal progress
  - marathon reward payout
- `MetaBuffInjector` applies purchased upgrades into live run owners on `SceneBootstrap.OnGameReady`
- `RunModifierController` persists run modifiers into `SaveData.runModifiers` and deletes slot artifacts on permadeath death when a slot path exists
- `DynamicDifficultyDirector` now short-circuits to nightmare modifiers instead of blending adaptive difficulty
- `PlayerInventory` now emits official `ItemCollectedEvent` payloads so long-tail global goals can use `HectonEventBus` instead of ad-hoc coupling

Constraints kept:

- no mutation of authored ScriptableObject assets at runtime
- no new hot-path allocations introduced in `Tick` / `FixedTick` / `SlowTick`
- no fake parallel progression owners; global profile remains the only authority for meta currency and permanent upgrades
- local save remains the only authority for per-run hardcore flags

Verification state:

- repository/code readback: `yes`
- Unity compile proof: `no`
- runtime proof: `no`

Status: `PENDING VERIFICATION`

## 2026-04-21 Execution Update - Context Advisories, Frontier Lore, Internal Achievements

### What was corrected

- PDA still had no analytical layer for repeated player failure
- the world still had no frontier-fed procedural lore pressure
- achievements existed only as an external-platform fantasy, not as an internal save-backed owner

### Implemented now

- `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs`
  - new player-owned contextual advisory owner on `ISlowTickable`
  - tracks:
    - repeated oxygen-death failures
    - repeated inventory-full collection failures
    - sustained deep exposure below `200m` without hull tier 1
  - advisories are deduplicated by stable IDs, persisted in save data, mirrored into PDA logbook, and published through `HectonEventBus`
- `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs`
  - new internal achievement owner
  - tracks:
    - traveled/swum distance from player transform deltas
    - crafted items from `ItemCraftedEvent`
    - biome discoveries from `HectonDiscoveryManager`
  - unlocks persist inside official save data and publish `AchievementUnlockedEvent`
- `Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs`
  - new frontier lore director on `ISlowTickable`
  - uses `PlayerExplorationTracker` to find explored-frontier boundaries
  - sources narrative payloads from the existing `PDADataLogTab` audio-log catalog
  - reuses `AudioLogPickup` as the live world interaction owner
  - uses `ObjectPoolManager` with a hard active cap instead of inventing an unbounded spawn stream
- `Assets/_Project/Scripts/SaveData.cs`
  - `v26` now also includes:
    - `PDAContextualAdvisoryDTO`
    - `ProceduralLoreStateDTO`
    - `AchievementRegistryDTO`
- `Assets/_Project/Scripts/SaveDataMigration.cs`
  - migration/repair for all three new DTO clusters
- `Assets/_Project/Scripts/SceneBootstrap.cs`
  - runtime player publication now installs:
    - `ProgressionRuntimeInstaller`
    - `NarrativeRuntimeInstaller`
- `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
  - exposes a bounded catalog copy API for official runtime consumers
- `Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs`
  - now exposes `ChunkWorldSize` so frontier directors do not guess grid scale

### Regression model

- CPU:
  - contextual advisory logic stays on `ISlowTickable`
  - frontier lore scans run on a long cadence instead of every frame
  - achievements use a cheap transform-delta sample for distance and event-driven counters for craft/discovery
- GC:
  - no intentional per-frame LINQ or collection churn was added
  - advisory and achievement strings allocate only on actual unlock/push paths
  - procedural lore uses preallocated chunk/catalog buffers and a hard active limit
- memory:
  - save payload grows slightly for advisories, lore placements, and achievement unlock state
  - runtime memory is bounded by fixed DTO caps and a small active lore list
- correctness:
  - frontier lore depends on the authored `PDADataLogTab` catalog existing in-scene
  - pooled lore clones currently derive from the first runtime `AudioLogPickup` template that can be resolved in the scene

### Why kept

- this keeps all three features inside existing owners:
  - PDA/logbook/save
  - audio-log archive/pickup
  - bootstrap player handoff
- no Steam/platform contract is required for internal achievements
- no second lore archive, second pickup type, or second save silo was created

## 2026-04-21 Execution Update - Pressure Envelope Runtime

### What was corrected

- pressure damage already existed, but it still read like hidden math instead of a usable gameplay contract
- `PDAContextualAdvisorySystem` was still keyed to a fake `200m` gate instead of the real suit `SafeDepth`
- `SuitAdvisoryController` still warned with generic depth strings and gave no live read on overpressure severity

### Implemented now

- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
  - exposes live pressure telemetry through existing owner state:
    - `SafeDepthMarginMeters`
    - `OverpressureMeters`
    - `PressureDamagePerSecond`
    - `PressureExposureSeverity01`
  - tracks real overpressure episodes and writes pressure-window breach summaries into `FieldOperationLogSystem`
- `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs`
  - pressure advisory no longer uses the fake `200m` heuristic
  - advisory now triggers only from real sustained overpressure using the player suit's current `SafeDepth` and live attrition severity
- `Assets/_Project/Scripts/UI/SuitAdvisoryController.cs`
  - depth warnings now surface the remaining safe-depth window
  - critical pressure warnings now report live overpressure metres and current hull attrition per second

### Regression model

- CPU:
  - pressure telemetry stays inside existing survival math and PDA advisory `ISlowTickable`
  - no new per-frame scene searches or owner churn
- GC:
  - new pressure strings allocate only on advisory/log threshold crossings, not per frame
  - live telemetry stays as scalar properties on the existing owner
- memory:
  - no new save payload or cache layer was added for pressure completion
- correctness:
  - pressure advisories now match the authored `SafeDepth` instead of a stale hard-coded depth fantasy
  - field-op pressure summaries currently log only when the player exits an overpressure episode or dies during one

### Why kept

- this closes `[23]` using existing owners instead of inventing a second hazard system
- pressure is now readable through survival, advisory, PDA, and field-log loops with one source of truth
- no render/visual subsystem was touched

## 2026-04-21 Execution Update - Habitat Air Recycling / Stale Air

### What was corrected

- dry shelter life support inside `BaseModule` was effectively infinite as long as the module stayed powered
- there was no gameplay distinction between a healthy compartment and a stale-air shelter with saturated scrubbers
- scanner/advisory loops could report service failures, but not degrading breathable reserve

### Implemented now

- `Assets/_Project/Scripts/BaseModule.cs`
  - life support now owns:
    - finite breathable reserve
    - powered scrubber recovery
    - occupancy drain while the player uses a module as dry shelter
    - stale-air throttling of oxygen refill
    - oxygen penalty when breathable reserve fully collapses
  - stale-air threshold crossings now write into `FieldOperationLogSystem`
- `Assets/_Project/Scripts/SaveData.cs`
  - `SaveData.CurrentVersion` moved through this slice and is now beyond `v28`; module breathable reserve persistence was introduced in the `v28` step
  - `ModuleDTO` now persists `airReserveNormalized`
- `Assets/_Project/Scripts/ConstructionManager.cs`
  - construction save/load now serializes breathable reserve state for `BaseModule`
- `Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs`
  - nearest inhabited module bridge now raises breathable-reserve warnings through `BaseIntegrityEvents`
- `Assets/_Project/Scripts/UI/SuitAdvisoryController.cs`
  - suit advisory now surfaces low base air quality from the existing base-event bridge
- `Assets/_Project/Scripts/ScannerTool.cs`
  - module scan summary now reports stale breathable reserve instead of treating low-air compartments as normal service space

### Regression model

- CPU:
  - habitat air logic stays inside the existing `BaseModule.SlowTick`
  - nearest-module air warnings stay on the existing `BaseIntegrityHUD` scan cadence
- GC:
  - no intentional hot-path allocations were added
  - stale-air strings allocate only on threshold crossings, scanner reads, and HUD warning pushes
- memory:
  - one extra scalar is now persisted per saved module through `ModuleDTO.airReserveNormalized`
- correctness:
  - old saves now default loaded module air reserve to full via version-gated fallback
  - stale air is intentionally constrained to dry shelter behavior only; no second atmosphere simulation was added

### Why kept

- this closes `[71]` inside the already correct owner: `BaseModule`
- it gives base shelter a maintenance cost without inventing a separate CO2 simulation stack
- the player-facing loop now exists across module runtime, scanner, suit advisory, and save/load persistence

## 2026-04-21 Execution Update - Hint-Without-Tutorial Layer

### What was corrected

- contextual advisories already existed, but they were still too narrow to count as a real hint-without-tutorial layer
- the player could repeat pressure deaths, stale-air shelter misuse, and base emergencies without the PDA learning from those failures
- base gameplay had signals, but not enough memory or escalation inside the advisory owner

### Implemented now

- `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs`
  - advisory owner now tracks and escalates:
    - repeated pressure deaths
    - repeated base emergency exposure
    - repeated stale-air incidents
  - the system now listens to existing `BaseIntegrityEvents` instead of inventing a second hint bus
  - repeated failure patterns are deduplicated through stable advisory IDs and still mirror into PDA logbook + `PlayerAdvisoryIssuedEvent`
- `Assets/_Project/Scripts/SaveData.cs`
  - `SaveData.CurrentVersion` is now `v29`
  - `PDAContextualAdvisoryDTO` now persists:
    - `pressureDeathCount`
    - `baseEmergencyCount`
    - `staleAirIncidentCount`

### Regression model

- CPU:
  - hint evaluation remains inside the existing advisory owner and existing base-event bridge
  - no new scan loop or world search was introduced
- GC:
  - new hint messages allocate only on actual advisory pushes
  - counter accumulation uses scalar fields only
- memory:
  - save payload grows by three integer counters inside `PDAContextualAdvisoryDTO`
- correctness:
  - advisory escalation now comes from real repeated failures instead of authored tutorial triggers
  - coverage is still limited to survival/base loops, not the full game

### Why kept

- this turns `[6]` from pure fantasy into a real partial system on the correct owner
- it teaches through repeated failure patterns without explicit popup tutorial scripting
- no separate tutorial manager, quest hack, or UI flow was added

Status remains `PENDING VERIFICATION`.

## 2026-04-20 Execution Update - Survival Death Loop, Last-Loss Marker, Active Sonar Cost, Base Cascade Failures

### What was corrected

- death feedback was still shallow:
  - fatal cause existed
  - teaching loop, exact run stats, and persistent last-loss marker did not
- active sonar still behaved like a free information button
- `BaseModule` stopped at breach/flood state and did not escalate into service-grade cascade failures

### Implemented now

- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
  - current-life telemetry now tracks life duration, peak depth, lowest O2, lowest energy, and lowest integrity
  - last completed life is persisted as a `SurvivalDeathRecord`
  - last-loss marker position, cause, and telemetry now save/load through `SaveData`
  - cause-specific survival advice now resolves through the existing survival owner
  - death record is archived into `FieldOperationLogSystem`
- `Assets/_Project/Scripts/SaveData.cs`
  - save format bumped to `v23`
  - persisted player death telemetry and `BaseModule` failure-mode state
- `Assets/_Project/Scripts/UI/SuitAdvisoryController.cs`
  - death advisory now emits:
    - exact fatal headline by cause
    - cause-specific survival advice
    - last-run stats summary
- `Assets/_Project/Scripts/UI/PDASpectrumTab.cs`
  - PDA spectrum/radar text now surfaces `LAST LOSS` distance + cause tag from the persisted death record
  - last-loss marker survives save/load and remains visible even when no other signal is present
- `Assets/_Project/Scripts/Visor/SpectrumSystem.cs`
  - every active sonar pulse now drains suit energy
  - every active sonar pulse now emits a fauna-facing noise signal
  - nearby `FaunaBrain` owners are directly provoked through existing spatial-grid lookup
- `Assets/_Project/Scripts/BaseModule.cs`
  - integrity collapse now resolves into deterministic cascade failure modes:
    - oxygen leak
    - fire
    - short circuit
  - oxygen leak drains player O2 inside the module
  - fire damages hull and burns suit energy inside the module
  - short circuit kills operational power until hull service is restored
  - cascade failures now log to `FieldOperationLogSystem` and push player warnings
  - repeated cascade failures now permanently lower the module repair ceiling, so service recovery is finite instead of infinite
- `Assets/_Project/Scripts/ConstructionManager.cs`
  - module save/load now persists and restores `BaseModuleFailureMode`
  - ambient accident scheduling now exists on the same owner and only targets already-neglected service modules
  - accident checks run on `ISlowTickable` cadence and escalate through the existing `BaseModule` cascade path instead of inventing another disaster subsystem
  - module save/load now also persists the reduced repair ceiling created by repeated failures
- `Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs`
  - nearest-module HUD bridge now publishes real breach/emergency state instead of only generic integrity percentages
  - emergency state is throttled and keyed to the tracked nearest module
- `Assets/_Project/Scripts/UI/SuitAdvisoryController.cs`
  - suit advisory now reacts to nearest base breach/emergency events with cause-specific service warnings
- `Assets/_Project/Scripts/ScannerTool.cs`
  - structure scan summaries can now resolve exact service-fault guidance from `BaseModule.CurrentFailureMode` instead of only generic service damage text

### Regression model

- CPU:
  - active-sonar cost adds one non-alloc fauna query per sonar pulse, not per frame
  - base cascade effects run only in existing `SlowTick`
  - nearest-base emergency surfacing reuses existing `BaseIntegrityHUD` scan cadence
- GC:
  - no new hot-path LINQ/list churn was intentionally added
  - death summary/advice formatting allocates only on death-facing UI/log path
- memory:
  - save payload grows slightly for death telemetry and module failure mode state
  - no new unbounded runtime cache was added
- correctness:
  - old saves are version-gated so `v22-` payloads do not fake zero telemetry values
  - death marker is informational in PDA/radar, not a spawned world beacon

Status remains `PENDING VERIFICATION`.

### Additional save presentation consistency completed in same owner cluster

- `Assets/_Project/Scripts/SaveSlotUI.cs`
  - full slot-card details now normalize scene labels and integrity labels through the same save-trust contract already used by hover preview
  - healthy slots no longer waste a second text line on raw `Primary`
  - authored slot auto-wire now prefers name-matched TMP targets before positional fallback
- `Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs`
  - save HUD notifications now append the resolved slot label instead of staying anonymous
  - failed save notification now uses the localized save-failed title instead of the old `CHECK LOGS` player-facing message
- `Assets/_Project/Scripts/MainMenuController.cs`
  - main-menu save/load modals now use player-facing slot labels instead of raw `slot_1`
  - corrupt/no-backup load classification is now null-safe on the error path
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
  - pause save-slot buttons and status text now use player-facing slot labels instead of raw persistence ids
  - save section refresh now reapplies slot labels when the menu language changes
  - pause save crash/failure messaging no longer tells the player to check console

### Why kept

- save shell now speaks with one voice: slot card, hover preview, and HUD no longer disagree about what a healthy slot is called
- player-facing save failures are cleaner and more actionable without exposing internal “go read logs” text
- main-menu save/load messaging is now consistent with the slot shell and no longer leaks raw persistence ids
- pause-menu save controls now stay aligned with the same slot naming contract as main menu and HUD
- this stays inside existing save/UI owners and does not touch payload format, scene flow, or third-party systems

### Additional save UX hardening completed in same owner cluster

- `Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs`
  - thumbnail capture moved from `OnSaveStarted` to `OnSaveCompleted`
  - explicit wrapper `captureCamera` is now forwarded into the thumbnail owner
- `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
  - thumbnail writes now stage through temp file then replace/move into final `.png`
- `Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs`
  - preview text auto-wire now prefers name-based targets before positional fallback
  - healthy slots no longer waste space on redundant integrity labels

### Why kept

- this improves save/load trust without changing save payload format
- it reduces false-fresh slot previews after failed save attempts
- it keeps thumbnail disk state aligned with successful save completion semantics

---

## 2026-04-20 Execution Update - Save Slot Hover Preview UX Completion

### What was corrected

- the save-slot hover panel no longer lies about showing metadata while only rendering a thumbnail

### Why this matters

- this is a Tier 1 completion on an existing owner, not a new subsystem
- players can now inspect slot freshness and integrity faster before committing to load/overwrite
- it extends the already-strong save backbone without touching save-file format or risky world/runtime owners

### Implemented now

- `Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs`
  - hover preview now reads `SaveSlotInfo` through `SaveManager.TryGetSaveSlotInfo`
  - shows slot title, timestamp, playtime, scene label, and integrity status
  - auto-wires preview text fields in cold path if scene serialization is incomplete
  - re-localizes visible preview text on language change

### Regression model

- CPU:
  - no hot-path change; metadata fetch happens only on hover preview open
- GC:
  - preview text discovery uses one cold `GetComponentsInChildren<TMP_Text>` scan in `Awake`
  - no per-frame allocations were intentionally added to `Tick`
- memory:
  - negligible; existing thumbnail texture ownership remains unchanged
- correctness:
  - depends on authored preview panel containing compatible TMP text targets or using explicit serialized refs

Status remains `PENDING VERIFICATION`.

---

## 2026-04-20 Execution Update - Quest Registry Hardening + Vegetation Bridge Compile Recovery

### What was corrected

- `QuestManager` no longer trusts duplicate or unknown `questId` values
- `HectonMapMagicVegetationBridge` no longer blocks compile on missing private types/methods from an incomplete refactor

### Why this matters

- content-pack and future mod-style quest additions need deterministic quest identity, not silent overwrite on duplicate ids
- compile verification was blocked behind the vegetation bridge, which meant every later execution slice stayed unprovable

### Implemented now

- `Assets/_Project/Scripts/Quest/QuestManager.cs`
  - registry ambiguity tracking for duplicate `questId`
  - activation/completion guards for unknown ids
  - load-time filtering of unknown stale save ids
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
  - restored internal `SampleContext`
  - restored managed terrain-mask cache fields used by chunk sampling
  - restored tile cache dispose path
  - restored missing chunk-build lifecycle methods
  - explicit `ITickable` + `ISlowTickable` registration casts

### Verification outcome

- Unity compile no longer reports the earlier bridge-local errors:
  - missing `SampleContext`
  - missing `ITickable.Tick(float)` implementation symptom
  - missing chunk lifecycle methods
  - ambiguous `Register/Unregister`
- new top blocker is unrelated to the bridge patch:
  - `error CS2001: Source file 'C:\\hades\\Hecton8\\Assets/_Project/Scripts/Gameplay/SargassumMovementInfluence.cs' could not be found`

### Regression model

- CPU:
  - quest changes affect only cold load / quest mutation paths
  - vegetation bridge recovery currently risks higher `SlowTick` cost because chunk builds are synchronous until the intended async job path is rebuilt
- GC:
  - no new per-frame string/list allocations were intentionally added to quest paths
  - vegetation bridge keeps existing builder/list reuse, but measured GC proof is absent
- memory:
  - tile mask bytes remain bounded per loaded tile; explicit dispose path now exists for native tile caches
- correctness:
  - quest stale-save handling is safer
  - vegetation bridge compile contract is restored, but runtime residency behavior still lacks log/profiler proof

Status remains `PENDING VERIFICATION`.

---

## Verified Project Truth

### Runtime owners that already exist

- `GameTickManager`
- `SaveManager`
- `SpatialAudioManager`
- `LocalizationManager`
- `InputManager`
- `RebindingManager`
- `SettingsManager`
- `QuestManager`
- `PlayerPDA`
- `SpectrumSystem`
- `HectonMusicDirector`
- `HectonSurfaceWeatherDirector`
- `CurrentManager`
- `BiomeMatrixDirector`
- `MapMagicBridge`
- `RuntimePerformanceProfiler`
- `DynamicResolutionScaler`

### Product capabilities already present in some form

- save temp/backup/checksum/migration chain
- save-slot thumbnails
- runtime key rebinding
- graphics/audio settings shell
- localization pipeline
- PDA/archive/scan/atlas signal surfaces
- sonar/echolocation presentation layer
- quest/discovery scaffolding
- dynamic music routing
- surface weather director
- underwater visuals/acoustic stack
- performance diagnostics and budget layers

### Major idea clusters that are still mostly absent

- Steamworks integration
- cloud save sync
- runtime telemetry backend
- workshop/modding/BepInEx stack
- streamer/Twitch integrations
- photo mode / recorder / trailer generation
- accessibility layer beyond baseline input rebinding
- meta-progression and long-tail retention systems
- injury/corrosion/quarantine/material fatigue stack
- full procedural ecology simulation layers from the idea file

### Worktree truth

- unrelated user edits already exist:
  - `Assets/_Project/Scripts/World/FloraInteractionManager.cs`
  - `Docs/DEPRECATED/External_And_Log_Bundles/SARGASOVY ShTUKI/SARGASOVY VODOROSLI.txt`
- `Docs/DEPRECATED/External_And_Log_Bundles/mnogo idey ot dipsika/` is deprecated reference material
- this pass must not rewrite or normalize unrelated user files

---

## Core Decisions

- Do not treat the Deepseek file as a roadmap. Treat it as a suggestion mine.
- Do not implement networked, Steam, Workshop, Twitch, analytics, or modding fantasies before core runtime proof.
- Reuse existing owners. Do not add parallel systems for save, music, PDA, scan, settings, or input.
- Prefer cheap completions of already-existing systems over new headline features.
- Performance-sensitive tasks must favor MX350 guardrails over novelty.
- Beauty passes are valid only when they stay inside current owner boundaries and hardware budget.

---

## Execution Progress

Concrete slices already started from this audit:

- `RebindingManager` persistence moved from PlayerPrefs-only storage to `controls.json` with temp-file writes and legacy migration fallback
- `HectonSurvivalSystem` now records fatal cause before death dispatch
- `SuitAdvisoryController` now surfaces cause-specific fatal advisories
- `SettingsManager` now persists a dedicated graphics preset and forwards it into existing world-quality owners on apply, reset, load, and scene transition
- `SettingsComparisonView` now compares persisted graphics presets instead of `QualityLevel`, fixing false "no change" results between `High` and `Ultra`
- `SettingsLivePreview` now stays aligned with actual Unity 6000 URP ownership: bloom and motion blur preview through `VolumeProfile`, while ambient occlusion remains persisted but not previewed from that path

No runtime gameplay proof is attached for these slices in this document.

Important correction from live API reflection:

- `UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion` exists in the project API
- but it inherits from `ScriptableRendererFeature`, not `VolumeComponent`
- therefore `VolumeProfile.TryGet(...)` is not a valid owner path for AO in the current settings stack

Current verification blocker in active worktree:

- latest force-refresh compile pass produced no `error CS*` or `warning CS*` diagnostics in Unity console
- construction validator now reports `[ConstructionValidation] PASS no issues found.`
- general Unity console still contains unrelated project noise:
  - repeated `The referenced script (Unknown) on this Behaviour is missing!`
  - jobs leak warning for persistent allocations
- no gameplay/runtime proof is attached for the current slices

Status remains `PENDING VERIFICATION`.

---

## Triage Legend

- `implemented` = live owner already exists and core feature is present
- `partial` = owner exists, but the idea is only partially realized
- `absent` = no meaningful implementation found
- `reject` = wrong direction for current project state or conflicts with repo rules
- `defer` = possible later, but blocked by priority/risk/dependencies

---

## Category Audit

### Analytics, integrations, meta-services

- `[1] Runtime telemetry and analytics` — `absent`
  Evidence: no first-party telemetry backend owner found; only local debug/diagnostic traces.
  Decision: `defer`
  Reason: external service work before runtime truth is waste.

- `[9] Experimental flags / A-B balance` — `absent`
  Evidence: no experiment flag service found.
  Decision: `defer`
  Reason: requires telemetry first, otherwise the feature is blind.

- `[10] Steam Rich Presence` — `absent`
  Evidence: no Steamworks first-party owner found.
  Decision: `defer`
  Reason: external dependency, no store/runtime proof captured.

- `[15] Cloud / cross-platform saves` — `absent`
  Evidence: save system is local and strong; no cloud sync owner found.
  Decision: `defer`
  Reason: good future work, wrong first wave.

- `[18] Last 30 seconds recorder` — `absent`
  Evidence: no runtime recorder owner found.
  Decision: `defer`
  Reason: high memory and IO risk on MX350-tier target.

- `[20] Anti-cheat / replay proof for leaderboards` — `absent`
  Decision: `reject`
  Reason: solo product, no current leaderboard stack, no ROI now.

- `[34v2] Stream Deck / MIDI / physical panel integration` — `absent`
  Decision: `reject`
  Reason: niche gimmick, zero production priority.

- `[38v1] Real-world weather API integration` — `absent`
  Evidence: `HectonSurfaceWeatherDirector` exists, but only for in-game authored weather.
  Decision: `reject`
  Reason: external API gimmick, inconsistent with authored world control.

- `[40v2] Steam Workshop content` — `absent`
  Decision: `defer`
  Reason: blocked by missing Steamworks/modding ownership and content validation pipeline.

- `[47v1] Workshop skins` — `absent`
  Decision: `defer`
  Reason: same blockers as Workshop.

- `[49v2] Real scientific dataset integration` — `partial`
  Evidence: world already leans on authored geology/biome matrices; no live NOAA/NASA import path found.
  Decision: `defer`
  Reason: possible authoring/reference input later, not runtime feature.

- `[50v1] Auto-trailer generation via ffmpeg` — `absent`
  Decision: `reject`
  Reason: production sideshow, not game runtime.

- `[50v2] Streamer ghost mode` — `absent`
  Decision: `defer`
  Reason: only matters after core HUD/privacy surfaces stabilize.

- `[68] Real bathymetry map import` — `absent`
  Decision: `defer`
  Reason: could be a tooling lane later, but current world pipeline is already overloaded.

- `[74] Twitch integration` — `absent`
  Decision: `reject`
  Reason: external-event chaos layer before core polish is sabotage.

### Retention, replayability, progression

- `[2] New Game+ / endless seed mode` — `absent`
  Decision: `defer`
  Reason: post-core progression feature; current priority is base product truth.

- `[8] Achievements with unlock rewards` — `partial`
  Evidence: no Steam achievements owner found; in-game reward shell not verified.
  Decision: `defer`

- `[25] Meta-progression between saves` — `absent`
  Decision: `defer`
  Reason: save/game loop foundations first.

- `[26] Seasonal events by real date` — `absent`
  Decision: `reject`
  Reason: content overhead and calendar gimmick without product maturity.

- `[27] Local records / self-competition` — `absent`
  Decision: `defer`
  Reason: cheap later feature once metrics and profile surfaces exist.

- `[40v1] Nightmare mode / permadeath / daily seed` — `absent`
  Decision: `defer`
  Reason: depends on stable death/save/balance truth first.

- `[48v1] Mirrored map mode` — `absent`
  Decision: `reject`
  Reason: one-hour gimmick claim is fantasy; current world stack is not built for that shortcut.

- `[65] Dynamic difficulty` — `absent`
  Decision: `defer`
  Reason: valuable later, but depends on reliable death/progression telemetry.

- `[75] Marathon goals` — `absent`
  Decision: `defer`
  Reason: long-tail retention work after moment-to-moment game solidifies.

### QoL, interface, tools

- `[3] Full gamepad / Steam Deck support` — `partial`
  Evidence: `InputManager`, `RebindingManager`, PDA/pause rebinding UIs exist; no Steam Deck-specific audit found.
  Decision: `implement next`
  Reason: existing owner stack exists; gap is completion, persistence, and validation.

- `[4] Automatic save backups` — `implemented`
  Evidence: `SaveManager` already uses `.tmp`, `.bak`, retention generations, integrity checks.
  Decision: `do not re-implement`

- `[5] Detailed graphics settings for weak hardware` — `partial`
  Evidence: `SettingsManager` exists with graphics toggles; runtime scalability layers also exist.
  Decision: `implement next`
  Reason: finish low-tier usability rather than invent new systems.

- `[6] Hint-without-tutorial system` — `partial`
  Evidence: `PDAContextualAdvisorySystem` now escalates repeated oxygen deaths, pressure exposure, repeated pressure deaths, repeated base emergencies, inventory saturation, and stale-air incidents through existing PDA/logbook/event owners.
  Decision: `keep and extend`
  Reason: the correct advisory owner now exists, but coverage is still focused on survival/base loops rather than the whole game.

- `[7] Modder console` — `absent`
  Decision: `reject`
  Reason: no modding stack; debug console for shipping product is wrong first move.

- `[12] Fog-of-war exploration map` — `absent`
  Evidence: discovery/scan systems exist, but no actual exploration map owner found.
  Decision: `implement later`
  Reason: high value, but needs dedicated PDA/map ownership plan.

- `[17] Better save/load UX` — `partial`
  Evidence: save slot UI and thumbnails exist; polish level not fully verified.
  Decision: `implement later`

- `[30] JSON localization` — `implemented`
  Evidence: localization JSON files and `LocalizationManager` exist.
  Decision: `do not re-implement`

- `[35] Photo mode` — `absent`
  Evidence: editor screenshot helper exists; no runtime photo mode owner found.
  Decision: `defer`

- `[46v2] HUD customization` — `absent`
  Decision: `defer`
  Reason: avoid settings explosion before HUD truth is locked.

- `[58] Blind-play / minimalist mode` — `absent`
  Decision: `defer`

- `[69] Personal notes with screenshots` — `absent`
  Evidence: save thumbnails exist; no PDA notes system found.
  Decision: `implement later`
  Reason: high-value hardcore feature once PDA ownership plan is explicit.

- `[72] Last death marker with cause` — `implemented`
  Evidence: `HectonSurvivalSystem` now persists `SurvivalDeathRecord`; `PDASpectrumTab` surfaces `LAST LOSS` distance + cause tag.
  Decision: `keep and verify`

### Survival, base, crafting

- `[23] Depth pressure system` — `implemented`
  Evidence: `HectonSurvivalSystem` now owns live safe-depth margin, overpressure severity, and pressure attrition telemetry; `SuitAdvisoryController` surfaces real margin/attrition; `PDAContextualAdvisorySystem` uses sustained real overpressure instead of a fake depth constant; `FieldOperationLogSystem` records pressure-window breaches.
  Decision: `keep and verify`
  Reason: this is now a real gameplay contract inside existing owners, not just HUD wording and background damage math.

- `[24] Pipes / cables / logistics between bases` — `absent`
  Decision: `defer`
  Reason: heavy building/system cost.

- `[29] Injury system` — `partial`
  Evidence: `HectonSurvivalSystem` now owns persisted `Bleeding` and `Fracture` states with damage-over-time, timed recovery, death-safe save/load, and collision-linked trauma escalation from `HectonPlayerMovement`; fracture now suppresses swim mobility through a dedicated runtime movement multiplier.
  Decision: `keep and verify`
  Reason: body-state trauma is now grounded in existing player owners, but treatment / healing items and fauna-specific blood-trail consumers are still absent.

- `[31v1] Corrosion of gear` — `partial`
  Evidence: `ToolDurabilitySystem` now applies passive underwater corrosion to the currently held tool, scales corrosion under thermal stress, and `ScannerTool` now degrades below critical condition before hard break.
  Decision: `keep constrained`
  Reason: this is now real runtime wear on the active tool path, but the repo still has no per-instance inventory identity for duplicate tools.

- `[32v2] Manual sample gathering mini-game` — `absent`
  Decision: `defer`

- `[33v2] Dynamic prices / trade` — `partial`
  Evidence: barter-related data/UI exists; no market simulation found.
  Decision: `defer`

- `[36v1] Permanent base upgrades / science passives` — `absent`
  Decision: `defer`

- `[37v1] Player mutations` — `absent`
  Decision: `reject`
  Reason: tone drift and systemic bloat risk.

- `[37v2] Modular vehicles` — `absent`
  Decision: `defer`

- `[38v2] Infection / quarantine` — `absent`
  Decision: `defer`

- `[41v1] Plant breeding` — `absent`
  Decision: `defer`

- `[41v2] Electromagnetic blackout storms` — `partial`
  Evidence: weather and electrical storm presentation layers exist; `AcousticZoneController` now also injects helmet-static pulses and underwater ambient warble from live surface electrical activity, but true gameplay blackout/power-loss layer is still not verified.
  Decision: `implement later`

- `[42v1] Explosive base failures` — `partial`
  Evidence: `BaseModule` now escalates zero-integrity state into oxygen leak / fire / short-circuit cascade failures, but no explosive VFX pass was added.
  Decision: `extend later only if gameplay proof justifies spectacle`

- `[44v2] Secondary recycling` — `absent`
  Decision: `defer`

- `[54] Material fatigue / limited repairs` — `partial`
  Evidence: `BaseModule` now applies permanent repair-cap loss after cascade failures, and `ConstructionManager` persists that reduced ceiling through save/load.
  Decision: `keep scoped to base modules`
  Reason: finite repair depth now exists where the ownership is already clear; broader gear/fabrication fatigue is still absent.

- `[62] Cold and heat system` — `partial`
  Evidence: `HectonSurvivalSystem` now resolves explicit `Cold` / `Heat` stress states from existing atmosphere + local hazard input; cold burns suit energy, heat burns hydration, and both feed `SuitAdvisoryController` plus `PDAContextualAdvisorySystem`.
  Decision: `keep and verify`
  Reason: the gameplay contract exists now, but authored biome/zone balancing and dedicated thermal shelter items are still absent.

- `[63] Random base accidents` — `partial`
  Evidence: `ConstructionManager` now runs a low-frequency ambient-accident scheduler on neglected modules and escalates through existing `BaseModule` cascade failures.
  Decision: `keep constrained`
  Reason: accident pressure now exists, but it is intentionally limited to worn / unpowered / flooded modules instead of constant harassment RNG.

- `[66] Multi-stage crafting` — `absent`
  Decision: `defer`

- `[71] Air recycling / CO2` — `implemented`
  Evidence: `BaseModule` now owns finite breathable reserve, powered scrubber recovery, stale-air refill throttling, and reserve collapse penalties; `ConstructionManager`/`ModuleDTO` persist reserve state; scanner and suit advisory surface low-air compartments.
  Decision: `keep and verify`

### Ecosystem, world, exploration

- `[11] Learn through death` — `implemented`
  Evidence: death-facing advisory now shows fatal cause, tactical advice, and exact last-run stats from persisted survival telemetry.
  Decision: `keep and verify`

- `[13] Genetic variability in creatures` — `absent`
  Decision: `defer`

- `[16] Secret biomes via portals` — `absent`
  Decision: `reject`
  Reason: cheap mystery fantasy, expensive world debt.

- `[21] Underwater currents simulation` — `partial`
  Evidence: `CurrentManager` and `CurrentVolume` exist.
  Decision: `implement later`
  Reason: likely expand existing owner, not invent new fluid logic.

- `[22] Biolum communication` — `partial`
  Evidence: `HectonBiolumController` and `HectonBiolumManager` now react to `SpectrumEvents.OnSonarPulse`, pushing short-lived response into existing global biolum pulse and flora shader-global paths; live authored-scene readability is still not verified.
  Decision: `continue verification later`

- `[32v1] Creature nesting` — `absent`
  Decision: `defer`

- `[33v1] Underwater traversal / vortices / updrafts` — `partial`
  Evidence: `CurrentVolume` now supports authored radial, vortex, updraft, and downdraft flow patterns through the existing sample path used by player movement, buoyancy, and ambient motion; live scene usage/tuning is still not verified.
  Decision: `implement later`

- `[35v1] Ecosystem epidemics` — `absent`
  Decision: `defer`

- `[43v2] Fish migrations` — `absent`
  Decision: `defer`

- `[44v1] Deep worms changing terrain` — `absent`
  Decision: `reject`
  Reason: terrain mutation cost is too high relative to current stability needs.

- `[45v2] Thermal vents / vortex traps` — `partial`
  Evidence: authored `CurrentVolume` can now express updraft/downdraft and spiral pull patterns without a new subsystem; thermal vent / trap placement and scene tuning are still not verified.
  Evidence: thermal world language exists; direct trap/gameplay layer not verified.
  Decision: `implement later`

- `[47v2] Protected hidden nests` — `absent`
  Decision: `defer`

- `[48v2] Deja vu anomalies` — `absent`
  Decision: `defer`
  Reason: cheap later world garnish, not first priority.

- `[56] Seasonal world changes` — `absent`
  Decision: `reject`
  Reason: huge content/state burden for little core payoff.

- `[60] Hidden elevator entrances to sub-biomes` — `absent`
  Decision: `defer`

- `[61] Active echolocation / sonar tool` — `implemented`
  Evidence: active sonar now drains suit energy and emits fauna-provocation/noise consequences through existing `SpectrumSystem` and spatial-grid owners.
  Decision: `keep and balance`

- `[73] Microplastic pollution mechanic` — `absent`
  Decision: `reject`
  Reason: theme stretch plus system bloat.

### Immersion, visual, atmosphere

- `[19] NPC AI chat with local LLM` — `absent`
  Decision: `reject`
  Reason: gimmick, hardware cost, zero current owner.

- `[28] Dynamic music` — `implemented`
  Evidence: `HectonMusicDirector` exists and already routes by zone/depth/pressure/tension.
  Decision: `extend only if evidence proves missing coverage`

- `[31v2] Helmet fogging` — `implemented (code-only)`
  Evidence: `VisorHUDController` + `SuitVisor.shader` now drive edge condensation from existing survival temperature/pressure telemetry without a new owner.
  Decision: `verify in live play`

- `[34v1] Electromagnetic storms` — `implemented (code-only)`
  Evidence: `HectonSurfaceWeatherDirector` now pulses existing visor/HUD + flashlight owners from live `electricalActivity` and lightning events.
  Decision: `verify cadence in live storms`

- `[36v2] Deep voices / whispers` — `absent`
  Decision: `defer`
  Reason: audio content pass after atmosphere core is measured.

- `[39v1] PDA TTS voices` — `absent`
  Decision: `reject`
  Reason: tech gimmick and localization debt.

- `[46v1] Thermocline layer` — `implemented (code-only)`
  Evidence: `HectonUnderwaterVisuals` now detects `DepthZoneDirector.CurrentZone` transitions and drives existing volume/visor/audio owners from `DepthZoneProfile` ambience deltas.
  Decision: `verify across current biome/depth authoring`

- `[49v1] Emotes and gestures` — `absent`
  Decision: `defer`

- `[52] Pulse effect synced to player stress` — `implemented (code-only)`
  Evidence: `SuitHUDV4CanvasOverlay` now reads `HectonPlayerMovement.CurrentUnderwaterStressIntensity01` and drives a low-cost rhythmic pulse through existing HUD chrome/reticle/gauge colors instead of adding another fullscreen post-process lane.
  Decision: `verify in live storm / high-stress readability pass`

- `[55] Caustics` — `partial`
  Evidence: `HectonUnderwaterVisuals` already owns a cheap shallow-caustics path (`enableShallowCaustics`, `_CausticsStrength`) suitable for weak hardware.
  Decision: `defer extra implementation`
  Reason: adding a second cookie/decal runtime path would duplicate an existing owner and carries unnecessary MX350 risk.

- `[59] Learning AI companion drone` — `absent`
  Decision: `defer`
  Reason: big systemic and narrative owner cost.

- `[64] Gesture wheel for NPCs` — `absent`
  Decision: `defer`

- `[67] Damage decals on creatures` — `absent`
  Decision: `defer`
  Reason: visual polish later; verify Decal Projector cost first.

### Content, mysteries, lore

- `[14] Quest system` — `partial`
  Evidence: `QuestManager`, `QuestData`, `QuestEvents` exist.
  Decision: `implement later`
  Reason: extend the current owner instead of pretending the feature is missing.

- `[39v2] Diplomacy with intelligent species` — `absent`
  Decision: `reject`
  Reason: content explosion and tone drift for current project state.

- `[53] Underwater mail / bottle lore drops` — `absent`
  Decision: `implement later`
  Reason: cheap content system once world breadcrumb ownership is explicit.

- `[57] Ritual system` — `absent`
  Decision: `reject`
  Reason: wrong tonal lane unless existing lore later proves otherwise.

- `[70] Unlockable concept art folders` — `absent`
  Decision: `defer`

- `[76] Developer room easter egg` — `absent`
  Decision: `defer`

- `[77] Photo-hunt mode` — `absent`
  Decision: `defer`
  Reason: depends on photo mode, task/request owner, fauna behavior pose support.

---

## Priority Stack

### Tier 0: do not touch now

- Steamworks / Workshop / Twitch / external APIs
- local LLM NPC chat
- Steam Workshop / BepInEx distribution layer before the internal API foundation is proven
- real-world weather/data integrations
- terrain-altering fauna fantasies
- seasonal world rewrites

### Tier 1: high-value completions on existing owners

- control remap persistence and UX completion
- low-tier graphics/settings completion for MX350 readability
- save/load UX cleanup around the already-strong save backbone
- death feedback / cause / teaching loop on top of current survival/advisory owners
- official internal modding foundation on current owners:
  - `HectonEventBus`
  - `HectonAPI`
  - dependency-ordered `ModLoader`
  - official save injection for mod payloads
- exploration map / notes once PDA ownership is scoped
- last-loss marker persistence and retrieval on PDA/radar owners

### Tier 2: beauty features that can be cheap

- caustics: cheap shallow path already exists in `HectonUnderwaterVisuals`; keep and verify, no second runtime path added
- helmet fogging: implemented in existing visor owner, still `PENDING VERIFICATION`
- thermocline presentation: implemented in existing underwater/transition owners, still `PENDING VERIFICATION`
- better storm gameplay coupling through existing weather/acoustic owners: implemented in weather + visor + flashlight owners, still `PENDING VERIFICATION`

### Tier 3: medium systems after proof

- quest expansion on `QuestManager`
- current/traversal hazard expansion on current/weather owners

---

## Performance Impact Model

### Likely improves performance

- better low-tier graphics controls on the existing settings stack
- finishing input/settings UX so players can actually downscale bad defaults
- extending current owners instead of adding parallel runtime layers

### Likely hurts performance if done blindly

- runtime recorder / GIF / MP4 buffer
- local LLM dialogs
- Twitch/Workshop/Steam overlays and polling
- terrain mutation features
- real-world weather/data API hooks
- fake “one-hour” world-transform gimmicks like mirrored map mode

### Likely improves beauty

- caustics
- helmet fogging
- thermocline transitions
- richer storm-to-HUD/audio coupling
- quest/breadcrumb surfaces that make the world read with intent

### Likely causes regression risk

- adding new global owners for already-owned systems
- touching project settings or package graph
- external-service features before runtime stabilization
- any save rewrite that ignores the existing robust backup chain

---

## Immediate Execution Order

### Workstream 1: establish audit bundle and live changelog

Result required:

- done in this folder

### Workstream 2: finish input rebinding persistence on the existing owner

Target:

- `Assets/_Project/Scripts/Input/RebindingManager.cs`

### Workstream 2A: establish official internal modding foundation on existing owners

Target:

- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonGameEvents.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
- `Assets/_Project/Scripts/ModdingAPI/IHectonMod.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/SaveData.cs`
- `Assets/_Project/Scripts/SaveDataMigration.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/ItemCatalog.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
- `Assets/_Project/Scripts/PlayerBuilder.cs`

Result required:

- typed safe event bus for mod subscribers with per-handler exception isolation
- cancellable damage hook through `HectonSurvivalSystem.TakeDamage()`
- runtime-safe item injection through `ItemCatalog` overlay without mutating authored ScriptableObject lists
- dependency-ordered code-mod loader scanning `Mods/` next to `Application.dataPath`
- official mod save payload injection through `SaveData.CustomModData`
- public façade through `HectonAPI`

Constraints:

- direct owner hooks only where owner timing matters:
  - pre-damage cancellation in `HectonSurvivalSystem`
  - post-placement world object hook in `PlayerBuilder`
- lifecycle bridge stays central on current owners:
  - `SaveManager` / `SaveEvents`
  - `SceneBootstrap`
  - `CraftingEvents`
- `QuestManager` is intentionally not used as a fake bridge owner in this pass because that would be architecture drift
- external managed-code loading is `PENDING VERIFICATION` and carries IL2CPP risk; current loader explicitly warns and disables dynamic assembly loading on IL2CPP builds

### Workstream 2B: extend modding foundation into content pipeline and supported settings UI

Target:

- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeInfo.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLocalizationBridge.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModMenuUIController.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModMenuModEntryView.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModMenuSettingToggleView.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModMenuSettingSliderView.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`
- `Assets/_Project/Scripts/UI/SettingsPanel.cs`

Result required:

- AssetBundle-backed content loading for prefabs, audio clips, and textures through `HectonAPI.Assets`
- cold-path raw `.png` fallback for texture loading when a mod ships loose files instead of a bundle
- mod localization discovery via `lang_*.json` and post-init injection into the existing `LocalizationManager`
- runtime loader registry with mod status, directory, bundle, and localization metadata for diagnostics/UI
- supported mod settings registration surface through `HectonAPI.UI.RegisterSetting(...)`
- optional `ModMenuUIController` view for a dedicated Mods panel inside settings without replacing first-party settings ownership
- persistent world spawn wrapper through `HectonAPI.World.SpawnPersistentPrefab(...)` with save-roundtrip restore

Constraints:

- content pipeline stays on current runtime owners:
  - loader owns discovery
  - asset manager owns bundle caches
  - localization owner remains `LocalizationManager`
  - settings persistence owner remains `UserOptionsPersistence`
- no direct reflection patching into localization dictionaries
- no new save schema expansion for world mod spawns; persistence goes through existing `SaveData.CustomModData`
- UI extension is optional and prefab-wired; current menus remain intact if the Mods panel is not authored into a scene/prefab yet

### Workstream 2C: ship an internal modding SDK and content injection for crafting/building

Target:

- `Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModMetadata.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/Fabricator.cs`
- `Assets/_Project/Scripts/ModuleCatalog.cs`

Result required:

- editor-side `Hecton Mod Builder` window for packing supported mod folders without hand-writing `mod.json`
- optional AssetBundle build from an authored `Assets/` subtree into `Mods/[modId]/[modId].bundle`
- managed DLL copy pipeline into the runtime `Mods/[modId]/` package root
- manifest generation with:
  - `Id`
  - `Name`
  - `Version`
  - `Author`
  - `Dependencies`
  - `EntryAssembly`
- supported `HectonAPI.Crafting.RegisterRecipe(...)` surface
- supported `HectonAPI.Construction.RegisterBuildable(...)` surface
- runtime recipe/buildable overlays that do not mutate authored ScriptableObject lists
- live `Fabricator` and `ModuleCatalog` owners extended to read runtime overlays

Constraints:

- recipe injection stays on the real crafting owner:
  - `Fabricator.AvailableRecipes`
  - no fake `CraftingManager` clone
- buildable injection stays on the real construction owner:
  - `ModuleCatalog`
  - `ConstructionManager.Catalog`
- authored `availableRecipes` / `allModules` lists remain source-of-truth assets and are never mutated at runtime
- alternative recipes for first-party result items are allowed; collision rejection is only for invalid payloads or construction identity alias conflicts
- runtime custom build categories remain sidecar metadata; they do not overwrite `BuildableData.family`

Problem:

- rebinding runtime exists, but persistence is still PlayerPrefs-only
- `SYSTEMS_CONTRACTS.md` expects full remap persistence to `controls.json`

Action:

- preserve existing public API
- add `controls.json` file persistence under the existing owner
- keep legacy PlayerPrefs fallback/migration so current users are not broken

### Workstream 3: verify touched slice

Required proof:

- compile proof if available in current environment
- if compile proof is blocked, mark code-review-only
- do not mark solved without logs

---

## First Execution Slice Chosen

Chosen slice: `controls.json` rebinding persistence.

Why this slice:

- it is directly supported by existing owners
- it closes a documented contract gap
- it improves gamepad/Steam Deck readiness without inventing new systems
- it avoids scene, prefab, package, and third-party risk

Current pass result:

- audit bundle created
- `RebindingManager` patched to use `controls.json` file persistence
- legacy PlayerPrefs payload remains as migration fallback
- compile/runtime proof still missing in current environment

Status: `CODE COMPLETE, PENDING VERIFICATION`

---

## Regression Model

- CPU: no hot-path impact expected because save/load occurs only on explicit rebind persistence actions
- GC: no gameplay-frame change expected; file IO is not added to `Tick`
- memory: negligible, limited to serialized binding JSON payload
- cadence: affects explicit controls UI flow only
- correctness risk:
  - malformed file payload
  - migration path from old PlayerPrefs payload
  - save/load failure on invalid persistent-data path
- why kept:
  - closes a documented gap with narrow blast radius

---

## Verification State

- repository/document evidence gathered: `yes`
- Unity/runtime proof gathered: `no`
- compile proof gathered: `no`

Environment blocker:

- `dotnet` not installed
- `msbuild` not installed
- `csc` not installed

Status remains `PENDING VERIFICATION`.
