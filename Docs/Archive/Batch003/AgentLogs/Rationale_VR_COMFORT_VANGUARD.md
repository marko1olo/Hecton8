# Rationale_VR_COMFORT_VANGUARD

Status: PENDING VERIFICATION

## Initial Authority
Problem: VR movement debt is split across likely bloated settings, camera FOV impact code, input/haptic hooks, and visor postprocess shader state.
Solution: Isolate comfort as data-driven presentation logic: velocity and angular jerk in zero-GC tick state, shader scalar for vignette, haptic command submission through existing abstractions when available, and telemetry ring entries where existing blackbox interfaces allow it.
Rejected Alternatives: Rigidbody lookup, `Camera.fieldOfView` animation, new blit passes, singleton access, and per-frame Unity object search. These violate zero-GC, registry, and stencil masking mandates.
Scalability potential: Low uses scalar smoothing and optional baked mask. Middle keeps procedural shader vignette. High adds stronger comfort response and more stable horizon/UI presentation. Ultra can spend saved budget on richer visor edge response without changing gameplay authority.
Hardware Impact: Expected low-end gain is from deleting FOV animation/lookups and avoiding extra postprocess passes. Estimated saving remains PENDING VERIFICATION until profiler/Unity logs exist; target is below 0.1ms system cost on i3/MX350.

## Decision 1 - Singleton Purge / Assembly Boundary
Problem: Prompt demanded purge of `VRComfort.Instance` and isolation into `Hecton8.VR`, but current code already uses `GlobalRegistry` for `VRSomaticProvider` and no VR comfort singleton exists.
Solution: Kept the registry-backed provider and shader-global signal path. Marked asmdef split blocked because no `Hecton8.VR.asmdef` exists and concurrent dirty changes touch shared registry/contracts.
Rejected Alternatives: Creating a new singleton facade, moving shared scripts into a new assembly without integrator ownership, or adding direct dependencies on systems other agents may still be editing.
Scalability potential: Low uses existing scalar signals. Middle keeps normal provider update. High/Ultra can add richer comfort telemetry without changing assembly references.
Hardware Impact: Avoided extra lookup/service indirection. Estimated low-end gain: 1-3us and zero hot allocation versus a find/singleton migration.

## Decision 2 - Impact FOV Purge
Problem: Camera juice still had impact and damage-driven projection FOV motion, which is a VR sickness source and conflicts with shader-owned tunneling.
Solution: Removed `_impactFovKickOffset`, impact FOV accumulation, recovery, and damage recoil. Left locomotion/sprint FOV paths intact because the prompt targeted impact animations and VR movement already suppresses juice FOV.
Rejected Alternatives: Keep impact FOV only for non-XR, or clamp it in XR. Both preserve the wrong behavioral primitive and invite reactivation.
Scalability potential: Low devices lose a jittery projection update. High/Ultra spend the saved cost on visor shader response instead of camera optics.
Hardware Impact: Estimated 5-12us saved during impact-heavy frames, plus no projection-matrix churn from impact recoil.

## Decision 3 - Rotation Jerk Culling
Problem: Headset angular spikes create neck-snapping HUD frames. Full physical camera smoothing would fight HMD authority.
Solution: Compute angular velocity, angular acceleration, and angular jerk from sanitized HMD rotation history; use jerk to damp visor/HUD rotational output and publish `_HectonVRComfortJerkState`.
Rejected Alternatives: Modify actual XR camera rotation, Rigidbody-derived angular velocity, or quaternion-heavy physics smoothing. Those break predictability or add dependencies.
Scalability potential: Low uses scalar jerk clamp only. Middle adds shader vignette contribution. High/Ultra can increase visor response richness through material parameters.
Hardware Impact: Approximate scalar math only; estimated 8us on i3/MX350, zero managed allocation.

## Decision 4 - Vignette And Shader Path
Problem: Comfort tunneling needed to avoid new passes while still responding to velocity, yaw, frame stutter, and jerk.
Solution: `HectonPlayerMovement` publishes `_VRComfortVignette01`; `HectonVisorUberPostFeature` forwards it into the existing material; `HectonVisorUberPost.shader` combines it with jerk state in the current pass.
Rejected Alternatives: New URP blit pass, Camera FOV animation, or duplicating the existing VR brownout feature.
Scalability potential: Low samples an authored `_HectonVRComfortMaskTex` when available, fallback hard mask. Middle uses procedural edge. High/Ultra can author denser comfort mask textures.
Hardware Impact: Avoided an estimated 50-120us extra fullscreen pass; shader adds a few scalar ops or one low-tier texture read.

## Decision 5 - Haptics / Telemetry / UI
Problem: Speed anchoring, blackbox evidence, and PDA safe-area behavior needed to remain decoupled from device-specific runtime code.
Solution: Speed haptics use `ToolHapticsRuntime.EnqueueCommand`; `MaxComfortVignette` and `JerkEvents` use `GlobalTelemetryBus`; PDA/HUD projection fill shrinks with comfort vignette.
Rejected Alternatives: Direct OpenXR rumble, per-frame file dumps, or a new canvas mask pass.
Scalability potential: Low uses short rumble pulses and projection shrink. Middle keeps stable center UI. High/Ultra can add richer visor edge art while keeping PDA readable.
Hardware Impact: Haptic command cost is cooldown-limited; telemetry is throttled hash-only. Estimated hot cost: 2-6us, zero GC.

## Verification Wall
Problem: Full compile verification is blocked by unrelated project state.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore` and `dotnet build Hecton8.Core.csproj --no-restore /p:BuildProjectReferences=false`; after fixing the stale impact FOV reference, the remaining blocker is `HectonNarrativeDirector.cs(1229,2): CS1513 } expected`.
Rejected Alternatives: Editing narrative/cartography/bootstrap files outside domain or reverting other agents' dirty changes.
Scalability potential: No runtime impact; integrator needs to clear unrelated compile wall before Unity verification.
Hardware Impact: Pending profiler data. Static proof remains zero-GC for newly added VR comfort hot paths.

## Decision 6 - Recheck Hardening
Problem: Final static pass found the current workspace still had a stale impact FOV path and mixed ownership risk between movement comfort and somatic comfort globals.
Solution: Deleted the remaining impact FOV constants, `_impactFovKickOffset`, trauma-side FOV writes, late FOV kick application, and reset references. Split comfort signal ownership: movement writes `_VRComfortVignette01`, somatic writes `_VRComfortVignette`, and visor/UI max-combine both. Low-tier mask sampling is guarded by `_HectonUberTextureFlags.w` so an unassigned mask uses threshold math without a texture sample. Somatic telemetry publishes from owned scalar state rather than shader readback.
Rejected Alternatives: Last-writer-wins on `_VRComfortVignette01`, shader readback inside the somatic tick, a new comfort blit pass, and runtime mask texture generation.
Scalability potential: Low uses no extra pass and no mask sample unless an authored mask is present. Middle uses procedural edge math. High/Ultra can author richer mask textures or stronger visor edge art without touching locomotion/camera authority.
Hardware Impact: On i3/MX350, stale FOV branch removal avoids 5-12us on impact frames; conditional mask sampling avoids a low-tier sample when no asset is assigned; telemetry remains stepped hash events with zero per-frame allocation.

## Decision 7 - XR Projection Firewall
Problem: Even after impact FOV purge, sprint/swim/input-reclaim FOV paths could still mutate camera projection during XR sessions.
Solution: `TriggerFOVKick`, `BeginInputReclaimFov`, and `UpdateFOV` now early-out when `HectonXRRuntimeState.IsXRActive`; `UpdateFOV` also clears local FOV blend/reclaim/swim offsets so stale non-XR transitions cannot accumulate under the headset. Shader comfort remains the VR feedback channel.
Rejected Alternatives: Clamp projection FOV to base under XR, or allow non-impact FOV to run. Both still write camera optics during VR and create a maintenance hole.
Scalability potential: Low keeps the cheapest scalar shader vignette path. Middle and High keep richer visor math. Ultra can increase shader comfort art without camera-projection risk.
Hardware Impact: Saves projection-matrix writes in XR FOV ticks and removes a VR nausea vector; estimate 2-6us avoided on frames where sprint/swim/cinematic reclaim would otherwise touch projection.

## Decision 8 - Low-Tier Mask Sample Cull
Problem: The comfort mask branch sampled `_HectonVRComfortMaskTex` whenever a mask was assigned, even when high-tier mode discarded the low-tier edge result.
Solution: Added `comfortLowTier01` and requires both low-tier mode and authored mask flag before sampling. High-tier uses procedural edge only; missing-mask low-tier uses threshold math.
Rejected Alternatives: Always bind/sample gray mask, or branch only on authored mask. Both waste a texture sample on high-tier frames.
Scalability potential: Low can still use a baked mask; Middle/High/Ultra spend no mask sample unless explicitly running low-tier mask logic.
Hardware Impact: Avoids one fullscreen texture sample per pixel on high-tier frames with a mask assigned; low-tier cost stays intentional and authored.

## Decision 9 - Frame-Rate Safety Override
Problem: The FPS<60 comfort baseline depended on normal comfort mode being active, so a settings-disabled comfort profile could drop frames in XR without forcing a safety vignette.
Solution: `UpdateVrComfortSignals` now computes frame-rate safety directly from real XR runtime state. The safety vignette can activate even when comfort/vignette settings are disabled, while sway, blur, velocity, and yaw signals remain gated behind normal comfort mode.
Rejected Alternatives: Force all comfort features on during frame drops, or respect the user toggle completely. The first reintroduces unwanted motion effects; the second violates the fail-safe requirement.
Scalability potential: Low gets the cheapest emergency tunnel. Middle/High/Ultra retain authored comfort effects only when enabled.
Hardware Impact: Adds a few scalar ops and no allocation; prevents frame-drop sickness without touching quality settings or global FPS managers.

## OMEGA POLISH CHANGES
Problem: Final anti-bloat pass needed proof that the VR comfort path did not rely on managed iteration, string formatting, exact normalization, or repeated floating divisions in hot math.
Solution: Static scans found no managed `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize` in touched comfort files. Replaced somatic head-speed, angular-speed, angular-acceleration, angular-jerk, jerk-limit, haptic-speed, movement yaw-rate, yaw-reference, and speed-reference divisions with `math.rcp` multiply forms.
Rejected Alternatives: Leave `/ safeDeltaTime` divisions because C# might optimize them, or replace the jerk calculation with a lookup table. Reciprocal multiplies are explicit and predictable; a LUT would hide a simple two-derivative scalar behind unnecessary cache pressure.
Scalability potential: Low gets scalar-only jerk/vignette math. Middle uses procedural shader edge. High uses richer comfort material response. Ultra can author mask assets and higher edge fidelity without changing gameplay authority.
Hardware Impact: Expected low-end gain is sub-micro to low microseconds per VR tick, but it removes repeated divide instructions from the comfort hot path. Zero managed allocations added.
Cinematic Cheats Used: Shader-edge vignette instead of Camera FOV; jerk culls visor/HUD presentation instead of XR camera pose; low-tier baked mask or threshold edge instead of extra pass; haptic speed anchor instead of physical simulation.
Final Git Diff Summary: `HectonVisorUberPost.shader`, `VRSomaticProvider.cs`, `HectonPlayerMovement.cs`, `CameraJuiceSystem.cs`, `HectonVisorUberPostFeature.cs`, and `SuitHUDPresentationController.cs` are modified. Diff stat at polish time: 6 files, 1602 insertions, 384 deletions, including concurrent changes already present in these shared files. Build command was not run because the latest user instruction explicitly forbids `dotnet build`; status remains PENDING VERIFICATION.

## Decision 10 - Vector Jerk And XR Pose Authority
Problem: The jerk filter still used scalar angular-speed deltas, so a fast direction reversal at similar speed could hide from the culler. Camera juice also still had procedural/seismic camera pose writes that could touch a tracked XR camera.
Solution: Replaced scalar jerk state with `float3` angular velocity and acceleration history derived from quaternion delta, then measures jerk magnitude from the `float3` second derivative. Camera juice now zeros procedural/seismic shake and returns before local position/rotation writes while XR is active. Movement snap-turn fade is cleared when frame-rate safety is active without normal comfort mode.
Rejected Alternatives: Keep scalar jerk as cheaper, or scale camera shake in XR. Scalar jerk is not the requested math; scaled tracked-camera shake is still authority theft from the HMD.
Scalability potential: Low uses approximate vector magnitude and reciprocal math. Middle/High/Ultra retain the same vector signal and can spend visual budget in shader instead of camera transform mutation.
Hardware Impact: Adds a handful of `float3` operations, removes XR camera transform writes, and keeps zero managed allocation. Estimated net is neutral-to-positive on low-end VR because pose mutation and nausea correction work move out of the camera.
