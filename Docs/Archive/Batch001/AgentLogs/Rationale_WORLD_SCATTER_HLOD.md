# WORLD_SCATTER_HLOD Rationale

Status: PENDING VERIFICATION

## Decision 0 - Batch File Extension
Problem: User requested Docs/Tasks/CURRENT_BATCH.md, but the repository contains Docs/Tasks/CURRENT_BATCH.txt.
Solution: Extracted the WORLD_SCATTER_HLOD XML tag from CURRENT_BATCH.txt using a CLI regex over the full file.
Rejected Alternatives: Did not infer task text from chat only; did not read neighboring prompts as architectural input.
Scalability potential: Keeps the work bound to the correct scatter/culling domain across low, middle, high, and ultra profiles.
Hardware Impact: No runtime impact. Prevents wrong-domain edits that would waste CPU/GPU time on i3/MX350.

## Decision 1 - Mandate Scope
Problem: Scatter HLOD touches GPU culling, foveation, instancing, zero-GC CPU upload, and squared-distance math.
Solution: Loaded eight mandates: foveated simulation LOD, GPU occlusion, URP HLOD, GPU sovereignty, MX350 compute kernels, zero-GC, performance budgets, and rsqrt/squared-distance law.
Rejected Alternatives: Did not bulk-read the entire registry. Did not use generic rendering assumptions where mandates existed.
Scalability potential: Low uses aggressive cull/update throttling and dither fakes; Middle raises residency; High/Ultra spend saved cycles on denser visible scatter and richer sway.
Hardware Impact: Expected low-end i3/MX350 gain comes from removing CPU cull work, avoiding sqrt/trig in hot paths, and reducing overdraw before vertex/fragment work. Exact microseconds PENDING VERIFICATION.

## Decision 2 - Tasks 1-5 Cull Order
Problem: The scatter field was vulnerable to spending heightmap, matrix, and normal work on far or peripheral candidates.
Solution: Kept squared-distance rejection at the top of GenerateScatterInstances, retained CPU precomputed _HectonScatterMinNormalYSq, and replaced hard radius edge with an 8x8 blue-noise-style threshold over the far squared-distance band.
Rejected Alternatives: Rejected length()/sqrt radius checks, Mathf.Acos slope gates, and CPU-side culling lists because they either burn ALU, add managed CPU work, or pop visibly in fog.
Scalability potential: Low/MX350 culls early and dissolves distant scatter; Middle keeps larger dither bands; High/Ultra can extend max visible distance and spend saved fill-rate on denser visible coral.
Hardware Impact: Expected gain on i3/MX350 is from avoiding height texture samples and clip transforms for rejected cells. Estimate: 0.08-0.26 ms GPU per 16k candidates, PENDING VERIFICATION.

## Decision 3 - Foveated Cache Ownership
Problem: Foveated skipped instances were being appended during the cull pass, which would duplicate them when a separate compaction pass consumed the full visibility cache.
Solution: GenerateScatterInstances now only updates visibility state for the active quadrant; CompactVisibleScatterInstances owns all appends from the cache.
Rejected Alternatives: Rejected append-in-cull plus append-in-compact because it can overcount indirect instances and inflate overdraw.
Scalability potential: Low uses one quadrant per frame with stable cache reuse; High/Ultra may lower cadence only by changing the quadrant/foveation policy later.
Hardware Impact: Prevents duplicate visible-index writes and accidental extra draws on MX350. Exact microseconds PENDING VERIFICATION.

## Decision 4 - Tasks 6-10 GPU Ownership
Problem: Dense scatter needs occlusion and cull cadence control without CPU visibility lists or per-instance GameObjects.
Solution: Hi-Z now projects an 8-corner bounds rect against the previous depth pyramid; render submission remains Graphics.RenderMeshIndirect; indirect args use LockBufferForWrite; cull updates are staggered by quadrant; peripheral dot assumes uploaded camera forward is normalized.
Rejected Alternatives: Rejected CPU occlusion queries, DrawMeshInstanced, Object.Instantiate proxies, SetData args uploads, and per-thread normalize(cameraForward).
Scalability potential: Low updates one quadrant per frame and keeps cached visibility stable; Middle can keep Hi-Z active in dense zones; High/Ultra can afford longer visibility and denser atlased scatter inside the same indirect path.
Hardware Impact: Expected MX350 gain is lower CPU submission cost and fewer hidden cluster fragments. Estimated combined save: 0.25-1.1 ms in blocked scatter fields, PENDING VERIFICATION.

## Decision 5 - Tasks 11-15 Visual Currency
Problem: Overdraw reduction alone risks making the scatter field visually flat on high-tier devices, while Kinematics still needs cheap vegetation drag data.
Solution: Added a pre-baked species bounds LUT, kept projected pixel culling before expensive tests, added sine-parabola vertex sway from abyssal flow magnitude, exported a 64-bin GPU density buffer, and packed atlas UV offsets into instance data.
Rejected Alternatives: Rejected Mesh.bounds reads in the frame loop, CPU plant sway, per-species material clones, and CPU density readback during culling.
Scalability potential: Low keeps atlas + density cheap and culls tiny objects; Middle uses stable sway; High/Ultra can increase scatter density and atlas variety while staying in one indirect draw family.
Hardware Impact: Low-end i3/MX350 avoids CPU density rebuilds and material churn. Top-tier visual overkill path gets richer per-instance sway/atlas variation from the saved submission and overdraw budget. Exact microseconds PENDING VERIFICATION.

## Decision 6 - Tasks 16-20 Closure and Compile Wall
Problem: The final scatter tasks required zero-fill avoidance, precomputed upload discipline, stale-symbol removal, an explicit compute sync marker, and a compile check, but the project already fails outside the WORLD_SCATTER_HLOD domain.
Solution: Kept the mod matrix staging on NativeArrayOptions.UninitializedMemory, uploaded the squared normal threshold and bounds/density data from CPU-side constants/LUTs, audited scatter-owned files with rg for stale symbols, placed DeviceMemoryBarrierWithGroupSync at the top of the compaction kernel, and ran three build attempts before marking compile verification dependency-blocked.
Rejected Alternatives: Rejected ClearMemory staging, SetData-style upload paths, dormant DrawMeshInstanced/Object.Instantiate code, and cross-domain edits to Celestial/Submarine/Voxel files just to claim a green build.
Scalability potential: Low keeps the cheapest data path and avoids unnecessary zeroing; Middle keeps deterministic cache compaction; High/Ultra can use the saved CPU/GPU budget for denser scatter, wider cull radii, richer atlas variation, and stronger sine-parabola sway without changing the submission model.
Hardware Impact: On i3/MX350 the expected gain comes from avoiding zero-fill staging, managed arg upload risk, dead legacy paths, and hidden-overdraw candidates. Compile verification remains PENDING VERIFICATION because unrelated errors stop the project build before a Unity shader import pass can be trusted.

## OMEGA POLISH CHANGES
Problem: Final polish required an anti-bloat audit for honest math, hot-path GC, cache locality, domain leakage, and build health after the 20-task checklist was closed.
Solution: Re-read the POLISH_MANDATE only after all tasks were checked or dependency-blocked. Re-read the domain file and confirmed ownership is Echelon 2, BRG Scatter Director. Re-scanned modified scatter files for hot-path managed patterns and forbidden math/submission symbols. Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly`.
Rejected Alternatives: Rejected adding speculative cross-domain fixes to Construction/Celestial/Submarine/Voxel files. Rejected switching status to VERIFIED MASTER GRADE because the WORLD_SCATTER_HLOD prompt requires PENDING VERIFICATION and the build still fails outside scatter.
Scalability potential: Low/MX350 path uses four-frame quadrant cull cadence, foveated peripheral cache, minimum projected-pixel rejection at >=2 pixels, blue-noise radius dissolve, squared-distance gates, and zero CPU visibility lists. Mid keeps the same GPU ownership with less aggressive tiny-instance rejection. High/Ultra lowers the projected-pixel cutoff to allow denser visible scatter, spends saved cycles on richer atlas variation and sine-parabola sway, and keeps the indirect submission model stable.
Hardware Impact: Honest calculations replaced by cinematic cheats: length/radius became squared dot tests; slope angle/acos became `_HectonScatterMinNormalYSq`; hard far cut became dithered evaporation; CPU/physics plant sway became vertex sine-parabola sway with rsqrt flow normalization; CPU density rebuild became a 64-bin GPU density export; runtime Mesh.bounds hot-path reads became a precomputed bounds LUT. On i3/MX350 this is expected to save 0.3-1.4 ms in dense blocked scatter scenes, PENDING VERIFICATION.
Final Git Diff: Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute 276 additions / 10 deletions; Hecton_ScatterIndirectLit.shader 29 additions / 3 deletions; GPUScatterDirector.cs 518 additions / 12 deletions; ScatterGPUIBackend.cs 11 additions / 9 deletions. Total modified scatter code: 834 insertions / 34 deletions before log/status updates.
Build Health: Final build failed at Assets/_Project/Scripts/ConstructionManager.cs(40,208): `ConstructionManager` does not implement `IOriginShiftListener.OnOriginShift(in OriginShiftEventData)`. Build also reported 48 warnings. No scatter-owned compile errors were reported in the final build output.
