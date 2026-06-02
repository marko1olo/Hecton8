# Rationale 1608

Date: 2026-06-01
Status: PENDING VERIFICATION

## D00 - Domain Containment

Problem: Agent 1608 must add dense interior presentation detail without adding runtime GameObject or material overhead.
Solution: Confine implementation to `Assets/_Project/Editor/Generators/Interiors/`; use Editor-only generation, Burst-compatible unmanaged DTOs, mesh fusion, atlas remapping, baked normal/grime masks, and deterministic socket selection.
Rejected Alternatives: Runtime placement was rejected because it adds hierarchy, material churn, draw-call pressure, and violates the prompt. Raw prefab YAML mutation was rejected because Unity prefab FileID alignment is fragile.
Scalability potential: Low uses baked normals, fused static bases, one atlas, sparse physical handles. Middle increases stamp/cable density. High keeps longer near-field instrument fidelity. Ultra adds denser grime, labels, and cable bundles without changing gameplay truth.
Hardware Impact: i3/MX350 expected gain is avoided transform traversal and material binds. Exact microseconds are PENDING VERIFICATION until Unity profiler/Frame Debugger data exists.

## D01 - Missing Source Library Boundary

Problem: `Assets/_Project/Prefabs/Instruments/` and `Assets/_Project/Editor/Generators/Interiors/` are absent.
Solution: Create schema-first Editor tooling that can operate with missing libraries by producing deterministic fallback instrument definitions and a generated ledger instead of inventing runtime dependencies.
Rejected Alternatives: Blocking on absent prefabs was rejected because the task requires pipeline materialization. Pulling data from unrelated runtime systems was rejected because it would cross domain boundaries.
Scalability potential: Low fallback definitions keep one-material procedural mockups; higher lanes can swap authored prefabs into the same schema.
Hardware Impact: Prevents dependency waits and keeps editor-generated assets deterministic. Runtime gain remains PENDING VERIFICATION.

## D02 - Deterministic Socket Selection

Problem: Decorative socket placement must be reproducible without using Unity random, wall-clock time, transform instance IDs, or future 1607 code.
Solution: `PopulateSocketsJob1608` selects instruments with integer hash thresholds from seed + stable socket hash + rule hash. Density is a continuous `DensityWeight` probability gate; `GlobalQualityWeight` only scales visual weight units, not truth layout type.
Rejected Alternatives: `UnityEngine.Random.Range` was rejected as nondeterministic authority. Direct dependency on absent `DecorativeSocketDTO` classes was rejected; parser accepts transform metadata now and generated mesh metadata can be added behind the same DTO later.
Scalability potential: Low keeps fewer placed details through continuous density. Middle/High/Ultra increase visual diversity through weight and density without changing runtime authority.
Hardware Impact: i3/MX350 avoids runtime selection entirely because baked prefab contains final placements. Expected runtime CPU saved: transform/search path removed, exact microseconds PENDING VERIFICATION.

## D03 - Static Base Fusion

Problem: Instrument bases are visual mass but do not need separate GameObjects.
Solution: `WeldInstrumentBasesJob1608` transforms static base vertices into room-local space and writes one fused static mesh. Moving handles remain separate named transforms only.
Rejected Alternatives: Keeping each base prefab was rejected because transform traversal and renderer submission scale linearly with clutter. Static batching alone was rejected because it still carries authoring hierarchy and material fragmentation risk.
Scalability potential: Low uses fused bases plus normal stamps. Middle adds more sockets. High/Ultra can add more moving handles and cable bundles while static bases stay one mesh.
Hardware Impact: i3/MX350 expected gain is lower transform traversal and fewer renderers. Measured microseconds: PENDING UNITY PROFILER.

## D04 - Micro-Detail Baking

Problem: Rivets, seams, shallow labels, and dust pores below 5 cm would create polygon noise and hierarchy overhead.
Solution: `NormalMapStampingJob1608` projects deterministic stamp influence into normal and grime pixels offline. Task-level fake-first decision: physical screw meshes are rejected; surface belief is carried by normal/grime response.
Rejected Alternatives: Tiny mesh rivets were rejected under the 5 cm rule. Runtime decals were rejected because they add render work and state.
Scalability potential: Low keeps baked normal/grime at smaller texture size. Middle increases texture size. High/Ultra can increase stamp count and map resolution up to 4K without runtime CPU.
Hardware Impact: i3/MX350 saves draw calls and triangles; exact GPU/VRAM impact PENDING Frame Debugger.

## D05 - Atlas And Import Policy

Problem: Instrument variety can explode texture bindings and material count.
Solution: `InteriorAtlasPacker1608` packs instrument swatches into one power-of-two atlas and applies Standalone BC7 import settings. Normal/grime maps use compressed import settings with non-sRGB normal path.
Rejected Alternatives: Per-instrument material assignment was rejected because SetPass count is the bottleneck. Runtime texture arrays were rejected because the required output is baked prefab content.
Scalability potential: Low uses the same atlas at smaller source detail. Middle/High/Ultra can use 2K/4K atlas while keeping one material route.
Hardware Impact: i3/MX350 expected gain is fewer texture binds and safer residency. Atlas efficiency is reported by the tool; actual VRAM proof PENDING Unity import.

## D06 - Build Contention

Problem: Task 15 asks for `dotnet build`, but compile throttling is active. Current checks detect active `dotnet` processes during this pass.
Solution: Mark build as `BLOCKED_BY_CONTENTION`; use static source checks until CPU < 50% and no compiler/dotnet process exists.
Rejected Alternatives: Launching another build was rejected because it violates the user's explicit CPU protection order and the batch compile contention rule.
Scalability potential: Not a runtime feature. Protects the multi-agent host from avoidable compile stalls.
Hardware Impact: Host CPU preserved. Compile proof remains PENDING VERIFICATION.

## D07 - Render Vertex Packing

Problem: The fused authoring vertex DTO is 64 bytes, while the Unity vertex declaration for position/color/normal/tangent/uv/hash is 56 bytes.
Solution: Add `InteriorRenderVertexDTO1608` and pack fused/cable vertices into that DTO before `SetVertexBufferData`.
Rejected Alternatives: Changing the job DTO to 56 bytes was rejected because it would weaken ARM64 padding and authoring flags. Uploading the 64-byte DTO directly was rejected because Unity vertex stride mismatch can fail import or corrupt attributes.
Scalability potential: Low through Ultra use the same packed render route; higher tiers increase baked detail count without changing upload layout.
Hardware Impact: i3/MX350 avoids bad vertex streams and keeps vertex payload tight at 56 bytes. Measured microseconds remain PENDING UNITY IMPORT.

## D08 - Apex Source Proof And JSON Removal

Problem: The prior tool wrote JSON proof files, while the current integrator order accepts only compiling C# source and generated Unity assets as completion proof.
Solution: Remove `WriteLedger`, `WriteReport`, `JsonUtility`, report DTO classes, SHA-256 report hashing, and JSON file writes from the interior generator. Add NUnit source-audit gates for hot dependency lookup, runtime phase surface, DataVault lock absence, JSON absence, and no process/build launcher.
Rejected Alternatives: Keeping JSON as optional diagnostics was rejected because the user explicitly revoked JSON proof. Broad runtime rewrites outside 1608 were rejected because current 1608 authority is Editor-only interior finishing and raw cross-domain changes would exceed the assigned boundary.
Scalability potential: Low through Ultra now share the same asset-generation proof route; scaling lives in continuous density, texture size, and baked normal/cable detail, not reports.
Hardware Impact: CPU saved is build-host/editor I/O only. Runtime impact is unchanged by report removal; source proof remains PENDING UNITY IMPORT/TEST.

## D09 - Editor Scan Allocation Cleanup

Problem: The generator used array-returning `GetComponentsInChildren<T>()` in cold prefab scans and transform counts.
Solution: Replace those sites with bounded static scratch `List<Renderer>` and `List<Transform>` overloads, cleared in `finally`.
Rejected Alternatives: Leaving array allocations was acceptable for cold Editor code but rejected because batch cabin generation can process many prefabs and the fix is small. Moving this to runtime registries was rejected; this is not runtime gameplay.
Scalability potential: Low devices benefit indirectly by cheaper content bake iteration; high/ultra can process denser authored instrument sets without temporary array churn during baking.
Hardware Impact: Editor heap churn reduced during mass generation. Runtime microseconds unchanged; no Unity profiler run.

## D10 - Data Map Import And Movable Handle Visibility

Problem: The grime/occlusion map was imported through a boolean normal/color path that made non-normal maps sRGB, corrupting scalar dirt data. Movable instrument handles were empty GameObjects, so the prefab hierarchy preserved future interactivity anchors but produced no visible handle geometry.
Solution: Replace the boolean texture import flag with `InteriorTextureRole1608`; atlas stays sRGB BC7, normal becomes linear BC5 NormalMap, grime becomes linear BC7 data map. Add one shared generated movable-handle mesh asset and attach it to `MOV_InstrumentHandle_*` objects with shadow casting disabled.
Rejected Alternatives: Per-handle authored prefab instances were rejected because they reintroduce hierarchy and renderer variation. Runtime handle construction was rejected because 1608 output must be baked assets. Keeping grime as sRGB was rejected because occlusion masks are data, not color.
Scalability potential: Low uses the same single shared handle mesh and linear grime mask; middle/high/ultra spend saved hierarchy cost on denser placed handles and larger baked masks without gameplay truth drift.
Hardware Impact: Runtime transform count remains bounded to movable parts only. Small-handle shadow caster cost is avoided on i3/MX350. Exact microseconds remain PENDING UNITY IMPORT/PROFILER.

## D11 - Socket Parser Scratch Discipline

Problem: `CollectSockets` allocated `List<Transform>` per bake pass while scanning marker transforms. This is Editor-only, but batch generation over many cabins would produce avoidable heap churn.
Solution: Add a static bounded transform scratch list in `InteriorSocketParser1608` and clear it through `finally`.
Rejected Alternatives: Keeping the allocation was rejected because the fixed-capacity scratch route is trivial and matches existing renderer scratch policy. Reusing global registries was rejected because prefab import is cold Editor work.
Scalability potential: Low through Ultra keep identical generated assets; high-density authoring libraries can be scanned with less editor heap churn.
Hardware Impact: Runtime microseconds unchanged. Editor allocation churn reduced; exact bake-time delta PENDING UNITY PROFILE.

## D12 - Atlas Cell Buffer Reuse

Problem: Atlas packing allocated a fresh `Color32[]` block for every instrument cell. At the current 64-rule cap this is cold Editor churn, not runtime cost, but it scales badly during repeated cabin bakes.
Solution: Allocate one cell-sized block buffer per atlas pack and refill it for each cell before `Texture2D.SetPixels32`.
Rejected Alternatives: Writing pixels through unsafe texture memory was rejected because this is cold Editor asset generation and the managed block buffer is simpler and sufficient. Leaving per-cell allocation was rejected because one reusable buffer removes the churn without changing output.
Scalability potential: Low through Ultra use the same atlas route; higher density authoring libraries avoid per-rule temporary array churn.
Hardware Impact: Runtime microseconds unchanged. Editor heap churn reduced during pack; exact delta PENDING UNITY PROFILE.

## D13 - Socket-First Normal Stamping

Problem: `NormalMapStampingJob1608` scanned all micro-sockets from every texture pixel. At 2048/4096 maps and dense socket counts this creates an avoidable cold bake-time explosion.
Solution: Keep the same deterministic visual fake, but invert the pass: iterate micro-sockets in stable order and stamp only the bounded pixel rectangle touched by each radius.
Rejected Alternatives: Runtime decals and tiny rivet meshes were rejected by the visual-fake mandate. Spatial hash bins were rejected as unnecessary complexity for one cold editor bake pass. Keeping pixel-first scan was rejected because it scales as `pixels * sockets`.
Scalability potential: Low keeps smaller maps and fewer sockets; middle/high/ultra can afford denser micro detail because cost scales with touched stamp area instead of full texture area times socket count.
Hardware Impact: Runtime microseconds unchanged. Editor bake CPU reduced in dense maps; exact delta PENDING UNITY PROFILE.

## D14 - Unmanaged Native Conversion Boundary

Problem: `ToNative<T>` helpers used `where T : struct`, which is weaker than the DTO law for NativeArray/Burst conversion.
Solution: Tighten both helpers to `where T : unmanaged` and add a source-audit assertion.
Rejected Alternatives: Runtime type checks were rejected because compile-time generic constraints are cheaper and clearer. Leaving `struct` was rejected because it permits managed-field structs until Unity throws at import/runtime.
Scalability potential: No visual difference. The generation pipeline now rejects invalid DTO shapes earlier across all quality weights.
Hardware Impact: Runtime microseconds unchanged. Risk of hidden managed DTO conversion failure reduced; compile proof PENDING because build is throttled.

## D15 - Atlas Rect Integration

Problem: The generator packed instrument swatches into one atlas but did not apply per-instrument atlas rectangles to fused static-base UVs. This could produce a valid atlas with wrong sampling.
Solution: Add `ApplyAtlasRects`, reuse the same atlas grid calculation as packing, store rects in `InstrumentRuleDTO1608`, propagate scale/offset into placements, and remap fused vertex `Uv0` during `WeldInstrumentBasesJob1608`.
Rejected Alternatives: Per-material instrument preservation was rejected because it destroys the atlas/draw-call goal. A second post-fusion UV pass was rejected because the weld job already touches every fused vertex once. Runtime material overrides were rejected as presentation drift.
Scalability potential: Low keeps one atlas and correct sampling; higher tiers can add more instrument variety without extra material routes.
Hardware Impact: Runtime SetPass path remains one material. Exact GPU/CPU savings PENDING Frame Debugger; correctness fix is static-source only until Unity import.

## D16 - Atomic Mesh Fusion On Invalid Source Data

Problem: `WeldInstrumentBasesJob1608` could skip an invalid source vertex or triangle while still appending part of the placement, producing mismatched fused vertex/index buffers.
Solution: Add preflight `IsValidVertexSlice` and `IsValidTriangleSlice` passes before writing any vertices or indices for a placement. Invalid slices now set `FaultNonFinite` or `FaultInvalidMesh` and reject the whole placement atomically.
Rejected Alternatives: Removing partially written vertices/indices after detection was rejected because NativeList rollback inside the job is more fragile than preflight validation. Trusting source prefabs was rejected because generated assets must fail closed.
Scalability potential: No visual change on clean data. On bad authored instruments, low through ultra lanes fail the same deterministic way instead of producing corrupt meshes.
Hardware Impact: Small extra cold validation loop per placement; runtime cost zero. Corrupt mesh import risk reduced; Unity import proof PENDING.

## D17 - Procedural Instrument Glyphs

Problem: Fallback atlas cells were flat bordered color blocks. That satisfies material batching but does not read as gauges, buttons, or industrial instruments.
Solution: Add deterministic offline glyph marks per atlas cell: gauge ring, tick cross, needle line, dark edge, and screw center.
Rejected Alternatives: Runtime decals and per-instrument materials were rejected because the whole point is one atlas and no runtime presentation work. Waiting for authored textures was rejected because fallback output must be useful.
Scalability potential: Low gets readable instrument identity from the same atlas. Middle/high/ultra can raise texture size and socket density without changing runtime truth.
Hardware Impact: Runtime microseconds unchanged. Offline pixel fill adds tiny cold CPU cost; visual readability improves without extra draw calls.

## D18 - Occlusion Map Polarity

Problem: The grime texture was also assigned as `_OcclusionMap`, but its base pixels were zero. In Unity Lit occlusion semantics, black is fully occluded, so the whole instrument material could become globally dirty/dark instead of only stamped pores.
Solution: Initialize the occlusion map to white and make `NormalMapStampingJob1608` locally darken stamped pixels with `min`, not brighten with `max`. Add an edit test that requires both untouched white pixels and darkened stamped pixels.
Rejected Alternatives: Keeping the black base was rejected because it corrupts material response. Creating a custom shader was rejected because the existing URP Lit route is adequate for this editor-only asset pipeline.
Scalability potential: Low keeps readable instruments with a single shared occlusion map. Middle/high/ultra can increase stamp density and texture size without changing material count or runtime truth.
Hardware Impact: Runtime microseconds unchanged. Visual correctness improves without extra draw calls or shader variants; Unity material import proof remains PENDING.

## D19 - Integrated Static Cable Bundles

Problem: Cable generation existed as an isolated method and test, but `FinishInterior` did not place cable bundles into generated prefabs. The domain requirement asked for hanging cable clusters, not just a callable helper.
Solution: Add `CreateCableBundleMeshAsset` to pair ceiling/floor sockets, generate deterministic catenary cable parts, combine them into one static mesh asset, and attach a single `GEN_CableBundles_1608` child with shadows disabled.
Rejected Alternatives: Runtime cable physics, Unity joints, per-cable GameObjects, and per-segment simulation were rejected by the visual-fake and frame-time mandates. Merging cables into the static base mesh was rejected for this pass because the existing cable mesh path already produces one renderer and keeps asset ownership clear.
Scalability potential: Low uses fewer pairs, strands, and segments from continuous `GlobalQualityWeight`. Middle/high/ultra raise pair count, strand count, segment count, radius variation, and slack while preserving one cable renderer.
Hardware Impact: Runtime cost is one optional static renderer instead of many cable objects or physics constraints. Exact SetPass/triangle delta remains PENDING Frame Debugger.

## D20 - Baked Mesh CPU Copy Release

Problem: Generated static bases and handle proxies used `UploadMeshData(false)`, retaining CPU-readable mesh data after baking. That wastes memory for assets that are not runtime-deformed.
Solution: Switch final generated static base, combined cable, and movable handle meshes to `UploadMeshData(true)`. Temporary cable part meshes keep CPU data only until `CombineMeshes` consumes them.
Rejected Alternatives: Leaving CPU copies for convenience was rejected because these are baked presentation meshes. Runtime deformation was rejected because handles/cables are visual proxies, not simulation authority.
Scalability potential: Low benefits from lower CPU-side mesh memory. Middle/high/ultra can spend saved memory on denser baked detail, larger atlases, and richer near-field instrument markings.
Hardware Impact: Runtime memory retention reduced after import; exact MB saved depends on generated detail count and remains PENDING Unity import.

## D21 - Movable Rule ABI Flattening

Problem: Movable instrument rules advertised `MovingVertexCount = 8` even though moving handle visuals are generated through one shared proxy mesh, not per-rule moving source vertices. This created a false ABI route for future systems.
Solution: Keep `InstrumentMovableFlag` and `Interactivity` as the authority for handle creation, but force `MovingVertexStart` and `MovingVertexCount` to zero for prefab and fallback rules. Add tests that assert fallback movable rules do not expose absent moving slices.
Rejected Alternatives: Appending duplicate moving vertices per instrument was rejected because it reintroduces per-rule geometry bloat for handles already covered by a shared proxy. Leaving the stale fields was rejected because future agents could read invalid slices.
Scalability potential: Low through Ultra share the same clean contract. Higher quality can scale handle count and proxy detail without changing rule DTO ownership.
Hardware Impact: Prevents accidental extra vertex reads and future corrupt slices. Runtime microseconds unchanged in current Editor-only route; correctness risk reduced.

## D22 - Socket Type Route Tightening

Problem: Fallback controls and many authored control names resolved to `TypeAny`, allowing ordinary panels, switches, and gauges to occupy ceiling/floor cable sockets. `Floor_Cable` names also risked ceiling misclassification if cable was checked first.
Solution: Resolve known controls to `Socket_Wall_Panel`, floor/conduit names to `Socket_Floor_Conduit` before cable/ceiling names, and add fallback floor conduit endpoints so cable bundles can use floor anchors without runtime physics.
Rejected Alternatives: Allowing all unknown instruments everywhere was rejected for known control families because it pollutes the cable layer. Hard-coding prefab paths was rejected because the instrument library must stay authoring-friendly.
Scalability potential: Low gets readable wall controls and cheap cable silhouettes. Middle/high/ultra can add richer cable/conduit authored prefabs while preserving the same socket route.
Hardware Impact: Avoids wrong placements and wasted renderer work on ceiling clutter. Expected runtime CPU remains one static base renderer plus one optional cable renderer; exact proof PENDING Unity import.

## D23 - Editor Texture Object Lifetime

Problem: Atlas/normal/grime PNG writes destroyed temporary `Texture2D` objects only after successful `EncodeToPNG`/file write. A failed batch could leak editor-side texture objects during mass cabin generation.
Solution: Wrap atlas and baked texture writes in `try/finally` and call `DestroyImmediate` in the finalizer path before `AssetDatabase.ImportAsset`.
Rejected Alternatives: Ignoring this as cold Editor-only was rejected because batch generation can run across many interiors and failure paths matter. Manual GC or delayed cleanup was rejected because Unity objects require explicit destruction.
Scalability potential: Low through Ultra output is unchanged. Higher texture sizes and denser batches become safer because failure cleanup is deterministic.
Hardware Impact: Runtime microseconds unchanged. Editor memory risk reduced during failed PNG/import passes; exact MB depends on selected texture size.

## D24 - Ceiling To Floor Cable Route Pairing

Problem: Cable sockets were collected into one array and paired by order. In fallback rooms, ceiling sockets are appended before floor conduit sockets, so early cable pairs connected ceiling-to-ceiling instead of hanging to conduit endpoints.
Solution: Split cable sockets into ceiling and floor arrays. When both sides exist, pair ceiling-to-floor first with deterministic modulo reuse; loose sequential pairing remains only for one-sided authored data.
Rejected Alternatives: Runtime cable physics and joints were rejected again. Sorting by transform name was rejected because socket kind already carries the route authority and name sorting does not guarantee physical pairing.
Scalability potential: Low gets one or few readable hanging routes. Middle/high/ultra can reuse conduit endpoints to increase cable density while preserving one combined static mesh.
Hardware Impact: Runtime renderer count unchanged. Visual correctness improves with no extra runtime CPU; exact triangle count still scales by `GlobalQualityWeight`.

## D25 - Per Output Atlas Path

Problem: The instrument atlas path was fixed as `TX_InteriorInstrumentAtlas_1608.png`. Multiple generated interiors in the same output folder could overwrite each other's atlas and corrupt material references.
Solution: Add sanitized `OutputName` into the atlas filename and pass `settings.OutputName` into `PackInstrumentAtlas`.
Rejected Alternatives: Keeping a shared atlas was rejected because libraries, seeds, density, and output names may diverge. Per-instrument textures were rejected because the one-atlas draw-call route remains correct.
Scalability potential: Low through Ultra can batch-generate many cabin variants without material texture collisions.
Hardware Impact: Runtime microseconds unchanged. Prevents asset overwrite churn and wrong texture residency during multi-cabin bakes.

## D26 - Matte Instrument Material Scalars

Problem: Generated material relied on shader defaults for bump scale, occlusion strength, metallic, and smoothness. Defaults can make small controls too glossy or make occlusion response inconsistent across URP/Standard fallback.
Solution: Set `_BumpScale`, `_OcclusionStrength`, `_Metallic`, and `_Smoothness` when the shader exposes them.
Rejected Alternatives: Custom shader work was rejected as overengineering for this pass. Per-instrument materials were rejected because they would break the one-material interior pack route.
Scalability potential: Low gets stable readable panels under cheap lighting. Middle/high/ultra can spend quality on denser geometry/maps without material drift.
Hardware Impact: Runtime cost unchanged. Material scalar setup is cold Editor work; visual stability improves without shader variants.

## D27 - Atlas Space Normal Wear

Problem: Fused static bases sample the instrument atlas through UV0, while normal/grime stamping was primarily room-space micro-socket projection. That means the assigned normal/occlusion maps could miss the actual atlas cells used by instrument meshes.
Solution: Extend `NormalMapStampingJob1608` with read-only placements and stamp deterministic wear inside each placement `AtlasScaleOffset` rectangle before optional micro-socket stamps.
Rejected Alternatives: A custom shader with triplanar room projection was rejected as overengineering. Runtime decals were rejected. Rewriting fused meshes to use a second UV set was rejected because URP Lit samples normal/occlusion from the main UV route without custom shader work.
Scalability potential: Low gets visible panel wear even with sparse micro sockets. Middle/high/ultra increase stamp count continuously through `GlobalQualityWeight` while preserving the same atlas material route.
Hardware Impact: Runtime cost unchanged. Cold bake cost scales by `placements * 2..8` bounded stamps, not texture pixels times placements.

## D28 - Placement Hash Zero Wear Fallback

Problem: Atlas-space wear originally skipped placements with `PlacementHash == 0`. The hash is deterministic but still a 32-bit value, so zero is rare rather than impossible.
Solution: Do not skip zero placement hashes. Build a fallback salt from instrument hash, socket hash, and placement index when `PlacementHash` is zero.
Rejected Alternatives: Ignoring the edge case was rejected because it creates silent visual loss. Storing a second salt field was rejected because the placement DTO already carries enough deterministic identity.
Scalability potential: Low through Ultra keep deterministic wear even on rare hash collisions without changing DTO layout.
Hardware Impact: Runtime cost unchanged. Cold bake cost unchanged except one branch per placement.

## D29 - Cable Material Isolation

Problem: `GEN_CableBundles_1608` used the same atlas material as fused instrument bases. Cable UVs are procedural, so they can sample arbitrary gauge/button cells and produce colored, noisy cables.
Solution: Add `MAT_InteriorCable_1608` as a cold Editor-created matte cable material and assign it only to the combined cable child. Keep the cable mesh one static renderer.
Rejected Alternatives: Forcing cable UVs into a black atlas corner was rejected because future atlas cells can change and alpha is not a stable Lit material mask. Per-cable materials were rejected because that breaks the one-renderer cable route. Runtime shader branching was rejected because cable color is static presentation.
Scalability potential: Low gets dark readable cable silhouettes. Middle increases pair/strand count. High/Ultra use the same material while spending quality on denser catenary bundles and richer instrument atlas detail.
Hardware Impact: Runtime CPU remains unchanged. Potential SetPass can increase by one only when cable bundles exist; accepted because visual correctness is otherwise broken and the cable route is one combined renderer with shadows off.

## D30 - Normal Grime Counter Truth

Problem: Texture write counters were marked only when micro-sockets existed, but atlas-space placement wear now writes normal/grime pixels even without micro-socket markers.
Solution: Mark normal/grime maps as written when either micro-sockets or placements exist.
Rejected Alternatives: Ignoring the counter drift was rejected because the in-memory proof surface would under-report valid atlas wear. Pixel-level write counting was rejected because all normal/grime textures are full assets written to disk after the job.
Scalability potential: No visual change. Low through Ultra get accurate bake counters for generated texture assets.
Hardware Impact: Runtime cost unchanged. Cold Editor cost is one boolean expression.

## D31 - Authored Socket Density

Problem: `InteriorSocketDTO1608.DensityHint` existed in the DTO and parser output but was ignored by placement. Authored dense/sparse decorative zones therefore collapsed into one global density probability.
Solution: Multiply the global density threshold by each socket's 0..255 `DensityHint` before the deterministic decimation roll. A zero hint rejects the socket; 255 preserves the global density route.
Rejected Alternatives: Binary low/high quality socket classes were rejected because density must scale continuously. Adding another rule weight field was rejected because socket density is already present in the DTO.
Scalability potential: Low uses sparse hints to keep silhouettes readable and cheap. Middle keeps authored panels populated. High/Ultra can set high hints in hero zones and buy denser controls without changing runtime authority.
Hardware Impact: Runtime cost unchanged. Cold Editor cost is one multiply/divide per socket; generated prefabs avoid needless low-priority clutter on compact lanes.

## D32 - Marker Name Density Parsing

Problem: After density became active, authored marker transforms still defaulted to 255 unless external tooling filled `DensityHint`, making the feature hard to use in existing prefab authoring.
Solution: Parse density intent directly from socket marker names: `NoAuto`/`Empty`/`Disabled` = 0, `Sparse` = 96, `MediumDensity`/`MidDensity` = 180, `Dense`/`Hero`/`HighDensity` = 255, floor/cable markers get conservative defaults.
Rejected Alternatives: Custom MonoBehaviour marker components were rejected because they add hierarchy/component overhead and require prefab migration. Regex parsing was rejected because simple name tokens are enough for cold editor use.
Scalability potential: Low authors can mark cheap zones sparse without new tooling. Middle keeps default panels. High/Ultra can mark hero instrument walls dense while still using one deterministic pipeline.
Hardware Impact: Runtime cost unchanged. Cold editor cost is bounded string token checks during prefab scanning.

## D33 - Full Texture Managed Array Removal

Problem: `WriteTexture` and atlas clear allocated full-size managed `Color32[]` buffers. At 4096 square maps, each full-texture managed array is roughly 64 MB and can spike editor GC pressure during mass cabin bakes.
Solution: Keep the 8-byte aligned `InteriorRgba32DTO1608` for jobs, but pack output into temporary `NativeArray<Color32>` and upload with `Texture2D.SetPixelData`. Atlas clear also uses `NativeArray<Color32>`. The small reusable atlas cell block remains managed because `SetPixels32(x,y,w,h,block)` is a bounded per-cell route.
Rejected Alternatives: Shrinking `InteriorRgba32DTO1608` to 4 bytes was rejected because it violates the current ARM64-aligned DTO contract. Using unsafe raw pointer uploads was rejected as unnecessary for Editor-only asset bake.
Scalability potential: Low keeps smaller textures and avoids managed spikes. Middle/High/Ultra can bake 2K/4K maps with less managed heap pressure.
Hardware Impact: Runtime cost unchanged. Editor managed allocation reduction is approximately one full RGBA texture buffer per normal/grime write and one atlas clear buffer; exact MB depends on selected texture size.

## D34 - Multiplatform Texture Import Overrides

Problem: Texture import configuration was explicit for the active importer but did not force mobile platform formats. Android/iPhone could fall back to project defaults, risking oversized BC-style or uncompressed interior atlases on handheld/mobile lanes.
Solution: Add `ApplyTexturePlatform` and set Standalone to BC5/BC7, Android to ASTC 6x6, and iPhone to ASTC 6x6 with a 2048 mobile max texture cap. Atlas color remains sRGB; normal/grime data roles keep their existing linear/normal semantics.
Rejected Alternatives: Trusting platform defaults was rejected because defaults are project-state dependent. Separate mobile atlas generation was rejected as overengineering for this Editor-only pass; one imported asset with platform overrides is the correct Unity route.
Scalability potential: Low/handheld lanes receive bounded ASTC texture memory. Middle keeps the same asset contract. High/Ultra retain Standalone BC5/BC7 and can use larger atlas source detail through `GlobalQualityWeight` without changing runtime truth.
Hardware Impact: Runtime CPU unchanged. Mobile/handheld VRAM risk is reduced by ASTC 6x6 plus 2048 cap; exact memory delta remains PENDING Unity import statistics.

## D35 - Cable Route Density Gate

Problem: `DensityHint` controlled instrument placement but not cable bundle route sockets. Authored `NoAuto` or sparse ceiling/floor cable markers could still spawn static cables, creating uncontrolled clutter on compact lanes.
Solution: Add `InteriorFinisherMath1608.ResolveDensityThreshold` and `PassesDensityGate`, route instrument placement through that shared gate, and make `CreateCableBundleMeshAsset` count/fill only cable sockets that pass `CableSocketPassesDensity`.
Rejected Alternatives: A separate cable-only density heuristic was rejected because it would drift from instrument socket semantics. Removing cable bundles at low density was rejected because binary quality switches are forbidden.
Scalability potential: Low keeps only deterministic high-value cable routes. Middle retains authored default ceiling/floor routes. High/Ultra can use dense/hero marker hints and `GlobalQualityWeight` to increase pair count, strand count, and segments without runtime authority changes.
Hardware Impact: Runtime CPU unchanged. Cold bake skips disabled/sparse cable meshes before `GenerateCableBundles` and `CombineMeshes`; exact editor-time delta remains PENDING Unity profiler.

## D36 - Explicit Material Map Binding

Problem: Instrument material setup relied on `material.mainTexture`. URP Lit and Standard usually resolve it, but generated or migrated material assets can retain stale `_BaseMap`/`_MainTex` bindings or cable texture slots.
Solution: Bind atlas explicitly to `_BaseMap` and `_MainTex` when present, force white base color for instrument material, and clear `_BaseMap`/`_MainTex` on the cable material before assigning the dark cable color.
Rejected Alternatives: A custom shader was rejected because URP Lit/Standard already provide the needed properties. Per-instrument materials were rejected because they break the atlas batching route.
Scalability potential: Low keeps readable un-tinted instruments and matte dark cables. Middle/High/Ultra can raise atlas resolution and detail density without material-state drift or extra shader variants.
Hardware Impact: Runtime cost unchanged. Cold material setup only; visual correctness improves without adding draw calls.

## D37 - Authored Instrument Texture Sampling

Problem: `TexturePaths` were collected from prefabs but atlas packing ignored them, producing procedural fallback swatches even when authored instrument textures existed.
Solution: Reuse one editor-only `Texture2D` scratch, blit each authored source texture into a temporary render target at atlas-cell resolution, read it into the scratch, and copy via `GetPixelData<Color32>` into the reusable block buffer. If no visible authored texture exists, keep the deterministic gauge glyph fallback.
Rejected Alternatives: Requiring source textures to be readable was rejected because imported production textures are often not readable. `GetPixels32()` was rejected because it allocates a managed full cell copy per source texture.
Scalability potential: Low still gets procedural readable gauges when sources are missing. Middle/High/Ultra can use authored art in the same atlas route without adding materials or runtime texture binds.
Hardware Impact: Runtime CPU unchanged. Cold Editor atlas generation pays one blit/readback per authored texture; runtime gains come from preserving the one-atlas material route while using real instrument art.

## D38 - Material Texture Property Discovery

Problem: Instrument prefab scanning only used `material.mainTexture`. URP/migrated materials can store the authored albedo in `_BaseMap`, `_MainTex`, `_BaseColorMap`, or `_AlbedoMap`, which made the atlas sampler miss valid art.
Solution: Add an explicit primary texture property list and resolve those slots before falling back to `mainTexture`.
Rejected Alternatives: Forcing artists to duplicate texture references into `mainTexture` was rejected because it is brittle and tool-hostile. Scanning every material property was rejected because the targeted albedo slots cover the expected project shader families without pulling masks/normals accidentally.
Scalability potential: Low keeps fallback glyphs when no art exists. Middle/High/Ultra get authored panel art folded into the same one-material atlas route.
Hardware Impact: Runtime CPU unchanged. Cold scan cost is four property checks per material; runtime texture/material route remains single-atlas.

## D39 - Authored Static Mesh Extraction

Problem: Authored instrument prefabs were reduced to one bounds box during library baking. This preserved socket occupancy but destroyed real bevels, face plates, screw wells, label panels, and other authored detail before atlas remap.
Solution: Add cold Editor `MeshFilter.sharedMesh` extraction into `InteriorInstrumentLibraryBuilder1608`: cached scratch lists read vertices, normals, tangents, UV0, and triangles with `Mesh.Get*` APIs, transform child meshes into prefab-root local space, preserve negative-scale winding, reject non-finite meshes, and fall back to the bounds box only when no valid mesh triangles exist.
Rejected Alternatives: Runtime prefab instances were rejected because they reintroduce hierarchy, material variation, and transform traversal. `Mesh.vertices`/`Mesh.triangles` getters were rejected because they allocate copies. Raw prefab YAML parsing was rejected because FileID/layout coupling is brittle.
Scalability potential: Low keeps authored silhouettes fused into one static base with existing atlas/normal/grime maps. Middle preserves more real prefab detail without runtime cost. High and Ultra can carry denser authored instrument meshes while still paying one fused static renderer and one atlas route.
Hardware Impact: Runtime i3/MX350 path saves the same transform/renderer overhead as the previous box path while restoring visible geometry. Cold bake cost increases by linear mesh extraction only; runtime microseconds remain unchanged until Unity import/profiler proof.

## D40 - Movable Child Mesh Exclusion

Problem: Real mesh extraction could also fuse authored moving parts inside the prefab, such as handles, levers, knobs, actuators, needles, or `MOV_*` children. That would turn interactive presentation anchors into static wall geometry.
Solution: Add `ShouldSkipMovableMesh` before static source extraction. The filter walks the mesh transform ancestry and rejects explicit moving-token children while leaving static face plates, bezels, sockets, and casing meshes eligible for fusion.
Rejected Alternatives: Fusing all authored meshes was rejected because it violates the static-base-only domain. Skipping entire movable prefabs was rejected because a switch or valve still needs its static back plate fused. Requiring a new marker component was rejected because existing authored hierarchies can express the intent by name.
Scalability potential: Low keeps only static silhouettes and one generic handle proxy. Middle/High/Ultra can add denser authored static casing while the movable proxy route stays bounded and independent.
Hardware Impact: Runtime CPU unchanged. Prevents future double-geometry and invalid interaction visuals; exact saved cost depends on authored movable child counts and remains pending Unity import.

## D41 - Atlas Alpha Squeeze

Problem: Authored texture sampling copied the whole source texture into an atlas cell, including fully transparent margins. That wasted atlas area and contradicted the alpha-empty squeeze requirement.
Solution: Resolve visible alpha bounds from the sampled scratch texture, clear the target cell, scale the visible region into the cell with padding, and report visible source area instead of blindly counting the full cell.
Rejected Alternatives: Rotating cropped texture content was rejected for this pass because the current rule DTO has no rotation field for UV remap, so texture-only rotation would misalign authored UVs. Per-texture variable cell packing was rejected as a larger atlas contract change; alpha crop is the safe incremental squeeze.
Scalability potential: Low gets less wasted visible space in 512/1024 atlases. Middle/High/Ultra can carry more authored art density before raising texture size.
Hardware Impact: Runtime CPU unchanged. VRAM dimensions remain governed by atlas size, but visible atlas occupancy metric is now truthful and transparent padding no longer eats cell detail.

## D42 - Micro Mesh Amortization Guard

Problem: Real mesh extraction could preserve authored screw, rivet, bolt, label, seam, engraving, decal, or sub-5 cm child meshes as physical geometry. That violates the micro-detail amortization rule.
Solution: Add `ShouldSkipMicroDetailMesh` below the prefab root. It skips explicit micro-detail names and child mesh bounds below 5 cm before static base extraction; those details remain represented by normal/grime stamping.
Rejected Alternatives: Keeping tiny authored geometry was rejected because it scales into polygon noise. Skipping the root mesh was rejected because small root instruments can still be legitimate controls. A custom marker component was rejected because name and bound heuristics cover existing art without migration.
Scalability potential: Low avoids needless tiny triangles. Middle/High/Ultra can spend saved triangle budget on larger static casing and richer normal/grime maps.
Hardware Impact: Runtime vertex/index count decreases on authored prefabs with tiny detail children. Exact triangle and microsecond savings are pending Unity import.

## D43 - Moving Handle Material Isolation

Problem: Moving handle proxy meshes used the instrument atlas material. Their generic UVs could sample arbitrary dial or button cells, producing visually corrupted handles.
Solution: Add `MAT_InteriorHandle_1608`, a matte non-atlas material for moving handle proxies, and assign it through `CreatePrefabAsset`.
Rejected Alternatives: Reusing the instrument material was rejected because it creates random atlas sampling. Per-handle unique materials were rejected because they fragment render state. Reusing the cable material was rejected because handles need a separate brass/dark-metal identity.
Scalability potential: Low keeps readable moving controls without atlas noise. Middle/High/Ultra can increase handle count while sharing one handle material.
Hardware Impact: Runtime may add one material lane when handles exist, but avoids corrupted atlas sampling; renderer count remains limited to actual movable controls.

## D44 - Instrument Rule Selection Fail-Close

Problem: `SelectInstrumentRule` accumulated only compatible weighted rules, but its final impossible-path fallback returned rule zero when `maxRules > 0`. If a future overflow or edge case reached that path, an incompatible first rule could be placed silently.
Solution: Return `-1` from the final fallback so placement skips the socket unless the weighted pass selected a compatible rule. Add a source-audit assertion that rejects the stale fallback string.
Rejected Alternatives: Keeping first-rule fallback was rejected because it is fail-open and hides data contract violations. Throwing from the Burst job was rejected because job code must signal through counters/faults or deterministic absence, not managed exceptions.
Scalability potential: Low through Ultra preserve the same density and quality behavior on valid data. Bad library/socket data now drops the invalid placement instead of corrupting visual semantics.
Hardware Impact: Runtime cost unchanged. Cold placement job cost unchanged; one unreachable fallback branch now fails closed.

## D45 - Authored Weight Saturation

Problem: `ResolveWeightUnits` trusted authored float weights and converted through `int`. Non-finite, negative, or extreme weights could distort deterministic selection or hit float-to-int overflow behavior before the final `uint` conversion.
Solution: Add `MaxRuleWeightUnits`, reject non-finite and sub-minimum values to `0.01f`, clamp weighted units to 65535, and convert from rounded float directly to `uint`. With 64 max rules, accumulator headroom remains bounded.
Rejected Alternatives: Normalizing all weights every bake was rejected because the selector only needs bounded integer units. Throwing on bad authored weight was rejected because a bad art value should degrade deterministically instead of killing the whole cabin.
Scalability potential: Low through Ultra keep authored relative weights within a bounded lane. Extreme hero weights no longer erase all other instruments or risk accumulator drift.
Hardware Impact: Runtime cost unchanged. Cold selection adds only finite/clamp operations; bad authored data now fails into deterministic bounded behavior.

## D46 - Zero-Length Native Buffer Preservation

Problem: `ToNative<T>` forced empty lists to length one. Empty `microSocketList` could therefore become one uninitialized native element, letting the normal/grime stamping pass read a phantom micro socket.
Solution: Allocate `NativeArray<T>` with `values.Count` exactly in both conversion helpers. Existing socket placement still fails closed before conversion when there are zero decorative sockets.
Rejected Alternatives: Keeping one sentinel element was rejected because no sentinel contract existed in the DTO. Filling a dummy element was rejected because a dummy micro socket is still a fake authored detail.
Scalability potential: Low through Ultra keep valid authored sockets unchanged. Empty micro-detail lanes now do no stamp work instead of inventing detail.
Hardware Impact: Runtime cost unchanged. Cold bake avoids one bogus element and possible accidental stamp work when no micro sockets exist.

## D47 - Normal Grime Counter Truth

Problem: Normal/grime map counters were marked as written when the placement buffer existed and had capacity, even if density gating produced zero actual placements. That made the in-memory proof surface overstate stamp work.
Solution: Use `counterValue.PlacementCount > 0u` plus real micro-socket length to decide whether stamp counters should be marked.
Rejected Alternatives: Scanning the whole placements buffer for flags was rejected because the placement job already owns and publishes `PlacementCount`. Counting neutral texture writes was rejected because the counter name represents stamped normal/grime pixels, not file existence.
Scalability potential: Low through Ultra get truthful counters under sparse density. Dense lanes still report stamped maps when actual placements exist.
Hardware Impact: Runtime cost unchanged. Cold bake cost unchanged; proof counters stop drifting on sparse or empty selection.

## D48 - Moving Handle Scale Propagation

Problem: Static instrument bases were fused with the placement matrix scale, but moving handle proxy GameObjects only copied position and rotation. Large and small instruments therefore received identical handle size.
Solution: Add `ExtractUniformScale` from placement matrix column lengths and apply it to the moving handle child local scale.
Rejected Alternatives: Baking unique moving handle meshes per placement was rejected because it adds asset churn and defeats the shared proxy route. Leaving uniform proxy size was rejected because it visibly disconnects handles from scaled bases.
Scalability potential: Low keeps one shared handle mesh while preserving authored scale. Middle/High/Ultra can use larger visual variation without unique handle mesh assets.
Hardware Impact: Runtime CPU unchanged. One transform scale assignment exists only in the generated prefab authoring path; renderer and material counts stay unchanged.

## D49 - Output-Scoped Instrument Material

Problem: Instrument atlases were output-name scoped, but the main instrument material used a fixed `MAT_InteriorFinisher_1608.mat` path. A later cabin bake in the same folder could rebind that shared material to a different atlas, corrupting older prefabs.
Solution: Pass `settings.OutputName` into `CreateOrUpdateMaterial` and create `MAT_{SanitizedOutputName}_InteriorFinisher_1608.mat`.
Rejected Alternatives: Sharing one material per folder was rejected because it is only safe when all prefabs share identical atlas/normal/grime assets. Duplicating materials per instrument was rejected because it breaks the atlas batching goal.
Scalability potential: Low through Ultra can bake multiple cabins into one folder without texture drift. Runtime still uses one main instrument material per generated cabin.
Hardware Impact: Runtime CPU unchanged for each prefab. Disk stores one scoped material per cabin output; this prevents stale atlas bindings without adding GameObjects.

## D50 - Atlas Grid Squeeze

Problem: `ResolveAtlasGrid` always used the target atlas size and computed cell size from average area. Small libraries could still write oversized 4K atlases, wasting VRAM and import time.
Solution: Treat `TextureSize` as an upper bound. Keep 512px cells where possible, compute required grid side from texture count, choose the smallest power-of-two atlas that fits, and halve cell size only when the requested upper bound is too small.
Rejected Alternatives: Always using the requested target size was rejected because it violates alpha/atlas squeeze intent. Fully variable bin packing was rejected for this pass because fixed-grid UV remap is already integrated and deterministic.
Scalability potential: Low outputs smaller atlases for small instrument sets. Middle/High/Ultra keep larger atlases only when count/quality needs them.
Hardware Impact: Runtime CPU unchanged. VRAM and texture import cost drop for sparse cabins; exact MB depends on instrument count and selected target.

## D51 - Atlas Wear De-Duplication

Problem: Atlas-space normal/grime wear iterated every placement. Since repeated instances of the same rule share one atlas cell, duplicated placements compounded wear on the same texture and made all copies look over-darkened.
Solution: Add `HasEarlierAtlasPlacement` and stamp atlas-space wear only for the first placement of each rule index. Micro-socket stamps still remain per socket.
Rejected Alternatives: Per-instance unique atlas cells were rejected because they explode atlas area and destroy batching. Runtime per-instance dirt was rejected because 1608 is offline presentation only.
Scalability potential: Low through Ultra keep shared atlas cells stable under dense repeated controls. Dense cockpits no longer punish repeated instrument types with global over-wear.
Hardware Impact: Runtime CPU unchanged. Cold normal/grime bake work decreases when many placements share a rule.

## D52 - Fallback Socket Hash Salting

Problem: Fallback sockets used only root name and ordinal for stable hash. Wall and micro fallback lists both started at low ordinals, so different socket classes could collide under the same root.
Solution: Add tag hash and socket kind bits to fallback stable hash construction.
Rejected Alternatives: Global counters across all fallback sockets were rejected because they couple list generation order to identity. Random salts were rejected because placement must remain deterministic.
Scalability potential: Low through Ultra keep stable fallback placement identity across wall, cable, floor, and micro lanes.
Hardware Impact: Runtime cost unchanged. Cold fallback generation adds two XOR inputs to the hash.

## D53 - Fallback Density Parity

Problem: Authored floor/ceiling socket markers had conservative density defaults, but generated fallback sockets hardcoded `DensityHint = 255` for every kind. Fallback cable routes could therefore overpopulate compared with equivalent authored routes.
Solution: Route fallback sockets through `ResolveDensityHint(string.Empty, kind)` so floor, ceiling, wall, and micro sockets use the same semantic density policy.
Rejected Alternatives: Keeping fallback max density was rejected because it makes missing marker authoring more expensive than authored marker data. Separate fallback constants were rejected because they would drift.
Scalability potential: Low produces fewer fallback cables on sparse cabins. Middle/High/Ultra still scale through continuous density and quality weights.
Hardware Impact: Runtime CPU unchanged. Cold cable mesh generation may skip more low-priority fallback routes on constrained density.

## D54 - Authored Socket Radius Scaling

Problem: Authored decorative socket markers had fixed radii regardless of marker scale. A scaled wall slot could still reject large instruments or accept small controls as if it were the default 18 cm surface.
Solution: Route authored marker radius through `ResolveSocketRadius`, derive the largest root-relative axis scale from `rootInverse * tr.localToWorldMatrix`, reject non-finite scale, and clamp micro/detail sockets separately from wall/control sockets.
Rejected Alternatives: Reading renderer bounds was rejected because the socket marker itself is the ownership signal and renderer traversal would couple placement to visual children. Leaving fixed authored radius was rejected because artists need continuous control without adding marker components.
Scalability potential: Low can keep small sparse sockets that admit only cheap controls. Middle/High/Ultra can author larger hero panels and gauges without changing runtime authority or material lanes.
Hardware Impact: Runtime CPU unchanged. Cold parser adds a few scalar operations per marker; placement quality improves without new GameObjects or runtime lookup.

## D55 - Box Geometry Index Offsets

Problem: `AppendBox` appended vertices at the current list tail but emitted triangle indices as 0..7. That was only valid when the box started at the rule slice origin; any prior vertices inside the same rule slice would corrupt triangle references.
Solution: Pass `ruleVertexStart` into `AppendBox`, compute `localBaseIndex = vertices.Count - ruleVertexStart`, and add that relative offset to every quad index. This preserves the source contract used by `WeldInstrumentBasesJob1608`, where triangle indices are local to the rule's `StaticVertexStart`.
Rejected Alternatives: Rewriting fallback boxes through a separate mesh builder was rejected because the local fix is exact and keeps the existing source DTO path. Deferring to the weld job was rejected because invalid source indices should never enter the library contract.
Scalability potential: Low through Ultra now preserve correct fallback geometry slices. Higher-detail authored prefabs still use extracted mesh slices; box fallback remains safe for missing or invalid geometry.
Hardware Impact: Runtime CPU unchanged. Cold generation cost is unchanged; corrupt fallback mesh risk is removed.

## D56 - Fallback Vertex Identity Match

Problem: Fallback box vertices were stamped with `typeHash`, while the owning rule used `InstrumentHash = HashString(name)`. That made render vertex identity differ from the rule identity for fallback instruments.
Solution: Compute `instrumentHash = HashString(name)` once in fallback rule creation, pass it into `AppendBox`, and assign the same value to `rule.InstrumentHash`.
Rejected Alternatives: Using socket type hash for vertex identity was rejected because type hash is routing metadata, not unique instrument ownership. Adding a second vertex identity field was rejected because the existing stream already carries the intended instrument hash.
Scalability potential: Low through Ultra keep consistent per-instrument debug/selection identity in fused render streams. Authored prefabs already follow this route; fallback schema now matches it.
Hardware Impact: Runtime CPU unchanged. Cold generation cost unchanged; identity drift risk removed.

## D57 - Socket Fit Scale

Problem: Authored socket radius controlled fit eligibility but not actual placement scale. A large marker could admit a large instrument, yet the final fused mesh still used only a fixed 0.92-1.08 random jitter.
Solution: Compute `fitRadius` from rule bounds and `MinSocketRadius`, derive `socketFitScale = socket.Radius / fitRadius`, clamp it to 0.55-2.25, and multiply the existing deterministic scale jitter. Reject non-finite `rule.BoundsExtents` in `RuleFitsSocket`.
Rejected Alternatives: Per-placement unique mesh generation was rejected because the existing matrix transform already scales fused vertices. Binary small/large slot classes were rejected because socket scale is continuous authoring data.
Scalability potential: Low can shrink cheap instruments into dense compact panels. Middle preserves default authored proportions. High/Ultra can enlarge hero gauges and valves without new renderers or material lanes.
Hardware Impact: Runtime CPU unchanged; this is offline placement math inside the Editor job. Fused vertex count and draw-call count remain stable.

## D58 - Static Bounds Ownership

Problem: Authored prefab rule bounds were resolved from all Renderer bounds. Moving handles, needles, labels, rivets, and other micro-detail children could inflate `BoundsExtents` and `MinSocketRadius` even though those meshes are deliberately excluded from static-base fusion.
Solution: Add `TryResolveStaticLocalBounds` and make `AppendPrefabRule` prefer bounds from the exact static mesh slice that passes `ShouldSkipMovableMesh` and `ShouldSkipMicroDetailMesh`. Renderer bounds remain a fallback only when no valid static mesh exists. Mesh scratch lists are explicitly cleared before each `Mesh.Get*` read.
Rejected Alternatives: Keeping renderer bounds was rejected because it violates one fact -> one owner -> one route: static-base geometry owns static-base fit. Duplicating the skip rules into a separate renderer pass was rejected because it would drift from the fusion path. Baking movable handles into bounds was rejected because handles are separate presentation anchors.
Scalability potential: Low gets tighter compact-panel placement without phantom handle size. Middle keeps authored static casings correctly fitted. High/Ultra can use larger hero gauges and dense controls while movable/micro children stop corrupting socket fit math.
Hardware Impact: Runtime CPU unchanged. Cold Editor scan cost is one MeshFilter pass already comparable to static extraction; expected runtime gain is indirect through fewer overlarge fallback bases and less visual collision in dense panels. Exact microseconds remain PENDING UNITY IMPORT/PROFILER.

## D59 - Negative Bounds Fail-Closed

Problem: `RuleFitsSocket` rejected non-finite `BoundsExtents` but did not explicitly reject negative extents. Valid Unity `Bounds` should not carry them, but a corrupted DTO or future importer bug could make socket fit math accept impossible geometry.
Solution: Add `math.any(rule.BoundsExtents < 0f)` fail-closed before socket mask and radius comparisons. The existing finite check remains first.
Rejected Alternatives: Trusting Unity `Bounds` was rejected because placement rules are DTOs and may eventually arrive from generated/imported sources. Clamping negative extents to positive was rejected because that hides corrupt source data and could place invalid instruments.
Scalability potential: Low through Ultra keep identical valid placement behavior. Bad data is skipped deterministically instead of producing unstable scale/fit decisions.
Hardware Impact: Runtime CPU unchanged because 1608 is Editor-only placement. Cold job adds one bool3 check per candidate rule; exact microseconds remain PENDING UNITY IMPORT/PROFILER.

## D60 - Atlas Aspect Preservation

Problem: Alpha-cropped authored instrument textures were scaled into a square write region. Wide labels and tall gauges could be visibly stretched even though their transparent margins were removed.
Solution: Preserve crop aspect ratio inside the fixed atlas cell by computing a single scale from available width/height, deriving centered `writeWidth` and `writeHeight`, and sampling only that rectangle. The UV contract remains fixed-grid; only the authored pixels inside the cell change.
Rejected Alternatives: Rotating texture cells was rejected again because the current rule DTO and fused UV remap have no rotation flag, so texture-only rotation would corrupt authored UVs. Variable-size bin packing was rejected as a larger contract change. Leaving square stretch was rejected because it deforms instrument semantics.
Scalability potential: Low keeps readable labels in smaller atlases without larger textures. Middle/High/Ultra can use denser authored texture libraries while preserving dial and placard proportions inside the same single-material atlas route.
Hardware Impact: Runtime CPU unchanged. Cold atlas packing adds a few scalar operations per cell and can improve visual quality without increasing texture size, renderer count, or material count. Exact microseconds remain PENDING UNITY IMPORT/PROFILER.

## D61 - Authored Atlas Padding Color

Problem: Aspect-preserved authored texture blocks can leave letterbox or pillarbox padding inside a fixed atlas cell. Because the generated instrument material is opaque by default, transparent-black padding can render as black bars instead of unused alpha.
Solution: Replace transparent block clearing with `ResolveAuthoredPaddingColor` and `FillBlock`. Dense opaque crops use their non-transparent edge average as padding color; sparse alpha crops fall back to a dark NASA-punk panel color.
Rejected Alternatives: Enabling alpha clipping on the shared instrument material was rejected because it would affect every static fused base and can create sorting/edge artifacts. Reverting to square stretching was rejected because it distorts labels and gauges. Per-texture material modes were rejected because they fragment batching.
Scalability potential: Low avoids black bar artifacts on small atlases. Middle/High/Ultra can use authored transparent texture sources while keeping one material and fixed-grid UVs.
Hardware Impact: Runtime CPU unchanged. Cold atlas packing adds one crop-local scan per authored texture; runtime renderer/material count remains unchanged. Exact editor microseconds remain PENDING UNITY IMPORT/PROFILER.

## D62 - Inner Alpha Preservation

Problem: The padding fill fixed outer letterbox bars, but `CopyCroppedAlphaBlock` still copied fully transparent source pixels inside the visible crop rectangle. Transparent holes in labels or decals could overwrite the panel backing with transparent black.
Solution: Load the source pixel into a local `Color32`, skip pixels with alpha <= 3, and only overwrite the block with visible pixels. The prefilled padding color remains underneath alpha-empty interior pixels.
Rejected Alternatives: Pre-multiplying alpha into opaque RGB was rejected because it would darken semi-transparent authored art. Forcing alpha to 255 was rejected because it would destroy future alpha-aware material compatibility. Keeping direct copy was rejected because it reintroduced black bars inside the crop.
Scalability potential: Low keeps authored decals readable on opaque material. Middle/High/Ultra can use more transparent-source artwork while keeping the same atlas/material route.
Hardware Impact: Runtime CPU unchanged. Cold atlas copy adds one alpha branch per written pixel; no renderer, material, texture-size, or runtime hierarchy cost changes.

## D63 - Packed Atlas Rect Ownership

Problem: Authored texture blocks can now be alpha-cropped, aspect-preserved, centered, and padded inside a fixed atlas cell, but the old rect assignment still described the full grid cell. Fused static-base UVs could sample padding rather than the actual visible authored pixels.
Solution: Move atlas packing before placement, let `FillTextureBlock` return the real `writeX/writeY/writeWidth/writeHeight`, and publish that packed rectangle through `ApplyPackedAtlasRect` into `InstrumentRuleDTO1608.UvMin/UvMax` before `PopulateSocketsJob1608` and `WeldInstrumentBasesJob1608` consume rule UVs.
Rejected Alternatives: Reverting to full-cell square art was rejected because it discards alpha squeeze and aspect preservation. Variable-size bin packing was rejected because the current fixed-grid atlas contract is deterministic and already integrated into fused UV remap. A post-fusion UV correction pass was rejected because placement and weld already consume rule rects once.
Scalability potential: Low keeps small atlases readable because UVs target visible pixels, not padding. Middle/High/Ultra can use more transparent and irregular authored instrument art without material splits, runtime decals, or extra mesh slices.
Hardware Impact: Runtime CPU unchanged. Cold atlas packing publishes four extra integers per source cell and removes visual sampling drift without increasing texture size, renderer count, material count, or runtime lookup.

## D64 - Stale Full-Cell Atlas API Removal

Problem: `InteriorAtlasPacker1608.ApplyAtlasRects` remained public after packed rect ownership moved into `PackInstrumentAtlas`. That stale API could reintroduce full-cell UVs that ignore alpha crop, aspect preservation, and centered padding.
Solution: Remove the public `ApplyAtlasRects` method and update source-audit tests to require its absence while keeping `ApplyPackedAtlasRect` as the private packer-owned route.
Rejected Alternatives: Keeping the method as a fallback was rejected because there is no correct fallback without the actual write rectangle. Marking it obsolete was rejected because Editor code inside the same file could still call it and silently drift.
Scalability potential: Low through Ultra now share one atlas UV authority. More irregular authored textures can be packed without future full-cell regressions.
Hardware Impact: Runtime CPU unchanged. Cold code surface shrinks; no renderer, material, texture-size, or mesh count changes.

## D65 - Renderer Bounds Fail-Closed Hygiene

Problem: Renderer fallback bounds can become non-finite through corrupt imported transforms, broken renderer data, or future authoring mistakes. Prefab rule scanning already preferred static mesh bounds, but its fallback route and socket marker renderer scans could still allow bad `Bounds` values to affect socket fit, placement radius, and default scale.
Solution: Skip non-finite renderer bounds in both prefab rule fallback scanning and decorative socket parsing. If a prefab rule has no valid static or renderer bounds, use one compact deterministic default bound instead of propagating bad data.
Rejected Alternatives: Clamping NaN/Infinity to zero was rejected because it hides corrupted authoring data. Failing the whole cabin bake was rejected because a single broken optional renderer should not kill all interior decoration when a safe compact bound can preserve generation.
Scalability potential: Low keeps dense panels from exploding scale because of one corrupt renderer. Middle/High/Ultra keep authored static bounds as the normal path; the fallback only prevents drift when content is damaged.
Hardware Impact: Runtime CPU unchanged. Cold editor scan adds finite checks only. The expected gain is stability, not frame time: bad bounds no longer create oversized meshes, atlas waste, or placement rejection cascades on weak devices.
