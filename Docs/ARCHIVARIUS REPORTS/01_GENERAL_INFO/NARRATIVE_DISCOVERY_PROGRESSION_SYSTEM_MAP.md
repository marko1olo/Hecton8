# HECTON-8 NARRATIVE / DISCOVERY / PROGRESSION SYSTEM MAP

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: detailed source-backed map for narrative, discovery, directive, lore, scan-log, PDA knowledge, and progression runtime ownership
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Persistent_Object_Registry.txt`, `DATA_Save_Persistence_Binary_Delta_Checksum.txt`, `PROG_Quest_State_Graph_Logic.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`

2026-05-01 trust note:

- Read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` and `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md` before any older counter, root-path, or build-artifact claim. Then read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` as historical/domain context before using this map as current project truth.
- This file maps ownership and event/save surfaces; it does not prove story pacing, quest progression correctness, or save/load recovery in Play Mode.
- Current event topology remains mixed: queue-backed lanes exist, but direct/static buses and managed mod-bus recursion risks still need runtime validation.

2026-05-01 lore lookup recheck:

- `LoreDatabaseManager` still has 50 fixed industrial lore seeds and uses packed `NativeArray<uint>` unlock words plus one `long` save word.
- Static hardcoded lore-hash scan found `NO_DUPLICATE_LORE_HASH_CONSTANTS` for the current 50 seeds.
- Source hardening was applied to `BuildLookupIfNeeded()`: lookup completion no longer depends on `Dictionary.Count == s_records.Length`, so a future FNV duplicate cannot force repeated lookup rebuilds on PDA/UI/lore reads.
- Collision handling remains a defect signal, not an auto-repair system. In Editor/Development it logs one duplicate-hash error; runtime proof is still absent.

## Purpose

The active docset already had fragments of this domain spread across:

- `GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md`
- `2026-04-30_SAVE_PARTICIPANT_LEDGER.md`
- `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`
- PDA- and UI-focused docs

What it still lacked was one dedicated authority page for the whole knowledge/progression spine:

- narrative discoveries
- lore bank and audio logs
- Atlas signal and directive chain
- early-game and ending spine
- scan log / field log / PDA logbook
- advisory and achievement systems

This file is that map.

## Proof Boundary

This map is based on current first-party source under `Assets/_Project/Scripts`.

It proves:

- current class-level ownership
- current save/load priority shape
- current event-listener topology
- current authored bootstrap root for lore-facing systems

It does not prove:

- that every arc plays correctly in live runtime
- that authored scene instances exist in every required context
- that all dependencies restore cleanly after save/load
- that narrative pacing is good in play, only that the ownership graph exists

## Top-Level Ownership Map

| Slice | Current primary owner | Evidence | Notes |
|---|---|---|---|
| Narrative discovery state root | `HectonNarrativeDirector` | `Assets/_Project/Scripts/HectonNarrativeDirector.cs:19`, `57-58` | save-backed root for discoveries and depth-tier state |
| Biome discovery state | `HectonDiscoveryManager` | `Assets/_Project/Scripts/HectonDiscoveryManager.cs:25`, `69-70` | biome discovery registry and PDA-facing biome knowledge |
| Lore bank / acquired lore records | `LoreDatabaseManager` | `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:83` | central lore record store, save-backed |
| Audio-log discovery + playback state | `AudioLogSystem` | `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs:36`, `75-76` | discovered logs, playback, NarrativeEvents bridge |
| Atlas signal strength + detection state | `AtlasSignalSystem` | `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:39`, `146-147` | signal progression root |
| Atlas decode interpreter | `AtlasSignalDecoder` | `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs:32` | phase decode and final decode bridge |
| Atlas directive spine | `Atlas6DirectiveSystem` | `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs:239` | directive-state owner listening to narrative + Atlas events |
| Corporate narrative spine | `CorporateOrderSystem` | `Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs:32` | save-backed slow-tick corporate-order arc |
| Early-game scripted spine | `FirstHourDirector` | `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:69` | listens to quest/audio-log/narrative/scan buses |
| Ending spine | `EndingSystem` | `Assets/_Project/Scripts/Gameplay/EndingSystem.cs:87` | listens to Atlas signal events and owns ending progression state |
| Quest-state spine | `QuestManager` | `Assets/_Project/Scripts/Quest/QuestManager.cs:17` | authored quest activation/completion state |
| Scan archive state | `ScanLogSystem` | `Assets/_Project/Scripts/ScanLogSystem.cs:11`, `50-51` | scan-entry persistence and unlock state |
| Field incident archive | `FieldOperationLogSystem` | `Assets/_Project/Scripts/FieldOperationLogSystem.cs:11`, `41-42` | field operation narrative/telemetry log |
| PDA journal archive | `PDALogbookManager` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:55`, `93-94` | late player journal persistence |
| PDA exchange knowledge/state | `PDAExchangeSystem` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:15`, `59-60` | exchange-state and recent transaction knowledge |
| PDA marker persistence | `PDAMarkerRegistry` | `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs:57`, `91-92` | marker knowledge and world guidance registry |
| Late contextual advisory state | `PDAContextualAdvisorySystem` | `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs:18` | save-backed advisory/prompt domain |
| Internal achievement state | `PlayerAchievementRegistry` | `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:18`, `73-78` | non-platform achievement progression |
| Frontier lore placement runtime | `ProceduralLoreDirector` | `Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs:16`, `67-68` | procedural lore drop seeding and persistence |
| Authored lore-system root/validator | `HectonLoreSystemsRoot` | `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:20`, `55-76` | scene-authored validation root, not the runtime owner of all logic |

## 1. Narrative Root Is Not The Whole Domain

### `HectonNarrativeDirector` Is The Discovery-State Spine

Current source makes `HectonNarrativeDirector` the save-backed root for:

- discovered narrative IDs
- discovery hash lookup
- active narrative POIs
- current depth tier
- narrative bus listening

Evidence:

- class declaration: `Assets/_Project/Scripts/HectonNarrativeDirector.cs:19`
- save/load priorities: `Assets/_Project/Scripts/HectonNarrativeDirector.cs:57-58`
- bus subscriptions: `Assets/_Project/Scripts/HectonNarrativeDirector.cs:108-126`

Current conclusion:

- this is the primary discovery-state owner
- it is not the whole lore/progression system
- it is one root inside a broader knowledge graph

### `HectonDiscoveryManager` Is Separate

`HectonDiscoveryManager` owns discovered biomes, last discovered biome ID, and biome-facing progression signals.

Evidence:

- class declaration: `Assets/_Project/Scripts/HectonDiscoveryManager.cs:25`
- save/load priorities: `Assets/_Project/Scripts/HectonDiscoveryManager.cs:69-70`
- biome event and knowledge role: `Assets/_Project/Scripts/HectonDiscoveryManager.cs:94-139`

Current conclusion:

- biome discovery is not folded into `HectonNarrativeDirector`
- the project already separates narrative discovery from biome discovery

## 2. Lore Bank And Audio Log Are Separate Owners

### `LoreDatabaseManager` Owns The Lore Bank

`LoreDatabaseManager` is the runtime-resident lore bank keyed by stable hashes.

Evidence:

- lore acquisition event type declared in same file: `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:31`
- manager declaration: `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:83`
- packed unlock storage and hash lookup guard: `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:218-225`, `700-730`

This makes it the content-state bank, not just a utility helper.

Current hardening note:

- the lookup table is now built once per instance using an explicit `_recordLookupBuilt` sentinel
- current authored hashes have no duplicate constants by static scan
- if a duplicate is introduced later, the system logs it once in Editor/Development and preserves previous last-wins dictionary behavior
- this does not prove that every scene has a correctly wired lore manager instance

### `AudioLogSystem` Owns Discovery + Playback Of Audio Logs

`AudioLogSystem` explicitly says it:

- stores discovered logs
- manages playback through `SpatialAudioManager`
- publishes events for PDA archive and subtitles
- bridges discovery into `NarrativeEvents`

Evidence:

- class declaration: `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs:36`
- priorities: `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs:75-76`
- comments describing the role: `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs:2-18`

Current conclusion:

- lore bank and audio-log runtime are adjacent but distinct
- `LoreDatabaseManager` is the record bank
- `AudioLogSystem` is the discovered-log and playback-state runtime

## 3. Atlas Chain Is Its Own Progression Stack

### `AtlasSignalSystem` Owns Signal-State Progression

`AtlasSignalSystem` tracks:

- signal strength
- detection
- reveal stages
- discovery synchronization into `NarrativeEvents`

Evidence:

- class declaration: `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:39`
- priorities: `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:146-147`
- discovery sync path: `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:416-428`

### `AtlasSignalDecoder` Owns Decode Interpretation

`AtlasSignalDecoder` listens to `AtlasSignalEvents` and manages decode phases and final decode messaging.

Evidence:

- class declaration: `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs:32`
- event registration: `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs:118-126`
- it also raises final narrative discovery on full decode: `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs:291-292`

### `Atlas6DirectiveSystem` Owns Directive State

`Atlas6DirectiveSystem` sits after raw signal and decode state.
It listens to narrative and Atlas events and owns a later directive spine.

Evidence:

- class declaration: `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs:239`
- from class signature alone it is both `INarrativeEventListener` and `IAtlas6EventListener`

Current conclusion:

- Atlas is not one system
- it is a chain:
  - signal state
  - decoder
  - directive state

## 4. Scripted Arc Spine Is Split, Not Unified

### `CorporateOrderSystem`

This is a separate save-backed slow-tick system for corporate-order progression.

Evidence:

- class declaration: `Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs:32`

### `FirstHourDirector`

This is the clearest cross-bus scripted progression owner in the current codebase.

It listens to:

- `QuestEvents`
- `AudioLogEvents`
- `NarrativeEvents`
- `ScanEvents`

Evidence:

- class declaration: `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:69`
- listener interfaces visible in signature

Current conclusion:

- this is a real progression orchestrator
- it is also a coupling hotspot between several knowledge systems

### `EndingSystem`

The ending spine is separate again.
It listens to Atlas progression rather than replacing earlier directors.

Evidence:

- ending events block starts in file: `Assets/_Project/Scripts/Gameplay/EndingSystem.cs:56`
- class declaration: `Assets/_Project/Scripts/Gameplay/EndingSystem.cs:87`

Current conclusion:

- early-game scripted arc and ending arc already have different owners
- this domain is phased, not centralized

## 5. Quest Spine Is Adjacent, Not Equivalent

`QuestManager` is still the authored quest-state root.

It belongs in this map because:

- it is a progression owner
- `FirstHourDirector` depends on quest events
- many other narrative/progression systems orbit quest state

But it is not the same thing as:

- `HectonNarrativeDirector`
- `Atlas6DirectiveSystem`
- `CorporateOrderSystem`
- `EndingSystem`

Current conclusion:

- â€œquest systemâ€ is only one branch of progression
- it is not the whole knowledge/narrative spine

## 6. Archive / Knowledge Surfaces

### `ScanLogSystem`

`ScanLogSystem` is the persisted archive of scan knowledge.

Evidence:

- class declaration: `Assets/_Project/Scripts/ScanLogSystem.cs:11`
- priorities: `Assets/_Project/Scripts/ScanLogSystem.cs:50-51`
- event listener role: same class signature

### `FieldOperationLogSystem`

This is a separate persisted field-incident log.

Evidence:

- class declaration: `Assets/_Project/Scripts/FieldOperationLogSystem.cs:11`
- priorities: `Assets/_Project/Scripts/FieldOperationLogSystem.cs:41-42`

Honest caveat:

- this file still contains `FixedCharBuffer.ToString()` entry helpers in static record methods
- this map records ownership, not hot-path cleanliness

### `PDALogbookManager`

This is a late journal/archive owner for player milestones and discoveries.

Evidence:

- class declaration: `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:55`
- priorities: `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:93-94`
- summary comment: `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:51-54`

### `PDAExchangeSystem`

This owns exchange-state knowledge and transaction history, not just UI buttons.

Evidence:

- class declaration: `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:15`
- priorities: `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:59-60`

### `PDAMarkerRegistry`

This is the persistence owner for markers and marker-facing knowledge.

Evidence:

- class declaration: `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs:57`

Current conclusion:

- archive and knowledge surfaces are not one PDA monolith
- they are distributed across scan, field logs, journal, exchange, and marker registries

## 7. Late Advisory / Meta Progression Layer

### `PDAContextualAdvisorySystem`

This is a late save-backed advisory layer.

Evidence:

- class declaration: `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs:18`

### `PlayerAchievementRegistry`

This is the internal achievement/meta progression owner.

Evidence:

- class declaration: `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:18`
- priorities: `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:73-78`
- it also ticks and listens to progression-related owner state

### `ProceduralLoreDirector`

This is the frontier lore-seeding system that keeps world exploration feeding lore pickups.

Evidence:

- class declaration: `Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs:16`
- priorities: `Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs:67-68`

Current conclusion:

- the late progression layer is not only achievements
- it also includes advisory context and procedural lore replenishment

## 8. Authored Lore Root Versus Runtime Owners

`HectonLoreSystemsRoot` is important, but it should not be misread.

What it is:

- a scene-authored validation/setup root for lore-related systems
- a content-health root checking expected systems and placed world surfaces

Evidence:

- class declaration: `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:20`
- intended purpose and scene note: `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:2-8`
- system validation and expected count: `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:22-39`, `55-76`

What it is not:

- the single runtime owner of all lore/progression logic
- a replacement for the runtime system graph above

Current conclusion:

- this file is the authored bootstrap/validation root
- the runtime logic remains distributed across many systems

## 9. Event Topology Inside This Domain

This domain is not on one event model.

Current known buses inside the narrative/progression knowledge stack:

- `NarrativeEvents`
- `ScanEvents`
- `AudioLogEvents`
- `QuestEvents`
- `AtlasSignalEvents`
- `Atlas6Events`

Known important listener examples:

- `HectonNarrativeDirector` -> `INarrativeEventListener`
- `AudioLogSystem` -> raises `AudioLogEvents` and `NarrativeEvents`
- `AtlasSignalDecoder` -> `IAtlasSignalEventListener`
- `FirstHourDirector` -> `IQuestEventListener`, `IAudioLogEventListener`, `INarrativeEventListener`, `IScanEventListener`
- `EndingSystem` -> `IAtlasSignalEventListener`
- `ScanLogSystem` -> `IScanEventListener`

Current conclusion:

- this domain is highly event-driven
- `FirstHourDirector` is one of the clearest cross-bus orchestration hotspots in the project

## 10. Save-Band Shape Inside This Domain

The current persistence shape is layered, not random:

### Early narrative/core bands

- `HectonNarrativeDirector` -> `5`
- `AudioLogSystem` -> `6`
- `LoreDatabaseManager` -> `7`
- `QuestManager` -> `7`
- `AtlasSignalSystem` -> `8`
- `Atlas6DirectiveSystem` -> `11`
- `CorporateOrderSystem` -> `12`
- `FirstHourDirector` -> `13`
- `EndingSystem` -> `14`

### Mid knowledge bands

- `HectonDiscoveryManager` -> `20`
- `ScanLogSystem` -> `35`
- `FieldOperationLogSystem` -> `36`
- `PDAExchangeSystem` -> `36`

### Late knowledge/UI-adjacent bands

- `PDALogbookManager` -> `205`
- `PDAContextualAdvisorySystem` -> `206`
- `PlayerAchievementRegistry` -> `207`
- `ProceduralLoreDirector` -> `208`
- `PDAMarkerRegistry` -> `210`

Current conclusion:

- early restore is dominated by state-director systems
- late restore is dominated by PDA/archive/advisory/meta surfaces
- this is a coherent shape, not arbitrary disorder

## 11. Current Truths That Old Docs Can Distort

| Flattened claim | Current source-backed truth |
|---|---|
| Narrative is one manager | False. It is distributed across narrative root, lore bank, audio logs, Atlas chain, scripted arc directors, and archive systems. |
| Quest manager owns progression | False. `QuestManager` owns one progression branch, not the whole knowledge spine. |
| PDA knowledge is just UI | False. Several save-backed knowledge owners exist beneath PDA-facing UI surfaces. |
| Lore root means runtime centralization | False. `HectonLoreSystemsRoot` is an authored validation/setup root, not the single runtime owner. |
| Audio logs are just content pickups | False. They are a real save-backed subsystem that bridges into narrative and subtitles. |

## 12. Recommended Read Order

If the task is narrative/progression-facing:

1. `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md`
2. `2026-04-30_SAVE_PARTICIPANT_LEDGER.md`
3. `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`
4. `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`

If the task is Atlas-facing:

1. `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md`
2. `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`
3. `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`

If the task is PDA-knowledge-facing:

1. `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md`
2. `GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md`
3. `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves ownership truth for one of the most distributed domains in the project. |

## Verdict

The narrative/discovery/progression domain is not one subsystem.

It is a layered graph with separate owners for:

- discovery state
- biome knowledge
- lore bank
- audio logs
- Atlas signal and decode
- directive progression
- early-game and ending arcs
- scan/archive/journal state
- advisory and achievements
- procedural lore replenishment

That distribution is current source truth.
Any future documentation that collapses it into one or two files will under-report the real architecture.

STATUS: PENDING VERIFICATION
