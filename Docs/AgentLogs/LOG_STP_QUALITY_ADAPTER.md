# STP_QUALITY_ADAPTER Log

## 2026-05-16 - STP Dynamic Resolution Adapter

What was wrong:
- Dynamic-resolution policy was split between world runtime scale and thermal-only graphics logic.
- No registry-facing `IResolutionScalerService` existed for render-scale policy.
- UI/HUD render textures could multiply by 3D render scale, which would let STP blur text.
- Stress smoothing was managed/local and not a native DataVault handoff.
- Final compile validation is blocked by external core project/tether contract churn.

What was done:
- Moved the adapter into `Assets/_Project/Scripts/Graphics/Scalability/`.
- Added `IResolutionScalerService`, `ResolutionScaleState`, `BufferID.ResolutionScaleState`, and `SystemID.GraphicsScalability`.
- Registered `GlobalRegistry.ResolutionScaler` and kept the old `IDynamicResolutionRuntime` writer path as the render-scale sink.
- Added one-frame-latent Burst EWMA for `SystemStress01`.
- Stored current scale, target scale, stress, tier, STP intent, sharpen, and AUP lock state in persistent native state.
- Implemented low-tier 0.5 base scale and 0.35 emergency scale.
- Kept high/ultra base at 1.0 with STP intent active for AA.
- Emitted `ResolutionChangedSignal` only on render-scale movement above 5 percent.
- Drove `_SharpenIntensity` from render-scale deficit.
- Removed HUD RT multiplication by 3D dynamic resolution while preserving valid UI/diegetic `targetTexture` paths.
- Added 300-frame blackbox telemetry with `CurrentRenderScale` and `StpActive`, dumping to `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER.bin` on NaN.
- Locked scale changes for three frames on `AupShiftSignal`.
- Static motion-vector check found no silt/bubble motion-vector writers; project VFX hit was debris `ForceNoMotion`.

Cinematic cheats used:
- Pixel-count fake: low-tier internal render scale 0.5, emergency 0.35, reconstructed by STP instead of native resolution.
- Temporal stability cheat: EWMA stress and AUP lock avoid scale yo-yo/history smearing.
- Sharpen cheat: one scalar increases perceived detail instead of adding a compensation pass.

Exact microseconds saved:
- Not measured. Source estimates recorded in `Docs/Tasks/Status_STP_QUALITY_ADAPTER.md`: 0-2 us/frame per adapter task, with real pixel savings dependent on GPU/resolution. Low-tier 0.5 scale is 25 percent pixel area; 0.35 is roughly 12 percent pixel area before STP.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore` attempt 1 failed on unrelated AI/tether/visor compile wall.
- Attempt 2 failed on duplicate tether signal definitions.
- Attempt 3 with restore failed because `Hecton8.Core.csproj` references missing `Assets/_Project/Scripts/Physics/Tethers/Contracts/TetherSignalContracts.cs`.
- Final validation: BLOCKED BY DEPENDENCY. No STP adapter compiler errors were reached in the logged gates.

## 2026-05-16 - Escalation Polish / Data Sovereignty Pass

What was wrong:
- The first pass still had adapter-owned persistent `NativeArray` fields for scale fallback and blackbox telemetry.
- Sequential native layouts left room for platform padding ambiguity on ARM64/Quest.
- The EWMA completion path could force a main-thread sync at the start of Tick.

What was done:
- Evicted STP telemetry into `GlobalDataVault` as `BufferID.ResolutionScaleTelemetry`.
- Removed adapter-owned persistent native arrays and the fallback scale buffer; the adapter now borrows DataVault views only.
- Converted STP/thermal render-state structs to explicit `Pack=1` layouts: 64B `ResolutionScaleState`, 48B `DrsTelemetryEntry`, 24B `DynamicResolutionRuntimeSnapshot`, and 20B `HardwareThermalSnapshot`.
- Changed hot-path EWMA completion to non-blocking unless teardown or DataVault hotswap forces structural sync.
- Re-ran static scans: no private `NativeArray`, no direct `new NativeArray`, no `Update`/`LateUpdate`/`FixedUpdate`, no managed event/delegate path in `Graphics/Scalability`.

Cinematic cheats used:
- Same pixel-count fake remains: low-tier 0.5 scale, emergency 0.35, STP reconstruction.
- Same reactive sharpen scalar remains; no extra post pass was added.
- High/Ultra remains 1.0 scale with STP/DLAA intent so visual overkill is left to downstream volumetric/silt/hull/particle systems instead of this policy layer stealing bandwidth.

Exact microseconds saved:
- Not measured. Source estimate changed only in failure/stall risk: local native allocation ownership removed, and Tick no longer forces EWMA completion unless already finished. The per-frame adapter estimate remains source-only at roughly 0-2 us by task.

Validation:
- `git diff --check` returned no whitespace errors for the STP-touched files; repository-wide check only reported existing CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore` attempt 4 failed outside this domain: `SargassumMicroFaunaBoids.cs` missing `EnsureVaultBufferHandle`, and `VehicleDockingModule.cs` missing `CacheFluidRuntime`/`ResetDockingRuntimeCaches`.
- No STP adapter or STP contract compiler errors appeared before the external wall.

## 2026-05-16 - Visual Budget Pass

What was wrong:
- The STP adapter preserved frame time on weak hardware but did not expose a direct high-tier visual budget.
- High/Ultra thermal max could collapse too far toward a mobile-grade render scale.
- Runtime scalability override was not the first source for STP tier selection.

What was done:
- Added `VisualOverkill01`, `DearLie01`, and `VisualFeatureFlags` to the existing 64B DataVault `ResolutionScaleState`.
- Published epsilon-gated shader globals for STP scale, scale deficit, dear-lie mode, visual overkill, and feature flags.
- Routed `_HectonVisorFluidVisualOverkill` from the adapter so visor-fluid salt/silt shader paths can consume the same budget when not overridden by their render feature.
- Raised High thermal max to 0.90 and Ultra thermal max to 1.0.
- Switched tier resolution to `GlobalRegistry.ScalabilityTier` before hardware-profile fallback.

Cinematic cheats used:
- Toaster mode: explicit `DearLie01=1`, low-tier render scale 0.5, emergency 0.35, no overkill flags.
- God-mode: visual feature flags advertise visor salt crystals, volumetric silt, procedural hull dents, 16-tap POM, SSS, and raymarched fog consumers.

Exact microseconds saved:
- Not measured. No new render pass, compute dispatch, file I/O, or native allocation was added. Shader-global writes are threshold-gated; source estimate remains inside the previous 1 us/frame reactive-VFX budget except on value changes.

Validation:
- Static scans found no private persistent `NativeArray`, no direct `new NativeArray`, no managed event/delegate path, and no `Update/LateUpdate/FixedUpdate` in `Assets/_Project/Scripts/Graphics/Scalability/`.
- `git diff --check` for STP-touched code returned no whitespace errors.
- Compile attempt 5 failed outside STP with 141 errors in Fauna/Bootstrap/Tools/HectonUnderwaterVisuals. No STP adapter or `ResolutionScaleState` errors appeared in the log.

## 2026-05-16 - Loop 8 / Native View Eviction + Compile Pass

What was wrong:
- The adapter had no persistent `NativeArray` ownership, but it still declared borrowed `NativeArray<T>` views in the source and the EWMA job payload.
- The one-frame job pointer lifetime was not explicitly fenced against DataVault compaction.
- Compile status was stale: previous gates were blocked by other domains.

What was done:
- Removed all `NativeArray<T>` declarations from `ThermalDynamicResolutionAdapter.cs`.
- Converted scale-state and telemetry access to DataVault `VaultBufferHandle<T>.ResolvePointer()` views.
- Added a DataVault `TryLockBuffer(BufferID.ResolutionScaleState)` / `TryUnlockBuffer` fence around the cross-frame Burst EWMA job.
- Enabled unsafe code in `Hecton8.Graphics.Scalability.asmdef` for the native pointer path.
- Re-ran static scans for `NativeArray<T>`, local persistent allocation, `Update/LateUpdate/FixedUpdate`, managed events/delegates, `string.Format`, and legacy blit/execute paths.
- Re-ran shader/compute scan for platform hazards; the STP adapter owns no compute dispatch or DirectX-only render path.

Cinematic cheats used:
- Toaster mode remains `DearLie01=1`, 0.5 base render scale, 0.35 emergency scale, and threshold-gated sharpen.
- God-mode remains 1.0 scale on High/Ultra with published flags for visor salt, volumetric silt, hull dents, 16-tap POM, SSS, and raymarched fog consumers.

Exact microseconds saved:
- Not measured. No profiler was run. Loop 8 is a data-sovereignty and compaction-safety fix, not a claimed frame-time win.
- Source estimate remains unchanged: adapter hot-path work is expected inside the previous 0-2 us/frame task estimates, with real savings coming from pixel-count reduction at low scale.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -maxcpucount:1 -p:UseSharedCompilation=false` passed in 4.30s with 0 warnings and 0 errors.
- Build log: `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt6_no_restore.txt`.
- Unity import, Play Mode, player build, profiler frame-time, GC, memory, and visual captures remain PENDING VERIFICATION.

## 2026-05-16 - Loop 9 / ABI Guard + Fault Dump Compaction

What was wrong:
- Packed STP and thermal structs were explicit, but the adapter did not verify runtime ABI sizes before touching DataVault-backed pointers.
- The NaN blackbox dump still serialized every field through `BinaryWriter` instead of writing the packed telemetry ring as the binary artifact it already is.
- Latest source validation had to be re-run after more agents changed the project; the prior clean compile was no longer enough evidence for current disk state.

What was done:
- Added a cold startup ABI guard for 48B `DrsTelemetryEntry`, 64B `ResolutionScaleState`, 20B `HardwareThermalSnapshot`, and 24B `DynamicResolutionRuntimeSnapshot`.
- On ABI mismatch, the STP adapter publishes a math-guard telemetry fault, disables itself, and avoids writing Unity render scale.
- Replaced `BinaryWriter` fault dump writes with a 20B little-endian header plus fixed 48B little-endian telemetry records staged through a contiguous stack span.
- Re-ran adapter-domain scans: no `NativeArray<T>`, no `new NativeArray`, no `Allocator.Persistent`, no `EventBus`, no managed delegate/event, no `string.Format`, and no `Update()` in `ThermalDynamicResolutionAdapter.cs`.
- Re-ran duplicate-signal scan: one definition each for `AupShiftSignal`, `ResolutionChangedSignal`, `HUDNotificationSignal`, `ThermalStateChangedSignal`, `SystemHealthSignal`, and `FrameTimeSignal`.
- Verified Unity MCP resources/templates are empty in this session; no Editor import, Play Mode, player build, profiler, GC, memory, or visual capture evidence is available from MCP.
- Performed one narrow cross-domain compile-wall repair in `Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`: added the missing packed-struct validator that an existing diagnostics `Awake()` call required.

Cinematic cheats used:
- Toaster mode remains `DearLie01=1`, low-tier 0.5 scale, emergency 0.35 scale, threshold sharpen, and no expensive visual flags.
- God-mode remains 1.0 scale on High/Ultra with STP-published flags for visor salt crystals, volumetric silt, procedural hull dents, 16-tap POM, SSS, and raymarched fog consumers.
- Fault-path binary dump is a data cheat: compact packed telemetry, not text, to keep Steam Deck/MicroSD crash capture lean.

Exact microseconds saved:
- Not measured. No Unity profiler, player build, or GC capture was run.
- Source-only impact: cold ABI validation adds no per-frame work; blackbox dump compaction reduces fault-path I/O calls, not normal-frame cost.
- Existing task estimates remain unchanged: 0-2 us/frame source estimates by checklist item, with real visual savings coming from pixel-count reduction at low scale.

Validation:
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt7_no_restore.txt`: failed outside STP on missing diagnostics helper methods.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt8_no_restore.txt`: failed before C# analysis because `Temp/obj/Hecton8.Core/project.assets.json` was missing.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_restore_attempt9.txt`: restore succeeded.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt9_no_restore.txt`: failed with 18 external Construction errors in `DroneFleetManager.cs` and `DroneCognitionJob.cs`; no STP adapter/scalability contract errors appeared.
- `git diff --check` on touched STP/diagnostics/doc files reported line-ending warnings only, no whitespace errors.

## 2026-05-16 - Loop 10 / DataVault Pointer Race Removal

What was wrong:
- `ResolutionScaleState` was locked while the EWMA Burst job owned the pointer, but the next `Tick()` could still resolve and write the same pointer if the job had not completed.
- Telemetry ring writes used raw DataVault pointers without a short lock around the immediate heartbeat/dump write.
- The previous compile wall shifted again; current disk state needed fresh validation evidence.

What was done:
- `Tick()` now skips `ResolutionScaleState*` resolution and DataVault scale-state writes while `_stressEwmaScheduled` is still true after the non-blocking completion check.
- `TryGetScaleState()` now returns false during an in-flight EWMA job instead of reading a possibly torn state.
- Added `TryLockTelemetryPointer()` and fenced STP telemetry writes/dumps with `TryLockBuffer(BufferID.ResolutionScaleTelemetry)` / `TryUnlockBuffer`.
- Kept the no-stall job discipline: no unconditional `Complete()` was added to hot `Tick()`.
- Re-ran adapter scans for local NativeArrays, persistent allocations, EventBus, managed delegates/events, string formatting, `Update()` surfaces, and unsafe math debt.
- Re-ran layout and shader-platform scans. STP structs remain explicit `Pack=1`; the adapter owns no compute shader and no DirectX-only shader path.

Cinematic cheats used:
- Low/toaster remains render-scale 0.5, emergency 0.35, STP reconstruction, sharpen scalar, and `DearLie01=1`.
- High/Ultra remains full scale with visual budget flags for visor salt, volumetric silt, hull dents, 16-tap POM, SSS, and raymarched fog consumers.
- No new simulation or render pass was added; pointer fencing bought stability, not spectacle.

Exact microseconds saved:
- Not measured. No profiler, player build, GC capture, or Unity timeline exists for this loop.
- Source-only impact: race removal and avoided forced job completion; one telemetry lock/unlock was added around heartbeat/dump writes.

Validation:
- Adapter static scan produced no hits for `NativeArray<T>`, `new NativeArray`, `Allocator.Persistent`, `EventBus`, managed delegate/event, `string.Format`, `Update/LateUpdate/FixedUpdate`, `.normalized`, or `math.normalize`.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt10_no_restore.txt`: empty log, process exit `-1`; not accepted as source evidence.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt11_no_restore.txt`: failed with 3 external errors in `PhysicsApplySystem.cs` on `_queueHash` and `PendingEventCapacity`; no STP adapter/scalability contract errors appeared.
- Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION.

## 2026-05-16 - Loop 11 / Blackbox Retry Fix + Gate 15 Evidence

What was wrong:
- The STP blackbox dump flag could be set before the dump file write was fully proven, suppressing a later retry after a transient I/O failure.
- Validation evidence was stale again because the shared project changed after loop 10.

What was done:
- Moved `_blackBoxDumped = true` to the end of `DumpBlackBoxOnceLocked()` after the 20B header and one contiguous telemetry body are written.
- Collapsed fault dump I/O to two stream writes: one header write and one body write.
- Re-ran adapter scans for local `NativeArray<T>`, persistent local allocation, `EventBus`, managed delegates/events, `string.Format`, `Update/LateUpdate/FixedUpdate`, unsafe normalization debt, and explicit packed layouts.
- Waited for overlapping external `dotnet build` processes to clear before running another isolated STP gate.
- Updated the task status and rationale with attempt 12-15 evidence instead of claiming runtime verification.

Cinematic cheats used:
- Toaster mode remains render-scale 0.5, emergency 0.35, STP reconstruction, threshold sharpen, and `DearLie01=1`.
- God-mode remains full scale with visual budget flags for visor salt crystals, volumetric silt, procedural hull dents, 16-tap POM, SSS, and raymarched fog consumers.
- Fault capture stays binary and fixed-size enough for Steam Deck/MicroSD survival; no JSON or text dump was added.

Exact microseconds saved:
- Not measured. No profiler, player build, GC capture, or Unity timeline exists.
- Loop 11 changes are fault-path reliability and source-validation work; normal-frame hot-path cost is unchanged by the blackbox flag move. Fault-path I/O is two writes, source-verified but not profiled.

Validation:
- Adapter static scan produced no hits for `NativeArray<T>`, `new NativeArray`, `Allocator.Persistent`, `EventBus`, managed delegate/event, `string.Format`, `Update/LateUpdate/FixedUpdate`, `.normalized`, or `math.normalize`.
- Layout scan confirms explicit `Pack=1` records: `DrsTelemetryEntry` 48B, `ResolutionScaleState` 64B, `HardwareThermalSnapshot` 20B, and `DynamicResolutionRuntimeSnapshot` 24B.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt12_outdir.txt`: build succeeded, 0 errors, 4 warnings in `ArchitectEyeVisualizer.cs`.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt13_outdir.txt`: build failed with 23 external errors in `World/EcosystemDirector.cs`; no STP adapter/scalability contract errors appeared before the wall.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt14_outdir.txt`: did not reach C#; `Temp/obj/Hecton8.Core/project.assets.json` was missing.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_restore_attempt15.txt`: restore succeeded.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt15_outdir.txt`: build succeeded in 4.51s with 0 warnings and 0 errors after the blackbox two-write patch.
- Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION.

## 2026-05-17 - Loop 12 / DataVault Scale-State Lock Closure

What was wrong:
- Main-thread `ResolutionScaleState` reads/writes still resolved a raw DataVault pointer without a short lock, even though the EWMA job path and telemetry path were fenced.
- The unlocked telemetry resolver remained as dead code after the loop 11 changes.

What was done:
- Removed the long-lived main-thread scale pointer from `Tick()`.
- `TryGetScaleState()` and `UpdateScaleState()` now lock `BufferID.ResolutionScaleState` with `SystemID.GraphicsScalability`, resolve the pointer, copy/read one record, and unlock in `finally`.
- `ScheduleStressEwmaJob()` now owns its own `ResolutionScaleState` lock acquisition and keeps the cross-frame lock until `CompletePendingStressJob()`.
- Removed the obsolete unlocked telemetry resolver.
- Re-ran domain scans for `NativeArray<T>`, `new NativeArray`, `Allocator.Persistent`, EventBus/delegate/event paths, `string.Format`, `Update/LateUpdate/FixedUpdate`, unsafe normalization debt, duplicate STP signals, explicit packed layouts, and STP-owned shader/platform hazards.

Cinematic cheats used:
- Toaster mode remains render-scale 0.5, emergency 0.35, STP reconstruction, sharpen scalar, and `DearLie01=1`.
- High/Ultra remain full-scale with visual budget flags for visor salt crystals, volumetric silt, procedural hull dents, 16-tap POM, SSS, and raymarched fog consumers.
- No new render pass, compute pass, physics simulation, or shader variant was added.

Exact microseconds saved:
- Not measured. No Unity profiler, player build, GC capture, or timeline exists for this loop.
- Loop 12 is a pointer-lifetime correctness fix. It adds a short lock/unlock around one 64B state read/write and removes an unused unlocked resolver; no frame-time savings are claimed.

Validation:
- Adapter scan has no local `NativeArray<T>`, `new NativeArray`, `Allocator.Persistent`, `EventBus`, managed delegate/event, `string.Format`, `Update/LateUpdate/FixedUpdate`, `.normalized`, or `math.normalize`.
- The only zero-GC scan hit is the Android-only cold `List<XRDisplaySubsystem>[4]` scratch buffer, preallocated under `UNITY_ANDROID && !UNITY_EDITOR` and reused only on scale changes.
- Duplicate-signal scan found exactly one definition each for `SystemHealthSignal`, `FrameTimeSignal`, `AupShiftSignal`, `ResolutionChangedSignal`, `HUDNotificationSignal`, and `ThermalStateChangedSignal`.
- Layout scan confirms explicit `Pack=1` records: `DrsTelemetryEntry` 48B, `ResolutionScaleState` 64B, `HardwareThermalSnapshot` 20B, and `DynamicResolutionRuntimeSnapshot` 24B.
- Shader/platform scan found no STP-owned compute shader, `numthreads`, DirectX-only shortcut, legacy blit, `ScriptableRenderPass.Execute`, or unsafe RenderGraph path.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt16_outdir.txt`: failed with 11 external errors in UI Navigation, Gameplay motor, Tether, and Interaction contracts; no STP adapter/scalability contract errors appeared.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt17_outdir.txt`: failed with one external `Interaction/EquipmentInteractionContracts.cs` conversion error; no STP adapter/scalability contract errors appeared.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt18_outdir.txt`: failed with three external `HectonPlayerMovement.cs` `MethodImpl` errors; no STP adapter/scalability contract errors appeared.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt19_outdir.txt`: build passed in 2.89s with 0 warnings and 0 errors after the external compile wall cleared.
- Current full-project source validation is source-green on gate 19; Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION.

## 2026-05-17 - Loop 13 / Burst EWMA Profiler Marker

What was wrong:
- `SystemStressEwmaJob` was Burst-compiled but had no profiler marker, so Unity profiler captures would not attribute the STP worker cost cleanly.

What was done:
- Added `Unity.Profiling.ProfilerMarker` instrumentation inside `SystemStressEwmaJob.Execute()`.
- Added `Unity.Profiling.Core` to `Hecton8.Graphics.Scalability.asmdef`.
- Re-ran the STP-domain scans for local NativeArrays, persistent allocator use, legacy events/delegates, string formatting, Unity `Update` methods, unsafe normalization debt, shader/RenderGraph hazards, and duplicate typed signal lanes.

Cinematic cheats used:
- No visual or policy change. Toaster remains 0.5 base / 0.35 emergency with STP reconstruction and sharpen; High/Ultra remain full-scale with visual-overkill flags for visor salt, volumetric silt, procedural hull dents, 16-tap POM, SSS, and raymarched fog consumers.

Exact microseconds saved:
- Not measured. This loop adds profiler attribution for a worker job and does not claim frame-time savings.

Validation:
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt20_outdir.txt`: command timed out after 185s with an empty log and no output DLL; classified as build-lane contention, not source evidence.
- `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt21_outdir.txt`: build passed in 4.40s with 0 warnings and 0 errors.
- Static scan of `Assets/_Project/Scripts/Graphics/Scalability/` has no local `NativeArray<T>`, `new NativeArray`, `Allocator.Persistent`, `EventBus`, managed delegate/event, `string.Format`, `Update/LateUpdate/FixedUpdate`, `.normalized`, `math.normalize`, STP-owned compute shader, legacy blit, or RenderGraph compatibility path.
- Duplicate signal scan still finds one definition each for `SystemHealthSignal`, `FrameTimeSignal`, `AupShiftSignal`, `ResolutionChangedSignal`, `HUDNotificationSignal`, and `ThermalStateChangedSignal`.
- Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION because Unity MCP resources are empty in this session.

## 2026-05-17 - Loop 14 / Burst Marker Shape Tightening

What was wrong:
- The STP EWMA profiler marker used `ProfilerMarker.Auto()` inside the Burst job body. Dotnet accepted it, but `Auto()` is an `IDisposable` scope shape; explicit marker bracketing is cleaner for Unity job/Burst validation.

What was done:
- Replaced `using (Marker.Auto())` with `Marker.Begin()` / `Marker.End()`.
- Removed the early return from `SystemStressEwmaJob.Execute()` so the marker always closes.
- Kept the EWMA math, DataVault pointer ownership, render-scale policy, signal lanes, visual-overkill flags, and telemetry format unchanged.

Cinematic cheats used:
- No visual-policy change. Low/MX350/Quest remain 0.5 base / 0.35 emergency with STP reconstruction and sharpen. High/Ultra remain full-scale with visor salt, volumetric silt, procedural hull dents, 16-tap POM, SSS, and raymarched fog consumer flags.

Exact microseconds saved:
- Not measured. This is Unity Burst/import risk reduction and profiler-shape cleanup, not a frame-time optimization claim.

Validation:
- No dotnet gate was run in this loop because the operator explicitly requested not to rebuild on every pass.
- Static scan found no `ProfilerMarker.Auto`, local `NativeArray<T>`, `new NativeArray`, `Allocator.Persistent`, `EventBus`, managed delegate/event, `string.Format`, `Update/LateUpdate/FixedUpdate`, `.normalized`, `math.normalize`, STP-owned compute/threadgroup path, legacy blit, or RenderGraph compatibility path in `Assets/_Project/Scripts/Graphics/Scalability/`.
- `git diff --check` reported only CRLF conversion warnings.
- Latest compile evidence remains gate 21 before this loop: 0 warnings, 0 errors. Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION.

## 2026-05-17 - Loop 15 / DataVault Registry Poll Removal

What was wrong:
- Scale-state and telemetry ensure paths still read `GlobalRegistry.DataVault` during steady-state operation despite the adapter owning hot-swap listener callbacks.

What was done:
- `TryEnsureScaleStateHandle()` now uses cached `_dataVault` unless the cache is null.
- `TryEnsureTelemetryHandle()` now uses cached `_dataVault` unless the cache is null.
- The cold fallback still binds `GlobalRegistry.DataVault` if the adapter starts before the vault is available.
- DataVault buffer ownership, lock/unlock owner id, STP telemetry format, render-scale policy, and signal lanes were not changed.

Cinematic cheats used:
- No visual-policy change. Low/MX350/Quest remain 0.5 base / 0.35 emergency with STP reconstruction and sharpen. High/Ultra remain full-scale with visual-overkill consumer flags.

Exact microseconds saved:
- Not measured. This removes a small source-level registry touch from the hot ensure path, but no profiler capture exists.

Validation:
- No dotnet gate was run in this loop because the operator explicitly requested not to rebuild on every pass.
- Static scan shows only two `GlobalRegistry.DataVault` reads in the adapter, both guarded by `_dataVault == null`.
- Static scan found no `ProfilerMarker.Auto`, local `NativeArray<T>`, `new NativeArray`, `Allocator.Persistent`, `EventBus`, managed delegate/event, `string.Format`, `Update/LateUpdate/FixedUpdate`, `.normalized`, `math.normalize`, STP-owned compute/threadgroup path, legacy blit, or RenderGraph compatibility path in `Assets/_Project/Scripts/Graphics/Scalability/`.
- `git diff --check` reported only CRLF conversion warnings.
- Latest compile evidence remains gate 21 before loops 14-15: 0 warnings, 0 errors. Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION.

## 2026-05-17 - Loop 16 / Runtime Bridge Clamp Repair and Signal Audit

What was wrong:
- The STP adapter's low-tier emergency policy targets 0.35, but `World/DynamicResolutionScaler.ApplySystemOverrideRenderScale()` clamped registry-owned system overrides to 0.5.
- That meant the adapter, DataVault, shader globals, and HUD notification path could report the 0.35 emergency policy while the actual registry render-scale writer refused to go below 0.5.
- `ScalabilityEvents` needed a source audit because it is not a `SignalBus<T>` lane.

What was done:
- Changed only `SystemOverrideMinimumRenderScale` in `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs` from 0.5f to 0.25f.
- Left the old autonomous scaler's quality floor intact; the change applies only to STP/system override calls through `IDynamicResolutionRuntime`.
- Audited `Core/IPlatformIntegration.cs` and confirmed `ScalabilityEvents` is a typed `NativeQueue<ScalabilityChangedEvent>` lane drained by `SystemDispatcher`, not a legacy string EventBus.
- Re-ran static scans for STP-domain `NativeArray<T>`, local persistent allocations, EventBus/delegate/event paths, `string.Format`, Unity `Update/LateUpdate/FixedUpdate`, unsafe normalization, STP-owned compute/threadgroup paths, legacy blits, and RenderGraph hazards.

Cinematic cheats used:
- Low/MX350/Quest emergency scale can now actually reach 0.35 through the runtime bridge.
- High/Ultra policy stayed full-scale with visual-overkill consumer flags for visor salt, volumetric silt, procedural hull dents, 16-tap POM, SSS, and raymarched fog.

Exact microseconds saved:
- Not measured. This is a correctness fix for pixel-count reduction, not a measured CPU optimization.
- Expected GPU gain is proportional to active pixel-count reduction only when the 0.35 path is reached; no profiler capture exists.

Validation:
- No dotnet gate was run in this loop because the operator explicitly requested not to rebuild on every pass.
- Static scan of `Assets/_Project/Scripts/Graphics/Scalability/` found no forbidden STP-domain hits.
- Runtime bridge scan confirms `SystemOverrideMinimumRenderScale = 0.25f` and `ApplySystemOverrideRenderScale()` clamps system overrides through that lower bound.
- `git diff --check` reported only CRLF conversion warnings.
- Latest compile evidence remains gate 21 before loops 14-16: 0 warnings, 0 errors. Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION.

## 2026-05-17 - Loop 17 / Direct Fallback Render-Scale Repair

What was wrong:
- `ThermalDynamicResolutionAdapter.ApplyDirectRenderScale(renderScale, bufferScale)` ignored its `renderScale` argument.
- If `GlobalRegistry.DynamicResolutionRuntime` was absent, the fallback resized scalable buffers but could leave `UniversalRenderPipelineAsset.renderScale` unchanged.
- Scene/prefab camera YAML currently shows `m_AllowDynamicResolution: 0`, so relying on camera dynamic-resolution flags alone is not a complete fallback.

What was done:
- Added `renderScale = ClampRenderScale(renderScale)` in `ApplyDirectRenderScale()`.
- Added `_urpAsset.renderScale = renderScale` before `ScalableBufferManager.ResizeBuffers(...)` when the URP asset exists.
- Left the registry runtime path unchanged because `DynamicResolutionScaler.ApplyRenderScale()` already writes URP asset render scale.

Cinematic cheats used:
- No policy change. Low/MX350/Quest remain 0.5 base / 0.35 emergency with STP reconstruction and sharpen.
- High/Ultra remain full-scale with visual-overkill consumer flags.

Exact microseconds saved:
- Not measured. This is fallback correctness, not a measured frame-time optimization.

Validation:
- No dotnet gate was run in this loop because the operator explicitly requested not to rebuild on every pass.
- Static scan found no forbidden STP-domain hits after the change.
- Direct fallback now writes both URP asset render scale and scalable-buffer scale.
- `git diff --check` reported only CRLF conversion warnings.
- Latest compile evidence remains gate 21 before loops 14-17: 0 warnings, 0 errors. Unity import, Play Mode, player build, profiler, GC, memory, and visual captures remain PENDING VERIFICATION.
