# LOG_TECHNICAL_ARTIST_DATA

## 2026-05-15 - PBR_MATERIAL_REFACTOR_SCOUT

Status: SURFACE DOCTRINE READY - PENDING UNITY IMPORT VERIFICATION

What was wrong:

- The prompt header claimed 15 tasks, but the XML contained 8 numbered tasks.
- First-party albedo luminance was not objectively tested.
- First-party material usage was weak: only 9 of 173 scanned materials use packed masks and 0 use detail slots.
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
- Albedo candidates decoded: 28
- Albedo energy failures: 0
- Albedo energy warnings: 0
- Detail candidates: 13
- ORM candidates: 17
- Texture import issue textures: 6
- Materials scanned: 173
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
- Corrected audit: 0 albedo energy failures, 6 texture import issues, 31 material slot issues.
- Scoped import fail flag: exit 2 as expected.
- Scoped material fail flag: exit 3 as expected.
