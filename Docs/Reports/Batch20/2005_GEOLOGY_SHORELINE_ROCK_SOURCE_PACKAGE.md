# 2005 GeologyForge Shoreline Rock Source Package

Batch ID: 2005  
Date: 2026-06-04  
Evidence class: static source package and handoff contract  
Runtime/editor status: Unity not opened. MCP not used. Imports not triggered. Dotnet build not run. Active `Assets/**` files not edited.

## Authority Read

- Root law: `AGENTS.md`
- Product locks: `VISION_LOCKS.md`, `TASTE.md`, `PROJECT_BIBLES.md`
- Domain bibles: `PROCEDURAL_ASSET_PIPELINE.md`, `3dmodel.md`, `3DMODEL_GEOLOGY_ROCKS.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, `terrain.md`, `water.md`, `world.md`, `performance.md`
- Mandates: `REND_Terrain_VirtualTexturing.txt`, `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- Required reports: `WORLD_PROCEDURAL_SCATTER_DRY_LAND_RISK_AUDIT_20260604.md`, `1907_TERRAIN_COASTLINE_MATERIAL_PACKAGE.md`, `1909_STATIC_VISUAL_DEBT_AUDIT_PROOF_MATRIX.md`

## Static Findings

1. The product-facing coastline and waterline remain blocked by material and proof debt. Batch19 identified `MAT_H8SurfaceWetBasaltReal_1428` and `TX_H8_WetBasaltShoreline_Albedo_1428.png` as candidates, not a complete shoreline material package. Normal, packed mask, wetness, foam residue, waterline blend, and material proof remain incomplete.
2. `WorldProceduralFieldSampler` depth logic treats dry terrain as depth `0`. Underwater placement rules with `minDepthMeters: 0` can select dry terrain unless repaired by a signed waterline/substrate route. This affects coral, kelp, rock floor, rock cluster, safe pocket, fauna, and landmark families.
3. All scanned procedural placement rules in the dry-land risk audit serialize `preferSeafloor: 0`, and no scanned rules serialize `requiredSubstrate`. Underwater rock, coral, and kelp rules therefore lack the first required product guard.
4. Procedural family assets still allow primitive proxies. Static samples under `Assets/_Project/Prefabs/WorldProceduralProxy` use Unity built-in primitive mesh guids. These are forbidden in product routes.
5. Existing candidate final geology prefabs exist under `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/`, including `PFB_Geo_RockFloor_*`, `PFB_Geo_RockCluster_*`, `PFB_Geo_RockShelf_*`, `PFB_Geo_RockArch_*`, `PFB_Geo_CaveEntrance_*`, and `PFB_Geo_LandmarkSpire_*`. Static samples show LODGroups and mesh references on several prefabs, but collider, material, placement, and visual proof remain unaccepted until Unity-side gates run.
6. `Assets/_Project/BakedGeometry/Geology/` has no accepted shoreline source package at this static pass. This report defines the missing package instead of editing active assets.

## Generator Route

Primary route: GeologyForge editor bake from staged CSV profiles.

- CSV schema exists at `Assets/_Project/Data/Geology/geology_generation_profiles.csv`.
- Current generator supports deterministic seed, resolution, octaves, AO rays, variations, LOD budgets, sector AUP, `GlobalQualityWeight`, mesh output, prefab output, manifest output, layout self-audit, and a 300-frame bake telemetry black box.
- Existing constants enforce `LodCount = 3`, 32-byte vertex layout, `CollisionTriangleBudget = 192`, and `CollisionProxyTriangleCount = 12`.
- `GeologyForgeSelfAudit` checks LODGroup, collision proxy, collider bounds, static renderer flags, and manifest references.

Secondary route: RockSculptor for hero erosion silhouettes only.

- Use for arch/overhang, cliff face modules, tidepool rims, and route-marker formations when hydraulic erosion/strata is required.
- Do not accept output if the triplanar material field is empty. RockSculptor currently falls back to Unity `Default-Material`; that is a reject condition for production.
- Its collider route is a proxy child, not LOD0 collision. Keep that constraint.

No runtime mesh generation is permitted for this package. All outputs are offline, deterministic, manifest-backed, and proofed before entering product placement.

## Target Rock Families

| Family ID | Product role | Source route | Material route | Placement route | Proof requirement |
|---|---|---|---|---|---|
| `GEO2005_SHORE_WET_DRY_OUTCROP` | Surface waterline geology, wet/dry transition, foam contact | GeologyForge basalt outcrop profile | Wet basalt + dry mineral edge + waterline mask | Signed waterline band, slope 0-45, no underwater scatter rule reuse | Shore close, shoreline wide, material flat pass, final lit pass |
| `GEO2005_BEACH_COBBLE_FIELD` | Dry beach and splash-zone grounding | GeologyForge small varied cobble profile | Dry basalt, damp underside, sand contact mask | Above water or splash band only, render-only dense cobbles | Grounding shot, density shot, no individual collider spam |
| `GEO2005_COAST_CLIFF_FACE_CHUNK` | Modular coastline wall replacing grey procedural cliffs | RockSculptor or GeologyForge large cliff module | Wet basalt lower band, dry basalt upper band, sediment streaks | Coastline sockets, slope 30-85, terrain-aligned | Cliff silhouette, seam shot, LOD overlay |
| `GEO2005_TIDEPOOL_RIM_WET_LEDGE` | Intertidal walk/readability and tidepool framing | RockSculptor shallow ledge profile | Polished wet rim, algae/mineral stain, dry top | Signed band around waterline, no coral on dry rim | Close material, gameplay readability, collider overlay |
| `GEO2005_SHALLOW_REEF_ANCHOR_ROCK` | Photic reef grounding without coral-on-land error | GeologyForge reef anchor profile | Wet basalt/limestone mix, algae stain, sand cavity | Depth > 1.5 m, prefer seafloor, substrate rock/sand mix | Underwater 0-5 m proof, no dry scatter proof |
| `GEO2005_UNDERWATER_SHELF_ROCK` | Traversal shelf and seafloor layer break | Existing shelf profile plus new wet material | Medium wet basalt, AO cavities, silt line | Depth > 8 m, slope 8-58, prefer seafloor | Shelf wide, route readability, collision proof |
| `GEO2005_HERO_ARCH_OVERHANG` | First-hour hero silhouette and route landmark | RockSculptor hero pass, GeologyForge backup | Layered eroded basalt, wet underside, bright rim | Depth/shore socket only, not random scatter | Hero shot from route, silhouette shot, collider compound proof |
| `GEO2005_MEDIUM_DEPTH_ROUTE_MARKER` | Medium-depth navigation formation | GeologyForge spire/marker profile | Darker basalt with readable mineral edge, no black fog reliance | Depth 20-120 m, route socket, sparse density | Medium-depth readability shot, low quality shot |
| `GEO2005_DEBRIS_BLEND_ROCKS` | Asset grounding around wreckage, props, cliffs | GeologyForge debris profile | Inherits local material family, vertex blend mask | Socket-local, density capped, no gameplay collision unless blocker | Contact shadow/material blend proof |
| `GEO2005_DISTANT_HLOD_COAST_MASS` | Distant coastline mass without primitive cliffs | Offline HLOD shell/impostor from accepted cliff/outcrop set | Baked composite, no close inspection use | Distance-only, no collider, terrain owns collision | HLOD transition shot, coastline wide shot |

The machine-readable variant source matrix is `2005_ROCK_VARIANT_MATRIX.csv`.

## Material and Texture Contract

The package needs a complete wet basalt/coast material stack before any final acceptance. Existing albedo-only or base/normal-only material candidates are not enough.

Required shoreline material families:

1. Wet basalt shoreline: glossy lower band, dry upper band, mineral chips, salt line, waterline wetness.
2. Dry basalt and mineral stain: readable in full daylight, not grey mush.
3. Beach sediment contact: black sand/silt accumulation in cavities and underside contact.
4. Reef anchor rock: wet basalt/limestone blend, algae stain, coral-safe substrate mask.
5. Medium-depth basalt: readable silhouettes and mineral lines without fog hiding.
6. HLOD coastline composite: baked from accepted close materials, no new generic atlas.

Channel contract:

- Albedo/base color: sRGB. No baked lighting, cast shadows, perspective object render, text, watermark, or AI artifact.
- Normal: linear, OpenGL/Unity orientation confirmed in import.
- Packed MRAO/wetness: shader-specific contract must be locked before authoring. Default bible route is R = metallic, G = roughness or smoothness as shader expects, B = AO, A = wetness/family/emission mask. The URP hot-path mandate contains an older AO/smoothness ordering, so no one may guess the packed channel order.
- Vertex color: R = chip/edge/mineral reveal, G = wetness/algae/mineral stain, B = AO/cavity, A = material blend/family mask.
- Detail maps: shared, array-compatible, no per-instance material clones.
- Foam/salt residue: decal or mask layer, not alpha-blended dense geometry unless profiler proof accepts it.

Texture source prompt contract for later manual/orchestrator generation:

```text
Seamless orthographic PBR material sample, wet black basalt shoreline rock, bright daylight, layered erosion shelves, mineral chips, salt-water wet line, fine cavity AO, algae stain only in lower damp pockets, no cast shadow, no horizon, no object render, no text, no watermark, no perspective, no baked lighting.
```

Generate candidate images manually or through an explicit image-generation orchestrator only. Do not import them until source resolution, channel split, color space, and rejection gates are verified.

The detailed texture contract is `2005_TEXTURE_CHANNEL_CONTRACTS.csv`.

## Placement Contract

Shoreline rocks must not reuse underwater placement rules blindly. The dry-land audit proves that depth `0` is ambiguous because it includes dry terrain.

Required placement bands:

- Dry coast above waterline: outcrops, cobbles, cliff chunks, debris blend only. No kelp, coral, reef anchors, seafloor floor rocks, fauna, or underwater proxy families.
- Wet/splash band: outcrops, tidepool rims, ledges, small cobbles. Requires signed waterline offset or explicit shoreline socket. Do not infer from `depth == 0`.
- Shallow submerged 1.5-8 m: reef anchor and shallow shelf rocks. Requires `preferSeafloor = true`, substrate rock/sand, and waterline proof that no instance appears on dry land.
- Medium depth 8-120 m: shelf rocks, route markers, larger anchors. Requires route/biome/depth socket and readable landmark silhouette.
- Distant coastline: HLOD mass only, no collider, not a close-inspection asset.

Rule repair requirements for later Unity owner:

1. Split shoreline rock rules from underwater rock rules.
2. Underwater rules must serialize nonzero minimum submerged depth or a signed waterline predicate.
3. Underwater rules must serialize `preferSeafloor: true`.
4. Underwater ecology rules must serialize `requiredSubstrate`.
5. Procedural families used by product routes must set proxy primitives off or route through a final-only selector.
6. Strict envelope mapping must not skip preferred biome, zone, or socket filters.
7. Scatter density must scale continuously with `GlobalQualityWeight`, never through binary low/ultra switches.

## LOD and Collider Contract

Every accepted visual family requires LOD0, LOD1, LOD2, and a separate collider/proxy route. LOD0-as-collider is rejected.

- Small/debris rocks: LOD0 under 4k triangles, LOD1 under 1.2k, LOD2 under 250. Dense instances should be render-only unless they affect navigation.
- Medium rocks: LOD0 under 9k, LOD1 under 3k, LOD2 under 600.
- Large/hero/cliff rocks: LOD0 under 18k, LOD1 under 7k, LOD2 under 1200.
- Collider budget: convex or compound proxy under 192-200 triangles per interactive family. Hero arches use multiple named compound proxies. Distant HLOD has no collider.
- Static rendering: SRP Batcher compatible shared materials, no material clones, dithered LOD transitions. GPU Resident Drawer/HLOD can be enabled only after platform proof.

The detailed collider and LOD contract is `2005_COLLIDER_LOD_CONTRACTS.csv`.

## Quality Scaling

`GlobalQualityWeight` is continuous. It may change mesh resolution, variant count, density, atlas resolution, HLOD distance, AO rays, and optional detail overlays. It must not change gameplay truth, placement authority, route identity, collider semantics, or DTO layout.

- Low: fewer variants, lower density, lower AO rays, smaller texture arrays, earlier HLOD. Silhouette, wet/dry boundary, and route readability remain intact.
- Middle: full placement rules, normal variant counts, 1K-2K close materials, accepted colliders and LODs.
- High: more variants, higher AO/detail, longer close LOD range, richer decal/mask breakup.
- Ultra: overkill density in allowed bands, higher material detail, stronger shoreline proof shots, but the same owners, families, and gameplay truth.

No low/ultra dichotomy is permitted. The package must degrade through several continuous steps.

## Proof Gates for Later Unity Execution

Minimum proof set:

1. Shoreline close shot: wet/dry outcrop, waterline, foam residue, bright surface lighting.
2. Shoreline wide shot: coastline, sky, ocean surface, Aegir/moons context if visible, no darkness masking.
3. Material flat pass: rocks on neutral lighting, no fog, no water distortion.
4. Final lit pass: same rocks in route context.
5. Shallow underwater shot: 1.5-8 m reef anchors and shelves, no dry coral/kelp.
6. Medium-depth shot: route marker readability without black fog reliance.
7. LOD overlay: LOD0/1/2 transition distances and no popping that breaks silhouette.
8. Collider overlay: proxies named and separated from visual meshes.
9. Scatter validation: zero underwater ecology/reef/seafloor instances above submerged threshold.
10. Low/Middle/High/Ultra comparison: continuous quality consequences, same gameplay truth.

Validation tools to run later:

- `GeologyVertexLayoutValidator`
- `GeologyForgeSelfAudit`
- `WorldProceduralFinalPrefabQualityGate`
- `Tools/GeneratedAssetProductionAudit.py`
- `Tools/MaterialAudit.py`
- Unity Frame Debugger/profiler only in a Unity execution slot, not this task.

## Reject Gates

Reject the package or generated output if any condition is true:

- Any product route uses `WorldProceduralProxy` primitive prefabs.
- Any final prefab uses built-in Unity primitive mesh as production geometry.
- LOD0 mesh is used as collider.
- Material uses Unity `Default-Material` or per-instance clones.
- Wet basalt final claim is based on albedo-only or base/normal-only assets.
- Packed channel order is guessed instead of shader-locked.
- Underwater rules allow `depth == 0` without signed waterline/substrate proof.
- Coral, kelp, reef anchor, seafloor rock, fauna, or underwater safe pocket appears on dry land.
- Surface/coastline proof is dark, muddy, fog-hidden, or grey placeholder terrain.
- "Static manifest exists" is treated as visual proof.
- HLOD coastline mass is inspectable at close range.

## Current Blockers

1. Complete wet basalt/waterline texture stack is missing. Existing candidates are insufficient for final acceptance.
2. Placement rule repair is required before shoreline and underwater scatter can be trusted.
3. Primitive proxy fallback remains enabled in procedural family assets.
4. Existing candidate final prefabs still need Unity-side material, collider, LOD, and visual proof.
5. No Unity proof was produced by this worker by instruction.

## Handoff Output

- Source family matrix: `Docs/Reports/Batch20/2005_ROCK_VARIANT_MATRIX.csv`
- Texture channel contract: `Docs/Reports/Batch20/2005_TEXTURE_CHANNEL_CONTRACTS.csv`
- Collider/LOD contract: `Docs/Reports/Batch20/2005_COLLIDER_LOD_CONTRACTS.csv`
- Execution checklist: `Docs/Reports/Batch20/2005_GENERATION_HANDOFF_CHECKLIST.md`

