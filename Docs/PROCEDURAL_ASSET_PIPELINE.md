# PROCEDURAL_ASSET_PIPELINE.md
## CONTRACT FOR PROCEDURAL GENERATION OF ORGANICS, GEOLOGY AND STRUCTURES
Target Hardware: NVIDIA MX350 2GB VRAM · i5-1135G7 · 12GB RAM
Engine: Unity 6000.x · URP Forward+
Assets: MapMagic 2.1.18 · GPU Instancer Pro · Mantis LOD Editor · Mesh Baker · Crest Ocean
Tone: Strict, deterministic, zero-assumption. Realism via math and shaders, not polygon count.

---
[REQ] Every procedural asset MUST pass all steps sequentially. Skipping any step = rejection.
[REQ] Category MUST be declared: ORGANIC / GEOLOGICAL / STRUCTURAL / INTERIOR_DECOR.
[REQ] AI generation MUST follow the exact pipeline below. No creativity in architecture.

---
CORE_PRINCIPLES
[REQ] Detail ≠ Polygons. Realism = normal maps + vertex displacement + triplanar + correct LOD transitions.
[REQ] One material = one draw call. All variations via GPU Instancer Color/Scale/Rotation randomization.
[REQ] Zero UV dependency for procedural geometry. Triplanar projection MANDATORY.
[REQ] Zero CPU animation. All motion via vertex shader (WorldPos + Time + Sine/Noise).
[REQ] AI MUST output each step sequentially. Do not merge steps. Do not skip validation.
[REQ] If exact API signature, SO type, or MapMagic node name is unknown → STOP. Request exact signature. Do not guess. Do not write // TODO.

---
TAXONOMY_AND_BUDGETS
[REQ] Global screen limits (MX350): ≤ 2.5M visible tris, ≤ 800 SetPass calls, ≤ 1.6GB VRAM textures, ≤ 100 unique materials.
[FORBID] Exceeding category tris budget.
[FORBID] Unique material per instance.
[FORBID] Transparent shader for opaque geometry.


[FIX] CULLING_DISTANCES_CORRECTION
[REQ] Replace outdated culling values. 15-50m causes premature pop-out, breaks flashlight/fog continuity, violates LOD progression logic.
[REQ] Correct culling distances (minimum, before frustum/occlusion takes over):
| Category        | Cull Distance | Rationale                                  |
|-----------------|---------------|--------------------------------------------|
| ORGANIC         | 60-120m       | Matches clear-water visibility + flashlight cone. Algae/coral fade into fog. |
| GEOLOGICAL      | 150-300m      | Rocks/cliffs form terrain silhouette. Must persist until LOD2 + fog culling. |
| STRUCTURAL      | 250-500m      | Bases/ships are navigation landmarks. Cull only beyond visual range or occlusion. |
| INTERIOR_DECOR  | 40-80m        | Indoor/line-of-sight dependent. Cull when out of room or behind sealed doors. |

[REQ] Implementation in Unity:
- Do NOT hardcode culling in scripts. Use Unity Layer Cull Distances + GPU Instancer Distance Culling.
- GPU Instancer Pro: Enable "Distance Culling", set Min/Max per prototype. Sync with LOD Group thresholds.
- URP: Tie culling to Fog Density. When fog opacity > 0.95, disable rendering via keyword/shader.
- Frustum Culling: ON (default). Occlusion Culling: ON for interiors/bases.
- [FORBID] Culling distances below 40m for any world-space object.

[REQ] PHOTOREALISTIC SURFACE INTEGRATION
Shader MUST include these exact nodes/logic to prevent "CG plastic" look on high-res textures:
1. SSS Approximation: Wrap Lighting (Half-Lambert) + `1 - dot(N, L)` mask on Albedo. Thin edges (coral/algae) MUST transmit light. Controlled via Mask.A (Translucency).
2. Curvature-Driven Wetness: Extract convex/concave from Normal map → modulate Roughness. Concave areas = wet/glossy (0.2). Convex = dry/matte (0.7). Zero manual painting.
3. Micro-Parallax Offset: `Mask.B` drives UV offset on Albedo/Normal. Max offset = 0.03. Prevents flat "wallpaper" effect on complex photos.
4. Fresnel Water Film: `Fresnel Effect` node must darken edges and blend with `Depth Fog Color`. Simulates water film on surface.
5. Normal Scale Control: Exposed float `_NormalScale`. Default 0.75 for phototextures (prevents "burned" edges on high-frequency details).
[FORBID] Direct texture sampling without curvature roughness, SSS mask, or Fresnel blend. Visual result will be rejected.
---
GEOMETRY_FUNDAMENTALS
[REQ] Clean base: quad-dominant topology, uniform density, no triangles < 0.05m².
[REQ] Procedural deformation via 3D noise (Simplex/Perlin): Amplitude 0.05-0.3m, Frequency 2-5.
[REQ] Auto-recalculate normals: Smooth Normals + Preserve Hard Edges for rock/coral plates.
[REQ] Edge bleeding: boundary vertices snap to Average Normal of adjacent chunks for seamless stitching.
[FORBID] Chaotic triangulation, T-vertices, non-manifold geometry.
[FORBID] Mesh does NOT require unwrap. Details applied in world space via triplanar.

---
[REQ] SAVE_VERSIONING & MIGRATION PIPELINE
Save files MUST include version header: uint SaveVersion, string EngineVersion, uint Checksum.
On load: if SaveVersion < CURRENT_VERSION → run SaveMigrator.ApplyDelta(). Never load raw data.
[FORBID] Direct JSON overwrite without backup. No .tmp → .sav rename on crash.
[REQ] Corruption recovery: .bak auto-promoted if .sav checksum fails. Log to crash_telemetry.log.
File: SaveVersioning.cs, SaveMigrator.cs
Reason: EA patches break DTO schemas. Unhandled migration = 1-star reviews.

---
[REQ] DYNAMIC_LIGHTING_PROBE_GRID
Outdoor probes MUST be placed procedurally: 1 probe per 200m², aligned to terrain height + 2m offset.
Baking: Bakery GPU Lightmapper for static bases/ruins ONLY. Probes update via LightProbes.TetrahedralizeAsync().
[FORBID] Realtime GI outdoors. Shadow cascades > 2 on MX350. Unbaked probes in caves.
[REQ] Probe density scales with biome: High (Reefs/Bases), Low (Abyssal plains), None (Caves → use vertex AO).
File: ProbeGridGenerator.cs, BakeryConfig.asset
Reason: 15×15km world without probe grid = flat, muddy lighting on MX350. Caves without vertex AO = visual noise.

---
[REQ] UNDERWATER_AUDIO_OCCLUSION_PIPELINE
All 3D audio MUST route through AudioMixerGroup "Underwater_Occlusion".
Dynamic LowPass: cutoff = Mathf.Lerp(20000, 800, immersionRatio * depthFactor). Q = 1.0.
Reverb: AudioReverbPreset zones per biome. Apply via SetAudioListenerReverb(). No real-time convolution.
[FORBID] AudioSource.spatialBlend = 0 underwater. Dry/wet mixing > 0.5 on MX350. Unbaked reverb zones.
[REQ] Doppler effect disabled underwater (physically incorrect). Distance rolloff = CustomCurve (logarithmic, fast decay).
File: UnderwaterAudioProcessor.cs, AudioMixer.mixer
Reason: Subnautica’s immersion lives in acoustics. Dry, unfiltered audio breaks Deep Sea Noir tone instantly.

---
[REQ] CRASH_TELEMETRY & INGAME_CONSOLE
Application.logMessageReceived → write to crash_telemetry.log with: timestamp, scene, depth, fps, vram, stacktrace.
In-game console: ~ toggle. Commands: fps, vram, goto <x,y,z>, spawn <prefab>, save_now.
[FORBID] Console in EA build. Telemetry upload without user consent. Blocking UI on crash.
[REQ] Auto-capture screenshot on exception. Attach to log. Save to /Saves/Debug/.
File: CrashTelemetry.cs, DebugConsole.cs
Reason: Solo dev cannot reproduce MX350-specific crashes without telemetry. Console accelerates playtesting 10×.

---
[REQ] STEAMWORKS_INTEGRATION_CORE
Achievements, Cloud Saves, Workshop (disabled), Telemetry.
Cloud saves: sync on Application.quitting and OnApplicationPause(false). Conflict resolution: latest_timestamp wins.
[FORBID] Sync on FixedUpdate. Blocking SteamAPI.RunCallbacks() in main thread. Unhandled achievement unlocks.
[REQ] Wishlist tracking: log SteamUserStats.GetStat("WishlistSource") on first launch.
File: SteamManager.cs, CloudSaveSync.cs
Reason: Steam algorithm pushes games with >70% cloud save adoption and achievement completion tracking.

---
[REQ] ACCESSIBILITY & CONTROL_REMAP
Full key/gamepad rebinding via InputActionMap. Save bindings to controls.json.
Colorblind modes: PostProcessVolume profile swap (Protanopia/Deuteranopia/Tritanopia). Adjust emission hues.
UI scaling: CanvasScaler.scaleFactor range 0.75–2.0. Step 0.25.
[FORBID] Hardcoded KeyCode in gameplay logic. UI scaling > 1.5 on HUD elements. Unremapped critical actions.
[REQ] Difficulty toggles: O2DrainMultiplier, PressureDamageMultiplier, MarkerVisibility (0-3). Save per profile.
File: ControlRemapper.cs, AccessibilitySettings.cs
Reason: Steam requires basic accessibility for visibility tags. Unremapped controls = 30% refund rate.

---
[REQ] PERFORMANCE_CI_REGRESSION
Weekly MX350 benchmark: Profiler.BeginSample("EA_Benchmark"). Capture: FrameTime, VRAM, SetPass, Batches, GCAlloc.
Thresholds: FrameTime ≤ 16.67ms, VRAM ≤ 1.6GB, SetPass ≤ 800, GCAlloc = 0.
[FORBID] Manual profiler checks without logging. Ignoring >10% regression. Skipping tests on shader changes.
[REQ] Auto-reject PR if benchmark fails. Output: performance_report.md with delta vs baseline.
File: BenchmarkRunner.cs, PerformanceThresholds.asset
Reason: Solo dev cannot track optimization debt manually. Automated CI catches regressions before they compound.

---
[REQ] ENDGAME_EA_RETENTION_LOOP
Post-5-hour content: DirectorAI spawns EphemeralEvents (thermal vents, drone migrations, cave collapses).
Depth challenges: DepthRecord tracker. Rewards: cosmetic HUD variants, base decor, lore fragments.
[FORBID] Static world after story completion. Unscalable difficulty. Dead zones with no content > 500m radius.
[REQ] MysteryCache system: procedural loot caches requiring multi-biome coordination. Community-driven tracking.
File: EphemeralEventDirector.cs, DepthChallengeTracker.cs
Reason: EA retention drops 60% after 5 hours without procedural events or community goals. Subnautica’s endgame failed this.

---
SEAMLESS_STITCHING
[REQ] Chunk boundaries: Vertex Normal Blending + Height Offset ≤ 0.02m.
[REQ] Structural/bases: Base Ring vertices fixed to Terrain/Snap Grid on spawn. Modular snap grid = 0.5m.
[REQ] MapMagic terrain hole edges covered by rock/debris scatter to hide geometry seams.
# TEXTURE_GENERATION_PROTOCOL

[REQ] All PBR textures (Albedo, Normal, Mask, Detail) MUST be generated as seamless tiling images.
[REQ] If texture generation is required — output a single MASTER PROMPT for image generation AI. Do not generate placeholder textures.
[REQ] Master Prompt format:
  "Seamless tiling PBR [texture_type] texture, [subject_description], [biome_context], top-down orthographic view, uniform lighting, no shadows, no perspective distortion, photorealistic, 4K resolution, edge-perfect seamless tile pattern, --tile --v 6 --ar 1:1"
[REQ] Subject-specific substitutions:
  Albedo: "albedo/diffuse surface color, natural variation, [material type] tones, zero specular highlights"
  Normal: "normal map visualization, [surface detail type] bumps and grooves, neutral purple-blue base, green-up convention, no color variation"
  Mask: "mask texture, grayscale distribution of roughness and AO, [material] micro-surface variation, neutral gray base"
  Detail: "fine detail normal map, micro-scratches, pores, grain, sub-millimeter surface noise, high frequency only"
[REQ] Every Master Prompt MUST include: --tile (seamless tiling flag), --ar 1:1 (square aspect), target resolution 2048x2048 or 1024x1024 per category budget.
[REQ] Biome context tokens:
  ORGANIC_shallow: "sunlit underwater, turquoise tones, silicon-based coral textures"
  GEOLOGICAL_slope: "weathered rock face, basalt and sediment layers, wet surface sheen"
  STRUCTURAL_ruin: "NASA-punk metal, corrosion, salt water staining, welded seams, peeling industrial paint"
  ORGANIC_abyss: "bioluminescent deep sea, dark basalt substrate, pale translucent growths, chemosynthetic textures"
[FORBID] Non-tiling textures. Photographs with visible edges. UV-dependent unwrap textures. Textures with baked-in shadows or perspective.
[FORBID] AI generating final Unity-ready .png directly. Only prompts — textures are validated and imported manually through Unity Texture Importer with correct compression settings (BC7/BC5).
[REQ] Post-generation Unity Import Settings (must accompany every texture prompt output):
  Texture Type: Default (Albedo/Mask) or Normal Map (Normal/Detail)
  Wrap Mode: Repeat
  Generate Mip Maps: On
  Compression: BC7 (color), BC5/DXT5nm (normals)
  Max Size: 2048 (hero/world), 1024 (props/scatter)
  sRGB: On (Albedo only), Off (all others)
  Read/Write: Off
[REQ] Validation after import: open texture in Unity → set tiling to 4x4 in material → inspect for visible seams. If seams visible → regenerate with prompt: "FIX SEAMS: previous output had visible tile boundaries at 2x repetition, ensure exact edge matching on all four sides".
---
COLLISION_RULES
[REQ] Collision type defined by category:
| Category        | Collider                  | Note                          |
|-----------------|---------------------------|-------------------------------|
| ORGANIC (small) | None                      | Vertex animation. Pass-through.|
| ORGANIC (large) | Capsule / Box             | Only if blocks path.          |
| GEOLOGICAL (≤3m)| 2-3 Primitives            | Box/Sphere per cluster.       |
| GEOLOGICAL (>3m)| MeshCollider (Convex)     | Based on LOD2 mesh only.      |
| STRUCTURAL      | MeshCollider (Static)     | isKinematic = true.           |
[FORBID] MeshCollider on LOD0.
[FORBID] Dynamic Rigidbody for static props.
[FORBID] >500 active colliders on screen.

---
TEXTURE_PIPELINE
[REQ] Texture specs:
| Map          | Format  | Max Size | sRGB | Note                  |
|--------------|---------|----------|------|-----------------------|
| Albedo       | BC7     | 2048     | Yes  | Tiling 2-4x           |
| Normal       | BC5     | 2048     | No   | Tangent Space, green flip checked |
| Mask (ARM)   | BC7     | 2048     | No   | R=AO, G=Rough, B=Height/Metal |
| Detail Normal| BC5     | 1024     | No   | Micro-relief, blended in shader |

[REQ] All textures MUST be seamless (Wrap Mode = Repeat).
[REQ] One atlas per biome/family. Max 1 Material for GPU Instancer batch.
[REQ] Generate Mip Maps = On. Streaming = Off. Compression = Crunch for BC7.
[FORBID] Non-tiling photos, unique textures per instance, uncompressed RGB, >2048px for scatter.

---
ENTERPRISE_SHADER_ARCHITECTURE
[REQ] URP Shader Graph structure:
- Master Node: PBR / AlphaTest / GPU Instancing
- Triplanar UV: World Space, Normal Type
- Texture Sampling: Albedo, Normal, Mask
- Detail Map Blending: Normal + Height
- Vertex Displacement: GPU-driven (sin/cos + Time + WorldPos)
- Depth Fog Integration
- Quality Tiers: Keyword _QUALITY_MX350 / _QUALITY_HIGH

[REQ] Mandatory shader features:
| Feature            | Implementation                                      | Purpose                  |
|--------------------|-----------------------------------------------------|--------------------------|
| Triplanar Mapping  | WorldPos → Dot(Abs(Normal), Axis) → Blend          | Seamless on any geometry |
| Vertex Animation   | sin(Time.y * Freq + WorldPos.xz * Phase) * Amp     | 0 CPU, GPU-only          |
| Depth Fog          | Lerp(MatColor, FogColor, exp(-Depth * Coeff))      | Atmosphere + hide LOD pop|
| Parallax/Height    | Mask.B * HeightScale * ViewDir                     | Volume without polys (HIGH tier only) |
| Quality Fallback   | _QUALITY_MX350 disables Parallax, reduces Disp Amp | MX350 stability          |

[REQ] GPU Instancing = ON. Variant Stripping = OFF (manual control).
[REQ] Max 8 texture samples per pixel.
[REQ] Cull Off (organic), Cull Back (hard surface). ZWrite On. Blend Off.
[FORBID] Transparent shaders for corals/rocks. GrabPass. ComputeBuffer in renderer. Dynamic branch if(). ScreenPosition dependencies.

---
OPTIMIZATION_AND_LOD
[REQ] Mantis LOD workflow:
1. Export LOD0 → Import to Mantis.
2. LOD1: Poly Reduction 40-50%, Preserve Silhouette = ON.
3. LOD2: Poly Reduction 85-90%, Collapse Threshold ↑, preserve silhouette.
4. Assign to LOD Group thresholds: 0.6 / 0.15 / 0.04 / 0.
5. Cross Fade = ON (Dithered) for near distances.

[REQ] GPU Instancer Pro setup:
- Color Variation: Hue ±0.05, Sat 0.9-1.1, Val 0.85-1.05.
- Scale/Rotation: Random Y 0-360°, X/Z tilt ±8°, Scale 0.7-1.3.
- Frustum Culling: ON. Occlusion Culling: ON.
- Buffer Size: Auto-grow, max 100k instances per prefab type.

[REQ] Cluster baking: Rock/cloral groups baked into SINGLE mesh via Mesh Baker → Mantis Decimation → LOD Group → GPU Instancer. Source meshes deleted after bake.
[REQ] MapMagic scatter: Pass coordinates via HectonRockOutput → GPU Instancer API. Floor Offset Y: -0.2 to -0.8m. Yaw random 0-360°.


[REQ] PROCEDURAL_ASSET_VALIDATION_GATE
Every AI-generated or procedurally placed asset MUST pass automated validation before runtime integration.
Validation checklist:
- Poly count ≤ category budget (ORGANIC ≤3000, GEO ≤8000, STRUCT ≤15000)
- Zero UV seams (triplanar projection verified in-editor)
- Shader compiles without warnings, GPU Instancing = ON, <8 texture samples
- Collider count ≤ 3 per instance (Box/Sphere/Convex only)
- LOD Group configured with dithered crossfade (0.6 / 0.15 / 0.04 / 0)
- No missing references, no runtime Instantiate(), no Update() calls
[FORBID] Skipping validation. Unverified assets trigger build rejection.
Reason: Prevents VRAM leaks, broken SRP batching, and physics spikes on MX350.

[REQ] RUNTIME_PERF_DEGRADATION_PROTOCOL
If frame time > 25ms for 3 consecutive frames, system MUST auto-degrade in strict order:
1. Disable vertex animation on flora
2. Reduce GPU Boids count by 50%
3. Activate _QUALITY_MX350 shader keyword (disable parallax/height blend)
4. Disable post-processing overrides (Bloom, DoF, Vignette)
5. Lower Volumetric Fog to Half-Res
[FORBID] Static quality settings. Hard crashes on performance spikes. Manual "optimize later" notes.
Reason: Guarantees playable 30+ FPS on MX350 without session-specific tuning.

[REQ] DELTA_SAVE_EDGE_CASE_HANDLING
Procedural chunks MUST serialize ONLY:
- Player-placed/modified structures
- Cut/removed resources (isCut=true)
- Dropped inventory items
- AI state flags (e.g., droneTension, baseIntegrity)
Unmodified procedural geometry MUST NEVER be saved.
On load: regenerate chunk from seed → apply delta changes → verify terrain hole/voxel alignment → snap player to nearest safe surface if spawn overlaps geometry.
[FORBID] Saving heightmaps, scatter coordinates, or full world state. Patch-breaking topology changes without heightmapLock override.
Reason: Prevents 500MB+ save files, chunk desync, and broken player spawns after graph updates.

[REQ] AI_CODE_INTEGRATION_WORKFLOW
All AI-generated code MUST follow strict pipeline:
1. Commit to isolated branch (feature/ai-[system])
2. Run automated profiler (GCMonitor + Frame Debugger) on MX350-equivalent scene
3. Verify zero GC in hot paths, VRAM <1.6GB, SetPass ≤600
4. Manual code review (architecture compliance, no hardcoded values)
5. Merge to main only after passing all checks
[FORBID] Direct pushes to main. Unprofiled code merging. Assuming "it works in editor" equals production ready.
Reason: Catches MX350-specific bottlenecks before they compound across systems.

[REQ] UNDERWATER_AUDIO_DSP_CHAIN
All 3D audio sources MUST route through mandatory signal chain:
1. LowPassFilter (cutoff scales exponentially with depth: 20000Hz → 400Hz at -5000m)
2. ConvolutionReverb (wet/dry ratio based on proximity to hard surfaces, decay time 1.2s-3.5s)
3. Dynamic Ducking (critical SFX > ambient by ≥12dB, attack 0.05s, release 0.3s)
4. HRTF panning disabled (underwater directionality relies on amplitude/frequency, not phase)
[FORBID] Raw audio playback without depth-based filtering. Unoccluded 3D sources. Real-time convolution on MX350.
Reason: Enforces Deep Sea Noir acoustics. Prevents frequency masking that breaks immersion on low-end audio hardware.

[REQ] PROCEDURAL_SPAWN_SAFETY_BOUNDS
All MapMagic scatter points and voxel cave entrances MUST enforce:
- Minimum 2.5m clearance from player spawn coordinates
- No overlap with active base modules or power cables
- Floor alignment Y offset: -0.2m to -0.8m (prevents floating assets)
- Maximum 1200 instances per 1000m tile (enforced by scatter density clamp)
[FORBID] Spawning inside geometry. Unclamped scatter density. Player spawn inside cave/terrain hole.
Reason: Prevents soft-locks, physics explosions, and immediate MX350 draw call overload.

[REQ] SHADER_VARIANT_STRICTION_POLICY
All URP shaders MUST explicitly declare:
- Supported quality keywords (_QUALITY_MX350, _QUALITY_HIGH)
- Maximum texture samplers per pass
- GPU Instancing ON, SRP Batcher compatible
- Strip unused variants in Player Settings
Pre-warm critical shaders in 00_BOOTSTRAP via ShaderVariantCollection.WarmUp().
[FORBID] multi_compile > 4 keywords. Dynamic keyword toggling in runtime. Unwarm shaders causing first-frame hitch.
Reason: Prevents shader compilation spikes and VRAM fragmentation during gameplay.

---
AI_GENERATION_PROTOCOL
[REQ] Output MUST follow this exact sequence. Do not skip steps.

[STEP 1] BASE_MESH
→ Generate clean geometry per category.
→ Verify: Manifold? Normals consistent? Edge loops clean? UV not required for procedural?
→ Output: .fbx/.obj (LOD0)

[STEP 2] TEXTURE_SET
→ Generate 4 tiling PBR maps (Albedo, Normal, Mask, Detail).
→ Verify: Seamless at 2x/4x tiling? Normal green flip? Mask R/G/B mapped? BC7/BC5 ready?
→ Output: .png/.tga (Compressed)

[STEP 3] SHADER_GRAPH_AND_MATERIAL
→ Assemble URP Graph per architecture above.
→ Enable GPU Instancing, Triplanar, Depth Fog, Quality Keywords.
→ Verify: Compiles without errors? <8 samples? Zero CPU animation?
→ Output: .shadergraph → .shader + MAT_[Category]_Universal

[STEP 4] LOD_BAKE_AND_COLLIDER
→ Process through Mantis (LOD1/LOD2). Set LOD Group thresholds.
→ Assign collider per category table.
→ Verify: Crossfade? Silhouette preserved? Poly budget met?
→ Output: Prefab with LOD Group + Collider + GPU Instancer Prototype

[STEP 5] INSTANCER_AND_SCATTER_RULES
→ Configure GPU Instancer Pro (Color, Scale, Rot, Culling, Buffer).
→ Write MapMagic Scatter profile: Biome mask, Density, Floor Offset, Yaw Random.
→ Verify: Draw Calls ≤ 1 per type? VRAM ≤ 20MB per set? FCPS stable?
→ Output: Config JSON + Prefab ready for runtime + Validation Report

---
VALIDATION_CHECKLIST
[REQ] AI MUST output this checklist filled after generation:
☐ Poly count matches taxonomy table
☐ Zero UV seams (Triplanar verified)
☐ Shader compiles + GPU Instancing ON + MPB compatible
☐ LOD0→1→2 transition smooth, dithered, no pop-in
☐ Draw calls ≤ 10 for 5k instances
☐ VRAM ≤ 1.6GB total for all procedural sets
☐ MX350 test: ≥45 FPS with 3k instances on screen
☐ Collision matches category rules (Convex/Prim/None)
☐ Animation 100% vertex-shader driven
☐ Missing Context Protocol followed if API unknown
[REQ] If any checkbox is empty → REJECT. No discussion.

---
ABSOLUTELY_FORBIDDEN
[FORBID] Low-poly "flat" meshes without normals/displacement.
[FORBID] Unique materials per instance.
[FORBID] Animation via Update(), Animator, Bones, Coroutines.
[FORBID] UV-dependent details for procedural geometry.
[FORBID] MeshCollider on LOD0 or Dynamic Rigidbody for statics.
[FORBID] Transparent shaders for opaque geometry.
[FORBID] AI guessing parameters. All values MUST derive from table or formula.
[FORBID] Bypassing validation. No checklist → rejection.
[FORBID] Instantiate() in runtime for scatter objects. Only GPU Instancer / Pools.
[FORBID] Runtime texture generation or Graphics.CopyTexture for mass assets.

---
APPENDIX_QUICK_REFERENCE
| Parameter           | MX350 Limit   | High-End Limit |
|---------------------|---------------|----------------|
| LOD0 Tris (ORG/GEO) | ≤ 5000 / 10000 | ≤ 25000        |
| Texture VRAM/type   | ≤ 22 MB       | ≤ 45 MB        |
| Draw Calls (Instanced)| 1-4         | 1-6            |
| Shader Samples      | ≤ 6           | ≤ 10           |
| LOD Crossfade       | Dithered (near)| Crossfade (all)|
| Animation           | GPU Vertex    | GPU + Compute  |

[REQ] Usage Prompt for AI:
Generate [Asset Category] [Asset Name] following PROCEDURAL_ASSET_PIPELINE.md strictly. Output each step sequentially. Stop after validation checklist. Target: MX350 2GB VRAM, URP Forward+, GPU Instancer compatible. Do not invent APIs. Do not skip steps. If context missing → request exact signature.