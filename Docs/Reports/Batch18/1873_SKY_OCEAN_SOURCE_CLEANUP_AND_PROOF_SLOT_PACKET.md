# 1873 Sky/Ocean Source Cleanup And Proof Slot Packet

Date: 2026-06-04
Agent: 1873
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

This packet defines source cleanup requirements and one future Unity proof slot for sky/ocean product-face debt.

Owned outputs:

- `Docs/Tasks/Status_1873.md`
- `Docs/AgentLogs/Rationale_1873.md`
- `Docs/AgentLogs/LOG_1873.md`
- `Docs/Reports/Batch18/1873_SKY_OCEAN_SOURCE_CLEANUP_AND_PROOF_SLOT_PACKET.md`
- `Docs/Reports/Batch18/1873_SKY_OCEAN_PROOF_SHOT_LIST.csv`

No source, prefab, asset, scene, `.meta`, binary, Unity menu, import, bake, PlayMode, profiler, build, or Data Monolith action was performed.

## Authorities And Inputs

Read required authorities:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `water.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`

Read required reports:

- `Docs/Reports/Batch18/1808_AEGIR_SKY_ACTIVE_PATH_AUDIT.md`
- `Docs/Reports/Batch18/1810_RUNTIME_PROOF_HARNESS_PREP.md`
- `Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_PACKET.md`
- `Docs/Reports/Batch18/1865_SKY_OCEAN_PRIMITIVE_RISK_PROOF_PACKET.md`
- `Docs/Reports/Batch18/1865_SKY_OCEAN_PRIMITIVE_RISK_MATRIX.csv`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `Docs/Reports/Batch18/1868_PRODUCT_FACE_UNITY_VALIDATOR_GATE.md`

Read required mandates:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

`Docs/Actual Domains of Project.txt` was checked and produced no content in this run. Narrow domain used: sky/ocean/world/water/rendering proof.

## Evidence Boundary

Static text reads prove only serialized file content and existing report content. They do not prove Unity import, live renderer state, scene composition, material instance binding, first-frame visibility, GameView quality, player route readability, profiler cost, GC, Frame Debugger order, or Low/Middle/High/Ultra behavior.

All sky/ocean acceptance remains `PENDING UNITY SLOT`.

## Current Static Source Evidence

### `Sky_System.prefab`

Current source still contains an active child named `Sphere` with:

- built-in primitive sphere mesh `m_Mesh: {fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}`;
- enabled `MeshRenderer` with `m_Enabled: 1`;
- material GUID `c94a1beef2372b8458941c2ed9d05d5e`, resolved by prior report as `Mat_HectonSky.mat`;
- enabled `SkySystemFollowCamera`.

Static decision: source prefab is not product-face clean. It remains a primitive leak source.

### `Ocean_Crest.prefab`

Current source still contains three active Crest input children using built-in primitive plane mesh `10209` with source `MeshRenderer m_Enabled: 1`:

- `SargassumOilFilmInput` with `Crest.RegisterAlbedoInput` and `_disableRenderer: 1`;
- `SargassumWaveDampingInput` with `Crest.RegisterAnimWavesInput` and `_disableRenderer: 1`;
- `SargassumFoamDampingInput` with `Crest.RegisterFoamInput` and `_disableRenderer: 1`.

Current source also keeps `SargassumMicroFaunaBoids.boidMesh` on built-in primitive plane mesh `10209`, with `boidCount: 128` and no VAT texture assignment in the inspected serialized fields.

Static decision: source prefab is not product-face clean. The three Crest planes have partial hidden-input evidence; the boid mesh remains visible-presentation risk until replaced or proven invisible/acceptable in Unity.

## Current Static Scene Override Evidence

`Assets/_Project/Scenes/02_HECTON_WORLD.unity` currently instantiates both prefabs.

For `Sky_System.prefab` GUID `0f6bce861507514438034ae0ebadea15`, the scene override block sets:

- `m_Mesh` to GUID `82a557da3388e5c4ab037b7bce64c08f`, previously classified as `SkyDome_Inverted.asset`;
- renderer material slot 0 to GUID `4746c0454c9f1a74c84e406956ab30e3`, previously classified as `MAT_SurfaceCloudPanorama_1428.mat`;
- `m_ReceiveShadows = 0`;
- `m_ReflectionProbeUsage = 0`;
- `runtimeCamera` to a scene camera reference.

For `Ocean_Crest.prefab` GUID `0a7f97b6028cb014e80782578e9bf734`, the scene override block sets:

- `SargassumWaveDampingInput` renderer `m_Enabled = 0`;
- `SargassumFoamDampingInput` renderer `m_Enabled = 0`;
- `SargassumOilFilmInput` renderer `m_Enabled = 0`;
- Crest ocean `_material` to GUID `9def92ac79181fe41b238e91663f0fad`, previously classified as `Assets/Crest/Crest/Materials/Ocean.mat`;
- `_primaryLight` to a scene light reference.

Static decision: scene overrides reduce immediate `02_HECTON_WORLD` risk but cannot close source prefab debt. They do not prove first-frame runtime state or future scene safety.

## Source Cleanup Requirements

### Sky Source Prefab

Required cleanup before product-face acceptance:

1. Replace the source prefab built-in sphere mesh with the same intended production sky dome mesh route or another authored/generated sky dome mesh that passes material/texture/proof gates.
2. Remove enabled primitive renderer exposure from the source prefab. No future scene may need an override to avoid a built-in sphere.
3. Keep sky follow behavior separate from visual proof. `SkySystemFollowCamera` can remain only if its read/update route is proven by Unity later; its existence does not validate art.
4. Record source mesh/material intent in the prefab or adjacent proof packet: mesh path, material path, texture role, active owner, failure mode, and future screenshot gates.
5. Run the product-face prefab validator after cleanup in a future Unity slot. Expected state must be no `PRODUCT_FACE_PREFAB_BUILTIN_PRIMITIVE_MESH` for `Sky_System.prefab`.

### Ocean Source Prefab

Required cleanup before product-face acceptance:

1. Convert the three Crest input planes to explicit hidden-input-only source state or replace them with non-rendering input carriers. A future scene override must not be required to hide them.
2. If Crest requires a renderer for registration, source must serialize renderer hidden state and the Crest input must disable it before any player-visible frame. This requires Inspector/runtime proof, not static text alone.
3. Keep third-party asset integrity. Do not clone or mutate Crest package materials as a cleanup shortcut. Assign existing asset materials only through a scoped owner.
4. Replace `SargassumMicroFaunaBoids.boidMesh` with an authored/generated mesh or VAT/impostor asset that cannot read as flat Unity planes in player capture.
5. If micro-fauna remains card/impostor based, the card geometry must be a designed biological/VFX impostor with material animation, orientation, depth fade, and Frame Debugger proof. A raw built-in plane is not accepted.
6. Run both static audit and future Unity validator after source cleanup. Expected state must be either no primitive mesh hit for `Ocean_Crest.prefab` or an explicit hidden-input-only exception with runtime proof.

## Hidden-Input-Only Criteria For Crest Planes

The three Crest input planes may remain only if every criterion is met:

1. Source prefab serializes the input renderer disabled, or the component disables it during owner initialization before camera/player visibility.
2. The input GameObject is not used as visible ocean/foam/oil art.
3. The material/shader exists only for Crest data input and is excluded from player-facing capture acceptance.
4. Inspector proof shows renderer disabled on the live instance before PlayMode capture.
5. Frame Debugger or equivalent shows no draw call for the input plane as visible geometry in surface/waterline/photic shots.
6. If a pass exists for data input, the proof names the pass, owner, timing, and target data, and distinguishes it from visible rendering.
7. Low/Middle/High/Ultra quality does not toggle the plane into visible art.
8. Prefab validator exception, if added later, cites this route and does not become a broad primitive bypass.

If any criterion is missing, the input planes are still product-face primitive debt.

## Boid / Micro-Fauna Primitive Mesh Risk

The risk is not that plane cards exist. The risk is that a Unity built-in primitive plane with generic material becomes visible as final biological/ocean detail.

Acceptable future replacements:

- authored micro-fauna mesh with non-primitive silhouette, material role proof, LOD/HLOD route, and capture proof;
- generated mesh/VAT family with manifest, UV/material proof, non-flat silhouette, and route capture;
- designed impostor/card set with packed texture, depth fade, orientation behavior, no obvious quad edges, and Frame Debugger/profiler proof.

Required proof:

- close player capture where micro-fauna/sargassum is visible enough to detect cards;
- motion/readability capture under normal daylight/photic conditions, not only darkness/storm;
- Frame Debugger proof of mesh/material/pass and absence of surprise primitive draw;
- profiler/GC proof for boid update and rendering path;
- continuous `GlobalQualityWeight` behavior: Compact reduces density/cadence/detail without changing route truth; High/Ultra add richness.

Reject if the player can identify flat quads, billboard sheets, debug planes, or crayon/noise particles posing as fauna.

## Single Future Unity Proof Slot Sequence

The future owner must run one complete slot. Do not split acceptance between separate static reports.

1. Process safety check:
   - confirm no other Unity/build/profiler/DataMonolith owner is active;
   - confirm CPU is not above the project gate;
   - confirm no `dotnet`, `csc`, Unity import, shader compile, player build, profiler, or Frame Debugger job is active.
2. Open Unity only after the slot is uncontested.
3. Wait for compile/import readiness. Record console state before any validator/capture.
4. Run `Hecton8/Validation/Product-Face Prefab Quality Gate`.
5. Record validator output. A current failure for sky/ocean primitives is expected until source cleanup is done; do not downgrade it.
6. Load `02_HECTON_WORLD` through the project route where possible. If direct scene load is used for inspection, label it as editor inspection, not normal boot proof.
7. Inspect active `Sky_System` instance: MeshFilter, MeshRenderer, material, sky follow owner, active state, and child additions.
8. Inspect active `Ocean_Crest` instance: Crest material, input plane renderer state, Crest input components, micro-fauna mesh/material, primary light, kinematics bridge.
9. Capture the shot list in `1873_SKY_OCEAN_PROOF_SHOT_LIST.csv`.
10. Capture Frame Debugger or equivalent for skybox, Aegir/moons/clouds, ocean surface, input planes, micro-fauna, foam/refraction/waterline, and any surprise primitive draw.
11. Capture profiler/GC evidence for the same route: frame time, main/render thread, Crest ocean, sky/celestial update, micro-fauna update/rendering, GC Alloc.
12. Capture Low/Compact, Middle, High, and Ultra comparison from matched camera positions. Quality changes sensory density only.
13. Write final proof with artifact paths, hardware/tier, timestamp, scene route, evidence class per claim, blockers, and rejection decisions.

Allowed final states for future owner:

- `RUNTIME PROOF PASS WITH CURRENT ARTIFACTS`
- `BLOCKED BY SPECIFIC UNITY EVIDENCE`
- `ABORTED DUE TO UNITY SLOT/BUSY BUILD GATE`

## Screenshot / Player Capture Requirements

The CSV shot list is the controlling machine-readable checklist. Minimum visual set:

- surface ocean from shore;
- coastline/waterline;
- photic shallows 0-100 m;
- medium-depth 200-400 m twilight route;
- Aegir and moon readability;
- normal day;
- night;
- storm;
- eclipse;
- Low/Compact, Middle, High, Ultra captures from matched camera contexts.

Storm, night, and eclipse are event-state checks only. They cannot replace normal bright surface/daylight proof.

## Frame / Debug Proof Requirements

Future proof must identify:

- active skybox material, mesh, renderer, and pass;
- Aegir renderer/material/texture route and moon/cloud draw route;
- active ocean material and Crest pass structure;
- whether the three Crest input planes draw visibly or only feed Crest data;
- micro-fauna mesh/material/pass and whether it is a primitive plane;
- hidden/active renderer state for each high-risk source object;
- draw/SetPass evidence for sky, ocean, cloud cards, foam, waterline, and micro-fauna;
- profiler/GC evidence tied to the same route and tier;
- no surprise primitive draw in player-facing surface/photic frames.

## Acceptance Language

Use this language:

- `Sky/ocean source cleanup is statically specified, not accepted.`
- `Scene YAML override evidence is STATIC_SOURCE only.`
- `Sky/ocean visuals are PENDING UNITY SLOT until current captures, Frame Debugger, profiler, and GC artifacts exist.`
- `Product-face primitive debt is closed only after source cleanup or hidden-input-only proof plus validator disposition.`
- `Static YAML cannot accept sky, Aegir, moons, ocean surface, waterline, foam, refraction, photic shallows, medium-depth readability, or micro-fauna presentation.`

Forbidden language:

- "accepted from static source";
- "runtime clean" without Unity artifact paths;
- "0 GC" without profiler/GC artifact;
- "optimized" without profiler and hardware/tier context;
- "Subnautica-level" without current player captures.

## Rejection Triggers

Reject future sky/ocean proof if any shot or debug artifact shows:

- muddy water;
- flat or noisy sky;
- crayon/sine-stripe gas giant;
- hidden surface by darkness, storm, fog, bloom, silt, or UI overlay;
- visible primitive sphere or plane as final art;
- visible Crest input plane outside hidden-input proof;
- micro-fauna as obvious flat primitive cards;
- weak waterline, no readable refraction, no foam/specular/wetness identity;
- photic shallows below Subnautica-level readability;
- medium-depth route with no silhouettes, return cues, or instrument readability;
- Low/Compact tier is ugly, muddy, black, or route-hostile;
- binary quality jumps instead of continuous `GlobalQualityWeight`;
- profiler/Frame Debugger/GC claim without artifact path.

## Owner Route

Source cleanup owner:

- owns prefab source changes only after a scoped implementation task;
- edits `Sky_System.prefab` and/or `Ocean_Crest.prefab` plus any required authored/generated mesh/material/proof files under a new owned scope;
- does not run runtime acceptance from static cleanup alone;
- must preserve Crest third-party material integrity.

Runtime proof owner:

- owns one Unity slot after source cleanup or as a blocked-proof inspection;
- runs validator, scene load, captures, Frame Debugger/equivalent, profiler, and GC proof;
- reports exact artifact paths and evidence class per claim;
- does not modify source/prefabs unless explicitly assigned implementation authority.

No dependency on sibling agents 1869-1872 is required. This handoff is complete from current static evidence.

## Evidence Claims

Claim: `Sky_System.prefab` source still contains an enabled built-in primitive sphere.
Evidence Class: STATIC_SOURCE
Artifact: `Assets/_Project/Prefabs/Sky_System.prefab`
Command or Unity tool: PowerShell static read / `Select-String`
Date: 2026-06-04
Residual risk: no Unity import/runtime proof.

Claim: `Ocean_Crest.prefab` source still contains enabled built-in primitive input planes and built-in plane boid mesh.
Evidence Class: STATIC_SOURCE
Artifact: `Assets/_Project/Prefabs/Ocean_Crest.prefab`
Command or Unity tool: PowerShell static read / `Select-String`
Date: 2026-06-04
Residual risk: no Unity import/runtime proof.

Claim: `02_HECTON_WORLD.unity` currently overrides the sky instance mesh/material and disables three ocean input renderers.
Evidence Class: STATIC_SOURCE
Artifact: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
Command or Unity tool: PowerShell static read / `rg`
Date: 2026-06-04
Residual risk: scene text does not prove live renderer state, first-frame safety, or visual quality.

Claim: product-face validator/audit gate will still treat sky/ocean primitives as debt until source cleanup or hidden-input proof exists.
Evidence Class: STATIC_DOC / STATIC_SOURCE
Artifact: `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`, `Docs/Reports/Batch18/1868_PRODUCT_FACE_UNITY_VALIDATOR_GATE.md`, `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
Command or Unity tool: static report/source reads only
Date: 2026-06-04
Residual risk: validator menu was not run in this task.

## Final State

STATIC SOURCE CLEANUP AND FUTURE PROOF-SLOT PACKET COMPLETE.

Runtime/editor/player acceptance remains `PENDING UNITY SLOT`.
