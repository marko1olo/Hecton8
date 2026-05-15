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
