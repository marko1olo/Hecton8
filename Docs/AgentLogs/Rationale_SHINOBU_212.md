# Rationale_SHINOBU_212

Status: IMPLEMENTED - COMPILE PENDING CPU GATE
Evidence class: STATIC_DOC / STATIC_SOURCE until Unity import and editor bake proof exist.

## Decision 00 - Domain and Product Route

Problem: SHINOBU_212 is a rendering/content-pipeline agent, but HECTON-8 requires every task to tie to the first 20 minutes route.
Solution: Treat impostor baking as a world-load/swim visibility cost reducer. It changes presentation only and keeps gameplay object existence outside the visual LOD hash path.
Rejected Alternatives: Adding runtime capture controllers or global registry services. Both expand global authority and add hot-path GPU work.
Scalability potential: Low uses earlier impostor swap and lower atlas resolution; Middle uses default 16 frames; High keeps 3D farther and uses denser frames; Ultra uses visual-overkill atlases while still drawing cards at horizon.
Hardware Impact: Expected i3/MX350 gain is reduced vertex submission and raster pressure for giant distant meshes. Exact microseconds: PENDING PROFILER.

## Decision 01 - Offline-Only Capture

Problem: Real-time offscreen capture for distant billboards burns GPU state changes and competes with gameplay rendering.
Solution: Editor-only baker using RenderTexture, CommandBuffer, compute packing, dilation, and AsyncGPUReadback. Runtime receives textures, mesh, material, and shader only.
Rejected Alternatives: Runtime `Camera.Render()` impostor refresh, Unity BillboardRenderer, Tree impostors, and managed pixel loops.
Scalability potential: Low/MX350 consumes baked albedo-depth and packed normal atlases; Middle raises dilation/size; High/Ultra consume more angular frames or longer 3D residency driven by continuous `GlobalQualityWeight`.
Hardware Impact: Expected gain on MX350 is geometry collapse to two triangles per far object and fewer draw/material changes. Exact microseconds: PENDING PROFILER.

## Decision 02 - DTO Alignment

Problem: Shader config data must be GPU/ARM64-safe and not drift under IL2CPP/Burst.
Solution: Define an explicit 16-byte `ImpostorConfigDTO` with offsets for `float2 AtlasGridSize`, `float DepthScale`, and `uint Flags`; validate with `UnsafeUtility.SizeOf` and `Marshal.OffsetOf` in an editor validator.
Rejected Alternatives: Sequential DTO with implicit padding, `Pack=1`, runtime bool fields, or C# properties.
Scalability potential: Same 16-byte payload across Low/Middle/High/Ultra; visual tier changes values, not layout.
Hardware Impact: Avoids unaligned reads and cache waste on ARM64/Steam Deck-class devices. Exact microseconds: static layout protection, runtime delta PENDING.

## Decision 03 - Visual Fake First

Problem: Distant massive geometry needs visual mass, not physical truth.
Solution: Use 2D impostor cards with view interpolation and depth reconstruction. Collision, save state, and netcode remain owned by the underlying environmental entity, not the visual card.
Rejected Alternatives: heavy LOD2 meshes, live capture, or simulating parallax with runtime mesh deformation.
Scalability potential: Low swaps earlier with cheaper shader path; Middle uses 16 frames; High/Ultra spend saved frame time on atlas fidelity, normal/depth fidelity, and longer 3D-to-card crossfade.
Hardware Impact: Expected to remove far-horizon vertex cost and overdraw from complex wreck silhouettes. Exact microseconds: PENDING FRAME DEBUGGER.

## Decision 04 - Replace ReadPixels Baker

Problem: Existing `HectonOctahedralImpostorBaker` used `RenderTexture.active`, `Texture2D.ReadPixels`, and `EncodeToPNG`, causing editor stalls and managed PNG byte arrays.
Solution: Replaced it with an Editor-only RenderTexture pipeline: command-buffer RT clear, replacement-shader capture, compute packing, depth-aware dilation, `AsyncGPUReadback`, and `ImageConversion.EncodeNativeArrayToPNG`.
Rejected Alternatives: Leaving ReadPixels as "editor-only acceptable"; adding runtime refresh cameras; using Amplify runtime billboard derivation.
Scalability potential: Low uses 8 views / 2048 / early swap, Middle 16 / 4096, High 16-32 / 4096, Ultra 32 / 8192 with VRAM warning.
Hardware Impact: Expected i3/MX350 gain is not from bake speed; it is runtime geometry collapse to one quad. Editor stall reduction is pending Unity stopwatch proof.

## Decision 05 - Static Purge Boundary

Problem: The word `BillboardRenderer` exists as a field name in legacy `World/ImpostorSystem`, but it is a generic `Renderer`, not Unity's built-in `BillboardRenderer` component.
Solution: Do not delete unrelated world code. Static reports flag real Unity BillboardRenderer/YAML tree evidence only. SHINOBU baker output owns new HLOD impostors.
Rejected Alternatives: Textual deletion by name, which would break unrelated runtime pooling and violate domain boundary.
Scalability potential: Low through Ultra all use the same generated atlas/material contract; legacy world code remains untouched until an owner replaces it.
Hardware Impact: Avoided breakage; microseconds saved come from the new HLOD path, not from deleting a non-matching field.

## Decision 06 - CPU Build Gate

Problem: Project rules forbid dotnet build when CPU is above 50%.
Solution: Checked CPU and compiler processes before attempting compile. CPU sample returned 100%; no csc/dotnet was active. Build is deferred.
Rejected Alternatives: Running dotnet build anyway to satisfy a checklist.
Scalability potential: No runtime impact.
Hardware Impact: Avoids starving parallel agents and Unity import workers; exact gain not applicable.

## Decision 07 - Full-Sphere Fibonacci Rig

Problem: Giant underwater objects can be approached from above, below, and lateral swim paths. Eight cardinal captures produce visible snapping.
Solution: Use a Burst `CalculateCaptureAnglesJob` with Fibonacci distribution for 16+ views and store camera position, view matrix, and projection matrix in unmanaged records.
Rejected Alternatives: Hand-authored view arrays, cardinal octa-only angles, managed `List<Matrix4x4>`.
Scalability potential: Low 8 views, Middle 16, High 16-32, Ultra 32 with larger atlas.
Hardware Impact: Runtime saves vertex/raster cost; bake angle generation target <25 us for 16 views, pending Unity benchmark.

## Decision 08 - Two-Atlas Runtime Bind

Problem: Per-view texture assets would create VRAM churn and material swaps.
Solution: Pack all frames into albedo-depth and normal-XY atlases via `PackImpostorAtlas.compute`; renderer binds exactly the atlas pair.
Rejected Alternatives: one texture per view, Texture2DArray requiring custom asset import policy, CPU pixel packing.
Scalability potential: Same shader path across Low/Middle/High/Ultra; only grid/resolution/view count changes.
Hardware Impact: Runtime material/texture churn reduced to stable atlas binds. Exact microseconds pending Frame Debugger.

## Decision 09 - Dilation On Depth Occupancy

Problem: Empty border pixels bleed into mips and create dark halos around far impostors.
Solution: Compute dilation searches nearest valid depth/occupancy pixel and writes albedo/normal into empty pixels before import mips.
Rejected Alternatives: disabling mips, increasing alpha clip until silhouette erodes, CPU dilation loops.
Scalability potential: Low radius 2, Middle 4, High 6, Ultra 8+ if atlas memory is approved.
Hardware Impact: Editor-only compute cost; runtime visual stability improves without extra draw cost.

## Decision 10 - Rollback Exclusion

Problem: Visual LOD matrices are presentation. Hashing them would make clients desync based on quality and distance.
Solution: Add rollback fence validator/report. Existing netcode descriptors only hash authoritative leaves; tail descriptors are `PresentationExcluded`.
Rejected Alternatives: adding HLOD DTOs to rollback leaves or synchronizing impostor selection.
Scalability potential: Quality can vary per client without network divergence.
Hardware Impact: No runtime networking payload added.

## Decision 11 - Forge / CSV Boundary

Problem: Artists need batch control without editing code, but string-heavy CSV parsing adds avoidable editor churn.
Solution: UI Toolkit forge owns interaction; CSV parser reads bytes into `FixedString64Bytes` records and numeric fields without `string.Split`.
Rejected Alternatives: IMGUI one-shot button, ScriptableObject-only profiles, raw text Split parser.
Scalability potential: Recipes cover Low/Middle/High/Ultra with continuous settings, not binary switches.
Hardware Impact: Editor-only; prevents bad profile choices that would ship oversized atlases to low-end hardware.

## Decision 12 - Burst Alias and Compile Flags

Problem: The first SHINOBU Burst jobs used fast math but not synchronous compile or explicit alias proof.
Solution: Added `CompileSynchronously = true`, `FloatMode.Fast`, `FloatPrecision.Standard`, and `[NoAlias]` to the output arrays in `CalculateCaptureAnglesJob` and `GenerateMockCaptureTargetJob`.
Rejected Alternatives: Allowing Burst to assume NativeArray aliasing or accepting async editor JIT latency.
Scalability potential: Low through Ultra use the same deterministic capture rig; higher tiers only increase view/atlas counts.
Hardware Impact: Expected gain is lower editor bake jitter and better SIMD eligibility; exact microseconds pending Unity benchmark.

## Decision 13 - Renderer Native Ownership Eviction

Problem: `HectonOctahedralImpostorRenderer` owned a persistent private `NativeArray<OctahedralImpostorInstance>` upload cache, which violates the Vault/H-Phi rule even if the data is visual.
Solution: Removed the cache. Runtime now uploads caller-owned NativeArrays directly or writes generated fallback/HLOD instances straight into `GraphicsBuffer.LockBufferForWrite` with unlock in `finally`.
Rejected Alternatives: Keeping a renderer-owned native upload cache and calling it "presentation only".
Scalability potential: Low uses the same direct GPU upload route as Ultra; quality changes atlas/view payloads, not memory ownership.
Hardware Impact: Removes one persistent native allocation and one CPU-side copy during HLOD binds; exact microseconds pending profiler.

## Decision 14 - Continuous Quality Adapter

Problem: SHINOBU renderer read `GlobalRegistry.ScalabilityTier` during the render tick, creating binary-tier behavior and hot registry dependency.
Solution: The renderer now derives shader weight and culling adapter tier from continuous `HomeostasisBrain.GlobalQualityWeight`. `ResolveContinuousEnterDistanceMeters` remains the distance-swap authority.
Rejected Alternatives: `if low tier` flags in Tick or direct registry tier reads every frame.
Scalability potential: 0.0-1.0 quality weight smoothly moves survival, middle, high, and ultra impostor behavior without a hard device switch.
Hardware Impact: Avoids hot registry reads and prevents sudden HLOD popping on thermal changes; exact frame delta pending profiler.

## Decision 15 - Forge Native State Reduction

Problem: The editor Forge window kept persistent private NativeArrays for profiles and preview records.
Solution: Converted them to cold managed editor caches and short-lived TempJob preview records disposed after each preview build. The parser still consumes `ReadOnlySpan<byte>` and writes fixed-string profile records.
Rejected Alternatives: Treating editor persistent NativeArrays as harmless and leaving a false H-Phi exception in SHINOBU files.
Scalability potential: Tooling can still author Low/Middle/High/Ultra profiles; runtime receives only baked assets.
Hardware Impact: Editor-only; removes native lifetime risk and hidden memory ownership.

## Decision 16 - Forensic Self-Audit Artifact

Problem: The previous inline self-audit did not prove task reconciliation, struct byte layout, H-Phi status, no-alias graph, compile guard, or Dear Lie complexity.
Solution: Added `Docs/Reports/SHINOBU_212_SELF_AUDIT.xml` and validated it as XML.
Rejected Alternatives: Chat-only report or vague "complete" statement.
Scalability potential: Audit records the continuous quality path and offline/GPU presentation boundary for future agents.
Hardware Impact: No runtime cost; reduces integration risk before Unity import.

## Decision 17 - Runtime Fallback Asset Removal

Problem: The renderer still had lazy runtime fallback creation for quad mesh, material, shader lookup, and managed vertex/index arrays.
Solution: Removed the fallback. Runtime now consumes only baked mesh/material/data assets. If they are missing, the renderer draws nothing and reports no fake readiness.
Rejected Alternatives: Keeping a first-draw fallback to hide authoring errors. That violates the offline asset contract and creates runtime allocations/material clones.
Scalability potential: Low through Ultra use the same pre-baked artifact route; quality changes shader samples and residency distance, not runtime asset generation.
Hardware Impact: Removes first-draw managed allocations and avoids material clone/SRP batching risk. Exact microseconds pending profiler.

## Decision 18 - Dispatcher Time Discipline

Problem: SHINOBU renderer read `Time.time` and `Time.frameCount` inside the tick/render path.
Solution: Replaced both with dispatcher `deltaTime` accumulation and a local tick counter. Matrix-fade age now reads `_impostorTimeSeconds`.
Rejected Alternatives: Treating Unity global time as acceptable because the system is presentation-only.
Scalability potential: Continuous quality and fade logic now move with dispatcher time instead of hidden Unity frame globals.
Hardware Impact: Small branch/global-read reduction; more important is deterministic instrumentation. Exact microseconds pending profiler.

## Decision 19 - Editor Recipe DTO Alignment

Problem: `HlodImpostorBakeSettings` carried a managed string and the CSV profile record had implicit layout.
Solution: Converted both to explicit 96-byte records with `FixedString64Bytes` at offset 0, numeric fields at aligned offsets, and explicit padding through byte 95.
Rejected Alternatives: Assuming editor-only DTOs do not matter. The Forge feeds the bake pipeline and audit layout claims must be source-backed.
Scalability potential: Designers can still tune Low/Middle/High/Ultra profiles from CSV without C# recompilation; profile records remain byte-stable.
Hardware Impact: Editor-only; removes managed recipe string from the settings DTO and reduces layout drift risk.

## Decision 20 - Low-Quality Shader Sample Collapse

Problem: The shader blended continuously but still sampled primary and secondary atlas frames at q=0.1.
Solution: Added a smooth quality gate. Below q=0.22 the shader samples one frame; q=0.22..0.55 restores secondary sampling through `smoothstep`.
Rejected Alternatives: Always sampling two frames and calling the blend weight "continuous scalability".
Scalability potential: Weak devices drop texture bandwidth while middle/high regain parallax interpolation smoothly.
Hardware Impact: Survival quality skips two texture samples per kept pixel. Exact microseconds pending GPU profiler.

## Decision 21 - Core Floating Origin Boundary

Problem: `HectonOctahedralImpostorRenderer` still resolved `_GlobalFloatingOffset` through `HectonMapMagicVegetationBridge`, creating a concrete world-generation bridge dependency in the SHINOBU presentation renderer.
Solution: Route floating-offset reads through `HectonFloatingOrigin.CurrentTotalOffset`, the existing core authority already used by other AUP-sensitive systems.
Rejected Alternatives: Keeping the MapMagic bridge as a shortcut or adding a SHINOBU-local offset cache. The former couples a renderer to a terrain bridge; the latter creates shadow spatial state.
Scalability potential: Low/Middle/High/Ultra all use the same origin authority; only impostor distance, atlas sampling, and real-geometry residency vary with `GlobalQualityWeight`.
Hardware Impact: No direct microsecond claim. The gain is compile-wall containment and removal of one cross-domain presentation dependency before Unity import.

## Decision 22 - SRP Batcher Uniform Boundary and Material Churn

Problem: The impostor shaders kept `_HectonImpostorTimeSeconds`, `_HectonImpostorFadeOutSeconds`, `_HectonUseVisibleMatrixStream`, and `_GlobalFloatingOffset` as loose uniforms while the renderer wrote them as material data. The renderer also polled static ScriptableObject atlas metadata every Tick before deciding whether to set material values.
Solution: Move the dynamic scalar/vector material fields into `CBUFFER_START(UnityPerMaterial)` in both the active and legacy impostor shaders, include `Hecton_Impostor.hlsl` after that declaration, and gate static material refresh by dirty/material/data identity. Floating-origin vector writes now happen only on material change or origin-shift/value change.
Rejected Alternatives: Leaving loose uniforms and relying on Unity to keep SRP Batcher behavior acceptable; keeping per-frame static data polling because the setter cache avoided some writes.
Scalability potential: Low devices avoid unnecessary material metadata churn while still using one-view sampling under low quality; high/ultra keep the same CBUFFER ABI and spend cost on atlas fidelity and two-view interpolation.
Hardware Impact: Removes steady-state ScriptableObject property reads, one Vector4 construction, and one material vector upload on non-origin-shift frames per active SHINOBU renderer. Exact microseconds pending profiler.

## Decision 23 - Args Buffer Rebind and Payload Fail-Closed

Problem: The renderer could recreate `_argsBuffer` after release while retaining `_argsMesh` and `_lastArgsInstanceCount`, allowing `EnsureIndirectArgsBuffer` to skip writing a freshly allocated indirect-args buffer. It also accepted missing atlas/data payloads, which could draw with stale material textures from a previous baked impostor.
Solution: Reset args mesh/count caches on args-buffer allocation and release, unlock the indirect args write in `finally`, and make static atlas binding return a validity bit. `Tick` now returns before drawing when data, albedo-depth atlas, or normal-depth atlas is missing.
Rejected Alternatives: Clearing shared material textures on missing data or relying on authoring discipline. Clearing shared materials risks damaging other renderers; relying on authoring discipline leaves stale payload draw hazards.
Scalability potential: Low/Middle/High/Ultra all use the same baked-payload contract; continuous quality only changes distance/sample cost after payload validity is proven.
Hardware Impact: Prevents a stale/zero indirect-args draw after GPU buffer recreation and avoids wrong-atlas overdraw. Exact microseconds are pending profiler; the primary gain is correctness under enable/disable and rebind cycles.

## Decision 24 - NaN Vaccination Across Bake and Shader Payload

Problem: SHINOBU still accepted non-finite imported bounds, mock extents, quality values, atlas samples, visible-instance matrix values, and captured depth/normal pixels. One NaN could propagate into capture matrices, baked atlases, lighting, fog, or `SV_Depth`, corrupting a far-horizon draw at exactly the distance where debugging is hardest.
Solution: Add finite fallback guards at every SHINOBU-owned boundary: DTO creation, residency distance resolution, Burst capture angle generation, mock point generation, atlas packing, dilation, shared impostor HLSL, active shader, and legacy shader. Invalid centers become zero-local, sizes become at least 0.5m, quality collapses to the minimum-survival scalar, missing depth becomes empty occupancy, normals fall back to up, and depth output falls back to a finite device depth.
Rejected Alternatives: Trusting imported prefab bounds, assuming replacement shaders cannot output NaN, or relying on `saturate` to clean non-finite values after the fact. `saturate(NaN)` is not a reliable architectural boundary, and late clipping does not protect `SV_Depth` or interpolated lighting.
Scalability potential: Low devices get the same one-view sample-collapse path but now with deterministic finite fallbacks under malformed assets or thermal-quality input. Middle/High/Ultra keep two-view interpolation and richer atlas data without risking NaN contamination from a single bad tile.
Hardware Impact: Adds a few scalar/vector finite checks in shader and editor compute paths. The cost is bounded and cheaper than a poisoned depth/color path causing overdraw artifacts or GPU debugging stalls; exact runtime microseconds remain pending profiler.

## Decision 25 - Reversed-Z Depth Bias Direction

Problem: The active and legacy impostor shaders finite-guarded `SV_Depth`, but still subtracted `depthOffset` when `UNITY_REVERSED_Z` was defined. Local render mandate Section10 forbids subtracting reversed-Z bias because it moves depth in the wrong direction for the project depth contract.
Solution: Change both `Hecton_HLOD_Impostor.shader` and `Hecton_OctahedralImpostor.shader` to add `depthOffset` in the reversed-Z branch. The impostor still decodes captured depth from atlas alpha, but the bias sign now matches the engine-wide reversed-Z rule.
Rejected Alternatives: Keeping the old sign because the value was finite, removing `SV_Depth`, or adding a runtime physics/depth proxy mesh. Those options either preserve incorrect occlusion/fog ordering or reintroduce geometry cost that SHINOBU exists to remove.
Scalability potential: Low keeps the same one-view sample collapse and now gets stable depth ordering on cheap reversed-Z devices. Middle/High/Ultra keep two-view parallax and richer atlases without depth-bias sign drift against fog, DoF, and occlusion.
Hardware Impact: No intentional ALU increase. This is a sign correction on an existing scalar path; the saved cost remains the Dear Lie geometry collapse to one quad and low-quality one-view sampling. Exact microseconds remain pending profiler.

## Decision 26 - Binary Tier API Residue Removal

Problem: `HectonChunkImpostorResidency` still exposed unused tier-based helpers (`IsLowTier`, `ResolveFlags(... HectonQualityTier)`, and `ResolveTierRepresentativeQuality`) after SHINOBU had moved the active path to continuous `GlobalQualityWeight`. It also exported a `FlagLowTierSnap` name into the HLOD payload surface.
Solution: Remove the unused tier helper APIs and keep only the float-weight `ResolveFlags` path. Rename the payload flag to `FlagSurvivalSnap` and update the single streaming caller that writes this SHINOBU-owned flag.
Rejected Alternatives: Leaving dead compatibility helpers as harmless, or refactoring the whole `WorldChunkResidencyManager` tier resolver. The first invites new binary callsites; the second is outside SHINOBU ownership and risks a compile-wall conflict.
Scalability potential: SHINOBU flag resolution now forces callers toward the continuous weight path. The remaining streaming tier branch is not expanded; future work can route chunk streaming itself through Homeostasis quality without needing a SHINOBU API change.
Hardware Impact: No frame-time claim. This is API-surface hardening that prevents future binary behavior in the impostor residency helper.

## Decision 27 - Branchless Continuous Swap-Distance Curve

Problem: `ResolveContinuousEnterDistanceMeters` consumed `GlobalQualityWeight`, but its implementation still selected the lower or upper curve with a hard `q < 0.5f` branch. The output was continuous, but the implementation still carried a binary quality split.
Solution: Compute both curve halves and blend them through `math.smoothstep(0.45f, 0.55f, q)`. The helper now uses `math.lerp`, `math.saturate`, and `math.smoothstep` without a quality-threshold branch.
Rejected Alternatives: Keeping the branch because the result did not visibly pop. That leaves a pattern future callers can copy into real binary behavior.
Scalability potential: Survival devices still swap earlier, middle devices stay near base distance, and high/ultra extend real-geometry residency. The transition is now a smooth mathematical blend through the midpoint instead of a branch.
Hardware Impact: Removes one scalar branch from the residency helper. Exact microseconds remain pending profiler; the practical value is mechanical enforcement of the continuum contract.
