# TECH_SCOUT Rationale

Status: PENDING VERIFICATION

## Assignment Binding

Problem: The AGENTS workflow requires extraction from a batch XML prompt, but `Docs/Tasks/CURRENT_BATCH.txt` is empty and no XML agent block exists on disk.
Solution: Bind to the chat master prompt as `TECH_SCOUT` and record the lack of batch source in status.
Rejected Alternatives: Guessing another agent's status file was rejected because `Status_HECTON-8.md` is already bound to Deterministic Replay.
Scalability potential: Process-only. Low/Middle/High/Ultra unaffected.
Hardware Impact: No runtime impact.

## GPU Resident Drawer

Problem: The old rendering law treated BRG/manual culling as the default for environment instances, but Unity 6000 URP provides GPU Resident Drawer for compatible MeshRenderer GameObjects.
Solution: Add `REND_GPU_Sovereignty` and require GPU Resident Drawer for compatible static/instanced environment families.
Rejected Alternatives: A blanket "100 percent environment" claim was rejected because skinned/deforming/procedural/non-GameObject families are not the same ownership domain.
Scalability potential: Low uses GRD plus GPU occlusion for cheap CPU submission. High/Ultra can spend saved CPU on denser scene dressing and better lighting.
Hardware Impact: Expected CPU render-submission reduction on i3/MX350; exact microseconds are PENDING MEASUREMENT.

## Burst SIMD

Problem: The prompt requested Burst 2.0/v512 intrinsics, but the project resolves Burst 1.8.28 and official Burst 1.8 docs expose v64/v128/v256 only.
Solution: Mandate v128 baseline and v256 only behind explicit AVX/AVX2 support checks; forbid v512 until official package evidence exists in the project.
Rejected Alternatives: Writing v512 helper code was rejected because it would be speculative and likely uncompilable.
Scalability potential: Low remains SSE/v128-safe; High/Ultra can use v256 paths when CPU support is verified.
Hardware Impact: Prevents invalid CPU feature assumptions on cheap i3 targets.

## Awaitable Standard

Problem: `Task.Run` appears in bootstrap/storage code, while Unity 6000 documents `Awaitable` as the preferred Unity async primitive and pools Awaitables.
Solution: Use Awaitable for Unity-facing orchestration and persistent worker ownership for MMF paging; ban per-request Task storms.
Rejected Alternatives: Replacing all background work with Awaitable was rejected because MMF paging still needs long-lived worker lifecycle and must not touch Unity API off-thread.
Scalability potential: Low reduces allocation/context-switch risk; High/Ultra can increase prefetch depth without changing async ownership.
Hardware Impact: Expected GC and tail-latency reduction; exact numbers PENDING BENCHMARK.

## RenderGraph

Problem: URP Compatibility Mode is legacy and Unity docs state new graphics features should use RenderGraph, but static scan still finds runtime legacy blit/command-buffer paths.
Solution: Update the rendering mandate to require `RecordRenderGraph` and mark `Graphics.Blit`, `ScriptableRenderPass.Execute`, and `Camera.AddCommandBuffer` as debt unless behind documented legacy quarantine.
Rejected Alternatives: Silent conversion of UI/visor code was rejected because those files are actively modified and need renderer owner validation.
Scalability potential: Low benefits from RenderGraph pass merging and transient resource optimization. High/Ultra can stack effects only through graph-visible resources.
Hardware Impact: Expected lower barrier/copy overhead; exact microseconds PENDING MEASUREMENT.

## AI Compute Boundary

Problem: Moving Eco-Director 100 percent to compute would force CPU readback for gameplay authority or make GPU state authoritative in a non-deterministic path.
Solution: Move only dense visual/scalar fields to compute; keep FrostTick CPU authority for deterministic biomass, encounter, and save-state decisions.
Rejected Alternatives: GPU-only Eco-Director was rejected because AsyncGPUReadback latency and platform variance break deterministic gameplay authority.
Scalability potential: Low uses CPU FrostTick and cheap GPU visualization; High/Ultra can increase compute grid resolution and visual overkill.
Hardware Impact: Expected CPU savings only for dense field evaluation; gameplay decision cost remains bounded on FrostTick.

## LZ4 Dictionaries

Problem: The prompt requested pre-trained LZ4 dictionaries and >90 percent compression, but the current save codec binds only baseline `LZ4_compress_default` and `LZ4_decompress_safe`.
Solution: Add a mandate requiring corpus training, explicit dictionary native bindings, and benchmark proof before adoption.
Rejected Alternatives: Claiming LZ4 dictionaries will hit 10:1 was rejected because ratio depends on DTO entropy and LZ4 favors speed over maximal compression.
Scalability potential: Low keeps decode cheap and relies on preconditioning/RLE; High/Ultra may use dictionary mode or a stronger cold-save codec if measured.
Hardware Impact: Potential disk/RAM bandwidth savings are PENDING BENCHMARK; current code cannot use dictionaries.

## Descriptor Binding

Problem: `Graphics.SetDescriptorSet` was requested, but no managed Unity API or project symbol exists for it.
Solution: Add a reality-check mandate: reduce binding overhead through SRP Batcher, GPU Resident Drawer, RenderGraph resources, Texture2DArray, and GraphicsBuffer.
Rejected Alternatives: Designing around a nonexistent C# API was rejected as fake reporting.
Scalability potential: Low uses fewer materials and bindings; High/Ultra can increase material richness through arrays/indirection without new per-object binds.
Hardware Impact: Expected driver overhead reduction only after material/state audit; PENDING MEASUREMENT.

## PROJECT_ATLAS

Problem: Task 8 asked to audit `PROJECT_ATLAS.md`, but no live root atlas file was found. Hits were archived reports.
Solution: Mark the task as BLOCKED/OUTDATED and avoid editing archived audit copies.
Rejected Alternatives: Treating `Docs/2026-04-30.../PROJECT_ATLAS.md` references as current was rejected.
Scalability potential: Process-only.
Hardware Impact: No runtime impact.

## Quantum Logistics

Problem: "Instant long-distance power sync" can become a fake physics requirement or a per-frame simulation sink.
Solution: Define Quantum logistics as deterministic graph-delta replication over existing LogisticsTick/FrostTick cadence.
Rejected Alternatives: Signal propagation/electron simulation was rejected because gameplay reads coarse node state, not physical wavefronts.
Scalability potential: Low stores compact summaries; High/Ultra can add richer visual interpolation without changing authority.
Hardware Impact: Avoids per-frame nonresident logistics work; exact savings PENDING MEASUREMENT.

## Terrain Virtual Texturing

Problem: Virtual Texturing could reduce terrain VRAM, but current static evidence shows MicroSplat/Texture2DArray paths rather than verified Unity SVT.
Solution: Add a terrain virtual-texturing mandate with texture arrays as low-tier default and SVT behind platform/profiler proof.
Rejected Alternatives: Enabling SVT by policy without page-cache measurements was rejected because page misses can hitch.
Scalability potential: Low uses packed arrays/mip bias; High/Ultra can use SVT for hero geology variety after validation.
Hardware Impact: Potential VRAM reduction PENDING CAPTURE.

## Span Zero-GC

Problem: The old zero-GC law covered `char[]` and `.ToString()` but did not explicitly govern `Span<char>`, `ReadOnlySpan<char>`, `string.Create`, or stackalloc limits.
Solution: Add Span law: TryFormat into preallocated buffers, no managed string creation, no escaping spans, bounded stackalloc.
Rejected Alternatives: Treating `string.Create` as zero-GC was rejected because it returns a managed string.
Scalability potential: Low uses fixed char buffers; High/Ultra can use richer UI text without GC if still span-buffered.
Hardware Impact: Prevents GC spikes; exact savings PENDING PROFILER.

## Blue Noise Shadows

Problem: Blue noise can improve fading shadows but can also become shimmer, extra texture cost, or a fake replacement for occlusion.
Solution: Allow blue noise only for alpha shadow fade/contact breakup and low-tier Bayer fallback.
Rejected Alternatives: More PCF taps on MX350 were rejected because the target is fixed tap count with better distribution.
Scalability potential: Low uses Bayer/cheap dither; High/Ultra can use blue noise with temporal sequence after capture.
Hardware Impact: Visual improvement at nearly fixed cost; exact GPU cost PENDING CAPTURE.

## Shader Stutter

Problem: Shader Variant Collections exist in bootstrap, but Linux/Vulkan first-use stutter is not proven solved.
Solution: Add Linux/Vulkan stutter mandate requiring variant stripping, curated SVC warmup, offscreen pre-touch, and player traversal capture.
Rejected Alternatives: Runtime `Shader.WarmupAllShaders` was rejected as broad and gameplay-hostile.
Scalability potential: Low strips variants aggressively; High/Ultra can keep richer variants only if warmup is proven.
Hardware Impact: Expected spike removal; PENDING LINUX/VULKAN PLAYER CAPTURE.

## Arena 2.0

Problem: The prompt asked for a Native Memory Arena, but the project already has arena allocators.
Solution: Add 2.0 governance over existing `HectonArenaAllocator`: high-water telemetry, frame-boundary reset, no escaping spans/pointers.
Rejected Alternatives: Creating another allocator was rejected because it fragments ownership and breaks sentinel accounting.
Scalability potential: Low caps arena and drops cosmetics on overflow; High/Ultra can increase arena cap after memory proof.
Hardware Impact: Prevents transient GC/native churn; exact savings PENDING SENTINEL CAPTURE.

## Haptic Spatialization

Problem: Haptic spatialization can become per-frame transform/audio-rate work.
Solution: Define event-authored local-space motor weights in the fixed HapticCommand queue.
Rejected Alternatives: Per-frame ambient haptic spatial simulation was rejected because it burns frame time for low information value.
Scalability potential: Low dominant-axis split; High adds distance/priority; Ultra adds platform trigger resistance through abstraction.
Hardware Impact: Zero steady-frame cost except queue scan; event transform cost only.

## AUP Circumference Wrapping

Problem: The AUP mandate handled floating origin shifts but did not define safe planetary circumference wrapping.
Solution: Add a fixed-point/int64 circumference-wrap law and require one shared `PlanetCircumferenceMeters` authority before wrap is enabled.
Rejected Alternatives: Float modulo and shader-side wrap were rejected because they break determinism, save/load identity, physics queries, and shader/noise continuity.
Scalability potential: Low hides discontinuity with fog and chunk fade; Middle wraps streaming sectors; High/Ultra prefetch both sides for visual continuity.
Hardware Impact: Prevents seam rework and keeps wrap math off the render hot path. Exact gain PENDING MEASUREMENT.

## DirectStorage Boundary

Problem: DirectStorage can reduce native asset IO overhead, but no Unity-managed DirectStorage API or project wrapper is present and MMF DTO paging is a different path.
Solution: Add a reality-check mandate: DirectStorage is a native-plugin Windows asset-streaming experiment only; MMF paging stays on persistent workers.
Rejected Alternatives: Claiming DirectStorage speeds `MemoryMappedFile` DTO loads was rejected as unsupported by current project evidence.
Scalability potential: Low keeps CPU LZ4/MMF; High/Ultra may test GPU decompression only where spare GPU compute exists.
Hardware Impact: 0 us claimed. Adoption requires Player benchmark on target storage and MX350 render load.

## Physics Determinism

Problem: Multithreaded body solving can introduce nondeterministic ordering and callback dependence.
Solution: Add a deterministic solver mandate using stable entity ID order, fixed timestep, fixed iteration count, ordered reductions, and 300-frame telemetry.
Rejected Alternatives: Treating PhysX worker order or collision callback order as gameplay authority was rejected.
Scalability potential: Low uses primitives and dominant-axis cheats; High/Ultra add presentation detail after deterministic authority completes.
Hardware Impact: Saves debug time and prevents replay divergence; runtime microseconds are PENDING REPLAY BENCHMARK.

## GPU Occlusion Culling

Problem: Unity 6000 GPU occlusion can reduce overdraw/draw work, but it can also cost GPU setup time in sparse scenes.
Solution: Add a Unity 6000 URP GPU occlusion mandate tied to Forward+, GRD, RenderGraph, and scene capture proof.
Rejected Alternatives: Hand-rolled Hi-Z culling before testing Unity's path was rejected for MeshRenderer environment sets.
Scalability potential: Low enables only dense occluded zones; High/Ultra buy more environment density with proven culling headroom.
Hardware Impact: Potential CPU/GPU savings on dense scenes are PENDING Rendering Statistics capture.

## Evidence Text Filter Audit

Problem: Text search can find terms but cannot prove integration, runtime safety, profiling, or editor state.
Solution: Add a reporting mandate that labels evidence classes and downgrades unsupported claims to `PENDING VERIFICATION`.
Rejected Alternatives: A blanket "all agents audited" claim was rejected because other agents write concurrently and text filters are not runtime tools.
Scalability potential: Process-only.
Hardware Impact: No runtime impact.

## i3 rsqrt Path

Problem: Hot path normalization and inverse-length math still risk `sqrt`/`.normalized` use in runtime code.
Solution: Add an i3 reciprocal square-root mandate: use Burst `math.rsqrt(max(dot, eps))`, squared comparisons, and feature-gated v256 batches.
Rejected Alternatives: Quake bit hacks, `sqrt` then reciprocal, and v512 assumptions were rejected.
Scalability potential: Low uses dominant-axis/L1 fakes for visual-only vectors; High/Ultra can use v256 batches after CPU feature proof.
Hardware Impact: Expected scalar/vector inverse-length savings on i3 are PENDING Burst Player benchmark.

## VR Stencil Masking

Problem: VR visor/HUD overdraw doubles cost per eye when hidden fragments still shade.
Solution: Add a stencil masking law requiring a cheap mask writer and consuming passes with stencil Equal.
Rejected Alternatives: Alpha-blended hidden HUD fragments and nested stencil tricks on Low were rejected.
Scalability potential: Low uses visor/HUD stencil only; High/Ultra may add richer visor layers after Frame Debugger proof.
Hardware Impact: Prior local estimate is 40-120 us GPU, still PENDING XR GPU CAPTURE.

## CI MATH_VIOLATIONS

Problem: No existing `MATH_VIOLATIONS` gate was found, and broad static scan still found `.normalized` candidates in runtime code.
Solution: Add a CI mandate with banned tokens, exclusions, and suppression artifact requirements.
Rejected Alternatives: Relying on reviewers to notice math debt manually was rejected.
Scalability potential: Process-only; Low benefits most once owners replace violations.
Hardware Impact: Preventive. Actual runtime gain depends on downstream fixes.

## VRS on MX350

Problem: Variable Rate Shading was requested, but Unity/NVIDIA evidence points to modern XR/VRS hardware classes rather than MX350 as a safe baseline.
Solution: Add a VRS reality mandate: MX350 assumes unsupported until runtime caps and Player capture prove otherwise.
Rejected Alternatives: Making VRS a Low-tier optimization pillar was rejected.
Scalability potential: Low uses render scale, stencil, occlusion, and LOD. High/Ultra can test VRS/foveated rendering where hardware reports capability.
Hardware Impact: 0 us claimed on MX350. Avoids depending on unavailable GPU features.

## Deterministic Slot-Machine RNG

Problem: Weighted random selection can become non-replayable if it uses Unity random, frame time, object instance ID, or floating cumulative weights.
Solution: Add deterministic integer weighted-selection law with stable seed components, table versioning, and replay-loggable results.
Rejected Alternatives: `Random.Range`, wall-clock seeds, floating cumulative weights, and modulo-biased tables were rejected.
Scalability potential: Low uses compact integer tables; High/Ultra add richer presentation variation after the deterministic result.
Hardware Impact: Replay stability; microseconds are PENDING determinism benchmark.

## Compile Medic - Data Archaeology Includes

Problem: Core compile failed after active DataArchaeology edits because `ScannerTool.cs` referenced `DataArchaeologyRuntime`, but `DataArchaeologyRuntime.cs` and its `LoreMmfEncyclopedia.cs` dependency were not included in `Hecton8.Core.csproj`. A follow-up error showed `ScannableFragment.cs` using `string.AsSpan()` without importing `System`.
Solution: Add the two missing generated-project compile includes and add `using System;` to `ScannableFragment.cs`.
Rejected Alternatives: Reverting the other agent's DataArchaeology edits was rejected because they are unrelated user/agent work and the missing includes/import were narrow compile blockers.
Scalability potential: Compile-only.
Hardware Impact: No runtime microseconds claimed.

## Async Asset Upload

Problem: Texture/mesh async upload budget was only partially controlled; bootstrap set buffer size but not time slice or persistent-buffer policy.
Solution: Add an async upload mandate and set `QualitySettings.asyncUploadTimeSlice` plus `QualitySettings.asyncUploadPersistentBuffer` in bootstrap alongside the existing tiered buffer size.
Rejected Alternatives: Leaving upload budget to project settings was rejected because hardware-tier decisions already happen in `GameBootstrapper`.
Scalability potential: Low/MX350 uses 64 MB / 1 ms; Middle uses 128 MB / 2 ms; High/Ultra uses 256 MB / 4 ms to buy faster hero-area residency.
Hardware Impact: Expected texture upload hitch reduction on i3/MX350; exact microseconds are PENDING PLAYER PROFILER.

## Global State Reset

Problem: Domain-reload-disabled and non-reload scene transitions can preserve stale static state, event handlers, NativeQueues, buffers, and async callbacks.
Solution: Add a global reset mandate requiring SubsystemRegistration reset, generation tokens, queue drains, JobHandle completion/cancel, NativeContainer disposal, and GlobalRegistry clearing.
Rejected Alternatives: Relying on `OnDestroy`, scene unload order, or domain reload was rejected because Unity explicitly preserves static state when domain reload is disabled.
Scalability potential: Low drops cosmetic queues first; High/Ultra may preserve warmed caches only after generation-token proof.
Hardware Impact: Prevents leaks and first-frame transition spikes; exact savings PENDING TRANSITION PROFILER.

## Compute Warp Sizing

Problem: The project has 64-thread MX350 kernels, but stale documentation/comments and common 256-thread assumptions can mislead mobile/low-tier dispatch policy.
Solution: Add compute group sizing mandate and correct the boid shader comment to match `THREAD_GROUP_SIZE 64`.
Rejected Alternatives: Universal 256-thread groups were rejected because small GPUs/mobile targets can lose occupancy or exceed register/shared-memory budget.
Scalability potential: Low uses 32/64 and staggered dispatch; High/Ultra can test 128/256 variants only with capture proof.
Hardware Impact: Avoids oversized dispatch groups on small GPUs; exact gain PENDING GPU CAPTURE.

## Pentarchy Audit

Problem: A five-pillar "Pentarchy" does not match the current authoritative domain model.
Solution: Add architecture audit mandate: `Docs/Actual Domains of Project.txt` defines 9 echelons / 85 domains, and any Pentarchy language is legacy shorthand only.
Rejected Alternatives: Treating Pentarchy as ownership authority was rejected because it omits evidence, scalability, streaming, determinism, and anti-corruption pillars.
Scalability potential: Process-only, but it protects cheap hardware by keeping enforcement pillars explicit.
Hardware Impact: 0 runtime microseconds claimed.

## Mandate Version 6.0 Summary

Problem: The final summary lacked Loop 6 updates and still needed explicit broken/outdated statements.
Solution: Update `MANDATE_VERSION_6.0.txt` with async upload, reset, compute sizing, and Pentarchy findings.
Rejected Alternatives: Marking the batch VERIFIED was rejected because Unity Console, PlayMode, profiler, Frame Debugger, RenderGraph Viewer, and Player captures were not produced.
Scalability potential: Summary binds Low/Middle/High/Ultra rules for downstream owners.
Hardware Impact: Process-only; runtime savings remain PENDING VERIFICATION.

## Omega Anti-Bloat Audit

Problem: Final polish required checking whether TECH_SCOUT changes introduced hot-path math, managed allocation, per-frame branches, or cache-hostile structures.
Solution: Scan touched runtime files and review the actual code diff. The new runtime change is limited to bootstrap `QualitySettings` assignments and tier helper switches; it does not run per frame and does not allocate.
Rejected Alternatives: Claiming `VERIFIED MASTER GRADE` was rejected because Unity Console, PlayMode, profiler, GCMonitor, RenderGraph Viewer, Frame Debugger, and Player captures are absent.
Scalability potential: Low/MX350 uses 64 MB / 1 ms async uploads and 64-thread compute defaults; Middle uses 128 MB / 2 ms; High/Ultra uses 256 MB / 4 ms and may test wider compute variants after capture.
Hardware Impact: Runtime hot-path cost added is 0 us steady-frame. Upload hitch reduction is PENDING PLAYER PROFILER.

## Omega Cinematic Cheats

Problem: The prompt required listing honest calculations replaced by cinematic cheats.
Solution: No physical or mathematical runtime simulation was edited by TECH_SCOUT, so no live honest calculation was replaced in code. Mandates now require future owners to use visual cheats where allowed: fog/chunk fade for low-tier planetary wrap seams, dominant-axis/L1 vector fakes for visual-only normalization, stencil rejection instead of shading hidden VR HUD fragments, integer deterministic RNG plus richer presentation variance, and staggered/64-thread compute instead of brute-force wider groups.
Rejected Alternatives: Editing unrelated simulation domains to force cheats was rejected as cross-domain scope violation.
Scalability potential: Low favors fakes and staggered work; Ultra spends saved cycles on visual density after authority math stays deterministic.
Hardware Impact: Microseconds saved are PENDING MEASUREMENT except previously logged VR stencil estimate of 40-120 us GPU, still unverified by this pass.

## Compile Verification

Problem: `--no-dependencies` initially failed because Unity-generated dependency DLLs under `Temp/bin/Debug` were missing, not because TECH_SCOUT source failed.
Solution: Run dependency-building Core compile, then rerun isolated Core compile. Dependency build passed with 47 external/package warnings and 0 errors; isolated Core passed with 0 warnings and 0 errors.
Rejected Alternatives: Treating missing metadata as a source failure was rejected after the dependency build regenerated the DLLs.
Scalability potential: Compile-only.
Hardware Impact: No runtime microseconds claimed.
