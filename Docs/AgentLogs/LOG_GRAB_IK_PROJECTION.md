# LOG_GRAB_IK_PROJECTION

## 2026-05-16 | GRAB_IK_PROJECTION | ANIMATION_LEAD

What was wrong:
- VR hand presence had no Animation/IK-owned physical projection kernel. Controller truth could visually pass through cockpit steel.
- No `VRHandManager` singleton or first-party `Physics.SphereCast` hand snapper was found to remove.
- Global compile is currently blocked outside this domain by missing VFX wake, docking/autopilot, light shaft, lockstep, and ecosystem contract symbols.

What was done:
- Added `Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs` and `.meta`.
- Added DataVault buffer IDs for `HandTargetAUP`, `HandActualAUP`, `HandGrabState`, `HandIkTelemetryRing`, and `HandIkTelemetryCursor`.
- Added an Animation/IK vault resolver, fixed two-hand `IJob`, hand AUP structs, grab state, output pose, and 300-frame black-box telemetry.
- Implemented analytical two-bone IK using law of cosines, `FastAcos`, `math.rsqrt`, pole-plane elbow direction, and joint-limit flagging.
- Implemented AUP shift rebase on target/actual lanes using `ShiftFrameId` and `AupShiftMeters`.
- Implemented low-tier/no-VR screen-space fallback, middle-tier plane projection, high-tier/explicit SDF projection, tangent scrape haptic flag/intensity, and ghost hand output when blocked separation exceeds 0.3 m.
- Added cold-path binary dump helper for `Docs/AgentLogs/Dump_GRAB_IK_PROJECTION.bin`.
- Preserved `VR_COCKPIT_MANUAL_OVERRIDE` compatibility by not touching `OpenXRManualOverrideLever`; scrape output remains bridgeable to `HapticRequest.ChannelGearScrape`.

Cinematic cheats used:
- Plane projection replaces rigidbody hand collision for middle tier.
- Encoded SDF trilinear sampling and gradient pushout replaces synchronous casts/joints for high tier.
- Ghost hand separates player controller truth from blocked physical hand instead of forcing impossible physical penetration.
- Fast nlerp and analytical reach clamps replace Animator IK and iterative solvers.

Exact microseconds saved:
- No Animator IK pass: estimated 30-80 us/frame saved versus graph callback IK.
- No `Physics.SphereCast` hand snapping: estimated 15-40 us/frame saved for two hands in cluttered cockpit geometry.
- Low-tier no-VR fallback: estimated 12 us/frame saved by bypassing IK/SDF.
- Plane projection path: estimated 7 us for two hands versus physics query/joint solve.
- DataVault fixed two-lane state: estimated under 2 us cold handle resolve, 0 us hot allocation.
- Black-box ring write: estimated 1 us/frame; replaces unbounded `Debug.Log` fault reporting.

Verification:
- `rg` scan found no `VRHandManager` or owned `Physics.SphereCast`.
- Omega scan found no `Vector3.Lerp`, `Quaternion.Slerp`, or `math.acos` in `Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore` attempted three times. Full compile remains `[BLOCKED BY DEPENDENCY]`; filtered post-polish build scan showed no `VRPhysicalHandPresence` or `Hecton8.Animation.IK` errors.

## 2026-05-16 | GRAB_IK_PROJECTION | MULTIPLATFORM INQUISITION PASS

What was wrong:
- Hand payload structs were still packed at 4-byte boundaries. That is not acceptable for the Quest/Android ABI requirement in the follow-up directive.
- Input/output hand lanes were job references but did not have dedicated GlobalDataVault buffer IDs yet.
- The NaN fallback path could still pass a previously invalid projected target into persistent state if the output failed validation after projection.

What was done:
- Converted all hand payload structs in `VRPhysicalHandPresenceIkJobs.cs` to `Pack = 1`.
- Verified the owned IK folder has no remaining `Pack = 4`, `Vector3.Lerp`, `Quaternion.Slerp`, `math.acos`, `Physics.SphereCast`, `VRHandManager`, `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, `EventBus`, managed delegate, `new NativeArray`, or allocator ownership hits.
- Added `HandPresenceInput=190` and `HandPresenceOutput=191` to `BufferID`, then extended the cold DataVault resolver to allocate/resolve them.
- Added a duplicate BufferID scan; no numeric collision was found.
- Tightened NaN fallback so invalid projection state is replaced by the sanitized fallback output before persistent state and telemetry writes.
- Added the IK lock-state marker to the binary dump header.

Cinematic cheats used:
- No new graphics-domain overreach. Animation/IK exports high-tier SDF contact, surface normal, haptic intensity, and ghost separation for visual systems to spend on overkill without coupling shaders or particles into this assembly.

Exact microseconds saved:
- No additional measured runtime savings claimed. The pass is correctness/ABI polish.
- Hot-path I/O remains 0 us because dump serialization stays cold-path only.
- DataVault input/output lanes add no per-frame allocation; expected hot-path cost remains two NativeArray reads/writes for two hands.

Verification:
- `rg` hot-path debt scan returned no forbidden owned IK patterns.
- `git diff --check` reported only line-ending warnings for touched files.
- `dotnet build` did not complete within the timeout after the external contract churn expanded; compile remains blocked outside this domain.

## 2026-05-16 | GRAB_IK_PROJECTION | DOMAIN-WIDE H-PHI PASS

What was wrong:
- `LeviathanTerrainIkJob` had `NativeArray` lanes with existing DataVault IDs but no resolver in the owned IK assembly, making ownership implicit.

What was done:
- Added `LeviathanTerrainIkVault.TryResolveBuffers` for segment positions, previous positions, bone matrices, 300-frame telemetry ring/cursor, optional encoded SDF, and optional terrain height samples.
- Re-ran owned-folder debt scans for forbidden layout, update-loop, allocation, physics, legacy event, delegate, and Unity interpolation patterns.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore`; current failure is external to Animation/IK: `GameBootstrapper.cs` cannot resolve `Hecton8.Core.Bucketing.ModuloSimulationBucketer`.

Cinematic cheats used:
- No new simulation truth added. The resolver only makes data ownership explicit for the existing mathematical terrain-hugging fake.

Exact microseconds saved:
- No new measured runtime savings claimed. Hot-path leviathan job code is unchanged.
- Cold resolver cost is outside frame-critical IK execution.

Verification:
- Forbidden-pattern scans over `Assets/_Project/Scripts/Animation/IK` returned no hits.
- `git diff --check` reported only line-ending warnings for touched code files.
- Compile retry reports no owned IK errors; it fails on missing Core.Bucketing type resolution.
