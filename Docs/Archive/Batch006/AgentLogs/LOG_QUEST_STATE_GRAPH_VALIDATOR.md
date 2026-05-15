# LOG - QUEST_STATE_GRAPH_VALIDATOR

## 2026-05-14 Quest DAG Stress Audit

Status: QUESTS VALIDATED
Evidence Class: STATIC_SOURCE + CLI_COMPILE + CLI_EXECUTION

What was wrong:
- `Data/Narrative/Quest_Graph.json` is missing. The assigned primary graph source cannot be parsed because it is absent from disk.
- Fallback live QuestData audit found `quest_atlas_core_reached` as the only End Game candidate. Continuation hardening corrected threshold-aware pathfinding; it is reachable, but it still has no event-driven completion trigger.
- Four quests have no event-driven Complete trigger: `quest_arrival`, `quest_biome_spine`, `quest_atlas_core_reached`, `quest_rad_shield`.
- `quest_copper_sample` has Manual trigger and is not auto-activated, so random player event sequences never activate it.
- External phase gates require owner proof: `quest_atlas_core_reached` Thermal, `quest_atlas_signal_decoded` Thermal, `quest_atlas_signal_detected` Abyssal.

What was done:
- Added `Tools/QuestStressTest.py`.
- The tool parses `Data/Narrative/Quest_Graph.json` when present.
- If the JSON is absent, the tool fails the source contract in the report and audits `Assets/_Project/Data/Lore/Quests/*.asset` as the live QuestData fallback.
- Implemented graph dependency/path traversal, same-event edge inference, cycle scan, dead-end scan, impossible/external requirement scan, deterministic event candidates, and 1,000,000-sequence stress simulation.
- Wrote machine-readable result artifact: `Docs/AgentLogs/QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json`.

Cinematic Cheats used:
- Offline static/bitmask simulation instead of Unity runtime scene playback. This preserves gameplay runtime and avoids adding MonoBehaviour/editor wrappers.
- Integer bitmasks for active/completed/phase state. This mirrors the packed quest-state mandate and avoids object-heavy per-frame-style logic in the tool.
- Vectorized event batches for the 1M run. This is a tooling cheat only; no runtime behavior was changed.

Exact microseconds saved:
- Runtime frame-time saved: 0 us measured; no Unity runtime code was changed.
- Offline tool-time estimate: scalar 10k smoke was 17.094s, vector 10k smoke was 1.159s. Projected scalar 1M at the same smoke rate is 1,709,400,000 us; measured vector 1M was 106,956,233 us. Estimated offline validation time avoided: 1,602,443,767 us. This is CLI tool wall-clock, not profiler frame-time.

Stress result:
- Command: `python Tools\QuestStressTest.py --sequences 1000000 --sequence-length 24 --json-output Docs\AgentLogs\QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json`
- Sequences: 1,000,000
- Events per sequence: 24
- Seed: `0x48385147`
- Elapsed: 106.9562325 seconds
- No-active softlocks: 0
- Manual/no-completion terminal stalls: 238,374
- End Game completed sequences: 0
- End Game active sequences: 416,774
- Cycles: 0
- Missing prerequisites: 0

Top dangerous breaks:
1. CRITICAL: `Data/Narrative/Quest_Graph.json` is missing; fallback QuestData assets were audited instead.
2. CRITICAL: event-driven Complete trigger missing on 4 quests.
3. HIGH: manual/no-completion quests can mask objective exhaustion without producing a no-active softlock.

REGRESSION MODEL:
- CPU: no gameplay CPU change; CLI tool only.
- GC: no gameplay GC change; Python allocations are outside Unity runtime.
- Memory: JSON artifact and script added; no Unity runtime memory ownership changed.
- Cadence: no Unity Tick/Update cadence touched.
- Correctness: static fallback audit is not scene signal proof. Unity Play Mode and producer wiring remain PENDING VERIFICATION.

HOT PATH IMPACT:
- Unity hot path: none.
- Quest runtime code: unchanged.
- Offline validation: vectorized bitmask simulation is deterministic and bounded.

FAILURE MODES:
- If `Data/Narrative/Quest_Graph.json` is later added with a different schema, the flexible JSON parser may need schema-specific tightening.
- QuestData YAML parsing is static text parsing, not Unity AssetDatabase proof.
- External phase gates are treated as possible when simulated phase events occur; actual scene producers were not verified.

WHY KEPT/REJECTED:
- Kept the tool under `Tools/` because the task is an offline audit.
- Rejected Unity runtime validation because it would be slower, scene-dependent, and outside the requested data stress scope.
- Rejected graph fabrication because missing source authority is itself a critical defect.

POLISH MANDATE:
- `Docs/Tasks/CURRENT_BATCH.md` does not contain a `<POLISH_MANDATE>` tag.
- Anti-bloat pass still ran on `Tools/QuestStressTest.py`: no TODO/FIXME markers, no Unity runtime writes, no quest runtime source edits, and the only optional dependency is lazy `numpy` import with scalar fallback.

## Continuation - User-Requested Hardening

Status: QUESTS VALIDATED
Evidence Class: STATIC_SOURCE + CLI_COMPILE + CLI_EXECUTION

What was improved:
- Corrected pathfinding to honor runtime `DepthReached >= threshold` semantics.
- Added `--export-graph` to `Tools/QuestStressTest.py`.
- Generated `Data/Narrative/Quest_Graph.json` from current QuestData assets, with provenance notes and explicit authored prerequisite edges only.
- Re-ran the full stress pass against `Data/Narrative/Quest_Graph.json` instead of fallback QuestData.

Second full-run command:
- `python Tools\QuestStressTest.py --sequences 1000000 --sequence-length 24 --json-output Docs\AgentLogs\QuestStressTest_QUEST_STATE_GRAPH_VALIDATOR.json`

Second full-run result:
- Source used: `C:\Hecton8\Data\Narrative\Quest_Graph.json`
- Requested JSON missing: False
- Sequences: 1,000,000
- Events per sequence: 24
- Seed: `0x48385147`
- Elapsed: 133.122 seconds
- No-active softlocks: 0
- Manual/no-completion terminal stalls: 238,374
- End Game completed sequences: 0
- End Game active sequences: 416,774
- Paths to End Game: 2
- Cycles: 0
- Missing prerequisites: 0

Current top dangerous breaks after hardening:
1. CRITICAL: event-driven Complete trigger missing on 4 quests: `quest_arrival`, `quest_biome_spine`, `quest_atlas_core_reached`, `quest_rad_shield`.
2. HIGH: 238,374 sequences ended with only manual/no-completion quests active.
3. HIGH: 4 impossible or externally-owned activation requirements need owner proof.

Regression model update:
- CPU/GC/memory runtime: no Unity runtime code changed.
- Data: new generated JSON mirror added under `Data/Narrative/Quest_Graph.json`.
- Correctness: primary-path audit now exists, but Unity scene signal producers and QuestData-vs-JSON source-of-truth ownership remain PENDING VERIFICATION.

## Continuation - Verified Gate And Orchestrator

What was wrong:
- The validator had stress evidence but no standalone pass/fail gate.
- The full validation chain required multiple manual commands.
- Current QuestData still allows End Game activation without End Game completion.

What was done:
- Added `Tools/QuestStressReportGate.py`.
- Added `Tools/RunQuestValidation.py`.
- Added `Docs/AgentLogs/QuestGraphRepairCandidates_QUEST_STATE_GRAPH_VALIDATOR.md`.
- Added `Docs/AgentLogs/QuestValidationRunbook_QUEST_STATE_GRAPH_VALIDATOR.md`.
- Re-ran compile, diff, and full validation chain.

Cinematic Cheats used:
- None. This is offline quest-graph validation.

Exact Microseconds saved:
- Runtime frame savings: 0 us.
- Runtime frame cost added: 0 us.
- Offline validation now avoids manual multi-command drift; this is process risk reduction, not gameplay CPU optimization.

Verification:
- `python -m py_compile Tools\QuestStressTest.py Tools\QuestStressReportGate.py Tools\RunQuestValidation.py` returned exit code 0.
- `git diff --check` returned exit code 0.
- `python Tools\RunQuestValidation.py` ran the export probe, full 1,000,000-sequence stress test, and report gate.

Full-chain result:
- Exit code: 1.
- Gate status: `QUEST_GATE_FAIL`.
- Interpretation: expected failure caused by authored quest graph defects, not validator failure.

Stress-run evidence:
- Source used: `Data/Narrative/Quest_Graph.json`.
- Quest count: 11.
- Event candidates: 17.
- Simulation: 1,000,000 sequences x 24 events.
- Elapsed: 32.986s.
- No-active softlocks: 0.
- Manual/no-completion terminal stalls: 238,374.
- End Game completed sequences: 0.
- End Game active sequences: 416,774.
- Paths to End Game: 2.
- Dead-end/no-complete findings: 4.
- Impossible/external requirement findings: 4.

Current status:
- Validator task: complete and improved.
- Authored quest graph: not clean.
- Unity runtime scene-signal proof: PENDING VERIFICATION.

## Continuation - Remediation Pass And Green Gate

What was wrong:
- `--export-graph` could rebuild from existing JSON instead of live QuestData once the mirror existed.
- `QuestStateManager` compiled item/discovery/signal payload IDs with `QuestFlagHashKernel`, but event producers send `LocHash` IDs.
- Four quests had no event-driven completion.
- `quest_copper_sample` had a manual trigger and was not auto-activated.
- `quest_first_hour_collect_titanium` had a redundant prerequisite that blocked activation on the same discovery event that completed the prerequisite.
- The simulator counted temporary inactive objective gaps as hard soft-locks even when future triggers still existed.

What was done:
- `Tools/QuestStressTest.py`: export now always rebuilds from live QuestData; soft-lock detection now requires no remaining uncompleted triggerable quest.
- `Assets/_Project/Scripts/Quest/QuestStateManager.cs`: payload and critical-item hashes now use `LocHash.Compute`, matching event producers.
- `Quest_Arrival.asset`: completes on `DiscoveryMade:first_hour_exit_lifepod`.
- `Quest_BiomeSpine.asset`: completes on `BiomeEntered:1`.
- `Quest_CopperSample.asset`: activates on `DiscoveryMade:first_hour_exit_lifepod`.
- `Quest_CoreReached.asset`: completes on `DepthReached:4800`.
- `Quest_RadShield.asset`: completes on `ItemCollected:Item_Equip_RadiationVeil`.
- `Quest_FirstHour_CollectTitanium.asset`: redundant exit prerequisite removed; trigger already gates on `first_hour_exit_lifepod`.
- Added `Assets/_Project/Scripts/Editor/QuestGraphRepairUtility.cs` plus `.meta` as the Unity AssetDatabase repair path for future runs.

Cinematic Cheats used:
- None. This is quest graph/data remediation.

Exact Microseconds saved:
- Runtime frame cost added: 0 us.
- Runtime frame savings are not claimed without Unity profiler. The improvement is correctness: dead objective states and hash-missed transitions are removed from the audited graph path.

Verification:
- Python syntax verified by AST/compile command: `PY_SYNTAX_OK`.
- `git diff --check` returned exit code 0.
- Smoke run: 10,000 sequences x 24 events, no-active softlocks 0, manual stalls 0, End Game completions 3,862, dead ends 0, impossible requirements 0.
- Full run: `python Tools\RunQuestValidation.py` returned exit code 0.
- Gate: `QUEST_GATE_PASS`.
- Full stress result: 1,000,000 sequences x 24 events, elapsed 147.333s, no-active softlocks 0, manual stalls 0, End Game completions 388,415, dead ends 0, impossible requirements 0.

Regression model:
- CPU: Unity runtime hot path unchanged except cold quest compile hash function for authored payload IDs.
- GC: no new gameplay allocations in Tick/Update paths.
- Memory: one Editor-only utility script and generated JSON/report artifacts.
- Cadence: no Tick/Update cadence changed.
- Correctness: offline graph gate is green; Unity import/Play Mode is still PENDING VERIFICATION because Unity and `dotnet` are unavailable in this environment.

HOT PATH IMPACT:
- Quest signal evaluation runtime node matching remains integer/hash comparison.
- No new per-frame allocation or string work added.

FAILURE MODES:
- `Item_Equip_RadiationVeil` must exist in the equipment item source that feeds `SuitUpgradeManager`; the hash alias exists in code, but Unity item catalog import was not verified here.
- Unity may reserialize raw-edited ScriptableObject YAML on import; an Editor repair utility is included for safe reapplication.
- Runtime phase producers still require Play Mode verification.

WHY KEPT/REJECTED:
- Kept minimal QuestData scalar changes because the graph defects were authored data defects.
- Rejected new EventIDs.
- Rejected global quest hash change because that risks save and quest identity drift.
- Rejected claiming Unity compile proof because the required tools are absent.

## Continuation - Handoff Consistency Pass

What was wrong:
- `Docs/AgentLogs/QuestValidationRunbook_QUEST_STATE_GRAPH_VALIDATOR.md` and continuation artifacts still described the pre-repair `QUEST_GATE_FAIL` as the expected current result.
- That stale text conflicted with the later full green gate and could cause duplicate repair work.

What was done:
- Updated the runbook so the expected current result is `QUEST_GATE_PASS`.
- Appended supersession notes to continuation status/log files instead of erasing earlier failure evidence.
- Recorded the decision in `Docs/AgentLogs/Rationale_QUEST_STATE_GRAPH_VALIDATOR.md`.

Cinematic Cheats used:
- None. Narrative graph validation and handoff hygiene only.

Exact Microseconds saved:
- Runtime frame cost: 0 microseconds.
- Offline operator-time savings only; no gameplay system was added.

Verified state:
- Current offline authority remains the full-chain command: `python Tools\RunQuestValidation.py`.
- Latest full-chain result: `QUEST_GATE_PASS` on 1,000,000 sequences x 24 events.
- Remaining gap: Unity import, C# compile, and Play Mode are PENDING VERIFICATION because required executables are unavailable.

Final command hygiene:
- Python syntax recheck returned `PY_SYNTAX_OK`.
- Standalone report gate returned `QUEST_GATE_PASS`.
- Scoped `git diff --check` over validator-touched tracked files returned exit code 0.
- Global `git diff --check` is polluted by unrelated trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md`; that file is outside this validator's write scope and was not changed here.
