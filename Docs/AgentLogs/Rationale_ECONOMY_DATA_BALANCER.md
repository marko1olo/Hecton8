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

Problem: The status file contained one contradictory self-review line claiming the validator no longer prints `ECONOMY BALANCED`, while the batch-required and implemented behavior is to print a separate pacing warning before exact `STATUS: ECONOMY BALANCED`.

Solution: Corrected the status wording and preserved the validator behavior: `energy_pacing_warning=literal_30kw_requires_owner_decision` followed by `STATUS: ECONOMY BALANCED`.

Rejected Alternatives: Leaving the contradiction was rejected because downstream agents would receive mixed instructions. Changing the validator status line again was rejected because it would violate the batch-required output string.

Scalability potential: Low/Middle/High/Ultra all consume the same validated static economy tables; the warning remains an owner decision gate for runtime energy pacing rather than a hidden data failure.

Hardware Impact: Documentation-only correction, 0 us gameplay. It prevents wasted importer/tuning work caused by contradictory proof text.
