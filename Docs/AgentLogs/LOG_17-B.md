# LOG_17-B

## 2026-06-03 - First 20 Minute Balance Monte Carlo

What was wrong:
- Runtime `Standard_Suit_V1.asset` used `maxOxygen=100` and `oxygenConsumptionRate=0.5`.
- `HectonSurvivalSystem` multiplies base drain by `1 + depth*0.1`, movement, stress, leak, and carry mass.
- 100,000 baseline sessions produced `prob_suffocated_before_wire=0.973900`.
- Copper was not the bottleneck: `prob_wire_starved_alive=0.000000`; ore weights were left unchanged.

What was done:
- Added `Tools/Economy/FirstTwentyMinuteBalanceSim.py`.
- Added `Tools/Economy/test_first_twenty_balance_sim.py`.
- Changed `Assets/_Project/Data/Survival/Standard_Suit_V1.asset`:
  - `maxOxygen: 100 -> 139.2400`
  - `oxygenConsumptionRate: 0.5 -> 0.0150`
- Changed `Data/Economy/Survival_Stats.json`:
  - `tank_capacity_liters: 600.0 -> 835.44`
  - `base_liters_per_second: 0.1 -> 0.09`
- Changed `biological_metabolism_profiles.csv`:
  - `Player.baseCalorieDrainPerSecond: 0.0028 -> 0.00196`
  - `Player.baseHydrationDrainPerSecond: 0.0035 -> 0.00245`
- Left ore CSV/JSON unchanged because measured starvation was not ore-driven.
- Left `Data/Balance/ToolHeat.csv` unchanged because tool overheat was not a first-wire failure source.

Cinematic cheats used:
- Offline surface-loop model instead of runtime scene traversal.
- Copper clump approximated as a short local-search streak matching the 85% repeat-bias shape in `ProceduralOreSpawner`.
- No physical metabolism simulation; table rates only.

Validation:
- Baseline seed: `0x48454338`, sessions: `100000`.
- Validation seed: `0x17B17B17`, sessions: `100000`.
- Final `prob_suffocated_before_wire=0.037580`.
- Final `prob_wire_starved_alive=0.001880`.
- Final `prob_survival_devalued_excess=0.155260`.
- Final wire time p95: `538.654327` seconds.
- Final wire time p99: `832.947519` seconds.
- Python verification: `python -m unittest Tools.Economy.test_first_twenty_balance_sim` passed.
- Unity/runtime verification: PENDING VERIFICATION. No C# changed; dotnet/Unity build not launched.

Exact microseconds saved:
- Runtime frame CPU saved: `0.000 us/frame`.
- Baseline offline sim cost: `61.659990 us/session`.
- Final offline sim cost: `460.324739 us/session`.
- This pass bought gameplay survivability, not frame time. Probability delta: death-before-wire reduced by `0.936320` absolute, equal to `936320` fewer deaths per 1,000,000 simulated starts.

## 2026-06-03 - Million-Session Soak, No-Apply

What was wrong:
- 100k validation can hide LCG-tail instability.
- User repeated the continuous Monte Carlo directive; current tuned files needed a larger no-apply proof run before any further mutation.

What was done:
- Ran `Tools/Economy/FirstTwentyMinuteBalanceSim.py` with `--runs 1000000 --tuning-passes 1` and no `--apply`.
- Wrote `Docs/Reports/First20Balance_17-B_million.json`.
- Wrote `Docs/Reports/First20Balance_17-B_million.md`.
- Kept ore weights unchanged because alive-starved remained below 0.2%.
- Kept `Data/Balance/ToolHeat.csv` unchanged because no tool-heat failure signal exists in this first-wire model.

Cinematic cheats used:
- Same offline route model as the 100k validation.
- No runtime scene traversal; LCG/resource/oxygen authority only.

Validation:
- Seed pack A: 1,000,000 sessions, `prob_suffocated_before_wire=0.038378`, `prob_wire_starved_alive=0.001816`, `prob_survival_devalued_excess=0.153006`, wire p95 `543.061375` seconds.
- Seed pack B: 1,000,000 sessions, `prob_suffocated_before_wire=0.038783`, `prob_wire_starved_alive=0.001807`, `prob_survival_devalued_excess=0.153756`, wire p95 `541.702790` seconds.
- Tuning result: `no_change`.
- Runtime verification: PENDING VERIFICATION.

Exact microseconds saved:
- Runtime frame CPU saved: `0.000 us/frame`.
- Offline seed pack A cost: `477.451402 us/session`.
- Offline seed pack B cost: `342.638430 us/session`.
- Additional table mutation rejected; no measured bottleneck remained in ore spawn or tool heat for the first-wire objective.

## 2026-06-03 - Tail Soak 2, No-Apply

What was wrong:
- The previous tuned curve needed another independent seed-family proof before any new mutation.

What was done:
- Ran `Tools/Economy/FirstTwentyMinuteBalanceSim.py --runs 1000000 --world-seed 0xA17B0001 --validation-seed 0xA17B0002 --tuning-passes 1` with no `--apply`.
- Wrote `Docs/Reports/First20Balance_17-B_tail2.json`.
- Wrote `Docs/Reports/First20Balance_17-B_tail2.md`.
- Tuning law returned `no_change`.

Cinematic cheats used:
- Same deterministic offline route model.
- No scene traversal and no wall-clock randomness.

Validation:
- Seed `0xA17B0001`: 1,000,000 sessions, `prob_suffocated_before_wire=0.038465`, `prob_wire_starved_alive=0.001810`, `prob_survival_devalued_excess=0.153869`, wire p95 `544.360537` seconds.
- Seed `0xA17B0002`: 1,000,000 sessions, `prob_suffocated_before_wire=0.038868`, `prob_wire_starved_alive=0.001796`, `prob_survival_devalued_excess=0.152935`, wire p95 `543.020164` seconds.
- Runtime verification: PENDING VERIFICATION.

Exact microseconds saved:
- Runtime frame CPU saved: `0.000 us/frame`.
- Offline seed pack A cost: `299.297135 us/session`.
- Offline seed pack B cost: `261.126648 us/session`.
- No additional mutation accepted; ore starvation and heat failure were not observed.
