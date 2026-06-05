# Texture Material Usage Review - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE` / `STATIC_DOC` only.
Scope: static GUID/path scan of `.mat`, `.prefab`, and `.unity` files under `Assets/_Project`; GUID resolution used all `Assets/**/*.meta` only to map references found by the `_Project` scan.
First-20 route moment: addresses static asset binding confusion for bright surface exit, Aegir/sky, shoreline foam, photic shallows, and medium-depth hero route candidates.

No Unity run. No import. No build. No prefab/material/scene mutation. No visual/runtime/Addressables/VRAM readiness claimed.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit`: static text/GUID hits are not runtime proof.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`: heavy texture reachability is not residency proof; unresolved Addressables ownership remains blocked.
- `REND_URP_Graphics_HotPath_Optimization_HLOD`: material/texture use must preserve visual floor and stay variant/VRAM disciplined.
- `3DMODEL_TEXTURES_MATERIALS.md`: texture roles, PBR channel roles, import settings, and generated-source limits govern disposition.
- `rendering.md`: surface, sky, Aegir, ocean, coastline, and photic shallows require bright readable premium material proof; static links do not satisfy it.

## Scan Inputs

- Scanned files under `Assets/_Project`: `1056` total (`.mat`, `.prefab`, `.unity`).
- Unity GUID refs found in scanned files: `2527` unique.
- Asset GUIDs resolved from `Assets/**/*.meta`: `14041` total.
- Resolved texture assets in `Assets`: `1085`.
- Resolved material assets in `Assets`: `1112`.
- Usage map rows written: `141`.
- Unresolved scanned GUIDs after `Assets/**/*.meta` map: `62` unique. These are package/built-in/missing/deleted until Unity readback or broader package GUID mapping proves otherwise.
- Unresolved GUID occurrences inside `_Project` material files: `596`.

Output CSV: `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_MAP_20260605.csv`.

## Active Scene Boundary

`ProjectSettings/EditorBuildSettings.asset` static enabled scenes:
- `Assets/_Project/Scenes/00_BOOTSTRAP.unity` - documented main handoff.
- `Assets/_Project/Scenes/01_MAIN_MENU.unity` - documented main handoff.
- `Assets/_Project/Scenes/01_ORBIT.unity` - enabled but root doctrine says not main handoff / needs owner clarification.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` - documented main handoff.

Static conflict: root doctrine says `01_ORBIT` exists but is not the main handoff, while `EditorBuildSettings.asset` currently has it enabled. Usage rows therefore expose both `active_build_scene_users` and `documented_main_handoff_scene_users`. Runtime route truth remains `PENDING VERIFICATION`.

Enabled scene material refs from static YAML:
- `Assets/_Project/Scenes/00_BOOTSTRAP.unity`: `6` resolved material refs.
- `Assets/_Project/Scenes/01_MAIN_MENU.unity`: `5` resolved material refs.
- `Assets/_Project/Scenes/01_ORBIT.unity`: `4` resolved material refs.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`: `139` resolved material refs; suspect proxy/placeholder materials: 4.

## Priority Texture Findings

| Target | Rows | Referenced Rows | Active Build Scene Rows | Static Finding |
|---|---:|---:|---:|---|
| `clouds0_diff` | 3 | 3 | 2 | Sky/Aegir source is materially reachable; shader-slot and visual acceptance still require Unity readback/captures. |
| `TX_H8AegirGasGiantBakedDisc_1428` | 1 | 1 | 1 | Sky/Aegir source is materially reachable; shader-slot and visual acceptance still require Unity readback/captures. |
| `Aegir_storms` | 2 | 1 | 1 | Sky/Aegir source is materially reachable; shader-slot and visual acceptance still require Unity readback/captures. |
| `oblaka!` | 1 | 1 | 1 | Sky/Aegir source is materially reachable; shader-slot and visual acceptance still require Unity readback/captures. |
| `foam.png` | 2 | 2 | 2 | `foam.png` is referenced by route materials and active world/ocean users, but disposition remains rejected visible support. |
| `wet basalt / shell / sand` | 48 | 7 | 7 | Imported basalt/sand textures are active through terrain/world materials; generated Docs sources have no GUID and remain source-only. |
| `WorldProceduralFlora imported stacks` | 44 | 36 | 28 | Imported stacks are materially reachable through flora/coral materials and baked prefab candidates; proxy material contamination reaches `02_HECTON_WORLD`. |

Representative active priority rows:
- `Assets/Crest/Crest/Textures/foam.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat[_BaseMap].
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png` -> `Assets/_Project/Scenes/01_ORBIT.unity;Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat[_StormTex]; Assets/_Project/Art/Materials/Mat_GasGiant.mat[_EmissionTex]; Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat[_AegirBandTex].
- `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/Mat_GasGiant.mat[_CelestialOcclusionTex]; Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat[_MainCloudTex]; Assets/_Project/Art/Materials/World/MAT_H8SurfaceCloudDeck_1428.mat[_BaseMap].
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat[_BaseMap|_MainTex].
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Khepri.mat[_BaseMap|_MainTex]; Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Nammu.mat[_BaseMap|_MainTex]; Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat[_BaseMap|_MainTex]; ... (+9).
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/Mat_Terrain.mat[_FlowNormal]; Assets/_Project/Art/Materials/World/MAT_H8SurfaceWetBasaltReal_1428.mat[_BumpMap]; Assets/_Project/Art/Materials/World/MAT_H8TerrainLit_BasaltSediment_1428.mat[_Normal0]; ... (+11).
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticPorousBasalt_1428.mat[_BaseMap|_MainTex]; Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticReefBasaltSand_1435.mat[_BaseMap|_MainTex]; Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroWetBasaltRock_1453.mat[_BaseMap|_MainTex]; ... (+6).
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_Color.jpg` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceLittoralShelf_1430.mat[_BaseMap|_MainTex].
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_NormalGL.jpg` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceLittoralShelf_1430.mat[_BumpMap].
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/Mat_Terrain.mat[_SandTex]; Assets/_Project/Art/Materials/World/MAT_H8TerrainLit_BasaltSediment_1428.mat[_Splat3].
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/NORMAL.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/MAT_H8TerrainLit_BasaltSediment_1428.mat[_Normal3]; Assets/_Project/_Archive/Mat_Ocean.mat[_NormalMap].
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/albedo___family.coral.branching.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCoralBranching_1428.mat[_BaseMap]; Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBranching_1457.mat[_BaseMap]; Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat[_BaseMap].
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/detail___family.coral.branching.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCoralBranching_1428.mat[_DetailMap]; Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBranching_1457.mat[_DetailMap]; Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat[_DetailMap].
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/mask___family.coral.branching.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCoralBranching_1428.mat[_MaskMap]; Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBranching_1457.mat[_MaskMap]; Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat[_MaskMap].
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/normal___family.coral.branching.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCoralBranching_1428.mat[_NormalMap]; Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBranching_1457.mat[_NormalMap]; Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat[_NormalMap].
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/albedo___family.coral.brittle.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBrittle_1457.mat[_BaseMap]; Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat[_BaseMap].
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/detail___family.coral.brittle.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBrittle_1457.mat[_DetailMap]; Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat[_DetailMap].
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle/mask___family.coral.brittle.png` -> `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBrittle_1457.mat[_MaskMap]; Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat[_MaskMap].
- Additional active priority rows: `24`; see CSV.

## Target Notes

- `Assets/_Project/Art/TEXTURES/clouds0_diff.png`: referenced by `MAT_AegirGasGiant_Impostor_1428` and `Mat_GasGiant`; active/static users include `02_HECTON_WORLD.unity` and GasGiant prefabs. `_PROLOGUE_CONTENT` duplicate cloud files are separate GUIDs; one is tied to `01_ORBIT`/prologue cloud prefab, another has no prefab/scene user.
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`: referenced by `MAT_H8SurfaceGasGiantDisc_1428`, active in `02_HECTON_WORLD.unity`. Static disposition remains prototype/not final.
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png`: referenced by `MAT_AegirGasGiant_Impostor_1428`, `MAT_AegirSky_Master`, and `Mat_GasGiant`; active/static users include `02_HECTON_WORLD.unity`, `01_ORBIT.unity`, and GasGiant prefabs. `_PROLOGUE_CONTENT` duplicate has no scanned user.
- `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`: referenced by `MAT_H8SurfaceCloudDeck_1428` in `02_HECTON_WORLD.unity`; also referenced by cloud/gas materials without scene users.
- `Assets/_Project/Art/TEXTURES/foam.png`: referenced by `MAT_H8SurfaceShoreFoam_1428`, multiple photic foam/caustic/shimmer materials, `MAT_SurfaceSplashFoamDirty_1428`, and `_Archive/Mat_Ocean.mat`; active/static users include `02_HECTON_WORLD.unity` and `Hecton Ocean.prefab`. Candidate disposition says `REJECTED_VISIBLE_SUPPORT_ONLY`; static reachability increases replacement priority.
- Imported wet basalt/sand textures: `Rock031_1K-JPG_Color`, `Rock031_1K-JPG_NormalGL`, `Ground079S_1K-PNG_Color`, `NORMAL.png`, and `Ground074` sand+green maps are materially reachable through terrain/world materials and active world scene/prefab refs. Generated wet basalt/shell/sand rows under `Docs/GeneratedAssets` have no Unity GUID and are not imported asset refs.
- `WorldProceduralFlora/Imported`: several 4-map stacks are reachable through material assets and baked flora prefabs. Four `WorldProceduralProxy` materials are directly referenced by `02_HECTON_WORLD.unity`, which is visible-route contamination until Unity owner replaces/proves route-safe material bindings.

## Proxy / Placeholder Contamination

Suspect material rule for this static pass: material path contains `WorldProceduralProxy`, `ProceduralPlaceholders`, `placeholder`, `proxy`, `RuntimeVisualProof`, `checker`, `debug`, or `prototype`. This is a text/path heuristic, not Unity runtime proof.

- Suspect materials with prefab/scene users: `88`.
- Suspect materials with active enabled-scene users: `4`.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat` -> active `Assets/_Project/Scenes/02_HECTON_WORLD.unity`; visible users `10`; texture stack: Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/mask___family.coral.branching.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/detail___family.coral.branching.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/normal___family.coral.branching.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/albedo___family.coral.branching.png.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat` -> active `Assets/_Project/Scenes/02_HECTON_WORLD.unity`; visible users `9`; texture stack: Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/mask___family.coral.massive.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/normal___family.coral.massive.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/detail___family.coral.massive.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/albedo___family.coral.massive.png.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat` -> active `Assets/_Project/Scenes/02_HECTON_WORLD.unity`; visible users `9`; texture stack: Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/mask___family.coral.plate.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/detail___family.coral.plate.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/normal___family.coral.plate.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/albedo___family.coral.plate.png.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat` -> active `Assets/_Project/Scenes/02_HECTON_WORLD.unity`; visible users `16`; texture stack: Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/normal___family.kelp.patch.dense.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/detail___family.kelp.patch.dense.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/mask___family.kelp.patch.dense.png; Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/albedo___family.kelp.patch.dense.png.

Other suspect materials with visible prefab candidate users but no active enabled scene user remain listed in the CSV by `proxy_placeholder_materials` and `visible_route_candidate_users`. They are candidate-pool contamination, not active route proof.

External material refs found from `_Project` prefabs/scenes:
- `Assets/Feel/MMTools/Tools/MMPrototypeTextures/MMBRP_Materials/MMBPR_RedSquares.mat` referenced by `1` scanned prefab/scene file(s). Texture internals were not scanned because source `.mat` is outside `Assets/_Project`.
- `Assets/Crest/Crest/Materials/Ocean-Underwater.mat` referenced by `1` scanned prefab/scene file(s). Texture internals were not scanned because source `.mat` is outside `Assets/_Project`.
- `Assets/Crest/Crest/Materials/Ocean.mat` referenced by `2` scanned prefab/scene file(s). Texture internals were not scanned because source `.mat` is outside `Assets/_Project`.

## Unknowns / Blockers

- GUID mapping is incomplete for package/built-in/deleted refs not resolved by `Assets/**/*.meta`; count is in Scan Inputs. Unity readback is required before deleting or rebinding anything.
- Generated wet basalt/shell/sand sources under `Docs/GeneratedAssets` are `STATIC_DOC`/source-only here. They have no Unity `.meta` GUID in `Assets`, so the `_Project` YAML scan cannot prove material usage.
- Material YAML slots prove serialized texture refs only. They do not prove shader effective property use, active renderer use, import quality, compression, streaming mip residency, SRP Batcher state, Addressables ownership, or visual quality.
- `EditorBuildSettings.asset` includes `01_ORBIT`; root route doctrine says it is not main handoff. Active-scene interpretation needs owner clarification or project setting proof before runtime claims.
- Proxy material path detection is conservative text evidence. It is enough to block promotion, not enough to prescribe YAML edits.

## Scalability Consequences

- Low/compact: imported priority textures need compressed/mipped material routes, not source-only/generated Docs refs. `foam.png` and proxy flora materials cannot be allowed to carry visible shallow/surface composition because flat support art fails the visual floor even if cheap.
- Middle: route materials should use cleaned PBR stacks with correct albedo/normal/MRAO roles and stable LOD/prefab bindings; static material reachability must be followed by Unity readback and scene captures.
- High: saved budget should buy richer Aegir/cloud detail, wet basalt breakup, better foam/contact masks, and denser near-field flora while preserving material identity.
- Ultra: extend LOD residency, richer material layering, stronger sky/ocean/celestial response, and visual overkill; do not change gameplay truth ownership or use unbounded residency.

## Regression Model

- CPU: no runtime code changed. Static scan only.
- GC: no runtime code changed. No 0 B/frame claim.
- Memory/VRAM: static source sizes and refs only; runtime residency and Addressables ownership remain unproven.
- Cadence: no runtime cadence changed.
- Correctness: reduces false promotion risk by separating active/static refs, documented handoff refs, visible candidate refs, and source-only rows.

Final status: `PENDING VERIFICATION`.
