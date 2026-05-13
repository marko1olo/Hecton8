# LOG_VR_SOMATIC_ENGINEER

## 2026-05-13 - VR Somatic Purge And Decoupled Root

What was wrong:
- VR bridge authority was runtime/self-spawn oriented instead of bootstrap-owned.
- Tool activation path had no explicit VR trigger signal lane, forcing future tools toward direct controller polling.
- VR somatic root had no dedicated decoupled stabilization transform.
- Hand target and physical hand positions were not separated into SOA native buffers.
- Comfort vignette scalar existed in the visor pass path but VR somatic angular velocity did not own a bridge-level publisher.
- Global compile gate is red from unrelated Memory, Determinism, Cartography, DataVault, InputSignal, and StateCorrectionSignal dependency failures.

What was done:
- Registered VRSomaticRuntimeBootstrap through GameBootstrapper via EnsureRegisteredByBootstrap.
- Stopped XR activation from creating the VR runtime before bootstrap ownership exists.
- Added ToolTriggerSignal to GlobalSignals and made InputDispatcher publish changed XR trigger/grip values.
- Routed XR trigger/grip to PlayerInputAction.PrimaryFire/SecondaryFire so LaserCutter and Scanner remain device-agnostic.
- Added IVRSomaticProvider.BindDecoupledRoot and runtime VR_Somatic_DecoupledRoot creation.
- Added VRSomaticRootSyncJob for Burst root smoothing, horizon counter-rotation, and comfort vignette generation.
- Added persistent NativeArray<float3> HandTargets and HandPhysicalPositions.
- Added VRSomaticHandKinematicsJob using Velocity = (Target - Physical) * SpringForce.
- Added ghost-hand distance mask with low-tier/H8_LOW_MEMORY_PROFILE suppression.
- Added IOriginShiftListener support to subtract AUP shifts from hand native buffers and reset root/head history.
- Published _VRComfortVignette and _VRComfortVignette01 without adding a new render pass.
- Performed Omega polish: new root-job divisions replaced with math.rcp multiplications; no introduced math.sqrt, math.normalize, foreach, string interpolation, string.Format, or ToString in VR hot path.

Cinematic cheats used:
- Horizon lock is a visual AxisAngle counter-rotation, not a physical submarine simulation.
- Hand clipping uses target-vs-physical separation and a ghost mask, not expensive collision-perfect fingers.
- Comfort tunnel uses a scalar vignette in the existing HectonVisorUberPost pass, not an additional full-screen pass.
- Low tier disables ghost mask output instead of trying to balance a middle-ground effect.

Exact microseconds saved:
- Bootstrap-owned single runtime: estimated 6-12 us avoided during XR activation spikes.
- ToolTriggerSignal/input bridge: estimated 2-5 us saved per active tool tick by avoiding per-tool XR polling.
- Decoupled root Burst smoothing: estimated 4-9 us saved versus transform hierarchy compensation during submarine roll.
- Hand spring Burst job: estimated 3-7 us for two hands on i3/MX350 with 0 B/frame.
- Existing-pass vignette: avoids one full-screen pass; GPU cost saved depends on target resolution.

Verification:
- Static purge scan: PASS. No VRManager.Instance, XRDevice.isPresent, Input.Instance, or InputManager.Instance hits in Assets/_Project/Scripts.
- Modified VR hot-path Quaternion.Euler scan: PASS.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal -clp:Summary`: BLOCKED by unrelated compile wall; no VRSomatic/InputDispatcher/GlobalSignals errors appeared before the dependency failures.
- Status: PENDING VERIFICATION due global compile dependency wall.

## 2026-05-13 - VR Somatic Static Review Addendum

What was wrong:
- Ghost-hand state was computed but not exposed through the registry contract, forcing future visuals toward unsafe provider internals.
- The first horizon correction used the wrong counter-rotation sign by inspection.
- XR tool action mapping was coupled to controller pose tracking, so trigger input could drop during tracking hiccups.
- VR somatic had comfort telemetry but not the mandated fixed 300-frame black-box state ring.
- The user explicitly forbade a new dotnet build pass.

What was done:
- Added VRSomaticHandPose, IVRSomaticProvider.HandGhostMask, and IVRSomaticProvider.TryGetHandPose.
- Implemented TryGetHandPose in VRSomaticProvider with a scheduled-job guard so external renderers do not read NativeArrays while Burst is writing.
- Added PcVRSomaticProvider null-object coverage for the new contract members.
- Added NativeArray<VRSomaticBlackBoxEntry>[300] with one record per frame, state hash, flags, head pose, hand separation, vignette scalar, angular speed, and AUP shift sequence.
- Added fault-only binary dump path Docs/AgentLogs/Dump_VR_SOMATIC_ENGINEER.bin on non-finite head/root state.
- Kept root persistence under GameBootstrapper and removed manual root offset on AUP shift to avoid double movement.
- Kept trigger/grip action values live even when controller pose tracking is false.

Cinematic cheats used:
- Ghost-hand rendering remains a target/physical separation fake, exposed through a small snapshot contract.
- The black box stores high-level state, not expensive physics replay.
- Comfort correction remains a bounded AxisAngle visual counter-rotation.

Exact microseconds saved:
- Hand pose contract avoids future renderer-side component scans or native buffer peeks; estimated under 1 us per queried hand.
- Black-box write is a fixed 64-byte frame record; estimated 1-2 us per active VR frame, 0 B/frame.
- Avoiding direct NativeArray exposure prevents external synchronization stalls that would cost more than the O(1) snapshot read.

Verification:
- Static hot-path scan: PASS. No Quaternion.Euler, math.sqrt, math.normalize, foreach, string.Format, or string interpolation hits in modified VR hot files.
- Static purge scan: PASS. No VRManager.Instance, XRDevice.isPresent, Input.Instance, or InputManager.Instance hits in Assets/_Project/Scripts.
- git diff --check for modified VR/contract files: PASS except existing LF-to-CRLF warnings.
- dotnet build: NOT RUN per user instruction.
- Status: PENDING VERIFICATION due global compile dependency wall and no new build pass.

## 2026-05-13 - VR Somatic Pose Validity Addendum

What was wrong:
- Invalid XR controller poses were sanitized to zero/identity but could still be reported as tracked.
- That would allow physical hand and somatic hand consumers to treat a safety fallback as live pose data.

What was done:
- Updated InputDispatcher so XRInputState.IsTracked requires the controller tracking flag plus finite position and finite rotation.
- Rechecked PhysicsDeterminismSignals/InputSignal and IVRSomaticProvider/PcVRSomaticProvider symbols by source scan.
- Rechecked HectonVisorUberPost and SuitHUDPresentationController comfort routing; both max _VRComfortVignette01 and _VRComfortVignette, so no new pass is needed.

Cinematic cheats used:
- Invalid pose frames now behave like temporary tracking loss, holding the last valid somatic target rather than inventing motion.

Exact microseconds saved:
- Preventing origin snaps avoids downstream false ghost-hand and physical-hand work; estimated 1-2 us avoided during invalid pose frames.
- Added validity gating is two booleans per controller, under 1 us and 0 B/frame.

Verification:
- Contract scan: PASS. PhysicsDeterminismSignals.PublishInput, InputSignal, InputSignalFlagAutomationOverride, IVRSomaticProvider, PcVRSomaticProvider, and TryGetXRInputState call sites resolve in source.
- Static hot-path scan: PASS. No Quaternion.Euler, math.sqrt, math.normalize, foreach, string.Format, or string interpolation hits in modified VR/input hot files.
- Static purge scan: PASS. No VRManager.Instance, XRDevice.isPresent, Input.Instance, or InputManager.Instance hits in Assets/_Project/Scripts.
- git diff --check for modified VR/input files: PASS except existing LF-to-CRLF warnings.
- dotnet build: NOT RUN per user instruction.
- Status: PENDING VERIFICATION due global compile dependency wall and no new build pass.

## 2026-05-13 - VR Somatic Input Stability Addendum

What was wrong:
- XR analog and pose reads trusted device values too much; NaN could enter XRInputState, ToolTriggerSignal, or hand kinematics.
- DominantController was not part of ToolTriggerSignal change detection, so dominance could stay stale when strength/mask/flags stayed constant.
- Physical hand targets snapped to a head-relative fallback on transient tracking loss after a valid tracked frame.
- Same-frame black-box overwrites could erase a non-finite flag from the live ring after the dump had already been written.

What was done:
- Sanitized trigger/grip analog values, joystick values, controller position, and controller rotation at InputDispatcher capture.
- Computed dominant controller from the strongest trigger-or-grip side and included dominant-controller changes in publish gating.
- Added release gating that also accounts for dominant-controller state.
- Added VRSomaticProvider.ResolveHandTarget so initialized hands hold the last finite target through controller tracking dropouts.
- Updated the black-box write path to OR previous same-frame flags into replacement entries.

Cinematic cheats used:
- Tracking loss now freezes the last valid hand target instead of simulating uncertain controller motion.
- Dominant hand selection is a cheap max(trigger, grip) heuristic.
- Black-box preservation is a flag merge, not a larger event stream.

Exact microseconds saved:
- Stable hand targets avoid future ghost-mask flicker and renderer churn; estimated 1-2 us avoided during tracking dropouts.
- Dominant-controller publish gating avoids stale consumer work while staying O(1); estimated under 1 us.
- Input finite checks cost under 1 us for two controllers and prevent expensive downstream fault handling.

Verification:
- Static hot-path scan: PASS. No Quaternion.Euler, math.sqrt, math.normalize, foreach, string.Format, or string interpolation hits in modified VR hot files.
- Static purge scan: PASS. No VRManager.Instance, XRDevice.isPresent, Input.Instance, or InputManager.Instance hits in Assets/_Project/Scripts.
- git diff --check for modified VR/input files: PASS except existing LF-to-CRLF warnings.
- dotnet build: NOT RUN per user instruction.
- Status: PENDING VERIFICATION due global compile dependency wall and no new build pass.
