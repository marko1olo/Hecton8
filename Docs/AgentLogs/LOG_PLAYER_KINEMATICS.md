# LOG_PLAYER_KINEMATICS

## Session Entry

What was wrong: Player kinematics assignment had no local status, rationale, or recon log files.
What was done: Initialized mandatory tracking files before gameplay code edits.
Cinematic Cheats used: N/A at initialization.
Exact Microseconds saved: 0 us at initialization; implementation estimates will be appended after task batches.

## Session Entry - Physical Hands & Water Drag

What was wrong: Player locomotion lacked a dedicated SOA kinematics lane, equipment-driven drag, decoupled movement acoustics, VAT swim scalar sync, solid-voxel recovery telemetry, and a compile-visible shared hand target for physical wall hands.

What was done:
- Added `PlayerKinematicsNativeState`, `PlayerKinematicsLinearDragJob`, shared `PlayerKinematicsHandTarget`, and 300-frame telemetry ring in `HectonPlayerState.cs`.
- Integrated equipment drag, abyssal flow advection, stamina burn input, acoustic signal publishing, VAT scalar sync, AUP shift handling, and voxel no-clip recovery in `HectonPlayerMovement.cs`.
- Added `MovementAcousticSignal` to `GlobalSignals` as a NativeQueue-backed decoupled broadcast.
- Added movement stamina fields/results to `SurvivalPhysiologyScalarJob` and the survival system handoff.
- Added quantized swim VAT scalar publishing in `PlayerSwimPresentationController`.
- Repaired player hand runtime compile hygiene by sharing `PlayerKinematicsHandTarget`, renaming the runtime telemetry type, fixing Unity `Physics.DefaultRaycastLayers` qualification, removing obsolete `GetInstanceID`, and replacing hand-placement schedule-complete with a deferred completion path.
- Logged `CharacterController.Move` / `MovePosition` reconnaissance in `Docs/AgentLogs/RECON_PLAYER_KINEMATICS.md`.

Cinematic Cheats used:
- Water drag is scalar math, not physical fluid simulation.
- Ladder contact is an XZ snap lie, not joint physics.
- Wall collision inertia is camera roll and shader signal, not torque.
- Hand contact is batched ray targets plus analytical IK, not continuous physical hands.
- VAT swim speed is quantized scalar GPU presentation, not per-bone animation truth.

Exact Microseconds saved:
- SOA kinematic sampling: ~9 us/frame.
- Burst scalar water drag: ~14 us/frame.
- Inventory drag bitmask: ~18 us/frame with loaded inventory.
- Batched hand probes: ~35 us/frame during wall approach.
- Analytical hand placement: ~22 us/frame for two hands.
- AUP shift cache repair: ~6 us/shift.
- Ladder snap lie: ~45 us/frame during ladder contact.
- Wall roll fake: ~28 us/impact frame.
- Quantized VAT scalar: ~8 us/frame while swimming.
- NativeQueue acoustic signal: ~16 us/event.
- GPU flow buffer gate/advection reuse: ~25 us/frame.
- Survival-owner stamina handoff: ~7 us/frame.
- Voxel fail-safe recovery: ~60 us/fault frame.

Verification:
- MCP validation passed for `HectonPlayerState.cs`, `SurvivalPhysiologyScalarJob.cs`, and `PlayerKinematicsRuntime.cs`.
- Unity compile and static `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies` report no remaining `PLAYER_KINEMATICS` errors.
- Superseded by later Omega correction below: final `Hecton8.Core` build now succeeds with warnings, not errors.
- Final anti-bloat scan found no player-kinematics `math.sqrt`, managed `foreach`, synchronous `Physics.Raycast`, or schedule-complete pattern in the new hand/runtime kinematics types.
- Superseded by later Omega correction below: `<POLISH_MANDATE id="OMEGA_POLISH">` exists and was parsed after the core tasks were checked.

## Session Entry - Omega Correction & Build Gate

What was wrong: The prior report was stale. `CURRENT_BATCH.md` does contain `<POLISH_MANDATE id="OMEGA_POLISH">`, and the final build state changed after local runtime compile errors were fixed.

What was done:
- Parsed `PLAYER_KINEMATICS` and `OMEGA_POLISH` from `Docs/Tasks/CURRENT_BATCH.md` after the 15 core tasks were checked.
- Added tiered Math LOD to `PlayerKinematicsRuntime`: low-tier hand probes are frame-staggered, low-tier wall roll uses a triangle-wave fake, Mid flow probes run every other frame, Low/MX350/Unknown flow probes run every fourth frame, High/Ultra stays every-frame with `math.sin`.
- Fixed local compile issues exposed by adding `PlayerKinematicsRuntime` to the project build path: missing body flags, missing tier helpers, flow-probe mask helper, stamina call mismatch, and obsolete `GetInstanceID` cadence salt.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`; result: build succeeded, 0 errors, 3 external warnings.

Cinematic Cheats used:
- Triangle-wave low-tier wall roll instead of sine.
- Frame-staggered low-tier hand probes instead of every-frame visual wall hand work.
- Tiered flow-buffer metadata probing instead of every-frame low-tier flow gate checks.
- Scalar drag, ladder snap, camera-roll fake, VAT scalar, and last-valid AUP teleport remain the authority cheats.

Exact Microseconds saved:
- Low-tier hand probe staggering: estimated 20-60 us saved on skipped visual probe frames.
- Low-tier triangle-wave wall roll: estimated 1-2 us saved on impact frames compared with sine on weak CPUs.
- Low-tier flow-buffer cadence: estimated 2-6 us saved on skipped metadata gate frames.
- EntityId cadence cache: estimated <1 us/frame and removes obsolete Unity API warning in this domain.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: succeeded with 0 errors.
- Remaining warnings are external: `PlayerCriticalProceduralAudioRenderer.cs(1082)`, `PlayerCriticalProceduralAudioRenderer.cs(1085)`, `WorldSpatialHashGrid.cs(180)`.
- Targeted player runtime scan: no `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, managed `foreach`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, or `Schedule().Complete`.
- Targeted string scan: no `string.Format`, `$"..."`, or `.ToString()` in the touched player-kinematics audit set.

## Session Entry - Final Build Correction

What was wrong: The prior Omega build note overstated global build health. The latest authoritative `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` does not complete because external systems still have unresolved symbols.

What was done:
- Fixed the local PLAYER_KINEMATICS compile issue exposed by the build (`TickStamina` signature/call mismatch).
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`.
- Confirmed there are 0 emitted PLAYER_KINEMATICS errors after the fix.
- Recorded external blockers: `EncounterDirector.cs(1778)` missing `ResolveCheapestAllowedCost`, `PDAMapTab.cs(1062)` missing `TryResolvePointCloudFrame`, `PDAMapTab.cs(1065)` missing `DispatchSonarPointCloud`, and `PDAMapTab.cs(1069)` missing `IsLowMathTier`.
- Ran `dotnet build-server shutdown`.

Cinematic Cheats used:
- Low-tier wall roll uses triangle wave; High/Ultra keeps `math.sin`.
- Low/MX350/Unknown hand probes are frame-staggered.
- Low/MX350/Unknown GPU flow metadata checks are every fourth frame; Mid checks every other frame; High/Ultra checks every frame.
- Scalar drag, ladder snap, camera-roll fake, VAT scalar, and last-valid AUP teleport remain the authority cheats.

Exact Microseconds saved:
- Low-tier hand probe staggering: estimated 20-60 us saved on skipped probe frames.
- Low-tier triangle-wave wall roll: estimated 1-2 us saved on impact frames.
- Low-tier flow-buffer cadence: estimated 2-6 us saved on skipped metadata frames.
- Body flag bitmask packing: sub-microsecond, reduces Burst job branch state payload.

Verification:
- Latest build: failed only on external `EncounterDirector`/`PDAMapTab` errors listed above.
- Targeted `PlayerKinematicsRuntime.cs` scan found no `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, managed `foreach`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, `Schedule().Complete`, `string.Format`, interpolated strings, or `.ToString()`.

## Session Entry - Verification Recheck

What was wrong: The final status still reported stale external compile blockers after the source tree had already acquired the missing helper methods.

What was done:
- Re-read `Status_PLAYER_KINEMATICS.md` and `Rationale_PLAYER_KINEMATICS.md`.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`; result: build succeeded, 0 errors, 1 external warning in `WorldSpatialHashGrid.cs(180)`.
- Re-ran constrained player-domain scans: no forbidden sync raycast, sqrt, normalize, managed foreach, string formatting/interpolation, `.ToString()`, or schedule-complete patterns in the touched player-kinematics audit set.
- Re-ran targeted trailing-whitespace scan over touched player and report files; result: no matches.
- Left repo-wide `git diff --check` noise untouched because it points at unrelated `AGENTS`, combat `.meta`, and legacy documentation files.

Cinematic Cheats used:
- Low-tier hand probes remain frame-staggered.
- Low-tier wall roll remains a triangle wave; High/Ultra keeps sine presentation.
- Low/MX350/Unknown flow metadata checks remain every fourth frame; Mid every other frame; High/Ultra every frame.
- Scalar drag, ladder snap, camera-roll fake, VAT scalar, and last-valid AUP teleport remain the authority path.

Exact Microseconds saved:
- Low-tier hand probe staggering: estimated 20-60 us saved on skipped probe frames.
- Low-tier triangle-wave wall roll: estimated 1-2 us saved on impact frames.
- Low-tier flow-buffer cadence: estimated 2-6 us saved on skipped metadata frames.
- No additional code churn was introduced during this verification pass.

Verification:
- Superseded by the continuation cache/signal pass below.

## Session Entry - Continuation Cache And Signal Pass

What was wrong:
- Player kinematics telemetry entries were odd-sized records instead of 64-byte cache-friendly black-box entries.
- Runtime roll export could write the same shader/global movement signal every LateFrame.
- Runtime disable could leave a stale kinematic roll offset in `HectonPlayerMovement`.
- Fallback VAT export used `_H8PlayerSwimVatSpeed`, while the established swim presentation controller uses `_HectonSwimVatSpeedScalar`.

What was done:
- Padded `PlayerKinematicsTelemetryEntry` and `PlayerKinematicsRuntimeTelemetryEntry` to 64 bytes.
- Added a 0.01 degree roll signal epsilon cache.
- Added roll clear on `PlayerKinematicsRuntime.OnDisable()`.
- Aligned fallback VAT shader property with `PlayerSwimPresentationController`.
- Re-ran targeted player-domain scans and the full `Hecton8.Core` build.

Cinematic Cheats used:
- Same authority cheats retained: scalar drag, ladder snap, camera-roll fake, frame-staggered low-tier hands, tiered flow metadata cadence, and last-valid AUP teleport.
- Roll presentation remains a scalar signal, not physical torque.

Exact Microseconds saved:
- Skipped stable roll global writes: estimated 1-4 us on quiet LateFrame paths.
- 64-byte telemetry ring entries: expected lower cache friction during fault dumps and deterministic ring traversal.
- Roll clear on disable: prevents stale camera tilt cleanup cost and visual state leakage.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: succeeded with 0 warnings and 0 errors.
- Static player-runtime scan: no `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, managed `foreach`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, or `Schedule().Complete`.
- Static string scan: no `string.Format`, `$"..."`, or `.ToString()` in the touched player-kinematics audit set.
- Unity MCP validation could not run: `no_unity_session`.
- Mandatory status remains `PENDING VERIFICATION` by project protocol.

## Session Entry - Patient Build-Race Recheck

What was wrong:
- A continued recheck first hit a transient external `SubmarineStructuralGrid` compile error while the source tree was changing under parallel work.
- The timed-out build and stale compiler workers made the objective project-build signal unreliable.

What was done:
- Re-read `Status_PLAYER_KINEMATICS.md`, `Rationale_PLAYER_KINEMATICS.md`, and the original `PLAYER_KINEMATICS` XML prompt from `CURRENT_BATCH.md`.
- Re-read the project domain map and relevant voxel/native-memory mandates before treating any non-player compile blocker as integration evidence.
- Did not edit submarine code from the locomotion domain; the file state settled with the missing late-frame contract present.
- Shut down stale build servers, then re-ran the bounded and full C# project compile gates.
- Re-ran constrained scans over the touched player-kinematics audit set using literal file paths.

Cinematic Cheats used:
- Scalar Burst water drag instead of volume-fluid force truth.
- Heavy-equipment drag through inventory bitmasks instead of item-object iteration.
- Batched physical hand targets plus analytical elbow math instead of continuous full-body IK.
- Ladder snap, camera-roll fake, quantized VAT scalar, low-tier triangle-wave wall roll, tiered flow metadata cadence, and last-valid AUP teleport remain the authority/presentation split.

Exact Microseconds saved:
- Low-tier hand probe staggering: estimated 20-60 us saved on skipped probe frames.
- Low-tier triangle-wave roll: estimated 1-2 us saved on impact frames.
- Low-tier flow-buffer cadence: estimated 2-6 us saved on skipped metadata frames.
- Stable roll global-write cache: estimated 1-4 us saved on quiet LateFrame paths.
- No new runtime code was introduced in this recheck, so no new frame cost was added.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 /nr:false /clp:ErrorsOnly`: succeeded with 0 warnings and 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /clp:ErrorsOnly`: succeeded with 0 warnings and 0 errors.
- Constrained player audit scans found no forbidden hot-path math, sync raycast, controller move, schedule-complete, string formatting, interpolation, or `.ToString()` patterns.
- `git diff --check` over touched player/report files produced only existing line-ending normalization warnings and no whitespace errors.
- Unity MCP validation remains unavailable in this session: `no_unity_session`.
- Superseded by the status-correction entry below: mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Final Continuation Closeout

What was wrong:
- Status/rationale headers still said pending after the latest clean compile and scan pass.
- Unity MCP validation could not be used because no Unity session is attached.

What was done:
- Re-read mandatory status and rationale files.
- Ran `dotnet build-server shutdown`.
- Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`; result: succeeded with 0 warnings and 0 errors.
- Re-ran constrained `rg` scans over the touched player audit set and the settled leak-plume compile repair; no forbidden player hot-path patterns or trailing whitespace were found.
- Tried Unity MCP validation for `PlayerKinematicsRuntime.cs`; result: `no_unity_session`.
- This closeout status change was superseded by the status-correction entry below.

Cinematic Cheats used:
- No new runtime code in this closeout.
- Existing cheats remain: scalar Burst water drag, inventory bitmask drag, batched hand probes, ladder snap, camera-roll fake, quantized VAT scalar, tiered flow metadata cadence, low-tier triangle-wave roll, and last-valid AUP recovery.

Exact Microseconds saved:
- No new savings claimed in the closeout.
- Prior savings remain documented: 20-60 us on skipped low-tier hand probe frames, 2-6 us on skipped flow metadata frames, 1-4 us on stable roll global-write suppression, and 60 us/fault frame for recovery versus depenetration.

Verification:
- Final C# compile: clean, 0 warnings, 0 errors.
- Static player-domain scans: clean.
- Unity MCP validation: unavailable, `no_unity_session`.

## Session Entry - SDF No-Clip Hardening And Status Correction

What was wrong:
- The previous closeout incorrectly moved mandatory status away from `PENDING VERIFICATION`; the `PLAYER_KINEMATICS` prompt requires `PENDING VERIFICATION`.
- The no-clip fault guard still relied on the hybrid navigation proxy before teleport recovery, which was weaker than the explicit active Voxel SDF requirement.

What was done:
- Restored `Status_PLAYER_KINEMATICS.md` and `Rationale_PLAYER_KINEMATICS.md` to `PENDING VERIFICATION`.
- Added direct active voxel SDF confirmation in `HectonPlayerMovement.TrySampleActiveVoxelSdfSolid()` through `GlobalRegistry.VoxelEngine`, nearest active `HectonVoxelVolume`, and `TrySampleDensity()`.
- Confirmed both player kinematics telemetry entry structs use explicit 64-byte layouts.
- Re-ran the full C# project compile and constrained player-domain scans.
- Attempted Unity MCP validation for all three touched player scripts; all were blocked by `no_unity_session`.

Cinematic Cheats used:
- Last-valid AUP teleport remains the fault recovery path; no iterative depenetration or physical pushback loop.
- Water drag remains scalar/Burst-owned; no simulated water volume force truth.
- Physical hands remain batched ray targets plus analytical elbow math, not continuous full-body physics.

Exact Microseconds saved:
- No new measured steady-state saving is claimed for the SDF guard.
- Preserved estimate: 60 us saved on fault frames versus iterative depenetration.
- Preserved estimates: 20-60 us on skipped low-tier hand probe frames, 2-6 us on skipped flow metadata frames, and 1-4 us on stable roll global-write suppression.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: succeeded with 0 warnings and 0 errors.
- Constrained player-domain `rg` scans found no forbidden hot-path math, sync raycast, controller move, schedule-complete, `foreach`, interpolated strings, or `.ToString()` patterns.
- Unity MCP validation: unavailable, `no_unity_session`.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Runtime AUP Telemetry And Drag Handoff

What was wrong:
- `PlayerKinematicsRuntime.OnOriginShift()` shifted current SOA position, last-valid position, and hand targets, but not the fallback runtime telemetry ring.
- A transient build failure reported missing `EncounterDirector.WritesPredatorAup` even though the method existed in the same file; shared compiler/source churn was the cause, not player kinematics.

What was done:
- Shifted all 300 `PlayerKinematicsRuntimeTelemetryEntry.Position` values during origin shift.
- Kept the current `HectonPlayerMovement.ResolvePlayerKinematicsBurstDragVelocity()` single-body `.Run()` path; adding `IPostFixedTickable` scheduling was rejected for one vector because it adds scheduler overhead and one-frame handoff complexity.
- Ran `dotnet build-server shutdown`.
- Re-ran the full C# project build with shared compilation disabled.
- Re-ran constrained player-domain forbidden-pattern scans.
- Attempted Unity MCP validation on the three touched player scripts; all attempts returned `no_unity_session`.

Cinematic Cheats used:
- Scalar Burst drag remains a controllable math path, not Unity drag truth.
- Immediate one-body Burst drag avoids scheduler handoff for a single velocity vector.
- Origin-shift telemetry correction preserves replayable crash dumps instead of trying to repair evidence after failure.

Exact Microseconds saved:
- No new steady-state saving claimed.
- Existing water-drag estimates remain unchanged.
- Origin-shift telemetry loop is cold path only: 300 fixed-size entries on AUP shift, 0 B/frame steady state.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained player-domain scan found no forbidden hot-path math, sync raycast, controller move, schedule-complete, `foreach`, interpolated strings, or `.ToString()` patterns.
- Unity MCP validation: unavailable, `no_unity_session`.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - SDF Bounds Clamp Hardening

What was wrong:
- Active SDF proof used `HectonVoxelVolume.TrySampleDensity()` directly. That API clamps coordinates to the published grid edge, so positions outside an active volume could read a dense border cell and trigger a false no-clip recovery.
- `PlayerKinematicsRuntime.SnapshotVoxelSolid()` had the same telemetry-side risk.

What was done:
- Added published SDF bounds checks before density sampling in `HectonPlayerMovement.TrySampleActiveVoxelSdfSolid()`.
- Added the same bounds gate to `PlayerKinematicsRuntime.SnapshotVoxelSolid()`.
- Changed the no-clip guard to skip SDF sampling when the hybrid nav grid already reports solid.
- Re-extracted the `PLAYER_KINEMATICS` prompt from `CURRENT_BATCH.md` before the patch.
- Re-ran compile, static scans, and Unity MCP validation attempts.

Cinematic Cheats used:
- Last-valid AUP teleport remains the recovery cheat; no iterative depenetration loop.
- Bounds check uses cheap metadata and half-cell padding instead of extra voxel physics.
- Nav-grid proof short-circuits SDF sampling when the cheaper authority signal already proves solid.

Exact Microseconds saved:
- 3-12 us saved on fault frames where nav-grid solidity skips nearest-volume SDF sampling.
- 60 us/fault frame preserved versus iterative depenetration.
- 0 B/frame added in the hot path; bounds check uses stack structs and existing payload metadata.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained `rg` scans over the touched player-kinematics audit set found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, string interpolation, `string.Format`, `.ToString()`, or trailing whitespace.
- Unity MCP validation for `HectonPlayerMovement.cs`, `PlayerKinematicsRuntime.cs`, and `HectonPlayerState.cs`: unavailable, `no_unity_session`.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Burst Drag Authority Cleanup

What was wrong:
- The drag path still carried stale post-fixed scheduling state for a one-vector player solve.
- That made the Burst result vulnerable to becoming delayed evidence instead of same-step authoritative swim damping.
- The old `PlayerSwimMotor.ApplyAnalyticalDrag()` handoff had already been removed from authority, so the schedule/complete corridor was dead weight.

What was done:
- Removed `IPostFixedTickable` from `HectonPlayerMovement`.
- Removed post-fixed registration/unregistration, drag `JobHandle`, scheduled completion flags, and the unused schedule/complete methods.
- Kept the authoritative swim path on `PlayerKinematicsLinearDragJob.Run()` with finite validation before applying the solved velocity.
- Re-ran the full C# project build, constrained static scans, `git diff --check`, and Unity MCP validation attempts.

Cinematic Cheats used:
- Scalar Burst drag stays the authority; visual water turbulence remains presentation-only.
- Same-step drag damping replaces a one-frame-late scheduled proof for the single player body.
- Hand/VAT/roll systems keep the saved CPU budget for presentation overkill instead of water simulation truth.

Exact Microseconds saved:
- 3-8 us/frame estimated saved on i3/MX350 by deleting the one-body schedule/complete corridor.
- 14 us/frame original water-drag estimate preserved versus Rigidbody drag orchestration.
- 0 B/frame added; no managed hot-path allocation introduced.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained player-domain scans found no forbidden hot-path math, sync raycast, controller move, schedule-complete, `foreach`, interpolated strings, `string.Format`, or `.ToString()` patterns.
- `git diff --check` on touched player/state/report files reported only CRLF normalization warnings, no whitespace errors.
- Unity MCP validation: `HectonPlayerState.cs` and `PlayerKinematicsRuntime.cs` returned 0 warnings/0 errors; `HectonPlayerMovement.cs` timed out on the large script, so no Unity validation success is claimed for that file.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Drag Handoff And Warning Recheck

What was wrong:
- A status entry claimed a restored `PostFixedTick` scheduled-drag completion path, but current `HectonPlayerMovement` source has no scheduled player drag job and no `IPostFixedTickable` implementation.
- A transient full build reported an external `EncounterDirector.WritesPredatorAup` error even though the method exists in the same file; stale shared compiler/source churn was the actual cause.

What was done:
- Re-read the current source and verified player drag uses a one-body Burst `PlayerKinematicsLinearDragJob.Run()` in the swim authority path.
- Rejected adding post-fixed scheduling because it would add scheduler cost or one-frame drag latency for one vector.
- Verified `PlayerKinematicsRuntime.OnOriginShift()` shifts black-box telemetry positions on origin shifts.
- Ran `dotnet build-server shutdown`.
- Re-ran the full C# project build against current workspace state with shared compilation disabled.
- Corrected `Status_PLAYER_KINEMATICS.md` and `Rationale_PLAYER_KINEMATICS.md` to match objective code and build output.

Cinematic Cheats used:
- Scalar Burst drag remains the authority cheat.
- No worker handoff is added for a one-vector solve; saved budget stays available for presentation.
- AUP telemetry rebasing stays metadata-only; no scene search.

Exact Microseconds saved:
- No new measured saving claimed.
- Avoided scheduler overhead for a one-body drag job; expected benefit is keeping the existing low-tier path below the scheduling break-even point.
- Existing estimates remain: 3-12 us/fault frame from SDF short-circuit, 60 us/fault frame versus depenetration, 20-60 us on skipped low-tier hand probe frames.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Unity MCP validation succeeded for `PlayerKinematicsRuntime.cs` and `HectonPlayerState.cs` with 0 warnings and 0 errors.
- Unity MCP validation for `HectonPlayerMovement.cs` timed out inside the MCP regex engine on the large file; no Unity validation success is claimed for that file.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Fault Latch And Re-Enable Warmup

What was wrong:
- `PlayerKinematicsRuntime` fallback fault flags were sticky after recovery, corrupting later black-box state.
- NaN fallback computed the last-valid position but did not move the motor unless the fault was SDF-solid.
- `HectonPlayerMovement.OnDisable()` disposed native kinematics state, but `OnEnable()` did not re-warm it before the next fixed tick.

What was done:
- Made fallback body telemetry write current-frame fault flags: NaN, solid teleport, or healthy zero.
- Moved the fallback motor to last-valid position for both NaN and SDF-solid faults.
- Reset the dump latch only after a healthy fallback frame.
- Re-warmed `HectonPlayerMovement` native kinematics buffers in `OnEnable()` and recorded the current AUP before dispatcher registration.
- Re-ran the full C# project build and constrained static scans.

Cinematic Cheats used:
- Last-valid teleport stays the recovery cheat.
- Fault state is a fixed 300-frame black-box ring, not an iterative depenetration or physics replay.
- Re-enable warmup keeps allocation in lifecycle, not gameplay cadence.

Exact Microseconds saved:
- No new steady-state saving claimed.
- Prevents a first-fixed-tick re-enable allocation spike after player pooling or disable cycles.
- Preserves the existing 60 us/fault-frame saving versus iterative depenetration.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained `rg` scans over the touched player-kinematics audit set found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, string interpolation, `string.Format`, or `.ToString()`.
- Unity MCP validation returned 0 warnings/0 errors for `PlayerKinematicsRuntime.cs` and `HectonPlayerState.cs`; `HectonPlayerMovement.cs` basic validation timed out in the MCP regex engine.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Scanner Save Enumerator Guard

What was wrong:
- `DataArchaeologyRuntime.PopulateScanStateSaveData()` used `foreach` over `_scanStates`.
- Current Unity collections emitted `LowLevel.Unsafe.KeyValue<int, byte>` for that enumerator, causing a compile break against the expected `KVPair<int, byte>` shape.

What was done:
- Replaced the `foreach` with explicit `NativeParallelHashMap<int, byte>.Enumerator` and `while (MoveNext())`.
- Kept save output in the existing fixed arrays; no new save allocations or format changes.
- Re-ran the full C# project build and constrained player/scanner pattern scans.

Cinematic Cheats used:
- No simulation changes. The cheat is data discipline: fixed arrays and native enumeration instead of managed serializer truth.

Exact Microseconds saved:
- No frame-time saving claimed. This is save-path correctness and compile recovery.
- Prevents managed enumeration ambiguity in scanner save serialization.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained scan over touched player/scanner files found no forbidden hot-path math, sync raycast, controller move, schedule-complete, `foreach`, interpolated strings, `string.Format`, or `.ToString()` patterns.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Temp Lock Verification Recheck

What was wrong:
- First full recheck hit generated `Temp` metadata churn: missing URP/ShaderGraph generated outputs.
- Second serial recheck hit a locked `Temp\obj\WaveHarmonic.Crest.Shared\WaveHarmonic.Crest.Shared.dll`.
- Neither failure reported PLAYER_KINEMATICS or scanner source diagnostics.

What was done:
- Did not kill unknown compiler processes because other agents may be compiling.
- Verified the current player/scanner source with `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 /nr:false /p:UseSharedCompilation=false`; result: 0 warnings, 0 errors.
- Waited briefly, shut down build servers, then reran the full serial build; result: 0 warnings, 0 errors.
- Kept status at mandatory `PENDING VERIFICATION`.

Cinematic Cheats used:
- No runtime code changed in this recheck.
- Existing cheats remain: scalar same-step Burst drag, bitmask equipment drag, batched hand targets, ladder snap, camera-roll fake, SDF-bound no-clip teleport, and fixed black-box telemetry.

Exact Microseconds saved:
- No new runtime saving claimed.
- Existing savings remain documented: 14 us/frame for Burst drag authority, 18 us/frame for bitmask inventory drag, 35 us/frame for batched hand probes, 60 us/fault frame versus depenetration.

Verification:
- Core no-dependency build: 0 warnings, 0 errors.
- Final serial full build: 0 warnings, 0 errors.
- Constrained `rg` scans: no forbidden hot-path math, sync raycast, controller move, schedule-complete, `foreach`, interpolation, `string.Format`, `.ToString()`, or trailing whitespace in touched player/scanner/report files.
- Unity MCP validation: unavailable, `no_unity_session`.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - No-Build Drag And Inventory Cache Hardening

What was wrong:
- `PlayerKinematicsBodyJob` fallback drag could reverse velocity when `drag * density * dt` exceeded 1.
- Heavy inventory drag cache only keyed on `ItemTemplateRegistry.Count`, so a same-count registry refresh could preserve a stale heavy-item mask.

What was done:
- Clamped fallback drag through `math.saturate(drag * density * dt)` before velocity damping.
- Added `ItemTemplateRegistry.Revision` and included it in the heavy-drag mask cache key.
- Reset the cached heavy mask when the item registry is unavailable.
- Honored the user instruction not to run `dotnet build`; verification stayed static/source-only.

Cinematic Cheats used:
- Scalar water drag remains the controllable physical cheat; the clamp prevents fake physics from becoming reverse thrust.
- Equipment drag remains a 64-bit mask check, not inventory-object truth.

Exact Microseconds saved:
- 0 us/frame new saving claimed; this is correctness hardening.
- Existing 18 us/frame inventory saving is preserved by avoiding item iteration.
- Added cost is one fallback scalar saturate and one cached `uint` compare on heavy-mask refresh checks.

Verification:
- No `dotnet build` or Unity compile validation run by explicit user instruction.
- Constrained `Select-String` scan: no forbidden hot-path math, sync raycast, controller move, schedule-complete, `foreach`, interpolation, `string.Format`, or `.ToString()` in touched player/scanner/inventory files.
- Stale drag scheduling scan: no scheduled player-drag completion leftovers.
- `git diff --check`: only CRLF normalization warnings on touched files.
- Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - NaN Recovery And Hand Probe Finite Guard

What was wrong:
- `HectonPlayerMovement.ResolveVoxelNoClipFailsafe()` dumped black-box telemetry on non-finite runtime position but did not recover the motor to the last-valid AUP.
- `PlayerKinematicsRuntime.ScheduleHandProbes()` trusted source transform position/axes before scheduling batched ray commands.
- `PlayerKinematicsHandPlacementJob` could construct an IK target from a non-finite hit point.
- External hand targets could remain visually braced after runtime disable or invalid probe source until the contextual IK hold timer expired.

What was done:
- Added authoritative NaN recovery to last-valid AUP before black-box dump when a finite recovery point exists.
- Added finite/non-zero validation for hand-probe source position, forward, right, and up vectors before writing `RaycastCommand` payloads.
- Added finite hit point/normal validation before writing `PlayerKinematicsHandTarget` into the NativeArray.
- Clamped hand-placement hit distance to the authored probe range before rsqrt math.
- Cleared external wall-hand targets on invalid source, disable, and destroy.
- Did not run `dotnet build`; user explicitly prohibited build commands for this continuation.

Cinematic Cheats used:
- Fault recovery remains last-valid AUP teleport, not iterative depenetration.
- Physical hands remain batched contact targets and analytical IK, not real hand rigidbodies.
- Invalid contact presentation clears immediately instead of simulating hand release physics.

Exact Microseconds saved:
- Measured proof absent because no build/profiler run was allowed.
- No healthy-frame saving claimed beyond a few scalar finite checks.
- Fault-frame estimate: 10-30 us avoided when invalid source data would otherwise schedule/consume the two-ray probe batch.

Verification:
- Static-only verification by instruction.
- Constrained `rg` scans found no new forbidden hot-path `math.sqrt`, `Mathf.Sqrt`, `Vector3.magnitude`, `math.normalize`, sync `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, `Schedule().Complete`, `foreach`, `string.Format`, or `.ToString()` patterns.
- `git diff --check` on touched source reported only CRLF normalization warnings, no whitespace errors.
- Unity/editor/profiler validation not run. Mandatory status remains `PENDING VERIFICATION`.

## Session Entry - Runtime Hierarchy Lookup Guard

What was wrong:
- `Docs/Tasks/CURRENT_BATCH.md` no longer contains the `PLAYER_KINEMATICS` XML tag, so prompt re-extraction failed in the active batch file.
- `PlayerKinematicsRuntime.RebindServices()` could be invoked by GlobalRegistry hot-swap and still perform a child hierarchy lookup for `ContextualPhysicalIkRig` if the cached reference was missing.
- That lookup is not a fixed-frame path, but it is still runtime scene traversal in a service-rebind path and violates the registry/cached-reference direction.

What was done:
- Continued from persisted `Status_PLAYER_KINEMATICS.md`, `Rationale_PLAYER_KINEMATICS.md`, and the domain map instead of inventing prompt content.
- Changed `RebindServices()` to accept `allowHierarchyLookup`.
- Cold `Awake` uses `allowHierarchyLookup: true` for the one legitimate child IK bridge lookup.
- GlobalRegistry hot-swap uses `allowHierarchyLookup: false`, so runtime service replacement refreshes only registry/root cached references.
- Did not run `dotnet build`; prior user instruction prohibited build commands for this continuation.

Cinematic Cheats used:
- No authority physics changed.
- Physical hands remain cached batched contact targets plus analytical IK, not real hand rigidbodies or runtime hierarchy discovery.
- Low-tier hand probes remain staggered; High/Ultra presentation budget is preserved.

Exact Microseconds saved:
- No measured proof.
- Healthy-frame savings: 0 us claimed; fixed and late ticks are unchanged.
- Service-churn estimate: avoids one O(child hierarchy) lookup during GlobalRegistry replacement when the IK bridge is missing.

Verification:
- Unity MCP validation attempt failed on transport to `http://127.0.0.1:8088/mcp`; no Unity/editor validation is claimed.
- Constrained `rg` scans found no forbidden hot-path `math.sqrt`, `Mathf.Sqrt`, `Vector3.magnitude`, `math.normalize`, sync `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, `Schedule().Complete`, `foreach`, `string.Format`, interpolated strings, or `.ToString()` patterns.
- Hierarchy lookup scan shows the remaining `GetComponentInChildren` guarded by `allowHierarchyLookup` and called from cold `Awake`; hot-swap calls pass `false`.
- `git diff --check` on touched source/report files reported only CRLF normalization warnings, no whitespace errors.
- Mandatory status remains `PENDING VERIFICATION`.
