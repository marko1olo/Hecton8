# Rationale_SONAR_POINT_CLOUD

## Initial Mandates
Read before coding:
- GPU_Compute_Kernels_Kernels_Optimization_MX350
- GPU_Compute_Warp_Sizing_Mobile
- REND_DescriptorBinding_Reality_Check
- REND_URP_Graphics_HotPath_Optimization_HLOD
- REND_Shader_Noir_Aesthetics_Dithering_Fog
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Performance_Budgets_FrameTime_VRAM_Limits
- MATH_Coordinate_Precision_AUP_FloatingOrigin

## Decision 1 - Replace CPU Point Cloud
Problem: PDAMapTab was producing sonar points with a Burst CPU job, then uploading the whole NativeArray to a GraphicsBuffer with SetData before a point-primitive draw.
Solution: Move point discovery into a compute shader, append valid hits on GPU, copy append count directly into indirect args, and draw one instanced quad mesh.
Rejected Alternatives: Keeping CPU Burst path was deterministic but violated the prompt and paid CPU upload bandwidth every refresh. Geometry shaders were rejected for URP portability and MX350 risk.
Scalability potential: Low = 4x4x4 live sample lanes and no height color branch. Middle = 8x8x8 samples. High = same topology with full color/depth/ping. Ultra = saved CPU time can be spent on denser external ping visuals later.
Hardware Impact: Expected CPU saving on i3/MX350 is the removed 1,728 point CPU write plus upload, estimated 55-90 us on refresh frames and no readback stalls.

## Decision 2 - Cross-Domain Predator Buffer Access
Problem: The task requires predator AUP dots, but the existing producer owns a private GraphicsBuffer in EncounterDirector and only publishes it as a global shader buffer.
Solution: Add a narrow GlobalRegistry interface method to IEncounterDirectorService so the UI/VFX consumer can bind the existing buffer without referencing EncounterDirector internals.
Rejected Alternatives: Duplicating predator positions in UI was rejected as stale state. Searching scene objects for the director was rejected as direct coupling and slow. Editing fauna internals beyond this interface was rejected.
Scalability potential: Low = zero predator count binds fallback buffer. Middle/High/Ultra = existing 16-slot predator buffer adds red dots with no new allocation.
Hardware Impact: One interface call and buffer bind when rendering the PDA; estimated below 2 us on i3/MX350.

## Decision 3 - Visual Fake Over Physical Sonar
Problem: Real acoustic propagation or dense voxel reconstruction would exceed the PDA visual need.
Solution: Use sign-change ray samples in a tiny grid plus a shader ping radius mask and dithered glass fade.
Rejected Alternatives: Full raymarch per pixel and physical wave simulation were rejected as frame-time waste for a diegetic mini-map.
Scalability potential: Low = coarse silhouette. Middle = readable cave shell. High/Ultra = height color, predator pulse, soft depth.
Hardware Impact: 64-512 compute lanes plus one indirect draw; expected to stay under 0.1 ms on MX350 when visible.

## Decision 4 - Keep Metadata in GPU Payload
Problem: The prompt asks for float3 append positions, but later tasks require intensity, predator flagging, and red pulse behavior per point.
Solution: Store xyz PDA-local position plus a w metadata channel in the append buffer; the material still consumes position as the first three floats and uses w only for alpha/predator sign.
Rejected Alternatives: A second CPU-side metadata buffer was rejected because it would reintroduce upload synchronization. Encoding predator state into coordinates was rejected as fragile and hard to debug.
Scalability potential: Low = w disables height branch and drives alpha only. Middle = normal points use alpha. High/Ultra = predator sign and pulse use the same payload without more buffers.
Hardware Impact: +4 bytes per point, 528 point capacity total; under 9 KiB append storage, cheaper than any CPU metadata upload.

## Decision 5 - Compile Verification Boundary
Problem: Unity compute shader import could not be completed through MCP because the editor session timed out and then became unavailable; batchmode cannot open the project while another Unity instance holds it.
Solution: Run `dotnet build Hecton8.Core.csproj` for C# and scan Editor.log for current Unity errors. C# build passes. Unity Editor.log shows current blockers in `WorldChunkResidencyManager.cs`, not the sonar UI files.
Rejected Alternatives: Editing world residency or unrelated audio/world compile walls was rejected as outside the assigned presentation/VFX domain.
Scalability potential: Not a runtime feature; preserves domain isolation under multi-agent work.
Hardware Impact: None. Verification risk remains only compute-import certainty until Unity session is available.

## OMEGA POLISH CHANGES
Problem: The first compute draft used honest divisions in shader math and no final signal noise.
Solution: Replaced HLSL divisions in the compute sampler, local PDA transform, ray step, predator radius scale, ping band, screen UV, and depth fade with `rcp` multiplication. Added deterministic per-instance signal jitter using `frac` and `_AcousticPingSignal.z`.
Rejected Alternatives: Sine noise and texture noise were rejected because they add unnecessary ALU/texture bandwidth for a diegetic map flicker. Full physical sonar persistence was rejected as fake precision.
Scalability potential: Low = 4x4x4 active lanes, height color disabled, same cheap jitter. Middle = 8x8x8 lanes, height color enabled. High = full color/predator/depth without CPU upload. Ultra = same base path leaves budget for future overkill overlays without changing the buffer contract.
Hardware Impact: RCP polish removes repeated divide latency from the shader path; expected micro gain is 2-5 us on MX350-class hardware under visible PDA load.

Problem: Silo audit found a necessary cross-domain edit for predator dots.
Solution: The only cross-domain change is a narrow `IEncounterDirectorService.TryGetPredatorAupGpuBuffer` contract and implementation. UI reads through GlobalRegistry, never through a concrete director reference.
Rejected Alternatives: EventBus duplication was rejected because the GPU buffer already exists and copying predator positions would add synchronization. Scene object search was rejected as architectural leakage.
Scalability potential: Low = fallback empty buffer, no predator work. High/Ultra = current 16 predator AUP entries pulse red through the same append buffer.
Hardware Impact: No new predator allocation; one interface branch and buffer bind when PDA renders.

Problem: Final build health required proof without touching unrelated domains.
Solution: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded. Unity MCP refresh timed out; Editor.log now shows Unity compile blocked by `WorldChunkResidencyManager.cs` CS8156, not the sonar files.
Rejected Alternatives: Fixing world residency compile errors from this VFX agent was rejected as domain breach.
Scalability potential: Verification-only.
Hardware Impact: None.

## Decision 6 - Runtime Asset Binding
Problem: The PDA spectrum/map tab is created procedurally, so the compute shader reference would be editor-only unless serialized through an existing runtime owner.
Solution: Add serialized shader/compute fields to `PlayerPDA`, forward them to `PDASpectrumTab`, then into `PDAMapTab.ConfigurePointCloudAssets`. The Player prefab stores direct GUID references to the shader and compute assets.
Rejected Alternatives: `Resources.Load` was rejected because project policy forbids runtime Resources fallback. Repeated `Shader.Find`/AssetDatabase probing was rejected because AssetDatabase is editor-only and repeated runtime lookup is hot-path debt.
Scalability potential: Low = direct refs avoid missing assets on weak devices. Middle/High/Ultra = same refs let the map enter the GPU path immediately with no search cost.
Hardware Impact: Removes missing-asset retry work and avoids 10-40 us startup/search spikes on low-end silicon; steady-state impact is 0 allocation and 0 lookup.
YAML Structure Verification: Required `m_RootGameObject` scan was executed; this regular prefab asset does not contain that PrefabInstance field. Secondary validation confirmed `%YAML 1.1`, Unity tag header, top-level `--- !u!1` GameObject records, the `Assembly-CSharp::Hecton8.UI.PlayerPDA` block, and exact shader/compute GUID + fileID property alignment.

## Decision 7 - Low-Tier Predator Coverage
Problem: The first compute layout injected predator AUP dots after the low-tier `_DispatchAxis` early-out, so 4x4x4 mode could only show four predator slots.
Solution: Move predator injection before SDF-lane LOD rejection and map `x + y * 8` on `z == 0` to cover the full 16-slot predator buffer while preserving low-tier SDF sampling.
Rejected Alternatives: Raising low-tier SDF dispatch to 8x8x8 was rejected because it wastes the saved Math LOD budget. CPU-side predator overlay was rejected because the prompt requires GPU append dots.
Scalability potential: Low = full red-contact readability with 4x4x4 SDF cost. Middle/High/Ultra = same 16 contacts plus full 8x8x8 shell.
Hardware Impact: Restores 16 predator visual dots for negligible ALU; keeps the 8x active-lane reduction on i3/MX350.

## Decision 8 - Stale SDF Guard
Problem: A transient SDF payload failure could leave an old `_sdfTexture` alive and still render a stale holo-map.
Solution: Track `_pointCloudSdfReady` separately from texture allocation and clear it on EMP/offline/no-payload paths; draw only when both texture and readiness are valid.
Rejected Alternatives: Destroying the 3D texture on every failure was rejected because it would create allocation churn when the stream recovers. Clearing the whole texture was rejected as unnecessary GPU upload work.
Scalability potential: Low = no false positive map on source loss. Middle/High/Ultra = persistent texture remains ready for fast recovery.
Hardware Impact: One bool gate; avoids needless compute/draw frames and prevents user-facing stale data without allocation.

## Decision 9 - Oriented PDA Draw Bounds
Problem: `Graphics.DrawMeshInstancedIndirect` used an axis-aligned bounds sized from approximate map width/height scalars. A rotated diegetic PDA panel could be culled even when visible.
Solution: Build the draw `Bounds` from all four `RectTransform.GetWorldCorners` points plus both depth-offset extremes. Normalize the panel normal with `math.rsqrt(normal.sqrMagnitude)` for stable depth extrusion.
Rejected Alternatives: Inflating a scalar cube around the map was rejected because it would overdraw too broadly and hide culling bugs. Keeping the approximate magnitude helper was rejected because it was less correct and no longer needed.
Scalability potential: Low = no accidental disappearance on handheld/diegetic panel rotations. Middle/High/Ultra = same reliable culling while richer point visuals remain available.
Hardware Impact: Adds seven `Bounds.Encapsulate` struct operations on visible PDA frames; removes overbroad fallback risk and prevents missing visual work. Estimated cost below 1 us CPU, correctness gain is higher value.

## Decision 10 - Remove Redundant Visible-Frame Binding
Problem: `EnsurePointCloudResources` rebound `_SonarPoints` and rechecked kernels on the visible draw path, then `RenderPointCloud` rebound the same buffer immediately before the draw.
Solution: Keep resource allocation in `EnsurePointCloudResources`, and bind `_SonarPoints` only at material creation and at the final draw submission.
Rejected Alternatives: Adding a dirty flag was rejected because the draw path already performs the necessary authoritative binding. Leaving the duplicate call was rejected as hot-path drift.
Scalability potential: Low = fewer Unity binding calls on MX350. Middle/High/Ultra = saved CPU submission overhead can be spent on stronger visual density later.
Hardware Impact: Removes one material buffer bind plus one resolved-kernel branch per visible PDA frame; estimated 1-4 us CPU on weak hardware.

## Decision 11 - Compute Import Risk Reduction
Problem: Static HLSL scan found an unnecessary `float3(_GridDimensions)` constructor. It might compile, but it was avoidable ambiguity in the compute import path.
Solution: Use `_GridDimensions` directly in the `max` expression.
Rejected Alternatives: Waiting for Unity import only was rejected because no Unity instance is currently connected, and the cleanup is mechanically safer.
Scalability potential: Verification-only; preserves the same 4x4x4/8x8x8 LOD behavior.
Hardware Impact: No runtime impact; reduces shader compiler ambiguity.

## Decision 12 - Point-Cloud Tier Hysteresis
Problem: The visible-frame path resolved low-tier state once for compute dispatch and again for material height colorization. It also switched immediately on `GlobalRegistry.ScalabilityTier` changes, which can create quality flicker or accidental high-tier dispatch bursts during dynamic scalability adjustment.
Solution: Resolve one per-frame low-tier state in `RenderPointCloud`, pass it into `DispatchSonarPointCloud`, and gate changes through a 2-second candidate window. Reset the gate on PDA enable so a disabled tab does not inherit stale tier state.
Rejected Alternatives: Keeping direct `IsLowMathTier` reads was rejected because it can desynchronize dispatch density and shader branch state. Smoothing point density in shader was rejected because it would still pay the expensive high-tier compute lane cost.
Scalability potential: Low = stable 4x4x4 dispatch and no height color during transient tier noise. Middle = unchanged 8x8x8 shell once the request is stable. High = stable height color/depth/predator visuals. Ultra = same path leaves headroom for future overkill overlays without changing dispatch contracts.
Hardware Impact: Adds four scalar fields and a few branches on visible PDA frames. Avoids transient 64-to-512 lane flips on weak silicon; estimated 15-35 us saved during tier oscillation and no measurable steady-state cost.

## Decision 13 - Compute Constant Buffer Packing
Problem: The sonar compute path still pushed multiple per-dispatch scalar/vector properties into Unity before every visible raymarch. That violates the GPU constant-buffer mandate and spends CPU submission time on binding trivia instead of visible quality.
Solution: Pack grid dimensions, volume origin, voxel size, player position, scalar params, and dispatch params into `HectonSonarMapConstants`. Upload one persistent 96-byte `GraphicsBuffer.Target.Constant` when `SystemInfo.supportsSetConstantBuffer` is true, and bind it with `SetConstantBuffer`.
Rejected Alternatives: Leaving the individual property sets was rejected as avoidable binding overhead. Forcing constant buffers on every device was rejected because backend support can vary; the no-allocation fallback keeps the individual `SetVector` path alive only when needed.
Scalability potential: Low = same packed constants feed the 4x4x4 coarse shell with no height color. Middle = 8x8x8 readable cave shell. High = full height color, predator pulse, depth fade, and ping scanline with fewer CPU bindings. Ultra = saved CPU submission budget can buy denser future overlays without changing the buffer contract.
Hardware Impact: Normal path removes five to eight Unity property sets per visible PDA compute refresh. Estimated i3/MX350 CPU submission gain: 3-8 us. Fallback devices are no worse than the prior path.

## Decision 14 - Compute Thread Group Query
Problem: The dispatch call assumed `[numthreads(8,8,8)]` with `(dispatchAxis + 7) >> 3`. The mandate requires C# to query compute kernel group sizes so shader edits cannot silently desynchronize dispatch coverage.
Solution: Cache `ComputeShader.GetKernelThreadGroupSizes` for `CSRaymarch` when kernels resolve, reset cached sizes when the compute asset changes, and compute dispatch group counts with integer ceil division against the cached sizes.
Rejected Alternatives: A shared constant was rejected because it still duplicates shader source truth in C#. Runtime query on every dispatch was rejected because the group size is static after kernel resolution.
Scalability potential: Low = 4-axis work still dispatches the minimum groups for the live kernel. Middle/High/Ultra = future kernel group changes can scale without C# math drift.
Hardware Impact: One kernel metadata query during asset resolve; no per-frame query. Prevents under-dispatch or over-dispatch risk if a later technical artist retunes HLSL group size for MX350/mobile.

## Decision 15 - Verification Boundary After Loop 9
Problem: A core-only C# build proves the `PDAMapTab` constant-buffer and group-query changes compile, but Unity compute import and visual capture still need an active editor. MCP briefly returned console entries, then the editor transport failed after shutdown.
Solution: Record both results separately: `Hecton8.Core` built with 0 warnings and 0 errors; full project-reference build also reached `Build succeeded` but emitted unrelated package warnings; Unity console/log evidence is partial and not enough for visual verification.
Rejected Alternatives: Claiming shader import victory from `dotnet build` was rejected because C# compilation does not compile HLSL compute kernels. Editing Crest/ocean validator or package warnings was rejected as outside this VFX task.
Scalability potential: Verification-only. The runtime scalability contract remains Low/Middle/High/Ultra through the existing Math LOD and packed constant path.
Hardware Impact: None beyond preventing false reporting. Status remains PENDING VERIFICATION until Unity can load the compute asset and capture the PDA holo-map.
