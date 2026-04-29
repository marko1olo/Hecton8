# HECTON-8 Player Retention Recovery Plan

Status: `PENDING VERIFICATION`
Date: `2026-04-15`
Owner: `Codex audit -> recovery pass`

## Purpose

This document exists because the current project state is structurally dangerous:

- the engineering layer is wider than the proven player loop
- the first hour is partially faked by timing instead of earned action
- quest and narrative ownership is split between multiple systems
- production truth and self-reported docs do not fully match

This is not a pitch document. It is a recovery plan for turning the current build from a technically ambitious prototype into a player-motivated game.

## Current Truth

### What is strong

- bootstrap / save / shell / world-framework architecture is broad
- there is real infrastructure for streaming, scatter, quests, lore, scanning, crafting, construction, audio, and progression
- there is enough authored data to support a real game if the ownership and player loop are corrected

### What is weak

- the project often simulates progress instead of proving it through player action
- first-hour pacing is too often timer-led
- quests exist, but part of the early chain is logically broken or underused
- production world content still carries trial / staging / proving-ground contamination
- some content assets are shells without payload
- some internal docs overstate readiness

## Why Players May Leave

### 1. The game promises more than it pays out

The project signals depth, mystery, pressure, civilization ruins, and systemic survival. The current codebase proves that the systems exist. It does not yet prove that the player is being pulled forward by a clean loop of:

`fear -> curiosity -> action -> reward -> new capability -> deeper risk`

Without that loop, the player experiences architecture instead of desire.

### 2. The first hour lacks enough earned milestones

The current first-hour layer has been using timed milestones for beats that should be tied to:

- real crafting
- real discovery
- real scanning
- real route commitment
- real resource pressure

If competence is granted by timer, the player feels managed, not empowered.

### 3. Early goals are not sharp enough

A player needs to know:

- what to do now
- why it matters
- what new danger or reward lies beyond it

Current quest/first-hour ownership does not consistently deliver that. Some early quest assets are present but not wired into a clean chain.

### 4. The world can still read as a workshop

If the production scene contains trial/staging language, if some logs have no audio payload, if shell docs claim superiority without live proof, the product reads as internal build rather than hostile living world.

Players leave when trust drops.

## Recovery Principles

### Principle 1. Replace fake progress with earned progress

Anything described as:

- first craft
- first module
- first contact
- first pressure jump

must be triggered by player action or player-located world truth, not by elapsed time alone.

### Principle 2. One owner per player-facing truth

Current ownership split:

- `FirstHourDirector` owns pacing beats
- `QuestManager` owns data-driven goals
- `MissionManager` also exists and risks overlap

Recovery rule:

- `QuestManager` is the owner of explicit player goals
- `FirstHourDirector` may pace the arc, but it must not silently replace real goal ownership
- `MissionManager` should not become a second early-game objective system unless its role is narrowed later

### Principle 3. The first 90 minutes must form a hard chain

The player should move through a chain like this:

1. Survive arrival
2. Learn immediate orientation
3. Secure first useful material
4. Complete first craft
5. Reach first pressure threshold
6. Detect first meaningful ruin/module truth
7. Receive the next deep-world pull

If any of these are soft, hidden, late, or fake, retention drops.

## Workstreams

## Workstream A - First Hour Recovery

### Goal

Make first-hour milestones earned, legible, and chained to actual player behavior.

### Problems

- `FirstCraft` was triggered by any narrative discovery in a time window
- `FirstModule` was granted on a timer, not on real structure contact
- first-hour beats were not consistently pushing or resolving actual quest state

### Required changes

- tie `FirstCraft` to `CraftingEvents.OnCraftCompleted`
- tie `FirstModule` to real module/ruin contact
- use `QuestManager` to complete/start real early goals
- stop auto-awarding competence beats by time alone

### Acceptance

- first craft milestone only occurs after real completed craft
- first module milestone only occurs after scan/discovery/world contact
- first arrival quest can complete
- first material quest appears before the player already solved it

## Workstream B - Early Quest Chain Repair

### Goal

Make the first quest chain actually function as a chain.

### Problems

- `quest_arrival` starts but does not complete through a live owner path
- `quest_copper_sample` activates after the copper is already collected and does not complete through event ownership

### Required changes

- complete `quest_arrival` from a real first-hour milestone
- convert `quest_copper_sample` into a real manual-activation + item-completion quest
- let the first-hour layer hand off to the quest layer instead of duplicating it

### Acceptance

- arrival objective starts and completes without debug intervention
- copper objective activates as a readable next-step goal
- copper objective completes on actual copper pickup

## Workstream C - Scene Truth Cleanup

### Goal

Remove prototype language and proving-ground contamination from the shipping route.

### Problems

- production scene still contains trial/staging strings and likely staging content

### Required changes

- inventory all trial/staging objects in `02_HECTON_WORLD`
- classify: shipping content / hidden dev harness / remove
- remove or isolate workshop content from player traversal

### Acceptance

- no visible trial/staging/proving-ground naming or structures in player route
- no player-critical shell/system depends on dev-only scene objects

## Workstream D - Content Payload Honesty

### Goal

Stop shipping shells that imply content but do not deliver it.

### Problems

- multiple audio log assets exist with no `audioClip`
- the PDA archive currently presents text-only logs as if they were voiced playback
- the PDA archive can self-unlock undiscovered logs if discovery gating is not enforced in UI
- some quest assets use `Manual = 99`, which must be audited case-by-case instead of assumed to be placeholder debt

### Required changes

- audit all lore/audio quest assets
- classify by state: real / shell / dead
- expose text-only lore entries honestly in PDA/archive UI
- prevent PDA/archive surfaces from bypassing world discovery ownership
- either wire payload or remove from player-facing discovery surfaces

### Acceptance

- no surfaced audio logs without playable or intentionally text-only payload
- no early-game quest asset with inverted or dead trigger logic

## Workstream E - Product Honesty

### Goal

Stop internal documentation from masking unresolved reality.

### Problems

- some docs claim shell/UX superiority while also admitting manual wiring and pending verification

### Required changes

- mark all overclaiming docs as internal hypothesis, not truth
- tie claims to build evidence only
- stop using comparison language unless validated in build

### Acceptance

- no production-readiness doc contradicts its own verification state

## Phase Plan

## Phase 0 - Immediate Repairs

This phase should happen first because it directly affects player motivation.

- repair first-hour milestone ownership
- repair `quest_arrival`
- repair `quest_copper_sample`
- make first module discovery action-based instead of timer-only

### Phase 0 Execution Log

#### Completed in code

- `FirstCraft` moved from fake narrative-timer detection to `CraftingEvents.OnCraftCompleted`
- `FirstModule` moved from timer auto-award to real discovery / scan contact
- `quest_arrival` now completes from the orientation milestone through `QuestManager`
- `quest_copper_sample` now uses manual activation plus real item-completion on `Data_Copper`
- old-save recovery now checks `SaveData.inventory` so the copper quest cannot stay active after the player already owns copper
- `quest_first_breath` is now activated as the next explicit goal when the copper goal completes
- `HectonLoreSystemsRoot` now has a registry-backed runtime lore recovery pass for the confirmed `0/0` placement state
- `LoreSystems.runtimeRecoveryRegistry` is now wired to `ColonistLoreRegistry.asset` in `02_HECTON_WORLD`
- live Play Mode verification confirmed the fail-safe creates `3 NarrativeDiscovery` and `2 AudioLogPickup` markers when authored scene placement is absent
- the recovery hosts were moved from inactive child anchors to active parent route roots, and checked marker samples now resolve as `activeInHierarchy = true` in Play Mode

#### Still pending

- current recovery slice compiles without console errors; remaining console noise is warning-level and includes `CameraJuiceSystem` volume configuration plus `AcousticZoneController` snapshot authoring gaps
- production scene contamination is now partially contained at runtime, but authored scene cleanup is still pending
- surfaced lore/audio payload audit has started; text-only log support is real, and the PDA/archive honesty slice is repaired, but asset-by-asset payload audit is still incomplete
- project asset audit currently shows no serialized `NarrativeDiscovery` or `AudioLogPickup` placements in first-party scenes/prefabs; runtime fallback now exists and is verified, but authored content placement is still a blocker
- first-90-minute route validation still needs live playtest evidence

#### New scene-truth recovery output

- added `SCENE_TRUTH_CLEANUP_AUDIT.md`
- shipping runtime now suppresses known `Tool_Staging` / `Fabrication_Trial` trial-route content before world startup
- live authored cleanup now also sets `--- WORLD ---/Tool_Staging`, `Fabrication_Trial`, and `__TEMP_DENSE_KELP_PREVIEW` inactive in `02_HECTON_WORLD`
- added `LORE_PLACEMENT_AUDIT.md`
- `HectonLoreSystemsRoot.ValidateSystems()` now reports zero-placement lore states instead of only system presence
- `HectonLoreSystemsRoot` startup now refreshes lore placement counts and emits a one-shot development warning when player-facing lore is still missing

#### Content-payload truth corrections

- `QuestData.Manual = 99` is a valid contract, not automatic placeholder debt
- the real lore issue is narrower and more important: text-only logs exist legitimately, but PDA/archive UX must not mislabel them or unlock them without discovery
- world zone/socket registries now ignore `zone.trial.*` content even if authoring residue still exists in the scene
- `Draft Terrain` and `CurrentVolume_PlayerSpawn_Test` remain audit-only until live Unity inspection proves they are safe to remove

#### Current blocker snapshot

- stale / unrelated compile failures currently visible in local editor logs include:
- `World/Biolum/HectonBiolumZone.cs`
- `Bootstrap/BootstrapController.cs`
- missing source files in `Scripts/Editor/` and `Scripts/Dev/`
- namespace / using failures in `HectonPlayerMovement.cs` and `VFX/CameraJuiceSystem.cs`

This means retention recovery code can be reviewed statically, but build-health confirmation remains `PENDING VERIFICATION`.

## Phase 1 - Early Route Hardening

- verify the first 20-40 minutes in a real build
- add route-critical notifications only where the player lacks clarity
- remove dead shell friction in pause/settings/save if it still breaks trust

## Phase 2 - World Truth Cleanup

- purge trial/staging contamination from the production scene
- verify real discovery targets exist on intended routes
- confirm the player can naturally hit at least one memorable ruin/module beat

## Phase 3 - Payload Recovery

- fix surfaced lore without payload
- wire or remove hollow quest/lore beats
- ensure discoveries lead to unlocks, routes, or knowledge with consequence

## Phase 4 - Retention Validation

- run build playtest focused on first 90 minutes
- measure whether goals are legible without dev knowledge
- identify dead time between beats

## Regression Model

### CPU

Early-game fixes must remain event-driven and avoid poll loops or new heavy scan logic.

### GC

No new per-frame allocs are allowed. Early-game recovery should stay on static events and existing managers.

### Memory

Quest/first-hour fixes are negligible. Scene cleanup may reduce memory if dev content is removed.

### Cadence

Risk: over-scripting the opening and turning the world into a tutorial.

Mitigation:

- keep goals sparse
- let the world remain the primary teacher
- use quest/objective messaging only to sharpen direction, not to over-explain

### Correctness

Risk: desync between first-hour milestone state and quest save state.

Mitigation:

- use existing `QuestManager` public API
- synchronize milestone-driven quest handoffs after load

## Immediate Implementation Started In This Pass

- replace fake `FirstCraft` trigger with real craft completion ownership
- replace fake `FirstModule` timer grant with real discovery/scan ownership
- complete `quest_arrival` when orientation is actually reached
- convert `quest_copper_sample` into a real next-step quest

## Verification Required

Everything below remains `PENDING VERIFICATION` until a live run or build confirms:

- first arrival quest starts and completes in a clean run
- copper quest appears after orientation and completes on real copper pickup
- first craft milestone does not fire from unrelated discovery
- first module milestone does not fire without real world contact
- no save/load desync appears between first-hour bits and quest state
