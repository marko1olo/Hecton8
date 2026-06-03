# Rationale_1726 - Visor Glass Dirt, Scratch, Salt Mask Baker

Proof state: PENDING_UNITY_EDITOR_BAKE

## Decision 00 - Operative Domain Boundary
Problem: Mandated file Docs/Actual Domains of Project.txt is missing, but the XML prompt grants a concrete domain and write boundary.
Solution: Use the extracted XML role and allowed directories as the operative domain: Editor baker, UI visor overlay, and visor shader include/runtime material contract.
Rejected Alternatives: Editing outside the XML boundary or waiting for a missing domain document would either violate domain control or stall the batch. Standard Unity "fix wherever referenced" is too broad and unsafe under 20+ agent concurrency.
Scalability potential: Low uses static compact mask assets; Middle uses the same packed contract at higher resolution; High uses denser mask detail; Ultra uses 2K packed visor wear without changing runtime truth.
Hardware Impact: i3/MX350 gain is removal of CPU pixel churn and texture allocation from runtime. Estimated saved runtime allocation: any prior Texture2D/pixel path becomes 0 B steady-state. Exact microseconds: PENDING STATIC AUDIT.

## Decision 01 - Offline Visual Fake Route
Problem: Runtime condensation simulation and CPU texture writes would spend frame time on presentation-only glass wear.
Solution: Bake dirt, scratches, salt, and condensation frames into one static RGBA mask with GPU shader offsets/thresholds at runtime.
Rejected Alternatives: Real-time CPU SetPixels, multiple mask textures, or gameplay-owned fog texture state. These create GC, upload bandwidth, and material management risk without adding gameplay truth.
Scalability potential: Low 512 mask, Middle 1024, High 1536, Ultra 2048. Same channel contract, only offline fidelity changes.
Hardware Impact: i3/MX350 avoids runtime texture upload stalls and reduces fragment texture fetches versus separate masks. Estimated fetch reduction target: 4 texture reads to 1 packed read, exact GPU us PENDING capture.

## Decision 02 - Packed Mask Shader Contract
Problem: SuitVisor.shader currently derives glass wear from separate scratch/fingerprint/droplet textures plus procedural ALU, while the assignment requires one baked 2K mask and no runtime CPU texture updates.
Solution: Add a single `_VisorMaskTex` contract: R=dirt/fingerprint, G=scratches, B=salt crust, A=64-frame condensation atlas. The shader samples RGB from stable visor UV and alpha from an 8x8 atlas using `_Time`, existing condensation strength, and cheap UV drift.
Rejected Alternatives: Assigning four texture slots, creating a runtime RenderTexture, or driving condensation through SuitHUDV4CanvasOverlay. Standard Unity CPU `Texture2D.Apply` paths are too slow and produce upload stalls; HUD overlay ownership would also mix presentation glass wear with UI data routing.
Scalability potential: Low keeps the same shader contract with a 512 offline bake; Middle uses 1024; High uses 1536; Ultra uses 2048 plus denser offline noise. Runtime truth and DTO layouts stay unchanged.
Hardware Impact: i3/MX350 avoids per-frame CPU texture work and collapses authored dirt/scratch/salt/condensation source data into one sampler binding. Estimated CPU upload saved: target 0.05-0.30 ms per avoided Apply path; exact profiler proof PENDING.

## Decision 03 - Project Texture Path
Problem: XML prompt names `Assets/Art/Textures/UI`, but the repository first-party asset root is `Assets/_Project/Art`.
Solution: Default baker output to `Assets/_Project/Art/Textures/UI/TX_Visor_{assetName}_Masks.png` and create the folder through AssetDatabase if missing.
Rejected Alternatives: Writing to `Assets/Art` would create a parallel non-project asset tree and bypass established `_Project` ownership. Writing into existing Detali textures would mix generated bake output with hand-authored detail art.
Scalability potential: Same generated path can hold Low/Middle/High/Ultra outputs by asset name without changing shader property names.
Hardware Impact: Importer settings enforce non-sRGB compressed mask storage; expected VRAM for 2048 BC7 is about 5.33 MiB, versus 16 MiB raw RGBA32. Exact importer confirmation PENDING.

## Decision 04 - Shader File Boundary Exception
Problem: The XML write scope names shader include directories, but the real visor material consumer is `Assets/_Project/Art/Shaders/SuitVisor.shader`; without a shader property and sampler, the baked mask would be dead data.
Solution: Make a narrow cross-boundary shader contract edit: add `_VisorMaskTex`, strength/UV/flipbook vectors, one flipbook sampler helper, and integrate the baked channels into existing scratch/smudge/condensation/refraction terms.
Rejected Alternatives: Creating an include without referencing it would not affect runtime. Editing post effects only would not dirty the physical helmet glass. Standard Unity material cloning was rejected because it risks runtime allocations and SRP Batcher churn.
Scalability potential: Low/Middle/High/Ultra all use the same shader contract; only the static imported texture resolution and detail density vary.
Hardware Impact: i3/MX350 pays one static packed mask binding and no CPU upload. Baked RGB presence gates alpha so the built-in black texture cannot accidentally fog glass when no asset is assigned.

## Decision 05 - Condensation Loop Math
Problem: A flipbook condensation atlas can visibly pop between frame 63 and frame 0 if the noise domain is not periodic.
Solution: Generate each alpha frame with a time coordinate on a unit circle: `t = frame / 63`, `time = (cos(tau*t), sin(tau*t))`. Frame 63 intentionally duplicates the endpoint, so shader blending from 63 to 0 is visually stable. Droplet pulses use sinusoidal phase offsets; fog samples use the same periodic vector. The validator samples frame 0 vs 63 and aborts if average alpha delta exceeds 0.05.
Rejected Alternatives: Linear 3D time noise, CPU generated random frames, or a runtime RenderTexture simulation. These either pop, allocate, or move visual-only work into gameplay.
Scalability potential: Low uses coarser tile resolution; Middle and High sharpen droplets; Ultra uses 256 px tiles in a 2048 atlas. The same periodic model applies at every resolution.
Hardware Impact: Runtime animation is two alpha texture samples and lerp; no Texture2D.Apply, no CPU pixel memory. Estimated CPU saved: 0.05-0.30 ms per avoided dynamic fog upload on weak silicon.

## Decision 06 - Compaction Fence And Runtime UI Safety
Problem: HUD readers can become unsafe if native memory compaction starts between handle lookup and pointer use.
Solution: No new DataVault route was introduced. Existing SuitHUDV4CanvasOverlay glitch table access checks `IsCompactionFenceActive` before and after `TryReadOnlyHandle`, validates handle identity, and clears binding on read failure. UIStateStore scalar readers return finite fallbacks when data is absent/stale.
Rejected Alternatives: Passing visor fog state through a new native pointer/job lane or adding GlobalRegistry hot polling. Standard Unity scene searches or service lookups in Update would violate cold-DI routing.
Scalability potential: Low devices get stable fallback UI rather than a stall; Middle/High/Ultra can still show richer shader glass because the baked mask is independent of DataVault compaction state.
Hardware Impact: Avoids job fence waits and hidden Complete calls in the visor path. Estimated runtime allocation added by this work: 0 B steady state.

## Decision 07 - Importer And VRAM Budget
Problem: A raw 2048 RGBA32 visor mask would cost 16 MiB before mip overhead, and multiple separate masks would violate the MX350 VRAM discipline.
Solution: Pack all visor wear into one mask and force non-sRGB compressed import: Standalone BC7, Android/iPhone ASTC_6x6, Clamp, no mipmaps to prevent 8x8 flipbook tile bleed.
Rejected Alternatives: Four separate mask textures, uncompressed PNG import defaults, or mipmapped alpha atlas. Standard mipmaps would reduce shimmer but risk cross-frame condensation bleeding between atlas tiles.
Scalability potential: Low 512 = about 0.25 MiB BC7, Middle 1024 = about 1 MiB, High 1536 = about 2.25 MiB, Ultra 2048 = about 4 MiB. Two Ultra masks remain under 16 MiB.
Hardware Impact: i3/MX350 saves about 12 MiB per mask versus raw RGBA32 and avoids three extra sampler bindings versus separate dirt/scratch/salt/condensation masks.

## Decision 08 - Build Gate
Problem: The task mandates compilation throttling, and the latest APEX directive requires no build spam and source/static proof over CPU-heavy compile loops.
Solution: Early build sample was CPU_LOAD=88, so build was blocked. Final sample was CPU_LOAD=23 and no dotnet/csc, but build was still not launched because the latest directive requested static validation only. Static verification used `git diff --check`, meta scan, source line reads, shader/property grep, and runtime texture mutation grep.
Rejected Alternatives: Running dotnet under high CPU, running a gratuitous build after APEX no-spam, or reporting a compile pass without execution. All would violate the active protocol.
Scalability potential: No runtime scalability impact; this is build hygiene.
Hardware Impact: Avoided adding host contention during the multi-agent batch. Compile proof remains PENDING_UNITY_EDITOR_BAKE.

## Decision 09 - APEX Code Proof Over JSON
Problem: The latest APEX directive rejects JSON proof artifacts, but the initial prompt/report route had produced a stale JSON file and a baker writer path.
Solution: Removed the JSON writer, report path UI field, report helpers, source hashing helpers, and stale Docs/Reports/VISOR_BAKER_REPORT_1726.json. The baker now writes only the generated packed PNG asset and binds it to the default visor material.
Rejected Alternatives: Keeping the JSON artifact would create false proof drift after shader/baker edits. Standard report-first workflow is lower value than source-only proof for this protocol.
Scalability potential: Low/Middle/High/Ultra unchanged; removing report I/O only reduces editor-side noise.
Hardware Impact: No runtime impact. Editor bake loses one extra text-file write and hash pass.

## Decision 10 - Shared Baker Transaction Route
Problem: VisorMaskBaker duplicated asset-folder/name/dispatch/file-write helpers and wrote PNG bytes directly, weaker than the established ProceduralTextureBaker transaction path.
Solution: Routed folder creation, name sanitization, dispatch-group math, atomic write, finalization, and rollback through ProceduralTextureBaker. Added a visor material snapshot so failed imports restore material bindings.
Rejected Alternatives: Keeping private duplicated helpers and direct File.WriteAllBytes would create drift and partial-output risk.
Scalability potential: Same quality ladder; editor transaction safety improves for every resolution.
Hardware Impact: Runtime 0 us. Editor bake avoids managed Color[] validation and limits failure cleanup to deterministic rollback.

## Decision 11 - Params Payload And Render Target Failure Gate
Problem: The params upload path allocated a one-element managed array for each bake/dry-run, and RenderTexture.Create() failure was not surfaced before dispatch.
Solution: Use one prewarmed static GpuBakeParams payload array for SetData and abort immediately when RenderTexture allocation fails, destroying the rejected RT.
Rejected Alternatives: Keeping a per-bake params array was small but avoidable; dispatching into an uncreated RT would convert allocation failure into undefined editor-side bake behavior.
Scalability potential: Low/Middle/High/Ultra keep the same shader contract; higher tiers fail fast before readback if the GPU cannot allocate the requested target.
Hardware Impact: Runtime 0 us. Editor bake removes one managed params-array allocation per dispatch and prevents invalid 2K RT dispatch attempts on constrained devices.

## Decision 12 - Single Asset Rollback Overload
Problem: VisorMaskBaker needed one output rollback snapshot but had to allocate a temporary one-element string array to use the existing shared helper.
Solution: Extend ProceduralTextureBaker with a one-path overload and route VisorMaskBaker through it.
Rejected Alternatives: Keeping caller-side `new[] { texturePath }` was avoidable allocation; adding a private visor-only rollback helper would duplicate the first-party transaction owner.
Scalability potential: Same output ladder. The shared overload helps single-output bakers stay on the common rollback route without caller arrays.
Hardware Impact: Runtime 0 us. Editor bake removes one small managed array allocation before output transaction capture.

## Decision 13 - Editor GPU Device Gate
Problem: The full baker checked compute shader availability, but dry-run did not mirror that gate, and neither path checked whether the editor GPU supports R8G8B8A8_UNorm for random-write load/store and CPU readback.
Solution: Add symmetric compute-shader and pre-transaction format gates using SystemInfo.supportsComputeShaders and SystemInfo.IsFormatSupported for GraphicsFormatUsage.LoadStore and ReadPixels.
Rejected Alternatives: Letting Dispatch, RenderTexture.Create, or ReadPixels discover the failure later would happen after path setup and could obscure the real platform limitation.
Scalability potential: Low/Middle/High/Ultra all use the same packed format; unsupported hardware fails before mutating assets.
Hardware Impact: Runtime 0 us. Editor bake avoids wasted RT allocation and transaction setup on devices that cannot support the required mask format.

## Decision 14 - Stable RGB Full-Visor UV
Problem: The compute kernel originally reused local 8x8 flipbook UV for all channels, so R dirt, G scratch, and B salt repeated per condensation tile while the runtime shader samples RGB as a single full-visor mask.
Solution: Keep `localUv` only for alpha flipbook condensation and evaluate stable RGB wear from full texture `uv`.
Rejected Alternatives: Changing the runtime shader to sample RGB from tile-local UV would destroy the physical visor-wear contract and make the 2K mask waste resolution. Splitting RGB and alpha into separate textures would violate the packed one-texture mandate.
Scalability potential: Low/Middle/High/Ultra all preserve the same packed texture layout. Higher resolution now buys actual full-glass detail instead of repeating tile detail.
Hardware Impact: Runtime 0 us. GPU sampling cost unchanged; visual fidelity improves because the existing single sample now reads a coherent full-visor wear field.

## Decision 15 - Baked Mask Procedural ALU Bypass
Problem: After adding the packed visor mask, SuitVisor.shader still called procedural scratch and smudge noise even when the editor baker had provided full RGB/A wear data. This kept the baked path tied to fallback fragment ALU.
Solution: Default `_VisorMaskStrengths` to zero so materials without a baked asset keep the old fallback. When VisorMaskBaker binds a generated mask it sets strengths to one, and shader-side material-strength branches skip procedural scratch, static smudge, and condensation smudge noise.
Rejected Alternatives: Removing procedural functions would break existing authored materials without baked masks. Adding a C# runtime material toggle or cloned material would violate the no runtime material-management route. Dynamic per-pixel branching on sampled mask presence was rejected; the new gates are material-strength driven.
Scalability potential: Low devices use the baked mask to avoid fallback ALU; Middle/High/Ultra can spend the saved shader budget on higher-resolution offline masks and existing visor overkill effects without changing runtime code.
Hardware Impact: Runtime CPU remains 0 us and 0 B GC. GPU fragment cost drops on baked visor materials by skipping procedural scratch and smudge loops; exact GPU microseconds require Unity/RenderDoc capture after the editor bake.

## Decision 16 - Baked Condensation Edge Bypass
Problem: The baked alpha atlas already contains edge-weighted condensation, but the shader still computed frost blue-noise warp and procedural edge shaping for baked-mask materials.
Solution: Keep condensation texture ownership split by `_VisorMaskStrengths.w`. When the packed mask is active, condensation uses the baked atlas directly. When procedural fallback is active or blended, the shader computes the old fingerprint/blue-noise/edge path inside the procedural branch.
Rejected Alternatives: Removing condensation fallback would break unbaked materials. Keeping unconditional blue-noise edge math would waste fragment ALU after the editor baker had already paid that cost offline.
Scalability potential: Low devices get the cheapest baked alpha path; Middle/High/Ultra can blend procedural fallback by lowering alpha strength if an art-directed material needs extra live variation.
Hardware Impact: Runtime CPU remains 0 us and 0 B GC. Baked visor materials skip one frost-blue-noise hash path and procedural edge solve per condensation fragment; exact GPU savings need capture after Unity bake.

## Decision 17 - Authored Scratch Sampler Bypass
Problem: The baked scratch channel can supply scratch mask gradients, but the shader still sampled and unpacked `_ScratchNormalMap` before applying baked scratches.
Solution: Add a material-strength gate: `_VisorMaskStrengths.y == 1` means baked scratch owns the path, so authored scratch normal sampling is skipped. Partial strength keeps a continuous blend by scaling authored tangent-space normal contribution.
Rejected Alternatives: Removing `_ScratchNormalMap` support would break unbaked and hand-authored materials. Keeping the unconditional sample would waste a texture fetch on baked visor masks.
Scalability potential: Low devices use the packed baked scratch path; Middle/High/Ultra can lower baked scratch strength for hybrid authored detail if art direction needs it.
Hardware Impact: Runtime CPU remains 0 us and 0 B GC. Baked visor materials skip one normal-map sample and unpack per fragment; exact GPU microseconds require capture after bake.

## Decision 18 - Packed Mask Sampler Gate
Problem: `_VisorMaskStrengths` defaulted to zero for fallback materials, but SuitVisor.shader still sampled `_VisorMaskTex` RGB and the two-frame alpha atlas before multiplying by zero.
Solution: Add uniform `visorMaskActive` from material strengths and branch-gate packed RGB sampling. Gate alpha atlas sampling separately on `_VisorMaskStrengths.w`.
Rejected Alternatives: Leaving unconditional samples would penalize every unbaked material. Removing fallback materials would violate compatibility. Per-pixel branching on sampled mask presence was rejected; this gate is material-uniform.
Scalability potential: Low devices using fallback materials avoid dead packed-mask fetches; baked materials still use the offline packed mask. Middle/High/Ultra can blend channels by strength without new variants.
Hardware Impact: Runtime CPU remains 0 us and 0 B GC. Unbaked visor materials skip one packed RGB fetch and two alpha atlas fetches per fragment.

## Decision 19 - RenderTexture Allocation Fail-Fast
Problem: VisorMaskBaker converted RenderTexture allocation failure into an InvalidOperationException, relying on the outer catch for normal unsupported/low-memory editor flow.
Solution: Replace `CreateMaskRenderTexture` with `TryCreateMaskRenderTexture`, returning a failure string and null RT without throwing. Full bake and dry-run now use the same controlled branch.
Rejected Alternatives: Keeping exception-as-control-flow makes expected GPU allocation failure harder to audit and noisier under constrained editor devices.
Scalability potential: Low-end editor devices fail before dispatch/readback while preserving rollback cleanup. Higher tiers unchanged.
Hardware Impact: Runtime 0 us and 0 B GC. Editor failure path avoids exception allocation/stack capture on RT creation failure.

## Decision 20 - Material Contract Before Asset Transaction
Problem: VisorMaskBaker captured output rollback state before checking whether the default visor material actually supported the packed-mask shader contract.
Solution: Move `TryCaptureDefaultVisorMaterialSnapshot` before output folder creation and rollback snapshot capture. Invalid material/shader setup now fails before asset transaction work starts.
Rejected Alternatives: Keeping rollback capture first wastes editor I/O/allocation on a bake that cannot be applied to the required material.
Scalability potential: No visual ladder change. Low-end editor devices avoid unnecessary filesystem work on contract failures.
Hardware Impact: Runtime 0 us and 0 B GC. Editor failure path reduces avoidable asset transaction preparation.
