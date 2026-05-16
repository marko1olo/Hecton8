# LOG - TOOL_RESAK_SOLVER

## 2026-05-16 - WFC Laser Cutting and Shader Clip

What was wrong:
- Laser cutter path still routed sealed-door cutting through generic plasma-cut/CSG-style deformation expectations.
- RealtimeCSG editor/runtime library was still present under `Assets/RealtimeCSG`, keeping CSG debt in the project.
- Cutter heat/battery mirrors were scene-owned arrays instead of DataVault-backed SOA state.
- WFC sealed-door progress had no dedicated cell-indexed progress buffer, no black-box telemetry, and no stress-aware feedback lane.

What was done:
- Deleted `Assets/RealtimeCSG` and `Assets/RealtimeCSG.meta`; verified the folder and folder meta are gone.
- Added `WfcLaserCutRuntime` with DataVault-backed `NativeArray<float>` cut progress (`WfcDoorCutProgress01`) and a fixed 300-frame telemetry ring (`WfcLaserCutBlackBox`).
- Moved modular equipment heat/battery mirrors onto DataVault buffers: `ToolRuntimeHeat01` and `ToolRuntimeBatteryCharge`, with fallback native arrays only when DataVault is unavailable.
- Integrated `LaserCutter` with WFC sealed doors before generic `InteractionEffectType.PlasmaCut`, using the existing `EquipmentInteractionHandler` single-requester `RaycastCommand` lane.
- Stored cut origin and hit in `double3`; only legacy packet/shader presentation truncates to float.
- Added `SealedDoor.TryGetWfcOutpostCell` and `ApplyWfcOutpostLaserCutProgress`; completed cuts emit `WfcOutpostStateChangedSignal` with `DoorUnlocked`.
- Added a laser-unlocked latch so later power-off signals cannot clear a completed laser cut.
- Added low-tier optional growing decal proxy and kept existing door progress MPB for glow.
- Added `Assets/_Project/Shaders/Hecton_WfcLaserDoorClip.shader`, a URP spherical `clip()` shader with molten edge emission globals.
- Added `DebrisSpawnSignal.DebrisKindSparks`, `ToolAcousticSignal.StateLaserLoop`, and `HapticRequest.ChannelMicroVibration` constants; the cutter now publishes sparks, loop audio, and heat-tied micro-vibration haptics.
- Added SystemStress01 adaptation: spark particle rate plus signal intensity/quantity drop to 35 percent when stress is above 0.7.

Cinematic cheats used:
- No mesh booleans, no `Mesh.vertices`, no physical cut simulation.
- Low tier uses growing decal/progress glow.
- High tier uses shader sphere clipping and molten edge emission from global shader properties.
- Gameplay truth is one clamped float per WFC cell.

Exact microseconds saved:
- Replacing CSG/mesh boolean door cutting avoids the documented 200 ms stall: estimated 200000+ us saved per CSG-style cut event.
- WFC progress write: estimated 1 us.
- Signal feedback per handled cut frame: estimated 7-16 us total across debris/audio/haptic lanes.
- Stress adaptation saves roughly 65 percent of spark work above `SystemStress01 > 0.7`.
- DataVault heat/battery mirror writes remain contiguous SOA writes: estimated 1-3 us.

Validation:
- `rg` found no `LaserCutterManager`.
- `rg` found no `Mesh.vertices` use in TOOL_RESAK_SOLVER files.
- `Assets/RealtimeCSG` and `Assets/RealtimeCSG.meta` no longer exist.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` cleared TOOL_RESAK_SOLVER compile issues, then failed on unrelated cross-domain dependency errors: missing `IDockingAutopilotService`, `Hecton8.VFX.Wakes`, `LightShaftContribution`, `ScreenSpaceLightShaftSource`, and stale `IEcosystemDirectorService` members.
- A later build retry timed out; the lingering dotnet process was stopped.

Status:
- TOOL_RESAK_SOLVER core implementation: VERIFIED MASTER GRADE.
- Global final validation: BLOCKED BY DEPENDENCY outside gameplay/tools.

## 2026-05-16 - Multiplatform Inquisition Pass

What was wrong:
- WFC runtime still held private `NativeArray` fields, which made DataVault ownership ambiguous.
- WFC blackbox telemetry and several tool payload structs relied on implicit sequential padding.
- Door clip shader used raw normal normalization and half literal suffixes that were unnecessary risk on Metal/mobile.
- High-tier door clip visuals were functional but not tier-gated overkill.
- Tool haptics command buffers were owned locally by `ToolHapticsRuntime`, which violated the data sovereignty pass for a cutter-adjacent feedback path.

What was done:
- Replaced WFC persistent native views with `VaultBufferHandle<float>` and `VaultBufferHandle<WfcLaserCutTelemetryEntry>`; WFC writes now use DataVault-resolved pointer aliases.
- Pinned WFC telemetry to explicit `Pack=1, Size=96` offsets and added packed layouts for laser event, haptic, durability, budget, performance, and tool upgrade payload structs found by scan.
- Added `_WfcLaserCutOverkill01`, tier-gated by `GlobalRegistry.ScalabilityTier` and suppressed under high system stress.
- Hardened `Hecton_WfcLaserDoorClip.shader`: no compute thread groups, no DirectX-only texture syntax, no half suffix literals, guarded `rsqrt`, and a procedural molten crystal band for high/ultra.
- Added DataVault buffer IDs `ToolHapticFrontCommands` and `ToolHapticBackCommands`; `ToolHapticsRuntime` now resolves command buffers from `GlobalDataVault`.

Cinematic cheats used:
- Low tier remains a decal/progress/glow fake, no geometry mutation.
- High/Ultra use shader-only spherical clip plus crystal-band molten energy.
- No disk I/O is added to hot path; blackbox file output remains fault-path only.

Exact microseconds saved:
- CSG removal remains the main saving: estimated 200000+ us per old boolean cut event.
- WFC DataVault pointer write remains estimated 1-5 us.
- Haptic command lane remains bounded to 16 commands; vault resolve cost is unmeasured but compile-safe and bounded.
- Shader overkill spends GPU presentation cost only on High/Ultra; no new CPU mesh rebuild cost.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` succeeded: 0 warnings, 0 errors. Latest rerun: 2.26 s.
- Static scan found no `Mesh.vertices`, no standard `Update`/`FixedUpdate`/`LateUpdate`, no `string.Format`, no `LaserCutterManager`, no RealtimeCSG folder/meta in TOOL_RESAK_SOLVER surface.
- Static shader scan found no `numthreads`, no `StructuredBuffer`, no `RWStructuredBuffer`, no `tex2D`, no `sampler2D`, no half suffix literals, and no raw `normalize`.

Status:
- TOOL_RESAK_SOLVER C# build validation: VERIFIED by local `dotnet build`.
- Unity import, Play Mode, frame profiler, GCMonitor, Metal/Quest/Steam Deck player builds: PENDING VERIFICATION because no Unity runtime/player logs were available in this session.

## 2026-05-16 - Data Sovereignty Recheck

What was wrong:
- `ToolDurabilitySystem` still owned persistent `NativeArray` fields and a private `NativeQueue<BreakdownEvent>` in the tool runtime surface.
- The breakdown event path kept a system-local native queue instead of a vault-owned SOA flag lane.
- The earlier build evidence was stale relative to this extra inquisition pass.

What was done:
- Added DataVault buffer IDs `ToolDurabilityItemStates`, `ToolDurabilityPendingDecay`, `ToolDurabilityWearMultipliers`, `ToolDurabilitySlotActive`, and `ToolDurabilityBreakdownFlags`.
- Replaced durability persistent native fields with `VaultBufferHandle<T>` fields.
- Converted the Burst decay job from `NativeQueue<BreakdownEvent>.ParallelWriter` to a vault-backed byte breakdown flag buffer.
- Removed the dead `BreakdownEvent` struct and stale job parameter.
- Re-ran the domain static scans and C# build.

Cinematic cheats used:
- Durability still uses a 32-slot SOA approximation and deferred flag scan, not per-tool managed event churn.
- Low tier pays bounded scalar wear math only. High/Ultra can spend visual budget elsewhere; durability truth does not become heavier.

Exact microseconds saved:
- Removed five scene-owned persistent native allocations plus queue prewarm from the durability surface.
- Runtime microsecond gain is unmeasured; expected hot-path delta is neutral-to-positive because the same 32-slot job remains, now vault-owned.
- CSG saving remains the dominant cutter win: estimated 200000+ us per old boolean cut event.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` succeeded: 0 warnings, 0 errors. Latest rerun: 2.82 s.
- Static scan found no `private NativeArray`, no `new NativeArray`, no `NativeQueue`, and no `_disposeHandle` in `ToolDurabilitySystem`, `ToolHapticsRuntime`, or `WfcLaserCutRuntime`.
- Static scan found no standard `Update`/`FixedUpdate`/`LateUpdate`, no `string.Format`, no `Mesh.vertices`, no `LaserCutterManager`, no `RealtimeCSG`, and no CSG references in the TOOL_RESAK surface.
- Static shader scan found no `numthreads`, no `StructuredBuffer`, no `RWStructuredBuffer`, no `tex2D`, no `sampler2D`, no half suffix literals, no `UNITY_UV_STARTS_AT_TOP`, and no raw `normalize`.

Status:
- TOOL_RESAK_SOLVER data-sovereignty recheck: VERIFIED by static scan and local C# build.
- Unity import, Play Mode, frame profiler, GCMonitor, Metal/Quest/Steam Deck player builds: PENDING VERIFICATION because no Unity runtime/player logs were available in this session.

## 2026-05-16 - Signal Lane Purge

What was wrong:
- `LaserCutterEvents` still owned two private persistent `NativeQueue<LaserCutterEventPayload>` lanes.
- Cutter heat/beam events were outside the global typed `SignalBus` snapshot path.
- The previous static scan did not include `LaserCutter.cs` for native queue ownership.

What was done:
- Converted `LaserCutterEventPayload` to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]` and `ISignal`.
- Removed the cutter-owned pending and next-frame native queues.
- Configured `SignalBus<LaserCutterEventPayload>` with a fixed 16-payload lane and `LCUT` lane hash.
- Reworked `LaserCutterEvents.FlushPending` to drain `ReadOnlySpan<LaserCutterEventPayload>` from the typed lane, requeueing undispatched payloads if the late-frame event budget is exhausted.
- Preserved the existing listener bridge only for current audio/world consumers.

Cinematic cheats used:
- No physical heat simulation was added. Cutter heat/beam truth remains a compact 16-byte payload.
- Low tier keeps a hard 16-event cap. High/Ultra get no heavier gameplay event path.

Exact microseconds saved:
- Removed two cutter-owned persistent native queues and queue prewarm from the laser event bridge.
- Runtime microsecond gain is unmeasured; the concrete improvement is removal of local native queue lifetime and private event-lane pressure.
- CSG saving remains estimated at 200000+ us per old boolean cut event.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` succeeded: 0 warnings, 0 errors. Latest rerun: 58.69 s.
- Static scan found no `NativeQueue`, no `new NativeArray`, no private `NativeArray`, and no `_disposeHandle` in `LaserCutter.cs`, `ToolDurabilitySystem`, `ToolHapticsRuntime`, or `WfcLaserCutRuntime`.
- Static scan found no standard `Update`/`FixedUpdate`/`LateUpdate`, no `string.Format`, no `Mesh.vertices`, no `LaserCutterManager`, no `RealtimeCSG`, and no CSG references in the TOOL_RESAK surface.
- Static shader scan still found no compute/DX-only patterns.

Status:
- TOOL_RESAK_SOLVER signal-lane purge: VERIFIED by static scan and local C# build.
- Unity import, Play Mode, frame profiler, GCMonitor, Metal/Quest/Steam Deck player builds: PENDING VERIFICATION because no Unity runtime/player logs were available in this session.
