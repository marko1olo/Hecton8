# LOG_TECHNICAL_ARTIST_DATA

## 2026-05-15 - PBR_MATERIAL_REFACTOR_SCOUT

Status: SURFACE DOCTRINE READY - PENDING UNITY IMPORT VERIFICATION

What was wrong:

- The prompt header claimed 15 tasks, but the XML contained 8 numbered tasks.
- First-party albedo luminance was not objectively tested.
- First-party material usage was weak: only 9 of 176 scanned materials use packed masks and 0 use detail slots.
- Hard-surface scratch/dust/carbon global overlays were not found in first-party texture results.
- There is a doctrine conflict: prompt ORM says `R=AO, G=Roughness, B=Metallic`; an older render mandate says `R=Metallic, G=AO, B=Smoothness, A=Emission`.

What was done:

- Created `Tools/MaterialAudit.py`.
- Executed Python energy-conservation test on `Assets/_Project`.
- Wrote JSON evidence to `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA.json`.
- Created `Docs/TECH_ART_PBR_SURFACE_DOCTRINE.md`.
- Created and maintained `Docs/Tasks/Status_TECHNICAL_ARTIST_DATA.md`.
- Created `Docs/AgentLogs/Rationale_TECHNICAL_ARTIST_DATA.md`.
- Re-extracted the prompt after task progress; task count remained 8.
- Checked for `<POLISH_MANDATE>` after core completion; tag was absent.

Audit numbers:

- Textures scanned: 138
- Albedo candidates decoded: 26
- Albedo energy failures: 0
- Albedo energy warnings: 0
- Detail candidates: 13
- ORM candidates: 17
- Texture import issue textures: 5
- Materials scanned: 176
- Materials with packed masks: 9
- Materials with detail slots: 0
- Materials with issues: 31
- Issue counts: `NO_PACKED_ORM_OR_MASK_SLOT=22`, `NO_DETAIL_MAP_SLOT=31`

Cinematic Cheats used:

- ORM channel packing replaces three mask textures with one packed data texture.
- Shared detail overlays replace unique high-resolution albedo escalation.
- Clearcoat is a single-pass Fresnel/specular fake, not a second pass.
- Brushed metal is a tangent/bitangent lobe modulation fake, not full anisotropic reflection truth.
- GOD_MODE spends saved memory on detail/clearcoat/wetness fidelity while low tier keeps packed masks and disables expensive overlays.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us because this pass changed tooling/docs only.
- Avoided future second clearcoat pass: estimated 100-400 us on MX350 per affected view, pending Unity profiler.
- Brushed-metal fake versus heavier reflection/anisotropic truth: estimated sub-20 us shader ALU per visible cluster, pending RenderDoc/Unity profiler.
- Material unique VRAM model: 6.65 MB standard set -> 2.99 MB optimized set, about 55% reduction under documented assumptions.

Verification:

- `python -m py_compile Tools\MaterialAudit.py`: passed.
- `python Tools\MaterialAudit.py --root Assets\_Project --sample-size 256 --json Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.json`: passed.
- `python Tools\MaterialAudit.py --root Assets\_Project --sample-size 256 --json Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.json --markdown Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.md`: passed.
- `git diff --check` on owned files: passed.
- Runtime hot-path keyword scan on owned files: no matches.
- Unity import, Unity Console, Frame Debugger, RenderDoc, and Memory Profiler: PENDING VERIFICATION. No runtime assets or shaders were mutated.

Regression model:

- CPU: no runtime code touched; no gameplay Tick/Update path added.
- GC: no runtime code touched; expected runtime allocation delta is 0 B/frame.
- Memory: JSON/doc/tool additions only; material VRAM savings are doctrine/model until shader/material migration.
- Cadence: offline validator can run in CI; no frame cadence impact.
- Correctness: ORM layout conflict is documented and must be resolved before enforcing importer/material migration.

Final state:

- SURFACE DOCTRINE READY.
- PENDING UNITY IMPORT VERIFICATION for any future material/shader adoption.

## 2026-05-15 - Hardening Pass

What was wrong:

- Validator output was JSON-only.
- Texture import settings were not audited.
- First import-setting classifier had false positives because raw substring matching read `storms` as `orm`.

What was done:

- Added `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA.md`.
- Added import-setting checks to `Tools/MaterialAudit.py`.
- Added `--fail-on-import-issues` and `--fail-on-material-issues`.
- Replaced raw ORM substring detection with tokenized filename classification and UI/skybox exclusions.
- Updated doctrine with corrected numbers and concrete import issues.

Cinematic Cheats used:

- Same as core pass: channel-packed masks and shared overlays remain the recommended surface fake path.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Audit precision improved; false-positive import debt removed from the report.

Verification:

- `python -m py_compile Tools\MaterialAudit.py`: passed.
- Validator rerun with JSON and Markdown output: passed.
- Corrected audit: 0 albedo energy failures, 5 texture import issues, 31 material slot issues.
- Scoped import fail flag: exit 2 as expected.
- Scoped material fail flag: exit 3 as expected.

## 2026-05-15 - Evidence Export Pass

What was wrong:

- Markdown report still required manual copy work for owners who need issue lists.
- Report rows had issue codes but no direct action text.
- Some status/doc counts were stale after the classifier correction.

What was done:

- Added CSV exports for texture import issues, material issues, and detail candidates.
- Added recommendation text to Markdown report rows.
- Re-ran the full first-party audit with JSON, Markdown, and CSV output.
- Normalized final counts across status, rationale, doctrine, log, JSON, and Markdown.

Cinematic Cheats used:

- No new runtime cheats. This pass strengthens the handoff for channel-packed masks and shared detail overlays.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Triage time saved is human/CI time, not frame time.

Verification:

- `python -m py_compile Tools\MaterialAudit.py`: passed.
- Full audit with JSON, Markdown, and CSV output: passed.
- Final audit: 26 albedo candidates, 0 albedo energy failures, 5 texture import issue textures, 31 material slot issues.

## 2026-05-15 - Surface Classifier Cleanup

What was wrong:

- Broad `_d` matching allowed non-surface skybox names into albedo energy classification.
- Dead raw token constants remained after the stricter classifier was added.

What was done:

- Replaced albedo matching with token-based surface names.
- Excluded UI/skybox paths from albedo classification.
- Removed dead classifier constants.

Cinematic Cheats used:

- None. This is audit precision cleanup.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.

Verification:

- Full audit after cleanup: 138 textures, 26 albedo candidates, 0 energy failures, 5 import issue textures, 17 ORM candidates, 13 detail candidates, 176 materials, 31 material issues.

## 2026-05-15 - Regression Test Harness

What was wrong:

- The validator had edge-case logic with no focused regression tests.
- Manual full-project audits catch current corpus state but not classifier and export regressions.

What was done:

- Added `Tools/test_material_audit.py`.
- Covered skybox/UI surface exclusion, surface albedo detection, ORM token detection, albedo energy failure/pass, data/normal import debt, missing material ORM/detail slots, and Markdown/CSV recommendation exports.

Cinematic Cheats used:

- None. This pass protects the offline audit gate that enables channel-packed masks and shared detail overlays.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Offline regression coverage prevents material-audit drift before shader/material migration.

Verification:

- `python -m unittest Tools.test_material_audit`: passed 5 tests.
- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- Full first-party audit after test harness: passed with 138 textures, 26 albedo candidates, 0 energy failures, 5 import issue textures, 17 ORM candidates, 13 detail candidates, 176 materials, 31 material issues.

## 2026-05-15 - Prompt ORM Separation Pass

What was wrong:

- The validator counted legacy `_MaskMap` and `_MetallicGlossMap` slots as packed-mask readiness.
- That was too vague for the prompt contract: ORM must be R=AO, G=Roughness, B=Metallic.

What was done:

- Split prompt ORM detection from legacy/unknown packed mask detection.
- Added `NO_PROMPT_ORM_SLOT` and `LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW`.
- Added channel-packing migration candidates to JSON, Markdown, and CSV.
- Updated the doctrine with the stricter audit numbers.

Cinematic Cheats used:

- Channel packing remains the intended visual fake: one data texture replaces separate AO/roughness/metallic sources before any expensive material pass is considered.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Expected future saving remains VRAM/bandwidth, not CPU: prompt ORM prevents extra texture samples for separate mask maps.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 5 tests.
- Full first-party audit: 0 prompt ORM slots, 9 legacy mask slots, 31 channel-packing candidates, 31 material issue materials, 0 energy failures, 5 import issue textures.
- Scoped fail gates after issue-code change: import-debt root `Assets\_Project\Art\TEXTURES\Detali` returned exit 2; material-debt root `Assets\_Project\Art\Materials` returned exit 3.

## 2026-05-15 - Residency Model Pass

What was wrong:

- The audit reported material debt but did not quantify texture residency pressure.
- The 50% VRAM improvement target existed in doctrine but not in machine-readable audit output.

What was done:

- Added per-texture width, height, memory role, and estimated resident MiB.
- Added aggregate estimated texture MiB and a texture memory hotspot CSV.
- Added a channel-packing savings model to JSON and Markdown.
- Added a regression test for the mip-aware MiB estimate.

Cinematic Cheats used:

- Channel packing remains the selected fake: collapse AO/roughness/metallic data into one prompt ORM texture and spend the saved residency on shared detail overlays.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Offline model estimates 113.46 MiB potential residency reduction across 31 channel-packing candidates under the documented material model.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 6 tests.
- Full first-party audit: estimated texture residency 497.565 MiB, channel candidate standard 206.15 MiB, optimized 92.69 MiB, saved 113.46 MiB, reduction 55.0%.
- Scoped fail gates after residency model: import-debt root returned exit 2; material-debt root returned exit 3.

## 2026-05-15 - GOD_MODE Override Export Pass

What was wrong:

- Task 8's texture override list existed in doctrine but was not exported as structured batch data.

What was done:

- Added 12 tiered GOD_MODE texture override rows to the audit JSON and Markdown.
- Added `MaterialAudit_TECHNICAL_ARTIST_DATA_god_mode_texture_overrides.csv`.
- Added regression coverage for override CSV generation.

Cinematic Cheats used:

- Texture escalation is tier-gated: low hardware keeps tight caps, high hardware spends saved ORM/detail memory on controlled visual density.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- The export prevents uncontrolled high-tier texture bloat by pairing every GOD_MODE cap with a fallback.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 6 tests.
- Full first-party audit: `god_mode_override_count=12` with prior material and energy counts preserved.
- Scoped fail gates after override export: import-debt root returned exit 2; material-debt root returned exit 3.

## 2026-05-15 - Global Detail Overlay Plan Pass

What was wrong:

- The audit listed discovered detail candidates but did not export the hard-surface overlay plan required for the 20% more detail target.

What was done:

- Added 10 global detail overlay roles to JSON and Markdown.
- Added `MaterialAudit_TECHNICAL_ARTIST_DATA_global_detail_overlay_plan.csv`.
- Each row includes source status, target surfaces, TOASTER fallback, GOD_MODE rule, and expected detail gain.

Cinematic Cheats used:

- Shared overlays buy perceived micro-surface richness without unique per-material 4K textures or extra passes.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Future shader cost stays bounded because these are shared overlays and tier-gated samples, not material clones.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 6 tests.
- Full first-party audit: `global_detail_overlay_count=10`, minimum expected detail gain 20%, existing surface counts preserved.
- Scoped fail gates after detail plan export: import-debt root returned exit 2; material-debt root returned exit 3.

## 2026-05-15 - Unresolved Material Reference Pass

What was wrong:

- Raw material texture GUIDs leaked into audit artifacts after GUID resolution.
- Unity internal `unity_ShadowMasks` initially polluted the unresolved reference count and had to be filtered out.

What was done:

- Added `UNRESOLVED_TEXTURE_GUID` issue detection.
- Ignored Unity internal `unity_` texture properties.
- Added `MaterialAudit_TECHNICAL_ARTIST_DATA_unresolved_texture_refs.csv`.

Cinematic Cheats used:

- None. This is dependency hygiene for material migration.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Prevents future material migration from inheriting missing/external texture references.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 6 tests.
- Full first-party audit: 9 materials with unresolved texture refs, 27 unresolved refs, 37 material issue materials, 0 energy failures.
- Scoped fail gates after unresolved-reference pass: import-debt root returned exit 2; material-debt root returned exit 3.

## 2026-05-15 - Scoped Resolve Root Pass

What was wrong:

- Scoped material audits could not resolve texture GUIDs outside the material folder, inflating unresolved-reference counts.

What was done:

- Added `--resolve-root` to `Tools/MaterialAudit.py`.
- Added a regression test for scoped material scanning with wider GUID resolution.
- Re-ran full audit and scoped fail gates with explicit resolve roots.

Cinematic Cheats used:

- None. This is audit precision and CI hygiene.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Offline scoped gates avoid full texture scan work while retaining correct GUID resolution.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 7 tests.
- Full first-party audit with `--resolve-root Assets\_Project`: 37 material issue materials, 9 unresolved-reference materials, 27 unresolved refs, 0 energy failures.
- Scoped material gate with `--resolve-root Assets\_Project`: exit 3; unresolved refs dropped from 106 to 19 versus narrow-root-only resolution.

## 2026-05-15 - CLI Gate Regression Pass

What was wrong:

- The CI fail flags were proven by manual shell wrappers but not by automated subprocess tests.

What was done:

- Added a subprocess regression test that executes `Tools/MaterialAudit.py` as a real CLI process.
- The test asserts import-debt exit 2 and material-debt exit 3.
- Re-ran full audit artifact generation and scoped fail gates after the test pass.

Cinematic Cheats used:

- None. This is offline gate hardening.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Prevents CI drift where the Python API works but the command-line gate silently stops failing.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 8 tests.
- Full first-party audit: 0 energy failures, 5 import issue textures, 37 material issue materials, 31 channel-packing candidates, 113.46 MiB modeled savings.
- Scoped import gate with `--resolve-root Assets\_Project`: expected exit 2 confirmed.
- Scoped material gate with `--resolve-root Assets\_Project`: expected exit 3 confirmed, 19 unresolved refs after wider GUID resolution.
- Prompt re-extraction confirmed the same 8 numbered tasks and required `SURFACE DOCTRINE READY` status.

## 2026-05-15 - Unresolved Reference Gate Pass

What was wrong:

- Broken/unresolved material texture references were only detectable through broad material issue gates.
- That mixed dependency faults with expected ORM/detail migration debt.

What was done:

- Added `--fail-on-unresolved-refs` to `Tools/MaterialAudit.py`.
- Exit code 4 now means unresolved material texture references.
- Added `gate_exit_codes` to JSON and a Gate Exit Codes section to Markdown.
- Updated doctrine with the gate contract.
- Extended the CLI subprocess test to prove exit 4.

Cinematic Cheats used:

- None. This is offline dependency hygiene.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Prevents material migration from spending shader/import work on assets with unresolved texture dependencies.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 8 tests.
- Full first-party audit: 0 energy failures, 5 import issue textures, 37 material issue materials, 9 unresolved-reference materials, 27 unresolved refs.
- Scoped import gate: expected exit 2 confirmed.
- Scoped unresolved-reference gate: expected exit 4 confirmed, 19 unresolved refs under `Assets\_Project\Art\Materials` with `--resolve-root Assets\_Project`.
- Scoped broad material gate: expected exit 3 confirmed.

## 2026-05-15 - Markdown Report Structure Pass

What was wrong:

- The generated Markdown report placed `Import Issue Counts` before unrelated VRAM and tier-override sections, then emitted the actual issue table later.

What was done:

- Moved the `Import Issue Counts` heading to the table emission point.
- Added a regression assertion that the heading is followed by the issue-count table header.
- Regenerated JSON/Markdown/CSV audit artifacts.

Cinematic Cheats used:

- None. This is report integrity.

Exact Microseconds saved:

- Runtime code changed: none.
- Immediate runtime CPU saving: 0 us.
- Prevents handoff errors when texture import debt is assigned from Markdown.

Verification:

- `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py`: passed.
- `python -m unittest Tools.test_material_audit`: passed 8 tests.
- Full first-party audit regenerated with existing counts preserved.
- Markdown readback confirms Gate Exit Codes and Import Issue Counts tables are present; import issue table starts at the corrected section.
- Scoped import/unresolved/material gates returned expected exits 2/4/3.
