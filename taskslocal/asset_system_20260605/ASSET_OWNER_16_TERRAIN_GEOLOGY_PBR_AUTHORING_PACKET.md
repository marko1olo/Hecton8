# Asset Owner 16 - Terrain / Geology PBR Authoring Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`.
Write scope: future terrain/geology texture, material, importer, and proof execution only.
Route scope: first exit, photic shallows, and medium-depth hero route terrain/geology.

No Unity run, import edit, material edit, prefab edit, scene save, build, Play Mode, profiler capture, Frame Debugger capture, Addressables build, or `Assets/` mutation is claimed by this packet.

## Mandates Followed

- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## Route Blocker

Terrain/geology material work is blocked by source mixing and missing proof. Existing wet basalt, shell/sand, gravel, rocks, and generated geology sources can guide authoring, but none can be promoted to the first-exit, photic, or medium-depth route until the owner produces route-owned PBR maps, import readback, material slot readback, route screenshots, memory/residency proof, and visual rejection evidence.

Current high-risk source routes:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt`: active and visible static users exist; importer, material, screenshot, and residency proof required.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green`: active and visible static users exist; role split and route screenshot proof required.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand`: active and visible static users exist; PBR channel and mips proof required.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/gravel`: active and visible static users exist; route role and material proof required.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/rocks`: active and visible static users exist; geology role and material proof required.
- `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)`: terrain/geology P0 source folder with visible/proxy risk; reject visible route use until final non-proxy material readback exists.
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429*`, `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102`, and `Docs/GeneratedAssets/Gemini/*`: source/reference only; direct import as route art is rejected.

## First-20 Route Target

This packet maps one blocker on the first-20 route: the first exit and shallow swim cannot rely on muddy scanned tiles, proxy materials, source-only generated maps, or dark grading to hide weak terrain. The terrain/geology owner must produce wet, readable, stratified, material-rich rock/sediment surfaces for:

- first exit shoreline and waterline rock;
- photic shallow substrate and shell/sand patches;
- route ledges, shelves, collapsed faces, and cave-mouth geology;
- medium-depth twilight rock and sediment surfaces where route readability still matters.

## Authoring Stack Required

Each terrain/geology material family must define:

- `TX_[Family]_[Variant]_Albedo`: base color only, no baked directional light, no fake shadows, sRGB import.
- `TX_[Family]_[Variant]_Normal`: tangent-space normal or triplanar-compatible normal detail, linear import, normal map type.
- `TX_[Family]_[Variant]_MRAO`: packed linear mask, shader contract stated before binding.
- Optional `TX_[Family]_[Variant]_Detail`: shared high-frequency detail normal/noise for near-field surfaces.
- Optional `TX_[Family]_[Variant]_Decal`: waterline erosion, mineral stains, sediment streaks, chipped edges, shell fragments, vent staining, or route-specific breakup.
- `MAT_[Family]_[Variant]`: shared material asset only; no runtime material clone route.

Material families must be scale-calibrated:

- wet basalt shoreline: wet edge sheen, chipped fracture normals, waterline erosion, sediment in cavities, roughness variation, non-metallic base.
- photic shell/sand substrate: shell fragments, ripple normals, light sediment color, low metallic, cavity AO, no aquarium-flat tint.
- medium-depth rock: subdued but readable strata, mineral staining, fracture planes, sediment streaks, no crushed black albedo.
- ore/geology accents: localized metallic inclusions only where the material truth supports it.

## PBR Channel Rules

Default MRAO route:

- R = metallic.
- G = roughness or smoothness according to the shader manifest; do not guess.
- B = ambient occlusion.
- A = emission, wetness, or family mask only when the shader contract states the meaning.

Hard rules:

- Albedo is color only; baked highlights and shadows are rejected.
- Normal maps must show usable fracture, sediment, shell, pore, or chipped-edge signal; accidental flat normals are rejected.
- Metallic may appear only in ore/mineral/industrial inclusions, not full wet rock.
- AO must be cavity-biased, not random dirt.
- Roughness/smoothness must carry wetness, sediment, exposed edge, and mineral state.
- Emission is rejected for ordinary terrain unless tied to vents, hot cracks, biological staining, or explicit route evidence.
- Separate AO/roughness/metallic textures are authoring intermediates only unless a shader contract requires them.

## Tiling, Detail, Decal, And Projection Routes

Use one of these routes per family:

- Tileable PBR stack for broad basalt, sand, gravel, and sediment fields.
- Triplanar/object projection for large irregular geology, cliffs, cave mouths, and non-unique rocks.
- Unique bake only for hero cave entrances, landmark rocks, interactable mineral nodes, or close-camera route pieces.
- Decal overlay for waterline erosion, wet contact, sediment streaks, mineral veins, chipped ledges, and traversal marks.
- Shared detail normal for compact near-field richness without exploding material count.

Required gates:

- 2x2 tile seam check for tileable sources.
- Mip preview check for dark seams, ringing, and lost shell/mineral detail.
- Normal/detail overlay check under neutral, grazing, and shallow-water lighting.
- Decals must be material-semantic, not random grunge.
- Triplanar scale must stay consistent across LODs and route pieces.

## MapMagic / Terrain Restrictions

- Terrain source shape, masks, scatter eligibility, and traversal classification remain terrain-owner facts.
- MapMagic interaction must route through approved bridge ownership; direct MapMagic API use is rejected.
- Do not change terrain chunk size at runtime.
- Do not use `Terrain.SampleHeight`, `Terrain.GetHeights()`, or runtime heightmap pulls as authoring shortcuts.
- Do not raw-patch terrain/material/scene YAML.
- Do not assign proxy or source-only material families into visible route terrain.
- Terrain textures must not create gameplay truth. Collision, traversal, resources, and save identity remain owned by their systems.

## Importer Gates

Before route material binding, produce import readback evidence for each texture role:

- albedo: sRGB enabled, compressed high quality, mips enabled for world use, streaming mips policy stated;
- normal: normal map type, sRGB disabled, BC5 or platform equivalent, mips enabled;
- MRAO/mask/detail: sRGB disabled, linear import, compressed high quality, channel contract documented;
- decal/contact masks: linear unless the shader contract requires color sampling;
- platform max size stated for compact, middle, high, and ultra lanes;
- generated/source-only `Docs/GeneratedAssets` files are not imported as final route art without a route-owned cleanup output and importer proof.

## Material Readback Gates

The future owner must capture material slot readback for:

- terrain layer or renderer material using basalt/sand/gravel/rock sources;
- shader name and material asset path;
- texture property names and bound texture paths;
- MRAO G-channel meaning;
- triplanar/detail/decal toggles and scalar values;
- SRP Batcher compatibility risk;
- static/proxy material references that must be removed from visible route use.

Static user rows are evidence of reachability only. They do not prove active renderer use, shader effect, route visibility, import settings, material correctness, or visual quality.

## Screenshot And Contact-Sheet Gates

Required visual proof before route promotion:

- contact sheet: albedo-only, normal-only, MRAO channel split, mips, and 2x2 tile preview;
- neutral URP material ball or plane preview;
- bright first-exit route screenshot from gameplay height;
- photic shallow screenshot with water/terrain contact visible;
- medium-depth route screenshot with landmark and traversal surface visible;
- compact-lane screenshot preserving silhouette, material identity, and route readability;
- high/ultra lane screenshot showing richer detail, not different terrain truth;
- screenshot angle must include traversal and return-path cues, not a cropped beauty-only view.

Reject screenshots that hide weak terrain behind darkness, fog, bloom, post-process, water blur, or distant framing.

## Visual Rejection Gates

Reject terrain/geology material work if any of these appear:

- muddy albedo, repeated ridges, random scanned tile noise, baked light, or fake cast shadows;
- flat sand/rock color without roughness, normal, and AO identity;
- smooth procedural blobs, toy cliffs, low-poly filler, or aquarium substrate;
- proxy/placeholder material in visible first-exit, photic, or medium-depth route content;
- source-only generated texture used as final art;
- broad terrain that becomes dark/noir in surface or photic zones;
- compact lane that loses wet material read, shell/sediment detail, route silhouettes, or waterline breakup;
- high/ultra lane that spends budget on density while leaving material truth weak.

## Memory / VRAM / Streaming Gates

Future import and binding work must report:

- source texture size and imported size per role;
- texture compression format and mip count;
- streaming mips enabled/disabled per role and reason;
- Addressables group/label/owner route before broad runtime residency is claimed;
- texture memory delta after import/binding;
- route scene texture memory and total reserved memory before gameplay start;
- async upload budget lane used by bootstrap, with no per-frame buffer/time-slice changes;
- compact lane texture budget pressure against the 900 MB texture budget and 1800 MB VRAM ceiling;
- rule for mip downgrade when residency pressure crosses the project threshold.

No static matrix can prove residency safety. It only identifies risk and required owner proof.

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` may scale texture size, mip bias, decal density, detail-normal intensity, triplanar octave/detail count, HLOD/material residency distance, and optional near-field geology dressing.

It must not scale or mutate terrain truth, biome identity, resource identity, collision, traversal classification, save identity, shader channel semantics, or material ownership route.

- Low/compact: compressed maps, baked AO, conservative normal strength, clear wet material identity, strong silhouettes, and readable shoreline/photic route. No flat fallback.
- Middle: route-owned PBR stacks, stable material slot readback, controlled decals, and conservative streaming behavior.
- High: richer detail normals, wet-edge masks, more decal breakup, longer LOD residency, and stronger stratification only after memory/render proof.
- Ultra: hero-only layered geology detail, denser route dressing, and visual overkill after memory and render proof; gameplay truth and material route do not change.

## Execution Order For Future Owner

1. Select terrain/geology families for wet basalt shoreline, photic shell/sand, gravel/rock, and medium-depth stratified geology.
2. Separate active `Assets` candidates from source-only `Docs/GeneratedAssets` candidates.
3. Produce cleaned route-owned albedo, normal, MRAO, optional detail, and optional decal sources.
4. Run tile seam, histogram, baked-light, normal, MRAO channel, mip, and compression preview checks.
5. Import through Unity with role-correct settings and capture importer readback.
6. Bind to shared `MAT_*` terrain/geology materials without runtime clones.
7. Capture material slot readback and remove proxy/placeholder route references.
8. Capture contact sheets and route screenshots for compact plus high/ultra lanes.
9. Capture Frame Debugger/Stats and memory/residency evidence when visible route rendering changes.
10. Record failures and rejected candidates so weak source files are not recycled into future route art.

## Regression Model

- CPU: static packet only; no runtime CPU change.
- GC: static packet only; no hot-path claim.
- Memory/VRAM: source and residency risks mapped; no import or residency proof claimed.
- Cadence: no runtime cadence changed.
- Correctness: future owner route is narrower; false promotion risk remains open until PBR roles, importer readback, material readback, screenshots, memory proof, and rejection evidence exist.

Final status: `PENDING_VERIFICATION`.
