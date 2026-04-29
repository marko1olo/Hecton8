# HECTON-8 SAVE PARTICIPANT LEDGER

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: source-backed inventory of current `ISaveable` participants and observed priority bands
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Purpose

The save/load runtime truth page established the pipeline.
What it did not yet provide was a broad participant ledger showing who currently uses that pipeline.

This file closes that gap.

It does not prove that every participant restores perfectly in live runtime.
It proves that the participant surface exists in current source and shows its observed priority layout.

## Proof Boundary

Primary evidence:

- `rg` source scan over `Assets/_Project/Scripts` for `ISaveable`
- direct line hits for `SavePriority`, `LoadPriority`, `PopulateSaveData(...)`, `LoadFromSaveData(...)`

This file is not:

- a full binary schema spec
- a corruption-recovery test report
- a live save/load integration proof

## High-Level Reality

Current save-participant surface is broad.
It is not limited to player stats and world state.

Confirmed participation spans:

- narrative
- audio log
- quest/progression
- PDA knowledge/markers
- player survival/inventory/health
- tools and upgrades
- construction/world state
- ecosystem/resource scarcity
- camera/VFX and some runtime presentation state
- modding/world persistence

This is a large real save graph, not a toy one.

## Observed Priority Bands

### Band A: Very Early Core / Narrative / System State (`5-14`)

| Owner | Save | Load | Evidence |
|---|---:|---:|---|
| `HectonNarrativeDirector` | `5` | `5` | `Assets/_Project/Scripts/HectonNarrativeDirector.cs:57-78` |
| `LODSystemManager` | `5` | `5` | `Assets/_Project/Scripts/World/LODSystemManager.cs:373-392` |
| `AudioLogSystem` | `6` | `6` | `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs:75-323` |
| `RunModifierController` | `6` | `6` | `Assets/_Project/Scripts/Meta/RunModifierController.cs:98-113` |
| `DynamicResolutionScaler` | `6` | `6` | `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs:290-308` |
| `LoreDatabaseManager` | `7` | `7` | `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:239-520` |
| `QuestManager` | `7` | `7` | `Assets/_Project/Scripts/Quest/QuestManager.cs:52-341` |
| `AtlasSignalSystem` | `8` | `8` | `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs:146-477` |
| `SuitUpgradeManager` | `9` | `9` | `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:77-464` |
| `HectonSurvivalSystem` | `10` | `10` | `Assets/_Project/Scripts/HectonSurvivalSystem.cs:1549-1588` |
| `Atlas6DirectiveSystem` | `11` | `11` | `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs:294-586` |
| `CorporateOrderSystem` | `12` | `12` | `Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs:79-292` |
| `FirstHourDirector` | `13` | `13` | `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:185-560` |
| `EndingSystem` | `14` | `14` | `Assets/_Project/Scripts/Gameplay/EndingSystem.cs:126-394` |

Current interpretation:

- early bands skew heavily toward narrative/state-director systems
- this is plausible if those systems must restore before later dependent surfaces

### Band B: Player / Inventory / Discovery / Tool Runtime (`20-21`)

| Owner | Save | Load | Evidence |
|---|---:|---:|---|
| `HectonDiscoveryManager` | `20` | `20` | `Assets/_Project/Scripts/HectonDiscoveryManager.cs:69-202` |
| `PlayerInventory` | `20` | `20` | `Assets/_Project/Scripts/PlayerInventory.cs:149-656` |
| `ToolDurabilitySystem` | `20` | `20` | `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:96-433` |
| `PlayerExplorationTracker` | `21` | `21` | `Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:56-224` |

Current interpretation:

- player knowledge/inventory/tool durability restores are grouped into the lower-mid band
- current code treats them as early-mid gameplay state, not ultra-late UI state

### Band C: Mid Gameplay / Logging / Scan / Beacon / Exchange (`35-37`)

| Owner | Save | Load | Evidence |
|---|---:|---:|---|
| `ScanLogSystem` | `35` | `35` | `Assets/_Project/Scripts/ScanLogSystem.cs:60-190` |
| `FieldOperationLogSystem` | `36` | `36` | `Assets/_Project/Scripts/FieldOperationLogSystem.cs:50-158` |
| `PDAExchangeSystem` | `36` | `36` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:71-324` |
| `BeaconNetworkSystem` | `37` | `37` | `Assets/_Project/Scripts/BeaconNetworkSystem.cs:52-198` |

Current interpretation:

- logging/exchange/beacon systems cluster together in the mid band

### Band D: World Pressure / Ecology / Resource / Damage State (`40-45`)

| Owner | Save | Load | Evidence |
|---|---:|---:|---|
| `FaunaGeneticsManager` | `40` | `40` | `Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs:31-108` |
| `ResourceScarcityDirector` | `40` | `40` | `Assets/_Project/Scripts/Economy/ResourceScarcityDirector.cs:128-343` |
| `VoxelDeltaProcessor` | `40` | `30` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:82-473` |
| `EnvironmentalStrainManager` | `41` | `41` | `Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs:51-366` |
| `EcosystemHealthDirector` | `42` | `42` | `Assets/_Project/Scripts/Ecosystem/EcosystemHealthDirector.cs:36-148` |
| `SargassumGlobalDragManager` | `45` | `45` | `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:653-2188` |

Current interpretation:

- this band carries world-pressure and ecological persistence
- `VoxelDeltaProcessor` is notable because its load priority is lower than its save priority

### Band E: World Registries / Simulation Registries (`50-60`)

| Owner | Save | Load | Evidence |
|---|---:|---:|---|
| `WorldStateManager` | `50` | `50` | `Assets/_Project/Scripts/WorldStateManager.cs:89-261` |
| `WorldProceduralStateRegistry` | `55` | `55` | `Assets/_Project/Scripts/WorldProceduralStateRegistry.cs:48-244` |
| `FaunaDirector` | `56` | `56` | `Assets/_Project/Scripts/FaunaDirector.cs:565-2430` |
| `ModWorldPersistenceManager` | `56` | `56` | `Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs:38-191` |
| `SeamRegistry` | `56` | `56` | `Assets/_Project/Scripts/SeamRegistry.cs:35-243` |
| `PlayerExpressionManager` | `60` | `60` | `Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs:98-381` |

Current interpretation:

- mid-late world registries and mod-world persistence live here
- this is where the save graph starts leaning toward broad world reconstruction rather than raw player state

### Band F: Late Construction / Camera / High-Level Restoration (`75-100`)

| Owner | Save | Load | Evidence |
|---|---:|---:|---|
| `CameraJuiceSystem` | `75` | `75` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:524-541` |
| `ConstructionManager` | `90` | `90` | `Assets/_Project/Scripts/ConstructionManager.cs:375-500` |
| `HectonPlayerHealth` | `100` | `100` | `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs:364-379` |

Current interpretation:

- `ConstructionManager` is intentionally late
- `HectonPlayerHealth` being at `100` is notable and deserves future dependency scrutiny if health must restore before some other player systems

### Band G: Very Late Knowledge / Advisory / UI-Adjacent Persistence (`205-210`)

| Owner | Save | Load | Evidence |
|---|---:|---:|---|
| `PDALogbookManager` | `205` | `205` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:93-248` |
| `PDAContextualAdvisorySystem` | `206` | `206` | `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs:87-187` |
| `PlayerAchievementRegistry` | `207` | `207` | `Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:78-176` |
| `ProceduralLoreDirector` | `208` | `208` | `Assets/_Project/Scripts/Narrative/ProceduralLoreDirector.cs:67-146` |
| `PDAMarkerRegistry` | `210` | `210` | `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs:91-301` |

Current interpretation:

- this band is late, knowledge-heavy, and UI-adjacent
- that generally matches the intended “late restoration” shape better than older docs implied

## Notable Structural Signals

### 1. Save Graph Is Large

This is not a tiny save surface.
Current source clearly shows a wide persistence graph.

### 2. Narrative Is Early

Many narrative/directive systems restore very early.
That is an explicit architectural choice, not an accident of one file.

### 3. Knowledge/UI-Adjacent Systems Are Late

PDA/logbook/marker/advisory/achievement/lore-context layers cluster around the `205+` band.
That is internally coherent.

### 4. Some Priorities Deserve Future Scrutiny

The most obvious “deserves a future dependency audit” cases in this pass:

- `VoxelDeltaProcessor` with `SavePriority 40` and `LoadPriority 30`
- `HectonPlayerHealth` at `100`
- large cluster around `56`

These are not declared bugs here.
They are dependency-audit candidates.

## What Looks Good

- save-participant surface is broad and real
- priority bands are not random noise; recognizable clustering exists
- narrative, player, world, construction, and knowledge layers all participate explicitly
- the docset now has both pipeline truth and participant truth

## What Looks Merely Acceptable

- source proves the participant graph exists, but not that the ordering is flawless under live restore
- some priority clusters are dense enough that hidden dependency edges are still plausible

## What Looks Weak

- there is still no live save/load replay proving these participants restore cleanly together
- participant breadth increases integration risk
- some priorities likely need dedicated dependency verification, not only documentation

## Failure Modes To Watch

- same-band systems can still hide ordering dependencies
- late knowledge/UI systems can reference state that restored incorrectly earlier
- broad world registries can restore technically “successfully” but still drift semantically

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves visibility into the size and ordering shape of the save graph. |

## Verdict

Current first-party save surface is large, layered, and materially more complex than a few previously referenced systems.

The active docset now has:

- pipeline truth
- event truth
- artifact truth
- participant ledger

That is a much stronger foundation for future real runtime validation.

STATUS: PENDING VERIFICATION
