# First-20 Scene / Resource / Proof Decision Brief - 2026-06-05

Status: STATIC DECISION BRIEF / PENDING VERIFICATION

Evidence class: STATIC_DOC, STATIC_SOURCE, STATIC_FILESYSTEM.

Runtime, Unity import, Play Mode, profiler, GCMonitor, Frame Debugger, visual, save/load, and player-build proof are absent.

## Scope

This brief resolves nothing by itself. It lists the only valid owner decisions for the first-20 scene spine, the code/docs each decision must change, the proof packet each decision requires, and the first-hour resource routes that remain viable.

First 20 Minutes moment: `boot -> world load -> bright semi-open shallow exit -> swim -> resource -> tool interaction -> craft/repair/build -> hazard -> save/load`.

Mandates followed:

- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

Authority read:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `Docs/README.md`
- `Docs/Reports/Batch31/DOCS_ACTUALITY_COVERAGE_AUDIT_20260605.md`
- `Docs/Reports/Batch31/SCRIPT_DOCUMENTATION_COVERAGE_GAP_LEDGER_20260605.md`
- `taskslocal/docs_actuality_20260605/05_SCENE_FIRST20_PROOF_DECISION_BRIEF.md`
- `Docs/Reports/Batch31/SCENE_FLOW_AUTHORITY_DRIFT_20260605.md`
- `Docs/Reports/Batch31/COPPER_STARTER_CHAIN_REACHABILITY_20260605.md`
- `Docs/Reports/Batch31/3108_FIRST20_STAKE_UI_ROUTE_OWNER.md`
- `Docs/Reports/Batch31/3110_LORE_WORLD_CONSISTENCY_OWNER.md`
- `Docs/Reports/Batch31/PROOF_HARNESS_REPLACEMENT_SPEC_20260605.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`
- `bootstrap.md`
- `systems.md`
- `gameplay.md`
- `inventory.md`
- `tools.md`
- `persistence.md`
- `world.md`

## Static Blockers

- Scene spine is contradictory: root authority says `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`, while BuildSettings/topology/first-20 docs include `01_ORBIT`.
- `Assets/_Project/Scripts/MainMenuController.cs:72-73` defaults both load and New Game targets to `02_HECTON_WORLD`; `:82` still defines `01_ORBIT`; `:1384-1386` resolves target from those serialized fields.
- `ProjectSettings/EditorBuildSettings.asset:8-18` has all four scenes enabled: `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, `02_HECTON_WORLD`.
- `Assets/_Project/Scripts/Core/GameStartContext.cs:7-8` still documents New Game and Load Game as direct world routes.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:116-123` declares constants and paths for all four scenes; `:8113-8126` excludes `01_ORBIT` from gameplay scene activation.
- Copper is statically coherent but not first-route reachable: `ResourceNodeTemplate_CopperVein.asset:19` requires Drill, while the starter drill item, metadata, held prefab, and acquisition route are absent.
- `SeafloorDrillTool.cs` exists and returns `ToolCapabilityMasks.Drill` at `Assets/_Project/Scripts/SeafloorDrillTool.cs:118`, but source presence is not a starter route.
- `H8VisualProofCapture1912.cs` is rejected as a proof harness base because it can disable renderers and save scenes; raw PNG folders are not proof packets.

## Valid Scene-Spine Decisions

### Decision 1 - Direct World Spine

Decision:
`01_ORBIT` is not product New Game. Production flow is `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`. Orbit becomes optional, debug, cinematic-only, or parked until a later explicit prologue route.

Exact code/docs to change:

- Keep `Assets/_Project/Scripts/MainMenuController.cs:72-73` targeting `02_HECTON_WORLD`; remove or clearly demote unused orbit start behavior if owner chooses to reduce dead surface.
- Update `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` boot row from orbit flow to direct world flow.
- Update `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` active scene spine and remove the current orbit-as-active-route statement.
- Update any orbit/prologue route card to optional/debug status, not product New Game acceptance.
- If orbit is removed from production build spine, change `ProjectSettings/EditorBuildSettings.asset` through Unity owner only; do not raw-edit this report into BuildSettings.
- Update proof predicates and route manifests to reject unexpected `01_ORBIT` during first-20 New Game.

Required proof packet:

- Unity Console/import clean after scene route change.
- Main menu New Game run proving `01_MAIN_MENU -> 02_HECTON_WORLD` without `01_ORBIT`.
- Play Mode or player route through safe anchor, photic exit, swim, resource/tool/craft, hazard, save, load, and restored state.
- h8_1475+ visual proof packet under `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` with manifest, checksum, copied Unity log, six canonical screenshots, route predicates, and `validate_proof_packet.py --strict` pass.
- Profiler, GCMonitor, memory/VRAM snapshot, and save directory diff.

Static risk:
This aligns root `AGENTS.md` and current MainMenu defaults, but it rejects the current first-20/topology orbit expectation and may discard product-facing prologue work unless explicitly parked.

### Decision 2 - Orbit Product Spine

Decision:
`01_ORBIT` is promoted as the product New Game handoff. The first-20 route starts through `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.

Exact code/docs to change:

- Update `AGENTS.md` scene-flow authority to include `01_ORBIT`; this requires explicit owner approval because normal agents must not edit `AGENTS.md`.
- Change `Assets/_Project/Scripts/MainMenuController.cs:73` `newGameTargetSceneName` to `01_ORBIT`.
- If Load Game must also pass through orbit, change `Assets/_Project/Scripts/MainMenuController.cs:72` and `GameStartContext` semantics accordingly; otherwise choose Decision 3, not this decision.
- Update `Assets/_Project/Scripts/Core/GameStartContext.cs:7-8` comments and any start-context route documentation.
- Keep `ProjectSettings/EditorBuildSettings.asset:8-18` with all four scenes enabled, then verify order through Unity.
- Update `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` to remove the unresolved drift note and name orbit as product handoff.
- Keep `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` orbit boot row, but add explicit owner route and proof predicates.
- Audit `GameBootstrapper.cs:8113-8126` so orbit exclusion from gameplay activation is intentional and proven, not accidental skipped initialization.

Required proof packet:

- Unity Console/import clean after route changes.
- Main menu New Game proof reaching `01_ORBIT`, then handoff to `02_HECTON_WORLD`.
- Orbit exit proof: route context is cinematic/product handoff only and does not own gameplay truth, save identity, or hot scene state.
- First-20 route proof after world handoff: safe anchor, photic exit, swim, resource/tool/craft, hazard, save/load restored state.
- h8_1475+ visual proof packet with orbit/new-game route predicate and all required screenshots/log/manifest/checksum.
- Profiler, GCMonitor, memory/VRAM snapshot, save directory diff.

Static risk:
This matches the first-20 contract and BuildSettings, but it conflicts with current root authority and current MainMenu New Game default until those are changed and Unity-proven.

### Decision 3 - Explicit Dual Spine

Decision:
New Game uses orbit as product handoff; Load Game resumes directly into `02_HECTON_WORLD`. This preserves the first-20 orbit presentation while keeping save/load resume direct.

Exact code/docs to change:

- Update `AGENTS.md` scene-flow authority to state two routes: New Game `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`; Load Game `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
- Change `Assets/_Project/Scripts/MainMenuController.cs:73` `newGameTargetSceneName` to `01_ORBIT`; keep `Assets/_Project/Scripts/MainMenuController.cs:72` `targetSceneName` as `02_HECTON_WORLD`.
- Update `Assets/_Project/Scripts/Core/GameStartContext.cs:7-8` comments to the New Game vs Load Game split.
- Update `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` to mark dual spine as resolved, not drift.
- Keep `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` boot row but add the Load Game direct-resume exception as a proof card.
- Keep `ProjectSettings/EditorBuildSettings.asset:8-18` with all four scenes enabled, then verify order through Unity.
- Update proof predicates so New Game and Load Game are separate route IDs, not interchangeable successes.

Required proof packet:

- Unity Console/import clean after route changes.
- New Game proof: `01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.
- Load Game proof: `01_MAIN_MENU -> 02_HECTON_WORLD` direct resume from a saved first-route state.
- Save/load proof must show the same inventory, resource depletion, repair/hazard/evidence flags, player position, and route cue state after direct load.
- h8_1475+ proof packet with manifest fields for `route_id=new_game_orbit` and `route_id=load_game_world_resume`, copied Unity log, six canonical screenshots, checksum, and strict validator pass.
- Profiler, GCMonitor, memory/VRAM snapshot, and save directory diff for both route starts.

Static risk:
This is the most precise match to the current first-20 contract language, but it has the highest proof burden because two route starts must be proven and cannot share one vague screenshot packet.

## First-Hour Resource Route Options

### Option A - Copper With Authored Starter Drill

Route:
`Data_Copper -> Comp_CopperWire` remains V0 only if starter Drill becomes real.

Exact changes required:

- Add `Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset`.
- Add `Assets/_Project/Data/Tools/ToolMetadata_SeafloorDrill.asset`.
- Add `Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab`.
- Add acquisition/provisioning route so the player receives or earns the drill before the copper node.
- Update Player/tool loadout authoring so `Item_Tool_SeafloorDrill` and held prefab are registered. Static anchors currently mention the missing held path in `PlayerToolManager.cs:2644` and `ToolLoadoutProvisioner.cs:246`.
- Keep `ResourceNodeTemplate_CopperVein.asset:19` Drill-gated. Do not weaken it.
- Keep `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs:1115-1134` fail-fast validation until Unity route proof exists.

Proof required:

- Unity route where player obtains/equips starter drill, drills copper, gets `Data_Copper`, crafts `Comp_CopperWire`, and sees a real repair/build/consequence.
- Tool UI/capability proof: no implied Drill by text only.
- Save/load proof for drill ownership, harvested copper state, crafted output, and route consequence.
- Profiler/GC proof for tool interaction and UI path.

Status: PENDING VERIFICATION.

### Option B - Preferred Static Reroute: FiberKelp / FiberMesh / PressureSeal

Route:
`Data_FiberKelp -> Comp_FiberMesh -> Comp_PressureSeal -> apply to visible P-63/bathy-drop pressure boundary`.

Static support:

- `ResourceNodeTemplate_FiberKelpStand.asset:19` requires tool class `0`.
- `Recipe_FiberMesh.asset` exists.
- `Recipe_PressureSeal.asset` exists.
- `FirstHourDirector.cs:740` already accepts `Comp_PressureSeal` as a first-craft milestone result.
- `3108_FIRST20_STAKE_UI_ROUTE_OWNER.md` and `3110_LORE_WORLD_CONSISTENCY_OWNER.md` choose this as preferred static route while Drill is missing.

Exact changes required:

- Update `FirstHourDirector.cs:715` `firstResourceQuestId` away from copper or add a FiberKelp route quest.
- Update `FirstHourDirector.cs:718` `firstResourceItemId` away from `Data_Copper` or allow the chosen starter resource set.
- Keep or promote `FirstHourDirector.cs:740` `Comp_PressureSeal` as the statically listed first-craft milestone; if `Comp_FiberMesh` is meant to be the first craft, add it explicitly through owner-approved route logic.
- Place reachable `ResourceNodeTemplate_FiberKelpStand` in the selected first route.
- Place and prove membrane/resin secondary inputs required by PressureSeal; current reports mark placement and acquisition proof absent.
- Add a real P-63/bathy-drop seal target with applied-repair state, visible leak/intensity/access change, and save-compatible flags.
- Update route docs and proof predicates to make PressureSeal the V0 repair result, not a fallback footnote.

Proof required:

- Unity route where player harvests FiberKelp, crafts FiberMesh/PressureSeal, applies PressureSeal to a visible seal target, and the route state changes.
- UI proof for missing inputs, craft/apply state, and post-apply result from named owners.
- Save/load proof for harvested node state, inventory, consumed inputs, crafted item, applied seal flag, leak/access/return-pocket state, and evidence flags.
- h8_1475+ screenshots must show resource affordance, repair target, and route consequence.

Status: PENDING VERIFICATION.

### Option C - Minimal Shallow Craft Proof: Silica / GlassPanel

Route:
`Data_SilicaShards -> Comp_GlassPanel`.

Static support:

- `ResourceNodeTemplate_SilicaShardCluster.asset:19` requires tool class `0`.
- `Recipe_GlassPanel.asset` exists.
- Reports mark `Comp_GlassPanel` as not currently listed by `FirstHourDirector` as the first-hour result.

Exact changes required:

- Add `Comp_GlassPanel` to the `FirstHourDirector` first-craft route only if it has a real first-hour repair/build target.
- Add scene placement for silica shards inside the oxygen-safe return loop.
- Add a physical target that consumes GlassPanel and changes route safety, visibility, instrument readability, or access.
- Update route docs and proof predicates if GlassPanel becomes the selected V0 resource.

Proof required:

- Unity route where the player collects SilicaShards, crafts GlassPanel, applies it to a real target, and sees a physical result.
- Save/load proof for collected/crafted/applied state.
- UI and compact readability proof.

Status: PENDING VERIFICATION.

## Rejected Shortcuts

- Weakening CopperVein from Drill to Any, Knife, Salvage, or free pickup.
- Treating `SeafloorDrillTool.cs` source presence as starter Drill acquisition proof.
- Starting the player with copper, FiberMesh, PressureSeal, or GlassPanel through dev inventory grants without a route card and proof.
- Accepting a recipe because the `.asset` exists while no scene acquisition, inventory transfer, craft station, repair target, and save/load route is proven.
- Using UI text or quest text as proof of a physical repair result.
- Accepting raw screenshots under `Docs/Screenshots/MCP` as proof packets.
- Extending `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`; replacement must use the new proof packet route from `PROOF_HARNESS_REPLACEMENT_SPEC_20260605.md`.
- Hiding weak surface/shallows with darkness, fog, noir grading, or cropped screenshots.

## Proof Harness Requirement

All scene-spine/resource decisions remain blocked until the rejected harness is replaced.

Required packet root:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`

Required files:

- `manifest.json`
- `manifest.sha256`
- `UnityEditor_h8_1475_{session}.log`
- `screenshots/01_surface_coast_aegir_ui_off.png`
- `screenshots/02_shoreline_close_1m.png`
- `screenshots/03_underwater_0_5m.png`
- `screenshots/04_underwater_20_50m_route.png`
- `screenshots/05_aegir_celestial_long.png`
- `screenshots/06_regression_low_oblique.png`

Required validation:

```powershell
python Tools\ProofGate\validate_proof_packet.py --packet-root Docs\Screenshots\HectonProofPackets\h8_1475_{session} --packet-id h8_1475 --session-id {session} --expected-quality qNNN --min-post-capture-clean-seconds 60 --strict
```

Current static filesystem check: `Docs/Screenshots/HectonProofPackets` is missing.

## Required Owner Decision Order

1. Choose scene spine: Direct World, Orbit Product, or Explicit Dual.
2. Choose first-hour resource route: authored Drill/Copper, preferred FiberKelp/PressureSeal, or Silica/GlassPanel with a real target.
3. Replace proof harness before any acceptance screenshot campaign.
4. Implement route changes through Unity owners; do not raw-edit scenes, prefabs, or BuildSettings.
5. Produce route proof packet, save/load proof, profiler/GC/memory proof, and visual proof.

## Regression Model

- CPU: no runtime code changed by this brief. Future route work must prove frame cost with profiler artifacts.
- GC: no runtime code changed by this brief. Future UI/tool/craft/save proof must include GCMonitor or profiler allocation evidence.
- Memory/VRAM: no assets changed by this brief. Future route proof must include memory/VRAM snapshots and texture/RT budgets.
- Cadence: no dispatcher cadence changed. Future route systems must name owner phase and avoid hidden Update/coroutine ownership.
- Correctness: this brief reduces false acceptance risk by forcing one scene spine, one resource route, and one proof packet standard before runtime claims.

## Low / Middle / High / Ultra Consequences

- Low: route proof must preserve water/sky/shallows readability, oxygen/depth/pressure UI, resource affordance, return cue, and repair consequence. Ugly compact mode is rejected.
- Middle: selected route must feel product-facing, not a narrow resource test. Add route cues, material response, and readable interaction feedback.
- High: spend extra budget on richer seal/leak feedback, stronger biota/material evidence, longer LOD residency, and better instrument response without changing route truth.
- Ultra: add sensory overkill and secondary evidence only after Compact and Middle prove the same gameplay route, save identity, and resource math.

## Verification State

STATIC REVIEWED:

- Required authority and Batch31 reports were read.
- Static source/filesystem anchors confirm scene-spine conflict, missing starter Drill assets, current MainMenu target defaults, Drill-gated copper, and rejected proof harness conditions.

PENDING VERIFICATION:

- Scene-spine owner decision.
- Unity route changes.
- First-hour resource route implementation.
- Starter Drill or PressureSeal/GlassPanel applied repair route.
- h8_1475+ proof harness replacement and proof packet.
- Unity Console/import, Play Mode/player run, profiler, GC, memory/VRAM, visual, save/load, and player-build proof.
