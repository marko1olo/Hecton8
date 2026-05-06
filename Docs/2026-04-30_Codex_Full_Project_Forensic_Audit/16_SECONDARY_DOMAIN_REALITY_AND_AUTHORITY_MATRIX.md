# 16 Secondary Domain Reality And Authority Matrix

Date: 2026-05-07
Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

Scoring note:
- Percentages below are audit estimates derived from code structure and ownership evidence.
- They are not profiler measurements and not ship forecasts.
- "Reality" means "implemented in code and materially connected to runtime."
- "Authority hygiene" means "how cleanly the system follows the intended registry/bootstrap/save/event architecture."

## 1. Matrix

| Domain | Reality % | Player-facing readiness % | Authority hygiene % | What is true | What is false |
|---|---:|---:|---:|---|---|
| Input | 90 | 75 | 25 | Fully real runtime stack with rebinding, device handling, and UI-facing event fan-out. | It is not architecturally aligned with the registry/bootstrap ideal. |
| Power | 88 | 60 | 78 | Real native graph backend with Burst, explicit buffers, and published states. | It is not yet broadly proven as a deeply exploited gameplay pillar. |
| Quest | 92 | 78 | 62 | Real authored plus native quest graph with Burst-backed state evaluation. | It is not a lightweight SO-only quest shell. |
| Narrative | 82 | 70 | 44 | Real persistence, procedural lore placement, and corporate order runtime. | It is not a single sovereign narrative runtime. |
| PDA | 84 | 74 | 32 | Real logbook, markers, exploration tracking, and HUD-facing state. | It is not event-bus-clean or centrally unified. |
| Progression | 80 | 72 | 38 | Real achievements and contextual advisory delivery. | It is not merely future roadmap material. |
| AtlasSignal | 86 | 76 | 58 | Real progression/narrative signal layer with save semantics and queue infrastructure. | It is not fully detached from singleton-era coupling. |
| Economy | 76 | 63 | 41 | Real scarcity/recycling/scrap loop pieces exist. | It is not yet a clean independent economy framework. |
| Ecosystem | 74 | 61 | 36 | Real genetics, migration, and health directors exist. | It is not yet a strongly sovereign domain boundary. |
| ModdingAPI | 83 | 55 | 29 | Real boot/load/persistence/mod surface is implemented. | It is not a zero-cost side experiment; it already affects architecture. |

## 2. Best-in-class secondary domains

### Quest

Why it scores high:
- The inner core is serious: `QuestStateManager` carries native arrays, packed snapshots, transition history, and Burst evaluation.
- The outer owner is content-aware and save-aware.

Why it does not score even higher:
- It still carries singleton identity alongside registry behavior.

### Power

Why it scores high:
- `LogisticsNetworkGraph` is one of the clearest mandate-aligned subsystems in the repo.
- The code reads like a backend meant for a real game, not a presentation trick.

Why it does not score even higher:
- The graph backend is ahead of its surrounding gameplay ecosystem.
- Sync points remain an evidence-based caution area until proven in live profiling.

### AtlasSignal

Why it scores high:
- It combines save semantics, progression semantics, and queue-backed event infrastructure better than most side domains.

Why it is still capped:
- It remains dependent on singleton-era neighbors and cross-domain direct lookups.

## 3. Highest authority debt

### Input

Evidence of debt:
- self-spawning singleton
- `DontDestroyOnLoad`
- large direct `Action` event surface
- no visible migration into the stricter registry/event-bus model

Meaning:
- This stack may work.
- It still contradicts the architectural doctrine the rest of the project claims to enforce.

### PDA and progression

Evidence of debt:
- direct `Action` event surfaces
- multiple nearby saveable owners
- strong dependency on other singleton managers

Meaning:
- High player value, low sovereignty.

### ModdingAPI

Evidence of debt:
- dedicated managed event bus (`HectonEventBus`)
- runtime persistence manager with its own lifetime assumptions
- broad API surface that hardens current architecture choices

Meaning:
- This subsystem is strategically powerful.
- It also makes future cleanup more expensive because public surface area tends to fossilize.

## 4. Auditor view

From an auditor position, the secondary-domain story is stronger than expected:

- The project does not collapse outside `World` and `UI`.
- Many side domains already have save/load contracts, runtime owners, content links, and player-facing behaviors.

But the audit also gets harsher here:

- These domains prove the project is broad enough that architecture debt is no longer local.
- Mixed authority is no longer a bug in one subsystem.
- Mixed authority is now a project property.

## 5. Player view

From a player position, this is the encouraging part:

- The project has enough real side systems to support texture, memory, goals, and fantasy.
- Lore, quests, markers, advisories, signals, recycling, scarcity, and achievements are not empty promises.

From a harsher player position:

- If these systems are not unified late, the player will feel fragmentation rather than richness.
- The likely failure mode is not "there is nothing to do."
- The likely failure mode is "too many medium-connected systems with uneven feedback quality."

## 6. Hard conclusion

The secondary domains upgrade the overall audit from:

- "large prototype with some real code"

to:

- "serious game with broad implementation depth and cross-era authority debt."

That is a better project than a paper tiger.
It is also a much harder project to finish cleanly.
