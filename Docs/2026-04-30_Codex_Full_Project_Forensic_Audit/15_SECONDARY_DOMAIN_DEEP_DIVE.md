# 15 Secondary Domain Deep Dive

Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

Purpose:
- Audit the second-line runtime domains that sit behind the obvious giants (`World`, `Gameplay`, `UI`) and decide whether they are real gameplay systems, partial infrastructure, or paper-heavy shells.
- Separate implementation truth from architecture quality. These are not the same thing in HECTON-8.

## 1. Domain gravity snapshot

Static code snapshot:

| Domain | Files | Lines | `Instance` hits | `DontDestroyOnLoad` hits | `GlobalRegistry.` hits | Native/Burst surface | Direct `Action` events |
|---|---:|---:|---:|---:|---:|---:|---:|
| Input | 3 | 2,849 | 14 | 6 | 0 | none | 33 |
| Power | 5 | 2,501 | 0 | 0 | 3 | very high | 0 |
| Narrative | 7 | 1,855 | 26 | 0 | 6 | light | 0 |
| Economy | 8 | 1,505 | 10 | 0 | 9 | none | 0 |
| Ecosystem | 9 | 1,089 | 16 | 0 | 7 | none | 0 |
| PDA | 6 | 1,382 | 39 | 0 | 9 | none | 3 |
| Quest | 7 | 2,940 | 9 | 0 | 12 | medium-high | 0 |
| AtlasSignal | 4 | 1,326 | 42 | 0 | 10 | light queue use | 0 |
| Progression | 3 | 752 | 7 | 0 | 6 | none | 2 |
| ModdingAPI | 16 | 3,490 | 25 | 2 | 0 | tiny | 4 |

Interpretation:
- These are not decorative folders. They contain real runtime ownership and real save/load involvement.
- The same project-wide disease repeats here: implementation depth is often real, but architecture purity is inconsistent.

## 2. Input

Evidence:
- `Assets/_Project/Scripts/Input/InputManager.cs:34` declares a large owner with its own runtime bootstrap language.
- `InputManager.Instance` is still lazy-created and may self-spawn a `[InputManager]` GameObject (`InputManager.cs:107-132`).
- `DontDestroyOnLoad` appears in the input stack (`InputManager.cs:132`, `InputManager.cs:356`, `RebindingManager.cs:55`, `RebindingManager.cs:86`, `UserOptionsPersistence.cs:41`, `UserOptionsPersistence.cs:62`).
- Input uses many direct C# events rather than queue-backed project buses (`InputManager.cs:194-227`).

What is genuinely good:
- This is not a fake shell. It is a broad input owner with device-style awareness, rebinding, runtime validation, and explicit action-map handling.
- The file is trying to honor zero-GC discipline in its own language and caching layout.

What is bad:
- It directly violates the stated architectural center of gravity. This stack is still fundamentally singleton and self-instantiating, not bootstrap-owned `GlobalRegistry` service authority.
- The event model is classic direct `Action` fan-out, not the project's stricter queue-backed event bus story.
- Because input sits at the root of gameplay and UI cadence, a legacy authority model here contaminates everything above it.

Verdict:
- Implementation reality: high.
- Architecture cleanliness: low.
- Production confidence: medium only if treated as legacy infrastructure that still needs authority cleanup.

## 3. Power

Evidence:
- `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:86` is a pure runtime graph owner, not a MonoBehaviour convenience shell.
- Native container ownership is broad and deliberate (`LogisticsNetworkGraph.cs:962-996`, `1009-1033`).
- Burst jobs exist in multiple stages (`LogisticsNetworkGraph.cs:208`, `791`, `806`, `884`).
- Explicit sync points exist (`LogisticsNetworkGraph.cs:1573`, `1604`).
- `PowerRelayNode.cs:13` bridges the graph into scene-facing `IPowerComponent`, `IPoolable`, `ISlowTickable`.

What is genuinely good:
- This is one of the most convincing "not on paper" systems in the entire project.
- It closely resembles the logistics/power mandate: graph ownership, node buffers, CSR-like traversal data, producer/consumer separation, published node states.
- The system is data-first, native-heavy, and clearly designed for determinism and scale.

What is bad:
- The existence of `.Complete()` is not automatically wrong, but it is where this subsystem becomes dangerous. Without live profiling, I cannot prove whether these sync points are in safe swap windows or whether they occasionally punch holes into frame cadence.
- The graph core is strong, but the public gameplay layer around it is still thin. There are only a few files in the folder. That means the backend maturity currently exceeds the visible gameplay ecosystem that consumes it.

Verdict:
- Implementation reality: very high.
- Mandate alignment: high.
- Player-visible maturity: medium, because backend quality is ahead of broad gameplay exploitation.

## 4. Quest

Evidence:
- `Assets/_Project/Scripts/Quest/QuestManager.cs:17` is a real runtime owner, not a placeholder.
- `QuestManager` registers both quest service and save service through the registry path (`QuestManager.cs:67-70`).
- `QuestStateManager` is a deep native owner (`QuestStateManager.cs:12`, `45-68`, `359-373`).
- Burst evaluation exists (`QuestStateManager.cs:1662`).
- Packed snapshot/save machinery is real (`QuestStateManager.cs:737-746`, `851`, `1244-1254`).

What is genuinely good:
- The quest stack is far more serious than average Unity project quest code.
- The authored `QuestData[]` shell is backed by a real bit-packed state layer, transition history, prerequisite graph, and Burst evaluation path.
- This is one of the rare places where authored-content and systems code actually meet cleanly.

What is bad:
- `QuestManager` still retains singleton identity (`QuestManager.cs:36`, `56-64`), even while being registry-aware.
- There is still a split between elegant native internals and a more conventional MonoBehaviour outer shell.
- This means the technical center is strong, but the authority boundary is still mixed.

Verdict:
- Implementation reality: very high.
- Architecture purity: medium.
- This is a real shipping-grade system core trapped inside a partially transitional runtime shell.

## 5. Narrative

Evidence:
- `LoreDatabaseManager.cs:83` is a saveable owner with `NativeArray<uint>` storage (`LoreDatabaseManager.cs:221`, `715-718`) and early save/load priority (`239-244`).
- `ProceduralLoreDirector.cs:16` is not just a data holder; it does spawn/despawn management, object pool usage, save integration, and slow-tick registration (`268-315`, `499-537`).
- `CorporateOrderSystem.cs:32` is another saveable slow-tick narrative owner with early load priority (`79-80`) and mixed `Instance` plus registry behavior (`88-105`, `127-139`).

What is genuinely good:
- The narrative stack is materially implemented. It owns unlock state, procedural placements, and corporate order progression.
- Lore is not being treated as mere text. It has persistence, procedural placement, and runtime ownership.

What is bad:
- This whole domain is still strongly singleton-coded.
- Narrative is spread across several independent owners with only partial unification.
- It feels like a set of real systems that grew independently rather than a single clean narrative runtime.

Verdict:
- Implementation reality: high.
- Structural elegance: medium-low.
- Strong enough to matter to the player, not yet clean enough to be called disciplined.

## 6. PDA and progression

Evidence:
- `PDALogbookManager.cs:55`, `PDAMarkerRegistry.cs:57`, and `PlayerExplorationTracker.cs:16` are all real save/runtime owners.
- PDA-facing systems still use direct `Action` events such as `LogbookChanged`, `MarkersChanged`, `ChunkExplored`, `AdvisoryPushed`, and `AchievementUnlocked` (`PDALogbookManager.cs:87`, `PDAMarkerRegistry.cs:85`, `PlayerExplorationTracker.cs:47`, `PDAContextualAdvisorySystem.cs:84`, `PlayerAchievementRegistry.cs:75`).
- `PDAContextualAdvisorySystem` and `PlayerAchievementRegistry` are late-load save participants with player-facing value (`206-207` priorities).

What is genuinely good:
- This is not fake flavor. The PDA/progression layer is wired into exploration, achievements, and advisory delivery.
- There is real player-facing scaffolding here for meaning, not just utility.

What is bad:
- The event model remains old-style and fragmented.
- The systems are numerous, individually real, and collectively decentralized.
- This is exactly the kind of layer that becomes "feature-rich but behaviorally inconsistent" late in production if no one reclaims authority.

Verdict:
- Implementation reality: high.
- Behavioral cohesion: medium-low.
- Player value potential: high, if unified and curated.

## 7. AtlasSignal

Evidence:
- `AtlasSignalSystem.cs:39` and `Atlas6DirectiveSystem.cs:239` are both saveable slow-tick narrative systems.
- Save/load priority is early and deliberate (`AtlasSignalSystem.cs:146-147`, `Atlas6DirectiveSystem.cs:294-295`).
- They are singleton-owned while also registering through `GlobalRegistry` (`AtlasSignalSystem.cs:155-178`, `333-345`; `Atlas6DirectiveSystem.cs:331-352`, `408-420`).
- Queue-backed event lanes exist (`AtlasSignalEvents.cs:39`, `159`; `Atlas6DirectiveSystem.cs:85`, `216`).

What is genuinely good:
- This domain is one of the better examples of a system that has a distinct identity, progression logic, and some event discipline.
- The early save priority suggests the team correctly treats it as progression-critical, not cosmetic.

What is bad:
- It still lives in the project's mixed-authority world: singleton identity, direct cross-owner lookups, and registry scheduling at the same time.
- It is more coherent than many side domains, but not sovereign.

Verdict:
- Implementation reality: high.
- Authority cleanliness: medium.
- Better than average for this project, still transitional.

## 8. Economy and ecosystem

Evidence:
- `ResourceScarcityDirector.cs:23` is saveable and slow-tick, and pulls from `QuestManager`, `GlobalRegistry.PlayerInventory`, and player runtime (`382-390`, `603-615`).
- `EcosystemHealthDirector.cs`, `FaunaGeneticsManager.cs`, and `MigrationDirector.cs` are all real owners, but strongly `Instance`-driven.

What is genuinely good:
- Both domains are real. They are connected to player inventory, quests, genetics, migration, and scarcity tuning.

What is bad:
- These domains are smaller, but they are not cleaner.
- They mostly inherit authority from other systems instead of defining clean domain boundaries of their own.

Verdict:
- Implementation reality: medium-high.
- Independence: low.
- These systems are real, but they are consumers of larger architecture rather than architecture exemplars.

## 9. ModdingAPI

Evidence:
- `ModLoader.cs:13` is a real runtime loader with boot hooks, save hooks, scene hooks, and ordered mod initialization.
- `HectonAPI.cs:21` exposes substantial surface area: events, items, crafting, recycling, construction, ecosystem, localization, UI, world, save state, mods.
- `HectonEventBus.cs:98` is a typed managed event bus with disposable subscriptions and per-handler exception isolation.
- `ModWorldPersistenceManager.cs:17` is a real save/bootstrap owner with `DontDestroyOnLoad` and pool-aware persistence support (`59-95`, `372-384`).

What is genuinely good:
- The modding layer is not fictional. It is large, explicit, and integrated with save/bootstrap/world persistence.
- It exposes enough surface to matter.

What is bad:
- This layer is architecturally foreign to the rest of the project's strict zero-GC/event-queue ideals.
- `HectonEventBus` is managed-list driven and uses `try/catch` isolation around subscriber dispatch. That is a sane modding choice, but it is a very different philosophy from the native queue discipline used in core first-party runtime systems.
- The more powerful this API becomes, the more it freezes mixed architecture decisions in place.

Verdict:
- Implementation reality: high.
- Runtime purity: intentionally relaxed.
- Strategically important, but it raises future stabilization cost.

## 10. Main conclusion

The second-line domains confirm the same project-wide truth:

- HECTON-8 is not lacking systems.
- HECTON-8 is lacking a single clean authority model across those systems.

The strongest second-line implementations are:
- `Power`
- `Quest`
- `AtlasSignal`
- `Narrative` core persistence

The most architecturally compromised second-line implementations are:
- `Input`
- `PDA/Progression`
- `ModdingAPI`

The most important practical conclusion:
- The project is already too real to diagnose as "missing features."
- The honest diagnosis is "implemented breadth with architectural era overlap."

That is a stronger project than a fake prototype.
It is also a more expensive project to stabilize.
