# Rationale - CRAFTING_COST_BALANCER

Evidence class rules: static files and CLI Python are not Unity runtime proof. Runtime GC, profiler, Play Mode, and player-build claims remain PENDING VERIFICATION.

## Intake

Problem: The assignment is offline economy balancing, but project hot-path mandates still apply because runtime consumers must receive numeric, contiguous, prevalidated data.
Solution: Keep the work in `Data/Economy`, `Tools`, and mandated docs/logs. Use offline JSON/CSV plus Python validation; do not touch C# runtime or Unity assets.
Rejected Alternatives: Unity ScriptableObject edits were rejected because the prompt says `NO_UNITY`; runtime formula balancing was rejected because it would move authoring math into hot-path consumers.
Scalability potential: Low tier loads precomputed numbers only; Middle/High/Ultra can spend saved runtime cycles on richer fabricator VFX/audio without changing recipe authority.
Hardware Impact: Estimated low-end i3/MX350 gain is avoiding any runtime recipe-cost derivation and hash calculation; static estimate only, no profiler artifact.

Problem: Existing economy state already includes `Recipes.json`, `Items.csv`, and `Tools/EconomyValidator.py`; blind replacement would break active batch work.
Solution: Extend static economy data with `Crafting_Costs.json` and focused validation while preserving the existing green validator behavior.
Rejected Alternatives: Replacing `Recipes.json` was rejected because current validator and item manifests depend on it; duplicating a separate validator name was rejected because the XML specifically names `Tools/EconomyValidator.py`.
Scalability potential: The data table can be compiled to native arrays by the Data Monolith without runtime string lookups.
Hardware Impact: Static estimate: saves hash/value reconciliation at startup by baking item hashes and mass/power/fabrication time offline.

## Loop 1 Decisions

Problem: The 50-item matrix needed to coexist with the already validated 40-recipe economy file.
Solution: Added `Crafting_Costs.json` as the CRAFTING_COST_BALANCER-owned table and generated `Crafting_Costs.csv` from the same source data.
Rejected Alternatives: Editing `Recipes.json` was too high-risk for parallel agents; making the CSV hand-authored was rejected because it would drift from JSON.
Scalability potential: Low reads a compact minified JSON or downstream binary bake; Middle/High/Ultra can retain identical authority while increasing fabricator presentation.
Hardware Impact: Estimated i3/MX350 impact is zero gameplay-frame cost because all balancing fields are precomputed offline.

Problem: Cost balance had to satisfy both mass conservation and 3-to-1 progression.
Solution: Each recipe result mass equals the sum of immediate ingredient masses, while value and kWh medians are tier-gated: cost ratios 2.931 and 3.173; power ratios 2.916 and 2.966. The kWh model is `craft.power.material_process_energy.v2`: thermal conditioning from specific heat/process delta/latent heat fraction, surface joining from density-derived area, feed work from m*g*h, and chamber vacuum work from pressure-volume energy.
Rejected Alternatives: Directly authoring output mass independently was rejected because it would violate mass conservation; unlabelled kWh constants were rejected because they are indistinguishable from placeholder balance.
Scalability potential: Low tier can use the same recipe data with minimal UI/VFX; Ultra can spend saved CPU on richer fabricator effects.
Hardware Impact: Static estimate: removes any need for runtime material/value summation in a fabricator loop; profiler proof absent.

## Loop 2 Decisions

Problem: Tier 2 progression can be bypassed if the table only uses scan flags or cost values.
Solution: Validator enforces that each Tier 2 recipe declares at least one non-consumed required tool that is a Tier 1 tool output.
Rejected Alternatives: Consuming the tool in every Tier 2 recipe was rejected because it would create fake mass sinks and punish normal crafting loops.
Scalability potential: Low tier does a single numeric requirement check; High/Ultra can show richer tool bench affordances as presentation only.
Hardware Impact: Static estimate: direct hash lookup of a required tool avoids runtime graph searching; no profiler evidence.

Problem: Deconstruction/rebuild loops can mint material or energy if reclaim equals or exceeds input.
Solution: The table fixes reclaim at 50%; validator rejects reclaim >= 1.0, value break-even, nonpositive quantities, and nonpositive kWh/time.
Rejected Alternatives: Runtime anti-exploit patching was rejected because authoring data should be mathematically valid before it reaches the game.
Scalability potential: Low through Ultra use the same economy truth; higher tiers can improve deconstruction VFX without changing outputs.
Hardware Impact: Static estimate: no deconstruction graph solve is needed in runtime hot paths for loop prevention.

## Loop 3 Decisions

Problem: Fabrication time must be authored explicitly so the fabricator UI and power wait do not derive hidden values in runtime.
Solution: Added `FabricationTimeSeconds` to every recipe and validator positivity checks.
Rejected Alternatives: Runtime calculation from tier and mass was rejected because it hides design balance and risks divergent UI/tool behavior.
Scalability potential: Low tier can show simple timers; Ultra can layer richer fabricator animation without changing authoritative completion time.
Hardware Impact: Static estimate: avoids runtime formula evaluation and lets UI read a scalar.

Problem: The task forbids Unity work, but economy data often tempts asset or ScriptableObject edits.
Solution: Kept the implementation to `Data/Economy/Crafting_Costs.*`, `Tools/*.py`, and mandated docs/logs.
Rejected Alternatives: Editing Unity item assets was rejected because no Unity import/console validation is available in this shell and the prompt says offline only.
Scalability potential: Static data can later be compiled by the Data Monolith for all tiers.
Hardware Impact: No player-frame impact; Unity runtime not touched.

## Loop 4 Decisions

Problem: Runtime systems need stable numeric identifiers, not strings, in hot paths.
Solution: The baker writes FNV-1a hash fields next to every authored ID and the validator checks those pairs.
Rejected Alternatives: Runtime hashing was rejected because the data mandate requires offline hash generation.
Scalability potential: Low tier performs integer lookups only; Ultra can layer presentation without changing identifiers.
Hardware Impact: Static estimate: removes startup or fabricator-path hash computation; no profiler artifact.

Problem: Designers need readable review data while runtime/import data must remain compact and deterministic.
Solution: Generated `Crafting_Costs.csv` from the same source as minified JSON and made the validator compare CSV rows back to JSON.
Rejected Alternatives: Manual CSV export was rejected because it can drift; pretty JSON was rejected for final output because Task 14 requires minification.
Scalability potential: CSV is review-only; compact JSON can be ingested or binary-baked for every hardware tier.
Hardware Impact: JSON size is 126,840 bytes after explicit binary/scalability/material-physics/God-Mode visual metadata; runtime impact remains PENDING VERIFICATION until imported by the Data Monolith.

## Loop 5 Decisions

Problem: The Standard O2 Tank must be achievable before the first five minutes without relying on non-starter resources.
Solution: Added `starter_edge_guard` with collection minutes, fabrication minutes, energy wait at 30 kW, and recursive raw-input starter-zone validation.
Rejected Alternatives: Hardcoding a starter exception was rejected because the recipe should pass the same mass and value rules as the rest of the table.
Scalability potential: Low tier keeps a short early survival loop; High/Ultra can improve fabricator visuals without shifting the timer.
Hardware Impact: Static estimate: scalar guard data avoids runtime path simulation for starter pacing.

Problem: Final status must live in disk artifacts, not chat.
Solution: `Crafting_Costs.json` stores `status_id=economy.crafting_costs.balanced`, validator prints `STATUS: ECONOMY BALANCED`, and this status file tracks every task.
Rejected Alternatives: A final chat-only claim was rejected by the evidence mandate.
Scalability potential: Stable status fields can be consumed by later batch tools or dashboards.
Hardware Impact: No runtime impact; evidence class is CLI_PYTHON and STATIC_DOC only.

## Inquisition Rework Decisions

Problem: JSON/CSV-only output forces SHINOBU/Data Monolith consumers to parse dynamic text before reaching flat records.
Solution: Added `Crafting_Costs.h8bin` v2 with an 80-byte header, 64-byte recipe records, 16-byte ingredient records, 16-byte tool records, and 16-byte God-Mode visual records. All structs use `<` little-endian packing and all offsets/file size are 16-byte aligned.
Rejected Alternatives: Runtime JSON parsing and ad hoc binary writing without readback were rejected.
Scalability potential: Toaster/i3 path can load the binary into flat native records; Ultra can read God-Mode FX hashes plus separate packed gradient/noise extra data.
Hardware Impact: Static estimate: binary path removes text parse cost from low-end ingestion; binary size is 7,424 bytes; Unity Data Monolith import remains PENDING VERIFICATION.

Problem: Deterministic no-profit proof alone did not satisfy the explicit 1,000,000-step Monte Carlo demand.
Solution: Added `Tools/CraftingEconomyMonteCarlo.py` using fixed LCG constants and multiply-high table selection. It writes `Crafting_MonteCarlo_Audit.json` tied to the current JSON SHA.
Rejected Alternatives: `random`/wall-clock stochastic tests were rejected because deterministic replay is required; modulo-biased selection was rejected by the RNG mandate.
Scalability potential: The audit is offline; runtime tiers are unaffected.
Hardware Impact: 1,000,000-step CLI run found `profit_steps=0`, max value delta `-1000`, max mass delta `-400000 mg`, max energy delta `-133000 mWh`.

Problem: Hash collision claims needed a disk artifact.
Solution: `Tools\VerifyCraftingCosts.py` checks every CRAFTING_COST_BALANCER ID/hash pair and writes `Crafting_Hash_Audit.json` with `collisions=0`.
Rejected Alternatives: Relying on `EconomyValidator` console output alone was rejected.
Scalability potential: Numeric IDs preserve stateless lookup across all tiers.
Hardware Impact: Static estimate: no runtime string comparisons required for this table.

Problem: H-Phi/Data Sovereignty fit needed a specific PROJECT_ATLAS comparison.
Solution: `Tools\VerifyCraftingCosts.py` checks `PROJECT_ATLAS.md` for Data Monolith, Crafting Fast-Fail Validator, and DataSovereignty references; it writes `Crafting_DataSovereignty_Audit.json`.
Rejected Alternatives: Per-fabricator private state and MonoBehaviour-owned recipe tables were rejected.
Scalability potential: Data Monolith path is the Toaster-compatible base; God-Mode presentation remains additional data, not authority.
Hardware Impact: Static estimate: flat records reduce i3/MX350 ingestion pressure; runtime proof remains pending.

Problem: Source audit found balance and packing literals that were valid but under-documented.
Solution: Promoted scale factors, starter pacing, reclaim ratio, cost-class bands, visual-overkill formulas, and bit-pack layout into named constants and emitted `binary_scale_model`, `starter_edge_model`, and `visual_overkill_model` metadata in `Crafting_Costs.json`.
Rejected Alternatives: Leaving constants only inside code was rejected because later SHINOBU/Data Monolith importers need a data contract, not folklore.
Scalability potential: Toaster reads the same flat record fields; God-Mode gets documented particle, steam, gradient LUT, harmonic-noise, and hydraulic-jolt ranges without touching authority.
Hardware Impact: Binary payload is 7,424 bytes after the v2 God-Mode visual table; CRC is `1295072744`. JSON metadata increased to 126,840 bytes, which is offline/import-side only.

Problem: The earlier kWh formula could still be read as tuned economy math.
Solution: Replaced it with material profile records on every raw and crafted output. The validator now recomputes `PowerCost_kWh` from specific heat, process temperature delta, latent heat fraction, density-derived surface area, surface joining energy, feed motion, and vacuum work.
Rejected Alternatives: Keeping a `complexity_kwh_per_value_unit` scalar was rejected because it is not a physical derivation.
Scalability potential: Toaster uses only the baked `PowerCost_kWh` scalar and aligned binary mWh field; God-Mode can inspect material profiles plus packed gradient/noise records for hotter fabricator arcs and dirtier process FX.
Hardware Impact: Static-only; no runtime profiler artifact. Binary size is 7,424 bytes, while JSON grows to carry audit-grade physical and visual provenance.

Problem: God-Mode data did not yet satisfy the explicit high-resolution gradient and complex harmonic-noise requirement as binary ingest data.
Solution: Added `GODMODE_VISUAL_STRUCT <IIII>` with `arc_gradient_lut_hash32`, `harmonic_noise_seed_hash32`, packed gradient samples/heat bands/RGBA channels, and packed harmonic frequency/amplitude/ridge mix.
Rejected Alternatives: Keeping these fields JSON-only was rejected because SHINOBU ingest needs a zero-cost binary path for every tier profile.
Scalability potential: Toaster ignores the table; Ultra can consume it for 2048-4096 sample slag heat gradients and deterministic harmonic ridge noise.
Hardware Impact: Static binary grew from 6,608 to 7,424 bytes; all offsets remain 16-byte aligned. Runtime cost remains PENDING VERIFICATION until Data Monolith import.

## OSHINO Revalidation Decisions

Problem: The escalation demanded proof that current disk artifacts, not chat memory, still satisfy the XML task and binary/data-truth constraints.
Solution: Reread `Status_CRAFTING_COST_BALANCER.md`, reread this rationale file, re-extracted the full XML prompt, rebaked crafting data, and reran all owned economy gates against the regenerated JSON SHA/binary CRC.
Rejected Alternatives: Reusing prior console output was rejected because the on-disk artifacts are the only authority after context compression.
Scalability potential: The Toaster path remains the 7,424-byte aligned H8 binary with scalar records; Ultra remains the same authority plus the God-Mode visual table.
Hardware Impact: Fresh static proof only. `CraftingCostsBaker` reported 50 recipes, binary CRC32 `1295072744`, SHA256 `632fdabb2a57dbe115f18fecc8a23a6ddda2bacb0cf53ea472f29b81a0377f69`; Unity import and runtime profiler proof remain PENDING VERIFICATION.

Problem: Cross-domain hard-science laws were named in the escalation, but the crafting economy domain does not own optical, gas, acoustic, or refraction LUT generation.
Solution: Ran the relevant verifier scripts without modifying those domains: Beer-Lambert optics via `VerifyOpticsBaker`, Dalton gas toxicity via `VerifyDaltonGasToxicity`, Sabine/Thorp/hydrostatic acoustics via `VerifySabineBaker`, Snell refraction via `VerifySnellRefractionLut`, and umbrella data truth via `VerifyMetricPhiDataTruth`.
Rejected Alternatives: Authoring Beer-Lambert, Dalton, or Sabine constants into `Crafting_Costs.json` was rejected as cross-domain contamination and fake provenance.
Scalability potential: Those systems already expose stateless binary lookup data; the crafting table only references material-process thermodynamics for kWh and keeps visual overkill as presentation metadata.
Hardware Impact: Static verifier outputs: optics matrix `393216` bytes aligned16, Dalton binary `128128` bytes aligned16, Sabine binary `524288` bytes with `<ff` records, Snell LUT `524288` bytes, Metric Phi `36` checks with `0` failures and `0` endian failures.

Problem: The economy table still needed fresh exploit pressure after the physical kWh rebake.
Solution: Reran `CraftingEconomyMonteCarlo.py --steps 1000000`; replay seed stayed `3366254365`, `profit_steps=0`, and worst deltas stayed negative for value, mass, and energy.
Rejected Alternatives: Relying only on direct reclaim ratio checks was rejected because the user explicitly required the million-step pressure test.
Scalability potential: No runtime simulation is needed; the audit proves the stateless table does not mint resources under deterministic random recipe pressure.
Hardware Impact: Offline CPU only. Runtime low-end path still reads fixed scalar mass/value/kWh fields, with no private economy state added.

Problem: Lore audit could become a cosmetic rewrite that damages stable IDs and designer-facing names.
Solution: Performed a targeted search for sterile/placeholder terms in owned crafting artifacts and logs. Hits were binary `magic` labels, Python `__future__` imports, and explicit rationale wording about rejecting placeholder constants; no recipe slang debt requiring data mutation was found.
Rejected Alternatives: Renaming item IDs/display names for tone was rejected because the XML task is balance/data authority, not localization, and stable hashes would churn.
Scalability potential: Existing Toaster/God-Mode presentation fields already give the dirty industrial fabricator profile through hydraulic arcs, resin smoke, slag heat gradients, and harmonic motor wobble.
Hardware Impact: No runtime change. No Unity asset or localization mutation was made.

Problem: The explicit `Verify*.py` demand needed an auditable sweep, not a hand-picked subset of known passing scripts.
Solution: Added `Tools\RunFullVerifySweep.py`, which enumerates every `Tools\Verify*.py`, runs each under `python -B`, applies required script-specific verification args for `VerifyLore.py --check`, and writes `Docs\AgentLogs\VerifySweep_CRAFTING_COST_BALANCER.json`.
Rejected Alternatives: A shell loop was rejected because Windows quoting already produced a false non-report; a chat-only list of commands was rejected because the CTO reads disk artifacts.
Scalability potential: The sweep is offline only. It increases Data Sovereignty by proving every current verifier has a reproducible stateless command path and JSON evidence trail.
Hardware Impact: No runtime hardware cost. Latest full sweep ran 24 verifier scripts, took about 460.1 seconds wall time in this shell, and ended with `failed_count=0`.

Problem: The independent verifier and economy validator still duplicated binary packing masks, byte-alignment divisors, scale factors, and the surface-area exponent as raw literals.
Solution: Moved those consumers to named constants from `CraftingCostsBaker.py` and added `surface_area_volume_exponent` to the JSON `power_model`, stored at full precision to avoid drift from the physical kWh terms.
Rejected Alternatives: Keeping duplicated numeric literals was rejected because the validator must prove the binary contract, not carry a second folklore copy of it. Rounding the `2/3` exponent to six decimals was tested and rejected because it changed `surface_joining_kwh` enough to fail `Recipe_Tool_Repair`.
Scalability potential: Toaster and Ultra consumers still read the same scalar/binary data; the source contract is now more explicit for future SHINOBU importers.
Hardware Impact: Runtime cost unchanged. JSON grew to 126,840 bytes; binary remains 7,424 bytes with CRC32 `1295072744` and SHA256 `632fdabb2a57dbe115f18fecc8a23a6ddda2bacb0cf53ea472f29b81a0377f69`.

Problem: The literal hardening was only proven by transient shell scans, so the next agent could reintroduce raw pack masks or scale factors without the full verifier sweep noticing.
Solution: Added `Tools\VerifyCraftingSourceContracts.py`, a permanent `Verify*.py` gate that scans owned verifier/simulator source for forbidden raw literals and validates JSON/binary contract constants against `CraftingCostsBaker.py`. It writes `Docs\AgentLogs\Crafting_SourceContract_Audit.json`.
Rejected Alternatives: Keeping the proof as an `rg` command was rejected because it leaves no stable artifact and is not picked up by `Tools\RunFullVerifySweep.py`.
Scalability potential: The source contract verifier protects Toaster and Ultra paths by keeping all pack layout and scale authority in one baker contract.
Hardware Impact: Runtime cost unchanged. Offline sweep now runs 26 verifier scripts; latest `VerifySweep_CRAFTING_COST_BALANCER.json` reports `failed_count=0`.

## OSHINO Revalidation Decisions - 2026-05-17

Problem: The live batch file no longer contains `CRAFTING_COST_BALANCER`, but the active status/rationale files still require this agent to finish its own economy evidence.
Solution: Treated `Docs\Archive\Batch_GIT_SYNC_REBASE\CURRENT_BATCH_local_auxiliary_20260517.md` as the preserved original directive and re-extracted the exact XML block before touching data.
Rejected Alternatives: Switching to the new live batch prompt was rejected because it would abandon the owned 15-task economy assignment; relying on chat memory was rejected by the anti-amnesia protocol.
Scalability potential: Stable disk evidence keeps the Data Monolith and Toaster/God-Mode contracts traceable across batch rollover.
Hardware Impact: No runtime change. This is evidence hygiene only.

Problem: The latest broad verifier failure was stale cross-domain evidence, not a crafting table defect: Metric Phi still recorded an old Quest DAG lore-manifest failure even though direct quest verification could pass after current lore check.
Solution: Reran `VerifyLore.py --check`, `VerifyMetricPhiDataTruth.py`, `VerifyQuestDag.py`, `VerifyQuestDagBinaryIndependent.py`, and `VerifyQuestDagDataTruth.py`, then refreshed the full sweep. The quest gate stayed strict; no fallback lore schema or weakened hash rule was added.
Rejected Alternatives: Adding permissive `source` fallback logic to `QuestCompiler.load_lore_hashes` was rejected because the current manifest and verifier path can be made consistent without changing the compiler contract.
Scalability potential: Quest data remains stateless bitmask/binary lookup data; PDA and encyclopedia payloads remain fixed hash-addressed slices.
Hardware Impact: Runtime cost unchanged. CLI evidence only; Unity import, Play Mode, profiler, GCMonitor, and player build remain PENDING VERIFICATION.

Problem: `EconomyValidator.py --negative-tests` had regressed to 6 malformed cases after tool restoration, while the previous economy evidence claimed 10.
Solution: Added four crafting-specific negative tests and explicit `validate_crafting_costs` checks for recipe count, status, mass parity, positive kWh/time, sub-break-even reclaim, Tier 2 Tier 1 tool gate, and physical energy term recomputation before binary readback.
Rejected Alternatives: Letting `VerifyCraftingCosts.py` alone cover these regressions was rejected because the XML names `Tools\EconomyValidator.py` as the economy simulator/gate.
Scalability potential: The validator now protects the Toaster H8CT scalar path and the full H8CR/God-Mode path from authoring drift before runtime ingestion.
Hardware Impact: Runtime cost unchanged. Offline validator now rejects 10 malformed cases; no player-frame cost.

Problem: The final sweep needed to reflect current disk state after the validator patch.
Solution: Reran `py_compile`, `VerifyCraftingSourceContracts.py`, `VerifyCraftingCosts.py`, `CraftingEconomyMonteCarlo.py --steps 1000000`, `EconomyValidator.py --root . --negative-tests`, `VerifyDataInquisition.py`, and `RunFullVerifySweep.py`. Final sweep report covers 29 verifier scripts with `failed_count=0`.
Rejected Alternatives: Keeping the earlier passing sweep was rejected because the validator source changed afterward.
Scalability potential: Fresh binary/hash/data inquisition confirms 46 aligned binary artifacts and 0 hash collisions for current static data.
Hardware Impact: Static tooling only. Monte Carlo stayed `profit_steps=0`; worst deltas remained negative (`-1000` milli-value, `-400000 mg`, `-133000 mWh`).
