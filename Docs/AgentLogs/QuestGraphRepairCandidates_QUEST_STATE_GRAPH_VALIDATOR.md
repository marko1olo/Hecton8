# Quest Graph Repair Candidates - QUEST_STATE_GRAPH_VALIDATOR

Date: 2026-05-15
Evidence source: `Docs/AgentLogs/QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json`
Scope: Narrative quest graph audit. No Unity asset YAML mutation performed.

## Defect Class 1: End Game Activates But Never Completes

Problem:
`quest_atlas_core_reached` is reachable by threshold-aware path analysis and was activated in the 1,000,000-sequence stress run, but it has no event-driven `Complete` trigger. The player can reach the terminal state without the graph recording end-game completion.

Candidate repair:
Add an authored completion trigger to `quest_atlas_core_reached`.

Preferred trigger:
`DepthReached >= 4800` only if design accepts the deepest Atlas Core arrival as the end-game proof.

Rejected automatic mutation:
Raw `.asset` YAML editing was rejected because quest ScriptableObject file IDs and serialized property layout must be changed through Unity AssetDatabase or manually reviewed YAML. This audit file does not alter Unity assets.

Risk:
If End Game completion requires a cutscene, boss state, or explicit Atlas interaction, `DepthReached >= 4800` is too broad. Narrative owner confirmation required before asset mutation.

## Defect Class 2: Quests Without Event-Driven Completion

Problem:
The stress run found four no-completion quests. These can become terminal active states, especially in random or out-of-order progression.

Affected quests:
- `quest_arrival`
- `quest_biome_spine`
- `quest_atlas_core_reached`
- `quest_rad_shield`

Candidate repairs:
- `quest_arrival`: complete on the same event that proves first-hour world exit, likely `DiscoveryMade:first_hour_exit_lifepod`, if this quest is intended as an arrival tutorial gate.
- `quest_biome_spine`: complete on `BiomeEntered >= 1` if entering Spine is itself the objective.
- `quest_atlas_core_reached`: complete on explicit Atlas Core final interaction, or `DepthReached >= 4800` only if depth arrival is final proof.
- `quest_rad_shield`: blocked until equipment/radiation protection event ownership is confirmed. Do not invent an event ID without runtime owner proof.

Rejected automatic mutation:
Single-use event creation was rejected. Project signal discipline forbids creating new EventIDs for isolated interactions. Reuse an existing typed quest event or route direct state through the quest owner.

## Defect Class 3: Manual / External Terminal Stalls

Problem:
238,374 of 1,000,000 event sequences ended with only manual/no-completion terminal quest state. This is not the same as "no active quest", but it is still a player progression risk because the graph waits on unproven external state.

Affected requirement classes:
- Manual trigger not auto-activated.
- Abyssal/Thermal phase context not proven by the quest graph source.

Candidate repair:
Document each external trigger owner in the quest graph export, then rerun the stress test with those owner events included as deterministic event candidates. If an owner cannot be named, the requirement is impossible from graph data and must be changed.

## Required Safe Implementation Path

1. Open the relevant QuestData assets in Unity through a temporary Editor audit/migration script.
2. Read each serialized quest before mutation and verify field names through `SerializedObject`.
3. Apply only completion trigger additions approved by the narrative owner.
4. Save assets through `AssetDatabase.SaveAssets()`.
5. Regenerate `Data/Narrative/Quest_Graph.json` using `Tools/QuestStressTest.py --export-graph`.
6. Rerun `Tools/QuestStressTest.py --sequences 1000000 --sequence-length 24`.
7. Run `Tools/QuestStressReportGate.py Docs/AgentLogs/QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json`.

## Current Status

2026-05-15 update:
The repair candidates above were applied to QuestData/validator code, then `python Tools\RunQuestValidation.py` was rerun.

Result:
- `QUEST_GATE_PASS`
- 1,000,000 sequences x 24 events
- 0 no-active softlocks
- 0 manual/no-completion terminal stalls
- 388,415 End Game completions
- 0 dead-end/no-complete findings
- 0 impossible/external requirement findings

Remaining verification:
Unity import and Play Mode are still pending because Unity and `dotnet` are unavailable in this environment.
