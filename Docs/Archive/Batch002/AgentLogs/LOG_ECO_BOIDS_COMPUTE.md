# LOG_ECO_BOIDS_COMPUTE

## 2026-05-11 - Spatial Hash Pass

Status: PENDING VERIFICATION

What was wrong:
Generic `Assets/_Project/Scripts/BoidSimulation.compute` still depended on a tiled full-neighbor scan. That kept the swarm path structurally tied to O(N^2) work even though the project already had a separate Sargassum-specific GPU grid. The generic lane had no GPU cell counter buffer, no build pass, and no host-side persistent grid buffers.

What was done:
- Added `ClearSpatialGrid` and `BuildSpatialGrid` kernels to `BoidSimulation.compute`.
- Added `RWByteAddressBuffer _SpatialGridCounts` and fixed-slot `RWStructuredBuffer<uint> _SpatialGridCells`.
- Implemented finite-position guards, negative-space-safe origin-offset hashing, 32x32x32 max grid resolution, and max 32 boids per cell.
- Replaced active neighbor accumulation with 27-cell lookup in `CSMain`.
- Bound persistent `GraphicsBuffer` grid storage from `HectonBoidController.cs`.
- Dispatch order is now clear grid -> build grid -> main simulation.
- Verified edited files contain no `GetData`, `AsyncGPUReadback`, or CPU boid-buffer readback.
- Recon scan found no real `void Update()`-based flocking scripts under `Assets/_Project/Scripts`.

Cinematic Cheats used:
- Fixed-slot cell occupancy instead of exact sorted neighbor lists. Predictable cap beats expensive perfect grouping on MX350.
- Origin-offset cell hashing instead of world-coordinate hash scatter. Stable grid indices are cheaper and easier to bound.
- Old tile path is preprocessor-disabled instead of runtime-disabled to avoid shader warnings.

Exact Microseconds saved:
No exact profiler result recorded. Fake microsecond numbers are rejected. Expected gain source is removal of global/tiled neighbor scans from the active generic boid path. Console verification confirms the previous BoidSimulation zero-iteration warning was removed after the preprocessor guard.

Compile evidence:
Unity refresh/import completed after reconnect. Console no longer reported `BoidSimulation.compute` warnings/errors. Global compile remains blocked by unrelated errors:
- `Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader(196)`: `_HectonMathLodMode` redefinition.
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`: missing `AupPreShiftSignal`.
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`: missing `IFixedTickable.FixedTick(float)`.
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: Burst unsupported catch/filter construction.

Integrator note:
Do not treat Task 15 as clean until those external compile blockers are resolved. The boid spatial hash itself imported without the prior thread-group loop warning after the fix.

## 2026-05-12 - Honest AAA R&D Upgrade Pass

Status: PENDING VERIFICATION

What was wrong:
The first spatial hash pass removed the worst global neighbor scan but still missed several core prompt requirements: shared-memory localized cell reads, SDF obstacle avoidance, abyssal flow advection, predator AUP evasion, Math LOD, panic/scatter metadata, and acoustic ping dispersion. The generic `BoidData` still used padding while the fish shader expected panic/state flags.

What was done:
- Added shared-memory staging for fixed spatial cell occupants in `BoidSimulation.compute`.
- Added cave voxel SDF binding from `HectonCaveVoxelLightingVolume` with a disabled fallback `Texture3D`.
- Added abyssal flow binding from `HectonFluidEngine.TryGetGpuAbyssalFlowFieldBuffer`.
- Added fixed 16-slot predator AUP upload and GPU predator escape/panic falloff.
- Added acoustic ping listener registration through `PhysicsEventBus` and radial compute kick uniforms.
- Replaced `BoidData` padding with `panic` and `stateFlags`, matching `BoidFishInstanced.shader`.
- Added `_BoidMathLodMode` so low tier disables alignment/cohesion while keeping separation.
- Fixed own compile errors caused by unsupported `Texture3D.GetRawTextureData` and direct unqualified `HectonFluidEngine` use.

Cinematic Cheats used:
- Fixed 16 predator slots instead of dynamic predator lists. Predictable cap, no managed churn.
- Flow sampling uses the published fluid buffer, not a duplicate boid-owned simulation.
- SDF normal uses finite differences from the existing cave texture instead of physics rays.
- Panic is a scalar plus one flag bit, leaving render/VFX to amplify it cheaply.
- Low-tier Math LOD preserves silhouette behavior by keeping separation and dropping social math first.

Exact Microseconds saved:
No exact profiler result recorded. Fake microseconds rejected. Expected saving sources are: no CPU boid readback, no per-boid acoustic/predator fan-out, reduced global neighbor reads via shared cell staging, and low-tier social accumulator skip. Exact numbers require a clean project compile and GPU profiler capture.

Compile evidence:
- Earlier `validate_script Assets/_Project/Scripts/HectonBoidController.cs`: 0 errors, 1 generic static warning about string concatenation in Update. Later MCP validation attempts timed out in the regex validator, so Unity console evidence is the current source of truth.
- Unity recompile after clearing console: no errors from `Assets/_Project/Scripts/HectonBoidController.cs` or `Assets/_Project/Scripts/BoidSimulation.compute`.
- Remaining hard blockers are outside the edited generic boid lane:
  - `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs(116,59)`: inaccessible `ResolveLegHomeLocal(int, int)`.
  - `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs(986,17)`: missing `TargetFootPositions` on `ProceduralCrabGroundRaycastBuildJob`.
  - `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs(436,17)`: missing `CaptureReceiverManagedRefs`.
  - `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs(456,13)`: missing `CaptureReceiverManagedRefs`.
  - `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs(607,17)`: missing `PublishCombatTelemetryAnomaly`.
  - `Assets/_Project/Scripts/SaveBinaryStorage.cs(7667,41)`: Burst BC1007 unsupported catch/filter construction.

Integrator note:
Tasks 7 and 9 remain pending. I did not fake BatchRendererGroup/indirect draw or compute frustum culling. Omega polish mandate is not eligible because the checklist is not 100% checked or blocked.

## 2026-05-12 - Indirect Draw And GPU Culling Pass

Status: PENDING VERIFICATION

What was wrong:
The generic boid render path still used `Graphics.RenderMeshPrimitives` and CPU AABB visibility. That was an honest miss against tasks 7 and 9: instance count was CPU-submitted and there was no GPU compacted visible list.

What was done:
- Added `ClearVisibleIndirectArgs` and `CullVisibleBoids` kernels to `BoidSimulation.compute`.
- Added persistent `_visibleBoidIndexBuffer` and raw `_visibleIndirectArgsBuffer` in `HectonBoidController.cs`.
- Uploaded static `GraphicsBuffer.IndirectDrawIndexedArgs` mesh fields only when `fishMesh` changes.
- Moved visible instance count ownership to GPU atomics at indirect args byte offset 4.
- Switched render submission to `Graphics.RenderMeshIndirect`.
- Updated `BoidFishInstanced.shader` to read `BoidData` through `_VisibleBoidIndices[SV_InstanceID]`.
- Reconfirmed edited generic boid files contain no `GetData`/`AsyncGPUReadback` render hot-path readback.

Cinematic Cheats used:
- Sphere-radius frustum test per boid instead of exact mesh bounds. It is conservative and cheap.
- Uniform six-plane upload from camera; no per-boid CPU culling.
- Raw indirect args instance-count reset preserves CPU-uploaded mesh topology and lets only count be GPU-owned.

Exact Microseconds saved:
No exact profiler result recorded. Fake numbers rejected. Expected savings are CPU-side: no CPU instance count decision, no CPU per-boid visibility list, and no `RenderMeshPrimitives` count submission. GPU cost is one cull dispatch plus six plane tests per boid; profiler capture is still required.

Compile evidence:
- `validate_script Assets/_Project/Scripts/HectonBoidController.cs`: 0 errors after this pass.
- Current Unity console after import reports no `HectonBoidController.cs`, `BoidSimulation.compute`, or `BoidFishInstanced.shader` errors.
- Current hard blockers are external:
  - `Assets/_Project/Scripts/VoxelDeltaProcessor.cs(3248,13)`: missing `VoxelChunkModifiedEvent`.
  - `Assets/_Project/Scripts/VoxelDeltaProcessor.cs(3248,57)`: missing `VoxelChunkModifiedEvent`.
  - `Assets/_Project/Scripts/VoxelDeltaProcessor.cs(3261,13)`: missing `VoxelChunkModifiedEvents`.

Integrator note:
Core ECO_BOIDS_COMPUTE tasks are now checked except Task 15, which remains dependency-blocked by non-boid compile errors. Exact performance claims remain pending profiler evidence.

## 2026-05-12 - Omega Polish Anti-Bloat Pass

Status: VERIFIED MASTER GRADE

What was wrong:
The indirect/culling implementation was functionally in place, but the code still carried two pieces of misleading residue: documentation references to the old `Graphics.RenderMeshPrimitives` path and an unused CPU `CheckFrustumVisibility()` AABB method. That was not runtime-expensive after the new path, but it was architectural bloat and a future regression trap.

What was done:
- Removed dead CPU `CheckFrustumVisibility()` from `HectonBoidController.cs`.
- Updated remaining render comments from `RenderMeshPrimitives` to `RenderMeshIndirect`.
- Re-ran scoped scans across `HectonBoidController.cs`, `BoidSimulation.compute`, and `BoidFishInstanced.shader`.
- Confirmed no `RenderMeshPrimitives`, `CheckFrustumVisibility`, `GetData`, `AsyncGPUReadback`, or readback references remain in the edited boid files.
- Confirmed added diff lines contain no new `foreach`, `string.Format`, interpolated strings, `.ToString()`, CPU/GPU readback, `Vector3.Distance`, `Mathf.Sqrt`, `math.sqrt`, `.normalized`, or `Random`.
- Ran `dotnet build .\Assembly-CSharp.csproj` per mandate. Multiprocess build failed with MSB4166 child-node crashes; single-process/no-restore runs exited 1 without actionable C# diagnostics. Unity console is still the actionable compiler surface.

Cinematic Cheats used:
- Conservative sphere-radius frustum test instead of exact fish mesh bounds.
- GPU visible-index compaction instead of CPU culling or CPU instance-count submission.
- Low-tier math drops alignment/cohesion first while keeping separation, preserving visual swarm readability on weak hardware.
- Panic/scatter remains one scalar plus one bit flag, leaving render/VFX to amplify cheaply.

Exact Microseconds saved:
No profiler capture exists because global compile is still blocked externally. Fake microseconds rejected. The measurable saving targets are CPU-visible instance count removal, CPU AABB cull removal, no readback, and one indirect draw fed by GPU visible indices.

Verification evidence:
- `validate_script Assets/_Project/Scripts/HectonBoidController.cs`: 0 errors after Omega polish.
- `git diff --check` on edited boid files: no whitespace errors, only Git LF-to-CRLF warnings.
- Unity console after refresh: no errors from `HectonBoidController.cs`, `BoidSimulation.compute`, or `BoidFishInstanced.shader`.
- Current external blocker: `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs(2534,13)` missing `WritePowerBlackBoxSample`.

Final scoped git diff:
```text
Assets/_Project/Scripts/BoidFishInstanced.shader |   5 +-
Assets/_Project/Scripts/BoidSimulation.compute   | 437 +++++++++++++++++-
Assets/_Project/Scripts/HectonBoidController.cs  | 543 +++++++++++++++++++++--
3 files changed, 939 insertions(+), 46 deletions(-)
```

Integrator note:
Do not mark global project compile clean until the external power-domain missing method is fixed. The ECO boid lane has no current console errors and no hot-path CPU readback in the edited files.
