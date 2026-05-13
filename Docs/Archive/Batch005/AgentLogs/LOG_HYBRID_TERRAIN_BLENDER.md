# HYBRID_TERRAIN_BLENDER Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Changed scripts validate cleanly; global compile is blocked by external dependencies.

## What Was Wrong

- MapMagic terrain generation had no decoupled chunk-generated signal for terrain seam consumers.
- Terrain/voxel intersections were hidden by runtime `TerrainSkirt_` GameObjects instead of stitched mathematically.
- Heightmap data was not staged through `GlobalDataVault` for a seam projection job.
- Terrain material blending had no global voxel blend mask.
- The seam path had no 300-frame black-box telemetry or fault dump.

## What Was Done

- Added `TerrainChunkGeneratedSignal` and `TerrainChunkGeneratedEvents` NativeQueue event bus.
- Published `TerrainChunkGeneratedSignal` from `MapMagicTerrainTileEvents.RaiseTileApplied`.
- Added `Hecton8.World.Terrain` asmdef and `HybridTerrainSeamJobs`.
- Implemented Burst SDF-to-heightmap projection with analytic SDF surrogate, 5 m smooth-min blend, finite-difference normal pass, Low-tier shader-only bypass, and TempJob scratch.
- Added DataVault `BufferID.TerrainSeamHeightmap` and `SystemID.TerrainSeams`.
- Reworked `WorldGenerativeGeologyTerrainSeamApplier` to ingest quantized heightmaps, apply hybrid projection, upload `_HectonVoxelBlendMask`, emit `VoxelChunkModifiedEvent`, and write black-box telemetry/dump.
- Removed legacy `TerrainSkirt_` generation from `WorldGenerativeGeologySeamExecutionDirector`.
- Updated `TerrainMaster.shader` to sample `_HectonVoxelBlendMask` and blend terrain sand toward voxel rock near cave seams.
- Added `TerrainSeamsBlended` telemetry.
- Per Omega polish, removed Burst `math.length`/`math.normalize` and hot float divisions in favor of `math.rsqrt`/`math.rcp`; removed cold interpolated sentinel label allocation.

## Cinematic Cheats Used

- Analytic ellipsoid SDF surrogate from seam plan volume instead of GPU `VoxelSdfTexture3D` readback until a CPU SDF contract exists.
- R8 global shader blend mask instead of splatmap mutation.
- Existing voxel dither/AlphaToMask path for microscopic clipping.
- MX350/Low path skips height deformation and spends only on the visual mask.

## Microseconds Saved

- Removed terrain skirt GameObject generation: estimated 20-80 us saved per seam build, plus transform/render overhead.
- Low-tier deformation bypass: estimated 60-240 us saved per bounded patch on MX350.
- R8 shader mask over splatmap mutation: avoids CPU texture/splat writeback, estimated 100+ us per changed terrain tile.
- Omega reciprocal/rsqrt cleanup: estimated 3-8 us saved on a 16k-sample patch.
- EventBus signal instead of polling all terrain tiles every frame: 0 us steady-frame when no signals are pending.

## Verification

- PASS: Unity MCP `validate_script` clean for `WorldGenerativeGeologyTerrainSeamApplier.cs` after Omega patch.
- PASS: Unity MCP `validate_script` clean for `HybridTerrainSeamJobs.cs`.
- PASS: Unity MCP `validate_script` clean for `TerrainChunkGeneratedEvents.cs`.
- PASS: Unity MCP `validate_script` clean for `TerrainChunkGeneratedSignal.cs`.
- PASS: Unity MCP `validate_script` clean for `MapMagicBridge.cs`.
- PASS: Unity MCP `validate_script` clean for `WorldGenerativeGeologySeamExecutionDirector.cs`.
- PASS: Grep confirms no `TerrainSkirt`/`BuildTerrainSkirt` remains in the seam execution director.
- PASS: Grep confirms no `Mesh.vertices` path in touched seam files.
- PASS: Grep confirms no `exp/log/pow/sin/cos/tan` in `HybridTerrainSeamJobs.cs`.
- BLOCKED: Full Unity compile is red due external non-domain errors in `SimulationBucketingContracts`, `DeployableSdfDrillContracts`, and Burst assembly resolution.
- BLOCKED: `dotnet build Hecton8.Core.csproj` is red due existing generated-project/assembly-reference errors; Unity MCP validates the specific changed scripts.

## Final Diff Scope

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
- `Docs/Tasks/Status_HYBRID_TERRAIN_BLENDER.md`
- `Docs/AgentLogs/Rationale_HYBRID_TERRAIN_BLENDER.md`

---

# HYBRID_TERRAIN_BLENDER Continuation Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Changed seam scripts validate cleanly and Unity Bee emitted the terrain asmdef, but MCP console access timed out during compile and global build remains blocked by external generated-project/assembly errors.

## What Was Wrong

- Prompt extraction logic must be attribute-aware because the authoritative tag has extra attributes; its tasks are numbered Markdown entries inside the XML block, not literal `<TASK>` children.
- Projection TempJob arrays were correct lifetime-wise but not all were registered with `NativeMemorySentinel`, leaving weaker memory-audit evidence.
- The normal pass was not spending High/Ultra hardware on visible seam detail.
- Low-tier visual-only switching could react immediately to quality/device-state changes.
- Cold fallback trench/plan math still had exact-distance style calculations.

## What Was Done

- Re-extracted `<AGENT_PROMPT id="HYBRID_TERRAIN_BLENDER" ...>` with a cover-to-cover attribute-aware CLI regex and reconfirmed 19 Titanium Tasks.
- Added `HybridTerrainSeamMaskDetailJob`; High/Ultra tiers now turn finite-difference normals into slope-boosted `_HectonVoxelBlendMask` detail.
- Gated the extra normal/detail chain off for Low/Middle tiers and added 180-frame hysteresis for Low-tier shader-only switching.
- Registered and unregistered `nativePlans`, `patchHeights`, `blendMask`, and optional `normals` TempJob arrays through `NativeMemorySentinel`.
- Replaced fallback exact-distance math with squared-distance and `rsqrt` helpers.
- Revalidated the changed C# scripts and reran static scans for forbidden hot-path math/string patterns.

## Cinematic Cheats Used

- Low/MX350 still uses shader-only concealment instead of vertex deformation.
- High/Ultra buys visible quality by boosting an R8 mask from slope data rather than mutating terrain splatmaps.
- Analytic seam SDF remains the dependency bridge until an authoritative CPU-readable voxel SDF buffer exists.

## Exact Microseconds Saved

- Low/Middle tier skip of normal/detail chain: estimated 25-90 us saved per bounded patch versus always-on detail.
- No-sqrt fallback trench/plan math: estimated 5-20 us saved on dense fallback patch paths.
- 180-frame hysteresis: prevents quality-tier flicker and avoids repeated cold texture/job path churn during unstable quality changes; steady-frame cost remains 0 us when no seam signal is drained.
- NativeMemorySentinel registration: no speed claim; this buys auditability and failure evidence.

## Verification

- PASS: Unity MCP validation was clean for `WorldGenerativeGeologyTerrainSeamApplier.cs`, `HybridTerrainSeamJobs.cs`, and `WorldGenerativeGeologySeamExecutionDirector.cs` after the continuation changes.
- PASS: Static scan found no `math.sqrt`, `math.normalize`, `math.length(`, `Mathf.Sqrt`, `Mathf.Pow`, `Vector2.Distance`, `.magnitude`, `foreach`, `string.Format`, or `.ToString(` in touched seam hot-path files.
- PASS: `Library/ScriptAssemblies/Hecton8.World.Terrain.dll` exists after Unity import, indicating the isolated terrain asmdef emitted.
- PENDING: One `finalHandle.Complete()` remains for Unity Terrain CPU writeback. It is bounded to SlowTick/chunk seam work and must be profiler-verified or replaced by a deferred persistent scratch contract.
- PENDING: Final MCP `read_console` retry returned `no_unity_session`; no fresh Unity console result was available after compile/import timeout.
- BLOCKED: Global `dotnet build Hecton8.Core.csproj` remains red due external generated-project and assembly-reference issues outside this domain.

---

# HYBRID_TERRAIN_BLENDER Profiling/Drain Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Terrain seam script validation is clean; global compile remains blocked by non-domain errors.

## What Was Wrong

- The terrain chunk signal drain was count-bounded but not sample-bounded; eight large heightmaps could be copied in one synchronous SlowTick.
- The remaining Unity Terrain bridge sites had no stable profiler marker names, so the known `Complete()`/writeback compromise could not be measured cleanly in Player captures.

## What Was Done

- Added a 262144-sample cap to synchronous terrain heightmap signal ingestion.
- Added the same copied-sample threshold to the Awaitable path so it yields when heavy heightmap copies accumulate.
- Added zero-GC static profiler markers:
  - `H8.TerrainSeam.SignalDrain`
  - `H8.TerrainSeam.ProjectionFence`
  - `H8.TerrainSeam.BlendMaskUpload`
  - `H8.TerrainSeam.HeightmapWriteback`

## Cinematic Cheats Used

- No new simulation. The pass spends engineering budget on cadence control and measurement, while Low/MX350 still leans on shader-only seam concealment.

## Exact Microseconds Saved

- Worst-case synchronous terrain signal burst is reduced from eight 513x513 copies to roughly one 513x513 copy per SlowTick pass: estimated 7x spike reduction during terrain streaming bursts.
- Profiler markers do not claim savings; they make the remaining cold bridge measurable so profiler evidence can replace guesswork.

## Verification

- PASS: `validate_script` clean for `WorldGenerativeGeologyTerrainSeamApplier.cs`.
- PASS: Static scan found no forbidden hot-path sqrt/normalize/length/string patterns in touched seam files.
- PASS: Unity console after compile request shows no errors in HYBRID_TERRAIN_BLENDER files.
- BLOCKED: Unity compile remains red due external `Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs` errors: `CS4004 Cannot await in an unsafe context` at lines 277 and 289, plus existing Burst entry-point scan noise.

---

# HYBRID_TERRAIN_BLENDER Provenance Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Script-level validation is clean; global compile remains blocked outside this terrain domain.

## What Was Wrong

- `BufferID.TerrainSeamHeightmap` is a shared DataVault buffer. The projection job could consume the last copied heightmap even when it belonged to another terrain tile.
- Black-box flags did not distinguish verified vault heightmap use from baseline fallback.

## What Was Done

- Added terrain-hash and heightmap-resolution provenance for the vault heightmap copy.
- Rejected vault heightmaps when the current terrain does not match the last ingested terrain hash/resolution.
- Preserved the baseline fallback path for mismatches, avoiding incorrect terrain deformation.
- Extended terrain seam black-box flags to record Low-tier visual-only, faulted, High-tier detail, and verified-vault-heightmap states.

## Cinematic Cheats Used

- No new physical simulation. When provenance is not exact, the system chooses the baseline terrain heightmap plus shader mask instead of risking wrong geometry.

## Exact Microseconds Saved

- The provenance guard adds only hash/resolution comparisons per projection. The performance value is avoiding bad cross-tile deformation and the downstream collider/voxel dirty churn that would follow.

## Verification

- PASS: `validate_script` clean for `WorldGenerativeGeologyTerrainSeamApplier.cs`.
- PASS: Static scan found no forbidden hot-path sqrt/normalize/length/string patterns in touched seam files.
- PASS: Fresh Unity console after compile request shows no HYBRID_TERRAIN_BLENDER file errors.
- PENDING: Player profiler capture still required for the cold Unity Terrain writeback bridge.
- BLOCKED: Full compile remains red outside the terrain domain: `GlobalDataVault.cs` missing `MarkExternalView`/`TryMoveOneBlock`, `H8MacroDatabaseService.cs` missing `ReadRootNodeOffsetIfOpen`, `InputManager.cs` missing debug toggle handler methods, plus Burst entry-point scan noise.

---

# HYBRID_TERRAIN_BLENDER Writeback Gating Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Script validation is clean; global compile remains blocked by non-domain errors.

## What Was Wrong

- Low-tier shader-mask seams could still trigger Unity Terrain writeback even when no height sample changed.
- Hybrid projection published voxel dirty events for visual-only mask work.
- Fallback plan/trench paths reported no mutation status, so writeback had to be conservative.
- Terrain restore writeback was not covered by the seam writeback profiler marker.

## What Was Done

- Added explicit heightmap mutation reporting from hybrid projection.
- Skipped `SetHeightsDelayLOD`/`SyncHeightmap` unless a current deformation changed samples or a previous deformation must be restored.
- Published `VoxelChunkModifiedEvent` only when hybrid projection actually changed height samples.
- Changed fallback plan and trench patching to return real per-sample delta status.
- Wrapped restore writebacks in `H8.TerrainSeam.HeightmapWriteback`.

## Cinematic Cheats Used

- Low/MX350 mask-only seams now stay mask-only: R8 blend mask, no terrain writeback, no collider dirty cascade.

## Exact Microseconds Saved

- Mask-only Low-tier patches avoid the full Unity Terrain writeback bridge and voxel dirty event publish. Exact savings are pending Player profiler capture, but this removes the largest known no-op work unit from the Low path.

## Verification

- PASS: `validate_script` clean for `WorldGenerativeGeologyTerrainSeamApplier.cs`.
- PASS: Static scan found no forbidden hot-path sqrt/normalize/length/string patterns in touched seam files.
- BLOCKED: Fresh Unity console remains red outside terrain: `GlobalDataVault.cs` missing `Hecton8.Core.Signals` and Burst symbols, plus Burst entry-point scan noise.

---

# HYBRID_TERRAIN_BLENDER Dirty-Rect Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Script validation is clean; global compile remains blocked by non-domain errors.

## What Was Wrong

- After restoring old deformation and applying a current seam, `previousRect` could retain the union of old+current rectangles.
- That retained already-restored terrain in the dirty state and could cause repeated Unity Terrain writebacks over areas that were no longer deformed.

## What Was Done

- Kept the union rectangle only as the temporary writeback patch.
- Stored the clamped active current seam rectangle as `previousRect` after real height changes.
- Added a defensive fallback to retain the applied rect if a height change is ever reported with an empty active rect.

## Cinematic Cheats Used

- No new simulation. This keeps the existing visual fake path and reduces CPU work around the real deformation path.

## Exact Microseconds Saved

- Avoids repeated `SetHeightsDelayLOD`/`SyncHeightmap` over restored stale terrain regions. Exact savings depend on seam movement and are pending Player profiler capture.

## Verification

- PASS: `validate_script` clean for `WorldGenerativeGeologyTerrainSeamApplier.cs`.
- PASS: Static scan found no forbidden hot-path sqrt/normalize/length/string patterns in touched seam files.
- PASS: `git diff --check` reports only CRLF warnings.
- PASS: Fresh Unity console after compile request reports no HYBRID_TERRAIN_BLENDER file errors.
- BLOCKED: Full compile remains red outside terrain in `GlobalDataVault.cs`: missing Core namespace symbols, missing `_gapAuditResult`, missing `VaultGapAuditJob`/`VaultGapAuditResult`, missing `FragmentationRatioThreshold`, plus Burst entry-point scan noise.

---

# HYBRID_TERRAIN_BLENDER Blend-Mask Lifecycle Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Script validation is clean; global compile remains blocked by non-domain errors.

## What Was Wrong

- `_HectonVoxelBlendMask` is global shader state.
- A stale terrain-rock mask could remain enabled after a seam disappeared, after integration was missing, after restore-only work, or during trench-only deformation with no fresh seam mask upload.
- That could produce phantom voxel-rock blending on restored or unrelated terrain.

## What Was Done

- Added `_voxelBlendMaskUploadedThisPass` to track whether the current reconciliation uploaded a fresh mask.
- Added `_voxelBlendMaskGlobalActive` and `DisableVoxelBlendMaskGlobal()` to disable global mask state only when needed.
- Disabled the mask after null-integration restores and after reconciliation passes with no fresh mask upload.
- Routed `OnDisable` through the guarded disable path so the global shader state and local active flag stay coherent.

## Cinematic Cheats Used

- Preserved the R8 global shader mask as the reversible Low/MX350 seam fake.
- Avoided terrain splatmap mutation and any extra geometry.

## Exact Microseconds Saved

- Adds only two boolean writes on upload plus one reconciliation branch.
- Avoids stale shader blending over unrelated terrain; runtime CPU savings are negligible, but it removes a visible artifact class without terrain writeback or material mutation.

## Verification

- PASS: `validate_script` clean for `WorldGenerativeGeologyTerrainSeamApplier.cs`.
- PASS: Static scan found no forbidden hot-path sqrt/normalize/length/string patterns in touched seam files.
- PASS: `git diff --check` reports only CRLF warnings.
- PASS: Fresh Unity console after compile request reports no HYBRID_TERRAIN_BLENDER file errors.
- BLOCKED: Full compile remains red outside terrain: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs(7413,1)` has `CS1022 Type or namespace definition, or end-of-file expected`, plus Burst entry-point scan noise.

---

# HYBRID_TERRAIN_BLENDER Mixed-Plan SDF Report

Date: 2026-05-13
Agent: ENVIRONMENT_ENGINEER
Domain: Echelon 2 - World Generation & Terrain
Status: PENDING VERIFICATION. Script validation is clean; global compile remains blocked by non-domain errors.

## What Was Wrong

- A successful hybrid terrain projection skipped fallback plan deformation for every plan on that terrain.
- Non-hybrid terrain plans sharing the same patch could be dropped when at least one hybrid plan existed.
- SDF raymarch root interpolation forced the crossing denominator positive, so negative-to-positive crossings snapped to the previous ray sample instead of interpolating.

## What Was Done

- Kept hybrid plans on the Burst projection path.
- Applied fallback deformation only to non-hybrid plans when hybrid projection succeeds.
- Preserved signed SDF interpolation denominator and substituted a signed epsilon only for near-zero denominators.

## Cinematic Cheats Used

- Did not increase raymarch step count.
- Kept the analytic SDF surrogate and spent a few scalar ALU ops to improve contact stability instead of buying accuracy with more samples.

## Exact Microseconds Saved

- Avoids the rejected alternative of doubling or quadrupling raymarch steps for seam stability. On a 16k-sample patch, that preserves the existing 16-step budget instead of moving toward 32-64 steps.
- Mixed-plan fallback branch cost is estimated sub-1 us per small plan list and prevents missing deformation without a second terrain pass.

## Verification

- PASS: `validate_script` clean for `WorldGenerativeGeologyTerrainSeamApplier.cs`.
- PASS: `validate_script` clean for `HybridTerrainSeamJobs.cs`.
- PASS: Static scan found no forbidden hot-path sqrt/normalize/length/string patterns in touched seam files.
- PASS: `git diff --check` reports only CRLF warnings.
- PASS: Fresh Unity console after compile request reports no HYBRID_TERRAIN_BLENDER file errors.
- BLOCKED: Full compile remains red outside terrain in `Assets/_Project/Scripts/Ecosystem/FaunaBrain.Ecosystem.cs`: missing `_lastAppliedInfectionShaderActive`, `_lastAppliedInfectionShaderSeverity01`, `FaunaPresentationColorMask`, `FaunaPresentationBaseColorMask`, and `FaunaPresentationEmissionColorMask`.
