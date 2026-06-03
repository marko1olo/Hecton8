# Rationale 1722

## Session Start
Problem: Active vehicle hull visual path may contain runtime CPU deformation and particle-style cavitation work.
Solution: Convert hull dents, wear, MRAO masks, and cavitation flipbooks to Editor-only baked textures driven by shader-side sampling.
Rejected Alternatives: Runtime mesh mutation, per-bubble particles, material cloning, and hot GlobalRegistry polling violate HECTON-8 frame and GC rules.
Scalability potential: Low uses smaller static textures and cheapest shader reads; middle increases mask density; high and ultra use larger baked atlases and sharper flipbooks without changing gameplay truth.
Hardware Impact: Expected runtime CPU saving is removal of mesh vertex upload and particle update cost on i3/MX350; exact microseconds remain pending code inspection and compile proof.

## Controller Refactor
Problem: `HullDentShaderController` previously owned a live dent stream path with signal scanning, DataVault hull-dent buffer writes, and global vector-array upload potential.
Solution: Controller now binds baked albedo, MRAO, displacement, and cavitation flipbook textures; uploads only scalar shader globals; samples vessel telemetry through a cached generation handle on a throttled cadence; writes a fixed 300-frame managed presentation black-box ring allocated cold.
Rejected Alternatives: Keeping runtime dent DTO writes or `Shader.SetGlobalVectorArray` would preserve hot CPU deformation bookkeeping. Runtime mesh edits, material clones, and particles were rejected outright.
Scalability potential: Low uses the same shader contract with smaller baked assets; middle increases baked scar/mask density; high and ultra bind sharper baked atlases and flipbooks without changing gameplay truth or save identity.
Hardware Impact: Estimated saved steady-state runtime cost on i3/MX350 is 35-70 us/frame from removing dent signal/vector-array work; cavitation particle replacement estimate is 120-400 us/frame depending on previous particle count. Values are estimates because compile/profiler pass did not complete.

## Shader Baked Path
Problem: UberNoir hull vertex path still contained dynamic deformation loops and legacy dent buffer fallbacks.
Solution: Added `Hecton_HullBakedDisplacement1722.hlsl`; UberNoir now checks `H8Hull1722IsBakedActive` and bypasses dynamic dent/deformation loops when baked maps are enabled. MRAO channel conversion preserves existing UberNoir mask convention.
Rejected Alternatives: Editing every material or creating a dedicated submarine-only shader would fragment the SRP batcher path. Keeping old loops hot behind a zero count was not strict enough.
Scalability potential: Low scales displacement strength and texture resolution offline; middle/high/ultra increase baked detail and normal bias from the same shader function.
Hardware Impact: GPU cost becomes one vertex texture fetch for displacement plus optional material texture replacement; CPU deformation cost is zero in steady state.

## Offline Baker
Problem: Hull wear, structural dents, and cavitation needed to become disk assets, not runtime simulation.
Solution: Added `HullCavitationBaker1722.cs` EditorWindow and `HullCavitationBaker1722.compute`. The baker binds source mesh vertices through Editor-only `GraphicsBuffer` populated from a prewarmed scratch list; the compute shader uses that buffer as deterministic mesh influence for dents/panels. Kernels bake albedo, displacement/slope/scar, MRAO, and a 64-frame 8x8 cavitation atlas. C# validates exact pixel count and finite displacement bounds before writing PNG/EXR and enforcing BC7/ASTC importer settings.
Rejected Alternatives: CPU-side pixel loops for 4K generation were rejected due editor stall risk; unbound procedural-only bake was rejected after re-reading Task 03; separate metallic/roughness/AO/biolum textures were rejected due VRAM/fetch cost.
Scalability potential: Low: 1024 hull / 512 atlas class. Middle: aligned intermediate sizes. High/Ultra: 4096 hull and 2048 cavitation atlas. Visual overkill is bought offline only.
Hardware Impact: Runtime VRAM is the trade. BC7 4096 texture is 16 MB. Three 4K hull maps per submarine are 48 MB. The 85 MB budget only holds for one full 4K hull plus cavitation, or for streamed variants; three resident full 4K hull variants would exceed it.

## APEX Polish Pass
Problem: Prior proof surface still had forbidden-looking artifacts: runtime presentation owned a persistent `NativeArray`, the Editor baker used `ComputeBuffer`/`mesh.vertices`, and report JSON I/O conflicted with the APEX source-code proof directive.
Solution: Runtime black-box storage is now a fixed managed ring allocated only in `OnEnable`; DTO layout is still proven with `UnsafeUtility.SizeOf<T>()`. Editor bake mesh transfer uses `Mesh.GetVertices` into a prewarmed `List<Vector3>` and uploads through `GraphicsBuffer`. Baker report JSON writer and SHA helpers were removed. Static scans show no `GlobalRegistry.Get<T>()`, `GetComponent()`, `WaitForCompletion`, `.Complete()`, `ComputeBuffer`, `mesh.vertices`, `Allocator.Persistent`, or `new NativeArray` in the touched runtime controller/baker proof surface.
Rejected Alternatives: Keeping persistent `NativeArray` in a MonoBehaviour conflicts with the native ownership mandate. Keeping JSON reports adds editor I/O proof churn. Reverting to `ComputeBuffer` ignores the GPU sovereignty mandate.
Scalability potential: Low through ultra tiers keep the same runtime code path; fidelity scales through baked texture dimensions, shader scalar weights, and flipbook tile resolution only.
Hardware Impact: Low-end CPU keeps the same expected 35-70 us/frame hull-controller saving and 120-400 us/frame cavitation-particle avoidance. Editor bake memory pressure is cold and bounded by one prewarmed mesh scratch list plus transient GPU targets.

## Functional Polish Pass
Problem: The cavitation flipbook was baked and phase-animated but not actually sampled by the hull material; the baker still had Editor allocations through `GetPixels()`, `mesh.triangles`, and possible scratch-list capacity growth.
Solution: Added `_H8HullCavitationUvParams`, clamped flipbook sampling to a configurable hull-UV window, and applied foam in `H8UberNoirApplyHullCavitationFoam` as albedo/smoothness/emission only. Pixel validation now uses `Texture2D.GetPixelData<Color/Color32>()`; mesh metrics use submesh index counts; mesh vertex scratch is fixed capacity and fails fast above 1,048,576 vertices.
Rejected Alternatives: CPU particles, global foam over the entire hull, dynamic list growth, `GetPixels()` arrays, and `mesh.triangles` array reads.
Scalability potential: Low uses one windowed atlas sample with low intensity; middle/high/ultra increase baked atlas resolution and foam brightness through existing continuous quality weights.
Hardware Impact: Keeps runtime CPU at zero for cavitation bubbles; removes large Editor validation arrays during 4K bake.

## Vessel Telemetry ABI Repair
Problem: The controller read invented `VesselTelemetryEntry` members, which would fail compile against the actual submarine ballast ABI.
Solution: Read the existing fields only: `HullCleanlinessMask` for 64-panel cleanliness, `TotalCareActionsCount` through `VesselTelemetryEntry.ResolveToneWeight01`, and finite `CurrentBallastRatio` for visual ballast strain. The popcount is local branchless scalar math and allocates nothing.
Rejected Alternatives: Extending the ballast DTO from the VFX domain would violate one-owner data doctrine. Reflection, LINQ, and managed bitset helpers were rejected for hot-path cost.
Scalability potential: Low through ultra use the same scalar telemetry decode; quality only changes shader strength and baked asset resolution.
Hardware Impact: Prevents a compile break with no added runtime allocation. The extra popcount every 16 frames is sub-microsecond on i3/MX350.

## Compaction Fence Audit
Problem: DataVault relocation can invalidate read views if a consumer reads during compaction.
Solution: `TryRefreshVesselTelemetry` checks `IsCompactionFenceActive` before `TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly)`. If the fence is active or read fails, cached scalar visual state remains and retry occurs on the next sample window.
Rejected Alternatives: `GetOrCreate` in the hot path and mutable hull dent buffer ownership were rejected. No job is scheduled, so no `JobHandle.CombineDependencies` path is needed for this controller.
Scalability potential: Low and high tiers share identical safety behavior; only visual update cadence and baked resolution vary.
Hardware Impact: Telemetry sampling is throttled to every 16 frames, reducing DataVault read pressure to a cosmetic cadence.

## Build Gate
Problem: Full compile proof required but build environment became saturated.
Solution: CPU sampled at 48.48% with no compiler processes, so one `dotnet build Hecton8.slnx` was launched. It timed out after 124 seconds and left orphaned workers. Those workers were terminated. Rerun was refused when CPU sampled 55.74%. Later APEX retry sampled CPU 43.02% with no compiler processes and launched exactly one throttled `dotnet build Hecton8.slnx --no-restore -maxcpucount:1`; it timed out after 244 seconds with no diagnostics. One leftover `dotnet.exe` from that run was terminated. Later checks alternated between CPU saturation and active Unity Roslyn/Bee compiler processes, so another build was refused.
Rejected Alternatives: Launching repeated builds over timeout would violate throttling. Reporting compile success without output was rejected.
Scalability potential: Not applicable to runtime.
Hardware Impact: No compile result; static validation only.

## Registry Listener Compatibility Polish
Problem: The controller used the legacy `GlobalRegistry.UnregisterHotSwapListener(this)` wrapper while the dominant project pattern uses the non-logging `TryUnregisterHotSwapListener` cold route.
Solution: Switch the controller's cold unregister path to `GlobalRegistry.TryUnregisterHotSwapListener(this)` after verifying the method exists in `GlobalRegistry.cs` and delegates directly to the same listener registry.
Rejected Alternatives: Keeping the wrapper was functionally valid but less aligned with project hot-swap hygiene. Adding a local helper or new dependency was unnecessary.
Scalability potential: No runtime fidelity change. The value is lifecycle determinism under registry service replacement and disable/destroy churn.
Hardware Impact: No steady-state frame cost. Avoids extra unregister miss logging/scanning behavior during lifecycle teardown.

## Compute Falloff Determinism And Artifact Cleanup
Problem: `HullCavitationBaker1722.compute` used reversed-edge `smoothstep(high, low, value)` falloffs. That is a portability risk across shader compilers even when it appears to behave as a reverse ramp. An obsolete untracked JSON proof artifact also remained after report generation was removed.
Solution: Convert the reverse falloffs to explicit `1 - smoothstep(low, high, value)` forms for panel lines, longitudinal ribs, dent lips, cavitation ring, and cone masking. Delete `Docs/Reports/HULL_CAVITATION_BAKER_REPORT_1722.json`; source, status, rationale, and log remain the proof surface.
Rejected Alternatives: Keeping reversed `smoothstep` depended on compiler behavior instead of explicit math. Keeping the JSON file contradicted the user's source-code-only directive.
Scalability potential: Same visual model across low through ultra tiers, with fewer GPU-compiler interpretation risks.
Hardware Impact: No meaningful runtime cost. The compute baker remains offline; the shader math is deterministic and equivalent.
