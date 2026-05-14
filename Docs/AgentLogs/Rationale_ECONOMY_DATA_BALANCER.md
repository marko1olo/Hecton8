# Rationale - ECONOMY_DATA_BALANCER

Date: 2026-05-14
Domain: Auxiliary Node, text/data only
Prompt: ECONOMY_DATA_BALANCER
Status: ECONOMY BALANCED by validator; literal 30 kW energy pacing and Unity runtime integration remain PENDING VERIFICATION.

## Decision 1 - Data-Only Boundary

Problem: The prompt requires economy output, but the project already has live Unity `RecipeData`, `ItemData`, and resource-node assets. Editing `.asset` YAML would cross into Unity asset mutation without scene/import proof.

Solution: Generated standalone static data under `Data/Economy` and did not touch `.cs`, `.asset`, `.prefab`, `.unity`, project settings, or third-party files. The C# Data Monolith can consume/bake these files later.

Rejected Alternatives: Direct YAML edits to `Assets/_Project/Data/Crafting/Recipes/*.asset` were rejected because raw Unity YAML mutation is fragile and current recipes are runtime-authoring assets, not the requested CSV/JSON output.

Scalability potential: Low uses baked tables; Middle can bake compact binary blobs; High can add richer economy telemetry; Ultra can add visual overkill through richer loot presentation without changing authoritative data.

Hardware Impact: Low-end i3/MX350 avoids runtime string parsing and asset traversal. Estimated runtime lookup saving: 25-80 us per economy query if importer bakes IDs to integer tables.

## Decision 2 - FNV-1a Hash Contract

Problem: The prompt requires every item, biome, and recipe to include a precomputed 32-bit FNV-1a hash. Self-review found the first pass used lowercase UTF-8 hashes, but active project consumers call `Hecton.Localization.LocHash.Compute`, which hashes UTF-16 code units in case-preserving order.

Solution: Re-hashed all generated CSV/JSON `hash32` fields to match `LocHash.Compute` bit patterns and updated `Tools/EconomyValidator.py` to enforce the same algorithm. `Data_TitaniumScrap` now stores unsigned `3511699502`, matching the signed runtime pattern `-783267794`.

Rejected Alternatives: Keeping the local lowercase UTF-8 convention was rejected because `ItemCatalog.FindByHash` and related runtime paths would miss generated IDs. Signed int-only hashes were rejected because JSON/CSV consumers can cast unsigned `uint` deterministically.

Scalability potential: Low bakes IDs into contiguous arrays; Middle keeps a debug ID map out of gameplay; High/Ultra can retain editor display names for tooling only.

Hardware Impact: MX350-class CPU avoids string hashing during recipe/resource checks. Estimated hot-path saving: 5-20 us per lookup burst, depending on table size and cache state.

## Decision 3 - Resource Set And Quartz Mapping

Problem: Prompt examples include Quartz, but current project content uses `Data_SilicaShards`, glass panels, and silica resource nodes instead of a `Data_Quartz` authority asset.

Solution: Used 15 current-compatible resources and mapped Quartz demand to `Data_SilicaShards` in the design document. Matrix has 10 biomes x 15 resources = 150 rows.

Rejected Alternatives: Inventing `Data_Quartz` was rejected because it would create a parallel item ID that does not appear in current ItemData/resource-node reality.

Scalability potential: Low uses 15-resource tables; Middle can add zone overrides; High/Ultra can add richer visual node variants while keeping the same resource IDs.

Hardware Impact: Avoiding a new item family prevents extra importer/catalog ambiguity. Estimated saving: 10-30 us per validation/import pass and zero extra runtime item branch.

## Decision 4 - Three-To-One Crafting Ladder

Problem: Existing Unity recipe assets mostly have flat `powerCost: 5`, so they do not encode meaningful progression cost or fabrication energy.

Solution: Generated 40 recipes with tier medians 4.35, 15.95, and 42.90 baseline units. Validator accepts tier ratios in `[2.4, 3.8]` and computed ratios are 3.667 and 2.690.

Rejected Alternatives: Exact 3.000 ratios per item were rejected because real recipes need mixed resource shapes for tools, base modules, and submarine upgrades. Flat energy cost was rejected as the known defect.

Scalability potential: Low uses simple recipe costs; Middle can apply scarcity inflation; High can add linked-network ingredient availability; Ultra can spend saved CPU on richer fabricator visuals and output staging.

Hardware Impact: Flat DAG recipe checks avoid recursive runtime solver work. Estimated hot-path saving versus dynamic graph construction: 40-120 us per craftability query.

## Decision 5 - Table-Driven Survival Metrics

Problem: O2, pressure, calories, and water drain can become fake simulation debt if modeled continuously without gameplay benefit.

Solution: Authored exact O2/stress/depth/activity bands and KCC velocity calorie bands in `Survival_Stats.json`. Fast swim is exactly 3x slow swim calories.

Rejected Alternatives: Simulated respiration, tissue metabolism, or per-frame chemistry was rejected under the visual-fake-first and frame-time mandates. It would not improve gameplay correctness.

Scalability potential: Low uses direct table lookup; Middle interpolates; High adds 2-second smoothing for presentation; Ultra adds HUD/audio intensity but keeps the same gameplay values.

Hardware Impact: SlowTick table sampling avoids continuous physiology math. Estimated saving: 20-60 us per survival update compared with multi-factor per-frame simulation.

## Decision 6 - Validator As Proof Boundary

Problem: Static data can silently rot: missing hashes, recipe cycles, resource row count drift, or syntax errors would not be caught by Unity compile.

Solution: Added `Tools/EconomyValidator.py` to parse all generated files and check JSON syntax, project-compatible FNV hashes, global generated-file hash collisions, resource matrix dimensions, duplicate biome/resource rows, positive LCG weights, exact recipe category quotas, recipe cycles, value math, result value parity, no-profit deconstruction, tier ratios, and survival burn rules.

Rejected Alternatives: Manual inspection was rejected because it cannot prove 781 hash pairs or graph acyclicity. Unity C# validator was rejected because prompt explicitly forbids C# work.

Scalability potential: Low uses this script in CI; Middle adds binary bake checks; High/Ultra can add simulation path timing without changing static data.

Hardware Impact: Validation runs offline, so gameplay cost is 0 us. It prevents importer failures and economy loop defects that would otherwise waste runtime debugging time.

## Decision 7 - Self-Review Corrections

Problem: Review against current project data found two integration risks after the first pass: the hash convention mismatch described above, and 22 recipe output IDs that are economy-defined rather than current `ItemData` / `BuildableData` stable IDs.

Solution: Corrected the hash convention in the generated data and validator. Documented the 22 economy-defined IDs in `Docs/Design/Economy_Matrix_v1.md` as requiring importer mapping or new authoring assets before direct `ItemCatalog` use. Did not rename them blindly because the prompt explicitly asks for base module and submarine upgrade economy targets, and current runtime data models many of those as components or construction buildables with different semantics.

Rejected Alternatives: Replacing every `Upgrade_*` with `Comp_*` was rejected because it would collapse the requested submarine-upgrade layer into component crafting without importer confirmation. Mapping every `Module_*` to existing `Build_*` IDs was rejected because several requested module kits do not have exact current construction stable IDs.

Scalability potential: Low keeps static economy-defined IDs and importer maps them once; Middle can add explicit binding tables; High/Ultra can add richer UI names and visual unlock staging without changing runtime lookup hashes.

Hardware Impact: Import-time binding avoids runtime string fallback or failed hash lookup retries. Estimated saving preserved: 5-20 us per lookup burst; static asset scan cost is offline only.

## Decision 8 - Machine-Readable Binding Review

Problem: The unresolved 22 module/upgrade IDs were documented in prose only. That is insufficient for the importer owner because prose can be missed or miscopied.

Solution: Added `Data/Economy/Runtime_Binding_Review.json` with `LocHash.Compute` hashes, unresolved economy IDs, candidate existing IDs where a close match exists, and notes describing semantic mismatch. Updated `Tools/EconomyValidator.py` to validate the binding report, re-scan current `Assets/_Project/Data` asset IDs, and include binding hashes in the total hash check.

Rejected Alternatives: Treating candidate component/buildable IDs as final mappings was rejected because component items and submarine upgrades are not the same runtime contract. Leaving the risk only in markdown was rejected because the C# importer needs structured data.

Scalability potential: Low uses this file as an import gate; Middle can add explicit binding tables; High/Ultra can add richer unlock visualization after mappings are approved.

Hardware Impact: The binding review is offline-only. It prevents failed runtime hash lookups and string fallback paths. Estimated preserved saving: 5-20 us per lookup burst.

## Decision 9 - Time-To-First-Submarine Correction

Problem: The report's `163.9 kWh` and `22.8 s` values were top-level target recipe totals only. They did not include prerequisite component recipes. The raw resource expansion was correct, but the energy/time summary was misleading.

Solution: Added `Data/Economy/Time_To_First_Submarine.json` and extended `Tools/EconomyValidator.py` to recursively expand the seven target items from `Recipes.json`. The validated full path is 46 recipe batches, 17 unique recipes, `433.1 kWh`, and `81.3 s` machine craft time. At a literal 30 kW source, static pathing plus energy wait is `901.7 minutes`.

Rejected Alternatives: Keeping the old `41 minute` report was rejected because it only makes sense when fabrication energy is precharged, abstracted, or supplied by a much stronger source. Silently lowering all kWh costs was rejected because that would change balance data without a runtime energy owner confirming the intended pacing model.

Scalability potential: Low can treat fabrication energy as abstracted authoring cost; Middle can gate energy by base battery state; High/Ultra can use richer charging VFX and fabricator staging after the energy model is approved.

Hardware Impact: The correction is offline-only, 0 us gameplay. It prevents a runtime tuning failure where players would wait hours under literal 30 kW energy gating.

## Decision 10 - Required Status String And Milestone Row Hardening

Problem: The validator must preserve the batch-required exact `STATUS: ECONOMY BALANCED` line, but the literal 30 kW first-submarine pacing remains a separate owner decision. The first-submarine report also had redundant `result_item_id` fields in recipe batch rows that were hashed but not cross-checked against `Recipes.json`.

Solution: Restored the final validator status to exact `STATUS: ECONOMY BALANCED` and prints `energy_pacing_warning=literal_30kw_requires_owner_decision` as a separate line. Hardened `validate_first_submarine_path` to reject duplicate target/raw/batch rows, non-positive quantities, missing recipe IDs, and recipe-batch result item mismatches.

Rejected Alternatives: Replacing the required status string was rejected because it violates the batch prompt. Hiding the 30 kW warning was rejected because it overstates runtime pacing readiness. Trusting the redundant report fields was rejected because a machine-readable handoff must fail on internal contradictions.

Scalability potential: Low can still consume validated static tables; Middle/High/Ultra energy pacing remains an explicit design/runtime owner decision instead of hidden validator optimism.

Hardware Impact: Offline-only validation, 0 us gameplay. It prevents incorrect importer or tuning work from treating a known energy-pacing issue as solved.

## Decision 11 - Documentation Evidence Consistency Review

Problem: The status file contained one contradictory self-review line implying the required `ECONOMY BALANCED` status had been removed, while the batch-required and implemented behavior is to print a separate pacing warning before exact `STATUS: ECONOMY BALANCED`.

Solution: Corrected the status wording and preserved the validator behavior: `energy_pacing_warning=literal_30kw_requires_owner_decision` followed by `STATUS: ECONOMY BALANCED`.

Rejected Alternatives: Leaving the contradiction was rejected because downstream agents would receive mixed instructions. Changing the validator status line again was rejected because it would violate the batch-required output string.

Scalability potential: Low/Middle/High/Ultra all consume the same validated static economy tables; the warning remains an owner decision gate for runtime energy pacing rather than a hidden data failure.

Hardware Impact: Documentation-only correction, 0 us gameplay. It prevents wasted importer/tuning work caused by contradictory proof text.

## Decision 12 - Negative Validator Proof

Problem: A green validator run proves the current files pass, but it does not prove the new failure gates reject malformed data. The first-submarine and matrix-alignment checks needed direct negative proof.

Solution: Executed validator functions against temporary copies outside project data. Mutations covered a forged first-submarine `result_item_id` with a corrected hash, a duplicated first-submarine raw resource row, and a matrix/recipe resource value drift. All three malformed cases failed as expected.

Rejected Alternatives: Editing project data to force failures was rejected because it would churn authoritative files. Trusting the happy path alone was rejected because it would not prove the guardrails.

Scalability potential: Low/Middle/High/Ultra benefit from the same offline importer gate. Runtime data remains flat and validated before any bake.

Hardware Impact: Temporary validation only, 0 us gameplay. It prevents malformed static data from reaching runtime tables where diagnosis would cost more engineering time.

## Decision 13 - Reproducible Negative Validator Mode

Problem: Negative validator proof existed in the log, but the proof was not directly reproducible by the next agent without retyping a temporary Python harness.

Solution: Added an opt-in `--negative-tests` mode to `Tools/EconomyValidator.py`. It copies economy files to a temporary directory, injects three malformed cases, confirms they fail with expected messages, prints `negative_cases=3`, and still leaves exact `STATUS: ECONOMY BALANCED` as the final output line.

Rejected Alternatives: Keeping the one-off harness was rejected because log-only proof decays. Running negative tests by default was rejected because normal CI/importer checks should stay short and stable unless strict proof is requested.

Scalability potential: Low uses normal validation; Middle/High/Ultra or CI can enable `--negative-tests` before an importer bake. Runtime tables remain unchanged.

Hardware Impact: Offline-only proof, 0 us gameplay. It prevents malformed static data from reaching runtime where diagnosis would cost more engineering time.

## Decision 14 - Exact UTF-16 Hash Iteration

Problem: `fnv1a32` claimed to match `LocHash.Compute` over UTF-16 code units, but the Python helper iterated Unicode code points and split them into two bytes. That is equivalent for ASCII/BMP IDs, but it is not exact for non-BMP characters.

Solution: Changed `fnv1a32` to iterate `value.encode("utf-16le")` bytes directly and added silent sentinel checks for `Data_TitaniumScrap`, `emoji_contract_probe`, and a non-BMP `LocHashProbe_` string built with `chr(0x1F600)`. Existing generated IDs are ASCII, so stored hash values remain unchanged; `Data_TitaniumScrap` still validates as unsigned `3511699502`.

Rejected Alternatives: Keeping the code-point loop was rejected because the function documentation and design report explicitly say UTF-16 code units. Re-hashing data was rejected because no generated ID changed under the exact implementation. Printing sentinel rows during normal validation was rejected because downstream checks require concise stable output.

Scalability potential: Low/Middle/High/Ultra all keep the same baked integer IDs. The improvement prevents future localization/tooling IDs with non-BMP characters from silently using a different hash than C#.

Hardware Impact: Offline validator only, 0 us gameplay. Runtime lookup savings remain the same because baked hash values are unchanged.

## Decision 15 - Robust Negative Mutation Selection

Problem: The `--negative-tests` result-item mismatch case used a fixed replacement ID. It worked against the current first-submarine report, but a future recipe-order change could make the fixed ID equal to the selected row and accidentally weaken the negative proof.

Solution: Added `choose_distinct_negative_item_id()` and a recipe-batch presence guard. The mutation now chooses a deterministic replacement from known resource IDs only if it differs from the current row, then recalculates the matching `LocHash.Compute` hash so the failure remains specifically a result-item mismatch.

Rejected Alternatives: Leaving the fixed ID was rejected because it depends on current row ordering. Random replacement was rejected because validation proof must be deterministic.

Scalability potential: Low/Middle/High/Ultra all use the same offline validation gate. CI can run `--negative-tests` without flaky data mutation behavior.

Hardware Impact: Offline-only validation, 0 us gameplay. It keeps malformed-data proof reliable before importer/runtime bake work.

## Decision 16 - Controlled Negative Mutator Failures

Problem: The `--negative-tests` mutators assumed first-submarine `recipe_batches` and `raw_resources` rows existed. If those sections were missing or empty, strict mode could throw a raw Python `KeyError` or `IndexError` instead of the validator's controlled failure format.

Solution: Added explicit `require()` guards in `mutate_first_sub_result_item()` and `mutate_first_sub_duplicate_raw()` so missing rows fail as `ECONOMY VALIDATION FAILED: ...` with deterministic messages.

Rejected Alternatives: Leaving raw Python exceptions was rejected because this tool is an importer gate and must report data failures in validator language. Adding a broader exception wrapper was rejected because it would hide the exact failing precondition.

Scalability potential: Low/Middle/High/Ultra and CI get deterministic failure messages from the same offline gate.

Hardware Impact: Offline-only validation, 0 us gameplay. It improves diagnosis before importer/runtime bake work.

## Decision 17 - Runtime Binding Plan Gate

Problem: `Runtime_Binding_Review.json` identified 22 unresolved economy-defined IDs, but it still mixed candidate existing IDs and missing-authoring cases in one review list. An importer owner could accidentally treat candidate suggestions as approved runtime mappings.

Solution: Added `Data/Economy/Runtime_Binding_Plan.json` and validator coverage. The plan mirrors all 22 unresolved IDs, marks 18 as candidate aliases pending owner confirmation, marks 4 as requiring new authoring assets, and explicitly sets `runtime_use_allowed=false` for every unresolved ID.

Rejected Alternatives: Directly replacing `Upgrade_*` with `Comp_*` or `Module_*` with `Build_*` was rejected because component items, buildables, and submarine upgrades are not proven equivalent runtime contracts. Editing Unity `.asset` files was rejected because this prompt remains text/data-only and Unity import proof is absent.

Scalability potential: Low uses the plan as an import-block gate; Middle can convert approved rows into a compact binary alias table; High/Ultra can add richer unlock visuals after owner approval without changing the static economy graph.

Hardware Impact: Offline-only validation, 0 us gameplay. Prevents runtime hash lookup retries/string fallbacks caused by unresolved IDs.

## Decision 18 - Items Manifest Validator Dependency

Problem: `Tools/EconomyValidator.py` now validates `Data/Economy/Items.csv`. Leaving that CSV untracked would make the committed validator fail on a clean checkout.

Solution: Include `Data/Economy/Items.csv` as a first-class economy data artifact. The validator checks item hashes, category hashes, optional source recipe hashes, baseline value parity against `Recipes.json`, raw resource tier/biome counts from `Resource_Distribution_Matrix.csv`, and crafted item source recipe parity.

Rejected Alternatives: Removing the Items.csv validator path was rejected because it is a useful flat manifest for zero-GC importer work. Committing the validator without the CSV was rejected because it would break the documented validation command.

Scalability potential: Low can bake this manifest into contiguous item records; Middle/High/Ultra can add presentation metadata after importer approval without hot-path string lookup.

Hardware Impact: Offline-only validation, 0 us gameplay. It preserves the expected hash/table lookup savings by preventing missing item-manifest bake inputs.

## Decision 19 - Item Manifest CSV Generation

Problem: The validator gained an `Items.csv` gate, but the manifest file was missing. Item values were spread across `Recipes.json` and `Resource_Distribution_Matrix.csv`, forcing importers to infer raw/crafted classification and resource biome coverage from multiple sources.

Solution: Generated `Data/Economy/Items.csv` mechanically from existing economy data. It contains all 55 `Recipes.item_values` entries, classifies 15 matrix resources as `raw_resource`, classifies 40 recipe outputs as `crafted`, records source recipe hashes for crafted items, and records resource tier/biome counts for raw resources.

Rejected Alternatives: Removing the validator gate was rejected because a flat item manifest is useful importer input. Hand-authoring the rows was rejected because it risks drift from existing authoritative data.

Scalability potential: Low uses the CSV directly for importer preflight; Middle can bake it to a compact item catalog; High/Ultra can add richer item presentation without changing economy IDs.

Hardware Impact: Offline-only data generation, 0 us gameplay. A flat manifest preserves catalog lookup savings by avoiding runtime cross-file inference.

## Decision 20 - Item Manifest Exact Set Guard

Problem: `Items.csv` could drift while still passing weak row-level checks if a crafted recipe output omitted its source recipe fields, or if `Recipes.item_values` stopped matching the exact union of matrix raw resources and recipe outputs.

Solution: Hardened validation so `Recipes.item_values` must equal `Resource_Distribution_Matrix.csv` raw resources plus `Recipes.json` recipe outputs. The validator now enforces exact row/raw/crafted counts, rejects crafted rows without source recipe ownership, and `--negative-tests` includes `items_missing_source_recipe` alongside the binding-plan runtime-approval rejection.

Rejected Alternatives: Row-count-only validation was rejected because it can miss wrong classification. Inferring crafted status during import was rejected because the flat manifest should be a deterministic importer input, not a runtime deduction surface.

Scalability potential: Low bakes a fixed raw/crafted item table; Middle can add importer alias resolution; High/Ultra can add richer unlock and fabricator presentation after bindings are approved without changing the authoritative economy graph.

Hardware Impact: Offline-only validation, 0 us gameplay. It preserves the intended hash/table lookup savings and prevents importer fallback logic from classifying item ownership at runtime.

## Decision 21 - First-Submarine Batch-Band Gate

Problem: The first-submarine handoff path sits inside the 5-50 recipe-batch band, but the primary `EconomyValidator.py` only checked the recorded recursive batch count for equality. A future data edit could keep the report internally consistent while becoming too short or too grindy.

Solution: Added a direct 5-50 recursive batch-band guard and a minimum five unique recipe guard to `validate_first_submarine_path`. Added `first_sub_batch_band_overflow` to `--negative-tests`; it rebuilds a structurally consistent temporary first-submarine report with doubled target quantities and confirms the validator rejects the overflow through the pacing-band rule.

Rejected Alternatives: Relying only on a separate graph-audit report was rejected because the primary balancer validator is the first gate most agents will run. Rejecting only mismatched totals was rejected because it does not prove the pacing boundary itself.

Scalability potential: Low keeps the first submarine path bounded for MX350-era pacing; Middle can tune route/harvest assumptions; High/Ultra can spend saved runtime cost on richer fabricator presentation without altering the authoritative batch band.

Hardware Impact: Offline-only validation, 0 us gameplay. It prevents late runtime balancing churn caused by a path that is mathematically valid but outside the intended progression band.

## Decision 22 - Items CSV Global Collision Scan

Problem: `Items.csv` hashes were validated row-by-row, but the global generated-file collision sweep did not scan that CSV directly. Adding it exposed an optional-field edge case: raw-resource rows have empty `source_recipe_id` and empty `source_recipe_hash32`, which must not be parsed as an integer hash.

Solution: Added `Items.csv` to `validate_global_hash_collisions()` and made the CSV collector skip empty optional IDs only when the sibling hash is also empty. Missing hashes for non-empty IDs now fail through the validator's controlled `require()` path.

Rejected Alternatives: Leaving `Items.csv` out of the global sweep was rejected because future importer metadata could add IDs not already duplicated in `Recipes.json`. Blindly ignoring empty hashes was rejected because it would hide malformed non-empty ID rows.

Scalability potential: Low keeps a flat manifest with direct hash validation; Middle can add approved runtime alias IDs later; High/Ultra can add richer item presentation metadata while the collision gate remains deterministic.

Hardware Impact: Offline-only validation, 0 us gameplay. It prevents runtime catalog fallback or misbinding caused by cross-file hash collisions before data reaches baked tables.
