# HECTON-8 Lore Placement Audit

Status: `PENDING VERIFICATION`
Date: `2026-04-15`
Scope: `Assets/_Project/Scenes` + `Assets/_Project/Prefabs` + lore registries/editor autofill

## Purpose

This document records the current truth of lore placement, not the intended design.

The question is simple:

`Do the implemented lore systems have real player-facing placements in first-party scenes/prefabs?`

## Evidence Collected

### 1. No serialized `NarrativeDiscovery` placements were found in first-party scenes/prefabs

Repository text audit across:

- `Assets/_Project/Scenes/*.unity`
- `Assets/_Project/Prefabs/**/*.prefab`

found no serialized references to the `NarrativeDiscovery` script and no matching discovery IDs for the current colonist/audio-log content slice.

### 2. No serialized `AudioLogPickup` placements were found in first-party scenes/prefabs

Repository text audit across the same first-party scene/prefab scope found no serialized references to `AudioLogPickup`.

### 3. Lore framework exists, but placement evidence is absent

The following systems/assets do exist:

- `AudioLogSystem`
- `QuestManager`
- `HectonNarrativeDirector`
- `ColonistLoreRegistry`
- `AudioLogData` assets
- `NarrativeDiscoveryAutoFillEditor`

This means the current blocker is not missing code framework.

The likely blocker is one of:

- world placements were never authored
- placements exist only as unsaved/local scene state outside the repository truth
- placements exist in non-first-party or ignored asset locations

### 3b. Live Unity scene confirms the placement count is currently zero

Live Unity inspection of the open `02_HECTON_WORLD` scene found:

- `NarrativeDiscovery`: `0`
- `AudioLogPickup`: `0`

The active `LoreSystems` root also reported:

- `_narrativeDiscoveryCount = 0`
- `_audioLogPickupCount = 0`

This removes the main ambiguity.
For the currently loaded production world, the player-facing lore placement count is not merely uncommitted in text assets.
It is zero in the live scene state.

### 4. Registry truth was partially repaired

`ColonistLoreRegistry.asset` now carries real `linkedAudioLog` references for entries with confirmed matching `AudioLogData` assets:

- `chen_m_datapad_01`
- `captain_last_broadcast`
- `biologist_samples`
- `medic_diary`
- `atlas6_terminal_sector3`

Entries without confirmed matching `AudioLogData` assets remain intentionally unlinked.

### 5. Editor autofill was incomplete and is now repaired

`NarrativeDiscoveryAutoFillEditor` previously copied only:

- `displayName`
- `interactVerb`

It now also copies:

- `linkedAudioLog`

This prevents further authoring drift once scene placements actually exist.

## Runtime/Product Risk

If the repository truth reflects the shipping scene truth, then the lore experience has a structural gap:

- the player can have archive/data systems
- the player can have quest/narrative framework
- but the world may still contain zero placed lore POIs and zero placed audio-log pickups

That means the game signals narrative depth in architecture while offering no guaranteed world entry points.

## Recovery Changes Already Added

### Validation path

`HectonLoreSystemsRoot.ValidateSystems()` now reports:

- system presence
- `NarrativeDiscovery` placement count
- `AudioLogPickup` placement count

It also emits explicit warnings when those counts are zero.

`HectonLoreSystemsRoot` startup now also refreshes those counts in runtime and emits a one-shot development warning if player-facing lore placement is still missing.

### Lore honesty path

The following runtime/source-of-truth repairs were already completed in parallel with this audit:

- PDA archive no longer self-unlocks undiscovered logs
- text-only logs are labeled honestly in PDA
- world prompts now distinguish voiced logs from text-only/archive-only records
- registry `linkedAudioLog` references were repaired for confirmed matching entries

### Runtime fail-safe path

`HectonLoreSystemsRoot` now contains a runtime-only recovery pass for the confirmed `0 NarrativeDiscovery / 0 AudioLogPickup` state.

It seeds five surrogate lore entry points from real reachable landmarks, using `ColonistLoreRegistry` as the runtime source of truth for `linkedAudioLog` references:

- `chen_m_datapad_01` -> `Scrap_Field`
- `biologist_samples` -> `Organic_Garden`
- `medic_diary` -> `Chemical_Seep`
- `captain_last_broadcast` -> `Forward_Fabricator` side marker
- `atlas6_terminal_sector3` -> `Forward_Fabricator` side marker

This path exists because the live production world currently proves the placement count is zero.
It is a shipping safety net, not an authored-world substitute.

WARNING: Regression risk in interaction readability if surrogate invisible collider markers sit too close to resource interactions or the fabricator itself.

### Live Play Mode verification

After wiring `LoreSystems.runtimeRecoveryRegistry` to `ColonistLoreRegistry.asset`, live Play Mode verification confirmed:

- `Lore_ChenDatapad01` created under active host `Resource_FieldSources` with `BoxCollider + NarrativeDiscovery`
- `Lore_BiologistSamples` created under active host `Resource_FieldSources` with `BoxCollider + NarrativeDiscovery`
- `Lore_MedicDiary` created under active host `Resource_FieldSources` with `BoxCollider + NarrativeDiscovery`
- `Lore_CaptainBroadcastTerminal` created under active host `Fabrication_Outpost` with `BoxCollider + AudioLogPickup`
- `Lore_Atlas6Terminal` created under active host `Fabrication_Outpost` with `BoxCollider + AudioLogPickup`

This host move was required because the first recovery pass attached markers to child anchors that are inactive in Play Mode.
The corrected recovery now places markers on active parent route roots, and live readback confirmed `activeInHierarchy = true` for the checked narrative and audio marker samples.

`LoreSystems` live state in Play Mode reported:

- `_narrativeDiscoveryCount = 3`
- `_audioLogPickupCount = 2`

The console also emitted the expected runtime warning:

`[LoreSystemsRoot] Applied runtime lore recovery because the production scene had no placed player-facing lore. This is a fail-safe, not a substitute for authored placement.`

## What Still Needs To Happen

### Required next step

A live Unity scene authoring pass must verify one of two truths:

1. placements actually exist in `02_HECTON_WORLD` but are not reflected in repository text truth
2. placements do not exist and must be authored

The new runtime fail-safe does not remove this requirement.
If it fires in Play Mode, that is proof the authored scene is still incomplete.

### If placements do not exist

Then the next real production task is not more framework work.

It is:

- place `NarrativeDiscovery` world POIs
- place `AudioLogPickup` world pickups where appropriate
- run autofill against the repaired registry
- verify discovery flow in Play Mode

### Concrete placement map prepared from live route owners

Live scene evidence currently gives three usable early-route anchors:

- `--- WORLD ---/Resource_FieldSources/Scrap_Field`
- `--- WORLD ---/Resource_FieldSources/Organic_Garden`
- `--- WORLD ---/Resource_FieldSources/Chemical_Seep`
- `--- WORLD ---/Fabrication_Outpost/Forward_Fabricator`

These are not ideal lore-fiction matches for every registry hint.
They are the current honest shipping landmarks that actually exist in the live scene.

Prepared first-pass recovery mapping:

- `chen_m_datapad_01` -> `Scrap_Field`
- `biologist_samples` -> `Organic_Garden`
- `medic_diary` -> `Chemical_Seep`
- `captain_last_broadcast` -> `Forward_Fabricator` side terminal marker
- `atlas6_terminal_sector3` -> `Forward_Fabricator` side terminal marker

Why this mapping is acceptable for recovery:

- it uses live route-critical or route-adjacent landmarks the player can actually reach
- it avoids attaching lore to already interactive `PickupItem` owners
- it seeds the world with real discoverable narrative entry points immediately

Why this mapping is still second-best:

- current scene authoring does not expose real colony-module landmarks matching the registry hints
- two entries would be surrogate terminal markers on the fabrication outpost rather than true command/navigation spaces
- deeper lore fiction is still constrained by missing authored world spaces, not by framework code

### If placements do exist in live Unity but not in repo

Then the task is repository truth recovery:

- save the real scene state
- ensure prefab/scene ownership is explicit
- re-audit serialized scene assets after save

## Conclusion

Current evidence says:

`Lore systems are implemented, registry truth is improved, and a verified runtime fail-safe now restores 5 player-facing lore entry points when authored placement is absent. Authored production placement is still missing and remains the next required cleanup target.`
