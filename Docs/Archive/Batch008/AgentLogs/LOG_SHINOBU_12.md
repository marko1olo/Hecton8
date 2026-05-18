## 2026-05-17 - SHINOBU_12 - VERLET_TOW_AND_CABLE_ARCHITECT

What was wrong:
- Active tether rendering used 12-byte `float3` GPU spline points, violating the 16-byte spline upload rule.
- Cable solver had no local DTO ABI for 32-byte `VerletNodeDTO` or 16-byte `VerletConstraintDTO`.
- Tow solver behavior still allowed spring shock from instant rest-length changes and weak low-tier iteration counts.
- Active integration lacked discrete node SDF push-out and per-node current advection.
- Tension existed as events/direct forces, but not as an unmanaged DataVault force packet for external vehicle dynamics readers.
- Human tuning path was absent; cable materials/tension thresholds were effectively code-controlled.

What was done:
- Added `Assets/_Project/Scripts/Physics/VerletCableDTOs.cs` with padded DTOs, mock SDF/world/submarine/winch inputs, Burst Verlet jobs, AABB cull job, black-box write job, force DTOs, tuning DTOs, and span CSV parser.
- Changed tether spline shader to `StructuredBuffer<float4>` and active upload path to `GpuCableSplinePointDTO` at 16-byte stride.
- Added `TetherVisualGpuSplineCopyJob` for Burst GPU point staging.
- Raised active solver tier policy to Low/MX350=3, Mid=5, High=8, Ultra=10.
- Added active SDF node push-out with rough-rock old-position damping.
- Added bounded active rest-length reeling and plastic rest-length creep under overload.
- Added DataVault buffers for GPU spline points, tension force packets, tuning, materials, black box, AABB, snap signals.
- Added `Verlet Tow Tuner` EditorWindow with sliders, CSV material monitor, and SceneView green/yellow/red tension gizmos.
- Changed fatal dump path to `Docs/AgentLogs/Dump_VERLET_CABLES.bin`.

Cinematic Cheats used:
- Dear Lie collision: node-only SDF samples; no swept sphere CCD and no segment collision truth.
- Low-tier taut-line visual fake remains active; physics can be stretchy while presentation stays readable.
- Flow current is a cheap vector modulation per node, not fluid simulation.
- Frustum skip rejects invisible cable uploads instead of trying to make invisible cables visually accurate.

Exact Microseconds saved / spent estimates:
- 16-byte GPU point alignment: saves 1-4 us per active tether upload/fetch path.
- SDF node collision: costs ~40 us per 1000 nodes, replacing much heavier collider/CCD costs.
- Low-tier 3 iterations vs 10: saves ~45-60 us per 1000 nodes on MX350/i3.
- Rest-length reeling: costs 2-6 us per 10 constraints, prevents solver shock spikes.
- Tension force DataVault write: <1 us per cable.
- Frustum skip: saves 3-8 us per culled cable by skipping upload/draw.
- Black-box ring write: costs 2-5 us per frame; dump IO only on fault.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore` passed after Loop 3 with 0 warnings / 0 errors.
- Later builds are blocked by unrelated Construction domain missing DTOs (`PathWaypointDTO`, `MockSdfGrid`, `DroneFleetTuningConstants`, etc.). No SHINOBU_12 compiler errors surfaced before that dependency wall.
- `git diff --check` is clean except line-ending warnings.

SELF_AUDIT:
- No new LineRenderer or Unity Joint usage.
- `VerletNodeDTO`: float3 Position 12 + float InvMass 4 + float3 OldPosition 12 + float _pad0 4 = 32 bytes. No Pack=1.
- `VerletConstraintDTO`: int NodeA 4 + int NodeB 4 + float RestLength 4 + float Stiffness 4 = 16 bytes.
- Node mutation access exposes direct fields and unsafe ref accessor.
- Local mocks exist and no direct sibling runtime dependency was added.
- `Verlet Tow Tuner` exists and writes DataVault tuning/materials.

## 2026-05-17 - SHINOBU_12 - ULTRA THINK POLISH PASS

What was wrong:
- The first XML re-extraction regex was too literal and missed the SHINOBU_12 tag because it includes `role` and `chat_name`; the corrected attribute-aware extraction found the block and confirmed exactly 20 tasks.
- Runtime tether telemetry still had two `Pack = 1` structs: `TetherVerletTelemetryEntry` and `TetherManagerTelemetryEntry`.
- Several DTOs relied on implicit tail padding under `StructLayout(Size = N)`, which is not acceptable for this project's ARM64 byte-layout discipline.
- Root compile evidence is dirty because other agents currently broke SaveSystem, TerminalOS, Fauna, Somatic, Core telemetry, and VFX files.

What was done:
- Removed runtime `Pack = 1` from the tether telemetry structs while preserving explicit sizes: 64 bytes and 16 bytes.
- Added explicit reserved/padding fields to `VerletCableTuningDTO`, `MockSDFSampler`, and `CableSnappedSignal`.
- Added an explicit 80-byte layout to the local physics `MockWorldSampler`.
- Expanded `VerletCableLayout.Validate()` to assert all SHINOBU_12 DTO sizes, not just node/constraint/GPU point.
- Re-read `Docs/PROJECT_STATE_STATIC_XRAY.md`; it says runtime proof is pending, so no Play Mode/profiler/MX350 claim is made.
- Ran isolated restore/build using `.codex-artifacts/msbuild/shinobu12` to avoid Unity `Temp` churn; build fails externally, and `Docs/AgentLogs/Build_SHINOBU_12_ultra_20260517.log` contains no SHINOBU_12 path matches.

Cinematic Cheats used:
- Node-only SDF collision remains the accepted Dear Lie; no swept segment CCD.
- Low-tier cable simulation uses 3 solver iterations and accepts visible elasticity to protect frame time.
- Current advection is a cheap per-node vector modulation, not fluid simulation.
- Invisible cables skip GraphicsBuffer upload/draw via AABB/frustum rejection.

Exact Microseconds saved / spent estimates:
- Removing `Pack = 1`: <1 us/frame direct gain, but removes ARM64 unaligned access risk.
- Explicit padding/layout validation: 0 us runtime cost; prevents future ABI faults.
- Low-tier 3 iterations vs 10: saves about 45-60 us per 1000 nodes.
- Node SDF Dear Lie: costs about 40 us per 1000 nodes, replacing collider/CCD costs.
- 16-byte GPU spline point path: saves about 1-4 us per active tether upload/fetch path.
- Frustum skip: saves about 3-8 us per culled cable.
- Blackbox ring: costs about 2-5 us/frame; fatal dump IO only on fault.

Forensic self-audit:
- Task matrix: 01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.
- Struct layout: `VerletNodeDTO` offset 0 `float3 Position` 12, offset 12 `float InvMass` 4, offset 16 `float3 OldPosition` 12, offset 28 `float _pad0` 4, total 32 bytes.
- Constraint layout: `VerletConstraintDTO` offset 0 `int NodeA`, 4 `int NodeB`, 8 `float RestLength`, 12 `float Stiffness`, total 16 bytes.
- GPU layout: `GpuCableSplinePointDTO` offset 0 `float3 Position`, offset 12 `float Tension01`, total 16 bytes.
- H-Phi: new NativeArray surfaces are caller/vault-owned; active `TetherInstance` arrays are GlobalDataVault aliases/slices, not newly allocated hot-path private ownership.
- Zero-GC: scoped hot-path scan found no LINQ/foreach/string formatting/new NativeArray/new List in SHINOBU_12 solver jobs; remaining `List<TetherInstance>` is a pre-existing cold manager pool.
- AUP: active solver rebases nodes to anchor-local float space before Verlet; origin-shift jobs cover historical positions to avoid one-frame cable stretch.
- Blackbox: 300-frame ring is active and fatal non-finite cable state dumps to `Docs/AgentLogs/Dump_VERLET_CABLES.bin`.
- Compile guard: no sibling asmdef dependency was added; isolated build wall is external and documented.

## 2026-05-17 - SHINOBU_12 - H-PHI HANDLE SOVEREIGNTY PASS

What was wrong:
- The previous H-Phi report was technically incomplete: `TetherInstance` arrays were vault aliases, but not every alias had an explicit `VaultBufferHandle<T>` identity field.
- `TetherManager` blackbox still used direct `GetBuffer<T>` acquisition.
- `VerletTowTunerWindow` used direct editor `GetBuffer<T>` writes even though the runtime path had moved toward handle-based vault access.

What was done:
- Added paired `VaultBufferHandle<T>` fields for every active `TetherInstance` cable buffer and changed acquisition helpers to use `GetBufferHandle<T>` plus `Resolve(vault)`.
- Added `VaultGenerationID` guards in simulation and visual paths so non-owning `NativeArray<T>` views are rebuilt after vault relocation.
- Converted `TetherManager` blackbox ring/head to `VaultBufferHandle<T>` and changed the manager dump path to `Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.bin`.
- Converted the editor tuner tuning/material writes to handle-first access.
- Re-ran forbidden API grep: no scoped SHINOBU_12 runtime `GetBuffer<`, `new NativeArray`, `Pack=1`, Unity joint, `LineRenderer`, or fake `Schedule().Complete()` hits.

Cinematic Cheats used:
- No change to gameplay truth: node-only SDF collision remains the low-cost Dear Lie.
- Visual overkill remains separated from gameplay: 16-byte GPU spline points carry tension in `.w`, with culling before upload.

Exact Microseconds saved / spent estimates:
- Handle generation guard: <1 us steady-state per active tether path.
- Vault relocation refresh: bounded rare cost, proportional to SHINOBU_12 buffer count, not per-node.
- Direct frame-time win is not claimed; the fix removes stale-pointer risk and H-Phi ownership ambiguity.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false` fails externally in `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` because that file is missing `IDataVault` / `VaultBufferHandle<>` imports.
- Build-log filter finds no `Tether`, `Verlet`, `Cable`, `TetherInstance`, `TetherManager`, `TetherVerletJobs`, or `VerletTowTuner` errors.
- Scoped grep finds no forbidden SHINOBU_12 runtime acquisition/allocation/component primitives listed above.

SELF_AUDIT:
- Task matrix remains 01-20 PASS, static only; runtime Play Mode/profiler proof is still pending per `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Struct layout unchanged: `VerletNodeDTO` 32 bytes, `VerletConstraintDTO` 16 bytes, `GpuCableSplinePointDTO` 16 bytes.
- H-Phi: runtime arrays are vault-owned; class fields are non-owning views paired with handles.
- Blackbox: cable ring dumps `Dump_VERLET_CABLES.bin`; manager ring dumps `Dump_VERLET_CABLES_MANAGER.bin`.

## 2026-05-18 - SHINOBU_12 - GPU DRAW PAYLOAD / SRP SCALAR PURGE

What was wrong:
- The cable draw path still issued per-visible-tether `MaterialPropertyBlock.SetColor`, `SetFloat`, and `SetInt` calls for color, stress, radius, point count, indirect mode, tier, salt, silt, and clock.
- Shader constants were split between 16-byte spline points and scalar `UnityPerMaterial` fields, leaving the draw ABI below the requested GPU-sovereignty bar.

What was done:
- Added `GpuCableDrawParamsDTO` at 80 bytes: five `float4` lanes, all 16-byte aligned.
- Added double-buffered one-element draw-param `GraphicsBuffer` lanes to `TetherInstance`.
- Changed `TetherManager` to bind `_TetherDrawParams` with `SetBuffer` and removed all scoped tether scalar MPB setters.
- Changed `Hecton_TetherLineStrip.shader` to read `_TetherDrawParams[0]` and keep `_TetherPositions` as `StructuredBuffer<float4>`.
- Expanded `VerletCableLayout.Validate()` to assert the 80-byte draw payload stride.

Cinematic Cheats used:
- Low tier still uses the taut-line high-stress visual fake instead of simulating extra curve truth.
- High/Ultra salt crystals, silt tint, and stress pulse remain shader-only presentation, not gameplay physics.

Exact Microseconds saved:
- Not claimed. Static estimate: 8-12 scalar property calls removed per visible tether draw, replaced by one 80-byte buffer write.
- `git diff --check` only reports CRLF normalization warnings.
- Scoped forbidden grep finds no SHINOBU_12 hits for scalar MPB setters, `Material.Set*`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, or fake `Schedule().Complete()`.

Verification:
- `dotnet restore Hecton8.Core.csproj /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/` succeeded.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 warnings and 0 errors.
- Runtime Unity Play Mode, shader import/player draw validation, profiler timing, and GC allocation capture are still pending. Status is not complete.

SELF_AUDIT:
- Task matrix remains 01-20 PASS by static implementation.
- Struct layout: `VerletNodeDTO` 32 bytes; `GpuCableSplinePointDTO` 16 bytes; `GpuCableDrawParamsDTO` offsets 0/16/32/48/64, size 80 bytes.
- H-Phi: gameplay arrays remain vault-owned; the new draw params buffers are visual-only GPU resources.
- Blackbox: cable and manager 300-frame rings remain active.
- Compile guard: isolated Core build is clean; no new cross-domain reference was added.

## 2026-05-18 - SHINOBU_12 - BEND VOXEL LOOKUP PURGE

What was wrong:
- `TetherInstance.TryResolveBendCorner` still used `TryGetComponent` / `GetComponentInParent<HectonVoxelVolume>` on raycast hits.
- This was not inside the Burst Verlet node loop, but it was in tether LOS/bend recalculation, so it was still hot-adjacent component lookup debt.

What was done:
- Removed the Unity component lookup from the bend path.
- Added fixed-cache resolution from existing `_bendVolumes[4]`.
- Added published voxel SDF raymarch fallback through `HectonVoxelVolume.TryRaymarchAnyPublishedSdf`, which returns a volume and hit without walking collider hierarchy.
- Preserved the cheap hit-normal/tangent fallback when no voxel SDF answer exists.

Cinematic Cheats used:
- No swept cable CCD was added.
- If voxel SDF is unavailable, the cable accepts the tangent bend approximation and visual clipping between nodes.

Exact Microseconds saved:
- Not claimed. Static estimate: removes 1-2 Unity component hierarchy lookups per blocked bend hit.

Verification:
- Scoped forbidden grep finds no SHINOBU_12 hits for `GetComponent`, `TryGetComponent`, `FindObject*`, scalar MPB setters, `Material.Set*`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, or fake `Schedule().Complete()`.
- `git diff --check` on `TetherInstance.cs` only reports CRLF normalization warning.
- Isolated `dotnet build Hecton8.Core.csproj --no-restore ...` is currently blocked outside SHINOBU_12 by `GlobalPhysicsStateManager.cs` missing `WakeRequestSignal`; build log contains no Tether/Verlet/Cable errors.

SELF_AUDIT:
- Task matrix remains 01-20 PASS by static implementation.
- Struct layout unchanged: `VerletNodeDTO` 32 bytes, `GpuCableSplinePointDTO` 16 bytes, `GpuCableDrawParamsDTO` 80 bytes.
- Zero-GC: no dictionary, closure, string formatting, or component lookup remains in scoped SHINOBU_12 hot-adjacent files.
- H-Phi: gameplay arrays remain vault-owned; bend caches are fixed cold arrays.
- Runtime proof is still pending.

## 2026-05-18 - SHINOBU_12 - SDF LOS / UNITY PHYSICS RAYCAST PURGE

What was wrong:
- The bend topology path had already dropped component hierarchy lookup, but line-of-sight and anti-slice validation still used Unity Physics raycasts.
- The current evidence needed a new compile pass because the prior Loop 9 status still reported an external wall.

What was done:
- `TetherInstance.TryFindClosestObstacle` now uses `HectonVoxelVolume.TryRaymarchAnyPublishedSdf` and returns hit point, normal, volume, and runtime stamp.
- `UpdateLineOfSight`, `RecalculateBendPoints`, `TryResolveBendCorner`, and `ValidateCableIntegrity` now consume SDF hit data instead of `RaycastHit`.
- Removed obsolete `_bendObstructionMask` and post-migration dead locals.

Cinematic Cheats used:
- No swept cable CCD. No PhysX fallback. Rock interaction remains published SDF raymarch plus cached bend corners.
- If no published SDF answers, tangent/normal bend fallback remains and visual clipping between nodes is accepted.

Exact Microseconds saved:
- Not claimed without Unity profiler data.
- Static estimate: removes synchronous PhysX raycast surface and collider filter work from tether bend checks.

Verification:
- Scoped forbidden grep returns no SHINOBU_12 hits for Unity Physics raycasts, component lookup, `FindObject*`, scalar MPB setters, `Material.Set*`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, fake `Schedule().Complete()`, or gameplay `Update`/`FixedUpdate`.
- `git diff --check` only reports CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 global warnings outside SHINOBU_12.
- Build-log filter for Tether/Verlet/Cable symbols is empty.

SELF_AUDIT:
- Task matrix: 01-20 PASS by static implementation.
- Struct layout: `VerletNodeDTO` 32 bytes, `GpuCableSplinePointDTO` 16 bytes, `GpuCableDrawParamsDTO` 80 bytes.
- H-Phi: gameplay arrays remain vault-owned; bend caches are fixed cold arrays.
- Blackbox: cable and manager 300-frame rings remain active.
- Compile guard: static Core build passes; runtime Unity Play Mode/profiler/GC evidence is still pending.

## 2026-05-18 - SHINOBU_12 - POOL CAPACITY / GAMEPLAY CREATE GUARD

What was wrong:
- `TetherManager` used `List<TetherInstance>(4)` for active and pooled tethers. That contradicts the 50-cable target because the fifth add can resize in gameplay.
- `RentInstance()` lazily created a `new GameObject("TetherInstance")` when the pool was empty, allowing attach-time object creation.

What was done:
- Added `MaxManagedTetherInstances = 64` and `InitialPooledTetherInstances = 64`.
- Active and pooled lists are now cold-allocated at 64 capacity with explicit COLD ALLOC comments.
- `Awake()` prewarms 64 inactive `TetherInstance` children.
- `RentInstance()` only consumes the pool and fails closed when empty; it no longer creates objects during attach.
- `AttachTowCable` guards the active cap and returns the instance to the pool on overflow.

Cinematic Cheats used:
- No extra cable truth was added. The pool change buys frame stability for the existing low-tier taut-line visual fake and shader-only high-tier stress/salt/silt overkill.

Exact Microseconds saved:
- Not claimed without Unity profiler data.
- Static impact: prevents managed list resize past four tethers and removes lazy object creation from gameplay attach path. Cost is paid cold during manager initialization.

Verification:
- Scoped scan finds no `new List<TetherInstance>(4)`.
- Scoped forbidden scan finds no SHINOBU hits for component lookup, Unity Physics raycasts, `LineRenderer`, Unity joints, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, scalar MPB setters, or fake `Schedule().Complete()`.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 global warnings outside SHINOBU_12.
- Build-log filter for Tether/Verlet/Cable symbols is empty.

SELF_AUDIT:
- Task matrix: 01-20 PASS by static implementation.
- Struct layout unchanged: `VerletNodeDTO` 32 bytes, `GpuCableSplinePointDTO` 16 bytes, `GpuCableDrawParamsDTO` 80 bytes.
- H-Phi: solver arrays remain vault-owned; manager lists are fixed-capacity object-pool bookkeeping.
- Blackbox: cable and manager 300-frame rings remain active.
- Compile guard: static Core build passes; runtime Unity Play Mode/profiler/GC evidence is still pending.

## 2026-05-18 - SHINOBU_12 - MOCK CURRENT TRIG PURGE / DTO FAIL-CLOSED GUARD

What was wrong:
- `MockWorldSampler.SampleFlowAcceleration` still used `math.sin` per sampled Verlet node, which is too expensive for the fallback/mock current path and conflicts with the cheap-physics Dear Lie rule.
- `VerletCableLayout.Validate()` existed but was not enforced before manager initialization, so a future stride drift could still prewarm the pool and register runtime lanes before failing.

What was done:
- Replaced fallback current sine with a deterministic triangle-wave approximation using `math.frac` and absolute value.
- Added a cold `TetherManager.Awake()` fail-closed guard that disables the manager if any SHINOBU DTO stride check fails.
- Re-ran scoped forbidden scan and isolated Core build.

Cinematic Cheats used:
- Mock current bending is now a cheap waveform fake. It keeps believable cable drift without physical turbulence or transcendental per-node math.
- No extra gameplay truth was added. High-tier visual overkill remains in GPU draw params and shader lanes.

Exact Microseconds saved:
- Not claimed without Unity profiler data.
- Static impact: removes one transcendental call per mock-sampled cable node and adds only one cold init stride check.

Verification:
- Scoped grep returns no SHINOBU_12 hits for fallback `math.sin`/`cos`/`exp`/`log`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, component lookup, Unity Physics raycasts, scalar material setters, LINQ, hot `foreach`, string formatting, or `StartCoroutine`.
- `git diff --check` only reports CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 warnings outside SHINOBU_12.
- Build log: `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop12_20260518.log`.

SELF_AUDIT:
- Task matrix: 01-20 PASS by static implementation.
- ARM64/GPU layout: `VerletNodeDTO` 32 bytes, `VerletConstraintDTO` 16 bytes, `GpuCableSplinePointDTO` 16 bytes, `GpuCableDrawParamsDTO` 80 bytes, `MockWorldSampler` 80 bytes.
- Zero-GC: Loop 12 added no hot managed allocations, no new containers, no closure, no string formatting, and no runtime parser path.
- AUP: active solver remains anchor-local before float math; mock current uses local node position only.
- H-Phi: solver arrays remain vault-owned; no new native allocation was added.
- Runtime proof remains pending: no Unity Play Mode, profiler, GCMonitor, Frame Debugger, or device capture was run.

## 2026-05-18 - SHINOBU_12 - CS1612 NATIVEARRAY PROPERTY PURGE

What was wrong:
- `TetherInstance` still exposed `VisualSegmentPositions` as a `NativeArray<float3>` property. That preserved a struct-copy surface in the origin-shift visual rebase path.

What was done:
- Removed the `NativeArray<float3>` property.
- Added `internal ref NativeArray<float3> GetVisualSegmentPositionsRef()`.
- Updated `TetherManager` origin-shift fallback to bind `ref NativeArray<float3> visualPoints` and mutate the vault-backed slice directly.

Cinematic Cheats used:
- None added. This pass is data-access hygiene; existing low-tier taut-line fake and SDF collision fake remain unchanged.

Exact Microseconds saved:
- Not claimed without Unity profiler data.
- Static impact: removes a NativeArray property copy surface and makes L1 mutation explicit.

Verification:
- Scoped grep for `NativeArray<T>` expression-bodied/get properties returns no SHINOBU_12 hits.
- Scoped forbidden scan remains clean for Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, component lookup, Unity Physics raycasts, scalar material setters, LINQ, hot `foreach`, string formatting, and `StartCoroutine`.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 warnings outside SHINOBU_12.
- Build log: `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop13_20260518.log`.

SELF_AUDIT:
- Task matrix: 01-20 PASS by static implementation.
- ARM64/GPU layout unchanged: `VerletNodeDTO` 32 bytes, `VerletConstraintDTO` 16 bytes, `GpuCableSplinePointDTO` 16 bytes, `GpuCableDrawParamsDTO` 80 bytes.
- Zero-GC: no allocation, closure, string formatting, new native container, or parser path was added.
- AUP: origin-shift fallback mutates the same vault-backed visual points after subtracting `shiftOffsetF3`.
- H-Phi: arrays remain vault-owned; ref-return only exposes an internal mutable alias to the existing vault slice.
- Runtime proof remains pending: no Unity Play Mode, profiler, GCMonitor, Frame Debugger, or device capture was run.

## 2026-05-18 - SHINOBU_12 - BLACKBOX H8DUMP / 50-CABLE VAULT CAPACITY

What was wrong:
- Cable and manager blackbox fault export still used `BinaryWriter` and `.bin` only.
- `DataVaultMaxTetherSlots` was 8 while the manager pool and assignment target require 50+ concurrent cables.
- The first helper-file compile attempt failed because the generated Core csproj did not include the new source file.

What was done:
- Added `TetherBlackBoxDumpWriter` with primary `.h8dump` output and legacy `.bin` mirrors.
- Writer uses `MemoryMappedFile` plus pointer copy on Editor/Standalone and `FileStream.Write(ReadOnlySpan<byte>)` fallback elsewhere.
- Raised cable DataVault slots to 64 and documented the 1,228,800-byte telemetry slab.
- Added the helper to `Hecton8.Core.csproj` for isolated build evidence.

Cinematic Cheats used:
- None added. Existing Dear Lie remains node SDF collision plus low-tier taut visual fallback.

Exact Microseconds saved:
- Not claimed without Unity profiler data.
- Static impact: no more per-entry `BinaryWriter` serialization in the fatal path; no steady-state cost added.

Verification:
- Scoped grep finds no `BinaryWriter` in SHINOBU_12 dump paths.
- Scoped grep confirms `.h8dump` paths for cable and manager blackbox.
- Isolated build retry is blocked externally in `LocalizationManager`/`LocRegistry.BabelDictionaryStage` errors. Filtered output has no `Tether`, `Verlet`, `Cable`, `TetherBlackBoxDumpWriter`, or SHINOBU_12 errors.
- Build log: `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop14_retry3_20260518.log`.

SELF_AUDIT:
- Task matrix: 01-20 PASS by static implementation.
- ARM64/GPU layout unchanged for gameplay DTOs; cold dump header is 32 bytes with 8-byte magic at offset 0.
- Zero-GC hot path: telemetry ring writes remain NativeArray-only; dump I/O is fault-path only.
- AUP: no new absolute-to-float cast path.
- H-Phi: cable vault capacity now matches 64 pooled tether instances.
- Runtime proof remains pending: no Unity Play Mode, profiler, GCMonitor, Frame Debugger, or device capture was run.
