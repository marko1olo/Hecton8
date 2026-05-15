# Rationale_TECHNICAL_ARTIST_DATA

## Prompt Authority

Problem: The extracted XML header says "15 TITANIUM TASKS" but contains 8 numbered tasks.
Solution: Use the numbered task list as the executable scope because it is concrete and bounded.
Rejected Alternatives: Executing phantom tasks was rejected because it would invent work outside the XML.
Scalability potential: Low/Middle/High/Ultra unaffected; this is scope control.
Hardware Impact: 0 us runtime impact.

## Mandate Conflict - ORM Packing

Problem: The direct prompt requires ORM packing as R=AO, G=Roughness, B=Metallic, while `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` contains an older packed-mask convention R=Metallic, G=AO, B=Smoothness, A=Emission.
Solution: Follow the prompt-specific ORM convention for this scout pass and record the older convention as a migration conflict requiring integrator decision before shader import enforcement.
Rejected Alternatives: Silently mixing both layouts was rejected because it would corrupt material response. Overwriting project shaders was rejected because this is an audit/doctrine task, not a shader migration task.
Scalability potential: Low = one 3-channel mask texture instead of three grayscale textures. Middle = same with 1024/2048 residency. High = saved memory buys detail normals and wear overlays. Ultra = same ORM contract with GOD_MODE larger authoring mips.
Hardware Impact: Estimated low-end gain is 40-70% VRAM reduction for mask sets that currently use separate AO/roughness/metallic textures; exact project delta pending script output.

## Material Audit Tool

Problem: A full `Assets` scan opened third-party libraries and timed out; the first refactor still walked every GUID meta file and then hit a stale `guid_map` reference.
Solution: Scope default scanning to first-party `Assets/_Project`, prune third-party directories unless explicitly requested, resolve only material-referenced GUIDs, skip image decoding for non-albedo candidates, and use Pillow sampling only for albedo energy checks.
Rejected Alternatives: Blind full-library decoding was rejected because it burns audit time on vendor content outside ownership. Writing a Unity editor mutator was rejected because this task is an audit and doctrine pass, not an import-setting migration.
Scalability potential: Low = no runtime impact and faster offline gate. Middle = same validator can run in CI. High = optional third-party mode remains available for asset-pack triage. Ultra = JSON output supports batch-level texture governance.
Hardware Impact: 0 us runtime impact. Final offline audit completed first-party scan with 138 textures, 26 albedo candidates, 176 materials, and 0 albedo energy failures.

## Surface Doctrine

Problem: First-party materials contain acceptable albedo luminance but weak surface data usage: packed masks are rare and detail slots are unused.
Solution: Create `Docs/TECH_ART_PBR_SURFACE_DOCTRINE.md` with the prompt-authoritative ORM layout, actual detail candidates, clearcoat fake, anisotropic fake, standard-vs-optimized VRAM model, NASA-Punk Noir rationale, and GOD_MODE override table.
Rejected Alternatives: Editing materials directly was rejected because shader/import convention conflict must be resolved before asset mutation. Inventing nonexistent hard-surface textures was rejected; the doc explicitly marks scratch/dust/carbon overlays as missing.
Scalability potential: Low = ORM 512 plus no detail overlays where budget is tight. Middle = ORM + single detail normal. High = stronger shared detail overlays and clearcoat fake. Ultra = GOD_MODE texture residency with demotion on VRAM pressure.
Hardware Impact: Optimized material model reduces unique set memory from about 6.65 MB to 2.99 MB per material under the documented assumption, about 55% VRAM reduction. Runtime CPU impact remains 0 us until shaders consume the doctrine.

## Omega Polish

Problem: Batch protocol requires polish mandate parsing only after core tasks are complete, but `CURRENT_BATCH.md` has no `<POLISH_MANDATE>` tag.
Solution: Record the absence and run owned-file static checks instead: Python compile, validator rerun, `git diff --check`, and runtime hot-path keyword scan against owned files.
Rejected Alternatives: Inventing a polish mandate was rejected because that would fabricate authority. Skipping final checks was rejected because the batch still requires an anti-bloat pass.
Scalability potential: Low/Middle/High/Ultra unchanged. The validator and doctrine remain offline, while future shader/material adoption can gate detail features by tier.
Hardware Impact: 0 us runtime impact. Static checks found no runtime hot-path code changes.

## Hardening Pass - Import Evidence

Problem: The first validator pass proved albedo energy and material slot debt, but did not inspect texture import settings. The first import-setting implementation also produced false positives because substring matching treated `storms` as `orm`.
Solution: Add import-setting checks for sRGB, normal-map importer type, mipmaps, compression/readability, add Markdown report output, add CI fail flags, and replace raw substring ORM detection with tokenized filename classification plus UI/skybox path exclusions.
Rejected Alternatives: Leaving the JSON-only audit was rejected because the next art pass would have to reverse-engineer issues. Keeping substring matching was rejected because noisy evidence is not evidence.
Scalability potential: Low = import issues can be fixed before wasting shader/material work. Middle = CI can fail on import/material debt. High = cleaner data supports richer detail overlays. Ultra = same validator can gate GOD_MODE texture escalations.
Hardware Impact: 0 us runtime impact. Corrected audit now reports 17 ORM candidates, 13 detail candidates, 5 texture import issue textures, 31 material slot issues, and 0 albedo energy failures.

## CI Gate Verification

Problem: Adding fail flags without proving their exit codes would make the validator look more complete than it is.
Solution: Run scoped debt roots: `Assets/_Project/Art/TEXTURES/Detali` with `--fail-on-import-issues` returned exit 2, and `Assets/_Project/Art/Materials` with `--fail-on-material-issues` returned exit 3.
Rejected Alternatives: Running multiple full-project scans in parallel was rejected after timeout because it overloaded the same asset tree and produced no additional evidence. Claiming flag behavior from code inspection alone was rejected.
Scalability potential: Low = fast scoped gates can run during authoring. Middle/High/Ultra = full root gate can run in CI/nightly when time budget permits.
Hardware Impact: 0 us runtime impact. Offline gate only.

## Evidence Export Pass

Problem: Markdown and JSON are not enough for batch triage when import/material debt needs to be assigned to multiple art owners.
Solution: Add CSV exports for texture import issues, material slot issues, and detail candidates, and add human-readable recommendations to Markdown rows.
Rejected Alternatives: Leaving only issue codes was rejected because it slows material migration. Generating Unity editor mutations was still rejected because the ORM convention conflict must be resolved before asset edits.
Scalability potential: Low = CSV can drive quick import cleanup lists. Middle = CI artifacts can be parsed by scripts. High/Ultra = same exports can gate GOD_MODE override rollout.
Hardware Impact: 0 us runtime impact. Final audit with CSV output reports 138 textures, 26 albedo candidates, 0 energy failures, 5 import issue textures, 17 ORM candidates, 13 detail candidates, 176 materials, and 31 material issues.

## Surface Classifier Cleanup

Problem: The albedo classifier still used the broad `_d` substring inherited from the first pass, so a skybox such as `panorama_den.png` could enter the PBR surface energy test.
Solution: Convert albedo detection to token-based surface names and exclude UI/skybox paths from albedo classification. Remove dead raw token constants left behind by the stricter classifier.
Rejected Alternatives: Keeping the broader match was rejected because skybox/UI texture evidence is outside the surface-material mandate.
Scalability potential: Low/Middle/High/Ultra unchanged; cleaner evidence prevents wrong owners from receiving false material cleanup.
Hardware Impact: 0 us runtime impact. Final albedo candidate count is 26 first-party surface textures with 0 energy failures.

## Regression Test Harness

Problem: The validator now carries logic that can silently regress: classifier tokenization, import-setting parsing, material-slot issue detection, and report export recommendations.
Solution: Add `Tools/test_material_audit.py` with synthetic assets and materials that prove the expected behavior without mutating Unity assets. Execute the suite through `python -m unittest Tools.test_material_audit` and compile both audit files.
Rejected Alternatives: Manual reruns alone were rejected because they only prove the current asset corpus, not the edge cases that caused earlier false positives. Unity editor mutation tests were rejected because this pass is offline audit tooling, not asset migration.
Scalability potential: Low = quick local proof before import cleanup. Middle = CI can run the same unit suite with the audit gate. High = richer material conventions can add tests before shader rollout. Ultra = GOD_MODE override rules can be validated without touching runtime.
Hardware Impact: 0 us runtime impact. The suite is offline QA; it protects the MX350 budget indirectly by preventing bad texture classifications and import debt from re-entering reports.

## Prompt ORM Separation

Problem: The validator still counted legacy `_MaskMap` and `_MetallicGlossMap` slots as packed-mask readiness, but the prompt requires ORM specifically as R=AO, G=Roughness, B=Metallic.
Solution: Split prompt ORM slots from legacy/unknown packed mask slots, add `NO_PROMPT_ORM_SLOT` and `LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW`, and export channel-packing migration candidates with LOW/MEDIUM/HIGH priority.
Rejected Alternatives: Treating legacy mask slots as complete was rejected because channel order is not proven. Editing material YAML to add `_ORMMap` slots was rejected because shader convention conflict remains unresolved and raw material mutation would be unsafe.
Scalability potential: Low = identify which assets can stay scalar/cheap and which need packed masks. Middle = migrate legacy masks only after channel review. High = saved slots buy shared detail overlays and clearcoat response. Ultra = GOD_MODE can raise detail density after prompt ORM is stable.
Hardware Impact: 0 us runtime impact. Updated audit found 0 prompt ORM material slots, 9 legacy/unknown mask slots, 31 channel-packing candidates, and 31 materials with material-slot issues. Fail gates were rechecked after the new issue codes.

## Residency Model Pass

Problem: The 50% VRAM target was documented but not emitted as machine-readable audit evidence, and the tool did not identify texture residency hotspots.
Solution: Add a deterministic BC-class memory estimate per texture from image dimensions and mip setting, aggregate estimated texture MiB, export texture memory hotspots, and include the standard-vs-optimized channel-packing savings model in JSON/Markdown.
Rejected Alternatives: Using Unity profiler numbers was not possible without Unity logs. Claiming exact runtime VRAM from Python was rejected; the report labels this as an offline estimate, not Unity import proof.
Scalability potential: Low = texture hotspots can be demoted before MX350 import. Middle = CI artifacts can flag oversized source data. High = saved ORM memory buys detail overlays. Ultra = GOD_MODE can spend the same model deliberately after residency checks.
Hardware Impact: 0 us runtime impact. Offline audit estimates 497.565 MiB scanned texture residency and 113.46 MiB potential savings across 31 channel-packing candidates under the documented 6.65 -> 2.99 MiB material model.

## GOD_MODE Override Export

Problem: Task 8 required a texture resolution override list for GOD_MODE, but the strongest form of that handoff is machine-readable audit data, not prose only.
Solution: Add 12 tiered override rows to the audit report and CSV export, covering TOASTER/DECK/PRO/GOD_MODE maxima, format, and fallback rule per asset class.
Rejected Alternatives: Creating or changing Unity quality/import settings was rejected because this scout owns doctrine and audit data, not project settings. Leaving only the Markdown doctrine table was rejected because other agents need parseable handoff data.
Scalability potential: Low = TOASTER caps stay explicit. Middle/PRO = texture raises are bounded. Ultra = GOD_MODE gets specific higher limits with fallback/demotion rules.
Hardware Impact: 0 us runtime impact. The generated CSV gives import/runtime owners deterministic caps without touching asset importers.

## Global Detail Overlay Plan

Problem: The prompt target includes 20% more detail, but the audit had only discovered detail candidates and did not export the hard-surface overlay roles needed for NASA-Punk surfaces.
Solution: Add 10 global detail overlay roles with source status, target surfaces, TOASTER fallback, GOD_MODE rule, and expected detail-gain percentage. Export the plan to JSON, Markdown, and CSV.
Rejected Alternatives: Claiming the existing flora detail candidates solved cockpit scratches/dust/carbon was rejected because those hard-surface overlays are missing authoring data. Mutating materials was rejected because no shader slot convention is finalized.
Scalability potential: Low = overlays disabled or baked. Middle = limited shared masks. High = richer shared detail for inspection surfaces. Ultra = GOD_MODE gets stronger 1024 overlays without per-material duplication.
Hardware Impact: 0 us runtime impact. The plan defines 10 shared overlays with minimum 20% expected detail gain, but all source statuses are explicit and material wiring remains pending shader/import authority.

## Unresolved Material References

Problem: Several material texture slots were still emitted as raw 32-character GUIDs in audit artifacts, which hides broken, external, or out-of-scope texture references.
Solution: Add unresolved texture GUID detection after first-party GUID mapping, ignore Unity internal `unity_` lighting properties, and export affected materials to a dedicated CSV.
Rejected Alternatives: Counting `unity_ShadowMasks` as material debt was rejected as noise. Silently leaving raw GUIDs in migration CSVs was rejected because integrators need exact reference debt before material migration.
Scalability potential: Low = broken/external references can be fixed before MX350 import passes. Middle = CI can flag missing dependencies. High/Ultra = high-tier material upgrades do not inherit broken texture slots.
Hardware Impact: 0 us runtime impact. Updated audit reports 9 materials with 27 unresolved first-party texture refs and 37 total material issue materials.

## Scoped Resolve Root

Problem: Scoped material audits over `Assets/_Project/Art/Materials` produced inflated unresolved-reference counts because texture GUID resolution was limited to the scan root.
Solution: Add `--resolve-root` so a narrow scan can resolve texture GUIDs against a wider first-party asset root without scanning all textures for energy/memory work.
Rejected Alternatives: Always scanning all of `Assets/_Project` was rejected because scoped CI gates need fast targeted runs. Ignoring scoped unresolved noise was rejected because it weakens fail-gate evidence.
Scalability potential: Low = scoped gates stay fast on cheap machines. Middle = CI can use narrow roots with full first-party resolution. High/Ultra = migration agents get cleaner material debt lists.
Hardware Impact: 0 us runtime impact. Scoped `Art/Materials` unresolved refs dropped from 106 to 19 when resolved against `Assets/_Project`; full-root audit remained 9 materials with 27 unresolved refs.

## CLI Gate Regression

Problem: The fail flags had manual shell evidence, but the regression suite did not prove the packaged CLI process returned the documented exit codes.
Solution: Add a subprocess regression test that creates synthetic debt, executes `Tools/MaterialAudit.py`, and asserts `--fail-on-import-issues` returns 2 and `--fail-on-material-issues` returns 3.
Rejected Alternatives: Direct function-only tests were rejected because `argparse`, stdout summary, and process return codes are the actual CI contract. Manual wrapper proof was rejected as insufficient long-term guardrail.
Scalability potential: Low = local authoring gates fail deterministically. Middle = CI can run the same suite before nightly asset scans. High/Ultra = material migration remains blocked by machine-verifiable surface debt instead of subjective review.
Hardware Impact: 0 us runtime impact. Regression suite now passes 8 tests and protects the offline gate behavior that keeps bad imports/material debt out of MX350 texture budgets.

## Unresolved Reference Gate

Problem: Broad material issues include expected migration work such as missing prompt ORM/detail slots, but unresolved texture GUIDs are dependency faults that need a sharper CI signal.
Solution: Add `--fail-on-unresolved-refs` with exit code 4, publish the exit-code contract in JSON/Markdown/doctrine, and extend the subprocess test to prove exit 4.
Rejected Alternatives: Reusing `--fail-on-material-issues` was rejected because it makes broken references compete with planned material modernization debt. Adding an editor mutator was rejected because this pass must not repair material YAML without shader/import authority.
Scalability potential: Low = dependency damage can block cheap-device builds without blocking all migration planning. Middle = CI can split import, unresolved-reference, and broad material modernization jobs. High/Ultra = high-tier material upgrades avoid inheriting missing/external texture references.
Hardware Impact: 0 us runtime impact. Full audit still reports 9 materials with 27 unresolved refs; scoped `Art/Materials` gate returns exit 4 with 19 unresolved refs after wider GUID resolution.

## Markdown Report Structure

Problem: The Markdown generator emitted the `Import Issue Counts` header before unrelated VRAM and tier-override sections, then wrote the actual import-issue table much later.
Solution: Move the header emission to the table emission point and add a regression assertion that the header is followed by the issue-count table.
Rejected Alternatives: Leaving the report structurally misleading was rejected because the CTO-facing Markdown is a handoff artifact, not decoration. Manually editing the generated Markdown was rejected because regeneration would reintroduce the defect.
Scalability potential: Low/Middle/High/Ultra unchanged; report consumers get deterministic issue ordering for triage.
Hardware Impact: 0 us runtime impact. The fix only changes offline Markdown generation and test coverage.

## Texture Budget Gate

Problem: The audit estimated texture residency but could not fail CI when the estimate crossed the 900 MiB MX350 texture budget.
Solution: Add a texture-budget model with PASS/WARN/FAIL status, `--texture-budget-mib`, and `--fail-on-texture-budget` returning exit 5 when the offline estimate exceeds the configured budget.
Rejected Alternatives: Treating budget overflow as broad material debt was rejected because memory budget failure is a different class from missing ORM/detail slots. Waiting for Unity profiler only was rejected because offline gates should catch obvious source-data overruns before import work.
Scalability potential: Low = 900 MiB MX350 guard blocks texture bloat. Middle = same budget can run nightly. High = threshold can be raised intentionally for stronger machines. Ultra = GOD_MODE overrides remain gated by explicit budget status rather than wishful resolution increases.
Hardware Impact: 0 us runtime impact. Current first-party estimate is 497.565/900.0 MiB, used ratio 0.5528, status PASS; this is an offline BC-class estimate, not Unity profiler proof.

## Albedo Read Error Gate

Problem: Pillow decode failures were stored per texture but not summarized, exported, or gateable; a corrupt albedo could skip the energy-conservation test while the report still showed zero energy failures.
Solution: Count all texture read errors for triage, export them to CSV/Markdown, and make `--fail-on-texture-read-errors` fail only when albedo candidates cannot be decoded for energy validation.
Rejected Alternatives: Failing on every decode error was rejected after the project revealed `ReflectionProbe-0.exr`; that file is not an albedo candidate and Pillow EXR support is not the PBR energy contract. Ignoring read errors entirely was rejected because corrupt albedo evidence would be false.
Scalability potential: Low = albedo corruption blocks cheap-device builds before import. Middle = non-albedo read warnings remain visible for asset owners. High/Ultra = material upgrade passes do not inherit silent broken albedo data.
Hardware Impact: 0 us runtime impact. Current audit reports 1 total texture read warning, 0 albedo read errors, and the albedo energy gate remains valid for 26 decoded candidates.

## Generated Lighting Texture Exclusion

Problem: The surface audit included scene-generated reflection-probe EXR data, creating a read warning unrelated to channel packing, detail maps, or albedo energy validation.
Solution: Skip scene `ReflectionProbe`, `Lightmap`, and `LightingData` EXR/HDR files during surface texture scanning, while retaining the albedo read-error gate for actual surface inputs.
Rejected Alternatives: Keeping the warning was rejected because it pollutes PBR surface debt. Removing EXR support globally was rejected because authored surface EXRs could still be valid if they appear outside generated scene-lighting paths.
Scalability potential: Low/Middle/High/Ultra unchanged; audit consumers receive cleaner surface-only evidence.
Hardware Impact: 0 us runtime impact. Full audit now reports 137 textures, 0 texture read errors, 0 albedo read errors, and 0 energy failures.

## Energy Warning Gate

Problem: Albedo bright-area warnings were reported but could not fail CI, allowing localized baked-bright albedo risk to remain non-blocking.
Solution: Add `--fail-on-energy-warnings` with exit code 7 while preserving hard overbright albedo failures as exit code 1.
Rejected Alternatives: Converting every warning into an unconditional failure was rejected because warnings are useful for staged adoption. Ignoring warnings was rejected because localized white patches can still cause PBR blowout even when mean luminance is below the hard fail threshold.
Scalability potential: Low = strict branches can block warning-grade albedo bloat. Middle/High/Ultra = art directors can allow warnings during authoring but enforce clean albedo before branch promotion.
Hardware Impact: 0 us runtime impact. Current first-party audit has 0 energy warnings and 0 energy failures.

## CI Surface Gate Profile

Problem: The passing surface gates existed only as separate flags, which makes CI/local invocation easy to drift and encourages partial validation.
Solution: Add `--ci-surface-gates`, publish `gate_profiles.surface_safe` in generated reports, and make the profile enable only the current-corpus safe gates: energy warnings, albedo read errors, and texture budget.
Rejected Alternatives: Enabling broad import/material/unresolved-reference gates in the profile was rejected because current first-party assets still have known migration debt and that would block every run before it can prove albedo energy/readability/budget safety. Keeping separate flags only was rejected because repeated manual flag strings are brittle.
Scalability potential: Low = toaster/MX350 builds get one command that blocks bright albedo risk, corrupt albedo data, and texture budget overflow. Middle = nightly CI can layer broad import/material gates separately. High/Ultra = GOD_MODE texture escalation remains budget-gated instead of hiding behind manual command variants.
Hardware Impact: 0 us runtime impact. Current first-party audit with `--ci-surface-gates` passes at 137 textures, 0 energy warnings, 0 albedo read errors, and 497.565/900.0 MiB texture budget PASS.

## Active Gate Artifact Evidence

Problem: The generated reports listed available gate profiles but did not prove which profile/gates were active during the audit run.
Solution: Add `active_gate_profiles` and `active_gates` to report metadata before writing JSON/Markdown, print the same fields to stdout, and add an `Active Gates` Markdown section.
Rejected Alternatives: Relying on stdout was rejected because logs and reports can be separated. Writing only JSON metadata was rejected because the Markdown report is the human-facing artifact.
Scalability potential: Low = MX350 gate artifacts remain auditable after handoff. Middle = CI can archive JSON/Markdown and prove the exact gate mode. High/Ultra = GOD_MODE escalation audits can distinguish budget-only runs from full migration gates.
Hardware Impact: 0 us runtime impact. Current artifact records `surface_safe` with active gates `energy_failures`, `energy_warnings`, `albedo_read_errors`, and `texture_budget`.

## Channel Packing Candidate Gate

Problem: Channel-packing candidates were counted and exported, but broad material issue exit 3 was the only way to fail a material migration job. That mixes prompt ORM debt with detail-slot and unresolved-reference debt.
Solution: Add `--fail-on-channel-packing-candidates` with exit code 8, include it in active-gate metadata, and cover it through the subprocess regression suite.
Rejected Alternatives: Adding channel debt to `--ci-surface-gates` was rejected because current first-party assets intentionally have 31 channel candidates and the safe profile must remain usable for albedo/readability/budget enforcement. Leaving only broad material failure was rejected because channel packing is the prompt's primary surface target.
Scalability potential: Low = channel migration can be tracked separately from safe budget checks. Middle = CI can block material PRs that add new non-ORM surfaces. High = saved ORM texture memory buys shared detail overlays. Ultra = GOD_MODE material escalation can require prompt ORM before higher mips are allowed.
Hardware Impact: 0 us runtime impact. Current surface-filtered scoped `Art/Materials` channel gate reports 14 candidates and returns exit 8; full first-party audit reports 22 channel candidates and 80.52 MiB modeled savings potential.

## Detail Map Missing Gate

Problem: Detail-map debt was only visible through broad material issues even though the prompt separately requires detail-map auditing and a 20% perceived-detail target.
Solution: Add `--fail-on-detail-map-missing` with exit code 9, expose `detail_map_missing_count`, export a dedicated detail-missing material CSV, and add a Markdown section for direct art handoff.
Rejected Alternatives: Using `--fail-on-material-issues` was rejected because it mixes detail absence with ORM, unresolved reference, and legacy mask debt. Adding the detail gate to `--ci-surface-gates` was rejected because current first-party assets intentionally have detail debt and the safe profile must remain usable.
Scalability potential: Low = detail slots can be enforced only on material migration branches. Middle = CI can prevent new near-field surfaces without shared detail overlays. High = shared scratches/dust/wear overlays buy the required 20% perceived detail. Ultra = GOD_MODE can raise detail overlay density after base materials have stable slots.
Hardware Impact: 0 us runtime impact. Current full audit reports 22 materials missing detail maps; scoped `Art/Materials` detail gate reports 14 and returns exit 9.

## Non-Surface Material Exclusion

Problem: The detail-missing CSV exposed projection HUD, celestial gas giant/moon, and terrain materials as false surface migration debt.
Solution: Add a conservative surface-material eligibility filter before ORM/detail issue generation and channel candidate generation. Exclude HUD/UI, celestial/moon/gas giant, skybox, terrain material names, and renderTexture/UI/skybox base-map references.
Rejected Alternatives: Keeping false positives was rejected because false debt wastes art migration time. Excluding all unresolved GUID materials was rejected because some unresolved references still represent real surface dependencies that must be fixed.
Scalability potential: Low = migration gates focus on actual hard-surface materials. Middle = CI reports stop assigning projection/celestial work to the surface-material queue. High/Ultra = saved detail/ORM budgets remain tied to visible inspectable surfaces rather than non-PBR special materials.
Hardware Impact: 0 us runtime impact. Full audit now reports 22 channel candidates, 22 detail-missing materials, 29 material issue materials, and 80.52 MiB modeled channel-packing savings potential.

## Surface Unresolved Reference Gate

Problem: Broad unresolved texture GUID debt is still useful, but it mixes surface-material dependencies with non-surface domains. The surface prompt needs a precise failure mode for hard-surface material refs only.
Solution: Add surface-unresolved counts, Markdown/CSV handoff, and `--fail-on-surface-unresolved-refs` with exit code 10. The gate only fails when unresolved GUIDs remain on materials that pass the surface-material eligibility filter.
Rejected Alternatives: Reusing broad unresolved exit 4 was rejected because it would block this domain on celestial, terrain, projection, or other non-surface material references. Ignoring unresolved surface refs was rejected because channel-packing/detail migration cannot trust a material whose base/normal/AO references cannot resolve.
Scalability potential: Low = surface migration blocks only real visible material dependency faults. Middle = CI can run broad dependency and surface dependency lanes separately. High = prompt ORM/detail work can proceed without inherited broken references. Ultra = GOD_MODE material overrides stay gated by valid source textures before higher mips are allowed.
Hardware Impact: 0 us runtime impact. Full first-party audit reports 9 broad unresolved materials / 27 refs, but only 2 surface unresolved materials / 8 refs; full-root surface gate returns expected exit 10 under wrapper. The two affected materials are `mat_Rock2.mat` and `mat_Rock_Shared.mat` under the Rock_4 asset.

## Surface Blocker Severity

Problem: The surface unresolved CSV listed GUIDs but did not classify which shader slots made the material unsafe for ORM/detail migration.
Solution: Group unresolved refs by base-color, normal, data-mask, detail, and other slots; assign severity; expose severity counts in JSON/Markdown/stdout and row-level severity in CSV.
Rejected Alternatives: Raw GUID-only export was rejected because it forces every downstream owner to duplicate slot classification. Blocking only on total unresolved count was rejected because base/normal loss is worse than a stale optional detail slot.
Scalability potential: Low = artists fix base/normal blockers first before cheap-device imports. Middle = CI can sort blocker/high/medium debt. High = surface migration can prioritize visible rock/cockpit/module materials. Ultra = GOD_MODE override rollout can require zero BLOCKER refs before spending higher mips.
Hardware Impact: 0 us runtime impact. Current full audit reports `surface_unresolved_blocker_materials=2`; both are Rock_4 surface materials with unresolved base, normal, and occlusion/data slots.

## Surface Migration Queue

Problem: The audit produced correct but fragmented worklists: unresolved refs, channel-packing candidates, and detail-map-missing materials had to be manually correlated before migration.
Solution: Build a single `surface_material_migration_queue` sorted by practical migration order: BLOCKER refs first, then medium legacy-mask review, then low-risk ORM/detail authoring.
Rejected Alternatives: Keeping separate CSVs only was rejected because cross-file correlation causes missed blockers and repeated artist triage. Promoting all missing ORM rows to medium was rejected after readback because low-risk base-only materials should stay LOW until they are near-field or hero.
Scalability potential: Low = queue starts with blocker repair so cheap-device imports do not carry broken surfaces. Middle = channel review can be batched. High = detail/ORM authoring can target visible surfaces first. Ultra = GOD_MODE material upgrades can consume the same queue after blockers are cleared.
Hardware Impact: 0 us runtime impact. Current full audit reports `surface_migration_queue_rows=22` with `BLOCKER=2, MEDIUM=9, LOW=11`.

## Prologue Planet False-Positive Filter

Problem: The migration queue still included three `_PROLOGUE_CONTENT/Textures/Planets/pLANET` cloud/surface materials as LOW prompt-surface ORM/detail work. Those materials are celestial/prologue content, not inspectable worn NASA-Punk hard-surface materials.
Solution: Extend non-surface filtering to `/textures/planets/` for texture classification, base-map references, and material paths; add regression coverage for a prologue planet texture and resolved planet material.
Rejected Alternatives: Leaving the rows in the queue was rejected because it wastes surface-material migration time. Blanket-excluding every material named `surface` was rejected because valid rocks, panels, and terrain-adjacent surfaces would be lost.
Scalability potential: Low = MX350-safe surface cleanup focuses on visible inspectable props and rocks. Middle = CI queue avoids assigning celestial/prologue work to the surface material lane. High = saved migration effort goes into shared detail overlays for cockpit/modules/rocks. Ultra = GOD_MODE overrides remain reserved for actual surface materials after blockers clear.
Hardware Impact: 0 us runtime impact. Current full audit reports `surface_migration_queue_rows=19` with `BLOCKER=2, MEDIUM=9, LOW=8`; channel-packing modeled savings is now 69.54 MiB after removing non-surface prologue false positives.
