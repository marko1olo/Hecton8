# LOG_FOVEATED_RENDER_COMMANDER

## 2026-05-16 - Hardware VRS / Fixed Foveated Rendering

What was wrong:
- VR was rendering full-resolution lens edges with no authoritative hardware foveation commander in `Assets/_Project/Scripts/Graphics/VR/`.
- No `VRSManager.Instance` existed to purge; the correct action was not to invent one.
- Legacy Quest FFR code exists in Core as `OculusFfrEnforcer`, but it is not referenced by scenes/prefabs and is not a complete Quest 3 / PC VR commander.
- OpenXR package APIs were unavailable in `Packages/manifest.json`; direct package references would be compile debt.

What was done:
- Added `Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs`.
- Added Unity XR hardware foveation control through `SystemInfo.foveatedRenderingCaps`, `XRDisplaySubsystem.foveatedRenderingLevel`, and `XRDisplaySubsystem.FoveatedRenderingFlags`.
- Mapped `SystemHealthSignal.SystemHealthIndex01` to Low/Medium/High FFR levels: 0.35, 0.62, 0.85.
- Consumed `SystemHealthSignal` and `ThermalStateChangedSignal` through `ReadOnlySpan<T>` snapshots; no event delegates or singleton manager.
- Added thermal/GPU pressure escalation to High FFR.
- Locked Quest 2/Oculus Quest-class runtimes to High fixed foveation.
- Enabled PC VR gaze-allowed VRS only when caps and finite eye fixation data are present.
- Disabled foveation on flat-screen PC by default.
- Added SRP UI camera fail-closed handling: cameras rendering UI layer mask force foveation off for text legibility, then restore target state.
- Added 300-frame fixed native blackbox ring and binary dump path `Docs/AgentLogs/Dump_FOVEATED_RENDER_COMMANDER.bin`.
- Added status and rationale evidence files for this prompt.

Cinematic Cheats used:
- Fixed foveation is the chosen lens-edge cheat; no render-target edge simulation, no physical pixel model, no custom radial shader pass.
- Quest 2 uses constant High FFR instead of adaptive hunting.
- UI layers are exempted by camera-level hard disable, not shader compensation.
- PC flat-screen path disables foveation unless explicitly allowed, avoiding invisible quality debt.

Exact microseconds saved:
- Exact measured microseconds saved: 0 recorded. Runtime profiling is blocked because `dotnet build` is red outside this domain.
- Estimated CPU cost added: 2-8 us per dispatcher tick for signal reads, 1-4 us per telemetry write, 3-12 us per UI camera toggle, 5-15 us for gaze probe when sampled.
- Estimated GPU budget recovered: Quest 2-class fixed FFR 200-1000 us on fill-rate-bound VR frames; avoided manual edge blit/downscale path 60-250 us GPU if such a path had been used. These are estimates, not profiler measurements.

Validation:
- Attempt 1 `dotnet build Hecton8.Core.csproj`: failed in `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` on unresolved AUP helper methods/fields.
- Attempt 2 `dotnet build Hecton8.Core.csproj --no-restore`: failed in `Assets/_Project/Scripts/Core/GlobalSignals.cs(580,50)` because `SignalBus<T>.SignalLaneAdapter` does not implement `ISignalLane.FlushPreSimulation(bool,int)`.
- Attempt 3: same `GlobalSignals.cs(580,50)` compile wall.
- No compile diagnostics reported from `Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs` before the external wall stopped the build.

Integrator note:
- Do not judge this task as green until Core/Gameplay compile walls are repaired.
- Do not revert the VR commander to fix those walls; the blockers are outside the assigned domain.

## 2026-05-16 - Escalation Polish / Data Sovereignty Pass

What was wrong:
- The previous commander owned a private persistent telemetry `NativeArray`. Sentinel registration was not enough; it still made the graphics/VR system a data owner.
- Telemetry struct used `Pack = 4`; that is acceptable on desktop but not brutal enough for ARM64/Quest layout discipline.
- The status report treated the blackbox as complete while it was not vault-sovereign.

What was done:
- Added `BufferID.FoveatedRenderBlackBox = 129` in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`.
- Replaced the private telemetry `NativeArray` with `VaultBufferHandle<FoveatedRenderTelemetryEntry>` resolved from `GlobalRegistry.DataVault`.
- Removed direct `new NativeArray`, `NativeMemorySentinel.RegisterNativeArray`, and `NativeMemorySentinel.UnregisterNativeArray` from `FoveatedRenderCommander.cs`.
- Changed `FoveatedRenderTelemetryEntry` to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`.
- Added vault generation to every telemetry heartbeat entry.
- Re-audited the VR file: no `Update()`, no `GameObject.Find`, no `FindObject*`, no `foreach`, no LINQ, no EventBus, no `string.Format`, no private native allocation.

Cinematic Cheats used:
- No new physical simulation was added. Foveation remains the lens-edge cheat.
- Toaster mode is constant High FFR on Quest 2-class hardware.
- God-mode keeps gaze-allowed VRS and reports hardware foveation globals; visual overkill belongs to downstream render/VFX systems, not this commander's data path.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Build remains red outside this domain.
- Private native allocation removed: 19.2 KB moved into vault ownership, not eliminated.
- Estimated added CPU from vault handle resolution: 0-1 us over direct native-array write.
- Estimated GPU recovery remains unchanged: Quest 2-class FFR 200-1000 us on fill-rate-bound frames; PC gaze VRS hardware dependent.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore` still fails, now with 105 external errors. Representative blockers: missing `Hecton8.Core.Contracts` / `Hecton8.Core.Memory` assembly references in bootstrap/voxel/fauna paths, missing `HectonShaderGlobalDataVaultBridge`, missing voxel debris constants, missing signal types (`VisualFlareSignal`, `AnomalyProximitySignal`, `CompassCalibratedSignal`, `FluidImpulseSignal`, `DebrisSpawnSignal`), and unrelated Gameplay helper/conversion errors.
- No reported errors named `FoveatedRenderCommander.cs`.

Integrator note:
- The only cross-domain edit from this pass is `BufferID.FoveatedRenderBlackBox`; it is a required DataVault identifier for the graphics/VR heartbeat and does not add gameplay behavior.

## 2026-05-16 - Escalation Polish / Stability Pass 2

What was wrong:
- The XR display apply path avoided redundant foveation writes, but it also skipped fresh `TryGetAppGPUTimeLastFrame` sampling when the target level and flags were unchanged.
- UI suppression used a boolean latch. Nested UI cameras could restore world foveation too early before the outer UI camera finished.

What was done:
- Changed `ApplyDisplayState` to enumerate running XR displays every policy sample, sample GPU app time, and only write foveation flags/level when the target changed or the display drifted.
- Replaced UI suppression boolean state with an integer depth counter; foveation is restored only when the final matching UI camera exits.
- Re-ran the static debt scan over `Assets/_Project/Scripts/Graphics/VR`; the only collection match is the cold static `List<XRDisplaySubsystem>` reused for subsystem enumeration.
- Re-ran filtered build diagnostics for `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, and `Graphics/VR`; no matching diagnostics were emitted. The global build remains red outside this domain.

Cinematic Cheats used:
- No new simulation. The commander still spends cycles on the hardware lens-edge cheat and UI correctness, not fake physical detail inside the policy system.
- High-end path keeps gaze-allowed VRS. Low-end path keeps constant High FFR on Quest 2-class hardware.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Build remains red outside this domain.
- Estimated CPU preserved by avoiding redundant XR writes: 1-4 us on unchanged policy samples.
- Estimated UI nesting fix performance change: 0 us material; it is correctness/stability hardening.
- Estimated GPU recovery remains Quest 2-class FFR 200-1000 us on fill-rate-bound frames; PC gaze VRS is hardware/runtime dependent.

Validation:
- Filtered build command produced no `Graphics/VR` diagnostics, but `dotnet build` still exits red due to external compile errors already logged.

## 2026-05-16 - Escalation Polish / Stability Pass 3

What was wrong:
- Thermal severity latched high permanently because it maxed against old state instead of recomputing from current thermal signals/service snapshots.
- Blackbox dump header reported `Marshal.SizeOf<FoveatedRenderTelemetryEntry>()` as 64 bytes but only 56 bytes of explicit fields were written per record.
- Non-finite XR display level or invalid eye descriptor could dump evidence without first guaranteeing a hardware foveation clear.

What was done:
- Reworked thermal severity consumption so lower severity signals/snapshots can recover policy from High FFR after pressure subsides.
- Bumped blackbox format to version 2 and wrote 8 bytes of explicit padding per telemetry record so each dump record matches the 64-byte pack-1 struct.
- Sanitized target/display foveation levels, tracked non-finite display state, wrote fault telemetry before dump, and forced hardware foveation clear on invalid XR display/eye state.
- Suppressed active hardware foveation reporting to XR shader globals when the current eye/display state is invalid.

Cinematic Cheats used:
- No extra render simulation. Low-end remains the fixed high FFR fill-rate cheat; high-end remains gaze-allowed VRS.
- Recovery from thermal pressure prevents PC/Quest 3 from being stuck in the low-tier visual compromise after transient heat.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Build remains red outside this domain.
- Estimated added CPU: under 1 us per policy sample for thermal recovery and display finite checks.
- Estimated GPU recovery remains Quest 2-class FFR 200-1000 us on fill-rate-bound frames; no new measured profiler data exists.

Validation:
- `rg` debt scan over `Assets/_Project/Scripts/Graphics/VR` still reports only the cold static `List<XRDisplaySubsystem>`.
- `git diff --check` for the VR file passed.
- Filtered `dotnet build` diagnostic scan again produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, or `Graphics/VR` matches. Full project build is still blocked by external compile errors.
- Repeated the filtered build diagnostic scan after shader-global invalid-state suppression; still no VR-domain diagnostics.
- Re-read `AGENTS.md` and `Docs/Actual Domains of Project.txt`; final wording remains `PENDING VERIFICATION` because Unity import, Play Mode, profiler, player build, and full compile are not available from the current red build.
- Corrected `COLD ALLOC` comments in `FoveatedRenderCommander.cs` to the canonical project format. A final filtered build diagnostic scan timed out after 147 seconds and left no `dotnet` process; it is not evidence of green validation.
- Re-ran filtered build diagnostics with `-m:1 /nr:false /clp:ErrorsOnly`; no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, or `Graphics/VR` diagnostics were emitted. Full build remains red externally.

## 2026-05-16 - Escalation Polish / Legacy Enforcer Quarantine

What was wrong:
- `Assets/_Project/Scripts/Core/OculusFfrEnforcer.cs` was a second hardware foveation owner with direct `XRDisplaySubsystem.foveatedRenderingLevel` writes.
- It held a private persistent `NativeArray<QuestFfrBlackboxEntry>` blackbox instead of using `GlobalDataVault`.
- It subscribed to a managed XR-active event and could clamp texture mip limits on Quest separately from the graphics/VR commander.

What was done:
- Preserved `QuestVulkanRuntimePolicy`; it is still used for Quest runtime classification.
- Reduced `OculusFfrEnforcer` to an obsolete disabled compatibility shim so old serialized components do not become missing scripts.
- Removed the legacy private native blackbox, direct foveation writes, XR-state event subscription, texture mip clamp, and duplicate dump path from the old class.

Cinematic Cheats used:
- No new simulation. Quest foveation remains the single low-tier visual cheat, now owned only by `FoveatedRenderCommander`.
- High-end gaze VRS can no longer be overwritten by the stale Quest-only enforcer.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Build remains red outside this domain.
- Estimated avoided CPU if the old component were accidentally enabled: one 60-frame XR subsystem scan plus blackbox write, roughly 2-10 us per sample.
- Private native allocation avoided if the old component were accidentally enabled: one 300-entry Quest FFR ring. The authoritative commander still uses the 19.2 KB DataVault ring.

Validation:
- Static scan shows no `NativeArray`, no old dump path, no managed XR-active subscription, and no direct foveation writes in `OculusFfrEnforcer.cs`.
- Filtered `dotnet build` diagnostic scan after quarantine produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, or `Graphics/VR` diagnostics. Full build remains red externally.

## 2026-05-16 - Escalation Polish / Duplicate Guard Fix

What was wrong:
- `FoveatedRenderCommander` duplicate handling destroyed the entire host GameObject. If a duplicate component were placed on a scene XR rig, that would delete unrelated rig components.

What was done:
- Changed duplicate handling to `Destroy(this)` so only the duplicate commander component is removed.

Cinematic Cheats used:
- None. This is scene safety hardening.

Exact microseconds saved:
- Exact measured microseconds saved: 0. This prevents scene-object loss, not frame-time cost.

Validation:
- Static scan found no `Destroy(gameObject)` in the VR commander.
- Filtered build diagnostics after the fix produced no VR/legacy foveation matches. Full build remains red externally.

## 2026-05-16 - Escalation Polish / Quest Android Detection

What was wrong:
- Quest 2 fixed-high FFR detection depended on `QuestVulkanRuntimePolicy.IsQuestRuntimeActive`, so an Android XR Quest runtime outside Vulkan would miss the low-tier fixed-FFR fake.
- The memory gate also needed an explicit Quest 3/Quest Pro exclusion, otherwise reserved-memory reporting could push high-end standalone headsets into the toaster policy.

What was done:
- Changed Quest 2/Oculus Quest detection to require Android + active XR + memory/device Quest-family evidence, independent of Vulkan.
- Cached the Quest class decision after XR activation so policy samples no longer repeat platform string classification.
- Added explicit Quest 3/Quest Pro exclusion before the low-tier memory fallback.
- Kept Unity `SystemInfo.foveatedRenderingCaps` as the actual hardware write gate.

Cinematic Cheats used:
- Low-tier Quest stays on the cheapest fixed-high FFR approximation. No adaptive search, no eye-tracking assumption.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Runtime profiling is still blocked.
- Estimated GPU recovery remains 200-1000 us on Quest 2-class fill-rate-bound frames when Unity caps honor fixed foveation.

Validation:
- Static scan shows no remaining `QuestVulkanRuntimePolicy.IsQuestRuntimeActive` dependency in `FoveatedRenderCommander`.
- Static scan shows cached Quest classification state instead of repeated policy-sample classification.
- Filtered build diagnostics after the change produced no VR/legacy foveation matches. Full build remains red externally.
- Unfiltered `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` now fails with 16 external errors in `World/SargassumMicroFaunaBoids.cs`, `Construction/VehicleDockingModule.cs`, and `VFX/HectonMarineSnowRenderer.cs`; no `Graphics/VR` files are named.
- Filtered build diagnostics after Quest 3/Pro exclusion again produced no VR/legacy foveation matches.

## 2026-05-16 - Escalation Polish / PC God-Mode Policy

What was wrong:
- PC VR on High/Ultra tiers without gaze data could still receive Low fixed foveation at zero pressure. That is a mobile compromise applied to expensive hardware without justification.
- The XR display scratch list was sized to 4; if Unity ever reports more display subsystems, the policy path could grow the list.

What was done:
- Added High/Ultra no-pressure fixed-foveation suppression. If there is no gaze, no thermal pressure, and no system pressure, the commander clears fixed foveation instead of applying Low FFR.
- Preserved pressure-driven fixed foveation and gaze-allowed VRS.
- Increased the reused XR display scratch list capacity from 4 to 8.

Cinematic Cheats used:
- Toaster mode remains Quest 2/Oculus Quest fixed High FFR.
- God-mode keeps full peripheral resolution unless gaze VRS or pressure earns the quality trade.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Runtime profiling is still blocked.
- Estimated CPU change: under 1 us per policy sample.
- Estimated quality impact: avoids unjustified fixed-FFR edge loss on High/Ultra PC VR no-pressure frames.

Validation:
- Static scan shows no new managed hot-path allocations, no new signal lane, and no VR-domain forbidden patterns.
- Filtered build diagnostics after the policy change produced no VR/legacy foveation matches. Full build remains red externally.

## 2026-05-16 - Escalation Polish / Blackbox Size Contract

What was wrong:
- The blackbox dump header used `Marshal.SizeOf<FoveatedRenderTelemetryEntry>()` during fault export. The record size is a fixed binary contract and should not be reflected at dump time.

What was done:
- Added `TelemetryRecordSizeBytes = 64`.
- Reused that constant in `[StructLayout(..., Size = TelemetryRecordSizeBytes)]` and in the binary dump header.

Cinematic Cheats used:
- None. This is postmortem binary stability work.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Dump path only.
- Estimated fault-path CPU avoided: negligible; value is removing size drift/metadata dependency from crash evidence.

Validation:
- Static scan confirms no `Marshal.SizeOf` remains in `FoveatedRenderCommander`.
- Filtered build diagnostics after the change produced no VR/legacy foveation matches. Full build remains red externally.

## 2026-05-16 - Escalation Polish / Thermal Service Loss

What was wrong:
- Thermal severity could still remain stale if the hardware thermal service disappeared after previously reporting pressure and no new thermal signal arrived.

What was done:
- Clear `_thermalSeverity` when `HardwareThermalService` is unregistered.
- Clear `_thermalSeverity` during signal consumption when there is no thermal service and no current thermal lane data.

Cinematic Cheats used:
- None. This protects dynamic policy recovery.

Exact microseconds saved:
- Exact measured microseconds saved: 0.
- Estimated CPU change: under 1 us per dispatcher tick.

Validation:
- Filtered build diagnostics after the stale-latch fix produced no VR/legacy foveation matches. Full build remains red externally.

## 2026-05-16 - Escalation Polish / Pressure Tier Wiring

What was wrong:
- `PressureLevel` and `FoveatedPressureTier` were consumed and logged, but the actual Low/Med/High resolver ignored them. That made homeostasis foveation pressure partially cosmetic.
- Health and thermal signal loops copied structs from `ReadOnlySpan<T>` instead of reading by reference.
- Cold allocation comments still contained non-ASCII dash separators.

What was done:
- Wired `PressureLevel` and `FoveatedPressureTier` directly into `ResolveTargetLevelCode`.
- `PressureLevel >= 3` or `FoveatedPressureTier >= 3` now resolves High; `PressureLevel >= 2` or `FoveatedPressureTier >= 2` now resolves at least Medium.
- Changed health and thermal signal reads to `ref readonly`.
- Converted the cold-allocation comments to ASCII-only separators.

Cinematic Cheats used:
- Pressure lanes now buy the intended visual fake: more aggressive hardware foveation under system pressure instead of expensive full-edge rendering.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Runtime profiling is still blocked by external compile failure.
- Estimated CPU change: under 1 us per policy sample.
- Estimated GPU recovery remains hardware and scene dependent; Quest 2-class fill-rate estimate stays 200-1000 us on pressure frames.

Validation:
- Static forbidden-pattern scan found no VR/legacy foveation debt patterns.
- Non-ASCII scan found no non-ASCII text in `FoveatedRenderCommander.cs` or the legacy shim.
- Filtered build diagnostics after the pressure-tier patch produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, or `Graphics/VR` matches.
- Unfiltered `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` fails externally at `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs(1166,18)` because `EnsureCoreCognitionVaultBuffers` is missing.

## 2026-05-16 - Escalation Polish / Player Dump Fallback

What was wrong:
- The blackbox dump path assumed the project `Docs/AgentLogs` directory could be derived from `Application.dataPath`. That is correct in editor-style layouts, but brittle in packaged Android/VR players.
- A failure while building the project dump path could prevent the persistent-data fallback from being attempted.

What was done:
- Added guarded project dump path construction.
- Added fallback to `Application.persistentDataPath/AgentLogs/Dump_FOVEATED_RENDER_COMMANDER.bin` when the project log path cannot be opened.
- Kept dump export one-shot so Steam Deck and MicroSD installs are not hammered every frame after a fault.

Cinematic Cheats used:
- None. This is postmortem survival work.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Runtime profiling is still blocked by external compile failure.
- Steady-state runtime cost remains 0 us because the file path is touched only on the fault dump path.

Validation:
- Static forbidden-pattern scan found no VR/legacy foveation debt patterns.
- Non-ASCII scan found no non-ASCII text in `FoveatedRenderCommander.cs` or the legacy shim.
- Filtered build diagnostics after the dump fallback produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, or `Graphics/VR` matches.
- Unfiltered `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` fails externally at `Assets/_Project/Scripts/Core/InputDispatcher.cs(7,2)` because a preprocessor symbol is defined/undefined after the first token.

## 2026-05-16 - Escalation Polish / RenderDispatcher UI Exemption

What was wrong:
- The VR commander owned direct `RenderPipelineManager` begin/end subscriptions for UI suppression. That duplicated the existing project render fan-out and violated the demand to prefer registry interfaces over private managed delegate ownership.
- UI suppression flags could persist in `_lastFlags` after the restore path.

What was done:
- Made `FoveatedRenderCommander` implement `IRenderable`.
- Registered it in `GlobalRegistry.Renderables`.
- Moved UI camera detection to `GlobalRenderContext.CurrentCamera`, which is already populated by the project `RenderDispatcher`.
- Removed direct `RenderPipelineManager` subscriptions from the VR commander.
- Cleared `FlagUiSuppressed` when a non-UI camera restores the target state and when telemetry is written outside an active UI suppression.

Cinematic Cheats used:
- UI text remains an absolute exemption from the fill-rate cheat. World cameras keep the cheap foveation fake; UI cameras buy clarity.

Exact microseconds saved:
- Exact measured microseconds saved: 0. Runtime profiling is still blocked by external compile failure.
- Estimated CPU change: neutral to under 1 us per render camera because one project-owned SRP fan-out replaces a private VR subscription.

Validation:
- Static forbidden-pattern scan found no VR/legacy foveation debt patterns and no direct `RenderPipelineManager` usage in the VR domain.
- Non-ASCII scan found no non-ASCII text in `FoveatedRenderCommander.cs` or the legacy shim.
- Filtered build diagnostics after the render-dispatcher migration produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, or `Graphics/VR` matches.
- Full error capture reports 130 external errors in `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`, `Assets/_Project/Scripts/RepairTool.cs`, and `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`; copied to `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

## 2026-05-16 - Escalation Polish / UI Suppression Latch Clear

What was wrong:
- Hardware foveation clear could leave `_uiSuppressionActive` true after a fault or shutdown clear. The hardware state was safe, but telemetry could keep reporting stale UI suppression.

What was done:
- `ClearHardwareFoveation()` now resets `_uiSuppressionActive`.
- `ClearHardwareFoveation()` now clears `FlagUiSuppressed` from `_lastFlags`.

Cinematic Cheats used:
- None. This is blackbox truthfulness and state hygiene.

Exact microseconds saved:
- Exact measured microseconds saved: 0.
- Estimated CPU change: below measurable noise; two scalar writes on clear paths only.

Validation:
- Static forbidden-pattern scan found no VR/legacy foveation debt patterns and no direct `RenderPipelineManager` usage in the VR domain.
- Non-ASCII scan found no non-ASCII text in `FoveatedRenderCommander.cs` or the legacy shim.
- Filtered build diagnostics after latch clear produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, or `Graphics/VR` matches.
- Full error capture reports 22 external errors in `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`, all missing `VaultNativeBuffer<>`; copied to `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

## 2026-05-16 - Escalation Polish / Render Bucket Rebind

What was wrong:
- `GlobalRegistry.ClearRuntimeBuckets()` can clear the render bucket while a persistent commander object still has a stale local `_registeredRenderable` flag.
- A stale flag could block re-registration and silently remove UI foveation exemption from the render fan-out.

What was done:
- `TryRegisterRenderable()` now checks `GlobalRegistry.Renderables.Contains(this)` before trusting local state.
- If the local flag is stale and the bucket no longer contains this commander, it attempts registration again.
- `RenderDispatcher` service replacement now calls `TryRegisterRenderable()` when a dispatcher appears.

Cinematic Cheats used:
- None. This keeps the UI-exemption safety path attached to the render loop after registry resets.

Exact microseconds saved:
- Exact measured microseconds saved: 0.
- Estimated CPU change: below 1 us on registration paths only; no steady-state render cost added.

Validation:
- Static forbidden-pattern scan found no VR/legacy foveation debt patterns and no direct `RenderPipelineManager` usage in the VR domain.
- Non-ASCII scan found no non-ASCII text in `FoveatedRenderCommander.cs` or the legacy shim.
- Filtered build diagnostics after render-bucket rebind hardening produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, or `Graphics/VR` matches.
- Full error capture reports 23 external errors: `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs` missing namespace `Hecton8.AI.Ecosystem`, and `Assets/_Project/Scripts/SubmarineFluidDynamics.cs` missing `VaultNativeBuffer<>`; copied to `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

## 2026-05-16 - Escalation Polish / Interface Dedup Audit

What was wrong:
- The project is running many parallel agents, so a foveation-specific signal or duplicate DataVault buffer ID would create integration debt.

What was done:
- Re-ran the `BufferID` duplicate audit after adding `FoveatedRenderBlackBox = 129`; result: `NO_BUFFERID_DUPLICATES`.
- Verified foveation pressure uses existing `SystemHealthSignal.FoveatedPressureTier`.
- Verified thermal escalation uses existing `ThermalStateChangedSignal`.
- Verified the VR domain has no legacy `EventBus`, no direct `RenderPipelineManager` subscription, and no private `NativeArray`.

Cinematic Cheats used:
- None. This is interface hygiene.

Exact microseconds saved:
- Exact measured microseconds saved: 0.
- Runtime cost: 0 us; audit-only.

Validation:
- Duplicate `BufferID` audit returned `NO_BUFFERID_DUPLICATES`.
- Static forbidden-pattern scan remains clean for the VR domain and legacy foveation shim.

## 2026-05-16 - Escalation Polish / Update Bucket Rebind And Green Build

What was wrong:
- `TryRegisterTick()` trusted the local `_registeredTick` bit after lifecycle registration.
- `GlobalRegistry.ClearRuntimeBuckets()` can clear the global updatable bucket and dispatcher lane without disabling the persistent commander object, leaving policy ticks vulnerable to silent detachment.

What was done:
- `TryRegisterTick()` now verifies both `GlobalRegistry.Updatables.Contains(this)` and `SystemDispatcher.GetLane(PriorityLayer.Core).Contains(this)`.
- If only one side contains the commander, it unregisters this owner through `GlobalRegistry.UnregisterUpdatable()` and performs a clean `GlobalRegistry.TryRegisterUpdatable()` pass.
- Re-ran filtered diagnostics, static debt scans, ASCII scan, `git diff --check`, and full `dotnet build`.
- Updated `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt` with the green build output.

Cinematic Cheats used:
- None. This is runtime registry survival. The visual cheat remains hardware foveation: fixed high on Quest 2-class hardware, stress-tiered fixed FFR on pressured devices, and gaze-allowed VRS on capable PC VR.

Exact microseconds saved:
- Exact measured GPU microseconds saved: 0. No headset capture was run.
- Estimated registration-path CPU change: under 1 us on lifecycle/service rebind paths only.
- Static steady-state GC: 0 B/frame by source audit.

Validation:
- `rg` forbidden-pattern scan found no `NativeArray<`, `new NativeArray`, `EventBus`, managed delegate fields, standard `Update()`, `foreach`, LINQ, `string.Format`, object find calls, direct `RenderPipelineManager`, temp render textures, `Marshal.SizeOf`, or `VRSManager.Instance` in the VR commander or legacy foveation shim.
- ASCII scan reports `ASCII_OK` for `FoveatedRenderCommander.cs`.
- Filtered build scan after update-bucket rebind produced no matching `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, `Graphics/VR`, or `SystemDispatcher.GetLane` diagnostics.
- `dotnet build Hecton8.Core.csproj -m:1 /nr:false /clp:ErrorsOnly` succeeded with 0 warnings and 0 errors.

Latest regression after green build:
- A later full build in the same shared workspace failed outside this domain.
- Current captured wall is `Assets/_Project/Scripts/SpatialAudioManager.cs`: missing `ClearVaultBackedTelemetryAliases` and multiple missing `EnsureVaultBackedArray` calls.
- I did not edit the audio domain. Latest evidence is stored in `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

## 2026-05-16 - Escalation Polish / Scene Rebind And Final Validation

What was wrong:
- The update-bucket repair was correct when registration was attempted, but scene-runtime service replacement needed a legal trigger that did not add `SceneManager.sceneLoaded` managed delegate debt.
- A transient external audio compile wall appeared after one green build and then cleared again in the shared workspace.

What was done:
- `OnGlobalRegistryServiceReplaced()` now handles `GlobalRegistryServiceSlot.Scene`.
- On non-null scene-service replacement, the commander re-registers tick and render buckets, resolves DataVault telemetry, and reapplies hardware foveation policy once.
- Re-ran static forbidden-pattern scans, duplicate `BufferID` audit, filtered VR build diagnostics, and full build.

Cinematic Cheats used:
- None in this patch. The domain cheat remains hardware VRS/FFR instead of edge render-target blits.

Exact microseconds saved:
- Exact measured GPU microseconds saved: 0. No Quest/PC VR headset profile capture was run.
- Estimated scene-rebind cost: one policy reapply on scene-service rebound; 0 us steady-state.

Validation:
- Static forbidden-pattern scan remains clean for the VR commander and legacy foveation shim.
- Duplicate `BufferID` audit returned `NO_BUFFERID_DUPLICATES`.
- Filtered build diagnostics after scene-service rebind hardening produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, `Graphics/VR`, or `GlobalRegistryServiceSlot.Scene` diagnostics.
- Latest `dotnet build Hecton8.Core.csproj -m:1 /nr:false /clp:ErrorsOnly` succeeded with 0 warnings and 0 errors.

## 2026-05-16 - Escalation Polish / ARM64 Telemetry Layout Sentinel

What was wrong:
- `FoveatedRenderTelemetryEntry` had `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`, but the commander did not verify the unmanaged size before resolving a DataVault pointer.
- If a future ABI or source edit broke the 64-byte contract, the blackbox path could write records with a different binary shape than the dump header declares.

What was done:
- Added a cold `UnsafeUtility.SizeOf<FoveatedRenderTelemetryEntry>() == TelemetryRecordSizeBytes` sentinel.
- `EnsureTelemetry()` now refuses to resolve or allocate the vault blackbox when the layout check fails.
- A failed layout check publishes `GlobalTelemetryBus.PublishMathGuardInvalidNumber(SourceHash)` instead of writing questionable data.

Cinematic Cheats used:
- None. This is Quest/ARM64 binary safety for the existing foveated rendering blackbox.

Exact microseconds saved:
- Exact measured GPU microseconds saved: 0. No headset capture was run.
- Runtime frame-time saved: 0 us measured.
- Estimated cost: one cold `UnsafeUtility.SizeOf` check per domain lifetime; 0 us steady-state after the static bool is set.

Validation:
- Static forbidden-pattern scan remains clean for the VR commander and legacy foveation shim.
- ASCII scan reports `ASCII_OK` for `FoveatedRenderCommander.cs`.
- Filtered build diagnostics after the layout sentinel produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, `Graphics/VR`, or `UnsafeUtility.SizeOf` diagnostics.
- Latest full build is blocked outside domain: `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs` is missing `ValidatePackedStructSizes` and `BuildVisualOverkillDiagnostics`; evidence is in `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

Latest current-disk build wall:
- A repeated full build shifted to 7 external errors.
- `ArchitectEyeVisualizer.cs` now has duplicate `ValidatePackedStructSizes`.
- `PlayerCriticalProceduralAudioRenderer.cs` and `AbyssalThermalManager.cs` have ambiguous `LaserCutterEventPayload` references and missing `ILaserCutterEventListener` implementation signatures.
- A repeated filtered build scan still produced no VR-domain or foveation-symbol diagnostics.

## 2026-05-16 - Escalation Polish / Lifecycle Teardown Symmetry

What was wrong:
- `TryRegisterTick()` had bucket-divergence repair, but unregister paths still trusted `_registeredTick`, `_registeredHotSwap`, and `_registeredRenderable`.
- A stale false local flag could leave this commander in a global bucket after registry churn.
- A duplicate component destroyed during `Awake()` could still run `OnDisable()`/`OnDestroy()` and clear hardware foveation state owned by the authoritative commander.

What was done:
- `TryUnregisterTick()` now scans `GlobalRegistry.Updatables` and `SystemDispatcher.GetLane(PriorityLayer.Core)` before returning.
- `TryRegisterHotSwap()` now checks `GlobalRegistry.HotSwapListeners.Contains(this)` before trying a new registration.
- `TryUnregisterHotSwap()` and `TryUnregisterRenderable()` now scan their authoritative buckets before trusting local flags.
- `OnGlobalRegistryServiceReplaced()` now unregisters tick ownership when `GlobalRegistryServiceSlot.Dispatcher` is replaced with null.
- `OnGlobalRegistryServiceReplaced()` now unregisters render ownership when `GlobalRegistryServiceSlot.RenderDispatcher` is replaced with null.
- `OnDisable()` and `Dispose()` now only call `ClearHardwareFoveation()` when `ReferenceEquals(s_activeCommander, this)` is true; duplicate components can clean their own registrations but cannot clear the active commander's XR state.

Cinematic Cheats used:
- None in this patch. The rendering cheat remains hardware VRS/FFR: Quest 2-class fixed-high foveation, pressure-tiered fixed FFR, and gaze-allowed VRS on capable PC VR.

Exact microseconds saved:
- Exact measured GPU microseconds saved: 0. No headset profiling capture was run.
- Estimated steady-state cost: 0 us. These checks run on lifecycle/service-rebind paths only.
- Estimated lifecycle CPU cost: under 1 us for fixed-capacity bucket scans in normal bucket sizes.

Validation:
- Static forbidden-pattern scan found no `Update()`, `LateUpdate()`, `FixedUpdate()`, `foreach`, LINQ, `string.Format`, `VRSManager.Instance`, direct `RenderPipelineManager`, `new NativeArray`, `Marshal.SizeOf`, legacy `EventBus`, managed delegate fields, object find calls, `Camera.main`, `Resources.Load`, or `StartCoroutine` in the VR commander or legacy foveation shim.
- ASCII scan reports `ASCII_OK` for `FoveatedRenderCommander.cs`.
- Duplicate `BufferID` audit returned `NO_BUFFERID_DUPLICATES`.
- Filtered build scans after lifecycle hardening and service-null unregister hardening produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, `Graphics/VR`, `GlobalRegistryServiceSlot.Dispatcher`, `GlobalRegistryServiceSlot.RenderDispatcher`, `TryUnregisterTick`, `TryUnregisterHotSwap`, `TryUnregisterRenderable`, or `ownsRuntimeState` diagnostics.
- `git diff --check` reports no whitespace errors; only existing CRLF normalization warnings for touched files.
- Latest full `dotnet build Hecton8.Core.csproj -m:1 /nr:false /clp:ErrorsOnly` is blocked outside domain by `Assets/_Project/Scripts/World/EcosystemDirector.cs`: 127 CS1612 return-value mutation errors against vault/SoA accessors. Evidence is stored in `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

## 2026-05-16 - Escalation Polish / Stale Owner Quarantine

What was wrong:
- `Render()` already rejected non-authoritative commanders, but `Tick()` and hot-swap callbacks could still execute if a duplicate, disposed, or stale commander survived in dispatcher/hot-swap buckets.
- That stale owner could consume signals, write telemetry, or dump blackbox evidence even though it did not own XR foveation state.

What was done:
- Added `TryDetachIfInactiveCommander()`.
- `Tick()` now exits through that guard before consuming `SignalBus` snapshots, applying policy, or writing telemetry.
- `OnGlobalRegistryServiceReplaced()` now exits through the same guard before handling service replacement.
- `RequestBlackBoxDump()` refuses non-authoritative or disposed commanders.
- The guard unregisters render, hot-swap, and tick ownership and clears cached thermal service without clearing active XR hardware foveation for a non-owner.

Cinematic Cheats used:
- None in this patch. The visual cheat remains hardware foveation, not a render-target edge blit.

Exact microseconds saved:
- Exact measured GPU microseconds saved: 0. No headset profiling capture was run.
- Valid hot-path cost added: one `ReferenceEquals` plus one bool check per commander tick.
- Stale cleanup cost: fixed-capacity bucket scans only on the error path.

Validation:
- Static forbidden-pattern scan found no `Update()`, `LateUpdate()`, `FixedUpdate()`, `foreach`, LINQ, `string.Format`, `VRSManager.Instance`, direct `RenderPipelineManager`, `new NativeArray`, `NativeArray<`, `Marshal.SizeOf`, legacy `EventBus`, managed delegate fields, object find calls, `Camera.main`, `Resources.Load`, or `StartCoroutine` in the VR commander or legacy foveation shim.
- ASCII scan reports `ASCII_OK` for `FoveatedRenderCommander.cs`.
- Duplicate `BufferID` audit returned `NO_BUFFERID_DUPLICATES`.
- Filtered build scan after stale-owner quarantine produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, `Graphics/VR`, `TryDetachIfInactiveCommander`, `RequestBlackBoxDump`, or `OnGlobalRegistryServiceReplaced` diagnostics.
- Latest full `dotnet build Hecton8.Core.csproj -m:1 /nr:false /clp:ErrorsOnly` is blocked outside domain by `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: 187 syntax/identifier errors around lines 2051-2095 plus a final `}` expected at line 4926. Evidence is stored in `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

## 2026-05-16 - Escalation Polish / Cached Service-Lane Policy

What was wrong:
- `ApplyPolicy()` still read `GlobalRegistry.ScalabilityTier` from a tick-driven policy sample.
- `EnsureTelemetry()` could fall back to `GlobalRegistry.DataVault` during telemetry writes if `_dataVault` was null.

What was done:
- Implemented `IScalabilityChangedEventListener`.
- Seeded `_qualityTier` during cold `OnEnable()` and update it through `ScalabilityEvents`.
- Unregisters from `ScalabilityEvents` during disable, dispose, and stale-owner quarantine.
- `ApplyPolicy()` now uses cached `_qualityTier`.
- `EnsureTelemetry()` now uses only cached `_dataVault`; DataVault replacement still arrives through `IGlobalRegistryHotSwapListener`.

Cinematic Cheats used:
- None in this patch. The domain cheat remains hardware foveation: fixed high on Quest 2-class devices, pressure-tiered FFR, and gaze-allowed VRS on capable PC VR.

Exact microseconds saved:
- Exact measured GPU microseconds saved: 0. No Quest/PC VR headset profile capture was run.
- Estimated CPU saving: below 1 us per policy sample by removing hot-path service-locator reads.
- Static hot-path GC: 0 B/frame by source audit.

Validation:
- Static forbidden-pattern scan found no `Update()`, `LateUpdate()`, `FixedUpdate()`, `foreach`, LINQ, `string.Format`, `VRSManager.Instance`, direct `RenderPipelineManager`, `new NativeArray`, `NativeArray<`, `Marshal.SizeOf`, legacy `EventBus`, managed delegate fields, object find calls, `Camera.main`, `Resources.Load`, or `StartCoroutine` in the VR commander or legacy foveation shim.
- ASCII scan reports `ASCII_OK` for `FoveatedRenderCommander.cs`.
- Duplicate `BufferID` audit returned `NO_BUFFERID_DUPLICATES`.
- Filtered build scan after cached service-lane cleanup produced no `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, `Graphics/VR`, `IScalabilityChangedEventListener`, `ScalabilityEvents`, `OnScalabilityChanged`, or `_qualityTier` diagnostics.
- Latest full `dotnet build Hecton8.Core.csproj -m:1 /nr:false /clp:ErrorsOnly` succeeded with 0 warnings and 0 errors. Evidence is stored in `Docs/AgentLogs/BuildErrors_FOVEATED_RENDER_COMMANDER.latest.txt`.

## 2026-05-16 - Escalation Polish / Foveation Downgrade Hysteresis

What was wrong:
- Low/Med/High foveation levels could downgrade on the next policy sample after pressure cleared.
- That created threshold-churn risk in VR: edge quality could pulse if `SystemStress01`, pressure tier, or thermal pressure crossed the boundary repeatedly.
- The behavior violated the state-hysteresis mandate for LOD/scalability switches.

What was done:
- Added a 2.5-second scalar downgrade hold in `FoveatedRenderCommander`.
- Pressure, thermal, and Quest 2-class upgrades still apply immediately.
- Downgrades keep the previous level while the hold is active.
- Telemetry now marks the hold with `FlagHysteresisHold`.
- Disabled/caps-missing paths clear the hold.
- High/Ultra no-pressure PC VR still clears fixed foveation so top hardware is not forced into mobile-style edge loss.

Cinematic Cheats used:
- Hardware foveation remains the cheat: Quest 2-class fixed-high FFR, Middle stress-tiered FFR, and High/Ultra gaze-allowed VRS.
- The hysteresis band is a perceptual stability cheat: it spends a few extra edge pixels for 2.5 seconds to prevent visible edge-quality flicker.

Exact microseconds saved:
- Exact measured GPU microseconds saved: 0. No Quest/PC VR headset profile capture was run.
- Estimated CPU cost: below 1 us per tick while a hold is active; no allocation and no new native buffer.
- Static hot-path GC: 0 B/frame by source audit.

Validation:
- Static forbidden-pattern scan found no `Update()`, `LateUpdate()`, `FixedUpdate()`, `foreach`, LINQ, `string.Format`, `VRSManager.Instance`, direct `RenderPipelineManager`, `new NativeArray`, `NativeArray<`, `Marshal.SizeOf`, legacy `EventBus`, managed delegate fields, object find calls, `Camera.main`, `Resources.Load`, or `StartCoroutine` in the VR commander or legacy foveation shim.
- Full `dotnet build Hecton8.Core.csproj -m:1 /nr:false /clp:ErrorsOnly` succeeded with 0 warnings and 0 errors.
