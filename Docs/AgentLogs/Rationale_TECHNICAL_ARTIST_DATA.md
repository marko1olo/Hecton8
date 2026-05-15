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
Hardware Impact: 0 us runtime impact. Offline audit completed first-party scan with 138 textures, 28 albedo candidates, 173 materials, and 0 albedo energy failures.

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
Hardware Impact: 0 us runtime impact. Corrected audit now reports 17 ORM candidates, 13 detail candidates, 6 texture import issues, 31 material slot issues, and 0 albedo energy failures.

## CI Gate Verification

Problem: Adding fail flags without proving their exit codes would make the validator look more complete than it is.
Solution: Run scoped debt roots: `Assets/_Project/Art/TEXTURES/WorldProceduralFlora` with `--fail-on-import-issues` returned exit 2, and `Assets/_Project/Art/Materials` with `--fail-on-material-issues` returned exit 3.
Rejected Alternatives: Running multiple full-project scans in parallel was rejected after timeout because it overloaded the same asset tree and produced no additional evidence. Claiming flag behavior from code inspection alone was rejected.
Scalability potential: Low = fast scoped gates can run during authoring. Middle/High/Ultra = full root gate can run in CI/nightly when time budget permits.
Hardware Impact: 0 us runtime impact. Offline gate only.
