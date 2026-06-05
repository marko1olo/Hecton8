# 2803 Shoreline / Foam / Photic Terrain Static Art Route Audit

Date: 2026-06-04
Worker: Batch28 Subagent 2803
Mode: static/read-only route audit. No Unity Editor, Play Mode, build, profiler, screenshots, or `Assets/**` edits.
Write scope: this report only.
Evidence class: `STATIC_SOURCE` / `STATIC_DOC` only. Runtime, import, visual, Frame Debugger, RenderGraph, profiler, GC, VRAM, and route acceptance remain `PENDING VERIFICATION`.

## Verdict

The shoreline route remains blocked. Current static state is less broken than Batch26 in one narrow place: `Ocean-Underwater.mat` now serializes nonzero caustics and nonblack foam colors. That does not promote the route. The active terrain and close-contact materials still do not satisfy the surface/photic floor.

Strongest blockers:

1. Active broad photic terrain still binds `MAT_H8_PhoticRouteTerrain_1464`, which uses rejected wet-basalt albedo `TX_H8_WetBasaltShoreline_Albedo_1428.png` as its only texture input. No normal, MRAO, wetness, shell/sand blend, or contact mask is serialized in that material.
2. The active terrain shader is a single-base-map triplanar shader with sine caustic and fake wet spec. It cannot prove premium wet rock/shell/sand/waterline material truth.
3. Active close foam is a transparent `ZWrite Off` ribbon using generic `foam.png`, not a contact-owned foam/salt/wetness mask family.
4. Active floor caustics are a transparent additive sine fake with no intrinsic light/depth/receiver ownership. It can help as a fake only after route gating and image proof.
5. The underwater Crest material improved from the Batch26 zero-caustic state, but `_CLIPSURFACE_ON`, `_CLIPUNDERTERRAIN_ON`, and `_TRANSPARENCY_ON` remain enabled on the material assigned to the underwater owner. Slab/cut risk is still live until runtime proof.
6. No current Docs-generated shoreline source inspected here is promotable. Wet basalt 1428/1429/periodic variants and Batch21 shell/sand/seabed candidates remain rejected or reference-only.
7. No `1475` six-view packet, no real 1 m shoreline close proof, and no material-scale/wet-contact proof were produced by this task.

First-20-minutes route impact: this audit removes ambiguity around why the surface-exit/shoreline/photic route still fails. The blocker is not lack of prose direction; it is missing accepted material families and missing proof of waterline contact.

## Authority Loaded

Root/domain authority:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `water.md`
- `terrain.md`
- `rendering.md`
- `shaders.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`

Mandate registry:

- `.agents-skills/REND_GPU_Sovereignty.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

Prior route evidence:

- `Docs/Reports/Batch26/2602_FOAM_CAUSTIC_CREST_MATERIAL_AUDIT.md`
- `Docs/Reports/Batch26/2603_SHORELINE_TERRAIN_ART_ROUTE_AUDIT.md`
- `Docs/Reports/Batch27/2704_SHORELINE_TEXTURE_GENERATION_QA_ROUTE.md`

Narrow domain: static shoreline/foam/photic terrain material, shader, scene binding, and generated-source intake route.

## Current Scene Route Evidence

### Active owner and ocean route

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4608-4625` has active/enabled `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.
- `02_HECTON_WORLD.unity:4651` assigns `oceanUnderwaterMaterial` to GUID `ef94c26e44a36e24a9dcbc5995a2bed1`, mapped to `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`.
- `02_HECTON_WORLD.unity:4743-4765` enables shallow caustics and serializes depth/light/fade controls.
- `02_HECTON_WORLD.unity:4842-4870` debug values still show `_debugIsUnderwater: 0`, `_debugCausticsStrength: 0`, and `_debugSunVisualActive: 0`. Static serialized debug is not runtime proof, but it does not prove a successful underwater/caustic state.
- `02_HECTON_WORLD.unity:43187-43196` overrides the Crest ocean material to `Ocean.mat` and `_createFoamSim: 1`.
- `02_HECTON_WORLD.unity:67216-67232` has `Crest.UnderwaterRenderer` enabled, `_volumeGeometry: {fileID: 0}`, and `_copyOceanMaterialParamsEachFrame: 1`.

Static consequence: the owner route exists, but material-only tuning is unsafe because the underwater renderer copies ocean material params each frame. Runtime owner metadata is mandatory before claiming caustic/foam success.

### Active terrain route

- `02_HECTON_WORLD.unity:57861-57889` has `H8_PhoticRouteTerrain_1464` active with `MeshRenderer` enabled.
- `02_HECTON_WORLD.unity:57906-57907` binds material GUID `bdbb2649ef167e74c9bc048ac189dd2c`, mapped to `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_PhoticRouteTerrain_1464.mat`.
- `MAT_H8_PhoticRouteTerrain_1464.mat:26-27` uses `_BaseMap` GUID `f423facb87a22fe49b436302764cb854`, mapped to `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`.
- `MAT_H8_PhoticRouteTerrain_1464.mat:30-40` has only scalar/color controls: caustic strength, fill light, texture scale, wet spec, caustic color, ridge/shadow/tint. No normal map, MRAO, wetness mask, shell/sand substrate, foam/contact mask, or caustic receiver mask is serialized.

Static consequence: active broad terrain is materially under-authored and still depends on a rejected source lineage. It can explain black/shell/flat reads without invoking disabled slabs.

### Active foam route

- `02_HECTON_WORLD.unity:38681-38707` has `H8_CREST_FOAM_INPUT_PASS_1464` active with `Crest.RegisterFoamInput` enabled and `_disableRenderer: 1`.
- `02_HECTON_WORLD.unity:38708-38715` shows the foam input `MeshRenderer` disabled, which is expected for a Crest input path and is not visible proof.
- `MAT_H8_CrestFoamInput_1464.mat:28` uses `_Strength: 4.8`.
- `02_HECTON_WORLD.unity:73844-73857` has `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` active with `MeshRenderer` enabled.
- `02_HECTON_WORLD.unity:73874-73875` binds material GUID `937eb9eb615657644ae3de1fcf2d76d4`, mapped to `MAT_H8_ShorelineFoamFine_1469.mat`.
- `MAT_H8_ShorelineFoamFine_1469.mat:30-35` uses `Assets/_Project/Art/TEXTURES/foam.png`.
- `MAT_H8_ShorelineFoamFine_1469.mat:41-59` is transparent, `_Alpha: 0.88`, `_Surface: 1`, `_Threshold: 0.1`, `_ZWrite: 0`.
- `H8_ShorelineFoamRibbon_1428.shader:32-35` is transparent alpha blend with `ZWrite Off`; `:96-110` samples R/G/B from the foam texture and fades by camera/surface logic.

Static consequence: authored foam infrastructure exists, but visible contact remains unproven. The ribbon is a presentation fake, not a shoreline contact owner.

### Active caustic route

- `02_HECTON_WORLD.unity:64133-64161` has `H8_FloorCausticSoft_1443` active with `MeshRenderer` enabled.
- `02_HECTON_WORLD.unity:64178-64210` binds `MAT_H8_FloorCausticSoft_1443` and mesh GUID `f715884a162ee6c4fbc2846cf6f8eac9`.
- `MAT_H8_FloorCausticSoft_1443.mat:28-32` has `_ScaleA: 1.05`, `_ScaleB: 1.72`, `_Sharpness: 8.2`, and cyan tint alpha `0.24`.
- `H8_FloorCausticSoft_1443.shader:24-26` is transparent additive, `ZWrite Off`, `Cull Off`.
- `H8_FloorCausticSoft_1443.shader:70-72` derives the caustic pattern from sine functions only.

Static consequence: this is a cheap visual fake. It is allowed only as shallow/lit/receiver-gated support. It is rejected as broad caustic proof because it has no texture, depth ownership, sun/cloud state, receiver mask, or occlusion gate in the shader.

### Disabled slab / curtain watchlist

These are not current serialized visible causes, but they remain reject-on-raw-enable risks:

- `NOIR_UPPER_PRESSURE_LID`: active object, huge transform scale `{38, 0.25, 30}`, renderer disabled at `02_HECTON_WORLD.unity:5331-5359`.
- `H8_WORLD_LOW_WATER_OCCLUSION_00_1428`: active object, renderer disabled at `02_HECTON_WORLD.unity:8101-8129`.
- `H8_DEPTH_LOW_SHELF_1428`: active object, huge transform scale `{58, 1.15, 8}`, renderer disabled at `02_HECTON_WORLD.unity:10164-10192`.
- `H8_DEPTH_CEILING_OCCLUSION_1428`: active object, huge transform scale `{70, 1, 8}`, renderer disabled at `02_HECTON_WORLD.unity:75415-75443`.
- `H8_UnderwaterHazeCurtain_1454`: inactive object, renderer disabled, material `MAT_H8_UnderwaterHazeCurtain_1454`, mesh `MESH_H8_UnderwaterHazeCurtain_1454` at `02_HECTON_WORLD.unity:93776-93853`.
- `H8_UnderwaterHazeCurtain_1454.shader:26-28` is transparent alpha with `ZWrite Off`, `Cull Off`; `:75-77` adds sine shimmer caustic color into a vertical band.

Static consequence: do not raw-enable these to hide weak terrain or water. They need an explicit owner route, depth/light limits, and low-oblique regression proof.

## Current Material / Shader Risk Changes Since Batch26

### Repaired or improved static state

- `Ocean-Underwater.mat:104-111` now has `_Caustics: 1` and `_CausticsStrength: 1.35`. Batch26 reported caustics off; current static file no longer matches that specific blocker.
- `Ocean-Underwater.mat:197-202` now has brighter depth/diffuse/foam colors and nonblack `_FoamBubbleColor`.

### Remaining blockers

- `Ocean-Underwater.mat:14-25` still enables `_CLIPSURFACE_ON`, `_CLIPUNDERTERRAIN_ON`, `_TRANSPARENCY_ON`, and `_UNDERWATER_ON`.
- `Ocean-Underwater.mat:114-115` still serializes `_ClipSurface: 1`, `_ClipUnderTerrain: 1`.
- `Ocean-Underwater.mat:179` still serializes `_Transparency: 1`.
- `Ocean.mat:196-197` still uses a dark blue/green base diffuse/grazing pair. Surface beauty is not proven by YAML values.
- `MAT_H8_SurfaceCrestOcean_1428.mat:14-27` remains a non-promoted candidate with clip, transparency, foam, caustics, and underwater keywords all enabled.
- `MAT_H8_SurfaceCrestOcean_1428.mat:112-116`, `:180`, and `:196` carry overdriven caustic/clip/transparency/wave foam risk if assigned without isolation.
- `Ocean_UnderwaterCurtain.mat:53-60`, `:92-97` still has `_CausticsStrength: 10`, `_FoamScale: 15`, black grazing, and neon green foam bubble color. It is unsafe for raw route use.
- `H8_PhoticTerrainLit_1453.shader:94-118` samples one base map triplanar, adds sine caustics, and applies fake wetness. It has no normal/PBR/mask family.

## Candidate Asset Paths

### Candidates for reference / future correction only

These may inform a corrected route, but are not promotable now:

- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png`
  - Manifest: `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`.
  - `TX_H8_WetBasaltShoreline_1429_MANIFEST.md:22-26` rejects it with LR seam `30.611`, TB seam `34.508`, luminance mean `82.999`.
  - Use: paintover/reference only. No derivation. No import.
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png`
  - Manifest says reference-only/rejected; brighter photic direction is useful, but tile repetition blocks production.
  - Use: reference for corrected shell/sediment/source prompt.
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`
  - Manifest and Batch21 audit reject it.
  - Use: reference for shell/sand/calcite prompt and scale only.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
  - Existing Unity texture currently used by many photic materials.
  - Manifest permits source/reference or small masked decal use only before seam/source repair.
  - Use: contaminated legacy reference, not broad terrain.

### Candidates for route isolation only, not promotion

- `Assets/Crest/Crest/Materials/Ocean.mat`
  - Active surface ocean route. Keep for controlled Crest route proof. Do not count YAML as final water proof.
- `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
  - Active underwater owner material. Needs runtime owner proof because static caustics improved but clip/transparency flags remain.
- `Assets/_Project/Art/Materials/World/Photic1469/MAT_H8_ShorelineFoamFine_1469.mat`
  - Active visible foam fake. Candidate for isolated contact proof only after accepted contact mask/source exists.
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticSoft_1443.mat`
  - Active receiver fake. Candidate for shallow/lit receiver proof only; not acceptable as global caustic route.

## Rejected / Blocked Asset Paths

Rejected for direct production binding:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
  - Active broad terrain use is blocked by `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md:26-33` and `QA/WetBasalt1428/GeminiTextureIntakeAudit.md:21-23`.
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png`
  - Rejected by source manifest and QA.
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png`
  - Rejected: diagnostic edge pinning hides seam metrics but strict band analysis fails.
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png`
  - Rejected with high seam/band and clipped/saturated channels.
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve.png`
  - Rejected with high band mismatch and saturation.
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`
  - `Audit/Batch21/GeminiTextureIntakeAudit.md:21-24` reports `PASS_STATIC: 0`, `REJECT: 2`; shell/sand source is rejected.
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png`
  - Same Batch21 audit rejection.
- `Assets/_Project/Art/TEXTURES/foam.png`
  - Usable as a generic temporary/ribbon texture only. Blocked as a promoted shoreline contact source because it has no sidecar source manifest, no RGBA channel contract, no 2x2/3x3 foam-contact QA, no salt/wetness ownership, and no route proof in this audit.
- `Assets/Crest/Crest/Materials/Ocean_UnderwaterCurtain.mat`
  - Blocked for raw enable because of overdriven caustic/foam values and neon/black risk.
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
  - Blocked for assignment until clip/transparency/underwater keyword state and overdriven foam/caustics are isolated and proven.

No files were found under `Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/` or `Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/` during this audit. The Batch27 route is a contract, not evidence that new accepted sources exist.

## No-Unity Generated Texture Intake Route

All new source work stays outside `Assets/**` until `READY_FOR_UNITY_IMPORT`.

Required source root:

```text
Docs/GeneratedAssets/Gemini/Outputs/Batch28/2803/ShorelineTerrain/
Docs/GeneratedAssets/Gemini/Outputs/Batch28/2803/ShellSandSubstrate/
Docs/GeneratedAssets/Gemini/Outputs/Batch28/2803/ShoreFoamSaltContact/
Docs/GeneratedAssets/Gemini/Outputs/Batch28/2803/Caustics/
Docs/GeneratedAssets/Gemini/Outputs/Batch28/2803/AlgaeBiofilm/
Docs/GeneratedAssets/Gemini/Outputs/Batch28/2803/DerivedStacks/
```

Required audit root:

```text
Docs/GeneratedAssets/Gemini/Audit/Batch28/2803/
```

Required source families, in order:

1. `TX_B28_2803_WetBasaltShoreline_AlbedoSource`
2. `TX_B28_2803_PhoticShellSandSubstrate_AlbedoSource`
3. `TX_B28_2803_ShoreFoamSaltContact_RGBAMaskSource`
4. `TX_B28_2803_ShallowCausticDecal_GrayscaleMaskSource`
5. `TX_B28_2803_ShallowCausticLookup_RGBAMaskSource`
6. `TX_B28_2803_ShallowAlgaeBiofilm_TintMaskSource`

Every source candidate must have:

- sidecar manifest beside the source file;
- prompt lineage and negative prompt;
- SHA-256, dimensions, timestamps, role, intended meters per tile, color space, and channel contract;
- static intake command and result;
- 2x2 and 3x3 tile previews;
- manual review notes for seams, banding, repeated hero shapes, baked lighting, text/logo/border, perspective/object render, crushed albedo, and false material truth;
- status label from the Batch27 route labels.

Allowed before Unity import:

- save source candidates under `Docs/GeneratedAssets/**`;
- write manifests under `Docs/GeneratedAssets/**`;
- run static intake QA into `Docs/GeneratedAssets/Gemini/Audit/Batch28/2803/**`;
- generate 2x2/3x3 previews and contact sheets;
- mark candidate status.

Forbidden before `READY_FOR_UNITY_IMPORT`:

- saving generated output under `Assets/**`;
- importing source candidates into Unity;
- deriving normal/MRAO/wetness/foam/caustic maps from rejected sources;
- replacing `L_Basalt`, Rock031, Crest ocean materials, or active route material GUIDs;
- broad use of wet basalt 1428/1429 lineage;
- solving weak terrain with darker grade, green haze, sine caustic overdrive, or extra transparent ribbons.

## Next Implementation Targets

1. Build an accepted wet basalt family.
   - Needs albedo, normal, MRAO/wetness, wet/salt/mineral mask, import plan, and flat/low/grazing previews before Unity import.
2. Build an accepted photic shell/sand/calcite substrate family.
   - Must provide scale witness at 1 m and avoid repeated shell clusters or beige mud.
3. Build an accepted RGBA foam/salt/wet-contact mask.
   - The current foam ribbon may consume it later, but the mask must express contact cause, not generic foam texture reuse.
4. Build shallow caustic receiver masks.
   - Caustics must be local, lit, depth-gated, and receiver-specific. No global sine sheet acceptance.
5. Replace `MAT_H8_PhoticRouteTerrain_1464` only through a controlled route-owner change.
   - Do not mutate legacy basalt GUIDs in place.
   - Create new B28 material/TerrainLayer family names and bind one variable at a time.
6. Keep slab/lid/haze curtain renderers off unless a route owner explicitly proves them with low-oblique screenshots.
7. After import/binding by a Unity owner, produce a new `1475` or newer packet. Do not rename 1474.

## Required 1 m Shoreline Proof

The next acceptable proof must include a dedicated close view:

```text
h8_1475_s01_shoreline_close_1m_q060_uioff_[timestamp].png
```

Required visible content:

- actual 1 m camera-distance waterline;
- organic foam contact following terrain/water shape;
- wet rock transition with roughness/specular response;
- shell/sand/sediment/calcite scale cue;
- material breakup at micro, meso, and macro scale;
- shallow depth falloff and readable water color;
- no black shell, no flat green curtain, no opaque foam strip, no caustic overpaint hiding terrain.

Required metadata:

- scene name;
- camera position/rotation/FOV;
- depth band and player underwater state;
- UI state;
- route label;
- timestamp;
- resolution and render scale;
- hardware/tier;
- continuous `GlobalQualityWeight`;
- weather/light/sun/moon state;
- active terrain material GUID/name;
- active ocean material GUID/name;
- active underwater material GUID/name;
- active foam/caustic object states;
- material values for clip, transparency, foam, caustics, base/diffuse colors, and shader keywords;
- log/import state newer than final screenshot.

Required companion proof:

- surface/coast/Aegir view;
- underwater 0-5 m photic shallows;
- underwater 20-50 m route view;
- Aegir/celestial long view;
- low-oblique slab/curtain regression view;
- manifest, log tail, Frame Debugger/RenderGraph evidence, and profiler/GC/VRAM proof by the Unity owner.

## Low / Middle / High / Ultra Consequences

Low / compact:

- Preserve bright/readable ocean, wet basalt identity, shell/sand scale cue, and contact foam.
- Reduce texture resolution, decal count, caustic cadence, and transparent layers before damaging material identity.
- No haze/darkness cover-up.

Middle:

- Use 1024-2048 key shoreline families where budget allows.
- Add accepted wetness/contact masks, local foam breakup, and enough substrate variation to read as authored.

High:

- Spend budget on richer normals, wetness, close geology, receiver caustics, foam variation, and better water/specular response.
- Do not change route truth or material ownership.

Ultra:

- Add local shoreline overkill: denser decals, sharper PBR stacks, richer foam/caustic layers, stronger near-field material witnesses.
- Still no new gameplay truth, save identity, terrain authority, or material channel contract changes from quality.

All consequences must interpolate through continuous `GlobalQualityWeight`, not binary low/high switches.

## Static Proof Boundary

This report proves only static file state and source-gate status. It does not prove visual quality, Unity import correctness, runtime owner behavior, Frame Debugger state, profiler cost, GC, VRAM, or route acceptance.

Final blocking statement: the current route is blocked by rejected broad terrain source, missing PBR/mask material families, active transparent foam fake without contact proof, active sine caustic fake without receiver/depth proof, persistent underwater clip/transparency risk, and missing 1 m shoreline packet evidence.
