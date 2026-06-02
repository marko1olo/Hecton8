# HECTON-8 Rendering Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: URP, RenderGraph, lighting, shadows, fog, water presentation, VFX, shaders, GPU budgets, post-process, and render proof.

## Prime Law

Rendering carries most of HECTON-8's realism. It must make darkness, pressure, water, corrosion, glass, instruments, silt, and scale feel expensive without turning MX350 into a slideshow. HECTON-8 rejects pretty post-processing that hides weak composition, unbounded volumetrics, material clones, generic blue sci-fi grading, and render features without proof.

The render path is a visual fake engine. It sells believable consequences faster than physical simulation.

## Truth Ownership

Rendering owns visibility, material response, light/fog/VFX presentation, render feature scheduling, GPU resource proof, and screenshot/capture truth. It does not own gameplay pressure, water fill, AI state, vehicle state, save state, or interaction truth.

Gameplay owners publish stable snapshots. Rendering consumes them and produces visuals. A render feature must never invent a state that the owning system did not publish.

## URP And RenderGraph Law

New runtime rendering work must use URP with RenderGraph enabled. Compatibility Mode is legacy debt for migration only.

Required:

- declared texture and buffer reads;
- declared writes;
- named profiler pass;
- Frame Debugger or RenderGraph Viewer proof for new passes;
- MX350 timing proof when runtime rendering changes;
- load-shed path when a pass exceeds budget.

Forbidden:

- `Graphics.Blit` in new runtime render paths;
- hidden `ScriptableRenderPass.Execute` chains;
- untracked command-buffer target writes;
- material instantiation in render sync;
- post stacks with no gameplay/readability value.

## Noir Color Doctrine

Pure black is forbidden on scene geometry. Black water needs structure. The minimum abyssal floor luminance must preserve silhouette, route, and instrument readability.

Palette anchors:

- abyssal floor: almost black green/blue, never void;
- silt grey: suspended particulate and depth fade;
- dirty cyan: instruments, sonar, system truth;
- decay amber: warnings, heat, oxidized service lights;
- bioluminescent teal: ecology, threat, contamination, route clue.

Color is semantic. Cyan cannot mean every interactive thing. Amber cannot be random decoration. Bioluminescence must imply biology, contamination, route, threat, or evidence.

## Fog And Water

Fog is not a blanket. It stages information.

Rules:

- low tier uses depth fog, LUT haze, dither, baked AO, and silhouette composition;
- raymarching is middle/high/ultra only after proof;
- fog density varies by depth, biome, silt, current, and interior/exterior state;
- caustics are allowed only where light physically has reason to exist;
- underwater visibility must preserve one readable route cue.

Reject empty black screens, bright aquarium haze, and fog used to hide bad assets.

## Lighting And Shadows

Darkness is default. Light is an expensive exception.

Required:

- light registry or owner route;
- range and intensity caps;
- eligibility for shadow casting;
- dark volumes for caves/interiors;
- baked AO and material masks before expensive screen effects;
- emissive proxies for non-critical far lights;
- player flashlight and navigation-critical lights prioritized.

MX350 rejects full volumetrics and per-fragment voxel GI as default. Dynamic shadows are budgeted, staggered, and demoted when they stop affecting player decisions.

## VFX

VFX is presentation, not physics truth.

Allowed default tools:

- GPU particles with hard pool caps;
- screen-space depth fades;
- authored flow masks;
- shader wobble;
- impostor drift;
- event-driven emitters;
- VAT debris or fluid hints;
- low-cadence probe approximation.

Forbidden:

- CPU particle state reads except diagnostics;
- full bubble/debris/snow simulation as default;
- particle overflow stalls;
- VFX that implies gameplay truth not present in systems;
- constant decorative noise over the whole screen.

## Shader And Material Discipline

Shaders must be shared, variant-bounded, and authored around HECTON-8 material truth.

Rules:

- all material data in SRP Batcher-compatible constant buffers;
- limited keyword count;
- global quality branches for non-critical features;
- channel-packed PBR masks;
- no per-object shader copies;
- no runtime texture compression;
- no separate AO maps when MRAO packing applies;
- dithered crossfade instead of alpha blend for dense props.

Generated assets must obey `3dmodel.md` and `PROCEDURAL_ASSET_PIPELINE.md`; rendering does not rescue broken topology.

## Performance And Proof

Every new render feature needs:

- target tier;
- render target format;
- resolution;
- pass cost;
- memory cost;
- overdraw risk;
- load-shed behavior;
- screenshot proof;
- Frame Debugger or RenderGraph proof;
- profiler proof when runtime code changed.

A pass over `0.1 ms` is suspicious until proven necessary and scalable.

## GlobalQualityWeight Scaling

Compact: depth fog, LUT haze, baked AO, dither, impostors, HLOD cards, low shadow count, static atlases.
Middle: stronger local lights, richer silt, limited raymarch zones, better material normals.
High: denser VFX, better shadows where readable, longer HLOD residency, more reactive screen materials.
Ultra: sensory overkill through richer near-field detail, volumetric zones, better water layers, and cinematic captures, without changing gameplay truth.

`GlobalQualityWeight` may scale render scale, local light count, shadow eligibility, volumetric zone quality, fog sample count, decal density, shader feature depth, VFX density, HLOD distance, and capture polish. It must not change gameplay truth, sensed truth, UI command semantics, save identity, material identity, or platform proof state.

## Rejection Gates

Reject:

- generic blue/purple sci-fi grade;
- bloom as visual identity;
- full volumetric default on MX350;
- material clones;
- unbounded transparent overdraw;
- UI damaged by post dither;
- shader variants without budget;
- render features with no proof artifact;
- screenshots that hide bad models behind darkness.

## Acceptance Sentence

Rendering is accepted only when it preserves route readability, sells pressure and material truth, scales continuously, proves its cost, and makes HECTON-8 look expensive through controlled fakes instead of brute-force effects.
