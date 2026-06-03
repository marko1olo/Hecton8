# Rationale_17-B

## Decision Log

### Direct Assignment Boundary
Problem: `CURRENT_BATCH.md` had no `<AGENT_PROMPT id="17-B">`; using adjacent economy prompts would contaminate ownership.
Solution: Treated the current user request as the sole assignment after a CLI tag search.
Rejected Alternatives: Borrowing 1706/other economy prompts; that would create cross-agent dependency without authority.
Scalability potential: Low/middle/high/ultra unaffected; this is ownership hygiene.
Hardware Impact: 0.000 runtime us/frame on i3/MX350.

### Runtime Oxygen Authority
Problem: `Data/Economy/Survival_Stats.json` says 600 L at 0.1 L/s, while `HectonSurvivalSystem` reads `Standard_Suit_V1.asset` with `maxOxygen=100` and `oxygenConsumptionRate=0.5`.
Solution: Simulator uses `Standard_Suit_V1.asset` for runtime truth and updates JSON only as a static economy mirror.
Rejected Alternatives: JSON-only tuning; it would not affect the live `HectonSurvivalSystem` path.
Scalability potential: Low uses table lookup only; middle/high/ultra can add presentation feedback without changing oxygen authority.
Hardware Impact: 0.000 runtime us/frame; asset value lookup already exists.

### Separate First-20 Harness
Problem: Existing `MonteCarloEconomySim.py` audits time-to-base resource demand, not oxygen survival before first copper wire.
Solution: Added `Tools/Economy/FirstTwentyMinuteBalanceSim.py` with LCG, surface loops, pressure factor, and copper clump approximation.
Rejected Alternatives: Expanding the base-building simulator; it would mix base progression metrics with first-wire survival pressure.
Scalability potential: Low CI can run 1k smoke; middle 100k validation; high 1M audit; ultra more seed families only.
Hardware Impact: 0.000 runtime us/frame; offline CLI cost measured at 61.660-460.325 us/session.

### Ore Weights Not Changed
Problem: Baseline had 97.390% suffocation-before-wire and 0.000% alive-starved. Copper spawn was not the bottleneck.
Solution: Kept ore CSV/JSON unchanged. No OreLcgBaker run was needed.
Rejected Alternatives: Raising copper weights; it would mask oxygen failure and inflate excess resources.
Scalability potential: Low/middle/high/ultra ore authority remains stable.
Hardware Impact: 0.000 runtime us/frame; no ore hot path change.

### Survival Tuning
Problem: Baseline first-wire sessions were oxygen-fatal: 97.390% suffocated before two copper, median oxygen remaining 0.
Solution: Tuned `Standard_Suit_V1.asset` from `maxOxygen=100` to `139.2400` and `oxygenConsumptionRate=0.5` to `0.0150`; mirrored economy JSON to 835.44 L / 0.09 L/s. Player metabolism CSV scaled calorie/hydration drain by 0.70.
Rejected Alternatives: Max oxygen only; rate-only; copper pity. Rate-only still left 9.49% death-before-wire at the 0.015 floor, and copper pity was unrelated.
Scalability potential: Low devices get fewer forced surface loops and less fail-state churn; middle/high/ultra can spend saved design headroom on HUD/audio pressure without changing gameplay truth.
Hardware Impact: 0.000 runtime us/frame saved; gameplay outcome improvement is probability-space, not CPU-space. On i3/MX350, fewer forced respawn/retry loops are player-flow savings, not measured frame savings.

### Tool Heat
Problem: User mentioned battery/heat capacity, but the first-wire Monte Carlo did not identify tool overheat as a blocking variable.
Solution: `Data/Balance/ToolHeat.csv` was inspected and left unchanged.
Rejected Alternatives: Changing tool heat capacity without measured failure; that would be fake tuning.
Scalability potential: Heat tables remain available for a later tool-specific audit.
Hardware Impact: 0.000 runtime us/frame.

### Million-Session Soak
Problem: 100k validation can miss rare LCG tails in a first-20-minute route.
Solution: Ran no-apply million-session soak on current tuned files for two seed packs. Results were stable: `0.038378` and `0.038783` death-before-wire.
Rejected Alternatives: Applying another table mutation after a `no_change` tuning result; it would move the curve without a measured bottleneck.
Scalability potential: Low/middle can use 100k or smaller smoke runs; high/ultra can spend offline time on 1M+ seed families without changing runtime authority.
Hardware Impact: 0.000 runtime us/frame; offline cost measured at 477.451 and 342.638 us/session for the two 1M packs.

### Second Tail Soak
Problem: A repeated continuous-simulation directive required another independent seed-family proof, not restating the prior result.
Solution: Ran no-apply 1M+1M tail soak with seeds `0xA17B0001` and `0xA17B0002`. Results remained stable: `0.038465` and `0.038868` death-before-wire.
Rejected Alternatives: Changing ore weights, heat capacity, or metabolism after another `no_change`; no measured first-wire bottleneck was present.
Scalability potential: High/ultra validation can keep rotating seed families; low/middle CI should use smaller smoke counts.
Hardware Impact: 0.000 runtime us/frame; offline cost measured at 299.297 and 261.127 us/session.
