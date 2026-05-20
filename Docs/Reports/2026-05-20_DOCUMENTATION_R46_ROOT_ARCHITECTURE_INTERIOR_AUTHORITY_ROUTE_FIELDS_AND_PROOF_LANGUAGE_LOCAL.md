# 2026-05-20 Documentation R46 Root/Architecture Interior Authority, Route Fields, and Proof Language

Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL.

Scope: active root and `Docs/ARCHITECTURE` documentation. Historical archives remain historical snapshots.

## What Was Wrong

- Some active root glossary/FAQ text still opened with an R43 "current boundary" paragraph before the later R45 paragraph.
- Root release/workflow docs still allowed singleton and `DontDestroyOnLoad` wording to read as acceptable architecture for new work.
- Global-authority boundary/migration docs foregrounded older R43/R42 counter tuples instead of the current R46 source-scale baseline.
- Route-card tables had owner/phase/capacity/proof fields but no explicit `Instrument` row for the route mechanism.
- Some route-card black-box dump paths were written like existing proof artifacts instead of planned/generated-on-fault targets.
- Static source scans, RenderGraph wording, AudioSource wording, and microsecond text still let docs read stronger than the available evidence.

## What Changed

- Promoted R46 as the current local static root/architecture documentation boundary for interior authority wording, route-field completion, and proof-language cleanup.
- Reclassified singleton/DDOL entries in `MASTER_RELEASE_WORK_PLAN.md` as historical capture/legacy notes, not new architecture approval.
- Updated global-authority counter orientation to the R46 static-source baseline: `GlobalRegistryHits=6179`, `PubSubHits=890`, `NativeHits=23375`, `NativeQueueRefs=115`, `ConfigureEnsure=271`, `CreateQueueSlots=73`, `EnsureLanes=135`, and `ScriptTypedLanes=1353`.
- Added `Instrument` fields to active route-card tables and clarified telemetry/black-box/fault-dump fields for SHINOBU_138 and SHINOBU_200.
- Demoted dump paths to planned/generated-on-fault targets unless a timestamped runtime trigger and output artifact are linked.
- Tightened static-evidence wording in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `GLOBAL_SIGNAL_CORRIDOR.md`, `SYSTEM_INTERCONNECT_MATRIX.md`, `URP_SCREENSHOT_PIPELINE.md`, `ADAPTIVE_STEM_AUDIO_MIXER.md`, `MACRO_ECOSYSTEM_MATHEMATICIAN.md`, `SAVE_V8_BINARY_SPEC.md`, `SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`, and `SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`.
- Recaptured R46 source counters: `ProjectCs=2074`, `ScriptCs=2013`, `NonTestCs=2048`, `ProjectLines=1418005`, `ScriptLines=1397407`, `NonTestLines=1411380`, `Asmdefs=142`, `NonTestAsmdefs=140`, `InterfaceHitsProject=347`, `InterfaceHitsScripts=342`, `InterfaceDecls=278`, `RegistryInterfaces=62`, `GlobalRegistryHits=6179`, `PubSubHits=890`, `NativeHits=23375`, `NativeQueueRefs=115`, `CreateQueueSlots=73`, `EnsureLanes=135`, `ConfigureEnsure=271`, and `ScriptTypedLanes=1353`.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: PASS.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: `JsonFiles=173`, `Bad=0`.
- Active root/architecture R46 boundary scan: `R46BoundaryScope=128`, `Missing=0`.
- Active architecture route-card field scan including `Instrument`: `RouteCardFiles=15`, `Missing=0`.
- Strict proof/stale-current scan: `StrictProofOrStaleHits=0`.
- `git diff --check -- Docs Tools AGENTS.md BUILD_PLAYTEST_ISSUES.md MASTER_RELEASE_WORK_PLAN.md ':!Docs/Tasks/CURRENT_BATCH.md'`: PASS, exit `0`, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6766 missing=61`. Missing refs remain one Dynamic Decals vendor asset reference, RealtimeCSG vendor icon/readme image references, `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`, and `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs`.

## Runtime Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, analytics endpoint, network send, or visual-route proof was run in this pass.
