# Rationale_AI_POTENTIAL_FIELD_NAVIGATOR

Status: PENDING VERIFICATION

## Intake Decisions

Problem: Prompt header declares 15 tasks, but the XML block contains 8 numbered executable tasks.
Solution: Use the numbered task list as scope and record the mismatch in status/logs.
Rejected Alternatives: Claiming 15 completed tasks would be a false report; inventing 7 missing tasks would breach batch parsing.
Scalability potential: Scope remains bounded to AI potential field design, simulator, and tuning data. Low/Middle/High/Ultra behavior will be encoded in tuning tiers.
Hardware Impact: Prevents unnecessary architecture sprawl on i3/MX350; no runtime cost yet.

Problem: AI steering through currents can become a per-entity physics simulation.
Solution: Treat `AbyssalFlowField` as an AI hint vector. Current alignment becomes a boost/resistance scalar in the potential field, not a direct force authority.
Rejected Alternatives: Applying global current forces to every predator; direct GPU readback from the flow texture; static NavMesh integration.
Scalability potential: Low uses one sampled flow vector at 10Hz; Middle adds SDF repulsion; High adds richer current conformity; Ultra can add local vortex interest without changing public contracts.
Hardware Impact: Expected low-end gain versus A* per predator is measured in avoided path queries; exact microseconds remain PENDING VERIFICATION until simulator/model output is recorded.

Problem: SDF obstacle avoidance can jitter when attraction, current, and wall repulsion fight at equal magnitude.
Solution: Use inverse-square repulsion with finite clamps and EWMA smoothing of the final steering vector after the first simulator pass.
Rejected Alternatives: Bezier smoothing, iterative relaxation, Unity NavMesh, raycast fan steering, and per-frame path recomputation.
Scalability potential: Low clamps repulsion samples and smooths hard; High/Ultra can keep more local SDF probes and visual overkill while preserving the same steering contract.
Hardware Impact: Lower branch and query count for i3/MX350; exact microsecond estimate remains PENDING VERIFICATION.

Problem: EWMA on the full steering vector delayed obstacle repulsion and produced SDF penetration in the simulator.
Solution: Split the steering solve into smoothed soft intent (`target + flow`) and immediate SDF repulsion. The simulator also applies a push-out fallback and penalizes it heavily.
Rejected Alternatives: Keep full-vector smoothing; hide negative clearance; allow fallback-assisted paths to win.
Scalability potential: Low retains immediate wall response with fewer probes. Middle/High/Ultra can add richer SDF gradients without changing the immediate-repulsion rule.
Hardware Impact: The split adds no new runtime query; it changes ordering. Low-end benefit is fewer correction spikes and fewer emergency path repairs. Simulator final: 48 candidates, 30 reached, final selected path reached in 34.6s, clearance 2.2249m, pushout 0.

Problem: Performance modeling could be misreported as measured Unity runtime.
Solution: Record it as static model only: 100 predators at 10Hz = 1000 samples/sec = 16.67 samples/frame at 60Hz, with estimated scalar ops/frame in JSON.
Rejected Alternatives: Fabricated microsecond savings from Python timing; claim GCMonitor proof without Unity.
Scalability potential: Low 10Hz, Middle 10Hz, High 15Hz, Ultra 20Hz; Ultra spends saved cycles on visual path curvature, not unbounded simulation.
Hardware Impact: Static low estimate is 3,500 scalar ops/frame; high estimate is 7,166.67 scalar ops/frame. Runtime microseconds remain PENDING VERIFICATION until Burst/Profiler capture.

Problem: `<POLISH_MANDATE>` tag is absent from `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Mark the tag read as dependency-blocked and run a local anti-bloat pass over touched files.
Rejected Alternatives: Inventing polish instructions; reading neighboring agent prompts as substitute authority.
Scalability potential: No runtime expansion. The final artifact set stays limited to simulator, JSON tuning, architecture note, and logs.
Hardware Impact: No extra runtime cost. Removed unused Python imports; no Unity assets, prefabs, scenes, or project settings were edited.

Problem: The first completion could regress silently because it depended on one manual simulator run.
Solution: Add `Tools/AI_Sim/test_ai_path_sim.py` and pin flow parameter constants inside `Data/AI/Navigation_Tuning.json`.
Rejected Alternatives: Rely on chat summary or manual rerun; leave source constants only in prose.
Scalability potential: Tests protect Low/Middle/High/Ultra tuning from losing zero-pushout clearance or idle flow drift.
Hardware Impact: No runtime impact. Tooling verification runs four tests in 0.487s on this workstation; Unity/Burst runtime performance remains PENDING VERIFICATION.

Problem: A future agent could edit the simulator, leave stale tuning JSON on disk, and still pass a superficial file existence check.
Solution: Add `python Tools/AiPathSim.py --check`, which loads `Data/AI/Navigation_Tuning.json`, replays selected weights, validates source parameters, confirms target reach, zero SDF pushouts, >=2m clearance, <=1 jitter event, idle flow drift, and 100-predator performance model constants.
Rejected Alternatives: Depend on manual visual inspection of JSON; add Unity runtime code without a profiling lane; trust the last generation timestamp.
Scalability potential: Low/Middle/High/Ultra profiles now have a fast artifact guard before runtime porting. High/Ultra visual overkill remains data-driven; Low remains protected from hidden clearance regression.
Hardware Impact: No runtime cost on i3/MX350. Tooling check cost is outside gameplay; exact Unity microseconds saved remain 0 claimed / PENDING VERIFICATION.

Problem: Tier JSON exposed binary float noise, which makes handoff data look mechanically generated and harder to diff.
Solution: Round exported steering weights to six decimals before JSON serialization.
Rejected Alternatives: Leave binary tail values; hand-edit JSON after each simulator run.
Scalability potential: Cleaner Low/Middle/High/Ultra diffs reduce accidental tuning churn while preserving deterministic values.
Hardware Impact: No runtime cost. Tooling-only formatting; Unity performance remains PENDING VERIFICATION.
