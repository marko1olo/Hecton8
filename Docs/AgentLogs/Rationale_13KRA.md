# 13KRA Rationale

Status: CODED / STATIC VERIFIED / RUNTIME BUILD BLOCKED BY EXISTING CANDICE SQLITE ERRORS; EDITOR BUILD BLOCKED BY EXISTING MAPMAGIC DUPLICATE-SYMBOL ERRORS; SLNX BUILD TIMED OUT AND CHILD PROCESSES WERE STOPPED

## Decision 001 - Assignment Source

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="13KRA">`; strict batch extraction returned no prompt.
Solution: Treat the chat-provided domain directive as the active single broad audit-and-fix task, record the missing XML condition, and continue only inside the named lighting/underwater VFX boundary.
Rejected Alternatives: Fabricating an XML task count; borrowing `RENDER_ABYSSAL_LIGHTING` or other archived prompts; editing ocean/Crest domains by inference.
Scalability potential: Keeps work constrained to presentation systems that can scale from cheap depth LUT/fog cheats to high-end visual overkill without changing gameplay truth.
Hardware Impact: Avoids cross-domain churn and expensive dependencies on i3/MX350; estimated saved integration/debug cost is unmeasured but material.

## Decision 002 - Mandate Set

Problem: The assignment spans lighting, caustics, fog, god rays, VFX, and quality scaling, but forbids ocean/Crest ownership.
Solution: Use eight mandates: abyssal lighting, noir fog/dithering, fluid VFX aesthetics, URP hot-path/HLOD, MX350 compute, zero-GC, visual-fake-first, and frame/VRAM budgets.
Rejected Alternatives: Reading physics/ocean mandates as primary authority; using online docs before local project authority; relying on generic Unity graphics knowledge.
Scalability potential: Low = LUT/dither/fake fog; Middle = richer stratified fog and caustics; High = gated half-res shafts; Ultra = denser sensory overload without new truth.
Hardware Impact: MX350 path stays shader/LUT/baked-first; expected gain is avoiding any >0.1 ms unproven runtime simulation in this domain.

## Decision 003 - Light Shaft DataVault Allocation Guard

Problem: `ScreenSpaceLightShaftRuntime.LateFrameTick` executed `EnsureBuffers()` and could call `GlobalDataVault.EnsureGenerationHandle` from a hot frame phase after a handle was invalidated or never cold-created.
Solution: Make buffer creation explicit with `EnsureBuffers(bool allowAllocation)`. Cold ownership phases (`OnEnable`, DataVault service replacement) pass `true`; hot frame presentation passes `false` and fails closed when handles are unavailable or allocation is locked.
Rejected Alternatives: Allocating lazily during `LateFrameTick`; searching the scene for fallback dependencies; moving ownership into ocean/Crest systems; rewriting the shaft renderer into a new architecture without profiler proof.
Scalability potential: Low = no shaft buffer allocation spikes, cheap cached snapshots only. Middle = same contract with moderate cadence. High = denser shafts/god-ray visuals behind existing quality weight. Ultra = visual overkill from shader/RenderGraph fidelity, not new gameplay truth ownership.
Hardware Impact: i3/MX350 avoids allocator/lock path spikes in the presentation phase; estimated direct fault-frame saving is ~14 us plus reduced tail-latency risk under DataVault churn.

## Decision 004 - Dynamic Point Light Storage Allocation Split

Problem: `DynamicPointLightCullingDirector.EnsureNativeStorage(false)` looked like a no-alloc runtime call, but the boolean only blocked mock data generation. `Tick`, `GenerateMockLightCullingData`, and `TryCommitExternalSourceCount` could still allocate DataVault buffers if storage became invalid.
Solution: Add explicit `allowAllocation` to `EnsureNativeStorage` and `AcquireBuffer`. Cold owner phases allocate; runtime tick/read/commit paths resolve existing buffers only and fail closed when unavailable.
Rejected Alternatives: Leaving allocation hidden behind the old boolean; moving dynamic light ownership to Unity `Light` objects; completing jobs or reallocating during late frame; broad rewrite of the culling pipeline.
Scalability potential: Low = existing buffers only, no allocation spikes. Middle = stable cadence with reduced active lights. High = more fake bounce/dynamic payload density. Ultra = near-field overkill from existing continuous quality and thermal weights.
Hardware Impact: i3/MX350 avoids repeated vault allocation attempts when storage is invalid; estimated savings are ~22 us per bad hot-frame attempt plus reduced hitch risk from allocation locks.

## Decision 005 - Caustic Vault Lock and Black Box Ownership

Problem: `AbyssalDeferredCausticsRuntime` owned caustic buffers but did not check `DataVault.IsAllocationLocked` before calling `EnsureGenerationHandle`; black-box dump path used a historical agent ID.
Solution: Fail closed before caustic buffer allocation when the vault is locked, and route caustic dumps to `Docs/AgentLogs/Dump_13KRA.bin`.
Rejected Alternatives: Allocating during lock windows; changing ocean swell ownership; changing caustic math or RenderGraph contracts; simulating physical photon transport instead of analytic screen-space lies.
Scalability potential: Low = existing analytical caustic buffer or no caustic, no allocation. Middle = quality-weighted fake wave inputs. High = deeper profile-driven caustic intensity. Ultra = more chromatic/SDF visual overkill through existing DTOs.
Hardware Impact: i3/MX350 avoids locked-vault allocation spikes; estimated direct saving is ~16 us on fault attempts, with deterministic fail-closed behavior under memory pressure.

## Decision 006 - Domain Black Box Proof Artifact Owner

Problem: Multiple lighting/scalability critical systems still wrote crash dumps to old filenames (`SHINOBU`, `LIGHTING_SURGEON`, `DRS_SURGEON`, `LIGHT_DIRECTOR`), making proof artifacts non-attributable to agent 13KRA.
Solution: Normalize dump filenames in the 13KRA domain to `Dump_13KRA.bin` while leaving DTO layouts, authority routes, and gameplay truth unchanged.
Rejected Alternatives: Keeping legacy owner names; inventing separate per-system dump names that violate the prompt's `Dump_[YourID].bin` rule; editing ocean/Crest systems.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime; only crash forensics ownership changes.
Hardware Impact: 0 us frame cost. Debug impact is faster post-crash route ownership because one agent file owns the current lighting/VFX dump evidence.

## Decision 007 - Interior GI No-Alloc Tick Recovery

Problem: `InteriorGIProbeVolumeRuntime.Tick` called `EnsureNativeState()` every frame when native storage was not ready. That path could allocate all probe/GI buffers, write tuning, and schedule boot clear from a hot simulation phase.
Solution: Add `allowAllocation` to `EnsureNativeState` and buffer acquisition. `Tick` resolves existing storage only; cold owner phases still allocate; `_nativeReady` is set only after all required buffers resolve.
Rejected Alternatives: Reallocating probe buffers from `Tick`; using Unity Light/probe scene searches; changing probe DTO layout; touching ocean/Crest adaptation.
Scalability potential: Low = no probe allocation recovery in hot frames. Middle = existing lower-resolution GI cadence. High = stronger fake bounce through current quality. Ultra = more visual GI density through existing resolution/quality gates.
Hardware Impact: i3/MX350 avoids full probe-buffer acquisition attempts from simulation frames; estimated saving is ~28 us on invalid-storage frames plus lower hitch risk.

## Decision 008 - Thermal DRS Hot Handle No-Alloc Default

Problem: DRS state, scale state, and telemetry writers used `TryEnsure*Handle` helpers that could allocate DataVault buffers from frame-state and telemetry paths.
Solution: Make `TryEnsure*Handle` default to no allocation. Only `Awake` and DataVault rebind pass `allowAllocation: true`; hot writes fail closed if the cold route did not provide buffers.
Rejected Alternatives: Allocating state buffers from telemetry/write paths; tying quality scaling to binary tiers only; changing dynamic resolution authority contracts.
Scalability potential: Low = stable fail-closed DRS telemetry; Middle = continuous resolution/visual budget scaling; High = overkill feature weights when headroom exists; Ultra = heavier post/visual budgets bought by headroom, not allocator spikes.
Hardware Impact: i3/MX350 avoids missing-handle DataVault allocation attempts in the resolution governor; estimated saving is ~18 us per bad hot write and fewer long-tail frame spikes.

## Decision 009 - GI Relay Allocation Lock Guard

Problem: GI relay native storage setup allocated via `EnsureGenerationHandle` without first honoring `DataVault.IsAllocationLocked`.
Solution: Exit `EnsureNativeStorage` while allocation is locked; existing cold owner phases can retry when the vault is available.
Rejected Alternatives: Throwing during locked DataVault replacement; hot fallback search for lighting services; moving SH relay ownership outside the lighting domain.
Scalability potential: Low/Middle/High/Ultra lighting behavior unchanged; the guard only prevents storage acquisition in forbidden lock windows.
Hardware Impact: Estimated ~12 us avoided on locked allocation attempts and reduced risk of startup/service-rebind hitches on low-end devices.

## Decision 010 - Dynamic Light No-Alloc Path Must Not Mutate Recovery State

Problem: After the first allocation split, `EnsureNativeStorage(allowAllocation: false, allowMockGeneration: false)` still computed source/SDF buffer change flags and could reset `_sourceBufferSeeded`, `_activeSourceCount`, `_mockSdfSeeded`, clear the source manifest, or write self-audit metadata from a hot no-allocation recovery attempt.
Solution: Gate source/SDF recovery mutation, self-audit writes, and mock generation behind `allowAllocation`. No-allocation paths now only resolve existing handles and report readiness.
Rejected Alternatives: Treating metadata resets as cheap enough; allowing hot read/recovery code to mutate ownership metadata; moving light source truth to Unity `Light` scene searches.
Scalability potential: Low = existing payload remains stable when storage is unavailable. Middle = no false source wipe during quality throttling. High = richer light payloads stay recoverable. Ultra = overkill dynamic lights keep deterministic ownership handoff.
Hardware Impact: i3/MX350 avoids a small metadata write path (~5 us estimated) and, more importantly, avoids false source-manifest loss after transient vault unavailability.

## Decision 011 - Light Shaft Shader Global Clear Dirty Guard

Problem: `ClearShaderGlobals` always sent nine shader global writes even when the shaft globals were already zeroed. Repeated disable/fail-closed calls could spend CPU on redundant presentation cleanup.
Solution: Add `_shaderGlobalsCleared`. Clear writes happen once per dirty interval; `PushShaderGlobals` marks globals dirty after publishing active shaft data.
Rejected Alternatives: Keeping unconditional cleanup writes; replacing shader globals with a new buffer path without profiler proof; touching ocean/Crest atmospheric authority.
Scalability potential: Low = fewer redundant global writes on weak devices. Middle = same behavior at normal fidelity. High/Ultra = saved CPU can be spent on denser fake shafts/caustics through existing quality gates.
Hardware Impact: i3/MX350 avoids nine repeated `Shader.SetGlobal*` calls on idle clear frames; estimated saving ~6 us per redundant clear event.

## Decision 012 - Lighting Scanner Proof Artifact Ownership

Problem: `OOP_Lighting_Scanner` had a fixed `Dump_13KRA.bin` field but still wrote `RENDERING_OPTIMIZATION_REPORT_SHINOBU_347.json`, shared key `shinobu_347_day_night_gi_relay`, JSON `"agent": "SHINOBU_347"`, and `[SHINOBU_347]` logs.
Solution: Normalize scanner proof ownership to `13KRA`, including dedicated report path, shared report key, JSON agent/domain, and debug prefix.
Rejected Alternatives: Leaving editor-only proof drift; renaming runtime contracts or BufferIDs owned by historical systems; broad report schema changes.
Scalability potential: Runtime Low/Middle/High/Ultra unchanged; proof artifacts now route to one owner.
Hardware Impact: 0 us runtime cost. Audit impact is direct attribution of lighting proof outputs to the active 13KRA domain.

## Decision 013 - Water Optics Allocation Lock and Dump Ownership

Problem: `WaterOpticsRuntime.EnsureVaultBuffers` reused existing handles safely, but when buffers needed fresh/clear acquisition it ignored `DataVault.IsAllocationLocked`. Its black-box path and VisualSync comment still named `SHINOBU_265`.
Solution: Allow no-clear reuse of already resolved handles, then fail closed before fresh `EnsureGenerationHandle` calls when allocation is locked. Route WaterOptics telemetry dump to `Docs/AgentLogs/Dump_13KRA.bin`.
Rejected Alternatives: Allocating optics DTO/telemetry buffers during DataVault lock windows; changing ocean surface authority; self-spawning a hidden runtime owner; keeping old proof-owner names.
Scalability potential: Low = no locked-vault allocation spike and no optics upload if state is unavailable. Middle = stable fake absorption/scattering payload. High = richer spectral visuals through existing quality. Ultra = overkill water optics via shader constants, not new gameplay truth.
Hardware Impact: i3/MX350 avoids locked-vault bootstrap attempts; estimated saving ~15 us per faulted bootstrap attempt plus lower startup/service-rebind hitch risk.

## Decision 014 - Scooter Noir Shaft Continuous Quality Budget

Problem: `HectonScooterVolumetricShaftsFeature` used a binary VRAM check (`<=2048 ? 16 : 24`) for flashlight shadow steps even though the shader clamps to max 5. Contact shadow steps were exposed as 4-8 in C#, but shader hardcoded exactly 3 samples, so the control was dead and violated continuous quality scaling.
Solution: Feed render scale, contact-shadow sample count, and flashlight voxel-shadow sample count from continuous `HomeostasisBrain.GlobalQualityWeight` plus a continuous low-VRAM pressure curve. Update shader contact shadows to consume `_HectonContactShadowSteps` with a fixed max of 3.
Rejected Alternatives: Leaving the binary hardware bucket; increasing shader max steps without profiler proof; adding physical volumetric raymarching; touching general ocean/Crest systems.
Scalability potential: Low = lower shaft render scale and 1-step contact/flashlight shadow sampling. Middle = smooth intermediate budgets. High = fuller 3-step contact and up to 5-step flashlight SDF. Ultra = higher half-res target scale when quality/headroom allows.
Hardware Impact: i3/MX350 avoids dead 16/24-step illusion and performs fewer real shader samples; estimated frame saving is ~20-42 us when underwater noir shafts/contact shadows are visible.

## Decision 015 - Water Optics Editor Proof Ownership

Problem: WaterOptics runtime now owns `Dump_13KRA.bin`, but `PostProcess_Fog_Scanner`, owner installer, and renderer feature installer still wrote `SHINOBU_265` in report sections, telemetry strings, and failure prefixes.
Solution: Normalize editor proof strings to `13KRA` and scanner section `agent_13kra_water_optics`.
Rejected Alternatives: Leaving editor-only drift; rewriting installer behavior; mutating scenes or renderer assets.
Scalability potential: Runtime Low/Middle/High/Ultra unchanged; only proof attribution changes.
Hardware Impact: 0 us runtime cost. Audit impact is removing false owner routing from water-optics proof outputs.

## Decision 016 - Noir Depth Fog Surface Fade and Quality Contract

Problem: `HectonNoirDepthFogFeature` hard-bypassed the whole pass inside the shallow readability band and did not send `GlobalQualityWeight` to `Hecton_NoirDepthFog.shader`. Result: threshold pop at the waterline/melkovodie edge and no continuous quality contract for fog density/dither.
Solution: Replace the hard shallow bypass with a continuous `ResolveSurfaceFogWeight01` scalar. Keep a true zero only for non-submerged/no-fog cases, then feed surface weight and `HomeostasisBrain.GlobalQualityWeight` through the 64-byte CBuffer. The shader scales density and dither with smooth curves.
Rejected Alternatives: Keeping binary pass skip at `CurrentDepth <= safeDepth`; moving waterline authority into ocean/Crest; adding physical extinction simulation; adding a second render pass for shallow fog.
Scalability potential: Low = surface weight can collapse fog to zero and lower dither amplitude. Middle = smooth shallow-to-abyss transition. High = stronger density/dither curve. Ultra = same cheap full-screen fake can layer with WaterOptics and marine-snow density without new truth ownership.
Hardware Impact: 0 us saved versus the old full skip; this deliberately spends the existing simple fullscreen pass only when fog weight is visible. Avoids visual pop without compute or extra allocations.

## Decision 017 - Volumetric Fog Dear Lie Proxy Must Survive Low Compute Tier

Problem: `HectonVolumetricParticulateFogFeature` had a low-tier raster Dear Lie proxy, but `AddRenderPasses` returned before enqueue whenever `AllowHighResourceComputeShaders` was false or the compute shader was absent. Weak devices lost fog instead of receiving the cheap proxy.
Solution: Split compute admission from feature admission. High-resource devices can still bind compute kernels. Low/no-compute devices force `proxyOnly`; if compute setup fails on a high device, the pass retries as raster proxy. Dump route now uses `Docs/AgentLogs/Dump_13KRA.bin`.
Rejected Alternatives: Forcing compute on MX350/mobile; leaving blank fog on low tier; rewriting the volumetric route; touching ocean/Crest surface systems.
Scalability potential: Low = Dear Lie raster proxy only. Middle = proxy-heavy blend with small internal scale. High = compute frustum grid/raymarch when kernels are valid. Ultra = higher ray steps, larger internal scale, and point-light contribution under existing quality math.
Hardware Impact: Estimated 350-900 us avoided versus forcing compute volumetric fog on weak hardware. Compared with the previous blank gate, this is an intentional visual spend for underwater atmosphere, not a frame-time saving.

## Decision 018 - Abyssal Caustics Public Reads Must Use ReadOnly Vault Views

Problem: `AbyssalDeferredCausticsRuntime.TryGetActiveParameters` and `TryGetTuning` are public read accessors, but they resolved mutable Vault views through `TryResolveVaultBuffer`. `RefreshExternalInputHandle` also validated the ocean swell input using mutable resolve even though caustics only reads that external lane.
Solution: Add `TryReadOnlyVaultBuffer` using `IDataVault.TryReadOnlyHandle`. Convert public caustics readbacks and external input handle validation to read-only views. Keep mutable `TryResolveVaultBuffer` only for owner-write/internal update paths.
Rejected Alternatives: Treating mutable phase views as harmless because the current code only copied values; broad rewrite of caustics buffer ownership; renaming legacy `Shinobu*` BufferIDs.
Scalability potential: Low/Middle/High/Ultra visual budgets unchanged; the fix hardens ownership and relocation safety for every quality tier.
Hardware Impact: 0 us frame-time gain. The value is correctness: read accessors no longer expose writable aliases, reducing DataVault compaction/ownership risk.

## Decision 019 - WaterOptics Public Readbacks Must Avoid Legacy Mutable TryReadHandle

Problem: `WaterOpticsRuntime.TryReadLatestParams`, `TryReadLatestTuning`, and telemetry readbacks used `TryReadHandle`, which the `IDataVault` interface labels as a legacy mutable view. These are public read accessors and must be pure.
Solution: Replace public readback plumbing with `TryReadOnly`, which calls `IDataVault.TryReadOnlyHandle`. Copy DTOs by value from `NativeArray<T>.ReadOnly` instead of passing mutable arrays into unsafe read helpers.
Rejected Alternatives: Keeping `TryReadHandle` because it does not allocate; changing write/editor profile paths; introducing managed copies or allocations.
Scalability potential: Low/Middle/High/Ultra optics behavior unchanged; proof route becomes pure and relocation-safe for all tiers.
Hardware Impact: 0 us direct frame-time gain. Removes writable alias exposure from public diagnostic/editor readbacks without adding allocations.

## Decision 020 - God-Ray RenderGraph Targets Must Not Allocate In RecordRenderGraph

Problem: `VolumetricLightFeature.RecordRenderGraph` resized persistent `RTHandle` half/full targets with `RTHandles.Alloc` when DRS or camera dimensions changed. The comments called it cold, but the code lived in the render record phase.
Solution: Replace persistent RTHandles with transient RenderGraph textures created from explicit `TextureDesc` objects, and resolve the god-ray render scale through continuous `HomeostasisBrain.GlobalQualityWeight`.
Rejected Alternatives: Keeping the RTHandle cache because resizing is rare; adding a new physical volumetric-light system; changing ocean/Crest lighting authority.
Scalability potential: Low = lower transient half-res target and minimum step budget. Middle = authored half-res scale. High = larger transient target and more raymarch steps. Ultra = visual overkill through existing compute shader budgets without allocator churn.
Hardware Impact: i3/MX350 avoids resize-frame RTHandle release/alloc spikes under DRS/camera changes; estimated 120-600 us avoided on those resize frames.

## Decision 021 - Biolum SSGI Needs A Raster Proxy Below Compute Tier

Problem: `HectonBiolumSSGIFeature` had the same hot RTHandle allocation pattern, static sample/intensity budgets, and a hard `AllowHighResourceComputeShaders` return that made weak devices lose biolum bounce completely.
Solution: Use transient RenderGraph gather/GI textures for the compute route, scale render resolution/sample count/intensity with continuous `GlobalQualityWeight`, and add a second fullscreen `ProxyComposite` shader pass that approximates emission bleed from source color/depth when compute is missing or disallowed.
Rejected Alternatives: Forcing compute on MX350/mobile; accepting blank biolum on low tier; simulating real GI or changing biota ownership.
Scalability potential: Low = fullscreen emission-threshold proxy only. Middle = lower-sample compute gather. High = authored compute SSGI. Ultra = higher authored sample/scale/intensity via continuous quality, still as a screen-space fake.
Hardware Impact: i3/MX350 avoids old DRS resize allocation spikes (~100-520 us on resize frames) and uses the raster proxy instead of heavy compute (roughly 250-700 us avoided versus forcing compute), while restoring underwater glow that the old gate removed.

## Decision 022 - DRS Survival Must Be A Continuous Presentation Budget

Problem: `HectonHalfResParticlesFeature`, `HectonAbyssalSsdoFeature`, and `HectonScooterVolumetricShaftsFeature` used DRS survival scale as a binary feature cull. Under heavy pressure the water retained gameplay truth but lost the depth-fog/particle/occlusion/shaft language that sells beauty and fear.
Solution: Keep `HectonDrsRenderFeatureGate.ShouldCullForSurvivalScale` only as a legacy compatibility wrapper, add continuous `ResolveSurvivalPressure01` and `ResolveSurvivalVisualWeight01`, and make each affected underwater presentation feature scale render resolution, radius, composite strength, and sample budgets from that continuous value.
Rejected Alternatives: Leaving binary culls; forcing full-budget passes at survival scale; adding physical volumetric simulation; touching ocean/Crest or gameplay authority.
Scalability potential: Low = minimum render scale, smaller SSDO radius, weak composite strength, and one-step shaft/contact lies. Middle = smooth intermediate cost. High = authored budgets. Ultra = overkill shaft/contact/SSDO presentation when global quality and DRS headroom allow.
Hardware Impact: i3/MX350 avoids full-budget underwater presentation during survival pressure; estimated 35-110 us saved on pressured frames versus full-budget rendering while preserving readable cheap depth atmosphere instead of blanking the effect.

## Decision 023 - God Rays Need A Low-Tier Dear Lie Proxy

Problem: `VolumetricLightFeature` no longer allocated persistent RTHandles, but it still refused to enqueue unless high-resource compute shaders were allowed. Weak devices therefore lost god rays entirely instead of getting a cheap depth/stripe proxy.
Solution: Add a raster fullscreen `Hidden/Hecton8/VolumetricLightProxy` shader, route it through `RuntimeShaderReferenceCatalog`, and make `VolumetricLightFeature` choose compute only when the compute shader exists and high-resource compute is allowed. Low/no-compute and failed-kernel paths now composite a cheap depth-aware shaft stripe.
Rejected Alternatives: Forcing compute god rays on MX350/mobile; accepting blank god rays; using a physical participating-media solve; changing ocean/Crest lighting authority.
Scalability potential: Low = fullscreen depth/triangle-wave shaft proxy. Middle = proxy or reduced compute depending on device. High = transient RenderGraph compute raymarch. Ultra = authored compute step/render-scale overkill through continuous quality.
Hardware Impact: i3/MX350 avoids estimated 250-700 us versus forced compute volumetrics while restoring visible god-ray language compared with the previous blank gate.

## Decision 024 - Dormant Voxel SSAO Must Not Enqueue Or Own Persistent Targets

Problem: `HectonVoxelSsaoFeature` explicitly has no runtime consumer, but `AddRenderPasses` still set up and enqueued the pass. The inactive RenderGraph branch also carried a latent persistent `_aoTexture`/`RTHandles.Alloc` path that would churn under DRS/camera resize if a future consumer toggled it on without a route card.
Solution: Expose `HasRuntimeConsumerAvailable`, guard `AddRenderPasses` before setup/enqueue, and replace the latent persistent AO target with a transient RenderGraph texture created from `aoDesc`.
Rejected Alternatives: Publishing a global `_HectonVoxelSSAOTex` without a consumer; keeping persistent RTHandle ownership because the branch is currently inactive; touching voxel world/Crest/ocean authority.
Scalability potential: Low = zero cost because no consumer exists. Middle = future consumer must declare a route and can use transient half-res AO. High = higher AO sample budgets remain possible behind continuous quality. Ultra = overkill voxel occlusion only after a real consumer contract exists.
Hardware Impact: i3/MX350 avoids roughly 8-20 us per camera from dead pass setup now. If the pass is re-enabled later, transient graph allocation avoids estimated 80-260 us resize-frame RTHandle churn.

## Decision 025 - Uber Noir Readbacks Must Use ReadOnly Vault Views

Problem: `HectonVisorUberPostFeature.Noir` and the reconstruction half used legacy mutable `TryReadHandle` for public/editor readbacks and telemetry reads. That violates the read-accessor purity doctrine and exposes writable aliases during diagnostic reads. Their dump filenames also bypassed the `Dump_13KRA.bin` proof route.
Solution: Split read/write helper semantics: owner-write paths keep `TryResolveHandle`, while read helpers return `NativeArray<T>.ReadOnly` via `TryReadOnlyHandle`. Normalize Noir and Reconstruction dump filenames to `Dump_13KRA.bin`.
Rejected Alternatives: Keeping legacy mutable reads because callers only index-copy values; adding managed DTO copies or allocations; editing broader visor HUD, damage, or ocean/Crest systems.
Scalability potential: Low/Middle/High/Ultra visual behavior is unchanged; all tiers now obey one safe read route, and high-end visual overkill does not depend on mutable diagnostic aliases.
Hardware Impact: 0 us direct frame saving. The gain is relocation safety and proof ownership: i3/MX350 avoids rare compaction/debug alias faults without adding allocations.

## Decision 026 - Lighting Relay Readbacks Must Not Use Mutable Legacy Handles

Problem: After the Noir pass, a broader 13KRA sweep still found `TryReadHandle` in Lighting-owned GI and Day/Night relay read paths. `TryReadCelestialState`, environment lighting copy, relay telemetry readback, and tuning copies were pure reads but received mutable `NativeArray<T>` views.
Solution: Convert those read paths to `NativeArray<T>.ReadOnly` via `TryReadOnlyHandle`. Keep `OpenDayNightRelayArray` and `TryOpenGIRelayBuffer` mutable because they are owner-write routes for tuning initialization, profile baking, telemetry cursor writes, and environment upload state.
Rejected Alternatives: Blindly converting write helpers to read-only and breaking owner writes; editing adjacent Visor AR stencil/dynamic decal files that are outside 13KRA; leaving Lighting readbacks mutable because they are local.
Scalability potential: Low/Middle/High/Ultra visual output is unchanged; the lighting state path is now relocation-safe for every quality tier.
Hardware Impact: 0 us direct frame saving. The correctness gain is preventing readback writable aliases during DataVault compaction windows on low-end hardware without adding managed copies.

## Decision 027 - Volumetric Fog Render Admission Must Not Repair Allocated State

Problem: `HectonVolumetricParticulateFogFeature.AddRenderPasses` called `RunColdMaintenanceIfDue`, but that function could run `TryPrepareNativeState` and `TryPrepareGpuState`. Those helpers allocate Vault buffers, fallback textures, materials, `GraphicsBuffer`s, and `RTHandle`s. A 30-frame cadence does not make an allocation path cold if it executes from render admission.
Solution: Add explicit `allowAllocation` to native/GPU prepare helpers and texture-handle resolution. `Create()` is the cold allocation owner and passes true. `AddRenderPasses` now runs diagnostic maintenance only and refreshes bridge globals with `allowExternalTextureHandleAllocation: false`.
Rejected Alternatives: Accepting cadence-based repair allocations; allocating external MarineSnow/AbyssalFlow RTHandles when shader globals change during render admission; forcing compute fog or editing ocean/Crest bridge producers.
Scalability potential: Low = fail closed to existing fallback/proxy state with no allocator spikes. Middle = stable proxy/compute admission using prebuilt resources. High = compute raymarch still uses transient RenderGraph textures. Ultra = visual overkill stays in quality-scaled ray steps/grid size, not repair allocation.
Hardware Impact: i3/MX350 avoids estimated 200-900 us spikes when missing fog resources were repaired from render admission and avoids unbounded RTHandle churn from changing external bridge textures.

## Decision 028 - Verification Noise Must Stay Outside 13KRA Scope

Problem: A global `git diff --check` now reports trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md`, and an external `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` is already running with MSBuild child nodes.
Solution: Treat the batch-file whitespace as unrelated workspace noise and keep the proof boundary on targeted 13KRA files. Do not launch or stop compilation while the external build is active.
Rejected Alternatives: Editing `CURRENT_BATCH.md` outside the domain just to make global whitespace clean; killing a build that was not started by 13KRA; claiming compile proof while compiler processes are active.
Scalability potential: Low/Middle/High/Ultra runtime visuals unchanged. The value is process safety under 20+ agent concurrency.
Hardware Impact: 0 us runtime. Avoids destructive verification interference and prevents parallel build contention on the local machine.

## Decision 029 - Bilateral DRS Repair Must Not Allocate From Hot Phases

Problem: `HectonBilateralDrsUpscalerRuntime` belongs to the graphics quality-scaling route and could retry service preparation from `PreSimulation` and `VisualSync`. That path allocated DataVault lanes, CSV scratch, dump directories, dispatcher bridge objects, and double `GraphicsBuffer` constants when state was missing or invalid.
Solution: Add explicit `allowAllocation` to service preparation, Vault acquisition, CSV scratch acquisition, and constant-buffer acquisition. Cold routes (`OnEnable`, editor tuning/profile writes, DataVault replacement) pass true. Hot owner phases pass false and only resolve already-prepared state.
Rejected Alternatives: Accepting hot self-repair because DRS is visual; moving quality scaling into ocean/Crest; leaving old `SHINOBU_236` proof ownership; adding a CPU/raster upscaler without profiler proof.
Scalability potential: Low = DRS fails closed to existing raster-cleared edge-mask proof instead of allocating. Middle = prepared compute path runs with stable buffers. High = bilateral edge mask/upscale spends compute only when quality and hardware allow. Ultra = stronger reconstruction remains compute-backed without changing truth ownership.
Hardware Impact: i3/MX350 avoids estimated 80-430 us repair spikes on invalid DRS runtime state and avoids GPU constant-buffer reallocation from VisualSync.

## Decision 030 - UberNoir Runtime Telemetry Ring Must Not Allocate From Late Frame

Problem: `HectonUberNoirRuntimeBridge.PushBlackBox` and fault `DumpBlackBox` called telemetry setup without an allocation mode. If `ShaderFeatureTelemetryRing` was missing, late-frame shader-feature publication could allocate a DataVault buffer. Fault dumps also wrote four historical subsystem files instead of the mandated 13KRA proof artifact.
Solution: Change telemetry setup to `EnsureTelemetryBuffer(bool allowAllocation)`. Cold routes (`Awake`, `OnEnable`, DataVault replacement) pass true; late-frame telemetry writes and fault dump snapshot pass false and fail to an empty 13KRA dump when the ring is unavailable. Collapse dump output to `Docs/AgentLogs/Dump_13KRA.bin`.
Rejected Alternatives: Lazy hot allocation because the buffer is small; keeping split integrator/extinction dump files; writing `.h8dump` duplicates; editing broader visor HUD or ocean/Crest systems.
Scalability potential: Low = no late-frame allocator repair, feature globals still degrade through existing stress/quality scalars. Middle = stable 300-frame telemetry ring after cold setup. High = more shader features allowed through existing high-cost/visual-overkill weights. Ultra = maximal noir/refraction/POM feature mask remains a shader-side presentation choice, not a new truth owner.
Hardware Impact: i3/MX350 avoids estimated 20-80 us allocator spikes when telemetry is missing or invalid and removes three extra fault-time file writes from crash capture. Steady-state frame cost is unchanged.

## Decision 031 - Global Shader Dispatch Must Be Resolve-Only In LateFrame

Problem: `GlobalShaderDispatcher.LateFrameTick` called `EnsureCommandBuffer()` and `EnsureShaderGlobalSlotsRuntime()`. If cold setup missed or DataVault was replaced, the frame-wide noir shader bridge could allocate a `CommandBuffer` or `ShaderGlobalState` from late-frame dispatch. `RecordTelemetry` and `DumpTelemetry` also reused that allocator route, editor readbacks used mutable slot views, and dump files still used old CBUFFER names or skipped output when Vault was unavailable.
Solution: Add explicit allocation mode to command-buffer and shader-slot preparation. Cold lifecycle (`Awake`, `OnEnable`, DataVault replacement) and editor write routes pass true. Late-frame dispatch, telemetry record, and fault dump pass false. Editor reads now use `TryReadOnlyHandle` via `TryReadCachedShaderGlobalSlots`. Dump output is normalized to `Docs/AgentLogs/Dump_13KRA.bin`; if Vault is unavailable, dump writes a zeroed 300-entry snapshot with `TelemetryFlagVaultUnavailable`.
Rejected Alternatives: Keeping hot self-repair because shader globals are central; relying on `vault.IsAllocationLocked` after entering the allocation helper; keeping mutable read views for editor convenience; editing Ocean/Crest shader producers.
Scalability potential: Low = shader globals fail closed to cached fallback and no allocator spike. Middle = stable preallocated global slots. High = richer fog/caustic/noir globals through existing continuous quality fields. Ultra = visual overkill remains a command-buffer/global-shader publish, not a new runtime truth owner.
Hardware Impact: i3/MX350 avoids estimated 35-140 us allocator spikes on missing-slot or missing-command-buffer frames and removes one duplicate fault-time dump write. Steady-state frame cost is unchanged.

## Decision 032 - Shader Global Fallback Must Wake After Dispatcher Failure

Problem: After a successful `GlobalShaderDispatcher.ExecuteGlobalDispatch`, `_visualSyncDispatcherActive` stayed true. If a later `LateFrameTick` returned early before command-buffer execution because command-buffer, layout, Vault, or slot-lock resolve failed, `HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync` still skipped fallback publication. Published fallback values were updated in memory, but not marked dirty while the dispatcher was considered active.
Solution: `GlobalShaderDispatcher.LateFrameTick` now marks the dispatcher inactive before any no-allocation resolve path. `ExecuteGlobalDispatch` marks it active only after `Graphics.ExecuteCommandBuffer`. `HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(false)` marks fallback globals dirty, so the next dispatcher visual-sync fallback flush publishes the latest cheap shader-state lie without allocating.
Rejected Alternatives: Setting every early return by hand; relying on the next producer publish to mark fallback dirty; making the fallback publish immediately from the failure path; editing Ocean/Crest producers.
Scalability potential: Low = stale fog/caustic/noir globals recover through one fallback vector upload path. Middle = stable shader globals while the dispatcher recovers. High = richer DataVault-backed dispatch resumes on the next successful command-buffer frame. Ultra = command-buffer visual overkill remains active when healthy, fallback carries believable presentation when unhealthy.
Hardware Impact: i3/MX350 avoids multi-frame stale underwater presentation after a resolve failure without adding allocation or extra shader uploads in the healthy steady state. Estimated saved artifact window is unbounded in bad Vault/command-buffer states; direct CPU cost is one static bool state update per late-frame attempt.

## Decision 033 - WaterOptics Marker Pass Must Not Ship In Production

Problem: `HectonWaterOpticsTelemetryFeature` defaulted `enableCommandBufferMarker=true`. The feature enqueued a RenderGraph raster pass that only executed `BeginSample/EndSample`, forced `AllowPassCulling(false)`, and attached the active color target as read/write. This is not player-visible water optics, not black-box telemetry, and not a visual fake; it is a permanent production marker pass.
Solution: Make the marker opt-in false by default and route both `AddRenderPasses` and `RecordRenderGraph` through `IsTelemetryMarkerAllowed`. The helper returns the serialized toggle only in `UNITY_EDITOR || DEVELOPMENT_BUILD`; release/player builds return false even if a renderer asset still has the old serialized true.
Rejected Alternatives: Keeping the marker because it is "just a sample"; deleting the feature and breaking diagnostic workflows; adding another runtime telemetry system; touching ocean/Crest water ownership.
Scalability potential: Low = no marker pass and no attachment barrier. Middle = same production behavior. High/Ultra = diagnostic marker can be enabled in development builds while real visual overkill remains in shader/fog/caustic systems, not profiler scaffolding.
Hardware Impact: i3/MX350 avoids an estimated 5-25 us per eligible camera plus one RenderGraph read/write attachment dependency. Steady-state visuals are unchanged because the pass had no visual output.

## Decision 034 - WaterOptics Installer Must Not Re-Enable Dev Marker

Problem: After the runtime marker was made dev-only/off-by-default, `WaterOpticsRendererFeatureInstaller` still repaired renderer feature settings to `enableCommandBufferMarker=true` and build verification required that true value. This kept bad serialized intent alive and would reintroduce marker-pass cost during editor/development captures.
Solution: Make installer repair write `enableCommandBufferMarker=false` and make build verification require `!markerProperty.boolValue`. Keep the feature subasset/reference active so explicit diagnostics can still exist, but default-authored renderer state no longer asks for a non-visual marker pass.
Rejected Alternatives: Trusting release preprocessor gating while editor/build tools keep turning the marker back on; deleting the installer and losing renderer-reference validation; editing renderer assets by hand without code guard coverage; touching Ocean/Crest water ownership.
Scalability potential: Low = no marker pass or color attachment dependency. Middle = same default no-pass behavior. High = developer can intentionally enable the marker in a development build for profiling. Ultra = visual overkill remains in actual water/fog/caustic shader routes, not diagnostic scaffolding.
Hardware Impact: Preserves the 5-25 us per eligible camera avoided by Decision 033 and prevents future installer repair from spending that budget on i3/MX350 development captures.

## Decision 035 - UberPost Internal Waterline Must Fade, Not Pop

Problem: `HectonVisorUberPostFeature.ResolveInternalWaterlineParams` used `cameraY < waterlineY - 0.03f` to jump the split line to a full-screen internal water mask and to publish `submerged01=1`. Near shallow/flood transitions this creates a binary postprocess pop inside the 13KRA underwater/noir presentation domain.
Solution: Add a cheap continuous `ResolveInternalWaterlineSubmergedWeight01` using fixed meter fade constants and `Smooth01`. The viewport split is lerped toward `InternalWaterlineFullScreenSplit` by that weight; full-screen shortcut remains only once the weight is effectively saturated.
Rejected Alternatives: Keeping the 3 cm hard threshold; moving waterline ownership to Ocean/Crest; adding a second post pass or physical water interface simulation; editing Visor AR/HUD systems outside the noir postprocess.
Scalability potential: Low = one scalar smoothstep and one lerp, no extra texture/pass. Middle = smoother shallow/flood transition. High = stronger authored internal water distortion remains available. Ultra = full-screen mask still saturates when deeply submerged while higher visual budgets are spent elsewhere.
Hardware Impact: 0 us saved; added scalar math is below measurement noise on i3/MX350. The value is removing a visible binary transition without adding allocations, branches in shader, or new authority routes.

## Decision 036 - Volumetric Fog Proxy Must Not Require Compute-Class Shader Target

Problem: `Hecton_VolumetricFog_DearLie.shader` is the low-tier/no-compute raster fallback for particulate fog, but both passes compiled with `#pragma target 4.5`. Unity documentation maps 4.5 to ES3.1/SM5-class capabilities including compute/random-write feature level, so this contradicted the proxy's job as the broadest cheap fallback.
Solution: Lower both fullscreen raster passes to `#pragma target 3.5`, matching existing low-tier proxy shaders in the project and the shader's actual requirements: depth/color sampling, constant buffers, integer vertex id, and one color output. Also remove stale `SHINOBU_233` owner comments from the paired feature file and add static test coverage.
Rejected Alternatives: Keeping target 4.5 because the high-tier path uses compute; using target 2.5/3.0 and risking feature loss around explicit LOD/integer/stereo macros; editing Ocean/Crest water systems; adding another fallback material.
Scalability potential: Low = the Dear Lie proxy can compile on weaker ES3.0/Metal/Vulkan-class devices instead of disappearing behind a compute-class target. Middle = same proxy with richer settings. High = compute path remains available when kernels and hardware allow. Ultra = raymarch/grid overkill remains gated separately by continuous quality.
Hardware Impact: 0 us frame-time saving. The gain is platform reach and fallback reliability on i3/MX350/mobile-class devices; avoided failure mode is a blank fog fallback, not a measurable hot-path cost.

## Decision 037 - Deferred Caustics Proxy Must Not Require Compute-Class Shader Target

Problem: `Hecton_DeferredCaustics.shader` is a fullscreen caustic presentation lie, but it was compiled with `#pragma target 4.5`. The shader has no `StructuredBuffer`, no RW target, no compute kernel, and no random-write dependency; 4.5 only narrows platform reach for the shallow/depth beauty layer.
Solution: Lower the shader to `#pragma target 3.5` and add static editor coverage that rejects target 4.5 plus StructuredBuffer/RW/ByteAddress dependencies in this specific proxy.
Rejected Alternatives: Keeping target 4.5 because the caustics runtime interacts with DataVault/SDF data; using target 2.5/3.0 and risking compatibility issues around fullscreen vertex ID, stereo macros, explicit LOD SDF sampling, and URP includes; editing Ocean/Crest swell authority.
Scalability potential: Low = cheap depth/procedural caustics survive on broad raster hardware. Middle = one-layer caustics with lower quality weight. High = second layer/chroma/SDF occlusion already scales through `GlobalQualityWeight`. Ultra = denser procedural/chromatic visual overkill stays available without changing gameplay truth.
Hardware Impact: 0 us frame-time saving. The gain is platform reach and fallback reliability on i3/MX350/mobile-class GPUs; avoided failure mode is losing the caustic beauty layer due an unnecessary SM5-class shader requirement.

## Decision 038 - Abyssal Caustics Proof Strings Must Match Current Owner

Problem: Caustics runtime proof now writes `Dump_13KRA.bin`, but `AbyssalCausticsContracts.cs` and `AbyssalCausticsLayoutAudit.cs` still named `SHINOBU_232` / `SHINOBU-owned`. That makes evidence ownership inconsistent in the caustics lane.
Solution: Normalize caustics contract comments and editor layout audit messages to 13KRA, and add static guard coverage that rejects the stale owner tokens.
Rejected Alternatives: Leaving stale owner strings because they are comments/editor logs; renaming BufferIDs or DTOs; changing caustic runtime ownership again; touching Ocean/Crest input producers.
Scalability potential: Low/Middle/High/Ultra visuals unchanged; proof ownership is now deterministic across all tiers.
Hardware Impact: 0 us runtime. The value is integration correctness: crash/layout evidence points to the active 13KRA owner instead of a historical agent.

## Decision 039 - DRS Survival Gate Must Not Expose A Binary Cull API

Problem: `HectonDrsRenderFeatureGate` had already been converted to continuous `ResolveSurvivalPressure01` / `ResolveSurvivalVisualWeight01`, but the unused `ShouldCullForSurvivalScale()` helper remained and returned a hard cull at saturated survival pressure.
Solution: Remove the binary helper and update static coverage to reject the hard-cull symbol and `>= 0.999f` threshold in the gate. Existing underwater presentation consumers continue to use continuous pressure/visual-weight methods.
Rejected Alternatives: Keeping the helper because no current consumer called it; changing consumer render behavior again; deleting the shared gate entirely; touching Ocean/Crest resolution ownership.
Scalability potential: Low = fog/particles/shafts degrade by scalar weight instead of disappearing. Middle = same pressure curve with moderate feature density. High = richer visuals when DRS pressure falls. Ultra = visual overkill remains available through existing quality and survival weights.
Hardware Impact: 0 us immediate runtime saving because the helper was unused. The gain is architectural: future low-end survival frames cannot reintroduce blank underwater presentation through this shared gate.

## Decision 040 - Bilateral DRS Proof Route Belongs Outside 13KRA

Problem: A self-review after the dump-path patch found a live 1335 status/rationale entry explicitly claiming the Bilateral DRS/upscaler route and `Docs/AgentLogs/Dump_1335_BilateralDrs.bin`. For 13KRA, the safe domain is underwater lighting/fog/caustic/god-ray presentation and quality behavior of those visuals, not taking ownership of another agent's DRS black-box artifact.
Solution: Revert the 13KRA dump-path claim, remove the 13KRA static assertion over the Bilateral DRS dump filename, and keep only the stale `SHINOBU_236` guard that protects against historical proof drift.
Rejected Alternatives: Overriding `Dump_1335_BilateralDrs.bin` with `Dump_13KRA.bin`; leaving a failing 13KRA test that contradicts the active subsystem owner; editing broader DRS authority routes.
Scalability potential: Low/Middle/High/Ultra DRS visual behavior unchanged. 13KRA retains control over underwater presentation degradation and avoids stealing the core upscaler proof route.
Hardware Impact: 0 us runtime. The gain is integration safety under 20+ agents: no proof artifact tug-of-war, no hot-path code changed.

## Decision 041 - Abyssal Lighting Editor Diagnostics Must Not Use Historical Owner Tags

Problem: `AbyssalLightingTunerWindow.ScanLoadedScenesForUnityProbeGroups()` reported loaded-scene Unity probe group count with `[SHINOBU_131]`. This is editor-only, but it is still proof/diagnostic output in the 13KRA abyssal lighting domain.
Solution: Change the diagnostic tag to `[13KRA]` and add editor static coverage rejecting `[SHINOBU_131]`.
Rejected Alternatives: Leaving editor-only false attribution; deleting the diagnostic; changing probe-group scan behavior.
Scalability potential: Low/Middle/High/Ultra runtime visuals unchanged. Editor proof now routes to the active lighting owner while runtime visual scaling remains in existing systems.
Hardware Impact: 0 us runtime. The only cost is editor-only string text change.

## Decision 042 - Dynamic Point-Light Contract Proof Text Must Match Current Owner

Problem: `DynamicPointLightCullingContracts.cs` still described Vault IDs and the 32-byte culling result layout as owned or assigned to `SHINOBU_151`, even though the active lighting proof route is 13KRA.
Solution: Normalize comments to 13KRA and add static editor coverage rejecting `SHINOBU_151`. Keep all legacy numeric `BufferID` values and explicit DTO field offsets unchanged.
Rejected Alternatives: Renaming `BufferID` values; changing DTO layout; leaving stale comments because they have no frame-time cost.
Scalability potential: Low/Middle/High/Ultra visuals unchanged. The culling lane keeps the same continuous quality math and proof attribution now matches the current lighting owner.
Hardware Impact: 0 us runtime. The gain is integration correctness: contract proof no longer points investigations at a historical owner.

## Decision 043 - GI Storage Acquisition Must Fail Closed

Problem: `HectonGIRelaySystem.AcquireBuffer` threw `InvalidOperationException` when DataVault allocation/resolve failed, and `InteriorGIProbeVolumeRuntime` could throw the same way when Vault was unavailable or returned an invalid native buffer. These are 13KRA lighting/GI presentation systems; missing storage must degrade underwater lighting, not abort runtime/editor flows.
Solution: Convert acquisition failures to default handles/arrays and require explicit readiness gates before initialization writes. `HectonGIRelaySystem.EnsureNativeStorage()` now validates `HasRequiredGIRelayStorage()` and `EnsureDayNightRelayNativeStorage()` before building SH profiles or setting `_nativeStorageReady`; day/night storage validates all relay buffers before tuning/profile initialization. Interior GI acquisition and resolve now return default when Vault is missing or invalid, letting `HasRequiredNativeBuffers()` and callers fail closed.
Rejected Alternatives: Keeping exceptions as integrity proof; swallowing exceptions with `try/catch`; allocating backup managed arrays; editing Ocean/Crest or broader adaptation ownership; changing BufferIDs/DTO layout.
Scalability potential: Low = missing Vault storage leaves GI/interior probes dark/fallback instead of crashing weak devices. Middle = cold storage initializes normally and preserves continuous quality/cadence scaling. High = day/night profiles and ambient relay run with richer visual weights once storage is valid. Ultra = visual overkill remains in existing SH/profile/fog paths without new gameplay truth owners.
Hardware Impact: i3/MX350 avoids estimated 35-140 us exception construction/unwind on faulted storage frames and, more importantly, avoids runtime abort during locked/invalid Vault windows. Steady-state frame cost is unchanged; added checks are cold setup/readiness only.
