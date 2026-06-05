# HECTON-8 Shader And Material Runtime Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: URP shaders, Shader Graph/HLSL, material variants, shader keywords, SRP Batcher, GPU instancing, triplanar materials, wetness, corrosion, flora sway, UI shaders, and shader proof gates.

## First-20 Route Hook

- First-20 moment: world load, first exit, resource pickup, tool interaction, and first hazard response need material truth for wet rock, ocean-facing geometry, instruments, tools, damage, biolum cues, and readable shallow-route assets.
- Route blocker removed: prevents opening-route visuals from relying on generic glow, material clones, random noise, or shader tricks that hide primitive meshes and missing PBR identity.
- Proof class: STATIC_DOC only; route acceptance still requires compact/high captures, Frame Debugger or RenderGraph proof for runtime rendering changes, shader variant proof, and GPU/profiler evidence for costly features.

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

## 2026-06-05 Static Source Anchors

Evidence class: STATIC_SOURCE only. Compile, Unity import, Frame Debugger, GPU profiler, GC, screenshot, and player-build proof remain PENDING VERIFICATION.

| Runtime | Owner / boundary | Static data route | GlobalQualityWeight consequence | Missing proof |
|---|---|---|---|---|
| `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs` | `Hecton8.Graphics.Materials`, `SystemID.GraphicsMaterials`; material-aging owner for pressure corrosion, scorch, deformation, temperature mirroring, and UberNoir degradation. It does not own hull truth, thermodynamics truth, or gameplay damage. | Registers dispatcher phase adapters for PreSimulation, Simulation, PostSimulation, and VisualSync. Owns DataVault buffers `VisualPressureAging*`, `UberNoirInstanceDegradation`, and 300-frame telemetry rings; borrows `ThermodynamicsTemperatureFrontMirror` and `StructuralIntegrity*` buffers. Uploads double-buffered `GraphicsBuffer` data into shader globals `_GlobalBaseAgingParams`, `_GlobalBaseAgingRuntime`, `_GlobalUberNoirDegradation`, and `_GlobalUberNoirDegradationRuntime`. | Reads global quality and scales sample/update budget and visual degradation cadence. It must not alter structural authority, DTO layout, save identity, or damage truth. | No visual capture, Frame Debugger, GPU/profiler, GCMonitor, Unity import, or runtime dump artifact was provided by this static audit. |
| `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs` | `Hecton8.Graphics.Materials`, `SystemID.GraphicsMaterials`; material-response owner for biome/pressure/wear/material state presentation. It does not own inventory, ecology, terrain, or damage authority. | Registers PreSimulation, Simulation, PostSimulation, VisualSync, and cold tick. Owns DataVault buffers `ShinobuMaterialStates`, `ShinobuMaterialPowers`, `ShinobuMaterialVisiblePayload`, `ShinobuMaterialConstants`, `ShinobuMaterialWearRates`, texture mappings, CSV scratch, and a 300-entry telemetry ring. Uses double-buffered `GraphicsBuffer` routes for `_H8UberNoirMaterialStates` and `H8UberNoirMaterialGlobals`. | Reads `HomeostasisBrain.GlobalQualityWeight`; scales simulation budget, cadence, telemetry sampling, shader quality weight, triplanar pixels, texture array memory, and moss-layer cost. Material identity and gameplay truth must remain stable. | No compiled shader variant count, SRP Batcher/instancing proof, GPU/profiler capture, GCMonitor, Unity import, or runtime visual artifact was provided by this static audit. |

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
