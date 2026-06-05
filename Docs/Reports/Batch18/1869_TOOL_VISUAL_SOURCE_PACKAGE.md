# 1869 Tool Visual Source Package

Date: 2026-06-04
Agent: 1869
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Static source package for replacing product-face primitive tool visuals across held and world pickup variants:

- Tool_BeaconDeployer
- Tool_Builder
- Tool_EnvAnalyzer
- Tool_Flashlight
- Tool_HarpoonLauncher
- Tool_Knife
- Tool_LaserCutter
- Tool_Propulsion
- Tool_Repair
- Tool_SalvageSampler
- Tool_Scanner
- Tool_StunPistol

No source, prefab, asset, scene, `.meta`, binary, Unity menu, import, bake, PlayMode, profiler, dotnet build, or Data Monolith action was run.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `tools.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `inventory.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_PRIMITIVE_REPLACEMENT_QUEUE.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_REPLACEMENT_MATRIX.csv`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `Docs/Reports/Batch18/1868_PRODUCT_FACE_UNITY_VALIDATOR_GATE.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent. Domain used: tools/equipment visual source, inventory pickup proxy visuals, and static proof gates.

## Static Findings

Every held/world tool pair still uses Unity built-in cube mesh:

```text
m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}
```

This is static YAML evidence only. It proves primitive mesh references in the prefabs, not runtime appearance.

Material routes are mostly placeholder materials under `Assets/_Project/Art/Materials/Tools/`. One exception needs a decision:

- `Tool_Propulsion_Held.prefab` material GUID resolves to `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat`.
- `Item_Tool_Propulsion_World.prefab` resolves to `Assets/_Project/Art/Materials/Tools/Mat_Tool_Propulsion_Placeholder.mat`.

Data routes are present:

- Held prefabs reference `Assets/_Project/Data/Items/Tools/Item_Tool_*.asset`.
- Held prefabs reference `Assets/_Project/Data/Tools/ToolMetadata_*.asset`.
- World pickup prefabs reference the same item data via `itemData`.
- `Tool_Propulsion_Held.prefab` also references `Assets/_Project/Data/Transport/TransportPreset_Manta.asset`.
- `Tool_Scanner_Held.prefab` references `Assets/_Project/Art/Meshes/M_ScannerMarkerQuad.asset` and `Assets/_Project/Art/Materials/MAT_HUD_ThreatChevronInstanced.mat` for scanner marker presentation only.

No accepted non-primitive body mesh was found for any of the 12 tool identities. The found support routes are useful but not sufficient:

- `Assets/_Project/Editor/Generators/Interiors/EquipmentPropBaker1715.cs`: viable offline hard-surface equipment mesh generator route. It can produce beveled boxes, cylinders, cable bundles, anchors, mesh validation, and collision proxy data. It currently bakes a cockpit/control-panel style prop, not distinct handheld tools.
- `Assets/_Project/Editor/Assembly/EquipmentPrefabFactory.cs`: viable offline assembly route for generated equipment meshes, LODs, materials, text surfaces, anchors, and `COL_*` proxies.
- `Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader`: viable shared casing shader for PBR tool bodies with BaseMap, Normal, packed mask, dynamic wear, fog, caustics, and shadow pass.
- `Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader`: viable small tool display/readout shader for scanner/analyzer/builder/repair screens.
- `Assets/_Project/Art/Shaders/Hecton_FlashlightConeSilt.shader`, `Hecton_VolumetricLight.compute`, and `Hecton_VolumetricLightProxy.shader`: support flashlight beam presentation, not body mesh.
- `Assets/_Project/Art/Shaders/Hecton_TetherLineStrip.shader`: support harpoon tether presentation, not body mesh.
- `Assets/_Project/Art/Meshes/M_ScannerMarkerQuad.asset`: scanner marker support only. It is not a scanner body.

Rejected candidate classes:

- Existing `Mat_Tool_*_Placeholder.mat` materials: placeholder/proxy only.
- `WorldProceduralProxy` prefabs: excluded by task and wrong source route for handheld tools.
- `FloraDataTemplate_KnifeMat.asset`: name collision; flora data, not a knife source.
- Generic `PROPS.prefab`/legacy aggregate routes: not verified as distinct held/world tool source.

## Owner Route

One visual source package per tool must be authored as an offline equipment asset family:

1. Author or generate LOD0 mesh parts per tool identity.
2. Derive LOD1 world pickup mesh from the same family, not a separate generic cube.
3. Derive LOD2 coarse pickup silhouette from the same family.
4. Assign shared material slots by role, not one material per part.
5. Preserve anchors and runtime ownership: tool gameplay owns truth; visual mesh is presentation only.
6. Add `COL_*` primitive/capsule/box proxies for interaction and pickup; no LOD0 visual MeshCollider.
7. Validate with prefab YAML, mesh path, material path, collider split, screenshots, player capture, and profiler only if runtime render behavior changes.

Do not patch current prefabs until a source asset exists and Unity authoring is allowed.

## Shared Material Contract

Each tool needs documented texture roles:

- Albedo: worn casing, labels, paint, rubber, glass, metal, residue.
- Normal: bevels, scratches, grip ribs, nozzle ridges, labels where raised.
- MRAO/packed mask: R metallic, G roughness/smoothness per shader contract, B AO, A emission/wetness/family mask.
- Glass/emissive: dirty lens/display/emitter masks; no generic glow surface.
- Rubber/metal split: grip, seals, casing, barrel, heat sink, blade, fins.
- Grime/wetness: salt, scratches, chipped paint, biological residue where tool-specific.
- Decals/labels: warning labels and service markings readable in first person.

`MAT_Equipment_Atlas.mat`, `Hecton_ToolDecayLit.shader`, and `Hecton_ToolScreenDiegetic.shader` are support routes. They do not close material proof until concrete texture paths and screenshots exist.

## Transform And Anchor Preservation

Authoring must preserve or create named anchors without changing gameplay truth:

- `ANCHOR_Grip_R` or `ANCHOR_Grip_LR`
- `ANCHOR_RayOrigin` family-specific alias: `ANCHOR_BuildRayOrigin`, `ANCHOR_ScanOrigin`, `ANCHOR_BeamOrigin`, `ANCHOR_WeldRayOrigin`, `ANCHOR_Muzzle`, `ANCHOR_ThrustOrigin`
- `ANCHOR_Emitter`, `ANCHOR_Nozzle`, `ANCHOR_LensCenter`, `ANCHOR_ElectrodeTips` as applicable
- `ANCHOR_TetherAnchor` for HarpoonLauncher
- `ANCHOR_Pickup` for world variants
- `ANCHOR_AUP_LocalOrigin` where tool range, projectile, tether, scan, or propulsion route can cross origin-shift-sensitive space

Presentation can move within the visual hierarchy. Runtime truth owners must not infer gameplay origins from decorative mesh bounds.

## Collider And Proxy Split

Required:

- Visual children named `VIS_*` or `LOD_*`.
- Collision/interaction children named `COL_*`.
- `COL_PickupTrigger` on world variants.
- `COL_Grip*`, `COL_Body*`, and verb-specific proxy (`COL_BeamOriginProxy`, `COL_MuzzleProxy`, `COL_SampleContactProxy`, etc.).
- No visual MeshCollider for LOD0 unless a future proof artifact shows a safe convex proxy route. Current package assumes primitive/capsule/box proxy split.

## Priority

First three tools to author for first-20-minute gameplay:

1. `Tool_Scanner`: first-hour evidence/navigation trust. Current marker support exists, but the handheld body is still a cube.
2. `Tool_Flashlight`: visibility is a resource. The lens/beam origin must read in hand and in pickups before dark routes can be judged.
3. `Tool_Repair`: survival infrastructure and damaged machinery verbs need a believable welder/nozzle/gauge silhouette.

Next tier after those: `Tool_Builder`, `Tool_SalvageSampler`, `Tool_LaserCutter`.

## Risk Register

- All 12 rows need source body assets. Static source support exists, accepted meshes do not.
- Propulsion has a material route mismatch and a transport preset dependency. Decision needed before authoring.
- Placeholder materials cannot be promoted by relabeling; they need real texture roles and screenshot proof.
- Scanner marker assets are not body assets.
- Any generator route that produces primitives must be treated as authoring debt and reported only. No patching in this task.
- No visual acceptance can be claimed without screenshots/player capture.
- No runtime/profiler claim can be made from this packet.

## Proof Ladder

Per row closure requires:

1. Prefab YAML: held and world variants contain no enabled Unity built-in primitive visual mesh.
2. Mesh path: LOD0/LOD1/LOD2 mesh assets exist and are distinct per tool identity.
3. Material path: all material slots resolve to project materials with documented texture roles.
4. Collider proof: visual mesh is separate from `COL_*` proxies and pickup trigger.
5. Anchor proof: grip/ray/muzzle/nozzle/emitter/scan/tether/pickup/AUP anchors are named and preserved.
6. Screenshot: held first-person and world pickup captures.
7. Player capture: tool use or pickup in context.
8. Profiler/Frame Debugger: required only if runtime render, light, beam, tether, VFX, or draw path changes.

## Matrix

Detailed per-tool rows are in:

`Docs/Reports/Batch18/1869_TOOL_VISUAL_SOURCE_MATRIX.csv`

## Result

What was wrong: product-facing held/world tool prefabs are still cube-based blockout art and mostly placeholder material routes.

What I did: built a static visual source package defining per-tool mesh parts, material roles, anchors, collider proxy split, LOD expectations, continuous `GlobalQualityWeight` consequences, proof ladder, and row status.

In-game result: PENDING VERIFICATION. Unity and player capture were forbidden.

What was verified: static prefab YAML, GUID-to-path resolution, data owner paths, and support source/shader paths only.
