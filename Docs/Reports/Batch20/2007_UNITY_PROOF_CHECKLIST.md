# 2007 Unity Ocean Proof Checklist

Status: STATIC CHECKLIST / UNITY OWNER ONLY
Unity/build/runtime/profiler: NOT RUN BY 2007

## Output Route

All proof artifacts must go under:

`Docs/Reports/Batch20/VisualProof/<SESSION_STAMP>_ocean_shoreline_waterline/`

Required folders:

```text
refs/critique/
refs/project/
shots/game/
shots/scene/
shots/player/
shots/comparisons/
profiler/
frame_debugger/
console/
rejected/
```

Forbidden:

- `Assets`
- `Assets/Screenshots`
- unnamed screenshots
- externally graded/cropped/sharpened acceptance captures
- passive capture counted as acceptance

## Preflight

- [ ] Confirm single Unity visual-proof owner.
- [ ] Confirm no import, build, shader compile, bake, or long-running profiling job is active.
- [ ] Create the proof folder under `Docs/Reports/Batch20/VisualProof/`.
- [ ] Record Unity version, active scene, Game View resolution, render pipeline asset, renderer asset, volume profile, quality lane, and numeric `GlobalQualityWeight`.
- [ ] Copy `Docs/Orchestration/Captures/unity_focus_state_20260604_125701.png` to `refs/critique/` or record exact path as `CRITIQUE_REFERENCE_ONLY`.
- [ ] Export or screenshot Console before and after.
- [ ] Capture baseline before any scene/material/shader/render/water/terrain change.

## Mandatory Shotlist

| Shot ID | Mode | UI | Quality | Depth/Route | Required proof |
|---|---|---|---|---|---|
| VP-OCE-SURF-WIDE-001 | Game + Scene | off | all lanes | surface wide coast ocean Aegir | ocean color horizon waterline coast material |
| VP-OCE-CRIT-RECREATE-001 | Game | off | Compact + Middle | critique target recreation | proves old weakness fixed or still failed |
| VP-OCE-SHL-CP-001 | Game + Scene | off | all lanes | close shoreline | foam contact breakup no flat seam |
| VP-OCE-SHL-WET-002 | Game | off | Compact + High | wet basalt/waterline | roughness normal wet/dry material identity |
| VP-OCE-FOAM-003 | Game | off | all lanes | foam band | premium lace not white strip |
| VP-OCE-ENTRY-004 | Game | on | Middle | player entry/exit | waterline readability with instruments |
| VP-OCE-UW-005 | Game + Scene | on/off | all lanes | underwater 0-5 m | photic clear surface underside bottom readable |
| VP-OCE-UW-025 | Game + Scene | on/off | all lanes | underwater 20-50 m | medium-depth route structure not abyss darkness |
| VP-OCE-HORIZON-006 | Game | off | Compact + High | horizon/Aegir/ocean | no hard band no color fight |
| VP-OCE-AEGIR-CROP-007 | Game | off | Compact + Ultra | Aegir long and crop | texture/cloud softness not sine stripes |
| VP-OCE-MAT-SLOTS-001 | Inspector/Frame Debugger | n/a | current | active materials | water foam wet basalt terrain caustic bindings |
| VP-OCE-FDG-001 | Frame Debugger/RenderGraph | n/a | Compact + High | render route | named passes and bound resources |
| VP-OCE-PROF-001 | Profiler/Memory/GC | n/a | Compact + High | changed route | CPU GPU GC memory VRAM evidence |

Each artifact row must include:

- scene;
- camera coordinates;
- camera mode;
- UI state;
- quality lane;
- numeric `GlobalQualityWeight`;
- evidence label;
- artifact path;
- pass/fail;
- residual risk.

## Frame Debugger / RenderGraph Checks

- [ ] `Hecton Ocean Single-Camera Depth` appears for the active Game View/player camera.
- [ ] `_H8OceanSourceDepth` is the active camera depth texture.
- [ ] `_H8OceanDepthFoamMask` is produced before water transparent rendering.
- [ ] `_GlobalShorelineFoam` is bound when shoreline foam has active rows.
- [ ] `_GlobalShorelineFoamCount` is greater than zero for a foam proof shot.
- [ ] `_GlobalShorelineFoamRuntime` is bound and matches numeric quality/state.
- [ ] `Hecton Ocean Wake Compute` appears only when compute kernels are supported and valid.
- [ ] `Hecton Ocean Wake Clear` appears when wake compute is unavailable or inactive.
- [ ] No hidden shoreline auxiliary camera appears.
- [ ] No Crest realtime depth/foam/planar-reflection camera path is active unless explicitly approved by the Crest owner.
- [ ] No `Graphics.Blit`, `CommandBuffer.Blit`, `AddUnsafePass`, runtime `Camera.Render`, or `ReadPixels` route appears in active render proof.

## Profiler / GC / Memory Checks

- [ ] Record 300+ frames with GC Alloc column visible: ocean/shoreline update path must be 0 B/frame.
- [ ] CPU profiler markers include ocean depth mask, wake compute submit, shoreline foam upload, and atmosphere surface publication where available.
- [ ] GPU profiler or RenderGraph timing includes depth mask and wake compute cost.
- [ ] Any pass over 0.1 ms has load-shed behavior and visual justification.
- [ ] Async wave-height readback is disabled unless specifically under test.
- [ ] If async readback is enabled, prove no same-frame blocking wait and record latency/cadence.
- [ ] Memory/VRAM artifact records RT sizes, Crest texture residency, water/foam textures, shadow maps, and post stack.
- [ ] No managed `SetData` in the shoreline/ocean hot route without an explicit artifact and waiver.

## Material And Source Binding Checks

- [ ] Active ocean material has credible wave normals, color, specular, waterline, and foam inputs.
- [ ] Crest textures, if used, are referenced as vendor assets, not copied or edited in place.
- [ ] `WaveNormals.png`, `foam.png`, `Foam2.png`, and `Caustics_tex_color.png` imports are inspected for format, mip, compression, and binding.
- [ ] Active wet basalt material has albedo, normal, roughness/wetness, AO/mask, and waterline transition route.
- [ ] Terrain material has control and packed mask channels populated for claimed final coastline.
- [ ] Shoreline foam ribbon/source masks are either present and bound or explicitly listed as blocker.
- [ ] No flat `MAT_H8_SurfaceFoamRibbons_1428` claim if `_BaseMap` or `_MainTex` remains empty.

## Gameplay / Readability Checks

- [ ] Shore entry and exit remains readable with UI on.
- [ ] Player can read depth transition from shore to 0-5 m.
- [ ] 20-50 m route still has navigation structure and return cues.
- [ ] Hazard/interactable silhouettes are not erased by foam, fog, water tint, caustics, or glare.
- [ ] Aegir/horizon gives orientation and scale without overpowering waterline readability.
- [ ] Compact, Middle, High, and Ultra preserve the same gameplay truth.
- [ ] High and Ultra add sensory richness only, not required navigation truth.

## Reject Gates

- [ ] Reject if water is opaque flat blue.
- [ ] Reject if shoreline has no contact foam/waterline breakup.
- [ ] Reject if foam is a flat white strip.
- [ ] Reject if wet rock reads as generic glossy plastic.
- [ ] Reject if shallow water is muddy or hides terrain.
- [ ] Reject if 20-50 m is treated as true abyss darkness.
- [ ] Reject if Compact is ugly and Ultra is used to excuse it.
- [ ] Reject if Aegir/moons/sky look muddy, low-res, or procedural-scribbled.
- [ ] Reject if Crest vendor files are edited.
- [ ] Reject if a passive screenshot or static doc is counted as runtime proof.
- [ ] Reject if proof artifacts are saved under `Assets`.

## Final Owner Report Shape

```text
What was wrong:
What changed:
Unity evidence:
Player evidence:
Profiler/render evidence:
Reference comparison:
Rejected captures:
Files/artifacts written:
Pass/fail:
Residual risks:
```

If Unity, player, profiler, or Frame Debugger did not run, state that directly.

