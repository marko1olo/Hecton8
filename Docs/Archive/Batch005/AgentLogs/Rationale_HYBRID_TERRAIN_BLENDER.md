# Rationale: HYBRID_TERRAIN_BLENDER

Status: PENDING VERIFICATION

## Initial Scope Decision

Problem: Hybrid MapMagic heightmap and first-party voxel caves produce hard terrain/cave intersections.
Solution: Implement isolated terrain seam pipeline under the World/Terrain boundary, consuming chunk signals and using Burst/native mesh data plus shader dither fallback.
Rejected Alternatives: Runtime GameObject skirts and classic Unity mesh vertex arrays are rejected because they hide symptoms, allocate, and create fragile scene dependencies.
Scalability potential: Low uses dither-only cheap concealment. Middle uses bounded seam snapping near player. High/Ultra can add finite-difference normals, blend masks, and visual overkill around close hero seams.
Hardware Impact: MX350/i3 path avoids vertex snapping on Low and caps work to async chunk generation, target 0 B hot-path GC and sub-0.1ms steady-state frame cost.

## Mandate Selection Decision

Problem: Task crosses voxel SDF, terrain rendering, async mesh updates, telemetry, and global registration.
Solution: Loaded VOX_MapMagic_Voxel_Seam_Alignment_Integration, VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline, REND_Terrain_VirtualTexturing, OPT_Zero_GC, OPT_Cinematic_Cheat, OPT_Performance_Budgets, DBG_Telemetry, and ARCH_Global_Registry.
Rejected Alternatives: Reading unrelated AI/audio/UI mandates would increase noise and risk cross-domain edits.
Scalability potential: Mandates define Low/Middle/High/Ultra seam behavior and require dither fallback before expensive geometry edits.
Hardware Impact: Selection preserves MX350 budget by prioritizing shader fake on Low and chunk-time jobs on higher tiers.

## Signal And Vault Decision

Problem: MapMagic terrain generation was listener-driven and did not expose a decoupled terrain chunk signal for seam systems.
Solution: Added `TerrainChunkGeneratedSignal` in contracts and `TerrainChunkGeneratedEvents` backed by a fixed NativeQueue. MapMagic tile apply publishes to the queue, and the seam applier drains a bounded count per slow tick or Awaitable chunk phase.
Rejected Alternatives: Directly calling the seam applier from MapMagic was rejected because 20+ agents are editing adjacent systems and direct runtime references would create fragile initialization order. A managed C# event was rejected for hot-path allocation risk.
Scalability potential: Low drains only metadata and uses shader mask. Middle/High/Ultra can consume the same signal and increase projection patch budgets without changing MapMagic.
Hardware Impact: MX350/i3 avoids polling all terrains every frame; bounded queue drain is estimated 2-4 us per signal plus cold height copy.

## SDF Projection Decision

Problem: The prompt requires terrain-to-voxel stitching, but no CPU-readable `VoxelSdfTexture3D` sampling contract exists in the terrain domain.
Solution: Implemented `HybridSdfHeightmapProjectionJob` as a Burst job that raymarches downward through a deterministic analytic SDF surrogate derived from the seam plan voxel volume. This keeps the seam math local, finite, and testable until an authoritative CPU SDF buffer/metadata contract exists.
Rejected Alternatives: GPU Texture3D readback was rejected because it would allocate/stall and violate the frame-time dictatorship. Directly depending on HectonVoxelEngine internals was rejected because it creates cross-domain coupling. Simulating high-resolution SDF columns was rejected as proton-counting.
Scalability potential: Low bypasses height deformation and uses only shader mask. Middle uses bounded patch raymarch. High/Ultra can replace the analytic surrogate with a real DataVault SDF buffer when a contract appears and spend saved cycles on denser hero seam normals.
Hardware Impact: Low-end silicon saves approximately 60-240 us per patch by bypassing deformation. High-end machines can afford the same job at higher patch/sample density.

## Legacy Skirt Removal Decision

Problem: Runtime cube `TerrainSkirt_` GameObjects hid seams instead of resolving height/texture continuity.
Solution: Removed terrain skirt generation and forced seam runtime rebuild versioning so old cached signatures rebuild without skirt children. Voxel collars and dither VFX remain for cave-side readability.
Rejected Alternatives: Keeping skirts as a fallback was rejected because it would mask broken math and add GameObject churn.
Scalability potential: Low keeps only cheap dither and global mask. High/Ultra can layer richer voxel collar shading without reintroducing terrain fake geometry.
Hardware Impact: Removes several primitive configure calls per seam, estimated 20-80 us per seam build plus transform/render overhead.

## Shader Mask Decision

Problem: Even with height projection, microscopic material mismatch reveals the seam.
Solution: TerrainMaster now samples `_HectonVoxelBlendMask` and raises rock weight inside the seam rect. The applier uploads a single R8 mask texture and global rect/params.
Rejected Alternatives: Per-material instance mutation was rejected because terrain materials are shared and would fragment batching. Full splatmap mutation was rejected because it is slower and harder to roll back.
Scalability potential: Low uses mask-only "dear lie". Middle uses mask plus small height projection. High/Ultra can increase mask resolution and add normal/detail overkill around hero cave mouths.
Hardware Impact: Low-end cost is one R8 texture sample only in the terrain shader; no managed allocation after cold texture resize.

## Black Box Decision

Problem: Terrain seam NaNs or bad bounds would otherwise leave no deterministic post-mortem evidence.
Solution: Added a 300-entry NativeArray ring recording frame, terrain hash, patch center, height range, blend max, flags, and state hash; fault path writes `Docs/AgentLogs/Dump_HYBRID_TERRAIN_BLENDER.bin`.
Rejected Alternatives: Debug.Log-only reporting was rejected because it loses frame history and is useless after a hard fault.
Scalability potential: Same 300-frame ring works across Low/Middle/High/Ultra; high-end can add extra visual diagnostics later without changing the crash evidence path.
Hardware Impact: Persistent ring is 19.2 KB. Steady-state write is one struct assignment per applied patch.

## Compile Block Decision

Problem: Full Unity compile cannot complete because the project currently has errors outside the Environment/Terrain domain.
Solution: Validated all changed C# scripts individually through Unity MCP and recorded the external blockers.
Rejected Alternatives: Editing `EcosystemDirector` or `Hecton8.UI.Tools` assembly resolution was rejected as out-of-domain architectural sabotage for this prompt.
Scalability potential: Terrain implementation remains isolated and ready for full compile once the external owners clear their blockers.
Hardware Impact: No runtime impact; verification status remains PENDING VERIFICATION until external compile blockers are removed.

## OMEGA POLISH CHANGES

Problem: The first Burst projection pass used generic `math.length`, one `math.normalize`, and repeated float divisions. That is acceptable for correctness but not acceptable under the Omega anti-bloat rules.
Solution: Replaced length/normalize with `math.rsqrt`-based helpers and converted hot float divisions to `math.rcp` multiplications. Added `HeightmapInvMaxIndex` so heightmap coordinate normalization is precomputed before the job. Removed a cold `$"..."` NativeMemorySentinel label allocation.
Rejected Alternatives: A lookup table was rejected because the seam SDF varies per plan and per position; a LUT would either alias visibly or need more cache/memory than the arithmetic it replaces. A compute shader was rejected for this pass because Unity Terrain height writeback would require CPU sync/readback and the task phase explicitly mandates Burst/TempJob.
Scalability potential: Low still takes the mask-only path. Middle gets cheaper per-sample projection. High/Ultra can spend the saved ALU on larger seam masks or denser hero-cave patch widths.
Hardware Impact: MX350/i3 saves estimated 3-8 us on a 16k-sample patch from reciprocal/rsqrt cleanup and avoids one cold managed string allocation per terrain baseline refresh.

## CONTINUATION UPGRADE PASS

Problem: The initial finite-difference normals were computed but not converted into extra visible seam quality, TempJob arrays were not visible to the native-memory audit layer, device-tier switching could flip immediately if quality settings changed, and fallback patch math still contained exact-distance patterns.
Solution: Added `HybridTerrainSeamMaskDetailJob` so High/Ultra tiers spend the normal output on slope-boosted R8 blend detail, registered/unregistered projection TempJob arrays with `NativeMemorySentinel`, added 180-frame Low-tier visual-only hysteresis, and replaced fallback `Vector2.Distance`/`.magnitude` style math with squared-distance plus `rsqrt` helpers.
Rejected Alternatives: Always running the normal/detail pass on MX350 was rejected because Low tier must buy visibility with shader mask only. GPU readback/compute was rejected again because Unity Terrain CPU writeback would still force synchronization. Rewriting external compile blockers was rejected as out-of-domain. Removing the remaining `Complete()` without a persistent Terrain patch scratch contract was rejected because `SetHeightsDelayLOD` needs CPU patch data now.
Scalability potential: Low/MX350 uses shader-only mask and stable hysteresis. Middle applies bounded projection without extra slope-detail work. High/Ultra spends saved cycles on richer seam mask detail and can later swap the analytic SDF surrogate for an authoritative DataVault SDF buffer.
Hardware Impact: Low silicon avoids the extra normal/detail chain, estimated 25-90 us saved per bounded patch versus always-on detail. The fallback no-sqrt cleanup saves an estimated 5-20 us on dense fallback trench/plan patches. NativeMemorySentinel bookkeeping is cold and gives audit evidence without managed hot-path allocation.

Regression Model: CPU risk is the remaining cold `finalHandle.Complete()` at terrain writeback. GC risk is controlled by TempJob arrays and static sentinel labels. Memory risk is bounded by per-patch TempJob arrays plus the persistent 300-entry black box. Cadence risk is controlled by bounded signal drain, Awaitable yield, and Low-tier hysteresis. Correctness risk remains the lack of a real CPU-readable `VoxelSdfTexture3D` contract; analytic SDF surrogate is intentionally marked as a dependency bridge.

## PROFILER AND SIGNAL DRAIN PASS

Problem: SlowTick signal ingestion could copy up to eight full terrain heightmaps before yielding, and the remaining Unity Terrain CPU bridge had no named profiler evidence path.
Solution: Added a 262144-sample drain budget to synchronous signal ingestion, added the same sample-budget yield trigger to the Awaitable path, and added static `ProfilerMarker`s for `H8.TerrainSeam.SignalDrain`, `H8.TerrainSeam.ProjectionFence`, `H8.TerrainSeam.BlendMaskUpload`, and `H8.TerrainSeam.HeightmapWriteback`.
Rejected Alternatives: Letting the existing count-only drain budget stand was rejected because one 1025 terrain tile is not equivalent to one small tile. Adding managed Stopwatch timing was rejected because profiler markers give better Player evidence with zero hot-path heap pressure. Editing macro database compile blockers was rejected as outside the Echelon 2 terrain domain.
Scalability potential: Low/MX350 copies at most one normal 513x513 heightmap per SlowTick before yielding work to a later tick; Middle/High/Ultra still drain multiple smaller tiles and now expose exact bridge samples for profiler captures.
Hardware Impact: Worst-case SlowTick copy bursts are capped from eight 513x513 maps to roughly one 513x513 map per synchronous pass, avoiding an estimated 7x copy spike in terrain-streaming bursts. Profiler markers add negligible runtime cost and provide measurement hooks for the pending bridge.

## DATAVAULT HEIGHTMAP PROVENANCE PASS

Problem: `BufferID.TerrainSeamHeightmap` is a shared DataVault buffer. Without provenance, a terrain patch could consume the last ingested heightmap from a different terrain tile and project a visually plausible but wrong seam.
Solution: Recorded terrain hash, frame, resolution, and cache revision when copying a MapMagic payload into the vault. Projection now accepts the vault buffer only when the current terrain hash and heightmap resolution match that provenance; otherwise it uses the persistent baseline heightmap. Black-box flags now record Low-tier visual-only, faulted, High-tier detail, and verified vault-heightmap states.
Rejected Alternatives: Allocating one DataVault buffer per terrain tile was rejected because the current vault API is keyed by `BufferID`, not terrain ID, and expanding it is a Core ownership change. Direct per-terrain persistent heightmap copies were rejected because they duplicate MapMagic data and increase resident memory. Ignoring the mismatch risk was rejected because cross-tile data contamination is worse than falling back to baseline.
Scalability potential: Low and Middle tiers avoid incorrect deformation with no extra allocation. High/Ultra get correct height provenance before spending extra normal/detail work. Future Core work can replace the single buffer with keyed vault aliases without changing the projection job contract.
Hardware Impact: Added cost is one terrain hash compare and one resolution compare per patch projection. Avoided cost is bad patch deformation and the follow-on collider/voxel dirty churn it would trigger.

## HEIGHTMAP WRITEBACK GATING PASS

Problem: Low-tier shader-mask seams and no-op fallback paths could still force `TerrainData.SetHeightsDelayLOD`, `SyncHeightmap`, and voxel dirty events even when no height sample changed.
Solution: Split hybrid projection success from actual heightmap mutation. The Burst path now reports `heightmapChanged`; fallback plan and trench paths return real delta status; Unity Terrain writeback runs only when current deformation changed samples or an older deformation must be restored. Voxel dirty events are emitted only for actual hybrid height changes, and restore writebacks use the same profiler marker as normal writeback.
Rejected Alternatives: Treating every active seam as a height change was rejected because it burns CPU on Low tier and dirties physics/collider systems for visual-only work. Removing the shader-mask path was rejected because it is the required MX350 cinematic cheat. Moving writeback to a new async terrain patch contract was rejected in this pass because Unity Terrain still requires CPU data and that broader contract needs Integrator/Core ownership.
Scalability potential: Low/MX350 gets mask-only seams without TerrainData churn. Middle avoids no-op fallback writes. High/Ultra still get full deformation and detail when samples actually change, while all writebacks remain visible under profiler markers.
Hardware Impact: Low-tier visual-only seams save the full TerrainData writeback and collider dirty cascade for every no-change patch. Estimated saved cost remains pending profiler capture, but the avoided work is the heaviest remaining cold bridge in this component.

## DIRTY RECT TRACKING PASS

Problem: The writeback path used a union of previous and current seam rectangles to restore old deformation and apply the new seam. After a real current height change, storing that union back into `previousRect` retained already-restored terrain and could force repeated TerrainData writebacks on later ticks.
Solution: Kept the union rectangle only as the temporary writeback patch, then stored the clamped active current seam rectangle as `previousRect` after real height changes. If an impossible edge case reports a change with an empty active rect, the code falls back to the applied rect to keep restoration state valid.
Rejected Alternatives: Storing the union was rejected because it grows dirty state beyond actual current deformation. Storing only exact changed samples was rejected because Unity Terrain APIs operate on rectangular patches and per-sample sparse tracking would add complexity without matching the API surface.
Scalability potential: Low/MX350 already avoids mask-only writeback; Middle/High/Ultra now avoid dragging restored stale rectangles through future deformation ticks.
Hardware Impact: Prevents repeated `SetHeightsDelayLOD`/`SyncHeightmap` over restored old terrain. Exact savings depend on seam movement, pending Player profiler capture.

## BLEND MASK LIFECYCLE PASS

Problem: `_HectonVoxelBlendMask` is a global shader binding. If a previous seam uploaded a mask and a later reconciliation only restored terrain, had no integration director, or processed trench-only deformation, the stale global mask could remain visually active even though no current seam mask existed.
Solution: Added per-pass upload tracking and a guarded global-disable path. Uploading a real seam mask marks the global state active; reconciliation disables the global mask whenever the current pass does not upload a fresh mask. `OnDisable` now uses the same guarded path so the internal active flag stays coherent.
Rejected Alternatives: Clearing the global mask every SlowTick was rejected because it would add redundant global shader state churn during stable active seams. Keeping the stale mask was rejected because it can paint voxel-rock blending onto unrelated restored terrain. Mutating terrain splatmaps was rejected again because the shader mask is the cheap reversible cinematic path.
Scalability potential: Low/MX350 keeps the mask-only fake but no longer leaks it after the seam disappears. Middle/High/Ultra keep full visual overkill around current seams without stale cross-terrain bleed.
Hardware Impact: Adds two boolean writes on upload and one branch after reconciliation. It avoids unnecessary terrain shader rock blending over stale regions and prevents debugging time wasted on phantom material seams.

## MIXED PLAN AND SDF CROSSING PASS

Problem: When a terrain patch had at least one hybrid seam plan, successful hybrid projection skipped the fallback plan path for all plans on that terrain. Non-hybrid terrain blend plans sharing the same patch could be dropped. Separately, raymarch root interpolation clamped `previousSdf - sdf` to a positive denominator, so negative-to-positive SDF crossings snapped to the previous sample instead of interpolating.
Solution: Fallback deformation now runs for non-hybrid plans even when hybrid projection succeeds, while hybrid plans avoid duplicate application. The SDF raymarch crossing code now preserves denominator sign and only substitutes a signed epsilon when the denominator is near zero.
Rejected Alternatives: Running fallback for every plan after hybrid projection was rejected because it would double-apply hybrid deformation. Leaving the positive denominator clamp was rejected because it biases crossings and can produce stepped seam contact heights. Increasing raymarch step count was rejected because it spends more CPU while preserving the interpolation bug.
Scalability potential: Low/MX350 keeps mask-only hybrid seams but still respects cheap fallback deformation for non-hybrid terrain features. Middle/High/Ultra get more stable SDF contact placement without increasing raymarch steps.
Hardware Impact: Mixed-plan fix adds one branch per plan in cold patch application. Signed interpolation has negligible ALU cost and avoids the need to raise `RaymarchStepCount`, saving the 2x-4x sample-cost alternative.

Exact cinematic cheats used:
- Analytic ellipsoid SDF surrogate from seam plan volume instead of real `VoxelSdfTexture3D` readback until a CPU SDF contract exists.
- R8 global terrain blend mask instead of splatmap mutation.
- Existing dithered voxel alpha/AlphaToMask path instead of microscopic geometry stitching.
- Low-tier shader-only concealment, no height deformation.

Final Git Diff:
See working tree diff for:
- `Assets/_Project/Scripts/World/Contracts/TerrainChunkGeneratedSignal.cs`
- `Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs`
- `Assets/_Project/Scripts/World/Terrain/Hecton8.World.Terrain.asmdef`
- `Assets/_Project/Scripts/World/Terrain/HybridTerrainSeamJobs.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologySeamExecutionDirector.cs`
- `Assets/_Project/Art/Shaders/TerrainMaster.shader`
- `Assets/_Project/Scripts/MapMagicBridge.cs`
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- `Assets/_Project/Scripts/World/WorldGenerativeGeologyTelemetry.cs`
