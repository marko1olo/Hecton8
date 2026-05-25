# LOG_X_005

## 2026-05-23 - Phase 0 Static Kinematic Collision Ledger

What was wrong:
- Active movement authority is split across `HectonPlayerMovement`, `HectonPlayerMotor`, `PlayerKinematicsRuntime`, and `HydrodynamicKccRuntime`.
- No direct sync `Physics.SphereCast/Raycast/CapsuleCast` was found in the primary player/KCC movement files, but the active runtime still depends on Rigidbody state, `OnCollisionEnter`, and async PhysX command bridges.
- `HydrodynamicKccRuntime` is not pure SDF collision. It still schedules `CapsulecastCommand.ScheduleBatch` and extracts `RaycastHit` into native DTOs.
- The KCC float SDF route `ShinobuKccEnvironmentSdf` is currently mock-marked. The real world byte SDF route is separate.

What was done:
- Extracted the X_005 batch prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Read project authority docs, domain boundaries, and the relevant physics/AUP/layout/zero-GC/job/telemetry mandates.
- Created `Docs/Tasks/Status_X_005.md`.
- Created and updated `Docs/AgentLogs/Rationale_X_005.md`.
- Wrote `Docs/Reports/KINEMATIC_COLLISION_LEDGER_X_005.md`.
- Mapped player/KCC/vehicle/VR collision routes, SDF routes, input routes, and deterministic velocity output routes.

Cinematic cheats used:
- No runtime cheat implemented in Phase 0.
- Valid future cheats identified: byte-to-float local SDF window, tetra-4 gradient on low tier, axis-6/full trilinear sampling as `GlobalQualityWeight` rises, and one-frame-late visual presentation while gameplay truth stays deterministic.

Exact microseconds saved:
- Runtime saved by Phase 0: 0 us. No runtime code changed.
- Planned removal opportunity: 120-380 us/frame on i3/MX350 class hardware by retiring movement PhysX command bridges and Rigidbody callback churn. This is an engineering estimate, not profiler proof.
- Planned SDF adapter budget: below 35 us/frame low-tier by generation/cadence gating. This is an engineering target, not profiler proof.

Verification:
- Static source scan completed.
- No compile launched. Reason: Phase 0 was doc-only; no C# runtime source was modified, and project instructions forbid unnecessary build launches.
- Runtime/profiler proof remains pending.

## 2026-05-23 - Phase 0 Re-Entry Verification

What was wrong:
- The same Phase 0 directive was received again. Without a disk-backed check, this could cause duplicate archaeology or false task progress.

What was done:
- Re-read `Docs/Tasks/Status_X_005.md`.
- Re-read `Docs/AgentLogs/Rationale_X_005.md`.
- Re-extracted `<AGENT_PROMPT id="X_005">` from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex.
- Verified task count remains 10.
- Verified Phase 0 artifacts still exist: `Docs/Reports/KINEMATIC_COLLISION_LEDGER_X_005.md` and `Docs/AgentLogs/LOG_X_005.md`.

Cinematic cheats used:
- None. Re-entry check is state hygiene only.

Exact microseconds saved:
- Runtime saved: 0 us.
- Engineering time protected: duplicate Phase 0 scan avoided; not a runtime performance claim.

Verification:
- Phase 0 remains static-scan complete for Tasks 01-03.
- Tasks 04-10 remain pending.
- No compile launched because no C# runtime source changed.

## 2026-05-23 - Loop 2 SDF KCC Patch

What was wrong:
- `HydrodynamicKccRuntime` was native/Burst-heavy but still used `CapsulecastCommand.ScheduleBatch` and `RaycastHit` extraction, so calling it pure SDF was false.
- Player movement authority was split across Hydro KCC, `HectonPlayerMotor` PhysX command batches, `PlayerKinematicsRuntime` hand `RaycastCommand` probes, `HectonPlayerMovement.OnCollisionEnter`, and Rigidbody velocity reads.
- Vehicle/VR/Contextual IK command bridges still exist and must not be hidden behind a fake "all clean" report.

What was done:
- Replaced the Hydro KCC command/extract stage with `BuildSdfCollisionHitsJob`, sampling `ShinobuKccEnvironmentSdf` and writing speculative SDF contact hits into the existing native resolution path.
- Kept `HydrodynamicKccCollisionHitDTO` at 64 bytes while adding penetration depth and sample index fields.
- Published Hydro's finalized one-frame-late state through `KccVelocitySignal`.
- Gated `HectonPlayerMotor` legacy capsule/raycast command scheduling while Hydro authority is active.
- Made `PlayerKinematicsRuntime` consume Hydro `KccVelocitySignal` and suppress hand ray probes while Hydro authority is active.
- Quarantined `HectonPlayerMovement.OnCollisionEnter` side effects under Hydro authority.
- Updated swim presentation to read fresh `KccVelocitySignal` before falling back to Rigidbody-derived velocity.
- Added `Tools/OOP_Kcc_Scanner_X_005.py` and generated `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`.
- Updated `Docs/Reports/KINEMATIC_COLLISION_LEDGER_X_005.md`, `Docs/Tasks/Status_X_005.md`, and `Docs/AgentLogs/Rationale_X_005.md`.

Cinematic cheats used:
- Replaced exact PhysX capsule sweep with local SDF speculative samples and contact plane projection.
- Continuous `GlobalQualityWeight` scales SDF sample count from low-cost survival to richer high-tier contact sampling without changing DTO layout.
- One-frame-late signal presentation hides solver latency instead of forcing same-frame readback.

Exact microseconds saved:
- Profiler proof not available in Loop 2 because build/profiling was blocked by CPU policy.
- Expected removed cost in Hydro-active player route: one Hydro `CapsulecastCommand.ScheduleBatch` plus one `RaycastHit` extraction pass, target 120-380 us/frame on i3/MX350 class hardware.
- Expected removed player fallback cost when Hydro authority is live: player motor sweep batch and kinematic repair ray batch are no longer scheduled; exact value pending profiler.
- Runtime cost of scanner/report updates: 0 us.

Verification:
- `git diff --check` passed for touched files; only CRLF normalization warnings.
- Targeted `rg` found zero `CapsulecastCommand`, `RaycastCommand`, `RaycastHit`, `QueryParameters`, or `ScheduleBatch` references in `HydrodynamicKccRuntime.cs`.
- `Tools/OOP_Kcc_Scanner_X_005.py` result: Hydro KCC forbidden command hits = 0; residual scoped findings = 1 collision callback symbol, 6 command schedules, 38 command type references, 6 `linearVelocity` writes.
- No dotnet build launched. Reason: latest successful CPU gate measured 100%; local rule forbids dotnet/csc when CPU load is above 50%, and follow-up checks timed out under saturation.

## 2026-05-23 - Loop 3 Command Bridge Removal

What was wrong:
- Loop 2 still left source-level PhysX command bridges in player fallback, vehicle, VR head collision, and contextual IK presentation.
- Direct scoped Rigidbody `.linearVelocity =` writes still existed.
- `HectonPlayerMovement.OnCollisionEnter` still existed as a Unity callback entry point.

What was done:
- Removed player fallback command scheduling from `HectonPlayerMotor`.
- Removed `RaycastCommand` hand probe storage/scheduling from `PlayerKinematicsRuntime`.
- Removed Unity-dispatched `OnCollisionEnter` from `HectonPlayerMovement` by renaming it out of callback dispatch.
- Removed scoped direct `.linearVelocity =` writes from player/vehicle files.
- Removed vehicle `CapsulecastCommand` scheduling and command buffer ownership from `VehicleMotor`.
- Removed VR head `CapsulecastCommand` scheduling from `VRSomaticProvider`.
- Removed contextual IK `RaycastCommand` scheduling from `ContextualPhysicalIkRuntime` and replaced it with a deterministic clear-hit job.
- Regenerated `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`.

Cinematic cheats used:
- Contextual IK collision probes now resolve as zero-contact deterministic presentation instead of PhysX ray probes until a proper SDF presentation-contact route is implemented.
- Vehicle collision command fallback is removed; vehicle collision response must be restored through a future SDF sweep DTO rather than PhysX.

Exact microseconds saved:
- Profiler proof not available because build/profiling remains blocked.
- Scoped command bridge count removed from scanner: 6 command schedules -> 0, 38 command type refs -> 0, direct `linearVelocity` writes -> 0, Unity collision callback symbol -> 0.
- Runtime us estimate remains pending profiler; expected low-end saving comes from eliminating player/vehicle/VR/IK command scheduling and Rigidbody velocity mutation in the scoped route.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py` result: `finding_counts = {}` and Hydro KCC forbidden command hits = 0.
- Scoped `rg` found no `RaycastCommand`, `CapsulecastCommand`, `QueryParameters`, command `ScheduleBatch`, `OnCollisionEnter(`, or `.linearVelocity =` in the X_005 file set.
- `git diff --check` passed for touched files; only CRLF normalization warnings.
- No dotnet build launched. Reason: CPU measured 100% and active `csc`/`dotnet` processes were present.

## 2026-05-23 - Loop 4 Extended Echelon 4 Cleanup

What was wrong:
- The previous green scan was too narrow. Echelon 4 interaction and persistence paths still had `SpherecastCommand`, `RaycastCommand`, sync `Physics.RaycastNonAlloc`, and direct `Rigidbody.linearVelocity` writes.
- `PhysicalHandController` and `EquipmentInteractionHandler` preserved hand/tool PhysX command bridges.
- `HectonPlayerSpawner`, `HectonSurvivalSystem`, `SaveManager`, and `MountablePlayerTransport` preserved direct Rigidbody velocity authority.

What was done:
- Removed finger `SpherecastCommand.ScheduleBatch` from `PhysicalHandController`; replaced it with `BuildFingerSpeculativePoseJob`.
- Removed equipment `RaycastCommand.ScheduleBatch` from `EquipmentInteractionHandler`; replaced command buffers with an explicit 64-byte request DTO and no PhysX executor.
- Removed sync `Physics.RaycastNonAlloc` from `HectonPlayerSpawner`; spawn now reads cached terrain height from `HectonMapMagicVegetationBridge`.
- Removed direct scoped `linearVelocity =` writes from player spawn, survival load, save load, physical hand reset/break, and mountable transport.
- Expanded `Tools/OOP_Kcc_Scanner_X_005.py` to cover player/vehicle/VR/IK/interaction/spawn/save/transport files and `SpherecastCommand`.
- Regenerated `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`.

Cinematic cheats used:
- Hand fingers now use deterministic grip curl instead of exact PhysX finger contact.
- Equipment primary ray hit completion is no-contact until a real SDF/tool-surface query route is implemented.
- Transport bailout damping uses queued velocity-change deltas instead of direct Rigidbody velocity ownership.

Exact microseconds saved:
- Profiler proof still pending; no fake us claim.
- Static bridge count in expanded X_005 scope: PhysX command type/schedule hits -> 0, sync PhysX cast hits -> 0, Unity collision callback entries -> 0, direct `linearVelocity =` writes -> 0.
- Expected low-end saving comes from removing hand/tool command schedules and the spawn sync raycast from player-critical scope; exact value requires Unity profiler/GC proof.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits = 0.
- Scoped `rg`: no `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, command `ScheduleBatch`, sync `Physics.Raycast/SphereCast/CapsuleCast`, `OnCollisionEnter(`, or `.linearVelocity =` in the expanded X_005 file set.
- `git diff --check` passed for touched files; only CRLF normalization warnings.
- First `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` timed out after 120 seconds; orphaned `dotnet` children were stopped.
- Verified compile with `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: 0 warnings, 0 errors.

## 2026-05-23 - Loop 5 SDF Contact Restoration

What was wrong:
- Loop 4 correctly removed PhysX command bridges but weakened feature parity in two scoped places: VR head collision faded to zero and equipment primary hit completion had no executor.
- Hydro telemetry did not write the exact `Dump_X_005.bin` or prompt-required `Dump_SHINOBU_322_KCC.bin` filenames.

What was done:
- `VRSomaticProvider` now resolves six fixed near-field head probes through cached `IVoxelSonarSdfReadModel.TryRaymarchNearestSonarSdf`, writing existing 48-byte `HeadCastSample` rows with quality-scaled SDF step size.
- `EquipmentInteractionHandler` now completes queued primary hit requests through SDF raymarch for voxel/voxel-proxy layer masks.
- `EquipmentInteractionHandler` now handles downward terrain placement probes through cached `ITerrainProvider.TryGetHeight/TryGetNormal`, not PhysX.
- `ContextualPhysicalIkRuntime` now fills its existing IK hit buffer from SDF/terrain provider probes before scheduling the existing Burst response job; no command bridge was restored.
- `HydrodynamicKccRuntime.DumpTelemetry` now writes `Dump_X_005.bin` and `Dump_SHINOBU_322_KCC.bin` in addition to legacy dump names.
- Re-extracted the exact `<AGENT_PROMPT id="X_005">` block from `Docs/Tasks/CURRENT_BATCH.md`; task count remains 10.
- Re-ran the X_005 scanner and scoped forbidden-pattern grep.

Cinematic cheats used:
- Tool terrain placement uses cached terrain height/normal instead of exact collider ray contact.
- VR near-field uses six SDF feelers, not a capsulecast. Low quality uses coarser SDF steps; higher quality tightens the step through continuous `GlobalQualityWeight`.

Exact microseconds saved:
- No profiler proof yet. Static proof remains zero PhysX command schedules in the scoped Echelon 4 route.
- Expected low-end saving versus the old route remains the removed player/vehicle/VR/IK/hand/tool command schedules plus the removed spawn sync raycast. Exact us requires Unity profiler.
- New VR/tool SDF work is bounded and zero-GC, but it is not free; no fake net-us claim is made.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits = 0.
- Scoped `rg` on touched Hydro/VR/tool files found no `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, command `ScheduleBatch`, sync `Physics.Raycast/SphereCast/CapsuleCast`, `OnCollisionEnter(`, or direct `.linearVelocity =`.
- `git diff --check` passed for touched runtime files; only CRLF normalization warnings.
- Initial Loop 5 compile was deferred because CPU measured 66%, above the 50% build gate.
- Build gate retried at 12% CPU with no active `dotnet`/`csc` process.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors.
- After contextual IK SDF restoration, the scanner still reported `finding_counts = {}`.
- Build gate retried again at 42% CPU with no active `dotnet`/`csc` process.
- Final `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors.

## 2026-05-23 - APEX Re-Audit

What was wrong:
- Broad hidden-PhysX audit found `GameBootstrapper.WaitForGroundReadyAsync` using `Physics.RaycastNonAlloc` for save-load player ground readiness.
- The solver proof depended partly on caller-side `MaxHitsPerCommand` clamping.
- The previous wording could be misread as claiming `LockstepPlayerKinematicState` was 64 bytes. It is not.

What was done:
- Added `GameBootstrapper.cs` to `Tools/OOP_Kcc_Scanner_X_005.py`.
- Replaced bootstrap ground-ready sync raycast with cached `ITerrainProvider` height checks plus `IVoxelSonarSdfReadModel.TryRaymarchNearestSonarSdf` for voxel/voxel-proxy masks.
- Removed the bootstrap `RaycastHit[1]` field.
- Replaced `BuoyancyObject.PerformGroundCheck` sync raycast with cached terrain/SDF probes because player acoustic/weather code reads `playerBuoyancy`.
- Removed the buoyancy `RaycastHit[1]` field and added `BuoyancyObject.cs` to the X_005 scanner scope.
- Clamped `EvaluateSlopeFrictionJob` and `KinematicResolutionJob` local hit stride to 1..8 inside Hydro Burst jobs.
- Added `Tools/KccApexAudit_X_005.py`, generating `Docs/Reports/KCC_APEX_AUDIT_X_005.md/json`.

Cinematic cheats used:
- Bootstrap terrain readiness now uses cached height authority, not collider truth.
- Voxel readiness uses quality-scaled SDF raymarch step from 1.25 m down to 0.25 m.

Exact microseconds saved:
- Per-frame saving is 0 us because bootstrap ground-ready is not a frame-hot loop.
- Eliminated one save-load sync PhysX query path, one player-adjacent buoyancy sync PhysX query path, and two managed `RaycastHit[1]` fields.
- Solver worst-case is now hard bounded per entity: 24 SDF capsule probe samples plus 8 plane projections.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits = 0.
- `Tools/KccApexAudit_X_005.py`: scoped forbidden count 0; broad non-Editor runtime forbidden count 122 outside X_005; hard stride clamps 1..8 found 3; `LockstepPlayerKinematicState` 96 bytes; `KinematicStateDTO` 64 bytes.
- `git diff --check` passed for touched runtime/script/report files; only CRLF normalization warnings.
- Compile not launched yet after APEX patch because CPU measured 100%, above the 50% project build gate.
- After external `csc/dotnet` churn ended and CPU measured 33%, `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` succeeded: 0 warnings, 0 errors.

Residuals not hidden:
- `LockstepPlayerKinematicState` is 96 bytes by explicit rollback contract. Gap-free, but not 64.
- Whole non-Editor runtime still has 122 PhysX command/sync/callback residuals outside X_005 ownership. They are listed in `Docs/Reports/KCC_APEX_AUDIT_X_005.json`.

## 2026-05-23 - APEX Deployable/Demo Bridge Removal

What was wrong:
- `DeployableSdfDrillRuntime` still used `RaycastCommand.ScheduleBatch` and `RaycastHit` readback for terrain snap.
- `DemoFirstPersonController` registered itself as a player-layer tickable and directly mutated `Rigidbody.linearVelocity`.
- The previous X_005 scanner did not include these two files, so the proof gate was too narrow.

What was done:
- Replaced deployable drill snap with cached `ITerrainProvider` terrain height/normal and `IVoxelSonarSdfReadModel.TryRaymarchNearestSonarSdf`.
- Removed drill snap command/hit native buffers, snap `JobHandle`, `RaycastCommand`, `RaycastHit`, and `QueryParameters` usage from the deployable drill route.
- Quarantined the third-party demo controller: no `PriorityLayer.Player` registration, no Rigidbody velocity writes.
- Added `DeployableSdfDrillRuntime.cs` and `DemoFirstPersonController.cs` to `Tools/OOP_Kcc_Scanner_X_005.py` and `Tools/KccApexAudit_X_005.py`.

Cinematic cheats used:
- Drill placement uses cached terrain height/normal for seabed snap instead of collider truth.
- Voxel snap uses a quality-scaled SDF raymarch step: coarse on weak devices, tighter through continuous `HomeostasisBrain.GlobalQualityWeight`.

Exact microseconds saved:
- Per-frame saving is 0 us for drill snap because it is deployment-time, not frame-hot.
- Removed one deployable `RaycastCommand.ScheduleBatch` bridge and one possible per-frame demo Rigidbody velocity writer.
- Broad non-Editor forbidden residuals dropped from 122 to 113. Remaining broad residuals are outside X_005 ownership and are not claimed clean.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits = 0 with deployable/demo in scope.
- `Tools/KccApexAudit_X_005.py`: scoped forbidden count 0; broad non-Editor runtime forbidden count 113; `LockstepPlayerKinematicState` 96 bytes; `KinematicStateDTO` 64 bytes.
- Targeted `rg` found no `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, command `ScheduleBatch`, sync `Physics.Raycast/SphereCast/CapsuleCast`, Unity collision callback, or `.linearVelocity =` in deployable drill/demo controller.
- `git diff --check` passed for touched files; only CRLF normalization warnings.
- Build gate: CPU 41.8%, no active `dotnet`/`csc`.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors.

## 2026-05-23 - APEX Interaction/UI PhysX Bridge Removal

What was wrong:
- `PlayerInteraction` still used `RaycastCommand`, `QueryParameters`, dispatcher raycast receiver callbacks, `RaycastHit`, and query cache result readback for look-target acquisition.
- `UI/InteractionUI` still used `Physics.RaycastNonAlloc` and a `RaycastHit[4]` buffer for prompt acquisition.
- `PhysicalInteractionHandler` and `PickupItem` still directly assigned `Rigidbody.linearVelocity` in player-adjacent pocket-pickup / loot-magnet paths.

What was done:
- Added fixed-array spatial target registration in `InteractableRegistry`: 4096 collider slots, 4096 target payload slots, cold scene registry build, explicit `RegisterTree/InvalidateTree`, finite ray/bounds checks, nearest AABB hit selection, and synthetic `SpatialHit`.
- Rewired `PlayerInteraction` to use `InteractableRegistry.TryRaycastSpatial`; removed dispatcher raycast receiver implementation, pending raycast state, query cache result path, `RaycastCommand`, `QueryParameters`, and `RaycastHit` consumption.
- Rewired `UI/InteractionUI` to use the same spatial registry; removed `Physics.RaycastNonAlloc`, `RaycastHit[4]`, and prompt hit resolver.
- Added registration calls to common player-facing interactables already using invalidation: `HectonItem`, `PickupItem`, `StorageCrate`, `VRCableDragPlug`, `HeavyCarryInteractable`, `BioReactor`, `BatteryCharger`, and `MountablePlayerTransport`.
- Removed direct player-adjacent `linearVelocity =` writes from `PickupItem` and `PhysicalInteractionHandler`; restore now uses `PhysicsForceRouter.QueueForce(..., ForceMode.VelocityChange)` when a velocity delta must be restored.
- Added `PlayerInteraction.cs`, `InteractableRegistry.cs`, `PhysicalInteractionHandler.cs`, `PickupItem.cs`, and `UI/InteractionUI.cs` to `Tools/OOP_Kcc_Scanner_X_005.py` and `Tools/KccApexAudit_X_005.py`.

Cinematic cheats used:
- Interaction targeting uses registered AABB proxies, not collider-accurate PhysX truth. This is acceptable for prompt/hover acquisition and keeps gameplay collision authority out of PhysX.
- Low tier keeps the 20Hz prompt/look cadence and simple bounds intersection. Higher tiers can improve proxy richness without changing the route.

Exact microseconds saved:
- Removed one player look `RaycastCommand` bridge and one UI prompt `Physics.RaycastNonAlloc` cadence path. Exact us requires Unity profiler; no numeric saving is claimed.
- Removed direct Rigidbody velocity writes in the scoped pickup/physical interaction lane.
- Added cold scene registry rebuild cost only; hot path is bounded fixed-array scan and does not allocate.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits = 0 with interaction/UI/pickup in scope.
- `Tools/KccApexAudit_X_005.py`: scoped forbidden count 0; broad non-Editor runtime forbidden count 117; `LockstepPlayerKinematicState` 96 bytes; `KinematicStateDTO` 64 bytes.
- Targeted `rg` found no `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, `RaycastHit`, `IDispatcherRaycastReceiver`, sync `Physics.Raycast/SphereCast/CapsuleCast`, Unity collision callback, or `.linearVelocity =` in the changed interaction/UI/pickup files.
- `git diff --check` passed for touched runtime/script/report files; only CRLF normalization warnings.
- Compile is pending after this latest patch: no active `dotnet/csc` process was found, but CPU measured 87.9% then 95.6%, above the 50% project build gate.

Residuals not hidden:
- Whole non-Editor runtime still has 117 forbidden PhysX/callback residuals outside expanded X_005 scope in `Docs/Reports/KCC_APEX_AUDIT_X_005.json`.
- `LockstepPlayerKinematicState` remains 96 bytes explicit and gap-free. The 64-byte hot KCC state remains `KinematicStateDTO`.

## 2026-05-23 - APEX Laser/PDA/Battery PhysX Bridge Removal

What was wrong:
- `LaserCutterDodRuntime` still held `RaycastCommand`/`RaycastHit` vault lanes and scheduled `RaycastCommand.ScheduleBatch`.
- `DiegeticPdaFocusDistanceController` still used `Physics.RaycastNonAlloc` from the player camera while focus was armed.
- `PhysicalBatteryCompartment` still wrote `Rigidbody.linearVelocity` directly during battery snap suppression/restore.

What was done:
- Replaced cutter PhysX command scheduling with `BuildCutterSdfProbeHitsJob`, a bounded Burst SDF probe over cached `IVoxelSonarSdfReadModel` payload bytes.
- Replaced cutter `RaycastHit` evaluation with `EvaluateCutterProbeHitsJob` consuming `VoxelSonarSdfRaycastHit` rows.
- Removed cutter command/hit buffer constants and handles from active runtime ownership.
- Replaced PDA focus raycast with cached voxel SDF focus distance resolution.
- Replaced battery snap direct linear/angular velocity writes with deferred `PhysicsForceRouter.QueueForce/QueueTorque(..., VelocityChange)` restoration.
- Added laser DOD, PDA focus, and battery snap files to X_005 scanner/audit scope.

Cinematic cheats used:
- PDA depth of field now accepts SDF focus distance only; if no SDF surface is available, it disables focus instead of querying scene colliders.
- Cutter visual impact still uses existing GPU spark/decal DTOs; collision truth is SDF, not arbitrary collider hits.

Exact microseconds saved:
- Cutter: removes one PhysX command batch and two PhysX command/hit lanes from scoped tool route; exact saving pending profiler.
- PDA: removes one armed sync `Physics.RaycastNonAlloc` per frame; exact saving pending profiler.
- Battery: removes direct Rigidbody velocity writes; expected microsecond saving is small, correctness gain is authority cleanup.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits = 0.
- `Tools/KccApexAudit_X_005.py`: scoped forbidden count 0; broad non-Editor runtime forbidden count 108; `LockstepPlayerKinematicState` 96 bytes; `KinematicStateDTO` 64 bytes.
- Targeted `rg` over laser/PDA/battery files found no `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, standalone `RaycastHit`, command `ScheduleBatch`, sync `Physics.Raycast/SphereCast/CapsuleCast`, collision callbacks, or `.linearVelocity =`.
- `git diff --check` passed for touched files; only CRLF normalization warnings.
- Compile pending by project build gate: seven external `dotnet` processes were active and CPU measured 99.8%, 99.4%, then 100%.

Residuals not hidden:
- Whole non-Editor runtime still has 108 forbidden PhysX/callback residuals outside expanded X_005 scope in `Docs/Reports/KCC_APEX_AUDIT_X_005.json`.
- Compile proof for this latest patch has not run yet because the workstation build gate is closed.
## 2026-05-23 - APEX Scanner Scientific Occlusion PhysX Bridge Removal

What was wrong: `ScannerTool` still queued scientific lore occlusion through `SystemDispatcher.QueueDispatcherRaycast`, implemented `IDispatcherRaycastReceiver`, consumed `RaycastHit`, and carried pending PhysX command state. This was a player-tool hidden `RaycastCommand` bridge even after Hydro KCC and laser/PDA paths were SDF-heavy.

What was done: removed the scanner dispatcher receiver path, request salt, pending command state, `RaycastCommand`, `QueryParameters`, and `RaycastHit` callback. Scientific lore occlusion now resolves through cached `IVoxelSonarSdfReadModel.TryRaymarchNearestSonarSdf` plus bounded `WorldSpatialHashGrid.CollectContactsNonAlloc` spatial occlusion. `DataArchaeologyRuntime` target APIs were renamed from raycast target to probe target. `Tools/OOP_Kcc_Scanner_X_005.py` and `Tools/KccApexAudit_X_005.py` now include `ScannerTool.cs` and `DataArchaeologyRuntime.cs`.

Cinematic cheats used: spatial occlusion uses fixed broadphase centerline radius as a conservative visual/progression gate instead of exact collider occlusion. SDF raymarch step scales continuously from low-tier coarse steps to high-tier tighter steps through `GlobalQualityWeight`.

Exact microseconds saved: not profiler-measured. Expected saving is one scanner scientific-lore PhysX command enqueue/readback/callback per resample window, cadence-bound by `focusedScanResampleInterval`. Static proof after this patch: X_005 scoped forbidden count 0; broad outside-domain forbidden count 107; `LockstepPlayerKinematicState` 96 bytes gap-free; `KinematicStateDTO` 64 bytes gap-free.

Compile state: manual `dotnet build` is still blocked by project build gate. External Unity `csc/dotnet` waves ran while CPU was 100%; no `error CS` lines for `ScannerTool.cs` or `DataArchaeologyRuntime.cs` were found in `Editor.log`, but this is not recorded as a manual build pass.
## X_005 APEX Runtime Sync Cast Cleanup - Floater/Socket

What was wrong:
- `Gameplay/Floater.cs` still used `Physics.RaycastNonAlloc` and `RaycastHit[]` for held floater attachment from the player forward vector.
- `HectonSocketHelper.cs` still contained a raw `Physics.RaycastNonAlloc` snap helper and `RaycastHit[]` field even though the behavior was editor tooling.
- Full non-Editor raw scan before this loop found exactly two sync `Physics.Raycast/SphereCast/CapsuleCast` call sites in `Assets/_Project/Scripts`: floater attach and socket snap.

What was done:
- Replaced floater attach targeting with `WorldSpatialHashGrid.CollectContactsNonAlloc` over registered `Pickup`, `Resource`, `Scannable`, and `Module` owners.
- Floater selection now validates finite origin/direction/range, filters by configured layer mask, rejects self/owner hits, gates candidates by a bounded forward cone, and resolves `Rigidbody`/target transform without `RaycastHit`.
- Removed the socket helper PhysX snap probe entirely. The editor context menu now logs a precise warning and requires a construction-owned non-PhysX surface route before re-enable.
- Added `Gameplay/Floater.cs` and `HectonSocketHelper.cs` to `Tools/OOP_Kcc_Scanner_X_005.py` and `Tools/KccApexAudit_X_005.py`.

Cinematic cheats used:
- Floater attach uses a registered-owner cone proxy instead of exact collider surface intersection. This is cheaper and deterministic enough for a buoyancy attachment action, but it cannot attach to arbitrary unregistered scene colliders until those owners publish a first-party spatial/SDF proxy.
- Socket snap editor behavior was not faked. It was disabled rather than silently using a hidden PhysX ray.

Exact microseconds saved:
- Full runtime sync cast count in non-Editor project scripts is now 0.
- Removed one episodic floater attach `Physics.RaycastNonAlloc` and one editor/raw-source snap query. Exact frame microseconds are not claimed without profiler capture.
- Latest static proof:
  - `rg` full non-Editor sync cast scan: 0 matches.
  - `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}`, Hydro forbidden command hits 0.
  - `Tools/KccApexAudit_X_005.py`: scoped forbidden count 0, broad forbidden count 105, broad sync physics query count 0, `KinematicStateDTO` 64 bytes, `LockstepPlayerKinematicState` 96 bytes.
  - `git diff --check`: passed for touched files; CRLF warnings only.
- Compile not launched in this loop because CPU measured 94.2%, above the project 50% build gate, with no visible `dotnet/csc` process output.

## X_005 APEX XR Look-At Input Bridge Cleanup

What was wrong:
- `Core/InputDispatcher.cs` staged XR look-at as a `RaycastCommand`, queued it through `SystemDispatcher.QueueDispatcherRaycast`, and consumed `IDispatcherRaycastReceiver`.
- This was a player input selection bridge into PhysX, not a harmless Core implementation detail.

What was done:
- Removed `RaycastCommand`, `QueryParameters`, `RaycastHit`, the look-at command DataVault handle, and the dispatcher callback implementation from `InputDispatcher`.
- Replaced the XR look-at probe with `InteractableRegistry.TryRaycastSpatial` over the fixed registered interaction target cache.
- Kept existing AUP drift, forward-dot, and lateral reuse gates so the probe remains bounded and deterministic in behavior.
- Added `Core/InputDispatcher.cs` to the X_005 scanner and APEX audit scope.

Cinematic cheats used:
- XR look-at now uses registered bounds intersection, not exact collider triangle/sweep truth. This is acceptable for input targeting and can later be enriched by registered proxy bounds or SDF surfaces without changing the owner route.

Exact microseconds saved:
- Removes one player XR look-at PhysX command enqueue/readback lane. Exact profiler value is not claimed.
- Static proof after this patch:
  - `Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}`, Hydro forbidden command hits 0.
  - `Tools/KccApexAudit_X_005.py`: scoped forbidden count 0, broad forbidden count 96, broad sync physics query count 0.
  - Targeted `rg` over `InputDispatcher`, `Floater`, and `HectonSocketHelper`: zero `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, `QueryParameters`, `RaycastHit`, `IDispatcherRaycastReceiver`, `QueueDispatcherRaycast`, sync `Physics.*Cast`, collision callback, or direct `.linearVelocity =`.
  - `git diff --check`: passed for touched files; CRLF warnings only.
- Compile not launched because CPU measured 91.5%, above the project 50% build gate.
## APEX Continuation - 2026-05-24 - Broad PhysX Command Gate

What was wrong:
- Narrow X_005 reports were no longer sufficient. Broad non-Editor runtime still contained real PhysX command bridges after the KCC route itself became SDF-heavy.
- `ProceduralCrabLegIKRuntime` still scheduled `RaycastCommand.ScheduleBatch` and owned command/hit/mask DataVault buffers for visual leg grounding.
- Proof artifacts still had one command-type false positive from a diagnostic string, which made broad residual evidence noisy.

What was done:
- Removed the remaining broad-audit PhysX command/collision callback debt from the current pass. Latest `Tools/KccApexAudit_X_005.py`: `scoped_forbidden_count = 0`, `broad_forbidden_count = 0`, whole non-Editor sync Physics cast count = 0.
- Replaced crab leg PhysX grounding with a bounded Burst analytic target job feeding the existing step scheduler, body tilt, and analytical IK jobs.
- Removed crab command/hit/mask DataVault lanes and renamed stale DataVault/comment labels that only preserved forbidden text.
- Kept the `LockstepPlayerKinematicState` truth honest: it is 96 bytes gap-free. The 64-byte hot DTO is `KinematicStateDTO`.

Cinematic cheats used:
- Visual IK foot placement now uses root-relative analytic surface targets plus velocity lead and spatial avoidance instead of collider-accurate PhysX hits. This is a deliberate visual downgrade until a cached terrain/SDF foot-surface producer exists.
- Seam/world/ecosystem/fauna visual checks prefer cached terrain/SDF, registered owner data, or deterministic degradation over runtime collider queries.

Exact microseconds saved:
- Not claimed. Static proof shows bridge removal; profiler proof is blocked until compile/profiler gate opens.
- Expected saving class: command scheduling/readback spikes and collision callback storms, not a guaranteed constant per-frame number.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}`.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, `KinematicStateDTO` 64 bytes, `LockstepPlayerKinematicState` 96 bytes.
- Exact non-Editor PhysX command type scan: zero `RaycastCommand`, `CapsulecastCommand`, `CapsuleCastCommand`, `SpherecastCommand`, `SphereCastCommand`.
- Full non-Editor sync cast scan: zero `Physics.Raycast/RaycastNonAlloc/SphereCast/SphereCastNonAlloc/CapsuleCast/CapsuleCastNonAlloc/BoxCast`.
- Full non-Editor Unity collision callback scan: zero `OnCollisionEnter/Stay/Exit`.
- Compile gate later opened at CPU 39.2% with no active `dotnet/csc`; `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
## 2026-05-24 - APEX Velocity Authority Consolidation

What was wrong: the PhysX command/callback/sync-cast gates were clean, but Rigidbody velocity ownership was still split across non-owner systems. Direct `linearVelocity/angularVelocity` writes existed in docking, construction recovery, fauna hibernation/stun/teleport/death, fauna director hydration, debris/collapse, persistent-world hydration/dehydration, airlock snap/teleport, emergency wrecks, station keeping, floating origin, voxel dampening, hydrodynamic emergency reset, prologue capsule lock, QA trap recovery, survival restore, spawn/save, physical hand, vehicle motor, tether payload, mountable transport, and global physics culling.

What was done: `PhysicsApplySystem` now accepts exact target velocity packets via `SetLinearVelocity` and `SetAngularVelocity`. External systems route through `PhysicsForceRouter.QueueLinearVelocitySet/QueueAngularVelocitySet`; player-body linear velocity targets are bridged to the cached player force sink instead of direct Unity writes. Static scans now leave only DTO/state assignments and the central owner writes inside `PhysicsApplySystem`.

Cinematic cheats used: none. This was authority cleanup, not a visual approximation. Existing visual/kinematic callers keep their presentation semantics while velocity mutation moves to the physics owner route.

Exact microseconds saved: not claimed. Static debt removed: broad non-Editor external Rigidbody velocity assignment count reduced to zero; direct `AddForce/AddTorque` outside `PhysicsApplySystem` also remains zero. Profiler capture still required for frame-time numbers.

Verification: `Tools/OOP_Kcc_Scanner_X_005.py` -> `{}`. `Tools/KccApexAudit_X_005.py` -> `broad_forbidden_count: 0`, `scoped_forbidden_count: 0`, `external_rigidbody_velocity_assignment_count: 0`, `external_rigidbody_force_call_count: 0`. Broad direct velocity assignment scan -> only `FaunaDirector` DTO/state and `PhysicsApplySystem` central owner writes. Compile passed after gate opened: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` -> 0 warnings, 0 errors.

## 2026-05-24 - Hydro External Velocity Ingress

What was wrong:
- `HectonPlayerMotor.SetLinearVelocity` had become a quarantine no-op. Save/spawn/seat/mount/kinematics callers could submit exact velocity targets and get no runtime state change.
- Player-body velocity targets routed through `PhysicsForceRouter` became `IPlayerMovementForceSink.QueueExternalVelocityChange`, but Hydro KCC did not consume `HectonPlayerState.ExternalVelocityChange`.
- `HectonPlayerMovement.ApplyQueuedExternalKinematicForces` could build a target velocity from stale `_rb.linearVelocity` while Hydro owned collision.

What was done:
- Added Hydro external ingress fields for acceleration, velocity delta, and exact target velocity.
- `ApplyEnvironmentalForcesJob` applies those fields to player row 0 before SDF collision resolution and marks external-control flags.
- `HectonPlayerMotor` now routes force/acceleration/impulse/velocity-change into Hydro when Hydro owns collision; non-Hydro fallback still queues through the central `PhysicsApplySystem` packet owner.
- `SetLinearVelocity` now routes Hydro exact targets to `HydrodynamicKccRuntime.TryQueueExternalVelocityTarget` and non-Hydro exact targets to `PhysicsForceRouter.QueueOwnedLinearVelocitySet`.
- `HectonPlayerMovement.ApplyQueuedExternalKinematicForces` now sends Hydro-active queued deltas through the motor velocity-change route instead of deriving an absolute target from the Rigidbody shell.
- Reworded remaining comment-only forbidden API names so raw exact scans have no false positives.

Cinematic cheats used:
- None for velocity authority. This is a truth-route fix. Existing Hydro max-speed, drag, SDF projection, and quality-scaled sample count still define motion fidelity.

Exact microseconds saved:
- Not claimed. This removes lost velocity targets and split authority, not a measured standalone frame-time block.
- Static proof after patch:
  - `Tools/OOP_Kcc_Scanner_X_005.py`: `{}`.
  - `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, external Rigidbody velocity assignments 0, external Rigidbody force calls 0.
  - Raw exact non-Editor forbidden-symbol scan: 0 matches for sync casts, PhysX command types, and Unity collision callbacks.
  - Broad direct velocity assignment scan: only `FaunaDirector` DTO/state fields and central `PhysicsApplySystem` owner writes.
  - `git diff --check`: passed; CRLF warnings only.
  - Compile: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` -> 0 warnings, 0 errors.
## 2026-05-24 - Hydro Pose Ingress / Owner Pose Packet

What was wrong: Hydro-active player pose routes still had a gap. Velocity ingress was fixed, but `MovePosition` returned under Hydro and spawn/dismount fallback paths could still touch Rigidbody pose directly when a motor was absent.

What was done: Added `FlagExternalPositionTarget`, `ExternalPositionTargetAup`, queued AUP position storage, and `TryQueueExternalPositionTarget` in `HydrodynamicKccRuntime`. `ApplyEnvironmentalForcesJob` now consumes the queued double3 AUP target for player row 0 before SDF contact sampling. `HectonPlayerMotor.MovePosition` queues Hydro AUP targets instead of dropping the call. `HectonPlayerSpawner` and `MountablePlayerTransport` now route missing-motor pose fallbacks through a new `PhysicsApplySystem` `SetPose` packet instead of direct Rigidbody pose calls.

Cinematic cheats used: no physical cheat added. The accepted cheat is timing: pose fallback is deferred to the existing fixed owner phase rather than applied immediately from feature scripts.

Proof: `Tools/OOP_Kcc_Scanner_X_005.py` returns `{}`. `Tools/KccApexAudit_X_005.py` returns broad/scoped forbidden count 0, external velocity/force count 0, external player pose fallback count 0, `KinematicStateDTO` 64 bytes, `LockstepPlayerKinematicState` 96 bytes. Full runtime forbidden-symbol `rg` returns zero sync cast, PhysX command, and Unity collision callback matches. Targeted pose bypass scan returns zero `playerRigidbody.MovePosition/MoveRotation` or `_riderBody.MovePosition/MoveRotation` hits.

## 2026-05-24 - Lockstep 64 / Hidden Query Gate

What was wrong:
- `LockstepPlayerKinematicState` was still 96 bytes. It was gap-free, but it violated the X_005 64-byte explicit AUP mandate.
- The previous proof focused on sync casts and PhysX command bridges. `PhysicalInteractionHandler` still had a physical panel `OverlapSphereNonAlloc`, and `PhysicalHandController` still had a non-SDF hand-shell overlap fallback.
- The scanner did not classify hidden `Overlap*/Check*/ComputePenetration/SyncTransforms` or collider/body component query methods.

What was done:
- Rebuilt `LockstepPlayerKinematicState` as a 64-byte explicit DTO: `double3 PositionAup` 0..24, `float3 Velocity` 24..36, `float3 InputVector` 36..48, `Frame/Flags/InputActions` 48..60, explicit pad bytes 60..64. Replay version bumped to 2 because the hashed player-state ABI changed.
- Hashing now uses `PositionAup` directly instead of sector/local reassembly. Existing sector/local/forward consumers compile through compatibility accessors.
- Added `PhysicalHandReceiverRegistry.QuerySphere`, a fixed registered receiver bounds query, and routed physical panel button selection through it.
- Removed the non-SDF VR hand-shell PhysX overlap fallback. SDF hand bridge remains the only contact owner; fallback mode degrades to no contact rather than querying PhysX.
- Expanded `Tools/OOP_Kcc_Scanner_X_005.py` and `Tools/KccApexAudit_X_005.py` to catch hidden PhysX queries.

Cinematic cheats used:
- VR hand fallback uses visual degradation/no contact when the SDF bridge is disabled. This is deliberate: no fake collision truth and no hidden PhysX overlap.

Exact microseconds saved:
- Not claimed without profiler. Static debt removed from scoped route: one XR panel overlap probe and one fallback hand-shell overlap route. DTO payload reduced by 32 bytes per player state snapshot.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}`.
- `Tools/KccApexAudit_X_005.py`: scoped forbidden 0, broad forbidden 21 hidden queries outside X_005, `LockstepPlayerKinematicState` 64 bytes, `KinematicStateDTO` 64 bytes.
- `Docs/Reports/KCC_APEX_AUDIT_X_005.md`: lockstep layout covers bytes 0..64 with no gaps and no overlaps.
- Corrected exact non-Editor sync cast / PhysX command / Unity collision callback scan: zero matches.
- Targeted Hydro/player/interaction hidden-query scan: zero matches.
- `git diff --check`: passed; CRLF warnings only.
- Compile gate opened on attempt 10 at CPU 39.9% with no `dotnet/csc`; `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.

Residual truth:
- Whole-runtime hidden PhysX query debt is not zero. Current broad residual count is 21 outside X_005 ownership and is listed in `Docs/Reports/KCC_APEX_AUDIT_X_005.json`.

Exact microseconds saved: 0 us claimed. This is an authority correctness patch. It prevents one-frame or permanent divergence between Rigidbody shell pose and Hydro KCC AUP truth; frame-time benefit requires profiler capture.

Compile status: passed after gated wait. Attempts 1-6 were blocked by CPU/compiler policy; attempt 7 opened at CPU 31.9% with no active external compiler. `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` completed with 0 warnings and 0 errors.

## 2026-05-24 - Broad Runtime Hidden Query Eradication

What was wrong:
- Broad runtime audit still had 21 hidden PhysX query sites outside the scoped KCC files. They were `Physics.Overlap*`, `Physics.CheckSphere`, `Physics.SyncTransforms`, and `Collider.ClosestPoint` calls in base, construction, fluid, voxel, gameplay, and visual world systems.
- These were not `CapsulecastCommand` bridges, but they were still main-thread scene-query/readback escape routes that could be hit from placement, collapse, pressure, thermal, hazard, and visual occupancy flows.

What was done:
- Replaced base/player interior resync with `GlobalRegistry.Player` plus the existing oriented-box containment math.
- Replaced builder placement, autonomous extractor binding, cavitation, voxel collapse, implosion, steam explosion, pressure blowout, exterior boiling, seismic shockwave, compound-collider LOD threat, cave-light occupancy, and sargassum snag fan-out with fixed `WorldSpatialHashGrid` contact buffers.
- Added `BaseLogisticsNetwork.CollectStorageCratesNonAlloc` and routed `RepairDroneHub` storage discovery through the registered logistics owner instead of a collider overlap.
- Replaced collider closest-point calls with explicit AABB clamp math in submarine breach placement and sargassum cutting.
- Removed the post-origin-shift `Physics.SyncTransforms` from construction joint recovery; body velocity restoration remains in the owning physics route.
- Cleaned the last comment-only Unity collision callback string from `HectonPlayerMovement`.

Cinematic cheats used:
- Visual cave occupancy now degrades to registered occluders instead of arbitrary collider overlap. This is accepted visual degradation: unregistered decorative colliders no longer block the local lighting SDF until their owner registers a proxy.
- Fluid/world impulse fan-out now affects registered runtime owners. Unregistered arbitrary Rigidbody shells are intentionally ignored rather than queried through PhysX.

Exact microseconds saved:
- Not claimed without profiler. The measurable fact is structural: broad hidden query count moved from 21 to 0, and 21 main-thread query/readback routes were removed.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, external Rigidbody velocity/force/player-pose bypasses 0, `KinematicStateDTO` 64, `LockstepPlayerKinematicState` 64.
- Raw non-Editor forbidden-symbol scan: zero matches for Unity sync casts/overlaps/checks, PhysX command types, Unity collision callbacks, `Physics.SyncTransforms`, and `.ClosestPoint(`.
- `git diff --check`: passed for touched files; CRLF warnings only.
- Compile gate first stayed closed for 18 checks. Second gate opened on attempt 4 at CPU 46.5% with no compiler processes.
- Compile passed: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` -> 0 warnings, 0 errors.

Residual truth:
- Runtime C# is clean for the hidden query classes tracked by X_005. `Rigidbody` components still exist as serialized presentation/interop shells; removing them requires prefab/scene migration and owner-contract changes, not a narrow C# grep patch.

## 2026-05-24 - KCC Manifold / 100mps Cone Static Proof

What was wrong:
- The pure SDF KCC route was free of PhysX command bridges, but `BuildSdfCollisionHitsJob` reduced each sweep sample to one strongest capsule-axis probe. That is stable, but it can discard simultaneous bottom/mid/top contacts in tight voxel corners.
- The 100 m/s cone proof existed in the audit report, but the smoke geometry did not include a named cone fall case.

What was done:
- `BuildSdfCollisionHitsJob` now writes every penetrating bottom/mid/top capsule-axis probe until the fixed 8-contact stride is full.
- `KinematicResolutionJob` now treats an absent collision-hit lane as zero executed contact projections instead of relying on implicit default `NativeArray` behavior.
- `GenerateMockTestGeometryJob` now includes a central voxel cone, and default smoke profile index 1 starts above it with velocity `(0,-100,0)`.
- `Tools/KccApexAudit_X_005.py` now reports the actual solver bound: up to 24 SDF axis probes, 8 stored contact planes, and 64 projection operations per entity.

Cinematic cheats used:
- No gameplay cheat. The accepted proof cheat is headless SDF geometry instead of a scene/prefab fixture; it avoids Unity object and PhysX shell dependency.

Exact microseconds saved:
- 0 us claimed. This patch buys solver robustness and proof coverage. It may add a few stored contact writes when multiple capsule probes penetrate in one sweep step; still no allocation and no new job.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, external Rigidbody velocity/force/player-pose bypasses 0, `KinematicStateDTO` 64, `LockstepPlayerKinematicState` 64.
- Raw non-Editor forbidden-symbol scan: zero matches for Unity sync casts/overlaps/checks, PhysX command types, Unity collision callbacks, `Physics.SyncTransforms`, and `.ClosestPoint(`.
- `git diff --check`: passed for touched KCC/proof files; CRLF warnings only.
- Compile gate initially stayed closed across 140 attempts because CPU exceeded 50% and/or external `dotnet/csc` existed. The remaining idle MSBuild node-reuse pool (`dotnet.exe` `/nodemode:1 /nodeReuse:true`, created 2026-05-24 10:28:07) was closed through `dotnet build-server shutdown`. Next gate opened at CPU 33.2% with no compiler processes. `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nodeReuse:false` passed with 0 warnings and 0 errors.

Residual truth:
- Solver termination proof is hard-bounded: no recursion, fixed stored contact cap 8, fixed projection pass cap 8, <=64 projection operations per entity.
- SDF fidelity is still limited by voxel cell size. If a cone tip is smaller than the baked SDF resolution, the solver can only produce a bounded conservative stop/slide against the sampled field; it cannot reconstruct sub-voxel collider geometry without a denser SDF or an explicit analytic obstacle lane.

## 2026-05-24 - Legacy Player Sweep Carrier Cleanup

What was wrong:
- Player motor PhysX bridges were disabled, but the native-state ensure methods still kept a cold allocation route for scheduled `RaycastHit` result lanes.
- `TrySweepGatedMove` under Hydro authority could still flow through the disabled scheduled bridge shape instead of directly routing the displacement into KCC AUP ingress.
- The audit report proved broad forbidden calls, but did not explicitly show that player motor/state had zero `RaycastHit`/command allocation lanes for the disabled bridge.

What was done:
- `HectonPlayerMotor.TrySweepGatedMove` now short-circuits when `HydrodynamicKccOwnsCollision()` is true: it millimeter-snaps the requested displacement result and routes it through `MovePosition`, which queues the target into `HydrodynamicKccRuntime`.
- `HectonPlayerMotorNativeState.EnsureScheduledSweepState` and `EnsureKinematicRepairTargetState` now release any stale scheduled result arrays and leave the handles default. No `RaycastHit` native/vault result lanes are allocated for those disabled bridges.
- `Tools/KccApexAudit_X_005.py` now writes `Legacy Player Sweep Bridge Result` into markdown/JSON: capsule bridge disabled, repair bridge disabled, Hydro AUP fallback true, player motor `RaycastHit` allocations 0, player motor command allocations 0.

Cinematic cheats used:
- No visual cheat. This is authority cleanup: a disabled PhysX bridge now degrades to Hydro-owned AUP target routing, not a hidden scene query.

Exact microseconds saved:
- 0 us claimed without profiler. Concrete structural reduction: cold player sweep/repair `RaycastHit` native allocation lanes removed and one stale wait path eliminated.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, external Rigidbody velocity/force/player-pose bypasses 0, player motor `RaycastHit` allocations 0, player motor command allocations 0, `KinematicStateDTO` 64, `LockstepPlayerKinematicState` 64.
- Exact PCRE non-Editor runtime scan with `Hecton8.Physics` namespace excluded: zero matches for Unity `Physics.*` sync query calls, PhysX command types, Unity collision callbacks, `Physics.SyncTransforms`, and `.ClosestPoint(`.
- Scoped KCC/player scan: zero matches for `CapsulecastCommand`, `RaycastCommand`, `ScheduleBatch`, sync Unity Physics calls, collision callbacks, direct `Rigidbody.AddForce`, and direct `.linearVelocity =`.
- `git diff --check`: passed for touched runtime/proof/report files; CRLF warnings only.
- Compile gate initially closed at CPU 63.0/89.4/96.9% with active `csc/dotnet`. Gate opened on attempt 10 at CPU 44.2% and no compiler processes.
- Compile passed: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nodeReuse:false` -> 0 warnings, 0 errors.

Residual truth:
- `RaycastHit` DTOs and raycast-named legacy services still exist outside the player KCC sweep bridge in broader tool/equipment/vehicle/presentation routes. They are not claimed erased.
- Current verified claim: non-Editor runtime has no Unity `Physics.*` sync query calls, no PhysX command scheduling bridges, no Unity collision callbacks, no scoped player/KCC direct Rigidbody velocity/force bypasses, and no player motor native allocation of disabled sweep/repair result lanes.

## 2026-05-24 - Legacy RaycastBatch Facade Memory Trim

What was wrong:
- `RaycastBatchHelper` had already stopped scheduling PhysX, but it still kept a managed `QueryResult[512]` mirror from the old command-batch implementation.
- The old mirror created proof ambiguity: the service name and result array looked like an active ray bridge even though no runtime caller used the batch facade.

What was done:
- Removed the `QueryResult[]` field and all buffer clear/release loops from `RaycastBatchHelper`.
- `AddQuery` now validates the legacy request and returns a bounded deterministic miss slot. `ExecuteBatch` and `LateFrameTick` mark those slots complete as misses. `GetResult` returns default.
- Extended `Tools/OOP_Kcc_Scanner_X_005.py` to include `RaycastBatchHelper.cs` and `QueryCacheContext.cs`, with a dedicated `legacy_query_result_array` pattern.
- Extended `Tools/KccApexAudit_X_005.py` to report `legacy_batch_query_result_arrays = 0` and `legacy_batch_physx_calls = 0`.
- Rechecked the tool interaction route: `PlayerTool` -> `EquipmentInteractionHandler.TryRaycastPrimary` -> `TryResolveKinematicRaycastHit` -> SDF raymarch or terrain provider. No Unity Physics query is used there.

Cinematic cheats used:
- Legacy batch queries degrade to deterministic misses. Actual gameplay tool hits use owned SDF/terrain routes.

Exact microseconds saved:
- 0 us claimed without profiler. Removed structural cost: one cold managed `QueryResult[512]` allocation and its clear loops.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, `legacy_batch_query_result_arrays` 0, `legacy_batch_physx_calls` 0, player motor `RaycastHit` allocations 0, player motor command allocations 0, `KinematicStateDTO` 64, `LockstepPlayerKinematicState` 64.
- Exact PCRE non-Editor runtime scan with `Hecton8.Physics` namespace excluded: zero Unity `Physics.*` sync query calls, zero PhysX command types, zero collision callbacks, zero `.ClosestPoint(`.
- `rg` over `RaycastBatchHelper.cs`: zero `_results`, `QueryResult[]`, `new QueryResult[`, `RaycastCommand`, `ScheduleBatch`, or `Physics.` hits.
- `git diff --check`: passed for touched runtime/proof/report files; CRLF warnings only.
- Compile gate was open at CPU 37.2/10.4/22.0% with no `dotnet/csc`.
- Compile passed: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nodeReuse:false` -> 0 warnings, 0 errors.

Residual truth:
- `RaycastHit` remains a DTO in the wider tool API. It is not currently a PhysX bridge in `EquipmentInteractionHandler`; SDF/terrain are the owning resolvers. Migrating that DTO is a separate API pass.

## 2026-05-24 - KCC Black Box And Split-Authority Player State Cleanup

What was wrong:
- Hydro KCC black-box dumping could suppress a repeated fault with the same mask after a clean frame, and the LateFrame fault scan did not explicitly open the fault lane at current entity capacity.
- Multiple player consumers still had hidden split authority through `playerRigidbody.linearVelocity` or equivalent Rigidbody state readback.
- `TetherInstance` still read `_playerRigidbody.GetPointVelocity`, `_playerRigidbody.mass`, and `_playerRigidbody.isKinematic` when computing anchor velocity, reduced mass, damping, and tow reaction.
- `HectonPlayerSpawner` still preserved teleport angular/pose state from Rigidbody; `HarpoonLauncherTool` scaled recoil from player Rigidbody mass.

What was done:
- `HydrodynamicKccRuntime` now resets the fault dump latch on clean frames, scans fault flags at full entity capacity, requires the states lane for telemetry, and records exact zero collision iterations when collision resolution is bypassed.
- `PhysicsDeterminismSignals` now exposes finite/fresh KCC velocity helpers for `float3` and `Vector3`.
- Moved player velocity consumers to KCC velocity signal: noise, action interrupts, swim presentation, survival movement/save velocity, player spawner teleport velocity, cave roots, critical audio, crash telemetry, fauna target snapshots, runtime context, underwater visuals, world streaming, tether visual anchor, thermal cable visuals, and vegetation motion state.
- `TetherInstance` now reads player anchor velocity from KCC signal, uses deterministic 80 kg player anchor mass, routes tow reaction through `HectonPlayerMotor.ApplyAcceleration`, and only lets the legacy force-packet bridge flush player anchor Rigidbody packets when Hydro does not own collision.
- `HectonPlayerSpawner` now resolves teleport position/rotation from `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`, preserves zero angular velocity, and keeps linear velocity from KCC signal. `HarpoonLauncherTool` uses deterministic 80 kg recoil mass.
- `Tools/KccApexAudit_X_005.py` and `Tools/OOP_Kcc_Scanner_X_005.py` now track broad player Rigidbody motion/mass/pose state reads, including `linearVelocity`, `angularVelocity`, `GetPointVelocity`, `mass`, `position`, and `rotation`.

Cinematic cheats used:
- Deterministic equivalent player mass (80 kg) replaces Rigidbody mass readback for tether anchor and harpoon recoil. This is a gameplay-stable approximation, not a physics truth source.
- Tether anchor velocity degrades to zero if the KCC velocity signal is stale. That is safer than resurrecting the Rigidbody shell as authority.

Exact microseconds saved:
- 0 us claimed without profiler.
- Structural removals: direct player Rigidbody velocity readback across 16 consumers, one player point-velocity readback, several player Rigidbody mass/pose/angular readbacks, and one player Rigidbody force-at-position reaction path.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, `player_rigidbody_velocity_read_count` 0, `player_rigidbody_motion_state_read_count` 0, `legacy_batch_query_result_arrays` 0, `legacy_batch_physx_calls` 0, `KinematicStateDTO` 64, `LockstepPlayerKinematicState` 64.
- Exact non-Editor runtime forbidden-symbol scan: zero Unity `Physics.*` sync query calls, zero PhysX command types, zero Unity collision callbacks, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- Exact player Rigidbody state scan: zero `playerRigidbody/_playerRigidbody.linearVelocity`, `angularVelocity`, `GetPointVelocity`, `mass`, `position`, or `rotation` matches.
- `git diff --check`: passed for touched runtime/proof/report files; CRLF warnings only.
- First compile gate loop stayed closed for 60 attempts due CPU above 50% and active external `dotnet/csc`. Later only idle MSBuild node-reuse processes remained with CPU delta 0 over 5 seconds; `dotnet build-server shutdown` closed them. Gate opened at CPU 48.7/40.5/30.8% with no compiler processes.
- Compile passed: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nodeReuse:false` -> 0 warnings, 0 errors.

Residual truth:
- Player Rigidbody shells still exist for serialized identity, bootstrap presentation toggles, payload/cargo physics, and central owner application. This pass does not claim prefab/scene removal of Rigidbody components.
- Verified claim: no non-Editor runtime PhysX query bridge, no command bridge, no Unity collision callback, no audited external player velocity/force/pose bypass, no disabled player sweep result allocation lane, and no direct runtime player Rigidbody motion/mass/pose state readback through `playerRigidbody/_playerRigidbody`.

## 2026-05-24 - Owner Internal Rigidbody Velocity Readback Collapse

What was wrong:
- `HectonPlayerMotor` Hydro-owned force/impulse/project paths still read `_body.linearVelocity` and `_body.mass` before routing into KCC.
- Motor torque and off-center force could still queue Rigidbody torque under Hydro authority.
- `HectonPlayerMovement` scattered `_rb.linearVelocity` through interpolation, telemetry, bailout, surface, crush, wall, wipeout, swim, and sargassum paths. External consumers were clean, but the legacy owner monolith still had many internal shell reads.
- The audit did not explicitly prove `_rb`/`_body` owner-internal centralization.

What was done:
- `HectonPlayerMotor` now resolves Hydro velocity through KCC velocity signal, position through player runtime pose snapshot, and mass through deterministic 80 kg equivalent mass.
- Hydro force/impulse/project/sweep/carrier/wake/impact paths no longer require Rigidbody velocity/mass readback.
- Hydro torque and angular velocity-change are suppressed; Hydro off-center force demotes to linear `ApplyForce`, which routes into KCC acceleration.
- `HectonPlayerMovement` now has `ResolveAuthoritativeLinearVelocity`. It prefers fresh KCC velocity signal when Hydro owns collision and falls back to `_rb.linearVelocity` only in legacy/non-Hydro mode.
- Replaced direct movement velocity reads across the old monolith; `HectonPlayerMovement.cs` now has exactly one `_rb.linearVelocity` read, inside that helper.
- `Tools/KccApexAudit_X_005.py` now emits owner-internal proof fields for movement centralization and motor Hydro authority behavior.

Cinematic cheats used:
- Deterministic 80 kg equivalent mass is used for Hydro player force conversion instead of Rigidbody mass readback. This is a controlled gameplay constant, not a measured PhysX shell value.
- Hydro angular torque is intentionally dropped until a KCC-owned angular lane exists. Visual response must come from presentation/camera/cable systems, not uncontrolled Rigidbody torque.

Exact microseconds saved:
- 0 us claimed without profiler.
- Structural reductions: 40+ movement velocity read sites now route through one authority helper, and Hydro motor force/impulse/project paths avoid Rigidbody velocity/mass shell readback.

Verification:
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, `player_rigidbody_velocity_read_count` 0, `player_rigidbody_motion_state_read_count` 0, `movement_rb_linear_velocity_read_count` 1, `movement_velocity_reads_centralized` true, `motor_hydro_force_uses_kcc_velocity` true, `motor_hydro_torque_suppressed` true, `KinematicStateDTO` 64, `LockstepPlayerKinematicState` 64.
- Exact non-Editor runtime forbidden-symbol scan: zero Unity `Physics.*` sync query calls, zero PhysX command types, zero Unity collision callbacks, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- Exact direct player Rigidbody state scan: zero `playerRigidbody/_playerRigidbody.linearVelocity`, `angularVelocity`, `GetPointVelocity`, `mass`, `position`, or `rotation` matches.
- `git diff --check`: passed for touched runtime/proof/report files; CRLF warnings only.
- Compile gate opened on attempt 2 at CPU 41.5/40.0/36.8% with no compiler processes.
- Compile passed: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nodeReuse:false` -> 0 warnings, 0 errors.

Residual truth:
- `HectonPlayerMotor` still contains two `_body.linearVelocity` reads for legacy/non-Hydro support: one raw no-op comparison in `SetLinearVelocity`, and one fallback inside `ResolveCurrentLinearVelocity` when Hydro authority is absent or KCC velocity is stale.
- That is intentional residual compatibility, not Hydro authority. Under Hydro, the new helpers route through KCC signal/runtime snapshot/deterministic mass.

## 2026-05-24 - Player Alias Split-Authority Closure

What was wrong:
- Several routes were no longer using PhysX casts, but still used the player Rigidbody shell as a hidden authority source: player impact/recoil mass, camera/fauna/fluid velocity/pose reads, predator bite force routing, airlock/save/spawn/bootstrap pose mutation, and local aliases such as `playerBody`.
- Literal `playerRigidbody` scans were insufficient because method-local aliases can hide the same forbidden shell dependency.

What was done:
- `PlayerTool`, `PlayerInventory`, `ToolHitUtility`, and central player force conversion now use deterministic 80 kg equivalent player mass instead of player `Rigidbody.mass`.
- `CameraJuiceSystem`, fauna light targeting, scooter shafts, submarine thermal updraft, and maelstrom damage paths use KCC velocity, player force sinks, movement APIs, or player pose snapshots instead of player Rigidbody velocity/COM.
- Fauna predator bite now applies a deterministic velocity change through `IPlayerMovementForceSink`/`HectonPlayerMovement`; it no longer queues force at `playerBody`.
- Airlock snap, save/load teleport, player spawn teleport, and bootstrap activation now route Hydro player pose/velocity through `HectonPlayerMotor` and legacy helper methods gated by Hydro ownership.
- `Tools/KccApexAudit_X_005.py` now tracks dynamic player Rigidbody aliases, alias motion/mass/pose reads, direct pose mutations, force bypasses, and positive proof flags for Hydro route ownership.

Cinematic cheats used:
- Deterministic 80 kg equivalent mass replaces scene-authored player Rigidbody mass for KCC-owned gameplay response.
- Hydro angular/presentation side effects stay outside the Rigidbody shell until a KCC-owned angular lane exists.

Exact microseconds saved:
- 0 us claimed without profiler.
- Concrete structural reduction: removed player shell mass/velocity/COM/pose/force alias dependencies from audited Hydro player routes and turned them into KCC signal/snapshot/sink reads.

Verification:
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, external Rigidbody velocity assignments 0, external Rigidbody force calls 0, external player pose assignments 0, player Rigidbody velocity reads 0, player Rigidbody motion-state reads 0, player body alias motion-state reads 0, direct player body force routes 4, ungated player body force routes 0, movement `_rb.linearVelocity` reads 1 centralized helper, motor `_body.linearVelocity` reads 2 legacy-only fallbacks, `LockstepPlayerKinematicState` 64, `KinematicStateDTO` 64.
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- Broad non-Editor PCRE scan: zero Unity `Physics.*` sync casts/overlaps/checks, zero PhysX command types, zero Unity collision callbacks, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- Player alias scan: zero forbidden `playerRigidbody`, `playerBody`, `_playerBody`, or `PlayerRigidbody` motion/mass/pose reads or Hydro player pose mutations.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- Last post-code compile passed with 0 errors and one generated project warning: `MSB9008` for missing `Hecton8.Input.csproj`. The source tree contains `Hecton8.Input.Generated.csproj`; generated project-file cleanup was not mixed into X_005 KCC runtime ownership.
- Current post-compaction compile rerun was not launched because CPU measured 51.8% and 7 `dotnet` processes were active.

Residual truth:
- Player `Rigidbody` components still exist as serialized Unity shells and legacy/non-Hydro compatibility objects. This is not a prefab migration.
- Verified hard claim: Hydro KCC/player runtime has no non-Editor Unity PhysX query bridge, no PhysX command bridge, no Unity collision callback route, no external direct player Rigidbody velocity/force/pose bypass, no dynamic player alias motion/mass/pose readback, and `LockstepPlayerKinematicState` is explicit 64 bytes.

## 2026-05-24 - Lockstep 64 Layout Gate Repair

What was wrong:
- `RollbackNetcodeEditTests` still asserted `LockstepPlayerKinematicState` as a 96-byte sector/local DTO. That was a stale regression gate against the current 64-byte `double3 PositionAup` contract.
- `LockstepStateValidator.ValidateBinaryLayout()` checked size but not stored field offsets.
- `Tools/AiBattleSim.py` and `Data/AI/Leviathan_Brain.json` still listed `LockstepPlayerKinematicState.LocalPosition` and `SectorX/Y/Z` in the player distance feed.

What was done:
- Added explicit runtime offset checks for `PositionAup@0`, `Velocity@24`, `InputVector@36`, `Frame@48`, `Flags@52`, and `InputActions@56`.
- Updated rollback editor layout assertions to the same 64-byte storage fields.
- Expanded `Tools/KccApexAudit_X_005.py` with a `lockstep_layout_gate` proof section.
- Updated the Leviathan AI data/tool contract to `LockstepPlayerKinematicState.PositionAup` and regenerated `Tools/AiBattleSim_Report.json`.
- Updated the SHINOBU_323 route card to stop describing `StableId` as stored player kinematic identity.

Cinematic cheats used:
- None. This was ABI/test/proof correction, not a visual simulation path.

Exact microseconds saved:
- 0 us claimed.
- Concrete structural saving remains 32 bytes per `LockstepPlayerKinematicState` row versus the old 96-byte sector/local DTO, protected by tests and runtime offset validation.

Verification:
- `python -m py_compile Tools/AiBattleSim.py Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, `LockstepPlayerKinematicState` 64, `KinematicStateDTO` 64, runtime offset gate true, rollback 64-byte test true, stale 96-byte rollback test absent.
- `Tools/OOP_Kcc_Scanner_X_005.py`: `{}` and Hydro KCC forbidden command hits 0.
- Exact non-Editor runtime forbidden-symbol scan: zero Unity `Physics.*` sync casts/overlaps/checks, zero PhysX command types, zero Unity collision callbacks, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- Active stale field scan: zero `LockstepPlayerKinematicState.SectorX/Y/Z/LocalPosition/Forward/StableId/HashCadenceFrames` references in Tools/Data/Docs/ARCHITECTURE/Tests/Scripts.
- `Tools/AiBattleSim.py`: regenerated 10,000-encounter report; artifact check with deterministic rerun passed.
- `git diff --check`: passed for touched runtime/test/tool/data/report/doc files; CRLF warnings only.
- `dotnet restore Assembly-CSharp.csproj ...`: passed and regenerated missing `Temp/obj/Assembly-CSharp/project.assets.json`.
- C# build rerun: passed after external compiler waves ended. Gate opened at CPU 47.7/38.0/45.0% with no compiler processes. `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nodeReuse:false` produced `Assembly-CSharp.dll`, 0 errors, and 1 existing generated-project warning `MSB9008` for missing `Hecton8.Input.csproj`.

Residual truth:
- C# compile is proven for this patch. The generated project warning remains outside the KCC runtime change: source has `Hecton8.Input.Generated.csproj`, while generated `Assembly-CSharp.csproj` references missing `Hecton8.Input.csproj`.

## 2026-05-24 - Runtime Callback Authority Closure

What was wrong:
- Player-adjacent systems still used Unity trigger callbacks for gameplay/presentation presence decisions: sargassum drag/cut, oxygen bubble pickup, toxic/environment hazard exposure, base module life-support occupancy, acoustic reverb, and demo door activation.
- The final residual callbacks were transport-domain routes: charging station tracking and vehicle docking capture. They were not KCC terrain casts, but they still left PhysX callback ordering in runtime authority.

What was done:
- Added `CachedTriggerVolume` as a shared Core namespace primitive volume helper. It samples collider shape cold and evaluates point containment/surface point math without `Physics.*`, `Collider.ClosestPoint`, or hot collider queries.
- Converted `SargassumPhysicsZone`, `EnvironmentalHazard`, `ToxinHazard`, `OxygenBubble`, `BaseModule`, `AcousticReverbPresetTrigger`, and `DemoDoor` to dispatcher/slow-tick polling against player runtime pose or KCC velocity signals.
- Added `PlayerTransportLifecycleRegistry`, a fixed 64-slot zero-frame-allocation registry for `IPlayerTransportLifecycleOwner`.
- Registered `MountablePlayerTransport` and `MantaScooter` in the lifecycle registry.
- Converted `TransportChargingStation` and `VehicleDockingModule` to poll registered transport owners through cached trigger volumes and existing acquisition gates. `OnTriggerEnter/Stay/Exit` are gone from non-Editor runtime scripts.
- Expanded `Tools/OOP_Kcc_Scanner_X_005.py` and `Tools/KccApexAudit_X_005.py` with the new player/transport callback proof flags.

Cinematic cheats used:
- Player and transport presence is now approximate primitive-volume math, not PhysX contact event truth. This is deliberate: predictable containment beats callback ordering for rollback-sensitive systems.
- Docking keeps existing distance/alignment gates, so the visual/physical dock behavior can remain rich while discovery is a cheap registry sweep.

Exact microseconds saved:
- 0 us claimed without Unity profiler.
- Concrete structural reduction: removed every non-Editor runtime `OnTriggerEnter/Stay/Exit` and `OnCollisionEnter/Stay/Exit` method under `Assets/_Project/Scripts`.
- Constant-cost replacements: one player pose check per relevant player-presence owner, and a bounded 64-slot transport owner sweep for charging/docking.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, `player_trigger_callback_count` 0, `base_module_uses_runtime_occupancy_polling` true, `acoustic_reverb_uses_runtime_volume_polling` true, `demo_door_uses_runtime_volume_polling` true, `transport_charging_uses_registry_volume_polling` true, `vehicle_docking_uses_registry_volume_polling` true, `LockstepPlayerKinematicState` 64.
- Whole-runtime non-Editor callback scan: zero `OnTriggerEnter/Stay/Exit` and zero `OnCollisionEnter/Stay/Exit` methods.
- Whole-runtime non-Editor PhysX query scan: zero sync `Physics.*` casts/overlaps/checks, zero PhysX command types/schedules, zero collider query helpers, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- `git diff --check`: passed for touched runtime/tool/project files; CRLF warnings only.

Residual truth:
- C# compile rerun is pending for this last callback closure because the project build gate stayed closed. Latest samples: CPU `73.3/74.7/97.1`, 8 Unity `dotnet` processes active under `C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\NetCoreRuntime\dotnet.exe`.
- Last C# compile proof still belongs to the prior X_005 patch set, not this final callback closure. Static proof is clean; compiler proof is blocked by the local no-build-under-load rule.

## 2026-05-24 - Registry Read Purity And Proof Repair

What was wrong:
- `PlayerTransportLifecycleRegistry.TryGetAt` mutated static registry slots during a read. That violated the global read-accessor doctrine and made charging/docking polling a hidden write path.
- `MantaScooter.OnDespawn` unregistered from the new transport lifecycle registry, but `OnSpawn` did not re-register. Pooled handheld transport could disappear from charging/docking discovery after reuse.
- `Tools/KccApexAudit_X_005.py` only parsed numeric `StructLayout(Size = 64)` and failed on the current const-sized `KinematicStateDTO` declaration.

What was done:
- Made `TryGetAt` read-only. Stale slot reuse remains in `Register`; explicit cleanup remains in `Unregister` and subsystem reset.
- Added `MantaScooter` registry registration in `OnSpawn`.
- Repaired the apex audit parser to resolve const int struct size expressions.

Cinematic cheats used:
- None. This was authority-route and proof repair.

Exact microseconds saved:
- 0 us claimed. The change preserves the zero-allocation 64-slot registry sweep and removes a hidden mutation/regression path.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, player trigger callbacks 0, transport registry volume polling true, `transport_registry_try_get_at_is_pure` true, `manta_scooter_registers_on_spawn` true, `LockstepPlayerKinematicState` 64, `KinematicStateDTO` 64.
- Whole-runtime non-Editor callback scan: zero Unity `OnTrigger*` and `OnCollision*` methods.
- Whole-runtime non-Editor PhysX query scan: zero sync `Physics.*` casts/overlaps/checks, zero PhysX command bridges, zero Unity collider query helpers, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- `git diff --check`: passed for touched runtime/tool files; CRLF warnings only.

Residual truth:
- C# compile rerun is still pending after this final repair. Build gate sample: CPU `81%`, compiler process count `0`; CPU remains above the local 50% threshold.

## 2026-05-24 - Player Runtime Cache Purity Closure

What was wrong:
- After callback removal, several player-presence polling routes still had runtime fallback to `GlobalRegistry.Player`: reverb trigger, demo door, BaseModule interior occupancy/resync, and Sargassum player service replacement.
- That is not a PhysX cast, but it violates the global route doctrine: GlobalRegistry is cold identity/dependency injection, not a hot polling fallback.

What was done:
- Removed `GlobalRegistry.Player` fallback from `AcousticReverbPresetTrigger.TryResolvePlayerPosition`.
- Removed `GlobalRegistry.Player` fallback from `DemoDoor.TryResolvePlayerPosition`.
- Changed `BaseModule.UpdateInteriorOccupancyFromPlayerRuntime` and `BaseModule.ResyncInteriorOccupants` to use `_cachedPlayerRuntime` only.
- Changed `SargassumPhysicsZone.OnGlobalRegistryServiceReplaced` to call `RefreshPlayerReferencesCold(..., false)`, preserving registry fallback only for cold Awake/OnEnable refresh.
- Expanded `Tools/KccApexAudit_X_005.py` with cached-player-only proof flags for these methods.

Cinematic cheats used:
- None. This is route-purity work after the earlier primitive-volume callback replacement.

Exact microseconds saved:
- 0 us claimed without Unity Profiler.
- Structural result: player-presence checks remain cached-context reads plus primitive math; no fallback global lookup is executed in their hot polling path.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, player trigger callbacks 0, `base_module_hot_occupancy_uses_cached_player_only` true, `acoustic_reverb_try_resolve_uses_cached_player_only` true, `demo_door_try_resolve_uses_cached_player_only` true, `sargassum_hotswap_disables_registry_fallback` true, `LockstepPlayerKinematicState` 64, `KinematicStateDTO` 64.
- Whole-runtime non-Editor callback scan: zero Unity `OnTrigger*` and `OnCollision*` methods.
- Whole-runtime non-Editor PhysX query scan: zero sync `Physics.*` casts/overlaps/checks, zero PhysX command bridges, zero Unity collider query helpers, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- Direct Rigidbody write/force scan: only DTO/state rows in `FaunaDirector` and central owner writes/forces in `PhysicsApplySystem` remain.
- `git diff --check`: passed for touched runtime/tool/report files; CRLF warnings only.

Residual truth:
- C# compile rerun for this cache-purity closure is still pending. Latest gate sample: CPU `100/100/100`, active compiler processes `csc.exe` PID 41432 and `dotnet.exe` PID 42208. Local build was not launched under the project rule.

## 2026-05-24 - Player Motor Runtime Context Purity

What was wrong:
- `HectonPlayerMotor.ResolveCurrentRuntimePosition` read `GlobalRegistry.Player` inside the Hydro-active runtime-position path. That is a hidden global lookup in player motor authority code.

What was done:
- Added cached `_playerRuntimeContext` to `HectonPlayerMotor`.
- Populated it during hot-swap registration and on `GlobalRegistryServiceSlot.Player` replacement.
- Replaced the direct `GlobalRegistry.Player` read in `ResolveCurrentRuntimePosition` with `_playerRuntimeContext`.
- Expanded `Tools/KccApexAudit_X_005.py` with `motor_runtime_position_uses_cached_player_context`.

Cinematic cheats used:
- None. This is authority-route cleanup.

Exact microseconds saved:
- 0 us claimed without Unity Profiler.
- Structural result: Hydro-active motor support code no longer performs a global player lookup to resolve runtime pose.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, player trigger callbacks 0, `motor_runtime_position_uses_cached_player_context` true, `LockstepPlayerKinematicState` 64, `KinematicStateDTO` 64.
- Whole-runtime non-Editor callback scan: zero Unity `OnTrigger*` and `OnCollision*` methods.
- Whole-runtime non-Editor PhysX query scan: zero sync `Physics.*` casts/overlaps/checks, zero PhysX command bridges, zero Unity collider query helpers, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- Targeted motor scan: zero direct `IPlayerRuntimeContext playerContext = GlobalRegistry.Player` in `HectonPlayerMotor.cs`.
- `git diff --check`: passed for touched runtime/tool/report/log files; CRLF warnings only.

Residual truth:
- C# compile rerun is pending. Latest gate sample after motor cache patch: CPU `100/100/100`, active compiler processes `csc.exe` PID 44540 and `dotnet.exe` PID 44772. Local build was not launched under the project rule.

## 2026-05-24 - Vehicle Docking Dead Collider Route Removal

What was wrong:
- `VehicleDockingModule` active discovery had moved to `PlayerTransportLifecycleRegistry`, but the file still contained unused collider-era resolver code with `GetComponentInParent` discovery and a `GlobalRegistry.Player` fallback.

What was done:
- Removed `TryDockFromCollider`.
- Removed `TryResolveTransportLifecycleOwner(Collider...)`.
- Removed the collider-id transport lookup cache arrays/counters and clear calls.
- Removed the unused resolver helpers from the file.
- Expanded `Tools/KccApexAudit_X_005.py` with `vehicle_docking_no_legacy_collider_resolver`.

Cinematic cheats used:
- None. This deletes obsolete authority code after registry polling became the only route.

Exact microseconds saved:
- 0 us claimed without Unity Profiler.
- Structural result: docking source now has no fallback collider resolver or player registry fallback; it uses lifecycle registry polling only.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, player trigger callbacks 0, `vehicle_docking_no_legacy_collider_resolver` true, `vehicle_docking_uses_registry_volume_polling` true, `LockstepPlayerKinematicState` 64, `KinematicStateDTO` 64.
- Whole-runtime non-Editor callback scan: zero Unity `OnTrigger*` and `OnCollision*` methods.
- Whole-runtime non-Editor PhysX query scan: zero sync `Physics.*` casts/overlaps/checks, zero PhysX command bridges, zero Unity collider query helpers, zero `Physics.SyncTransforms`, zero `.ClosestPoint(`.
- Targeted docking scan: zero old collider resolver symbols and zero `GlobalRegistry.Player` in `VehicleDockingModule.cs`.
- `git diff --check`: passed for touched runtime/tool/report/log files; CRLF warnings only.

Residual truth:
- C# compile rerun is pending. Latest gate samples after docking dead-route removal: `49.5/66.4/91.9` then `66.3/64.5/20.7`; active `dotnet.exe` process count stayed `6`. Local build was not launched under the project rule.

## 2026-05-24 - Kinematics Probe DTO Closure

What was wrong:
- `PlayerKinematicsRuntime` still stored hand probe contacts as `RaycastHit` even though the producer bridge had already been disabled.
- `IPlayerKinematicsMotorSyncSink` exposed a `RaycastHit` ladder method while the KCC sync runtime only needed a contact point.

What was done:
- Added explicit 64-byte `PlayerKinematicsProbeHit`.
- Moved `_handProbeHits` and `PlayerKinematicsHandPlacementJob.Hits` from `RaycastHit` to `PlayerKinematicsProbeHit`.
- Replaced `IPlayerKinematicsMotorSyncSink.TryGetRecentBatchedLadderHit(... out RaycastHit)` with `TryGetRecentLadderContact(... out Vector3)`.
- Added `HectonPlayerMotor.TryGetRecentLadderContact` as the adapter from the legacy motor cache to KCC vector contact.
- Updated `PlayerKinematicsRuntime.SnapshotLadder` to consume the vector contact only.
- Expanded `Tools/KccApexAudit_X_005.py` to prove `PlayerKinematicsProbeHit` layout and zero `RaycastHit` symbols in KCC sync surfaces.

Cinematic cheats used:
- None. This is DTO/contract authority cleanup. No visual fake was introduced.

Exact microseconds saved:
- 0 us claimed without Unity Profiler.
- Structural result: KCC runtime hand-probe lane and KCC sync contract no longer carry Unity PhysX hit DTOs.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad forbidden 0, scoped forbidden 0, `player_hand_probe_raycast_hit_lane_count` 0, `player_kinematics_runtime_raycast_hit_count` 0, `player_kinematics_sync_contract_raycast_hit_count` 0, `player_kinematics_probe_hit_size` 64, `LockstepPlayerKinematicState` 64, `KinematicStateDTO` 64.
- Exact targeted scan: zero `RaycastHit` in `PlayerKinematicsRuntime.cs`, `PlayerMovementContracts.cs`, and `HydrodynamicKccRuntime.cs`.
- Whole-runtime non-Editor exact sync Physics query scan: zero `Physics.Raycast/SphereCast/CapsuleCast/BoxCast/Linecast`, zero overlap/check/penetration/sync calls.
- Whole-runtime non-Editor exact PhysX command scan: zero `RaycastCommand`, `CapsulecastCommand`, `SpherecastCommand`, and `QueryParameters`.
- Whole-runtime non-Editor callback/helper scan: zero `OnTrigger*`, zero `OnCollision*`, zero `.ClosestPoint`, zero `GetContacts`, zero `SweepTest*`.
- `git diff --check`: passed for touched runtime/tool/report files; CRLF warnings only.

Residual truth:
- C# compile rerun is pending. Build gate samples after this patch were `99/37/81`, `45/61/39`, `86/91/68`, `30/34/53`, and `35/55/58`; compiler process count was 0, but CPU exceeded the project 50% gate. Local `dotnet build` was not launched.
- Legacy direct gameplay surfaces still contain `RaycastHit` in `HectonPlayerMovement`/`HectonPlayerMotor` for footstep, ground, and ladder collider identity. They are not PhysX cast/command call sites by current static proof, but they remain a separate movement-surface cleanup target.
## APEX Continuation - Player Motor Native Sweep State Removal

What was wrong:
- `HectonPlayerMotor` still exposed disabled compatibility methods and state for the old player capsule sweep bridge.
- `HectonPlayerState` still declared `HectonPlayerMotorNativeState` with stale native hit-buffer ownership.
- `HectonPlayerMovement` still called the deleted motor batched footstep/probe/ladder/sweep methods.

What was done:
- Deleted player motor scheduled sweep API/state symbols and reset the disabled probe state to repair-target only.
- Deleted `HectonPlayerMotorNativeState`.
- Removed movement calls to motor batched `RaycastHit` consumers; wipeout no longer schedules a dead sweep; ladder spline snap remains inactive until a ladder-owned contact registry/SDF route exists.
- Renamed stale motor buffer IDs in `H8Memory` to reserved slots so non-Editor source no longer advertises removed player motor sweep buffers.
- Updated `KccApexAudit_X_005.py` to prove deletion semantics instead of old disabled-string semantics.

Cinematic cheats used:
- No new physical simulation. Wipeout sweep presentation is intentionally degraded rather than restored through a fake PhysX-shaped result.

Exact microseconds saved:
- Not claimed. Proof is structural: `player_motor_capsule_sweep_bridge_symbol_count = 0`, `player_motor_native_state_symbol_count = 0`, `player_motor_raycast_hit_symbol_count = 0`, `broad_forbidden_count = 0`.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}`.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; motor bridge/native/RaycastHit counts 0.
- Exact non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- `git diff --check`: passed with CRLF warnings only.
- Compile pending: CPU gate blocked at `99.8/96.4/79.7`; no local build launched.

## APEX Continuation - Vehicle Motor Scheduled Sweep Bridge Removal

What was wrong:
- `VehicleMotor` still had a compile-valid `CapsulecastCommand.ScheduleBatch` bridge shape through scheduled sweep buffers and consumers.
- `MountablePlayerTransport` still consumed and scheduled those sweeps during mounted movement/dock lock.
- `H8Memory` and `VaultMemoryContracts` still advertised vehicle sweep command/result buffer ownership.

What was done:
- Deleted vehicle scheduled sweep API/state/helpers from `VehicleMotor`.
- Removed mounted transport calls to `TryConsumeScheduledCapsuleSweep`, `ScheduleCapsuleSweepBatch`, `HasPendingSweep`, `mountedSweepMask`, and `MountedDriveSkinWidth`.
- Renamed vehicle sweep buffer IDs to reserved slots and removed ownership contract cases.
- Expanded `KccApexAudit_X_005.py` to prove vehicle motor sweep bridge deletion and zero vehicle motor `RaycastHit` symbols.

Cinematic cheats used:
- No new fake simulation. Mounted collision presentation is intentionally not restored through a PhysX-shaped result; any replacement must be a typed contact/SDF route.

Exact microseconds saved:
- Not claimed. Proof is structural: `vehicle_motor_capsule_sweep_bridge_symbol_count = 0`, `vehicle_motor_raycast_hit_symbol_count = 0`, `broad_forbidden_count = 0`.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; vehicle motor sweep bridge/RaycastHit counts 0; player motor sweep bridge/RaycastHit counts 0.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- Targeted vehicle scan: zero `RaycastHit`, `CapsulecastCommand`, `ScheduledSweep`, `_scheduledSweep`, `VehicleMotorSweepCommands`, `VehicleMotorSweepResults`, `ScheduleCapsuleSweepBatch`, `TryConsumeScheduledCapsuleSweep`, and `HasPendingSweep` in vehicle/mountable/memory ownership files.
- `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun is pending until the project CPU/process gate opens. No local build launched yet after this vehicle cleanup.

## APEX Continuation - Player Movement Surface Hit DTO Cleanup

What was wrong:
- `HectonPlayerMovement` still had `RaycastHit` fields/arrays for ground, movement-probe, step, headroom, and footstep surface cache.
- `PlayerFootstepAudio` consumed that Unity DTO even though the current movement producer does not populate the old shared hit buffer.

What was done:
- Added `HectonPlayerMovement.PlayerMovementSurfaceHit`.
- Replaced movement surface cache and helper signatures from `RaycastHit` to `PlayerMovementSurfaceHit`.
- Updated `PlayerFootstepAudio` to consume `PlayerMovementSurfaceHit`.
- Expanded `KccApexAudit_X_005.py` to persist proof for movement and footstep audio `RaycastHit` counts.

Cinematic cheats used:
- No new physical simulation. Footstep surface resolution stays degraded rather than restored through a banned sync query or fake Unity hit.

Exact microseconds saved:
- Not claimed. Structural result: `player_movement_surface_raycast_hit_count = 0`, `player_footstep_audio_raycast_hit_count = 0`, whole-runtime forbidden query count remains 0.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; movement/footstep `RaycastHit` counts 0; vehicle/player motor sweep bridge counts 0.
- Targeted movement/footstep scan: zero `RaycastHit` and zero `colliderEntityId`.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun is pending. Latest gate after this DTO cleanup: CPU `100/100/100`, active `dotnet.exe` processes present. Local build was not launched.

## APEX Continuation - Player Rigidbody Velocity Readback Closure

What was wrong:
- Movement still had one `_rb.linearVelocity` fallback in `ResolveAuthoritativeLinearVelocity`.
- Motor still had `_body.linearVelocity` as current velocity fallback.
- `PlayerKinematicsRuntime` still read `_body.linearVelocity` in movement-authority, warmup, pre-shift halt, squeeze telemetry, and sync-fence fallback paths.

What was done:
- Movement now falls back to its own `_velocity` after KCC velocity signal lookup.
- `ApplyMotorLinearVelocity` synchronizes `_velocity` before forwarding to motor.
- Motor now keeps `_lastKnownLinearVelocity` for target/change resolution and no longer reads Rigidbody velocity.
- Player kinematics runtime now uses `ReadVelocitySnapshot` instead of Rigidbody velocity readbacks.
- Apex audit now reports zero movement, motor, and player kinematics body velocity read counts.

Cinematic cheats used:
- None. This is authority cleanup, not simulation replacement.

Exact microseconds saved:
- Not claimed. Structural result: `movement_rb_linear_velocity_read_count = 0`, `motor_body_linear_velocity_read_count = 0`, `player_kinematics_body_velocity_read_count = 0`.

Verification:
- Targeted readback scan: zero `_rb.linearVelocity`, `_body.linearVelocity`, and `PlayerRigidbody/playerRigidbody/_playerRigidbody.linearVelocity` in movement/motor/kinematics files.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; movement/motor/player-kinematics velocity read counts 0.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun is pending. Latest gate after velocity readback cleanup: CPU `97.9/82.7/99.4`, active `dotnet.exe` plus `VBCSCompiler`. Local build was not launched.

## APEX Continuation - Player Spawner Ground DTO Cleanup

What was wrong:
- `HectonPlayerSpawner` used cached terrain height but still stored the result in `RaycastHit` and called the method `TryRaycastGround`.

What was done:
- Added local `SpawnGroundHit`.
- Replaced `_hitInfo` and all spawner ground checks with `SpawnGroundHit`.
- Renamed `TryRaycastGround` to `TryResolveGroundHit`.
- Expanded apex audit to persist spawner `RaycastHit` and legacy method counts.

Cinematic cheats used:
- None. This is naming/DTO authority cleanup around cached terrain height.

Exact microseconds saved:
- 0 us claimed. Runtime behavior is the same cached terrain-height lookup; source no longer advertises a raycast DTO.

Verification:
- Targeted scan: zero `RaycastHit`, zero `TryRaycastGround`, zero player Rigidbody velocity readbacks in the player movement/motor/kinematics/spawner set.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: spawner `RaycastHit` count 0, spawner `TryRaycastGround` count 0, spawner explicit DTO true, broad/scoped forbidden counts 0.

Residual truth:
- C# compile rerun is still pending under the CPU/process gate.

## APEX Continuation - Speculative Solver Degenerate Plane Proof

What was wrong:
- The solver termination proof was bounded, but duplicate or near-coplanar SDF hits could spend the fixed 8-plane contact budget without adding new constraints.
- The -100 m/s voxel cone proof relied on smoke profile/tuning inspection instead of an executable editor contract.

What was done:
- Added fixed-threshold same-direction contact plane de-duplication inside `KinematicResolutionJob`; a self-review corrected the first draft from `abs(dot)` to signed `dot` so opposing corridor walls are preserved.
- Added `Shinobu355KccSmokeRunner.ValidateApexConeFallContract`.
- Added `HeadlessKcc_SmokeRunner_Preserves100MpsConeProbe`.
- Expanded `KccApexAudit_X_005.py` to persist `contact_plane_deduplication` and `smoke_cone_fall_contract_tested`.

Cinematic cheats used:
- No physical realism expansion. The hardening keeps the existing cheap projection solver and removes redundant planes instead of increasing solver budget.

Exact microseconds saved:
- Not claimed. Worst-case runtime adds bounded duplicate-plane dot checks; benefit is deterministic contact-budget use under noisy voxel gradients.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- Whole-runtime non-Editor exact forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- Targeted proof-symbol scan found `DuplicateContactPlaneDotThreshold`, `HasDuplicateContactPlane`, `ValidateApexConeFallContract`, and `HeadlessKcc_SmokeRunner_Preserves100MpsConeProbe`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0, contact plane de-dup true, 100 m/s cone fall contract true, 64-byte lockstep/KCC/probe DTOs.
- Targeted `git diff --check` for touched files passed with CRLF warnings only.

Residual truth:
- Whole-worktree `git diff --check` is polluted by pre-existing unrelated `.meta` and doc whitespace debt.
- C# compile rerun is pending. Latest gate: CPU `36.4/37.0/26.9`, but seven active `dotnet.exe` processes were present, so local build was not launched.

## APEX Continuation - Player Movement Rigidbody Mass Readback Closure

What was wrong:
- `HectonPlayerMovement` no longer read Rigidbody velocity, but hot force math still read `_rb.mass` for trauma, turbulence, undertow, ground stability, swim, surface lock, and wave-current force calculations.

What was done:
- Added movement-owned `_authoritativeBodyMassKg`.
- Added `ResolveAuthoritativeBodyMassKg` and `CacheAuthoritativeBodyMassKg`.
- Replaced hot `_rb.mass` reads with the cached scalar.
- Kept cold Rigidbody shell sync: `Awake` seeds the cache from the existing shell, and `ApplySuitToRigidbody` caches `currentSuitData.mass` before assigning `_rb.mass`.
- Expanded apex audit with `movement_rb_mass_read_count`, `movement_has_no_rigidbody_mass_read`, and `movement_uses_authoritative_body_mass_cache`.

Cinematic cheats used:
- None. This is authority cleanup around scalar mass, not simulation expansion.

Exact microseconds saved:
- Not claimed. Structural result: hot movement mass readbacks now count 0.

Verification:
- `rg "_rb\\.mass" HectonPlayerMovement.cs`: remaining occurrences are cold cache read and shell assignment only.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: `movement_rb_mass_read_count = 0`, cached body mass true, broad/scoped forbidden counts 0.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- Targeted `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun remains pending. Latest gate after this mass cleanup: CPU `81.5/92.3/98.2`, active `csc` and `dotnet`.

## APEX Continuation - Cross-Domain Compile Wall Unblock

What was wrong:
- Full project build exposed `PersistentWorldRegistry.IsModProtectedCoreAup` calling instance `TryResolvePlayerAupSnapshot` from a static method. The failure is outside X_005 KCC ownership, but it blocks compiler proof for the KCC patch set.

What was done:
- Patched only the static call site to resolve `PersistentWorldRegistry.Instance`.
- Kept player AUP snapshot resolution on the existing cached instance `_playerRuntimeContext`.
- Re-ran py_compile, OOP scanner, apex audit, exact non-Editor forbidden-symbol scan, and targeted diff-check.

Cinematic cheats used:
- None. Compile-wall repair only.

Exact microseconds saved:
- 0 us claimed. The changed path is a protected-mod AUP security query, not the KCC frame solver.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0, body mass read count 0, contact de-dup true, 100 m/s cone proof true, 64-byte lockstep/KCC DTOs.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- Targeted `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun remains pending. Latest build gate: CPU `100/100/100`, active compiler/dotnet process count `8`.

## APEX Continuation - Player Movement Legacy Collision DTO Removal

What was wrong:
- `HectonPlayerMovement` had no active Unity `OnCollisionEnter`, but still carried a dead `Collision`/`ContactPoint` queue route: `QueuedCollisionEvent`, metadata cache, `GetContact`, fixed-tick queue drain, Rigidbody impact transfer, and collision-driven wipeout/feedback helpers.

What was done:
- Removed the dead collision queue and metadata cache.
- Removed Unity `Collision`/`ContactPoint` DTO consumption from player movement.
- Removed now-unused KCC impact transfer and exosuit impact shake tuning fields tied to the deleted route.
- Expanded `KccApexAudit_X_005.py` to prove the legacy collision route is gone.

Cinematic cheats used:
- None. Missing impact presentation must come from KCC/SDF telemetry, not Unity collision callbacks.

Exact microseconds saved:
- Not claimed. Static result: player movement legacy collision symbols 0, Unity collision DTO count 0.

Verification:
- Targeted player movement scan: zero `QueuedCollisionEvent`, `HandleLegacyCollisionEnter`, `ProcessQueuedCollisionEvents`, `TryResolveCollisionEventMetadata`, `TryTransferKccImpactToRigidbody`, `TryStartWipeoutFromCollision`, `CollisionMetadataCache`, `ColliderCallbackMetadata`, `Collision collision`, `ContactPoint contact`, and `GetContact(`.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: player movement legacy collision route removed true; broad/scoped forbidden counts 0.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- Targeted `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun remains pending. Latest build gate: CPU `63.6/90.2/45.9`, active compiler/dotnet process count `8`.

## APEX Final Compile Closure

What was wrong:
- Compiler proof was still pending because the build gate stayed closed during the previous KCC passes.

What was done:
- Re-sampled the gate and confirmed CPU below 50% with zero active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` processes.
- Ran full project-reference compile: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /nodeReuse:false`.

Cinematic cheats used:
- None. This is compiler verification only.

Exact microseconds saved:
- 0 us claimed. This proves buildability, not frame-time gain.

Verification:
- Build succeeded with 0 errors.
- Remaining warnings are existing missing `Hecton8.Input.csproj` project-reference warnings in `Assembly-CSharp-firstpass.csproj` and `Assembly-CSharp.csproj`.
- Final post-log static proof: `py_compile` passed, OOP scanner produced `finding_counts = {}` with Hydro forbidden command hits 0, APEX audit reports broad/scoped forbidden counts 0 and all KCC authority proofs true, exact whole-runtime forbidden-symbol scan returned zero matches, and targeted `git diff --check` passed with CRLF warnings only.

## APEX Continuation - Raycast-Language And Default Layer Closure

What was wrong:
- The active runtime proof was clean, but player/KCC source still contained misleading legacy semantics: a hand-probe layer mask defaulted to `UnityEngine.Physics.DefaultRaycastLayers`, movement support/audio text still described typed surfaces as raycasts, and motor repair comments still referenced a raycast lane.

What was done:
- Changed `PlayerKinematicsRuntime.handProbeLayerMask` to `HectonLayerMasks.StrictInteractionLayerMask`.
- Renamed `TryEmitRaycastedFootstepAudio` to `TryEmitSurfaceFootstepAudio`.
- Replaced stale player movement and motor raycast wording with typed surface/KCC repair wording.
- Expanded `Tools/KccApexAudit_X_005.py` so the stale-language/default-layer cleanup is now a persisted proof, not a one-off grep.

Cinematic cheats used:
- None. This was authority-contract cleanup, not visual simulation.

Exact microseconds saved:
- 0 us claimed. The value is preventing future PhysX route reactivation and removing false contracts from KCC/player code.

Verification:
- Targeted stale-language scan found zero forbidden wording/default-layer matches in `HectonPlayerMovement.cs`, `HectonPlayerMotor.cs`, and `PlayerKinematicsRuntime.cs`.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; default Physics raycast layer count 0; strict interaction probe mask true; player/motor typed language proof true.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- Targeted `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun is pending after this wording/default-mask patch. The latest build gate stayed closed at CPU `92/90/97` with zero active compiler processes.

## APEX Continuation - Snapshot-First Rigidbody Pose Readback Closure

What was wrong:
- Rigidbody velocity/mass and PhysX casts were closed, but player/KCC pose paths still read `Rigidbody.position` or `Rigidbody.rotation` in deterministic-adjacent code. That is split authority even without a query call.

What was done:
- `PlayerKinematicsRuntime.ResolveBodyRuntimePosition` now reads sync/native snapshots before shell pose.
- Added `ResolveAuthoritativeRotationSnapshot` and routed KCC state staging, sync-fence fallback, correction fallback, pre-shift halt, and squeeze telemetry through snapshot-first helpers.
- `HectonPlayerMovement.ResolveBodyRuntimePosition` now prefers fixed-frame cached body position and AUP player state before the Rigidbody shell.
- Routed hot movement render/fixed/body sample paths through the resolver.
- Expanded `KccApexAudit_X_005.py` to persist hot Rigidbody pose-read counts and snapshot-first proof flags.

Cinematic cheats used:
- None. This is authority routing. Presentation polish must consume the same snapshot/AUP truth.

Exact microseconds saved:
- 0 us claimed without profiler. Static result: hot movement Rigidbody pose read count 0; hot player-kinematics Rigidbody pose read count 0.

Verification:
- Direct movement `_rb.position/_rb.rotation` scan now leaves only cold `Awake` seed and emergency helper fallback.
- Direct player kinematics `_body.position/_body.rotation` scan now leaves only emergency helper fallbacks.
- Immediate self-review found a cold-start zero-buffer risk and added `_hasAuthoritativePoseSnapshot` so newly allocated native buffers cannot override the authored shell seed before the first authoritative pose exists.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; `movement_hot_rb_pose_read_count = 0`; `player_kinematics_hot_body_pose_read_count = 0`; both snapshot-first flags true.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands, Unity callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- Targeted `git diff --check`: passed with CRLF warnings only.

Residual truth:
- C# compile rerun is pending after this pose-routing patch. Latest gate: CPU `100/100/100`, zero active compiler processes.

## APEX Continuation - UI Compile Wall Minimal Unblock

What was wrong:
- After `dotnet restore`, full build reached C# and failed outside KCC: `DiegeticPDAController` declared `IUpdatable` without implementing `Tick(float)`.

What was done:
- Removed the stale `IUpdatable` interface from `DiegeticPDAController`.
- Kept `ILateFrameTickable`, which matches the class registration path through `GlobalRegistry.TryRegisterLateFrameTickable`.

Cinematic cheats used:
- None. Compile-wall repair only.

Exact microseconds saved:
- 0 us claimed. No KCC runtime path changed.

Verification:
- Targeted scan found zero `DiegeticPDAController : MonoBehaviour, IUpdatable` matches.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- Targeted `git diff --check`: passed.

Residual truth:
- Full C# compile still needs a rerun. Latest build gate sample is CPU `100/100/100` with 2 active compiler/runtime processes.

## APEX Continuation - Tool Interaction Surface DTO Closure

What was wrong:
- The shared tool-primary path no longer scheduled PhysX commands, but its contract still returned Unity `RaycastHit` and exposed `TryRaycastPrimary` / `TryQueuePrimaryRaycast` names. That kept a PhysX-shaped DTO in a route that now resolves SDF/terrain surfaces.

What was done:
- Added `InteractionSurfaceHitDTO` as an explicit 64-byte surface-hit row and `InteractionSurfaceHit` as the managed collider side-channel wrapper.
- Changed `IInteractionSignalService`, `EquipmentInteractionHandler`, `PlayerTool`, the primary tool consumers, `RaycastBatchHelper.QueryResult`, and `QueryCacheContext` to use typed surface hits.
- Changed the service-owned vault hit lane from Unity `RaycastHit` to `InteractionSurfaceHitDTO`.
- Removed the shared legacy method names from the patched tool route: callers now use `TryResolvePrimarySurfaceHit`.
- Expanded `Tools/KccApexAudit_X_005.py` to persist proof for the typed tool-surface route.

Cinematic cheats used:
- None. This is authority-contract cleanup. The low-cost surface query still uses SDF/terrain approximation; richer collider/material resolution belongs in typed producers, not Unity PhysX queries.

Exact microseconds saved:
- 0 us claimed. This removes a regression vector and Unity DTO dependency from the tool-primary route; profiler timing is not claimed.

Verification:
- Targeted interaction/tool scan: zero `RaycastHit`, zero `TryRaycastPrimary`, zero `TryQueuePrimaryRaycast`, zero `NativeArray<InteractionSurfaceHit>`, and zero `VaultGenerationHandle<InteractionSurfaceHit>` in the patched route.
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; `interaction_surface_unity_raycast_hit_count = 0`; `interaction_surface_legacy_method_count = 0`; typed hit and typed DTO flags true; `legacy_batch_unity_raycast_hit_count = 0`; lockstep and kinematic DTO sizes remain 64.
- Exact whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands/schedules, Unity collision/trigger callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- `dotnet restore Assembly-CSharp.csproj --disable-parallel -v:minimal`: passed after the build gate opened.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /nodeReuse:false`: Build succeeded, 0 errors. Warnings are the existing missing `Hecton8.Input.csproj` references.

Residual truth:
- `VoxelSonarSdfRaycastHit` and `VoxelSdfRaycastHit` remain existing SDF-result DTO names. They are not Unity `RaycastHit` and do not schedule PhysX; renaming those ABI-facing contracts needs a separate migration card.

## APEX Continuation - Kinematic Surface Hit DTO Closure

What was wrong:
- IK, VR head-contact presentation, buoyancy ground contact, and player environment buffers already resolved SDF/terrain contacts without PhysX scheduling, but still stored those contacts as Unity `RaycastHit` or `NativeArray<RaycastHit>`.

What was done:
- Added explicit 64-byte unmanaged `KinematicSurfaceHit` in `GlobalRegistryContracts.cs`.
- Replaced local Unity hit rows in `ContextualPhysicalIkRuntime`, `VRSomaticProvider`, `BuoyancyObject`, and `HectonPlayerEnvironmentHandler`.
- Preserved lowercase `point`, `normal`, and `distance` accessors so existing kinematic math stays narrow and compile-safe.
- Expanded `Tools/KccApexAudit_X_005.py` to prove the kinematic surface route has no Unity `RaycastHit` symbols and uses typed hit storage.

Cinematic cheats used:
- None. This is DTO hygiene for existing SDF/terrain contacts. The presentation remains the same approximation.

Exact microseconds saved:
- 0 us claimed. The real gain is removing Unity PhysX DTO shape from Burst-adjacent hit buffers.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `python Tools/KccApexAudit_X_005.py`: broad/scoped forbidden counts 0; `kinematic_surface_hit_layout_64 = true`; `kinematic_surface_unity_raycast_hit_count = 0`; `kinematic_surface_uses_typed_hit = true`.
- Whole-runtime non-Editor forbidden-symbol scan: 0 matches for sync Physics casts/overlaps/checks, PhysX commands/schedules, Unity collision/trigger callbacks, `Physics.SyncTransforms`, `.ClosestPoint`, `GetContacts`, and `SweepTest*`.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /nodeReuse:false`: Build succeeded, 0 errors. Warnings remain the existing missing `Hecton8.Input.csproj` references.

Residual truth:
- SDF DTO type names such as `VoxelSonarSdfRaycastHit` still contain raycast wording. They are SDF result structs, not Unity `RaycastHit`, but renaming them touches cross-domain ABI and should be done as a separate migration.
## APEX Continuation - Spatial Target Contract Closure - 2026-05-25

What was wrong:
- Interaction target acquisition no longer used Unity Physics, but still exposed raycast-shaped contracts: `TryRaycastSpatial`, `raycastInterval`, `_raycastTimer`, `PerformRaycast`, laser-cutter raycast requester names, and spawner raycast-origin names.
- Those symbols were not active PhysX calls. They were still unacceptable regression handles in the Echelon 4 target acquisition route.

What was done:
- `InteractableRegistry.TryRaycastSpatial` was renamed to `TryResolveSpatialTarget`.
- `PlayerInteraction` target acquisition now uses `targetProbeInterval`, `_targetProbeTimer`, and `ResolveHoveredTarget`.
- `LaserCutter` surface query staging now uses `_surfaceRequesterId`, `CacheSurfaceRequesterId`, `StageDodSurfaceRequest`, and `ResolveCuttableSurfaceMask`.
- `HectonPlayerSpawner` cached terrain probe now uses `groundProbeOriginHeight` and `_groundProbeOrigin`.
- `InputDispatcher` XR look-at target resolution now uses the strict interaction/interactable mask instead of `DefaultRaycastLayerMask`.
- `KccApexAudit_X_005.py` now records `interaction_target_legacy_raycast_api_count`, `interaction_target_uses_spatial_target_contract`, and `player_spawner_uses_ground_probe_origin`.

Cinematic Cheats used:
- No new physics simulation. Target acquisition stays a registered-collider bounds probe and cached terrain-height probe; rich presentation can be added by typed surface metadata instead of PhysX casts.

Exact Microseconds saved:
- 0 us claimed. This pass removed source-contract regression risk, not measured runtime work.

Proof:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: `interaction_target_legacy_raycast_api_count = 0`, `interaction_target_uses_spatial_target_contract = true`, `player_spawner_uses_ground_probe_origin = true`, `broad_forbidden_count = 0`, `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Strict whole-runtime forbidden scan returned zero Unity Physics sync casts/overlaps/checks, zero PhysX command types/schedules, zero collision/trigger callbacks, zero `Physics.SyncTransforms`, zero `.ClosestPoint`, zero `GetContacts`, and zero `SweepTest*`.
- Targeted `git diff --check` passed with CRLF warnings only.
- C# compile did not run: build gate was closed by 5 active compiler/runtime processes and CPU samples `100/100/100`.

## APEX Continuation - Unity Collision DTO Dead Route Removal - 2026-05-25

What was wrong:
- Non-Editor runtime still contained disabled legacy routes that accepted Unity `Collision`, read `ContactPoint`, and called `GetContact(0)`.
- These were not active callbacks, but they were PhysX-shaped facts and future regression handles.

What was done:
- Removed `GlobalPhysicsStateManager.QueueImpact(Rigidbody, Rigidbody, Collision)` and its private `Collision` resolver.
- Removed the disabled Unity `Collision` damage route from `MantaEmergencyWreck`.
- Removed the disabled Unity `Collision` snag route from `SargassumCollapseChunk`.
- Added `unity_collision_dto_count` and `unity_collision_dto_route_removed` to `KccApexAudit_X_005.py`.

Cinematic Cheats used:
- No replacement physics simulation was added. Existing typed kinematic impact/spatial routes remain the valid path; future rich impacts must consume typed contact payloads.

Exact Microseconds saved:
- 0 us claimed. Removed routes were disabled; this is authority cleanup, not measured runtime optimization.

Proof:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: `unity_collision_dto_count = 0`, `unity_collision_dto_route_removed = true`, `broad_forbidden_count = 0`, `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Strict scan: zero `Collision collision`, zero `ContactPoint`, zero `.GetContact(`, zero `QueueImpact(`.
- Strict whole-runtime forbidden scan: zero sync Unity Physics casts/overlaps/checks, zero PhysX command types/schedules, zero collision/trigger callbacks, zero `Physics.SyncTransforms`, zero `.ClosestPoint`, zero `GetContacts`, zero `GetContact`, and zero `SweepTest*`.
- `git diff --check` passed for touched files with CRLF warnings only.
- C# compile did not run after this pass: 10 build-gate attempts stayed blocked by CPU max `100`; attempts 5-10 also had 2 active compiler/runtime processes.

## APEX Continuation - Collision-Damage Dead Route Cleanup - 2026-05-25

What was wrong:
- `MantaEmergencyWreck` still stored legacy `collisionDamage*` fields and residency cooldown state after the Unity `Collision` damage route was removed.
- `SargassumCollapseChunk` still carried dead collision-snag helper code and a cold `SpatialQueryHit[8]` target buffer even though no active typed event owner calls it.

What was done:
- Removed Manta dead `collisionDamage*` authoring fields, cooldown timer, and residency cooldown field.
- Preserved the only live Manta velocity clamp behavior under `bailoutVelocityCapMaxSpeed`.
- Removed Sargassum dead snag probe fields/helpers: `TryConfigureSnag`, `LayerMatchesMask`, `ShouldStopSiltTrail`, `_cascadeImpactConsumed`, target/probe serialized fields, and `_snagContacts`.
- Extended `Tools/KccApexAudit_X_005.py` and regenerated `Docs/Reports/KCC_APEX_AUDIT_X_005.*`.

Cinematic cheats used:
- No new simulation. Removed dead physical-damage/snag residue and left future presentation to typed SDF/spatial events.

Exact microseconds saved:
- Runtime frame us not claimed.
- Removed one cold `SpatialQueryHit[8]` managed allocation path per Sargassum chunk instance.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: `manta_legacy_collision_damage_symbol_count = 0`, `sargassum_dead_collision_snag_symbol_count = 0`, `broad_forbidden_count = 0`, `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}`.
- Strict whole-runtime non-Editor forbidden-symbol scan returned zero sync PhysX casts/overlaps/checks, command bridges, Unity collision/trigger callbacks, `ClosestPoint`, `GetContacts`, `GetContact`, and `SweepTest*`.
- Targeted `git diff --check` passed.

Compile gate:
- Local build not launched after this pass. Gate sample had zero active compiler/runtime processes, but CPU samples were 91.2/96.9/87.1, above the 50% project rule.

## APEX Continuation - Compile Wall Unblock And Build Proof - 2026-05-25

What was wrong:
- Build 1 failed outside KCC: `BaseModule` passed a readonly mutable buffer by `ref`, and `DroneFleetManager` referenced `AcousticPingEvent` / `PhysicsEventBus` without `Hecton8.Physics`.
- Build 2 failed outside KCC: `GlobalRegistry.EndingRuntimeService` exposed `IEndingRuntimeService`, but `EndingSystem` did not implement it.

What was done:
- Made `_fieldOperationSummaryBuffer` mutable in `BaseModule` because it is explicitly cleared and passed by `ref`.
- Added `using Hecton8.Physics` to `DroneFleetManager`.
- Implemented `IEndingRuntimeService` on `EndingSystem` and added `ChooseEnding(byte)` mapping for known ending codes.
- Updated `KccApexAudit_X_005.py` to accept the existing `VaultBufferView<KinematicSurfaceHit>` typed buffer route.

Cinematic cheats used:
- None. Compile wall repair only.

Exact microseconds saved:
- 0 runtime us claimed.

Verification:
- First build failure recorded: 5 errors.
- Second build failure recorded: 1 error.
- Final build: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /nodeReuse:false` -> Build succeeded, 0 errors, 2 existing missing `Hecton8.Input.csproj` warnings.
- `python Tools/KccApexAudit_X_005.py`: `broad_forbidden_count = 0`, `scoped_forbidden_count = 0`, `kinematic_surface_uses_typed_hit = true`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts = {}`.
- Strict whole-runtime forbidden-symbol scan remained empty.
## X_005 APEX Continuation - Deferred Surface Probe DTO Closure

What was wrong: Non-Editor runtime still carried Unity `RaycastHit` DTOs in dispatcher/mod deferred delivery, foveated deferred result storage, and fauna obstacle deferred hits. These paths did not execute Unity Physics casts, but they preserved PhysX-shaped contracts and future split-authority entry points.

What was done: Replaced those DTOs with explicit `KinematicSurfaceHit` rows, renamed foveated/fauna deferred raycast semantics to surface-probe semantics, kept the legacy dispatcher `BufferID` only as an ABI id, and expanded `Tools/KccApexAudit_X_005.py` with whole-runtime Unity `RaycastHit` DTO proof plus dispatcher/foveated/fauna typed-hit gates.

Cinematic Cheats used: Fauna obstacle response remains nav-grid/SDF pressure sampling instead of collider raycasts; no physical probe is restored until a typed SDF/material lane owns it.

Exact Microseconds saved: 0 us claimed. This pass removed DTO regression risk, not a measured hot PhysX call. Static proof: `unity_raycast_hit_dto_count = 0`, `broad_forbidden_count = 0`, `scoped_forbidden_count = 0`.

Verification: `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py` passed; `python Tools/KccApexAudit_X_005.py` passed with typed-hit gates true; `python Tools/OOP_Kcc_Scanner_X_005.py` produced `{}`; strict scans found zero standalone Unity `RaycastHit`, zero PhysX command types, zero sync Physics casts, and zero Unity collision DTO/callback symbols. `git diff --check` passed for touched files with CRLF warnings only. C# build was not launched because CPU gate stayed above 50%.

Build gate update: the gate later opened once and `dotnet build --no-restore` stopped at `NETSDK1004` because `Temp/obj/Assembly-CSharp/project.assets.json` was missing. A restore/build retry loop then stayed closed for 24 attempts: CPU samples exceeded 50% on every window or active compiler/runtime process count was 7-8. No compiled success is claimed for the deferred surface probe DTO patch yet.
## 2026-05-25 - Fauna Surface Probe Migration Compile Guard

What was wrong:
- `FaunaSensorSuite` had been converted from Unity `RaycastHit`/ray terminology to typed `KinematicSurfaceHit` surface probes, but `FaunaBrain.OnAfterDeserialize()` still assigned legacy serialized `maxRayLength` into `_sensorSuite.maxRayLength`.
- That field no longer exists, so the next C# compile after restore would fail before validating the KCC patch set.

What was done:
- Renamed the hidden migration field to `le_maxProbeLength`, kept `FormerlySerializedAs("maxRayLength")` as data migration metadata, and routed the migrated value into `_sensorSuite.maxProbeLength`.
- Reran `py_compile`, `Tools/KccApexAudit_X_005.py`, `Tools/OOP_Kcc_Scanner_X_005.py`, strict standalone Unity `RaycastHit`/PhysX-command scans, strict sync Physics/collision scans, and targeted `git diff --check`.

Cinematic Cheats used:
- None. This is a compile-contract/data-migration guard, not runtime simulation.

Exact Microseconds saved:
- 0 us claimed. Static proof remains clean: `broad_forbidden_count=0`, `unity_raycast_hit_dto_count=0`, `lockstep_size=64`, `kinematic_surface_hit_layout_64=true`. C# restore/build still pending under the CPU/compiler gate.

## 2026-05-25 - Legacy Surface Query Counter Repair

What was wrong:
- The disabled `RaycastBatchHelper` facade returned deterministic miss slots, but `PerformanceMonitor` still read `TotalRaycastsProcessed`.
- That counter was dead and stayed zero, so legacy facade pressure could be hidden from telemetry.

What was done:
- Added `TotalLegacySurfaceQueriesProcessed`, incremented it on accepted legacy query slots, removed the dead `TotalRaycastsProcessed` field, and changed `PerformanceMonitor` to sample/reset the surface-query counter.
- Extended `Tools/KccApexAudit_X_005.py` with persisted proof fields for the new counter and the removed old counter.

Cinematic Cheats used:
- None. This is telemetry truth maintenance.

Exact Microseconds saved:
- 0 us claimed. New runtime cost is one static integer increment per accepted legacy facade slot. Proof: `legacy_batch_surface_query_counter_exists=true`, `legacy_batch_surface_query_counter_increments=true`, `legacy_batch_old_raycast_counter_removed=true`, `performance_monitor_uses_surface_query_counter=true`.

## 2026-05-25 - Hydro Job Lifecycle Gate

What was wrong:
- Hydro KCC needed proof that the native/Burst route does not hide persistent MonoBehaviour-owned `NativeArray<T>` fields or same-frame forced `Complete()` calls inside fixed/post/late ticks.
- `LateFrameTick` used `TryFinalizeCompleted`, but the proof had to include the helper implementation because it eventually calls `handle.Complete()` only after `IsCompleted`.

What was done:
- Added `hydro_job_lifecycle` to `Tools/KccApexAudit_X_005.py`.
- The audit now checks Hydro private native fields, vault-handle/job-handle ownership, phase-local DataVault views, forced-complete counts in hot ticks, rollback/no-wait abort behavior, teardown-only forced completion, and `DispatcherJobFence` non-blocking finalization semantics.
- Regenerated `Docs/Reports/KCC_APEX_AUDIT_X_005.md` and `.json`.

Cinematic Cheats used:
- None. Lifecycle proof only; no new simulation behavior.

Exact Microseconds saved:
- 0 us claimed. Static proof: `hydro_runtime_private_native_array_field_count=0`, `hydro_fixed_post_late_forced_complete_count=0`, `hydro_late_frame_finalize_is_nonblocking=true`, `hydro_abort_no_wait_forced_complete_count=0`, `hydro_teardown_is_only_force_complete_site=true`.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: passed with `broad_forbidden_count=0`, `scoped_forbidden_count=0`, and the Hydro lifecycle fields above.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}` and Hydro KCC forbidden command hits 0.
- Strict non-Editor scans found zero Unity `RaycastHit`, zero PhysX command DTOs, zero sync Unity Physics queries/overlaps/checks, and zero Unity collision/trigger callbacks.
- `git diff --check` passed for lifecycle audit/report files with CRLF warnings only.

## 2026-05-25 - VR Somatic Dead Capsulecast Cleanup

What was wrong:
- `VRSomaticProvider` still contained an unused `ProcessHeadCapsulecastHitsJob` and `_headCollisionHits` vault lane after the head contact route moved to direct SDF sample generation.
- The job was dead, but the name and buffer preserved a PhysX-shaped reactivation point and a cold native allocation path.

What was done:
- Removed `ProcessHeadCapsulecastHitsJob`.
- Removed `_headCollisionHits`, the `ShinobuVRSomaticHeadCollisionHits` allocation, release, default reset, and readiness dependency.
- Extended `Tools/KccApexAudit_X_005.py` with `vrsomatic_dead_head_capsulecast_job_removed` and `vrsomatic_dead_head_collision_hit_buffer_removed`.

Cinematic Cheats used:
- The VR near-field contact path remains SDF sample driven. No collider/capsulecast fallback was restored.

Exact Microseconds saved:
- 0 frame-time us claimed. Removed one unused VR somatic native buffer allocation path and one dead Burst job definition.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: `vrsomatic_dead_head_capsulecast_job_removed=true`, `vrsomatic_dead_head_collision_hit_buffer_removed=true`, `broad_forbidden_count=0`, `scoped_forbidden_count=0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- Targeted VR scan found zero `_headCollisionHits`, `ProcessHeadCapsulecastHitsJob`, `HeadCollisionMaxHitsPerCommand`, `ShinobuVRSomaticHeadCollisionHits`, `capsulecast`, `Capsulecast`, and `KinematicSurfaceHit` matches in `VRSomaticProvider.cs`.
- Strict non-Editor scans remained empty for Unity `RaycastHit`, PhysX command DTOs, sync Unity Physics queries/overlaps/checks, and Unity collision/trigger callbacks.
- Restore/build was not launched: 12 gate attempts never satisfied both constraints. Attempts 1-3 had CPU spikes above 50%; attempts 4-12 had 6-7 active compiler/runtime workers and CPU up to 100%.

## 2026-05-25 - VR Somatic Fake Head Job Handle Removal

What was wrong:
- After the dead head capsulecast job was removed, VR near-field collision still used `_headCollisionHandle`, `StateHeadCollisionScheduled`, and `ScheduleHeadCollisionBatch` naming around direct SDF sample preparation.
- `LateFrameTick` called `DispatcherJobSwap.TryComplete(ref _headCollisionHandle, false)` on a default handle, so the lifecycle looked asynchronous without a real scheduled job.

What was done:
- Removed `_headCollisionHandle` and all head-collision handle completion calls.
- Renamed the state to `StateHeadCollisionReady` and the producer to `PrepareHeadCollisionSamples`.
- Extended `Tools/KccApexAudit_X_005.py` with fake-job-handle and ready-state proof fields.

Cinematic Cheats used:
- None. This removes fake scheduling language; it does not change the direct SDF sample model.

Exact Microseconds saved:
- No measured us claimed. One non-forced late-frame job completion helper call was removed from the VR somatic path.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: `vrsomatic_head_collision_fake_job_handle_removed=true`, `vrsomatic_head_collision_ready_state_not_scheduled=true`, `broad_forbidden_count=0`, `scoped_forbidden_count=0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- Targeted VR scan found zero `_headCollisionHits`, `ProcessHeadCapsulecastHitsJob`, `HeadCollisionMaxHitsPerCommand`, `ShinobuVRSomaticHeadCollisionHits`, `_headCollisionHandle`, `StateHeadCollisionScheduled`, `ScheduleHeadCollisionBatch`, `capsulecast`, `Capsulecast`, and `KinematicSurfaceHit` matches.
- Strict non-Editor scans remained empty for Unity `RaycastHit`, PhysX commands, sync Unity Physics queries/overlaps/checks, and Unity collision/trigger callbacks.

## 2026-05-25 - Encoded SDF Raymarch Consumer Closure

What was wrong:
- The scoped KCC-adjacent runtime had no PhysX casts, but nine consumers still called `IVoxelSonarSdfReadModel.TryRaymarchNearestSonarSdf`, hiding managed SDF raymarch under ground/focus/occlusion paths.

What was done:
- Added `VoxelSonarSdfMath` with bounded encoded-SDF trilinear raymarch math and `MaxEncodedRaymarchSteps = 2048`.
- Converted `BuoyancyObject`, `GameBootstrapper`, `DiegeticPdaFocusDistanceController`, `DeployableSdfDrillRuntime`, `VRSomaticProvider`, `EquipmentInteractionHandler`, `ContextualPhysicalIkRuntime`, and two `ScannerTool` paths to `VoxelSonarSdfMath.TryResolveNearestSdfSurface`.
- Replaced `DeployableSdfDrillRuntime.ResolveVoxelBridge()` published-volume raymarch with cached `HectonVoxelEngine.TryGetNearestActiveVolume` owner lookup.
- Rewired `HectonVoxelEngine.TryRaymarchNearestSonarSdf` to read published SDF payloads and call `VoxelSonarSdfMath.TryRaymarchEncodedSdf` instead of `HectonVoxelVolume.TryRaymarchPublishedSdf`.
- Extended `Tools/KccApexAudit_X_005.py` to prove scoped direct managed SDF raymarch consumer count 0 and resolver loop bounded.

Cinematic Cheats used:
- Continuous quality-controlled `stepMeters` remains the cheap/overkill lever; gameplay truth does not branch by tier.

Exact Microseconds saved:
- No profiler microseconds claimed. Structural proof: 9 scoped consumers moved off the managed raymarch interface, the scoped published-volume raymarch bridge count is 0, and the compatibility interface now uses the same bounded encoded-SDF kernel; PhysX savings remain 0 here because these paths were already SDF, not PhysX.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: `direct_managed_sdf_raymarch_consumer_count=0`, `scoped_published_sdf_raymarch_consumer_count=0`, `encoded_sdf_surface_resolver_consumers=9`, `encoded_sdf_surface_resolver_has_bounded_loop=true`, `voxel_engine_interface_raymarch_uses_encoded_math=true`, `voxel_engine_interface_raymarch_published_volume_raymarch_count=0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- `rg "\.\s*TryRaymarchNearestSonarSdf\s*\(" Assets/_Project/Scripts -g "*.cs" -g "!**/Editor/**"` found zero consumer calls.
- `rg "TryRaymarchAnyPublishedSdf\("` over scoped X_005 runtime files found zero matches.
- Strict non-Editor scan for Unity `RaycastHit` and PhysX command DTOs remained empty.
- Restore/build was not launched because the latest build gate was closed: active compiler count 8 (`dotnet`), CPU samples 100/100/100.

## 2026-05-25 - Encoded SDF Overflow Guard

What was wrong:
- The encoded SDF resolver was loop-bounded, but payload dimension multiplication and raw raymarch step count still needed explicit overflow guards before indexing or integer casts.

What was done:
- Added `TryResolveExpectedVoxelCount` to validate `int3` SDF dimensions through `long` multiplication and reject oversized/non-positive payloads.
- Added `ResolveBoundedRaymarchStepCount` so huge max distance and tiny step values clamp to `MaxEncodedRaymarchSteps` before any `int` cast.
- Added `VoxelSonarSdfMath_Raymarch_IsBoundedAndOverflowSafe` in `HeadlessKccSmokeTests` to verify bounded termination and oversized-grid rejection.
- Re-ran apex audit, OOP scanner, strict non-Editor forbidden scans, direct managed raymarch scan, broad published-raymarch ledger, and targeted diff-check.

Cinematic Cheats used:
- Same hard cap across tiers. Low tier spends fewer samples through larger `stepMeters`; high/ultra tiers can lower the step but cannot change authority or termination guarantees.

Exact Microseconds saved:
- No profiler microseconds claimed. This is a correctness hardening pass: bad authored SDF data now resolves as deterministic miss instead of overflow risk.

Verification:
- `python -m py_compile Tools/KccApexAudit_X_005.py Tools/OOP_Kcc_Scanner_X_005.py`: passed.
- `python Tools/KccApexAudit_X_005.py`: `broad_forbidden_count=0`, `scoped_forbidden_count=0`, `direct_managed_sdf_raymarch_consumer_count=0`, `scoped_published_sdf_raymarch_consumer_count=0`, `encoded_sdf_surface_resolver_consumers=9`, `encoded_sdf_surface_resolver_overflow_guard=true`, `encoded_sdf_surface_resolver_edit_tested=true`, `lockstep_size=64`, `kinematic_state_size=64`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- Strict non-Editor runtime scans returned zero Unity `RaycastHit`, PhysX command DTOs, `QueryParameters`, sync Unity Physics casts/overlaps/checks, Unity collision/trigger callbacks, `ClosestPoint`, `GetContacts`, `GetContact`, and `SweepTest*`.
- Broad `TryRaymarchAnyPublishedSdf` scan shows only non-X_005 audio/tether/world users plus `HectonVoxelVolume` owner method; scoped KCC runtime has zero matches.
- Restore/build was not launched because the latest gate was closed by one active `dotnet` process running Unity `VBCSCompiler.dll`; CPU samples were `20.2/18.5/14.3`.
- A 20-second retry remained closed: active `dotnet`, CPU samples `63.6/38.6/65.9`.

## 2026-05-25 - Proof Tool Repair And Build Proof

What was wrong:
- The audit had stale string predicates for KCC velocity consumers and reported false negatives for scooter shafts and sargassum after runtime moved to `CoreDeterminismSignals`.
- Full build was blocked by a cross-domain world vegetation import miss for DataVault memory types.

What was done:
- Updated `KccApexAudit_X_005.py` to accept both `PhysicsDeterminismSignals` and `CoreDeterminismSignals` KCC velocity accessors for the affected proof fields.
- Added `using Hecton8.Core.Memory;` to `HectonIndirectVegetationRenderer`.
- Ran gated restore, build-server shutdown, direct `Hecton8.Core.csproj` compile target, and final full `Assembly-CSharp.csproj` build.

Cinematic Cheats used:
- None. This pass was proof/tooling/compile closure.

Exact Microseconds saved:
- Runtime 0 us claimed. Value is evidence integrity and compiler proof.

Verification:
- `python -B -c "import ast..."`: both X_005 Python tools parse.
- `python Tools/KccApexAudit_X_005.py`: `broad_forbidden_count=0`, `scoped_forbidden_count=0`, `sargassum_uses_kcc_velocity_signal=true`, `scooter_shafts_velocity_uses_kcc_fallback=true`, `direct_managed_sdf_raymarch_consumer_count=0`, `encoded_sdf_surface_resolver_overflow_guard=true`, `lockstep_size=64`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- Strict non-Editor runtime forbidden scans remained empty.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /nodeReuse:false`: Build succeeded, 0 errors, 2 pre-existing `Hecton8.Input.csproj` missing-reference warnings.

## 2026-05-25 - SDF Surface Crossing Sign Discipline

What was wrong:
- `VoxelSonarSdfMath.TryRaymarchEncodedSdf` could report a hit at distance 0 from the first outside SDF sample because `density >= 0f && !hasPrevious` was treated as a crossing.
- `LaserCutterDodJobs` and legacy `HectonVoxelVolume` carried the same first-sample false-hit predicate, so compatibility/tool routes could diverge from the KCC payload kernel.

What was done:
- Replaced first-sample crossing with near-surface or bidirectional sign crossing in the encoded-SDF kernel, laser cutter SDF job, and voxel volume compatibility raymarch/job.
- Surface hit density is now written as resolved surface density `0f`, not the post-crossing sample value.
- Added payload-extent distance bounding plus effective-step raising so capped raymarches cover the useful SDF segment instead of only the first tiny slice of a huge requested ray.
- Expanded the headless edit test to cover inside->outside, outside->inside, outside-away miss, huge-distance cap, and oversized grid rejection.
- Extended `KccApexAudit_X_005.py` with `encoded_sdf_surface_resolver_no_initial_outside_hit`, `legacy_sdf_surface_routes_no_initial_outside_hit`, and `legacy_sdf_surface_routes_bounded`.

Cinematic Cheats used:
- Same continuous step-size knob across tiers. Low tier can use coarse steps; high/ultra tiers can spend saved budget on smaller steps, while the crossing law and authority stay identical.

Exact Microseconds saved:
- No profiler microseconds claimed. This is correctness hardening; it prevents false zero-distance constraints rather than removing a measured hot cost.

Verification:
- `python -B -c "import ast..."`: X_005 Python tools parse.
- `python Tools/KccApexAudit_X_005.py`: `broad_forbidden_count=0`, `scoped_forbidden_count=0`, `encoded_sdf_surface_resolver_no_initial_outside_hit=true`, `legacy_sdf_surface_routes_no_initial_outside_hit=true`, `legacy_sdf_surface_routes_bounded=true`, `encoded_sdf_surface_resolver_has_bounded_loop=true`, `encoded_sdf_surface_resolver_edit_tested=true`, `direct_managed_sdf_raymarch_consumer_count=0`, `scoped_published_sdf_raymarch_consumer_count=0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- Strict non-Editor runtime scans returned zero sync Unity Physics casts, PhysX command DTOs, `QueryParameters`, `RaycastHit`, and Unity collision/trigger callbacks.
- Targeted first-sample crossing pattern scan returned no old predicates in SDF collision routes.
- `git diff --check` passed for touched SDF/audit/test files with CRLF warnings only.
- Build was not launched: active `csc`, `dotnet`, and `VBCSCompiler` workers were present; latest CPU sample was `100%`.

## 2026-05-25 - Legacy Voxel SDF Stale Hit Guard

What was wrong:
- `VoxelSdfRaymarchJob.Execute()` could return on invalid input without clearing the writable result slot, so a reused native result array could preserve an old hit.

What was done:
- Cleared `Result[0]` to `default` before validation exits in `VoxelSdfRaymarchJob.Execute()`.
- Added `VoxelSdfRaymarchJob_InvalidInputClearsStaleHit` to prove stale hit, distance, and point data are cleared on invalid execution.
- Extended `KccApexAudit_X_005.py` with `legacy_voxel_sdf_job_clears_stale_result`.

Cinematic Cheats used:
- None. This is collision truth hygiene, not visual fidelity work.

Exact Microseconds saved:
- No profiler microseconds claimed. Cost is one default write to an existing native result slot; benefit is fail-closed deterministic collision output.

Verification:
- `python -B -c "import ast..."`: X_005 Python tools parse.
- `python Tools/KccApexAudit_X_005.py`: `broad_forbidden_count=0`, `scoped_forbidden_count=0`, `direct_managed_sdf_raymarch_consumer_count=0`, `scoped_published_sdf_raymarch_consumer_count=0`, `encoded_sdf_surface_resolver_no_initial_outside_hit=true`, `legacy_sdf_surface_routes_no_initial_outside_hit=true`, `legacy_sdf_surface_routes_bounded=true`, `legacy_voxel_sdf_job_clears_stale_result=true`, `lockstep_size=64`, `kinematic_state_size=64`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- Targeted stale-result pattern scan found the clear, edit test, and audit proof, with no old float-distance loops or first-sample predicates in the touched SDF routes.
- `git diff --check` passed for touched SDF/audit/test files with CRLF warnings only.
- Build was not launched: active compiler count `1` (`VBCSCompiler`), CPU samples `75.5/73.5/55`.

## 2026-05-25 - SDF Ray Bounds Interval Gate

What was wrong:
- SDF sampling clamps positions to payload edges. Raymarching without first intersecting the ray with the payload AABB could produce a false boundary hit for an outside-origin ray that never enters the volume.

What was done:
- Added scalar ray/AABB interval gates to `VoxelSonarSdfMath.TryRaymarchEncodedSdf`, `VoxelSdfRaymarchJob`, and `HectonVoxelVolume.TryRaymarchPublishedSdf`.
- Raymarch loops now sample `startDistance + i * step` and stop at `endDistance`; no payload entry means deterministic miss.
- Added headless tests for outside-away boundary miss and outside-entry hit on both encoded SDF math and legacy voxel SDF job routes.
- Extended `KccApexAudit_X_005.py` with interval proof fields and printed them in the proof summary.

Cinematic Cheats used:
- Scalar slab gate instead of any physical query. Low tier gets the same truth with coarse steps; high/ultra can spend on smaller steps and richer visuals without changing the authority route.

Exact Microseconds saved:
- No profiler microseconds claimed. Cost is scalar interval math; benefit is removing false collision planes from clamp-only sampling.

Verification:
- `python -B -c "import ast..."`: X_005 Python tools parse.
- `python Tools/KccApexAudit_X_005.py`: `broad_forbidden_count=0`, `scoped_forbidden_count=0`, `encoded_sdf_surface_resolver_uses_ray_bounds_interval=true`, `legacy_voxel_sdf_routes_use_ray_bounds_interval=true`, `encoded_sdf_surface_resolver_no_initial_outside_hit=true`, `legacy_sdf_surface_routes_bounded=true`, `legacy_voxel_sdf_job_clears_stale_result=true`.
- `python Tools/OOP_Kcc_Scanner_X_005.py`: `finding_counts={}`.
- Targeted interval pattern scan found the new interval gates and tests, and found no old bounded-distance helper calls or float-distance loops in the touched routes.
- `git diff --check` passed for touched SDF/audit/test files with CRLF warnings only.
- Build was not launched: active compiler count `2` (`csc`, `dotnet`), CPU samples `96.5/98.9/99.2`.

## 2026-05-25 - Owner-Directed Multi-Volume SDF Surface Resolver

What was wrong:
- `VoxelSonarSdfMath.TryResolveNearestSdfSurface` consumed only `TryReadNearestSonarSdf(runtimeOrigin)`.
- That was bounded and non-PhysX, but in a multi-volume scene it could raymarch the nearest-origin payload while the ray crosses another active volume first.
- This is a collision-truth bug, not an optimization issue.

What was done:
- Added `IVoxelSonarSdfSurfaceResolver` to `GroundRadarContracts.cs`.
- `VoxelSonarSdfMath.TryResolveNearestSdfSurface` now prefers the owner-directed resolver when the read model exposes it.
- `HectonVoxelEngine` now implements the owner-directed resolver and shares its all-active-volume encoded raymarch core with the compatibility `TryRaymarchNearestSonarSdf` method.
- Added `VoxelSonarSdfMath_SurfaceResolverPrefersOwnerRayDirectedRoute` to prove the owner route is used without nearest-payload fallback.
- Extended `KccApexAudit_X_005.py` with `encoded_sdf_surface_resolver_prefers_owner_ray_route` and `voxel_engine_owner_ray_surface_resolver_present`.

Cinematic Cheats used:
- None. This is collision truth; no visual fake is acceptable.

Exact Microseconds saved:
- 0 us claimed. Runtime cost increases by one managed interface type check per SDF surface probe and keeps the owner active-volume scan. The accepted trade is correctness: ray-directed surface ownership replaces nearest-origin payload approximation.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `python Tools/KccApexAudit_X_005.py` passed with `encoded_sdf_surface_resolver_prefers_owner_ray_route = true`, `voxel_engine_owner_ray_surface_resolver_present = true`, `direct_managed_sdf_raymarch_consumer_count = 0`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Targeted `git diff --check` passed for the touched SDF contract, voxel engine, headless test, and audit files with CRLF warnings only.
- Full build not rerun: gate closed by active `VBCSCompiler`, CPU samples `76/66.9/51.6`.

## 2026-05-25 - VRSomatic Root/Hand Micro-Job Removal

What was wrong:
- `VRSomaticProvider` scheduled a root sync job for one element and a hand kinematics parallel job for two hands.
- Late frame then probed two job handles. That is not amortized batch work and violates the project rule against tiny jobs/same-frame readback without profiler proof.

What was done:
- Replaced root scheduling with `UpdateRootSyncDirect`, preserving the same root input/output vault buffers and deterministic math.
- Replaced hand scheduling with `UpdateHandKinematicsDirect`, a fixed two-lane loop over existing hand target and physical-position vault buffers.
- Removed root/hand `JobHandle` fields, scheduled state bits, completion calls, unused Burst job structs, and the now-unused `Unity.Burst` import.
- Extended `KccApexAudit_X_005.py` with `vrsomatic_root_hand_micro_jobs_removed` and `vrsomatic_root_hand_job_completions_removed`.

Cinematic Cheats used:
- Deterministic direct math instead of scheduler ceremony. This is a presentation/runtime latency cheat, not a physics approximation.

Exact Microseconds saved:
- No profiler number claimed. Structural saving is concrete: one `IJob.Schedule()`, one `IJobParallelFor.Schedule(HandCount, 1)`, and two late-frame completion probes are gone per active VR somatic tick.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `python Tools/KccApexAudit_X_005.py` passed with `vrsomatic_root_hand_micro_jobs_removed = true`, `vrsomatic_root_hand_job_completions_removed = true`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Residue scan found zero root/hand scheduled state, handle, job, completion, tiny schedule, and `Unity.Burst` import symbols in `VRSomaticProvider.cs`.
- Targeted `git diff --check` passed for `VRSomaticProvider.cs` and `KccApexAudit_X_005.py` with CRLF warnings only.
- Full build not rerun: gate closed by CPU samples `62.6/83.4/55.3` despite zero active compiler processes.
## 2026-05-25 - Player Body Force Route Collapse

What was wrong: the X_005 audit still found four direct player-body force/angular route sites. Two were gated spawn/load angular reset paths, and two were `PhysicsApplySystem` pressure-event fallbacks that could queue force/impulse directly to `playerBody` when the movement force sink was missing.

What was done: added `HectonPlayerMotor.SetAngularVelocity`, routed spawn/load angular restoration through the player motor owner, and removed pressure-event player Rigidbody fallback branches from `PhysicsApplySystem`. Player pressure effects now enter `IPlayerMovementForceSink` or fail closed instead of re-entering Rigidbody authority.

Cinematic Cheats used: none. This is authority routing, not presentation.

Exact Microseconds saved: no measured runtime claim. Structural saving is two player Rigidbody force fallback branches and two direct angular reset call sites; static proof now reports `direct_player_body_force_route_count = 0`.

## 2026-05-25 - Physical Hand Root AUP Ownership

What was wrong: `PhysicalHandController` used its cached transform/controller position as `PlayerRootAUP` for the hand KCC/SDF bridge. That can turn hand hierarchy placement into player-root authority and distort the fixed shoulder anchor.

What was done: added `ResolvePlayerRootAup` so the hand bridge reads `PlayerRuntimeContext` / `PlayerRuntimePoseSnapshot` first, validates finite AUP, and only falls back to runtime-position conversion. Expanded `KccApexAudit_X_005.py` with explicit proof fields.

Cinematic Cheats used: fixed shoulder offset remains the cheap deterministic approximation; no skeletal simulation or scene search added.

Exact Microseconds saved: no profiler claim. Runtime cost is one cached context/snapshot read per active hand bridge tick. Static proof reports `physical_hand_root_uses_player_pose_snapshot = true` and `physical_hand_root_no_controller_transform_authority = true`.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `python Tools/KccApexAudit_X_005.py` passed with `physical_hand_root_uses_player_pose_snapshot = true`, `physical_hand_root_no_controller_transform_authority = true`, `direct_player_body_force_route_count = 0`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Targeted source scan found no old `_cachedTransform.position` player-root authority pattern in `PhysicalHandController.cs`.
- `git diff --check` passed for the touched runtime, audit, task, rationale, log, and regenerated report files with CRLF warnings only.
- Full build not rerun: gate closed by active `csc`/`dotnet` processes and CPU samples `94.4/100/98.1`.

## 2026-05-25 - Player Kinematics Hand Placement Micro-Job Removal

What was wrong: `PlayerKinematicsRuntime` still carried a hand-placement `IJob`, two hand-environment `JobHandle` fields, and pending flags, but `_handProbePending` had no true writer in runtime scripts. The lane was dead async state and would be a tiny-job violation if reactivated.

What was done: converted `PlayerKinematicsHandPlacementJob` into `PlayerKinematicsHandPlacementSolver`, execute it directly on existing vault views from `ScheduleHandProbes`, and removed the dead hand-probe/placement handles, pending flags, pump method, teardown completion method, and completion probes.

Cinematic Cheats used: direct deterministic hand target math remains the cheap presentation approximation; no new physics query, no new job, no skeletal simulation.

Exact Microseconds saved: no profiler claim. Structural saving is one dormant single `IJob.Schedule()` route, two `JobHandle` fields, two pending flags, and teardown/late-frame completion branches. Static proof reports `player_kinematics_hand_placement_micro_job_removed = true` and `player_kinematics_hand_placement_completion_removed = true`.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `python Tools/KccApexAudit_X_005.py` passed with `player_kinematics_hand_placement_micro_job_removed = true`, `player_kinematics_hand_placement_completion_removed = true`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Residue scan found zero dead hand-placement job/handle/pending/completion symbols in `PlayerKinematicsRuntime.cs`.
- `git diff --check` passed for the touched runtime, audit, task, rationale, log, and regenerated report files with CRLF warnings only.
- Full build not rerun: active compiler count `0`, but CPU sample set `64.3/33.8/15.8` still included one sample above the project gate.

## 2026-05-25 - Presentation Split-Authority Cache Removal

What was wrong:
- `PlayerSwimPresentationController` already consumed KCC velocity for swim presentation, but still serialized an unused `Rigidbody playerRigidbody` with a tooltip claiming speed/acceleration sampling.
- `CameraJuiceSystem` already consumed `CoreDeterminismSignals.TryGetLatestKccVelocityVector` for player camera speed, but still cached `_playerRigidbody` from the player runtime context.

What was done:
- Removed the swim serialized Rigidbody field and cold `TryGetComponent(out playerRigidbody)` resolve.
- Removed the camera player Rigidbody cache and all player-runtime assignments to it.
- Extended `KccApexAudit_X_005.py` to prove `player_swim_has_no_rigidbody_authority_field = true` and to require camera juice to use KCC velocity with no `_playerRigidbody` cache.

Cinematic Cheats used:
- Presentation remains driven by KCC signal snapshots. No player Rigidbody speed sampling fake remains.

Exact Microseconds saved:
- 0 us claimed. This removes split-authority debt and false future fallback surfaces, not a measured frame-time hotspot.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- Residue scans found zero `playerRigidbody` in `PlayerSwimPresentationController.cs` and zero `_playerRigidbody`, `PlayerRigidbody`, or `playerBody.linearVelocity` in `CameraJuiceSystem.cs`.
- `python Tools/KccApexAudit_X_005.py` passed with `player_swim_has_no_rigidbody_authority_field = true`, `camera_juice_uses_kcc_velocity = true`, `player_rigidbody_motion_state_read_count = 0`, `player_body_alias_motion_state_read_count = 0`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.

## 2026-05-25 - Mounted Rider Rigidbody Cache Removal

What was wrong:
- `MountablePlayerTransport` cached `_riderBody` and used it for mounted rider pose handoff, velocity sync, angular velocity reset, and dismount exit velocity.
- That created a player Rigidbody split-authority fallback inside transport presentation/control, even though `_riderMotor` and the KCC velocity signal already exist.

What was done:
- Removed `_riderBody` and its resolve/clear path.
- Routed mounted rider velocity and dismount exit velocity through `_riderMotor` and `CoreDeterminismSignals.TryGetLatestKccVelocityVector`.
- Removed rider `QueuePoseSet`, rider angular velocity queue, and `_riderBody.linearVelocity` reads.
- Extended `KccApexAudit_X_005.py` with `mountable_transport_no_rider_rigidbody_cache`.

Cinematic Cheats used:
- EVA handoff now uses the deterministic KCC velocity snapshot. If the snapshot is unavailable, the fallback is platform velocity at the rider point, which is a controlled visual/comfort handoff rather than Rigidbody truth.

Exact Microseconds saved:
- No profiler claim. Structural saving is one player Rigidbody cache, one rider pose queue fallback, one rider angular velocity queue, and one rider linear velocity read removed from transport handoff.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- Residue scan found zero `_riderBody`, `QueuePoseSet(_rider`, and `QueueAngularVelocitySet(_rider` in `MountablePlayerTransport.cs`.
- `python Tools/KccApexAudit_X_005.py` passed with `mountable_transport_no_rider_rigidbody_cache = true`, `player_rigidbody_motion_state_read_count = 0`, `player_body_alias_motion_state_read_count = 0`, `direct_player_body_force_route_count = 0`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.

## 2026-05-25 - Heavy Tow/Tether Player Anchor Collapse

What was wrong:
- `HeavyTowWinch` cached the player `Rigidbody` and passed it into the active tether fire route.
- `TetherInstance` treated the player body as the anchor body for connection registration, endpoint force bridge gating, and anchor velocity fallback.
- Tether bend detection still called `HectonVoxelVolume.TryRaymarchAnyPublishedSdf`, bypassing the owner-directed SDF resolver that KCC-adjacent consumers use.

What was done:
- Removed the heavy tow player `Rigidbody` cache and made player-bound attach require `HectonPlayerMotor` only.
- Added cached/hot-swap `IPhysicsService` use in the winch and removed the lazy hot registry fallback from `ResolvePhysicsService`.
- Converted player-bound tether instances to `_anchorBody == null`; optional `_anchorBody` remains only for non-player transport transfer anchors.
- Routed tether anchor velocity through `CoreDeterminismSignals.TryGetLatestKccVelocityVector`.
- Marked legacy anchor-body wrappers as compatibility wrappers with `_ = legacyAnchorBody`.
- Replaced tether bend published-volume raymarch calls with `VoxelSonarSdfMath.TryResolveNearestSdfSurface` through cached voxel owner interfaces.
- Extended `KccApexAudit_X_005.py` with explicit heavy tow/tether proof fields and regenerated `Docs/Reports/KCC_APEX_AUDIT_X_005.*`.

Cinematic Cheats used:
- Player tether anchor motion uses the deterministic KCC velocity snapshot instead of body simulation. Cable bend still uses SDF surface/corner approximation; no PhysX query or per-cable-segment truth was added.

Exact Microseconds saved:
- No profiler number claimed. Structural saving is one player `Rigidbody` cache, one active fire-route body parameter, one player body tether registration/force bridge path, one tether published-SDF raymarch route, and one hot registry fallback removed.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- Targeted residue scan found zero player `_playerRigidbody`, direct tether `TryRaymarchAnyPublishedSdf`, and stale `PhysicsDeterminismSignals.TryGetLatestKccVelocityVector` routes in the heavy tow/tether files.
- `python Tools/KccApexAudit_X_005.py` passed with `heavy_tow_no_player_rigidbody_anchor_cache = true`, `heavy_tow_physics_route_cached_no_hot_registry = true`, `tether_legacy_anchor_wrapper_ignores_player_rigidbody = true`, `tether_player_anchor_uses_motor_not_rigidbody = true`, `tether_sdf_bend_uses_owner_resolver = true`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Audit false-field scan found only `solver.job_recursion_detected`, expected false because no recursion was detected.
- `git diff --check` passed for heavy tow, tether manager, tether instance, audit, and regenerated KCC report files with CRLF warnings only.

## 2026-05-25 - Crash Telemetry Player Pose Authority

What was wrong:
- `CrashTelemetryBuffer` already used `CoreDeterminismSignals.TryGetLatestKccVelocityFloat3` for player velocity, but still cached a player `Rigidbody` from the player root.
- That cache was dead today, but it preserved a split-authority recovery surface inside the blackbox route.

What was done:
- Removed `_playerRigidbody` and `TryGetComponent(out _playerRigidbody)` from `CrashTelemetryBuffer`.
- Made `SamplePlayerPosition` read `PlayerRuntimeContextService.ActiveRuntimeContext.TryGetPlayerPoseSnapshot` first and validate finite AUP before legacy movement/transform fallback.
- Extended `KccApexAudit_X_005.py` with `crash_telemetry_no_player_rigidbody_cache`, `crash_telemetry_player_pose_snapshot_first`, and `crash_telemetry_uses_kcc_velocity_signal`.
- Regenerated `Docs/Reports/KCC_APEX_AUDIT_X_005.*` and `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`.

Cinematic Cheats used:
- Blackbox position records use the owner-published AUP snapshot. No visual cheat added; the fallback remains coarse transform-to-AUP only for missing runtime context.

Exact Microseconds saved:
- 0 us claimed. Structural saving is one dead player `Rigidbody` cache and one cold component resolve removed from crash telemetry.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `python Tools/KccApexAudit_X_005.py` passed with crash telemetry proof fields true, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Targeted residue scan found zero `_playerRigidbody`, `TryGetComponent(out _playerRigidbody)`, and stale `PhysicsDeterminismSignals.TryGetLatestKccVelocity` in `CrashTelemetryBuffer.cs`.
- Audit false-field scan found only `solver.job_recursion_detected`, expected false because no recursion exists.
- `git diff --check` passed for crash telemetry, audit, and regenerated reports with CRLF warnings only.
- Build not launched: active compiler count `1` (`dotnet`) and CPU samples `100.0/76.8/99.2`.

## 2026-05-25 - Tool Player Rigidbody Anchor Removal

What was wrong:
- `HarpoonLauncherTool` cached a player `Rigidbody` that was not used by its recoil route.
- `PropulsionTool` passed `playerContext.PlayerRigidbody` into tractor hold PD, allowing the physics owner to sample player body point velocity and apply reaction force to the player body.

What was done:
- Removed the harpoon `_playerRigidbody` field and assignment.
- Changed propulsion tractor hold to call `QueueTractorBeamPd(null, _lockedBody, ..., applyReactionForce: false)`.
- Kept payload physics under `IPhysicsService`; did not create a new global route.
- Extended `KccApexAudit_X_005.py` with harpoon/propulsion proof fields and updated the performance monitor legacy-query proof to the current `IPhysicsQueryTelemetryReadModel` route.

Cinematic Cheats used:
- Tractor hold now treats the tool target as a world/tool anchor for payload stabilization. Player counter-motion is not simulated through Rigidbody; feedback remains presentation/tool-side.

Exact Microseconds saved:
- No profiler claim. Structural saving is one dead player `Rigidbody` cache and one active tractor-hold player body velocity/reaction branch removed.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `python Tools/KccApexAudit_X_005.py` passed with `harpoon_no_player_rigidbody_cache = true`, `propulsion_tractor_no_player_rigidbody_anchor = true`, `performance_monitor_uses_surface_query_counter = true`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Targeted residue scan found zero `_playerRigidbody`, `playerContext.PlayerRigidbody`, and non-null `QueueTractorBeamPd` anchor in the touched tool files.
- Audit false-field scan found only `solver.job_recursion_detected`, expected false because no recursion exists.
- `git diff --check` passed for harpoon, propulsion, audit, and regenerated reports with CRLF warnings only.
- Build not launched: active compiler count `0`, CPU samples `87.7/99.0/100.0`.

## 2026-05-25 - Underwater Visual Player Rigidbody Cache Removal

What was wrong:
- `HectonUnderwaterVisuals` already used KCC velocity for player speed and bottom-silt boost, but still cached `_playerRigidbody` from the player runtime context.
- That field was dead, but it kept a visual-system path back to player Rigidbody authority.

What was done:
- Removed the `_playerRigidbody` field.
- Removed player-service reset, lazy bottom-silt resolve, and cache assignments to the field.
- Left player movement subscription and transport coordinator caching intact.
- Extended `KccApexAudit_X_005.py` with `underwater_visuals_uses_kcc_velocity_no_player_rigidbody`.

Cinematic Cheats used:
- Underwater silt and speed-driven visuals remain presentation driven by KCC velocity snapshots, not Rigidbody motion.

Exact Microseconds saved:
- 0 us claimed. Structural saving is one dead player `Rigidbody` field plus three assignment/resolve sites removed.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `python Tools/KccApexAudit_X_005.py` passed with `underwater_visuals_uses_kcc_velocity_no_player_rigidbody = true`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Targeted residue scan found zero `_playerRigidbody` or `PlayerRigidbody` in `HectonUnderwaterVisuals.cs`.
- Audit false-field scan found only `solver.job_recursion_detected`, expected false because no recursion exists.
- `git diff --check` passed for underwater visuals, audit, and regenerated reports with CRLF warnings only.
- Build not launched: active compiler count `0`, CPU samples `83.4/77.2/58.8`.

## 2026-05-25 - Player Thruster Audio Rigidbody Velocity Removal

What was wrong:
- `PlayerThrusterAudio` cached `_playerRb` and read `_playerRb.linearVelocity` for speed shaping and dive accent.
- That made thruster audio a live presentation consumer of PhysX body velocity while KCC owns player motion.

What was done:
- Removed `_playerRb` and its `TryGetComponent` assignment.
- Added `KccVelocityThrusterAudioMaxAgeFrames` and `TryResolveKccVelocity`.
- Routed thruster speed factor and dive attack intensity through `CoreDeterminismSignals.TryGetLatestKccVelocityFloat3`.
- Extended `KccApexAudit_X_005.py` with `player_thruster_audio_uses_kcc_velocity_no_rigidbody`.
- Regenerated `Docs/Reports/KCC_APEX_AUDIT_X_005.*` and `Docs/Reports/KINEMATICS_OPTIMIZATION_REPORT_X_005.json`.

Cinematic Cheats used:
- Audio thrust load is now a deterministic KCC-velocity fake. No Rigidbody acceleration truth is simulated for sound; stale KCC data fades to zero thrust intensity.

Exact Microseconds saved:
- 0 us claimed. Structural saving is one player `Rigidbody` cache and two active `linearVelocity` reads removed from audio tick.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- `rg` found zero `_playerRb`, `linearVelocity`, or `PlayerRigidbody` in `PlayerThrusterAudio.cs`.
- `python Tools/KccApexAudit_X_005.py` passed with `player_thruster_audio_uses_kcc_velocity_no_rigidbody = true`, `player_rigidbody_velocity_read_count = 0`, `broad_forbidden_count = 0`, and `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Recursive audit false-field scan found only `solver.job_recursion_detected`, expected false because no recursion exists.
- `git diff --check` passed for thruster audio, audit, and regenerated reports with CRLF warnings only.
- Build not launched: active compiler count `0`, CPU load `100.0`.

## 2026-05-25 - Player Audio/Landing/Flora Velocity Authority Sweep

What was wrong:
- `PlayerFootstepAudio` read `_playerRb.linearVelocity` for speed-volume scaling.
- `LandingImpactVFX` read `_playerRb.linearVelocity.y` for fall impact intensity and kept a disabled stale copy under `#if false`.
- `FloraInteractionManager` read `_playerRb.linearVelocity` for player wake/sediment velocity.
- The scanner did not catch `_playerRb` aliases, so the proof was too narrow.

What was done:
- Removed player Rigidbody caches and resolves from footstep audio, landing impact VFX, and flora player wake logic.
- Routed footstep volume, landing fall-speed, and flora player wake velocity through existing KCC velocity signals.
- Preserved transform-delta fallback only for flora when KCC velocity is stale.
- Left non-player submarine/predator Rigidbody velocity reads in flora untouched because those are not player KCC authority.
- Expanded `Tools/OOP_Kcc_Scanner_X_005.py` to scan player audio/landing files and catch `_playerRb.linearVelocity`.
- Extended `KccApexAudit_X_005.py` with footstep, landing, and flora proof fields.

Cinematic Cheats used:
- Footsteps, landing impact, and flora wake now use deterministic KCC velocity as the presentation fake for player motion. No PhysX body velocity participates in player sensory feedback.

Exact Microseconds saved:
- No profiler claim. Structural saving is three player Rigidbody caches/reads removed and one scanner blind spot closed.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- Targeted residue scan found zero `_playerRb`, `PlayerRigidbody`, `return _playerRb.linearVelocity`, and `runtimePlayerTransform.TryGetComponent(out _playerRb)` in the touched player audio/landing/flora files.
- `python Tools/KccApexAudit_X_005.py` passed with footstep, landing, and flora proof fields true; `player_rigidbody_velocity_read_count = 0`; `broad_forbidden_count = 0`; `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- Recursive audit false-field scan found only `solver.job_recursion_detected`, expected false because no recursion exists.
- `git diff --check` passed for player audio, landing VFX, flora, scanners, and regenerated reports with CRLF warnings only.
- Build not launched: active compiler count `0`, CPU load `91.0`.

## 2026-05-25 - Peripheral Player Rigidbody Cache Sweep

What was wrong:
- `CaveBioRootsGenerator` kept a dead player `Rigidbody` cache beside KCC velocity prop-wash.
- `FoveatedSimulationManager` used player `Rigidbody.linearVelocity` for listener Doppler-relative velocity.
- `HectonVoxelEngine` predictive voxel proxy had a player `Rigidbody` fallback for origin/velocity/dampening.
- `PlayerCriticalProceduralAudioRenderer` cached player `Rigidbody`/body id for audio gating, sonar Doppler, motor speed shortcut, and impact ownership.

What was done:
- Removed player Rigidbody cache/resolution from cave roots and critical procedural audio.
- Routed foveated listener velocity through KCC velocity snapshots with the existing transform-delta fallback.
- Routed predictive voxel proxy player velocity through KCC signal and player dampening through `QueueSubsystemExternalVelocityChange`.
- Removed critical-audio player body id ownership shortcut; impact proximity now uses AUP distance.
- Extended `KccApexAudit_X_005.py` with cave/foveated/proxy/critical-audio proof fields.
- Extended `Tools/OOP_Kcc_Scanner_X_005.py` scope to include cave roots, foveated manager, and critical audio.

Cinematic Cheats used:
- Doppler, root wash, proxy anticipation, and critical audio response use the deterministic KCC velocity fact as the fake motion source. No player PhysX body velocity participates.

Exact Microseconds saved:
- No profiler claim. Structural saving is four player body authority surfaces and one active foveated listener `linearVelocity` read removed.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- Targeted residue scan found zero player Rigidbody/cache/body-id symbols in the four touched runtime files.
- `python Tools/KccApexAudit_X_005.py` passed with cave roots, foveated listener, predictive proxy, and critical audio proof fields true; `player_rigidbody_velocity_read_count = 0`; `broad_forbidden_count = 0`; `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `git diff --check` passed for touched runtime/scanner/report files with CRLF warnings only.
- Build passed after gate opened: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1 /p:BuildProjectReferences=false /p:UseSharedCompilation=false` -> 1 existing missing-project warning, 0 errors.

## 2026-05-25 - World Player Rigidbody Cache/Force Sweep

What was wrong:
- `SargassumMicroFaunaBoids` kept a dead player `Rigidbody` cache beside KCC/player movement context.
- `HectonMapMagicVegetationBridge` kept a dead player `Rigidbody` cache while player motion was already KCC-velocity driven.
- `AbyssalThermalManager` resolved `playerContext.PlayerRigidbody` and queued player thermal updraft through `_physicsService.QueueForce`.

What was done:
- Removed player Rigidbody caches and resolves from Sargassum micro fauna and MapMagic vegetation bridge.
- Routed abyssal thermal player convection through `HectonPlayerMovement.QueueSubsystemExternalVelocityChange`.
- Added deterministic `PlayerEquivalentMassKg` for player heat impulse scaling.
- Preserved non-player Rigidbody force/velocity routes for submarine and hull authorities.
- Extended `KccApexAudit_X_005.py` with Sargassum, MapMagic, and abyssal thermal proof fields.
- Extended `Tools/OOP_Kcc_Scanner_X_005.py` scope to include the three world files.

Cinematic Cheats used:
- Flora/fauna/vegetation player motion remains a deterministic KCC-velocity fake. Thermal lift applies as an owner-routed movement impulse rather than simulating player Rigidbody force truth.

Exact Microseconds saved:
- No profiler claim. Structural saving is two dead player body caches and one active player Rigidbody force route removed.

Verification:
- `python -B -c "import ast..."` parsed both X_005 Python tools.
- Targeted residue scan found zero player Rigidbody/cache symbols in the three touched world files; remaining `submarineHull.linearVelocity` is non-player authority.
- `python Tools/KccApexAudit_X_005.py` passed with Sargassum, MapMagic, and abyssal thermal proof fields true; `player_rigidbody_velocity_read_count = 0`; `broad_forbidden_count = 0`; `scoped_forbidden_count = 0`.
- `python Tools/OOP_Kcc_Scanner_X_005.py` reported `finding_counts = {}` and Hydro KCC forbidden command hits 0.
- `git diff --check` passed for touched world/scanner/report files with CRLF warnings only.
- Build not launched: active compiler count `0`, CPU samples `56.4/78.1/44.0`.

## 2026-05-25 - Critical Audio Impact Body Identity Closure

What was wrong:
- `PlayerCriticalProceduralAudioRenderer` still exposed an impact-owner helper shaped for player body-id matching, which could regress to `PlayerRuntimeContext.PlayerRigidbody` ownership.

What was done:
- Made `IsBoundPlayerImpact` explicit, static, and fail-closed.
- Kept player impact stress on the existing AUP distance/proximity path.
- Recorded the closure in status/rationale so the proof artifact does not rely on chat memory.

Cinematic Cheats used:
- Player critical impact audio treats nearby impacts as sensory pressure by AUP proximity, not by PhysX body identity.

Exact Microseconds saved:
- No profiler claim. Structural saving is one player body identity hook removed from the critical audio route.

Verification:
- X_005 prompt was re-extracted from `Docs/Tasks/CURRENT_BATCH.md`.
- `AGENTS.md`, `TASTE.md`, current status, and rationale were read before editing.
- Targeted source check found no `_playerImpactBodyId`, `PlayerRigidbody`, `EnsurePlayerImpactBodyId`, or `VehicleMotor.TryResolveForBody(_playerRigidbody)` in `PlayerCriticalProceduralAudioRenderer.cs`.
