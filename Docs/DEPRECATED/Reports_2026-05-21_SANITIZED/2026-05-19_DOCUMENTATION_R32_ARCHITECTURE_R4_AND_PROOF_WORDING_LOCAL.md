# DOC_GLOBAL R32 Architecture R4 And Proof-Wording Local Pass

Date: 2026-05-19
Agent: DOC_GLOBAL_DOCS_REFRESH
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / READ_ONLY_SUBAGENT_AUDIT
Runtime proof: ABSENT

## Scope

R32 is a local root/architecture documentation-currency pass. It updates active stable root and architecture entrypoints and selected architecture interiors. It does not edit GitHub state and does not claim Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, mod runtime, platform, campaign, or visual proof.

## Corrections

- Promoted the active DOC_GLOBAL root/architecture chain to R32 where current documentation interiors were changed.
- Added R32 R4/current-boundary wording to root and architecture entrypoints and architecture interiors that had only older R31/R30/R29/R28 framing.
- Demoted `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` from "current actuality manifest" to historical snapshot wording in active architecture docs.
- Corrected Mod API static validator counters from stale `SchemaRevision=15` / `SourceSignals=161` to the current observed static-tool result `SchemaRevision=16` / `SourceSignals=162` / `ModCommandSizeBytes=64`.
- Demoted `SHINOBU_125_SCAVENGING_LOOT_ORACLE_ROUTE_CARD.md` from `STATIC GREEN` to `STATIC ROUTE CARD / GREEN REVIEW ARTIFACT REQUIRED / UNITY COMPILE PENDING`.
- Added R4 actuality boundary to `PROCEDURAL_WRECKAGE_GLOBAL_AUTHORITY_ROUTE_CARD_SHINOBU_121.md`.
- Added local source anchors for architecture docs whose runtime contract text lacked direct file anchors: thermodynamics, flora sway, habitat fluid incursion, macro ecosystem, procedural wreckage, loot oracle, PDA streamer, voxel surface nets, and flora/fauna symbiosis.
- Reworded Subnautica 2 production-contract source/disk sections so static snapshots are not described as current proof.
- Reworded temporary Roslyn/net10 text in future-seam docs so it remains static-tool orientation only unless an artifact tuple is attached.

## Current Static Gates

- `Tools/AtlasCheck.py` remains red: current R32 result `ATLAS_CHECK_FAIL references=6549 missing=59`. Missing refs are RealtimeCSG vendor icon/readme images plus `Assets/_Project/Scripts/Core/Memory/Editor/VaultXRayWindow.cs` and `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.
- `Docs/Modding/Validate_Mod_API_Static.ps1` is static validator evidence only. It is not mod runtime proof.
- R27 remains the latest deliberate root/architecture source-counter/index snapshot until a newer counter pass reruns it.

## Validation

- Targeted stale-current/proof scan over active root/architecture/report surfaces: no hits for stale R31/R32 absence wording, `SchemaRevision=14`, `SourceSignals=160`, `Current actuality manifest`, `STATIC GREEN`, `Current disk proof`, or `Current source truth`.
- R4 marker scan: `ScopeFiles=81`, missing `0`, duplicate `0`.
- Local markdown link scan: `ScopeFiles=81`, missing links `0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- Active non-archive docs JSON parse: `JsonFiles=131`, ok `131`, bad `0`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=59`.
- Scoped `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## Proof Limits

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof was run for R32.

Runtime microseconds saved: `0us`. This pass is documentation/tooling only.
