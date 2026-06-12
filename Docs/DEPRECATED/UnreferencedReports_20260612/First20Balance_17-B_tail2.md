# First 20 Minute Balance Monte Carlo - 17-B

Evidence class: OFFLINE_PYTHON_STATIC_DATA. Runtime Unity proof remains PENDING VERIFICATION.

## Authority
- Ore table: `C:/hades/Hecton8/Data/Economy/Ore_Distribution.json`
- Survival asset: `C:/hades/Hecton8/Assets/_Project/Data/Survival/Standard_Suit_V1.asset`
- Economy survival JSON: `C:/hades/Hecton8/Data/Economy/Survival_Stats.json`
- Tool heat CSV: `C:/hades/Hecton8/Data/Balance/ToolHeat.csv`

## Baseline
- runs: 1000000
- suffocated before wire: 0.038465
- wire starved alive: 0.001810
- survival devalued excess: 0.153869
- wire p95 seconds: 544.361
- oxygen p50 remaining: 0.000000

## Final
- runs: 1000000
- suffocated before wire: 0.038868
- wire starved alive: 0.001796
- survival devalued excess: 0.152935
- wire p95 seconds: 543.020
- oxygen p50 remaining: 0.000000

## Applied Changes
- none

## Scalability
- low: same authoritative tables, lower session count in CI smoke.
- middle: 100k-session validation used here.
- high: 1M-session audit acceptable when machine is idle.
- ultra: more seeds only; gameplay authority and DTO layout unchanged.
