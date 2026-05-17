# External Economy Validation Evidence - VR_JERK_THRESHOLD_AUDIT

Owner boundary: `VR_JERK_THRESHOLD_AUDIT` owns `Data/UX` VR comfort data. This file records external economy verifier output only. No economy recipe, item, or distribution data was edited by this task.

## Monte Carlo

Command: `python Tools/Economy/MonteCarloEconomySim.py`

Output:

```text
ECONOMY MONTE CARLO COMPLETE
players=10000 max_nodes=10000
average_minutes=41.325
p99_minutes=59.285
copper_p99_minutes=55.500
total_nodes_mined=1541057
million_step_audit_passed=True
failures=0
report=Docs\Reports\Economy_MonteCarlo_Audit.md
histogram=Docs\Reports\Economy_MonteCarlo_TimeToBase_Histogram.png
tuned_json=Tools\Economy\Ore_Distribution_Tuned.json
STATUS: ECONOMY PROVEN
```

Report token checks:

- `Million-step proof: True`
- `Failures after 10000 nodes: 0`
- `Final p99 time:` present in `Docs/Reports/Economy_MonteCarlo_Audit.md`

## Recipe Graph

Command: `python Tools/EconomyRecipeGraphAudit.py --report Docs/AgentLogs/EconomyRecipeGraphAudit_VR_JERK_THRESHOLD_AUDIT.md`

Observed output:

```text
graph.cycle_count=0
graph.is_dag=true
graph.nodes=55
graph.edges=122
recipe_identity.recipe_count=40
status=ECONOMY SECURED
```

Report token checks:

- `Is DAG: True`
- `Cycle count: 0`
- `STATUS: ECONOMY SECURED`

## Economy Validator

Command: `python Tools/EconomyValidator.py --root .`

Output:

```text
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
items_rows=55 raw=15 crafted=40
matrix_recipe_value_aligned_resources=15
recipes=40 tier_ratios=[3.667, 2.69]
crafting_costs=50 cost_ratios=[2.931, 3.173] power_ratios=[2.916, 2.966] starter_o2_minutes=4.446 binary_bytes=7424 toaster_binary_bytes=2464 monte_carlo_steps=1000000
survival_velocity_bands=5
binding_unresolved_ids=22
binding_plan_blocked_ids=22 candidates=18 author_required=4
first_sub_recursive_kwh=433.1 literal_minutes=901.7
hash_pairs_checked=1737
unique_id_hashes=449
energy_pacing_warning=literal_30kw_requires_owner_decision
STATUS: ECONOMY BALANCED
```

## Boundary

This evidence proves the current external economy graph is acyclic and the current economy Monte Carlo run meets the one-million-step audit floor. It does not make VR comfort data responsible for future economy drift.
