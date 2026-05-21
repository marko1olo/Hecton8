# SHINOBU_107 Remaining Burst Triage

Date: 2026-05-21
Evidence: STATIC_SOURCE / SHINOBU_140 scanner output
Report Source: `Docs/Reports/SHINOBU_107_StaticScan/SHINOBU_140_Burst_Job_Directives.json`

## Summary

Scanner artifact state after Loop 402:

- `Burst_Job_Directives`: 272
- `Compile_Wall`: 71
- `Runtime_Struct_Layout`: 9
- `totalCritical`: 352
- `totalWarnings`: 24

Remaining Burst rows split as:

- 269 rows: source already has `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard`, but the current scanner path classifier expects `FloatMode.Fast`.
- 3 rows: untracked or in-flight source uses `FloatMode.Fast` where the classifier expects deterministic mode:
  - `Assets/_Project/Scripts/Environment/Fluids/EmergencyMockOceanKinematicsAdapter.cs:67`
  - `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs:1586`
  - `Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs:1598`

## Top Remaining Deterministic Buckets

| Count | Bucket |
| ---: | --- |
| 70 | `World` |
| 37 | `Physics` |
| 23 | `AI` |
| 18 | `Gameplay` |
| 16 | `Tools` |
| 15 | `Construction` |
| 15 | `Fauna` |
| 13 | `Animation` |
| 9 | `UI` |
| 8 | `Economy` |
| 6 | `ModdingAPI` |
| 6 | `Environment` |
| 4 | `Power` |
| 3 | `Atmosphere` |
| 3 | `Audio` |
| 3 | `Ecosystem` |
| 3 | `Equipment` |
| 3 | `ModularEquipmentEngine.cs` |
| 3 | `Physiology` |

## Decision

Do not bulk-convert these 269 deterministic rows to `FloatMode.Fast` from SHINOBU_107. Many buckets are simulation, save-adjacent, physics, world-state, or other authoritative integrations. The user mandate requires deterministic mode for rollback, kinematics, and authoritative state integrations; changing those jobs to Fast from a Signal Corridor/static-gate pass would risk cross-platform state drift.

Do not blindly expand the scanner classifier across whole folders yet. Some folders also contain presentation-only jobs where Fast may be correct. A correct burn-down requires owner route proof or a file-specific owner map, not a broad folder token. Loop 399 added only exact deterministic route tokens for `Plugins/Crest/OceanKinematics` and `RadiationHazardGrid`. Loops 400-402 added only exact filename tokens documented in `SHINOBU_107_BURST_EXACT_ROUTE_AUDIT.md`, clearing deterministic-authority rows without changing domain runtime source.

## Safe Next Actions

1. Owner agents classify their jobs as `authoritative deterministic`, `kinematic deterministic`, `rollback deterministic`, or `presentation fast`.
2. SHINOBU_140 scanner should consume an explicit owner/mode map instead of path-only heuristics.
3. SHINOBU_107 can continue removing narrow dead imports and true incomplete directives, but should not rewrite other domains' deterministic math.

## Build Gate

No build was launched. `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` remains absent; build verification remains blocked by the external World source gate and CPU/process guard.
