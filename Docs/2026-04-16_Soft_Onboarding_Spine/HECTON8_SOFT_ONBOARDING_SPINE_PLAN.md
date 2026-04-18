# HECTON-8 Soft Onboarding Spine

Status: `PENDING VERIFICATION`
Date: `2026-04-16`
Owner: `Codex implementation pass`

## Purpose

This is not a fantasy rewrite.

The repo already contains most of the systems needed for a strong first-hour introduction:

- `FirstHourDirector`
- `QuestManager`
- `WorldReadabilityDirector`
- `WorldZoneDirector`
- `BiomeMatrixDirector`
- `AudioLogSystem`
- `AtlasSignalSystem`
- runtime lore recovery through `HectonLoreSystemsRoot`

The current weakness is composition.
The player is being addressed by parallel systems instead of being pulled through one readable, escalating route.

This document defines the route spine the runtime should reinforce.

## Design Rule

Subnautica does not lead by hard tutorial lock.
It leads by readable promises:

1. a safe return point
2. a visible near-term lure
3. a first shortage that requires commitment
4. a meaningful return
5. a breadcrumb that converts curiosity into route ownership
6. a larger distant mystery that matters later

HECTON-8 must do the same in its own fiction.

## HECTON-8 Equivalents

- Lifepod equivalent: `Fabrication Outpost` / fabrication stop / human foothold
- Early safe shelf equivalent: starter resource water with one readable silhouette and one reliable calm pocket
- Lifepod breadcrumb equivalent: service relays, terminal markers, maintenance stops, emergency work caches
- Aurora equivalent: a delayed world event or system rupture that changes the meaning of a known horizon
- Radio chain equivalent: logs + relay hints + route-critical service markers
- Macro mystery equivalent: `Atlas-6` signal, but not as the first major pull

## Required First-Hour Spine

### Phase 0 - Arrival / Orientation

Player truth:

- I am not safe everywhere
- I do have one readable fallback
- I can leave and return on my own terms

Runtime owner:

- `FirstHourDirector` completes `quest_arrival`
- `WorldReadabilityDirector` reinforces safe-pocket and landmark readability

Required read:

- one strong return silhouette
- one nearby readable shelf
- zero demand for immediate deep commitment

### Phase 1 - First Real Material

Player truth:

- the first meaningful material is not in free water
- value improves slightly deeper and slightly farther from the obvious shelf

Runtime owner:

- `QuestManager` owns `quest_copper_sample`
- `FirstHourDirector` reinforces the push only after orientation is real

Required read:

- starter resource zone must feel readable, not flat
- first useful material must feel earned, not gifted by timer

### Phase 2 - First Return / Human Foothold

Player truth:

- the outpost is not just decoration
- returning with something matters
- preparation changes the next trip

Runtime owner:

- `FirstHourDirector` recognizes post-resource return beats
- zone guidance should treat fabrication as a relief space, not a hard funnel

Required read:

- fabrication zone reads as control, reset, and decision
- no forced lock-in

### Phase 3 - First Route Truth

Player truth:

- the world is not only resources
- human traces point to where to go next
- lore is route value, not archive clutter

Runtime owner:

- `NarrativeDiscovery`
- `AudioLogSystem`
- runtime lore recovery only as fail-safe, not as authored substitute

Required read:

- first relay/log should sit on a believable return route
- the player should feel they found a lead, not a collectible

### Phase 4 - First Deeper Commitment

Player truth:

- the next meaningful thing is deeper
- depth is a route problem, not only a stat gate
- memory of silhouette and exit matters

Runtime owner:

- `quest_first_breath`
- `WorldZoneDirector` + `BiomeMatrixDirector` + `WorldReadabilityDirector`

Required read:

- one clearer branch toward deeper water
- at least one reorientation pocket

### Phase 5 - Module / Ruin Pull

Player truth:

- scraps stop being enough
- ruins and modules are now the real trail

Runtime owner:

- `FirstHourDirector`
- `ScanEvents`
- `NarrativeEvents`

Required read:

- “scan ruins/modules” must come after the player has enough grounding to care
- not as a random timer bark into empty context

### Phase 6 - Macro Mystery

Player truth:

- there is a larger system behind the local route
- I am now ready to care about it

Runtime owner:

- `AtlasSignalSystem`
- `AtlasSignalDecoder`

Required read:

- Atlas-6 remains a later pull
- it should not replace the local breadcrumb spine

## Implementation Rule

Do not add a second competing owner for explicit player goals.

- `QuestManager` stays the owner of explicit goals
- `FirstHourDirector` stays the owner of pacing and soft orchestration
- `MissionManager` must not be expanded into another early-route truth source

## Runtime Work Applied In This Pass

This pass adds contextual early guidance to `FirstHourDirector`:

- zone-aware resource nudges after real orientation
- fabrication-return nudges after real resource acquisition
- lore/service relay nudges after the first meaningful return
- authored `EmergencyServiceRelay` nodes now serve as the local breadcrumb chain: cache + lore + next-route handoff
- `EmergencyServiceRelayDirector` now owns the intro relay chain while Atlas is still only stage-1 background anomaly
- `FirstHourDirector` now checks the relay chain before generic zone hints, so the first 40 minutes can be driven by service-route breadcrumbs instead of timer-only copy
- deeper-route nudges tied to zone context instead of only elapsed time
- module/ruin nudges gated behind the player reaching the deeper route phase
- director-side random missions / rare discoveries are now held back until the early spine reaches a real milestone, so side noise does not override onboarding truth
- `WorldReadabilityDirector` now has a runtime fail-safe bootstrap for scenes where the authored component is missing, because live scene inspection showed the readability layer was absent from `02_HECTON_WORLD`
- the same runtime/bootstrap recovery path now ensures `EmergencyServiceRelayDirector` exists under `[MANAGERS]`, so the relay chain does not disappear when scene authoring is incomplete

The intent is simple:

- fewer generic timer barks
- more messages that match where the player actually is
- a real local breadcrumb chain before Atlas becomes an identified mystery

## Additional Hardening

- `DepthZoneDirector` now suppresses depth-band HUD text until the player has actually cleared `Orientation`, and it now respects a cooldown so crossing a depth boundary cannot spam the HUD.
- `WorldReadabilityRuntimeBootstrap` now prefers the existing `[MANAGERS]` root when recovering a missing readability owner at runtime.
- `WorldReadabilityDirector` now observes biome/zone context silently before `Orientation` and only starts publishing readability guidance after the first-hour spine has actually handed control to readable exploration. This prevents the runtime readability fail-safe from becoming a second onboarding voice.
- `AtlasSignalSystem` no longer fires the first `OnSignalDetected` beat during the opening route just because the starting position sits inside the raw mathematical signal radius. The first detection now waits for `FirstHourMilestone.FirstModule`.
- `AtlasSignalSystem` no longer behaves like a constant distance meter to the core. Atlas now manifests as a late depth-driven reveal chain: first rhythm, then pattern, then content fragments, then stable carrier. The system remembers the maximum unlocked reveal stage in saves, so these beats behave like discoveries instead of repeating noise.
- `AtlasSignalSystem` no longer uses a stage-1 HUD notification. The first rhythmic contact is now a world/anomaly beat only; readable HUD copy starts later, once Atlas has progressed to formal unstable contact.
- `AtlasSignalDecoder` no longer bypasses that gate by advancing Atlas phases directly from passive strength or rare-discovery pulses before `FirstHourMilestone.FirstModule`.
- `AtlasSignalDecoder` now rehydrates its non-terminal decode phase from the live Atlas reveal state after load/runtime bootstrap, so PDA/decoder copy does not fall back to phase-zero lies after a save-load handoff.
- `AtlasSignalDecoder` no longer owns explicit quest activation/completion. It only advances Atlas decode state and emits Atlas/narrative events. `QuestManager` remains the sole owner of Atlas quest state through `OnSignalDecoded` plus curated discovery triggers; the old raw `OnSignalDetected` quest path is no longer part of the Atlas onboarding spine.
- `AtlasSignalDecoder` and `PDAAtlasSignalTab` no longer identify the anomaly as `Atlas-6` at stage 2. Phase 2 now stays at “unstable pattern / emotional imprint”; identity only hardens at stage 3, after the player has already committed deeper.
- `quest_atlas_signal_detected` no longer activates on the first formal contact event. It now waits for the stage-3 identity discovery (`atlas6_signal_identified`), so explicit goaling starts when Atlas actually becomes an identified late-game anomaly instead of a vague deep-water pattern.
- `PDAAtlasSignalTab` no longer bypasses the same gate by reading raw Atlas strength, phase, and direction directly from runtime singletons during the opening route. The tab now waits for both the first-hour milestone gate and formal Atlas contact (`revealStage >= 2`); stage 1 remains background anomaly only, with no stable PDA telemetry.
- `PDAAtlasSignalTab` no longer treats the raw `OnSignalDetected` event as player-facing proof of contact. Its visible state now derives from readable Atlas stages instead of the first internal detection pulse.
- `FirstHourDirector` no longer invents its own timed Atlas bark text. `FirstAnxiety` and `HumCloser` now complete only from Atlas reveal-stage progression, so the first-hour spine observes Atlas instead of narrating it.
- `HectonNarrativeDirector` no longer turns director-side rare-discovery requests into early Atlas pulses before the module-route phase.
- `HectonNarrativeDirector` now rebroadcasts rare-discovery Atlas pulses only after stage-3 identity contact exists, so director AI cannot inflate ghost-stage or stage-2 pattern Atlas into a fake second reveal path.
- `HectonMusicDirector` no longer fires discovery stingers from director-side rare-discovery requests before `FirstHourMilestone.FirstCraft`, so audio pacing stops hinting at side-content before the player has basic footing.
- `Atlas6DirectiveSystem` no longer upgrades the player to Atlas-contact status from arbitrary `atlas6_*` discoveries or the raw `OnSignalDetected` event. It now waits for curated identity-grade Atlas discoveries (`atlas6_signal_identified` and later core-facing discoveries), which keeps Atlas-owned faction logic behind stage-3 recognition instead of stage-2 pattern contact.
- `ScannerTool` no longer barks like a quest compass the moment bearing lock appears. Stage-3 scanner copy now frames Atlas as a held return/drift signal instead of an explicit “signal detected, go deeper” order.
- `FirstHourDirector` now advances the early quest spine correctly when a loaded save already contains the first core material in inventory; the save/load path no longer strands the player between `quest_copper_sample` and `quest_first_breath`.
- `HectonDiscoveryManager` no longer emits naked release logs for biome-discovery events.

Why:

- the onboarding spine was still vulnerable to secondary notification owners crowding the first 20-30 minutes
- the authored scene truth is still missing `WorldReadabilityDirector`
- the Atlas macro-mystery must stay later than the local breadcrumb route
- release logging from progression-adjacent systems should stay under development guards

## Verification Requirement

This document does not claim success by prose.

Required runtime verification later:

1. New game through real bootstrap route
2. Observe first 20-30 minutes without debug teleport
3. Confirm guidance sequence is:
   - sparse
   - readable
   - not contradictory
   - not spammy
4. Confirm first lore hint appears only after a believable return or route commitment
5. Confirm `Atlas-6` still lands later than local route ownership

Without live play proof, status remains `PENDING VERIFICATION`.
## Atlas Late Manifestation Contract

- `Reveal Stage 1`: only a strange deep rhythm. No formal detection. No quest activation. No bearing.
- `Reveal Stage 2`: formal signal detection. Scanner may acknowledge signal strength, but still no directional lock.
- `Reveal Stage 3`: content fragments and usable bearing. This is the first stage where Atlas can start acting like a navigable anomaly instead of background dread.
- `Reveal Stage 4`: stable carrier / late-game lock on source.

- `AtlasSignalSystem.IsDetected` is stage-aware now. Stage 1 no longer counts as a real contact.
- `AtlasSignalDecoder` no longer activates `quest_atlas_signal_detected` on the first rhythm beat. Quest ownership starts at stage 2.
- `ScannerTool` exposes Atlas strength at stage 2, but bearing only at stage 3.
- `PDAAtlasSignalTab` can show faint telemetry once a reveal stage exists, but directional telemetry stays withheld until stage 3.
- Save migration/runtime load treat legacy `atlasSignalDetected` saves as at least `Reveal Stage 2`, so old formal detections do not degrade into stage-1 noise after the new staging pass.
- `FirstHourDirector` no longer carries timer fallbacks for `FirstAnxiety` / `HumCloser` at all. Both beats now mirror Atlas reveal stages directly, and `IsFirstHourComplete` only closes when `HumCloser` is genuinely reached through Atlas progression.
