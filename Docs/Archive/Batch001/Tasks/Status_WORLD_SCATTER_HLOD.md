# WORLD_SCATTER_HLOD Status

Agent: FOVEATED_CULLING_MASTER
Domain: ECHELON 2 WORLD GENERATION & TERRAIN - BRG Scatter Director
Batch Source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION

## Mandates Loaded
- REND_Foveated_Simulation_LOD.txt
- REND_GPU_Occlusion_Culling_6000.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_GPU_Sovereignty.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- MATH_Rsqrt_i3_SIMD.txt

## Checklist
- [x] 1. SQUARED-DISTANCE CULLING | HLSL uses dot(diff,diff) for planar/full distance; no length() in scatter cull path | DOD: squared-distance law | Alternative rejected: length()/sqrt exact distance | Estimate: 35-90us saved per 16k candidates on MX350, PENDING VERIFICATION
- [x] 2. EARLY REJECT KERNEL | Planar squared-distance/dither reject stays before terrain UV, height sample, clip matrix, Hi-Z, and normal sampling | DOD: reject cheapest first | Alternative rejected: sample height before range gate | Estimate: 80-180us saved in sparse visibility frames, PENDING VERIFICATION
- [x] 3. FOVEATED UPDATE MASK | Compute shader resolves clip UV and uses squared center radius with ((frame+instance)&3) cache cadence outside 0.4 center | DOD: foveated cached visibility | Alternative rejected: CPU foveated lists | Estimate: 120-260us GPU work avoided in peripheral scatter, PENDING VERIFICATION
- [x] 4. CONSTANT NORMAL-Y SLOPE REJECTION | CPU clamps normal Y >= 0.8660254 and uploads _HectonScatterMinNormalYSq; shader uses squared comparison | DOD: no trig slope gate | Alternative rejected: Mathf.Acos/angle slope tests | Estimate: 20-60us saved per 16k candidates, PENDING VERIFICATION
- [x] 5. DITHERED RADIUS CULLING | Far edge uses deterministic 8x8 blue-noise-style threshold over squared distance band | DOD: visual fake dissolve | Alternative rejected: hard pop at far plane | Estimate: visual overdraw reduction PENDING VERIFICATION
- [x] 6. HI-Z OCCLUSION CULLING | Compute samples previous-frame depth pyramid using projected 8-corner bounds rect | DOD: GPU Hi-Z reject before normal sampling | Alternative rejected: CPU occlusion queries | Estimate: 0.10-0.45ms overdraw saved in blocked coral fields, PENDING VERIFICATION
- [x] 7. BATCH RENDERER GROUP / INDIRECT RENDERING | Scatter path submits through Graphics.RenderMeshIndirect with append visible indices | DOD: GPU indirect submission | Alternative rejected: DrawMeshInstanced/Object.Instantiate scatter proxies | Estimate: 0.20-0.70ms CPU submission saved, PENDING VERIFICATION
- [x] 8. INDIRECT ARGS LOCK-BUFFER | GPUScatterDirector and ScatterGPUIBackend write indirect args through GraphicsBuffer.LockBufferForWrite | DOD: no managed SetData args upload | Alternative rejected: one-element managed array SetData | Estimate: 5-20us CPU + GC-risk removed, PENDING VERIFICATION
- [x] 9. STAGGERED FRUSTUM UPDATE | GenerateScatterInstances updates one quadrant per frame and compaction reads the full visibility cache | DOD: 4-frame cull amortization with cache | Alternative rejected: all-candidate cull update every frame | Estimate: 0.18-0.55ms GPU spike reduction, PENDING VERIFICATION
- [x] 10. PERIPHERAL CAMERA DOT | Shader uses uploaded Transform.forward as normalized and compares dot^2 against distanceSq without per-thread forward length | DOD: normalized camera contract | Alternative rejected: normalize camera forward per thread | Estimate: 25-70us per 16k candidates, PENDING VERIFICATION
- [x] 11. PRECOMPUTED BOUNDS LUT | C# uploads a 16-entry float4 species bounds LUT through LockBufferForWrite; shader indexes by species | DOD: no Mesh.bounds in hot path | Alternative rejected: per-frame mesh bounds reads | Estimate: CPU alloc risk removed, PENDING VERIFICATION
- [x] 12. DEPTH-DERIVATIVE EDGE REJECTION | Projected radius gate rejects sub-4x4 pixel scatter before frustum/Hi-Z/normal work | DOD: screen-space size cull | Alternative rejected: draw all tiny scatter into fog | Estimate: 0.05-0.20ms fragment/vertex saved in distance bands, PENDING VERIFICATION
- [x] 13. WIND SWAY ALU | Vertex shader applies sine-parabola sway modulated by _AbyssalFlowWeatherCurrent magnitude | DOD: visual fake motion | Alternative rejected: CPU/physics plant sway | Estimate: CPU 0us added, GPU visual cost PENDING VERIFICATION
- [x] 14. SARGASSUM DRAG EXPORT | Compute compaction writes 64-bin _HectonScatterDensityBins and exposes TryGetSargassumDragDensityBuffer | DOD: GPU 1D density export | Alternative rejected: CPU density rebuild/readback in cull loop | Estimate: cross-system drag data without hot CPU allocation, PENDING VERIFICATION
- [x] 15. TEXTURE ATLASING FOR SCATTER | Instance AtlasFlow packs UV scale/offset for 4x4 atlas species selection in the indirect shader | DOD: per-instance atlas offset | Alternative rejected: per-species material clones | Estimate: SetPass/material count reduction PENDING VERIFICATION
- [x] 16. MOD MATRIX STAGING | Mod matrix staging remains NativeArrayOptions.UninitializedMemory and the scatter path avoids zero-fill ClearMemory staging | DOD: fully overwritten uninitialized NativeArray staging | Alternative rejected: managed arrays or cleared NativeArray staging | Estimate: 10-35us CPU zero-fill avoided per build/update pass, PENDING VERIFICATION
- [x] 17. PRECOMPUTE UPLOADS | CPU uploads _HectonScatterMinNormalYSq, density params, and the species bounds LUT as constants/buffers | DOD: precompute scalar/LUT before dispatch | Alternative rejected: shader trig/sqrt or Mesh.bounds hot-path reads | Estimate: 20-65us saved per 16k candidates, PENDING VERIFICATION
- [x] 18. REMOVE STALE SYMBOLS | rg audit found no forbidden scatter symbols: length, distance, acos, DrawMeshInstanced, Object.Instantiate, SetData, ClearMemory | DOD: stale-symbol purge | Alternative rejected: leave dormant legacy paths in scatter-owned files | Estimate: prevents regression and GC risk, PENDING VERIFICATION
- [x] 19. NATIVE MEMORY BARRIER | CompactVisibleScatterInstances starts with DeviceMemoryBarrierWithGroupSync before cache compaction | DOD: explicit compute synchronization marker | Alternative rejected: implicit-only compaction code | Estimate: correctness guard; perf neutral until Unity shader import, PENDING VERIFICATION
- [x] 20. OMEGA COMPILE CHECK | [BLOCKED BY DEPENDENCY] Three compile attempts reached unrelated Celestial/Submarine/Voxel errors; scatter-owned files produced no reported compiler errors | DOD: 3-strike fail-fast protocol | Alternative rejected: edit outside WORLD_SCATTER_HLOD domain to force a green build | Estimate: compile verification blocked, PENDING VERIFICATION

## Iteration Log
- Loop 0: Prompt extracted from CURRENT_BATCH.txt. Required mandates loaded. Existing scatter implementation inspection pending.
- Loop 1: Tasks 1-5 implemented and reviewed in Hecton_GpuScatter.compute/GPUScatterDirector.cs. dotnet build attempted; failed on unrelated HectonCelestialEngine/SubmarineFluidDynamics errors before a Unity shader import check could run.
- Loop 2: Tasks 6-10 implemented and reviewed. Stale-symbol scan for scatter-owned files returned no forbidden matches. Hecton8.Core build retry timed out after existing project-wide failures had already been observed.
- Loop 3: Tasks 11-15 implemented and reviewed. Found and fixed duplicate foveated append risk during own-code pass; compaction is now sole visible-index writer.
- Loop 4: Tasks 16-20 closed. diff --check passed for modified scatter files; forbidden-symbol scan stayed clean. Compile verification is dependency-blocked by unrelated Celestial/Submarine/Voxel failures after three attempts.
- Loop 5: OMEGA_POLISH executed after all 20 tasks were checked or dependency-blocked. Domain file confirms BRG Scatter Director ownership. Zero-GC/math scan found no hot-path managed upload, no stale scatter symbols, and only cold setup/value-type allocations. Final Hecton8.Core build failed outside scatter at ConstructionManager.cs(40,208) with 48 warnings; status remains PENDING VERIFICATION.
