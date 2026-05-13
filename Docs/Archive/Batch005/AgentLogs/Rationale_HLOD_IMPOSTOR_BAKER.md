# Rationale_HLOD_IMPOSTOR_BAKER

Overall status: PENDING VERIFICATION

## Initial Decision Record

Problem: Horizon wrecks/bases cannot be rendered as full geometry at 2 km on MX350 without burning triangle, SetPass, and overdraw budget.
Solution: Build an editor-baked octahedral impostor path: offline captures into a capped atlas, runtime quad/indirect draw path, shader tile selection, quality-tier dither toggle.
Rejected Alternatives: Runtime camera capture and per-object prefab impostors. Standard Unity LODGroup alone still renders mesh memory and does not solve far-horizon overdraw at the required scale.
Scalability potential: Low snaps nearest octa tile; Middle uses dithered tile transition; High adds normal/depth lighting response; Ultra permits tighter transition thresholds and denser atlas residency if VRAM headroom exists.
Hardware Impact: Estimated low-end MX350/i3 win is fewer far-horizon triangles and lower CPU object churn; expected savings are pending actual Unity profiling. STATUS: PENDING VERIFICATION.

Problem: Multiple agents are editing adjacent systems.
Solution: Keep runtime API decoupled through data assets/interfaces and avoid concrete dependency on streaming classes unless an existing contract is found.
Rejected Alternatives: Direct calls into guessed WorldChunkResidencyManager methods. That creates compile risk and violates parallel-agent isolation.
Scalability potential: Decoupled data lets streaming, BRG, or editor tooling bind later without rewriting the baker.
Hardware Impact: Avoids main-thread sync and runtime allocation from guessed GameObject orchestration. Measured proof absent.

## Loop 1 Decisions

Problem: HLOD impostors need source textures without runtime camera work.
Solution: Added `HECTON-8/Bake HLOD Impostor` editor baker that clones the selected renderer hierarchy, centers it for precision, captures 8 deterministic upper/lower octa views, and writes albedo/alpha plus normal/depth atlases.
Rejected Alternatives: Runtime camera capture, Amplify-only bake paths, and per-frame material slicing. Those add runtime stalls or third-party hard dependency.
Scalability potential: Low uses the same baked atlas with nearest-tile selection; Middle/High/Ultra reuse the same 2048-wide payload with shader-side visual upgrades.
Hardware Impact: Expected low-end gain is moving capture and view selection off CPU runtime; estimated per-object runtime capture avoidance is hundreds to thousands of microseconds, pending profiling.

Problem: Baked impostor metadata must survive streaming and origin-shift without GameObject dependency.
Solution: Added `HectonOctahedralImpostorData` and unmanaged `OctahedralImpostorInstance` records that store bounds, depth range, fade, flags, and universe-space center.
Rejected Alternatives: Storing scene component references in the asset or spawning prefab impostor instances. Those break zero-GC streaming swaps.
Scalability potential: One data contract supports chunk HLOD, landmark HLOD, and RenderMeshIndirect batches.
Hardware Impact: Replacing Transform-driven proxy spawning with contiguous native records targets sub-100 us swap work for hundreds of instances on MX350-class hardware, pending measurement.

Problem: Unity compile verification cannot pass because the project currently has unrelated compiler errors.
Solution: Ran Unity refresh/compile and filtered console for new impostor errors; no `HectonOctahedral*` errors were reported. Global wall remains in unrelated `InputDispatcher`, geology seam, UI cockpit, and existing chunk streaming signal ambiguity.
Rejected Alternatives: Editing unrelated domains to force a green global compile. That would violate domain boundary and parallel-agent isolation.
Scalability potential: Renderer code remains isolated and can be validated once the global wall is cleared by owning agents.
Hardware Impact: No runtime impact; this is verification scope control.

## Loop 2 Decisions

Problem: Far-field impostors must rotate to camera without Transform churn or per-object CPU `LookAt`.
Solution: Implemented camera-facing quad basis in `Hecton_Impostor.hlsl` from `_WorldSpaceCameraPos`, world up, and per-instance bounds.
Rejected Alternatives: MonoBehaviour billboard scripts or CPU-updated matrices. Those would add Transform writes and dispatcher cost for every impostor.
Scalability potential: Low through Ultra all share one shader path; higher tiers spend saved CPU on dithered view transition and normal/depth lighting.
Hardware Impact: Expected CPU savings are proportional to active impostors; 500 impostors avoid roughly 500 Transform updates and culling object touches per frame, estimated 200-1000 us on low-end CPU pending profiler proof.

Problem: Octahedral view selection must not force CPU material/UV changes.
Solution: Shader ranks the eight baked view directions, selects primary/secondary atlas tiles, and uses IGN dither for non-low tiers.
Rejected Alternatives: CPU tile index upload per impostor or alpha-blended dual quads. CPU upload adds hot-path traffic; alpha blending causes overdraw and ghost silhouettes.
Scalability potential: Low snaps to primary view; Middle/High/Ultra use spatial dither for stable transitions.
Hardware Impact: Keeps runtime state as one instance buffer and one indirect draw; expected CPU tile-switch cost is 0 us.

Problem: Impostors need light response from headlights without full geometry.
Solution: Normal/depth atlas RGB stores world normal, shader reconstructs normal and applies cheap camera/headlight-facing response with fog.
Rejected Alternatives: Fully unlit cardboard or additional dynamic light loops. Unlit breaks immersion; light loops violate far-field budget.
Scalability potential: Low can still snap tile selection; High/Ultra get richer normal response from the same atlas.
Hardware Impact: Adds a few ALU ops and one normal/depth sample in fragment, trading saved triangle cost for visible far-field lighting.

## Loop 3 Decisions

Problem: Streaming must hand off LOD2 impostors beyond 500m without adding a packed `ChunkState` bit.
Solution: Added `WorldChunkResidencyManager` native `OctahedralImpostorInstance` publish buffer and optional `HectonOctahedralImpostorRenderer` binding. Slow tick emits chunks beyond `impostorLod2DistanceMeters` while the existing load radius keeps real geometry inside 500m.
Rejected Alternatives: Adding `LOD2` to `ChunkState`. The byte enum is already saturated; a new flag would collide with existing state capacity and break serialized assumptions.
Scalability potential: Low uses the same records with nearest-angle shader snap; High/Ultra can keep more far chunks active because the renderer remains one indirect draw.
Hardware Impact: Expected low-end gain is replacing far chunk GameObject proxy churn with one NativeArray upload; estimated swap path stays under 100 us for 512 authored chunks on slow tick, pending profiler proof.

Problem: A flat quad must avoid obvious intersection and origin-shift jitter.
Solution: Shader samples baked depth alpha for `SV_Depth` bias and applies `_GlobalFloatingOffset` to universe-space impostor centers, matching existing HLOD AUP render convention.
Rejected Alternatives: Pure alpha-test quads with no depth shaping, or runtime mesh shells. Alpha-only quads fail occlusion; mesh shells reintroduce triangle cost.
Scalability potential: Low keeps the same depth path; High/Ultra can use better baked depth/normal atlases without changing runtime topology.
Hardware Impact: Depth bias adds one cheap scalar operation after the existing normal/depth sample; expected cost is fragment-bound but cheaper than real far geometry.

Problem: Low-tier MX350 must cut fragment ALU and swap without GC.
Solution: Renderer pushes `_HectonImpostorQualityFlags`; shader disables secondary-tile dither when quality is Low/MX350/Unknown. Streaming publishes native records and clears/binds buffers instead of instantiating or destroying proxy objects.
Rejected Alternatives: Runtime LODGroup prefab swaps and per-object GameObject pools. Pools still touch transforms and lifecycle code on swap.
Scalability potential: Low = nearest tile only; Middle = dithered tile selection; High = normal/depth lighting; Ultra = denser far residency if VRAM allows.
Hardware Impact: Expected MX350 gain is one indirect draw and no Transform churn; exact microseconds pending profiler capture.

## Loop 4 Decisions

Problem: Task 18 requires shader/SRP validation, but Unity C# compilation is blocked by unrelated domains.
Solution: Verified local scripts with `validate_script`, read the shader asset through Unity MCP, confirmed the material data is inside `UnityPerMaterial` CBUFFER and the per-instance data is in a `StructuredBuffer`, and filtered the console for impostor shader names with zero hits.
Rejected Alternatives: Claiming full project compile success or patching unrelated `GlobalSignals`, `EcosystemDirector`, or audio raymarch files. Those are outside this prompt's domain.
Scalability potential: Shader is SRP-batcher-facing for material constants and indirect-instance friendly for far batches; final support proof must wait for global compile cleanup.
Hardware Impact: No measured runtime impact yet; verification is blocked before playmode/profiler capture by external compile errors.

## Loop 5 Self-Inquisition

Problem: Re-read found two bloat/cost issues: Low tier still computed IGN before the branch, and fragment depth used `positionCS.z / w`, which is unsafe for pixel-stage `SV_POSITION`.
Solution: Moved IGN evaluation inside the non-low branch and changed depth output to use saturated device depth plus baked-depth bias.
Rejected Alternatives: Leaving the branch as-is because it was "probably optimized." That violates the MX350 math LOD requirement.
Scalability potential: Low now truly snaps without dither ALU; higher tiers keep the dithered lie.
Hardware Impact: Low tier saves the IGN dot/fract sequence per impostor fragment; exact microseconds depend on overdraw and are pending GPU capture.

Problem: Prompt re-read required no billboard roll when camera rolls.
Solution: Verified billboard basis is derived from object-to-camera vector and fixed world up, not camera up, with a fallback right vector for near-vertical views.
Rejected Alternatives: Using camera transform up/right. That would roll the quad with camera roll and make horizon impostors swim.
Scalability potential: Same stable basis across all tiers.
Hardware Impact: No added cost; fixed-up basis is the existing math path.

## Loop 6 Continued Recheck

Problem: The active WCRM integration had evolved from the first native `OctahedralImpostorInstance` publish buffer into a signal-driven `float4x4` matrix stream, leaving one dead persistent native array and stale status wording.
Solution: Removed `_lod2ImpostorInstances` allocation, sentinel registration, and disposal. Kept the matrix layout because `HlodImpostorSwapJob`, `InstanceCulling.compute`, and `Hecton_Impostor.hlsl` agree on center `_m03/_m13/_m23`, radius `_m31`, flags `_m32`, and spawn time `_m33`.
Rejected Alternatives: Keeping both storage paths "just in case." That burns native memory per chunk and creates two authorities for the same residency state.
Scalability potential: Low uses the same matrix stream with snap flags; Middle/High/Ultra use the culling-visible matrix stream and can buy more far silhouettes with the saved CPU/object churn.
Hardware Impact: Removes one persistent `NativeArray<OctahedralImpostorInstance>[maxChunkCount]`; estimated memory saved is roughly 128 bytes per configured chunk before sentinel overhead, about 64 KB at 512 chunks.

Problem: Renderer tick still wrote matrix-stream mode, fade time, and fade duration through `Material.Set*` each frame even though the shader consumes them as globals.
Solution: Moved `_HectonUseVisibleMatrixStream`, `_HectonImpostorTimeSeconds`, and `_HectonImpostorFadeOutSeconds` updates to `Shader.SetGlobal*`, leaving material constants for atlas, depth scale, and quality flags only when they change.
Rejected Alternatives: Per-frame material property churn. It risks SRP-batcher instability and dirty material state for values that are draw-global in this renderer path.
Scalability potential: Low/MX350 avoids extra material dirtiness; High/Ultra can use GPU culling without creating material variants for matrix-stream mode.
Hardware Impact: Expected CPU win is small but deterministic: three per-frame material property writes moved off the material hot path. Exact microseconds remain blocked by global compile/profiler dependency.

Problem: Unity MCP validation became unavailable after editor readiness waits and script validation attempts.
Solution: Ran local file contract checks and a `dotnet build Assembly-CSharp.csproj --no-restore` probe to expose the current compile wall without editing unrelated domains.
Rejected Alternatives: Claiming compile success or patching missing audio/physics/persistence namespaces outside the render architect domain.
Scalability potential: No runtime change; this preserves the render work while external domain owners clear their assembly breaks.
Hardware Impact: No runtime impact. Verification remains blocked by unrelated project-wide compile errors and stale generated csproj state until Unity imports the new scripts.

## OMEGA POLISH CHANGES

Problem: The fallback impostor GPU payload still carried more data than the shader uses.
Solution: Packed `OctahedralImpostorInstance` to two `Vector4`s with explicit 32-byte layout: `CenterFade` and `SizeFlags`. Removed the unused `Matrix4x4` and the `Matrix4x4.TRS` call from instance creation.
Rejected Alternatives: Keeping a general local-to-world matrix for future flexibility. This is a horizon billboard system, not a transform renderer; the matrix was dead weight.
Scalability potential: Low/MX350 fallback batches consume minimum upload bandwidth; High/Ultra still use the 64-byte matrix stream when GPU culling is active.
Hardware Impact: Fallback structured-buffer stride drops from 112 bytes to 32 bytes per instance. At 512 impostors this is roughly 40 KB less upload/storage per fallback buffer, before driver overhead.

Problem: The renderer allocated fallback upload buffers, indirect args, and a quad during `Awake` even when no impostors were bound.
Solution: Made renderer resources lazy. `Tick` now allows the GPU-culling matrix stream without requiring `_instanceBuffer`, and local indirect args are created only for fallback instance-buffer rendering.
Rejected Alternatives: Eager allocation for perceived readiness. It spends memory on scenes where no far HLOD is currently visible.
Scalability potential: Low-tier scenes with no distant wreck/base impostors pay 0 renderer buffer cost until first bind; High/Ultra still allocate on first visible batch.
Hardware Impact: Avoids cold `NativeArray`, `GraphicsBuffer`, indirect args, and fallback mesh allocations at renderer startup. Exact microseconds are scene-dependent.

Problem: The editor baker copied every tile through a temporary `Texture2D` and `GetPixels`, creating unnecessary editor-side allocations and CPU copy work.
Solution: Removed `tilePixels` and reads directly from the tile render target into the destination atlas via `atlas.ReadPixels(..., tileX, tileY, false)`. Added a `try/finally` guard to restore `RenderTexture.active` and replacement shader state.
Rejected Alternatives: Leaving editor allocation churn because it is not runtime. Slow tooling produces fewer usable high-quality impostors and hides bake regressions.
Scalability potential: Same output atlas, lower bake overhead; large object batches become less punishing to author.
Hardware Impact: Removes one tile texture allocation and 16 per-tile `GetPixels` array copies per bake.

Problem: The atlas tile mapping used integer divide/modulo even though the mandate fixes the atlas at 4x2.
Solution: Replaced runtime `viewIndex % 4` and `viewIndex / 4` with `viewIndex & 3u` and `viewIndex >> 2`.
Rejected Alternatives: Keeping dynamic atlas columns in the hot shader path. The prompt requires one 2048 4x2 octa atlas.
Scalability potential: Low and high tiers share cheaper tile mapping; higher tiers spend ALU on view dither and normal response instead.
Hardware Impact: Saves one integer modulo/divide pair per impostor fragment/vertex atlas selection path; exact GPU microseconds require profiler capture after global compile is fixed.

## Loop 8 Fixed-Atlas Inquisition

Problem: Loop 7 removed atlas division, but the shader and renderer still carried dynamic `_AtlasGrid` and unused `_DepthScale` properties. That bloats UnityPerMaterial state, editor material writes, and renderer property IDs for values the fixed atlas mandate does not allow to vary.
Solution: Removed `_AtlasGrid` and `_DepthScale` from the impostor shader properties/CBUFFER, removed renderer `Shader.PropertyToID` fields and material writes, removed editor material writes, and moved atlas addressing to fixed constants. Loop 10 corrected the final scale to the actual 2048x2048 4x4 square-cell lattice with 4x2 occupied cells.
Rejected Alternatives: Keeping dormant properties for future atlas variants. The task mandates one 2048 atlas with 8 views in a 4x2 layout; future flexibility here would tax every current draw.
Scalability potential: Low/MX350 keeps the cheapest nearest-tile path with less material state; Middle/High/Ultra spend the saved ALU/property churn on dither, normal/depth lighting, and denser far-silhouette residency.
Hardware Impact: Removes two material properties from SRP-batcher CBUFFER state and two editor/runtime material writes per data/material change. Exact microseconds remain PENDING VERIFICATION because global compile/playmode profiling is still blocked.

Validation: Unity MCP `validate_script` reports 0 diagnostics for `HectonOctahedralImpostorRenderer.cs`, `HectonOctahedralImpostorBaker.cs`, and `HectonOctahedralImpostorTypes.cs`. Unity console filters for `HectonOctahedral`, `Hecton_Octahedral`, and `Hecton_Impostor` return 0 errors. `dotnet build Hecton8.Core.csproj --no-restore --nologo --verbosity:minimal` still fails with 92 project-wide errors in unrelated missing contracts/namespaces (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, audio propagation/echolocation, CCD, MacroSwarm, BrineLayerSample, etc.) and generated csproj staleness for the new renderer.

## Loop 9 Stable-Fade Hot Path

Problem: The fragment shader still evaluated IGN fade dithering for every visible impostor, even after fade reached full opacity. That is permanent fragment ALU for a transition effect that should only exist during handoff.
Solution: Wrapped fade dither and fade `clip` in `if (input.fade01 < 0.999h)`, keeping transition dithering during fade-in/out while stable fully visible impostors skip the extra noise path.
Rejected Alternatives: Always-on fade dither for simplicity. It spends fragment work forever to support a state that exists only during residency transitions.
Scalability potential: Low/MX350 gets cheaper stable horizon impostors; Middle/High/Ultra keep the same transition quality and can spend the saved ALU on dithered view selection and normal/depth lighting.
Hardware Impact: Saves one IGN dot/fract sequence and one fade clip per stable impostor fragment. Exact microseconds remain PENDING VERIFICATION because Unity refresh/console readiness is unstable and global compile remains blocked.

Validation: `rg` confirms the stable-fade guard is present and fixed 4x2 atlas constants remain. Unity asset refresh timed out after 60 seconds waiting for editor readiness. Console filter for `Hecton_Impostor` returned 0 entries on retry; `Hecton_Octahedral` read retries timed out. Renderer `validate_script` still reports 0 diagnostics.

## Loop 10 Atlas Contract and Bake Truth

Problem: The runtime shader sampled albedo as `sample * _BaseColor` and then multiplied alpha by `_BaseColor.a` again. That double-applied tint alpha and could erase valid impostor silhouettes.
Solution: Sample albedo/alpha raw, apply `_BaseColor.a` once for alpha clip, and apply `_BaseColor.rgb` only to RGB lighting.
Rejected Alternatives: Lowering `_AlphaClipThreshold` to hide the issue. That would preserve the double-alpha bug and make authored alpha unreliable.
Scalability potential: All tiers get the same silhouette correctness; Low/MX350 keeps the same cheap clip path.
Hardware Impact: No extra cost; the same multiply count is now spent correctly.

Problem: After the 32-byte payload pass, code still calculated capture depth/magnitude/max values that `OctahedralImpostorInstance.Create` ignores.
Solution: Removed `resolvedSize.magnitude`, HLOD max-depth calculation, and matrix fallback max-depth calculation; callers pass `0f` into the preserved API slot.
Rejected Alternatives: Keeping dead math for "semantic clarity." Dead magnitude uses a square root in a utility path and violates the anti-bloat pass.
Scalability potential: Low through Ultra share the smaller CPU prep path; future depth payload would need an explicit format revision, not a ghost parameter.
Hardware Impact: Removes one magnitude sqrt from `HectonOctahedralImpostorData.CreateInstance` and two max-depth chains from renderer conversion paths.

Problem: The baker wrote 512x512 square captures into the first two rows of a 2048x2048 atlas, but shader UVs treated the atlas as two 1024-high rows. That sampled across empty/neighbor space and broke view lookup.
Solution: Reframed the atlas as a 4x4 square-cell lattice with 4x2 occupied cells. Updated shader tile scale to `float2(0.25, 0.25)`, data metadata to `(4,4)`, and baker constants to explicit `TileWidth`/`TileHeight`.
Rejected Alternatives: Stretching captures into 512x1024 cells. That would distort silhouettes and bake aspect error into every impostor.
Scalability potential: Low gets stable nearest-view sampling; Middle/High/Ultra get correct dithered transitions and normal/depth response from the intended tile.
Hardware Impact: No extra runtime cost; this is a correctness fix with the same bitmask/shift addressing.

Problem: The original albedo pass used source materials directly, risking baked lighting/post effects and double-lighting in the runtime impostor shader. It also did not explicitly force source LOD0.
Solution: Added `Hidden/Hecton8/Editor/OctahedralImpostorAlbedoAlpha`, an unlit replacement shader that preserves base color and alpha only. Baker now loads it for albedo capture and forces cloned `LODGroup`s to LOD0 before capture.
Rejected Alternatives: Capturing the regular scene-lit material pass. That makes far impostors inconsistent under submarine headlights and time-of-day lighting.
Scalability potential: Low keeps clean albedo with cheap runtime lighting; High/Ultra can spend normal/depth lighting on a neutral source instead of double-lit baked color.
Hardware Impact: Editor-only replacement shader cost; runtime avoids compensating hacks for baked-light artifacts.

Validation: `validate_script` reports 0 diagnostics for `HectonOctahedralImpostorBaker.cs`, `HectonOctahedralImpostorData.cs`, and `HectonOctahedralImpostorRenderer.cs`. Console filters for `Hecton_Impostor`, `Hecton_Octahedral`, `OctahedralImpostorAlbedoAlpha`, and `HectonOctahedral` return 0 errors. `rg` confirms no stale `TileSize`, `AtlasRows`, `_Cutoff`, dead depth magnitude/max calculation, or double-alpha pattern remains in touched HLOD files.

## Loop 11 Matrix Fallback Bounds

Problem: `BindNativeMatrices` can enter `BindMatricesAsOctahedralFallback` before `ResolveMatrixBounds` if the GPU culling source buffer is unavailable. That left fallback draws using stale/default bounds in a failure path.
Solution: `BindMatricesAsOctahedralFallback` now computes combined bounds while converting matrices to 32-byte impostor instances, then assigns `_drawBounds` and `_hasBoundsOverride`.
Rejected Alternatives: Assuming GPU culling source buffer allocation never fails. The renderer already has a fallback path; its culling bounds must be correct.
Scalability potential: Low/MX350 fallback mode and any degraded GPU-culling path retain correct culling; High/Ultra still use compute-visible buffers when available.
Hardware Impact: Adds one bounds encapsulation pass only during matrix binding/fallback conversion, not per-frame `Tick`. Prevents invisible or over-wide far HLOD draws when culling service is unavailable.

Validation: First Unity `validate_script` call disconnected mid-command; retry reports 0 diagnostics for `HectonOctahedralImpostorRenderer.cs`. Console filter for `HectonOctahedral` returns 0 errors.

## Loop 12 Safe Normal Bake

Problem: The editor normal/depth replacement shader used raw `normalize(input.normalWS)`. Zero-length or malformed source normals could bake undefined values into the normal atlas, later poisoning runtime lighting.
Solution: Replaced raw normalize with explicit length-squared guard and `rsqrt`, falling back to world-up normal for invalid input.
Rejected Alternatives: Trusting authored mesh normals. The baker is a production tool and must harden bad source assets instead of baking their defects into shared HLOD atlases.
Scalability potential: Low through Ultra share cleaner baked normals; High/Ultra lighting benefits most because they spend more visual budget on normal response.
Hardware Impact: Editor-only ALU increase; runtime cost unchanged. Prevents visual artifacts and NaN contamination in baked data.

Validation: `rg` confirms no `normalize(` remains in `Hecton_EditorOctaImpostorNormalDepth.shader`. Unity console reads for the normal/depth shader timed out because the MCP session was not ready.

Exact cinematic cheats used:
- 8 fixed octahedral views in 4x2 occupied cells inside the 2048x2048 4x4 square-cell atlas.
- Fixed world-up camera-facing billboard instead of transform rotation.
- Bitmask/shift atlas addressing.
- IGN dither only on non-low tiers.
- Fade dither only while impostors are actually transitioning.
- Unlit editor albedo/alpha capture; runtime fakes far lighting from baked normals.
- Safe editor normal bake falls back to world-up on malformed normals.
- Fallback draw bounds are recooked from matrix payloads when GPU culling is unavailable.
- Baked normal/depth response on a flat quad instead of far mesh lighting.

Final Git Diff:
- Tracked diff currently includes `WorldChunkResidencyManager.cs`, `Status_HLOD_IMPOSTOR_BAKER.md`, and `Rationale_HLOD_IMPOSTOR_BAKER.md`.
- New untracked assets/scripts in this task: `HectonOctahedralImpostorRenderer.cs`, `HectonOctahedralImpostorTypes.cs`, `HectonOctahedralImpostorData.cs`, `HectonOctahedralImpostorBaker.cs`, `Hecton_Impostor.hlsl`, `Hecton_OctahedralImpostor.shader`, `Hecton_EditorOctaImpostorNormalDepth.shader`, `Hecton_EditorOctaImpostorAlbedoAlpha.shader`, and `LOG_HLOD_IMPOSTOR_BAKER.md`.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo --verbosity:minimal` remains red with 92 errors due unrelated missing namespaces/types and stale generated csproj state; latest probe also reports `WorldChunkResidencyManager.cs` cannot see `HectonOctahedralImpostorRenderer` because Unity has not regenerated project files for the new script.
