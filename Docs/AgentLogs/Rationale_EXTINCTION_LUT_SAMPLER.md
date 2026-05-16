# Rationale_EXTINCTION_LUT_SAMPLER

State: FIFTH PASS COMPLETE; EXTINCTION PATH IMPLEMENTED; ANDROID STREAMINGASSETS URI STAGING ADDED; UNUSED EXTINCTION SAMPLER REMOVED; DOTNET CORE COMPILE GREEN; UNITY PLAYER/PROFILER VALIDATION PENDING

## Decision 0: Mandate Selection
Problem: The task crosses shader color, C# asset loading, AUP depth semantics, and quality tiers.
Solution: Use REND_Shader_Noir_Aesthetics_Dithering_Fog, OPT_Zero_GC_Policy_AllocFree_Mandate, REND_URP_Graphics_HotPath_Optimization_HLOD, and MATH_Coordinate_Precision_AUP_FloatingOrigin as governing mandates.
Rejected Alternatives: Reading unrelated AI/physics registries would waste context and create cross-domain contamination.
Scalability potential: Low uses one vertex LUT sample; Middle/High use stronger per-pixel response; Ultra can reuse the same helper for volumetric shafts.
Hardware Impact: Expected LOW/MX350 cost is one LUT sample per vertex plus interpolator, avoiding broad per-pixel bandwidth.

## Decision 1: Visual Fake First
Problem: Physical underwater spectral extinction could become a raymarch/simulation problem.
Solution: Treat extinction as a globally bound LUT sampled by depth/turbidity in shader, not a simulated light transport system.
Rejected Alternatives: Per-light volumetric truth and CPU particle/medium simulation are too slow and outside prompt scope.
Scalability potential: Toaster path stays LUT-only; high-end path spends saved cycles on per-pixel fog and light shaft coloring.
Hardware Impact: Estimated LOW saving versus 8-step raymarch: 80-250 microseconds per fullscreen pass on i3/MX350 class hardware, pending profiler proof.

## Decision 2: Packed 2D R16F LUT
Problem: Water_Extinction_Matrix.bin is a 256x256x256 half-float volume, but Unity runtime Texture3D support and memory paths vary by target hardware.
Solution: Use the documented packed 4096x4096 R16F layout, validate the texel count with GetRawTextureData<half>(), and compute flat index in HLSL as ((depth*256)+turbidity)*256+wavelength.
Rejected Alternatives: Texture3D import pipeline, CPU lookup tables, and per-material upload all increase dependency and runtime cost.
Scalability potential: Low samples the same packed texture once per vertex; Middle/High sample per pixel; Ultra reuses the same helper in volumetric shafts.
Hardware Impact: LOW/MX350 expected saving versus Texture3D emulation/copy path: 20-80 microseconds/frame, pending profiler proof. Memory stays a single 32 MiB R16 texture when supported.

## Decision 3: Loader Placement
Problem: The first loader version lived under Hecton8.Graphics.Materials.asmdef, which is autoReferenced:false and weakens runtime bootstrap certainty.
Solution: Move LutArrayResolver to Assets/_Project/Scripts/Rendering under the existing core rendering bridge area so RuntimeInitializeOnLoadMethod is compiled with the core runtime path.
Rejected Alternatives: Adding an asmdef reference from Core to Graphics.Materials risks a cycle because Graphics.Materials already references Core; reflection bootstrap adds string fragility.
Scalability potential: Cold load path is stable across tiers; device tier only changes texture format precision.
Hardware Impact: No frame impact. Startup performs one explicit 32 MiB read and one texture upload; no per-frame GC.

## Decision 4: Fog Color Authority
Problem: Legacy RenderSettings.fogColor writes created split authority against the shader/post extinction path.
Solution: Remove runtime fog color assignments from HectonUnderwaterVisuals and HectonCelestialEngine while keeping fog mode/density and lifecycle restore reads intact.
Rejected Alternatives: Keeping color writes would fight the Beer-Lambert post stack; deleting lifecycle restore would break scene state ownership.
Scalability potential: Low/Middle/High all use one shader color authority; Ultra can enrich fog color without C# RenderSettings races.
Hardware Impact: Estimated CPU noise removed: 2-8 microseconds per affected camera/update path; main gain is deterministic visual authority.

## Decision 5: Turbidity Signal Adaptation
Problem: The prompt names WeatherStateSignal(Turbidity), but local weather state does not expose that exact signal in the rendering path.
Solution: Feed LUT Y shift through existing decoupled shader globals: HectonUnderwaterVisuals publishes current turbidity; GlobalWeatherDirector publishes a weather-intensity turbidity shift.
Rejected Alternatives: Inventing a new WeatherStateSignal or direct-coupling the shader path to weather internals violates the signal authority rule.
Scalability potential: Low uses the same scalar in vertex sampling; High/Ultra use it per pixel and in shaft compositing.
Hardware Impact: No extra allocation; global vector publish is already in existing visual/weather update surfaces.

## Decision 6: Quality Ladder
Problem: A single "balanced" underwater extinction path would either waste low-end bandwidth or undersell high-end visuals.
Solution: Low = vertex LUT sample; Middle = per-pixel UberNoir and post fog tint; High = per-pixel material plus volumetric shaft tint; Ultra = same path can be expanded with additional shaft/fog passes without changing data layout.
Rejected Alternatives: Always-per-pixel path, raymarching, or one global fog color are either too expensive or visually flat.
Scalability potential: Toaster path remains visually colored with minimal samples; RTX path gets sharper object depth response and colored shafts.
Hardware Impact: Low saves estimated 40-140 microseconds/frame versus per-pixel material sampling at 1080p; High/Ultra spend 15-70 microseconds/frame where the result is visible.

## Decision 7: Compile Wall Handling
Problem: dotnet build Hecton8.Core.csproj exits 1 due existing cross-domain dependency errors unrelated to extinction work.
Solution: Dump full build output to Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt, run a targeted Roslyn compile for LutArrayResolver, and mark final project validation blocked by dependency while preserving the implemented shader/loader work.
Rejected Alternatives: Claiming green verification, editing unrelated compile-wall domains, or reverting implemented code to hide unrelated failures.
Scalability potential: None; this is integration state tracking.
Hardware Impact: None.

## Decision 8: Omega Polish Result
Problem: Polish mandate bans emissive exemption branching and demands verified status, but the project compile is not green.
Solution: Keep emissive exemption branchless with a material mask and lerp(); mark project verification blocked instead of claiming a false master-grade build.
Rejected Alternatives: `if (emissive)` shader branch, new shader keyword branch, or false "VERIFIED MASTER GRADE" report despite 105 compile errors.
Scalability potential: Branchless mask scales across Low/Middle/High/Ultra without variant explosion.
Hardware Impact: Estimated branch-divergence avoidance: 0-8 microseconds/frame depending visible emissive coverage; exact measured value unavailable because Unity/profiler run is blocked by current compile wall.

## Decision 9: Steam Deck / Android LUT IO Pass
Problem: The original cold loader used full-file staging for a 33,554,432 byte LUT, which is acceptable on desktop SSD but hostile to Steam Deck MicroSD and managed-heap pressure.
Solution: Stream Water_Extinction_Matrix.bin sequentially through a 128 KiB scratch window into Texture2D.GetRawTextureData<byte>(), with the same chunk path feeding the ARGB32 fallback converter.
Rejected Alternatives: File.ReadAllBytes, per-frame streaming, UnityWebRequest for filesystem paths, or CPU-side spectral lookup tables.
Scalability potential: Low and High tiers share the same GPU texture; only the shader sampling frequency changes.
Hardware Impact: Startup allocation drops from one 32 MiB managed file buffer to one 128 KiB scratch buffer. Exact startup milliseconds are unmeasured in this shell; expected benefit is reduced MicroSD stall amplitude and lower GC heap pressure.

## Decision 10: Shader Global DataVault Bridge
Problem: HectonUnderwaterVisuals and GlobalWeatherDirector both wanted to publish extinction parameters, creating split shader-global authority.
Solution: Added DataVault-backed water-extinction runtime/weather slots in HectonShaderGlobalDataVaultBridge and routed both systems through that bridge.
Rejected Alternatives: Direct Shader.SetGlobalVector in every producer or inventing a new managed delegate/event for turbidity.
Scalability potential: Low samples vertex extinction from the same global state; High/Ultra consume per-pixel and shaft tint without extra producer coupling.
Hardware Impact: No measured frame delta; expected gain is deterministic state ownership and fewer duplicate global writes.

## Decision 11: Multiplatform Shader Constraints
Problem: Quest/Android/Metal cannot tolerate D3D-only shader assumptions or hidden compute limits in a shared water extinction path.
Solution: Kept extinction as TEXTURE2D plus LOAD_TEXTURE2D using integer packed coordinates; no compute shader, no thread groups, no RWTexture, no groupshared memory, and no DirectX-only syntax.
Rejected Alternatives: Texture3D-only path, compute prefilter, or raymarched spectral volume.
Scalability potential: Toaster path samples once per object; High/Ultra sample per-pixel and reuse the same packed LUT in post/shafts.
Hardware Impact: Thread-group risk is 0 because no compute dispatch exists. GPU cost remains texture bandwidth plus a small ALU index calculation.

## Decision 12: DataVault Bucketer Compile Repair
Problem: The build reached a hard wall because ModuloSimulationBucketer.cs was deleted while GameBootstrapper still referenced Hecton8.Core.Bucketing.ModuloSimulationBucketer.
Solution: Added a DataVault-resolved ModuloSimulationBucketer implementation that stores persistent tables in GlobalDataVault handles instead of private NativeArray fields, then included it in Hecton8.Core.csproj.
Rejected Alternatives: Restoring the old private-NativeArray implementation from HEAD or deleting the bootstrap service and leaving GlobalRegistry.SimulationBucketer null.
Scalability potential: Low tier remains one slow bucket per frame; high tier retains two active slow buckets and scheduled rebalance state through vault buffers.
Hardware Impact: Avoids reintroducing local persistent array ownership. Exact frame impact unmeasured; rebalance copy is bounded to the entity table and cadence-gated.

## Decision 13: Compile Wall Boundary
Problem: After clearing local shader/VFX/core-adjacent blockers, dotnet build still fails with 185 cross-domain errors in LockstepStateValidator, SubmarineFluidDynamics, EcosystemDirector, and SargassumMicroFaunaBoids.
Solution: Wrote the current build dump and stopped broad repairs under the 3-strike dependency rule; these errors require owners for determinism, submarine fluid, ecosystem, and fauna data-vault migrations.
Rejected Alternatives: Claiming VERIFIED MASTER GRADE, hiding the dump, or doing a blind mass rewrite outside the rendering/shader prompt.
Scalability potential: None for extinction; this is integration debt classification.
Hardware Impact: None. Compile validation remains blocked by external source errors, not by the extinction shader path.

## Decision 14: Extinction Parameter DataVault Slot
Problem: LutArrayResolver still directly wrote `_ExtinctionLUTParams`, `_ExtinctionLUTRuntime`, and `_ExtinctionLUTWeatherParams`, leaving split shader-vector authority after the first DataVault bridge pass.
Solution: Added a DataVault-backed water-extinction params slot in HectonShaderGlobalDataVaultBridge and routed LutArrayResolver success/fallback vector publishes through bridge methods. The resolver now owns the texture bind only; vector state goes through one bridge.
Rejected Alternatives: Keeping direct `Shader.SetGlobalVector` in the resolver or adding a managed event/delegate notification for one cold bootstrap publish.
Scalability potential: Low, Middle, High, and Ultra consume the same global vector lane; only shader sample frequency changes by tier.
Hardware Impact: No measured frame delta. Expected effect is ownership determinism and avoidance of duplicate global vector writes; hot path remains zero managed allocation.

## Decision 15: Biome Fog NativeArray Eviction
Problem: HectonUnderwaterVisuals owned six Persistent NativeArrays for a one-lane biome fog transition job, violating the DataVault sovereignty rule in a rendering visual system.
Solution: Added dedicated BufferID values and replaced component-owned arrays with `VaultBufferHandle<T>` fields. The component resolves NativeArray views only at schedule/commit boundaries and the vault owns allocation, generation, and disposal. Added `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to BiomeTransitionSample, BiomeTransitionFogSource, and BiomeTransitionFogResult for ARM/Quest layout stability.
Rejected Alternatives: Leaving component-owned Persistent NativeArrays because they were small, or removing the Burst job and doing managed blending in Tick.
Scalability potential: Low keeps the same one-lane fake fog blend with no private allocation owner; High/Ultra can expand visual-family fog sources through vault capacity without changing the component lifecycle.
Hardware Impact: Runtime visual math cost is intended to stay unchanged. Memory ownership moves to GlobalDataVault; expected CPU gain is 0 us, expected stability gain is fewer private native lifetimes and better ARM layout predictability.

## Decision 16: Latest Compile Wall Boundary
Problem: After the rendering DataVault polish, the latest dotnet build dump fails with 23 errors outside the extinction files: EcosystemRuntimeInstaller references a missing Hecton8.AI.Ecosystem namespace and SubmarineFluidDynamics references missing VaultNativeBuffer<>.
Solution: Wrote the latest dump to Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt and classified it as external to this rendering prompt. Static scans show no latest build errors in HectonUnderwaterVisuals, LutArrayResolver, HectonShaderGlobalDataVaultBridge, BiomeTransitionFogBlendJobs, or H8Memory.
Rejected Alternatives: Claiming VERIFIED MASTER GRADE, reverting another agent's ecosystem/submarine migration, or editing SubmarineFluidDynamics from a shader/extinction prompt without ownership.
Scalability potential: None for extinction; this is integration boundary tracking.
Hardware Impact: None. Current build failure blocks Unity/player validation, not the shader hot path itself.

## Decision 17: Android StreamingAssets URI Staging
Problem: The cold LUT resolver skipped non-filesystem StreamingAssets paths. On Android/Quest, StreamingAssets can resolve to a `jar:`/URL-style location, which meant the LUT would only load if a persistent-data copy already existed.
Solution: Added a URL-style StreamingAssets staging path that uses UnityWebRequest with DownloadHandlerFile to copy the matrix into `Application.temporaryCachePath/Hecton8/WaterExtinction/Water_Extinction_Matrix.bin`, validates the exact 33,554,432 byte count, then streams that cached file through the existing 128 KiB scratch buffer into Texture2D.GetRawTextureData<byte>().
Rejected Alternatives: DownloadHandlerBuffer and downloadHandler.data were rejected because they recreate the 32 MiB managed heap staging failure. Per-frame Addressables/async loading was rejected because this prompt requires a cold global bind before gameplay shader use. Keeping the old skip behavior was rejected because it leaves Quest/Android dependent on an undocumented pre-copy.
Scalability potential: Low/MX350/Quest uses the same single packed R16/ARGB32 fallback texture and vertex-sampled LOW path; High/Ultra keep per-pixel and shaft tint without a new data layout.
Hardware Impact: Hot-path cost stays 0 us. Android first boot now pays a bounded cold APK-to-cache copy plus the existing chunked texture upload; managed peak remains a 128 KiB scratch buffer rather than a 32 MiB matrix byte array. Exact cold milliseconds are unmeasured in this shell.

## Decision 18: Compile Revalidation
Problem: The prior status recorded an external compile wall, but the disk state changed under parallel-agent work and needed fresh evidence.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` and wrote the output to Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt. Latest result: Build succeeded, 0 warnings, 0 errors.
Rejected Alternatives: Leaving stale BLOCKED status after the wall cleared, or claiming Unity/player/runtime validation from a C# compile.
Scalability potential: None directly; this validates the code path can compile into the current core assembly.
Hardware Impact: None. Unity shader import, Android device APK staging, RenderDoc, GCMonitor, and player build remain unmeasured.

## Decision 19: Extinction Sampler Binding Cleanup
Problem: `Hecton_WaterExtinction.hlsl` declared `SAMPLER(sampler_ExtinctionLUT)` even though the LUT path samples with integer `LOAD_TEXTURE2D`. On constrained shader backends, unused sampler declarations are needless binding surface and confuse platform audits.
Solution: Removed the unused sampler declaration. Kept `_ExtinctionLUT` as a texture-only global and retained integer packed-coordinate loads.
Rejected Alternatives: Leaving the unused sampler because the compiler probably strips it, or switching to filtered SAMPLE_TEXTURE2D and adding sampler dependency back into the path.
Scalability potential: Low/Middle/High/Ultra keep the same LUT data and sample cadence; this is binding hygiene, not a visual change.
Hardware Impact: Expected runtime delta is 0 us; potential benefit is avoiding an unnecessary sampler binding on mobile/Metal backends. Domain thread-group sweep found max `numthreads` product 512, below the 1024 limit.
