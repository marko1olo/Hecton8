# Status_VRAM_ASSET_SCOUT

Agent: VRAM_ASSET_SCOUT
Role: TOOLING_ENGINEER
Domain: VRAM and memory budget asset audit
Prompt task count: 9
Status: VRAM AUDITED
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM

## Loaded Mandates

- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- STRM_Async_Asset_Upload_Texture_Settings.txt
- STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- QA_Evidence_Text_Filter_Audit.txt

## Hygiene

- [x] Batch prompt extracted by CLI | DOD: regex extraction from Docs/Tasks/CURRENT_BATCH.md captured only AGENT_PROMPT id VRAM_ASSET_SCOUT | Alternatives Rejected: MCP/basic file preview because prompt protocol requires CLI extraction | Microseconds estimate: 0us runtime, offline only
- [x] Existing agent state checked | DOD: Status_VRAM_ASSET_SCOUT.md and Rationale_VRAM_ASSET_SCOUT.md were missing before creation | Alternatives Rejected: reusing other agent logs, forbidden by batch hygiene | Microseconds estimate: 0us runtime, offline only

## Task Checklist

- [x] Task 1: Texture inventory CSV | DOD: Docs/Reports/VRAM_Budget_Audit.csv generated with 1,652 import-root texture rows, 302 mesh rows, 1 RenderTexture row, expanded Unity texture container coverage, GLB/GLTF mesh-source coverage, source-container risk flags, texture importer metadata, mesh importer metadata, split redline CSVs, JSON summary, and remediation report companion | Alternatives Rejected: manual spreadsheet and non-import Docs/AgentLogs screenshot pollution because neither is repeatable VRAM evidence | Microseconds estimate: 0us runtime, offline tooling only
- [x] Task 2: BC7 size calculation | DOD: each texture row includes BC7 bytes, MiB, and full-mip MiB using width * height * 1 BPP and 4/3 mip factor | Alternatives Rejected: PNG/JPG file size because disk compression is not GPU residency | Microseconds estimate: 0us runtime, static VRAM prevention only
- [x] Task 3: Redline detection | DOD: 801 texture rows flagged with VRAM CRIME for >2048 dimension, import max >2048, or static RGBA32/uncompressed suspects; 23 source-container risk rows flagged across HDR/EXR/PSD/GIF/TGA/TIFF/BMP | Alternatives Rejected: silently trusting Unity importer defaults without metadata readback | Microseconds estimate: 0us measured, PENDING PROFILER for actual saved frame time
- [x] Task 4: Polygon inquisition | DOD: OBJ triangles counted; ASCII/binary FBX PolygonVertexIndex parsed when readable; GLB/GLTF triangles counted from glTF accessors; file size, static geometry estimate, and mesh importer metadata recorded for every mesh; 293 mesh redline/risk rows exposed after geometry, Read/Write, blend-shape, compression, collider, and LOD checks, with 16 first-party mesh importer risk rows split out | Alternatives Rejected: Unity-only ModelImporter dependency because this agent is offline tooling | Microseconds estimate: 0us runtime, risk reduction only
- [x] Task 5: Atlas suggestions | DOD: summary identifies five first-party small-texture atlas groups after correcting the initial too-strict grouping pass | Alternatives Rejected: editor-icon atlas noise from third-party folders | Microseconds estimate: 0us measured, reduced SetPass/VRAM pending art integration
- [x] Task 6: Tools/MemoryBudgetCheck.py | DOD: repeatable checker created, AST syntax parse passed, and read-only unit tests passed | Alternatives Rejected: one-off PowerShell report because it cannot parse FBX/PNG/JPG cleanly | Microseconds estimate: 0us runtime
- [x] Task 7: VRAM overflow validator | DOD: checker sums import-root total, runtime-candidate, and first-party production full-mip BC7 estimates plus static geometry and RenderTexture estimates; emitted [CRITICAL_VRAM_OVERFLOW] at 1,298.65 MiB total/runtime-candidate, 505.62 MiB first-party production, 48.05 MiB static mesh geometry estimate, and 7.03 MiB static RenderTexture estimate | Alternatives Rejected: budget pass/fail by file count or polluted non-import screenshot totals | Microseconds estimate: 0us measured, PENDING PROFILER
- [x] Task 8: link.xml stripping check | DOD: three link.xml files found and summarized; result marked LINK_XML_PRESENT_STATIC_ONLY | Alternatives Rejected: claiming IL2CPP readiness from link.xml text | Microseconds estimate: 0us runtime
- [x] Task 9: Low-tier texture halving rationale | DOD: summary lists halving candidates and static estimate shows 179 runtime-candidate >1024 textures could save 816.50 MiB full-mip BC7 if halved | Alternatives Rejected: blanket deletion/downscale of source assets | Microseconds estimate: 0us measured, VRAM relief pending import/profile proof

## Iterative Loops

- Loop 1: Complete. Implemented scanner and ran first full audit; tasks 1-3 produced CSV, BC7 totals, and redline flags. Prompt re-extracted after first task block.
- Loop 2: Complete. Mesh audit and initial atlas suggestions inspected; py_compile passed.
- Loop 3: Complete. Self-audit caught atlas output defect: only three groups emitted. Grouping logic changed to first-party parent-directory atlas candidates.
- Loop 4: Complete. Reran checker; five atlas groups emitted, overflow validator triggered, link.xml and low-tier halving sections present.
- Loop 5: Complete. Read generated summary/CSV, checked row counts, mesh redline, texture crime count, and script self-review surface.
- Loop 6: Complete. Added parser unit tests, first-party/runtime directory cost summaries, reran full audit, and validated CI failure behavior.
- Loop 7: Complete. Added importer streaming/readable metadata, generated Docs/Reports/VRAM_Remediation_Plan.md, reran full audit, and confirmed CI gate still fails on current debt.
- Loop 8: Complete. Added machine-readable JSON, split texture/mesh redline CSVs, excluded .codex-build/.codex-artifacts generated trees, fixed Markdown overflow comparison, and reran audit/CI from clean counts.
- Loop 9: Complete. Repaired LOG_VRAM_ASSET_SCOUT.md chronological ordering to restore top-old/bottom-new reporting hygiene.
- Loop 10: Complete. Converted Python tests to read-only fixture/payload tests after workspace-write sandbox denied Python temp writes; cleaned only MemoryBudgetCheck bytecode temp files.
- Loop 11: Complete. Hardened scanner with streaming JPEG header parsing, case-insensitive generated-tree exclusion, JSON schema/gate metadata, and regenerated reports from the current source tree.
- Loop 12: Complete. Added mesh ModelImporter metadata parsing and risk flags for Read/Write, mesh compression off, blend-shape import, import colliders, and keep-quads; regenerated all reports.
- Loop 13: Complete. Added conservative static mesh geometry-buffer estimates, single-asset geometry redline flagging, report/JSON fields, and regenerated all reports.
- Loop 14: Complete. Expanded texture coverage beyond PNG/JPG/JPEG to TGA, BMP, PSD, TIFF, DDS, HDR, EXR, and GIF; regenerated all reports from the broader texture set.
- Loop 15: Complete. Added source-container risk flags, runtime texture extension pressure summary, remediation queue for risky texture containers, and regenerated all reports.
- Loop 16: Complete. Probed Unity-importable blind spots, found one first-party .glb mesh outside the scanner, added GLB/GLTF triangle counting and runtime mesh extension summaries, regenerated all reports.
- Loop 17: Complete. Attempted CURRENT_BATCH POLISH_MANDATE extraction; tag is absent because CURRENT_BATCH now belongs to a different batch. Performed final anti-bloat checks: GLB is the only remaining non-FBX/OBJ Unity mesh-format hit and is now scanned; exotic texture probe returned zero hits; no MemoryBudgetCheck/test bytecode remained.
- Loop 18: Complete. Added static RenderTexture audit coverage, generated `Docs/Reports/VRAM_RenderTexture_Redlines.csv`, and exposed RT+Depth estimate/gate fields in the summary payload. DOD: `.renderTexture` assets are counted separately from source textures/meshes and flagged for depth/MSAA/mips/random-write risks. Alternatives Rejected: waiting for Unity profiler before static triage of assets that can hit the 320 MiB RT+Depth budget. Microseconds estimate: 0us runtime, offline scanner only.
- Loop 19: Complete. Added static runtime RenderTexture source hotspot reporting for code-created RTs; JSON/summary/remediation now expose 61 total RT source hits, 53 non-editor/runtime hits, and pattern counts for profiler follow-up.
- Loop 20: Complete. Added `Docs/Reports/VRAM_RenderTexture_SourceHotspots.csv` as a sortable machine-readable queue for dynamic RT profiler follow-up; optimized report generation to scan RT source hotspots once and reuse the result across Markdown/JSON/CSV writers.
- Loop 21: Complete. Verified `--ci` returns before the dynamic RT source-hotspot scan and documented the evidence boundary: latest CI gate run spent 47.86 seconds on static asset enumeration in this workspace, so the gate is correct but still not cheap automation. DOD: no runtime/game code touched; no Unity assets mutated. Alternatives Rejected: claiming a fast gate from an early return while broad texture/mesh enumeration remains the dominant cost. Microseconds estimate: 0us runtime, offline tooling only.
- Loop 22: Complete. Tightened discovery to Unity/importable asset roots `Assets`, `Packages`, and `Data`, with fallback to the provided root only when those roots are absent. DOD: removed 16 non-import screenshot/doc rows from VRAM totals, kept all runtime-candidate counts intact, exposed scan roots in JSON and summary, and preserved link.xml discovery through the same asset walk. Alternatives Rejected: continuing to count `Docs/AgentLogs` screenshots and `_agent_screen_capture.png` as VRAM assets. Microseconds estimate: 0us runtime, offline evidence quality only.
- Loop 23: Complete. Split RT source hotspot scanning into a path-list helper so unit tests can validate runtime RT allocation patterns without walking every project script. DOD: test suite remained 15 tests and dropped from 83.68 seconds to 8.65 seconds in this run; full report scan still records 61 RT source hotspots and 53 runtime-priority hotspots. Alternatives Rejected: keeping a project-wide source scan inside unit tests or weakening RT hotspot pattern coverage. Microseconds estimate: 0us runtime, tooling verification only.
- Loop 24: Complete. Added a read-only generated-report consistency test that cross-checks `VRAM_Budget_Audit.json` against `VRAM_Budget_Audit.csv` for texture, mesh, and RenderTexture counts, enforces scan roots `Assets/Packages/Data`, and rejects `Docs/` or `_agent_screen_capture` texture rows. DOD: report drift is now caught by the unit suite without regenerating assets or touching Unity import settings. Alternatives Rejected: trusting Markdown inspection or hardcoding current asset totals in tests. Microseconds estimate: 0us runtime, tooling verification only.
- Loop 25: Complete. Added `--validate-reports` to validate existing JSON/CSV report consistency without scanning the asset tree. DOD: no-scan validator checks report presence, CSV headers, JSON parse, texture/mesh/RenderTexture count parity, import-root scope, unknown asset types, screenshot/doc pollution, and overflow gate consistency; CLI exits 0 on current reports. Alternatives Rejected: forcing every report-consistency check through the full 90-second scanner or through unit tests only. Microseconds estimate: 0us runtime, tooling/CI handoff only.
- Loop 26: Complete. Extended `--validate-reports` to include split artifact parity for texture redlines, mesh redlines, RenderTexture redlines, and RenderTexture source hotspots. DOD: validator now rejects stale split CSV queues when they disagree with JSON counters, while still avoiding a full asset scan. Alternatives Rejected: validating only the broad CSV and leaving dedicated remediation queues to drift. Microseconds estimate: 0us runtime, tooling/CI handoff only.
- Loop 27: Complete. Removed a brittle hardcoded `rt_hotspots=61` assertion from the validator test and derived the expected value from `VRAM_Budget_Audit.json` instead. DOD: the test still proves split report parity while allowing legitimate future RT hotspot count changes after report regeneration. Alternatives Rejected: freezing today's hotspot count in source code. Microseconds estimate: 0us runtime, tooling correctness only.

## Verification

- python -m py_compile Tools/MemoryBudgetCheck.py: PASS
- python -m py_compile Tools/MemoryBudgetCheck.py Tools/test_memory_budget_check.py: PASS
- PYTHONDONTWRITEBYTECODE=1 python AST syntax parse for Tools/MemoryBudgetCheck.py and Tools/test_memory_budget_check.py: PASS
- PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s Tools -p test_memory_budget_check.py: PASS, 17 tests, elapsed 9.666 seconds after removing the hardcoded RT hotspot count assertion
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --validate-reports: PASS; reports valid: textures=1652 meshes=302 render_textures=1 texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61 scan_roots=Assets,Packages,Data
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root .: PASS as scanner execution, with expected [CRITICAL_VRAM_OVERFLOW] finding; current counts 1,652 import-root textures / 302 meshes / 1 RenderTexture; texture crime rows 801; source-container risk rows 23; mesh redline/risk rows 293; RenderTexture redline/risk rows 1; static mesh geometry estimate 48.05 MiB; static RenderTexture estimate 7.03 MiB; latest full report generation elapsed 89.33 seconds.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --ci: EXPECTED FAIL, ci_exit_code=2 because static redlines/overflow are present; current counts 1,652 import-root textures / 302 meshes / 1 RenderTexture; texture crime rows 801; source-container risk rows 23; mesh redline/risk rows 293; RenderTexture redline/risk rows 1; static mesh geometry estimate 48.05 MiB; static RenderTexture estimate 7.03 MiB; dynamic RT source-hotspot scan is bypassed in CI, but broad static asset parsing still measured 57.17 seconds on the earlier timed pass and was rechecked after adding `--validate-reports`.
- Docs/Reports/VRAM_Remediation_Plan.md: generated with non-production quarantine, first-party clamp, streaming mipmap, atlas, and mesh LOD action queues
- Docs/Reports/VRAM_Budget_Audit.json: generated with schema_version=1, generated_utc, scan_root_names, resolved_scan_roots, skipped_directory_names, gate_reasons, texture source-container risk counts, runtime texture extension summary, runtime mesh extension summary, mesh importer risk counts, first-party mesh importer risk counts, and static geometry estimate fields; no .codex-build duplicate payload present
- Docs/Reports/VRAM_Budget_Audit.json schema validation: PASS. texture_count=1652, mesh_count=302, render_texture_count=1, resolved_scan_roots=Assets/Packages/Data, rt_hotspots=61, runtime_rt_hotspots=53, gate_reasons include CRITICAL_VRAM_OVERFLOW.
- Docs/Reports/VRAM_Texture_Redlines.csv and Docs/Reports/VRAM_Mesh_Redlines.csv: generated; mesh redline CSV now includes geometry estimate and ModelImporter metadata columns
- Docs/Reports/VRAM_RenderTexture_Redlines.csv: generated; one row for Assets/_Project/Art/TEXTURES/RT_HUD_Display.renderTexture with 7.03 MiB static estimate and depth-stencil risk flag
- CSV structural validation: PASS. VRAM_Budget_Audit.csv rows=1956 columns=43 bad_rows=0; texture redlines rows=947 columns=7 bad_rows=0; mesh redlines rows=294 columns=14 bad_rows=0; RenderTexture redlines rows=2 columns=11 bad_rows=0.
- RenderTexture source-hotspot validation: scanner report now records 61 RT source hotspots, 53 runtime/non-editor RT source hotspots, pattern split = 19 new RenderTexture / 18 RTHandles.Alloc / 16 RenderTextureDescriptor / 8 RenderTexture.GetTemporary.
- Docs/Reports/VRAM_RenderTexture_SourceHotspots.csv: generated; rows=62 including header, columns=8, bad_rows=0, runtime profiler priority rows=53.
- Docs/AgentLogs/LOG_VRAM_ASSET_SCOUT.md: chronological block order repaired after self-audit found MACHINE GATE above REMEDIATION
- Docs/AgentLogs/LOG_VRAM_ASSET_SCOUT.md: chronological block order rechecked after no-scan validator block relocation; LOG_ORDER_OK headers=21, latest order is top-old/bottom-new through 2026-05-15T20:49:00+03:00.
- Test hygiene: read-only test suite now avoids Python temp-file writes under workspace-write sandbox; MemoryBudgetCheck pyc temp files removed
- Prompt re-extraction note: Docs/Tasks/CURRENT_BATCH.md no longer contains AGENT_PROMPT id VRAM_ASSET_SCOUT as of this loop; current file belongs to a new batch. Continued from persisted Status/Rationale by user instruction.
- POLISH_MANDATE extraction note: Docs/Tasks/CURRENT_BATCH.md currently contains no POLISH_MANDATE tag; final anti-bloat pass was executed from persisted VRAM_ASSET_SCOUT state instead of inventing missing batch instructions.
- C# dotnet build: NOT RUN. No .csproj files are present in current root scan; this task changed Python tooling and docs only.
