# COMPUTE ENERGY EQUIVALENTS

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T19:10+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Input value: 2,297.33 MWh

## Boundary

This is not measured OpenAI datacenter telemetry. It is a translation of the audit-model energy number derived from the prompt constant:

`0.05 kWh / 1,000 tokens`

The specific input here is `2,297.33 MWh`, previously attached to the live token ledger. The purpose is scale comprehension.

## Direct Conversion

| Unit | Value |
|---|---:|
| MWh | 2,297.33 |
| GWh | 2.29733 |
| kWh | 2,297,330 |
| Joules | 8,270,388,000,000 |
| Gigajoules | 8,270.39 |
| Terajoules | 8.270 |

## Human-Scale Equivalents

Assumptions are explicit and approximate.

| Equivalent | Assumption | Value |
|---|---:|---:|
| Household days | 30 kWh/day | 76,577.7 home-days |
| Household years | 30 kWh/day | 209.8 home-years |
| Household months | 900 kWh/month | 2,552.6 home-months |
| EV full charges | 75 kWh battery | 30,631.1 charges |
| 100 W bulb | 0.1 kW continuous | 22,973,300 hours |
| 100 W bulb | 0.1 kW continuous | 2,622.5 years |
| 10 W LED bulb | 0.01 kW continuous | 229,733,000 hours |
| 10 W LED bulb | 0.01 kW continuous | 26,225.2 years |
| 60 W laptop | 0.06 kW continuous | 38,288,833 hours |
| 60 W laptop | 0.06 kW continuous | 4,370.9 years |
| 500 W workstation/server | 0.5 kW continuous | 4,594,660 hours |
| 500 W workstation/server | 0.5 kW continuous | 524.5 years |
| 1 MW load | 1 MW continuous | 2,297.33 hours |
| 1 MW load | 1 MW continuous | 95.72 days |

## Electricity Cost Scenarios

These are tariff scenarios only, not an invoice.

| Electricity price | Cost |
|---:|---:|
| USD 0.05 / kWh | USD 114,866.50 |
| USD 0.10 / kWh | USD 229,733.00 |
| USD 0.15 / kWh | USD 344,599.50 |
| USD 0.30 / kWh | USD 689,199.00 |

## Plain-English Scale

2,297.33 MWh is about:

- 2.30 GWh.
- 2.30 million kWh.
- 210 years of one 30 kWh/day household.
- 30.6k full 75 kWh EV charges.
- A 1 MW industrial load running continuously for 95.7 days.

## Verdict

The energy number is large enough to be legible at grid scale, but it is model-derived, not measured. The honest label is:

`audit-model energy equivalent: 2.30 GWh`

Do not call it actual OpenAI power consumption without datacenter telemetry.
