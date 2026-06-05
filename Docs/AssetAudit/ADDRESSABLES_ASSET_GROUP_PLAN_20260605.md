# Addressables Asset Group Plan - 2026-06-05

Status: `STATIC PLAN / PENDING UNITY OWNER PROOF`.
Evidence class: `STATIC_DOC` / `STATIC_SOURCE` only.
Write scope: this markdown plus `ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`.
First-20 route moment: frames asset ownership risks for the bright first surface exit, Aegir/sky readability, shoreline/ocean contact, photic shallows, player audio loops, route UI, and candidate nature/geology pools.

No Unity run, import, build, catalog build, prefab mutation, material mutation, scene mutation, project setting mutation, or `Assets` edit was performed.

## Mandates Followed

- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`

## Static Finding

`Docs/Reports/AssetSystem_20260605/ADDRESSABLES_STATIC_COVERAGE_GAP_3218_20260605.md` confirms `Assets/AddressableAssetsData` exists but has recursive file count `0` and non-meta file count `0`. No static Addressables settings asset, group asset, profile asset, schema asset, catalog, or entry evidence exists for the asset classes below.

That means:

- no Addressables readiness claim is valid;
- no group/key/label is proven on disk;
- serialized material, prefab, scene, or MusicDirector refs are not residency proof;
- direct `AudioClip` refs are lifecycle blockers until owner exceptions or Addressables ownership are proved;
- this file is a future Unity-owner plan, not acceptance.

## Proposed Group Rows

The canonical row data is in `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`.

| Priority | Proposed group | Label | Source scope | Owner | Disposition |
|---|---|---|---|---|---|
| P0 | `group_audio_music` | `audio.music.musicdirector` | `Assets/_Project/Audio/Music for Game/*.ogg`; MusicDirector profiles | Audio/MusicDirector owner | `PROPOSED_BLOCKED_STATIC_NO_GROUP_PROOF` |
| P0 | `group_audio_amb_long` | `audio.ambient.long` | `Assets/_Project/Audio/Ambient/*`; `Assets/_Project/Audio/Atmos*.wav`; `Underwater Ambient.wav` | Audio/Ambient owner | `PROPOSED_BLOCKED_POLICY_CONFLICT` |
| P0 | `group_core_player_audio_loops` | `audio.player.loop` | `Assets/_Project/Audio/Breathing/*`; `Assets/_Project/Audio/Movement/swimming*`; direct `Player.prefab` refs | Player/audio lifecycle owner | `PROPOSED_BLOCKED_DIRECT_PREFAB_REFS` |
| P0 | `group_core_ui_sfx_short` | `audio.ui_sfx.short` | `Assets/_Project/Audio/UI/*`; footsteps; impacts; short movement/SFX clips | Audio UI/SFX owner | `PROPOSED_BLOCKED_OWNER_EXCEPTIONS` |
| P1 | `group_surface_sky_celestial` | `visual.surface.sky_aegir` | `Mat_HectonSky.mat`; Aegir/cloud/sky texture candidates | Sky/Celestial material owner | `PROPOSED_BLOCKED_READBACK` |
| P0 | `group_surface_water_contact` | `visual.surface.water_contact` | Crest foam sources; `MAT_SurfaceSplashFoamDirty_1428.mat`; `MAT_H8_CrestFoamInput_1464.mat` | Ocean/Crest material owner | `PROPOSED_BLOCKED_REJECTED_VISIBLE_SOURCE` |
| P1 | `group_photic_terrain_pbr` | `visual.photic.terrain_pbr` | wet basalt/sand/geology PBR candidates; `Mat_Terrain.mat`; photic terrain materials | Terrain material owner | `PROPOSED_BLOCKED_SOURCE_ONLY` |
| P0 | `group_photic_flora_materials` | `visual.photic.flora_materials` | `WorldProceduralFlora/Imported`; proxy coral/kelp material refs | Flora/material promotion owner | `PROPOSED_BLOCKED_PROXY_CONTAMINATION` |
| P1 | `group_photic_flora_prefabs` | `prefab.photic.flora_candidates` | `Assets/_Project/Prefabs/Nature/Flora/Baked`; BioForge shallows | Mesh/Prefab promotion owner | `PROPOSED_BLOCKED_VISUAL_LOD_READBACK` |
| P1 | `group_geology_prefabs` | `prefab.geology.procedural_finals` | `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals` | Mesh/Prefab promotion owner | `PROPOSED_BLOCKED_MATERIAL_ROUTE_PROOF` |
| P1 | `group_core_ui_sprites` | `ui.sprite.core_hud` | `Assets/_Project/Art/Sprites/ui/*`; `oxygen-tank.png`; HUD support masks | UI/HUD owner | `PROPOSED_BLOCKED_ATLAS_BINDING_PROOF` |

## Load And Release Rules

- Heavy texture, terrain, flora, geology, sky, water, audio, and HLOD groups use `AssetLoadMode.RequestedAssetAndDependencies` unless a Unity owner proves a bounded always-hot exception with Memory Profiler evidence.
- Music and long ambience are streamed or banked only through owned audio routes with handle/ref-count/release proof. Static MusicDirector profile refs do not prove Addressables ownership.
- Short UI/SFX clips may become core always-hot exceptions only after an owner records size, import settings, playback route, and release or lifetime policy.
- Player-critical loops are not generic SFX. Direct `Player.prefab` `AudioClip` refs remain blockers until every row has owner route, group/key or exception, and 0 B/frame playback proof.
- Water/foam/contact must not use rejected `foam.png` as visible final art. It may remain source/reference only until route-owned contact masks are authored and proved.
- Crest material work must use asset materials. No runtime Crest material clone, wrapper, or override is authorized by this plan.
- Proxy/placeholder prefab pools are not promoted by adding Addressables groups. `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` remain rejected for visible route placement.

## Proof Required Before Any Readiness Claim

- Addressables settings asset, groups, schemas, labels, profiles, and entries on disk.
- Stable key owner per asset class.
- Load mode proof, with `RequestedAssetAndDependencies` for heavy groups unless a measured exception exists.
- Loaded handle count, owner list, ref-count samples, and release ledger.
- Texture memory, mip residency, RAM, VRAM, and compact pressure proof.
- Unity Console/import/readback proof.
- Play Mode or player capture for load/release paths.
- Frame Debugger/Stats and screenshots for surface sky/Aegir, ocean contact, terrain, and photic flora.
- Runtime MusicDirector/mixer/DSP route proof for music, ambience, player loops, UI/SFX, and stingers.
- 0 B/frame proof for hot playback, UI, and runtime consumers.

## Low / Middle / High / Ultra Consequences

Low/compact:

- Proposed groups must protect the 1800 MB compact VRAM ceiling and 900 MB texture budget before visible surface/photic assets are called safe.
- No flat replacements. Sky, water, terrain silhouettes, UI sprites, and player loops need owned keys so residency can be controlled without degrading readability.
- Audio must cap active banks and avoid direct prefab lifecycle leaks.

Middle:

- Route-owned PBR terrain, contact foam masks, sky/Aegir candidates, flora materials, UI sprites, and audio banks can expand density only after groups and release proof exist.
- Middle-tier must look genuinely good; Addressables cannot become a reason to ship weak or unbound art.

High:

- Longer LOD/mip residency, richer detail normals, stronger water response, denser near-field flora, and broader MusicDirector variety require proven group/key ownership and pressure behavior.
- High-tier cost savings buy visual/audio richness, not new gameplay truth.

Ultra:

- Visual overkill may extend cloud depth, Aegir detail, wet terrain layering, contact foam richness, flora density, and audio spatial richness only through proven owner groups.
- Ultra must not bypass ref-count, release, memory, or Addressables proof.

## Regression Model

CPU:

- No runtime code changed. Future Unity owner work must prove load dispatch and release work stay within streaming budgets and do not introduce main-thread stalls.

GC:

- No runtime code changed. No 0 B/frame claim is made. Future hot playback/UI/asset consumers require GCMonitor or Profiler proof.

Memory/VRAM:

- Static ledgers show large source pools: 190 texture/source rows and 138 audio rows. Residency is unproven because no Addressables group/catalog evidence exists.
- Future owners must prove texture mip residency, audio memory, loaded handle counts, release behavior, and compact pressure response.

Cadence:

- No runtime cadence changed. Future groups need dispatch cadence, active bank limits, preload windows, release windows, and pressure load-shed proof.

Correctness:

- This plan reduces false promotion risk by separating source candidates, serialized refs, rejected visible sources, direct prefab refs, and missing Addressables ownership.
- Correctness remains blocked until Unity readback, runtime route proof, and visual/audio acceptance exist.

## Hard Rejection

- Do not claim Addressables readiness from this plan.
- Do not edit `Assets` from this planning task.
- Do not raw YAML patch `.mat`, `.prefab`, `.unity`, `.asset`, Addressables settings, or project settings.
- Do not launch Unity/build/import for this plan.
- Do not use direct prefab audio refs, proxy materials, rejected foam, or placeholder pools as acceptance evidence.

Final status: `PENDING UNITY OWNER PROOF`.
