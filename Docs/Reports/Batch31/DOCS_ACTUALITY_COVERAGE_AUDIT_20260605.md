# Documentation Actuality and Coverage Audit - 2026-06-05

Status: STATIC_DOC / STATIC_SOURCE AUDIT - RUNTIME PROOF ABSENT

Evidence class: STATIC_DOC, STATIC_SOURCE, STATIC_FILESYSTEM, CLI_TEST_STATIC.

Mandates followed: `ARCH_Pentarchy_Audit`, `QA_Evidence_Text_Filter_Audit`, `ARCH_Execution_Phases`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Signal_Lane_Segregation`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

## Current Front

Orchestrator documentation front. No Unity mutation, build, scene save, prefab edit, import, or Play Mode action was run in this pass.

Process gate at audit time: CPU sample returned `100`; Unity, Unity ILPP, Unity licensing, Unity auto-quitter, crash handler, and package manager processes were present. Full build, Unity import mutation, and scene/prefab writes remain blocked by process discipline.

Static inventory sample:

- `Assets/_Project/Scripts`: `2545` `.cs` files.
- `Assets/_Project`: `171` `.asmdef` files.
- `Docs`: `19474` `.md` files including reports and archive-heavy trees.
- `Docs/ARCHITECTURE`: `186` `.md` files.
- `Docs/Reports/Batch31`: `23` `.md` files before this audit file.

## What Was Wrong

1. Scene spine authority is contradictory.
   - Root `AGENTS.md` says normative flow is `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD` and that `01_ORBIT` is not the main handoff.
   - `ProjectSettings/EditorBuildSettings.asset` includes enabled `01_ORBIT`.
   - `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` and first-20 docs still include orbit in active spine.
   - `Assets/_Project/Scripts/MainMenuController.cs` currently has `newGameTargetSceneName = "02_HECTON_WORLD"`.
   - Result: New Game, first-20, save/load, and visual proof can target different routes.

2. Documentation authority order drift existed in `Docs/README.md`.
   - It acted like an alternate hierarchy and placed `TASTE.md` before task mandates.
   - `PROJECT_BIBLES.md` and `VISION_LOCKS.md` were not in the starting read map.

3. Quality gates drift existed between root guardrails and `Docs/QUALITY_GATES.md`.
   - Root guardrail SetPass target is `600`; `Docs/QUALITY_GATES.md` only had `<= 800`.
   - Root batch and memory targets were not explicit rows in the performance table.
   - `quality.md` and `Docs/QUALITY_GATES.md` both covered proof without a clean boundary.

4. First-20 copper/drill reports are partially stale against current source.
   - `Assets/_Project/Scripts/SeafloorDrillTool.cs` exists and returns `ToolCapabilityMasks.Drill`.
   - The first-hour route is still incomplete because current path checks returned missing:
     - `Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset`
     - `Assets/_Project/Data/Tools/ToolMetadata_SeafloorDrill.asset`
     - `Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab`
     - `Assets/_Project/Data/Items/Resources/Resource_CopperOre.asset`
     - `Assets/_Project/Data/Items/Resources/Resource_CopperWire.asset`
   - `ContentSanityValidator.cs` still fail-fast checks Drill-gated copper without item and held prefab.

5. Proof harness status remains rejected/static-only.
   - `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` still contains quarantine and scene dirty/save behavior according to Batch31 report anchors.
   - Raw MCP screenshots under `Docs/Screenshots/MCP` are diagnostic only unless converted into a manifest-backed `Docs/Screenshots/HectonProofPackets/h8_1475_*` packet.

6. Root bible evidence headers were uneven.
   - `quality.md` and `PROJECT_BIBLES.md` had status lines but no explicit `Evidence class:` field.

7. Lore packet replay variables had one family-hook drift.
   - `VISION_LOCKS.md` forbids family-revenge and missing-relative openings.
   - `Docs/Lore/Gameable_World_Packets.md` and `Docs/Lore/ContentPacks/CP03_Drowned_Colony_Barnard_Hook.md` still allowed Barnard marks to point toward family or direct personal links.
   - Static fix required professional-only axes: mentor, rival crew, old debt, employer, yard school, revoked work contact, and procedure recognition.

8. Script-to-stable-doc exact anchors are incomplete.
   - Script coverage scout checked `2545` scripts against `276` stable docs.
   - Top `20` live-class gaps are recorded in `Docs/Reports/Batch31/SCRIPT_DOCUMENTATION_COVERAGE_GAP_LEDGER_20260605.md`.
   - Historical reports and archives mention many of these classes, but stable docs do not consistently tie live class to owner, route, phase, lane, failure mode, and missing proof.

9. Standing root route bibles had uneven evidence headers.
   - A route-bible scan from `PROJECT_BIBLES.md` found many standing root bibles without `Evidence class:`.
   - Mechanical fix added `Evidence class: STATIC_DOC` after existing `Status:` lines where missing.
   - Several route bibles already had unrelated dirty edits before this header pass; those edits were preserved.

10. Stable proof-language sweep found one live static-label drift in root route bibles.
   - `data.md` still instructed static-only work to use `STATIC VERIFIED`.
   - Fixed to `STATIC_SOURCE_REVIEWED` / `STATIC_DOC_REVIEWED`.
   - Other grep hits were mostly negative rules, proof requirements, technical job completion language, or `quality.md` label definitions; not patched in this pass.

## What I Did

- Updated `Docs/README.md` so it is a read map, not a competing authority hierarchy.
- Added `PROJECT_BIBLES.md` and `VISION_LOCKS.md` to the start map.
- Added source-reality wording that `.cs` presence can disprove stale docs but does not prove runtime readiness.
- Updated `Docs/QUALITY_GATES.md` with an explicit `quality.md` vs executable-gate boundary.
- Split SetPass into target `<= 600` and emergency blocker ceiling `<= 800`.
- Added batches `<= 1800` and total memory `<= 4096MB` rows.
- Added `Evidence class: STATIC_DOC` to `quality.md` and `PROJECT_BIBLES.md`.
- Removed family-hook wording from Barnard packet replay variables in `Docs/Lore/Gameable_World_Packets.md` and `Docs/Lore/ContentPacks/CP03_Drowned_Colony_Barnard_Hook.md`.
- Added `Docs/Reports/Batch31/SCRIPT_DOCUMENTATION_COVERAGE_GAP_LEDGER_20260605.md`.
- Added a 2026-06-05 coverage addendum to `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`.
- Added task pack `taskslocal/docs_actuality_20260605/` with five bounded documentation fronts.
- Spawned worker `019e94d8-383e-7eb3-a722-01293c768f97` for core signal/memory/persistence anchors; integrated anchors in `Docs/SYSTEMS_CONTRACTS.md`, `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`, `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`, `data.md`, and `persistence.md`.
- Spawned worker `019e94d8-af30-7202-bc96-194766d099fc` for the first-20 scene/resource/proof decision brief; integrated report `Docs/Reports/Batch31/FIRST20_SCENE_RESOURCE_PROOF_DECISION_BRIEF_20260605.md`.
- Spawned worker `019e94e0-e383-7f70-9981-36e50c875061` for UI/sonar/visor/navigation anchors; integrated anchors in `ui.md`, `sonar.md`, `rendering.md`, `UI_DIEGETIC_HUD_STANDARDS.md`, `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md`, and `Docs/ARCHITECTURE/SHINOBU_265_WATER_OPTICS_ROUTE_CARD.md`.
- Spawned worker `019e94e0-7d7f-7d60-a995-47353e0a5877` for water/vehicle/damage/lighting anchors; integrated anchors in `water.md`, `physics.md`, `vehicles.md`, `logistics.md`, `lighting.md`, `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md`, `Docs/ARCHITECTURE/Vehicle_Component_Damage_Router_SHINOBU_152.md`, `Docs/ARCHITECTURE/SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`, `Docs/ARCHITECTURE/SHINOBU_251_SUBMARINE_ADDED_MASS_ROUTE_CARD.md`, and `Docs/ARCHITECTURE/SHINOBU_263_ANALYTICAL_GERSTNER_WAVE_SOLVER.md`; primary patched water static-proof wording to `STATIC_SOURCE_REVIEWED`.
- Spawned worker `019e94e2-68ef-7df0-ae00-fc0a9532bda7` for world/material/mining/flora/AI anchors; integrated anchors in `shaders.md`, `world.md`, `terrain.md`, `voxels.md`, `tools.md`, `ai.md`, `creatures.md`, and `3DMODEL_FLORA_CORAL.md`.
- Added `Evidence class: STATIC_DOC` to standing root route bibles that lacked the header. Follow-up scan found no missing `Evidence class:` among the `PROJECT_BIBLES.md` route set.
- Updated `data.md` static-only proof wording from `STATIC VERIFIED` to static-reviewed language.

## In-Game Result

None. This pass is static documentation/source governance only.

## What Was Verified

- `python -B -m unittest discover -s Tools\ProofGate -p test_*.py` ran earlier in this orchestration pass and returned `OK` for `21` tests in shell output. This is `CLI_TEST_STATIC` only; no persisted test log artifact was produced in this pass.
- Static source/filesystem checks verified current script and asmdef counts above.
- Static source/filesystem checks verified first-hour drill route assets above are absent by exact path.
- Static docs verified scene spine and quality-gate contradictions.

## Regression Model

- CPU: no runtime code edited; no runtime CPU claim.
- GC: no runtime code edited; no GC claim.
- Memory: no runtime code edited; no memory claim.
- Cadence: documentation only; no dispatcher cadence changed.
- Correctness: improves agent routing by preventing stale source/proof claims from being treated as runtime acceptance.

## Hot Path Impact

No hot-path code was changed.

## Failure Modes

- Docs can still drift unless source maps are refreshed after major code batches.
- Scene spine remains unresolved because changing `AGENTS.md`, BuildSettings, or New Game route requires owner decision and Unity-proof work.
- Copper route remains unproven until item, metadata, held prefab, acquisition/provisioning, and Unity route proof exist or the first route is explicitly rerouted away from copper.

## Low / Middle / High / Ultra Consequences

- Low: agents get stricter static proof language and fewer false runtime claims.
- Middle: Batch31 documentation work can prioritize scene spine and first-20 route blockers instead of repeating stale missing-code claims.
- High: multiple owners can parallelize against a cleaner authority map without treating `800` SetPass as success.
- Ultra: proof packets still require Unity/Frame Debugger/Profiler/GC artifacts; static docs remain insufficient.

## Next Tasks

1. Resolve scene spine authority: direct world route, orbit route, or explicit dual New Game vs Load Game route cards.
2. Re-audit first-hour copper after deciding starter drill authoring versus fiber/silica route reroute.
3. Convert proof harness work to a safe manifest-backed packet route; keep `H8VisualProofCapture1912.cs` rejected until reviewed or removed.
4. Refresh `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md` date and deltas after subagent script coverage results return.
5. Integrate worker output from the two spawned doc-anchor agents, then assign the remaining water/vehicle/UI/world/material anchor waves.
6. Continue architecture/proof-language hygiene outside Batch31; this audit did not exhaust all historical reports or archives.
