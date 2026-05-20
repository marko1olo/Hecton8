# Documentation R41 Root/Architecture Global-Authority Internal Residue

Date: 2026-05-20
Scope: root anchors, active `Docs` root entrypoints, active `Docs/ARCHITECTURE` contracts, generated architecture atlas tooling.
Evidence class: `STATIC_DOC` / `STATIC_SOURCE` / `FILESYSTEM` / `PY_TOOL`.

## Result

R41 is a local static documentation-currency pass. It does not claim Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, network route, or visual proof.

Primary corrections:

- Promoted the current DOC_GLOBAL root/architecture boundary from R40 to this R41 report after finding active Global Authority documents with stale interior R37/R38/R40 wording.
- Corrected generated atlas wording from the old R38 blocker label to the current R41 static blocker label.
- Corrected `Tools/BuildArchitectureAtlas.py` source line counting so newline-terminated files are not overcounted by one line.
- Added formal `YELLOW / STATIC_SOURCE_ONLY` route-card disposition fields to hydrodynamic KCC, chemical influence grid, dynamic point-light culling, and scavenging loot oracle route cards.
- Rechecked `SAVE_V8_BINARY_SPEC.md`: `Assets/_Project/Scripts/SaveCompressionDictionary.cs` is absent on current disk, so dictionary-LZ4 remains future-only until an owner path exists.

## R41 Source-Scale Orientation

Current source-count probe at this pass:

- First-party C# files under `Assets/_Project`: `1962`.
- First-party C# files under `Assets/_Project/Scripts`: `1903`.
- First-party non-test C# files excluding `Assets/_Project/Tests*`: `1938`.
- First-party physical lines under `Assets/_Project`: `1344787`.
- Script physical lines under `Assets/_Project/Scripts`: `1324687`.
- Non-test physical lines: `1338671`.
- Broad `interface` token hits: `324` project-wide, `321` under scripts.
- Direct interface declaration lines under scripts: `270`.
- Direct public interfaces in `GlobalRegistryContracts.cs`: `62`.
- First-party asmdefs: `133`; excluding tests: `131`.
- `GlobalRegistry.` line hits under scripts: `6060`.
- Publish/subscribe line hits under scripts: `340`.
- Native-collection line hits under scripts: `15291`.
- `GlobalSignals.cs` `NativeQueue<...>` refs: `115`.
- Direct `GlobalSignals.CreateQueue(...)` slots: `73`.
- Typed `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs`: `135`.
- `SignalBus<T>.Configure/EnsureInitialized` hits inside `GlobalSignals.cs`: `271`.
- Script-level typed `SignalBus<T>.EnsureInitialized()` matches: `267`.

These are volatile static counters in a dirty multi-agent workspace. Rerun before exact downstream use.

## Validation Snapshot

- `python Tools\BuildArchitectureAtlas.py`: PASS, regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- AST parse for `Tools/BuildArchitectureAtlas.py`, `Tools/AtlasCheck.py`, and `Tools/test_architecture_atlas.py`: PASS, `Files=3`.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: BLOCKED by pycache filesystem permission denial under `Tools\__pycache__`; no bytecode proof is claimed.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated docs JSON parse: PASS, `JsonFiles=130`, `Bad=0`.
- Active root/architecture R4 marker scan: PASS, `ScopeFiles=98`, `Missing=0`, `Duplicate=0`.
- Targeted stale-boundary scan for R37/R40-current/R41-absent residue: PASS, no active hits in root/architecture scope.
- Scoped `git diff --check`: PASS with line-ending warnings only.
- Runtime proof: absent.

## Known Blocker

`Tools\AtlasCheck.py` remains a required separate gate. Current R41 result is `ATLAS_CHECK_FAIL references=6642 missing=58`; missing refs are `Assets/Dynamic Decals/Resources/Decal.obj` plus RealtimeCSG vendor icon/readme image paths. R41 does not make the atlas verified.
