# 1816 Surface Route Unity Slot Packet

Agent: 1816 / SURFACE_ROUTE_UNITY_SLOT_PACKET_BUILDER  
Date: 2026-06-04  
Evidence class: STATIC_DOC / STATIC_SOURCE only  
Final runtime state: PENDING UNITY SLOT

This packet consolidates completed Batch 18 surface/shallow reports into a single handoff for one future Unity owner. It does not claim Unity Editor access, Play Mode behavior, profiler data, Frame Debugger data, screenshots, build health, scene edits, material edits, or runtime acceptance.

## Inputs And Missing Inputs

Required inputs read:

| Report | Usable claim | Proof type | Current confidence | Routing decision |
|---|---|---|---|---|
| `1801_WORLD_SURFACE_ROUTE_EVIDENCE.md` | Active scene has route sockets, coast/water/sky hooks, and current baseline surface weaknesses. | STATIC_SOURCE / STATIC_SCREENSHOT_BASELINE | High for file/object existence; no runtime acceptance. | Use as baseline target map and rejection source. |
| `1802_SURFACE_SHALLOW_ASSET_INVENTORY.md` | Candidate ocean, wet basalt, foam, flora, industrial, Aegir, VFX, and HUD assets exist; no asset is production-ready from path alone. | STATIC_SOURCE / STATIC_ASSET | High for path inventory; medium for visual fit. | Use for candidate pools and third-party boundaries. |
| `1806_SURFACE_ROUTE_ACTION_MANIFEST.md/.csv` | Twelve route beats define spawn, surface, waterline, Aegir, coast, photic route, industrial, resource, scrap, fabricator, return path. | STATIC_DOC / STATIC_SOURCE | High as source action manifest. | Collapse into one strict Unity-slot order. |
| `1808_AEGIR_SKY_ACTIVE_PATH_AUDIT.md/.csv` | Active Aegir path appears to be `Mat_HectonSky` plus `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428`; disabled/noir sky routes are not proof. | STATIC_SOURCE | Medium-high until Unity confirms material instances and bindings. | Verify active route before any sky edit or capture claim. |
| `1809_PHOTIC_SHALLOWS_BIOTA_MANIFEST.md/.csv` | 0-100 m biota bands and density targets are authored as continuous `GlobalQualityWeight` samples. | STATIC_DOC / STATIC_SOURCE | High as placement intent; no runtime scatter proof. | Use as placement order around route sockets. |
| `1810_RUNTIME_PROOF_HARNESS_PREP.md/.csv` | Sixteen proof IDs define capture, gameplay, profiler, GC, and Frame Debugger requirements. | STATIC_DOC | High as proof checklist; no runtime proof. | Use proof IDs and rejection gates for acceptance. |
| `1813_STALE_BLOCKER_ERRATA_PACKET.md` | Old screenshots, static estimates, generated artifacts, and stale runtime fallback claims are not current proof. | STATIC_DOC | High as guardrail. | Prevent stale dependency mistakes. |
| `1814_COPPER_CATALOG_COLLISION_AUDIT.md` | Raw route owner is `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`; duplicate legacy `Data_Copper` remains an identity risk. | STATIC_SOURCE | High for data collision; no Unity validation. | Copper smoke gate must mark catalog validation pending until fixed by data owner. |
| `1815_STARTER_TOOL_AND_ROUTE_CRAFT_AUTHORITY.md` | Static boundary note only; no Unity/tool/craft proof. | STATIC_DOC | Low detail. | Keep tool/craft route smoke gate pending. |
| `1807_SHORELINE_WATERLINE_OFFLINE_BAKE_SPEC.md` | Missing. | ABSENT | No evidence. | Mark shoreline bake packet `PENDING_1807`; do not wait. |

`ocean.md` was requested by the task but is absent. `PROJECT_BIBLES.md` routes ocean/water presentation through `water.md`; this packet uses `water.md` as the water authority.

## Unity Slot Ownership

Single future Unity owner:

- Owns the live scene/material/renderer inspection pass in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`.
- Owns all reversible Unity setup, screenshots, Play Mode smoke checks, profiler, GC, Frame Debugger, console, and material binding proof.
- Must not split water/sky/coast/biota/fabricator captures across separate unverifiable routes.
- Must downgrade every unproven runtime claim to `PENDING UNITY SLOT`.

Offline-preparable before or beside that owner:

- Source packet review and route checklist preparation.
- Foam mask, wet basalt mask, Aegir texture polish, industrial bake, and biota placement data preparation.
- Shoreline bake spec remains `PENDING_1807`; only existing foam/wet-edge candidates may be inspected until the missing report or a new owned bake spec exists.
- Copper catalog collision repair is a separate data-owner task; Unity smoke proof must not pretend it is already fixed.

## Strict Implementation Order

The ordered machine-readable version is `Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_ORDER.csv`.

1. `1816_STEP_01_SOURCE_SYNC`: read this packet, 1806 CSV, 1808 matrix, 1809 CSV, and 1810 checklist; no Unity claim.
2. `1816_STEP_02_UNITY_BASELINE`: future Unity owner opens the route only when slot/build/CPU gates pass; capture console/baseline state before edits.
3. `1816_STEP_03_ACTIVE_SCENE_SURVEY`: inspect active scene references from 1806; reject inactive or disabled candidate proof.
4. `1816_STEP_04_AEGIR_ACTIVE_PATH`: prove or fix active sky/Aegir path from 1808 before using any sky screenshot.
5. `1816_STEP_05_OCEAN_ROUTE`: inspect `Ocean_Crest` and first-party ocean material candidates; no blind Crest camera enable.
6. `1816_STEP_06_SHORELINE_PENDING_1807`: keep shoreline bake spec pending; inspect existing foam/wet basalt candidates only.
7. `1816_STEP_07_COAST_WET_BASALT`: establish coast/wet basalt/rock dressing without grey procedural read.
8. `1816_STEP_08_PHOTIC_ENTRY`: apply 0-5 m and 5-20 m biota intent from 1809.
9. `1816_STEP_09_RESOURCE_BOWL`: apply 20-45 m resource-bowl visibility rules around copper and scrap.
10. `1816_STEP_10_LOWER_PHOTIC`: establish 30-100 m readability, lower photic silhouettes, and return view.
11. `1816_STEP_11_INDUSTRIAL_TRACE`: make dock/sub/turbine/wreck/service traces read as machinery and evidence.
12. `1816_STEP_12_COPPER_ROUTE`: verify `Node_Copper_A` physical read and mark catalog collision proof pending data-owner validation.
13. `1816_STEP_13_SCRAP_ROUTE`: verify `Scrap_A` physical salvage read.
14. `1816_STEP_14_FABRICATOR_SAFE_POCKET`: verify `Forward_Fabricator` / `Fabrication_Outpost` as a physical station and safe return node.
15. `1816_STEP_15_RETURN_AND_COCKPIT`: prove world-first return path plus HUD/oxygen/sonar readability.
16. `1816_STEP_16_FIRST_SCREENSHOT_SET`: capture the required surface/shallow set.
17. `1816_STEP_17_PROFILER_DEBUGGER_GC`: produce profiler, GC, memory/VRAM, batches/SetPass, and Frame Debugger artifacts for the same route.
18. `1816_STEP_18_PLAYER_SMOKE_GATES`: execute oxygen, copper/tool, safe-room/base return, and hazard readability smoke checks.
19. `1816_STEP_19_QUALITY_TIERS`: compare Compact/Middle/High/Ultra from matched cameras; quality changes sensory density only.
20. `1816_STEP_20_FINAL_PROOF_PACKET`: final future report must include artifact paths or mark exact blockers.

## First Screenshot Set

All items are `PENDING UNITY SLOT` until captured from the current scene.

| Shot | Source proof IDs | Requirement |
|---|---|---|
| Surface coast first read | `1810_PROOF_01`, `1810_PROOF_02` | Player-eye view with ocean, coastline, Aegir, clouds/moons, route cue, and readable HUD/instruments. |
| Waterline close-up | `1810_PROOF_03` | Low angle with water color, wave normals, specular, foam, wet basalt, entry/exit line, and return landmark. |
| Under-surface 5-20 m | `1810_PROOF_05` | Bright colorful photic entry, Starter_ReefField corridor, oxygen state, return direction, authored biota. |
| 30-100 m route | `1810_PROOF_06` | Forward and return views with readable lower photic silhouettes and instruments; no fog/dark cover. |
| Aegir horizon | `1810_PROOF_02` plus 1808 matrix | Active skybox/Aegir/cloud/moon path in one frame; disabled/noir candidates rejected. |
| Cockpit/shallow readability | `1810_PROOF_11`, `1810_PROOF_12` | Return path, HUD/oxygen/sonar readability, no UI overlap, no UI-only navigation. |
| Industrial/resource/fabricator close set | `1810_PROOF_07` through `1810_PROOF_10` | Machinery, copper, scrap, and fabricator must read physically and support route decisions. |
| Quality comparison | `1810_PROOF_15` | Same camera for Compact/Middle/High/Ultra; Compact remains attractive and readable. |

## Profiler And Frame Debugger Gates

No numeric performance result exists in this packet.

Future proof must include:

- Unity Profiler artifact tied to the same route sequence.
- GC Alloc column or GCMonitor/ProfilerRecorder artifact for exercised hot paths.
- Frame Debugger capture for active water, sky/Aegir, coast, foam, biota/instancing if touched, HUD, and UI overlay passes.
- Memory/VRAM notes for texture, render target, and material changes.
- Batches and SetPass evidence for coastline, industrial, flora, sky cards, and foam.
- Console state for the current session.
- Hardware/tier/context and exact artifact paths.

Reject any profiler, GC, Frame Debugger, or timing claim without artifact path, scene, timestamp, tier, and repro route.

## Player Route Smoke Gates

All gates are `PENDING UNITY SLOT`.

| Gate | Requirement | Source |
|---|---|---|
| Oxygen loop | Player enters water, reads oxygen/reserve/warning state, decides retreat or continue, and HUD path is 0-GC under proof. | `1810_PROOF_12` |
| Copper/tool route | `Node_Copper_A` is visible and physically credible; raw copper identity collision from 1814 is either fixed by data owner or explicitly pending. | `1810_PROOF_08`, 1814 |
| Scrap/tool route | `Scrap_A` reads as recoverable physical salvage with route cost. | `1810_PROOF_09` |
| Fabricator route | `Forward_Fabricator` / `Fabrication_Outpost` reads as physical machinery and route anchor, not menu-only UI. | `1810_PROOF_10` |
| Safe-room/base return | Player can return to `Route_Anchor`, coast, or station using world landmarks first and instruments second. | `1810_PROOF_11` |
| Hazard readability | First hazard, pressure, weather, or fauna cue telegraphs cause and counterplay; static fauna names are not behavior proof. | `1810_PROOF_13` |
| Death/respawn/drop rule | Required only if death is reachable in the proof route; otherwise mark pending. | `1810_PROOF_14` |

## Forbidden Moves

- Fake runtime proof from static paths, YAML, old screenshots, or report prose.
- Flat water, flat-color foam, grey coast, one-note sky, sparse photic route, or primitive hero debris.
- Reliance on disabled/inactive candidates: `H8_SURFACE_OCEAN_READ_1428`, `H8_AEGIR_SKY_BACKDROP_1428`, `SURFACE_GAS_GIANT_1428`, inactive noir sky cards, inactive biolum field.
- Broad scene cleanup, package mutation, or unrelated refactor.
- Blind Crest realtime depth/foam camera enable.
- Mutating Crest, MapMagic, GPUInstancer, MeshBaker, or SciFiFacility package assets.
- WorldRuntime/procedural placeholder assets as final visual proof.
- Darkness, fog, silt, bloom, storm, noir grading, or UI overlays used to hide weak surface/shallow art.
- Binary quality switches or an ugly Compact tier.
- Gameplay truth, save identity, DTO layout, resource identity, or route ownership changing with `GlobalQualityWeight`.

## Quality Consequences

Compact:

- Bright ocean color, clear Aegir/sky silhouette, strong coast/foam route landmarks, limited but authored biota, readable oxygen/HUD, conservative VFX.
- No ugly fallback, black water, flat foam, or UI-only route.

Middle:

- More shoreline foam blending, wet basalt breakup, biota density, cheap caustic cues, and route/instrument support.

High:

- Stronger water glint, richer coast detail, denser LOD flora, improved Aegir/cloud/moon softness, measured VFX/render features.

Ultra:

- Visual overkill in sky layering, near-field water/foam, photic biota density, machinery wear, lens/instrument polish.
- Same gameplay route and same item/save authority as Compact.

## Future Unity Implementer Prompt

```xml
<UNITY_IMPLEMENTER_PROMPT id="1816_SURFACE_ROUTE_SLOT">
  <ROLE>Single future Unity owner for surface/photic first-route implementation and proof.</ROLE>
  <INPUTS>
    Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_PACKET.md
    Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_ORDER.csv
    Docs/Reports/Batch18/1806_SURFACE_ROUTE_ACTION_MANIFEST.csv
    Docs/Reports/Batch18/1808_AEGIR_SKY_BINDING_MATRIX.csv
    Docs/Reports/Batch18/1809_PHOTIC_SHALLOWS_BIOTA_MANIFEST.csv
    Docs/Reports/Batch18/1810_SURFACE_ROUTE_CAPTURE_CHECKLIST.csv
  </INPUTS>
  <BOUNDARY>
    Use Unity only when the slot is uncontested and build/CPU gates pass.
    Do not edit scene, prefab, material, shader, data, or package assets outside this ordered packet.
    Do not mutate third-party packages.
    Do not claim runtime, profiler, Frame Debugger, GC, interaction, or screenshot proof without current artifact paths.
    Keep 0-100 m bright, colorful, readable, and beautiful. Darkness is not a surface fix.
  </BOUNDARY>
  <ROUTE>
    00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD -> spawn -> surface look -> waterline -> Starter_ReefField -> Route_Anchor -> Node_Copper_A -> Scrap_A -> Forward_Fabricator/Fabrication_Outpost -> 30-100 m route -> return path.
  </ROUTE>
  <ORDER>
    Follow 1816_SURFACE_ROUTE_UNITY_SLOT_ORDER.csv in step order. Do not skip Aegir active-path proof, waterline proof, photic route proof, or player smoke gates.
  </ORDER>
  <PRODUCE>
    Current screenshot set, Play Mode smoke notes, console state, profiler artifact, GC artifact, Frame Debugger artifact, memory/VRAM notes, batches/SetPass notes, and quality-tier comparison.
  </PRODUCE>
  <FINAL_STATE>
    Use only one final state:
    RUNTIME PROOF PASS WITH CURRENT ARTIFACTS
    BLOCKED BY SPECIFIC UNITY EVIDENCE
    ABORTED DUE TO UNITY SLOT/BUSY BUILD GATE
  </FINAL_STATE>
</UNITY_IMPLEMENTER_PROMPT>
```

## Final State

STATIC UNITY-SLOT PACKET COMPLETE.

Runtime/editor acceptance remains `PENDING UNITY SLOT`. Shoreline offline bake packet remains `PENDING_1807`.
