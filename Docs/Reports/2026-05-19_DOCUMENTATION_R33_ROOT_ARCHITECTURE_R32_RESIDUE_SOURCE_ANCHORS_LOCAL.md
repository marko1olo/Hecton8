# DOC_GLOBAL R33 Root / Architecture R32 Residue + Source Anchors Local Pass

Date: 2026-05-19
Status: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC

## Scope

This pass updates active root and architecture documentation internals. It does not update Unity scenes, generated binaries, runtime systems, or marketing docs. Historical archive/report bodies remain historical unless they are active entrypoints.

## Corrections

- Promoted R33 above R32 in active root/architecture entrypoints after R33 edits existed on disk.
- Added source-anchor sections to runtime-facing architecture contracts that previously had no local path anchors.
- Corrected shorthand or stale scene-bootstrap wording: no first-party `SceneBootstrap.cs` exists in the current source scan; scene activation/readiness is anchored to `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`, direct-entry protection to `Assets/_Project/Scripts/Bootstrap/SceneGuard.cs`, and authored LOD scene registration to `Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs`.
- Corrected `SYSTEM_INTERCONNECT_MATRIX.md` from stale `SceneBootstrap` event wording to current `GameBootstrapper` event payload/listener/flush ownership.
- Regenerated the atlas after R33 cleanup; current AtlasCheck blocker is `57` RealtimeCSG vendor icon/readme image refs only. Earlier R32 missing-path residue for `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` no longer appears in the current AtlasCheck missing set.
- Corrected binary-payload path language: root `Data/Audio/Acoustic_LUT.bin`, `Data/Visuals/Water_Extinction_Matrix.bin`, `Data/Visuals/Biolum_Profiles.bin`, `Data/Balance/Baked/Babel_Dictionary.h8bin`, and `Data/Balance/Baked/H8StaticData.bin` are present and 16-byte aligned in the current filesystem scan; mirrors for the three runtime payloads under `Assets/_Project/Data` are not present; `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin` is a separate visible Babel payload.
- Demoted CSV/path claims for absent `drone_chassis_specs.csv`, `drone_specs.csv`, and root `input_profiles.csv` to pending-artifact wording.
- Demoted SteamDB Steam Deck wording to third-party public metadata, not HECTON-8 platform proof.
- Kept SHINOBU_02 source-scale spot check as volatile read-only orientation only; R27 remains the latest deliberate DOC_GLOBAL source-counter/index and physical-line snapshot until a deliberate counter pass reruns.

## Static Payload Spot Check

The following file existence/size checks were local filesystem checks only:

| Path | Exists | Bytes | 16-byte remainder |
|---|---:|---:|---:|
| `Data/Audio/Acoustic_LUT.bin` | true | 524288 | 0 |
| `Data/Visuals/Water_Extinction_Matrix.bin` | true | 393216 | 0 |
| `Data/Visuals/Biolum_Profiles.bin` | true | 25936 | 0 |
| `Data/Balance/Baked/Babel_Dictionary.h8bin` | true | 1296 | 0 |
| `Data/Balance/Baked/H8StaticData.bin` | true | 896 | 0 |
| `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin` | true | 1534512 | 0 |

These are filesystem facts only. They are not runtime load, shader import, profiler, Frame Debugger, Memory Profiler, save/load, or player-build proof.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6671 missing=57`; missing set is RealtimeCSG vendor icon/readme image refs only.
- `powershell -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=131`, `Bad=0`.
- Active architecture R4 marker scan: `ScopeFiles=83`, `Missing=0`, `Duplicate=0`.
- Source-anchor filesystem scan: `SourceAnchorPathsChecked=221`, `Missing=0`.
- Duplicate architecture heading-body scan: `DUP_HEADING_GT2=0`.
- Scoped local markdown link scan: `MarkdownLinksChecked=53`, `Missing=0`.
- Scoped `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R33.
- `Tools/AtlasCheck.py` remains red on `57` missing references: RealtimeCSG vendor icon/readme images only in the current regenerated atlas.
- Mod API static validation is a static-tool check only, not mod runtime proof.
- R27 remains the latest deliberate DOC_GLOBAL source-counter/index and physical-line snapshot until a deliberate full counter pass reruns.
