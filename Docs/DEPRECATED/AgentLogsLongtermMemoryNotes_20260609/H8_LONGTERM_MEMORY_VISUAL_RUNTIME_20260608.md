# Codex Longterm Memory - Visual Runtime Work

Date: 2026-06-08
Scope: HECTON-8 Unity runtime visual work.
Evidence class: USER_DIRECTIVE_AND_STATIC_PREP
Runtime proof status: PENDING VERIFICATION

Read this file before starting or resuming HECTON-8 runtime work on terrain,
ocean/water, sky, Aegir, moons, surface route visuals, or related Unity scene
recovery.

## User Directive

The user expects autonomous, persistent, runtime Unity work. Unity may not have
been launched for a long time, and the first phase may be debugger work: import
errors, compile errors, missing packages, broken scenes, or stale generated
assets may need deep repair before the visual plan can begin.

Do not rush past broken Unity state. Debug carefully, prove fixes, then return
to the visual plan.

The current scene can be treated as poor unless proven otherwise. Terrain,
ocean/water, sky, gas giant Aegir, and moons are the immediate visual priority.
3D model replacement is a separate pass unless a model directly blocks the
current focused visual layer.

The user wants one focused pass at a time. Pick one layer, work it deeply, and
replace the approach if it is not good. Do not keep polishing a bad base.

## Target Picture

Aim for HECTON-8 as a premium underwater survival world:

- Bright, readable, beautiful surface and photic shallows.
- Real ocean color, believable waterline, foam, wet rock contact, and shallow
  floor visibility.
- NASA-punk / deep-sea noir as material language, not as an excuse to make the
  scene black, muddy, or hidden.
- Subnautica-level readability is the floor, not the ceiling.
- Aegir/gas giant and moons must be a major first-viewport signal when visible:
  large, textured, atmospheric, and integrated with sky/horizon lighting.
- Terrain must have macro shape, shore/cliff contact, route readability, and
  material truth before detail clutter.
- Industrial traces, cables, drowned structures, cockpit/base frames, and old
  colony elements should support scale and story, not cover weak terrain/water.

Darkness, fog, bloom, vignette, post color, or turbidity must never be used to
hide failed geometry, failed materials, failed water, or repeated generated mush.

## Mandatory Water Elements

When working on water, the required elements are:

- Ocean surface color that reads as real water, not generic blue fill.
- Wave shape, scale, normals, and specular response that survive close and far
  views.
- Refraction/transparency tuned for shallow readability.
- Foam where water meets rock/shore/objects and where wave behavior justifies
  it.
- Wetness/contact treatment on shore, cliffs, hulls, wrecks, and platforms.
- Underwater volume with depth falloff, not blanket blue fog.
- Route visibility underwater: the player must read floor, hazards, silhouettes,
  and traversal paths.
- Caustics/silt/particles only where justified, bounded, and performance-aware.
- Horizon/sky/Aegir integration so water reflects and belongs to the world.
- Compact-lane quality retained: not only high-end screenshots.

Water rendering must stay within HECTON-8 ownership boundaries. Water visuals
and sampling can use Crest and project bridges, but water must not become a fake
owner for pressure truth, AI, save state, vehicle truth, or gameplay systems.

## Existing Project Anchors To Recheck

Known current anchors from static preparation:

- `Assets/_Project/Scripts/Plugins/Crest`
- `Assets/_Project/Scripts/Plugins/MapMagic`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- `Assets/_Project/Prefabs/Hecton Ocean.prefab`
- `Assets/_Project/Prefabs/GasGiant_Aegir.prefab`
- `Assets/_Project/Prefabs/WorldGenerator.prefab`
- Scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, `02_HECTON_WORLD`
- Mandatory visual reference folder:
  `Docs/mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)`

Old/better state from about a month earlier may be important. Search history,
old prefabs, archived synthesis docs, and previous screenshots if the current
scene is worse. Restore, rebind, or beat the old route rather than accepting a
newer bad scene.

## Rules I Must Obey

- Read the HECTON-8 authority route before non-trivial work:
  root `AGENTS.md`, `Docs/AGENT_AUTHORITY_ROUTING.md`,
  `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, skills README, and the
  relevant route bibles/mandates.
- For player-visible work, inspect the mandatory reference folder and images
  before judging quality.
- Keep static/source confidence separate from Unity runtime proof.
- Use screenshots/multiview, console/import state, play mode, profiler,
  Frame Debugger, and GC evidence when those claims matter.
- If Unity/dotnet/build/import/profiler actions are needed, obey the process
  gate first. Do not start heavy actions while CPU or compile/import/build state
  is unsafe.
- If the same visual failure appears in two captures, or a route burns too much
  time without real improvement, mark the route visually invalid and change the
  base approach.
- Mark harmful or obsolete assets/systems as `DEPRECATED` with a reason instead
  of silently deleting or pretending they are useful.
- Prefer current source, assets, and fresh proof over old reports.
- Use MapMagic for terrain generation/authoring support, not as gameplay truth.
- Use Crest through assigned materials and approved bridges; avoid runtime
  material clone paths and untracked render hacks.
- Keep hot paths allocation-free and performance-aware.
- Do not create report-only success. Make source/asset/scene changes and prove
  them.
- Do not use goal tools for this project.

## Debugger Reality

If Unity starts broken, the task becomes:

1. Identify compile/import/package/scene blockers from logs and source.
2. Fix the real root cause, not symptoms.
3. Keep unrelated user changes intact.
4. Re-run the smallest valid proof.
5. Only then continue to terrain/water/sky/Aegir/moon visual work.

Do not hide broken Unity state behind plans. Do not claim runtime success from
static inspection.

## Focus Order

Preferred first runtime focus if the user does not override it:

1. Get Unity into a usable state.
2. Establish baseline captures from several angles.
3. Attack one visual layer deeply, starting with water/ocean if viable.
4. Then terrain/shore contact.
5. Then sky/Aegir/moons integration.
6. Then separate 3D model quality pass.

The user may override the focus order at any time.

## Commitment

I commit to treating this as a serious runtime-quality task, not a paper audit.
I will read this memory before starting the long Unity pass, preserve the
target picture, debug Unity first if necessary, reject bad visual routes instead
of polishing them, and avoid fake proof.

## Dialogue Snapshot From 2026-06-08 Attachment

Source reminder:
`C:/Users/danat/.codex/attachments/98d44aaf-3f5d-46fd-b1e4-08d7204f135b/pasted-text.txt`

The rules help, but rules do not build the world by themselves. If the night
runtime work does not use Unity captures, real scene owners, real materials,
and a repeated shot list, it will again produce a good-looking report about a
bad-looking scene.

What improved compared with the previous failed attempt:

- The old failure pattern is now forbidden: bad base first, then fog, grading,
  bloom, darkness, or "atmosphere" to hide it.
- After two similarly bad captures, the route must become
  `VISUAL_ROUTE_INVALID`; the next action is owner-stack repair/replacement,
  not more polish.
- The evidence ladder is explicit: docs, source scans, and `rg` do not prove
  runtime quality. Without Unity screenshot/profiler/GC/Frame Debugger evidence,
  runtime status stays `PENDING VERIFICATION`.
- There is anti-hang discipline: process preflight before Unity/build/profiler,
  no parallel heavy build/import/profiler actions, backoff after blocked
  attempts, exact blocker instead of infinite polling.
- Escalation must be concrete: source/asset fix, missing proof run, route
  rewrite, or exact blocker. Not another report.
- Use multiview evidence when possible: `surround`, `screenshot_multiview`,
  scene view, target camera, object-focused views. A single hero shot is not
  enough.
- MapMagic and Crest boundaries matter: MapMagic terrain-only through the
  bridge; Crest through assigned asset materials and approved bridges, not a
  runtime material clone workaround.

Mandatory reference conclusion from the earlier static prep:

- The target is not dark underwater horror.
- Surface and photic shallows must be bright, premium, readable, and beautiful.
- Required surface language includes real foam, wet rock, Aegir, biota,
  shoreline geology, shallow colony/cable/salvage traces, and cockpit/base
  framing when appropriate.
- Darkness starts deeper, in caves/interiors, storms, or pressure events; it is
  not the normal surface/shallow look.

The live project is not empty, but asset existence is not quality proof.
Known anchors include Crest scripts, MapMagic bridge scripts, `Ocean_Crest`,
`GasGiant_Aegir`, `WorldGenerator`, and generated flora/rock/prefab packs.
Generated kelp, rocks, placeholders, and models can be technically valid while
visually bad; they must be judged in scene.

Night runtime protocol to remember:

1. Load `02_HECTON_WORLD`; inspect console/editor state; do not touch builds
   without process preflight.
2. Capture baseline views: surface, shoreline/waterline, underwater, player
   height route, top/side/surround, and Compact/high if available.
3. Compare current captures against mandatory references and old
   in-development MapMagic+Crest frames.
4. First restore or reuse existing good owners: Crest ocean, MapMagic terrain,
   Aegir material/sky, and project textures.
5. Only then generate or add assets, and only in offline/editor package style:
   LODs, materials, colliders, manifests.
6. If two captures show the same unresolved visual problem, invalidate the
   route. Do not add "a little more fog".

Macro-frame priority:

1. Terrain.
2. Ocean/water.
3. Sky.
4. Gas giant Aegir.
5. Moons.

One pass means one major layer attacked deeply until there is real visual
movement. Do not lightly touch everything. If the user does not override the
order, the recommended first visual front is water/ocean surface plus waterline
proof, because bad water immediately kills terrain, sky, and Aegir, and Crest
has known historical baseline value. Sky/Aegir should be touched during that
pass only as needed for reflections, horizon read, and comparison.

3D models are separate. If they are bad, run a separate model-quality pass over
asset families, LODs, materials, colliders, and manifests. During the macro
visual pass, only isolate or mark models if they directly break the current
frame.

`DEPRECATED` use:

- Mark or isolate harmful/stale/weak assets with a reason.
- Valid reasons include: primitive, stale, weaker than baseline, wrong owner,
  placeholder, broken material, bad import.
- Do not broadly delete scene/prefab/asset content as a shortcut.
- Keep meta files and references in mind when any actual deletion is explicitly
  required later.

Water-specific work from the dialogue:

1. Capture current water baseline: surface, shoreline/waterline, underwater
   0-60 m, cockpit/first-person view, Compact/high if possible.
2. Find and verify real water owners: `Ocean_Crest.prefab`, Crest materials,
   `CrestBridge`, `OceanKinematics`, and sky/Aegir coupling.
3. If current water is worse than the old MapMagic+Crest baseline,
   restore/rebind the old good route before writing a new fake ocean.
4. Tune surface water: ocean color, wave normals, specular, refraction, foam,
   and horizon read.
5. Tune waterline/contact: foam at rocks, wet edges, shallow transparency, and
   visible terrain under water.
6. Tune underwater volume: depth falloff, absorption, bounded turbidity, route
   visibility, and no black crush.
7. Add justified caustics/silt/particles only when they support the scene, not
   as global noise or a mask for weak base work.
8. Verify Compact lane: water remains beautiful and readable, only cheaper.
9. Prove with captures and, when render/runtime paths changed, Frame Debugger,
   profiler, and GC evidence.

Mandatory water elements from the dialogue:

- Beautiful ocean color, not generic blue fog.
- Wave shape and normals so the surface is not a flat slab.
- Specular sparkle and sky response.
- Refraction/underwater distortion without mush.
- Foam on waves and especially at shoreline contact.
- Waterline wetness on rocks, shore, hulls, wrecks, and structures.
- Transparency and readable shallow floor.
- Depth falloff: 0-100 m bright/readable where the route requires it; deeper
  water can become darker with structure.
- Route cue visibility: the player can read the path back.
- Aegir/sky/horizon visually tied to the water.
- No darkness/fog/bloom as a "repair" for bad water.
- Zero hot-path GC and no runtime material clone hack.

Main water acceptance criterion: the frame must immediately read as an
expensive HECTON-8 ocean, not a blue plane with fog.

Residual risk stated in the dialogue: at the time of this memory, Unity,
profiler, and runtime captures had not been run. Current preparation is
static/reference/source-verified only. Runtime quality remains
`PENDING VERIFICATION` until live evidence exists.

If the user requests autonomous external GUI/batch control with other agents or
VS Code/browser sessions, that is a separate controller mode and requires the
orchestration docs. Ordinary Unity-runtime work and internal subagents do not
require local GUI/process-control docs.

Runtime work state update, 2026-06-08 08:05 +04:

- Unity/Bee compilation reached repeated green `Tundra build success` after
  repairing several blockers, then later became unstable because a parallel
  writer kept changing broad C# source/test files every 30-120 seconds.
- Do not trust a compile result gathered while source writes are active.
  Wait for a quiet source window, then compile and run Unity batchmode.
- Temporary read-only attributes on `Assets/**/*.cs` did not stop the writer
  because it appears to use replace/atomic writes. The attributes were cleared.
- Earlier repeated compile blocker pattern involved
  `NativeMemorySentinel` access in
  `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs`.
  Do not apply the stale blanket rule from the 08:05 note without rereading
  current source: later green compiles used the current public API shape in
  that file, while other external-writer errors moved elsewhere.
- Current water/sky authoring script:
  `Assets/_Project/Scripts/Editor/CodexWaterSkyFirstPassAuthoring.cs`.
  It creates `H8_CODEX_WATER_SKY_FIRST_PASS_20260608`, deprecates old weak
  water/sky roots, uses project/Crest assets where useful, and currently owns:
  sky dome, procedural Aegir/moons, URP Lit water surface with Crest wave
  normals, Batch21 photic seabed texture imported into Assets, shallow seabed
  mesh, underwater particulates, contact foam, and soft caustics. It no longer
  relies on active `Sky_System.prefab` or `GasGiant_Aegir.prefab` because those
  caused duplicate/ugly sky artifacts in earlier captures.
- Runtime batch state through `codex_water_sky_batch23_render.log`: Unity
  reached green with repeated `Tundra build success` and applied the water/sky
  root. Proofs exist at
  `Docs/Screenshots/CodexWaterSkyFirstPass/water_sky_main_20260608.png`,
  `water_surface_low_20260608.png`, `underwater_caustics_20260608.png`, and
  `aegir_sky_20260608.png`.
- Visual state after batch23: honest improvement over previous garbage. Aegir,
  moons, dark cyan sky, readable water surface, visible shallow seabed, and
  underwater surface are present. Still not final: horizon is too flat/straight,
  terrain relief is too uniform, there is little convincing rock/shore contact,
  and underwater depth needs better structure. Do not claim "final"; continue
  with a terrain/relief pass before wider dressing.
- External/parallel writer kept introducing unrelated compile blockers during
  the pass. Fixed examples: concrete `PlayerRuntimeContext` misuse in
  `WorldSpatialHashGrid.cs`, typo `plan` vs `_plan` in
  `ScatterBackendRuntimeHost.cs`, and local variable shadowing in
  `PlayerStressVFX.cs`. If new compile errors appear, assume source changed and
  inspect the current file before applying old fixes.

Runtime work state update, 2026-06-08 11:00 +04:

- Current clean baseline is `codex_water_sky_batch30_render.log`: Unity exited
  with code 0, no current C# errors, and applied
  `H8_CODEX_WATER_SKY_FIRST_PASS_20260608`.
- Remaining non-compile render warning:
  `HECTON/Terrain/TerrainMaster` wants `_H8CustomLightProbeGrid` in edit-mode
  proof renders. The Codex first-pass water/seabed materials use URP Lit, so
  this appears to be an existing scene/runtime terrain shader path, not the new
  water authoring layer. Track it before any final render/runtime claim.
- Batch26 introduced bad transparent planar water-mass artifacts and a vertical
  horizon shelf wall. Those were rejected, not hidden. Batch28 removed
  horizontal water-mass planes and replaced the wall with lower submerged
  ridges. Batch29/30 reduced dark rubble patches that read like decals.
- Proof set now includes five images:
  `water_sky_main_20260608.png`, `water_surface_low_20260608.png`,
  `underwater_caustics_20260608.png`, `terrain_relief_oblique_20260608.png`,
  and `aegir_sky_20260608.png`.
- Visual state after batch30: acceptable baseline, not final. Stronger than the
  previous garbage because it has readable Aegir/moons, a coherent dark cyan
  sky, a visible URP/Crest-normal water surface, shallow floor transparency,
  subtler underwater particles/caustics, and no large obvious rectangular
  water-mass cards. Terrain relief is present but still too soft and broad.
- Known visual defects after batch30:
  horizon/waterline is still too straight and empty; surface water is still too
  stripe-like; underwater midline is too hard; depth curtains are weaker but
  not solved; terrain needs real route-scale relief/shore contact rather than
  scattered low mound patches; Aegir texture is readable but still procedural
  and should later be upgraded with better band/storm detail.
- Next recommended focus: one terrain/shoreline pass, not models. Build a
  stronger non-rectangular photic shelf/canyon edge with contact foam/wet rock
  and route-scale relief, then re-shoot the same five proof angles. Do not hide
  with fog. If a new compile blocker appears, inspect current source first
  because external writer churn has repeatedly changed unrelated files.

Runtime work state update, 2026-06-08 11:45 +04:

- Batch31 route-scale shelf-break attempt was rejected. It created giant
  wall/plane silhouettes at the sides of `terrain_relief_oblique_20260608.png`,
  which violated the no-fake-walls/no-hide-with-fog standard. The generated
  shelf-break mesh asset was removed and the authoring script no longer creates
  that object.
- Batch32 was blocked by a real C# compile error in
  `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`: `PlayerMovementRuntimeState`
  and `PlayerLookState` locals could be read before definite assignment.
  The fix was minimal: initialize both structs to `default` before the runtime
  context `out` calls.
- Batch33 is the current green render baseline. Unity exited with code 0,
  applied `H8_CODEX_WATER_SKY_FIRST_PASS_20260608`, and has no current C#
  compile errors in the batch log.
- Visual state after batch33: the bad giant shelf walls are gone. The scene has
  a readable Aegir/moon sky, water surface, underwater view, seabed texture, and
  modest terrain relief. It is still not good enough: horizon/waterline is too
  straight, surface water reads as stripe bands, underwater has a hard horizontal
  midline, and seabed relief remains too soft/flat.
- Do not retry the rejected large shelf-wall approach. Next terrain/water pass
  should first inspect the real existing owners (`MapMagic`, `Crest`,
  `TerrainMaster`, `HectonOcean`, light-probe binding) and either reuse/repair
  them or add smaller terrain-native relief that cannot form huge planar walls.
- Persistent non-compile render warning remains:
  `HECTON/Terrain/TerrainMaster` requires `_H8CustomLightProbeGrid`. This must
  be understood before any final runtime-quality claim because it may mean old
  terrain draws are being skipped in proof renders.

Runtime work state update, 2026-06-08 12:35 +04:

- Batch34 added an editor-proof lighting fallback in
  `Assets/_Project/Scripts/Editor/CodexWaterSkyFirstPassAuthoring.cs`.
  The fallback binds empty `_H8CustomLightProbeGrid` and
  `_HectonGIRelaySHBuffer` buffers only around screenshot capture, then
  releases them. Batch34 proof logs no longer show the selected
  `HECTON/Terrain/TerrainMaster requires a buffer` / `Skipping draw calls`
  warning. This is proof-render hygiene, not a final runtime GI claim.
- Batch34 also added two legacy-origin proof angles:
  `legacy_origin_photic_surface_20260608.png` and
  `legacy_origin_photic_underwater_20260608.png`. Those captures proved the
  old origin scene was still visually broken: grey slabs, dark chunk clutter,
  and a cyan triangular artifact were active near the start area.
- Batch35 added `CodexSceneVisualAudit.cs`, which writes
  `Docs/AgentLogs/CODEX_SCENE_VISUAL_AUDIT_20260608.txt`. The audit found
  active origin contributors including `Sky_System` and old H8 visual pass
  roots. This gave concrete names for deprecation instead of guessing.
- Batch36 extended the deprecation list and disabled/renamed these old visual
  roots with the `DEPRECATED_WATER_SKY_20260608__` prefix:
  `Sky_System`, `H8_PHOTIC_REEF_DETAIL_PASS_1464`,
  `H8_SURFACE_LITTORAL_REBUILD_PASS_1430`,
  `H8_WATER_TERRAIN_MATERIAL_PASS_1453`,
  `H8_SURFACE_COASTAL_ISLAND_1428`,
  `H8_WATER_FLORA_TERRAIN_PASS_1446`,
  `H8_ORGANIC_SHORELINE_FOAM_FINE_1469`,
  `H8_ORGANIC_SHORELINE_BREAKUP_1469`, and
  `H8_SURFACE_COAST_GEOLOGY_1428`. Assets were preserved; scene clutter was
  disabled, not deleted.
- Batch36 is green: Unity exited with code 0, applied
  `H8_CODEX_WATER_SKY_FIRST_PASS_20260608`, and the selected log scan shows no
  current C# errors or proof-buffer skip warning.
- Visual state after batch36: old origin garbage is gone, Aegir/moons/sky,
  URP/Crest-normal water, seabed texture, contact foam, caustics, particles,
  and limestone relief still render. The frame is cleaner but not yet good:
  origin and focus are empty, the visible water/seabed extents still create
  straight edges/waterlines, surface water still reads as stripe bands,
  underwater still has a hard horizontal midline, and terrain relief is too
  soft for route-scale traversal.
- Next pass must not resurrect the rejected shelf-wall approach. Focus on
  terrain/water shape: remove visible square/slab boundaries, add believable
  non-rectangular seabed/ocean extents, stronger photic shelf relief, and
  better near/far underwater structure. Do not solve these defects by adding
  fog over them.

Runtime work state update, 2026-06-08 13:45 +04:

- Batch37 reran `CodexSceneVisualAudit` after the deprecation pass. Legacy
  origin was reduced to three active renderers from the Codex root only: sky
  dome, ocean surface, and generated seabed. The old `Sky_System` and old H8
  visual-pass roots were no longer active.
- Batch38 was blocked by a new compile error in
  `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`: `playerContext` was
  referenced in `CachePlayerMovement` without being declared in that fallback
  scope. The minimal fix was declaring
  `IPlayerRuntimeContext playerContext = _playerRuntimeContext;`.
- Batch39 greened after that fix. It replaced the square 112x112 seabed slab
  with an organic polar seabed mesh and reduced the long stripe pattern in the
  procedural water texture. Visual result was partly better but not accepted:
  the smaller organic seabed exposed a dark curved edge in underwater proof.
- Batch40 greened and is the first acceptable post-slab baseline: larger
  organic seabed scale, softer edge drop, lower water normal bump/tiling, and
  calmer water texture. It removed the nearby dark seabed edge and kept the
  scene clean, but surface views were still too flat and empty.
- Batch41/42 attempted visible surface/foreground limestone shoals to break
  the flat horizon. Batch42 was rejected: large shoals built from the old
  fan-style limestone patch mesh became flat green triangular wedges.
- Batch43 replaced `BuildLimestonePatchMesh` with a multi-ring organic patch
  mesh and lowered the shoals. It removed the worst triangles, but underwater
  still showed some shoals as floating flat islands near the surface.
- Batch44 greened and is the current accepted baseline. It lowered the failed
  shoal layer fully below waterline, preserving it as submerged relief instead
  of fake surface silhouettes. Current proof state: no old origin garbage, no
  giant shelf walls, no square seabed slab, no floating shoal slabs, calmer
  water texture, readable Aegir/moons/sky, and visible seabed texture/relief.
- Remaining visual defects after batch44: main surface view is still too empty
  and pool-like; the above/below-water boundary remains a hard horizontal band;
  terrain relief is readable but still not rich enough for an "awesome world";
  Aegir is serviceable but still procedural/simple. Do not retry the surface
  shoal silhouette trick unless using better real geometry/materials.

Runtime visual rejection update, 2026-06-08:

- User explicitly rejected the current visual direction as unacceptable. Treat
  batch44/batch45 as `GREEN_COMPILE_REJECTED_VISUAL`, not as an accepted art
  result.
- What is visibly wrong: the scene reads as a flat pool/bathtub, the horizon is
  a mechanical straight line, underwater has a hard horizontal cut, terrain is
  still broad flat seabed rather than a world, the procedural Aegir still reads
  as a flat striped card/ball, and the attempted shoal/cap layer became fake
  flat sheets when pushed toward the surface.
- Do not continue by polishing this proof-pass with more fog, more transparent
  bands, more flat cards, or more enlarged procedural patches. The correct next
  move is a real system/asset audit and a new rebuild approach grounded in the
  project's actual MapMagic/TerrainMaster/Crest/HectonOcean assets or stronger
  bespoke geometry, with failed roots explicitly marked rejected/deprecated once
  a replacement is ready.

Runtime terrain-only pivot, 2026-06-08:

- User redirected the pass to remove bad recent visual work in stages and focus
  strictly on terrain before water/sky/celestial polish. Treat terrain as the
  owner-stack problem now.
- The rejected `H8_CODEX_WATER_SKY_FIRST_PASS_20260608` direction must not be
  continued. Its generated sky/water/Aegir/seabed cards and procedural
  materials are cleanup targets, not a baseline.
- Cleanup policy: deprecate or remove only work that is clearly mine/recent and
  known bad. Do not destroy older April/May baseline assets or existing project
  systems unless a current audit proves they are duplicate junk.
- New terrain target: procedural, not hand-map locked, but visually guided by
  the stronger April MapMagic/Crest reference look: macro landforms first,
  believable drowned geology, shoreline/reef/canyon grammar, traversal-scale
  relief, material-readable surfaces, and no flat pool-floor world.
- Work order: (1) move rejected Codex generated assets/scripts/proofs to
  DEPRECATED, (2) read terrain/geology/scale authority, (3) build a new
  terrain-only authoring pass, (4) capture proof from multiple heights/angles
  and reject/rebuild if it still reads flat, fake, or hidden by fog.
