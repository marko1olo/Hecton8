# ASSET_OWNER_18 - Product-Face Validator Evidence Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` + `STATIC_SOURCE` only.
Scope: future-owner packet for existing editor validators covering product-face material/texture routes, prefab primitive risk, and sky-ocean source risk.
Boundary: no Unity launch, batchmode execution, import, Play Mode, prefab edit, scene save, build, screenshot, Frame Debugger capture, profiler capture, or `Assets/` mutation was performed for this packet.
First-20 route blocker mapped: false promotion risk for visible product-face tools, resources, transport, construction, sky, Aegir, clouds, moons, and ocean source art on the bright first-exit/photic route.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

## Evidence Boundary

These validators are editor source gates. Their findings are useful stop signals, not in-game visual proof.

- Static source and editor log text prove only the inspected source condition.
- Validator no-finding text, if any future run emits it, does not prove route visuals, active scene binding, import settings, VRAM, material-submit counts, shader variants, GC, frame time, screenshots, or player route quality.
- A future batchmode log can raise the validator observation to `UNITY_CONSOLE` evidence for that editor run only. It still cannot prove in-game visuals.
- Any claim beyond source inspection remains `PENDING_VERIFICATION` until backed by Unity readback, Game View and Scene View captures, Frame Debugger, profiler, memory, and route-scene artifacts.

## Mapped Validators

| Validator | Menu / executeMethod target | Source file | Primary gate |
|---|---|---|---|
| Product-face material texture | `Hecton8/Validation/Product-Face Material Texture Gate` / `Hecton8.EditorTools.ProductFaceMaterialTextureValidator.ValidateFromMenu` | `Assets/_Project/Scripts/Editor/ProductFaceMaterialTextureValidator.cs` | Material route, texture-role, package/default/placeholder/blockout/diagnostic route debt |
| Product-face prefab quality | `Hecton8/Validation/Product-Face Prefab Quality Gate` / `Hecton8.EditorTools.ProductFacePrefabQualityValidator.ValidateFromMenu` | `Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs` | Built-in primitive visual mesh risk and missing renderer hierarchy |
| Sky-ocean source primitive | `Hecton8/Validation/Sky-Ocean Source Primitive Gate` / `Hecton8.EditorTools.ProductFaceSkyOceanSourceValidator.ValidateFromMenu` | `Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs` | Sky/Ocean source prefab primitive risk, Crest hidden input exceptions, Sargassum boid mesh source |

## Safe Future Execution Boundaries

Future Unity owner may run these only after the asset-front process gate clears: CPU below the local threshold, no active `dotnet`, `csc`, `MSBuild`, `Unity.ILPP.Runner`, `UnityShaderCompiler`, `UnityPackageManager`, import, or build process, and no dirty-scene save prompt.

Use menu execution in an open editor only for read-only inspection. Do not save scenes, prefabs, project settings, materials, import settings, Addressables data, or generated reports under `Assets/`.

Safe batchmode shape for a future owner:

```text
Unity.exe -batchmode -projectPath c:\hades\Hecton8 -quit -executeMethod Hecton8.EditorTools.ProductFaceMaterialTextureValidator.ValidateFromMenu -logFile Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_material_texture_validator_<timestamp>.log
Unity.exe -batchmode -projectPath c:\hades\Hecton8 -quit -executeMethod Hecton8.EditorTools.ProductFacePrefabQualityValidator.ValidateFromMenu -logFile Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_prefab_quality_validator_<timestamp>.log
Unity.exe -batchmode -projectPath c:\hades\Hecton8 -quit -executeMethod Hecton8.EditorTools.ProductFaceSkyOceanSourceValidator.ValidateFromMenu -logFile Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_sky_ocean_source_validator_<timestamp>.log
```

Expected artifact paths:

- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_material_texture_validator_<timestamp>.log`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_prefab_quality_validator_<timestamp>.log`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_sky_ocean_source_validator_<timestamp>.log`
- Optional synthesis after all three future runs: `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_product_face_validator_synthesis_<timestamp>.md`

## Material / Texture Validator Scope

`ProductFaceMaterialTextureValidator` inspects:

- Exact product-face prefabs: `Player`, `Sky_System`, `Ocean_Crest`, `Item_Titanium`, `STRUCTURES`, and `Buildings/Cube`.
- Product-face prefab roots: held tools, item tools, resource pickups, and transport.
- Static material targets for tools, resources, player, transport, shared equipment, sky/ocean, event-only environment, deep-only environment, diagnostics, and package defaults.
- Required albedo/base texture, normal/detail-normal texture, packed material mask, and packed channel declaration where each static material target requires them.
- Forbidden material routes: package default, placeholder, blockout, diagnostic, package-cache Lit material, and environment material assigned outside allowed sky/ocean/event/depth scope.
- Static text debt in selected Batch18 reports for package default GUID or package-cache Lit material traces.

It does not prove:

- Importer settings, compression, mip chain, streaming mip behavior, texture residency, SRP Batcher state, material variant count, shader quality, active scene instance binding, route visibility, screenshots, frame time, GC, or VRAM.
- That a material looks premium in bright surface or underwater lighting.
- That placeholder-looking art is fixed by having texture slots.

Failure interpretation:

- Missing albedo, normal, packed mask, or channel declaration means the material source is blocked for product-face use until texture-role and channel semantics are fixed.
- Package default, placeholder, blockout, or diagnostic route findings are hard stop signals for visible product-face renderers.
- Environment-route out-of-scope means a sky/ocean/depth/storm material is leaking into a prefab family that does not own that visual route.
- Missing static material target or static report is source ambiguity. Treat as blocked until the owner either restores the path or removes it from the validator with documented reason.

## Prefab Primitive Validator Scope

`ProductFacePrefabQualityValidator` inspects:

- The same exact product-face prefabs and prefab roots as the material validator.
- Required prefab presence and required root folder presence.
- Built-in Unity primitive visual mesh references through `WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh(prefabPath)`.
- Presence of a renderer hierarchy.

It does not prove:

- Replacement mesh quality, bevels, LOD chain, collider proxy layout, material routes, import settings, active scene instance state, interaction anchors, Addressables identity, screenshots, frame time, GC, or VRAM.
- That a non-primitive mesh has acceptable silhouette, scale, texture density, or route readability.

Failure interpretation:

- Missing required prefab/root means the product-face source set has missing entries and the gate cannot be trusted as coverage.
- Built-in primitive mesh finding means visible source art still reads as cube/sphere/capsule/plane-class placeholder risk and must not be promoted into route content.
- No renderer hierarchy means the prefab is either hidden/input-only or broken for this generic visual gate. It needs explicit owner proof outside this validator.

## Sky-Ocean Source Validator Scope

`ProductFaceSkyOceanSourceValidator` inspects:

- `Assets/_Project/Prefabs/Sky_System.prefab`.
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`.
- MeshFilter hierarchies for built-in primitive mesh GUID or Unity default primitive fallback.
- Visible active primitive meshes, with stronger category for sky dome body primitive risk.
- Narrow Crest input-source primitive exceptions at exact paths: `SargassumOilFilmInput`, `SargassumWaveDampingInput`, and `SargassumFoamDampingInput`, requiring the expected Crest input component and disabled renderer or `_disableRenderer`.
- `Hecton8.World.SargassumMicroFaunaBoids.boidMesh` source presence and non-built-in mesh route.
- Scene override risk warnings for sky and ocean source prefabs.

It does not prove:

- `02_HECTON_WORLD` live scene binding, active renderer visibility, Crest runtime state, waterline quality, skybox state, Aegir/cloud/moon texture quality, import roles, screenshots, Frame Debugger, profiler, memory, or route beauty.
- That hidden source primitives remain hidden in every runtime route state.
- That non-built-in boid mesh source looks good or scales correctly.

Failure interpretation:

- Missing `Sky_System` or `Ocean_Crest` source prefab blocks sky/ocean source coverage.
- Visible active primitive mesh in sky/ocean source is a hard stop for surface route source quality.
- Crest input exception only covers exact hidden data-input paths; it is not visible art proof.
- Missing or built-in Sargassum boid mesh means ocean micro-fauna source remains blocked until an authored/generated mesh, VAT, or designed impostor route is proven.
- Scene override warnings require future live-scene readback. Static prefab cleanup is not active scene proof.

## Rejection And Stop Conditions

Stop future promotion work if any of these appear:

- Any validator emits `Debug.LogError`.
- Missing required prefab/root/source prefab.
- Product-face renderer uses package default, placeholder, blockout, diagnostic, or out-of-scope environment material route.
- Required material role texture or packed channel declaration is missing.
- Built-in primitive mesh appears on visible product-face prefab source or visible sky/ocean source.
- Sky/Aegir/cloud/moon/ocean proof is attempted from orbit/prologue/static refs instead of `02_HECTON_WORLD` route readback.
- Future evidence claims route visuals from static source, menu text, or batchmode log alone.
- Any proposed fix uses raw YAML mutation, scene save without scoped dirty proof, runtime material clone, Crest wrapper, package default material, or darkness/fog/post to conceal weak art.

## Regression Model

- CPU: this packet changes no runtime CPU. Future fixes risk more renderers, LODGroup work, shadow casters, shader features, and batch submission cost. Any new system above `0.1 ms` needs profiler evidence and load-shed behavior.
- GC: this packet changes no runtime code. Future fixes must avoid hot-path allocation, runtime mesh generation, runtime material instantiation, `Resources.Load`, per-frame string work, or scene search. Per-frame allocation claims require GCMonitor/Profiler artifacts.
- Memory/VRAM: validator logs do not prove residency. Future fixes risk texture budget growth, mip pressure, material variants, and longer LOD residency. Compact VRAM ceiling remains 1800 MB with texture budget discipline and mip downgrade pressure when the project reaches its configured threshold.
- Renderer submission/batches: material cleanup can regress batching if unique materials or shader variants multiply. Use shared material families, atlases/trim sheets, SRP Batcher-compatible shaders, and GPU Resident Drawer paths where appropriate.
- Cadence: no runtime cadence changed here. Future LOD, renderer, or visual-source changes need hysteresis and continuous scaling, not flicker-prone state flips.
- Correctness: prefab identity, save identity, interaction anchors, collider authority, Addressables keys, and Crest/MapMagic ownership must not change without route owner proof.
- Visual floor: any future fix that is fast but flat, muddy, blurry, primitive, or hidden by darkness is rejected by product-face route standards.

## Continuous GlobalQualityWeight Consequences

These are checkpoints on one continuous `GlobalQualityWeight` curve, not binary quality modes.

- Weak hardware, about `0.0-0.25`: keep final non-primitive silhouettes, role-correct compressed material maps, baked AO, readable sky/ocean composition, and premium product-face identity. Reduce density, residency, shadow eligibility, secondary layers, and update cadence smoothly.
- Middle hardware, about `0.25-0.55`: keep stable LOD transitions, final material families, documented collider proxies, and sky/ocean slot proof before route use. Avoid unique material drift.
- High hardware, about `0.55-0.85`: spend saved budget on richer detail normals, decals, wetness, glass/display response, stronger Aegir/cloud material response, longer LOD residency, and denser near-field route dressing after measured proof.
- Ultra hardware, about `0.85-1.0`: extend visual overkill through richer trim, bevel density, layered atmosphere, stronger celestial texture response, higher route dressing density, and longer HLOD residency. Gameplay truth, prefab identity, DTO layout, save identity, collider authority, and material channel semantics stay unchanged.

## Future Owner Minimum Artifact Chain

1. Run the three validators with log files under `Docs/Reports/AssetSystem_20260605/`.
2. Synthesize findings with exact command lines, timestamps, editor version, and open errors.
3. For any source cleanup, perform Unity-safe scoped edits only after process gate clears.
4. Read back prefab assets and live `02_HECTON_WORLD` scene instances.
5. Capture bright surface/photic screenshots, Frame Debugger, Stats, profiler, memory, and GC evidence before any visual route claim.

Final status: `PENDING_VERIFICATION`.
