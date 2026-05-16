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
