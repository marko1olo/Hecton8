# 2026-05-20 Documentation R47 Root/Architecture Authority Spine, Runtime Wording, and Counter Drift

Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC.

Scope: active root documentation and `Docs/ARCHITECTURE` entrypoints. Historical archives remain historical snapshots. No GitHub operation, Unity Editor operation, Play Mode run, or `dotnet build` was performed.

## What Was Wrong

- Some active authority entrypoints still named R45 or R46 as current after the R47 root/architecture correction layer had already been introduced.
- `Docs/PROJECT_ATLAS.md` still carried the R46 current-boundary paragraph, the R46 `142/140` asmdef count, and the old pre-R47 AtlasCheck tuple.
- `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md` had an R47 boundary header but an interior R46 boundary note, which made the file internally inconsistent.
- `Docs/Reports/README.md` repeated the old runtime-wired evidence-class label as an active token while describing the R47 demotion.
- Root/global-architecture wording still exposed a stale global-event-bus shorthand where current source orientation is typed `SignalBus<T>` lanes plus documented `GlobalSignals` NativeQueue bridge lanes.
- Several active route-card and binary-payload notes could still be read as existing runtime dump or wired-runtime proof instead of static source/path orientation.

## What Changed

- Promoted R47 as the current local static root/architecture authority-spine/runtime-wording/counter-drift boundary across active root indexes, architecture indexes, reports index, and generated-atlas source text.
- Updated `Docs/PROJECT_ATLAS.md` to R47 current boundary, `143/141` first-party asmdef orientation, typed SignalBus/NativeQueue-bridge terminology, and the current red AtlasCheck tuple.
- Updated `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md` so its interior boundary note matches R47 and treats R46 as prior.
- Reworded `Docs/Reports/README.md` so it records the runtime-wired evidence-class demotion without keeping the stale evidence label as an active searchable token.
- Updated global architecture/domain wording to distinguish first-party typed `SignalBus<T>` lanes from the `GlobalSignals` NativeQueue bridge and from cold/mod-facing `HectonEventBus` usage.
- Demoted binary payload rows from runtime-wired language to `STATIC_SOURCE_RUNTIME_PATH_PRESENT` where the evidence is source path resolution only.
- Clarified route dump paths as planned/generated-on-fault targets unless linked to a timestamped runtime trigger and output artifact.
- Recaptured R47 source counters after final validation: `ProjectCs=2088`, `ScriptCs=2027`, `NonTestCs=2062`, `ProjectLines=1424399`, `ScriptLines=1403799`, `NonTestLines=1417772`, `Asmdefs=143`, `NonTestAsmdefs=141`, `InterfaceHitsProject=343`, `InterfaceHitsScripts=340`, `InterfaceDecls=278`, `RegistryInterfaces=62`, `GlobalRegistryHits=6213`, `PublishSubscribeDirectHits=586`, `SignalCorridorBroadHits=2502`, `NativeHits=18617`, `NativeQueueRefs=115`, `CreateQueueSlots=73`, `EnsureLanes=135`, `ConfigureEnsure=271`, and `ScriptTypedLanes=1447`.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: PASS.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: `JsonFiles=174`, `Bad=0` with UTF-8 BOM and UTF-16 fallback.
- Active root/architecture R47 boundary scan: `R47BoundaryScope=134`, `Missing=0`.
- Active architecture route-card field scan including `Instrument`: `RouteCardFiles=14`, `Missing=0`.
- Strict proof/stale-current scan: no scoped hits for stale R46/R42 current-boundary text, stale AtlasCheck tuples, stale runtime-wired evidence label, or stale global-event-bus shorthand.
- `git diff --check -- Docs Tools AGENTS.md BUILD_PLAYTEST_ISSUES.md MASTER_RELEASE_WORK_PLAN.md ':!Docs/Tasks/CURRENT_BATCH.md'`: PASS, exit `0`, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6781 missing=61`. Missing refs remain one Dynamic Decals vendor asset reference, RealtimeCSG vendor icon/readme image references, `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`, and `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs`.

## Runtime Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, analytics endpoint, network send, shader import, or visual-route proof was run in this pass.
