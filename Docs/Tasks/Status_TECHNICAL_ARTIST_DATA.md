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
- [x] Task 2 MICRO-DETAIL LIBRARY | DOD: first-party texture scan found 14 detail candidates; doctrine lists 10 actual detail maps and separately lists missing hard-surface overlays. Alternatives Rejected: claiming nonexistent scratch/dust/carbon maps exist. Estimate: 0 us CPU until shader uses shared detail; potential unique texture memory saved by sharing overlays.
- [x] Task 3 CLEARCOAT FAKE | DOD: single-pass fake clearcoat parameters and HLSL math documented for wet glass/chrome. Alternatives Rejected: second-pass rendering and material clone stacks. Estimate: avoids one additional transparent/spec pass, approximate saving 100-400 us on MX350 per affected view versus extra pass.
- [x] Task 4 ANISOTROPIC FAKE | DOD: cockpit brushed-metal lobe modulation documented using tangent/bitangent banding and shared brush detail. Alternatives Rejected: full anisotropic GGX migration or ray-traced reflection truth. Estimate: sub-20 us shader ALU impact per visible cluster versus multi-pass/ray-based reflection waste.
- [x] Task 5 MATERIAL VALIDATOR | DOD: `Tools/MaterialAudit.py` created, py_compile passed, and first-party audit executed to JSON. Alternatives Rejected: Unity editor mutator and full third-party decode as default. Estimate: 0 us runtime impact; offline gate only.

### Loop 2 - Feedback

- [x] Task 6 SELF-AUDIT LOOP 1 | DOD: standard 5-texture material model compared against optimized albedo+normal+512 ORM+shared detail model. Alternatives Rejected: balanced middle-ground without target VRAM delta. Estimate: 6.65 MB -> 2.99 MB per set under documented assumption, about 55% unique texture VRAM reduction.
- [x] Python energy-conservation test executed | DOD: `python Tools\MaterialAudit.py --root Assets\_Project --sample-size 256 --json Docs\AgentLogs\MaterialAudit_TECHNICAL_ARTIST_DATA.json`. Alternatives Rejected: visual-only judgement. Estimate: 0 us runtime impact; 0 albedo failures, 0 warnings.
- [x] Surface/material audit reviewed | DOD: JSON reviewed: 138 textures, 28 albedo candidates, 30 ORM candidates, 173 materials, 31 material issues. Alternatives Rejected: chat-only unverifiable summary. Estimate: no runtime change; material debt quantified.

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

- [x] Added import-setting validation | DOD: validator now checks sRGB, normal-map type, mips, compression/readability where visible in `.meta`. Alternatives Rejected: filename-only audit. Estimate: 0 us runtime impact; 6 suspect texture imports found.
- [x] Added Markdown audit report | DOD: `Docs/AgentLogs/MaterialAudit_TECHNICAL_ARTIST_DATA.md` generated for human review. Alternatives Rejected: JSON-only evidence. Estimate: 0 us runtime impact.
- [x] Fixed false positive ORM classification | DOD: tokenized classifier no longer treats `storms` as ORM and excludes UI/skybox paths from surface data checks. Alternatives Rejected: accepting noisy audit evidence. Estimate: 0 us runtime impact.
- [x] Added CI fail flags | DOD: `--fail-on-import-issues` and `--fail-on-material-issues` added to `Tools/MaterialAudit.py`. Alternatives Rejected: manual-only validation. Estimate: 0 us runtime impact.
- [x] Verified CI fail flags | DOD: scoped import-debt run returned exit 2; scoped material-debt run returned exit 3. Alternatives Rejected: untested CLI switches. Estimate: 0 us runtime impact.
