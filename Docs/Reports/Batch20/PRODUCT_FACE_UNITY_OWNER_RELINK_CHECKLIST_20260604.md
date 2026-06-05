# ProductFace Unity Owner Relink Checklist - 2026-06-04

## Boundary

This checklist is for the later Unity owner. Batch20 source-manifest work did not run Unity, did not run a build, did not import textures, and did not edit active prefab/material/scene/script assets.

Do not start this checklist during an existing Unity import/build/compile pass. Do not run `dotnet build` unless a later task explicitly requires it and CPU/dotnet/csc guards are clear.

## Required Inputs

- Accepted source packages matching `Docs/Reports/Batch20/product_face_source_manifest_draft_20260604.csv`.
- Production ingestion manifests created from the draft:
  - `Assets/_Project/Data/ProductFace/TextureIngestion/product_face_texture_source_manifest.csv`
  - `Assets/_Project/Data/ProductFace/TextureIngestion/product_face_texture_manifest.csv`
- Import settings for albedo, normal, and packed masks declared before import.
- One scoped Unity owner. No parallel prefab relink agents.

## Preflight Gates

- Verify every manifest row has an accepted owner, shader route, source package path, and channel contract.
- Reject any row whose source package writes directly into prefab bindings.
- Reject any source derived from package/default `Lit.mat`, Unity primitives, placeholder flat materials, unlicensed decal sheets, or route donors without owner approval.
- Confirm `Hecton_ToolDecayLit` tool body rows use `PackedMaskV1`, not MRAO or ORM.
- Confirm `Hecton_MraoAtlasLit` rows use MRAO, not ToolDecayLit packed masks or ORM.
- Confirm `Hecton_ProceduralBio` rows use ORM, not MRAO.
- Confirm `SuitVisor` rows use visor-specific masks only.
- Confirm transport glass/lens rows remain blocked until their shader channel contract exists.
- Confirm sky/ocean rows are route-owned and not ProductFace PBR donors.

## Import Settings

- Albedo/color maps: sRGB on.
- Normal maps: texture type Normal Map, platform compression set to normal/BC5-equivalent where available.
- Packed masks: sRGB off, no normal import, no color-space conversion.
- Decal/alpha masks: sRGB off unless the declared shader contract explicitly says color.
- Source files stay outside runtime-visible import paths unless owner-approved.

## Material Creation

- Create ProductFace-owned material assets only after source package validation.
- Set the shader first, then assign textures according to the declared contract.
- Never use a texture map packed for one shader family in another family.
- Keep material names route-specific and explicit, for example `MAT_PF_Tool_Builder_Body_ToolDecayLit`.
- Do not mutate package materials.
- Do not overwrite third-party materials in place.

## Relink Scope

### Tools

Relink held and world renderers for exactly these body roles:

- `Tool_BeaconDeployer`
- `Tool_Builder`
- `Tool_EnvAnalyzer`
- `Tool_Flashlight`
- `Tool_HarpoonLauncher`
- `Tool_Knife`
- `Tool_LaserCutter`
- `Tool_Propulsion`
- `Tool_Repair`
- `Tool_SalvageSampler`
- `Tool_Scanner`
- `Tool_StunPistol`

Rules:

- Body material must be `Hecton_ToolDecayLit`.
- `ToolScreenDiegetic`, cone, beam, projection, tether, spark, and decal materials are support lanes only.
- Capture held and world proof for each tool class.

### Resources

Relink or classify these pickup rows:

- `CopperOre`
- `FiberKelp`
- `HydrocarbonResin`
- `MembraneTissue`
- `SilicaShards`
- `SilverOre`
- `SulfurClumps`
- `TitaniumScrap`
- `Item_Titanium` legacy alias/quarantine decision

Rules:

- Mineral/scrap rows use MRAO unless a dedicated pickup shader supersedes it.
- Organic rows use ORM if `Hecton_ProceduralBio` is approved.
- `Item_Titanium` cannot become a separate independent visible material without owner approval.

### Transport

Relink these vehicle routes only after material slot classification:

- `CargoSled`: hull, rubber grip, label trim.
- `ExosuitFrame`: frame, rubber seals, signal labels.
- `MicroSub`: hull, trim; glass remains blocked until glass shader contract exists.
- `ScoutGlider`: body, grips/rubber; lens/signal remains blocked until slot split or combined shader contract exists.

Rules:

- Runtime proof shell materials are not final ProductFace sources.
- Glass/lens cannot be faked through generic transparent Lit.

### Player

Relink these player roles only after player owner approval:

- First-person gloves/forearms.
- Torso/pelvis/legs/fins.
- Helmet/visor housing.
- Visor glass through `SuitVisor`.
- Labels/latches/instrument trims.

Rules:

- `MAT_PlayerSwimBlockout` is not an acceptable final ProductFace material.
- Visor glass requires fingerprint, visor wear, scratch normal, runoff normal, and droplet masks according to `SuitVisor`.
- Primitive sphere visor proof does not count.

### Sky / Ocean

Handle as route-owned proof and cleanup, not ProductFace PBR relink:

- Sky dome clouds.
- Sky dome source mesh.
- Aegir gas giant disc.
- Moons/celestial bodies.
- Surface ocean first-party candidate versus active Crest route.
- Foam/waterline ribbons.
- Crest hidden inputs.
- Photic shallows clarity.

Rules:

- Surface, sky, Aegir, moons, ocean surface, and photic shallows must stay bright/readable and premium.
- Darkness cannot hide weak sky or water art.
- Crest hidden-input materials require Frame Debugger proof that they are not visible fallback art.

### Construction / Debris / Ruins

Classify and relink only approved rows:

- Pressure module panel shell.
- Pressure door gasket/latch.
- Service pipe/cable bundle.
- Titanium ruin scrap panel.
- Concrete/basalt ruin chunk.
- Ruins service-label decal atlas.

Rules:

- `Buildings/Cube.prefab` third-party checker material must be classified, replaced, or quarantined by the construction owner. No blind deletion.
- `STRUCTURES.prefab` package/default Lit usage must be replaced only after slot ownership is known.
- Terrain, transport, and resource materials are not automatic construction donors.

## Proof Required

- Static material matrix after relink showing no default/package Lit or placeholder material in ProductFace-owned rows.
- Primitive/default-material validator output after relink.
- Texture slot audit proving albedo, normal, and packed masks are assigned to declared shader slots.
- Screenshot or capture proof for:
  - held tools,
  - world tools,
  - resource pickups,
  - four vehicles,
  - player FP gloves/visor/suit/fins,
  - sky/Aegir/moons/ocean/photic shallows,
  - construction/debris/ruins.
- Frame Debugger proof for hidden Crest input materials if they remain.

## Fail Gates

- Any ProductFace row still using package/default `Lit.mat`.
- Any ProductFace row still using `Mat_Tool_*_Placeholder`, `Mat_Resource_*` flat shells, `MAT_PlayerSwimBlockout`, or prototype checker material as final visible art.
- Any direct AITexture-to-prefab binding.
- Any MRAO/ORM/PackedMaskV1 channel swap.
- Any transport glass/lens row relinked without an approved channel contract.
- Any sky/ocean route using darkness or fog to hide weak surface art.
- Any low-tier result that looks flat, muddy, primitive, or placeholder.

## Quality Consequences

GlobalQualityWeight can scale imported resolution, secondary decal density, detail-normal usage, texture streaming priority, proof cadence, and optional validation strictness.

It must not change:

- gameplay truth,
- material channel semantics,
- item identity,
- save identity,
- DTO layout,
- prefab ownership,
- route authority.

Low, Middle, High, and Ultra must all preserve the same shader slots and relink targets.

