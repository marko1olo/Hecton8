# Rationale_FAUNA_BEHAVIOR_SIMULATOR

Evidence class: STATIC_DOC until recreated Python tool is executed. Runtime Unity/profiler proof remains absent.

## Mandate Selection

Problem: The assignment is an offline utility-AI weight discovery task, not runtime fauna implementation.
Solution: Load only AI cognition, swarm/population, acoustic sensory, deterministic RNG, cinematic-cheat, and evidence-reporting mandates.
Rejected Alternatives: Loading the full mandate registry would inflate context and does not improve correctness for a Python-only batch tool.
Scalability potential: Low uses scalar population math and small telemetry; Middle/High/Ultra can increase sweep resolution and replicate count without changing the data contract.
Hardware Impact: On i3/MX350-class devices this stays out of runtime entirely; expected gameplay hot-path cost is 0 microseconds until constants are consumed by C#.

Problem: Batch prompt requested `CURRENT_BATCH_OSHINO.md`, but only `Docs/Tasks/CURRENT_BATCH.md` exists.
Solution: Extracted the `FAUNA_BEHAVIOR_SIMULATOR` XML tag from the active batch file using a CLI regex command.
Rejected Alternatives: Guessing from chat text or reading neighboring prompts. Neighbor prompts were ignored after extraction.
Scalability potential: File-backed prompt source remains stable under context compression.
Hardware Impact: No runtime hardware impact.

Problem: A concurrent workspace change removed the FAUNA status/log/data/tool files after the first full run.
Solution: Recreate only this agent's required files and rerun artifact generation. Do not revert or modify other agents' files.
Rejected Alternatives: Reverting broad workspace state; forbidden because this is a multi-agent dirty worktree.
Scalability potential: File-backed rerun restores batch handoff without cross-domain damage.
Hardware Impact: No runtime hardware impact.

## Design Decisions

Problem: Utility AI balance needs predator pressure without fragile extinction/overpopulation oscillation.
Solution: Use aggression and hunger as positive hunt drive, quadratic fear as a late scarcity brake, and prey logistic growth as the macro ecology fake.
Rejected Alternatives: Linear fear curve because it suppresses early hunting too aggressively; full per-creature ecology truth because it is a runtime waste for balance discovery.
Scalability potential: Toaster runtime consumes only constants. Top-tier runtime can spend saved CPU on richer predator presentation, acoustic cues, and visible overkill while keeping the same macro balance.
Hardware Impact: Estimated runtime gain versus per-creature truth simulation is unmeasured but functionally removes this balancing workload from the frame; profiler proof remains absent.

Problem: The prompt forbids Dotnet/C# work.
Solution: Verification uses `python -m py_compile`, `python -m json.tool`, and the Python simulation CLI only.
Rejected Alternatives: `dotnet build` and Unity integration were rejected for this task because they would violate the prompt's `NO DOTNET` objective.
Scalability potential: Data handoff is engine-agnostic and can be consumed later by a proper C# owner.
Hardware Impact: No runtime hardware impact from this task.

## Loop 2 Decisions

Problem: The required million-step run needed current on-disk artifacts after concurrent file removal.
Solution: Reran `python Tools\AI_Sim\FaunaBalanceSim.py --frames 1000000 --discovery-frames 12000`; final telemetry: prey `9436.618`, stalker `38.109`, alpha leviathan `1.664`, score `0.4471`.
Rejected Alternatives: Using the earlier removed artifact or the quick smoke run. Neither leaves current file evidence.
Scalability potential: Low/Middle/High/Ultra runtime tiers consume the same constants; higher tiers should spend saved CPU on predator presentation, denser prey schools, stronger acoustic/visual tells, not per-agent ecology truth.
Hardware Impact: Runtime cost remains 0 microseconds for this offline tool. JSON ingestion/runtime application remains PENDING VERIFICATION.

Problem: Aggression sweet spot must avoid prey extinction and predator starvation.
Solution: Use heatmap score over aggression/fear candidates and select `AggressionScalar=1.38`, `FearScalar=0.76`; the final run kept prey near the `9600` target and stalkers near the `36` target while preserving alpha presence.
Rejected Alternatives: Max aggression was rejected because the scoring model penalizes predator overkill, prey collapse, and alpha starvation. Lower aggression was rejected because stalker pressure falls under target.
Scalability potential: Toaster tier can use this as a low-frequency scalar. Ultra tier can add visible overkill such as longer lunge anticipation, richer sonar tells, and denser fleeing prey while keeping constants unchanged.
Hardware Impact: No frame-time cost measured; this is a data export. Expected runtime delta is 0 microseconds until integrated.

Problem: Sensory noise must model 1-bit radar errors without making predators useless.
Solution: Tested noise `0.00`, `0.03`, `0.06`, `0.09`, `0.12`, `0.18`, `0.24`; the JSON keeps `0.12` as the tolerance line.
Rejected Alternatives: Perfect-signal tuning was rejected because it would overfit and fail when radar/retinal signals are occluded.
Scalability potential: Low tier can use fixed noise tolerance. High/Ultra can add richer acoustic feedback and false-positive presentation without changing ecology truth.
Hardware Impact: Runtime proof absent. Offline estimate is 0 microseconds inside Unity because this file only exports constants.

## Loop 3 Decisions

Problem: Retinal blindness from another agent would make visual-only predator hunting brittle.
Solution: Use acoustic tracking as the compensation channel. Rerun ratios: acoustic under retinal blindness `0.56321` of normal kill throughput; no-acoustic retinal blindness `0.08101`.
Rejected Alternatives: Retinal-only perception was rejected because it collapses under blindness/occlusion. Noisy acoustic false positives are cheaper and controllable compared with full perception physics.
Scalability potential: Low tier can use acoustic scalar only. Middle can add sparse acoustic investigation. High/Ultra can add richer sonar wakes, positional audio tells, and cinematic hunt anticipation.
Hardware Impact: Runtime application is unmeasured. Offline tool adds 0 microseconds to frame time; future acoustic tracking must be profiled in Unity before any 0-GC claim.

Problem: Fear buildup shape must keep predators believable without wiping prey.
Solution: Keep `fearCurvePower=2.0`. The comparison exported `linearVsQuadraticScoreDelta=0.058491`, meaning the linear curve scored worse.
Rejected Alternatives: Linear fear was rejected because it applies too much fear at low threat and suppresses early hunting; cubic fear was not selected because it delays braking too long for scarcity protection.
Scalability potential: Toaster tier uses one multiply. Ultra tier can use the same scalar to drive richer animation, bioluminescent panic, and audio layers.
Hardware Impact: Runtime cost estimate is one extra multiply if integrated. Actual C# cost remains PENDING VERIFICATION.
