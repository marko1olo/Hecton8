# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# KINEMATIC_COLLISION_LEDGER_X_005

Date: 2026-05-23
Agent: X_005
Phase: 0 static archaeology
Status: PENDING VERIFICATION - no Unity playmode, profiler, or compile proof in this phase.
Scope: `Assets/_Project/Scripts`, targeted runtime player/KCC/vehicle/VR kinematic files, plus non-Editor physics query scan.

## Mandate Frame

- Active domain: Echelon 4 Player/Kinematics/Physics KCC.
- Source authority: current source reality, not earlier reports.
- Hot route rule: GlobalRegistry is cold identity only; hot movement publication must use SignalBus/DataVault lanes.
- Quality rule: no binary low/ultra switch. Fidelity must scale through continuous `GlobalQualityWeight`.
- Proof rule: no performance claim without profiler/runtime evidence. All us estimates below are planning estimates, not measured savings.

## Task 01 - Kinematic Collision Inquisition

Static result: no direct `Physics.Raycast`, `Physics.SphereCast`, `Physics.CapsuleCast`, `Physics.BoxCast`, or `Physics.Linecast` calls were found inside the active player/KCC movement files scanned. The active problem is more specific: legacy Rigidbody authority, Unity collision callbacks, and async `RaycastCommand`/`CapsulecastCommand` bridges still sit on movement-critical paths.

| Route | Evidence | Classification | Execution phase | Risk | Phase 1 action |
|---|---|---:|---|---|---|
| Legacy player body authority | `Assets/_Project/Scripts/HectonPlayerMovement.cs:2657`, `2667`, `4308`, `6633`, `6670`, `7519`, `8737`, `8740`, `8743` | Rigidbody + callback | Fixed/update/collision callback | Player state, camera juice, audio, stamina, transport, AUP sync are coupled to `_rb` and `OnCollisionEnter`. | Do not delete. Insert KCC-owned state bridge first, then retire callback consumers by replacing data source. |
| Player motor sweep bridge | `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:626`, `645`, `1027`, `1100`, `1952`, `2133`, `2440` | Async PhysX command bridge + Rigidbody writes | Fixed/PostFixed/Late | No direct sync cast, but collision authority still depends on PhysX command results and `Rigidbody.MovePosition/linearVelocity`. | Replace locomotion sweep with SDF/speculative plane set; keep async commands only as explicit fallback until validated. |
| Player kinematics bridge | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:80`, `1114`, `1202`, `1251`, `2036`, `2535`, `3075`, `3118`, `3670`, `3676` | Burst SDF assist + hand raycast bridge + Rigidbody output | Fixed/Late | SDF squeeze exists, but movement output still writes Motor/Rigidbody. Hand placement uses `RaycastCommand.ScheduleBatch`. | Reuse SDF sampling and `KccVelocitySignal`; isolate Rigidbody writes behind one presentation bridge. |
| Hydrodynamic KCC runtime | `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:22`, `177`, `1326`, `1354`, `1365`, `1547`, `2598`, `2740`, `2797`, `2820`, `2930` | Native/Burst KCC with async PhysX capsule bridge | Fixed/PostFixed/Late | This is not pure SDF collision yet. It extracts `RaycastHit` into native DTOs and resolves in Burst. | Use it as migration nucleus; replace `CapsulecastCommand` stage with SDF speculative hits. |
| Vehicle motor | `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:708`, `720`, `780`, `956`, `966`, `1037` | Async PhysX capsule bridge + Rigidbody | Fixed/PostFixed | Vehicle movement is same failure class as player motor, but separate gameplay domain. | Defer code edits until player KCC bridge is stable; ledger keeps vehicle as follow-up target. |
| VR/body interaction kinematics | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:1965`, `3332`, `3352`; `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:281`, `2404` | Async PhysX command bridge | Late/interaction | Not primary locomotion truth, but affects collision perception and hand/head placement. | Keep as secondary bridge; only migrate after locomotion state route is stable. |

Non-Editor direct sync physics queries outside primary movement were found in runtime systems including `BuoyancyObject`, `HectonPlayerSpawner`, `InteractionUI`, `PhysicalInteractionHandler`, `PhysicalHandController`, `BioReactor`, `Floater`, `EnvironmentalHazard`, `RandomEventSystem`, `SubmarineCompoundColliderAuthoring`, and `SubmarineFluidDynamics`. These are not all X_005-owned movement authority. They are evidence for a broader physics query cleanup, not permission to edit outside domain.

Immediate compile-wall impact: removing Rigidbody state, `OnCollisionEnter`, or async command buffers in one pass would break player movement, camera feedback, sound signals, hand IK, vehicle motion, and transport handoff. Correct route is staged: publish KCC state first, consume it through existing signal/DataVault lanes, then shrink PhysX bridges.

Planning us estimate: 0 us saved by Phase 0 itself. Estimated retirement opportunity after implementation is 120-380 us/frame on i3/MX350 class hardware, pending profiler proof.

## Task 02 - SDF Interface Reconciliation

Two SDF paths exist and they are not the same owner route.

### World byte SDF

- Buffer: `BufferID.VoxelSdfTexture3D = 14`.
- Descriptor: `BufferID.VoxelSdfPayloadDescriptor = 620`.
- Descriptor DTO: `Assets/_Project/Scripts/Core/Contracts/GroundRadarContracts.cs:27`, `VoxelSdfPayloadDescriptorDTO`.
- Descriptor fields: `VolumeOrigin`, `GridDimensions`, `VoxelCellSize`, `SdfRangeMeters`, `ByteCount`, `BufferId`, `BufferGeneration`, `SdfVersion`, `OwnerSystemId`, `Flags`.
- Read validator: `Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs:714` verifies descriptor, buffer id, generation, byte count, and `FlagValid`.
- Player kinematics consumer: `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1910` reads `HectonVoxelVolume.TryGetPublishedSonarSdfPayload`; `1954` optionally reads `VoxelSdfTexture3D` from `SystemID.WorldStreaming`.
- Sampling: `TrySampleSdfTrilinear` decodes `byte` density with `sdfRange`; gradient mode scales between tetra-4 and axis-6 using continuous quality/cadence (`4207`).

### Hydrodynamic KCC float environment SDF

- Buffer: `BufferID.ShinobuKccEnvironmentSdf = 71763`.
- Grid: `KccEnvironmentGridDTO`, explicit 64 bytes at `HydrodynamicKccRuntime.cs:177`.
- Default dimensions: 16 x 8 x 16 = 2048 cells (`2357-2360`).
- Cell size: default 2m (`3614-3627`).
- Current status: `UpdateEnvironmentGridSnapshot` always ORs `FlagEnvironmentMock` (`3653`), so current KCC environment is explicitly mock-marked.
- Current use: `ApplyEnvironmentalForcesJob` samples SDF for friction/slope/environment state; KCC collision still uses `CapsulecastCommand`.

Gap: no verified real producer adapts the world byte SDF route into `ShinobuKccEnvironmentSdf` as a float collision field for `HydrodynamicKccRuntime`. This is the first implementation bottleneck. A byte-to-float adapter must be owner-routed through DataVault, with explicit generation/descriptor validation, before claiming SDF collision sovereignty.

Planning us estimate: 0 us saved now. Expected adapter cost target is below 35 us/frame on low tier by cadence/generation gating, pending profiler proof.

## Task 03 - Registry And Signal Mapping

Input ownership:

- `InputDispatcher` writes current `InputStateDTO` to DataVault and publishes `InputStateSignal` at `Assets/_Project/Scripts/Core/InputDispatcher.cs:642-650`.
- Discrete commands publish `PlayerInputSignal` at `InputDispatcher.cs:3304`.
- Automation/replay overrides are routed through `CoreDeterminismSignals.TryConsumeLatestInputOverride` at `InputDispatcher.cs:2622`.
- `HydrodynamicKccRuntime` has `HydrodynamicKccInputDTO` and a `NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter`, plus `TryRegisterExternalInputWriter`, but no runtime source wiring was found outside the KCC file itself.

Output ownership:

- Existing deterministic velocity lane: `KccVelocitySignal`, explicit 128 bytes, in `Assets/_Project/Scripts/Core/GlobalSignals.cs:656`.
- Bus config: `SignalBus<KccVelocitySignal>` uses lane hash `0x50484B56` at `GlobalSignals.cs:8125`.
- Facade: `PhysicsDeterminismSignals.PublishKccVelocity` forwards to `CoreDeterminismSignals.PublishKccVelocity`.
- Current producer found: `PlayerKinematicsRuntime.PublishKccVelocitySignal` at `PlayerKinematicsRuntime.cs:2535`, called from fixed/pre-shift paths.

Route decision: no new hot global registry route is justified. Phase 1 should reuse `InputStateDTO/InputStateSignal` as input source, `ShinobuHydroKccInputs` as KCC input lane, and `KccVelocitySignal` as output publication. GlobalRegistry should remain cold dependency discovery only.

Planning us estimate: 0 us saved now. Avoided risk: one extra hot GlobalRegistry poll per frame and duplicate movement facts.

## Phase 0 Verdict

The repository is partially migrated already, but not complete:

- SDF squeeze and byte SDF sampling exist.
- A native hydrodynamic KCC runtime exists.
- A deterministic KCC velocity signal exists.
- A 300-frame telemetry ring exists in the hydrodynamic runtime.
- The active movement authority is still split across Rigidbody, callbacks, async PhysX command bridges, and native KCC prototypes.
- `HydrodynamicKccRuntime` cannot be reported as pure SDF collision while `CapsulecastCommand.ScheduleBatch` remains in the collision stage.

Next safe implementation order:

1. Add/verify an SDF adapter from `VoxelSdfPayloadDescriptorDTO` + `VoxelSdfTexture3D` into `ShinobuKccEnvironmentSdf`.
2. Add SDF speculative collision DTOs in native layout rather than consuming `RaycastHit`.
3. Route external input from `InputDispatcher` into `ShinobuHydroKccInputs`.
4. Publish KCC state through `KccVelocitySignal` and visual output through one one-frame-late presentation bridge.
5. Only then disable or retire Rigidbody/PhysX movement bridges behind a feature flag or migration gate.

## Loop 2 Patch Ledger - 2026-05-23

Applied code changes:

- `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`
  - Removed the Hydro KCC `CapsulecastCommand.ScheduleBatch` collision stage and `RaycastHit` extraction path.
  - Added `BuildSdfCollisionHitsJob`, a Burst `IJobParallelFor` that samples `ShinobuKccEnvironmentSdf`, computes speculative capsule penetration, writes 64-byte `HydrodynamicKccCollisionHitDTO` contacts, and feeds the existing slope/resolution jobs.
  - Added SDF hit flags plus penetration/sample fields without changing the hit DTO size.
  - Published finalized one-frame-late Hydro state through `PhysicsDeterminismSignals.PublishKccVelocity`.

- `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs`
  - Added cached `HydrodynamicKccRuntime` authority detection.
  - When Hydro authority is active, legacy player `CapsulecastCommand`/`RaycastCommand` scheduling returns false and clears pending fallback state.

- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`
  - Added Hydro authority branch that consumes `KccVelocitySignal` instead of running the old Rigidbody-centered movement body path.
  - Suppressed hand probe `RaycastCommand.ScheduleBatch` while Hydro authority is active.

- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
  - Suppressed `OnCollisionEnter` queuing and queued PhysX collision processing while Hydro KCC owns collision authority.

- `Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs`
  - Reads fresh `KccVelocitySignal` before falling back to player movement/Rigidbody velocity.

Current proof:

- Targeted `rg` finds zero `CapsulecastCommand`, `RaycastCommand`, `RaycastHit`, `QueryParameters`, or `ScheduleBatch` references in `HydrodynamicKccRuntime.cs`.
- `Tools/OOP_Kcc_Scanner_X_005.py` generated `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`.
- Scanner result: `hydrodynamic_kcc_runtime_clean = true`.
- Scanner residuals in scoped movement/presentation files: 1 collision callback symbol, 6 PhysX command schedules, 38 PhysX command type references, 6 `linearVelocity` writes.

Residuals not hidden:

- `ShinobuKccEnvironmentSdf` is still the active Hydro collision source. The byte world SDF to Hydro float SDF adapter remains the next correctness bottleneck before claiming full cave-world SDF sovereignty.
- Compile was not run in Loop 2 because latest successful CPU load measured 100%, local project rules forbid dotnet/csc above 50%, and follow-up process/CPU checks timed out under saturation.

## Loop 3 Patch Ledger - 2026-05-23

Applied code changes:

- `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs`
  - Removed player fallback `CapsulecastCommand`/`RaycastCommand` construction and command `ScheduleBatch`.
  - Converted `SetLinearVelocity` into a quarantine shim that does not write Rigidbody velocity.

- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`
  - Removed `RaycastCommand` hand probe storage and command scheduling.
  - Prevented commit fallback from writing Rigidbody velocity when no motor is present.

- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
  - Removed the Unity `OnCollisionEnter` callback entry by renaming it to a non-dispatched legacy handler.
  - Removed direct fallback `_rb.linearVelocity = Vector3.zero`.

- `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs`
  - Removed vehicle `CapsulecastCommand` scheduling and command buffer ownership.
  - Removed direct vehicle Rigidbody velocity writes in the scoped route.

- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs`
  - Removed VR head `CapsulecastCommand` buffer, builder job, and command `ScheduleBatch`.

- `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs`
  - Removed contextual IK `RaycastCommand` buffers/build job and command `ScheduleBatch`.
  - Replaced the command pass with `ContextualPhysicalIkClearHitsJob`, leaving response jobs deterministic and PhysX-free.

Current proof:

- `Tools/OOP_Kcc_Scanner_X_005.py` now reports `finding_counts = {}` for the scoped X_005 files.
- Scoped `rg` finds no `RaycastCommand`, `CapsulecastCommand`, `QueryParameters`, command `ScheduleBatch`, Unity `OnCollisionEnter(`, or direct `.linearVelocity =` in the X_005 file set.

Residuals not hidden:

- Feature parity for VR/contextual IK collision richness is reduced until a proper SDF-backed presentation contact route replaces the removed PhysX probes.
- Feature parity for vehicle collision response now requires a vehicle SDF sweep DTO/solver rather than a PhysX command fallback.
- Compile was not run because CPU measured 100% and active `csc`/`dotnet` processes were present.

## Loop 4 Patch Ledger - 2026-05-23

Applied code changes:

- `Assets/_Project/Scripts/Interaction/PhysicalHandController.cs`
  - Removed finger `SpherecastCommand` buffers, build job, hit buffer, and command `ScheduleBatch`.
  - Added `BuildFingerSpeculativePoseJob`, which writes deterministic finger curl/pose data without PhysX.
  - Replaced direct grabbed-body `linearVelocity =` reset/clamp with queued velocity-change deltas through `PhysicsForceRouter`.

- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`
  - Removed tool `RaycastCommand` staging/scheduled buffers and command `ScheduleBatch`.
  - Added explicit 64-byte `InteractionRaycastRequestDTO` for request bookkeeping.
  - Kept completed hit results deterministic no-contact until an SDF/tool-surface query executor exists.

- `Assets/_Project/Scripts/HectonPlayerSpawner.cs`
  - Removed sync `Physics.RaycastNonAlloc` ground probe.
  - Spawn terrain height now comes from `HectonMapMagicVegetationBridge.TryGetCachedTerrainHeight`.
  - Replaced direct `playerRigidbody.linearVelocity =` with `HectonPlayerMotor.SetLinearVelocity` quarantine call.

- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
  - Save velocity now prefers latest `KccVelocitySignal`.
  - Load velocity no longer writes player Rigidbody directly.

- `Assets/_Project/Scripts/SaveManager.cs`
  - Loaded player velocity no longer writes player Rigidbody directly.

- `Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs`
  - Rider velocity sync/exit now routes through `HectonPlayerMotor.SetLinearVelocity`.
  - Transport bailout drift/damping uses `PhysicsForceRouter.QueueForce` deltas instead of direct `linearVelocity =`.

- `Tools/OOP_Kcc_Scanner_X_005.py`
  - Expanded scanner scope to player/vehicle/VR/IK/interaction/spawn/save/transport files.
  - Added `SpherecastCommand`, sync PhysX casts, and broad direct `.linearVelocity =` detection.

Current proof:

- `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`: `finding_counts = {}`.
- Hydro KCC forbidden command hits: 0.
- Scoped `rg` across expanded X_005 files found no `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, command `ScheduleBatch`, sync `Physics.Raycast/SphereCast/CapsuleCast`, Unity collision callback entry, or direct `.linearVelocity =`.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors.

Residuals not hidden:

- `HectonMapMagicVegetationBridge` height cache must be present for spawner terrain validation; no PhysX fallback remains.
- Finger/tool contact feature parity is intentionally reduced until SDF-backed hand/tool contact DTOs are implemented.
- Whole-repo scan still finds PhysX/Rigidbody residuals outside X_005 domain in Core/Fauna/World/Construction/Tools. X_005 does not claim those domains clean.

## Loop 5 Patch Ledger - 2026-05-23

Applied code changes:

- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs`
  - Added cached `IVoxelSonarSdfReadModel` binding and hot-swap handling.
  - Replaced no-contact VR near-field path with six fixed SDF raymarch probes.
  - Writes existing 48-byte `HeadCastSample` DTOs; no PhysX command buffers or schedules are reintroduced.
  - SDF step size scales continuously through `GlobalQualityWeight`.

- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`
  - Added cached `IVoxelSonarSdfReadModel` and `ITerrainProvider` dependencies through registry contracts.
  - Completed queued primary hit requests through SDF raymarch for voxel/voxel-proxy masks.
  - Completed downward terrain probes through cached terrain height/normal for terrain masks.
  - Kept the 64-byte `InteractionRaycastRequestDTO`; no `RaycastCommand` executor exists.

- `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs`
  - Added cached `IVoxelSonarSdfReadModel` and `ITerrainProvider` dependencies through registry contracts.
  - Filled the existing IK hit buffer from SDF/terrain probes before scheduling the existing Burst response job.
  - Restored foot, hand-brace, and tool-retraction contact input without `RaycastCommand.ScheduleBatch`.

- `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`
  - Added `Dump_X_005.bin` and `Dump_SHINOBU_322_KCC.bin` as Hydro telemetry dump write targets.

Current proof:

- `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`: `finding_counts = {}` after Loop 5.
- Hydro KCC forbidden command hits: 0.
- Scoped `rg` on touched Hydro/VR/tool files found no `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, command `ScheduleBatch`, sync `Physics.Raycast/SphereCast/CapsuleCast`, Unity collision callback entry, or direct `.linearVelocity =`.
- `git diff --check` passed for touched runtime files; only CRLF normalization warnings.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors.
- After contextual IK restoration, `Tools/OOP_Kcc_Scanner_X_005.py` still reported `finding_counts = {}`.
- Final `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors.

Residuals not hidden:

- `EquipmentInteractionHandler` SDF/terrain completion is not a Burst KCC solver; it is a non-PhysX, deferred interaction bridge for tool/placement feature parity.
- `ContextualPhysicalIkRuntime` SDF hit filling is a bounded main-thread owner-interface bridge feeding the existing Burst response job; it is not a new Burst collision job.
- Whole-repo PhysX/Rigidbody residuals outside Echelon 4 remain outside X_005 ownership.

## APEX Re-Audit Ledger - 2026-05-23

Applied code changes:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
  - Removed save-load ground-ready `Physics.RaycastNonAlloc`.
  - Removed `_groundCheckHits`.
  - Added cached terrain readiness through `ITerrainProvider.TryGetHeight`.
  - Added voxel readiness through `IVoxelSonarSdfReadModel.TryRaymarchNearestSonarSdf`.

- `Assets/_Project/Scripts/BuoyancyObject.cs`
  - Removed player-adjacent buoyancy `Physics.RaycastNonAlloc` ground probe.
  - Removed `_groundHitBuffer`.
  - Added terrain/SDF provider ground probes and registry hot-swap cache.
  - Unsupported collider-only layers now resolve no-ground instead of doing a scene query.

- `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`
  - `EvaluateSlopeFrictionJob` now clamps local hit stride to 1..8.
  - `KinematicResolutionJob` now clamps local hit stride to 1..8.
  - Together with `BuildSdfCollisionHitsJob`, all Hydro collision loops have local finite bounds.

- `Tools/OOP_Kcc_Scanner_X_005.py`
  - Added `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` and `Assets/_Project/Scripts/BuoyancyObject.cs` to the X_005 scanned player-critical scope.

- `Tools/KccApexAudit_X_005.py`
  - New proof script for hidden PhysX count, broad residual list, solver boundedness, and DTO byte layout.

Current proof:

- `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`: `finding_counts = {}` after adding bootstrap to scope.
- `Docs/Reports/KCC_APEX_AUDIT_X_005.json`: scoped forbidden count 0; broad non-Editor runtime forbidden count 122 outside X_005.
- Hydro solver bound: `ResolveIterationCount` max 8; three local stride clamps 1..8; max 24 SDF capsule probe samples and 8 resolution plane projections per entity.
- 100 m/s fall at `KccSmokeFixedDeltaTime = 0.016666667` covers 1.666667 m/frame; the solver samples along the displacement, clamps penetration response, quantizes final AUP to millimeters, and has no recursive call path.
- `LockstepPlayerKinematicState` is 96 bytes, covered 0..96, no gaps. It is not 64 bytes.
- `KinematicStateDTO` is 64 bytes, covered 0..64, no gaps, and remains the Hydro KCC hot state.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors after APEX runtime patches.

Residuals not hidden:

- Whole-repo PhysX command/sync/callback residuals remain outside X_005 in construction, AI sight, UI focus, scanner/tooling, and world systems. They are not claimed clean.
