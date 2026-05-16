# Rationale_FOVEATED_RENDER_COMMANDER

Prompt: FOVEATED_RENDER_COMMANDER
Domain: GRAPHICS/VR
State: PENDING VERIFICATION / FINAL_VALIDATION BLOCKED BY DEPENDENCY

## Session Hygiene
Problem: Required status and rationale files were missing at session start.
Solution: Create current-batch evidence files before code edits.
Rejected Alternatives: Proceeding chat-only; violates batch logging protocol.
Scalability potential: Evidence files preserve decisions across context compression and parallel agent work.
Hardware Impact: 0 us runtime impact on i3/MX350; documentation-only.

## Hardware Foveation API Surface
Problem: The prompt demands Unity 6 OpenXR foveation, but the project manifest has no `com.unity.xr.openxr` package; direct package calls would add a compile dependency that is not present.
Solution: Use Unity core XR foveation surface: `SystemInfo.foveatedRenderingCaps`, `XRDisplaySubsystem.foveatedRenderingFlags`, `XRDisplaySubsystem.foveatedRenderingLevel`, and `TryGetAppGPUTimeLastFrame`. This satisfies Unity 6 hardware VRS/FFR without introducing a package reference.
Rejected Alternatives: `UnityEngine.XR.OpenXR` feature calls; custom render-target edge downscale; shader-only radial blur masquerading as VRS.
Scalability potential: Low = fixed FFR high on Quest 2-class hardware. Middle = stress-mapped Low/Med/High FFR. High = PC VR gaze-allowed VRS when fixation exists. Ultra = hardware gaze VRS plus saved fill-rate spent on denser noir lighting and post passes.
Hardware Impact: i3/MX350 flat-screen path is disabled, so 0 us cost. Quest 2 expected GPU fill-rate gain is 0.2-1.0 ms when caps are honored. RTX/eye-tracked PC VR benefit depends on runtime driver; CPU policy sample remains under 15 us.

## PC God-Mode Fixed-Foveation Suppression
Problem: The commander mapped no-pressure PC VR to Low fixed foveation when hardware caps existed but eye tracking was absent. That is acceptable as a pressure fallback, but it is mobile-style quality loss on High/Ultra rigs when there is no stress.
Solution: Read `GlobalRegistry.ScalabilityTier`; on High/Ultra, if there is no gaze, no thermal pressure, and no system pressure, target level is forced to 0 and the hardware state is cleared. Gaze-tracked PC VR and pressure-driven fallbacks still work.
Rejected Alternatives: Applying fixed FFR at all times on 4090-class VR, disabling all PC VR foveation including gaze, or adding a new signal for a policy that can be derived from existing tier/stress data.
Scalability potential: Low remains the fixed foveation fake. Middle keeps stress-tiered FFR. High/Ultra spend pixels when affordable and only use VRS when gaze or pressure justifies it.
Hardware Impact: Exact measured savings remain 0. Estimated CPU change is under 1 us per policy sample; image-quality gain is qualitative until VR captures are available.

## Signal-Decoupled Stress Policy
Problem: Foveation must react to project-wide pressure without directly depending on Homeostasis internals or a singleton manager.
Solution: Consume `SignalBus<SystemHealthSignal>` and `SignalBus<ThermalStateChangedSignal>` through `ReadOnlySpan<T>`, cache `IHardwareThermalService` from `GlobalRegistry`, and only commit XR display state when the desired level or flags change.
Rejected Alternatives: Per-frame polling of arbitrary managers, `UnityEvent`, `Action<T>`, or inspector-only quality curves.
Scalability potential: Low = High FFR when stress/thermal pressure rises. Middle = Low/Med/High scalar tiers. High = GPU app time pressure participates. Ultra = same policy leaves budget for overkill visuals instead of simulation noise.
Hardware Impact: Expected CPU work is 2-8 us per dispatcher tick; XR subsystem write happens every 30 frames by default or on forced state change.

## UI Legibility Fail-Closed
Problem: Hardware VRS/FFR can damage text and HUD edges when a UI layer is rendered through the same XR camera.
Solution: Subscribe to SRP begin/end camera rendering. If a camera culling mask includes the configured UI layer, set foveation level 0 for that camera and restore the target state only after the last matching UI camera exits.
Rejected Alternatives: Trusting all UI to render on a separate overlay, compensating with text shaders, leaving edge text blurred, or using a single boolean latch that can restore too early during nested camera rendering.
Scalability potential: Low = legible UI wins over fill-rate saving. Middle = only UI cameras pay the foveation toggle. High = world cameras remain foveated. Ultra = overlay cameras can later move to platform compositor layers without changing this commander.
Hardware Impact: 3-12 us CPU per UI camera in worst case; saves QA time by making text degradation deterministic instead of runtime-dependent.

## GPU Pressure Freshness
Problem: The display apply path previously returned early when the desired foveation state was unchanged. That avoided redundant XR writes but also let `TryGetAppGPUTimeLastFrame` go stale, weakening thermal/homeostasis escalation.
Solution: Always enumerate running `XRDisplaySubsystem` entries on the policy sample, sample GPU app time, and only write flags/level if the target changed or the display drifted from the target.
Rejected Alternatives: Rewriting XR foveation state every sample, ignoring GPU timing after first apply, or moving GPU pressure polling into a separate manager.
Scalability potential: Low devices keep cheap fixed FFR. Middle and High devices get current pressure data without display-state churn. Ultra still avoids pointless XR writes while preserving gaze VRS policy.
Hardware Impact: Expected CPU stays in the 2-8 us policy-sample range; measured microseconds saved remain 0 until the global build is fixed and VR profiling can run.

## Thermal Recovery And XR NaN Hardening
Problem: Thermal severity was accumulated with a max latch, so one throttling event could force high foveation forever. XR display level reads also needed a harder guard because a provider returning NaN must not poison telemetry or shader globals.
Solution: Recompute thermal severity from the current frame's thermal signals plus the hardware service snapshot when either exists, allowing lower severity to recover. Sanitize target and display levels, mark non-finite display state, write the fault frame, suppress active shader-global reporting for the invalid state, dump blackbox, and clear hardware foveation.
Rejected Alternatives: Permanent thermal fail-high latch, trusting XR provider state, or dumping evidence without disabling the bad hardware foveation state.
Scalability potential: Low tier still escalates fast under real pressure, but can return to cheaper policy after recovery. High/Ultra preserve adaptive gaze VRS instead of being stuck in mobile-style fixed high FFR forever.
Hardware Impact: Expected added CPU is under 1 us per policy sample. Exact measured savings remain 0 until the build is green and VR profiling can run.

## Quest 2 Fixed-Foveation Lock
Problem: Weak mobile VR silicon burns fill-rate at lens edges and should not spend CPU/GPU time searching for perfect per-frame foveation.
Solution: Detect Android XR Quest/Oculus runtime through XR active state, memory gate, and device tokens without requiring Vulkan classification; explicitly exclude Quest 3/Quest Pro before the memory-gate fallback; lock target level to High while preserving PC/Quest 3 dynamic behavior. Actual hardware writes remain gated by Unity foveation caps.
Rejected Alternatives: Thermal-only escalation, per-frame adaptive edge mask, assuming eye tracking exists on low-tier devices, Vulkan-only Quest 2 detection, or letting reserved-memory reporting downgrade Quest 3/Pro into the Quest 2 policy.
Scalability potential: Low = toaster path is the cheapest constant approximation. Middle = dynamic stress mapping. High = gaze allowed. Ultra = saved cycles buy higher visual density.
Hardware Impact: Quest 2-class path costs under 3 us per sample and can recover hundreds of GPU microseconds to more than 1 ms depending scene fill-rate.

## Blackbox Crash Evidence
Problem: Hardware foveation failures can be driver/runtime specific; chat reports cannot explain NaN, invalid eye descriptors, or flag transitions after context compression.
Solution: Store the last 300 frames in `GlobalDataVault` as `BufferID.FoveatedRenderBlackBox`, resolved through `VaultBufferHandle<FoveatedRenderTelemetryEntry>`, and dump `Docs/AgentLogs/Dump_FOVEATED_RENDER_COMMANDER.bin` on non-finite state or explicit request. Dump records are padded to 64 bytes to match the pack-1 telemetry struct size.
Rejected Alternatives: Private persistent `NativeArray`, managed `List<T>`, text logs every frame, unpadded binary records, or relying on Unity console history.
Scalability potential: Low = fixed 19.2 KB vault memory with no growth. Middle/High/Ultra = same blackbox supports richer postmortem without runtime allocations.
Hardware Impact: 1-5 us CPU per tick; fixed vault footprint, no GC.

## Data Sovereignty Rework
Problem: The first implementation owned a private persistent telemetry `NativeArray`. That was sentinel-tracked but still violated the project H-Phi rule that persistent data belongs in the vault.
Solution: Added the narrow cross-domain buffer ID `FoveatedRenderBlackBox = 129` and made the commander stateless over a vault handle. The telemetry struct now uses `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]` for ARM64/Quest layout predictability.
Rejected Alternatives: Keeping the private `NativeArray`, adding an ad hoc graphics registry, or inventing a duplicate foveation signal.
Scalability potential: Low devices get one fixed 19.2 KB aligned heartbeat ring; High/Ultra can read the same vault buffer for diagnostics without adding extra foveation-owned allocations.
Hardware Impact: Private allocation removed. DataVault resolution adds a few scalar checks, estimated 0-1 us over the previous direct array write, buying leak ownership and alignment guarantees.

## Legacy Quest Enforcer Quarantine
Problem: `Assets/_Project/Scripts/Core/OculusFfrEnforcer.cs` still contained a second hardware foveation writer, a private persistent `NativeArray` blackbox, a managed XR-active event subscription, and a Quest texture mip clamp. It was unreferenced, but if activated it would fight the graphics/VR commander and violate DataVault ownership.
Solution: Kept `QuestVulkanRuntimePolicy` because the commander uses it for runtime classification, and reduced `OculusFfrEnforcer` to a disabled obsolete compatibility shim so old serialized components do not become missing scripts.
Rejected Alternatives: Leaving duplicate direct foveation writes in Core, deleting the MonoBehaviour class and risking missing-script fallout, or migrating another blackbox when the correct owner is already `FoveatedRenderCommander`.
Scalability potential: Low Quest policy remains centralized in the commander. High/Ultra PC/Quest 3 gaze VRS cannot be overwritten by a stale Quest-only component.
Hardware Impact: Removes a potential 60-frame duplicate XR subsystem scan and one 300-entry private native allocation if the legacy component is accidentally enabled. Exact measured savings remain 0 until runtime profiling is possible.

## Duplicate Commander Safety
Problem: The duplicate guard used `Destroy(gameObject)`, which could delete an entire scene rig if a duplicate commander was added to an existing XR/graphics object.
Solution: Duplicate detection now destroys only the duplicate component with `Destroy(this)`, preserving the host GameObject and unrelated components.
Rejected Alternatives: Leaving the whole-object destroy path, allowing multiple commanders to race over XR foveation state, or adding another singleton-style manager.
Scalability potential: Scene composition stays safe across Quest, PC VR, and editor test rigs; only the authoritative commander writes hardware state.
Hardware Impact: 0 us steady-state. The fix prevents catastrophic scene-object loss, not a measurable frame-time gain.

## Compile Wall Boundary
Problem: Build attempts failed before final green validation because other agents changed files outside the `Graphics/VR` domain.
Solution: Do not edit or revert those files except narrow foveation-interface cleanup required by this domain. Current blocker set is 16 external errors: `SargassumMicroFaunaBoids.cs` missing `EnsureVaultBufferHandle`, `VehicleDockingModule.cs` missing `CacheFluidRuntime`/`ResetDockingRuntimeCaches`, and `HectonMarineSnowRenderer.cs` missing `_vehicleWakeJobResult`/`_telemetryRing`.
Rejected Alternatives: Scope creep into Gameplay/Core, reverting other agents' dirty work, or claiming green build from partial evidence.
Scalability potential: Keeps the VR commander isolated and ready for integration once compile owners repair their domains.
Hardware Impact: 0 us runtime; integration dependency only.

## Omega Polish Inquisition
Problem: The final polish mandate requires verified master grade, but the global build is red outside the assigned graphics/VR domain.
Solution: Perform anti-bloat source audit anyway: no per-frame managed collections, no `Update()`, no object find calls, no LINQ, no manual render-target edge downscale, no VRS singleton dependency, and no stale GPU-pressure latch.
Rejected Alternatives: Claiming full green validation despite external compiler errors; editing `GlobalSignals.cs` or `PlayerKinematicsRuntime.cs` from the VR task.
Scalability potential: Low tier remains a constant fixed-foveation fake; Middle responds to stress; High/Ultra use gaze-allowed VRS when hardware supports it. The same code path scales without branch-heavy shader or render-target bloat.
Hardware Impact: 0 B/frame GC by static audit. Expected saved GPU time remains estimate-only until the project compiles and VR profiling can run.

## Multiplatform Inquisition
Problem: VR foveation has to survive Quest/Android ARM64, Metal/Mac, Steam Deck storage pressure, and PC high-end without collapsing into one middle-ground policy.
Solution: ARM64 gets pack-1 64-byte telemetry and vault alignment; Metal/Mac receive no shader or compute-thread assumptions; Steam Deck avoids normal-runtime disk I/O; PC VR keeps gaze-allowed VRS instead of mobile fixed foveation when eye data exists.
Rejected Alternatives: Treating Quest policy as universal, adding shader/compute overkill inside the foveation commander, or dumping telemetry continuously to disk.
Scalability potential: Toaster path is constant fixed FFR. Middle path is stress-tiered FFR. High/Ultra path is gaze-allowed VRS plus shader-global reporting so saved cycles can be spent by downstream visual systems.
Hardware Impact: No measured GPU savings yet. Static estimates remain Quest 2 200-1000 us GPU recovery on fill-rate-bound frames, PC VR hardware dependent, CPU policy under 15 us per sample.
