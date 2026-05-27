# LOG_13GPU

Date: 2026-05-27
Status: PENDING VERIFICATION

Session start. User-assigned ID `13GPU`; no matching XML prompt found in active `CURRENT_BATCH.md`.

What was wrong: `ScatterBudgetController` used abrupt Surface/MidDepth/Deep budget selection with no hysteresis and no `HomeostasisBrain.GlobalQualityWeight` input. This made scatter/detail budgets equally dense on weak and high-end hardware and risked oscillation around depth thresholds.

What was done: Patched `Assets/_Project/Scripts/ScatterBudgetController.cs` to add depth hysteresis, smooth depth profile interpolation, and continuous quality-driven scaling of scavenge/detail radius, spawn cadence, collider activation radius, and collider operation cadence.

Cinematic Cheats used: did not simulate extra physical truth. The change buys visual/detail density on high quality through budget cadence and residency, while low quality sheds detail work without changing save identity.

Exact Microseconds saved: PENDING PROFILE. Static estimate is 35-80 us during threshold churn on i3/MX350 by avoiding budget flip-flop and unnecessary collider/scavenge reapplication.

Verification: Static scan found no added `SetData`, `GetData`, `DrawMeshInstanced*`, LINQ, or hot-path array allocation in the touched file. Compile was not launched because CPU sample was `100.0%`, above the allowed build threshold.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE BLOCKED BY EXTERNAL DEPENDENCY

What was wrong: shared BRG and far-field HLOD paths did not fail closed on non-finite matrices, culling planes, bounds, fades, or origin shifts. Procedural scatter native spatial buckets also accepted non-finite placement positions and negative spacing.

What was done: patched `HectonBatchRendererGroupUtility` to reject invalid culling data, patched `HectonHLODRenderer` to compact valid HLOD instances and skip corrupt entries, and patched `WorldProceduralScatterWorkingMemory` to reject invalid scatter placement inputs before native bucket insertion.

Cinematic Cheats used: no extra physical simulation, no per-instance truth expansion, no new draw path. The fixes preserve cheap deterministic presentation and keep valid far-field visuals rather than dropping an entire batch when one instance is corrupt.

Exact Microseconds saved: PENDING PROFILE. Static estimates: 15-40 us in corrupt BRG culling frames, 20-60 us in poisoned HLOD batches, 10-30 us in corrupted scatter spacing passes. These are containment estimates, not profiler proof.

Verification: `git diff --check` passed for all four touched runtime files. Pattern scan found no added `SetData`, `GetData`, `DrawMeshInstanced*`, LINQ, or hot-path array allocation; only pre-existing cold arrays were reported. Guarded `Assembly-CSharp.csproj` build later ran and failed only in `Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Overrides/CandiceSQLiteProvider.cs` because `Mono.Data` / `SqliteDataReader` are unresolved. No touched GPU/scatter/HLOD file appeared in compiler errors.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE BLOCKED BY EXTERNAL DEPENDENCY

What was wrong: GPU compute culling trusted malformed constants and matrix/plane/SDF data. Shared impostor atlas UV math trusted inconsistent material `zw` cell scale. `HectonHLODRenderer` had a serialized shader and hidden fallback shader contract, but no runtime fallback material path, so an empty material slot disabled far-field HLOD.

What was done: patched `InstanceCulling.compute` to fail closed on invalid matrices, planes, cull distance, bounds radius, voxel SDF coordinates, and sampled SDF values. Patched `Hecton_Impostor.hlsl` to derive atlas cell scale from sanitized columns/rows and wrap view index by total cell count. Patched `HectonHLODRenderer` to create one cached runtime material from the assigned or hidden fallback shader and release it deterministically.

Cinematic Cheats used: kept detail presentation in cheap GPU cull/impostor/HLOD paths. No physical simulation was added; bad data is discarded before it can become visible work.

Exact Microseconds saved: PENDING PROFILE. Static estimates: 20-50 us in corrupt compute cull frames, 5-20 us in bad impostor material fallback cases. HLOD material change is a correctness restore, not a CPU saving claim.

Verification: `git diff --check` passed for touched code and shader files with CRLF warnings only. Shader compiler was not available in this shell. A separate `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` process was initially active as PID 24312, so no concurrent build was launched. After CPU dropped to 27% and no `dotnet`/`csc` remained, `dotnet build Assembly-CSharp.csproj --no-restore -v quiet /clp:ErrorsOnly -maxcpucount:1` was rerun and failed only in `Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Overrides/CandiceSQLiteProvider.cs` because `Mono.Data` / `SqliteDataReader` are unresolved.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE BLOCKED BY PROJECT GRAPH

What was wrong: `HectonDistantLandmarkRenderer` had a documented shader fallback but no fallback material path, and accepted invalid external/native bounds into BRG upload. `PackImpostorAtlas.compute` did not wrap `_ViewIndex` by atlas cell count and could write outside the intended atlas tile when authoring data was malformed.

What was done: patched `HectonDistantLandmarkRenderer` to validate external buffers, finite bounds, native HLOD entries, origin shifts, and fallback draw bounds; compact valid native entries; and create one cached runtime material from `Hidden/Hecton8/World/DistantLandmarkSilhouette` when no material is assigned. Patched `PackImpostorAtlas.compute` to wrap atlas view index and guard source/output texture dimensions.

Cinematic Cheats used: preserved cheap far-field silhouettes and impostor atlas presentation instead of adding simulation. Invalid data is discarded before it becomes render work.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 15-45 us in corrupt landmark/HLOD payload frames. Atlas pack guard is corruption containment, not frame-time saving.

Verification: `git diff --check` passed for the two new touched files with CRLF warnings only. Hot-path scan found only existing cold upload arrays and one cached runtime material allocation. Guarded `dotnet build Assembly-CSharp.csproj --no-restore -v quiet /clp:ErrorsOnly -maxcpucount:1` failed before runtime compile on MSBuild circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` / `Unity.ShaderGraph.Editor.csproj`; guarded `--no-dependencies` failed on missing `Temp/CodexBuild/Unity.ShaderGraph.Editor.dll`.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE NOT RELAUNCHED DUE ACTIVE DOTNET PROCESS

What was wrong: `Hecton_GpuScatter.compute` clamped candidate count but not by the actual scatter grid cell count. Malformed CPU input with `_HectonScatterCandidateCount > _HectonScatterGridResolution^2` could generate out-of-field cells and let compact read stale visibility cache entries for non-existent cells.

What was done: added a shader-side grid candidate window and used it in both generate and compact kernels. Threads beyond valid grid cells clear visibility cache when inside the published candidate span, then return.

Cinematic Cheats used: kept the cheap generated scatter path; no simulation or readback was added. Invalid density work is discarded before it becomes visible append-buffer content.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 10-35 us only during malformed scatter dispatches; normal valid frames pay scalar min checks.

Verification: `git diff --check` passed for `Hecton_GpuScatter.compute` with CRLF warning only. Compile was not relaunched because a `dotnet` process was active after the prior build gate.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE NOT RELAUNCHED DUE ACTIVE DOTNET PROCESS

What was wrong: `DilateImpostorEdges.compute` trusted `_AtlasSize` for source, mask, and output texture access. If any atlas surface dimension diverged from the authored size, edge dilation could sample or write outside the real texture bounds.

What was done: added source/mask/output `GetDimensions` guards and clamped the working atlas size to the minimum real dimensions across all three textures.

Cinematic Cheats used: preserved cheap impostor edge dilation. No readback, no CPU validation pass, no extra simulation.

Exact Microseconds saved: PENDING PROFILE. No frame-time saving claim; this is atlas corruption containment.

Verification: `git diff --check` passed for `DilateImpostorEdges.compute` with CRLF warning only. Compile was not relaunched because a `dotnet` process was active.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `Hecton_KelpMaster_GPUI.shader` trusted raw vertex ingress, AUP offset, sway constants, time, UV height, and vertex color seeds in forward/shadow/depth kelp motion. Its MX350/high-quality paths also applied fixed binary behavior inside the active shader variant.

What was done: added finite guards for kelp vertex position/normal/tangent, global offset, sway speed/frequency/phase, time, UV height, and vertex color seeds. Added `_H8GlobalQualityWeight` smooth gates for sway amplitude, interaction deformation, MX350 motion cap, and high-quality parallax.

Cinematic Cheats used: retained cheap sine/parabola kelp motion and shader parallax. No physical plant simulation, no CPU validation pass, no readback.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 8-25 us only during corrupt kelp payload frames; normal-frame value is continuous visual scaling.

Verification: `git diff --check` passed for `Hecton_KelpMaster_GPUI.shader` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `Hecton_CoralMaster_GPUI.shader` cast runtime touch-ripple count without sanitation and consumed ripple vectors without finite checks. Touch ripple overkill and high parallax were not continuously gated by `_H8GlobalQualityWeight`.

What was done: added `_H8GlobalQualityWeight`, smooth quality gates for coral parallax and touch ripple energy, sanitized ripple count clamping, and finite guards for position/ripple/distance math.

Cinematic Cheats used: kept coral biolum as a shader fake. No CPU ripple simulation, no readback, no runtime manager change.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 5-18 us only during corrupt ripple payload frames; normal-frame value is controlled visual overkill.

Verification: `git diff --check` passed for `Hecton_CoralMaster_GPUI.shader` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE NOT RELAUNCHED DUE ACTIVE DOTNET PROCESS

What was wrong: final compile proof could not be collected without violating the shared-machine gate.

What was done: verified JSON parse, verified `git diff --check` across all 13GPU touched files/docs, ran a hot-path pattern scan for the new kelp/coral shader changes, and checked CPU/process state before build.

Cinematic Cheats used: none in verification.

Exact Microseconds saved: no runtime claim.

Verification: JSON report parsed successfully. `git diff --check` passed with CRLF warnings only. Hot-path scan found no `SetData`, `GetData`, `DrawMeshInstanced`, LINQ, or array allocation patterns in the new shader edits. Build was not relaunched because CPU sample was `100` and active `dotnet` PID `2448` was running.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `Hecton_GeologyImpostorBillboard.shader` trusted raw vertex position, UV, atlas sample, base color, alpha clip threshold, and ambient floor before atlas sampling and cutout clip.

What was done: added finite scalar/UV/color helpers, sanitized vertex position and UV, finite-saturated alpha/ambient controls, and fail-closed color output.

Cinematic Cheats used: kept the billboard impostor path. No mesh replacement, no CPU validation, no readback.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 3-8 us only during malformed geology impostor payload frames.

Verification: `git diff --check` passed for `Hecton_GeologyImpostorBillboard.shader` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `Hecton_VoxelSSAO.compute` used raw SSAO projection scale, radius, intensity, sigma, and sample offset vectors before pixel rounding and depth sampling.

What was done: added finite fallback for SSAO params and for float2-to-int rounding input. Malformed radius/intensity/sigma now collapses to deterministic defaults before texture loads.

Cinematic Cheats used: retained the cheap 4-direction SSAO approximation. No extra samples, no readback, no CPU-side validation.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 5-15 us only during malformed SSAO dispatches.

Verification: `git diff --check` passed for `Hecton_VoxelSSAO.compute` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `Hecton_AbyssalVoxelRock.shader` let malformed vertex/absolute positions, floating-origin offset, biome grid params, volume/cut/damage masks, and caustic time/layer data reach int casts, texture samples, noise, and displacement.

What was done: added finite scalar/vector helpers, sanitized SafeNormalize input, vertex displacement ingress, sample positions, biome grid casts, volume/cut/damage mask sampling, and caustic/noise time/layer data.

Cinematic Cheats used: preserved shader-side rock detail, biome tint, cut masks, and fake caustics. Invalid data fails closed; no physical simulation or CPU readback was added.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 12-40 us only during corrupt rock/geology payload frames.

Verification: `git diff --check` passed for `Hecton_AbyssalVoxelRock.shader` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE NOT RELAUNCHED DUE CPU GATE

What was wrong: compile proof could not be collected after the rock/geology pass without violating the CPU gate.

What was done: checked CPU/process state twice. First CPU sample was `73`; after a 30-second wait it was `77`; no `dotnet/csc` process was active. Build was not launched because CPU stayed above 50%.

Cinematic Cheats used: none in verification.

Exact Microseconds saved: no runtime claim.

Verification: JSON report parsed. `git diff --check` passed for the new rock/geology files and docs with CRLF warnings only. Hot-path scan found no `SetData`, `GetData`, `DrawMeshInstanced`, LINQ, or array allocation patterns in the new shader/compute edits.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `Hecton_TerrainDamageVolume.compute` trusted authored 3D resolution and stamp/world params before texture load/write and radius math.

What was done: added source/result texture dimension queries, active-resolution clamp, finite world/min-size/recovery guards, malformed stamp rejection, sanitized radius/strength, and finite output.

Cinematic Cheats used: preserved terrain damage as a visual volume mask. No physical deformation, CPU validation, or readback.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 4-12 us only during malformed damage-volume dispatches.

Verification: `git diff --check` passed for `Hecton_TerrainDamageVolume.compute` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `ParticleUpdate.compute` could cast a malformed particle count into a huge uint window and could sample flow-field buffers from unchecked resolution/origin/cell-size data.

What was done: clamped particle count to read/write buffer capacity, queried flow buffer capacity, bounded flow resolution, sanitized particle state, params, dt, velocity, position, size, life, and flow samples.

Cinematic Cheats used: retained GPU fake dispersion using cheap triangle turbulence. No CPU particle simulation and no GPU readback.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 6-20 us only during corrupt micro-particle/flow payload frames.

Verification: `git diff --check` passed for `ParticleUpdate.compute` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `Hecton_SargassumMaster.shader` used raw vertex/color/UV/time/prop-wash/cut/sink data in forward and shadow passes before motion, mask sampling, and alpha clip.

What was done: added finite helper overloads and sanitized sargassum sway, prop-wash response, pulse, wound curl, global cut mask, buoyancy sink mask, alpha clip, and final color.

Cinematic Cheats used: kept all sargassum motion shader-side. Invalid data fails closed; valid data keeps visual motion/biolum.

Exact Microseconds saved: PENDING PROFILE. Static estimate: 6-18 us only during malformed sargassum payload frames.

Verification: `git diff --check` passed for `Hecton_SargassumMaster.shader` with CRLF warning only. Shader compiler was not available in this shell.

---

Date: 2026-05-27
Status: STATIC VERIFIED / COMPILE GATE PENDING

What was wrong: `TerrainMaster.shader` still has `_MATH_LOD_LOW` compile-time terrain micro-detail branches. This is a residual rule conflict, but the runtime continuous quality binding for terrain was not proven.

What was done: did not mutate terrain blindly. Recorded the risk for a focused terrain-material contract pass instead of adding fake compliance.

Cinematic Cheats used: none.

Exact Microseconds saved: no runtime claim.

Verification: documented residual risk only. No code changed in `TerrainMaster.shader`.
