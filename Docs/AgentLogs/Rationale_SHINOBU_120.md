# Rationale_SHINOBU_120

Created: 2026-05-19

## Decision 001 - Implementation Boundary
Problem: The assignment targets volumetric particulate fog, but the repository already contains separate noir depth fog, half-res volumetric light, abyssal flow, and GPU marine snow systems.
Solution: Add a dedicated RenderGraph compute facade for volumetric particulate fog that consumes the existing abyssal flow globals and MarineSnow fog-density texture instead of rewriting those owners. This matches the presentation-layer mandate and keeps the system in VISUAL_SYNC.
Rejected Alternatives: Replacing HectonMarineSnowRenderer or VolumetricLightFeature would couple unrelated domains and risk breaking existing GPU particle/wake work. CPU fluid simulation is rejected by the Dear Lie and frame-time mandates.
Scalability potential: Low uses dithered analytic fog with minimal steps; middle uses reduced-resolution short raymarch; high uses more steps and flow/noise; ultra allows up to 64 steps and extra scattering samples.
Hardware Impact: i3/MX350 avoids CPU particle truth and per-frame RT allocation; expected gain is avoiding a Shuriken/CPU-fluid path, exact microseconds PENDING VERIFICATION.

## Decision 002 - Registry and Evidence
Problem: AGENTS.md requires status and rationale files before recording completion, and the repo has no prior SHINOBU_120 state.
Solution: Create explicit status and rationale files and keep task checkboxes tied to verifiable implementation evidence. Unknown metrics remain PENDING VERIFICATION.
Rejected Alternatives: Marking tasks complete from source inspection alone would create fake reports.
Scalability potential: Evidence-first status allows low/middle/high/ultra claims to remain tied to source or profiler proof.
Hardware Impact: No runtime impact. Process guard only.

## Decision 003 - 64B Fog DTO
Problem: Fog state must cross C#/shader/vault boundaries without hidden managed properties or layout drift.
Solution: Use VolumetricFogParamsDTO with explicit 64B layout: FogColorAndDensity at 0, ScatteringParams at 16, FlowAdvection at 32, QualityAndLimits at 48. Validate with UnsafeUtility.SizeOf and Marshal.OffsetOf, matching established project layout checks.
Rejected Alternatives: Auto-layout structs, ScriptableObject runtime state, or properties were rejected because they hide layout and invite GC/property graph work.
Scalability potential: The same DTO drives low, middle, high, and ultra paths through continuous floats instead of tier enums.
Hardware Impact: i3/MX350 receives one 64B constant upload instead of managed parameter fan-out. Exact microseconds PENDING PROFILER.

## Decision 004 - RenderGraph Persistent Targets
Problem: The feature needs half/quarter raymarch and full-res composite without per-frame temporary texture allocation.
Solution: Allocate persistent RTHandles quantized to 64-pixel buckets, import them into RenderGraph, and execute raymarch/composite as compute passes.
Rejected Alternatives: RenderTexture.GetTemporary, CommandBuffer.Blit, and material full-screen passes were rejected because they conflict with the no-temp/no-blit mandate and existing RenderGraph patterns.
Scalability potential: Low uses quarter-ish internal scale; middle increases scale; high/ultra increase scale while preserving the same pass topology.
Hardware Impact: Avoids temp RT churn on weak GPUs; exact allocation and GPU savings PENDING PROFILER.

## Decision 005 - Dithered Proxy Through 64 Steps
Problem: A binary low/ultra switch would violate GlobalQualityWeight and waste weak hardware on a token loop.
Solution: Resolve ray steps continuously from 4 to 64 and proxyBlend continuously from analytic dithered fog to full raymarch. At proxyBlend >= 0.999, the shader bypasses the loop.
Rejected Alternatives: Fixed 16/32/64 presets and simulating real suspended-fluid physics were rejected. Fog is a cinematic cheat, not a fluid solver.
Scalability potential: Low = analytic dithered opacity; middle = short reduced-res march; high = longer flow/noise march; ultra = 64-step scattering with more point lights.
Hardware Impact: Toaster path skips the march loop; RTX path spends saved budget on visual density and shafts. Exact microseconds PENDING PROFILER.

## Decision 006 - Flow and Silt Ownership
Problem: Wake-reactive fog needs propeller turbulence, but the fog feature must not invent direct dependencies on another agent's submarine code.
Solution: Consume existing shader globals from HectonMarineSnowRenderer and abyssal flow. Marine snow fog density becomes the wake/silt input; abyssal flow advection warps volumetric noise.
Rejected Alternatives: New 3D SiltBuffer owner, CPU fluid grids, or submarine component lookup were rejected as cross-domain coupling and frame-time risk.
Scalability potential: Low can ignore inactive globals with 1x1 fallback textures; middle/high/ultra progressively reveal flow and wake density when producers are present.
Hardware Impact: Uses already-produced GPU fields; exact microseconds PENDING PROFILER.

## Decision 007 - Biome and CSV Bridge
Problem: Extinction must react to biome/water profile without a black-box fog stack or managed parsing in the hot path.
Solution: Remove direct BiomeMatrix reads from the render domain, seed default extinction DTOs, expose a ReadOnlySpan<byte> CSV parser for water_extinction_profiles.csv, and blend vault-loaded profiles by local depth band until the biome owner publishes an unmanaged DTO/signal. The editor path now streams file bytes into vault-owned CsvScratch and parses directly into the vault profile array.
Rejected Alternatives: FindObjectOfType, GlobalRegistry.BiomeMatrix concrete polling, string.Split, per-frame CSV parsing, editor File.ReadAllBytes staging, and temp NativeHashMap/GetValueArray copying were rejected.
Scalability potential: Weak devices still use one selected extinction profile; middle/high/ultra spend the same DTO profile data on smoother depth-band blending and denser scattering.
Hardware Impact: No hot-path managed parser; exact microseconds PENDING PROFILER.

## Decision 008 - Telemetry Honesty
Problem: The prompt requests GPU timing and a black-box ring, but the current implementation could not be compiled/profiled because CPU guard blocked build execution.
Solution: Implement a 300-entry NativeArray telemetry ring, source-state hash, flags, estimated GPU usec, and binary dump on NaN or estimated >2ms. The dump now writes the NativeArray memory through `ReadOnlySpan<byte>` and `FileStream` instead of copying into a managed `byte[]`. Mark GPU query/profiler numbers as pending instead of fabricating results.
Rejected Alternatives: Reporting fake exact GPU query values, omitting the black box, or allocating a managed dump staging array were rejected.
Scalability potential: Same ring records low/middle/high/ultra behavior and can be swapped to real GPU query values after compile/profiler access.
Hardware Impact: Ring write is fixed-size NativeArray state; exact measured cost PENDING PROFILER.

## Decision 009 - Compile-Wall Boundary Repair
Problem: The first pass directly referenced Hecton8.Environment types from a render feature, crossing a sibling domain without a contracts route.
Solution: Remove the concrete BiomeMatrix/AtmosphereProfile read. Current fog uses owner-local settings, vault DTO overrides, and vault-loaded extinction profiles. A biome hash blend requires a future unmanaged contract or signal from the biome owner.
Rejected Alternatives: Keeping `using Hecton8.Environment`, polling `GlobalRegistry.BiomeMatrix`, or adding a new one-off global route without a route card were rejected.
Scalability potential: Low/middle/high/ultra still blend through the same DTO; future biome data can be fed by a single owner route without rewriting the shader.
Hardware Impact: Removes per-frame concrete registry dependency and compile-wall risk. Exact microseconds PENDING PROFILER.

## Decision 010 - Scheduled Mock Light Job and Double Buffer
Problem: Per-frame `IJob.Run()` and a single point-light GraphicsBuffer violated dependency-chain and GPU upload discipline.
Solution: Schedule `BuildMockVolumetricLightsJob` with exact Burst flags and `[NoAlias]`, upload completed prior-frame data into the inactive GraphicsBuffer via `UnsafeUtility.MemCpy`, then flip active buffers.
Rejected Alternatives: Same-frame `.Run()`, `Complete()` before checking `IsCompleted`, `SetData`, and single-buffer CPU/GPU contention were rejected.
Scalability potential: Low uploads zero/one cheap light after first completed job; high/ultra feed up to eight synthetic lights without changing topology.
Hardware Impact: Reduces main-thread stall and PCIe contention risk. Exact microseconds PENDING PROFILER.

## Decision 011 - Explicit Dear Lie Dither
Problem: The prior proxy path used stochastic noise but did not prove the requested ordered dither fallback.
Solution: Add a 4x4 Bayer matrix generated arithmetically, blend it with temporal screen noise, and keep the low-quality branch outside the raymarch loop.
Rejected Alternatives: Full raymarch on weak hardware and stochastic-only dirt were rejected.
Scalability potential: Below the low-quality threshold the shader collapses to one exponential fog evaluation; higher weights smoothly re-enter reduced-resolution raymarching and 64-step overkill.
Hardware Impact: Low path bypasses the loop and avoids 3D noise octaves. Exact microseconds PENDING PROFILER.

## Decision 012 - Silt Ownership
Problem: The literal task asks for a new persistent 3D SiltBuffer, but the project already has MarineSnow wake/fog-density ownership.
Solution: Consume existing MarineSnow fog-density/wake texture as the authoritative silt signal. Do not create a duplicate wake/SiltBuffer owner in this render facade.
Rejected Alternatives: Direct WakeGeneratedSignal dependency, Submarine kinematic DTO polling, and a second 3D density owner were rejected by one-owner authority and compile-wall rules.
Scalability potential: Low ignores absent texture through 1x1 fallback; high/ultra amplify the existing wake density in the raymarch.
Hardware Impact: Avoids duplicate compute injection and memory surface. Exact microseconds PENDING PROFILER.

## Decision 013 - RenderGraph Buffer Ownership
Problem: The compute pass imported textures into RenderGraph but bound the fog constant buffer through `Shader.SetGlobalConstantBuffer` before the graph passes, leaving a hidden resource edge outside RenderGraph validation.
Solution: Import `_paramsBuffer` and the active point-light GraphicsBuffer as BufferHandles, declare read access with `builder.UseBuffer`, bind the constant buffer inside each compute render function, and pass the point-light buffer through the graph handle route.
Rejected Alternatives: Keeping process-wide shader state was rejected because it can race graph scheduling and hides the proof required by the zero-temp RenderGraph mandate.
Scalability potential: Low, middle, high, and ultra all use the same declared buffer route; quality changes remain scalar fields in the 64B DTO instead of variant-specific globals.
Hardware Impact: Avoids implicit global-state hazards and improves RenderGraph scheduling visibility. Exact microseconds PENDING PROFILER.

## Decision 014 - Noir Luminance Floor
Problem: The shader allowed `_HectonVolumetricFogColorAndDensity.rgb` to resolve to pure black through `max(color, 0)`, which can erase particulate depth cues and violates the Deep Sea Noir mandate against flat black voids.
Solution: Add `ResolveNoirFloorColor` with a tiny blue-green luminance floor and use it in both proxy and raymarched branches.
Rejected Alternatives: Clamping in C# settings only was rejected because editor/CSV/runtime overrides can still feed zero into the shader. A shader floor is the final visual authority.
Scalability potential: Low proxy fog remains legible under Bayer dithering; high/ultra raymarching preserves color energy for scattering without adding samples.
Hardware Impact: One `max(float3)` per affected path; expected cost is below measurable threshold. Exact microseconds PENDING PROFILER.

## Decision 015 - Proxy Curve and Texture Edges
Problem: Static subagent audit found two resource/math defects: `Mathf.SmoothStep(0.12, 0.42, q)` was incorrectly treated like HLSL `smoothstep(edge0, edge1, x)`, preventing proxyBlend from reaching 1.0, and external MarineSnow/AbyssalFlow textures were raw global reads without RenderGraph texture edges.
Solution: Replace proxy math with an explicit cubic smoothstep polynomial over the 0.12..0.42 quality interval. Wrap external texture sources in cached RTHandles, import them into RenderGraph, declare read access, and bind TextureHandles inside the compute pass.
Rejected Alternatives: A boolean low-end branch was rejected by the GlobalQualityWeight continuum. Per-frame texture wrappers were rejected by zero-temp policy. Direct producer dependencies were rejected by compile-wall rules.
Scalability potential: Low now truly executes the analytic Bayer proxy branch; middle smoothly blends back into reduced raymarching; high/ultra keep declared graph inputs for flow/wake sampling.
Hardware Impact: Low-end path can finally bypass the loop as designed. Exact microseconds PENDING PROFILER.

## Decision 016 - Shader NaN Write Barrier
Problem: The shader guarded many denominators but still wrote final raymarch and composite values to UAVs without a finite check, allowing one bad upstream texture/depth value to poison the frame.
Solution: Add `SafeFiniteScalar`, `SafeFiniteColor`, and `ResolveSafeFogWrite`; sanitize proxy/raymarch writes and composite color/alpha before UAV output.
Rejected Alternatives: C# telemetry-only NaN detection was rejected because it catches symptoms after the GPU write. The shader must prevent propagation at the write barrier.
Scalability potential: Same barrier applies from proxy mode through 64-step mode without variants or branches on hardware tiers.
Hardware Impact: A few finite checks at write-out only; expected cost is below measurable threshold. Exact microseconds PENDING PROFILER.

## Decision 017 - Vault CSV Scratch
Problem: The editor CSV load still used managed `File.ReadAllBytes`, a temp `NativeHashMap`, and `GetValueArray(Allocator.Temp)`. It was cold-only, but it weakened the human-control facade proof and left the reserved CsvScratch buffer unused.
Solution: Add `ExtinctionCsvScratchBytes`, stream the file into `ShinobuVolumetricFogCsvScratch`, and parse the scratch `ReadOnlySpan<byte>` directly into `ShinobuVolumetricFogExtinctionProfiles` with duplicate hash upsert inside the fixed-capacity array.
Rejected Alternatives: Keeping temp native containers was rejected because a fixed 16-profile table does not need a hash map. A runtime binary reader was rejected because this task is the editor/human-tuning CSV facade, not a production payload route.
Scalability potential: Low, middle, high, and ultra all read the same compact profile DTOs; designers can tune depth/biome-style water clarity without C# recompilation.
Hardware Impact: Removes cold editor native temp allocations and one copy. Runtime hot-path impact remains unchanged. Exact microseconds PENDING PROFILER.

## Decision 018 - Span-Based Telemetry Dump
Problem: The surge dump path still allocated a managed `byte[]` and used `Marshal.Copy` before writing the 300-frame telemetry ring. It was failure-only, but the black-box path should be deterministic and allocation-minimal.
Solution: Wrap the telemetry NativeArray pointer in `ReadOnlySpan<byte>` and stream directly to the dump file with `FileStream.Write`.
Rejected Alternatives: Keeping the managed staging array was rejected because the dump size is fixed and the source memory is already contiguous/blittable.
Scalability potential: Same dump path works for low/middle/high/ultra; no gameplay or shader quality behavior changes.
Hardware Impact: Removes one managed allocation and one memory copy on surge/NaN dump. Exact microseconds PENDING PROFILER.

## Decision 019 - Explicit Visual Phase
Problem: Shader drift used Unity's implicit `_Time.y`, making low-quality update shedding undocumented and hiding a global dependency in the shader.
Solution: Compute a presentation-only visual phase in C# from `Time.frameCount`, quantized by a polynomial `GlobalQualityWeight` curve from about 5Hz to 60Hz, pass it in `_HectonVolumetricFogCompositeParams.w`, and use that scalar for dither temporal noise, flow advection, and mock light phase.
Rejected Alternatives: Keeping `_Time.y` was rejected because it bypassed the SHINOBU DTO/frame-param route. A gameplay simulation tick route was rejected because this fog remains rollback-excluded presentation.
Scalability potential: Low quality advances visual drift less frequently while proxy fog bypasses raymarching; high/ultra update every frame with 64-step overkill available.
Hardware Impact: Low-end shader temporal work now naturally freezes between quantized phase updates instead of changing every frame. Exact microseconds PENDING PROFILER.
