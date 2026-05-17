# Rationale_FOVEATED_RENDER_COMMANDER

Prompt: FOVEATED_RENDER_COMMANDER
Domain: GRAPHICS/VR
State: PENDING VERIFICATION / DOTNET BUILD CLEAN / VR RUNTIME PROFILING REQUIRED

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
Solution: Consume `SignalBus<SystemHealthSignal>` and `SignalBus<ThermalStateChangedSignal>` through `ReadOnlySpan<T>` with `ref readonly` reads, cache `IHardwareThermalService` from `GlobalRegistry`, and only commit XR display state when the desired level or flags change.
Rejected Alternatives: Per-frame polling of arbitrary managers, `UnityEvent`, `Action<T>`, or inspector-only quality curves.
Scalability potential: Low = High FFR when stress/thermal pressure rises. Middle = Low/Med/High scalar tiers. High = GPU app time pressure participates. Ultra = same policy leaves budget for overkill visuals instead of simulation noise.
Hardware Impact: Expected CPU work is 2-8 us per dispatcher tick; XR subsystem write happens every 30 frames by default or on forced state change.

## Pressure Tier Mapping
Problem: `PressureLevel` and `FoveatedPressureTier` were written to telemetry and used for High/Ultra suppression checks, but the target level resolver still selected Low/Med/High from stress and thermal state only.
Solution: Feed both pressure bytes into `ResolveTargetLevelCode`. Thermal pressure, Quest 2 lock, `PressureLevel >= 3`, or `FoveatedPressureTier >= 3` resolve High; `PressureLevel >= 2` or `FoveatedPressureTier >= 2` resolve at least Medium.
Rejected Alternatives: Leaving pressure bytes as telemetry-only fields, inventing a duplicate foveation signal, polling HomeostasisBrain directly, or adding another event bus path.
Scalability potential: Low/Quest still gets fixed High. Middle reacts to project pressure lanes without a manager dependency. High/Ultra still avoid fixed FFR when no pressure or gaze exists.
Hardware Impact: Exact measured savings remain 0. Estimated CPU change is under 1 us per policy sample; GPU savings depend on pressure duration and XR hardware support.

## Interface Deduplication Audit
Problem: The foveation commander must not create a duplicate signal or duplicate buffer ID while 20+ agents are changing adjacent systems.
Solution: Reused `SystemHealthSignal.FoveatedPressureTier` and `ThermalStateChangedSignal`; audited `BufferID` values after adding `FoveatedRenderBlackBox = 129` and found no duplicate `BufferID` values.
Rejected Alternatives: Adding a new foveation signal, using legacy EventBus, or hiding telemetry in a private native allocation.
Scalability potential: Pressure policy stays connected to the existing homeostasis lane across Low/Middle/High/Ultra tiers.
Hardware Impact: 0 us runtime; audit-only.

## UI Legibility Fail-Closed
Problem: Hardware VRS/FFR can damage text and HUD edges when a UI layer is rendered through the same XR camera.
Solution: Register the commander as `IRenderable` in `GlobalRegistry.Renderables` and use the existing `RenderDispatcher`/`GlobalRenderContext` per-camera fan-out. If a camera culling mask includes the configured UI layer, set foveation level 0 before that camera renders; the next non-UI camera restores the target foveation state. Hardware clear resets the UI suppression latch and telemetry flag, and renderable registration verifies global bucket membership before trusting the local registered flag.
Rejected Alternatives: Trusting all UI to render on a separate overlay, compensating with text shaders, leaving edge text blurred, per-frame camera scans, or owning direct `RenderPipelineManager` delegate subscriptions in the VR system.
Scalability potential: Low = legible UI wins over fill-rate saving. Middle = only UI cameras pay the foveation toggle. High = world cameras remain foveated. Ultra = overlay cameras can later move to platform compositor layers without changing this commander.
Hardware Impact: 3-12 us CPU per UI camera in worst case; no per-frame camera search; saves QA time by making text degradation deterministic instead of runtime-dependent.

## GPU Pressure Freshness
Problem: The display apply path previously returned early when the desired foveation state was unchanged. That avoided redundant XR writes but also let `TryGetAppGPUTimeLastFrame` go stale, weakening thermal/homeostasis escalation.
Solution: Always enumerate running `XRDisplaySubsystem` entries on the policy sample, sample GPU app time, and only write flags/level if the target changed or the display drifted from the target.
Rejected Alternatives: Rewriting XR foveation state every sample, ignoring GPU timing after first apply, or moving GPU pressure polling into a separate manager.
Scalability potential: Low devices keep cheap fixed FFR. Middle and High devices get current pressure data without display-state churn. Ultra still avoids pointless XR writes while preserving gaze VRS policy.
Hardware Impact: Expected CPU stays in the 2-8 us policy-sample range; measured microseconds saved remain 0 until the global build is fixed and VR profiling can run.

## Thermal Recovery And XR NaN Hardening
Problem: Thermal severity was accumulated with a max latch, so one throttling event could force high foveation forever. XR display level reads also needed a harder guard because a provider returning NaN must not poison telemetry or shader globals.
Solution: Recompute thermal severity from the current frame's thermal signals plus the hardware service snapshot when either exists, allowing lower severity to recover; clear severity when the thermal service is removed or no current thermal data exists. Sanitize target and display levels, mark non-finite display state, write the fault frame, suppress active shader-global reporting for the invalid state, dump blackbox, and clear hardware foveation.
Rejected Alternatives: Permanent thermal fail-high latch, stale service-loss severity, trusting XR provider state, or dumping evidence without disabling the bad hardware foveation state.
Scalability potential: Low tier still escalates fast under real pressure, but can return to cheaper policy after recovery. High/Ultra preserve adaptive gaze VRS instead of being stuck in mobile-style fixed high FFR forever.
Hardware Impact: Expected added CPU is under 1 us per policy sample. Exact measured savings remain 0 until the build is green and VR profiling can run.

## Quest 2 Fixed-Foveation Lock
Problem: Weak mobile VR silicon burns fill-rate at lens edges and should not spend CPU/GPU time searching for perfect per-frame foveation.
Solution: Detect Android XR Quest/Oculus runtime through XR active state, memory gate, and device tokens without requiring Vulkan classification; cache classification after XR activation; explicitly exclude Quest 3/Quest Pro before the memory-gate fallback; lock target level to High while preserving PC/Quest 3 dynamic behavior. Actual hardware writes remain gated by Unity foveation caps.
Rejected Alternatives: Thermal-only escalation, per-frame adaptive edge mask, assuming eye tracking exists on low-tier devices, Vulkan-only Quest 2 detection, repeated policy-sample platform string classification, or letting reserved-memory reporting downgrade Quest 3/Pro into the Quest 2 policy.
Scalability potential: Low = toaster path is the cheapest constant approximation. Middle = dynamic stress mapping. High = gaze allowed. Ultra = saved cycles buy higher visual density.
Hardware Impact: Quest 2-class path costs under 3 us per sample and can recover hundreds of GPU microseconds to more than 1 ms depending scene fill-rate.

## Blackbox Crash Evidence
Problem: Hardware foveation failures can be driver/runtime specific; chat reports cannot explain NaN, invalid eye descriptors, or flag transitions after context compression.
Solution: Store the last 300 frames in `GlobalDataVault` as `BufferID.FoveatedRenderBlackBox`, resolved through `VaultBufferHandle<FoveatedRenderTelemetryEntry>`, and dump `Docs/AgentLogs/Dump_FOVEATED_RENDER_COMMANDER.bin` on non-finite state or explicit request. Dump records are padded to the compile-time 64-byte record-size contract used by the pack-1 telemetry struct. A cold `UnsafeUtility.SizeOf<FoveatedRenderTelemetryEntry>() == 64` sentinel now gates vault pointer resolution before any record write. If project-path creation is unavailable in a player build, fall back to `Application.persistentDataPath/AgentLogs`.
Rejected Alternatives: Private persistent `NativeArray`, managed `List<T>`, text logs every frame, unpadded binary records, dump-time `Marshal.SizeOf`, trusting ABI layout without a guard, relying on Unity console history, or repeatedly hammering storage after a failed dump.
Scalability potential: Low = fixed 19.2 KB vault memory with no growth. Middle/High/Ultra = same blackbox supports richer postmortem without runtime allocations. Steam Deck/player builds avoid normal-runtime disk I/O and use one-shot fault export only.
Hardware Impact: 1-5 us CPU per tick; fixed vault footprint, no GC. The layout sentinel is one cold `UnsafeUtility.SizeOf` check per domain lifetime. Fault-path file allocation is not frame steady-state.

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

## Update Bucket Rebind
Problem: `GlobalRegistry.ClearRuntimeBuckets()` can clear the global update bucket and dispatcher lanes while the persistent commander object still holds `_registeredTick = true`.
Solution: `TryRegisterTick()` now verifies both `GlobalRegistry.Updatables.Contains(this)` and `SystemDispatcher.GetLane(PriorityLayer.Core).Contains(this)` before trusting the local flag. If only one side contains the commander, it unregisters this owner from both sides and registers cleanly again.
Rejected Alternatives: Adding a standard `Update()`, subscribing to managed scene-load delegates, or trusting the local flag after registry resets.
Scalability potential: Low/Middle/High/Ultra policies keep running after scene-runtime bucket clears without introducing per-frame managed Unity callbacks.
Hardware Impact: 0 us steady-state. Registration-path scan is under 1 us and runs only during lifecycle/service rebind paths.

## Scene Service Rebind
Problem: Bucket verification still needs a legal trigger after scene-runtime replacement; adding `SceneManager.sceneLoaded` would add another managed delegate path.
Solution: Reuse the existing `IGlobalRegistryHotSwapListener` lane. When `GlobalRegistryServiceSlot.Scene` receives a non-null replacement, the commander re-registers tick/render buckets, resolves telemetry, and reapplies the current policy once.
Rejected Alternatives: Standard `Update()`, direct `SceneManager.sceneLoaded` subscription, polling scene state, or adding another signal.
Scalability potential: Scene transitions keep Low/Middle/High/Ultra foveation policy attached without per-frame scene scans.
Hardware Impact: 0 us steady-state; one policy reapply on scene-service rebound.

## Lifecycle Bucket Teardown
Problem: Registration repair verified the global update/render buckets on the way in, but teardown still trusted local `_registered*` booleans. A stale false flag could leave this commander in `GlobalRegistry`/`SystemDispatcher` buckets, and a duplicate component destroyed during `Awake()` could run disable/destroy paths that cleared hardware foveation owned by the active commander.
Solution: Make tick, render, and hot-swap unregister paths scan their authoritative buckets before returning; make hot-swap registration scan `GlobalRegistry.HotSwapListeners` before trusting `_registeredHotSwap`; unregister immediately when dispatcher/render-dispatcher service slots are replaced with null; gate `ClearHardwareFoveation()` in `OnDisable()` and `Dispose()` behind `ReferenceEquals(s_activeCommander, this)` while still allowing stale self-registration cleanup.
Rejected Alternatives: Adding a standard `Update()` watchdog, trusting local booleans after registry churn, leaving duplicate teardown able to clear active XR state, or editing GlobalRegistry for a single-domain cleanup issue.
Scalability potential: Low/Middle/High/Ultra policies survive service churn without extra per-frame callbacks, and duplicate scene/prefab components cannot knock High/Ultra PC VR back to disabled foveation.
Hardware Impact: 0 us steady-state; lifecycle bucket scans are fixed-capacity linear checks on enable/disable/service-rebind paths only. Exact measured GPU microseconds saved remain 0.

## Stale Owner Quarantine
Problem: `Render()` refused non-authoritative instances, but `Tick()` and hot-swap callbacks could still execute if a destroyed, disposed, or duplicate commander somehow remained in dispatcher/hot-swap buckets after service churn.
Solution: Add `TryDetachIfInactiveCommander()`. `Tick()` and `OnGlobalRegistryServiceReplaced()` now bail out through that guard before signal consumption, telemetry writes, blackbox dumps, or XR hardware mutation. The guard unregisters render, hot-swap, and tick ownership and clears the cached thermal service without calling `ClearHardwareFoveation()` for a non-owner. `RequestBlackBoxDump()` also refuses non-authoritative callers.
Rejected Alternatives: Leaving stale owners to early-return forever, adding a standard `Update()` sweep, clearing XR hardware from duplicate teardown, or adding a new manager to police commander instances.
Scalability potential: Low/Middle/High/Ultra policies stay single-owner even under scene reloads and registry churn; duplicate prefab or scene components cannot interfere with Quest fixed FFR or PC gaze VRS.
Hardware Impact: Valid hot path adds one `ReferenceEquals` and one bool check per commander tick. Stale cleanup scans fixed-capacity buckets once on the error path. Exact measured GPU microseconds saved remain 0.

## Cached Service Lane Policy
Problem: `ApplyPolicy()` still read `GlobalRegistry.ScalabilityTier` from a tick-driven policy sample, and `EnsureTelemetry()` could fall back to `GlobalRegistry.DataVault` during telemetry writes when the cached vault was null.
Solution: Implement `IScalabilityChangedEventListener`, seed `_qualityTier` during cold enable, update it through the existing `ScalabilityEvents` typed lane, unregister on teardown/stale quarantine, and make `EnsureTelemetry()` use only the cached `_dataVault` handle. DataVault service replacement still refreshes the cached handle through `IGlobalRegistryHotSwapListener`.
Rejected Alternatives: Polling `GlobalRegistry.ScalabilityTier` in `Tick()`, inventing a new foveation quality signal, keeping a hot-path service-locator fallback for DataVault, or using managed delegates.
Scalability potential: Low/Middle/High/Ultra policy still reacts to platform tier changes, but through a typed lane; High/Ultra no-pressure PC VR keeps full pixels unless gaze or pressure justifies VRS.
Hardware Impact: Removes two service-locator reads from tick-driven policy/telemetry paths. Exact measured GPU microseconds saved remain 0; CPU gain is expected below 1 us per policy sample.

## Foveation Downgrade Hysteresis
Problem: Low/Med/High foveation level resolution could downgrade on the next policy sample after pressure cleared. That violated the state-hysteresis mandate and could create visible edge-quality flicker in VR when system stress hovers around thresholds.
Solution: Add a scalar 2.5-second downgrade hold. Pressure, thermal, and Quest 2-class upgrades still apply immediately. Downgrades keep the previous foveation level while the hold is active, record `FlagHysteresisHold` in telemetry, and decay using sanitized dispatcher `deltaTime`. Disabled/caps-missing paths clear the hold, and High/Ultra no-pressure PC VR still clears fixed foveation so top hardware is not forced into mobile edge loss.
Rejected Alternatives: Immediate threshold switching, adding a new signal, storing hysteresis in a private NativeArray, relying only on HomeostasisBrain hysteresis, or delaying upgrades under thermal pressure.
Scalability potential: Low/Quest keeps stable fixed-high FFR under pressure. Middle avoids Low/Med churn. High/Ultra keep gaze-allowed VRS or full pixels when pressure is absent.
Hardware Impact: Adds one finite-delta clamp per commander tick while a hold is active and a few scalar branches per policy sample. Exact measured GPU microseconds saved remain 0; CPU cost is expected below 1 us per tick during the hold.

## Gaze VRS Loss Grace
Problem: One invalid XR eye-fixation sample could drop PC VR from gaze-tracked VRS to fixed foveation or disabled state on the next policy sample, causing edge-quality churn on High/Ultra hardware.
Solution: Add a 0.75-second scalar grace hold that only applies when the previous target mode was `GazeTracked`, XR is still active, the runtime is standalone-like, the device is not Quest 2-class locked, and Unity foveation caps still expose foveation image or non-uniform raster support. Telemetry marks this with `FlagGazeGraceHold`; disabled/caps-missing paths, Quest fixed-FFR paths, and High/Ultra no-pressure fixed-disable paths clear the hold.
Rejected Alternatives: Blindly enabling gaze without fresh eye data, keeping stale gaze for multiple seconds, adding a new signal lane, storing the grace in a private NativeArray, or disabling gaze immediately on a single provider miss.
Scalability potential: Low/Quest remains fixed-high and unaffected. Middle fixed-FFR remains pressure-tiered. High/Ultra PC VR gets stable gaze-allowed VRS instead of a one-sample visual drop, preserving saved cycles for downstream visual overkill.
Hardware Impact: Adds one finite-delta clamp shared with the existing hysteresis decay and a few scalar branches in the policy sample. Exact measured GPU microseconds saved remain 0; estimated CPU cost is below 1 us per policy sample.

## XR GPU Time Unit Repair
Problem: Unity XR display GPU timing is reported in seconds, but the commander stored it as milliseconds and compared it directly against a 10.75ms pressure threshold. That made GPU-time thermal escalation effectively unreachable from this signal.
Solution: Convert the sampled XR GPU time from seconds to milliseconds immediately after `TryGetAppGPUTimeLastFrame`, reject negative or non-finite samples, and keep the existing `_latestGpuTimeMs` telemetry/threshold contract.
Rejected Alternatives: Raising the threshold, trusting the old variable name, relying only on `SystemHealthSignal.GpuUtil01`, or adding a new frame-time signal when the existing XR display timing already provides the needed input.
Scalability potential: Low/Quest fixed-high FFR remains unchanged. Middle and pressured standalone VR now escalate from real GPU app time. High/Ultra still keep full pixels when unpressured, but can engage foveation correctly when GPU frame time crosses the 10.75ms gate.
Hardware Impact: Adds one multiply by 1000 and one finite guard per running XR display on policy samples. Exact measured GPU microseconds saved remain 0 until headset profiling is run; the fix restores an existing pressure gate rather than claiming a measured saving.

## Quest Identity False-Latch Repair
Problem: Android XR Quest classification could cache `false` before `XRSettings.loadedDeviceName` or stable device tokens were available. On Quest 2-class hardware, that could permanently skip the fixed-high FFR fake for the session.
Solution: Keep Quest classification pending until there is positive Quest 2/Quest 3/Quest Pro evidence or a loaded non-Quest XR device name. While pending, report `FlagQuestClassificationPending` in telemetry and retry on later policy samples. Positive Quest 2 evidence or Quest-family memory gate still caches true; Quest 3/Pro evidence still caches false.
Rejected Alternatives: Permanent false caching on empty XR identity, memory-only Quest 2 detection without Quest-family tokens, per-frame unmanaged allocation, or adding a new signal for a local platform classification state.
Scalability potential: Low/Quest 2 no longer misses the high fixed-foveation path because of early identity timing. Quest 3/Pro and PC VR still avoid the toaster lock. Middle/High/Ultra policies remain unchanged after classification is conclusive.
Hardware Impact: Pending identity costs a few string-token checks per policy sample until XR identity is conclusive; no allocation and no new native buffer. Exact measured GPU microseconds saved remain 0 until headset profiling is run.

## Runtime Sample Cadence Guard
Problem: `sampleIntervalFrames` had a 1-240 inspector range, but runtime code only rejected non-positive values. Corrupted serialized data or a debug script could push it far above 240 and delay Quest/PC VR pressure adaptation for minutes.
Solution: Define shared min/max constants and clamp the value at the dispatcher scheduling point before each policy commit. Values below one fall back to the 30-frame default; values above 240 are capped at 240.
Rejected Alternatives: Trusting `[Range]` metadata, adding a managed validation callback, clamping only in editor, or sampling every frame to mask bad data.
Scalability potential: Low/Quest still gets predictable high fixed FFR; Middle pressure-tiered FFR cannot be silently starved by corrupted cadence; High/Ultra PC VR keeps no-pressure full pixels but can still react to thermal/GPU pressure inside the intended window.
Hardware Impact: Adds two integer branches per policy scheduling event. Exact measured GPU microseconds saved remain 0; this is stability protection, not a measured runtime win. Full rebuild intentionally skipped per current user instruction.

## Compile Wall Boundary
Problem: The shared build moved between external compile walls and green states while this VR domain was being polished.
Solution: Did not edit or revert unrelated domains. After the Quest identity false-latch repair, ran filtered foveation diagnostics and then a full build with restore/analyzers/shared compilation disabled; the current full build is clean.
Rejected Alternatives: Scope creep into vehicle/fluid simulation, reverting other agents' dirty work, or claiming green build from stale evidence.
Scalability potential: Keeps the VR commander isolated while still accepting the final global compile as integration evidence.
Hardware Impact: 0 us runtime; integration boundary only.

## Final Validation
Problem: Final validation regressed multiple times while other domains were moving; stale green output could not be trusted.
Solution: Captured the current truth after the Quest identity false-latch repair. Filtered diagnostics for the VR commander, blackbox, legacy shim, and Quest classification symbols are empty, and the current full `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false /p:RunAnalyzers=false /v:minimal /clp:ErrorsOnly` succeeds with 0 warnings and 0 errors. Static forbidden-pattern scan for the VR commander and legacy shim remains empty.
Rejected Alternatives: Treating earlier green output as current truth, ignoring later external compile walls, claiming VR runtime readiness from compile-only evidence, or hiding missing headset profiling.
Scalability potential: The VR commander is compile-clean and ready for Quest/PC VR runtime profiling; Low/Quest fixed-high FFR with pending-identity retry, Middle pressure-tiered FFR, and High/Ultra gaze-allowed VRS with sample-loss grace and corrected GPU-time pressure remain intact.
Hardware Impact: Exact measured GPU microseconds saved remain 0 until VR hardware capture is run. Static estimates remain Quest 2 200-1000 us GPU recovery on fill-rate-bound frames, PC VR hardware dependent, CPU policy under 15 us per sample.

## Omega Polish Inquisition
Problem: The final polish mandate requires verified master grade, but runtime VR profiling is still absent.
Solution: Perform anti-bloat source audit and current compile validation truthfully: no per-frame managed collections, no `Update()`, no object find calls, no LINQ, no manual render-target edge downscale, no VRS singleton dependency, no stale GPU-pressure latch, no early Quest false-latch, no unguarded telemetry layout, static VR scan clean, filtered VR diagnostics clean, and latest full build clean.
Rejected Alternatives: Claiming full green validation from filtered diagnostics, relying on stale green output, adding standard Unity update callbacks, or editing unrelated domains.
Scalability potential: Low tier remains a constant fixed-foveation fake; Middle responds to stress and corrected GPU-time pressure; High/Ultra use gaze-allowed VRS when hardware supports it and hold through one bad eye-data sample. The same code path scales without branch-heavy shader or render-target bloat.
Hardware Impact: 0 B/frame GC by static audit. Exact measured GPU microseconds saved remain 0 until VR profiling can run on target hardware.

## Multiplatform Inquisition
Problem: VR foveation has to survive Quest/Android ARM64, Metal/Mac, Steam Deck storage pressure, and PC high-end without collapsing into one middle-ground policy.
Solution: ARM64 gets pack-1 64-byte telemetry and vault alignment; Metal/Mac receive no shader or compute-thread assumptions; Steam Deck avoids normal-runtime disk I/O; PC VR keeps gaze-allowed VRS instead of mobile fixed foveation when eye data exists.
Rejected Alternatives: Treating Quest policy as universal, adding shader/compute overkill inside the foveation commander, or dumping telemetry continuously to disk.
Scalability potential: Toaster path is constant fixed FFR. Middle path is stress-tiered FFR. High/Ultra path is gaze-allowed VRS plus shader-global reporting so saved cycles can be spent by downstream visual systems.
Hardware Impact: No measured GPU savings yet. Static estimates remain Quest 2 200-1000 us GPU recovery on fill-rate-bound frames, PC VR hardware dependent, CPU policy under 15 us per sample.
