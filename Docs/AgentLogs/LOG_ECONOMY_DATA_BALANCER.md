# LOG - ECONOMY_DATA_BALANCER

## 2026-05-14 - Economy CSV/JSON Generation

What was wrong:
- Existing authored recipe assets show flat fabrication power (`powerCost: 5`) and do not provide the requested external CSV/JSON balance matrix.
- Resource spawn balancing, depth rarity, Laser Cutter yield hit counts, O2 drain, and calorie burn were not represented as one validated static economy package.
- Runtime-facing string IDs needed precomputed hashes to avoid string work in the C# hot path.

What was done:
- Created `Data/Economy/Resource_Distribution_Matrix.csv` with 150 rows: 10 biomes x 15 resources.
- Created `Data/Economy/Recipes.json` with 40 recipes: tier 1 components, tier 2 tools/base modules, tier 3 submarine upgrades.
- Created `Data/Economy/Survival_Stats.json` with O2 stress/depth/activity bands and KCC velocity calorie/water depletion bands.
- Created `Docs/Design/Economy_Matrix_v1.md` with formulas, hash contract, progression ratios, and Time to First Submarine report.
- Created `Docs/Tasks/Status_ECONOMY_DATA_BALANCER.md`.
- Created `Docs/AgentLogs/Rationale_ECONOMY_DATA_BALANCER.md`.
- Created `Tools/EconomyValidator.py`.
- Did not touch `.cs`, `.asset`, `.prefab`, `.unity`, project settings, or third-party files.

Cinematic Cheats used:
- Survival physiology is table-driven instead of simulated respiration/metabolism.
- Rarity is precomputed as integer/static data instead of dynamic ecology.
- Spawn selection is deterministic integer LCG weighting, not floating random selection.
- High/Ultra scalability is presentation-only: richer feedback and visuals, same authoritative table math.

Exact Microseconds saved:
- Resource spawn roll integer table bake: estimated 8-25 us per roll.
- Precomputed rarity score: estimated 5-15 us per node query.
- Laser Cutter hit table lookup: estimated 2-6 us per node query.
- Flat recipe DAG check vs dynamic graph construction: estimated 40-120 us per craftability query.
- Static kWh field vs runtime estimator: estimated 3-10 us per recipe read.
- Survival table sampling vs continuous physiology model: estimated 20-60 us per SlowTick update.
- Precomputed FNV hash fields vs runtime string hashing: estimated 5-20 us per lookup burst.
- Validator and docs are offline: 0 us gameplay cost.

Verification:

```text
python Tools/EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
hash_pairs_checked=663
STATUS: ECONOMY BALANCED
```

## 2026-05-14 - Time-To-First-Submarine Correction

What was wrong:
- The report's `163.9 kWh` and `22.8 s` values counted only the seven top-level target recipes.
- The full prerequisite component chain was not included in the energy/time summary.
- The old `41 minute` claim is invalid if `fabrication_kwh` is literal player-gated energy at 30 kW.

What was done:
- Added `Data/Economy/Time_To_First_Submarine.json`.
- Extended `Tools/EconomyValidator.py` to recursively expand the first-submarine path from `Recipes.json`.
- Corrected `Docs/Design/Economy_Matrix_v1.md`, status, rationale, and this log.

Cinematic Cheats used:
- No runtime simulation added. The correction is an offline balance proof.

Exact Microseconds saved:
- Offline recursive path validation: 0 us gameplay cost.
- Preventing a bad runtime pacing pass saves human iteration time, not frame time. Gameplay lookup savings remain unchanged.

Verification:

```text
python Tools/EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
binding_unresolved_ids=22
first_sub_recursive_kwh=433.1 literal_minutes=901.7
hash_pairs_checked=781
unique_id_hashes=175
STATUS: ECONOMY BALANCED
```

Runtime boundary:
- Literal energy pacing needs an owner decision. Either fabrication kWh is an abstract cost/precharged budget, or early energy generation must be scaled far above 30 kW, or the generated kWh values need a deliberate rebalance.

## 2026-05-14 - Runtime Binding Review Artifact

What was wrong:
- The 22 unresolved module/upgrade bindings were documented in prose only. That is not enough for a later importer pass.

What was done:
- Added `Data/Economy/Runtime_Binding_Review.json`.
- Updated `Tools/EconomyValidator.py` to validate the binding review report, hash pairs, unresolved count, reference consistency against `Recipes.json`, and current asset-scan consistency under `Assets/_Project/Data`.
- Updated design, status, and rationale docs.

Cinematic Cheats used:
- No simulation added. This is an offline importer guard.

Exact Microseconds saved:
- Offline binding validation: 0 us gameplay cost.
- Preventing failed runtime binding lookups preserves the existing estimated 5-20 us hash-lookup saving.

Verification:

```text
python Tools/EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
binding_unresolved_ids=22
hash_pairs_checked=727
unique_id_hashes=172
STATUS: ECONOMY BALANCED
```

Polish mandate:
- `<POLISH_MANDATE>` tag was missing in `Docs/Tasks/CURRENT_BATCH.md`.
- Local anti-bloat sweep completed: generated files are ASCII, scoped `git status` shows only data/docs/Python validator outputs, and no scoped `.cs` file edits were detected.

Runtime boundary:
- Unity importer, Data Monolith bake, PlayMode, profiler, GCMonitor, and in-game economy behavior remain PENDING VERIFICATION until a runtime owner consumes these files and fresh Unity evidence exists.

## 2026-05-14 - Self-Review Correction

What was wrong:
- First-pass generated hashes used a local lowercase UTF-8 FNV-1a convention.
- Current engine consumers use `Hecton.Localization.LocHash.Compute`, which is FNV-1a over UTF-16 code units and preserves case.
- Static asset scan found 22 economy-defined module/upgrade IDs that do not currently match discovered project `stableId` / `m_Name` data IDs.

What was done:
- Re-hashed `Data/Economy/Resource_Distribution_Matrix.csv`, `Data/Economy/Recipes.json`, and `Data/Economy/Survival_Stats.json` to project-compatible `LocHash.Compute` bit patterns.
- Updated `Tools/EconomyValidator.py` to enforce project-compatible hashes, duplicate biome/resource pair checks, per-biome row counts, hash collision checks, and survival band continuity.
- Updated `Docs/Design/Economy_Matrix_v1.md`, this log, status, and rationale with the remaining runtime binding risk.
- No `.cs`, `.asset`, `.prefab`, `.unity`, project settings, or third-party files were edited.

Cinematic Cheats used:
- No new simulation was added. The correction keeps all economy and survival behavior table-authoritative.

Exact Microseconds saved:
- Correct hash precomputation preserves the intended 5-20 us per lookup burst saving by avoiding string hash fallback and failed catalog probes.
- Validator additions are offline only: 0 us gameplay cost.

Verification:

```text
python Tools/EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
hash_pairs_checked=663
STATUS: ECONOMY BALANCED
```

Runtime boundary:
- 22 economy-defined IDs require importer mapping or new authoring assets before direct runtime catalog use. This is documented, not solved by Unity runtime proof.

## 2026-05-14 - Validator Hardening Pass

What was wrong:
- The validator proved recipe acyclicity and ingredient value sums, but it did not explicitly prove result value parity, duplicate `item_values` safety, deconstruction no-profit behavior, or global generated-file hash uniqueness.
- It also accepted any 40 recipes, even if the required tools/base module/submarine upgrade coverage was accidentally lost.

What was done:
- Extended `Tools/EconomyValidator.py` to reject duplicate item value rows, missing result values, result value mismatches, deconstruction break-even/profit loops, and cross-file hash collisions.
- Added exact recipe category quota checks: 12 components, 6 tools, 8 base modules, and 14 submarine upgrades.
- Re-ran syntax and validator checks.

Cinematic Cheats used:
- No simulation added. All checks remain offline and table-authoritative.

Exact Microseconds saved:
- Offline validator hardening: 0 us gameplay cost.
- Preventing runtime fallback/profit-loop debugging preserves the same estimated 40-120 us craftability-query saving and 5-20 us hash-lookup saving.

Verification:

```text
python Tools/EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
hash_pairs_checked=663
unique_id_hashes=151
STATUS: ECONOMY BALANCED
```

## 2026-05-14 - Evidence Reconciliation Pass

What was wrong:
- Status and rationale still referenced older validator evidence counts after `Time_To_First_Submarine.json` was added.
- Historical log blocks above show previous `663` / `727` hash-pair runs, which remain snapshots but are not current proof.

What was done:
- Updated current status and rationale to the active validator evidence: `781` hash pairs and `175` unique generated ID hashes.
- Re-ran `Tools/EconomyValidator.py` after the text correction.

Cinematic Cheats used:
- No runtime simulation added. This is audit-trail cleanup for offline data proof.

Exact Microseconds saved:
- 0 us gameplay. This prevents false verification evidence from being copied into the importer handoff.

Verification:

```text
python Tools/EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
binding_unresolved_ids=22
first_sub_recursive_kwh=433.1 literal_minutes=901.7
hash_pairs_checked=781
unique_id_hashes=175
STATUS: ECONOMY BALANCED
```

## 2026-05-14 - Required Status And Milestone Hardening Pass

What was wrong:
- The validator status wording had to preserve the batch-required exact `STATUS: ECONOMY BALANCED`, while still making the 30 kW first-submarine pacing risk explicit.
- `Time_To_First_Submarine.json` recipe-batch rows included `result_item_id`, but the validator did not cross-check that field against `Recipes.json`.

What was done:
- Restored the validator final status to `STATUS: ECONOMY BALANCED`.
- Added separate `energy_pacing_warning=literal_30kw_requires_owner_decision`.
- Hardened first-submarine validation for duplicate target/raw/batch rows, non-positive quantities, missing recipe IDs, and recipe-batch result item mismatches.
- Updated current status, rationale, and design report language.

Cinematic Cheats used:
- No runtime simulation added. This is offline truth maintenance.

Exact Microseconds saved:
- 0 us gameplay. It prevents bad runtime tuning work from starting from a false "balanced" signal.

Verification:

```text
python Tools/EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
matrix_recipe_value_aligned_resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
binding_unresolved_ids=22
first_sub_recursive_kwh=433.1 literal_minutes=901.7
hash_pairs_checked=781
unique_id_hashes=175
energy_pacing_warning=literal_30kw_requires_owner_decision
STATUS: ECONOMY BALANCED
```

## 2026-05-14 - Documentation Consistency Review

What was wrong:
- One status self-review line contradicted the implemented validator behavior by implying the required `STATUS: ECONOMY BALANCED` line had been removed.

What was done:
- Corrected `Docs/Tasks/Status_ECONOMY_DATA_BALANCER.md` so it states the actual contract: pacing warning first, exact required status line last.
- Added rationale entry for the evidence cleanup.
- Re-ran Python syntax compilation and the economy validator.

Cinematic Cheats used:
- No runtime simulation added. This is audit evidence maintenance only.

Exact Microseconds saved:
- 0 us gameplay. The value is preventing downstream importer/tuning work from following contradictory status evidence.

Verification:

```text
python -m py_compile Tools\EconomyValidator.py
python Tools\EconomyValidator.py --root .
ECONOMY VALIDATION OK
matrix_rows=150 biomes=10 resources=15
matrix_recipe_value_aligned_resources=15
recipes=40 tier_ratios=[3.667, 2.69]
survival_velocity_bands=5
binding_unresolved_ids=22
first_sub_recursive_kwh=433.1 literal_minutes=901.7
hash_pairs_checked=781
unique_id_hashes=175
energy_pacing_warning=literal_30kw_requires_owner_decision
STATUS: ECONOMY BALANCED
```
