# Rationale Continuation - QUEST_STATE_GRAPH_VALIDATOR - 2026-05-15

Primary rationale file:
`Docs/AgentLogs/Rationale_QUEST_STATE_GRAPH_VALIDATOR.md`

Reason for supplement:
The local command runner timed out repeatedly on 2026-05-15, including bounded file reads. I did not blindly patch the primary rationale file because preserving audit-chain integrity is more important than satisfying a log mutation mechanically.

## Decision 008: Add Standalone Quest Report Gate

Problem:
The 1,000,000-sequence stress test produced a JSON evidence report, but without a standalone gate another agent or CI job could miss critical authored defects and treat the batch as clean.

Solution:
Added `Tools/QuestStressReportGate.py`, a zero-Unity-dependency Python script that reads the stress-test JSON and fails on:
- Missing or insufficient sequence count.
- Any no-active softlock count above zero.
- Missing or zero End Game completion count.
- Manual/no-completion terminal stalls.
- Dead-end/no-complete findings.
- Impossible or externally-owned requirements.

DOD pattern:
Fail-fast evidence audit. The tool does not mutate assets and can run after the stress test in CI or local validation.

Rejected Alternatives:
- Embedding this gate into Unity Play Mode: too heavy for a report-only check and requires editor availability.
- Mutating QuestData assets directly: unsafe without Unity AssetDatabase verification and narrative owner approval.
- Exact-key-only JSON parsing: too brittle during batch work; normalized-key fallback was added.

Scalability potential:
Low: no runtime cost; audit runs outside the game.
Middle: can gate every batch report.
High: can be wired into CI to block defective quest data.
Ultra: can aggregate multiple agent reports and enforce narrative graph health before build promotion.

Hardware Impact:
0 frame cost on i3/MX350. CLI post-processing only.

## Decision 009: Add Repair-Candidate Handoff Instead Of Asset Mutation

Problem:
The stress test identified authored quest defects, but fixing them requires knowing narrative intent and the serialized QuestData schema. Raw YAML mutation risks corrupting assets or inventing illegal single-use EventIDs.

Solution:
Added `Docs/AgentLogs/QuestGraphRepairCandidates_QUEST_STATE_GRAPH_VALIDATOR.md` with concrete candidate repairs and the safe Unity AssetDatabase path for implementation.

DOD pattern:
Evidence-separated handoff. The audit states what is proven, what is candidate repair, and what remains blocked by owner confirmation.

Rejected Alternatives:
- Creating new quest events for each defect: violates signal discipline.
- Treating manual/external triggers as valid without owner proof: hides progression risk.
- Editing `.asset` YAML blind: violates prefab/YAML mutation protocol.

Scalability potential:
Low: cheap devices unaffected.
Middle: repair owner can apply only confirmed trigger changes.
High: once graph defects are removed, high-tier narrative presentation can rely on stable quest state.
Ultra: clean quest graph supports richer branch-dependent presentation without brittle fail states.

Hardware Impact:
0 frame cost. Prevents runtime progression stalls rather than optimizing frame time.

## Decision 010: Verification Boundary Under Command Runner Failure

Problem:
Command execution timed out on trivial shell operations, Python compile checks, and git diff checks. Continuing to claim verified status would create a fake report.

Solution:
New additive files were created through `apply_patch` only. Their verification state is explicitly marked PENDING until shell execution returns.

DOD pattern:
Fail-fast honesty. Do not report measurements or compile status without command output.

Rejected Alternatives:
- Claiming the new gate compiled from visual inspection.
- Editing primary status/rationale files blind without readback.
- Mutating runtime or Unity asset files under degraded tooling.

Scalability potential:
Low/Middle/High/Ultra: no runtime cost. Audit integrity is preserved.

Hardware Impact:
0 frame cost.

## Decision 011: Add Full-Chain Quest Validation Orchestrator

Problem:
The validated workflow required separate manual commands for graph export, the 1,000,000-sequence stress run, and report gating. That creates operator error risk during batch handoff.

Solution:
Added `Tools/RunQuestValidation.py`, a small subprocess orchestrator that runs the complete evidence chain from the repository root.

DOD pattern:
Single-command reproducibility. The script only coordinates existing tools and writes reports; it does not mutate Unity assets.

Rejected Alternatives:
- Expanding the stress tester into a larger mixed-responsibility tool: unnecessary ownership creep.
- Writing a PowerShell-only wrapper: less portable and harder to run from CI Python environments.
- Skipping graph export in the wrapper: would allow stale `Data/Narrative/Quest_Graph.json` to mask QuestData drift.

Scalability potential:
Low: no runtime cost.
Middle: one command for local validation.
High: CI can execute the same command after narrative asset changes.
Ultra: batch promotion can block on a deterministic quest-graph evidence chain.

Hardware Impact:
0 frame cost on i3/MX350. CLI-only validation outside gameplay.

## Decision 012: Treat Report Gate Failure As Correct Current Outcome

Problem:
The full orchestration command now runs, but it exits with code 1 because the report gate detects authored quest graph defects. A careless handoff could mislabel that as tooling failure.

Solution:
Record gate failure as the correct evidence result for current QuestData. The stress harness and gate are functioning; the authored narrative graph remains defective.

DOD pattern:
Fail-fast audit. Passing the command is not the goal; exposing current quest defects is the goal.

Rejected Alternatives:
- Weakening the gate so the command exits 0 despite End Game never completing.
- Marking the graph clean because no no-active quest softlock was found.
- Mutating QuestData without owner confirmation to force a green gate.

Scalability potential:
Low: no runtime cost.
Middle: prevents broken quest data from passing local validation.
High: CI can block batch promotion until authored graph defects are repaired.
Ultra: future narrative branching can rely on completed graph invariants instead of fragile manual stalls.

Hardware Impact:
0 frame cost. The gate runs outside gameplay.

Measured Evidence:
Full chain command: `python Tools\RunQuestValidation.py`
Result: exit code 1 due expected `QUEST_GATE_FAIL`.
Stress run: 1,000,000 sequences x 24 events, elapsed 32.986s.
No-active softlocks: 0.
Manual/no-completion terminal stalls: 238,374.
End Game completed: 0.
End Game active: 416,774.
Dead-end/no-complete findings: 4.
Impossible/external requirement findings: 4.
