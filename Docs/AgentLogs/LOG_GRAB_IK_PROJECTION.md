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
