# Rationale_SHINOBU_43

## Decision 001 - Buffer-Only Material Authority
Problem: UberNoir material response needs runtime rust/moss/SSS/emissive variation without breaking SRP Batcher through per-material mutation.
Solution: Use global GraphicsBuffer-backed DTOs indexed by instance/visible index. Shader reads material state; C# writes preallocated CPU mirrors and uploads to GPU buffers.
Rejected Alternatives: `Material.SetFloat`, `Material.SetColor`, `MaterialPropertyBlock`, and `Renderer.material` edits are rejected because they fragment batching or mutate renderer-local material state.
Scalability potential: Low uses scalar wear/salt masks and legacy textures; Middle blends masks and caustic phase; High adds array sampling and SSS; Ultra adds triplanar/detail/anisotropic overlays.
Hardware Impact: On i3/MX350, avoiding SRP Batcher breaks is expected to save draw submission stalls larger than any single material effect cost; target material dispatch remains under 0.1 ms.

## Decision 002 - DTO Layout Discipline
Problem: The prompt demands a 48-byte global constants DTO and a 16-byte instance material DTO, while also requiring emissive power-level data.
Solution: Keep `InstanceMaterialDTO` exactly 16 bytes for wear/salt/bio/texture hash and place power/depth/flags in a parallel 16-byte DTO. Keep global constants at 48 bytes with explicit ARM64-safe sequential layout.
Rejected Alternatives: Expanding the required 16-byte DTO to 32 bytes was rejected because it would violate the self-audit requirement and increase bandwidth for instances that do not need power data.
Scalability potential: Low-tier can bind one sentinel entry; high/ultra tiers can bind dense visible-instance buffers with per-object response.
Hardware Impact: Parallel 16-byte lanes keep cache lines predictable and let weak GPUs skip higher-cost fields when quality is low.

## Decision 003 - AUP-Stable Procedural Wear
Problem: World-space rust, salt, moss, and caustic noise drift when floating origin or AUP offsets change.
Solution: Use AUP-stable coordinates by subtracting `_TotalUniverseOffset` from world position before procedural sampling, and keep per-instance seeds in buffer data.
Rejected Alternatives: Raw `positionWS` and material UV-only effects were rejected because they shimmer across origin shifts and cannot sell large-scale marine exposure.
Scalability potential: Low uses one scalar depth/salt curve; Middle adds mask/noise; High adds directional streaks; Ultra layers caustic and detail arrays.
Hardware Impact: Cheap devices get stable results with two to three scalar ops; expensive devices buy more texture/detail samples with the same deterministic coordinate basis.

## Decision 004 - DataVault Ownership and Minimal Core Reservation
Problem: Material response requires persistent CPU mirrors, visible indices, tuning constants, telemetry, CSV scratch, and GPU upload payloads without private NativeArray fields.
Solution: Reserved `SystemID.GraphicsMaterials` plus `BufferID.ShinobuMaterial*` lanes and requested all persistent memory through `VaultBufferHandle<T>` at boot/cold bind.
Rejected Alternatives: Local persistent `NativeArray`, runtime `List<T>`, or writing into unrelated VFX buffers. These would either fragment memory or create sibling-domain coupling.
Scalability potential: Low requests the same handles but schedules fewer rows and lower cadence; Middle/High/Ultra raise effective budget via `GlobalQualityWeight` without changing memory contracts.
Hardware Impact: DataVault ownership avoids allocator churn; expected gain on i3/MX350 is 0 B/frame GC and stable cache-linear updates.

## Decision 005 - 16B Truth DTO Plus 32B Visible GPU Payload
Problem: The assignment demands a 16-byte `InstanceMaterialDTO`, but also requires power-level, depth, and flags for emissive and culling-visible upload.
Solution: Keep `InstanceMaterialDTO` exactly 16B for core truth, use `MaterialPowerDTO` as a parallel 16B lane, then pack only visible instances into one 32B `MaterialVisibleDTO` GraphicsBuffer consumed by UberNoir.
Rejected Alternatives: Expanding `InstanceMaterialDTO` to 32B violates the ARM64 prompt. Uploading two GPU StructuredBuffers increases bind/read pressure. Encoding power into unused hash bits would make authoring brittle.
Scalability potential: Low can upload one/few visible payloads; Middle/High/Ultra can fill the same 32B visible lane with richer state while preserving source ABI.
Hardware Impact: 32B visible upload for 8192 rows is 256KB; avoiding blind 50k upload saves roughly 1.3MB/frame of bus traffic.

## Decision 006 - CBuffer and StructuredBuffer Instead of Material Mutation
Problem: Runtime material changes with `Material.SetFloat`, `SetColor`, MPB, or `Renderer.material` break SRP Batcher and clone material state.
Solution: Bind `_H8UberNoirMaterialStates` as a global StructuredBuffer and `H8UberNoirMaterialGlobals` as a 48B global constant buffer. Editor tuning writes DataVault constants, then runtime uploads the same CBuffer.
Rejected Alternatives: MaterialPropertyBlock, per-renderer material instance, shader keyword debug variants, or per-material float edits.
Scalability potential: Low fades to UV and scalar masks; Middle enables detail blend; High adds SSS/anisotropic; Ultra enables triplanar/caustic overkill from the same buffer contract.
Hardware Impact: Preserves SRP Batcher; estimated 80-500 us/frame saved in dense structure scenes versus material-instance churn.

## Decision 007 - Emergency Mock Wear Rates and CSV Bridge
Problem: No active material binary payload or texture binding binary exists in the current ledger, but the shader must be live and designer-tunable.
Solution: Generate 16B aligned `WearRateDTO` defaults and deterministic dummy wear/power/depth states, then allow `texture_set_indices.csv` to update texture hashes/slices through a byte parser and DataVault scratch buffer.
Rejected Alternatives: Failing initialization when binaries are absent, hardcoding final art assumptions, JSON parsing, or `File.ReadAllText`/string split.
Scalability potential: Low/missing-data path remains visually readable; High/Ultra swap richer slice mappings when arrays exist.
Hardware Impact: Mock generation is cold. CSV polling is Editor/Development guarded; shipping runtime avoids file polling allocations.

## Decision 008 - Dear Lie Caustics and Wear Projection
Problem: Physically honest volumetric caustics and real corrosion simulation are too expensive for Quest 3/low-tier hardware.
Solution: Use AUP-stable shader-space triangle/noise caustics and mask-driven rust/moss/salt blends. Low quality collapses to triangle/UV; high quality blends procedural/texture/triplanar detail.
Rejected Alternatives: Raymarched volumetric caustics, Navier-Stokes water-light simulation, mesh pitting, decals, or material swaps.
Scalability potential: Low: single UV albedo plus scalar rust/salt. Middle: noise/mask blend. High: SSS/anisotropic. Ultra: triplanar arrays and richer caustic interference.
Hardware Impact: Avoids a fullscreen/volumetric pass and CPU corrosion simulation; estimated 200-900 us/frame saved depending scene.

## Decision 009 - Compile-Wall Isolation
Problem: Global core compile currently fails in unrelated systems while 20+ agents mutate the workspace.
Solution: Keep SHINOBU edits confined to material runtime/shader/editor plus minimal BufferID/SystemID reservation; record compile failures as dependency blockers when they are outside this domain.
Rejected Alternatives: Editing `LocalizationManager` or `SubmarineDynamicsRuntime` from the material-response lane, or claiming green compile falsely.
Scalability potential: Compile-wall isolation preserves iteration speed for all hardware tiers by preventing cross-domain repair churn.
Hardware Impact: No frame impact. Developer hardware impact is reduced by not forcing broad unrelated rebuild edits.

## Decision 010 - Double-Buffered GraphicsBuffer Uploads
Problem: A single visible material buffer uploaded every frame can still stall the render thread even if it preserves SRP Batcher, especially when the same buffer remains bound while the next frame writes into it.
Solution: Use A/B structured and constant `GraphicsBuffer` lanes. Upload into the non-bound lane with `LockBufferForWrite` plus `UnsafeUtility.MemCpy`, then flip the read index and bind globally. Add dirty flags so unchanged visible payloads/constants do not move across the CPU/GPU boundary.
Rejected Alternatives: `GraphicsBuffer.SetData` on the bound buffer, managed arrays, `MaterialPropertyBlock`, or blind per-frame upload of all rows. `SetData` can be acceptable in cold prototypes but is not strict enough for the polish mandate.
Scalability potential: Low and Middle tiers often reuse the previous material buffer for many frames as cadence collapses; High and Ultra can still stream rich visible payloads without forcing a driver sync on the read lane.
Hardware Impact: On i3/MX350 and Quest-class UMA, skipping unchanged 8192-row payloads avoids up to 256KB/frame and reduces driver synchronization risk; estimated 20-120 us saved on upload-heavy frames before profiler capture.

## Decision 011 - Continuous Shader Collapse Instead Of Hidden High-Cost Lerp
Problem: Previous quality lerps could still evaluate high-cost noise/texture-array work before blending it away, and a stale high legacy `_H8GlobalQualityWeight` could keep SHINOBU materials expensive after thermal degradation.
Solution: `H8UberNoirGlobalQualityWeight()` now chooses the SHINOBU CBuffer quality when material flags are active. Texture-array blend is continuous but fades to zero below q=0.12. Caustics branch to triangle projection below q=0.25, and macro wear noise branches to triangle noise below q=0.3.
Rejected Alternatives: Binary low/high hardware switches, `max(materialWeight, legacyWeight)`, and always-running procedural noise followed by a zero blend. Those approaches either pop visually or lie about ALU savings.
Scalability potential: Low: UV base texture, scalar rust/salt/moss, triangle caustics/noise. Middle: array slices fade in and richer noise resumes. High: triplanar, SSS, anisotropic metals. Ultra: texture arrays, procedural caustic interference, and visual overkill remain available.
Hardware Impact: Low-tier fragment paths avoid several texture-array samples and two value-noise layers per caustic evaluation. Exact GPU microseconds require Unity shader import/profiler capture, but the branch removes work rather than only hiding it in the final color.

## Decision 012 - Bootstrap Purity Without Scene Objects
Problem: The material runtime still used a hidden `MonoBehaviour` host with a generated `GameObject` and `DontDestroyOnLoad`, which violates the bootstrap/GlobalRegistry ownership law and can pollute scene lifetime under multi-agent editor runs.
Solution: Convert `ShinobuMaterialResponseRuntime` into a dispatcher-owned sealed service allocated once from the runtime initializer, registered through `GlobalRegistry`, and shut down through `Application.quitting` plus static reset. Cold service and dispatcher adapter allocations are explicitly marked.
Rejected Alternatives: Hidden scene host, `AddComponent`, `DontDestroyOnLoad`, and `enabled = false` shutdown. Those hide lifecycle ownership inside Unity scene state and make registry/compile-wall audits weaker.
Scalability potential: Low through Ultra share the same service lifetime; quality changes alter math and uploads, not scene hosts or component enablement.
Hardware Impact: Frame impact is indirect but real: no scene object creation/destruction path on play reload, no component callback scan, and no hidden object surviving failed bootstrap. Estimated gain is small per frame but removes a class of editor/runtime lifecycle stalls.

## Decision 013 - Quality-Bounded Blackbox Sampling
Problem: Blackbox telemetry previously scanned every visible material row every visual sync, turning diagnostics into a hidden O(visibleRows) tax even when GlobalQualityWeight had collapsed.
Solution: Telemetry now samples a quality-curved budget: 32-384 rows, clamped to a minimum forensic floor of 16. The state hash and means remain stable enough for autopsy while weak hardware does not pay a full 8192-row scan.
Rejected Alternatives: Full-buffer telemetry every frame, disabling telemetry on low tiers, or writing only upload time. Full-buffer scans waste CPU; disabling telemetry violates the blackbox mandate.
Scalability potential: Low uses a sparse sample and still records upload time/state hash; Middle/High/Ultra increase diagnostic density through the same continuous curve.
Hardware Impact: At 8192 visible rows, low quality samples roughly 32 rows instead of all rows, removing about 8160 DTO reads from the visual sync telemetry path. Expected CPU save is 5-25 us/frame depending cache pressure.

## Decision 014 - Deterministic Flicker And NaN Hardening
Problem: Runtime power flicker used transcendental-style phase logic and some pack inputs were only saturated after potential non-finite values entered the payload.
Solution: Replace power flicker with a deterministic hash-to-triangle wave and sanitize state/power/depth values with `math.isfinite` before pack math. This keeps rollback-friendly visual variation without `math.sin` and prevents NaN payload propagation into the shader buffer.
Rejected Alternatives: `math.sin`, UnityEngine random state, or allowing shader-side cleanup to mask bad CPU DTOs. Trig is unnecessary for a fake flicker, and shader cleanup would hide corrupted DataVault state.
Scalability potential: Low gets the same visual pulse at integer/hash cost; Ultra can spend saved ALU on material array/SSS detail instead.
Hardware Impact: Removes per-row sine-style math from material updates and prevents one bad DTO from poisoning the visible material buffer. Expected gain is small per row but deterministic and SIMD-friendlier.

## Decision 015 - Split Raw Work Shedding From Smoothed Shader Fidelity
Problem: Raw `GlobalQualityWeight` must collapse CPU work immediately under thermal pressure, but feeding the same unsmoothed value into shader branch thresholds can create visual/ALU flicker near q=0.25 and q=0.3.
Solution: Keep `MaterialRuntimeScalarsDTO.GlobalQualityWeight` raw for CPU cadence and simulation budget. Publish a separately smoothed `_publishedShaderQualityWeight` into the 48B CBuffer using `math.lerp`, `math.step`, and a smooth polynomial. HLSL caustic and macro-wear transitions now use `H8UberNoirSmoothRange01` and only evaluate heavy branches above the lower threshold.
Rejected Alternatives: Binary low/high switches, globally smoothing CPU quality, or always evaluating rich shader work and blending it away. Smoothing CPU quality delays thermal load shedding; always-running rich branches lie about performance.
Scalability potential: Low: CPU drops immediately and shader stays cheap below the lower band. Middle: cheap/rich material details crossfade over a narrow stable band. High/Ultra: rich caustics, texture arrays, triplanar wear, SSS, and anisotropic metals remain available without material swaps.
Hardware Impact: Prevents threshold thrash on weak devices and avoids heavy caustic/noise evaluation below q=0.22. Estimated gain is scene-dependent, but it removes redundant high-cost shader work instead of just smoothing the final color.

## Decision 016 - Texture Vitality As Shader Fake, Not Residency Claim
Problem: The task says to revive textures, but the binary ledger does not prove active material texture-tier payloads or Addressables texture arrays. Loading or rebinding texture arrays from SHINOBU would invent cross-domain asset authority and risk VRAM/upload stalls.
Solution: Keep texture residency untouched and add shader-only wear vitality: AUP-stable rust pores, salt crystals, wet edges, and moss veins. Low quality gets one triangle-mask family fading in from q=0.05 to q=0.18. Rich value-noise pores/veins and hash crystals start only after q=0.24 and blend to q=0.58. The effect writes PBR response channels: albedo, smoothness, occlusion, emissive moss edge, and high-quality micro-normal perturbation.
Rejected Alternatives: Runtime `Texture2DArray` loading without owner proof, material texture slot mutation, decal growth systems, mesh pitting, and CPU corrosion maps. Those either break SRP Batcher, inflate VRAM, or simulate what can be faked in the existing shader pass.
Scalability potential: Low: nearly plain UV/PBR with faint triangle wear. Middle: richer ALU pores/crystals/veins. High: vitality plus texture arrays, triplanar, SSS, and anisotropy. Ultra: visual-overkill uses the same ALU fake layered with the existing array and caustic paths, still no material clone.
Hardware Impact: CPU impact is 0 us because no C# update, DataVault handle, or GraphicsBuffer payload changed. Low-q GPU fragments skip the two value-noise layers and the micro-normal branch; exact GPU microseconds remain PENDING VERIFICATION until Unity shader import and profiler capture are possible.

## Decision 017 - Material Domain Assembly Isolation
Problem: `ShinobuMaterialResponseRuntime.cs` was under `Assets/_Project/Scripts/Rendering`, which is governed by the parent `Hecton8.Core.asmdef`. That makes material-response edits part of the largest core assembly and worsens the compile wall.
Solution: Move the runtime into `Assets/_Project/Scripts/Graphics/Materials` under `Hecton8.Graphics.Materials.asmdef`, switch the namespace to `Hecton8.Graphics.Materials`, enable unsafe on that material assembly, and add only the required core-infrastructure/package references. Move the EditorWindow into a dedicated `Hecton8.Graphics.Materials.Editor` assembly.
Rejected Alternatives: Leaving SHINOBU in Core, adding references from global `Hecton8.Editor`, or creating a new sibling dependency on unrelated rendering systems. Parent Core placement was convenient but expensive; global editor coupling would make every material tuner edit more likely to recompile unrelated editor tooling.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The benefit is developer scalability: material-response iteration is bounded to the material assembly and editor facade instead of broad Core.
Hardware Impact: No frame-time impact. Developer hardware impact is reduced compile scope after Unity regenerates project files. Unity import remains PENDING VERIFICATION because a foreign Unity batchmode process owns the project.
