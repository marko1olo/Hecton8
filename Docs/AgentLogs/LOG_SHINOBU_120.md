# LOG_SHINOBU_120

## 2026-05-19 - Volumetric Particulate Fog Director

What was wrong:
- No SHINOBU_120-specific volumetric particulate fog facade existed.
- Existing atmosphere stack had separate noir depth fog, volumetric light, abyssal flow, and MarineSnow systems, but no dedicated RenderGraph fog that scaled 4..64 ray steps by GlobalQualityWeight.
- There was no SHINOBU_120 64B vault DTO, no 300-frame fog black box, no abyssal atmosphere UI, and no local water_extinction_profiles.csv parser for this domain.

What was done:
- Added BufferID slots 71130..71134 for SHINOBU volumetric fog params, point lights, telemetry, extinction profiles, and CSV scratch.
- Added VolumetricFogParamsDTO with explicit 64B layout and validation; added PointLightDTO, VolumetricFogTelemetryEntry, WaterExtinctionProfileDTO, ref access helpers, Burst mock light job, and ReadOnlySpan<byte> CSV parser.
- Added Hecton_VolumetricFog.compute: low-end dithered analytic proxy, wrapped noise, abyssal flow advection, MarineSnow/wake density injection, point-light scattering, opacity early break, 64-step max raymarch, bilateral composite, and heatmap overlay.
- Added HectonVolumetricParticulateFogFeature: RenderGraph compute raymarch/composite, persistent RTHandles, persistent GraphicsBuffers, LockBufferForWrite uploads, vault-backed DTO state, BiomeMatrix atmosphere bridge, 300-entry telemetry, and Dump_VOLUMETRIC_SURGEON.bin surge dump path.
- Updated HectonRenderPipelineValidator to insert/configure the feature and assign the compute shader in managed renderer data.
- Added UI Toolkit Abyssal Atmosphere Tuner for DTO sliders, GlobalQualityWeight forcing, extinction CSV load, and telemetry graph.
- Added Docs/water_extinction_profiles.csv with default abyss/brine/silt/vent profiles.

Cinematic Cheats used:
- Dithered analytic fog proxy replaces raymarch on survival quality.
- Noise uses local 256m wrapping instead of absolute universe precision.
- Existing MarineSnow fog-density/wake texture is consumed as the silt field instead of simulating particles or fluid again.
- Abyssal flow texture warps fog noise visually; it is not a new physical fluid owner.
- Bilateral composite hides reduced-resolution raymarch cost behind depth-aware upsample.

Exact Microseconds saved:
- NOT MEASURED. Unity compile/profiler was blocked by project guard: CPU sampled at 100 percent, and rule forbids build when CPU >50 percent.
- Source-level evidence only: no RenderTexture.GetTemporary, CommandBuffer.Blit, Graphics.Blit, SetData, GetData, string.Split, or FindObjectOfType in new runtime hot-path files.
- Feature telemetry currently records estimated GPU microseconds and dumps on NaN/>2ms estimate. Real GPU query integration remains PENDING PROFILER.

Verification performed:
- Prompt extracted from Docs/Tasks/CURRENT_BATCH.md using SHINOBU_120 tag.
- Domain read from Docs/Actual Domains of Project.txt.
- Mandates read: noir fog/dithering, URP hot path, VFX compute particles, MX350 compute, mobile warp sizing, struct layout, zero-GC, execution phases, signal lanes.
- Static grep found no target MarineSnow/SiltDust/DeepSeaParticles Shuriken prefabs to delete; unrelated ParticleSystems were preserved.
- Path-limited `git diff --check` reported no SHINOBU_120 whitespace errors; only CRLF warnings in existing repo style.

<SELF_AUDIT>
Agent: SHINOBU_120
Domain: Echelon 7 Atmosphere & Celestial / Volumetric Fog & Marine Snow/Silt facade
Task count: 20

DTO layout:
- VolumetricFogParamsDTO size: 64 bytes by source validation.
- FogColorAndDensity offset: 0.
- ScatteringParams offset: 16.
- FlowAdvection offset: 32.
- QualityAndLimits offset: 48.
- Shader CBUFFER HectonVolumetricFogParams field order matches DTO order.

Buffer IDs:
- ShinobuVolumetricFogParams = 71130.
- ShinobuVolumetricFogPointLights = 71131.
- ShinobuVolumetricFogTelemetryRing = 71132.
- ShinobuVolumetricFogExtinctionProfiles = 71133.
- ShinobuVolumetricFogCsvScratch = 71134.

RenderGraph allocation evidence:
- Persistent RTHandles: _HectonVolumetricFogHalf and _HectonVolumetricFogComposite.
- Persistent GraphicsBuffers: 64B constant params and PointLightDTO x8 structured lights.
- Runtime hot-path grep found no RenderTexture.GetTemporary/Blit/SetData/GetData.
- Cold allocations remain for persistent buffers, fallback textures, and binary surge dump only.

Scalability:
- Low: dithered analytic fog, loop bypass at proxyBlend >= 0.999, reduced internal scale.
- Middle: reduced-res short raymarch with bilateral composite.
- High: increased scale, more steps, more mock point lights, flow/noise/wake density.
- Ultra: up to 64 ray steps and full fixed light buffer consumption.

Unresolved verification:
- Unity compile not run due CPU guard.
- GPU query timing not implemented; current telemetry is estimated GPU usec.
- Profiler microseconds are PENDING PROFILER.
</SELF_AUDIT>

## 2026-05-19 - Ultra-Think Polish Pass

What was wrong:
- Runtime feature had a direct concrete `Hecton8.Environment` dependency.
- Mock light generation used per-frame `IJob.Run()`.
- Burst flags were incomplete and the NativeArray job field lacked `[NoAlias]`.
- Point-light upload used one GraphicsBuffer, not a CPU-write/GPU-read double-buffer.
- Dear Lie fallback used stochastic dither only, not an explicit Bayer matrix.
- Shader scattering had point lights but no primary directional light contribution.

What was done:
- Removed the direct Environment/BiomeMatrix read. Biome-specific data now enters only through SHINOBU vault extinction profiles or the existing fog params DTO.
- Reworked mock lights into a scheduled Burst job with exact flags and `[NoAlias]`.
- Added point-light GraphicsBuffer A/B and inactive-buffer upload via `UnsafeUtility.MemCpy`.
- Added shader-side 4x4 Bayer dither blended with temporal noise for the low-quality proxy branch.
- Added `_SunDirection` / `_FinalGiantAbyssLight` directional scattering.
- Multiplied abyssal flow advection by `_Time.y` in the wrapped noise path.
- Removed editor DTO mutation through `System.Func` wrappers.

Cinematic Cheats used:
- 1D exponential fog plus ordered dither for survival quality.
- Existing MarineSnow wake density is the silt truth for this facade; no duplicate CPU/3D fluid simulation.
- Directional and point-light scattering are visual approximations, not physical participating-media truth.

Exact Microseconds saved:
- NOT MEASURED. CPU guard after polish sampled 97.716 percent and final guard sampled 100 percent, so build/profiler launch remains forbidden.
- Source-level expected savings: no same-frame `IJob.Run()`, no single-buffer light upload contention, no raymarch loop in proxy path.

<SELF_AUDIT_POLISH>
<TASK_RECONCILIATION>
<TASK id="01" status="PASS">Target Shuriken prefab scan found no exact MarineSnow/SiltDust/DeepSeaParticles owners to delete; unrelated ParticleSystems preserved.</TASK>
<TASK id="02" status="PASS">Custom RenderGraph feature is validator-wired; no black-box fog dependency added.</TASK>
<TASK id="03" status="PASS">Fog DTO uses raw fields and ref mutation helpers; no hot DTO properties.</TASK>
<TASK id="04" status="PASS">Primary DTO layout validator checks size 64 and offsets 0/16/32/48.</TASK>
<TASK id="05" status="PASS">Mock light generation is a Burst job with exact flags and NoAlias; scheduled one-frame latency, not Run.</TASK>
<TASK id="06" status="PASS">Compute raymarch samples depth, wrapped density noise, directional light, and PointLightDTO scattering.</TASK>
<TASK id="07" status="PASS">Low-quality branch bypasses the raymarch loop and uses 1D exponential fog plus 4x4 Bayer/temporal dither.</TASK>
<TASK id="08" status="PASS">Abyssal flow texture offsets fog noise over time; no CPU fluid simulation.</TASK>
<TASK id="09" status="PASS_BY_OWNER_SUBSTITUTION">Literal new 3D SiltBuffer is not added. Existing MarineSnow wake/fog-density owner is consumed to preserve one-owner authority.</TASK>
<TASK id="10" status="PASS">Persistent reduced/full RTHandles plus depth-aware bilateral compute composite.</TASK>
<TASK id="11" status="PASS">Ray steps scale 4..64 from GlobalQualityWeight and shader breaks on opacity.</TASK>
<TASK id="12" status="YELLOW">Direct BiomeTransitionManager concrete read removed. Current route is vault extinction profiles/depth bands until biome owner publishes unmanaged contract.</TASK>
<TASK id="13" status="PASS">Shader receives local wrapped float3 noise offset, not absolute double3 AUP.</TASK>
<TASK id="14" status="PASS">New SHINOBU buffers are visual/vault/editor only; no rollback/Merkle wiring found.</TASK>
<TASK id="15" status="PASS">Vault buffers use UninitializedMemory; GPU uploads use persistent buffers and LockBufferForWrite.</TASK>
<TASK id="16" status="YELLOW">300-entry ring and dump path exist; GPU query is still estimated telemetry pending profiler access.</TASK>
<TASK id="17" status="PASS">UI Toolkit tuner mutates vault DTOs and displays telemetry graph; no OnGUI path.</TASK>
<TASK id="18" status="PASS">CSV parser uses ReadOnlySpan<byte>, FNV-1a, no string.Split.</TASK>
<TASK id="19" status="PASS">Shader heatmap blends by executed raymarch steps.</TASK>
<TASK id="20" status="PASS_STATIC">Self-audit exists on disk; runtime/profiler proof remains pending.</TASK>
</TASK_RECONCILIATION>
<STRUCT_LAYOUT>
VolumetricFogParamsDTO: Size=64. FogColorAndDensity float4 offset 0 size 16. ScatteringParams float4 offset 16 size 16. FlowAdvection float4 offset 32 size 16. QualityAndLimits float4 offset 48 size 16. 16+16+16+16=64, multiple of 16, no Pack=1.
PointLightDTO: Size=32, two float4 lanes, multiple of 16.
VolumetricFogTelemetryEntry: Size=64, explicit telemetry cache line; single writer in render feature, no parallel atomic counter.
WaterExtinctionProfileDTO: Size=64, explicit profile cache line.
</STRUCT_LAYOUT>
<SCALABILITY_CURVE>
GlobalQualityWeight drives internal scale, ray steps, proxy blend, point-light count, and shader noise octaves. Below roughly 0.3, proxyBlend dominates and the shader collapses to one exponential fog solve with ordered dither; expensive 3D noise/ray steps are bypassed when proxyBlend reaches 0.999. High weight moves toward 64 steps, more lights, flow/noise advection, and directional scattering.
</SCALABILITY_CURVE>
<H_PHI_VAULT_STATUS>
No private persistent NativeArray/NativeList/NativeHashMap fields in the runtime feature. Requested vault buffers: ShinobuVolumetricFogParams=71130, ShinobuVolumetricFogPointLights=71131, ShinobuVolumetricFogTelemetryRing=71132, ShinobuVolumetricFogExtinctionProfiles=71133. CsvScratch=71134 reserved; editor CSV load currently writes parsed profiles into the profile buffer.
</H_PHI_VAULT_STATUS>
<POINTER_ALIASING_AND_DEPENDENCIES>
BuildMockVolumetricLightsJob has [NoAlias] on PointLights and exact Burst flags. Runtime schedules one job when no prior job is pending. It uploads only after JobHandle.IsCompleted then Complete() returns without intended stall. No SystemDispatcher JobHandle chain exists for ScriptableRenderPass RenderGraph injection; this is a render-facade one-frame-latency job.
</POINTER_ALIASING_AND_DEPENDENCIES>
<COMPILE_GUARD>
No new asmdef reference was added. Direct sibling concrete Environment dependency was removed from SHINOBU runtime files. H8Memory contains concurrent unrelated BufferID edits by other agents; SHINOBU-owned additions are 71130..71134.
</COMPILE_GUARD>
<DEAR_LIE>
Before: literal wake/silt fluid would require O(volume cells + wake injectors) compute plus cross-domain vehicle coupling. After: fog samples existing MarineSnow wake density and analytic noise in O(pixels * selected ray steps), or O(pixels) in proxy mode. The illusion is ordered dither plus exponential fog, not physical particulate truth.
</DEAR_LIE>
<VERIFICATION>
Static greps clean for direct Environment dependency, .Run, temp RT, Blit, SetData/GetData, string.Split, FindObjectOfType, OnGUI, Pack=1, and DTO properties in SHINOBU files. git diff --check clean for edited SHINOBU files. CPU guard blocked compile/profiler at 97.716 percent, then 100 percent.
</VERIFICATION>
</SELF_AUDIT_POLISH>

## Post-Polish Memory Consistency Correction - 2026-05-19

What was wrong: Status Task 12 and Rationale Decision 007 still described the first-pass direct BiomeMatrix read after the runtime feature had already been repaired to remove the sibling-domain concrete dependency.

What was done: Updated Status_SHINOBU_120.md and Rationale_SHINOBU_120.md to state the current route: vault-loaded extinction profiles plus local depth-band blending, with direct BiomeMatrix/BiomeTransitionManager output blocked until the biome owner publishes an unmanaged DTO or signal.

Cinematic Cheats used: No extra simulation added. The render facade keeps water-family variation as cheap profile blending and spends budget on shader-side scattering, Bayer proxy fog, and MarineSnow wake-density sampling.

Exact Microseconds saved: PENDING PROFILER. CPU guard sampled 93.39 percent during this correction; build/profiler was not launched by project rule.

## Loop 7 RenderGraph Ownership Polish - 2026-05-19

What was wrong: The fog pass imported its render targets through RenderGraph, but `_paramsBuffer` was exposed with `Shader.SetGlobalConstantBuffer` before the passes. That hid a constant-buffer dependency from RenderGraph and weakened the zero-temp/no-hidden-state proof. The shader also allowed the fog medium color to clamp to pure black, which erases the required noir particulate read.

What was done: `_paramsBuffer` and the active point-light buffer are now imported as RenderGraph buffers and declared through `builder.UseBuffer`. The constant buffer is bound inside each compute pass via `SetComputeConstantBufferParam`, and point lights are passed through the BufferHandle route. The compute shader now uses `ResolveNoirFloorColor` in proxy and raymarched fog paths.

Cinematic Cheats used: No new fluid truth. The patch preserves the existing analytic medium, ordered dither, MarineSnow wake-density sampling, and shader-side scattering.

Exact Microseconds saved: PENDING PROFILER. Expected gain is architectural safety rather than claimed frame-time reduction: RenderGraph has explicit buffer dependencies and the shader retains depth cues without extra ray steps.

## Loop 8 Subagent Finding Repair - 2026-05-19

What was wrong: Static subagent audit found that the low-end proxy curve used Unity `Mathf.SmoothStep` with HLSL edge semantics. That meant proxyBlend never reached 1.0, so the shader bypass branch for survival quality was dead. The same audit found external MarineSnow/AbyssalFlow texture reads were bound as raw globals without RenderGraph texture edges, and final UAV writes lacked a finite write barrier.

What was done: `ResolveProxyBlend` now uses explicit cubic smoothstep over quality 0.12..0.42, allowing quality <=0.12 to execute the analytic Bayer proxy branch. MarineSnow and AbyssalFlow textures are now wrapped in cached RTHandles, imported into RenderGraph, declared with `builder.UseTexture`, and bound as TextureHandles. Shader proxy/raymarch output uses `ResolveSafeFogWrite`, and composite output sanitizes NaN fog/source values before writing.

Cinematic Cheats used: Low quality now truly collapses to one exponential fog solve with ordered dither. Wake/silt remains an owner-substitution through MarineSnow density; no duplicate 3D fluid truth was added.

Exact Microseconds saved: PENDING PROFILER. Source-level expected saving is now real for quality <=0.12 because the raymarch loop is bypassed instead of accidentally always running.

Verification note: whitespace scan over SHINOBU files returned no trailing whitespace. Focused grep found no remaining `Mathf.SmoothStep(0.12f` proxy misuse and no `Shader.SetGlobalConstantBuffer` in the SHINOBU runtime feature. `git diff --check` over SHINOBU files returned clean. Compile/profiler remained blocked: guards sampled CPU up to 100 percent with active `csc`/`dotnet`, then 89.43 percent with no dotnet/csc, still above the project build threshold.

## Loop 9 CSV Scratch Hardening - 2026-05-19

What was wrong: The editor CSV path was cold, but still used `File.ReadAllBytes`, a temp `NativeHashMap`, and `GetValueArray(Allocator.Temp)`. That contradicted the stricter human-control facade proof and left `ShinobuVolumetricFogCsvScratch` unused.

What was done: Added a fixed 64KB CSV scratch constant, streamed CSV bytes into the vault-owned scratch buffer, and changed the parser to write directly into the fixed-capacity `ShinobuVolumetricFogExtinctionProfiles` array. Duplicate profile hashes update in place; excess profiles are dropped after capacity instead of allocating.

Cinematic Cheats used: None added. This is data-route cleanup for the Beer-Lambert extinction profile fake already feeding the shader.

Exact Microseconds saved: PENDING PROFILER. Source-level expected saving is removal of cold editor temp allocations and one native copy; runtime frame cost is unchanged.

## Loop 9B Telemetry Dump Allocation Removal - 2026-05-19

What was wrong: `DumpTelemetryRing` copied the 300-entry NativeArray into a managed `byte[]` before writing the `.bin` forensic dump. It was not frame-normal, but it was still a failure-path allocation and an avoidable copy.

What was done: The dump now wraps the NativeArray memory in `ReadOnlySpan<byte>` and writes it directly through a `FileStream`. The stale `System.Runtime.InteropServices` import was removed from the render feature.

Cinematic Cheats used: None. This is black-box hygiene.

Exact Microseconds saved: PENDING PROFILER. Source-level expected saving is one managed allocation and one fixed-size copy on surge/NaN dump.

## Loop 9C Visual Phase Route - 2026-05-19

What was wrong: Shader dither and flow drift used Unity's implicit `_Time.y`, so the time source was hidden from SHINOBU frame params and low-quality update cadence did not explicitly collapse toward the requested survival behavior.

What was done: Added `ResolveVisualPhaseSeconds(GlobalQualityWeight)` in the render feature. It maps quality through a cubic curve to an update cadence from roughly 5Hz to 60Hz, writes the resulting visual phase into `_HectonVolumetricFogCompositeParams.w`, and uses it for shader temporal dither, flow advection, and mock light phase.

Cinematic Cheats used: Presentation phase quantization. At low quality, fog motion holds for multiple frames instead of recomputing unique temporal noise every frame; high quality keeps per-frame motion.

Exact Microseconds saved: PENDING PROFILER. Expected low-end benefit is reduced temporal churn in the proxy/short-march path; no measured claim until profiler access is legal.
