# 1893 Product Face Actual Material Assignment Matrix

Agent: 1893
Mode: REPORT_ONLY_STATIC_PREFAB_YAML_MATERIAL_AUDIT
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/import/AssetDatabase/PlayMode/profiler/screenshots/DataMonolith: NOT RUN

## Scope

This packet maps current renderer material GUID assignments from the exact product-face prefab scope named by the task. It resolves GUIDs only through static `.mat.meta` files under `Assets`, `Packages`, and visible `.codexbuild` package/default paths. It does not edit prefabs, materials, assets, source, scenes, binaries, `.meta`, generated meshes, task files, or sibling outputs.

Scanned prefabs: 42.
CSV rows: 61 material assignments.
Unresolved GUID rows: 0.
Default/package Lit.mat GUID rows: 17.
Blocked rows: 55. Hidden-input pending-proof rows: 3. Partial/channel-pending rows: 3.

Owned matrix:

- `Docs/Reports/Batch18/1893_PRODUCT_FACE_ACTUAL_MATERIAL_ASSIGNMENT_MATRIX.csv`

## Inputs Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `tools.md`, `inventory.md`, `player.md`, `world.md`, `celestial.md`
- `ocean.md`: missing at root during static presence check; no replacement policy was invented from that missing file.
- Batch18 reports `1867`, `1880`, `1881`, `1882`, `1883`, `1885`, `1887`, `1888`, `1889`, and present `1890`.
- Mandates: `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`, `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`.

## Static Classification Rules Used

- `31321ba15b8f8eb4c954353edc038b1d` resolves to `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat` and is rejected for product-face materials.
- `Mat_Tool_*_Placeholder` rows are rejected. 1880 blocks tool placeholder routes and package `Lit.mat` for tool bodies.
- `Mat_Resource_*` rows are blocked as flat resource material shells. 1881 says the current resource materials are flat color with empty texture slots and missing source packages.
- `MAT_PlayerSwimBlockout` is rejected as player body blockout. 1882 marks it as non-product material source.
- `Mat_Visor_Glass` is only partial static visor source. It is route-locked to player visor work and still needs missing scratch/fingerprint/grime plus mesh proof.
- `MAT_SargassumOilFilm`, `MAT_SargassumWaveDamping`, and `MAT_SargassumFoamDamping` are hidden-input candidates only on `Ocean_Crest.prefab`; runtime hidden-state and Frame Debugger proof remain required.
- `Mat_HectonSky` is route-locked to `Sky_System.prefab` but not accepted as current prefab-source closure. Normal surface/sky proof cannot be replaced by storm/noir/depth concealment.
- `MMBPR_RedSquares` is a third-party prototype/checker material on `Buildings/Cube.prefab` and is rejected as a product-face construction material source.
- No current scoped row assigns `Noir`, `Storm`, `Depth`, terrain, Crest ocean package, flora, or other environment donor material to tool/resource/transport/player body prefabs. Environment route assets found here remain route-locked to sky/ocean/visor contexts only.

## Status Counts

| status | rows |
|---|---:|
| BLOCKED_TOOL_PLACEHOLDER | 23 |
| BLOCKED_DEFAULT_PACKAGE_LIT | 17 |
| BLOCKED_RESOURCE_FLAT_COLOR_SOURCE | 8 |
| BLOCKED_PLAYER_BLOCKOUT | 6 |
| HIDDEN_INPUT_CANDIDATE_PENDING_PROOF | 3 |
| BLOCKED_THIRD_PARTY_PROTOTYPE_CHECKER | 1 |
| PARTIAL_STATIC_VISOR_MATERIAL_PENDING_MESH_RELINK | 1 |
| PENDING_NON_BODY_HUD_CHANNEL_PROOF | 1 |
| PENDING_SKY_PREFAB_SOURCE_RELINK | 1 |

## Top Material GUID Concentrations

| rows | guid | resolved material |
|---:|---|---|
| 17 | `31321ba15b8f8eb4c954353edc038b1d` | `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat` |
| 6 | `3c9d5ec2c203c77409d0cb6d22c962a1` | `Assets/_Project/Art/Materials/Gameplay/MAT_PlayerSwimBlockout.mat` |
| 2 | `042f253a051192d4591fc921c3b2aa15` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_HarpoonLauncher_Placeholder.mat` |
| 2 | `05a9d9b8cef38164fb1ba430fbf720e0` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_EnvAnalyzer_Placeholder.mat` |
| 2 | `0ee55981801d7084bba89b587ed1d8a3` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_BeaconDeployer_Placeholder.mat` |
| 2 | `21bb5814f706ec140ab043a48c6da7b5` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_LaserCutter_Placeholder.mat` |
| 2 | `36a877ee51b6eda4495e1c2df87febff` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_SalvageSampler_Placeholder.mat` |
| 2 | `380476af342d2f44494e9cdf99c9a17b` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_Builder_Placeholder.mat` |
| 2 | `4cc87a4c07f367746aea7d51485bf446` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_StunPistol_Placeholder.mat` |
| 2 | `4cef51cfa995df9418eae9934fb47c59` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_Flashlight_Placeholder.mat` |
| 2 | `6cf124a1a4752624eb5b25ce2ddec7bc` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_Knife_Placeholder.mat` |
| 2 | `7a5fa1d5af698a54e9a904ac3f5cfd70` | `Assets/_Project/Art/Materials/Tools/Mat_Tool_Scanner_Placeholder.mat` |

## Exact Assignment Examples

- `Assets/_Project/Prefabs/Player.prefab:558 - {fileID: 2100000, guid: 31321ba15b8f8eb4c954353edc038b1d, type: 2}` -> `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat` -> `BLOCKED_DEFAULT_PACKAGE_LIT`.
- `Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab:211 - {fileID: 2100000, guid: 31321ba15b8f8eb4c954353edc038b1d, type: 2}` -> `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat` -> `BLOCKED_DEFAULT_PACKAGE_LIT`.
- `Assets/_Project/Prefabs/Ocean_Crest.prefab:70 - {fileID: 2100000, guid: 9ea2a31a068e95944bc77c3fe396c4f6, type: 2}` -> `Assets/_Project/Art/Materials/MAT_SargassumOilFilm.mat` -> `HIDDEN_INPUT_CANDIDATE_PENDING_PROOF`.
- `Assets/_Project/Prefabs/Sky_System.prefab:69 - {fileID: 2100000, guid: c94a1beef2372b8458941c2ed9d05d5e, type: 2}` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat` -> `PENDING_SKY_PREFAB_SOURCE_RELINK`.
- `Assets/_Project/Prefabs/Buildings/Cube.prefab:69 - {fileID: 2100000, guid: 1a9e66cbf3543a44cb2cf068585f07d4, type: 2}` -> `Assets/Feel/MMTools/Tools/MMPrototypeTextures/MMBRP_Materials/MMBPR_RedSquares.mat` -> `BLOCKED_THIRD_PARTY_PROTOTYPE_CHECKER`.
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab:94 - {fileID: 2100000, guid: ae507c2ce65eefc42b52651c83be0b33, type: 2}` -> `Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat` -> `BLOCKED_RESOURCE_FLAT_COLOR_SOURCE`.
- `Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab:187 - {fileID: 2100000, guid: 7a5fa1d5af698a54e9a904ac3f5cfd70, type: 2}` -> `Assets/_Project/Art/Materials/Tools/Mat_Tool_Scanner_Placeholder.mat` -> `BLOCKED_TOOL_PLACEHOLDER`.
- `Assets/_Project/Prefabs/Item_Titanium.prefab:73 - {fileID: 2100000, guid: 31321ba15b8f8eb4c954353edc038b1d, type: 2}` -> `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat` -> `BLOCKED_DEFAULT_PACKAGE_LIT`.

## Current Risk Summary

Most dangerous assignment: package-cache URP `Lit.mat` through GUID `31321ba15b8f8eb4c954353edc038b1d`. It appears on player body/blockout surfaces, transports, `Tool_Propulsion_Held`, `Item_Titanium`, and `STRUCTURES`. This is worse than a single missing art source because one default route contaminates several product-face families and can falsely look like a material assignment while carrying no owned source contract.

Visible primitive material debt remains blocked for player, tools, resources, transports, legacy titanium, structures, and `Buildings/Cube`. Hidden-input treatment is only recorded for the three Sargassum carrier materials in `Ocean_Crest.prefab`; no broad validator bypass is implied.

## Future Relink Priorities

1. Eradicate `31321ba15b8f8eb4c954353edc038b1d` from retained product-face prefabs: player, transports, `Tool_Propulsion_Held`, `Item_Titanium`, and `STRUCTURES`. Replace only with project-owned `MAT_*` role packages.
2. Replace `MAT_PlayerSwimBlockout` and default player body slots with suit/body/visor material packages while preserving `HandAnchor`, camera/HUD, swim attachments, and collider truth.
3. Relink held/world tool families together. Placeholder tool materials must become tool-specific casing/rubber/glass/label/emission packages under the `Hecton_ToolDecayLit` or declared shader contract.
4. Replace resource `Mat_Resource_*` flat shells with resource-specific albedo/normal/packed mask material sources. Preserve `Data_Copper` and `Data_TitaniumScrap` truth.
5. Resolve legacy `Item_Titanium`, `STRUCTURES`, and `Buildings/Cube` through a future Unity owner. No deletion or quarantine is authorized from this static matrix.
6. Prove `Ocean_Crest` Sargassum inputs are hidden data carriers before visible frames, or remove/relink the primitive carriers. Prove `Sky_System` prefab source cleanup separately from scene override evidence.

## Low / Middle / High / Ultra Consequences

- Low/compact: no ugly fallback. Product-face prefabs still need owned material identity, correct albedo/normal/packed-mask semantics, readable silhouettes, and no default/package/placeholder/checker material route.
- Middle: each family needs distinct material source packages, wetness/wear semantics where physical, and correct channel contracts before relink.
- High: richer normals, decals, scratches, grime, labels, glass/runoff, wetness, and longer near-field material residency can be added inside the same declared contracts.
- Ultra: visual overkill can add micro chips, bolts, secondary fittings, dense labels, stronger masks, and richer sky/ocean/visor response. It must not change gameplay truth, item IDs, collider identity, save identity, DTO layout, anchors, or authority routes.

## Evidence Boundary

This is static YAML/meta evidence only. It proves current serialized material GUID text and static `.mat.meta` resolution. It does not prove Unity import health, scene overrides, active renderer visibility, GameView material loading, SRP Batcher state, runtime hidden-state, visual quality, profiler cost, GC, memory, screenshots, or player acceptance.

## Verification

Commands run:

```powershell
git diff --check -- Docs/Reports/Batch18/1893_PRODUCT_FACE_ACTUAL_MATERIAL_ASSIGNMENT_MATRIX.md Docs/Reports/Batch18/1893_PRODUCT_FACE_ACTUAL_MATERIAL_ASSIGNMENT_MATRIX.csv Docs/Tasks/Status_1893.md Docs/AgentLogs/Rationale_1893.md Docs/AgentLogs/LOG_1893.md
```

Result: PASS, no output.

```powershell
Import-Csv Docs/Reports/Batch18/1893_PRODUCT_FACE_ACTUAL_MATERIAL_ASSIGNMENT_MATRIX.csv | Measure-Object
```

Result: PASS, `Count: 61`.

Static term cross-check across owned report/CSV/status/rationale/log:

| term | hits | result |
|---|---:|---|
| `31321ba15b8f8eb4c954353edc038b1d` | 26 | PASS |
| `PackageCache` | 25 | PASS |
| `Placeholder` | 43 | PASS |
| `MAT_PlayerSwimBlockout` | 13 | PASS |
| `Sky_System` | 7 | PASS |
| `Ocean_Crest` | 11 | PASS |
| `Tool_Propulsion` | 8 | PASS |
| `Item_Titanium` | 8 | PASS |

Final state: STATIC MATRIX COMPLETE. Runtime, Unity, import, visual, Frame Debugger, profiler, GC, and material-loading proof were not run and are not claimed.
