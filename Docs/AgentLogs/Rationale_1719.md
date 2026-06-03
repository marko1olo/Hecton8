# Rationale 1719 - Caustic Projection And Optics Baker

Status: PENDING VERIFICATION

## Decision 0 - Scope And Mandates

Problem: Runtime caustic simulation or volumetric photon/ray work would violate the visual-fake and MX350 frame-budget laws.
Solution: Keep optical simulation inside an Editor-only baker under `Assets/_Project/Editor/Bakers/`; runtime receives compressed, repeat-wrapped atlas/cookie assets only.
Rejected Alternatives: Real-time volumetric caustic raymarching, runtime `RenderTexture` caustic generation, and runtime `Texture2D.SetPixels`; all spend frame time on a presentation effect that can be pre-baked.
Scalability potential: Low emits smaller atlases with no spectral split; Middle adds more frames/resolution; High enables full RGB spectral dispersion; Ultra increases ray/sample density and atlas resolution for visual overkill.
Hardware Impact: i3/MX350 keeps steady-state caustics as texture sampling/projection, avoiding fill-rate-heavy raymarching and managed texture churn. Runtime savings are static-estimated only until profiler proof exists.

## Decision 1 - Output Path

Problem: The task text named `Assets/Art/Textures/Lighting`, but AGENTS.md defines first-party assets under `Assets/_Project/`.
Solution: Default output path is `Assets/_Project/Art/Textures/Lighting`, while the EditorWindow keeps the output folder editable.
Rejected Alternatives: Writing to `Assets/Art` would create a second first-party asset root and violate the project folder contract.
Scalability potential: Low/Middle/High/Ultra variants can coexist under one lighting texture folder with stable `TX_CausticFlipbook_*` naming.
Hardware Impact: No direct CPU gain. Prevents duplicate asset roots and accidental untracked texture residency.

## Decision 2 - Existing Runtime Route

Problem: `Assets/_Project/Scripts/Rendering/AbyssalCaustics` already owns a DataVault-fed RenderGraph fullscreen caustics route.
Solution: Do not rewrite it in this task. Add an offline atlas/cookie baker in editor space only.
Rejected Alternatives: Replacing the active runtime feature would create integration risk outside the direct baker requirement.
Scalability potential: Cheap devices can use smaller generated cookies; high devices can either use richer cookies or the existing runtime feature if separately profiled.
Hardware Impact: No runtime code added. Existing runtime cost is unchanged; baked-cookie route gives a lower-cost alternative for scenes that can bind Light cookies.

## Decision 3 - Optical Algorithm

Problem: Full photon splatting needs atomic accumulation or large scratch buffers and risks editor stalls at 4096 atlases.
Solution: Use a Burst `IJobParallelFor` per output pixel, periodic Gerstner-height normals, Snell refraction, and local Jacobian/determinant convergence to estimate caustic intensity.
Rejected Alternatives: Forward photon splatting with random rays, compute shader readback, and real-time raymarching. Those add contention, GPU dependency, or runtime cost.
Scalability potential: Low uses 1024-class atlas and near-zero spectral weight; Middle increases grid/frame resolution; High/Ultra uses larger grid and full spectral split.
Hardware Impact: Runtime gets a texture lookup/projection route. Editor bake cost is cold and Burst-parallel.

## Decision 4 - Spectral Packing And Import

Problem: Separate channel textures would waste VRAM and texture samples.
Solution: Pack red, green, and blue wavelength intensity directly into RGB. Enforce sRGB, mipmaps, Repeat, and Standalone BC7 import settings for flipbooks; enforce non-sRGB Clamp BC4 mask for waterline clipping.
Rejected Alternatives: EXR/uncompressed output and separate R/G/B assets. Both exceed texture discipline for an effect that can live in one compressed sample.
Scalability potential: Weak devices can bake smaller/no-dispersion atlases; middle/high/ultra can increase atlas and spectral strength without changing runtime DTOs or save identity.
Hardware Impact: 4096 BC7 flipbook is ~16 MB. Three 4096 variants are ~48 MB, under the requested 65 MB ceiling and below the MX350 900 MB texture budget slice.

## Decision 5 - Compaction Fence And Runtime Truth

Problem: A caustic renderer can be tempted to read waterline/player DataVault state directly for clipping and projection.
Solution: This baker reads no runtime vault state. It produces a static waterline mask and documents UV/texture contract; runtime clipping must consume owner-published snapshots in a separate route.
Rejected Alternatives: `GlobalDataVault.TryGetLatestCreated()` or hot DataVault pointer reads in the baker/runtime projection path.
Scalability potential: The mask is constant low-res; richer devices can use higher precision shader clipping later without changing the baked atlas.
Hardware Impact: Avoids hot pointer/fence races and keeps lighting projection independent of compaction windows.

## Decision 6 - Build Gate

Problem: Task 19 requests `dotnet build`, but the host had CPU_LOAD=90 with active `dotnet` PID 3100, then CPU_LOAD=100 with active `dotnet` PIDs 3100 and 32280.
Solution: Refused `dotnet build`; used Unity MCP `validate_script` instead. Result: success, 0 errors, 0 warnings.
Rejected Alternatives: Starting a second build under load. That directly violates the prompt guard.
Scalability potential: No runtime effect.
Hardware Impact: Prevented host contention and avoided a false compile wall caused by concurrent compiler pressure.

## Decision 7 - Apex Polish Source-First Cleanup

Problem: The first baker version duplicated asset-folder, atomic-write, rollback, JSON, SHA, and fault-dump helpers already covered by first-party editor infrastructure or rejected by the later source-first directive.
Solution: Reuse `ProceduralTextureBaker` for folder normalization, rollback snapshots, atomic writes, and AssetDatabase finalization. Remove JSON/SHA/dump paths from the caustic baker. Add `UnsafeUtility.SizeOf<T>()` layout gate for the unmanaged job payload.
Rejected Alternatives: Keeping a parallel baker utility stack. That increases maintenance surface and leaves stale proof artifacts beside the source.
Scalability potential: Low/Middle/High/Ultra texture outputs still scale through `GlobalQualityWeight`; proof no longer adds unrelated disk I/O.
Hardware Impact: Runtime unchanged for the offline baker. Editor bake failure handling is safer because partial outputs roll back through the shared transaction layer.

## Decision 8 - DataVault Write-Lock Flattening

Problem: `AbyssalDeferredCausticsRuntime` calculated caustic parameters while holding the parameters write lock.
Solution: Build input snapshots and run caustic parameter jobs into stack scratch before acquiring the write lock; under lock, copy only pending/active DTOs and set `_pendingGpuUpload`.
Rejected Alternatives: Leaving math inside the lock because it already validated. That keeps a compaction-fence stall vector.
Scalability potential: Weak devices get shorter lock windows; high/ultra can raise visual richness without extending DataVault lock duration.
Hardware Impact: i3/MX350 main-thread write-lock section is now bounded to two `CausticsParametersDTO` assignments and one flag write, pending profiler proof.

## Decision 9 - Baked Atlas Binding In Existing RenderGraph Route

Problem: The baker produced the requested RGB flipbook, but the active deferred caustics shader still spent per-pixel ALU on procedural Voronoi caustics and had no source-level route for the offline atlas.
Solution: Extend `HectonDeferredCausticsFeature` and `Hecton_DeferredCaustics.shader` in place. Optional atlas/mask textures are serialized on the existing feature and applied to the existing material during cold `Create`/`OnValidate`; null textures force zero weights and keep the old procedural fallback.
Rejected Alternatives: A new projector, light-cookie manager, runtime RenderTexture generator, or second caustics service. Those would duplicate route ownership and raise setup/runtime cost.
Scalability potential: Low can bind a small atlas with one frame sample and no procedural branch; Middle/High/Ultra can increase atlas resolution, frame interpolation, and spectral bake richness through continuous weights without changing DTO layout.
Hardware Impact: MX350 shifts caustic detail from shader Voronoi ALU to precompressed texture bandwidth when an atlas is assigned. Exact GPU microsecond delta is pending Frame Debugger/Profiler capture.

## Decision 10 - Renderer Asset Cold Bind

Problem: A baked caustic atlas is not operational if every renderer feature must be wired by hand after the bake.
Solution: Add `Bake Default And Bind Renderers` and window `Bake Flipbook And Bind Renderers` to `CausticOpticsBaker1719.cs`. The baker loads the generated atlas/mask assets, finds existing `HectonDeferredCausticsFeature` references in the four first-party renderer assets, and writes only serialized feature settings.
Rejected Alternatives: A separate installer utility, direct YAML writes, or a runtime discovery path. Separate tooling duplicates ownership; YAML writes bypass Unity serialization; runtime discovery would add scene/asset lookup risk.
Scalability potential: Low/Mobile/Quest can bind smaller atlases with one-frame sampling; PC/PC_High can bind larger frame grids and allow shader frame blending through the same serialized fields.
Hardware Impact: No runtime lookup or allocation is introduced. Setup is an Editor-only asset serialization pass; steady-state renderer receives already-bound textures during cold material creation.

## Decision 11 - Light Cookie Fallback And Atlas Cell Inset

Problem: The animated flipbook route is correct for the deferred shader, but Unity `Light.cookie` fallback needs a single cookie-ready texture and the mipped atlas can bleed between adjacent frame cells.
Solution: Extract frame 0 into `TX_CausticLightCookie_*` during the same editor bake transaction, with rollback and compressed repeat/mip import. In the shader, inset atlas sampling inside each frame cell to reduce bilinear and mip bleed.
Rejected Alternatives: Auto-assigning cookies into open scenes or disabling flipbook mipmaps. Auto scene edits can corrupt authored lighting; disabling mips violates the importer requirement and increases shimmer.
Scalability potential: Low/Quest can use the static cookie asset when animated caustic pass cost is not acceptable; Middle/High/Ultra can use the animated atlas route with cleaner mip behavior.
Hardware Impact: Cookie extraction is editor-only. Shader inset adds coordinate ALU but avoids visible atlas-frame contamination; measured GPU cost remains pending profiler proof.

## Decision 12 - Explicit Cookie Assignment And Stable Atlas Inset Params

Problem: Emitting a cookie asset is not enough for a usable Light Cookie workflow, and shader-side texture-dimension queries add unnecessary API compatibility risk.
Solution: Add an explicit selected-light cookie assignment action to the baker window and feed `_HectonBakedCausticAtlasTexelParams` from `HectonDeferredCausticsFeature` during cold material setup.
Rejected Alternatives: Auto-mutating every scene light after bake or keeping `Texture2D.GetDimensions` in the shader. Auto mutation violates authorship; shader metadata queries are avoidable because atlas dimensions are known on CPU.
Scalability potential: Low/Quest can bind a static selected-light cookie; Middle/High/Ultra can still use the animated deferred atlas with cleaner cell sampling. The same generated assets serve both routes.
Hardware Impact: Runtime remains 0 managed allocation. The new selected-light assignment is editor-only, and the texel vector is a material constant set outside frame hot loops.

## Decision 13 - Cookie Import Type And 2D Light Eligibility

Problem: A `TX_CausticLightCookie_*` texture imported as `Default` can work as a generic texture, but it is not the strict Unity light-cookie import route. A 2D cookie can also be assigned accidentally to incompatible Point/Area lights.
Solution: Import the cookie derivative as `TextureImporterType.Cookie` with explicit 2D shape and keep Repeat wrapping. Gate selected-light assignment to Directional and Spot lights only.
Rejected Alternatives: Leaving the cookie as `Default`, or assigning to every selected `Light`. Default import is too loose for a source-controlled fallback asset; Point/Area assignment creates invalid or misleading scene state.
Scalability potential: Low/Quest can use a directional static cookie as the cheapest caustic projection. Middle/High/Ultra retain the animated baked atlas route and may still use Spot cookie authoring where a local cave light is intentional.
Hardware Impact: No runtime CPU/GC cost. Importer correctness improves asset residency and authoring behavior; compatibility gate prevents bad scene data.

## Decision 14 - Importer Self-Audit And Caustics Ownership Labels

Problem: Texture importer assignment is not proof that Unity persisted the intended platform overrides after `SaveAndReimport`, and caustics runtime comments still carried inherited `13KRA` ownership language.
Solution: Add a post-reimport importer validator that checks type, shape, color space, mipmaps, wrap/filter, readability, max size, and Standalone/Android overrides. Rename the caustics fault dump route to `Docs/AgentLogs/Dump_1719.bin` and replace inherited comments with caustics-owned wording.
Rejected Alternatives: Treating setter calls as sufficient proof, or leaving stale 13KRA labels in a 1719-owned caustics patch. Both weaken source-level ownership and can hide a broken asset-import contract.
Scalability potential: Low/Middle/High/Ultra outputs all pass the same importer gate; resolution and spectral richness can scale continuously without changing the import contract or runtime DTO ownership.
Hardware Impact: Runtime cost is 0 us. The gate prevents uncompressed/readable or non-cookie assets from silently reaching weak devices where VRAM and sampling cost are most constrained.
