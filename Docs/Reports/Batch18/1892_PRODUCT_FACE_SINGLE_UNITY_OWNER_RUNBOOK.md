# 1892 Product-Face Single Unity Owner Runbook

Agent: 1892
Mode: REPORT_ONLY_STATIC_UNITY_SLOT_RUNBOOK
Evidence class: STATIC_DOC / STATIC_SOURCE
Unity/build/import/menu/PlayMode/screenshots/profiler/DataMonolith/exporters: NOT RUN
Status: STATIC RUNBOOK COMPLETE / ALL UNITY RESULTS PENDING UNITY SLOT

## Scope

This is the controller-grade execution contract for the future single Unity owner who will clean product-face primitives after the current static authoring, material, texture, validator, anchor, and exclusion packets.

Owned outputs:

- `Docs/Reports/Batch18/1892_PRODUCT_FACE_SINGLE_UNITY_OWNER_RUNBOOK.md`
- `Docs/Reports/Batch18/1892_PRODUCT_FACE_SINGLE_UNITY_OWNER_SEQUENCE.csv`
- `Docs/Tasks/Status_1892.md`
- `Docs/AgentLogs/Rationale_1892.md`
- `Docs/AgentLogs/LOG_1892.md`

No source, asset, prefab, scene, `.meta`, binary, task file, Unity menu, import, exporter, DataMonolith, PlayMode, screenshot, profiler, or build action was run or modified by this task.

## Authorities Read

Root and domain authority:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `performance.md`
- `world.md`
- `celestial.md`
- `player.md`
- `tools.md`
- `inventory.md`

Absent authority requested by task:

- `ocean.md`: absent.
- `transport.md`: absent.
- `.agents-skills/PERF_Runtime_CPU_GC_ZeroAlloc.txt`: absent.

Mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`

Prior Batch 18 packets read by targeted extraction:

- `1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `1868_PRODUCT_FACE_UNITY_VALIDATOR_GATE.md`
- `1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `1876_TRANSPORT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `1877_PLAYER_SUIT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `1878_SKY_OCEAN_SOURCE_VALIDATOR_IMPLEMENTATION.md`
- `1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`
- `1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `1885_PRODUCT_FACE_PREFAB_ANCHOR_REFERENCE_STATIC_SNAPSHOT.md`
- `1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`
- `1887_PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET.md`
- `1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md`
- `1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MANIFEST.md`
- `1890_PRODUCT_FACE_MATERIAL_TEXTURE_VALIDATOR_IMPLEMENTATION.md`
- `1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md`

`1890_PRODUCT_FACE_MATERIAL_TEXTURE_VALIDATOR_IMPLEMENTATION.md` exists. Do not mark `1890_PENDING`; the future validation stack must include `Hecton8/Validation/Product-Face Material Texture Gate`.

## Static Starting Facts

Evidence boundary: all facts below are static text/YAML/report facts. They do not prove Unity import, GameView visuals, runtime behavior, profiler state, Frame Debugger state, or screenshot acceptance.

- 1867 reports 42 `PRODUCT_FACE_PREFAB_BUILTIN_PRIMITIVE_MESH` errors across product-face prefabs.
- 1868 adds `Hecton8/Validation/Product-Face Prefab Quality Gate` as a read-only Unity-side gate.
- 1874-1877 add editor-only source authoring routes for tools, resources, transport, and player suit mesh assets. These routes were not executed.
- 1878 adds `Hecton8/Validation/Sky-Ocean Source Primitive Gate` as a read-only Unity-side sky/ocean source gate.
- 1880-1883 show most product-face material roles are missing real texture sets. Flat color shells, default material GUIDs, and package `Lit.mat` routes are rejected.
- 1885 records anchor/reference preservation requirements.
- 1886 and 1891 identify the AI texture pipeline as reusable only through a ProductFace-specific manifest and a no-prefab-edit import phase.
- 1887 blocks deletion/quarantine of legacy roots without production-reference proof.
- 1888 defines shader-specific channel contracts. Future agents must not guess channel layout from `MRAO`, `ORM`, `ARM`, `Mask`, or `Packed`.
- 1889 excludes sky/ocean/terrain/Crest/flora/depth/noir materials as product-face donor sources.
- 1890 adds `Hecton8/Validation/Product-Face Material Texture Gate` as a read-only material/texture gate. Unity execution is still pending.

## Single Unity Owner Law

Only one future owner may perform Unity/editor/import/relink/proof work for this packet. That owner must serialize import refresh, mesh asset creation, material import, prefab relink, validator menu execution, screenshots, Frame Debugger/profiler/GC capture, and any later DataMonolith bake.

No other agent may concurrently run Unity, import, menu items, PlayMode, screenshots, profiler, Frame Debugger, build, DataMonolith, mesh exporters, or prefab/scene mutation while this owner holds the slot.

Acceptance from mixed editor states is rejected. Validator output, route screenshots, profiler proof, and rollback verification must come from the same Unity state after the same relink batch.

## Preflight Process Gate

Before Unity opens or receives any mutation:

1. Confirm CPU is below the project gate and no `dotnet`, `csc.exe`, Unity import, shader compiler, player build, profiler capture, exporter, or DataMonolith process is active.
2. Confirm a single-owner lock exists. The lock must name owner, start time, target categories, and blocked editor actions.
3. Capture a dirty tree snapshot before any Unity work. Separate owned future changes from other agents' dirty files.
4. Create a rollback plan before import or prefab save:
   - VCS branch/commit checkpoint, or
   - owned rollback folder outside runtime product routes, with prefab and `.meta` snapshots together.
5. Record target assets, GUIDs, renderer paths, material slots, anchor transforms, script/data references, and validator baseline.
6. Abort if the editor opens into compile/import errors unrelated to the owned route and cannot be isolated without touching other agents' work.

PENDING UNITY SLOT: CPU/process/build/editor state has not been checked by this agent.

## Static Prerequisites Before Unity Work

The Unity owner may start only when these static prerequisites exist:

- `Tools/GeneratedAssetProductionAudit.py`.
- `Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs`.
- `Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs`.
- `Assets/_Project/Scripts/Editor/ProductFaceMaterialTextureValidator.cs`.
- Source authoring scripts for tools, resources, transport, and player suit from 1874-1877.
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv`.
- `Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MATRIX.csv`.
- ProductFace-specific material/texture manifest or equivalent dry-run source/import manifest for any texture import.
- No dependency on broad `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv` for product-face relink.

If any prerequisite is absent, do not open Unity for product-face mutation. Write a static blocker and stop.

## Product-Face Source Generation Order

The future owner must execute source generation in this exact order:

1. Tool meshes:
   - menu/method route from 1874;
   - output root: `Assets/_Project/Art/Generated/ProductFace/Tools/`;
   - all 12 held/world families remain paired.
2. Resource pickup meshes:
   - route from 1875;
   - output root: `Assets/_Project/Art/Generated/ProductFace/Resources/`;
   - preserve `Data_Copper` and `Data_TitaniumScrap` truth.
3. Transport meshes:
   - route from 1876;
   - output root: `Assets/_Project/Art/Generated/ProductFace/Transport/`;
   - preserve `RiderAnchor`, `DismountAnchor`, presets, and mount contracts.
4. Player suit/body meshes:
   - route from 1877;
   - output root: `Assets/_Project/Art/Generated/ProductFace/PlayerSuit/`;
   - preserve `HandAnchor`, camera stack, HUD, `Suit_Visor`, and `Swim_*Attachment`.
5. Sky/ocean source validation:
   - run `Hecton8/Validation/Sky-Ocean Source Primitive Gate`;
   - source cleanup is not a ProductFace donor route.
6. Material/texture import route:
   - import only through ProductFace-specific manifests and product-owned texture/material folders;
   - do not touch prefabs in the import phase.
7. Material/texture validator:
   - run `Hecton8/Validation/Product-Face Material Texture Gate`;
   - abort on missing material family channel contract or forbidden donor route.

Do not resurrect stale non-ProductFace generated folder paths such as generic `Generated/Resources`, generic AI texture folders, terrain/coastline/environment routes, or old report-only source paths as relink authority.

## ProductFace Roots To Preserve

Current source route roots are locked:

- `Assets/_Project/Art/Generated/ProductFace/Tools/`
- `Assets/_Project/Art/Generated/ProductFace/Resources/`
- `Assets/_Project/Art/Generated/ProductFace/Transport/`
- `Assets/_Project/Art/Generated/ProductFace/PlayerSuit/`

Future source prefab roots may exist under category-specific `Sources/` folders only when the Unity owner proves they are product-owned and do not mutate gameplay anchors or data truth.

## Texture Import And Prefab Boundary

Texture import can use ProductFace-specific manifests without touching prefabs:

- Valid manifest route: `Assets/_Project/Data/ProductFace/TextureIngestion/product_face_texture_manifest.csv` or stricter equivalent.
- Valid output folders:
  - `Assets/_Project/Textures/ProductFace/{Resources|Tools|Transport|PlayerSuit|Shared}/`
  - `Assets/_Project/Materials/ProductFace/{Resources|Tools|Transport|PlayerSuit|Shared}/`
- Valid import phase writes: texture assets, material assets, import reports, validation reports.
- Invalid import phase writes: prefab YAML, scenes, runtime scripts, task files, Crest/MapMagic materials, broad AI texture binding CSV.

Hard boundary:

- `ai_texture_prefab_bindings.csv` is not allowed for product-face relink.
- Broad prefab binding, wildcard renderer paths, "all renderers" rows, missing renderer paths, folder-wide binding, and generic `AITextureMaterialBinder.AssignMaterialFromManifest` use are rejected.
- Prefab material slot mutation belongs only to a later ProductFace relink owner step with dry-run diff first.

## Material Channel Contracts

Abort if a material family has no declared channel contract.

Accepted static channel contracts from 1888:

- `Hecton_ToolDecayLit` / PackedMaskV1:
  - `_BaseMap`: albedo/base color, sRGB.
  - `_BumpMap`: tangent normal.
  - `_MaskMap`: `R = Metallic`, `G = AO/Occlusion`, `B = Smoothness`, `A = EmissionMask`.
- `Hecton_ProceduralBio` / ORM:
  - `_AlbedoAtlas`: organic albedo atlas.
  - `_NormalAtlas`: triplanar normal atlas.
  - `_ORMAtlas`: `R = Occlusion`, `G = Roughness`, `B = Metallic`, `A = EmissionMask`.
- `Hecton_MraoAtlasLit` / MRAO:
  - `_BaseMap`: albedo/base color, sRGB.
  - `_NormalMap`: normal map.
  - `_MraoMap`: `R = Metallic`, `G = Roughness`, `B = AO`, `A = EmissionMask`.
- `SuitVisor`:
  - `_HUD_RenderTexture`: HUD render texture.
  - `_ScratchNormalMap`: scratch normal.
  - `_FingerprintTex`: R fingerprint/smudge mask.
  - `_VisorMaskTex`: `R = Dirt`, `G = Scratch`, `B = Salt`, `A = Condensation`.
  - `_WaterRunoffNormalTex`: runoff normal.
  - `_WaterDropletMaskTex`: droplet mask.

Blocked until explicitly declared:

- tool screen packed maps;
- per-mineral pickup material route;
- organic pickup shader selection;
- scrap pickup route;
- transport hull/glass/rubber routes;
- player suit body and trim atlases;
- AI/UberNoir ARM product-face use;
- moon texture roles;
- Sargassum hidden input and micro-fauna source contracts.

Rejected as final source:

- Unity default material;
- package-cache `Lit.mat`;
- `Mat_Tool_*_Placeholder`;
- `Mat_Resource_*` flat shells;
- `MAT_PlayerSwimBlockout`;
- runtime proof swatches;
- diagnostic/checker/error/flat-color materials;
- empty texture slots.

## Environment Source Exclusions

1889 is binding for product-face material sourcing. The following are references only, not ProductFace donors:

- sky/cloud/Aegir/moon textures and materials;
- Crest package materials, shaders, wave normals, foam, caustics, OceanInputs;
- first-party surface ocean, waterline, swell, foam, and shoreline assets;
- basalt, terrain, rock, gravel, mud, sand, wet-strata, terrain layer assets;
- kelp, coral, flora, shallow biological atlases and materials;
- sargassum hidden input masks/materials and micro-fauna presentation materials;
- depth/noir/fog/storm/weather/eclipse/dirty splash materials and LUTs;
- route-owned visor maps except within the player visor route.

If a future owner wants a derivative look, they must create a product-owned derivative texture/material with owner approval, explicit albedo/normal/packed roles, shader channel layout, import proof, and `PENDING UNITY SLOT` proof label until captures exist.

## Anchor And Reference Preservation

Player:

- Preserve `HandAnchor`, `Main Camera`, `FirstPerson_Overlay_Camera`, `SpaceCamera`, `HUD_Render_Camera`, `Suit_Diegetic_HUD_V4_Projection`, `Suit_Visor`, `VisorHUDController`, `SuitHUDPresentationController`, `PlayerToolManager.handAnchor`, `PlayerSwimPresentationController`, and every `Swim_*Attachment`.
- Root `CapsuleCollider` remains movement truth.
- Visual mesh never becomes movement collision.

Tools:

- Relink held and world variants by family in the same Unity owner slot.
- Preserve `_toolData`, `PlayerToolSwimContract`, `PickupItem`, `InteractionHighlighter`, scan events, and `Tool_Propulsion_Held` transport support references.
- Do not infer ray/beam/muzzle/tether/pickup origins from mesh bounds. Add explicit anchors only under the Unity owner and record transforms.

Resources:

- Preserve `PickupItem`, `InteractionHighlighter`, `ScannableTarget`/`ScannableFragment`, item data references, pickup trigger location, and readable interaction distance.
- `CopperOre` keeps `Data_Copper.asset`.
- `TitaniumScrap` and retained `Item_Titanium` keep `Data_TitaniumScrap.asset`.

Transport:

- Preserve `RiderAnchor`, `DismountAnchor`, `PlayerTransportFeelContract`, `MountablePlayerTransport`, transport presets, occupancy/drive/camera feel fields, and current collider truth until vehicle owner proof changes it.

Sky/ocean:

- Preserve `SkySystemFollowCamera`, runtime camera route, primary light reference, Crest ocean material assignment route, Crest input bindings, `SargassumCrestDampingController`, `SargassumMicroFaunaBoids`, Crest kinematics/depth components, and micro-fauna owner references.
- Crest input planes can be hidden-input candidates only under exact Frame Debugger/runtime proof.

## Legacy Contamination Handling

No deletion or blind move is authorized by this runbook.

`Buildings/Cube.prefab`:

- `DELETE_FORBIDDEN_WITHOUT_UNITY_OWNER`.
- Static evidence found a GUID reference in `Assets/MapMagic/Map_Graph/Old tries/Terrain.asset`.
- A MapMagic/construction owner must classify it before quarantine.
- If retained, it needs a pressure-rated construction/module source package, material role proof, collider proxy proof, and screenshots.

`Item_Titanium.prefab`:

- `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`.
- Static GUID scan did not find broad production GUID references, but editor/bootstrap/validator and possible scene object expectations exist.
- If retained, it must canonicalize to `TitaniumScrap` mesh/material/data truth and preserve `Data_TitaniumScrap` plus scan route.

`STRUCTURES.prefab`:

- `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`.
- Must not leak primitive `Item_Titanium` child if retained.
- If retained, delegate to canonical child assets or leave production routes after proof.

`Tool_Propulsion_Held`:

- Uses package/default material GUID in prior static evidence.
- Must be replaced with project-owned tool material source under the same held/world family relink pass.

Default material GUID `31321ba15b8f8eb4c954353edc038b1d`:

- Rejected for retained product-face prefabs.
- Replacement proof must include project-owned material paths and live validator pass, not only mesh replacement.

## Validation Stack

Future owner must run the stack from the same Unity/project state:

1. Static audit:

```powershell
python Tools/GeneratedAssetProductionAudit.py --root .
```

2. Static audit hard gate:

```powershell
python Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error
```

3. Unity menu:

```text
Hecton8/Validation/Product-Face Prefab Quality Gate
```

4. Unity menu:

```text
Hecton8/Validation/Sky-Ocean Source Primitive Gate
```

5. Unity menu from 1890:

```text
Hecton8/Validation/Product-Face Material Texture Gate
```

6. Route screenshot set listed below.
7. Frame Debugger, profiler, GC, memory/VRAM, and SRP Batcher sanity when Unity is safe and runtime/visual acceptance is being claimed.

Do not claim runtime, visual, profiler, import, screenshot, SRP Batcher, or Frame Debugger acceptance from static text.

## Route Screenshot Set

Required after validators are green and before visual acceptance:

- surface coastline;
- Aegir/moons horizon;
- waterline close-up;
- under-surface 5-20 m;
- photic shallows 30-100 m;
- medium-depth hero route;
- cockpit/tool/resource close-ups;
- transport body close-ups if available;
- player suit close-ups if available.

Storm, eclipse, night, depth/noir, or fog captures do not replace normal daylight/surface/photic proof.

## Visual Rejection Criteria

Reject if any route shows:

- primitive body silhouette;
- flat color material;
- empty texture slots;
- unresolved/default/package material;
- muddy water;
- darkened surface;
- fog/noir/storm hiding weak art;
- untextured transport;
- crude tool silhouette;
- unreadable pickup;
- low-resolution or procedural-looking Aegir/moons/sky;
- waterline/foam that reads as opaque ribbon or flat strip;
- ProductFace assets below the Subnautica-level surface/shallow/medium-depth visual floor.

## Gameplay Rejection Criteria

Reject if product-face cleanup breaks:

- oxygen loop;
- starter tool route;
- copper/material pickup route;
- base/safe-room return;
- marker/sonar readability;
- pickup scan/interaction;
- tool held/world identity;
- `Tool_Propulsion_Held` transport support;
- mount/dismount safety;
- hostile route fairness before the player has tools.

## Performance Rejection Criteria

No fake numbers. Reject acceptance without:

- build/import/editor state evidence when Unity is used;
- profiler evidence for runtime/render cost claims;
- GC allocation checks for hot paths touched by future work;
- Frame Debugger or RenderGraph/SRP Batcher sanity for material/shader/render route changes;
- import size and VRAM review for new texture/material assets;
- LOD/HLOD or explicit visibility-range proof for visible generated assets;
- load-shed path if a render feature, shader feature, transparency, VFX, or dense material route adds cost.

Static reports may say `PENDING UNITY SLOT`; they may not say optimized, 0 GC, frame-clean, or visually accepted.

## Rollback Rules

Every Unity mutation step needs rollback before it starts:

- backup prefab and `.meta` together or use VCS checkpoint;
- record touched assets, GUIDs, renderer paths, material slots, anchors, script refs, data refs, collider refs, and source report paths;
- abort on compile/import error;
- abort on null material slot, missing texture slot, unresolved channel contract, missing normal import, packed mask sRGB error, default/package material route, or forbidden environment donor route;
- abort if any critical anchor/reference moves unexpectedly;
- abort if any validator red-gates critical refs;
- abort if screenshot shows visual rejection criteria;
- rollback whole category before moving to the next category;
- rerun static audit and all relevant Unity validators after rollback.

No partial save is acceptable when a validator red-gates critical refs. Do not leave mixed held/world tool families, partial resource truth, or half-cleaned sky/ocean source state.

## Parallel Work While Unity Is Busy

Safe for other agents:

- reports;
- source-only validators;
- manifests;
- static no-mutation audits;
- material/source role matrices;
- lore/content packets;
- screenshot shot-list prep;
- rollback plan drafts;
- CSV task slicing;
- source code review that does not run Unity/import/build.

Not safe in parallel:

- Unity/editor import refresh;
- menu execution;
- mesh asset creation;
- material import that triggers Unity;
- prefab relink/save;
- scene capture;
- PlayMode;
- screenshots;
- profiler/Frame Debugger/GC capture;
- build/dotnet;
- exporters;
- DataMonolith bake.

## Serialized Inside Unity

These actions must be serialized under one owner:

- import refresh;
- mesh source menu execution and asset creation;
- texture/material import;
- material validator menu execution;
- prefab relink;
- anchor/reference comparison;
- scene capture;
- route screenshots;
- Frame Debugger/profiler/GC proof;
- DataMonolith bake if a later explicit task adds it.

## Low / Middle / High / Ultra Consequences

Compact/Low:

- reduced texture size, density, cadence, and optional detail are allowed;
- no primitive/default/flat fallback;
- surface, sky, Aegir, waterline, photic shallows, tools, pickups, player suit, and transport must stay readable and materially credible.

Middle:

- full game truth with distinct ProductFace material families, correct albedo/normal/packed maps, readable labels/wear/wetness, and stable LODs.

High:

- richer normals, wetness, scratches, glass response, bevels, labels, foam/waterline detail, stronger route readability, and longer visual residency.

Ultra:

- visual overkill through micro fittings, droplets, decals, secondary cables, richer foam/atmosphere/micro-fauna, and denser material detail.
- No new gameplay truth, item IDs, collider identity, save identity, DTO layout, anchors, transport presets, recipe truth, or authority route.

## Future Unity Owner Prompt

```xml
<SUB_AGENT_PROMPT role="ProductFace Single Unity Owner">
You are the only Unity/editor/import/relink/proof owner for the ProductFace cleanup slot. Follow AGENTS.md, PROJECT_BIBLES.md, VISION_LOCKS.md, TASTE.md, quality.md, 3dmodel.md, 3DMODEL_TEXTURES_MATERIALS.md, 3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md, rendering.md, performance.md, world.md, celestial.md, player.md, tools.md, and inventory.md. ocean.md and transport.md were absent in agent 1892 static runbook context.

Before opening or mutating Unity, confirm CPU/process/editor/build/shader compiler/DataMonolith state, acquire a single-owner lock, snapshot the dirty tree, and create rollback for every target prefab plus .meta. Do not touch unrelated agent files.

Execute in order: static prerequisites, tool mesh sources, resource pickup mesh sources, transport mesh sources, player suit/body sources, ProductFace texture/material import through ProductFace-specific manifests only, Product-Face Material Texture Gate, player relink, held/world tool pair relink, resource relink, transport relink, sky/ocean source cleanup, legacy root classification. Never use ai_texture_prefab_bindings.csv for ProductFace relink. Never clone or mutate Crest package materials. Never use environment, terrain, flora, depth/noir, storm, or route-owned sky/ocean assets as ProductFace donors.

Run from the same Unity state: python Tools/GeneratedAssetProductionAudit.py --root ., python Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error, Hecton8/Validation/Product-Face Prefab Quality Gate, Hecton8/Validation/Sky-Ocean Source Primitive Gate, Hecton8/Validation/Product-Face Material Texture Gate. Then capture the required route screenshot set and only then Frame Debugger/profiler/GC/memory proof if acceptance is claimed.

Abort and rollback the whole category on compile/import error, validator red gate, moved critical anchor/reference, missing material channel contract, default/package/placeholder material, forbidden environment donor, primitive visible body, flat color material, muddy/darkened surface, unreadable pickup, untextured transport, crude tool silhouette, oxygen/tool/resource/base/marker/sonar route break, or missing profiler/Frame Debugger/GC proof for runtime claims.

All unproven Unity/import/runtime/visual/profiler statements remain PENDING UNITY SLOT.
</SUB_AGENT_PROMPT>
```

## Pending Unity Slot Labels

PENDING UNITY SLOT:

- Unity compile/import health.
- Mesh source menu execution.
- Generated mesh asset existence.
- Texture import state.
- ProductFace material asset creation.
- Prefab relink.
- Anchor/reference live comparison.
- Product-Face Prefab Quality Gate execution.
- Sky-Ocean Source Primitive Gate execution.
- Product-Face Material Texture Gate execution.
- Route screenshots.
- Frame Debugger/profiler/GC/memory proof.
- Visual acceptance.
- Runtime gameplay acceptance.
- Any future DataMonolith bake.

## Verification

Required static verification commands for this packet:

```powershell
git diff --check -- Docs/Reports/Batch18/1892_PRODUCT_FACE_SINGLE_UNITY_OWNER_RUNBOOK.md Docs/Reports/Batch18/1892_PRODUCT_FACE_SINGLE_UNITY_OWNER_SEQUENCE.csv Docs/Tasks/Status_1892.md Docs/AgentLogs/Rationale_1892.md Docs/AgentLogs/LOG_1892.md
Import-Csv Docs/Reports/Batch18/1892_PRODUCT_FACE_SINGLE_UNITY_OWNER_SEQUENCE.csv | Measure-Object
```

Static term cross-check targets:

- `ProductFace`
- `Unity owner`
- `GeneratedAssetProductionAudit`
- `Prefab Quality Gate`
- `Sky-Ocean Source Primitive Gate`
- `Subnautica`
- `Aegir`
- `photic`
- `rollback`
- `ai_texture_prefab_bindings`

Results are recorded in `Docs/AgentLogs/LOG_1892.md`.

## Result Boundary

What was wrong: prior packets created source routes, validators, material role packets, anchor snapshots, exclusions, and AI texture guardrails, but there was no single serialized Unity-owner runbook that told the future owner what to do, what to abort, and what other agents must not do in parallel.

What I did: produced this static runbook and the companion sequence CSV.

In-game result: PENDING UNITY SLOT. No Unity or runtime evidence exists from agent 1892.

What was verified: static documents, mandates, prior reports, owned file diff check, CSV parse count, and required term presence only.
