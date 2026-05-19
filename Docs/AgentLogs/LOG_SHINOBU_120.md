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
- Routed abyssal flow advection through SHINOBU's explicit visual phase after the later `_Time.y` purge.
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

## Loop 10 Compile-Wall Evidence - 2026-05-19

What was wrong: Compile proof was still missing after prior CPU/process guards blocked build launch. When the guard finally allowed a narrow Core build, compilation failed before SHINOBU validation because `Hecton8.Core.csproj` references absent `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.

What was done: Recorded the build command, failure mode, and ownership boundary in Status and Rationale. No SHINOBU runtime code was changed, and no World bridge file or csproj include was guessed, restored, or deleted from this domain.

Cinematic Cheats used: None. This pass is evidence hygiene only.

Exact Microseconds saved: PENDING PROFILER. No runtime measurement is legal while Core compile proof is blocked by the missing World source file.

Integrator note: compile-wall strike 1. The next legal validation step is for the World/build owner to restore the missing file or regenerate/fix `Hecton8.Core.csproj`; SHINOBU_120 should then rerun the guarded narrow build and Unity import/profiler checks.

<SELF_AUDIT_LOOP10>
<TASK_RECONCILIATION>
<TASK id="01" status="PASS_STATIC">Exact target prefab names were scanned; no SHINOBU-owned ambient MarineSnow/SiltDust/DeepSeaParticles Shuriken owner remained to delete. Unrelated hazard/construction particles were not touched.</TASK>
<TASK id="02" status="PASS_STATIC">Fog route is a custom URP RenderGraph compute feature; no managed black-box post fog was added.</TASK>
<TASK id="03" status="PASS_STATIC">VolumetricFogParamsDTO and related DTOs expose raw public fields. No hot-path DTO properties were found in VolumetricFogContracts.cs.</TASK>
<TASK id="04" status="PASS_STATIC">Primary DTO is explicit 64B with offsets 0, 16, 32, 48 and layout validation through UnsafeUtility/Marshal.</TASK>
<TASK id="05" status="PASS_STATIC">BuildMockVolumetricLightsJob has exact Burst flags and [NoAlias]; the render feature schedules it with one-frame latency.</TASK>
<TASK id="06" status="PASS_STATIC">Hecton_VolumetricFog.compute implements depth raymarch, noise density, directional scattering, and PointLightDTO scattering.</TASK>
<TASK id="07" status="PASS_STATIC">Low quality resolves proxyBlend to 1.0 and returns before the raymarch loop with exponential fog plus Bayer/temporal dither.</TASK>
<TASK id="08" status="PASS_STATIC">Abyssal flow is sampled as a declared RenderGraph texture edge and driven by explicit visual phase, not hidden Unity _Time.</TASK>
<TASK id="09" status="PASS_BY_OWNER_SUBSTITUTION">Literal new 3D SiltBuffer was rejected. Existing MarineSnow fog-density/wake texture is the single owner route for silt/wake truth.</TASK>
<TASK id="10" status="PASS_STATIC">Persistent reduced/full RTHandles are imported into RenderGraph; no RenderTexture.GetTemporary path is present.</TASK>
<TASK id="11" status="PASS_STATIC">Ray steps scale continuously 4..64 from GlobalQualityWeight; shader breaks on opacity threshold.</TASK>
<TASK id="12" status="YELLOW_CONTRACT_BLOCKED">Direct BiomeTransitionManager/BiomeMatrix concrete access was removed. Current route is vault extinction profiles/depth bands until an unmanaged biome DTO/signal exists.</TASK>
<TASK id="13" status="PASS_STATIC">C# sends 256m wrapped local float3 noise offset in FlowAdvection.xyz; shader never receives absolute double3 AUP.</TASK>
<TASK id="14" status="PASS_STATIC">SHINOBU buffers are visual/vault/editor only; no StateRingBuffer, Merkle, or rollback authority wiring was added.</TASK>
<TASK id="15" status="PASS_STATIC">Vault buffers use UninitializedMemory; GPU params/lights use persistent GraphicsBuffers and LockBufferForWrite.</TASK>
<TASK id="16" status="YELLOW_MEASUREMENT_BLOCKED">300-entry telemetry ring and direct native dump exist; GPU timing is estimated until Unity profiler/GPU query proof is available.</TASK>
<TASK id="17" status="PASS_STATIC">UI Toolkit tuner mutates vault DTOs and loads CSV through vault scratch; graph remains editor-side telemetry.</TASK>
<TASK id="18" status="PASS_BY_FIXED_TABLE_SUBSTITUTION">CSV parser uses ReadOnlySpan<byte>, FNV-1a, and writes directly into the fixed vault profile table. NativeHashMap was rejected because 16 fixed profiles do not need a dynamic map or temp copy.</TASK>
<TASK id="19" status="PASS_STATIC">Shader heatmap blends by executed-step ratio; no CPU readback debug path was introduced.</TASK>
<TASK id="20" status="PASS_LOGGED_STATIC">Self-audit exists on disk; runtime readiness remains blocked by compile/import/profiler evidence.</TASK>
</TASK_RECONCILIATION>
<STRUCT_LAYOUT_VERIFICATION>
VolumetricFogParamsDTO: offset 0 FogColorAndDensity float4 16B; offset 16 ScatteringParams float4 16B; offset 32 FlowAdvection float4 16B; offset 48 QualityAndLimits float4 16B. Total 64B, 64 % 16 = 0, no Pack=1.
PointLightDTO: offset 0 PositionRadius float4 16B; offset 16 ColorIntensity float4 16B. Total 32B, 32 % 16 = 0.
VolumetricFogTelemetryEntry: explicit 64B. Frame/RaySteps/Scale/Usec occupy 0..15, CameraPositionLocalAndQuality 16..31, StateHash/Flags/Density/Distance 32..47, DebugValues 48..63. Single writer, no atomic counter.
WaterExtinctionProfileDTO: explicit 64B. Hash/depth/density 0..15, AbsorptionAndScatter 16..31, BiomeWeights 32..47, Reserved 48..63.
</STRUCT_LAYOUT_VERIFICATION>
<SCALABILITY_CURVE_EXPLANATION>
GlobalQualityWeight feeds internal resolution scale, ray steps, point-light count, proxyBlend, visual phase cadence, and shader octave count. Below roughly 0.3 the path collapses toward analytic exponential fog, Bayer/temporal dither, lower internal scale, fewer lights, and slower visual phase cadence. At proxyBlend >= 0.999 the raymarch loop is not entered. At high weight the same topology grows toward 64 steps, more point lights, 4-octave FBM, flow advection, wake-density sampling, directional scattering, and the debug heatmap route.
</SCALABILITY_CURVE_EXPLANATION>
<H_PHI_VAULT_STATUS>
Runtime feature owns zero private persistent NativeArray/NativeList/NativeHashMap fields. Vault handles requested at boot/render activation: ShinobuVolumetricFogParams=71130, ShinobuVolumetricFogPointLights=71131, ShinobuVolumetricFogTelemetryRing=71132, ShinobuVolumetricFogExtinctionProfiles=71133. Editor CSV scratch route uses ShinobuVolumetricFogCsvScratch=71134.
</H_PHI_VAULT_STATUS>
<POINTER_ALIASING_DEPENDENCY_GRAPH>
BuildMockVolumetricLightsJob consumes no upstream JobHandle in the current RenderGraph facade and outputs _mockLightsJobHandle. The pass checks IsCompleted before Complete(), uploads prior-frame lights to the inactive GraphicsBuffer, flips active buffer, then schedules the next job. [NoAlias] is present on the PointLights NativeArray field. RenderGraph dependencies are declared with UseTexture for source/depth/half/composite/MarineSnow/AbyssalFlow and UseBuffer for params/point lights.
</POINTER_ALIASING_DEPENDENCY_GRAPH>
<COMPILE_GUARD>
No direct sibling-domain concrete using remains in SHINOBU runtime/editor files. Current compile proof is blocked before SHINOBU validation because Hecton8.Core.csproj references missing Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs. SHINOBU did not mutate that World dependency.
</COMPILE_GUARD>
<DEAR_LIE_CONFIRMATION>
The fake is ordered screen-space dither plus 1D exponential fog at low quality and shader-side noise/wake sampling at higher quality. Before a literal silt/wake fluid owner, complexity would be O(volume cells + wake injectors) and cross-domain coupled to submarine motion. After SHINOBU's route, low quality is O(pixels), while high/ultra is O(pixels * selected ray steps) with selected ray steps continuously capped at 64.
</DEAR_LIE_CONFIRMATION>
<VERIFICATION>
Static grep found no RenderTexture.GetTemporary, Blit, SetData/GetData, File.ReadAllBytes, File.WriteAllBytes, new byte[], NativeHashMap, GetValueArray, Allocator.Temp, using Hecton8.Environment, FindObjectOfType, OnGUI, Pack=, hot DTO properties, or _Time usage in SHINOBU runtime/editor/shader files. git diff --check is clean apart from CRLF warnings on docs. dotnet build was attempted once under guard and failed on the missing World file; profiler and Unity import proof remain absent.
</VERIFICATION>
</SELF_AUDIT_LOOP10>
## 2026-05-19 - Loop 11 Bandwidth/Layout/Variant Polish

What was wrong -> SHINOBU still had three small but real architectural leaks: layout validation repeated `Marshal.OffsetOf` from render/editor validation calls, the 64B fog CBuffer was uploaded every frame with no page dirty proof, and `Hecton_VolumetricFog.compute` declared `_MATH_LOD_LOW/_MATH_LOD_HIGH` variants even though the shader never branched on them.

What was done -> Cached `VolumetricFogNativeLayout.Validate()` through a static bool, added `VolumetricFogParamsDTO` hash/equality dirty gating before `LockBufferForWrite`, reset that gate on CBuffer recreation/teardown, and removed the unused compute `multi_compile` line. The active quality route remains the continuous `GlobalQualityWeight` DTO.

Cinematic Cheats used -> No new simulation. This pass preserves the existing Dear Lie path: dithered analytic proxy below the low-quality curve and 4..64 raymarch steps only when the quality scalar buys the cost.

Exact Microseconds saved -> PENDING PROFILER. Static cost removed: repeated reflection calls after type init, redundant 64B CBuffer map/unmap on unchanged fog state, and dead SHINOBU compute variants during warmup/variant handling.

Compile proof -> Not rerun. Existing guarded build is still blocked outside SHINOBU by missing `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` referenced by `Hecton8.Core.csproj`.

## 2026-05-19 - Loop 12 Point-Light Dirty Page

What was wrong -> The CBuffer gained a dirty gate, but the 8-entry point-light structured buffer still uploaded every completed mock-light job. At low quality, visual phase is quantized, so identical synthetic light pages can repeat for several frames.

What was done -> Added a hash over active point-light count and all uploaded `PointLightDTO` float4 lanes. If the page hash/count matches the previous upload, SHINOBU skips `LockBufferForWrite`, skips inactive-buffer upload, and keeps the current active GPU buffer.

Cinematic Cheats used -> The mock lights remain visual-only proxy shafts generated near the camera. No physical light solver, no Unity light components, and no CPU scene query were introduced.

Exact Microseconds saved -> PENDING PROFILER. Static cost removed on unchanged light pages: one structured-buffer map/unmap, one 8 * 32B copy, and one buffer flip.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

## 2026-05-19 - Loop 13 AUP-Local Camera Route

What was wrong -> Fog DTO/job inputs still trusted raw camera transform position/forward. That is presentation space; SHINOBU needed an origin-owner route without importing World `AbsoluteUniversePosition` into the Visor runtime.

What was done -> Resolved camera position through a Core floating-origin offset snapshot: runtime float3 -> camera AUP double3 -> subtract same committed offset -> local float3. Camera forward is finite-normalized before entering the mock-light Burst job.

Cinematic Cheats used -> The shader still receives local/wrapped presentation data, not absolute doubles. The fog remains a camera-local visual facade, not a simulation owner.

Exact Microseconds saved -> PENDING PROFILER. This pass buys precision safety, not speed; added cost is one double3 add/subtract and one guarded normalize per rendered camera frame.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

## 2026-05-19 - Loop 14 Quality Continuum Math Authority

What was wrong -> The fog feature already used a continuous `GlobalQualityWeight`, but several authority curves were still expressed with `Mathf` helpers. That made the mandated `math.lerp/math.step` proof weaker than the shader and docs demanded.

What was done -> Added a shared cubic quality curve and moved ray-step selection, internal scale, proxy blend, point-light count, visual phase cadence, setup clamping, and GPU-usec estimation onto `math.saturate`, `math.lerp`, and an explicit `math.step` low-quality survival floor.

Cinematic Cheats used -> The low path remains a dithered analytic exponential proxy. The patch makes that collapse explicit: weak devices remain on the cheap proxy until the quality scalar polynomial releases cost back into raymarching.

Exact Microseconds saved -> PENDING PROFILER. This patch is correctness/audit hardening for the scalability law, not a measured speed claim.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

## 2026-05-19 - Loop 15 Finite Quality Scalar Vaccination

What was wrong -> `math.saturate` was being treated as enough protection for quality control, but invalid scalar input can still poison rounding, visual cadence, resource sizing, or estimate math if NaN/infinity reaches those paths.

What was done -> Added `ResolveFiniteSaturated` and routed quality/scale inputs through it before polynomial curves, `math.lerp`, `math.step`, point-light count, visual phase cadence, and GPU-usec estimation. Invalid editor internal-scale endpoints now fall back to 0.25/0.67 before RTHandle sizing.

Cinematic Cheats used -> Invalid quality now fails toward the minimum-survival dithered proxy. That keeps the ocean readable and cheap instead of letting one bad scalar collapse the frame into undefined GPU parameters.

Exact Microseconds saved -> PENDING PROFILER. This is NaN containment, not a speed claim; expected extra scalar checks are below profiler noise.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

## 2026-05-19 - Loop 16 Shader FBM Octave Collapse

What was wrong -> `ResolveFogDensity` always evaluated a second fine FBM branch once the proxy path released, so the advertised 1-to-4 octave quality ramp still paid avoidable low/middle ALU.

What was done -> The shader now evaluates coarse FBM first and only evaluates the fine-detail FBM when `GlobalQualityWeight` ramps past 0.35. Fine detail blends in continuously, so the visual transition is scalar-driven instead of a hardware mode.

Cinematic Cheats used -> Low quality keeps noisy water through Bayer/proxy or single-FBM fog; high quality buys richer particulate turbulence by spending ALU only when the quality scalar permits it.

Exact Microseconds saved -> PENDING PROFILER. Static saving: one FBM evaluation avoided per ray step in the low/middle raymarch band.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

## 2026-05-19 - Loop 17 External Shader-Global Snapshot

What was wrong -> Telemetry repeated MarineSnow/AbyssalFlow global shader queries after the same frame already queried those globals for RenderGraph resource import and compute binding.

What was done -> The render pass now captures producer globals once per RenderGraph record and passes pre-read MarineSnow/flow-active booleans into telemetry. The black-box flags and graph binding now describe the same external snapshot.

Cinematic Cheats used -> No new simulation. This keeps the wake/silt path as a MarineSnow-owned visual density fake while reducing duplicate global-state bookkeeping.

Exact Microseconds saved -> PENDING PROFILER. Static saving: two global shader queries removed from telemetry per camera frame.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

## 2026-05-19 - Loop 18 Editor Facade NaN Quarantine

What was wrong -> The UI Toolkit tuner mutates the same vault DTO consumed by the runtime fog feature, but its slider paths could still pass NaN/infinity through `math.clamp`/`math.saturate`.

What was done -> Added finite fallback clamps in the tuner. Slider display, default quality seeding, and density/scatter/extinction/anisotropy/flow/quality writes now collapse invalid values to SHINOBU defaults before mutating `VolumetricFogParamsDTO`.

Cinematic Cheats used -> No new simulation. The facade preserves designer control without allowing the control surface to poison the shader.

Exact Microseconds saved -> PENDING PROFILER. Editor-only safety; no player hot-path speed claim.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

<SELF_AUDIT_LOOP18>
<TASK_RECONCILIATION>
<TASK id="01" status="PASS_STATIC">Ambient target ParticleSystem names were scanned; SHINOBU-owned marine snow/silt/deep sea particulate route is shader/compute, not Shuriken.</TASK>
<TASK id="02" status="PASS_STATIC">Custom URP RenderGraph compute feature owns volumetric fog; no standard Unity fog/post stack dependency was added.</TASK>
<TASK id="03" status="PASS_STATIC">Volumetric fog DTOs are raw-field unmanaged structs; runtime mutation uses vault-backed refs, no hot DTO properties.</TASK>
<TASK id="04" status="PASS_STATIC">VolumetricFogParamsDTO is explicit 64B with offsets 0/16/32/48 and cached layout validation.</TASK>
<TASK id="05" status="PASS_STATIC">Burst mock scattering lights exist with exact Burst flags, [NoAlias], deterministic math, scheduled one-frame latency, and dirty GPU upload.</TASK>
<TASK id="06" status="PASS_STATIC">Hecton_VolumetricFog.compute performs reduced-resolution depth raymarch, HG scattering, directional light, point-light buffer, flow and silt sampling.</TASK>
<TASK id="07" status="PASS_STATIC">Low scalar path uses analytic exponential fog plus Bayer/temporal dither and returns before the raymarch loop at proxyBlend >= 0.999.</TASK>
<TASK id="08" status="PASS_STATIC">Abyssal flow texture is a declared RenderGraph read edge and advects wrapped fog noise through explicit SHINOBU visual phase, not hidden _Time.</TASK>
<TASK id="09" status="YELLOW_OWNER_ROUTE">Wake/silt density is consumed from MarineSnow's authoritative fog-density texture, which owns dynamic wake buffers. A duplicate SHINOBU 3D SiltBuffer remains rejected by one-owner authority unless integrator mandates a new route card.</TASK>
<TASK id="10" status="PASS_STATIC">Persistent RTHandles drive reduced-resolution raymarch and depth-aware composite; no per-frame temporary RT allocation or Blit path.</TASK>
<TASK id="11" status="PASS_STATIC">Ray steps scale 4..64 through math.lerp polynomial, low proxy bypass exists, opacity early-out exists, and shader fine FBM detail is quality-gated.</TASK>
<TASK id="12" status="YELLOW_CONTRACT_BLOCKED">Direct BiomeTransitionManager concrete access was removed; vault extinction profiles/depth bands are active until biome owner publishes unmanaged contract data.</TASK>
<TASK id="13" status="PASS_STATIC">Runtime passes only local/wrapped float3 noise offsets; Core floating-origin offset is used before narrowing, and shader never receives absolute double3 AUP.</TASK>
<TASK id="14" status="PASS_STATIC">Fog state is presentation-only and absent from rollback/Merkle state authority; visual drift is not gameplay truth.</TASK>
<TASK id="15" status="PASS_STATIC">Vault buffers use UninitializedMemory; GPU buffers are persistent; CBuffer and point-light pages use LockBufferForWrite plus dirty gates.</TASK>
<TASK id="16" status="YELLOW_MEASUREMENT_BLOCKED">300-entry telemetry ring and native-span dump exist; GPU usec remains estimated until Unity profiler/GPU query proof is possible.</TASK>
<TASK id="17" status="PASS_STATIC">UI Toolkit tuner mutates vault DTOs, uses finite clamps, and exposes graph/tuning without C# recompilation.</TASK>
<TASK id="18" status="PASS_STATIC">CSV parser uses ReadOnlySpan<byte>, fixed vault profile table, FNV hashes, and vault scratch buffer; no string.Split or temp NativeHashMap.</TASK>
<TASK id="19" status="PASS_STATIC">Heatmap debug route is shader-side by executed-step ratio; no CPU readback debug path.</TASK>
<TASK id="20" status="PASS_LOGGED_STATIC">Self-audit and loop reports are appended to this log; runtime/import/profiler proof remains blocked externally.</TASK>
</TASK_RECONCILIATION>
<STRUCT_LAYOUT_VERIFICATION>
VolumetricFogParamsDTO: offset 0 FogColorAndDensity float4 16B; offset 16 ScatteringParams float4 16B; offset 32 FlowAdvection float4 16B; offset 48 QualityAndLimits float4 16B; total 64B, 64 % 16 = 0.
PointLightDTO: offset 0 PositionRadius float4 16B; offset 16 ColorIntensity float4 16B; total 32B, 32 % 16 = 0.
VolumetricFogTelemetryEntry: FrameIndex/RaySteps/RenderScale/EstimatedUsec 0..15; CameraPositionLocalAndQuality 16..31; StateHash/Flags/Density/Distance 32..47; DebugValues 48..63; total 64B.
WaterExtinctionProfileDTO: ProfileHash/Depth/Density 0..15; AbsorptionAndScatter 16..31; BiomeWeights 32..47; Reserved 48..63; total 64B.
</STRUCT_LAYOUT_VERIFICATION>
<SCALABILITY_CURVE_EXPLANATION>
GlobalQualityWeight is finite-saturated before use. It drives internal RT scale, ray steps, proxyBlend, point-light count, visual phase cadence, shader octave count, and fine-FBM detail. Below the survival band, the shader writes dithered analytical exponential fog and bypasses the raymarch loop. In the low/middle raymarch band, the shader uses coarse FBM only; above 0.35 quality, fine particulate FBM fades in continuously. High/ultra reaches 64 steps, more point lights, directional scattering, flow advection, MarineSnow density, and heatmap diagnostics.
</SCALABILITY_CURVE_EXPLANATION>
<H_PHI_VAULT_STATUS>
Runtime feature owns zero private persistent NativeArray/NativeList/NativeHashMap fields. Vault handles: ShinobuVolumetricFogParams=71130, ShinobuVolumetricFogPointLights=71131, ShinobuVolumetricFogTelemetryRing=71132, ShinobuVolumetricFogExtinctionProfiles=71133. Editor CSV scratch: ShinobuVolumetricFogCsvScratch=71134.
</H_PHI_VAULT_STATUS>
<POINTER_ALIASING_DEPENDENCY_GRAPH>
BuildMockVolumetricLightsJob consumes no upstream JobHandle in the current facade and outputs _mockLightsJobHandle. The pass checks IsCompleted before Complete(), uploads prior-frame point-light pages only when their hash/count changed, then schedules the next job. [NoAlias] is present on the PointLights NativeArray. RenderGraph declares texture reads/writes and params/point-light buffer reads with UseTexture/UseBuffer.
</POINTER_ALIASING_DEPENDENCY_GRAPH>
<COMPILE_GUARD>
No direct sibling runtime using remains for Environment or World. The SHINOBU runtime depends on Core/VFX contracts only. Compile proof remains blocked before SHINOBU validation by missing non-owned World source Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs.
</COMPILE_GUARD>
<DEAR_LIE_CONFIRMATION>
The volumetric truth is an optical fake: Bayer/procedural dither plus analytic exponential fog at low quality, screen-space MarineSnow density for wake/silt, and shader-side FBM/flow advection at higher quality. Rejected CPU fluid/Silt owner complexity would be O(volume cells + wake emitters) and cross-domain. Active SHINOBU low path is O(pixels); high path is O(pixels * selected ray steps) capped at 64 with opacity early-out.
</DEAR_LIE_CONFIRMATION>
<VERIFICATION>
Latest static checks: git diff --check returns only CRLF warnings; bad-pattern grep remains clear for temp RT/blit/SetData/GetData/File.ReadAllBytes/new byte[]/NativeHashMap/Allocator.Temp/Pack=/_Time/multi_compile/direct Environment/World using. Build/profiler proof remains blocked by the non-SHINOBU missing World source.
</VERIFICATION>
</SELF_AUDIT_LOOP18>

## 2026-05-19 - Loop 19 Runtime DTO Boundary Sanitization And Local Noise

What was wrong -> Runtime SHINOBU still trusted several serialized/runtime lanes after editor hardening: fog color/settings, vault override color, CSV profile ranges, profile absorption/scatter, debug heatmap, and estimated GPU timing could still carry NaN/infinity into the DTO, telemetry, or shader frame constants. The shader FBM coordinate used `samplePositionWS`, which is the wrong precision surface for high-frequency particulate noise in a 100km world.

What was done -> Added finite fallback clamps to the runtime DTO boundary and profile application path. Invalid estimated GPU time is now flagged in telemetry bit 16 and stored as a finite `0` before the dump path writes the ring. The shader now computes density noise from `samplePositionLocal + _HectonVolumetricFogFlowAdvection.xyz`, while preserving world/sample coordinates only for AbyssalFlow and point-light scattering.

Cinematic Cheats used -> The fog remains a visual fake: low scalar still uses analytical dithered exponential fog, and high scalar uses camera-local FBM plus flow advection. This avoids CPU fluid truth and avoids large-world noise shimmer without inventing a new simulation owner.

Exact Microseconds saved -> PENDING PROFILER. This is correctness/precision hardening; static expectation is preventing invalid DTO/telemetry writes and avoiding large-coordinate FBM instability, not a measured frame-time reduction.

Compile proof -> Not rerun. External missing World source still blocks Core compile before SHINOBU validation.

<SELF_AUDIT_LOOP19_DELTA>
<TASK_RECONCILIATION_DELTA>
<TASK id="04" status="PASS_STATIC_DELTA">Runtime DTO writes now finite-clamp settings, vault overrides, CSV profile lanes, and telemetry values before hitting the 64B params page.</TASK>
<TASK id="12" status="YELLOW_CONTRACT_BLOCKED_DELTA">Depth/profile blending remains the legal fallback. Profile values are now finite-sanitized; direct biome-owner read is still blocked until an unmanaged owner route exists.</TASK>
<TASK id="13" status="PASS_STATIC_DELTA">Shader FBM coordinates now use local ray travel plus wrapped camera offset, not raw `samplePositionWS`, preserving AUP-local precision for high-frequency fog noise.</TASK>
<TASK id="16" status="YELLOW_MEASUREMENT_BLOCKED_DELTA">Telemetry still uses estimates, but invalid estimate rows are flagged and stored finite before binary dump.</TASK>
</TASK_RECONCILIATION_DELTA>
<STRUCT_LAYOUT_VERIFICATION_DELTA>No layout change. Primary DTO remains 64B at offsets 0/16/32/48. PointLightDTO remains 32B. Telemetry and extinction profile DTOs remain 64B.</STRUCT_LAYOUT_VERIFICATION_DELTA>
<SCALABILITY_CURVE_DELTA>No new tier switch. The same `GlobalQualityWeight` curves drive proxy, ray steps, internal scale, point-light count, visual cadence, and shader detail.</SCALABILITY_CURVE_DELTA>
<H_PHI_VAULT_DELTA>No private NativeArray/NativeHashMap/List fields were added. Existing vault handles remain unchanged.</H_PHI_VAULT_DELTA>
<POINTER_ALIASING_DELTA>No job layout change. `[NoAlias]` remains on `BuildMockVolumetricLightsJob.PointLights`; RenderGraph buffer/texture declarations unchanged.</POINTER_ALIASING_DELTA>
<COMPILE_GUARD_DELTA>No sibling Runtime using was added. `rg` remained clear for direct `Hecton8.World` / `Hecton8.Environment` imports in SHINOBU files.</COMPILE_GUARD_DELTA>
<DEAR_LIE_DELTA>Noise precision was repaired in the shader fake itself; no CPU fluid, wake solver, or new silt owner was introduced.</DEAR_LIE_DELTA>
<VERIFICATION_DELTA>`git diff --check` returned only CRLF warnings. Bad-pattern grep remained clear for temp RT, blit, SetData/GetData, File.ReadAllBytes, new byte[], NativeHashMap, Allocator.Temp, Pack=, `_Time`, `multi_compile`, and direct sibling-domain using.</VERIFICATION_DELTA>
</SELF_AUDIT_LOOP19_DELTA>
