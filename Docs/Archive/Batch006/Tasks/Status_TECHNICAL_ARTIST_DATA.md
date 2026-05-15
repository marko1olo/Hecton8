# TECHNICAL_ARTIST_DATA - PBR_MATERIAL_REFACTOR_SCOUT

Status: SURFACE DOCTRINE READY
Unity verification: PENDING UNITY IMPORT VERIFICATION
Domain: Surface Materials / PBR Texture Data
Prompt source: `Docs/Tasks/CURRENT_BATCH.md`
Task count: 8 numbered tasks in extracted XML. Header claims 15; rejected as stale.

## Mandates Loaded

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt - tooling only; no runtime hot path.
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt - MX350 texture budget and tier rules.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt - shader/material fakes before simulation or second pass.
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt - SRP Batcher, channel packing, texture budget.
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt - noir color/contrast constraints.
- STRM_Async_Asset_Upload_Texture_Settings.txt - texture upload/residency implications.

## Iteration Log

### Loop 0 - Prompt / Mandate / Hygiene

- [x] Extract own XML block | DOD: CLI extraction from `CURRENT_BATCH.md`, not MCP read. Alternatives Rejected: using neighboring prompts or stale header count. Estimate: 20 us runtime impact, offline only.
- [x] Verify agent state files | DOD: missing status/rationale confirmed before creation. Alternatives Rejected: reading archived batch logs. Estimate: 0 us runtime impact.
- [x] Load 6 relevant mandates | DOD: render, VRAM, zero-GC, visual-fake, noir, texture upload mandates loaded. Alternatives Rejected: bulk-ingesting every mandate. Estimate: 0 us runtime impact.

### Loop 1 - Tasks 1-5

- [x] Task 1 ORM PACKING SPEC | DOD: `Docs/TECH_ART_PBR_SURFACE_DOCTRINE.md` defines prompt-authoritative ORM as R=AO, G=Roughness, B=Metallic and records conflict with older packed-mask mandate. Alternatives Rejected: mixed channel layouts or shader mutation without integrator decision. Estimate: saves about 40-70% mask VRAM depending source import.
- [x] Task 2 MICRO-DETAIL LIBRARY | DOD: corrected first-party texture scan found 13 detail candidates; doctrine lists 10 actual detail maps and separately lists missing hard-surface overlays. Alternatives Rejected: claiming nonexistent scratch/dust/carbon maps exist. Estimate: 0 us CPU until shader uses shared detail; potential unique texture memory saved by sharing overlays.
- [x] Task 3 CLEARCOAT FAKE | DOD: single-pass fake clearcoat parameters and HLSL math documented for wet glass/chrome. Alternatives Rejected: second-pass rendering and material clone stacks. Estimate: avoids one additional transparent/spec pass, approximate saving 100-400 us on MX350 per affected view versus extra pass.
- [x] Task 4 ANISOTROPIC FAKE | DOD: cockpit brushed-metal lobe modulation documented using tangent/bitangent banding and shared brush detail. Alternatives Rejected: full anisotropic GGX migration or ray-traced reflection truth. Estimate: sub-20 us shader ALU impact per visible cluster versus multi-pass/ray-based reflection waste.
- [x] Task 5 MATERIAL VALIDATOR | DOD: `Tools/MaterialAudit.py` created, py_compile passed, and first-party audit executed to JSON. Alternatives Rejected: Unity editor mutator and full third-party decode as default. Estimate: 0 us runtime impact; offline gate only.

### Loop 2 - Feedback

- [x] Task 6 SELF-AUDIT LOOP 1 | DOD: standard 5-texture material model compared against optimized albedo+normal+512 ORM+shared detail model. Alternatives Rejected: balanced middle-ground without target VRAM delta. Estimate: 6.65 MB -> 2.99 MB per set under documented assumption, about 55% unique texture VRAM reduction.
- [x] Python energy-conservation test executed | DOD: `python Tools\MaterialAudit.py --root Assets\_Project --sample-size 256 --json Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.json`. Alternatives Rejected: visual-only judgement. Estimate: 0 us runtime impact; 0 albedo failures, 0 warnings.
- [x] Surface/material audit reviewed | DOD: JSON reviewed: 138 textures, 26 albedo candidates, 17 ORM candidates, 13 detail candidates, 176 materials, 31 material issues, 5 texture import issues. Alternatives Rejected: chat-only unverifiable summary. Estimate: no runtime change; material debt quantified.

### Loop 3 - Doctrine / Batch Update

- [x] Task 7 NASA-Punk Noir rationale | DOD: doctrine states functional/worn surface rules, diffuse brightness discipline, and wear semantics. Alternatives Rejected: generic clean sci-fi material direction. Estimate: visual improvement through masks/details, no CPU change.
- [x] Task 8 GOD_MODE texture overrides | DOD: doctrine includes GOD_MODE resolution/format table and load-shed rules. Alternatives Rejected: raising MX350 defaults or project settings. Estimate: high-tier VRAM spends are gated; low tier remains protected.

### Loop 4 - Self-Review

- [x] Re-read owned code/tool | DOD: `Tools/MaterialAudit.py` was compiled after refactor; stale `guid_map` bug fixed. Alternatives Rejected: leaving failed audit as "tooling issue." Estimate: 0 us runtime impact.
- [x] Validate no runtime hot-path changes | DOD: changed files are Python/docs/logs/status only. Alternatives Rejected: material/shader/prefab mutation before convention conflict is resolved. Estimate: 0 B/frame, 0 us runtime.
- [x] Re-extract prompt after 3+ tasks | DOD: `CURRENT_BATCH.md` re-extracted, still 8 numbered tasks. Alternatives Rejected: continuing from memory after context risk. Estimate: 0 us runtime impact.

### Loop 5 - Final Inquisition

- [x] Polish mandate checked after all tasks complete/blocked | DOD: `CURRENT_BATCH.md` was scanned after core task completion; no `<POLISH_MANDATE>` tag exists. Alternatives Rejected: inventing a polish directive. Estimate: 0 us runtime impact.
- [x] Final log appended | DOD: `Docs/AgentLogs/LOG_TECHNICAL_ARTIST_DATA.md` created with final breakdown. Alternatives Rejected: chat-only report. Estimate: 0 us runtime impact.
- [x] STATUS final state set to SURFACE DOCTRINE READY or blocked | DOD: all 8 prompt tasks complete, no blocked tasks. Alternatives Rejected: claiming Unity import verification without Unity console/profiler evidence. Estimate: 0 us runtime impact.

### Loop 6 - Hardening Pass After User Escalation

- [x] Added import-setting validation | DOD: validator now checks sRGB, normal-map type, mips, compression/readability where visible in `.meta`. Alternatives Rejected: filename-only audit. Estimate: 0 us runtime impact; 5 suspect texture imports found.
- [x] Added Markdown audit report | DOD: `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA.md` generated for human review. Alternatives Rejected: JSON-only evidence. Estimate: 0 us runtime impact.
- [x] Fixed false positive ORM classification | DOD: tokenized classifier no longer treats `storms` as ORM and excludes UI/skybox paths from surface data checks. Alternatives Rejected: accepting noisy audit evidence. Estimate: 0 us runtime impact.
- [x] Added CI fail flags | DOD: `--fail-on-import-issues` and `--fail-on-material-issues` added to `Tools/MaterialAudit.py`. Alternatives Rejected: manual-only validation. Estimate: 0 us runtime impact.
- [x] Verified CI fail flags | DOD: scoped import-debt run returned exit 2; scoped material-debt run returned exit 3. Alternatives Rejected: untested CLI switches. Estimate: 0 us runtime impact.

### Loop 7 - Evidence Export Pass After User Escalation

- [x] Added recommendation text | DOD: Markdown report now emits direct recommendations for texture import and material slot issues. Alternatives Rejected: issue-code-only handoff. Estimate: 0 us runtime impact.
- [x] Added CSV exports | DOD: generated texture import issue, material issue, and detail candidate CSVs under `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA_*`. Alternatives Rejected: Markdown-only handoff. Estimate: 0 us runtime impact.
- [x] Re-ran full audit with CSV output | DOD: final audit reports 138 textures, 26 albedo candidates, 0 energy failures, 5 import issue textures, 17 ORM candidates, 13 detail candidates, 176 materials, and 31 material issues. Alternatives Rejected: relying on stale pre-CSV audit numbers. Estimate: 0 us runtime impact.

### Loop 8 - Surface Classifier Cleanup

- [x] Tightened albedo classifier | DOD: albedo detection now uses tokens and excludes UI/skybox paths, removing `panorama_den.png` style false positives from PBR surface energy testing. Alternatives Rejected: broad `_d` substring matching. Estimate: 0 us runtime impact.
- [x] Removed dead classifier constants | DOD: obsolete raw `ALBEDO_TOKENS` and `ORM_TOKENS` constants removed from `Tools/MaterialAudit.py`. Alternatives Rejected: leaving unused audit code. Estimate: 0 us runtime impact.

### Loop 9 - Regression Test Harness

- [x] Added synthetic material-audit tests | DOD: `Tools/test_material_audit.py` covers classifier exclusions, albedo energy failure/pass, import-setting debt, material slot debt, and Markdown/CSV recommendation exports. Alternatives Rejected: relying on manual audit reruns only. Estimate: 0 us runtime impact; offline QA only.
- [x] Executed Python regression suite | DOD: `python -m unittest Tools.test_material_audit` passed 6 tests after the residency-model addition. Alternatives Rejected: treating code inspection as validator proof. Estimate: 0 us runtime impact.
- [x] Recompiled audit tooling | DOD: `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py` passed. Alternatives Rejected: leaving late test-file syntax unchecked. Estimate: 0 us runtime impact.
- [x] Re-ran full first-party audit after test harness | DOD: `python Tools\MaterialAudit.py --root Assets\_Project --sample-size 256 --json Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.json --markdown Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.md --csv-prefix Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA` passed and preserved final counts. Alternatives Rejected: relying on pre-test audit artifacts. Estimate: 0 us runtime impact.

### Loop 10 - Prompt ORM Separation Pass

- [x] Separated prompt ORM from legacy mask slots | DOD: `Tools/MaterialAudit.py` now distinguishes prompt ORM (`R=AO, G=Roughness, B=Metallic`) from `_MaskMap`/`_MetallicGlossMap` legacy or unknown packed masks. Alternatives Rejected: counting legacy masks as prompt-ready. Estimate: 0 us runtime impact.
- [x] Added channel-packing migration candidates | DOD: validator now emits `channel_packing_candidate_count`, priority counts, Markdown section, and `MaterialAudit_TECHNICAL_ARTIST_DATA_channel_packing_candidates.csv`. Alternatives Rejected: material issue list without migration triage. Estimate: 0 us runtime impact.
- [x] Re-ran full first-party audit after ORM separation | DOD: final audit reports 0 prompt ORM slots, 9 legacy mask slots, 31 channel-packing candidates, 31 material issue materials, 0 energy failures, and 5 import issue textures. Alternatives Rejected: leaving old packed-mask counts ambiguous. Estimate: 0 us runtime impact.
- [x] Re-verified CI fail gates after ORM separation | DOD: `Assets\_Project\Art\TEXTURES\Detali` returned import-debt exit 2 and `Assets\_Project\Art\Materials` returned material-debt exit 3. Alternatives Rejected: trusting earlier gate proof after issue-code changes. Estimate: 0 us runtime impact.

### Loop 11 - Residency Model Pass

- [x] Added texture residency estimates | DOD: validator now records width, height, memory role, per-texture estimated resident MiB, aggregate estimated texture MiB, and texture memory hotspot CSV. Alternatives Rejected: leaving VRAM pressure as only a prose model. Estimate: 0 us runtime impact; offline BC-class estimate only.
- [x] Added channel-packing savings model to audit output | DOD: JSON/Markdown now report 31 channel candidates as 206.15 MiB standard -> 92.69 MiB optimized, saving 113.46 MiB or 55.0% under the documented model. Alternatives Rejected: unquantified "50% less VRAM" claim. Estimate: 0 us runtime impact.
- [x] Re-ran full audit after residency model | DOD: final audit reports 497.565 MiB estimated texture residency, 0 energy failures, 5 import issue textures, 0 prompt ORM slots, 9 legacy mask slots, and 31 channel candidates. Alternatives Rejected: stale JSON/Markdown/CSV artifacts. Estimate: 0 us runtime impact.
- [x] Re-verified fail gates after residency model | DOD: import-debt scoped run returned exit 2 and material-debt scoped run returned exit 3 with the new summary fields present. Alternatives Rejected: assuming added summary fields did not affect exit paths. Estimate: 0 us runtime impact.

### Loop 12 - GOD_MODE Override Export Pass

- [x] Converted GOD_MODE overrides to audit data | DOD: `Tools/MaterialAudit.py` now emits 12 tiered texture override rows in JSON/Markdown and `MaterialAudit_TECHNICAL_ARTIST_DATA_god_mode_texture_overrides.csv`. Alternatives Rejected: prose-only batch updater handoff. Estimate: 0 us runtime impact.
- [x] Re-tested export path | DOD: `python -m unittest Tools.test_material_audit` passed 6 tests including GOD_MODE override export assertion. Alternatives Rejected: assuming CSV generation from implementation only. Estimate: 0 us runtime impact.
- [x] Re-ran full audit after override export | DOD: final audit reports `god_mode_override_count=12` with prior surface counts preserved. Alternatives Rejected: stale artifacts without the new override CSV. Estimate: 0 us runtime impact.
- [x] Re-verified fail gates after override export | DOD: import-debt scoped run still returned exit 2 and material-debt scoped run still returned exit 3 with `god_mode_override_count=12` present. Alternatives Rejected: assuming new summary output did not disturb fail exits. Estimate: 0 us runtime impact.

### Loop 13 - Global Detail Overlay Plan Pass

- [x] Converted detail-overlay target into audit data | DOD: `Tools/MaterialAudit.py` now emits 10 global detail overlay roles with TOASTER/GOD_MODE rules and expected detail-gain percentages. Alternatives Rejected: detail-map guidance only in prose. Estimate: 0 us runtime impact.
- [x] Added global detail overlay CSV | DOD: generated `MaterialAudit_TECHNICAL_ARTIST_DATA_global_detail_overlay_plan.csv`; minimum expected detail gain is 20%. Alternatives Rejected: leaving the "20% more detail" target unstructured. Estimate: 0 us runtime impact.
- [x] Re-ran full audit after detail plan export | DOD: final audit reports `global_detail_overlay_count=10` with 138 textures, 0 energy failures, 31 channel candidates, and 12 GOD_MODE override rows preserved. Alternatives Rejected: stale artifacts without the new CSV. Estimate: 0 us runtime impact.
- [x] Re-verified fail gates after detail plan export | DOD: import-debt scoped run still returned exit 2 and material-debt scoped run still returned exit 3 with `global_detail_overlay_count=10` present. Alternatives Rejected: assuming new report sections could not affect exit paths. Estimate: 0 us runtime impact.

### Loop 14 - Unresolved Material Reference Pass

- [x] Added unresolved texture GUID detection | DOD: validator now flags material texture slots that remain raw GUIDs after first-party GUID resolution, while ignoring Unity internal `unity_` lighting properties. Alternatives Rejected: treating raw GUID leakage in reports as acceptable evidence. Estimate: 0 us runtime impact.
- [x] Added unresolved texture reference CSV | DOD: generated `MaterialAudit_TECHNICAL_ARTIST_DATA_unresolved_texture_refs.csv` with 9 affected materials and 27 unresolved refs. Alternatives Rejected: burying unresolved refs inside channel candidate rows. Estimate: 0 us runtime impact.
- [x] Re-ran full audit after unresolved-reference pass | DOD: final audit reports 37 material issue materials, 9 unresolved-reference materials, 27 unresolved refs, 0 energy failures, 31 channel candidates, and prior GOD_MODE/detail exports preserved. Alternatives Rejected: stale material debt counts. Estimate: 0 us runtime impact.
- [x] Re-verified fail gates after unresolved-reference pass | DOD: import-debt scoped run still returned exit 2 and material-debt scoped run still returned exit 3 with unresolved-reference summary fields present. Alternatives Rejected: assuming new material issue type did not affect exit paths. Estimate: 0 us runtime impact.

### Loop 15 - Scoped Resolve Root Pass

- [x] Added `--resolve-root` support | DOD: scoped audits can scan a narrow folder while resolving material texture GUIDs against `Assets\_Project`. Alternatives Rejected: forcing every scoped material gate to scan all textures. Estimate: 0 us runtime impact.
- [x] Added scoped-resolution regression test | DOD: `python -m unittest Tools.test_material_audit` passed 7 tests, including a material folder scan resolving a texture outside the scan root. Alternatives Rejected: proving behavior only with project assets. Estimate: 0 us runtime impact.
- [x] Re-ran full audit with explicit resolve root | DOD: `--root Assets\_Project --resolve-root Assets\_Project` preserved full audit counts: 37 material issue materials, 9 unresolved-reference materials, 27 unresolved refs, 0 energy failures. Alternatives Rejected: stale JSON without resolve-root metadata. Estimate: 0 us runtime impact.
- [x] Re-verified scoped fail gates with resolve root | DOD: import-debt scoped run returned exit 2; material-debt scoped run returned exit 3 and dropped scoped unresolved refs from 106 to 19 by resolving against `Assets\_Project`. Alternatives Rejected: accepting inflated scoped unresolved debt. Estimate: 0 us runtime impact.

### Loop 16 - CLI Gate Regression Pass

- [x] Added subprocess CLI fail-gate test | DOD: `Tools/test_material_audit.py` now launches `Tools/MaterialAudit.py` as a real process and asserts import-debt exit 2 plus material-debt exit 3. Alternatives Rejected: relying only on direct function tests or manual shell wrappers. Estimate: 0 us runtime impact.
- [x] Recompiled and re-ran regression suite | DOD: `python -m py_compile Tools\MaterialAudit.py Tools\test_material_audit.py` passed and `python -m unittest Tools.test_material_audit` passed 8 tests. Alternatives Rejected: leaving CLI exit-code behavior unprotected. Estimate: 0 us runtime impact.
- [x] Re-ran full audit artifacts after CLI test pass | DOD: full audit with `--root Assets\_Project --resolve-root Assets\_Project` preserved 0 energy failures, 5 import issue textures, 37 material issue materials, 31 channel candidates, and 113.46 MiB modeled savings. Alternatives Rejected: stale artifacts after test harness changes. Estimate: 0 us runtime impact.
- [x] Re-verified scoped fail gates after CLI test pass | DOD: scoped import gate still returned expected exit 2 and scoped material gate still returned expected exit 3 with 19 resolved-root unresolved refs. Alternatives Rejected: assuming subprocess test proof replaced project-corpus proof. Estimate: 0 us runtime impact.
- [x] Re-extracted prompt after loop 16 | DOD: CLI regex extraction confirmed the same 8 numbered tasks and required status `SURFACE DOCTRINE READY`. Alternatives Rejected: continuing from compressed-chat memory. Estimate: 0 us runtime impact.

### Loop 17 - Unresolved Reference Gate Pass

- [x] Added dedicated unresolved-reference fail gate | DOD: `--fail-on-unresolved-refs` returns exit 4 when material texture GUIDs cannot be resolved. Alternatives Rejected: treating broken references as indistinguishable from expected ORM/detail migration debt. Estimate: 0 us runtime impact.
- [x] Published exit-code contract in reports | DOD: generated JSON now includes `gate_exit_codes`, Markdown includes a Gate Exit Codes section, and doctrine lists exit codes 1/2/3/4. Alternatives Rejected: relying on source-code inspection for CI behavior. Estimate: 0 us runtime impact.
- [x] Extended CLI subprocess test | DOD: `python -m unittest Tools.test_material_audit` still passes 8 tests and now asserts unresolved-reference exit 4 in addition to exit 2 and exit 3. Alternatives Rejected: manual-only proof for the new gate. Estimate: 0 us runtime impact.
- [x] Re-ran full audit and scoped gates | DOD: full audit preserved 0 energy failures, 5 import issues, 37 material issue materials, 27 unresolved refs; scoped import/material/unresolved gates returned expected exits 2/3/4. Alternatives Rejected: stale artifacts after CLI contract change. Estimate: 0 us runtime impact.

### Loop 18 - Markdown Report Structure Pass

- [x] Fixed import-issue Markdown section ordering | DOD: `## Import Issue Counts` now emits directly before its issue-count table instead of before unrelated VRAM/GOD_MODE sections. Alternatives Rejected: leaving a misleading empty header in the CTO-facing report. Estimate: 0 us runtime impact.
- [x] Added export regression assertion | DOD: test now asserts `## Import Issue Counts` is followed by the issue table header. Alternatives Rejected: trusting manual report inspection only. Estimate: 0 us runtime impact.
- [x] Re-ran audit artifacts and scoped gates after report fix | DOD: full audit preserved counts; Markdown readback shows Gate Exit Codes and Import Issue Counts tables; scoped gates returned exits 2/4/3. Alternatives Rejected: stale Markdown after report generator fix. Estimate: 0 us runtime impact.

### Loop 19 - Texture Budget Gate Pass

- [x] Added texture-budget model | DOD: audit output now reports estimated MiB, 900 MiB budget, 810 MiB warning threshold, used ratio, and PASS/WARN/FAIL status. Alternatives Rejected: leaving budget enforcement as prose only. Estimate: 0 us runtime impact.
- [x] Added texture-budget fail gate | DOD: `--fail-on-texture-budget` returns exit 5 when estimated residency exceeds `--texture-budget-mib`. Alternatives Rejected: folding budget overflow into material issue debt. Estimate: 0 us runtime impact.
- [x] Added budget regression tests | DOD: test suite now covers budget PASS/WARN/FAIL and subprocess exit 5; `python -m unittest Tools.test_material_audit` passed 9 tests. Alternatives Rejected: manual threshold proof only. Estimate: 0 us runtime impact.
- [x] Re-ran project audit with budget gate | DOD: full audit with `--fail-on-texture-budget` passed at 497.565/900.0 MiB, status PASS; import/unresolved/material scoped gates still returned expected exits 2/4/3. Alternatives Rejected: stale artifacts without budget fields. Estimate: 0 us runtime impact.

### Loop 20 - Albedo Read Error Gate Pass

- [x] Added texture read-error reporting | DOD: audit output now counts all texture read errors and exports `MaterialAudit_TECHNICAL_ARTIST_DATA_texture_read_errors.csv`. Alternatives Rejected: silently swallowing Pillow decode failures. Estimate: 0 us runtime impact.
- [x] Added albedo read-error fail gate | DOD: `--fail-on-texture-read-errors` returns exit 6 only when albedo candidates cannot be decoded for energy validation. Alternatives Rejected: failing the energy gate on non-albedo EXR reflection probe decode support. Estimate: 0 us runtime impact.
- [x] Added read-error regression tests | DOD: test suite now covers corrupt albedo reporting and subprocess exit 6; `python -m unittest Tools.test_material_audit` passed 10 tests. Alternatives Rejected: manual corrupt-file proof only. Estimate: 0 us runtime impact.
- [x] Re-ran project audit with read-error gate | DOD: full audit reports 1 total texture read warning (`ReflectionProbe-0.exr`), 0 albedo read errors, 0 energy failures, budget PASS, and expected scoped exits 2/4/3. Alternatives Rejected: marking non-albedo EXR decode support as PBR energy failure. Estimate: 0 us runtime impact.

### Loop 21 - Generated Lighting Texture Exclusion Pass

- [x] Excluded scene-generated lighting textures | DOD: scanner skips scene `ReflectionProbe`, `Lightmap`, and `LightingData` EXR/HDR files because they are not surface albedo/ORM/normal/detail data. Alternatives Rejected: reporting Pillow EXR decode support as PBR surface debt. Estimate: 0 us runtime impact.
- [x] Added generated-lighting exclusion test | DOD: test suite now proves a bogus `Scenes/.../ReflectionProbe-0.exr` is skipped, while corrupt albedo still fails read validation. Alternatives Rejected: ad hoc path cleanup without regression coverage. Estimate: 0 us runtime impact.
- [x] Re-ran full audit and scoped gates | DOD: full audit now reports 137 textures, 0 texture read errors, 0 albedo read errors, 0 energy failures, budget PASS; scoped gates returned expected exits 2/4/3. Alternatives Rejected: leaving non-surface scene generated texture noise in the CTO report. Estimate: 0 us runtime impact.

### Loop 22 - Energy Warning Gate Pass

- [x] Added optional energy-warning fail gate | DOD: `--fail-on-energy-warnings` returns exit 7 when albedo bright-area warnings are present and hard failures are absent. Alternatives Rejected: treating near-threshold bright-area risk as non-blocking forever. Estimate: 0 us runtime impact.
- [x] Added synthetic warning coverage | DOD: test suite now creates a dark albedo with localized white patches and asserts `WARN`, plus subprocess exit 7. Alternatives Rejected: relying on current corpus having zero warnings. Estimate: 0 us runtime impact.
- [x] Re-ran full audit and scoped gates | DOD: full audit with `--fail-on-energy-warnings` passed with 0 warnings and 0 failures; import/unresolved/material scoped gates returned expected exits 2/4/3. Alternatives Rejected: stale artifacts without the new gate contract. Estimate: 0 us runtime impact.

### Loop 23 - CI Surface Gate Profile Pass

- [x] Added `--ci-surface-gates` profile | DOD: profile enables current-corpus safe gates for energy warnings, albedo read errors, and texture budget. Alternatives Rejected: enabling broad import/material/unresolved gates in the profile while current assets still contain known migration debt. Estimate: 0 us runtime impact.
- [x] Published gate profile in reports | DOD: JSON and Markdown now include `gate_profiles.surface_safe = energy_warnings, albedo_read_errors, texture_budget`. Alternatives Rejected: leaving CI profile behavior discoverable only from source. Estimate: 0 us runtime impact.
- [x] Added subprocess profile regression coverage | DOD: `python -m unittest Tools.test_material_audit` now passes 12 tests and proves `--ci-surface-gates` returns exit 7 for warning-grade albedo. Alternatives Rejected: manual CLI proof only. Estimate: 0 us runtime impact.
- [x] Re-ran full audit and scoped gates after profile pass | DOD: full audit with `--ci-surface-gates` passed at 137 textures, 0 energy warnings, 0 albedo read errors, 497.565/900.0 MiB texture budget PASS; import/unresolved/material scoped gates returned expected exits 2/4/3. Alternatives Rejected: stale artifacts without the profile output. Estimate: 0 us runtime impact.

### Loop 24 - Active Gate Artifact Evidence Pass

- [x] Added active gate metadata | DOD: generated reports now include `active_gate_profiles` and `active_gates` in addition to available profile definitions. Alternatives Rejected: relying on stdout alone to prove which gate mode produced JSON/Markdown artifacts. Estimate: 0 us runtime impact.
- [x] Added active gate Markdown section | DOD: Markdown now emits `## Active Gates` with active profile and active gate list. Alternatives Rejected: JSON-only metadata that the CTO-facing report would not expose. Estimate: 0 us runtime impact.
- [x] Extended profile subprocess regression | DOD: regression test now writes JSON/Markdown during a profile-gated warning run and asserts active profile/gate evidence in both artifacts. Alternatives Rejected: trusting report metadata by source inspection. Estimate: 0 us runtime impact.
- [x] Re-ran full audit and scoped gates after active metadata pass | DOD: full audit with `--ci-surface-gates` records `active_gate_profiles=surface_safe` and `active_gates=energy_failures,energy_warnings,albedo_read_errors,texture_budget`; import/unresolved/material scoped gates returned expected exits 2/4/3. Alternatives Rejected: stale artifacts without active gate readback. Estimate: 0 us runtime impact.

### Loop 25 - Channel Packing Gate Pass

- [x] Added dedicated channel-packing candidate gate | DOD: `--fail-on-channel-packing-candidates` returns exit 8 when materials are missing prompt ORM channel packing. Alternatives Rejected: using broad material issue exit 3 for channel-packing jobs, because that mixes ORM migration with detail/unresolved-reference debt. Estimate: 0 us runtime impact.
- [x] Added active-gate metadata for channel gate | DOD: CLI summary and generated report metadata record `channel_packing_candidates` when that gate is active. Alternatives Rejected: hidden fail mode with no report evidence. Estimate: 0 us runtime impact.
- [x] Extended subprocess regression | DOD: `python -m unittest Tools.test_material_audit` still passes 12 tests and now asserts channel-packing exit 8 plus active-gate stdout. Alternatives Rejected: manual-only exit-code proof. Estimate: 0 us runtime impact.
- [x] Re-ran full audit and scoped gates after channel gate | DOD: full surface-safe audit still passes; scoped `Art/Materials` channel gate returns expected exit 8 with 23 channel candidates, while import/unresolved/material scoped gates return expected exits 2/4/3. Alternatives Rejected: adding channel gate to `--ci-surface-gates` while current assets intentionally fail channel migration. Estimate: 0 us runtime impact.

### Loop 26 - Detail Map Gate Pass

- [x] Added dedicated detail-map missing gate | DOD: `--fail-on-detail-map-missing` returns exit 9 when base materials have no detail-map slots. Alternatives Rejected: relying on broad material issue exit 3, because detail debt is the prompt's separate "20% more detail" target. Estimate: 0 us runtime impact.
- [x] Added detail missing material export | DOD: Markdown now includes `Detail Map Missing Materials` and CSV export now writes `MaterialAudit_TECHNICAL_ARTIST_DATA_detail_map_missing_materials.csv`. Alternatives Rejected: forcing artists to scrape broad material issue CSV for detail-map work. Estimate: 0 us runtime impact.
- [x] Extended subprocess/export regression | DOD: `python -m unittest Tools.test_material_audit` still passes 12 tests and now asserts detail-map exit 9, active-gate stdout, Markdown section, and CSV export. Alternatives Rejected: manual-only proof. Estimate: 0 us runtime impact.
- [x] Re-ran full audit and scoped gates after detail gate | DOD: full surface-safe audit still passes and reports 31 first-party materials missing detail maps; scoped `Art/Materials` detail gate returns expected exit 9 with 23 missing detail slots, while channel/import/unresolved/material gates return expected exits 8/2/4/3. Alternatives Rejected: adding detail gate to `--ci-surface-gates` while current assets intentionally fail detail migration. Estimate: 0 us runtime impact.
