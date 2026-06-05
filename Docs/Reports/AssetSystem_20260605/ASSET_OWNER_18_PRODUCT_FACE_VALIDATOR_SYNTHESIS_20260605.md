# Asset Owner 18 Product-Face Validator Synthesis - 2026-06-05

Status: `UNITY_EDITOR_SOURCE_GATE_FAILED / RUNTIME_AND_VISUAL_PROOF_PENDING`.
Evidence class: `UNITY_BATCHMODE_LOG`.
Runtime proof: absent.
Visual proof: absent.
Profiler/GC/memory proof: absent.

## Scope

This report summarizes read-only Unity batchmode execution of the product-face validator gates mapped by `taskslocal/asset_system_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_EVIDENCE_PACKET.md`.

No `Assets/`, `ProjectSettings/`, or `Packages/` changes were made by the validator pass according to post-run protected-scope status checks.

First-20 route blocker removed: none. This pass identifies source gates that block product-face promotion for the bright first exit, tool viewmodels, resource pickups, transport prefabs, sky, Aegir/ocean source, and photic route proof.

## Commands

```text
Unity.exe -batchmode -projectPath C:\hades\Hecton8 -quit -executeMethod Hecton8.EditorTools.ProductFaceMaterialTextureValidator.ValidateFromMenu -logFile Docs\Reports\AssetSystem_20260605\ASSET_OWNER_18_material_texture_validator_20260605_130615.log
Unity.exe -batchmode -projectPath C:\hades\Hecton8 -quit -executeMethod Hecton8.EditorTools.ProductFacePrefabQualityValidator.ValidateFromMenu -logFile Docs\Reports\AssetSystem_20260605\ASSET_OWNER_18_prefab_quality_validator_20260605_130822.log
Unity.exe -batchmode -projectPath C:\hades\Hecton8 -quit -executeMethod Hecton8.EditorTools.ProductFaceSkyOceanSourceValidator.ValidateFromMenu -logFile Docs\Reports\AssetSystem_20260605\ASSET_OWNER_18_sky_ocean_source_validator_20260605_130941.log
```

## Results

| Gate | Result | Evidence |
|---|---|---|
| Product-face material/texture gate | `FAILED` | `Prefabs=42`, `Materials=43`, `Failures=183`, `Warnings=4` |
| Product-face prefab quality gate | `FAILED` | `Checked=42`, `Errors=42` |
| Sky/ocean source primitive gate | `FAILED` | `CheckedPrefabs=2`, `Failures=2`, `Warnings=2` |

## Hard Failures

- Product-face tool, item, player, transport, construction, shell, sky/ocean/depth, and diagnostic material targets still include placeholder, blockout, package-default, forbidden route, missing texture role, or missing channel-semantics failures.
- `Assets/_Project/Prefabs/Player.prefab` contains product-face renderers using blockout material and URP package default `Lit.mat` routes.
- `Assets/_Project/Prefabs/Tools/Held/*` and `Assets/_Project/Prefabs/Items/Tools/*` use placeholder tool materials and/or built-in primitive mesh IDs.
- `Assets/_Project/Prefabs/Resources/Pickups/*` and `Assets/_Project/Prefabs/Transport/*` use Unity built-in primitive mesh IDs.
- `Assets/_Project/Prefabs/Sky_System.prefab` has visible active sky dome primitive risk: `Sky_System/Sphere` uses Unity built-in primitive mesh `Sphere`.
- `Assets/_Project/Prefabs/Ocean_Crest.prefab` has micro-fauna primitive risk: `SargassumMicroFaunaBoids.boidMesh` points to Unity built-in primitive mesh `Plane`.

## Non-Fail Notes

- Crest hidden input primitives at exact source paths were accepted only as narrow data-input sources:
  - `Ocean_Crest/SargassumWaveDampingInput`
  - `Ocean_Crest/SargassumFoamDampingInput`
  - `Ocean_Crest/SargassumOilFilmInput`
- These accepted input exceptions are not visual proof and do not clear ocean surface, foam, waterline, or micro-fauna presentation.

## Rejection Decision

Product-face promotion is blocked.

Do not claim product-face visual readiness from the current player/tool/resource/transport/sky/ocean source assets. Static source gates prove the opposite: visible product-facing prefabs still contain placeholder material routes, package-default material routes, blockout material routes, missing PBR role slots, missing channel declarations, and built-in primitive mesh risks.

## Required Next Owners

1. Product-face material owner: replace placeholder/blockout/package-default material routes with route-owned material families that have albedo/base, normal/detail-normal, packed mask, and declared channel semantics.
2. Product-face prefab owner: replace built-in primitive visual mesh sources with authored/generated production meshes, LOD chains, collider proxies, and material proof.
3. Sky/Aegir owner: replace or prove source sky dome mesh route; live scene override is not accepted without Game View, Scene View, Frame Debugger, and console proof.
4. Ocean/Crest owner: replace `SargassumMicroFaunaBoids.boidMesh` primitive plane route with authored/generated mesh, VAT, or designed impostor proof; Crest input exceptions remain data-only.

## Regression Model

- CPU: no runtime code changed. Validator batchmode import/compile work is editor-only evidence.
- GC: no gameplay path changed. No `0 B/frame` claim.
- Memory/VRAM: validator logs do not prove residency. MX350 batchmode reported VRAM 1964 MB and app budget 1669 MB in the material validator log, but no runtime memory proof exists.
- Cadence: no runtime cadence changed.
- Correctness: source blockers are now Unity-log-backed; runtime, visual, scene instance, Addressables, Frame Debugger, profiler, and player-build status remain pending.

Final status: `PENDING VERIFICATION` for runtime/visual quality, `FAILED` for the three product-face source gates.
