# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Texture Generation Style Lock

Status: ACTIVE / PENDING ART QA
Agent: SHINOBU_361
Domain: Echelon 8 Presentation / Tech Art / Static PBR Texture Audit
Prompt source: `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md`
Queue scope: 175 unique texture targets, 413 deficient slot/reference rows collapsed, 238 duplicate target references removed.
Operator playbook: `Docs/Reports/TextureGenerationExecutionPlaybook_SHINOBU_361.md`
PBR/reference execution guide: `Docs/Reports/TexturePBRSetAndExternalReferenceGuide_SHINOBU_361.md`

## Direct Answer

Yes: send visual samples to the image generator. Do not send a random moodboard and do not dump every existing asset into every generation. Use a controlled reference pack so all images share the same material language.

The lock is:

1. One global style reference set for the whole project.
2. One category reference set for the current batch.
3. One same-family approved result after the first good texture in that family exists.

That is enough. More references will average the style into sludge.

Internet references are allowed only below project-owned references. Use them as pre-reference direction for taste, not as source texture ownership. Real subsea habitats, Subnautica concept art, and Pinterest boards can inform the board; approved HECTON outputs still become the stronger reference after the first batch.

## Existing Project References To Use

These files already exist and should anchor the look.

### Global Mood / Planetary Color References

- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png`

Use these for Hecton color temperature: teal/cyan science light, deep ocean blue, opal mineral highlights, amber storm warmth. They are mood and palette references, not hard-surface panel references.

### Flora / Biology References

- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_AlbedoAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_NormalAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_ORMAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_MatCap.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall/albedo___family.kelp.tall.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy/albedo___family.kelp.canopy.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense/albedo___family.kelp.patch.dense.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/albedo___family.coral.branching.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive/albedo___family.coral.massive.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/albedo___family.coral.plate.png`

Use these for biological rhythm, reef color restraint, translucency hints, kelp/coral surface detail, and mask discipline.

### Procedural Flora / Rock Shape References

These are `.asset` and prefab sources, not bitmap prompts. Render preview screenshots before using them as image-generator references.

- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/Kelp/`
- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/TubeCoral/`
- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/PorousRock/`
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/`

Use these as silhouette and family-shape references. Do not feed `.asset` files directly to an image model.

### Material Family Identity References

These materials are identity references and Unity-side assignment anchors. Use rendered previews or their bound textures, not raw `.mat` files, for image generation.

- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_*`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_*`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_*`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_*`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_route_power*`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_*`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_service_scar*`
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_ProceduralBio_Shallows*`

## Style Target

The target is not grimdark. It is expensive abyssal expedition design:

- warm off-white ceramic/composite pressure panels
- satin titanium structural rails
- graphite rubber seals and anti-slip fields
- teal/cyan science accents
- amber locator, safety, and power accents
- restrained coral, violet, opal, and pearl biological colors
- clean mineral dust, salt halos, polished use-wear, and controlled scratches

The image should look like a funded research civilization surviving under an alien ocean. Not horror trash, not black mud, not random sci-fi junk, not military grunge.

## Style Lock Rules

Use the same material grammar everywhere:

- Habitat: off-white composite, satin titanium, graphite rubber, teal/amber accents, curated salt wear.
- Habitat walls: layered system, not repeated tile. Build base pressure skin first, then service/conduit overlay, then separate mounted instruments/tools. Wall albedo must leave calm fields for placed details.
- Flora: pearl membrane, olive/teal kelp, coral/violet biology, wet specular behavior, controlled emissive veins.
- Geology: blue-black basalt only as value base, plus opal mineral veins, pale sediment, turquoise/cyan hydrothermal deposits.
- Sky/celestial: Hecton storm palette from existing planet/cloud textures, not generic space wallpaper.
- Tools/resources/support: inherit habitat materials first, then add a clear gameplay color signal.

Every generation must still obey:

- flat, top-down, orthogonal orthographic view
- completely uniform diffuse lighting
- zero directional shadows
- perfect seamless tiling
- no scene, no perspective object, no dramatic lighting
- no text, no logos, no watermark, no border

## Reference Pack Protocol

## When A Category Has No References Yet

Do not wait for perfect references and do not use random internet filler. Use the manual bootstrap seed pass in `Docs/Reports/TextureReferenceBootstrap_SHINOBU_361.md`.

The rule is simple:

1. Generate 3 candidates from a bootstrap seed prompt.
2. Pick 1 winner as the family reference.
3. Use that winner while generating the real production cards.
4. After 2 strong production textures exist in that family, stop using the seed and use the approved production textures as references.

This is mandatory for weak-reference families: habitat master surfaces, tools, gameplay signal surfaces, resource pockets, visor glass, terminals, and support markers.

### Habitat / Tool / Gameplay Surface Batch

Use:

1. One global mood reference from the planet/cloud set.
2. One approved BLOCKER habitat texture result after it exists.
3. One function-specific reference if available, such as floor panel, wall panel, visor glass, warning stripe, or tool casing.

Until the first habitat texture is approved, use `REF_SEED_001`, `REF_SEED_002`, `REF_SEED_003`, or `REF_SEED_004` from `TextureReferenceBootstrap_SHINOBU_361.md` plus one planetary mood reference. Do not use flora references for habitat except for tiny accent color alignment.

### Flora Batch

Use:

1. `TX_ProceduralBio_Shallows_AlbedoAtlas.png`
2. One matching imported family albedo, such as kelp, coral branching, coral massive, or coral plate.
3. One approved generated texture from the same family once it exists.

Do not mix coral and kelp references unless the prompt is explicitly for a hybrid biome material.

### Geology / Terrain Batch

Use:

1. `surface_diff.png`
2. `surface_norm.png` or `surface_spec.png`
3. A rendered preview from `BioForge/Shallows/PorousRock/` or `Prefabs/Nature/Rocks/ProceduralFinals/`
4. One approved geology output after the first rock texture passes QA.

The geology batch must stay triplanar-safe: no directional cracks that imply one fixed world orientation, no baked shadows, no scenic cliff photos.

### Sky / Celestial Batch

Use:

1. `Aegir_storms.png`
2. `clouds0_diff.png`
3. `surface_diff.png` if a planet surface relationship is needed.

These are allowed to be more painterly than material surfaces, but still must not become random stock space art.

## Full Production List

The full hand-authored target list is in `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md`.

Current production scope:

- 175 unique target textures total.
- 171 textures require `GENERATE_REPLACEMENT_PBR`.
- 4 textures require `REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT`.
- 15 `BLOCKER` targets first: prologue habitat/near-view surfaces and immediate presentation blockers.
- 154 `MEDIUM` targets second: habitat families, flora, geology, resources, support markers, tool states.
- 6 `LOW` targets last: distant/background sky and residual low-risk presentation surfaces.
- Category split: 126 `HABITAT_INTERIORS`, 26 `FLORA_EPIDERMIS`, 23 `GEOLOGY_TRIPLANAR`.

## Required Work Order

### Phase 0 - Build Reference Board

Create one small reference board with:

- 2 global mood refs from planet/cloud textures.
- 2 flora refs from `TX_ProceduralBio_Shallows_*` and imported flora albedos.
- 2 geology refs from planet surface and rendered rock/procedural previews.
- 4 bootstrap seeds if hard-surface references are absent: `REF_SEED_001`, `REF_SEED_002`, `REF_SEED_010`, `REF_SEED_011`.
- 1 empty slot reserved for first approved habitat result.

Do this before generating new production images.

### Phase 1 - Generate The 15 BLOCKER Textures

Use the first 15 cards in `TextureProductionQueue_SHINOBU_361_HANDMADE.md`.

Generate three candidates per target. Pick one winner per target. After the first two good habitat outputs exist, use them as same-family references for the rest of the BLOCKER batch.

Reject outputs with:

- dramatic lighting
- black crushed grime
- generic military sci-fi panels
- random warning text
- perspective objects
- non-tiling seams
- over-busy detail that will shimmer on MX350

### Phase 2 - Generate Flora And Geology

Generate:

- 26 flora targets using flora references.
- 23 geology targets using geology references.

Flora should be attractive and alive, with wet specular structure and restrained bioluminescent veining. Geology should read as premium alien abyssal terrain, not gray noise.

### Phase 3 - Generate Remaining Habitat, Tools, Resources, Support

Generate the remaining habitat/tool/resource/support targets after BLOCKER style is approved. Use accepted BLOCKER surfaces as hard references so the rest of the project inherits the same material taste.

### Phase 4 - Produce PBR Map Set Per Accepted Albedo

For every accepted texture:

- Albedo: BC7, sRGB on.
- Normal: BC5, sRGB off, generated from accepted height/detail source or dedicated normal prompt when the surface needs real depth separation.
- ORM: BC7 or single RGB mask import, sRGB off.
- ORM Red: ambient occlusion.
- ORM Green: roughness.
- ORM Blue: metallic.
- Emissive masks: only for flora veins, UI/support signals, and active power accents. Do not bake brightness into diffuse albedo.

### Phase 5 - Import And Validate

Use:

- `Tools/BatchImportTextures.py`
- `Docs/Reports/BatchImportTextures_SHINOBU_361_import_plan.csv`
- `Docs/Reports/production_texture_manifest.csv`

Then validate in Unity material previews and actual route surfaces. No runtime readiness claim until Unity import, console, Play Mode, Memory Profiler, Frame Debugger, and visual captures exist.

## What Not To Do

- Do not send all 175 prompts to a generator as one giant job.
- Do not mix all references into every generation.
- Do not use random internet sci-fi references as primary style truth.
- Do not use dark horror references to force mood.
- Do not let internet references override approved project refs or generated style anchors.
- Do not accept outputs with beautiful cinematic lighting; these are material source maps, not concept art.
- Do not let the model write labels, numbers, logos, fake UI text, or caution words into texture albedo.
- Do not use pure black as a base material color.
- Do not create separate AO, roughness, and metallic textures when the target is `_ORM`.

## QA Acceptance Gate

An output is accepted only if it passes all checks:

- Looks beautiful as a texture source, not just technically valid.
- Matches the premium abyssal expedition palette.
- Tiles seamlessly in both axes.
- Has no directional light or baked scene shadow.
- Keeps albedo restrained; shine belongs in roughness/specular response, not diffuse.
- Has a clear normal-map plan.
- Has a clear ORM packing plan.
- Can scale down to MX350 without becoming noisy.
- Can scale up to Ultra with extra masks/detail without changing texture identity.
