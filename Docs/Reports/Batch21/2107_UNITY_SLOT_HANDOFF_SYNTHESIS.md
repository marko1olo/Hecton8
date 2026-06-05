# 2107 Unity Slot Handoff Synthesis

Agent ID: 2107
Batch: batch21_art_replacement_wave
Role: UNITY_SLOT_HANDOFF_SYNTHESIS_ONLY
Evidence class: STATIC_DOC / STATIC_SOURCE
Runtime state: PENDING VERIFICATION

No Unity, MCP, Play Mode, profiler, import, dotnet build, csc, project build, asset edit, material edit, scene edit, prefab edit, package edit, shader edit, generated source output, or validator tool edit was performed.

## Boundary

This packet sequences future Unity-owner work. It does not close any production visual row.

It uses existing Batch20/Batch21 evidence to define:

- Unity-owner handoff order;
- Gemini/source-generation priority order;
- blocker statuses;
- reject gates;
- proof labels.

Static source paths, YAML refs, CSV rows, prompt IDs, and generated package plans are evidence of static planning only. They are not visual proof.

## Authorities And Mandates Read

Root and route authorities:

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3dmodel.md`
- `world.md`
- `terrain.md`
- `water.md`
- `atmosphere.md`
- `celestial.md`
- `rendering.md`
- `shaders.md`

Local mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_GPU_Sovereignty.txt`

Evidence reports read:

- `Docs/Reports/Batch20/2016_SURFACE_PHOTIC_MATERIAL_DEBT_TRIAGE.md`
- `Docs/Reports/Batch20/2019_PRIMITIVE_PROXY_ART_DEBT_ELIMINATION_PLAN.md`
- `Docs/Reports/Batch20/2019_PROXY_DEBT_QUEUE.csv`
- `Docs/Reports/Batch20/2019_GENERATION_ROUTE_MATRIX.csv`
- `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md`
- `Docs/Reports/Batch21/2101_WET_BASALT_SHORELINE_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch21/2101_WET_BASALT_STATIC_QA_GATE_CHECKLIST.md`
- `Docs/Reports/Batch21/2101_WET_BASALT_PBR_ROLE_AND_IMPORT_INTENT.csv`
- `Docs/Reports/Batch21/2102_PHOTIC_SEABED_SUBSTRATE_SOURCE_PACKAGE_AND_QA_GATE.md`
- `Docs/Reports/Batch21/2103_CORAL_REEF_FLORA_SOURCE_PACKAGE_CONSTRAINTS.md`
- `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.md`
- `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.csv`
- `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.json`
- `Docs/Reports/Batch21/2105_AEGIR_SKY_CLOUD_SOURCE_PACKAGE_AND_PROOF_GATES.md`
- `Docs/Reports/Batch21/2106_PRODUCTFACE_RESOURCE_TOOL_PICKUP_SOURCE_PACKAGE.md`

## Sibling Output Audit

No 2101-2106 output required this task to wait. All present sibling outputs remain static/source evidence only.

| ID | Present artifacts | Static completeness | Runtime/visual state |
|---|---|---|---|
| 2101 | Wet basalt source package, QA checklist, PBR role CSV | PRESENT. Static source package and gates exist. | PENDING VERIFICATION: no image generation, import, material bind, capture, profiler. |
| 2102 | Photic seabed source package and QA gate | PRESENT. Static source package and transition rules exist. | PENDING VERIFICATION: no source image, import, route capture, profiler. |
| 2103 | Coral/reef/flora source constraints | PRESENT. Static constraints and no-fallback gates exist. | PENDING VERIFICATION: no mesh/source generation, import, placement proof, overdraw proof. |
| 2104 | Validator MD, CSV, JSON | PRESENT. Static validator reports `3008` findings, `1947` critical, `346` active-scene findings. | PENDING VERIFICATION: no Unity, no capture, no runtime binding proof. |
| 2105 | Aegir/sky/cloud source package and proof gates | PRESENT. Static source package and prompt gates exist. | PENDING VERIFICATION: no generation, import, sky binding, captures, profiler. |
| 2106 | Product-face tool/resource pickup source package | PRESENT. Static source constraints and proof templates exist. | PENDING VERIFICATION: no mesh/source generation, import, interaction proof, captures. |

## 2019 Sequencing Extract

2019 defines the correct high-level order:

1. Static/doc controller pass: queue owners from `2019_PROXY_DEBT_QUEUE.csv` and `2019_GENERATION_ROUTE_MATRIX.csv`.
2. Source unblock pass: generate or restore missing source textures and lock shader channel contracts before import.
3. Unity owner slots: scene primitive/null audit, placement repair, flora/coral import, geology/waterline import, sky/ocean/Aegir/moon relink.
4. Integrator gate: static validators plus current Unity visual/profiler packet.

Future Unity-owner only steps:

- editing `Assets/_Project/Scenes/02_HECTON_WORLD.unity`;
- editing placement rule `.asset` files;
- importing generated meshes, textures, materials, and prefabs;
- relinking material references;
- disabling production proxy fallback in family assets;
- running material previews, screenshots, Frame Debugger, profiler, GC, memory, or VRAM proof.

2107 does none of those.

## Blocker Matrix

| Blocker | Evidence | Status | Future owner rule |
|---|---|---|---|
| Active scene primitive mesh refs | `2019-Q001`; 2104 built-in primitive findings | BLOCKED BY UNITY SLOT | Replace visible product-face primitive mesh refs with accepted authored/generated prefabs; material repaint does not count. |
| Null renderer material slots | `2019-Q002`; 2104 null slot findings | BLOCKED BY UNITY SLOT | Bind authored materials or remove invalid renderer slots, then prove in active scene. |
| Placeholder/default/package/proxy materials | `2019-Q003`, `2019-Q014`, `2019-Q015`; 2016 placeholder family blocker; 2104 placeholder refs | BLOCKED BY UNITY SLOT | Replace with route-owned authored material or prove diagnostic-only exclusion with dependency scan. |
| Sky/cloud/Aegir/moon source blockers | 2016 top blockers; `2019-Q004`; `2019-G001`; 2105 | BLOCKED BY SOURCE | Resolve source refs and source QA before Unity relink; reject muddy/noir surface and procedural stripe/scribble sky. |
| Terrain/triplanar/wetness/rock source blockers | 2016 ranks 4-8; `2019-Q005`, `2019-Q016`; `2019-G002`, `2019-G003`; 2101 | BLOCKED BY SOURCE | Source/channel contract first; no relink before shader channel order and PBR roles are locked. |
| Photic seabed/source floor | `2019-Q012`; `2019-G014`, `2019-G015`; 2102 | BLOCKED BY SOURCE | Use source candidate plus PBR derivation and material proof; no generic blue-gray sand or fog-only acceptance. |
| Dry-land kelp/coral/underwater rock leakage | `2019-Q006`, `2019-Q007`, `2019-Q008`; 2103 | BLOCKED BY UNITY SLOT | Unity owner must repair depth/substrate/waterline rules and prove dry-land zero plus submerged acceptance. |
| Flora/coral proxy finals | `2019-Q009`, `2019-Q010`; `2019-G004` to `G009`; 2103 | BLOCKED BY SOURCE | Generate/import real finals only after topology/material gates; missing finals skip or block, never proxy fallback. |
| Shoreline/photic/medium geology proxy finals | `2019-Q011`, `Q012`, `Q013`, `Q015`; `2019-G011` to `G016`; 2101/2102 | BLOCKED BY SOURCE | Generate close-source rocks first; HLOD coast mass depends on accepted close assets. |
| Ocean/Crest proof | `2019-Q017`; AGENTS third-party asset integrity | PENDING PROFILER | Measure existing Crest/ocean route before change; no custom runtime wrapper or material clone. |
| Static validator gap | `2019-Q018`; 2104 | BLOCKED BY UNITY SLOT | Active scene/source validator helps queue work; it cannot provide production visual closure. |
| Product-face tool/resource pickup primitive debt | 2106 plus targeted Batch18 evidence | BLOCKED BY SOURCE | Source/mesh/material/collider package required before Unity owner import and interaction/icon proof. |

## Unity-Owner Slot Order

Machine-readable order: `Docs/Reports/Batch21/2107_UNITY_SLOT_HANDOFF_ORDER.csv`.

1. Active scene primitive/null/default audit and visible product-face replacement.
2. Material source import/bind after source/channel contracts.
3. Placement rule repair for kelp, coral, and rocks.
4. Flora/coral final import, family relink, proxy fallback disable.
5. Geology/waterline final import, placement split, collision/LOD proof.
6. Sky/ocean/Aegir/moon relink, haze/fog tuning, Crest/ocean proof.
7. Product-face tool/resource pickup mesh and material replacement.
8. Integrator acceptance gate.

This order puts source/channel correctness before broad relinks, but keeps active-scene primitive/null/default audit first because scene overrides can keep broken art alive even when source prefabs improve.

## Gemini / Source-Generation Priority

Machine-readable order: `Docs/Reports/Batch21/2107_SOURCE_GENERATION_PRIORITY_ORDER.csv`.

Spend order follows 2022, with 2101-2106 used only as static package context:

1. Wet basalt shoreline albedo.
2. Shore foam/salt contact mask.
3. Photic seabed substrate albedo/height source.
4. Shallow branching coral albedo/height source.
5. Aegir cloud-band source.
6. Bright surface cloud deck.
7. Caustic/particle lookup source.
8. Kelp blade/holdfast source.
9. Scanner/tool casing source.
10. Resource ore pickup material source.

Source priority note:

- This is a source queue, not generated art.
- A generated image remains a source candidate until intake QA, manual tiling review, PBR derivation, Unity import, material binding, captures, and profiler/VRAM proof where required.
- Do not retry ranks 6-10 until ranks 1-5 have at least one generated candidate and static intake result, unless the controller explicitly reprioritizes.

## Proof Matrix

| Future work type | Minimum proof | Evidence label only after proof exists |
|---|---|---|
| Static queue or validator | CSV/JSON/MD report with scan scope and open rows | STATIC VERIFIED |
| Source image candidate | saved path outside `Assets/**`, prompt ID, SHA-256, 2x2 and manual 3x3 review, intake QA | STATIC VERIFIED for source QA only |
| PBR derivation | albedo/normal/mask/debug previews, role table, channel contract | STATIC VERIFIED or EDITOR VERIFIED depending tool path |
| Unity import/material bind | import settings, GUIDs, material slot table, dependency scan, material preview | EDITOR VERIFIED |
| Scene visual closure | current Game View and Scene View captures for the target route, low and high tier where relevant | PLAYER-CAPTURE VERIFIED or EDITOR/PLAYMODE label matching actual tool |
| Runtime/render/material route change | Frame Debugger/RenderGraph, profiler, GC, memory, VRAM artifacts | PROFILER VERIFIED |
| Placement rule closure | rule diff, dry-land rejection, submerged acceptance, route capture, overdraw/profiler if dense | EDITOR/PLAYMODE/PROFILER VERIFIED as actually run |
| Integrator visual acceptance | static validator plus current captures plus profiler packet when changed routes require it | Mixed labels; no static-only closure |

## Asset-Source Preconditions

| Preconditions | Required state before Unity bind |
|---|---|
| Source package | prompt ID, source role, candidate path outside `Assets/**`, SHA-256, rejection notes. |
| Channel contract | shader-specific order for albedo, normal/height, roughness/smoothness, AO, metallic, wetness, emission, masks. |
| Import intent | texture type, sRGB/linear, compression, mips, streaming, max size per quality weight. |
| QA result | intake audit plus manual 2x2 and 3x3 review. PASS_STATIC is not Unity acceptance. |
| Manual tiling review | seams, macro repetition, mips/downsample, low-res mush, baked light, text/logos, perspective artifacts. |
| PBR derivation | base albedo only, physical normal/height source, cavity AO, material roughness, documented MRAO/ORM packing. |

## Rejection Gates

Reject future closure if any gate hits:

- primitive mesh remains visible as production art;
- null/default/package/proxy/placeholder material remains production-bound;
- material repaint is used to disguise primitive geometry;
- source path, YAML, GUID, CSV, or prompt existence is treated as visual proof;
- unresolved GUIDs or empty material roles remain;
- channel order is guessed from filename;
- generated source has baked light, cast shadows, perspective, text, watermark, crayon noise, low-res mush, or muddy/noir cover;
- surface, sky, Aegir, moons, ocean surface, coastline, or photic shallows are darkened to hide weak art;
- kelp/coral appear on dry land unless a separate intertidal family and proof packet exists;
- alpha walls, flat decals, smooth blobs, paper-thin plates, ribbon-only blades, tube bouquets, or noise carpets are accepted as flora/coral finals;
- LOD0 visual mesh is used as production collider;
- HLOD coast mass is built from unaccepted close-source assets;
- Crest/ocean is wrapped or cloned at runtime instead of assigning/proving the asset route;
- static validator rows are marked closed without current Unity/import/capture/profiler evidence.

## Cleanup Rule

No destructive cleanup is authorized by this synthesis.

Future deletion is allowed only after:

1. obsolete reference proof;
2. dependency scan across active scenes, prefabs, materials, and source assets;
3. `.meta` handling for Unity assets;
4. rollback path;
5. import/compile proof by the Unity owner.

If proof is absent, quarantine or leave as blocked. Do not delete to make the report look cleaner.

## GlobalQualityWeight Consequences

`GlobalQualityWeight` is continuous. These consequences apply to future Unity/source owners only.

| Lane | Consequence |
|---|---|
| Low / compact | Smaller imported maps, reduced density, earlier HLOD, cheaper fog/haze, lower secondary masks. Surface/photic beauty, material identity, route cues, dry-land rejection, and silhouettes remain mandatory. |
| Middle | Expected player lane. Full PBR role coverage, readable wet/dry and seabed material identity, nonprimitive product-face assets, good scene captures, no proxy/default fallback. |
| High | Richer normals, wetness, foam breakup, cloud depth, coral pores, kelp fibers, geology fractures, longer LOD residency, stronger waterline/sky response after compact proof. |
| Ultra | Visual overkill through higher source detail, density, atmosphere, material microdetail, and hero closeups only after source QA, Unity import, scene proof, profiler, GC, memory, and VRAM remain stable. |

Quality may scale fidelity, density, cadence, residency, and optional diagnostic depth. It must not change gameplay truth, route truth, item/tool identity, save identity, collider truth, material channel semantics, or proof state.

## Status Legend

| Status | Meaning |
|---|---|
| CANDIDATE | Mentioned by evidence but not verified as an active target in current Unity context. Future owner must inspect first. |
| BLOCKED BY SOURCE | Needs source package, generated candidate, QA, PBR derivation, import intent, or channel contract before Unity binding. |
| BLOCKED BY UNITY SLOT | Needs Unity owner execution, active scene/prefab/material inspection, import, relink, placement edit, or capture. |
| PENDING PROFILER | Needs profiler, Frame Debugger, RenderGraph, GC, memory, or VRAM proof. |
| PENDING PLAYER CAPTURE | Needs current Game View, Scene View, Play Mode, or player capture proof. |
| NOT APPLICABLE | Evidence does not support the item for this wave or route. Not used to hide unresolved rows. |

## Target Audit

- Hard paths quoted in this report come from named static reports or sibling packets.
- All actual Unity target paths, materials, prefabs, scene objects, shader slots, and generated source outputs remain CANDIDATE until the future Unity owner verifies them in the current Editor/project state.
- No row in this packet says a production visual target is closed.
- No static YAML/path/GUID/CSV/prompt evidence is accepted as visual proof.

## Self-Audit

- Same-wave dependency: NONE. 2101-2106 were read as already written artifacts only.
- Unity/MCP/build/profiler/import: NOT RUN.
- Assets/Packages/ProjectSettings/Library/Temp/UserSettings touched: NO.
- Generated source outputs touched: NO.
- Validator tools touched: NO.
- Static proof upgraded to runtime/visual proof: NO.
- Production visual row closed: NO.

## Verification State

STATIC VERIFIED:

- authority docs and relevant mandates were read;
- Batch20 evidence and Batch21 2101-2106 outputs were read or summarized where machine files were large;
- Unity-owner slot order was written;
- source-generation priority order was written;
- blocker matrix, proof matrix, rejection gates, source preconditions, cleanup rule, status legend, target audit, and scaling consequences were written.

PENDING VERIFICATION:

- all source image generation;
- source image intake QA;
- PBR derivation;
- Unity import;
- material binding;
- scene/prefab edits;
- placement edits;
- sky/ocean/Aegir/moon relink;
- Crest/ocean proof;
- tool/resource pickup replacement;
- Game View / Scene View / Play Mode / player captures;
- Frame Debugger;
- profiler;
- GC;
- memory;
- VRAM;
- gameplay interaction proof.
