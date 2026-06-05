# 3103 Water / Crest / Foam / Caustic Owner

ID: `3103`  
Role: `WATER_CREST_FOAM_CAUSTIC_OWNER`  
Date: 2026-06-05  
Status: `STATIC VERIFIED` for source/material route classification. `PENDING VERIFICATION` for Unity readback, visual quality, Frame Debugger, profiler, GC, and player capture.

## Scope

Static owner plan only. No Unity launch, no Play Mode, no material edits, no shader edits, no prefab edits, no scene edits, no build.

Reason: Unity is not running, no Unity MCP/tool lane is exposed in this session, and a `dotnet` process is active. Build launch is forbidden under the current gate.

## Mandates Followed

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`

Authority read:

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `water.md`
- `rendering.md`
- `shaders.md`
- `quality.md`
- `taskslocal/batch31_night_visual_recovery/3103_WATER_CREST_FOAM_CAUSTIC_OWNER.txt`
- `Docs/Reports/Batch30/3004_WATER_FOAM_CAUSTIC_ROUTE_AUDIT.md`
- `Docs/Reports/Batch31/MATERIAL_TEXTURE_CRITICALS_20260605.md`
- `Docs/Reports/Batch31/CREST_TERRAIN_GUID_RESOLUTION_20260605.md`

## Current Static Route

Surface Crest route:

- `Assets/_Project/Prefabs/Ocean_Crest.prefab:463` binds `Crest.OceanRenderer._material` to `Ocean.mat` GUID `9def92ac79181fe41b238e91663f0fad`.
- `Assets/_Project/Prefabs/Ocean_Crest.prefab:482` has `_createFoamSim: 0`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:43189` overrides `_material` to `Ocean.mat`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:43195` overrides `_createFoamSim` to `1`.

Static verdict: active serialized surface route is `Assets/Crest/Crest/Materials/Ocean.mat`, not `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`.

Underwater route:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4651` binds `HectonUnderwaterVisuals.oceanUnderwaterMaterial` to `Ocean-Underwater.mat` GUID `ef94c26e44a36e24a9dcbc5995a2bed1`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4743` has `enableShallowCaustics: 1`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4746` has `causticsFadeOutDepth: 18`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4747` has `causticsMinLightFactor: 0.18`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:67222` has `Crest.UnderwaterRenderer._volumeGeometry: {fileID: 0}`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:67228` has `_copyOceanMaterialParamsEachFrame: 1`.

Static verdict: underwater material asset values are not final runtime proof because Crest can copy ocean material parameters each frame.

## Crest GUID Classification

The unresolved Crest GUIDs are replicated in canonical Crest materials:

- `33331381cbc5c564583cd5e47314cf78` appears in `Ocean.mat`, `Ocean-Underwater.mat`, and `MAT_H8_SurfaceCrestOcean_1428.mat` for `_MainTex`, `_WD_Sampler_0`, `_WD_Sampler_Hi`, `_WaveDataTex`, and related rows.
- `ba628b5ad7a570e4b95c3ee64a5c605d`, `6b165028befdf0745b04ebdfbf672681`, and `e94a5d7132329854281515fe36afb70e` repeat in `_WD_Sampler_*` rows.
- `f9a8c5bb065e21748a23f214a1f3a250` repeats in `_Skybox`.
- Valid Crest shader GUID `986f7c6732e8a6e4881407d7f15f25c3` resolves through `Assets/Crest/Crest/Shaders/Ocean.shader.meta`.
- Valid Crest wave normal GUID `800e061692ff7a84e887f439d3364410` resolves through `Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png.meta`.

Disposition:

- Treat Crest `_WD_*`, replicated `_MainTex`, and replicated `_Skybox` missing refs as runtime/stale Crest slots until Unity/Crest readback proves otherwise.
- Do not replace them with artist textures by raw YAML, text patch, or guess.
- Do not create Crest runtime material clones, wrappers, or per-object overrides.
- If Crest requires a material, bind the asset material directly through the Unity owner pass.

## Material Risk Map

`Ocean.mat`:

- Active surface route.
- `_Caustics: 1`, `_CausticsStrength: 0.92`, `_Foam: 1`, `_FoamScale: 0.032`, `_WaveFoamCoverage: 0.68`, `_WaveFoamStrength: 2.35`.
- Static source is wired enough to test, not accepted. Numeric material values do not prove Crest foam texture contribution, pass order, shoreline contact, or visual quality.

`Ocean-Underwater.mat`:

- Active underwater material reference.
- `_Caustics: 0`, `_CausticsStrength: 0`, `_Foam: 1`, `_FoamScale: 1.1`.
- `HectonUnderwaterVisuals.ResolveCausticsStrength()` gates caustics by `enableShallowCaustics`, underwater state, depth fade, light factor, adaptive scale, and soundscape scale.
- Static source proves potential, not visible caustics.

`Ocean_UnderwaterCurtain.mat`:

- High-risk if routed raw.
- `_CausticsStrength: 10`, `_FoamScale: 15`, `_FoamBubbleColor: {0.435, 1, 0, 1}`, `_DiffuseGrazing: {0, 0, 0, 1}`.
- No current volume geometry route is proven. Keep rejected unless a named owner gate, low-oblique capture, and profiler proof exist.

`MAT_H8_SurfaceCrestOcean_1428.mat`:

- Same Crest shader as `Ocean.mat`, but not active route proof.
- `_CausticsStrength: 1.65`, `_FoamScale: 0.028`, `_WaveFoamStrength: 3.8`, `_WaveFoamLightScale: 2.15`.
- Use only as isolated Unity trial if controller assigns it. It is not a repair by itself and can recreate overdriven cyan/green sheet artifacts.

`MAT_H8_ShorelineFoamFine_1469.mat`:

- Scene object `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` is active and renderer-enabled.
- Material uses full-alpha transparent foam with `_Alpha: 1`, `_Threshold: 0.07`, `_EdgeFade: 0.1`, `_Softness: 0.36`.
- This is an authored transparent ribbon. It is not Crest foam simulation proof and needs waterline/sorting/overdraw capture.

`MAT_H8_FloorCausticSoft_1443.mat`:

- Scene object `H8_FloorCausticSoft_1443` is active but `MeshRenderer.m_Enabled: 0`.
- Material has `_Tint.a: 0.24`, `_Sharpness: 8.2`, `_ScaleA: 1.05`, `_ScaleB: 1.72`.
- No visible receiver is proven. If enabled later, it must be shallow-light/depth gated and rejected in abyss, caves, storm, eclipse, or blocked-light cases.

## Rejected Paths

- Replacing Crest `_WD_*` missing GUIDs with artist textures.
- Assigning `MAT_H8_SurfaceCrestOcean_1428.mat` as a blind fix.
- Runtime material clones or wrappers for Crest.
- Enabling `Ocean_UnderwaterCurtain.mat`, haze curtains, slabs, pressure lids, or caustic planes without an owner gate.
- Accepting transparent shoreline ribbon as Crest foam proof.
- Accepting caustic material numbers as visible caustic proof.
- Hiding weak water with darkness, fog, bloom, or noir grade.

## Crest Foam Proof Path

Unity owner checklist, readback first:

1. Open `02_HECTON_WORLD` without applying unrelated dirty scene changes.
2. Read active `Crest.OceanRenderer._material` and confirm GUID `9def92ac79181fe41b238e91663f0fad`.
3. Read `_createFoamSim` on the scene instance and confirm the scene override is active.
4. Confirm `Crest.RegisterFoamInput` objects exist and keep input renderer-disabled if Crest expects that.
5. Capture Crest foam debug or Frame Debugger proof that foam input writes into Crest foam simulation texture.
6. Capture shoreline close at 1 m, 5 m, and low-oblique angle: foam must follow contact/waves and not read as a detached strip.
7. Capture transparent overdraw/sorting proof for `H8_ORGANIC_SHORELINE_FOAM_FINE_1469`.
8. Tune only after baseline proof. Prefer Crest foam first; authored ribbon remains fallback/accent only.

Acceptance requires:

- Crest foam texture contribution proof.
- Waterline capture.
- Sorting/overdraw check.
- Compact and High capture.
- Profiler/Frame Debugger artifact before runtime acceptance.

## Caustic Proof Path

Unity owner checklist, readback first:

1. Confirm active underwater owner object `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.
2. Confirm `oceanUnderwaterMaterial` points to `Ocean-Underwater.mat`.
3. During runtime capture, read `_debugIsUnderwater` and `_debugCausticsStrength`.
4. Prove `_debugCausticsStrength > 0` only when underwater, shallow enough, and light factor exceeds `causticsMinLightFactor`.
5. Prove caustics fade out by depth at `causticsFadeOutDepth: 18`, and disappear in unlit cave/storm/eclipse cases.
6. If using `H8_FloorCausticSoft_1443`, enable only behind a named shallow-light receiver gate and prove no abyss/cave leakage.
7. Use Frame Debugger/RenderGraph proof for pass order and cost.

Acceptance requires:

- Believable light reason: shallow daylight, floodlight, glass, pool, or local projector.
- Receiver proof: wet floor, terrain, hull, or flooded interior surface.
- Fade proof: depth/weather/cave/darkness.
- No unsupported global dancing caustics.

## Safe Unity Owner Binding Checklist

Allowed:

- Directly assign Crest asset material where Crest requires it.
- Inspect material properties through Unity APIs.
- Clear stale serialized rows only if Unity/Crest readback proves they are ineffective and the controller approves mutation.
- Bind route-owned foam/caustic fallback materials only with rollback notes and before/after captures.

Forbidden:

- Raw YAML material edits for Crest slots.
- Runtime `new Material(...)` clones or custom wrappers around Crest materials.
- Treating `_WD_*` rows as artist texture slots.
- Enabling green curtains, water sheets, slabs, or caustic planes without named owner gate.
- Claiming runtime acceptance from static values.

Proof packet fields:

- Scene name and camera transform/FOV.
- Active material GUIDs and keywords.
- Crest foam sim state and foam input state.
- Underwater owner debug values.
- Object active/renderer states for foam ribbon, floor caustic, curtain/sheet/slab candidates.
- `GlobalQualityWeight`, render scale, quality lane.
- Unity Console clean-window summary.
- Frame Debugger/RenderGraph and profiler artifact paths.
- Screenshots: surface/coast/Aegir, shoreline foam close, underwater 0-5 m, underwater 20-50 m, low-oblique slab regression, caustic receiver.

## GlobalQualityWeight Consequences

Use continuous interpolation. These are anchors, not binary switches.

Low / Compact:

- Canonical Crest route only.
- Preserve bright readable ocean color, surface sparkle, waterline, and route cues.
- Crest foam proof required before any foam acceptance.
- No broad haze curtain, pressure slab, green underwater curtain, or global caustic.
- Caustic hints limited to justified shallow light/floor cases at low strength.

Middle:

- Add verified Crest shoreline foam contribution.
- Add one narrow authored foam accent only if sorting/overdraw is clean.
- Add owner-gated underwater caustics from `HectonUnderwaterVisuals`.
- Add sparse particulates only if they preserve water structure.

High:

- Buy richer foam breakup, stronger normals/specular, and bounded caustic lace.
- Add local underwater haze from owner snapshots, not global fog cover.
- Keep gameplay truth and save identity unchanged.

Ultra:

- Layer premium foam detail, higher-frequency caustic variation, wet-rock response, stronger surface sparkle, and richer photic volume after Compact passes.
- Ultra buys sensory density only. It does not add new truth, hidden route authority, or material ownership changes.

## Regression Model

CPU:

- No code changed by this agent. Runtime cost remains unmeasured.
- Foam/caustic proof must show no suspicious >0.1 ms feature without load-shed.

GC:

- No hot path changed by this agent.
- Unity owner must provide GCMonitor/profiler proof for any runtime owner mutation or diagnostic tool use.

Memory/VRAM:

- No material or texture binding changed by this agent.
- Unity owner must record texture memory and RT/depth impact if enabling caustic or foam render features.

Cadence:

- `HectonUnderwaterVisuals` caustic strength is state-derived; runtime proof must show no per-frame material clone or unmanaged ownership drift.

Correctness:

- Crest remains third-party owner for ocean material route.
- Presentation caustics/foam must not become gameplay truth.
- `GlobalQualityWeight` must scale visual density/cadence only.

Failure modes:

- Wrongly patching Crest runtime slots.
- Transparent foam strip sorting over water.
- Overdriven first-party ocean candidate causing cyan/green sheet water.
- Underwater material values overwritten by Crest copy-each-frame path.
- Caustics visible without light reason.
- Curtain/slab regression hiding weak water art.

## Evidence-Class Summary

| Claim | Evidence class | Artifact | Runtime risk |
|---|---|---|---|
| Surface route uses `Ocean.mat` | `STATIC VERIFIED` | `Ocean_Crest.prefab`, `02_HECTON_WORLD.unity`, `Ocean.mat.meta` | Runtime override/import not read back. |
| Scene enables Crest foam sim | `STATIC VERIFIED` | `02_HECTON_WORLD.unity` `_createFoamSim` override | Crest texture contribution unproven. |
| Underwater owner uses `Ocean-Underwater.mat` | `STATIC VERIFIED` | `02_HECTON_WORLD.unity`, `Ocean-Underwater.mat.meta` | Runtime copy-each-frame can overwrite assumptions. |
| Crest `_WD_*` missing GUIDs are likely runtime/stale slots | `STATIC VERIFIED` | Repeated GUIDs across canonical and candidate materials | Unity/Crest readback still required. |
| Foam is not accepted | `STATIC VERIFIED` | Material/source route only | Needs Crest debug, Frame Debugger, shoreline capture. |
| Caustics are not accepted | `STATIC VERIFIED` | Underwater owner potential and disabled receiver | Needs runtime debug, light/depth proof, Frame Debugger. |

## Static Verdict

Current water recovery should not start by editing material YAML or swapping to the first-party Crest candidate. The owner-correct path is:

1. Prove current canonical `Ocean.mat` route in Unity.
2. Prove Crest foam simulation contribution before tuning foam.
3. Prove `HectonUnderwaterVisuals` caustic gating before enabling any caustic receiver.
4. Keep `MAT_H8_SurfaceCrestOcean_1428.mat` isolated until readback proves why current canonical route cannot be tuned safely.
5. Keep curtains, sheets, slabs, and unsupported caustic planes rejected.

Acceptance remains `PENDING VERIFICATION`.
