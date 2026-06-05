# 1880 Tool Material Texture Role Package

Date: 2026-06-04
Agent: 1880
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Report-only material and texture source audit for the 12 product-face tool families:

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

Owned outputs:

- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Tasks/Status_1880.md`
- `Docs/AgentLogs/Rationale_1880.md`
- `Docs/AgentLogs/LOG_1880.md`

No source, Unity asset, prefab, scene, `.meta`, binary, generated mesh, Unity menu, import, bake, PlayMode, profiler, dotnet build, or Data Monolith action was run.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `tools.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `Docs/Reports/Batch18/1869_TOOL_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain used: tool product-face material/texture source and proof packet.

## Static Findings

All 12 held/world tool prefab pairs still reference Unity built-in cube mesh:

```text
m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}
```

Current body material assignments are not production material sources:

- 11 tool families resolve held/world body materials to `Assets/_Project/Art/Materials/Tools/Mat_Tool_*_Placeholder.mat`.
- Those placeholder materials use `Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader` but bind no texture assets.
- `Tool_Propulsion_Held.prefab` resolves to package-cache URP `Lit.mat` under `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/.../Runtime/Materials/Lit.mat`.
- `Tool_Propulsion_World.prefab` resolves to `Assets/_Project/Art/Materials/Tools/Mat_Tool_Propulsion_Placeholder.mat`.
- Scanner marker support exists, but it is not a scanner body source: `Assets/_Project/Art/Meshes/M_ScannerMarkerQuad.asset` and `Assets/_Project/Art/Materials/MAT_HUD_ThreatChevronInstanced.mat`.

Static candidate support exists, but it does not close any material role:

- `Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader`: shared tool body shader candidate.
- `Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader`: display/readout shader candidate.
- `Assets/_Project/Art/Shaders/Hecton_FlashlightConeSilt.shader`: flashlight beam support only.
- `Assets/_Project/Art/Shaders/Hecton_TetherLineStrip.shader`: harpoon tether support only.
- `Assets/_Project/Art/Shaders/Hecton_LaserCutRadianceDecal.shader` and `Assets/_Project/Art/Materials/VFX/MAT_ShinobuPlasmaBeamIndirect.mat`: cutter effect support only.
- `Assets/_Project/Art/Materials/Construction/MAT_Equipment_Atlas.mat`: project-owned equipment material shell, but static scan found no texture bindings. It is not enough for tool role closure.
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_DirtyPressureGlass.mat`, `MAT_RuntimeVisualProof_WetPressureMetal.mat`, and label materials are visual-proof reference materials only unless a future Unity owner promotes them with texture role/import proof.

No credible project-owned albedo/normal/packed-mask/emission/detail texture set was found for these tool body roles. Existing terrain/flora/visor detail textures are not tool casing/grip/nozzle/lens/label source packages.

## Shader And Map Contract

`Hecton_ToolDecayLit.shader` declares:

- `_BaseMap`: base/albedo texture.
- `_MaskMap`: packed mask labelled `R Metallic G AO B Smoothness A Emission`.
- `_BaseColor`, `_EmissionColor`, `_Metallic`, `_Smoothness`, `_OcclusionStrength`.

`Hecton_CoreLit.hlsl` decodes packed masks as:

- R = metallic.
- G = occlusion/AO.
- B = smoothness.
- A = emission mask.

This differs from the generic 3D-model bible wording that permits G as roughness/smoothness according to shader contract. For these current tool shader candidates, the report uses the concrete shader contract: `R Metallic / G AO / B Smoothness / A EmissionMask`.

Every future tool material package must provide:

- Base/albedo: casing paint, rubber, metal, glass tint, label base, dirt/wear color.
- Normal: bevels, scratches, grip ribs, vents, nozzle rings, lens scratches, label raise where relevant.
- Packed mask: R metallic, G AO, B smoothness, A emission mask for `Hecton_ToolDecayLit`.
- Emission/readout: screen, lens, status strip, charge, beam, or diagnostic mask where the tool needs it.
- Detail/wear/label: salt, chipped paint, grime, heat discoloration, residue, serial labels, warning decals.

## Role Rules

Required roles per tool:

- Casing: worn pressure-rated metal/polymer shell, not flat color.
- Rubber/grip: ribbed black rubber, seals, strap, or insulation with normal detail.
- Metal/nozzle/blade: blade, barrel, nozzle, heat sink, spool, electrode, clamp, or intake metal with metallic and smoothness response.
- Glass/lens/screen: dirty pressure glass, scratched lens, sample tube, gauge, display, or explicitly marked not applicable.
- Emissive trim: display/readout/status/lens/charge masks only. No generic glow slab.
- Labels/decals: warning/service labels, direction marks, serials, material class marks.
- Wear/dirt/heat/residue: salt, scratches, grime, heat stains, slag, biological residue, fingerprints, or cable abrasion where relevant.

If a role has no credible existing candidate, the matrix marks `MISSING_SOURCE_REQUIRED`. It does not say "use color".

## Forbidden Assignments

Future relink must reject:

- Unity default material.
- Package-cache `Lit.mat` or package-cache `Lit.shader` as a product-face material route.
- `Assets/_Project/Art/Materials/Tools/Mat_Tool_*_Placeholder.mat`.
- `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_*`.
- `Assets/_Project/Art/Shaders/Hecton_RuntimeFlatColor.shader`.
- `Assets/_Project/Art/Shaders/Hecton_RuntimeCheckerboardUnlit.shader`.
- `Assets/_Project/Art/Materials/Diagnostics/MAT_ErrorCube.mat`.
- Scanner marker materials assigned as scanner body materials.
- Terrain/flora/celestial/visor textures assigned to tool bodies without a tool-specific material role manifest.
- Raw flat color-only material substitutes.

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` may scale material richness continuously. It must not change gameplay truth, tool IDs, capability masks, save identity, collision identity, anchors, DTO layout, scan/beam/muzzle origins, or authority routes.

- `0.0`: tool still has authored silhouette, casing/rubber/metal/glass separation, packed masks, readable labels where gameplay-relevant, and no flat placeholder color.
- Middle: adds grime, salt, label density, screen dirt, grip rib detail, and stronger AO.
- High: adds wetness response, scratched glass, heat discoloration, richer normal detail, and tighter material transitions.
- `1.0`: adds screws, micro scratches, secondary cables, extra decal passes, small display/lens detail, and richer sensory response only.

No low-tier to ultra-tier binary switch is accepted. Richness scales by texture resolution, detail mask strength, optional decal density, emission intensity/detail, and material feature budget.

## Future Unity Proof Required

This packet does not claim visual acceptance. Future closure requires one Unity owner in one consistent project state:

1. Import or create project-owned material and texture assets.
2. Assign held/world material slots for each tool family.
3. Prove no held/world visual MeshFilter still uses built-in cube.
4. Prove no product-face tool body uses placeholder, package `Lit.mat`, debug, trial, or flat-color material.
5. Prove texture import settings: albedo sRGB, normal as normal map/BC5 where possible, packed mask linear, mips enabled, compression set.
6. Prove material texture bindings and shader channel semantics.
7. Capture held first-person screenshot and world pickup screenshot per tool.
8. Capture use-case screenshot where the material role matters: scanner scan, flashlight dark route, repair weld target, cutter target, harpoon tether, propulsion thrust.
9. Run Frame Debugger/profiler only where visual/render acceptance or render-path cost is claimed.

Until then all tool material visual quality is `PENDING VERIFICATION`.

## Matrix

Detailed per-tool role rows are in:

`Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_MATRIX.csv`

## Verification Performed

Commands run:

```powershell
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\AGENTS.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\taskslocal\batch18_night_orchestration\1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.txt'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\PROJECT_BIBLES.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\VISION_LOCKS.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\TASTE.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\quality.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\tools.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\3dmodel.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\3DMODEL_TEXTURES_MATERIALS.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\Docs\Reports\Batch18\1869_TOOL_VISUAL_SOURCE_PACKAGE.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\Docs\Reports\Batch18\1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\Docs\Reports\Batch18\1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\.agents-skills\QA_Evidence_Text_Filter_Audit.txt'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\.agents-skills\CORE_Tools_Equipment_Interaction_Raycast_Heat.txt'
Get-Content -Raw -LiteralPath 'C:\hades\Hecton8\.agents-skills\REND_URP_Graphics_HotPath_Optimization_HLOD.txt'
rg --files 'Assets/_Project/Prefabs/Tools' 'Assets/_Project/Prefabs/Items/Tools' 'Assets/_Project/Art/Materials' 'Assets/_Project/Art/Shaders' 'Assets/_Project/Art/Textures' 'Assets/_Project/Art/Meshes'
rg -n 'Properties|_BaseMap|_BaseColor|_Normal|_Mask|MRAO|Emission|Wear|Detail|Roughness|Metallic|AO|Smoothness|Shader ' 'Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader' 'Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader' 'Assets/_Project/Art/Shaders/Hecton_TetherLineStrip.shader' 'Assets/_Project/Art/Shaders/Hecton_FlashlightConeSilt.shader' 'Assets/_Project/Art/Shaders/Hecton_LaserCutRadianceDecal.shader'
rg -n "HectonCoreLitDecodePackedMaskV1|struct HectonPackedMaskV1|PackedMask" 'Assets/_Project/Art/Shaders'
```

Static GUID-resolution scan result:

- All 24 held/world prefab entries are built-in cube visual bodies.
- Tool placeholder materials bind `Hecton_ToolDecayLit.shader` and no texture assets.
- Propulsion held material resolves to package-cache URP `Lit.mat`.

## Result

What was wrong: generated/future tool meshes would still be able to relink as flat colored shapes because no concrete material role package or texture source matrix existed, and current tool materials are placeholder/no-texture or package `Lit.mat`.

What I did: built a static material/texture role package and CSV for all 12 product-face tool families. Each role either names a static shader/support candidate or marks `MISSING_SOURCE_REQUIRED`.

In-game result: PENDING VERIFICATION. Unity and runtime proof were forbidden.

What was verified: static docs, prefab YAML, material GUID resolution, shader property/channel semantics, and absence of current tool texture bindings.
