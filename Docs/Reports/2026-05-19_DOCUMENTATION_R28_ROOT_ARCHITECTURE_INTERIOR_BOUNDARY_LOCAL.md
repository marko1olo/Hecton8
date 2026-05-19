# 2026-05-19 Documentation R28 Root / Architecture Interior Boundary Local

Date: 2026-05-19
Agent: DOC_GLOBAL_DOCS_REFRESH
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / TOOL_OUTPUT
Runtime proof: ABSENT

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-19 R28 Interior Actuality Boundary

This report is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this report.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scope

R28 is a root/architecture interior-boundary pass. It does not recapture source counters; R27 remains the latest DOC_GLOBAL root/architecture source-counter/index snapshot.

R28 targeted active root and architecture entrypoints plus architecture files that still had only generic R4 boundary language and no explicit current DOC_GLOBAL root/architecture blocker note.

## Corrections

- Added an explicit `2026-05-19 DOC_GLOBAL R28 Interior Note` to 25 active `Docs/ARCHITECTURE/*.md` files.
- Promoted R28 in root and architecture read-order surfaces: `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/PROJECT_ATLAS.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, and `Docs/Reports/README.md`.
- Reclassified R27 as the latest source-counter/index boundary rather than the latest overall DOC_GLOBAL root/architecture boundary.
- Preserved R27 static source counters instead of manufacturing a new counter pass.

## Files With New R28 Interior Notes

- `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`
- `Docs/ARCHITECTURE/AI_PACING_MODEL.md`
- `Docs/ARCHITECTURE/AI_POTENTIAL_FIELD_NAVIGATION.md`
- `Docs/ARCHITECTURE/ARENA_ALLOCATOR_2_0.md`
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
- `Docs/ARCHITECTURE/CONTENT_SAVE_SLOT_TOPOLOGY.md`
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`
- `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/ECS_DOTS_ADOPTION_PLAN.md`
- `Docs/ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md`
- `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`
- `Docs/ARCHITECTURE/INPUT_DETERMINISM_SHINOBU_36.md`
- `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`
- `Docs/ARCHITECTURE/SHINOBU_48_SEED_SHIP_ANOMALY.md`
- `Docs/ARCHITECTURE/SHINOBU_61_APEX_COGNITION.md`
- `Docs/ARCHITECTURE/SHINOBU_69_VOLUMETRIC_PLASMA_BEAM.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_PLAYER_LOOP_TO_HECTON8_GAP_MATRIX.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_TO_HECTON8_TACTICAL_BACKLOG.md`
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Docs/ARCHITECTURE/UNCLAIMED_FUTURE_SYSTEM_SEAMS.md`
- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`

## Current Blockers

- `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor icon/readme image references unless a later run proves otherwise.
- `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`). This is static validator proof only, not mod runtime smoke proof.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, mod runtime smoke, platform run, campaign telemetry, and visual proof are absent.

## Validation

- R28 interior note scan: `25` active `Docs/ARCHITECTURE/*.md` files contain `DOC_GLOBAL R28 Interior Note`.
- Scoped active root/architecture/direct-report/Archivarius R4 marker scan: `ScopeFiles=325`, `MissingCount=0`, `DuplicateCount=0`.
- Scoped local markdown link scan: `MissingCount=0`, `Files=0`, `ScopeFiles=157`.
- Stale current-red Mod API blocker scan over active root/architecture/report surfaces: no hits.
- `python Tools/test_architecture_atlas.py`: pass, `10` tests.
- `python Tools/AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`; missing refs remain RealtimeCSG vendor icon/readme image references.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1`: pass, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

This report must not be used as Unity runtime, Play Mode, profiler, GCMonitor, player-build, platform-build, or visual proof.
