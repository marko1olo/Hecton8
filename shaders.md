# HECTON-8 Shader And Material Runtime Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: URP shaders, Shader Graph/HLSL, material variants, shader keywords, SRP Batcher, GPU instancing, triplanar materials, wetness, corrosion, flora sway, UI shaders, and shader proof gates.

## Prime Law

Shaders sell material truth. They do not rescue bad meshes, fake systems, or broken art direction.

Every shader must serve a material family or readable state: corroded metal, scratched glass, wet rubber, pressure ceramic, abyssal tissue, rock strata, sonar screen, silt volume, or diegetic UI. HECTON-8 rejects generic sci-fi glow, random noise, material clones, unbounded variants, and expensive effects without ownership.

## Truth Ownership

Shaders own visual material response, vertex deformation presentation, mask interpretation, quality branches, and GPU resource use. They do not own gameplay truth. A shader may display wetness, stress, biolum phase, scan confidence, or damage only from authored data, baked mesh channels, material parameters, or owned snapshots.

Generated assets must obey `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, and `PROCEDURAL_ASSET_PIPELINE.md`; shader tricks cannot excuse primitive silhouettes, missing bevels, broken UVs, or absent LOD/collision proof.

## Material Data Rules

Required:

- SRP Batcher-compatible constant buffer layout;
- shared materials plus MaterialPropertyBlock or GPU instance data;
- channel-packed masks where possible;
- bounded shader keyword count;
- no per-object material instantiation in gameplay;
- no runtime texture compression or import changes;
- finite parameter ranges with NaN/Inf guards;
- documented UV set, vertex color, and texture channel semantics.

## Shader Families

Core families:

- hard surface: bevel-aware normals, MRAO, edge wear, rust, salt, wetness;
- geology: triplanar strata, cracks, mineral veins, sediment, dampness;
- flora/coral: vertex color sway/amplitude/phase/AO, biolum masks, tissue translucency fakes;
- fauna: skin wetness, scars, pressure discoloration, eye/biolum logic, VAT support;
- water/silt: depth fog, caustic fake, particulate, wet boundary;
- UI/screen: scanline, glass dirt, CRT/terminal response, zero-lie state colors;
- damage: decal blend, scorch, pressure cracks, emergency pulses.

Each shader family must list texture slots, mask channels, quality features, and fallback.

## Variant And Performance Law

Forbidden:

- keyword explosion for every asset variant;
- dynamic branching on gameplay truth that belongs in CPU snapshots;
- full-screen expensive shader effects without proof;
- transparent overdraw as default material strategy;
- UI damaged by world post effects;
- duplicated shaders for minor color differences.

Use continuous `GlobalQualityWeight` for optional features. Use variants only for major platform or pipeline differences that cannot be expressed safely at runtime.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale normal detail, parallax depth, wetness layers, silt distortion, biolum secondary pulses, vertex deformation richness, UI glass response, decal blend complexity, and diagnostic overlays. It must not change gameplay truth, material identity, save data, or collision/interaction results.

Compact keeps shared shaders, MRAO, essential normals, simple wetness, vertex-color deformation, and no expensive parallax. Middle adds richer normals and local state effects. High adds selective parallax/secondary masks. Ultra adds hero-only material overkill after proof.

## Production Packet

Any shader, material runtime, keyword, variant, or material-data change must declare:

- shader family and material identity;
- texture/channel contract;
- keyword and variant count;
- SRP Batcher and instancing compatibility;
- material property source and owner;
- `GlobalQualityWeight` feature scaling route;
- Compact and High captures with material proof;
- Frame Debugger/GPU/profiler proof for costly features.

A shader that hides bad topology, clones materials, or changes gameplay perception without owner state is rejected.

## Proof Artifacts

Shader work must provide:

- shader family and material contract;
- texture/channel map;
- keyword list and variant count;
- SRP Batcher/instancing compatibility note;
- compact-tier screenshot;
- Frame Debugger or RenderGraph proof if runtime rendering changed;
- GPU/profiler proof for costly features;
- fallback path for compact hardware.

## Rejection Gates

Reject:

- shader used to hide bad mesh generation;
- material clone per object;
- unbounded keywords;
- random noise as material identity;
- baked lighting inside generated albedo;
- shader state that lies about gameplay;
- "looks good" without flat-light/material proof.

## Acceptance Sentence

Shaders are accepted only when they express real material/state data, remain shared and bounded, scale continuously, prove GPU cost, and make generated assets look authored rather than disguised.
