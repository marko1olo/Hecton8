# Unity Visual Proof Capture Runbook - 2026-06-04

Status: STATIC RUNBOOK / NO UNITY RUN / NO BUILD / NO IMPORT / NO ASSETS EDIT

Workspace: `C:\hades\Hecton8`

Audience: the single Unity owner who will capture visual proof later.

## Boundary

This runbook does not prove current Unity visual quality. It defines how the Unity owner must capture proof without polluting `Assets` and without converting static reports into runtime evidence.

Hard limits:

- Do not save screenshots, clips, profiler captures, Frame Debugger exports, manifests, or notes under `Assets` or `Assets/Screenshots`.
- Do not run multiple Unity visual-proof owners at once.
- Do not edit `Assets` while capturing a baseline packet. Capture first, change second, capture again.
- Do not call a static document, source scan, or passive screenshot a Unity proof packet.
- Do not crop, grade, resize, sharpen, or externally post-process acceptance captures.

Fresh passive capture boundary:

`Docs/Orchestration/Captures/unity_focus_state_20260604_125701.png` is a critique target only. It can be copied or referenced inside `refs/critique/`, but it cannot be counted as acceptance, before-proof, after-proof, Game View proof, Scene View proof, profiler proof, or player proof.

## Authorities Read For This Runbook

Root authorities:

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `presentation.md`
- `water.md`
- `terrain.md`
- `celestial.md`
- `atmosphere.md`

Relevant mandate registry entries:

- `QA_Evidence_Text_Filter_Audit.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_Terrain_VirtualTexturing.txt`
- `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`

## Evidence Labels

Use these labels in manifests and reports. Never upgrade a claim beyond the evidence label.

| Label | Meaning | Cannot prove |
|---|---|---|
| `STATIC_DOC` | Runbook, bible, report, checklist, design note. | Unity state, runtime visual quality, profiler cost, material binding. |
| `STATIC_SOURCE` | Source/YAML/text search or inspected file. | Scene wiring, import health, runtime behavior, visual acceptance. |
| `UNITY_EDITOR_CAPTURE` | Scene View or editor screenshot. | Player camera truth, UI/player proof, build proof. |
| `UNITY_GAME_CAPTURE` | Game View screenshot or clip from editor. | Standalone build proof or device proof. |
| `PLAYER_CAPTURE` | Standalone player screenshot or clip. | Profiler internals unless profiler artifact is paired. |
| `UNITY_CONSOLE` | Console log export/screenshot. | Visual quality or frame budget. |
| `FRAME_DEBUGGER` | Frame Debugger or RenderGraph Viewer capture/export. | Player readability or beauty. |
| `PROFILER` | Unity Profiler, Memory Profiler, GC, rendering stats, or GPU profiler artifact. | Composition quality or reference match. |
| `REFERENCE_COMPARE` | Side-by-side reference board or local reference path comparison. | Runtime proof unless paired with runtime captures. |
| `PENDING_VERIFICATION` | Claim is not proven by current artifacts. | Nothing accepted. |

Required non-trivial claim fields:

- Claim
- Evidence Label
- Artifact path
- Tool or command used
- Capture timestamp
- Scene and quality lane
- Residual risk

## Output Path Contract

All proof packets go under:

`Docs/Reports/Batch20/VisualProof/<SESSION_STAMP>_<CHANGE_SLUG>/`

Use local workstation time for `SESSION_STAMP`:

`YYYYMMDD_HHMMSS`

Use lowercase ASCII for `CHANGE_SLUG`:

`shoreline_foam_relink`, `aegir_material_pass`, `scatter_depth_gate`, `baseline_no_change`

Required folder layout:

```text
Docs/Reports/Batch20/VisualProof/20260604_153012_baseline_no_change/
  manifest_20260604_153012_baseline_no_change.json
  operator_notes_20260604_153012_baseline_no_change.md
  refs/
    critique/
    external/
    project/
  shots/
    game/
    scene/
    player/
    comparisons/
  clips/
  profiler/
  frame_debugger/
  console/
  rejected/
```

Exact screenshot filename format:

`VP_<SESSION_STAMP>_<CHANGE_SLUG>_<SHOT_ID>_<SCENE>_<MODE>_<UI>_<QUALITY>_<DEPTH>_<REV>.png`

Example:

`VP_20260604_153012_shoreline_foam_relink_VP-SHL-CP-001_02_hecton_world_game_uioff_compact_surface_after.png`

Exact clip filename format:

`VP_<SESSION_STAMP>_<CHANGE_SLUG>_<SHOT_ID>_<SCENE>_<MODE>_<UI>_<QUALITY>_<DEPTH>_<REV>.mp4`

Exact profiler filename format:

`PROF_<SESSION_STAMP>_<CHANGE_SLUG>_<EVIDENCE_ID>_<SCENE>_<QUALITY>_<REV>.<ext>`

Exact Frame Debugger or RenderGraph filename format:

`FDG_<SESSION_STAMP>_<CHANGE_SLUG>_<EVIDENCE_ID>_<SCENE>_<QUALITY>_<REV>.<ext>`

Revision values:

- `before`
- `after`
- `baseline`
- `regression`
- `rejected`

No spaces. No `final`, `final2`, `good`, `nice`, `accepted`, or unnamed screenshots.

## Manifest Minimum

Every proof packet requires `manifest_*.json` with these fields:

```json
{
  "session_stamp": "20260604_153012",
  "change_slug": "baseline_no_change",
  "unity_owner": "single owner name or ID",
  "unity_version": "recorded by Unity owner",
  "scene": "02_HECTON_WORLD",
  "route_or_position": "recorded coordinates or route stage",
  "global_quality_weight": 0.0,
  "quality_lane": "Compact",
  "camera_mode": "Game View",
  "ui_state": "UI_ON",
  "evidence_labels": ["UNITY_GAME_CAPTURE", "REFERENCE_COMPARE"],
  "reference_paths": [],
  "shotlist_ids": [],
  "artifact_paths": [],
  "known_missing_artifacts": [],
  "residual_risks": []
}
```

`global_quality_weight` must be a continuous float. The proof report may group shots as Compact, Middle, High, and Ultra, but the manifest must record the actual numeric setting or owner-controlled proxy value used for each lane. Do not report binary low/high modes as HECTON-8 quality proof.

## Reference Comparison Contract

Reference comparison is mandatory for surface, sky, Aegir, moons, coastline, ocean surface, photic shallows, underwater shallows, medium-depth hero routes, and route-visible terrain/scatter.

Valid reference sources:

- local files under `Docs/Reports/Batch20/VisualProof/<SESSION>/refs/`
- local project-approved reference folders if the Unity owner names the exact path
- screenshot from the same proof packet as the before state
- the passive critique capture, labeled `CRITIQUE_REFERENCE_ONLY`

Each reference entry must state:

- reference path
- why it is relevant
- what it proves or critiques
- which shotlist rows it compares against

If no relevant reference exists, the visual claim stays `PENDING_VERIFICATION`. Do not invent a reference by prose.

## Capture Preflight

Before the first screenshot:

1. Confirm no other Unity owner is using the editor for visual proof.
2. Confirm no import, build, shader compile, or long bake is active.
3. Create the output folder under `Docs/Reports/Batch20/VisualProof/`.
4. Record Unity version, active scene, Game View resolution, render pipeline asset, renderer, volume profile, quality lane, and `GlobalQualityWeight`.
5. Clear or export the Unity Console before capture, then capture the console after the packet.
6. Copy the current shotlist CSV into the session folder or record its path and revision.
7. Place references under `refs/` or record exact local paths.
8. Capture baseline before any scene, material, shader, water, sky, terrain, scatter, UI, or route change.

## Required Capture Packet

The minimum visual packet for a route-visible change contains:

- Game View and Scene View match from the same position.
- UI on and UI off for player-relevant shots.
- Shoreline close and shoreline wide.
- Underwater 0-5 m.
- Underwater 20-50 m.
- Aegir long shot and Aegir crop.
- 360 sky pan.
- Compact, Middle, High, and Ultra consequences.
- Old regression angles.
- Console export or screenshot.
- Profiler/GC/memory/VRAM proof if runtime/render/material residency behavior changed.
- Frame Debugger or RenderGraph proof if render features, Crest hidden inputs, custom passes, shader path, or visibility ownership changed.

Do not accept a proof packet that shows only High or Ultra. Compact is not allowed to become ugly mode.

## Before And After Rules

### Scene Change

Before:

- Capture the route entrance, route exit, horizon/sky context, shoreline, underwater 0-5 m, underwater 20-50 m, and any visible hazard/landmark affected by the scene change.
- Capture Game View and Scene View from matching coordinates.
- Capture UI on/off if player instruments are visible or route readability depends on HUD.

After:

- Repeat the exact same positions, camera FOV, time-of-day or macro state, quality lane, UI state, and route state unless the change intentionally modifies those facts.
- Add one free-look angle only after the matched angle is captured.
- If the after state removes an object, include Scene View proof that it is actually gone and Game View proof that the route did not become empty or less readable.

### Material Or Shader Change

Before:

- Capture close material read at gameplay distance.
- Capture glancing angle where roughness, normal, wetness, foam, caustics, or specular response is visible.
- Capture one wider route shot proving the material still supports scale and navigation.

After:

- Repeat all before shots.
- Add Frame Debugger or RenderGraph proof if the shader path, pass count, hidden material status, or renderer feature changed.
- Add profiler/GC/memory/VRAM proof if new textures, render targets, variants, material instances, or runtime updates changed.

Failure examples:

- wet rock becomes generic glossy plastic;
- foam becomes a flat white strip;
- Aegir becomes low-resolution bands or sine stripes;
- sky becomes a gradient with no structure;
- water becomes generic blue fog;
- material proof is only a crop with no gameplay-context shot.

### Scatter Or Biome Placement Change

Before:

- Capture shoreline/dry boundary.
- Capture shallow seafloor 0-5 m.
- Capture underwater 20-50 m.
- Capture at least one route silhouette where scatter affects navigation.

After:

- Repeat exact positions.
- Include a static or runtime placement dump if available. Label it `STATIC_SOURCE` unless it is generated in Unity runtime with matching artifact.
- Show that underwater-only scatter does not appear on dry shoreline unless a separate shoreline rule owns it.
- Show that compact still has meaningful silhouettes and route cues after density reductions.

Failure examples:

- kelp/coral/seafloor rocks on dry land due to depth 0 acceptance;
- random scatter with no geology/biome reason;
- proxy placeholder families in surface, shoreline, photic, or medium-depth hero routes;
- density cut that makes the route flat or empty.

## Pass Criteria

Global visual pass requires all of these:

- Surface, coastline, ocean surface, sky, Aegir, moons, photic shallows, and medium-depth hero routes meet or exceed the Subnautica-level floor.
- 0-100 m open water is bright, colorful, readable, and beautiful unless inside a valid cave or temporary storm/eclipse event.
- 20-50 m remains structured and navigable, not generic fog.
- Shoreline has waterline detail, foam/contact breakup, wet material identity, terrain silhouette, and no dry-land underwater scatter.
- Aegir and moons read as premium textured celestial bodies, not muddy or procedural scribbles.
- Game View and Scene View match enough to prove no editor-only composition fraud.
- UI on shots prove instruments do not destroy readability and are not hiding weak art.
- UI off shots prove the world itself carries route, scale, and material truth.
- Compact, Middle, High, and Ultra preserve the same gameplay truth and route ownership.
- High and Ultra spend budget on sensory richness, not new required gameplay truth.
- Every non-static claim has the matching artifact class.

Three-pillar pass requires:

- graphics proof;
- optimization proof when runtime/render path changed;
- gameplay/readability proof when route, hazard, UI, oxygen, depth, sonar, or threat state is part of the claim.

## Fail Criteria

Reject the packet if any item is true:

- screenshots are saved under `Assets` or `Assets/Screenshots`;
- a passive capture is counted as acceptance;
- only one favorable angle is supplied;
- crop is supplied without its paired long shot;
- old before shots are reused after a change;
- Game View and Scene View disagree materially and no reason is recorded;
- UI off proof is missing for visual art claims;
- UI on proof is missing for player route/instrument claims;
- Compact lane is muddy, flat, dark, primitive, or below the visual floor;
- High/Ultra hides a weak base result with bloom, fog, volumetrics, or grading;
- surface or photic shots are noir-dark by default;
- fog/darkness hides terrain, water, sky, Aegir, scatter, or weak meshes;
- terrain looks like random noise, toy low-poly, smooth blobs, or empty planes;
- water looks like blue fog, flat normal scrolling, or unmotivated caustics;
- Aegir/moons/sky look low-resolution, muddy, or placeholder;
- hidden Crest/input materials are claimed hidden without Frame Debugger proof;
- profiler, GC, memory, or VRAM claims lack profiler artifacts;
- reference comparison is missing for player-facing visual quality;
- report says "acceptable", "good enough", "final", or "optimized" without evidence labels.

## Old Regression Angles

Old regression angles are not optional. They exist because single-angle beauty shots have hidden prior failures.

Required regression themes:

- passive critique recreation: replicate the visible weakness from `unity_focus_state_20260604_125701.png` and prove it is fixed or still failed;
- surface darkness misuse: normal surface should not be black/noir unless storm/eclipse state is explicitly captured;
- Aegir crop and long shot: no muddy low-resolution bands or sine-stripe look;
- shoreline foam/wet basalt: no flat white ribbon, no glossy plastic rock, no broken wet/dry boundary;
- dry-land underwater scatter: no kelp/coral/seafloor-only props on dry shoreline;
- photic shallow clarity: no generic blue fog, no empty aquarium, no hidden route;
- medium-depth structure: twilight and pressure, not true darkness at 20-50 m;
- hidden Crest/input status: no visible package/default material accepted as final route art.

## Quality Consequences

Compact:

- Must preserve composition, readable water color, sky/Aegir/moon structure, route silhouettes, material identity, UI legibility, and return-path cues.
- May reduce texture resolution, scatter density, particle count, raymarching, bloom, volumetrics, shadow count, reflection richness, and optional telemetry cadence.
- Fails if it becomes muddy, flat, black, generic, or primitive.

Middle:

- Expected main player lane.
- Must look genuinely good, with richer scatter/decal/material read than Compact and stable gameplay readability.
- Must not depend on Ultra-only features for route understanding.

High:

- Adds richer material response, longer HLOD residency, better wetness, stronger sky/water detail, more local VFX, and better lighting where profiler budget allows.
- Must not change gameplay truth, item identity, hazard truth, save identity, route ownership, or shader channel semantics.

Ultra:

- Visual overkill lane: stronger atmosphere, light shafts, water/reflection/foam detail, richer Aegir/cloud/moon presentation, dense but controlled flora/VFX, visor contamination, and near-field material detail.
- Cannot make an otherwise failed Compact/Middle result acceptable.
- Cannot add required navigation or gameplay truth unavailable on lower lanes.

## Static Vs Unity Vs Player Vs Profiler Reporting

Use this report block per claim:

```text
Claim:
Evidence Label:
Artifact:
Tool:
Timestamp:
Scene:
Quality Lane:
GlobalQualityWeight:
Pass/Fail:
Residual Risk:
```

Examples:

```text
Claim: Aegir visible at first exit is premium and not muddy.
Evidence Label: UNITY_GAME_CAPTURE + REFERENCE_COMPARE
Artifact: Docs/Reports/Batch20/VisualProof/20260604_153012_aegir_material_pass/shots/game/VP_..._VP-AEG-CP-002_...png
Tool: Unity Game View screenshot
Timestamp: 20260604_153012
Scene: 02_HECTON_WORLD
Quality Lane: Compact
GlobalQualityWeight: 0.12
Pass/Fail: FAIL
Residual Risk: crop shows banding; long shot lacks cloud softness.
```

```text
Claim: New water pass costs under budget.
Evidence Label: PROFILER + FRAME_DEBUGGER
Artifact: Docs/Reports/Batch20/VisualProof/20260604_153012_surface_water_pass/profiler/PROF_...raw
Tool: Unity Profiler
Timestamp: 20260604_153012
Scene: 02_HECTON_WORLD
Quality Lane: Compact
GlobalQualityWeight: 0.12
Pass/Fail: PENDING_VERIFICATION
Residual Risk: no standalone player capture yet.
```

## How Not To Fake Proof

- Do not use editor Scene View as player proof.
- Do not use player proof as profiler proof.
- Do not use static source as material-slot proof after import.
- Do not hide UI unless the paired UI-on shot exists.
- Do not crop away waterline seams, bad terrain, low-resolution sky, or scatter mistakes.
- Do not move the camera to a non-gameplay impossible angle unless the shot is labeled `UNITY_EDITOR_CAPTURE`.
- Do not grade, blur, sharpen, denoise, or paint over capture files.
- Do not use photo-mode FOV/depth-of-field that the player cannot reach unless it is labeled non-acceptance marketing capture.
- Do not accept darkness, fog, bloom, or post-process as a replacement for actual water, terrain, sky, Aegir, moon, or material quality.
- Do not call a route playable if the proof packet contains only scenery.

## Final Unity Owner Report Shape

The Unity owner's final report for a proof session must use:

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

If Unity, player, profiler, or Frame Debugger did not run, say so directly. Do not fill the gap with static documentation.

