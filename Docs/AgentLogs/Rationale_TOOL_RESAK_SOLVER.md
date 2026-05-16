# Rationale - TOOL_RESAK_SOLVER

Current state: TOOL SIGNAL/SPAN PURGE STATIC VERIFIED - GLOBAL BUILD BLOCKED BY UI/ECOSYSTEM DEPENDENCIES

## Decision Log

### 2026-05-16 - Baseline
Problem: Existing implementation unknown. Task requires removal of CSG and replacement with WFC progress plus shader/decal clipping without crossing domain ownership.
Solution: Read project contracts and scan live code before editing. Use existing interfaces/signals/DataVault if present; add minimal local contracts only when no project contract exists.
Rejected Alternatives: Blind replacement or invented service graph. Standard Unity mesh boolean/path using Mesh.vertices is forbidden by prompt and stalls the main thread.
Scalability potential: Low uses decal/progress fake; Middle keeps deterministic progress with modest sparks; High uses shader sphere clip; Ultra can increase molten edge richness without increasing gameplay truth.
Hardware Impact: Expected low-end gain is removal of 200 ms CSG stall. Exact microseconds are PENDING VERIFICATION until compile and runtime profiling.

### 2026-05-16 - Loop 1 Tasks 1-5
Problem: The old cutter path queued generic plasma-cut work and allowed voxel/CSG-style deformation to remain the perceived solution for sealed doors.
Solution: Route WFC sealed doors through the existing `EquipmentInteractionHandler` single-requester `RaycastCommand` lane, then branch in `LaserCutter` before `InteractionEffectType.PlasmaCut`. Store origin/hit in `double3` and pass only presentation floats to shader globals.
Rejected Alternatives: Direct `Physics.Raycast`, per-door managers, mesh boolean cuts, and `Mesh.vertices` mutation. Those approaches add main-thread stalls, create singleton coupling, or violate the batch prompt.
Scalability potential: Low uses an optional growing decal plus sealed-door progress MPB. Middle keeps sparks/audio/haptics. High clips by shader sphere. Ultra can author richer molten materials on the same globals without changing gameplay truth.
Hardware Impact: MX350/i3 path removes CSG editor/runtime DLL load and avoids mesh rebuilds; expected saving remains 200000+ us versus boolean cuts, with added hot-path work below 100 us because state is one cell float plus signal writes.

Problem: Cutter heat and battery were still backed by tool-local native mirrors, so downstream systems could not audit them through the vault.
Solution: Move `ModularEquipmentEngine` heat and battery mirrors to `GlobalDataVault` buffers (`ToolRuntimeHeat01`, `ToolRuntimeBatteryCharge`) with the existing fixed NativeArray fallback only if the vault is unavailable.
Rejected Alternatives: Adding a laser-only duplicate buffer in `LaserCutter`. That would create divergent truth from the modular equipment service.
Scalability potential: Low and High tiers read the same compact SOA; visuals can scale without re-querying tool components.
Hardware Impact: Removes per-system ownership drift and keeps mirror writes contiguous. Expected hot-path delta is neutral to positive; vault-backed cache lines replace two scene-owned arrays.

### 2026-05-16 - Loop 2 WFC SOA, Visuals, Signals
Problem: Sealed-door cutting needed persistent WFC truth without mesh mutation and without inventing a new manager.
Solution: Add `WfcLaserCutRuntime` as a static data-oriented helper that resolves the WFC cell from `SealedDoor`, writes `CutProgress01` to a DataVault `NativeArray<float>`, records a 300-frame black box, and drives existing `GlobalSignals`.
Rejected Alternatives: Door-owned dictionaries, scriptable singleton managers, and spawning VFX/audio objects directly. Those choices allocate or couple gameplay to presentation owners.
Scalability potential: Low only scales an optional decal and door MPB progress. Middle emits modest sparks/audio/haptics. High and Ultra use shader globals to make authored door materials clip and glow without changing gameplay data.
Hardware Impact: One cell float write plus three compact signal pushes replaces CSG mesh rebuilds. MX350 expected saving remains 200000 us class; added CPU work expected below 100 us in the cutting frame.

Problem: Power unlock and laser-cut unlock shared the same WFC `DoorUnlocked` bit, so later power-off telemetry could undo a completed laser cut.
Solution: Add a private `_wfcOutpostLaserUnlocked` latch in `SealedDoor`; power updates may clear power state, but they do not clear a laser-completed unlock.
Rejected Alternatives: Adding a new persistent flag bit outside the documented mutable mask, or publishing duplicate state corrections each frame.
Scalability potential: The latch is local and branch-only; all tiers get deterministic persistence behavior.
Hardware Impact: Negligible CPU cost; prevents repeated state churn and duplicated persistence writes.

### 2026-05-16 - Loop 3 Stability and Build Wall
Problem: Feedback tasks required sparks, audio, haptics, stress adaptation, and postmortem proof without widening runtime ownership.
Solution: Add constants to existing signal structs, publish `DebrisSpawnSignal(Sparks)`, `ToolAcousticSignal(LaserLoop)`, and `HapticRequest(MicroVibration)`, clamp all progress with `math.saturate`, and dump `Dump_TOOL_RESAK_SOLVER.bin` only on invalid numeric state.
Rejected Alternatives: Local prefab instantiation, local audio-only state, or `Debug.Log` as the black box. Standard Unity instantiation would allocate and break lane segregation.
Scalability potential: Low stress drops spark rate to 35 percent; High/Ultra can spend saved CPU on molten shader richness.
Hardware Impact: Stress adaptation cuts non-critical spark pressure by roughly 65 percent above `SystemStress01 > 0.7`; microsecond cost is one signal write per lane.

Problem: Final `dotnet build` is blocked after local cutter/WFC errors were cleared.
Solution: Treat final validation as blocked by dependency after repeated build passes. Errors are outside the assigned gameplay tool surface: missing docking autopilot contracts, VFX wakes, light shaft contracts, and ecosystem interface drift.
Rejected Alternatives: Stubbing foreign-domain contracts from the laser task or reverting WFC implementation. Both would create architectural sabotage or fail the requested feature.
Scalability potential: No effect on runtime; this is an integration dependency wall.
Hardware Impact: No runtime cost. Build remains blocked until owning agents restore the missing contracts.

### 2026-05-16 - Omega Polish
Problem: The WFC hot call surface carried unused direction/normal parameters after the shader and decal paths settled on hit point plus progress.
Solution: Remove dead parameters and keep the cutter-to-runtime call surface to consumed data only.
Rejected Alternatives: Leaving unused data for hypothetical future effects. That hides real dependencies and bloats call sites.
Scalability potential: Low/Middle/High/Ultra behavior unchanged; less argument churn in the hot path.
Hardware Impact: Tiny CPU/register-pressure reduction, estimated below 1 us, but removes ambiguity from the cutting kernel.

### 2026-05-16 - Multiplatform Inquisition
Problem: WFC runtime still cached DataVault views as private `NativeArray` fields, so the code looked like system-owned native state even though the allocation came from the vault.
Solution: Replace persistent `NativeArray` fields with `VaultBufferHandle<float>` and `VaultBufferHandle<WfcLaserCutTelemetryEntry>`. The hot path resolves generation-checked vault pointers, writes one cell float, and writes one telemetry entry into the DataVault blackbox buffer.
Rejected Alternatives: Keeping local `NativeArray` fields, allocating a tool-owned native arena, or hiding ownership in a singleton manager. Those paths violate Data Vault Sovereignty and make H-Phi worse.
Scalability potential: Low/MX350 keeps the same one-float truth and decal fake. Middle keeps sparks/audio/haptics. High adds shader clip. Ultra adds tier-gated molten crystal-band energy without increasing gameplay truth.
Hardware Impact: WFC CPU work remains one pointer resolve and one float write; expected hot-path cost stays in the 1-5 us class, not measured in Unity profiler. The 200000+ us CSG stall remains removed.

Problem: ARM64/Quest builds cannot rely on implicit padding in blackbox/tool payloads.
Solution: Pin WFC telemetry as `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`, pin laser events and haptic command payloads with `Pack=1`, and pin remaining tool-side sequential structs found by scan.
Rejected Alternatives: Assuming Mono/Windows layout equals IL2CPP ARM64 layout. That is not acceptable for Quest/Android.
Scalability potential: Low/Middle/High/Ultra share identical binary payload layout; debug dumps stay readable across devices.
Hardware Impact: Fixed layout avoids alignment-dependent faults. No runtime cost beyond unchanged stores.

Problem: Metal/mobile shader path had avoidable risk from raw normalization and half literal suffixes; high-end visual path was too flat.
Solution: Replace raw `normalize` with guarded `rsqrt`, remove half suffix literals, keep the shader fragment-only with no compute thread groups, and add `_WfcLaserCutOverkill01` crystal-band molten energy gated by `GlobalRegistry.ScalabilityTier` and system stress.
Rejected Alternatives: Compute raymarching or real geometry cuts. Compute adds platform/thread-group risk; geometry cuts reintroduce the CSG class of stall.
Scalability potential: Low/Unknown/MX350 gets 0 overkill and decal/cheap shader. Middle gets 0.2 overkill. High gets 0.7 overkill. Ultra gets 1.0 overkill unless stress exceeds 0.7.
Hardware Impact: Low tier pays only simple fragment math. High/Ultra spend saved CSG time on richer edge energy; added GPU cost is presentation-only and not CPU frame-time.

Problem: The cutter/haptics feedback path still depended on `ToolHapticsRuntime` owning two native command buffers.
Solution: Add `ToolHapticFrontCommands` and `ToolHapticBackCommands` DataVault buffer IDs and resolve haptic command buffers through `VaultBufferHandle<HapticCommand>`, preserving the existing read-only snapshot consumed by `InputDispatcher`.
Rejected Alternatives: Rewriting the input dispatcher contract or keeping local native buffers. Rewriting input is outside the laser task; keeping buffers violates the inquisition requirement.
Scalability potential: Low and Steam Deck avoid extra I/O or heap churn; High/Ultra can receive richer haptic envelopes without changing buffer ownership.
Hardware Impact: Removes scene-owned native allocations from the haptic command lane. Runtime cost is a vault-handle resolve and bounded 16-command buffer scan.

Problem: Final validation was previously dependency-blocked.
Solution: Re-run `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.
Rejected Alternatives: Reporting old blocked state or stubbing other agents' contracts.
Scalability potential: No direct runtime effect.
Hardware Impact: Build evidence only. Result: 0 errors, 0 warnings. Latest rerun: 2.26 s.

### 2026-05-16 - Data Sovereignty Recheck
Problem: `ToolDurabilitySystem`, a cutter-adjacent tool runtime, still owned five persistent native containers and a native breakdown queue. That violated the current H-Phi/DataVault inquisition even though the laser/WFC runtime itself had already been evicted.
Solution: Add DataVault buffer IDs `ToolDurabilityItemStates`, `ToolDurabilityPendingDecay`, `ToolDurabilityWearMultipliers`, `ToolDurabilitySlotActive`, and `ToolDurabilityBreakdownFlags`. Replace persistent `NativeArray`/`NativeQueue` fields with `VaultBufferHandle<T>` fields, resolve bounded stack aliases only at use sites, and make the Burst decay job write byte breakdown flags instead of enqueueing native events.
Rejected Alternatives: Keeping scene-owned persistent arrays under `NativeMemorySentinel`, retaining a private `NativeQueue<BreakdownEvent>`, or moving breakdowns to managed delegates. Those options keep state ownership in the system and add contract ambiguity for Quest/Android memory layout.
Scalability potential: Low/MX350 keeps one 32-slot SOA job and one byte flag scan. Middle/High/Ultra can scale authored wear rules without changing ownership or adding allocation churn. No hot-path disk I/O was added.
Hardware Impact: Removed five scene-owned persistent native allocations plus queue prewarm from the tool durability surface. Microsecond gain is unmeasured; expected frame delta is neutral-to-positive, with the concrete benefit being no local native lifetime to leak and no private queue pressure.

Problem: Rebuild after the durability eviction initially reported four missing sanitizer methods in `GlobalSignals.cs`; a file re-read showed the methods present and a second no-shared-compiler build succeeded.
Solution: Treat the first build as a transient concurrent-edit/incremental observation and verify with a fresh `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.
Rejected Alternatives: Editing the signal guard block without a live compiler error, or marking the tool work blocked after the methods were objectively present.
Scalability potential: No runtime effect.
Hardware Impact: Build evidence only. Latest result: 0 errors, 0 warnings in 2.82 s.

### 2026-05-16 - Signal Lane Purge
Problem: `LaserCutterEvents` still owned two private `NativeQueue<LaserCutterEventPayload>` lanes and a next-frame queue. That violated the typed-lane mandate and left cutter heat/beam events outside the project-wide `SignalBus` telemetry surface.
Solution: Make `LaserCutterEventPayload` a packed `ISignal` (`Pack=1, Size=16`) and configure `SignalBus<LaserCutterEventPayload>` with a fixed 16-payload lane hash. `LaserCutterEvents.FlushPending` now reads a `ReadOnlySpan<LaserCutterEventPayload>` snapshot and only keeps the existing listener bridge for audio/world compatibility.
Rejected Alternatives: Keeping the native queues under `NativeMemorySentinel`, replacing them with managed arrays, or forcing audio/world consumers to migrate in the laser task. The first keeps private state; the second is not a typed lane; the third crosses domain ownership and risks breaking consumers outside GAMEPLAY/TOOLS.
Scalability potential: Low/MX350 gets a 16-payload hard cap. Middle/High/Ultra can receive the same cutter event truth through the global typed lane without adding local queues.
Hardware Impact: Removed two cutter-owned persistent native queues and their prewarm work. Runtime delivery now depends on the global SignalBus flush; microsecond gain is unmeasured, but local native queue lifetime and reentrant queue pressure are gone.

Problem: Signal-lane purge needed compile proof and static evidence.
Solution: Re-run `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`, then scan the TOOL_RESAK surface for `NativeQueue`, `new NativeArray`, standard Unity `Update`, `string.Format`, `Mesh.vertices`, CSG, and shader portability hazards.
Rejected Alternatives: Reporting the refactor without a build, or ignoring the remaining `LaserCutterEvents` queue because the WFC runtime was already clean.
Scalability potential: No additional runtime behavior beyond typed-lane delivery.
Hardware Impact: Build evidence only. Latest result: 0 errors, 0 warnings in 58.69 s.

### 2026-05-16 - Signal Payload Guard Pass
Problem: The cutter typed lane used the global `SignalBus`, but `LaserCutterEvents.EnsureInitialized()` had drifted into `GlobalSignals.InitializeAllQueues()`, which cold-allocates every configured signal lane just to register one cutter event bridge. The cutter payload guard also sanitized heat only, leaving invalid event type and stale flags able to propagate.
Solution: Configure only `SignalBus<LaserCutterEventPayload>` in `LaserCutterEvents.EnsureInitialized()`. Add `LaserCutterEventPayload.StateFlagBeamActive` to the canonical signal contract and extend `SanitizeLaserCutterEventPayload` to clamp heat, reset invalid event types to `HeatChanged`, and mask flags to the legal bit set for the selected event type.
Rejected Alternatives: Keeping the whole-registry initialization in the cutter bridge, adding a local duplicate payload struct, or relying on consumers to reject invalid flags. Whole-registry init creates cold-start noise; duplicate structs create interface chaos; consumer-side rejection spreads the NaN/flag policy across audio/world.
Scalability potential: Low/MX350 pays only one 16-payload lane initialization when the cutter bridge is touched. Middle/High/Ultra keep the same payload truth with no extra event path. Invalid event data is corrected before listeners see it.
Hardware Impact: Avoids cold-initializing unrelated signal lanes from the cutter path. Exact microseconds are unmeasured; expected low-end benefit is lower cold-start/native queue churn, not per-frame math savings.

Problem: Latest validation build no longer reaches a clean global compile because another domain currently breaks `DiegeticGyroCompassRuntime`.
Solution: Stop after confirming the errors are outside TOOL_RESAK and do not reference `LaserCutter`, `GlobalSignals` cutter payload edits, WFC, haptics, or durability. Preserve the scoped tool changes and record the dependency wall for integration.
Rejected Alternatives: Editing UI/navigation from the laser cutter task or reverting the typed payload guard. UI/navigation is outside GAMEPLAY/TOOLS and the errors are not caused by this pass.
Scalability potential: No runtime effect in TOOL_RESAK.
Hardware Impact: Build evidence only. Result: global build blocked by `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs` missing `_dialMatrixBuffer`/`_dialMatrices` and a `ComputeBuffer` to `GraphicsBuffer` mismatch. Static TOOL_RESAK scans stayed clean.

### 2026-05-16 - Tool Signal Delegate Purge
Problem: Tool durability and player-tool break/use propagation still used managed C# events: `ToolDurabilitySystem.OnToolBroken`, `PlayerTool.OnToolUsed`, `PlayerTool.OnToolBroken`, and `PlayerToolManager` slot events. This violated the typed-lane requirement and kept a hot gameplay state-change path outside `SignalBus<T>`.
Solution: Route durability break handling through the existing `ItemDurabilityChangedSignal` lane and `ReadOnlySpan<ItemDurabilityChangedSignal>` consumption in `PlayerToolManager`. Remove the `PlayerTool` event bridge; expose only cached last-use scalar state for `PlayerNoiseEmitter` so tool-use noise stays direct, bounded, and non-subscribing. Remove `PlayerToolManager` slot delegates and make `PlayerTransportCoordinator` consume `ToolLoadoutChangedSignal` by source id/sequence through the typed lane.
Rejected Alternatives: Creating a duplicate "ToolBroken" signal, keeping managed events as "compatibility", or polling `ToolDurabilitySystem.IsBroken()` every frame. Duplicate signal names create interface chaos; managed events preserve the exact debt being purged; durability polling adds frame work and misses the existing canonical item-durability lane.
Scalability potential: Low/MX350 consumes bounded frame snapshots only when the manager/coordinator ticks. Middle/High/Ultra keep the same gameplay truth and can add presentation consumers on existing lanes without adding gameplay delegate fanout.
Hardware Impact: Removes managed event subscription/unsubscription and delegate invocation from the tool break/use/slot path. Exact microseconds are unmeasured; expected low-end gain is reduced GC/delegate risk and less callback fanout. Latest filtered build found no TOOL_RESAK file errors; full global build remains outside-domain blocked.

### 2026-05-16 - Haptic ReadOnlySpan Bridge
Problem: `ToolHapticsRuntime` still exposed a `NativeArray<HapticCommand>.ReadOnly` front-buffer snapshot to `InputDispatcher`, so a consumer-facing tool API kept leaking the native container shape instead of the mandated span surface.
Solution: Convert `GetFrontBuffer()` and `TryGetFrontBufferSnapshot(...)` to return `ReadOnlySpan<HapticCommand>` backed by the DataVault-resolved front buffer pointer and bounded by `_frontCount`. Update `InputDispatcher` to consume the span. Add `ItemDurabilityChangedSignal.FlagBroken` to remove the magic broken bit from tool-manager filtering.
Rejected Alternatives: Keeping the `NativeArray.ReadOnly` API as "already vault-backed", copying haptic commands into a managed array, or creating a second haptic signal. The first preserves the interface leak; the second allocates; the third duplicates the haptic command lane.
Scalability potential: Low/MX350 reads at most 16 haptic commands through a span. Middle/High/Ultra can use richer command envelopes without exposing vault containers to device dispatch.
Hardware Impact: No command-copy allocation; haptic snapshot remains a pointer/length view. Exact microseconds are unmeasured. Latest filtered build found no TOOL_RESAK file errors; latest full global build is blocked outside this task by `DiegeticGyroCompassRuntime` missing blackbox/AUP fields and `EcosystemDirector` generic native pointer/upload inference errors.
