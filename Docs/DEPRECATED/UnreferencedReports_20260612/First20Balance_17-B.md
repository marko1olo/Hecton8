# First 20 Minute Balance Monte Carlo - 17-B

Evidence class: OFFLINE_PYTHON_STATIC_DATA. Runtime Unity proof remains PENDING VERIFICATION.

## Authority
- Ore table: `C:/hades/Hecton8/Data/Economy/Ore_Distribution.json`
- Survival asset: `C:/hades/Hecton8/Assets/_Project/Data/Survival/Standard_Suit_V1.asset`
- Economy survival JSON: `C:/hades/Hecton8/Data/Economy/Survival_Stats.json`
- Tool heat CSV: `C:/hades/Hecton8/Data/Balance/ToolHeat.csv`

## Baseline
- runs: 100000
- suffocated before wire: 0.973900
- wire starved alive: 0.000000
- survival devalued excess: 0.000000
- wire p95 seconds: 189.819
- oxygen p50 remaining: 0.000000

## Final
- runs: 100000
- suffocated before wire: 0.037580
- wire starved alive: 0.001880
- survival devalued excess: 0.155260
- wire p95 seconds: 538.654
- oxygen p50 remaining: 0.000000

## Applied Changes
- `C:/hades/Hecton8/Assets/_Project/Data/Survival/Standard_Suit_V1.asset` maxOxygen: 100.0 -> 139.23999999999998
- `C:/hades/Hecton8/Assets/_Project/Data/Survival/Standard_Suit_V1.asset` oxygenConsumptionRate: 0.5 -> 0.015
- `C:/hades/Hecton8/Data/Economy/Survival_Stats.json` oxygen.base_liters_per_second: 0.1 -> 0.09
- `C:/hades/Hecton8/Data/Economy/Survival_Stats.json` oxygen.tank_capacity_liters: 600.0 -> 835.44
- `C:/hades/Hecton8/biological_metabolism_profiles.csv` Player.baseCalorieDrainPerSecond: 0.0028 -> 0.00196
- `C:/hades/Hecton8/biological_metabolism_profiles.csv` Player.baseHydrationDrainPerSecond: 0.0035 -> 0.00245

## Scalability
- low: same authoritative tables, lower session count in CI smoke.
- middle: 100k-session validation used here.
- high: 1M-session audit acceptable when machine is idle.
- ultra: more seeds only; gameplay authority and DTO layout unchanged.
