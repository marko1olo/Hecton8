# 1809 Photic Shallows Biota Placement Manifest

Final state: STATIC BIOTA MANIFEST COMPLETE - PENDING UNITY SLOT.

This artifact is a static authoring manifest for the 0-100 m photic shallows route. It does not claim Unity execution, scene edits, runtime placement, profiler results, GC results, frame-time results, capture results, or live fauna behavior. Every density number in the CSV is an authoring target for a future implementation pass, not a measured instance count.

Owned outputs:

- `Docs/Reports/Batch18/1809_PHOTIC_SHALLOWS_BIOTA_MANIFEST.md`
- `Docs/Reports/Batch18/1809_PHOTIC_SHALLOWS_BIOTA_MANIFEST.csv`

## Authority Chain

Read authority for this task:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `ecosystem.md`
- `terrain.md`
- `water.md`
- `vfx.md`
- `creatures.md`
- `ai.md`
- `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md`
- `Docs/Reports/Batch18/1802_SURFACE_SHALLOW_ASSET_INVENTORY.md`

Selected mandate files:

- `REND_Instanced_Flora_Physics.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `QA_Evidence_Text_Filter_Audit.txt`

## Product Lock

The photic shallows are not allowed to be empty, flat, dark, or hidden by noir lighting. For 0-100 m, the route must read as bright, colorful, navigable, and alive. Depth, caves, interiors, storms, and temporary eclipse windows may carry darkness; the starter photic route may not use darkness to hide weak water, weak shore, weak terrain, or weak biota.

The required biota job is not decoration. Flora, coral, kelp, passive life silhouettes, biolum accents, and industrial biofilm must:

- make the first route readable from spawn through Starter_ReefField, Route_Anchor, Copper_A, Scrap_A, Forward_Fabricator, and the lower photic threshold;
- produce distinct silhouettes at waterline, 5-20 m, 20-45 m, 45-80 m, and 80-100 m;
- preserve gameplay reads for resources, fabrication, and return lanes;
- scale continuously with `GlobalQualityWeight`, not with binary quality switches;
- remain attractive on compact settings and use saved frame budget for higher visual density on high-end hardware.

## Static Source Evidence

Scene and route evidence:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` contains active route sockets for `Starter_ReefField`, `Route_Anchor`, `Copper_A`, `Scrap_A`, `Forward_Fabricator`, and `Route_Frontier`.
- `Starter_ReefField` is an active `WorldZoneAnchor` labelled `Starter Fossil Shelf Field`, with intent for carbonate and kelp GPUI coverage near spawn.
- `Route_Anchor`, `Copper_A`, `Scrap_A`, and `Forward_Fabricator` are active `WorldContentSocket` entries. This is route-layout evidence only, not visual proof.
- `H8_WORLD_BIOLUM_FIELD_1428` exists but is inactive. It cannot be used as active biolum proof.
- `H8_FAUNA_SHADOW_BODY_*` objects exist as static scene silhouettes. They cannot be reported as living AI, swimming fauna, or gameplay hazards without future owner proof.

Asset evidence:

- `Assets/_Project/Prefabs/Nature/Flora/Baked/` contains baked flora families for `family_kelp_tall`, `family_kelp_patch_dense`, `family_kelp_canopy`, `family_coral_low`, `family_coral_branching`, `family_coral_massive`, and `family_coral_plate`.
- The baked flora README identifies this folder as the source of truth for real flora final-prefab routing, but `GEN_` starter prefabs remain static candidates until a visual proof pass accepts them.
- Representative baked prefabs include LOD groups with LOD0/LOD1/LOD2 thresholds and renderer guards. This is static source evidence, not runtime culling proof.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/` contains mapped coral and kelp material families using `Hecton_CoralMaster_GPUI.shader` and `Hecton_KelpMaster_GPUI.shader`.
- The inspected coral and kelp proxy materials have base/detail/mask/normal maps. Emission maps are not assigned on the inspected flora proxy materials, so navigation biolum must come from explicit authored biolum assets or materials in a later slot.
- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/` contains `Kelp`, `TubeCoral`, and `PorousRock` generated mesh sets. These are source asset pools, not finished placed route proof.

Rejected or excluded source evidence:

- `family_coral_brittle` is excluded from this 0-100 m manifest because its placement rule starts at 900 m.
- `family_kelp_abyssal` is excluded from this 0-100 m manifest because it is an abyssal/deep family.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/` and matching procedural-placeholder materials are rejected for final photic route proof.
- `bubble vent atlas - bad - redo.png` is rejected.
- `PFB_Debris_WreckField.prefab` is layout/support candidate only, not hero-ready evidence.
- Legacy or fallback ocean materials do not satisfy the photic surface/water art floor.

## Manifest Bands

The CSV is divided into authored route bands:

- `0-5 m`: waterline and first descent. Needs open sky/waterline readability plus clear biota silhouettes.
- `5-20 m`: first photic immersion. Needs dense color and kelp/coral memory landmarks.
- `20-45 m`: core starter reef. Needs resource-pocket readability, shelter, and non-uniform vegetation.
- `45-80 m`: deeper photic route. Needs wall/ledge/service-scar guidance without turning into abyss noir.
- `80-100 m`: lower photic threshold. Needs bigger silhouettes and danger language, but still readable and not blacked out.

The manifest intentionally ties biota to route function:

- waterline framing;
- starter reef entry;
- resource bowl;
- carbonate ledges;
- alabaster pool rims;
- return lane to fabrication;
- sediment-fan patch islands;
- tectonic wall cracks;
- lower photic silhouette bands;
- apex-shadow threshold;
- biolum navigation beads;
- industrial biofilm service scars;
- porous nursery substrate.

## Density And Quality Scaling

The CSV density columns are authoring targets at sampled points on a continuous `GlobalQualityWeight` curve:

- compact column: `GlobalQualityWeight` around 0.00.
- middle column: `GlobalQualityWeight` around 0.40.
- high column: `GlobalQualityWeight` around 0.75.
- ultra column: `GlobalQualityWeight` around 1.00.

Implementation rule:

```text
Density = lerp(compact_authoring_target, ultra_authoring_target, GlobalQualityWeight)
```

The future system may vary cadence, density, cluster radius, optional accent count, LOD distance, and secondary VFX through this continuous weight. It must not change gameplay truth ownership, DTO layout, save identity, route authority, or socket identity.

Compact mode is not allowed to become barren. It keeps the minimum route silhouettes: waterline fringe, first kelp/coral corridor, resource bowl, fabricator perimeter, and lower photic silhouettes.

## Required Future Unity Proof

The next Unity placement slot must produce proof before any runtime claim:

- waterline capture with Aegir sky, coast, route anchor, and first biota silhouettes;
- 5-20 m forward swim capture from spawn into Starter_ReefField;
- 20-45 m resource-pocket capture showing `Copper_A` and `Scrap_A` remain visible;
- 15-50 m `Forward_Fabricator` capture showing biota perimeter with clear approach radius;
- 30-100 m route-facing capture through the deeper photic band;
- top-down route capture proving cluster spacing and route continuity;
- 30-80 m wall/ledge capture showing coral plates, service scar, and readable route shape;
- 60-100 m lower photic capture showing silhouettes without abyss darkness;
- day and low-light biolum capture showing route beads as navigation evidence, not random glow;
- compact/middle/high/ultra comparison captures for the same route angle;
- material close-ups for coral, kelp, tube coral, porous rock, and biofilm support assets.

If the future slot touches runtime scatter, GPUI registration, streaming, culling, jobs, VFX, or fauna behavior, it must also provide runtime proof from the appropriate owner. This manifest does not provide that proof.

## Future Unity Implementer Prompt

```xml
<UNITY_IMPLEMENTER_PROMPT id="B18_PHOTIC_SHALLOWS_BIOTA_SLOT">
  <OBJECTIVE>
    Convert the 1809 static CSV into authored 0-100 m photic-shallows placement around Starter_ReefField, Route_Anchor, Copper_A, Scrap_A, Forward_Fabricator, and the lower photic threshold.
  </OBJECTIVE>
  <BOUNDARY>
    No runtime hero-procedural shortcut. No placeholder WorldRuntime procedural assets as final proof. No abyssal/dark treatment in 0-100 m photic water. No claim of fauna behavior unless an AI owner and capture proof exist.
  </BOUNDARY>
  <INPUTS>
    Docs/Reports/Batch18/1809_PHOTIC_SHALLOWS_BIOTA_MANIFEST.csv
    Assets/_Project/Prefabs/Nature/Flora/Baked/
    Assets/_Project/Art/Materials/WorldProceduralProxy/
    Assets/_Project/Art/Generated/Flora/BioForge/Shallows/
    Assets/_Project/Scenes/02_HECTON_WORLD.unity
  </INPUTS>
  <PLACEMENT_RULES>
    Use the route_zone and depth_band columns as hard authoring lanes.
    Treat density columns as sampled GlobalQualityWeight targets, interpolated continuously.
    Keep resource and fabrication sockets readable.
    Keep compact mode attractive and navigable.
    Use biolum only when it has explicit route meaning and visible owner.
  </PLACEMENT_RULES>
  <PROOF_REQUIRED>
    Submit screenshot set listed in 1809 report.
    Submit material close-ups for every accepted family.
    If runtime scatter is introduced, submit profiler, frame-debugger, memory, and GC evidence from the owning runtime path.
  </PROOF_REQUIRED>
</UNITY_IMPLEMENTER_PROMPT>
```

## Future Offline Asset And Bake Prompt

```xml
<OFFLINE_ASSET_BAKE_PROMPT id="B18_PHOTIC_SHALLOWS_BIOTA_BAKE">
  <OBJECTIVE>
    Replace weak static candidates with finished photic-shallows assets for coral, kelp, tube coral, porous nursery rock, biolum organisms, and industrial biofilm service scars.
  </OBJECTIVE>
  <REQUIRED_OUTPUTS>
    Baked prefab variants with LOD0/LOD1/LOD2.
    Material bindings with albedo/detail/mask/normal and explicit emission only where biolum has route meaning.
    Collision/interaction exclusions where assets sit near sockets.
    Designer-facing CSV or ScriptableObject bridge data matching the 1809 CSV route rows.
  </REQUIRED_OUTPUTS>
  <REJECTION_GATES>
    Reject grey primitives, muddy single-hue palettes, random glow, flat kelp planes, hero debris proxies, and any 0-100 m set dressing that looks abyssal.
    Reject any asset that cannot pass compact, middle, high, and ultra visual consequences without changing route truth.
  </REJECTION_GATES>
</OFFLINE_ASSET_BAKE_PROMPT>
```

## Scalability Consequences

Low/compact:

- Keep waterline fringe, first kelp/coral corridor, resource bowl, fabricator perimeter, and lower photic silhouettes.
- Use fewer clusters and wider spacing, not empty water.
- Preserve open visibility to route sockets.

Middle:

- Add more plate shelves, coral mounds, sediment patch islands, and canopy framing.
- Introduce controlled biolum bead chains only at turn points and return points.

High:

- Increase cluster variety, secondary coral accents, canopy layers, and passive silhouette frequency.
- Add stronger parallax in 20-80 m bands while keeping navigation lanes open.

Ultra:

- Use the saved budget for richer cluster density, more variant spread, layered canopy, and fine biolum accenting.
- Ultra is visual overkill on top of the same route truth, not a different gameplay route.

## Rejection Gates

Reject the future implementation if any of these remain true:

- waterline or 5-20 m route still reads as empty blue water;
- broad shallow route is flat, dark, muddy, or hidden by fog/noir;
- biolum is random glowing noise;
- coral/kelp placement blocks resource or fabrication readability;
- inactive scene objects are cited as active proof;
- `family_coral_brittle` or `family_kelp_abyssal` is used as normal 0-100 m photic flora;
- placeholder/procedural runtime prefabs are used as final route proof;
- primitive debris becomes hero foreground evidence;
- density samples are treated as binary quality switches;
- any runtime/profiler/AI/live-scene claim is made without the corresponding proof artifact.

## Static Final Scan

- MD manifest present: yes.
- CSV manifest present: yes.
- Required CSV columns present: yes.
- Route zones cover 0-5 m, 5-20 m, 20-45 m, 45-80 m, and 80-100 m: yes.
- Static proof labels used: yes.
- Unity/runtime/profiler claims made: no.
- Scene edits made: no.
- Placeholder families rejected: yes.
- Empty/flat shallow route accepted: no.

Result: `STATIC BIOTA MANIFEST COMPLETE - PENDING UNITY SLOT`.
