# HECTON-8 Rendering Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: URP, RenderGraph, lighting, shadows, fog, water presentation, VFX, shaders, GPU budgets, post-process, and render proof.

## Prime Law

Rendering carries most of HECTON-8's realism. It must make darkness, pressure, water, corrosion, glass, instruments, silt, and scale feel expensive without turning MX350 into a slideshow. HECTON-8 rejects pretty post-processing that hides weak composition, unbounded volumetrics, material clones, generic blue sci-fi grading, and render features without proof.

The render path is a premium presentation engine. It sells believable consequences faster than physical simulation.

## Surface And Celestial Brightness Boundary

Noir darkness is a depth, cave, storm, interior, and pressure-event tool. It is not the default grade for surface water, coastline, sky, Aegir, moons, or photic shallows.

Surface rendering must preserve daylight or motivated celestial/atmospheric light, readable terrain material, ocean color, sky gradients, cloud structure, and gas-giant/moon texture detail. Auto exposure, LUTs, fog, post-process, and tone mapping must not crush the surface into muddy darkness.

Compact may reduce reflection resolution, cloud layers, distant detail, and secondary shafts, but it must still look intentionally beautiful. High and Ultra use the budget for richer atmosphere, volumetric shafts, cloud depth, reflections, foam, wet material response, and celestial texture detail.

Surface, photic-shallow, and medium-depth hero-route captures must meet or beat the Subnautica-level floor for readability, beauty, water color, terrain material detail, and scenic composition. Compact reduces density and resolution; it must not downgrade art direction into a dark, flat, muddy, or placeholder-looking scene.

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

Current URP quality defaults:

- default Standalone quality profile is Surface/Medium unless current project settings prove a newer owner-approved default;
- medium PC RP asset path: `Assets/_Project/Data/URP_Medium (PC_RPAsset).asset`;
- low/compact RP asset path: `Assets/_Project/Data/URP_Low (PC_RPAsset).asset`;
- compact/low renderer path: `Assets/_Project/Data/Mobile_Renderer.asset`;
- medium defaults: HDR on, MSAA off, FXAA path, render scale `1.0`;
- low/compact defaults: HDR on, MSAA off, FXAA path, render scale about `0.85` unless hardware detector/settings owner overrides continuously through `GlobalQualityWeight`.

Do not change Quality, URP assets, renderers, HDR/MSAA/AA mode, or render scale defaults without reading `settings.md`, current ProjectSettings/URP assets, and providing Frame Debugger/profiler/screenshot proof. These defaults are route facts, not proof that current Unity quality settings are correctly bound.

## Live Source Anchors - 2026-06-05

Evidence class: STATIC_SOURCE / STATIC_DOC only. These anchors do not prove Unity import, Frame Debugger, RenderGraph Viewer, profiler, GC, visual quality, or player-build readiness.

- `Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs` is the current fullscreen visor droplet/leak distortion renderer. Static source shows `HectonVisorFluidDistortionFeature : ScriptableRendererFeature, IGlobalRegistryHotSwapListener, ILateFrameTickable`; it caches player, fluid, and DataVault dependencies through lifecycle/hot-swap paths, registers visual-sync work, writes a 300-row `BufferID.VisorRefractionBlackBox` telemetry ring under `SystemID.Vfx`, and dumps `Docs/AgentLogs/Dump_1335_VisorFluidRefraction.bin` only on non-finite input.
- RenderGraph ownership: `VisorFluidPass.RecordRenderGraph` reads active color, depth, opaque color, optional compute-resolved diegetic lens mask, and imported constant buffers; writes `_HectonVisorFluidDistortion`; and assigns `resourceData.cameraColor`. It uses authored `FeatureSettings.material` and optional `lensComputeShader`; it is not allowed to instantiate runtime materials or become gameplay water/pressure truth.
- Presentation boundary: wet lens, hull-stress leaks, rain, water-density signal, dust, Snell/chromatic refraction, and lens-mask distortion are presentation approximations. They may sell water/pressure/visor material belief and scale through `GlobalQualityWeight`/visual-overkill fields, but they must not own flooding, pressure damage, fluid simulation, survival truth, save state, or navigation truth. Missing proof: renderer asset binding/import, Frame Debugger or RenderGraph Viewer pass order, GPU/CPU timing, GCMonitor, compact/high captures, and verification that the effect preserves center readability instead of hiding weak art.

## Current Decal Routes

Decals are presentation evidence, not gameplay truth.

Runtime trauma and visor decals:

- `DynamicDecalVaultRuntime` owns Vault-backed fixed decal request/runtime/telemetry buffers under the VFX owner route.
- `DeferredDecalPass` is the current screen-space trauma consumer. It composites the published buffer in a bounded render pass; it must not spawn `DecalProjector`, Canvas blood, per-trauma GameObjects, particle splats, fracture meshes, or material clones.
- Damage/impact producers publish typed unmanaged signals. The decal route consumes those snapshots and may fail closed on missing cold storage, non-finite matrices, upload stalls, queue overflow, or visual-sync ownership conflicts.
- Screen-space decal output may be scaled by `GlobalQualityWeight` for active count, decay, refraction richness, atlas sampling, and density only. It must not change damage truth, save identity, rollback/Merkle state, DTO layout, or interaction authority.

Generated world-support decals:

- Batch34 decal and UV atlases are source assets until imported, padded/split/alpha-checked, and bound through editor tooling.
- `WorldSupportGeneratedDecalMaterialBuilder` is the current first-party material builder for generated world-support decals. It must fail at the exact missing source id/path, use transparent render state, and avoid warning-only aggregate failure masking.
- `WorldProceduralSupportFinalAuthoring` must attach deterministic first-party quad decal children for support/world props. It must not depend on vendor ScifiFacility decal prefabs or instantiate vendor decal objects.
- `Batch34VisorTraumaDecalArrayIntegrator` bakes promoted Batch34 alpha candidates into the visor trauma `Texture2DArray` and binds compatible `DeferredDecalPass` renderer features.
- `Tools/ValidateWorldSupportGeminiMaterialRoute.py` and `Tools/ValidateBatch34VisorTraumaDecalArrayRoute.py` are static contract guards. Passing them is not Unity import, renderer binding, Frame Debugger, profiler, visual, or player-build proof.

Required failure modeling before runtime acceptance: no source texture, bad alpha/padding, missing material GUID, wrong transparent state, stale vendor prefab dependency, duplicate decal owner, queue full, stale renderer feature handle, missing atlas slice, scene unload, domain reload, and save/load attempts to treat decal presentation as gameplay state.

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
- **Red Light Absorption & Inky Water**: Ocean water must not render as tropical blue. Water is a dark, heavy inky-gray, utilizing volumetric fog with Red Light Absorption (red wavelength light is absorbed exponentially with depth, leaving only decolorized, greenish-blue, dead Abyssal visuals below 200m).
- caustics are allowed only where light physically has reason to exist;
- underwater visibility must preserve one readable route cue.

Reject empty black screens, bright aquarium haze, and fog used to hide bad assets.

## Diegetic UI & Material Wear

- **Diegetic UI Raycast Click Mapping**: Standard overlay UI canvases (`Screen Space - Overlay`) are strictly banned. Terminal screens must render on in-world 3D textures. User interactions must map from camera center physics raycasts directly to the UV coordinates of the target screen.
- **PBR Wear & Corrosion**: All database structures, submarine hulls, and suit models must utilize galvanic corrosion, wear, leak, and scratch masks to enforce the utilitarian NASA-punk aesthetic.


## Lighting And Shadows

In abyss, caves, interiors, and pressure-event routes, darkness is baseline pressure and motivated light is an expensive exception. On the surface, shoreline, ocean skin, photic shallows, sky, Aegir, and moons, motivated daylight, sky light, celestial light, and reflected water light are mandatory readability and beauty tools.

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

## First-20 Route Hook

- First-20 moment: world load and first exit must render surface, sky/Aegir/moons where visible, ocean skin, photic water, terrain, HUD, tool feedback, and hazard readability without crushed darkness.
- Route blocker removed: rendering cannot use post, fog, bloom, or noir grade to hide weak surface/shallow art or unproven route visibility.
- Proof class: screenshot, Frame Debugger or RenderGraph Viewer for changed passes, Profiler/GCMonitor for runtime render work, and Play Mode/player capture for route readability.

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
- surface, photic-shallow, or medium-depth captures below the Subnautica-level visual floor.

## Automation Blindness & Goodhart's Law Rule

**Python validators and automated scripts are banned from evaluating Beauty Renders.**

The historical failure mode: a validator checked for "no black pixels (RGB < 15)". Instead of fixing the PBR shader, the system disabled ACES Tonemapping, removed Exponential Depth Fog (Abyss atmosphere), and cranked Ambient Light to flat `Color(0.8, 0.8, 0.8)`. This destroyed micro-shadows, killed volume, and turned NASA-Punk into cheap mobile graphics.

**Validator scope boundaries:**

- Structural analysis tools: ALLOWED to analyze raw X-Ray maps (`GetHeights`, `GetSteepness` exports), gradient variance, seam detection, and structural data. Live examples under `Tools\`: `ValidateTerrainProbeEvidence.py`, `ValidateTerrainSurfaceMaterialBindings.py`, `BuildWorldNestedTerrainPreview.py`. (analyze_terrain.py — unbackticked: dead path — was named here but does not exist — verified 2026-07-27; the permission is for the tool CLASS, so write or extend a real script rather than hunting for that filename.)
- Python validators: BANNED from analyzing final shaded screenshots / Beauty Renders.
- Beauty quality assessment: ONLY via Multimodal Vision (agent eyes) with ACES Tonemapping active, DirectionalLight Soft Shadows active, and Exponential Depth Fog active. Agent must describe specific pixels and features seen, not just "looks good".

**Signs of Goodhart Optimization (immediate reject):**

- ACES Tonemapping disabled.
- Ambient Light set to flat grey or white for "darkness fix".
- Exponential Depth Fog removed to "pass pixel check".
- Directional light intensity raised to hide shadow bugs.
- Any post-process feature disabled to satisfy an automated metric.

**Required validator output** (when validators ARE used for structural checks): raw numeric data (height variance, seam delta, slope histogram), not color descriptions.

## Offline Bake vs Runtime Generation Law

**Runtime procedural 3D mesh generation is banned** (except Marching Cubes voxels running in background Burst threads).

Flora (coral, kelp, seagrass), base props, tools, ore nodes, and fauna meshes are generated **offline in the Editor** via `WorldProceduralCoralMeshBuilder` and similar tools. The Editor script generates N unique high-poly variants, computes Tangents (required for PBR normal mapping), and saves them as `.asset` prefabs on disk.

At runtime: `ProceduralScatterDirector` uses `BatchRendererGroup` (BRG) with `Graphics.RenderMeshIndirect`. The CPU provides `NativeArray<float4x4>` positions. A Compute Shader performs frustum culling. The survivors draw in a single GPU indirect call.

**`GameObject.Instantiate` is banned for scatter.** Each GameObject has a Transform that calls into C++ native objects on every mutation. 100,000 objects = CPU hierarchy death.

**`Graphics.RenderMeshInstanced` is banned for high-count scatter** — it lacks indirect draw support and caps at 1023 instances. Only `Graphics.RenderMeshIndirect` through BRG is acceptable.

## Acceptance Sentence

Rendering is accepted only when it preserves route readability, sells pressure and material truth, scales continuously, proves its cost, and makes HECTON-8 look expensive through controlled premium approximations instead of brute-force effects.

