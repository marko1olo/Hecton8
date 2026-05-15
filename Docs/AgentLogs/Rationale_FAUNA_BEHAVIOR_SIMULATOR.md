# Rationale_FAUNA_BEHAVIOR_SIMULATOR

Evidence class: CLI_PYTHON_SIMULATION for generated Python/JSON artifacts. Runtime Unity/profiler proof remains absent.

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

## Polish Decisions

Problem: `<POLISH_MANDATE>` was absent from `Docs/Tasks/CURRENT_BATCH.md`, but the status was 100% checked and an anti-bloat pass was still required.
Solution: Record `POLISH_MANDATE_NOT_FOUND` and perform local anti-bloat on own files only.
Rejected Alternatives: Inventing a missing polish directive or reading neighboring agent prompts.
Scalability potential: Keeps handoff bounded and reduces runtime data ingestion surface.
Hardware Impact: No runtime hardware impact measured.

Problem: The first constants export duplicated the full 40 KB report, including 101 timeline samples, in `Data/AI/Fauna_Global_Weights.json`.
Solution: Changed `FaunaBalanceSim.py` so the `Data/AI` output is a compact 1812-byte constants handoff with a pointer to `Tools/AI_Sim/FaunaBalanceSim_Report.json`; the report retains full heatmap/noise/retinal/fear telemetry.
Rejected Alternatives: Keeping duplicate report data in the runtime-facing constants file. It is avoidable bloat.
Scalability potential: Low-end devices parse the compact handoff; high-end tooling can inspect the detailed report offline.
Hardware Impact: Runtime cost is still unmeasured and PENDING VERIFICATION; file-size reduction is static evidence only.

## Repeat-Seed Validation Decisions

Problem: A single deterministic seed can hide coefficient fragility.
Solution: Added `--validate-selected` mode and ran 5 deterministic replicates at 200,000 frames each using the selected constants.
Rejected Alternatives: Rerunning full heatmap discovery for each seed. That is offline-expensive and does not change the selected constants unless a failure is found.
Scalability potential: Low-end runtime still consumes the same constants; validation data stays offline. High-end tooling can raise replicate count or frames without changing runtime contract.
Hardware Impact: Runtime impact remains 0 microseconds. Offline replicate validation elapsed under 30 seconds in this environment.

Problem: Replicate evidence must be visible in the compact constants file without bloating it with full per-replicate rows.
Solution: Store only `replicateValidation` summary in `Data/AI/Fauna_Global_Weights.json` and write full rows to `Tools/AI_Sim/FaunaBalanceSim_ReplicateValidation.json`.
Rejected Alternatives: Embedding all replicate rows in the runtime-facing constants file. It repeats the earlier bloat problem.
Scalability potential: Runtime consumers parse a bounded summary; balancing tools can inspect the detailed file.
Hardware Impact: Static file-size impact only: compact constants file is `2250` bytes after summary; runtime parsing cost remains unmeasured.

## Regression Harness Decisions

Problem: The simulator had CLI validation artifacts but no committed regression harness, so future edits could silently break schema alignment.
Solution: Add `Tools/AI_Sim/test_fauna_balance_sim.py` with standard-library `unittest` checks for compact/report consistency, replicate summary consistency, bounded selected-weight run, and short replicate validation.
Rejected Alternatives: Adding third-party test dependencies or rerunning the million-frame sweep in tests. Both are unnecessary for a fast guard.
Scalability potential: Low-cost test catches data-contract drift before runtime owners consume bad constants; high-end/manual validation still uses the full CLI sweeps.
Hardware Impact: Runtime impact remains 0 microseconds. Final regression test execution completed offline in `1.470 s`.

Problem: Dynamic import of a dataclass module failed because `importlib` did not register the module in `sys.modules`.
Solution: Register the simulator module in `sys.modules` before `exec_module`.
Rejected Alternatives: Turning `Tools/AI_Sim` into a package or modifying production script import paths; both are broader than this tool requires.
Scalability potential: Keeps the regression harness self-contained and path-stable.
Hardware Impact: No runtime hardware impact.

## Selected Constant Range Decisions

Problem: Artifact equality checks catch drift between files, but they do not catch both files drifting together into absurd selected constants.
Solution: Add `SELECTED_CONSTANT_RANGES` and validate required keys, numeric type, finite values, and ranges in compact constants and detailed report.
Rejected Alternatives: Relying on the million-frame result alone. A hand-edited JSON could bypass simulation evidence.
Scalability potential: Runtime consumers receive bounded constants; balancing tools can widen ranges deliberately in code review if design needs change.
Hardware Impact: Runtime impact remains 0 microseconds. Validation is offline/read-only.

Problem: Runtime-loading helper could ingest bad constants without a clear error.
Solution: `load_selected_weights()` now calls the same selected-constant validator and raises `ValueError` with explicit reasons.
Rejected Alternatives: Letting `KeyError` or bad math surface later in simulation. That is less diagnostic.
Scalability potential: Faster failure when future agents or CI consume corrupted handoff data.
Hardware Impact: No runtime hardware impact.

## Non-Finite Guard Decisions

Problem: Python JSON serialization allows `NaN` and `Infinity` by default, which would poison a balance handoff and can break strict parsers.
Solution: Set `allow_nan=False` in `write_json()` and add recursive non-finite scans to `--check-artifacts`.
Rejected Alternatives: Trusting the current math path to stay finite forever. Future coefficient changes could introduce non-finite values.
Scalability potential: Runtime consumers get strict JSON. Offline tooling fails fast before bad constants reach Unity.
Hardware Impact: Runtime impact remains 0 microseconds. Check is offline/read-only.

Problem: The non-finite guard needed proof against an actual bad artifact.
Solution: Add `test_artifact_checker_rejects_nonfinite_numbers`, which writes a temporary constants JSON containing `NaN` and expects `ARTIFACT_CHECK_FAILED`.
Rejected Alternatives: Only testing happy-path artifacts. That does not prove the guard catches the failure mode.
Scalability potential: Prevents silent bad-data propagation as the simulator evolves.
Hardware Impact: No runtime hardware impact.

## Artifact Checker Decisions

Problem: The regression tests validate artifacts, but humans and CI need a direct CLI check that fails with a nonzero exit code when JSON artifacts drift.
Solution: Add `--check-artifacts` to `Tools/AI_Sim/FaunaBalanceSim.py`; it compares compact constants, detailed report, and replicate validation files, prints sizes/frame counts, and returns `2` on any mismatch.
Rejected Alternatives: Relying only on `unittest`; it works for developers but is less direct for a batch pipeline wanting a single artifact check command.
Scalability potential: Low-end runtime remains unaffected; batch CI can now validate handoff files without rerunning simulation.
Hardware Impact: Runtime impact remains 0 microseconds. CLI check is offline and read-only.

Problem: The test harness did not cover the new checker.
Solution: Add `test_artifact_checker_passes_current_outputs`.
Rejected Alternatives: Manual-only checker verification, which would regress silently.
Scalability potential: Keeps the checker contract stable as schema evolves.
Hardware Impact: No runtime hardware impact.

## Artifact Header Contract Decisions

Problem: The checker could accept artifacts whose numeric constants still matched while the schema, producer, evidence class, Unity-proof status, species targets, or report paths had drifted.
Solution: Add canonical header/path/species-target constants in `Tools/AI_Sim/FaunaBalanceSim.py` and validate them in `--check-artifacts` for constants, detailed report, and replicate validation files.
Rejected Alternatives: Trusting matching selected constants alone. That permits fake provenance and fake runtime verification claims.
Scalability potential: Low-tier runtime consumers keep a small, bounded handoff. High/Ultra tooling can inspect detailed reports without changing the contract.
Hardware Impact: Runtime impact remains 0 microseconds. Validation is offline/read-only.

Problem: Header hardening needed proof against real bad edits.
Solution: Add regression tests for schema drift, `runtimeUnityProof` drift, report path drift, non-finite numbers, out-of-range constants, and bad selected-weight loads.
Rejected Alternatives: Happy-path-only checks. They do not prove the guard rejects corrupted artifacts.
Scalability potential: Future batch agents can change simulator internals while the artifact boundary fails fast on contract breakage.
Hardware Impact: No runtime hardware impact.

Problem: The current sandbox denies writes inside `tempfile.TemporaryDirectory()` paths and `py_compile` fails while atomically replacing `.pyc` files under `__pycache__`.
Solution: Use deterministic workspace-local test artifact folders, set `sys.dont_write_bytecode = True`, verify with `python -B`, and use direct source `compile()` as the syntax gate.
Rejected Alternatives: Requesting Dotnet/Unity verification or relying on `py_compile` despite known filesystem denial. Both are inappropriate for this Python-only task.
Scalability potential: The test harness stays runnable in restricted CI/sandbox contexts.
Hardware Impact: No runtime hardware impact. Remaining untracked `__pycache__` cleanup is blocked by `WinError 5` filesystem permissions.

## Replicate Contract Decisions

Problem: Replicate validation could claim `REPLICATE_STABLE` while the compact summary drifted on frame count, replicate count, or the weights used by the detailed replicate report.
Solution: Compare `framesPerReplicate`, `replicates`, and the replicate report's weight subset against the compact selected constants in `--check-artifacts`.
Rejected Alternatives: Trusting `status`, `failureCount`, and population summary alone. That does not prove the repeat-seed evidence used the exported weights.
Scalability potential: Runtime consumers keep one compact constants file; offline validation proves the selected values and repeat-seed evidence remain tied together.
Hardware Impact: Runtime impact remains 0 microseconds. Validation is offline/read-only.

Problem: The new replicate comparisons needed failure-mode evidence.
Solution: Add regression tests that corrupt compact replicate frame/replicate counts and detailed replicate `fearScalar`; both must produce `ARTIFACT_CHECK_FAILED`.
Rejected Alternatives: Updating checker logic without negative tests.
Scalability potential: Future agents can regenerate replicate reports without silently breaking the compact-to-detail contract.
Hardware Impact: No runtime hardware impact.

## Original Task Evidence Decisions

Problem: Artifact consistency did not prove that all original primary task evidence remained present after future edits.
Solution: Extend `--check-artifacts` to validate heatmap rows, exact noise case coverage, retinal-blind acoustic compensation, and quadratic-vs-linear fear evidence.
Rejected Alternatives: Relying on manual JSON inspection or assuming the full report still contains the required proof.
Scalability potential: The compact constants file stays small while the detailed tool report remains machine-auditable.
Hardware Impact: Runtime impact remains 0 microseconds. Validation is offline/read-only.

Problem: The compact million-frame summary could drift on score or stability while frame count and population still matched.
Solution: Compare `score` and `stability` as well as `frames` and `population` against the detailed million-frame run.
Rejected Alternatives: Partial summary validation. It can hide a hand-edited stability claim.
Scalability potential: Runtime consumers receive a compact but fully tied summary.
Hardware Impact: No runtime hardware impact.

Problem: `load_selected_weights()` could ingest constants with a bad artifact header if the selected constants themselves looked valid.
Solution: Reuse artifact header validation inside the loader before constructing `UtilityWeights`.
Rejected Alternatives: Letting invalid status/provenance flow into validation runs.
Scalability potential: Future automation fails before running replicate validation on untrusted data.
Hardware Impact: No runtime hardware impact.

Problem: Generated Python cache directories could not be deleted under this sandbox (`WinError 5`) and polluted `git status`.
Solution: Add standard Python bytecode cache ignore patterns to `.gitignore`.
Rejected Alternatives: Repeated cleanup attempts after permission denial.
Scalability potential: Python tooling can run without creating visible repository noise.
Hardware Impact: No runtime hardware impact.

## Sweet Spot and Run-Weight Contract Decisions

Problem: The detailed heatmap could stop proving the exported aggression/fear sweet spot if the top heatmap row drifted away from selected constants.
Solution: Validate that `report.heatmapTop10[0].weights.aggressionScalar` and `fearScalar` match the exported selected constants.
Rejected Alternatives: Counting heatmap rows only. That proves data presence but not sweet-spot linkage.
Scalability potential: Future sweeps can change internal scoring while preserving a machine-checked selection boundary.
Hardware Impact: Runtime impact remains 0 microseconds. Validation is offline/read-only.

Problem: The million-frame run could report population/score evidence produced by weights different from the exported constants.
Solution: Reuse the weight-subset validator against `report.millionFrameRun.weights`.
Rejected Alternatives: Trusting compact summary values alone.
Scalability potential: Runtime consumers receive constants tied to the million-frame proof that selected them.
Hardware Impact: No runtime hardware impact.

## Million-Frame Timeline Decisions

Problem: The detailed report could keep a correct final score and population while losing the timeline evidence that tracks prey and predator state across the million-frame run.
Solution: Validate `report.millionFrameRun.samples` count, first frame, last frame, integer frame type, and strict frame ordering.
Rejected Alternatives: Checking only the compact million-frame summary. That proves endpoint data, not tracking evidence.
Scalability potential: Runtime consumers still parse only compact constants; offline tooling keeps the detailed time series auditable.
Hardware Impact: Runtime impact remains 0 microseconds. Validation is offline/read-only.

Problem: Timeline validation needed failure-mode proof.
Solution: Add regression tests for sample truncation and frame-order drift.
Rejected Alternatives: Relying on code review or human JSON scrolling.
Scalability potential: Future report compaction cannot silently remove tracking evidence without failing tests.
Hardware Impact: No runtime hardware impact.
