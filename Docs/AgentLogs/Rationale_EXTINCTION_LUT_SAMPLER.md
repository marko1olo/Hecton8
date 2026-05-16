# Rationale_EXTINCTION_LUT_SAMPLER

State: CORE TASKS IMPLEMENTED; FINAL PROJECT COMPILE BLOCKED BY EXISTING CROSS-DOMAIN ERRORS

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
