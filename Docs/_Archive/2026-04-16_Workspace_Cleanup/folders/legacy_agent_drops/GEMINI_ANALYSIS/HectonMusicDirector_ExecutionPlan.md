Date: 2026-04-16

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Hecton Music Director Execution Plan

Status: PENDING VERIFICATION

## 1. Audio Library Findings

Source folder: `Assets/_Project/Audio/Music for Game`

Detected `.ogg` count by family:

- `main_menu`: 2
- `prologue`: 1
- `shallow` (`melkovodie_*`): 6
- `shelf` (`shelf_*`): 10
- `abyss` (`abyss_*`): 10
- `abyss_thermal`: 3
- `cave`: 10
- `base` (`warm_base_lofi_*`, `wam_base_lofi_*`): 11
- `being_attacked`: 4
- `danger`: 2
- `ambient_deep`: 4
- `ambient_long`: 2
- `ambient_short`: 3
- `dead_reefs`: 5
- `stinger_*`: 11

Current reality: the library is already larger than the original 60-track estimate. The system must assume continued growth.

## 2. Routing Model

The biome sets tone. It does not hard-lock the playlist.

Planned exploration routing:

- `MainMenu`: 100% `main_menu`
- `Prologue`: 100% `prologue`
- `Base / interior / service approach`: 70% `base`, 20% `shallow` calm bleed, 10% `shelf` calm bleed
- `Shallow water`: 60% `shallow`, 20% `shelf`, 10% `ambient_deep`, 10% `dead_reefs`
- `Shelf / mid-depth`: 55% `shelf`, 20% `shallow`, 15% `dead_reefs`, 10% `ambient_deep`
- `Abyss`: 55% `abyss`, 15% `shelf`, 15% `ambient_deep`, 15% `cave`
- `Thermal`: 60% `abyss_thermal`, 20% `abyss`, 20% `cave`
- `Cave`: 65% `cave`, 15% `abyss`, 10% `shelf`, 10% `ambient_deep`

Combat / danger routing:

- Bed music on high threat: `being_attacked` primary, `danger` secondary
- `stinger_dangerous_*`: overlay only, not a loop bed

Stinger routing:

- `stinger_discovery_*`: overlay on discoveries / rare finds
- `stinger_being_saved_*`: overlay on rescue / relief / post-threat release
- `stinger_hallucination_*`: explicit scripted override only
- `ambient_short` and named `short30sec` clips: short-form bridge tracks, not stingers

## 3. Runtime Architecture

Owner: `HectonMusicDirector` in `Hecton8.Audio`

Contracts:

- `ITickable`: fades, ducking, wait timer, clip-end handling
- `ISlowTickable`: context sync from existing systems
- No coroutine fades
- No LINQ
- No hot-path allocations

Voice layout:

- Voice A: exploration / combat bed
- Voice B: crossfade target bed
- Voice C: stinger overlay with bed ducking

State machine:

1. `Waiting`
2. `Selecting`
3. `Playing`
4. `Crossfading`
5. `Override`

Selection rules:

- choose tension band from `HectonDirectorAI.TensionScore` or manual override
- prefer long-form beds by default
- allow short-form bridge clips by profile chance and cooldown
- prevent immediate repeat of last played clip in same mode
- fall back to opposite tension band, then fallback profile, instead of failing hard

Transition rules:

- zone/profile change: smooth crossfade
- combat latch entry: immediate crossfade to combat profile
- combat latch exit: return to resolved exploration profile after hysteresis
- interior / base: base profile wins over combat
- force override clip: wins over every auto state until cleared or finished

## 4. Integration Inputs

Existing runtime owners to consume:

- `WorldZoneDirector.ActiveRuntimeInstance`
- `BiomeMatrixDirector.ActiveRuntimeInstance`
- `AcousticZoneController.Instance`
- `HectonDirectorAI` via explicit serialized reference or same-root resolve

Public API required on music director:

- `SetManualBiomeProfile(...)`
- `ClearManualBiomeProfile()`
- `SetManualTension01(...)`
- `ClearManualTensionOverride()`
- `ForceOverrideTrack(...)`
- `ClearForcedOverride(...)`
- `PlayDangerStinger()`
- `PlayDiscoveryStinger()`
- `PlayRecoveryStinger()`
- `StopMusic(...)`

## 5. Data Layer

Needed assets/code:

- `HectonMusicClip`: clip reference + volume trim + selection weight + role
- `HectonMusicBiomeProfile`: calm/tense long beds, calm/tense short beds, stingers, pause/fade config, weighted bleed profiles

Why this structure stays viable:

- new clips can be appended without changing runtime code
- profiles can stay broad and interchangeable
- combat/stinger logic stays separate from exploration routing

## 6. Volume Strategy

The code can only provide per-entry trim. It cannot solve true loudness normalization by itself.

Required author pass after code:

1. assign every clip into a profile bucket
2. set per-entry trim for outliers
3. route director output into a dedicated music mixer group, or intentionally reuse `AmbientGroup`
4. verify that music ducking does not bury mission-critical SFX

## 7. Work Split

Agent work in this pass:

- write plan
- implement data types
- implement runtime director
- compile and check console

Developer work after this pass:

- create profile assets
- assign clips from the music folder into those profiles
- hook the director into bootstrap / manager root if scene is missing it
- verify mixer routing and loudness trims in editor and playtest

## 8. Verification Protocol

Required before calling the system done:

- no compile errors
- bed crossfade works
- stinger ducking works
- zero-GC proof from GC monitor in gameplay scene
- transition checks:
  - shallow -> shelf
  - shelf -> cave
  - underwater -> base interior
  - exploration -> combat
  - combat -> relax
  - forced override -> release

Without profiler or GCMonitor numbers, status remains PENDING VERIFICATION.
