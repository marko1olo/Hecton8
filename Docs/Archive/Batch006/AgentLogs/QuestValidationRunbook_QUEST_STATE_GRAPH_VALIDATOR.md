# Quest Validation Runbook - QUEST_STATE_GRAPH_VALIDATOR

Date: 2026-05-15

## Full Evidence Chain

Run from repository root:

```powershell
python Tools\RunQuestValidation.py
```

Equivalent expanded commands:

```powershell
python Tools\QuestStressTest.py --export-graph Data\Narrative\Quest_Graph.json --sequences 1 --sequence-length 1 --json-output Docs\AgentLogs\QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR_export_probe.json
python Tools\QuestStressTest.py --sequences 1000000 --sequence-length 24 --json-output Docs\AgentLogs\QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json
python Tools\QuestStressReportGate.py Docs\AgentLogs\QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json --min-sequences 1000000
```

## Expected Current Result

The report gate is expected to pass after the 2026-05-15 remediation pass.

Latest accepted evidence:
- `python Tools\RunQuestValidation.py` returned exit code 0.
- Gate output: `QUEST_GATE_PASS`.
- Stress run: 1,000,000 sequences x 24 events.
- No-active softlocks: 0.
- Manual/no-completion terminal stalls: 0.
- End Game completed sequences: 388,415.
- Dead-end/no-complete findings: 0.
- Impossible/external requirement findings: 0.

If this command fails again, treat it as a regression against the repaired QuestData assets or validator contract. Do not weaken the gate.

## Safe Repair Loop

1. Repair QuestData through Unity AssetDatabase when Unity is available; direct YAML scalar edits require explicit review and documentation.
2. Do not create single-use EventIDs for isolated quest fixes.
3. Regenerate `Data/Narrative/Quest_Graph.json`.
4. Rerun `python Tools\RunQuestValidation.py`.
5. If the gate fails, inspect `Docs\AgentLogs\QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json`.
6. Only then update `Docs\Tasks\Status_QUEST_STATE_GRAPH_VALIDATOR.md`.

## Acceptance Criteria

- `Tools\QuestStressTest.py` compiles.
- `Tools\QuestStressReportGate.py` compiles.
- `Tools\RunQuestValidation.py` compiles.
- Full stress run executes 1,000,000 sequences.
- No no-active quest softlocks.
- End Game completion count is greater than zero.
- No dead-end/no-complete quest findings.
- No impossible/external requirements without documented owner proof.
- Unity Play Mode verification is run after asset repairs.
