# PROJECT_AUDIT Unity Time Triage

Date: 2026-05-21
Evidence class: STATIC_SOURCE / STATIC_TOOL only. No Unity import, compile, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or device proof was executed.

## Source

- Tool: `Tools/PolishMandateStaticAudit.py`
- JSON artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_risk_buckets.json`
- Markdown artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_risk_buckets.md`
- Command: `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_risk_buckets.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_risk_buckets.md`

## Raw Count Preservation

The raw Unity time warning class is:

- `unityTimeCritical`: 962 matches / 261 files

Additive time-kind buckets:

- `unityTimeFrameCount`: 842
- `unityTimeWallClock`: 118
- `unityTimeDelta`: 2
- Sum: 962

Additive build-surface buckets:

- `unityTimeBuildPlayerRuntime`: 925
- `unityTimeBuildEditorOnly`: 14
- `unityTimeBuildQaDevProof`: 23
- Sum: 962

Additive primary-risk buckets:

- `unityTimeRiskFrameStampOrTelemetry`: 806
- `unityTimeRiskGameplayWallClock`: 80
- `unityTimeRiskCooldownOrPerfLog`: 38
- `unityTimeRiskEditorOrProof`: 37
- `unityTimeRiskGameplayDelta`: 1
- Sum: 962

## Interpretation

The previous `unityTimeCritical=964` number was too blunt. Most hits are `Time.frameCount`, usually frame stamps, signal payload stamps, blackbox entries, warning cooldowns, or telemetry descriptors. They still need owner-phase route review, but they are not the same risk as simulation integration using `Time.deltaTime`.

The serious current gameplay-delta debt is now isolated to one player-runtime row:

| File | Line | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs` | 616 | Visual shoreline foam decay uses `Time.deltaTime`; likely presentation-only, but still not dispatcher-owned. |

Two previous gameplay-delta rows were removed:

- `FaunaBrain.TryResolvePredatorLungeCcdPosition()` now uses the last dispatcher `FixedTick(float fdt)` value instead of `Time.fixedDeltaTime`.
- `SubmarineFluidDynamics.UpdateBrineHullBreachState()` now uses `_currentFixedDeltaTime`, already assigned from dispatcher `FixedTick(float fixedDeltaTime)`.

The remaining high-volume risk is not `deltaTime`; it is player-runtime wall-clock ownership:

| File | Wall-clock rows | Static meaning |
|---|---:|---|
| `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` | 20 | Destruction/regrowth/fade timers appear to use `Time.time`; needs owner-tick conversion or proof that they are presentation-only. |
| `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs` | 4 | Timeline seconds derive from `Time.time`; needs simulation clock route review. |
| `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | 4 | Some combat/death presentation timers still use wall clock. |
| `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` | 4 | Presentation/UI timing likely, but should be isolated from gameplay truth. |
| `Assets/_Project/Scripts/HectonBoidController.cs` | 3 | Acoustic ping/presentation timing uses wall clock; needs gameplay vs visual separation. |

## Safe Next Actions

1. Do not mass-replace `Time.frameCount`; first classify whether the value is a blackbox stamp, signal frame, dispatcher frame mirror, or gameplay authority.
2. Convert remaining `Time.deltaTime` only when the caller has a dispatcher `dt` route. For presentation-only VFX, document it as non-authoritative or route through visual frame timing.
3. For `Time.time`, split presentation cooldowns from gameplay truth. Gameplay timers should use owner-local accumulated dispatcher seconds or lockstep tick counters.
4. `DestructibleOrganicManager` is the next real wall-clock owner to inspect because it owns destruction/regrowth facts and also appears in private-native ownership debt.

## 2026-05-22 Organic Clock Follow-Up

`DestructibleOrganicManager` has now been migrated off `Time.time` for owner-state timing. It uses a local organic clock advanced through dispatcher `Tick(float deltaTime)` and feeds that value into corpse expiry, decomposition, wilt, touch, overgrowth, mature spore cadence, damage visuals, and Dear Lie regeneration.

Focused proof:

- `rg -n "Time\.time" Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` returns no rows.
- `rg -n "Time\.fixedDeltaTime|Time\.deltaTime" Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` returns no rows.
- Broad artifact: `Docs/Reports/PROJECT_AUDIT_polish_time_after_organic_clock.json`.

Updated static counts from that artifact:

- `unityTimeCritical`: 940
- `unityTimeWallClock`: 97
- `unityTimeRiskGameplayWallClock`: 60
- `unityTimeRiskGameplayDelta`: 1

Interpretation: the highest-priority wall-clock owner from the first time triage is no longer on Unity wall clock. Remaining wall-clock work should move to `MigrationDirector`, `FaunaBrain`, `SpectrumSystem`, and `HectonBoidController` after local owner-route inspection.
