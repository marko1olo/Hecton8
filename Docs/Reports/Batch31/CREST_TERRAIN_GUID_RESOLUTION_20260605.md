# Crest / Terrain GUID Resolution - 2026-06-05

Status: STATIC INTEGRATION / NO UNITY ACCEPTANCE

Evidence class: `STATIC_SOURCE`, `SUBAGENT_STATIC_REPORT`.

Mandates followed:

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

## Verdict

The unresolved Crest material GUIDs are replicated in canonical Crest materials, not only in first-party `MAT_H8_SurfaceCrestOcean_1428.mat`.

Treat the Crest wave-data slots as runtime/stale serialized data until Unity/Crest readback proves otherwise. Do not replace those slots with artist textures by text edit.

`terrain.mat` and `Mat_TriplanarRock.mat` are stale and should not be used as valid wet basalt/geology route materials.

## Crest Blockers

| File / Slot | Current GUID | Status | Safe Action |
|---|---|---|---|
| `MAT_H8_SurfaceCrestOcean_1428.mat` `_MainTex` | `33331381cbc5c564583cd5e47314cf78` | missing `.meta`, also in canonical Crest materials | Unity owner inspects Crest import state; do not bind artist texture by text. |
| `MAT_H8_SurfaceCrestOcean_1428.mat` `_Skybox` | `f9a8c5bb065e21748a23f214a1f3a250` | missing `.meta`, also in canonical Crest materials | Resolve in Unity only; likely stale optional cubemap override if procedural sky remains active. |
| `MAT_H8_SurfaceCrestOcean_1428.mat` `_WD_Sampler_0`, `_WD_Sampler_Hi`, `_WD_Tex_Hi`, `_WaveDataTex` | `33331381cbc5c564583cd5e47314cf78` | missing `.meta`, Crest wave-data route | Runtime-populated/stale sampler. Do not replace with artist texture. |
| `MAT_H8_SurfaceCrestOcean_1428.mat` `_WD_Sampler_1`, `_WD_Sampler_Lo` | `ba628b5ad7a570e4b95c3ee64a5c605d` | missing `.meta`, Crest wave-data route | Unity/Crest owner clears or confirms runtime binding. |
| `MAT_H8_SurfaceCrestOcean_1428.mat` `_WD_Sampler_2` | `6b165028befdf0745b04ebdfbf672681` | missing `.meta`, Crest wave-data route | Same. |
| `MAT_H8_SurfaceCrestOcean_1428.mat` `_WD_Sampler_3` | `e94a5d7132329854281515fe36afb70e` | missing `.meta`, Crest wave-data route | Same. |

Valid Crest references:

- `Assets/Crest/Crest/Shaders/Ocean.shader.meta` owns shader GUID `986f7c6732e8a6e4881407d7f15f25c3`.
- Crest shader meta lists valid defaults for `_Normals`, `_FoamTexture`, and `_CausticsTexture`.
- `Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png.meta` owns canonical normal GUID `800e061692ff7a84e887f439d3364410`.
- First-party ocean material overrides normal with `Assets/_Project/Art/TEXTURES/TX_H8_SurfaceWaterNormals_1428.asset`.

## Terrain / Geology Blockers

| File / Slot | Current GUID | Status | Safe Action |
|---|---|---|---|
| `Assets/_Project/Art/Materials/terrain.mat` shader | `58f9232bdfcb5064f9b47d1dddb46260` | missing `.meta`; stale/old route | Replace assignment through Unity owner with valid first-party terrain material. |
| `terrain.mat` `_BaseMap`, `_MainTex`, `_Rock_Albedo` | `47f0a231c050423488e0ff6f7d66f813` | missing `.meta` | Do not reuse. |
| `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat` shader | `74659692f6350ba46b88180d9c826630` | missing `.meta` | Replace with current first-party wet basalt/geology material. |
| `Mat_TriplanarRock.mat` `_Rock_Albedo` | `47f0a231c050423488e0ff6f7d66f813` | missing `.meta` | Do not patch stale GUID. |

Valid static terrain/geology candidates:

- `Assets/_Project/Art/Materials/Mat_Terrain.mat`
- `Assets/_Project/Art/Shaders/TerrainMaster.shader`
- `Assets/_Project/Art/Shaders/H8_PhoticTerrainLit_1453.shader`
- `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroWetBasaltRock_1453.mat`
- `Assets/_Project/Art/Materials/World/Photic1465/MAT_H8_AuthoredWetBasaltBreakup_1465.mat`

## Required Unity Owner Pass

1. Inspect Crest canonical materials and first-party ocean material in Unity.
2. Confirm which missing GUID slots are effective shader properties, runtime wave-data placeholders, or stale serialized rows.
3. Do not add runtime Crest wrappers, clones, or custom material instantiation.
4. Assign Crest asset material directly where Crest requires it.
5. Replace scene/material assignments that use stale `terrain.mat` or `Mat_TriplanarRock.mat` with valid first-party geology materials through Unity APIs only.
6. Capture surface ocean, shoreline wet rock, and material slot readback in proof packet.

## Current Disposition

`PENDING VERIFICATION`.
