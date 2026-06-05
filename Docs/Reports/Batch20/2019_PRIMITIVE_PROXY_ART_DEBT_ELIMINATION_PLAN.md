# 2019 Primitive/Proxy Art Debt Elimination Plan

Agent ID: 2019
Status: STATIC PLANNING ONLY. No Unity, scene, asset, material, generated texture, or script edits were made.
Scope: surface, photic, and medium-depth hero routes.

## Authorities Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3dmodel.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `world.md`
- `terrain.md`
- `water.md`
- `rendering.md`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent in this workspace. Narrow domain inferred: generated art, placement, terrain/world route composition, waterline/render proof.

## Evidence Inputs

- `Docs/Reports/Batch20/2002_SURFACE_SHALLOW_SCENE_REPAIR_LEDGER.md`
- `Docs/Reports/Batch20/2003_KELP_ROCK_DRY_LAND_PLACEMENT_SPEC.md`
- `Docs/Reports/Batch20/2004_BIOFORGE_FLORA_CORAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch20/2004_FLORA_CORAL_VARIANT_MATRIX.csv`
- `Docs/Reports/Batch20/2004_TEXTURE_CHANNEL_CONTRACTS.csv`
- `Docs/Reports/Batch20/2004_GENERATION_HANDOFF_CHECKLIST.md`
- `Docs/Reports/Batch20/2005_GEOLOGY_SHORELINE_ROCK_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch20/2005_ROCK_VARIANT_MATRIX.csv`
- `Docs/Reports/Batch20/2005_TEXTURE_CHANNEL_CONTRACTS.csv`
- `Docs/Reports/Batch20/2005_COLLIDER_LOD_CONTRACTS.csv`
- `Docs/Reports/Batch20/2005_GENERATION_HANDOFF_CHECKLIST.md`
- `Docs/Reports/Batch20/2016_SURFACE_PHOTIC_MATERIAL_DEBT_TRIAGE.md`
- `Docs/Reports/Batch20/2016_TOP_BLOCKING_MATERIALS.csv`
- `Docs/Reports/Batch20/2016_REPLACEMENT_ACCEPTANCE_RUBRIC.csv`

## Static Findings

1. Active route art still has primitive/proxy contamination. 2002 reports `342` Unity built-in primitive mesh references in the active world scene and `137` filtered product-face primitive references, including moon, water, surface, basalt, caustic, and shore-facing names.
2. Active scene material binding is not clean. 2002 reports `45` active-scene null material slots. 2016 ranks sky, terrain, triplanar rock, wetness, rock, coral, and placeholder material families as open blockers.
3. Dry-land ecology leakage is credible. 2002/2003 show kelp, coral, and rock placement rules using `minDepthMeters: 0` and `preferSeafloor: 0`, which is not a valid submerged-placement proof.
4. Generated-source routes exist but are not final proof. 2004 defines flora/coral variants and source slots; 2005 defines shoreline/geology variants. These are handoff packages, not imported/proven Unity assets.
5. Placeholder/default material routes remain blockers. 2002 names BioForge and GeologyForge fallback material paths; 2016 reports `WorldRuntime/ProceduralPlaceholders/*_Placeholder.mat` as a production-route blocker until excluded or replaced.
6. Surface/photic/medium-depth acceptance is blocked by evidence, not taste ambiguity. The governing docs require bright, readable, premium surface and photic visuals. Darkness, fog, default materials, primitive rocks, dry-land kelp/coral, and procedural scribbles are rejected.

## Elimination Categories

### A. Active Scene Primitive Meshes

Evidence:
- `2002_SURFACE_SHALLOW_SCENE_REPAIR_LEDGER.md` finding `B20-2002-001`.
- Active scene static parser totals: `TOTAL_PRIMITIVE_MESH_REFS=342`, `FILTERED_PRODUCT_FACE_PRIMITIVES=137`.

Replacement route:
- Unity-owner-slot only. Replace visible product-face primitive meshes with accepted generated/source prefabs or explicit hidden/input-only markers.
- No docs-only closure. No material repaint of a primitive counts as production art.

Proof required:
- Active scene primitive validator output.
- Current surface/coast/photic/medium-depth screenshots.
- Frame Debugger and profiler/GC proof if render/material/scatter paths changed.

### B. Null, Package, Default, Placeholder, And Proxy Materials

Evidence:
- `B20-2002-002`, `B20-2002-010`, `B20-2002-011`.
- `2016_TOP_BLOCKING_MATERIALS.csv` ranks sky, terrain, triplanar rock, wetness, surface rocks, coral proxy material, and procedural placeholders as open blockers.

Replacement route:
- Generated-source missing blockers first for sky/terrain/rock/wetness source refs.
- Unity-owner-slot relink/import only after channel contracts and source texture paths are locked.
- Diagnostic-only exclusion is acceptable only with dependency scan proving no production scene/prefab route binds the placeholder.

Proof required:
- Material role table: albedo, normal, MRAO/mask, detail, emission/wetness.
- Import settings: sRGB/linear, normal type, compression, mips, streaming.
- Dependency scan proving old broken materials are fixed or no longer production-bound.
- Flat-light, channel-debug, final URP material previews.
- Current route captures.

### C. Dry-Land Kelp, Coral, Reef, And Underwater Rock Scatter

Evidence:
- `B20-2002-003`, `B20-2002-004`, `B20-2002-005`.
- `2003_KELP_ROCK_DRY_LAND_PLACEMENT_SPEC.md` confirms `depth == 0` is ambiguous and cannot prove submerged placement.

Replacement route:
- Safe docs/planning now: queue exact rule families and acceptance gates.
- Unity-owner-slot only: edit placement rules/assets, run placement validation, inspect scene results.
- Generated-source missing blockers: intertidal shoreline flora and debris blend need separate source families; underwater kelp/coral cannot be reused on dry land.

Proof required:
- Rule diff with submerged threshold or signed waterline predicate.
- `preferSeafloor` or equivalent hard gate.
- Required substrate/route context serialized and honored.
- Dry-land rejection proof and submerged acceptance proof.
- Overdraw/profiler proof for dense flora/coral fields.

### D. Flora/Coral Primitive Or Proxy Finals

Evidence:
- 2004 variant matrix rejects primitive stalks, ribbon-only blades, tube coral, smooth blob coral, paper-thin plates, alpha-only fan cards, proxy fallback, and dry-land placement.
- 2002 names BioForge fallback material and proxy risks.

Replacement route:
- Use `2004_*` source package for final flora/coral source generation.
- Build generated finals only in a Unity/editor generation owner slot.
- Production family profiles must skip missing real finals, not fall back to proxy primitives.

Proof required:
- Flat-material silhouette sheet before texture detail.
- PBR closeups and channel-debug sheets.
- Vertex color R/G/B/A sheet: sway, biolum, AO, family/wear.
- LOD overlay, validator output, and photic-route screenshots.

### E. Shoreline, Photic, And Medium-Depth Geology Proxy Debt

Evidence:
- 2005 variant matrix defines accepted replacement families for wet/dry outcrop, cobbles, cliff chunks, tidepool rims, shallow reef anchor rocks, underwater shelves, hero arches, medium-depth route markers, debris blend rocks, and distant HLOD coast mass.
- 2016 reports terrain, triplanar rock, and surface rock material blockers.

Replacement route:
- Complete wet basalt/waterline texture stack before final geology acceptance.
- Generate geology variants through GeologyForge/RockSculptor in a Unity/editor owner slot.
- Split shoreline rock rules from underwater rock rules.
- Disable primitive proxy fallback in product-facing families after real final variants exist.

Proof required:
- GeologyForge/RockSculptor manifest.
- LOD0/1/2 triangle counts and decimation method.
- Separate collider/proxy proof; LOD0 visual mesh cannot be collision.
- Shoreline close/wide, shallow underwater, medium-depth marker, LOD, collider, and HLOD transition captures.

### F. Sky, Ocean, Aegir, Moons, Waterline Source Debt

Evidence:
- `B20-2002-006` reports null sky/Aegir/cookie source slots and high haze/fog values.
- `2016_TOP_BLOCKING_MATERIALS.csv` ranks `Skybox.mat`, `Mat_HectonSky.mat`, and `Mat_HectonSky_CloudOverlay.mat` as blockers.
- `B20-2002-007` flags Crest underwater material parameter copying as profiler-suspicious until measured.

Replacement route:
- Generated-source missing blockers: sky/cloud/cubemap refs must resolve before relink.
- Unity-owner-slot only: bind sky/ocean/Aegir/moon sources, tune haze/fog, inspect current captures.
- Do not write custom runtime wrappers or clone Crest materials.

Proof required:
- Source-aware sky/ocean validator output.
- 360 sky and cropped Aegir/moon/horizon captures.
- Surface ocean and underwater 5-20 m captures.
- Frame Debugger/profiler proof for changed render/ocean paths.

## Sequencing

1. Static/doc controller pass: use `2019_PROXY_DEBT_QUEUE.csv` and `2019_GENERATION_ROUTE_MATRIX.csv` to assign owners. No Unity state changes.
2. Source unblock pass: generate or restore missing source textures listed by 2004, 2005, and 2016. Lock shader channel contracts before import.
3. Unity owner slot 1: scene primitive/null material audit and visible primitive replacement. Fix only scoped product-face scene references.
4. Unity owner slot 2: placement rule repair for kelp, coral, rocks, shoreline/intertidal split, and dry-land rejection proof.
5. Unity owner slot 3: flora/coral generated final import, family profile relink, proxy fallback disable, photic route proof.
6. Unity owner slot 4: geology/waterline generated final import, placement split, collision/LOD proof, shoreline/medium-depth proof.
7. Unity owner slot 5: sky/ocean/Aegir/moon material/source relink and capture/profiler packet.
8. Integrator gate: run static validators plus current Unity visual/profiler packet. Static docs alone cannot close any production visual row.

## Safe Now

- Maintain planning docs and queues under `Docs/Reports/Batch20/2019_*`.
- Maintain concise `Status_2019.md`, `Rationale_2019.md`, and `LOG_2019.md`.
- Hand off exact evidence, source blockers, owner sequencing, and proof requirements.

## Unity-Owner-Slot Only

- Editing `Assets/_Project/Scenes/02_HECTON_WORLD.unity`.
- Editing placement rule `.asset` files.
- Importing/generated meshes, textures, materials, prefabs.
- Relinking material references.
- Disabling production proxy fallback in family assets.
- Running Unity screenshots, Frame Debugger, profiler, or material previews.

## Generated-Source Missing Blockers

- `SRC_2004_*` flora/coral texture sources are planned, not proven imported.
- 2005 wet basalt, dry basalt, waterline, foam/salt residue, shallow reef, and medium-depth material stacks are incomplete.
- 2016 sky/cloud/terrain/triplanar/wetness/rock texture refs remain open blockers.
- Shader-specific packed channel order must be locked before treating mask/ORM/MRAO files as valid.

## Low / Middle / High / Ultra Consequences

- Low: fewer variants, lower density, smaller maps, earlier HLOD, simple shared materials. Silhouette, material identity, waterline, route cues, and dry-land rejection remain mandatory.
- Middle: full family coverage for production routes, accepted LOD/collider/material stacks, route readability intact.
- High: richer normals, detail maps, wetness, decals, denser photic flora/coral, longer LOD residency, stronger waterline/rock response.
- Ultra: visual overkill density and material richness for hero shots only after scene truth, placement ownership, collision, and profiler proof stay unchanged.

No tier may use primitive meshes, dry-land kelp/coral, default/package materials, unresolved texture refs, or placeholder/proxy finals as production art.

## Verdict

STATIC REJECTED for primitive/proxy art readiness. The safe work is complete only as a planning handoff. Production acceptance requires scoped Unity-owner execution, generated-source completion, and current proof artifacts.
