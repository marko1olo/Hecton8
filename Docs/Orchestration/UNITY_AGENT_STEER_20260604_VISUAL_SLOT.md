# Unity Agent Steer 2026-06-04 Visual Slot

Target thread/agent: `Продолжить работу по логам`

Use this as a direct steer prompt when the Unity owner is active or when it reports a visual pass.

## Steer Prompt

You are still the single Unity owner. Do not stop at "it works"; prove the scene visually.

Fresh orchestrator screenshot inspected:

- `Docs/Orchestration/Captures/unity_focus_state_20260604_125701.png`

Verdict from that shot: improved over the black/noir failure, but still below the floor.

- Coast/island still reads grey, barren, procedural, and under-detailed.
- Water reads as a dark green flat sheet in the mid/far view, not a rich surface with shoreline transparency/contact logic.
- Aegir reads as a pale translucent disc/sticker with weak atmosphere integration, not a huge believable methane-rich gas giant behind the horizon.
- Sky is brighter, but cloud/Aegir/water/coast composition is still not premium.
- There is no visible Subnautica-level photic shallows proof in that frame.
- This frame is not an acceptance capture. It is a critique target.

Current hard rejects:

- Do not place primitive rocks, primitive seaweed, cube/sphere/plane scatter, or random decorative objects on land or shore.
- Seaweed on dry land is rejected unless it is explicitly drift/wrack with correct wet/dry context, material, scale, and shoreline logic.
- Grey striped barren island is rejected. It needs wet basalt breakup, strata, erosion, foam contact, shallow transparency, alien photic biota, and occasional industrial/colony remnants.
- Do not use darkness, fog, bloom, or postprocess to hide weak terrain, bad materials, empty coast, or primitive meshes.
- Aegir cannot look like a sine stripe ball, sticker, washed translucent decal, cyan rim object, or separate sky layer. It must sit behind the horizon and be occluded by atmosphere/haze, not by cutting the planet texture.

Use existing/editor-time asset systems before inventing primitives:

- BioForge: `BioForgeWindow`, `BioForgeGenerator`, `ShallowsBioForgeBatchBaker`.
- Geology Forge: `GeologyForgeWindow`, `GeologyForgeGenerator`, `GeologyForgeSelfAudit`, `RuntimeMeshGenerationScanner`, `AbyssalGeologyStudio1606`, `RockSculptorEngine1713`.
- Flora topology/finalization: `FloraTopologyStudio1604/1711`, `WorldProceduralFlora*Authoring`.
- Terrain/biome bakes: Topography Forge, Hydraulic Erosion Forge, Biome Splatmap Forge, Ecosystem Density Forge, Static SDF Forge.

Required proof captures, saved outside `Assets`:

1. Game view, same surface framing as latest pass, UI on.
2. Same Game view, UI off.
3. Matching Scene view from same position, gizmos off.
4. Regression angle for old white ocean quads/ribs.
5. Regression angle for low-oblique ocean plane artifacts.
6. Shoreline close shot 1-2 m above water: foam, wet rock, shallow substrate, real material breakup.
7. Underwater 0-5 m: surface underside, shore shallows, caustics/refraction, readable beauty.
8. Underwater 20-50 m photic route: route cue, particles/silt, terrain silhouettes, biota density.
9. Aegir long shot and crop: no rim/seam/sticker edge, correct atmospheric occlusion.
10. 360 sky pan: prove no vertical seam, pano pole column, or broken cloud layer.

Acceptance:

- Surface, sky, Aegir, coastline, ocean surface, photic shallows, and medium-depth route must be bright, legible, beautiful, and Subnautica-level or better.
- Darkness belongs to depth, caves, interiors, storms, pressure events, and eclipse windows only.
- Low/Compact quality may reduce density/cadence/resolution but must not become flat, muddy, primitive, or unreadable.
- High/Ultra quality must spend saved performance on visible richness: shoreline material variation, foam/contact detail, clouds/Aegir integration, underwater particles/biota, not a different gameplay truth.
- Do not mark a pass accepted without screenshot paths and a short critical verdict against the mandatory reference images.

If screenshot tools save to `Assets/Screenshots`, fix the route first or move files immediately to `Docs/Screenshots` / `Docs/Orchestration/Captures` before Unity import loops. Prefer screenshot capture paths outside `Assets`.

Scatter-specific correction now has static proof:

- `rule.coral.reef`, `rule.kelp.starter`, `rule.rocks.floor`, `rule.rocks.cluster`, `rule.coral.branching`, `rule.coral.low`, `rule.kelp.canopy`, `rule.kelp.patch.dense`, `rule.kelp.tall`, and `rule.pocket.safe` have dry-land/proxy risk classes.
- Dry terrain can become depth `0` through `WorldProceduralFieldSampler`, so underwater rules cannot accept depth `0` unless they are explicit wet/intertidal species with proof.
- Coral/kelp/seafloor rocks need positive water-depth/substrate/seafloor gates and final-ready variants in visible routes.
- Proxy/placeholder primitive variants are forbidden in visible shoreline/photic content.
