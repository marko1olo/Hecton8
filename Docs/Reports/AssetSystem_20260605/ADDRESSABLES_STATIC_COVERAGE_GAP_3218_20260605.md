# Addressables Static Coverage Gap - Asset Worker 3218 - 2026-06-05

Status: `PENDING RUNTIME PROOF`.
Evidence boundary: `STATIC_SOURCE` / `STATIC_YAML_SCAN` only.
Write scope: report only. No asset edits, Unity run, dotnet run, import, prefab/material/scene mutation, catalog build, or project setting mutation was performed.

First-20 route moment: addresses static Addressables ambiguity for bright surface exit, Aegir/sky visibility, shoreline/ocean contact, photic shallows, player audio loops, and candidate prefab pools. It does not prove runtime residency.

Mandates followed:

- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `streaming.md`
- `AGENTS.md`

## Static Addressables Source State

| Source | Static finding | Evidence class | Residual risk |
|---|---|---|---|
| `Assets/AddressableAssetsData` | Directory exists, but recursive file count is `0`; non-meta file count is `0`. No settings asset, group asset, profile asset, schema asset, catalog, or entry YAML was found. | `STATIC_SOURCE` | Unity may be able to create settings later; current disk state has no static group/key evidence. |
| `ServerData` | Not present in scoped file-system check. | `STATIC_SOURCE` | No generated player catalog/bundle evidence. |
| `Library/com.unity.addressables` | Not present in scoped file-system check. | `STATIC_SOURCE` | No local Addressables cache evidence accepted as project source. |
| Editor/source helpers | `HectonTextureImportDictator`, `ItemCatalog`, and `ContentAuthorityBuildValidators` reference Addressables APIs and required groups/load modes. | `STATIC_SOURCE` | Code capability is not populated Addressables data, not a catalog, not runtime load/release proof. |
| `Docs/Audio/audio_asset_ledger.csv` | All `138` rows have `addressable_group=PENDING_ADDRESSABLES` and `addressable_key=PENDING_ADDRESSABLES`. | `STATIC_SOURCE` | Audio owner/key route is unassigned in the production-facing ledger. |

Static Addressables files do not prove loaded handle count, residency, release, VRAM, memory, or correctness. In this pass there were no project Addressables settings/group/catalog files to inspect, so every candidate below remains a coverage gap until a Unity owner creates/validates groups and runtime proof exists.

## Priority Coverage Table

| Priority category | Static Addressables evidence | Affected paths | Owner/key gap | Runtime proof still needed |
|---|---|---|---|---|
| Foam/contact | Not found. No Addressables settings/group/key entry exists for foam/contact assets. | `Assets/Crest/Crest/Textures/foam.png`; `Assets/Crest/Crest/Textures/Foam2.png`; `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat`; `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_CrestFoamInput_1464.mat`; `Assets/_Project/Scenes/02_HECTON_WORLD.unity` static refs. | No `group_shallow`, `group_ocean`, `group_surface_contact`, or equivalent key. `foam.png` is already rejected visible support in asset audit; replacement ownership is separate from Addressables coverage. | Unity readback of active Crest/ocean/foam material slots; handle ledger; loaded handle count; release ledger; texture/mip residency; VRAM estimate; surface/shoreline capture and Frame Debugger proof. |
| Sky/Aegir/cloud | Not found. No Addressables settings/group/key entry exists for sky, Aegir, cloud, skybox, or celestial candidate textures/materials. | `Assets/_Project/Art/Materials/Mat_HectonSky.mat`; `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`; `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`; `Assets/_Project/Art/Materials/World/MAT_H8SurfaceCloudDeck_1428.mat`; `Assets/_Project/Art/TEXTURES/clouds0_diff.png`; `Assets/_Project/Art/TEXTURES/Aegir_storms.png`; `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`; `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`. | No `group_core`, `group_surface_sky`, `group_celestial`, or stable key evidence. Missing/static-stale sky material GUIDs remain readback blockers, not Addressables proof. | Unity readback of scene skybox and renderer material refs; Addressables group/key assignment; load mode proof; handle/ref-count proof; memory/VRAM proof; surface/Aegir/moon/cloud screenshot proof. |
| Terrain wet basalt/sand | Not found. No Addressables settings/group/key entry exists for terrain PBR stacks or terrain material route candidates. | `Assets/_Project/Art/Materials/Mat_Terrain.mat`; `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroWetBasaltRock_1453.mat`; `Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_AuthoredWetBasaltBreakup_1465.mat`; `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg`; `Rock031_1K-JPG_NormalGL.jpg`; `TX_H8_WetBasaltShoreline_Albedo_1428.png`; `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png`; `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_Color.jpg`. | No `group_shallow`, `group_terrain`, `group_photic_terrain`, or stable key evidence. Generated wet basalt/shell/sand sources under `Docs/GeneratedAssets` are source-only and have no Unity asset GUID route. | Terrain receiver readback; cleaned PBR import proof; group/key ownership; loaded terrain texture handle count; mip residency; VRAM ledger; photic terrain visual capture; release behavior under pressure. |
| Flora/proxy materials | Not found. No Addressables settings/group/key entry exists for imported flora stacks, proxy material refs, or baked flora candidates. | `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/*`; `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`; `MAT_family_coral_massive.mat`; `MAT_family_coral_plate.mat`; `MAT_family_kelp_patch_dense.mat`; `Assets/_Project/Scenes/02_HECTON_WORLD.unity`; `Assets/_Project/Prefabs/Nature/Flora/Baked`. | No `group_flora`, `group_photic_flora`, `group_shallow`, or stable key evidence. Four `WorldProceduralProxy` materials remain active-scene static contamination until Unity owner replaces or proves route-safe material bindings. | Renderer/material readback; visible route object list; Addressables group/key assignment; LOD/material binding proof; handle/ref-count/release proof; mip/VRAM proof; photic flora screenshot and Frame Debugger proof. |
| Audio - music | Not found. Ledger has `84` music rows, all `PENDING_ADDRESSABLES` for group and key. | `Assets/_Project/Audio/Music for Game/*.ogg`; MusicDirector profile assets referenced in asset index. | No `group_audio_music` or stable cue key coverage. MusicDirector profile refs are serialized route evidence, not Addressables ownership/release proof. | Runtime MusicDirector load path; handle count; release ledger; active bank count; memory/residency proof; mixer routing proof; cadence/listening proof. |
| Audio - ambient | Not found. Ledger has `12` ambient rows, all `PENDING_ADDRESSABLES` for group and key. | `Assets/_Project/Audio/Ambient/spaceship sounds - ambient.mp3`; `Assets/_Project/Audio/Atmos *.wav`; `Assets/_Project/Audio/Underwater Ambient.wav`. | No `group_audio_amb` or stable ambient bank key coverage. Import/load policy conflict remains unresolved in docs. | Owner route; load-type decision; active bank budget; handle/ref-count/release proof; audio memory proof; first-exit/shallow mix proof. |
| Audio - player_loop | Not found. Ledger has `5` player_loop rows, all `PENDING_ADDRESSABLES` for group and key. | `Assets/_Project/Audio/Breathing/*`; `Assets/_Project/Audio/Movement/swimming - underwater.ogg`; `Assets/_Project/Audio/Movement/swimming -onwater.wav`; direct `Player.prefab` refs from remediation matrix. | No `group_audio_player`, `group_core_player_audio`, or stable loop key coverage. Direct prefab refs remain lifecycle blockers unless owner documents an exception. | Player prefab readback; owner/load/release/playback route; 0 B/frame playback proof; DSP/mixer route; handle/release proof for long loops. |
| Audio - UI | Not found. Ledger has `5` UI rows, all `PENDING_ADDRESSABLES` for group and key. | `Assets/_Project/Audio/UI/*.wav`; `*.mp3`; `*.flac`. | No `group_core_ui_audio` or stable UI cue key coverage. Short UI clips may be core/static exceptions, but no owner exception is recorded in Addressables evidence. | UI audio owner route; import/load decision; playback proof; no string-event route; 0 B/frame proof; release/always-hot exception ledger. |
| Audio - SFX | Not found. Ledger has `30` SFX rows, all `PENDING_ADDRESSABLES` for group and key. | `Assets/_Project/Audio/Footsteps/*`; `Assets/_Project/Audio/Impact/*`; `Assets/_Project/Audio/Movement/dive_splash.wav`; `Assets/_Project/Audio/SFX/*`; `Assets/_Project/Audio/Thruster/*`. | No `group_audio_sfx`, `group_player_sfx`, or stable cue key coverage. Several rows are direct `Player.prefab` refs in remediation matrix. | Prefab readback; cue owner route; pooled/DSP playback route; handle/ref-count/release proof or documented core exception; memory proof. |
| Audio - voice | Not found. Ledger has `2` voice rows, all `PENDING_ADDRESSABLES` for group and key. | `Assets/_Project/Audio/VO/Stubs/VOStub_Chen_Log01_EN.wav`; `VOStub_Chen_Log01_RU.wav`; `AudioLog_chen_m_datapad_01.asset` per asset index. | No `group_audio_voice`, localization voice-bank key, or placeholder disposition key coverage. VO is placeholder only. | Localization/VO owner route; placeholder replacement/exception; Addressables key per locale; load/release proof; memory proof; subtitle/audio sync proof. |
| Prefab candidate pools | Not found. No Addressables settings/group/key entry exists for candidate prefab pools. | `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`; `Assets/_Project/Prefabs/Nature/Flora/Baked`; `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows`; rejected/blocked pools: `Assets/_Project/Prefabs/WorldProceduralProxy`, `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`, `Assets/_Project/Prefabs/Construction/Final`. | No `group_world_prefab_*`, `group_shallow_prefabs`, `group_geology`, `group_flora`, or stable prefab key evidence. `ItemCatalog` contains editor helper code that can create world prefab groups, but current `Assets/AddressableAssetsData` has no populated data. | Unity prefab readback; LOD/collider/material proof; Addressables group/key assignment; async instantiate/load proof; release ledger; pool lifecycle proof; visual route capture. |

## Static Evidence Commands

No-mutation commands used:

```powershell
rg --files Assets/AddressableAssetsData
rg -n "m_Name:|m_GroupName:|m_Address:|m_GUID:|guid:|Addressable|AssetLoadMode|Requested|AllPacked|Bundle|Groups|entries|m_Entries" Assets/AddressableAssetsData
Get-ChildItem -LiteralPath Assets\AddressableAssetsData -Recurse -File -Force
Import-Csv -LiteralPath Docs\Audio\audio_asset_ledger.csv | Group-Object class
Import-Csv -LiteralPath Docs\Audio\audio_asset_ledger.csv | Group-Object addressable_group
Import-Csv -LiteralPath Docs\Audio\audio_asset_ledger.csv | Group-Object addressable_key
rg -n "AddressableAssetSettingsDefaultObject.GetSettings\(true\)|FindGroup\(|CreateGroup\(|CreateOrMoveEntry\(|SetAddress\(|m_Address|m_GUID|AddressableAssetGroup" Assets\_Project\Scripts\Editor Assets\_Project\Scripts\ItemCatalog.cs Assets\_Project\Scripts\Core\Content\Editor
```

## Rejection Boundary

- Do not claim Addressables readiness from package presence, editor helper code, serialized material refs, direct prefab refs, or audio profile refs.
- Do not invent group/key names in data without Unity owner assignment and settings asset proof.
- Do not treat static material/scene reachability as residency proof.
- Do not treat direct prefab `AudioClip` refs as valid streaming ownership.
- Do not promote `WorldProceduralProxy` or placeholder prefab pools because Addressables are absent; lack of Addressables is an additional blocker, not acceptance.

## Runtime Proof Required Before Any Readiness Claim

- Addressables settings asset, groups, schemas, labels, and entries on disk.
- Stable key owner per asset class.
- `AssetLoadMode.RequestedAssetAndDependencies` proof for heavy texture/audio/terrain/HLOD groups unless a measured exception exists.
- Loaded handle count and owner list.
- Ref-count and release ledger sample.
- RAM and VRAM estimate, including texture mip residency where relevant.
- Pressure behavior under compact budget.
- Unity Console/import/readback proof.
- Play Mode or player capture for actual load/release paths.
- Visual proof for surface, sky/Aegir, ocean/foam, terrain, and photic flora candidates.

## Scalability Consequences

- Low/compact: no static Addressables coverage means heavy surface/photic assets cannot be claimed pressure-safe. Player survival UI/audio, sky/ocean readability, and return-route silhouettes need owner keys before residency can be disciplined.
- Middle: route-owned PBR, foam/contact, terrain, flora, and audio banks need populated groups plus load/release proof before density or mix breadth can be expanded.
- High: richer Aegir/cloud detail, water response, wet basalt breakup, and near-field flora require valid key ownership and residency proof before longer LOD/mip residency is allowed.
- Ultra: visual overkill can only extend from proven group/key/residency routes. It must not bypass owner keys, release ledgers, or memory proof.

## Regression Model

- CPU: no runtime code changed. Editor helper source was read only.
- GC: no runtime code changed. No 0 B/frame claim.
- Memory/VRAM: no Addressables/project catalog evidence exists for candidate residency. Memory and VRAM remain unproven.
- Cadence: no load/release cadence was run or changed.
- Correctness: this report prevents false promotion of asset-front candidates by separating serialized/source reachability from missing Addressables owner/key evidence.

Final status: `PENDING RUNTIME PROOF`.
