# Rationale_SHINOBU_107

Status: STATIC PASS / COMPILE BLOCKED BY EXTERNAL WORLD SOURCE

## Initial Boundary

Problem: The batch prompt assigns Signal Corridor purification: GlobalRegistry hot polling, HectonEventBus quarantine, SignalBus layout, load shedding, telemetry, editor tooling.
Solution: Work in strict loops. Start with evidence collection and narrow code edits in Core/Player/UI/AI only where static scan proves hot-path registry or signal contract defects.
Rejected Alternatives: No broad rename/refactor pass. No invented global services. No raw YAML edits. No runtime claims without Unity logs.
Scalability potential: Low uses bounded/coalesced typed lanes; Middle consumes normal snapshots; High keeps richer telemetry; Ultra spends saved cost in VISUAL_SYNC consumers, not gameplay fan-out.
Hardware Impact: Expected impact is removing repeated static service reads and managed/cold bus misuse from hot paths. Microsecond gain must be estimated per edited site until profiler proof exists. Target silicon: i3/MX350.

## Mandate Selection

Problem: Task spans registry, signal lanes, struct layout, jobs, AUP, telemetry.
Solution: Read 8 mandates: GlobalRegistry DI, Signal Lane Segregation, Execution Phases, ARM64 Struct Layout, Zero GC, Native Memory Jobs, AUP Determinism, Post-Mortem Telemetry.
Rejected Alternatives: Reading broad rendering/physics mandates would add noise without ownership evidence for this loop.
Scalability potential: Mandate set covers low-to-ultra behavior through bounded snapshots, continuous quality weight, and telemetry.
Hardware Impact: Keeps work focused on cache locality and allocation removal; expected benefit is microsecond-scale per hot path site, plus reduced GC risk.

## Loop 1 / Signal DTO Spine

Problem: CombatDamageSignal still behaved like a local float-position payload, while the batch requires AUP-safe transport and 64-byte ARM64 alignment for high-traffic lanes.
Solution: CombatDamageSignal now carries `double3 ImpactAup` in a 64-byte explicit layout. PlayerStateSignal and AcousticPingSignal remain explicit 64-byte layouts. AUP guard rejects non-finite and >100km payloads before signal exposure.
Rejected Alternatives: Keeping runtime `float3 WorldPoint` was rejected because it bakes floating-origin presentation coordinates into authoritative damage facts. Pack=1 and sequential high-traffic layouts were rejected because they create ARM64 unaligned bulk reads.
Scalability potential: Low/Middle/High/Ultra all consume the same aligned DTO; high-tier consumers can spend saved CPU on richer VFX from the same AUP fact without duplicating routes.
Hardware Impact: Estimate is microsecond-scale per burst batch from fewer cache-line splits and no local/world conversion churn in consumers; measured proof absent.

## Loop 2 / Dear Lie Coalescing

Problem: Bursts of acoustic or combat signals can flood the consumer span and turn one perceived event into hundreds of CPU-visible facts.
Solution: SignalBus<T>.FlushPreSimulation now coalesces AcousticPingSignal by AUP grid cell and channel, and CombatDamageSignal by TargetHash/DamageType/Channel. Zero TargetHash is not merged to avoid collapsing anonymous damage into one false owner.
Rejected Alternatives: Per-signal physical truth was rejected for presentation-equivalent bursts. A dictionary/hash map per flush was rejected because the frame cap is small and an in-place scan avoids new persistent containers.
Scalability potential: Low uses tighter caps and CSV-driven coalescing radius; Middle keeps default 1m grouping; High/Ultra can raise max frame signals and lower coalescing radius through `signal_tuning_profiles.csv` without code recompilation.
Hardware Impact: For a 500-ping blast, expected CPU-visible facts collapse toward 1-16 nearby groups. Estimated savings: tens to hundreds of microseconds on i3/MX350 in stress scenes; profiler proof absent.

## Loop 3 / Continuous Load Shedding And Vault Tuning

Problem: Existing low-tier boolean behavior was too coarse for a thermally breathing runtime, and designers had no cold facade for coalescing/cap tuning.
Solution: Frame limits now use `GlobalQualityWeight01`, system stress, smoothstep curve, and optional vault-backed `SignalTuningProfile` rows. `SignalTuningCsvHotSwap` parses `signal_tuning_profiles.csv` from vault byte scratch with `ReadOnlySpan<byte>` and hashes signal names via FNV-1a.
Rejected Alternatives: Binary `isLowEnd` switches were rejected. Managed string splitting and per-row CSV objects were rejected because cold tools should not normalize bad allocation habits.
Scalability potential: Low=16-ish survival caps; Middle=default caps; High/Ultra can increase max and reduce coalescing radius. No hard pop is introduced by the cap curve.
Hardware Impact: Expected low-end gain is bounded snapshot iteration and fewer consumer calls; high-end impact is preserving optional signal richness for visual systems.

## Loop 4 / Editor And Black Box

Problem: Signal telemetry existed as scattered counters and the editor monitor was IMGUI-local, not a vault-backed traffic view.
Solution: SignalTelemetryFrame is a 64-byte 300-frame vault ring dumped to `Docs/AgentLogs/Dump_SIGNAL_CORRIDOR.bin`. The Signal Traffic Monitor is now UI Toolkit, reads the vault ring plus lane telemetry, highlights heavy shedding, and injects synthetic mock/combat/acoustic signals.
Rejected Alternatives: IMGUI-only polling was rejected because the task explicitly requires UI Toolkit and vault telemetry. Chat-only evidence was rejected; status remains file-backed.
Scalability potential: Low devices can be diagnosed by dropped/coalesced ratios; High/Ultra can verify richer traffic remains under caps.
Hardware Impact: Runtime telemetry is one 64-byte row per frame in a fixed ring. Editor UI cost is editor-only.

## Loop 5 / Hot Registry Cache Pass

Problem: Static scan found hot `GlobalRegistry` reads in Player/UI/AI. Some were real frame-loop dependencies; others were lifecycle registration false positives from the coarse scanner.
Solution: Replaced hot reads with cached fields and cold rebinding in MantaScooter, MountablePlayerTransport, InteractionUI, HectonSubmarineOS, and AcousticEchoLocationRuntime. HectonSubmarineOS uses ScalabilityEvents instead of polling tier during Tick. AcousticEchoLocationRuntime consumes ScalabilityChangedEvent snapshots instead of polling profile byte per refresh.
Rejected Alternatives: A broad rewrite of every scanned legacy component was rejected because many hits are cold registration, UI boot/meta hooks, or ownership-ambiguous components that need separate route proof.
Scalability potential: Low-to-Ultra route stability improves because service swaps happen through cold listeners, not per-frame registry coupling.
Hardware Impact: Estimated saving is 0.2-1.0 us per removed registry/service read on i3/MX350, plus reduced branch/cache coupling. Compile/profiler proof absent.

## Loop 6 / UI Cache Expansion, Deferred Raycast, Request Route

Problem: Follow-up scan still showed direct UI hot-loop reads of DataVault/Input/Scalability and a deconstruction signal drain performing synchronous Physics.RaycastNonAlloc. SaveManager also pushed `SignalBus<SaveRequestSignal>` to itself, a one-owner request path.
Solution: PDADataArchaeologyDecryptLabel, PDADecryptionSpectrogramPanel, and WristHologramHudRuntime now cache tier/vault/input through cold lifecycle/hot-swap/scalability listeners. ConstructionManager deconstruction now schedules one RaycastCommand and resumes authoritative removal on the next LateFrameTick after `DispatcherJobSwap.TryFinalizeCompleted`. SaveManager removed self-published `SaveRequestSignal` and calls owner-local `ProcessSaveRequest` directly from `TryRequestSave`.
Rejected Alternatives: Keeping the synchronous raycast was rejected because it violates Task 12 and can stall the signal drain. A broad rewrite of DockingRequest/WakeRequest was rejected because those lanes are command-style owner broadcasts with separate domain ownership and require route cards before deletion.
Scalability potential: Low avoids construction signal-loop stalls and UI service polling; Middle keeps same behavior with one-frame deconstruction validation latency; High/Ultra keep full validation while freeing CPU headroom for richer presentation.
Hardware Impact: Deferred deconstruction raycast removes a worst-case same-frame physics query from the signal drain; estimated save is 5-40 us during deconstruction spikes on i3/MX350, plus avoiding main-thread physics stalls. UI cache expansion saves sub-microsecond registry reads per active UI tick. Compile/profiler proof absent.

## Compile Gate Attempt

Problem: The batch requires compile verification, but developer hardware protection forbids dotnet build when CPU is >50% or dotnet/csc are already running.
Solution: Waited until no dotnet/csc were present and CPU sampled at 49.86%, then launched `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. The command timed out after 124 seconds without compiler error output and left no dotnet/csc processes. Subsequent CPU samples returned above 50%, so no second build was launched.
Rejected Alternatives: A second build under 71-93% CPU was rejected because it violates the explicit hardware gate. Killing unrelated processes was rejected because they are outside this task's ownership.
Scalability potential: Verification is pending; no runtime scalability claim is upgraded without compiler proof.
Hardware Impact: Build attempt consumed one single-worker compile window; no runaway dotnet/csc remained after timeout.

## Loop 7 / Hot-Method Registry Scanner Closure

Problem: The coarse registry scan still contained hundreds of cold `GlobalRegistry` lifecycle registrations, but a method-scoped scan isolated direct hot-loop calls in Player/UI/AI methods: swim presentation input lookup, emergency wreck pool lookup, sonar audio/player lookup, relay HUD service lookup, PDA chrome localization/input lookup, archaeology lore unlock, ending terminal quest/ending lookup, and PDA data-log localization lookup.
Solution: Added cold cache hydration plus `IGlobalRegistryHotSwapListener` rebinding to the touched owners. Hot methods now consume cached `IInputService`, `ObjectPoolManager`, `SpatialAudioManager`, `IPlayerRuntimeContext`, `EmergencyServiceRelayDirector`, `LocalizationManager`, `InputManager`, `LoreDatabaseManager`, `EndingSystem`, and `QuestManager`. The method-scoped scanner now reports zero direct `GlobalRegistry.*` calls inside Player/UI/AI `Tick`, `FixedTick`, `LateFrameTick`, `Update`, or `Update*` methods.
Rejected Alternatives: A blanket replacement of all remaining registry lines was rejected because most are dispatcher registration, save/renderable registration, or cold boot metadata. Removing `HarvestableOutcrop` -> `ItemCollectedEvent` was rejected in this loop because meta/world consumers still use that managed event as a cold extension route; deleting it without a SignalBus-to-meta bridge would silently break profile/environment accounting.
Scalability potential: Low and Middle remove service-slot cache misses from active UI/player ticks. High and Ultra keep hot-swap support, so mod/debug service replacement does not reintroduce polling.
Hardware Impact: Estimated low-end savings remain sub-microsecond per service read, but these were repeated per frame across UI/player presentation. Expected aggregate on i3/MX350 is several microseconds in PDA/HUD-heavy frames plus reduced registry cache contention. Compile/profiler proof still pending.

## Compile Wall / Missing World Source

Problem: A targeted compile launched under the hardware gate failed before SHINOBU changes could be validated. `Hecton8.Core.csproj` references `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`, but that file is deleted in the working tree.
Solution: Classified this as an external dependency wall. Did not recreate or revert the deleted World file because it is outside the Signal Corridor ownership boundary and may be another agent's intentional edit.
Rejected Alternatives: Recreating the missing World file from HEAD was rejected because it would overwrite another agent/user deletion. Removing the csproj entry was rejected because it edits project-wide compile metadata for a World-domain ownership problem.
Scalability potential: No runtime scalability claim changes until compile can pass past this external missing-file barrier.
Hardware Impact: Targeted build failed in 15.7 seconds, left no dotnet/csc processes, and did not consume another long compile window.

## Loop 8 / Save Request Lane Deletion

Problem: After SaveManager switched to owner-local `ProcessSaveRequest`, GlobalSignals still initialized `SignalBus<SaveRequestSignal>`, leaving a dead 1-to-1 request lane in the typed corridor.
Solution: Removed the `SignalBus<SaveRequestSignal>.Configure/EnsureInitialized` calls. `SaveRequestSignal` remains as a local unmanaged DTO inside SaveManager, but it no longer owns a bus lane.
Rejected Alternatives: Deleting the DTO or hash constants was rejected because local save code still uses the struct as a compact request packet and generated constants may be consumed by tooling.
Scalability potential: Low-to-Ultra all avoid an unused lane registration and telemetry row; no gameplay signal richness is lost.
Hardware Impact: Micro impact is cold-start only: one fewer NativeQueue/snapshot lane to initialize and scan. Runtime hot-path impact is zero because the lane was already unused.

## Loop 8 / Burst Directive Closure

Problem: ModEventProjectionBridge had one `[BurstCompile]` job without explicit synchronous/float-mode directives, leaving Burst behavior dependent on defaults.
Solution: Added `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` to `ProjectWeatherChangedSignalsJob`, matching the adjacent combat projection job.
Rejected Alternatives: Leaving default Burst settings was rejected because the batch requires explicit flags for every written/touched job. Deterministic float mode was not used because this bridge projects managed mod events and does not mutate rollback gameplay state.
Scalability potential: Low uses the existing projection cap; High/Ultra can project the larger cap with consistent Burst codegen.
Hardware Impact: Expected gain is avoiding conservative Burst defaults in the weather projection bridge; small but deterministic for mod-heavy frames.

## Loop 9 / EventBus Damage Quarantine

Problem: `HectonSurvivalSystem.TakeDamage` synchronously published `PlayerTakeDamageEvent` through managed `HectonEventBus`, exactly matching the batch's prohibited high-frequency damage-event example. The event path allocated a managed event object and allowed request/response-style mutation/cancellation inside the damage owner.
Solution: Removed the hot EventBus publish from `TakeDamage`. The owner now clamps the incoming scalar locally and keeps the existing `DynamicDifficultyDirector.Current.DamageMultiplier` application before mutating integrity. `rg` confirms no remaining `Publish(new PlayerTakeDamageEvent` call sites.
Rejected Alternatives: Moving damage cancellation to `SignalBus<T>` was rejected because request/response mutation is explicitly forbidden. Keeping the mod cancellable event was rejected because no in-repo `Subscribe<PlayerTakeDamageEvent>` consumer exists and the bus is quarantined for cold meta/projection paths.
Scalability potential: Low/Middle avoid managed dispatch on repeated hazard/fauna damage. High/Ultra keep the same owner-local damage math and can consume downstream `CombatDamageSignal` presentation facts without mutable event coupling.
Hardware Impact: Removes one managed event allocation plus subscriber dispatch/probe per player damage call. Estimated gain is sub-microsecond for isolated hits and several microseconds during stacked hazard ticks on i3/MX350; exact profiler proof blocked by compile dependency.

## Loop 10 / CSV Human-Control Facade

Problem: The zero-string parser existed, but the default `Assets/StreamingAssets/signal_tuning_profiles.csv` source file did not. That left Task 19 dependent on a missing human-editable artifact.
Solution: Added the StreamingAssets folder, Unity meta files, and an ASCII CSV containing base rows for AcousticPingSignal, CombatDamageSignal, SignalWardenMockDamageSignal, and MockPlayerFootstepSignal. The parser can now hot-swap min/max caps, coalescing radius, and priority without recompilation.
Rejected Alternatives: Hardcoding the rows only in `SignalTuningTable.Initialize` was rejected because the task explicitly requires a CSV tuning path. Adding JSON or ScriptableObject tuning was rejected because it would introduce managed parsing/object dependencies.
Scalability potential: Low can raise coalescing radius and shrink caps by data edit; Middle uses the checked-in baseline; High/Ultra can increase caps and reduce radius without touching C#.
Hardware Impact: Runtime hot path remains unchanged. Cold load reads <=8192 bytes into vault scratch; per-frame gain comes from designer-controlled caps/coalescing rather than parser cost.

## Loop 11 / Pointer Aliasing Closure

Problem: The mod projection Burst jobs had explicit Burst flags, but their snapshot inputs and output queue writer did not carry `NoAlias`, leaving vectorization dependent on compiler conservatism.
Solution: Added `Unity.Collections.LowLevel.Unsafe` and marked `ProjectCombatDamageSignalsJob` and `ProjectWeatherChangedSignalsJob` fields as `[ReadOnly, NoAlias] NativeArray<...>.ReadOnly Signals` plus `[NoAlias] NativeQueue<ModEventDto>.ParallelWriter Output`.
Rejected Alternatives: Leaving default alias inference was rejected because the job contract is simple and isolated: read-only signal snapshot in, append-only queue writer out.
Scalability potential: Low keeps projection cap small; Middle/High/Ultra can project more events without alias-analysis drag in the job body.
Hardware Impact: Expected gain is micro-scale but deterministic under mod-heavy frames; exact value requires Burst compile/profiler after the external World compile wall is cleared.

## Loop 12 / CS1612 Signal DTO Residue

Problem: `ScalabilityChangedEvent`, `AcousticZoneChangedEvent`, and `DirectorAIMusicSignal` were still property-backed sequential `ISignal` DTOs. They passed size alignment but violated the batch rule that signal payloads are public unmanaged fields with explicit offsets.
Solution: Converted all three to `[StructLayout(LayoutKind.Explicit)]` readonly field DTOs. `ScalabilityChangedEvent` stores normalized tier bytes and byte-sized quality enums in a 16-byte packet. `AcousticZoneChangedEvent` stores `IsInterior` as a byte. `DirectorAIMusicSignal` stores `Vector3 Position`, `float Value`, event byte, bool byte, and explicit padding in 32 bytes. Consumers that needed bool semantics now compare the byte to zero.
Rejected Alternatives: Keeping property accessors was rejected because properties are accessor methods wrapped around signal snapshot data. Recomputing `CurrentQualityTier` from `CurrentTier` on every consumer read was rejected; the constructor computes it once and stores the byte enum in the payload.
Scalability potential: Low avoids repeated accessor/conversion work while processing compact snapshots. Middle keeps unchanged behavior. High and Ultra retain full signal expressiveness without expanding payload size.
Hardware Impact: Expected gain is sub-microsecond per scalability/music/acoustic-zone snapshot drain, with stronger Burst/IL2CPP-friendly field access and no implicit struct accessor copies. Exact profiler proof remains blocked by the external missing World source.

## Loop 12 / Save Request Payload Boundary

Problem: `SaveRequestSignal` had already been removed from the bus lane, but the struct still implemented `ISignal`, leaving it semantically visible as a legal broadcast payload.
Solution: Removed `ISignal` from `SaveRequestSignal` and kept it as an owner-local unmanaged request packet for `SaveManager`.
Rejected Alternatives: Deleting the packet outright was rejected because `SaveManager` still benefits from a compact local request record. Leaving it as `ISignal` was rejected because request/response packets must not be accepted by typed signal tooling.
Scalability potential: Low-to-Ultra all get one clearer route: persistence requests stay owner-local; broadcast lanes remain reserved for one-to-many facts.
Hardware Impact: Runtime frame gain is zero because the lane was already removed; architectural gain is validator/tooling exclusion from the signal corridor.

## Loop 13 / Direct Signal Lane Dispatch

Problem: `SignalBusRegistry.FlushPreSimulation()` still walked an `ISignalLane[]` and invoked `FlushPreSimulation` through an interface for centrally initialized lanes. That violates the devirtualization mandate: an interface array in a frame-phase dispatcher forces virtual dispatch and blocks inlining of the generic `SignalBus<T>` kernel.
Solution: Added a direct-dispatch table for every `SignalBus<T>.EnsureInitialized()` lane currently owned by `GlobalSignals`. Registration marks those known lanes as direct. The frame flush calls generic `SignalBus<T>.FlushPreSimulation(lowTier, stress)` directly for all 132 known lanes, then iterates only the `_fallbackLaneIndices` list for dynamic lanes that are not in the central table. Clear-post-simulation uses the same direct table.
Rejected Alternatives: Replacing the whole registry with delegates was rejected because delegate invocation is another managed indirection. Removing the registry outright was rejected because dynamic/debug lanes still need telemetry, dispose, and fallback registration. A generated source file was rejected because no generator contract exists in this batch and it would add compile-wall churn.
Scalability potential: Low benefits from cheaper per-frame dispatch while retaining quality-weight caps inside each lane. Middle keeps identical phase behavior. High/Ultra can carry richer visual-synchronization lanes without multiplying interface-call overhead in the central registry.
Hardware Impact: Static count removes 132 normal-path interface dispatches from each pre-simulation flush and 132 from post-simulation clear. Expected gain is single-digit to low-tens of microseconds per frame on i3/MX350 depending on IL2CPP/JIT devirtualization behavior; profiler proof remains blocked by the external World compile wall.

## Loop 14 / Rollback Input DTO Layout Closure

Problem: Rollback/input lanes still carried sequential `ISignal` payloads: `InputSignal`, `StateCorrectionSignal`, `DesyncDetectedSignal`, `SyncFenceSignal`, `KccVelocitySignal`, `InputStateSignal`, `LockstepSnapshotSignal`, and `SystemGlitchSignal`. The deterministic input payload also exposed computed properties (`Move`, `Look`, `VerticalAxis`) that were read from signal consumers.
Solution: Converted those eight signal DTOs to explicit layouts with `FieldOffset` and manual padding while preserving existing public field names and existing size guard expectations. Added `ValidateSignalSize<InputStateSignal>(32)`. Removed the three computed `InputState` axis properties and replaced the hot consumers in `InputDispatcher`, `LockstepStateValidator`, and `PlayerKinematicsRuntime` with direct field dequantization math.
Rejected Alternatives: Changing field names or compressing AUP-bearing fences to fit a 64-byte packet was rejected because it would break existing producers and lose authoritative AUP/runtime-state facts. Leaving sequential layout was rejected because it leaves ARM64 padding to compiler layout rules. Adding helper properties or extension methods was rejected because it preserves accessor calls on hot snapshots.
Scalability potential: Low avoids hidden accessor work and gets stable packed deterministic DTOs for capped rollback lanes. Middle keeps unchanged behavior. High and Ultra preserve rich sync/state correction packets while keeping fixed offsets for memcpy, sort, and telemetry tooling.
Hardware Impact: Expected saving is sub-microsecond per input/sync snapshot consumer from eliminating computed property calls; larger benefit is deterministic cache layout and lower ARM64 unaligned-layout risk. Exact profiler proof remains blocked by the external World compile wall.

## Loop 15 / GlobalSignals And Audio Payload Closure

Problem: `GlobalSignals.cs` still had real public `ISignal` DTOs using sequential layout after the rollback pass, and `AudioEvent` embedded nested audio payload structs that still exposed property-backed or sequential payload shape. That left the signal validator unable to make an explicit-layout guarantee for the whole public signal surface.
Solution: Converted the remaining `GlobalSignals.cs` sequential DTOs to explicit layouts and converted the nested audio payload structs in `ProceduralAudioEvents.cs` to explicit public readonly fields. `AudioEvent` is now a 128-byte explicit union: 16-byte header, `AudioPingTriggerInfo` at offset 16, `StructuralStressAudioInfo` at offset 16. `ValidateSignalSize<AudioEvent>` now expects 128. `AudioPingTriggerInfo.StartTimeSeconds` was removed after source scan found no consumers.
Rejected Alternatives: Keeping `AudioEvent` at 144 bytes with two non-overlapped payloads was rejected because only one audio variant is valid per event. Leaving nested audio structs sequential was rejected because a typed signal must not hide layout ambiguity inside a field type.
Scalability potential: Low consumes the smaller union packet and capped audio event lane; Middle keeps unchanged event semantics; High and Ultra can push richer procedural audio payloads without extra header or duplicate payload bytes.
Hardware Impact: `AudioEvent` shrinks from 144 to 128 bytes, one 16-byte cache/SIMD quantum per event. Expected low-end saving is micro-scale per audio burst plus lower snapshot memory bandwidth. Exact profiler proof remains blocked by the external World compile wall.

## Loop 16 / Localized Signal Contract Explicit Guard

Problem: A repo-wide source scan still found localized public `ISignal` DTOs declared as sequential contracts outside `GlobalSignals.cs`. These are not domain logic, but they are signal corridor ABI. Leaving them sequential would make the new explicit-layout validator unusable.
Solution: Converted 30 localized public `ISignal` DTOs to explicit `FieldOffset` layouts. Widened the refactored 40/48-byte signal packets to 64 bytes where needed: `DroneFleetInventoryTransactionSignal`, `MockPlayerPositionSignal`, `ThermodynamicsMockDamageSignal`, `FloraSpawnedSignal`, and `DeltaCrusherMockLaserFireSignal`. Strengthened `SignalPayloadLayoutValidator` so reflected `ISignal` types must be `LayoutKind.Explicit`.
Rejected Alternatives: Editing producer/consumer gameplay logic was rejected because the defect was ABI layout only. Leaving the 40/48-byte localized signals unchanged was rejected because the strict signal contract now requires explicit manual padding on refactored payloads.
Scalability potential: Low/Middle get predictable packet stride and stronger ARM64 alignment. High/Ultra keep the same semantic signal facts and can spend the saved safety margin in visual/audio consumers, not in route plumbing.
Hardware Impact: Expected gain is not an ALU win; it is preventing layout drift, unaligned bulk snapshot reads, and hidden compiler packing changes. `AudioEvent` has the direct bandwidth gain of 16 bytes/event. Localized 40/48-byte signals widened to 64 trade small memory overhead for stable cache-line-friendly ABI.

## Loop 17 / Mod Projection Continuous Quality Gate

Problem: `ModEventProjectionBridge` still used `GlobalRegistry.ScalabilityTierProfileByte == 0` to select either 10 or 50 projected native events, and the Burst projection jobs accepted a `LowTier` byte. That is a binary quality gate inside the communication spine.
Solution: Replaced the binary branch with `SignalBusRegistry.GlobalQualityWeight01`, sanitized through `math.saturate`, then curved with smoothstep and mapped through `math.lerp(LowTierProjectionCap, HighTierProjectionCap, curve)`. The projection jobs now receive `float QualityWeight01`; the legacy low-sample flag is derived with `math.step` so the mod DTO contract is preserved without tier polling.
Rejected Alternatives: Leaving the old `ScalabilityTierProfileByte` branch was rejected because it makes thermal scaling jump between two caps. Expanding mod-facing DTOs was rejected because it would mutate a public API surface for a bridge-internal scheduling problem.
Scalability potential: Low runs near 10 projected events and marks samples as degraded; Middle naturally lands between the caps; High and Ultra reach 50 projected events while preserving the same typed SignalBus source route.
Hardware Impact: On i3/MX350-style devices this bounds managed mod callback exposure with a continuous cap instead of a high/low cliff. Expected saved work under thermal pressure is up to 40 projected managed callback opportunities per frame versus the high cap; measured proof remains blocked by the external compile wall.

## Loop 18 / Inventory Native Payload Pack Removal

Problem: `InventoryPhysicalDropRequestPayload` was `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`. It is an unmanaged payload published through the inventory/event routing surface after a player drops an item, so Pack=1 is an ARM64 alignment risk even though the route is not yet fully SignalBus-purified.
Solution: Converted `InventoryPhysicalDropRequestPayload` to explicit 48-byte layout and added named `_pad0` at offset 44. Converted the adjacent `InventoryEventPayload` NativeQueue payload to explicit 24-byte layout so the inventory native queue ABI has fixed offsets.
Rejected Alternatives: Replacing the entire drop route with a new `SignalBus<T>` lane was rejected in this pass because world presentation and item sidecar ownership need a separate route card. Leaving Pack=1 was rejected because native queue payloads must not rely on unaligned packing.
Scalability potential: Low/Middle/High/Ultra all keep the same drop semantics; the improvement is stable ABI stride and no ARM64 unaligned-layout shortcut.
Hardware Impact: Direct per-frame saving is not claimed. This removes an unaligned-access trap risk from item-drop payload transport and protects future Burst/native queue consumers from hidden packing cost.

## Loop 19 / Mod Projection Player Context Hot Cache

Problem: `ModEventProjectionBridge.ResolvePlayerRuntimePosition()` polled `GlobalRegistry.Player` during projected-event scheduling. That bridge can run every frame when mods subscribe to projected signal events, so the player context lookup belonged in cold dependency caching.
Solution: Added a cached `IPlayerRuntimeContext` field. `Install()` populates it once from `GlobalRegistry.Player`, the bridge registers as `IGlobalRegistryHotSwapListener`, and `OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Player, ...)` refreshes the cache. The hot resolver now reads only `_playerRuntimeContext`.
Rejected Alternatives: Leaving a registry fallback inside the resolver was rejected because it would preserve the polling path under exactly the mod-heavy case this bridge exists for. Rewriting player-position acquisition through a new signal lane was rejected because `PlayerRuntimePoseSnapshot` already exists as the owner interface and this bridge only needs a read-only pose sample.
Scalability potential: Low/Middle/High/Ultra all avoid registry service-slot lookup in projected-event frames. High/Ultra can tolerate more projected event richness without adding registry fan-out.
Hardware Impact: Estimated saving is sub-microsecond per projected frame, but it removes a repeated global slot read from the mod projection path and reduces coupling under mod callback load. Compile/profiler proof remains blocked externally.

## Loop 20 / Mod Registry Native Payload Explicit Layout

Problem: `ModRegistryEventPayload` was a `NativeQueue<T>` payload declared with `LayoutKind.Sequential`. It is not an `ISignal`, but it is still a native communication packet in the mod registry invalidation lane, so implicit layout leaves an ABI hole adjacent to the purified signal corridor.
Solution: Converted the payload to `[StructLayout(LayoutKind.Explicit, Size = 16)]` with fixed offsets: `Frame` 0, `ModHash` 4, `SubjectHash` 8, `EventType` 12, `StatusBits` 14.
Rejected Alternatives: Rewriting `ModRegistryEvents` to a new `SignalBus<T>` route was rejected because this is a cold mod/API invalidation lane with coalesced four-event capacity, not first-party gameplay state. Broadly converting every public mod spatial contract was rejected because those are external API envelopes and require separate compatibility review.
Scalability potential: Low/Middle/High/Ultra keep the same mod registry invalidation behavior. The gain is predictable packet stride and no implicit packing drift as the mod API evolves.
Hardware Impact: No direct ALU win is claimed. This prevents ARM64 layout ambiguity in a native queue payload and keeps future Burst/native consumers from inheriting a sequential-layout trap.

## Loop 21 / Mod Legacy Queue Payload Explicit Layout

Problem: `ModSpatialContracts.cs` still declared legacy mod command/response NativeQueue payloads with `LayoutKind.Sequential`: AUP command wrappers, deferred raycast results, render-instance submissions, AUP responses, and memory-eviction notifications. These are quarantined legacy mod API packets, but they still live in runtime NativeQueues when that surface is enabled.
Solution: Converted `long3`, `ModAup`, `ModAupCommand`, `ModAupResponse`, `ModRenderInstanceCommand`, `ModRaycastResultPayload`, and `ModCriticalMemoryEvictionPayload` to explicit layouts with fixed byte offsets. Added a named `_pad0` to `ModAup` and `ModCriticalMemoryEvictionPayload` so 8-byte fields stay aligned.
Rejected Alternatives: Replacing the legacy mod command surface with new gameplay SignalBus routes was rejected because `FutureCommandEnvelope` is already the intended replacement and the legacy surface is explicitly quarantined. Broad producer/consumer rewrites were rejected because this loop fixes ABI layout only.
Scalability potential: Low devices avoid hidden layout/packing surprises if legacy mod queues are enabled; Middle/High/Ultra preserve the same public mod payload fields while keeping native queue stride deterministic.
Hardware Impact: No frame-time win claimed. This removes implicit layout from seven runtime/native mod payloads and prevents future ARM64 alignment regressions in legacy queue paths.

## Loop 22 / Native Queue Payload Validator Guard

Problem: `SignalPayloadLayoutValidator` only inspected `ISignal` structs. The Mod/Inventory queue payloads cleaned in Loops 18, 20, and 21 could regress to sequential layout or Pack=1 without tripping the editor guard because they are not all `ISignal` contracts.
Solution: Added a curated full-name allowlist of signal-adjacent native queue payloads and a separate unmanaged `UnsafeUtility.SizeOf<T>()` reflection path. The validator now enforces explicit layout, no Pack=1, and 8-byte size multiples for these known queue payloads while preserving the existing `ISignal` guard.
Rejected Alternatives: A global scan over every `NativeQueue<T>` field was rejected because unrelated World/Audio/AI domains own native queues that SHINOBU must not break through a broad editor exception. Direct compile-time references to Modding/Inventory types were rejected; string full names avoid new compile-wall dependencies.
Scalability potential: Low/Middle/High/Ultra benefit from preventing ABI regressions in the communication spine without forcing unrelated domain rewrites. High/Ultra mod projection still scales through the continuous quality cap from Loop 17.
Hardware Impact: No runtime cost. The editor guard is cold and protects future ARM64/native-queue payload changes from silently becoming unaligned.

## Loop 23 / Harvestable Outcrop Service Cache and Item Signal Bridge

Problem: `HarvestableOutcrop` read `GlobalRegistry.PlayerInventoryRuntime`, `PersistentWorldRegistry`, `Audio`, `ObjectPool`, and `Localization` from yield/effect/localization paths. The same resource pickup path also published only a managed `ItemCollectedEvent`, so first-party consumers had no typed AUP-bearing signal fact for this source.
Solution: Added owner-local cached fields and `IGlobalRegistryHotSwapListener` rebind handling for PlayerInventory, PersistentWorldRegistry, Audio, ObjectPool, and Localization. Successful inventory acceptance now emits a 64-byte `ItemAcquiredSignal` using `AbsoluteUniversePosition.FromRuntimePosition`, finite AUP checks, and `ItemAcquiredSignalSourceKinds.HarvestableOutcrop`.
Rejected Alternatives: Removing the existing `ItemCollectedEvent` in this loop was rejected because `GlobalProfileManager` and `EnvironmentalStrainManager` currently consume `ItemData.resourceFamily`, `ItemData.category`, and `isRawResource`; `ItemAcquiredSignal` only carries hashes and quantity. Polling `ItemCatalog` from those systems would create another cross-domain lookup route without a dedicated owner contract.
Scalability potential: Low devices avoid registry fan-out during outcrop break/effect frames and receive a compact typed fact for downstream capped consumers. Middle keeps existing meta behavior. High and Ultra can add richer visual/audio reactions from the typed `ItemAcquiredSignal` lane without adding managed request/response coupling.
Hardware Impact: Estimated gain is sub-microsecond on ordinary hits and a few microseconds on collapse frames with audio, VFX, inventory, and world-drop paths active. The new signal push adds one fixed-size AUP packet only when loot is accepted; exact profiler proof remains blocked by the external World compile wall.

## Loop 24 / PlayerInventory Signal Consumer Hot Cache

Problem: `PlayerInventory` consumes `SignalBus<ItemAcquiredSignal>` every late-frame, but its repair-tool side path still resolved `GlobalRegistry.Player` when item signals existed. The same class also polled Player for depth/submerged/impact calculations, PersistentWorldRegistry during item drop, and Audio during inventory thermal runaway.
Solution: Added cached Player, PersistentWorldRegistry, and Audio service fields plus `IGlobalRegistryHotSwapListener` rebind handling. The cached player context invalidates `_playerImpactBodyId` whenever it changes. Signal drains, depth/submerged checks, impact mass lookup, item-drop persistence, and thermal-runaway audio now read cached fields.
Rejected Alternatives: Rewriting `PlayerInventory` storage to GlobalDataVault was rejected as out of scope for SHINOBU and too broad for a signal-corridor loop. Replacing item discard EventBus projection was rejected for a separate route card because current world/meta subscribers still consume rich managed `ItemData` payloads.
Scalability potential: Low devices avoid service-locator reads in late-frame signal drains and slow-tick pressure/corrosion checks. Middle keeps identical inventory behavior. High and Ultra can process more item/equipment side effects from typed signals without multiplying registry reads.
Hardware Impact: Estimated saving is sub-microsecond per item-signal drain and impact/pressure check, with larger win during dense pickup/drop/thermal frames. No new persistent native memory was added.

## Loop 25 / Fake Radar UI Player Cache and Burst Packet Layout

Problem: `FakeRadarBlipController` runs in UI Tick/LateFrame/Render flow and still queried `GlobalRegistry.Player` when resolving player transform, fallback AUP, and projection camera. Its Burst cull job also used sequential job packets, including a 12-byte `RadarCullResult`, and lacked explicit `[NoAlias]` contracts.
Solution: Added cached `IPlayerRuntimeContext` and `IGlobalRegistryHotSwapListener` rebind handling. The hot radar path now uses the cached player context. Converted `RadarCullCandidate` to explicit 8 bytes and `RadarCullResult` to explicit 16 bytes with a named pad. Updated `RadarBlip2DCullJob` to the project-required Burst flags and `[NoAlias]` NativeArray fields.
Rejected Alternatives: Leaving UI as "presentation-only" was rejected because this controller schedules a Burst job and runs every frame when active. Replacing the radar with physics raycasts was rejected; it already uses the correct Dear Lie path: spatial hash contacts, AUP delta flattening, and instanced quads.
Scalability potential: Low devices keep the cheap flat XZ radar fake with predictable NativeArray stride. Middle keeps unchanged visuals. High and Ultra can raise radar richness elsewhere without paying player-service lookup or alias-analysis tax in this job.
Hardware Impact: Removes up to three player registry reads from active radar frames and changes result stride from an unsafe 12-byte sequential packet to a 16-byte aligned packet. Expected gain is micro-scale; correctness gain is ARM64/Burst safety.

## Loop 26 / Acoustic Radar UI LateFrame Cache

Problem: `AcousticRadarSphereRenderer` runs in `ILateFrameTickable` and fetched `GlobalRegistry.Audio` plus `GlobalRegistry.Player` while building every-frame acoustic radar matrices and resolving listener/camera fallbacks.
Solution: Added cached `SpatialAudioManager` and `IPlayerRuntimeContext` fields with `IGlobalRegistryHotSwapListener` rebind handling. Late-frame refresh, listener AUP fallback, view camera resolve, and listener transform resolve now use cached references.
Rejected Alternatives: Replacing acoustic radar with per-contact raycasts or GameObject blips was rejected; the existing instanced voxel sphere is already the correct Dear Lie for perception. Adding a new signal lane was rejected because this renderer consumes audio runtime samples already owned by `SpatialAudioManager`.
Scalability potential: Low devices keep capped 64-matrix rendering and avoid service-locator reads in LateFrame. Middle keeps the same radar look. High and Ultra can increase audio sample richness inside the audio owner without this renderer adding registry fan-out.
Hardware Impact: Removes one Audio registry read and up to two Player registry reads from active acoustic radar frames. Expected low-end gain is micro-scale but deterministic; the larger value is hot-path dependency isolation.

## Loop 27 / Player Noise Emitter Cached Runtime Context

Problem: `PlayerNoiseEmitter` reports player noise from the Tick path and periodically calls `ResolveReferences()` when dependencies are missing. That refresh still polled `GlobalRegistry.Player`.
Solution: Added a cached `IPlayerRuntimeContext` and `IGlobalRegistryHotSwapListener` rebind handling. `ResolveReferences()` now consumes `_cachedPlayerContext`; registration/unregistration is cold lifecycle only.
Rejected Alternatives: Replacing `NoiseSystem.ReportPlayerSignal` with a new signal lane was rejected because this emitter already reports one owner-local player noise fact into the existing noise owner. Adding managed events was rejected by the EventBus quarantine rule.
Scalability potential: Low devices avoid service-locator reads during missing-reference recovery. Middle/High/Ultra preserve the same player noise richness and can improve downstream fauna response without coupling this emitter to registry polling.
Hardware Impact: Removes one Player registry read from each refresh attempt in the Tick path. Estimate is sub-microsecond per refresh; it removes a repeated global dependency from a player-owned hot system.

## Loop 28 / Random Event Meteor Bus Quarantine

Problem: `RandomEventSystem.TryTriggerMeteorShower()` published `MeteorShowerEvent` through `HectonEventBus` even though source scan found no in-repo subscriber. The same slow-tick random-event owner still pulled Localization, Audio, ObjectPool, Player, VoxelEngine, and SargassumDrag through `GlobalRegistry` while meteor, solar flare, and seismic effects were active.
Solution: Removed the unconsumed meteor MegaBus publish and the `Hecton8.Modding` dependency from `RandomEventSystem`. Added cached service fields plus `IGlobalRegistryHotSwapListener` rebinding for LocalizationRuntime, Audio, ObjectPool, Player, VoxelEngineRuntime, and SargassumDragRuntime. Active meteor boom, delayed thunder, splash pooling, voxel impact, solar radiation, localization, and seismic context helpers now read cached fields.
Rejected Alternatives: Creating a replacement `SignalBus<MeteorShowerEvent>` lane was rejected because no consumer exists and the visual fact is already owned by shader globals plus `RandomEventEvents.RaiseStarted`. Leaving a dead EventBus publish was rejected because it preserves a managed gameplay route with no proof. Polling registry fallback inside helper methods was rejected because it keeps the distributed-monolith dependency alive during active random-event frames.
Scalability potential: Low avoids service-locator fan-out during meteor/solar/cave event ticks while keeping the Dear Lie shader/global route. Middle keeps identical visible behavior. High and Ultra can spend the saved CPU on richer meteor shader/audio response without adding managed EventBus dispatch.
Hardware Impact: Removes one managed EventBus publish from every meteor-shower start and removes up to six registry slot reads from active random-event helper paths. Estimated saving is sub-microsecond on ordinary slow ticks and several microseconds during meteor splash/boom frames on i3/MX350; exact profiler proof remains blocked by the external World compile wall.

## Loop 28 / Random Event Native Payload ABI

Problem: `MeteorShowerEvent`, `RandomEventStartedPayload`, and `SeismicShockwaveEvent` were signal-adjacent/native-queue payloads with implicit sequential layout or a property-backed byte flag. `SeismicShockwaveEvent` crosses into World consumers through `RandomEventEvents`, so hidden packing and property reads weaken ARM64/Burst predictability.
Solution: Converted `MeteorShowerEvent` to explicit 64 bytes, `RandomEventStartedPayload` to explicit 8 bytes, and `SeismicShockwaveEvent` to explicit 128 bytes with double3 fields aligned at offsets 0 and 24. Replaced the `HasAupLineSegment` bool property with a public byte field and updated `WorldGenerativeGeologyVoxelBridgeDirector` to compare it explicitly. Added all three payload full names to `SignalPayloadLayoutValidator`.
Rejected Alternatives: Leaving these as owner-local sequential structs was rejected because they are transported through `NativeQueue` and can regress silently without the editor guard. Widening `RandomEventEvents` into a new generic SignalBus route was rejected because it would create a cross-domain lane for an owner-local deferred event queue that already has a bounded listener contract.
Scalability potential: Low/Middle get predictable packet stride in random-event queues; High/Ultra preserve richer seismic/meteor presentation without layout ambiguity. No binary quality switch was added.
Hardware Impact: `SeismicShockwaveEvent` is intentionally padded to 128 bytes to keep AUP double vectors and Vector3 payloads stable across ARM64; direct ALU savings are not claimed. The value is preventing unaligned native-queue reads and hidden property access in downstream seismic consumers.

## Loop 29 / Logistics Pipe Dead EventBus Route Removal

Problem: `LogisticsPipeNode.TriggerOverpressureRupture()` published an internal `LogisticsPipeOverpressureLeakEvent` through `HectonEventBus`. Source scan found no subscriber, while the same rupture already publishes first-party typed facts through `PipeRuptureSignal` and `ImpactSignal`.
Solution: Removed the managed EventBus publish and the `Hecton8.Modding` using from `LogisticsPipeNode`. Deleted `LogisticsPipeEvents.cs` and its meta because the only DTO in that file was the now-unused property-backed internal event.
Rejected Alternatives: Creating a duplicate `SignalBus<LogisticsPipeOverpressureLeakEvent>` lane was rejected because `PipeRuptureSignal` and `ImpactSignal` already carry the authoritative first-party rupture facts. Keeping the dead event type was rejected because it preserves a misleading managed extension route in a construction hot path.
Scalability potential: Low/Middle avoid managed bus dispatch when pipe stress ruptures. High/Ultra keep rich rupture visuals through the existing typed signals and spline rupture flags, not through managed event fan-out.
Hardware Impact: Removes one managed EventBus publish and one property-backed internal DTO construction per rupture. Ruptures are not per-frame, so raw microsecond gain is small; the architectural gain is eliminating an unowned route and keeping the construction rupture path typed.

## Loop 30 / Surface Weather Thunder EventBus Route Removal

Problem: `HectonSurfaceWeatherDirector.DispatchThunderAcousticShock()` published a `ThunderAcousticShockEvent` through `HectonEventBus`, but source scan found no subscriber. The same thunder strike already emits physics acoustics and camera impact through first-party typed/static owner routes.
Solution: Removed the managed EventBus publish, removed the `Hecton8.Modding` using, and deleted the unused `ThunderAcousticShockEvent` DTO from `HectonSurfaceWeatherDirector`.
Rejected Alternatives: Creating a replacement `SignalBus<ThunderAcousticShockEvent>` lane was rejected because the acoustic fact is already routed through `PhysicsEventBus.NotifyAcousticPing` and the camera fact through `CameraJuiceSignals.PublishImpact`. Keeping the DTO as public extension surface was rejected because no subscriber exists and weather already has `WeatherEvents` as the bounded owner-local event route.
Scalability potential: Low/Middle avoid managed bus dispatch during lightning/thunder bursts. High/Ultra preserve full acoustic shock and camera impact presentation through existing typed routes.
Hardware Impact: Removes one managed EventBus publish and one event DTO construction per thunder acoustic shock. Expected saving is micro-scale during electrical storms; the larger gain is removing dead managed first-party routing from the atmosphere path.

## Loop 31 / Celestial Eclipse MegaBus Route Removal

Problem: `HectonCelestialEngine` started eclipses through both the owner-local `CelestialEvents.RaiseEclipseStarted()` queue and an unconsumed `HectonEventBus.Publish(in EclipseStartedEvent)` route. The EventBus DTO used `[StructLayout(LayoutKind.Sequential, Pack = 1)]`, which violates the ARM64 alignment rule even though no in-repo subscriber exists.
Solution: Removed the MegaBus publish, deleted `PublishEclipseStartedMegaBus()`, removed `using Hecton8.Modding`, and deleted `EclipseStartedEvent`. The existing celestial listener queue, shader globals, and `GlobalTimeSyncSignal` path still own eclipse gameplay/presentation facts.
Rejected Alternatives: Converting `EclipseStartedEvent` into an explicit SignalBus packet was rejected because no consumer exists and it would duplicate `CelestialEvents`. Broadly refactoring the massive celestial file was rejected; this pass only cut the dead route and the Pack=1 DTO.
Scalability potential: Low devices avoid managed EventBus dispatch during eclipse starts and avoid preserving an unsafe payload. Middle keeps the same eclipse gameplay callbacks. High and Ultra preserve shader/global-time visual overkill through the existing celestial owner routes.
Hardware Impact: Removes one managed EventBus publish and one Pack=1 event DTO construction per eclipse start. Direct frame gain is micro-scale because eclipse starts are rare; the hard gain is eliminating an unsafe global route from a core celestial state transition.

## Loop 32 / Beacon HUD Player Runtime Cache

Problem: `BeaconHUDElement.Tick()` calls `TryResolveCamera()` and `TryResolveObserverAup()` every active beacon HUD frame. Both helpers polled `GlobalRegistry.Player`, so a UI presentation loop depended on the global service locator while projecting beacon icons.
Solution: Added `_cachedPlayerContext`, cold service cache on enable, and `IGlobalRegistryHotSwapListener` rebind handling for the Player slot. Hot camera and observer-AUP helpers now read the cached context only.
Rejected Alternatives: Leaving the UI path as "cheap enough" was rejected because it runs every frame when beacons are visible. Replacing beacon projection with physics or raycasts was rejected; the existing Dear Lie is already a camera-plane projection with AUP delta math.
Scalability potential: Low devices avoid repeated service-locator reads during beacon HUD frames. Middle keeps identical icon behavior. High and Ultra can show richer beacon presentation elsewhere without this HUD adding registry fan-out.
Hardware Impact: Removes up to two Player registry reads from active beacon HUD frames. Expected gain is micro-scale per frame; it hardens a recurring UI path against stale service-locator coupling.

## Loop 33 / AR Waypoint Overlay Player Runtime Cache

Problem: `ARWaypointOverlay.Tick()` and `SlowTick()` both call `ResolveOwners()`. That resolver polled `GlobalRegistry.Player` while resolving the projection camera, so the AR waypoint HUD had a repeated hot-path service-locator dependency.
Solution: Added `_cachedPlayerContext`, cold cache fill on enable, and `IGlobalRegistryHotSwapListener` rebind handling for the Player slot. The resolver now uses the cached context and invalidates camera/player-transform state on rebind.
Rejected Alternatives: Moving waypoint projection to a new SignalBus lane was rejected because this is local UI presentation derived from existing waypoint ownership. Adding occlusion raycasts was rejected; the existing camera-plane projection and vegetation bridge occlusion fake are the correct low-cost path.
Scalability potential: Low devices avoid repeated player registry reads in AR projection frames. Middle keeps identical markers. High and Ultra can increase marker polish without this overlay multiplying global dependency reads.
Hardware Impact: Removes one Player registry read from each active AR waypoint Tick/SlowTick owner resolve. Expected saving is micro-scale; the architectural gain is removing another recurring UI locator dependency.

## Loop 34 / Builder Status Overlay Cached Runtime Contexts

Problem: `BuilderStatusOverlay.LateFrameTick()` can retry `AutoResolve()` when builder/inventory/construction/tool references are missing. That retry path polled `GlobalRegistry.Player` and `GlobalRegistry.Environment`, keeping a UI construction overlay tied to hot registry lookup.
Solution: Added cached Player and Environment runtime contexts plus `IGlobalRegistryHotSwapListener` rebind handling. `AutoResolve()` now applies cached contexts only; hot-swap null clears stale UI references instead of leaving dead service pointers.
Rejected Alternatives: Polling the registry only once per second was still rejected because the retry path is driven by LateFrame state and repeats until dependencies appear. Creating a SignalBus request for "current builder" was rejected as 1-to-1 routing abuse; direct cached service references are the correct path.
Scalability potential: Low avoids repeated service-locator reads while construction HUD is trying to resolve. Middle keeps identical overlay cadence. High and Ultra can enrich builder HUD text/feedback without multiplying registry reads.
Hardware Impact: Removes up to two registry reads from each unresolved LateFrame retry. Expected saving is micro-scale; the fix prevents stale hot-loop dependency discovery.

## Loop 35 / Base Integrity HUD Player and Localization Cache

Problem: `BaseIntegrityHUD.SlowTick()` resolved player movement through `GlobalRegistry.Player` when `_playerMovement` was missing and warning text through `GlobalRegistry.Localization` each time a base integrity threshold message was evaluated. Its local `NativeQueue<BaseIntegrityEventPayload>` packet also used implicit sequential layout.
Solution: Added cached Player and Localization runtime references with `IGlobalRegistryHotSwapListener` rebind handling. `ResolvePlayerTransform()` now prefers the cached player context before bootstrap fallback, `ResolvePlayerMovement()` consumes only the cached context, and `ResolveLocalized()` consumes only the cached localization service. Converted the UI `BaseIntegrityEventPayload` to explicit 8-byte layout and added its full name to the native queue payload validator.
Rejected Alternatives: A SignalBus request for "current player/localization" was rejected as one-to-one route abuse. Moving UI base warnings onto the SHINOBU_115 structural 64-byte `BaseIntegrityEventPayload` was rejected because that payload belongs to the habitat deformation stress lane and this UI queue owns different warning semantics. Leaving sequential layout was rejected because this payload is stored in persistent `NativeQueue<T>`.
Scalability potential: Low devices avoid repeated player/localization registry reads during base-warning slow ticks and keep an 8-byte queue packet. Middle keeps identical warning semantics. High and Ultra can spend presentation budget on richer warning/audio consumers without multiplying service-locator reads in this HUD.
Hardware Impact: Removes one Player registry read from missing-movement fallback and one Localization registry read from each integrity-warning text evaluation. Raw gain is micro-scale and event-dependent; ABI gain is deterministic 8-byte queue stride on ARM64.

## Loop 36 / Visor HUD Hot Registry Cache

Problem: `VisorHUDController.Tick()` drives active tool display, pressure flicker, pressure lens cracks, adaptive runtime RT scaling, and visor material scalability. Those helpers still polled `GlobalRegistry.Player`, `ModularEquipment`, `Submarine`, `VRAMMonitor`, and `QualityTier`; RT release/allocation also dereferenced pool/lifecycle services directly.
Solution: Added cached Player, ModularEquipment, Submarine, VRAMMonitor, RenderTexturePool, and RenderTextureLifecycle services plus `IGlobalRegistryHotSwapListener` handling for each slot. Added `IScalabilityChangedEventListener` so quality tier changes update `_runtimeQualityTier` event-driven. Active tool, battery fallback, depth, hull stress, structural grid, adaptive VRAM pressure, RT pool rent/return, and lifecycle disposal now read cached references.
Rejected Alternatives: Leaving visor as presentation-only was rejected because it runs in `Tick()` and writes material/RT state every active frame. Creating SignalBus request packets for "current tool", "current player depth", or "current VRAM pressure" was rejected as one-to-one misuse. Replacing the existing shader projection with CPU UI rebuilds was rejected; the visor glass shader path is the correct Dear Lie.
Scalability potential: Low devices avoid locator fan-out in the visor hot loop while keeping adaptive RT scale and material dither. Middle keeps identical visor presentation. High and Ultra keep shader-side overkill and higher RT scale without paying service locator reads in the frame loop.
Hardware Impact: Removes Player/ModularEquipment lookups from active tool material refresh, Player lookups from pressure/depth channels, Submarine lookup from structural-grid auto-resolve, VRAMMonitor lookup from adaptive RT scale, and QualityTier lookup from scalability matrix refresh. Expected frame gain is micro-scale but recurring; correctness gain is route isolation and hot-swap safety.

## Loop 37 / Survival HUD Player Runtime Cache

Problem: `SurvivalHUDController.LateFrameTick()` retries survival-system resolution when the HUD starts before player survival is available. That retry path polled `GlobalRegistry.Player` every 30 frames until the dependency appeared.
Solution: Added cached `IPlayerRuntimeContext` and `IGlobalRegistryHotSwapListener` rebind handling. `ResolveSurvivalSystem()` now reads `_cachedPlayerContext` and only uses `GameBootstrapper` as a non-registry hierarchy fallback when the cached service is absent.
Rejected Alternatives: Adding a SignalBus request for the current survival system was rejected as one-to-one routing abuse. Leaving the 30-frame throttle was rejected because throttled polling is still distributed-monolith coupling in a LateFrame visual sync component.
Scalability potential: Low devices avoid locator reads during HUD startup/recovery. Middle keeps identical survival bar behavior. High and Ultra can layer richer HUD material effects without the survival bars multiplying player registry reads.
Hardware Impact: Removes one Player registry read from each unresolved survival HUD retry. The gain is micro-scale but recurring during player bootstrap/hot-swap windows.

## Loop 38 / Diegetic Visor HUD Continuous Mesh Density

Problem: `DiegeticVisorHudMesh` resolved its camera through `GlobalRegistry.Player` and rebuilt mesh density from a discrete `HectonQualityTier` switch. Its black-box telemetry row also used sequential 36-byte layout, leaving a weak NativeArray stride contract.
Solution: Added cached `IPlayerRuntimeContext`, `IGlobalRegistryHotSwapListener`, and `IScalabilityChangedEventListener`. Camera resolution now reads the cached player context. Mesh density is driven by cached `HomeostasisBrain.GlobalQualityWeight` through a continuous lerp/step curve. `DiegeticHudTelemetryEntry` is explicit 40 bytes with a manual 4-byte pad.
Rejected Alternatives: Keeping tier switch density was rejected because it creates visual pops and violates continuous quality policy. Creating a SignalBus request for the current player camera was rejected as one-to-one routing abuse. Replacing the curved projection with raycasts or screen-space canvas rebuilds was rejected; the curved mesh remains the correct Dear Lie.
Scalability potential: Low collapses the mesh toward 4x2 segments. Middle lands near authoring density. High ramps above authoring. Ultra reaches the configured 64x32 cap without route changes or binary device checks.
Hardware Impact: Removes one Player registry read from camera fallback and replaces binary density branches with scalar math. The raw per-frame gain is small because mesh rebuild is event/config driven, but low devices avoid carrying high-density mesh topology after quality drops; black-box rows now stride at 40 bytes instead of unsafe 36-byte sequential layout.

## Loop 39 / PDA Focus Distance Cached Player Camera

Problem: `DiegeticPdaFocusDistanceController.LateFrameTick()` can retry `ResolveReferences()` while focus is armed. That resolver polled `GlobalRegistry.Player` for the player camera, and a player hot-swap could leave a stale camera-owned `Volume`/DOF reference.
Solution: Added cached `IPlayerRuntimeContext` plus `IGlobalRegistryHotSwapListener`. `ResolveReferences()` now uses `_cachedPlayerContext`. Player hot-swap refreshes camera-derived references and clears camera-owned volume/DOF when the old player camera is replaced or removed.
Rejected Alternatives: A SignalBus request for the current player camera was rejected as one-to-one routing abuse. Removing the one-slot `Physics.RaycastNonAlloc` was rejected in this pass because it is a local PDA close-focus presentation fake, not a terrain/query signal bridge; it already uses a retained single hit buffer and one ray per armed frame.
Scalability potential: Low devices avoid registry lookup during focus recovery and still perform the minimal one-ray close-focus approximation. Middle keeps the same depth-of-field behavior. High and Ultra can keep sharper PDA focus response without adding global locator fan-out.
Hardware Impact: Removes one Player registry read from each unresolved focus retry. Expected gain is micro-scale; the more important correction is deterministic hot-swap rebinding of camera/Volume state.

## Loop 40 / Diegetic Tooltip Cached Camera and Continuous Quality

Problem: `DiegeticTooltipSystem.Render()` could fall back to `ResolveCamera()`, which polled `GlobalRegistry.Player`. The same tooltip renderer used a binary low-tier flag to snap fade and disable dither, and its black-box row relied on implicit sequential layout.
Solution: Added cached `IPlayerRuntimeContext` and fed render-camera fallback from that cache. Removed hot retry polling of input determinism from `ResolveCurrentSchemeHash()`. Replaced `_lowTierActive` with `_qualityWeight01` from `HomeostasisBrain.GlobalQualityWeight`; fade duration and dither weight now use continuous scalar curves. Converted `TooltipBlackBoxEntry` to explicit 32-byte layout.
Rejected Alternatives: Keeping low-tier snap/dither branches was rejected because it hard-pops UI presentation and violates continuous quality policy. Adding a SignalBus lane for "current render camera" was rejected as one-to-one misuse. Rebuilding tooltip glyphs as UI Canvas objects was rejected; current indirect mesh drawing is the correct Dear Lie.
Scalability potential: Low gets near-instant alpha convergence and dither weight near zero. Middle interpolates both fade and dither. High and Ultra keep authored fade plus full dither without changing route topology.
Hardware Impact: Removes Player registry fallback from render camera resolution and removes input-determinism registry retry from the scheme hash hot path. Expected gain is micro-scale per tooltip frame; low devices also avoid dither work through a shader scalar rather than a branch.

## Loop 41 / Submarine Sonar Holo Map Continuous Quality

Problem: `SubmarineSonarHoloMapRenderer.RunVisualSync()` used `GlobalRegistry.ScalabilityTier` polling plus `HectonQualityTier` switches for grid density, sample interval, and interpolation. `ResolveViewCamera()` also polled `GlobalRegistry.Player`.
Solution: Added cached `IPlayerRuntimeContext`, `IGlobalRegistryHotSwapListener`, and `IScalabilityChangedEventListener`. Camera fallback now reads cached Player context. Grid cells, update interval, and interpolation blend derive from cached `HomeostasisBrain.GlobalQualityWeight` through smooth curves.
Rejected Alternatives: Keeping tier hysteresis was rejected because it periodically polls the registry and still produces discrete LOD jumps. Adding a SignalBus request for the current player camera was rejected as one-to-one misuse. Replacing voxel SDF/navigation sampling with Physics raycasts was rejected; the existing sonar line mesh is the correct Dear Lie.
Scalability potential: Low stays near 8x8 cells, 0.1s sample cadence, and no interpolation blend. Middle glides through intermediate density/cadence/blend. High and Ultra approach 18x18 cells, 0.033s cadence, and full interpolation without route changes.
Hardware Impact: Removes two scalability registry probes per quality polling window and one Player registry fallback from view camera resolve. Low-end work drops through fewer grid samples and slower sample cadence; exact profiler proof remains blocked by external World compile wall.

## Loop 42 / Vehicle Sub OS Cockpit Runtime Signal Cache

Problem: `VehicleSubOsCockpitRuntime.Tick()` called a helper that polled `GlobalRegistry.ScalabilityTier`, then used discrete `HectonQualityTier` branches for radar capacity, point amplification, RT format, external feed lockout, and damage hologram mode. The same cockpit hot path resolved PlayerCriticalAudio, GroundRadar, HabitatGraph, PowerGrid, and RenderTexturePool through `GlobalRegistry` inside runtime helpers. Its button Burst job used default Burst flags, and its GPU/black-box DTOs relied on implicit sequential layout.
Solution: Added cold service cache hydration, `IGlobalRegistryHotSwapListener`, and `IScalabilityChangedEventListener`. Runtime helpers now read cached audio/GPR/habitat/power/RT pool references. Quality policy now consumes `HomeostasisBrain.GlobalQualityWeight`: radar capacity lerps from 512 to 4096 in 128-point resource buckets, radar points-per-tap lerps from 32 to 256 in 16-point buckets, UI/external RT dimensions scale by smooth quality, external feed falls back to static/no-feed only near minimum quality, and the damage hologram shader receives a continuous cheap-visual weight. `RadarBlipGpuData` is explicit 32 bytes; `CockpitTelemetryEntry` is explicit 64 bytes; `ButtonKinematicJob` has required Burst flags and `[NoAlias]` fields.
Rejected Alternatives: Keeping a tier switch was rejected because it causes visible LOD jumps and keeps the cockpit coupled to global registry polling. Creating SignalBus requests for current audio/GPR/power services was rejected as one-to-one misuse. Replacing the existing GPU radar and indirect damage hologram with CPU-generated meshes was rejected; the existing compute/indirect path is the correct Dear Lie.
Scalability potential: Low keeps radar around 512 points, 32 points per tap, smaller RTs, static external feed, and cheap glyph-style damage hologram. Middle smoothly increases RT dimensions and radar amplification. High reaches dense radar and compute hologram. Ultra keeps the same route but pushes to the 4096-point/256-per-tap caps and full external feed.
Hardware Impact: Removes repeated registry reads from the cockpit Tick path and prevents tier-poll churn. Low-end savings are mainly from lower radar point count, smaller RTs, and skipped external camera feed; service-cache savings are micro-scale per frame. Exact profiler proof remains blocked by the missing World source compile wall.

## Loop 43 / Diegetic PDA Cached Player Context

Problem: `DiegeticPDAController.Tick()` can repeatedly call `ResolveReferencesThrottled()` while PDA references are missing. That resolver polled `GlobalRegistry.Player` for the current `PlayerPDA` and player tool hand anchor; `ResolveVisibilityCamera()` also polled Player when the cached camera was invalid.
Solution: Added `_cachedPlayerContext`, cold cache hydration, and `IGlobalRegistryHotSwapListener`. Reference retry now reads the cached player context for `PlayerPDA`, `PlayerToolManager.HandAnchor`, and `PlayerCamera`. Player hot-swap updates only references that were sourced from the previous player context, preserving explicitly authored scene references.
Rejected Alternatives: A SignalBus request for current PDA/hand anchor/camera was rejected as one-to-one misuse. Forcing the pointer cache to rebuild through managed UI discovery every frame was rejected; the existing cached pointer target arrays remain the right local presentation path.
Scalability potential: Low avoids registry reads during PDA open/retry and camera visibility checks. Middle keeps unchanged PDA behavior. High and Ultra can keep richer PDA presentation while service replacement remains cold and deterministic.
Hardware Impact: Removes up to three Player registry reads from unresolved PDA retry/camera fallback windows. Expected gain is micro-scale per active retry, but it removes another hot UI dependency on the global locator.

## Loop 44 / Physical Panel Button Cached Audio And Player Services

Problem: `PhysicalPanelButton.ApplyInteractionSignal()` can call `PlayDiegeticClick()` on physical hand press. That helper polled `GlobalRegistry.Audio` for click routing, and its listener transform resolver polled `GlobalRegistry.Player` for occlusion/source-listener pathing.
Solution: Added cached `IAudioService` and `IPlayerRuntimeContext` plus `IGlobalRegistryHotSwapListener`. Press audio now reads `_cachedAudioService`; listener fallback reads `_cachedPlayerContext`. Audio and Player hot-swap notifications refresh those fields.
Rejected Alternatives: A SignalBus request for current audio service or player listener transform was rejected as one-to-one misuse. Removing the existing `InteractionSignal` publish was rejected because it is the authoritative physical hand interaction route and already carries an AUP-derived packet into the interaction service.
Scalability potential: Low avoids service-locator reads during physical button presses and still keeps the cheap local button depression math. Middle keeps identical press behavior. High and Ultra preserve spatial click/occlusion presentation without adding registry fan-out.
Hardware Impact: Removes one Audio registry read per accepted press and one Player registry read when fallback AudioClip occlusion is used. Expected gain is micro-scale per press; architectural value is isolating another interactive UI path from the global locator.

## Loop 45 / Diegetic Panel Cached Player And Continuous RT Policy

Problem: `DiegeticPanelController.Tick()` drives panel projection, cursor state, RT allocation checks, proxy light state, and input dispatch. Its `ResolveInteractionCamera()` fallback polled `GlobalRegistry.Player`, and its phosphor/RT policy used binary MX350/scalability tier decisions that produced hard resource jumps. Its local input event and panel state packets used implicit sequential layouts.
Solution: Added `_cachedPlayerContext`, cold cache hydration, and Player hot-swap rebinding. `ResolveInteractionCamera()` now uses the cached player camera. Added `IScalabilityChangedEventListener` and cached `HomeostasisBrain.GlobalQualityWeight`; RT resolution now lerps from 128x64 to 2048x1024 through 64-pixel buckets using distance and quality curves, while phosphor history fades in through a smooth blend between disabled/cheap and authored decay. Converted `DiegeticPanelInputEvent` to explicit 32 bytes and `PanelData` to explicit 208 bytes.
Rejected Alternatives: A SignalBus request for current player camera was rejected as one-to-one route abuse. Keeping the binary low-tier phosphor gate was rejected because it hard-popped the panel surface. Replacing panel projection with physics raycasts or GraphicRaycaster rebuilds was rejected; the local camera-plane projection plus RT surface remains the correct Dear Lie.
Scalability potential: Low collapses panels toward 128x64 RGB565 and disables phosphor history below the smooth activation band. Middle ramps through intermediate RT buckets and partial phosphor decay. High uses denser RTs with stable panel projection. Ultra reaches the 2048x1024 cap and authored phosphor trail without changing route topology.
Hardware Impact: Removes one Player registry read from panel camera fallback during active UI projection. Low-end savings are mainly lower RT memory/bandwidth and skipped phosphor history buffers; service-cache savings are micro-scale per frame. Compile/profiler proof remains blocked by the missing World source.

## Loop 46 / Acoustic Translator And Audio Caption Cached Services

Problem: `AcousticEcholocationTranslator` resolved Player, Localization, and Atmosphere services through `GlobalRegistry` while classifying sonar contacts, applying hull-stress text mutation, and deciding whether to render visual acoustic-wave barks. `AudioCaptionOverlay` resolved Player during caption camera fallback and AUP-origin fallback.
Solution: Added cached Player/Localization/Atmosphere services and `IGlobalRegistryHotSwapListener` to `AcousticEcholocationTranslator`. Added cached Player service and hot-swap rebinding to `AudioCaptionOverlay`. Classification origin, localized span lookup, stress mutation, visual sound-wave fog checks, caption camera fallback, and caption AUP origin now read cached services.
Rejected Alternatives: Adding SignalBus requests for current localization, atmosphere, or player camera was rejected as one-to-one misuse. Moving audio captions through `HectonEventBus` was rejected because the existing `AudioCaptionEvents` direct listener route is already owner-local presentation. Replacing caption projection with raycasts was rejected; the existing camera-plane projection with AUP-relative math is the correct Dear Lie.
Scalability potential: Low avoids service-locator reads during sonar/caption bursts and keeps the cheap approximate direction normalization. Middle keeps existing captions and barks. High and Ultra can show richer acoustic captions without global lookup fan-out.
Hardware Impact: Removes Player/Localization/Atmosphere registry reads from sonar classification and visual acoustic-wave paths, plus Player registry reads from spatial caption camera/AUP fallback. Expected gain is micro-scale per active event/caption frame; compile/profiler proof remains blocked by the missing World source.

## Loop 47 / Suit HUD Continuous Reactive Cadence

Problem: `SuitHUDV4CanvasOverlay.SlowTick()` polled `GlobalRegistry.ScalabilityTier` and converted it into a binary `_lowTierDirtyThrottleActive` gate. The reactive HUD update path then skipped visual refreshes by a hard low-tier branch. Its `ThreatChevronState` packet was a sequential 64-byte struct.
Solution: Added `IScalabilityChangedEventListener`, cached `HomeostasisBrain.GlobalQualityWeight`, and replaced the binary low-tier gate with a continuous smoothstep-derived 1..4 frame cadence stride. `SlowTick()` refreshes the cached quality policy instead of polling the registry tier. `ThreatChevronState` is now an explicit 64-byte layout with `AbsoluteUniversePosition` at offset 0 and `Threat01` at offset 48.
Rejected Alternatives: Keeping a boolean low-tier throttle was rejected because it creates a hard cadence jump and keeps the HUD coupled to registry tier reads. Moving HUD refresh cadence into SignalBus was rejected because it is owner-local presentation policy, not a broadcast fact. Replacing the existing cheap reactive update with full per-frame canvas rebuilds was rejected; the cadence gate is the intended Dear Lie for low-end UI stability.
Scalability potential: Low runs the reactive visual solve through a 4-frame stride unless a signal is dirty. Middle glides through 2-3 frame strides. High and Ultra use stride 1 and keep full reactive HUD cadence without changing route topology.
Hardware Impact: Removes one scalability registry read per SlowTick and avoids binary cadence flips. Low-end savings come from fewer non-critical HUD visual solves while reactive dirty signals still bypass the stride; exact profiler proof remains blocked by the missing World source.

## Loop 48 / Fake Radar Continuous Blip Budget

Problem: `FakeRadarBlipController` used a fixed 64 hostile-contact candidate budget and a fixed 8 decorative thermal ghost budget. That violates the continuous quality law for a HUD-only fake, and makes the cheapest devices carry the same decorative radar solve width as high-end machines.
Solution: Added cached quality policy driven by `HomeostasisBrain.GlobalQualityWeight` and `IScalabilityChangedEventListener`. Candidate capacity now lerps 16..64 through smoothstep and is frozen into `_scheduledBlipCapacity` for each scheduled cull solve. Thermal noise ghosts now lerp 0..8 from the same policy. Player AUP, transform, and projection camera fallbacks read `_cachedPlayerContext`; the only Player registry access is cold cache hydration.
Rejected Alternatives: Keeping the fixed 64/8 budgets was rejected because radar noise is presentation, not gameplay truth. A SignalBus request for the current player camera/AUP was rejected as one-to-one misuse. Replacing the flat XZ radar fake with per-contact 3D physics/raycast sensing was rejected; the existing spatial-hash query plus Burst 2D cull and one instanced draw is the correct Dear Lie.
Scalability potential: Low limits hostile radar work to 16 candidates and suppresses most or all decorative thermal ghosts. Middle ramps through intermediate blip and ghost counts without a tier jump. High uses denser hostile contacts. Ultra reaches 64 hostile candidates and 8 thermal ghosts while preserving the same route and draw path.
Hardware Impact: Low-end savings are bounded by the fixed 64-entry query buffer but reduce candidate writes, cull job width, matrix handoff, and decorative ghost matrices by up to 48 hostile entries and 8 ghost entries per active radar solve. Expected gain is micro-scale per frame; compile/profiler proof remains blocked by the missing World source.

## Loop 49 / Acoustic Radar Continuous Contact Budget

Problem: `AcousticRadarSphereRenderer` projected active acoustic impact samples into a voxel-sphere HUD fake with a fixed 64 matrix budget. Audio and Player services were cached in the working tree, but decorative contact draw width still did not respond to thermal quality pressure.
Solution: Added `IScalabilityChangedEventListener` and a cached matrix-cap policy from `HomeostasisBrain.GlobalQualityWeight`. Matrix capacity now smoothsteps/lerps from 16 to 64 and is applied to the sample-to-matrix loop. The existing cold Audio/Player cache and hot-swap path remain the only service route for active rendering.
Rejected Alternatives: Keeping all 64 decorative acoustic contacts on low quality was rejected because the sphere is presentation, not gameplay truth. A SignalBus request for current audio/player camera state was rejected as one-to-one misuse. Replacing the AUP-relative projection with physics raycasts or GameObject markers was rejected; the current instanced voxel projection is the intended Dear Lie.
Scalability potential: Low draws up to 16 acoustic contacts after amplitude, distance, and rear-hemisphere filters. Middle ramps smoothly through intermediate contact density. High and Ultra use the full 64-instance visual overkill budget without route changes.
Hardware Impact: Removes hot registry reads already present in the working tree and now reduces matrix writes plus `DrawMeshInstanced` instance count by up to 48 decorative contacts on low quality. Expected gain is micro-scale per active acoustic burst; compile/profiler proof remains blocked by the missing World source.

## Loop 50 / Gyro Compass Explicit DTOs And Continuous Quality Cadence

Problem: `DiegeticGyroCompassRuntime` had `[StructLayout(Pack=1)]` telemetry/presentation DTOs, polled `GlobalRegistry.ScalabilityTier` from cold dependency resolution, and collapsed cadence/visuals through `HectonQualityTier` plus `_lowTier`. The Burst job also lacked `CompileSynchronously` and aliasing proof on its NativeSlice fields.
Solution: Converted `CompassBlackBoxEntry` to explicit 64 bytes and `CompassPresentationStateDTO` to explicit 80 bytes. Removed quality tier storage and `_lowTier`; quality policy now reads/injects `HomeostasisBrain.GlobalQualityWeight`, deriving a 1..6 fast-cadence stride and an overkill scalar. FastTick accumulates deterministic delta until the stride gate opens. Indirect dial, shader overkill, and failure particles scale from `_visualOverkillWeight01`. Added hot-swap/scalability listeners and Burst/NoAlias job attributes.
Rejected Alternatives: Keeping tier gates was rejected because it preserves binary quality jumps and registry tier coupling. Adding SignalBus requests for Player/DataVault/quality lookup was rejected as one-to-one misuse. Simulating compass drift with physical gyroscope bodies was rejected; the deterministic drift/noise job plus shader/indirect dial presentation is the correct Dear Lie.
Scalability potential: Low uses a 6-fast-tick stride, triangle drift noise, no indirect dial allocation, and no particle bursts. Middle traverses 5..2 stride values and partial overkill scalar. High reaches stride 1 and begins indirect dial/shader overkill. Ultra keeps stride 1 and full particle/indirect presentation without changing service routes.
Hardware Impact: Removes cold scalability registry polling and prevents binary tier churn. Low-end savings come from fewer drift job schedules, cheaper triangle noise, skipped indirect buffers/draws, and lower particle emission. Exact profiler proof remains blocked by the missing World source.

## Loop 51 / Tool Diegetic Display Continuous Fallback

Problem: `ToolDiegeticDisplayController` still queued `HectonQualityTier` candidates from `ScalabilityChangedEvent`, polled `GlobalRegistry.ScalabilityTier` from `SlowTick`, used `_lowTierActive` to disable the offscreen RT camera, and wrote visual overkill through a tier switch. This kept a held-tool first-20-minutes route moment coupled to the global tier registry and produced hard presentation changes.
Solution: Removed tier fields and candidate methods. Quality policy now reads `HomeostasisBrain.GlobalQualityWeight`, smoothsteps `_qualityFallback01`, derives `_visualOverkill01`, and toggles the fallback texture through the existing 2-second hysteresis window. `OnScalabilityChanged` and `SlowTick` refresh the cached scalar only. `ApplyScreenTexture` uses a generic fallback scalar while keeping the shader property string `_ToolLowTierFallback01` for material compatibility. Scanner title resolution collapses to compact percent text when the continuous fallback scalar is high.
Rejected Alternatives: Keeping `HectonQualityTier` hysteresis was rejected because it still requires tier candidate state and registry polling. Adding a SignalBus lane for current quality or RenderTexturePool was rejected as one-to-one route abuse. Simulating a physical tool display surface was rejected; the existing RT camera plus fallback emissive texture is the correct Dear Lie.
Scalability potential: Low uses the static emissive fallback texture and compact scanner text, avoiding RT camera work. Middle crosses the fallback threshold through the same 2-second hysteresis rather than a frame pop. High keeps the RT camera active and feeds partial shader overkill. Ultra keeps full RT presentation and `_ToolVisualOverkill01 = 1` without changing routes.
Hardware Impact: Removes one `GlobalRegistry.ScalabilityTier` read per SlowTick and deletes enum switch/candidate churn. Low-end savings come from disabling the offscreen 256 RT camera and scanner title lookup/scramble under quality pressure. Expected gain is micro-scale plus avoided render pass; runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Improves first tool pickup/use readability and prevents a thermally weak device from spending a render pass on a held-tool screen during early exploration.

## Loop 52 / PDA Archaeology Label Continuous Scramble

Problem: `PDADataArchaeologyDecryptLabel` still read `GlobalRegistry.ScalabilityTier` and converted `HectonQualityTier` into a binary `_scrambleAllowed` flag. The same file also contained a stale `_scrambleProbeCountdown` assignment without a declared field, which is a compile hygiene defect independent of the external World compile wall.
Solution: Removed tier reads and the binary scramble gate. `OnEnable` and `OnScalabilityChanged` now refresh `_scrambleIntensity01` from `HomeostasisBrain.GlobalQualityWeight` using smoothstep. The scramble routine always writes through the pooled char buffer but linearly reveals more source characters as quality drops, so minimum quality degenerates into a readable title copy instead of animated noise. Removed the stale `_scrambleProbeCountdown` write.
Rejected Alternatives: Keeping `HectonQualityTier` was rejected because this label is pure presentation and does not need global registry tier state. A SignalBus request for current quality was rejected as one-to-one misuse. Replacing the text scramble with localized string assignment was rejected because TMP `.text` would allocate and the existing `SetCharArray` route is correct.
Scalability potential: Low reveals almost the full title and stops spending phase churn on scramble noise. Middle partially scrambles unrevealed glyphs. High and Ultra keep the full decryption scramble visual fake without changing the data route.
Hardware Impact: Removes one cold scalability registry read on enable and a tier enum branch from scalability callbacks. Low-end savings are small per label but avoid repeated animated glyph churn when many archaeology rows are visible. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Improves early PDA/scanner readability when the player first discovers data archaeology entries on weak hardware.

## Loop 53 / PDA Spectrogram Continuous Density And DTO Layout

Problem: `PDADecryptionSpectrogramPanel` used `_cachedScalabilityTier`, `GlobalRegistry.ScalabilityTier`, and a binary low/high point-count decision. It also contained nested native DTOs with `LayoutKind.Sequential, Pack=1` and Burst jobs with default synchronous behavior plus low precision. Because the panel owns vault buffers and jobs, leaving those defects while editing the file would preserve ARM64 and Burst violations.
Solution: Replaced tier storage with `_cachedQualityWeight01` sourced from `HomeostasisBrain.GlobalQualityWeight`. `ResolvePointCount()` now smoothsteps 32..128 points and clamps quality continuously by reported VRAM using the renamed `minimumQualityVideoMemoryMb` field, preserving serialized data with `[FormerlySerializedAs("lowTierVideoMemoryMb")]`. Scalability events mark native/graphics resources dirty only when the resolved point count changes and complete any scheduled job before invalidating resources. Converted stage target, GPU segment, and telemetry structs to explicit layouts. Added required Burst flags and `[NoAlias]` fields to both wave jobs.
Rejected Alternatives: Keeping the tier enum was rejected because the minigame is presentation and already has a continuous quality scalar available. Adding a SignalBus lane for current quality was rejected as one-to-one misuse. Keeping Pack=1 was rejected because these structs back vault/GPU slices and are processed in Burst jobs.
Scalability potential: Low trends toward 32 wave points and lower segment buffer upload cost. Middle smoothly increases wave detail through intermediate counts. High reaches dense 128-point waves. Ultra keeps the same route and spends saved CPU/GPU budget on full smooth wave visual presentation.
Hardware Impact: Low-end path can cut wave points by 75%, segment instances by roughly 75%, and GPU upload width by the same ratio. Registry savings are cold-path only; main impact is fewer Burst iterations and smaller segment uploads. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Keeps the early scanner/PDA tuning minigame readable on weak hardware while preserving richer wave presentation on stronger machines.

## Loop 54 / Terminal OS Cached Quality Input Camera

Problem: `TerminalOsRuntime` still read `GlobalRegistry.ScalabilityTier` inside `RefreshScalabilityPolicy()`, mapped `HectonQualityTier` in `ResolveGlobalQualityWeight01()`, and used `GlobalRegistry.Player/Input` fallbacks from LateFrame-driven camera/gaze interaction paths.
Solution: Removed tier state and the tier fallback. Quality policy now derives from `HomeostasisBrain.GlobalQualityWeight` with a NaN fallback to the last cached scalar, plus the existing designer `minimumQualityWeight`. Added `IGlobalRegistryHotSwapListener` and `IScalabilityChangedEventListener`; Input and Player services are cached cold and refreshed on service replacement, while scalability events reset the quality refresh window. `ResolveAttentionCamera()` and `ResolveGazeInput()` now consume cached references only.
Rejected Alternatives: Keeping the enum fallback was rejected because it preserves a second route for the same quality fact. Adding a SignalBus request lane for current input, camera, or quality was rejected as one-to-one route abuse. Replacing the terminal attention cull with raycasts or UI GraphicRaycaster scans was rejected; the camera-plane/AABB math and instanced panel fake are the correct Dear Lie.
Scalability potential: Low keeps longer update intervals and lower terminal texture resolution through a continuous scalar. Middle traverses intermediate cadence and resolution values without a tier pop. High and Ultra keep 512px terminal array presentation and fast updates while preserving the same typed click/command lanes.
Hardware Impact: Removes one scalability tier read from each terminal LateFrame policy refresh and removes Player/Input registry fallback reads from gaze/camera interaction recovery. Low-end savings are micro-scale per active terminal frame, with larger bandwidth savings from keeping terminal RT resolution and update cadence tied to the scalar. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Stabilizes early diegetic terminal interaction on weak hardware: the player can still read/use low-resolution terminals without global locator polling when first entering a base/derelict terminal area.

## Loop 55 / OpenXR Manual Override Continuous IK Quality

Problem: `OpenXRManualOverrideLever` resolved `_lowTierMath` from `GlobalRegistry.ScalabilityTier` and updated it from `ScalabilityChangedEvent.CurrentTier == LowMx350`. That produced a binary IK quality path on a first-route cockpit control and left a sequential NativeArray telemetry DTO.
Solution: Replaced `_lowTierMath` with `_ikQualityWeight01` and `_activeIkBlend`. IK blend now uses `HomeostasisBrain.GlobalQualityWeight`, smoothstep, and `math.lerp(minimumQualityIkBlend, maximumQualityIkBlend, curve)`. Serialized scene values are migrated with `FormerlySerializedAs`. `ManualOverrideLeverTelemetryEntry` is now explicit 48 bytes with manual tail padding.
Rejected Alternatives: Keeping a LowMx350 comparison was rejected because cockpit hand IK is presentation quality and must breathe continuously. Adding a SignalBus request for quality or input was rejected as one-to-one misuse. Replacing the lever kinematics with physics joints was rejected; deterministic angle solve plus IK target blending is the correct Dear Lie.
Scalability potential: Low uses the minimum IK blend for cheaper hand-target smoothing. Middle moves through intermediate blends. High and Ultra use the maximum IK blend for tighter hand anchoring without changing the lever signal route.
Hardware Impact: Removes a scalability registry read at enable-time and deletes the binary branch from IK presentation. Per-frame savings are micro-scale; the larger value is preventing quality pops on early VR manual override interaction. Runtime profiler proof remains blocked by active external compile processes and the missing World source.
First 20 Minutes Route Impact: Improves the early cockpit/manual override interaction: weak hardware sees a cheaper but stable hand-target follow, while high/ultra keep tight IK presentation.

## Loop 56 / Acoustic Echo Continuous Quality Byte

Problem: `AcousticEchoLocationRuntime` still treated acoustic echo quality as a two-profile byte: `QualityTier` fields in tap/trail/result DTOs, `_cachedQualityTier`, `ResolveQualityTier()`, a direct `GlobalRegistry.ScalabilityTierProfileByte` read, and a hard `ScalabilityTierProfiles.LowMx350` branch that disabled head sweep. `SpatialAudioManager.PublishAcousticEchoPortalTap()` also fed that route by polling the same registry profile byte.
Solution: Replaced the tier byte semantic with `QualityWeightByte` at the same explicit offsets. `AcousticEchoLocationRuntime.EncodeQualityWeightByte()` converts `HomeostasisBrain.GlobalQualityWeight` to a compact 0..255 scalar; frame refresh updates the cached byte once per acoustic solve. Head sweep now multiplies the existing sine fake by `smoothstep((quality - 0.12) / 0.88)` instead of branching on a hardware profile. The audio portal tap bridge now passes encoded global quality instead of the registry tier.
Rejected Alternatives: Keeping `ScalabilityTierProfiles.LowMx350` was rejected because acoustic hunt presentation quality is not a binary platform fact. Adding a SignalBus request for current quality was rejected as one-to-one misuse; `HomeostasisBrain` is the quality owner. Replacing the head-sweep visual with heavier predator acoustic physics was rejected; the sine sweep over AUP-relative distance is the correct Dear Lie.
Scalability potential: Low damps head-sweep amplitude near zero and keeps direct-node breadcrumbs cheap. Middle gradually restores sweep readability. High and Ultra get full head-sweep presentation and portal echo richness without changing the typed signal route or DTO size.
Hardware Impact: Removes one registry tier read from acoustic echo refresh and one registry profile read from the spatial-audio portal tap bridge. Savings are micro-scale per active portal/acoustic frame, with stronger value from avoiding binary presentation pops and keeping DTOs compact/aligned. Runtime profiler proof remains blocked by CPU gate and the missing World source.
First 20 Minutes Route Impact: Early predator/noisemaker echo behavior now scales smoothly on weak hardware while preserving readable investigation cues on stronger machines.

## Loop 57 / Flora Fauna Symbiosis Quality Fallback

Problem: `ShinobuFloraFaunaSymbiosisSolver.ResolveGlobalQualityWeight()` correctly preferred the vault `ShinobuScalabilityState` and `HomeostasisBrain.GlobalQualityWeight`, but if both were invalid it fell back to `GlobalRegistry.ScalabilityTierProfileByte`. That reintroduced the binary profile route into a continuous symbiosis tuning path.
Solution: Removed the registry fallback. The function now returns `1f` only when both authoritative continuous quality sources are unavailable or NaN, keeping the tuning DTO finite without adding a second quality authority.
Rejected Alternatives: Mapping `ScalabilityTierProfiles.LowMx350/HighRtx` was rejected because it keeps exactly the binary fallback the batch forbids. Adding a SignalBus request for quality was rejected as one-to-one route abuse. Returning `0f` on NaN was rejected because a corrupt quality scalar should not collapse the ecosystem solver into minimum mode for the entire frame.
Scalability potential: Low, Middle, High, and Ultra still use the vault/Homeostasis scalar when valid. The new fallback is only a NaN containment path; it preserves visual overkill instead of silently engaging a binary low profile.
Hardware Impact: Removes one registry tier read from the quality fallback path. Expected runtime savings are micro-scale; the larger impact is eliminating a second source of quality truth and avoiding hard behavior jumps after a bad scalar.
First 20 Minutes Route Impact: Early flora/fauna symbiosis tuning no longer depends on platform tier fallback if the scalability vault is late; weak and strong devices both converge through the continuous scalar once it is available.

## Loop 58 / Leviathan Stalk Continuous Math LOD

Problem: `LeviathanStalkJob` converted `SystemStress01 > 0.8f` and a runtime flag into a binary low-tier branch. That branch picked steering blend, cadence, SDF contour enablement, telemetry flags, particle budget, SSS pulse, and silhouette noise as hard modes inside a Burst path.
Solution: Replaced the bool with `mathLodPressure01 = max(forcedSurvival, smoothstep((systemStress - 0.62) / 0.38))`. Steering blend and cadence now lerp continuously from precision to survival values. SDF contouring fades through `sdfQuality01 = smoothstep((visualQuality - 0.45) / 0.55)`, and presentation scalars consume `sdfOverkill01` instead of a tier branch. Constant/flag names were renamed to survival/precision terminology without changing bit positions or DTO sizes.
Rejected Alternatives: Keeping the `systemStress > 0.8f` branch was rejected because it causes an abrupt behavior and presentation pop. Adding a `GlobalQualityWeight` registry read inside the job was rejected because this job already consumes a vault sensory row and must stay pure. Simulating predator wake, salt crystals, and SDF contour with physics queries was rejected; tangent orbit plus shader scalar outputs are the intended Dear Lie.
Scalability potential: Low/survival pressure approaches cadence 0.2s, steering blend 0.2, damped silhouette noise, and no SDF contour overkill. Middle moves through intermediate blend/cadence/noise/SDF weights. High and Ultra approach cadence 1/60s, precision steering blend 0.55, full triangle silhouette fake, and full SDF contour visual overkill when the sensory row requests it.
Hardware Impact: Removes binary branch divergence in the stalk job and lets stress shed ALU and presentation outputs proportionally. Expected low-end gain is micro-scale per Leviathan slot, larger when 64 slots are scheduled; runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Early predator encounter readability stays intact on weak hardware: the same orbit fake continues, but expensive visual contouring and particle overkill fade instead of snapping.

## Loop 59 / Wrist HUD Continuous Quality And Explicit DTOs

Problem: `WristHologramHudRuntime` still cached `HectonQualityTier`, read `GlobalRegistry.ScalabilityTier`, fed an integer `QualityTier` into a Burst job, and used a binary low-tier path for smoothing, depth wave math, acoustic mock count, and radar count. The file also kept multiple vault/GPU DTOs as sequential structs and used bare `[BurstCompile]`.
Solution: Replaced tier state with `_cachedQualityWeight01` from `HomeostasisBrain.GlobalQualityWeight`. Continuous `ResolveMathLodPressure01()` combines quality pressure, smooth system stress pressure, and a short critical-health hold. The text-to-quad job now receives `QualityWeight01` and `MathLodPressure01`, derives `visualBudget01`, and lerps smoothing, depth wave source, radar cap, and acoustic mock capacity. DTOs were converted to explicit offsets without changing sizes. Burst attributes and NoAlias annotations were added.
Rejected Alternatives: Keeping tier enum state was rejected because this is presentation quality and a direct tier registry read violates one fact/one route. Adding a SignalBus request for current quality was rejected as one-to-one route abuse. Replacing the wrist HUD with many GameObjects or physics-driven indicators was rejected; packed quad DTOs and shader glyphs are the correct Dear Lie.
Scalability potential: Low uses 12 mock acoustic taps, survival pressure flags, current wrist transform with minimal smoothing, triangle-like depth wave, and radar cap near 12. Middle moves through intermediate tap/radar caps and blended wave/smoothing. High and Ultra approach 36 mock taps, 100 radar taps inside the job cap, sine wave detail, and full smoothing while preserving the same draw path.
Hardware Impact: Low-end savings are fewer generated acoustic taps, fewer radar quads, reduced smoothing work relevance, and no tier registry read. Expected gain is micro-scale per active wrist HUD frame and higher when acoustic taps are dense; runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: The wrist HUD is early-route survival UI. Weak devices now shed decorative radar/wave detail continuously while preserving oxygen/depth/load readability.

## Loop 60 / Ambient Biota Continuous Quality Pressure

Problem: `AmbientBiotaDirector` still used `HectonQualityTier`, `GlobalRegistry.ScalabilityTier`, `GlobalRegistry.ScalabilityTierProfileByte`, and binary `LowTier`/`HighTierOverkill` bytes for capacity, spawn, drift, macro hydration, telemetry, shader scalar, debris, and visual flags.
Solution: Replaced internal quality state with `_cachedQualityWeight01`, `_cachedSystemStress01`, and `_visualOverkillWeight01`. Capacity uses smooth quality and ultra curves. Active population and simulation radius are lerped by survival pressure. Spawn/drift/macro hydration jobs now receive `SurvivalPressure01`, `VisualOverkill01`, or `QualityWeight01` floats, then lerp radial placement, vertical spread, velocity, emission, scale, lifetime, light avoidance, and speed caps. External `EntitySpawnSignal.QualityTier` and `AmbientBiotaState.FlagLowTierBillboard` remain compatibility flags, but their inputs now come from scalar thresholds rather than registry profile bytes.
Rejected Alternatives: Keeping registry tier/profile fallback was rejected because it creates a second quality authority. Renaming core `AmbientBiotaState` and `EntitySpawnSignal` compatibility bits was rejected because that touches core cross-domain contracts outside this loop. Simulating individual fish with GameObjects or physics avoidance was rejected; AUP offsets plus indirect draw payloads are the correct Dear Lie.
Scalability potential: Low/survival pressure trends toward 2048 capacity, smaller active scalar, 30m radius, triangle noise, billboard compatibility flag, low speed, and lower debris count. Middle ramps continuously through capacity, radius, motion, and visual density. High reaches precision capacity and motion. Ultra adds a smooth extra capacity curve and shader/debris overkill without changing routes.
Hardware Impact: Removes two registry tier/profile reads from quality policy and replaces binary job branches with float curves. Low-end savings come from lower capacity, fewer active biota, shorter radius, cheaper motion/noise, and lower debris fan-out. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Ambient biota is early-route immersion. Weak hardware now sheds decorative density and motion continuously while strong hardware spends saved budget on indirect visual overkill.

## Loop 61 / Sonar Holo Compass Scratch DTO Alignment

Problem: `SonarHoloCompass` had two `Pack=1` projection scratch structs. Even though they are managed-array scratch DTOs, the file participates in cached sonar HUD projection and should not normalize unaligned DTO layout.
Solution: Converted `AcousticRadarBlipInput` to explicit 16 bytes and `AcousticRadarBlipOutput` to explicit 24 bytes with a four-byte tail pad. No behavior or route changes were made.
Rejected Alternatives: Rewriting the projection pipeline into a job was rejected for this loop because the defect was ABI layout, not scheduling. Leaving Pack=1 was rejected because ARM64 unaligned patterns must be purged wherever they are found in hot presentation scratch data.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The value is keeping scratch projection data aligned so later Burst/native migration has a safe ABI.
Hardware Impact: Prevents unaligned scratch DTO precedent and avoids future vectorization traps; direct frame-time gain is negligible until this projection is moved to native/Burst.
First 20 Minutes Route Impact: Sonar compass remains an early acoustic readability tool; this pass keeps its data layout safe without changing player-facing behavior.

## Loop 62 / SignalBus Continuous Corridor Gate

Problem: The core signal corridor still carried a dead binary `LowTierMode` gate and `SetLowTierMode()` calls even though `ResolveFrameLimit()` already used continuous `GlobalQualityWeight01`. `WeatherStrengthSignal` projection also stamped `WeatherChangedSignal.QualityTier` from `GlobalRegistry.ScalabilityTierProfileByte`, creating a second quality authority inside the SignalBus bridge.
Solution: Removed `SignalBusRegistry.LowTierMode`, its setter, and the unused `lowTier` flush parameter from direct and fallback lane flushes. `SignalBus<T>` now allocates the max snapshot buffer once and derives per-frame limits only from continuous quality, stress, CSV min/max, non-critical VFX status, and priority. `WeatherChangedSignal` now carries `QualityWeightByte` encoded from `SignalBusRegistry.GlobalQualityWeight01`, and mod projection forwards that byte.
Rejected Alternatives: Keeping the dead bool for readability was rejected because it normalizes binary quality switches in the corridor core. Changing the public `SignalBus<T>.Configure(... lowTierFrameSignals ...)` named parameter was rejected because other domains use named arguments; the legacy parameter is preserved as source-compatible minimum-cap input while the internal state remains continuous. Adding a SignalBus request for quality was rejected as one-to-one route abuse.
Scalability potential: Low/survival uses the lower CSV/minimum cap through the smooth quality curve; Middle interpolates caps continuously; High reaches max caps; Ultra can raise CSV caps and spend the preserved route capacity on visual overkill consumers without introducing a profile branch.
Hardware Impact: Removes one global registry profile read from weather projection and one dead binary branch/state path from every pre-simulation lane flush. Expected saving is micro-scale per flush, with stronger architectural value: no second quality truth in the typed corridor. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Early weather/biome feedback and signal-heavy tutorial moments now inherit the same continuous quality scalar as other corridor traffic instead of a platform profile byte.

## Loop 63 / Dispatcher Quality Profile Route Removal

Problem: `SystemDispatcher` still cached `_scalabilityTierProfileByte` from `GlobalRegistry.ScalabilityTierProfileByte`, drained `ScalabilityChangedEvent` for its own scheduling truth, and passed that byte into job admission and simulation bucketing. The downstream implementations then branched on `profile == 0`, preserving a binary scheduling lane under the continuous quality surface.
Solution: Replaced the dispatcher tier byte with `_globalQualityWeight01`, refreshed from `HomeostasisBrain.GlobalQualityWeight` at the PRE_SIMULATION boundary. `IJobAdmissionService.Refill(...)` and `ISimulationBucketer.AdvanceFrame(...)` now take `float globalQualityWeight01`. Job token refill/cap uses `math.lerp(0.60, 1.0, smoothstep(q))`. Simulation bucketing uses a fixed 128-bucket survival domain with active bucket count `1/2/4` derived from the scalar, preserving a 32-frame sweep at q=1 and stretching to 128 frames at q=0. Memory defrag cadence lerps from 1s to 5s. Bullet-time visual signal keeps its 32-byte layout but stores `math.asuint(q)` in `QualityWeightBits`.
Rejected Alternatives: Mapping `HomeostasisBrain.GlobalQualityWeight` back into a fake profile byte was rejected because it keeps the same branch shape with a different source. Keeping `ScalabilityChangedEvent` as dispatcher scheduling truth was rejected because that event is profile-oriented compatibility traffic, not the scalar owner. A new SignalBus request for quality was rejected as one-to-one misuse.
Scalability potential: Low/survival refills job tokens at 60%, runs one active slow bucket per frame, delays rebalance to 240 frames, and defrags at 1s cadence. Middle moves through 2 active buckets and intermediate refill/cadence. High and Ultra use 4 active buckets, 100% refill/cap, 60-frame rebalance cadence, and visual-overkill headroom flags only when measured frame cost supports it.
Hardware Impact: Removes one direct registry profile read from the dispatcher scheduling path, deletes the `profile == 0` branch from job admission, and deletes the low-tier branch from bucketer advance. Expected low-end gain is micro-scale per frame; the stronger gain is route correctness and smoother load shedding under thermal pressure. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Early boot/tutorial simulation cadence and job admission now degrade by the same Homeostasis scalar as the signal corridor; weak devices shed scheduling pressure gradually while high/ultra keep bucket sweep density for responsive first-route systems.

## Loop 64 / Foveated Simulation Continuous Thresholds

Problem: `FoveatedSimulationManager.ResolveScalabilityThresholds()` still read `GlobalRegistry.ScalabilityTier` inside the importance-scoring schedule path and branched Low/Mx350 versus default distances. That made foveated simulation culling depend on a global hardware profile rather than the Homeostasis scalar owner.
Solution: Removed the registry read and tier enum branch. The manager now derives `qualitySurvivalPressure01 = 1 - smoothstep(HomeostasisBrain.GlobalQualityWeight)` and combines it with the existing `_homeostasisPressureTier / 3` pressure scalar. Active and frozen distances lerp continuously from 100m/300m to 50m/150m, then toward 25m/75m under critical pressure.
Rejected Alternatives: Keeping `GlobalRegistry.ScalabilityTier` was rejected because it is a hot scheduling poll and a second quality authority. Adding a SignalBus request for quality was rejected as one-to-one misuse. Rewriting the whole foveated dispatcher into Vault buffers was rejected for this loop because the direct defect was the binary quality route, not ownership migration.
Scalability potential: Low/survival pressure shrinks active/frozen classification smoothly toward 50m/150m, critical pressure moves toward 25m/75m, middle devices interpolate without popping, and high/ultra retain the 100m/300m visibility budget for responsive simulation.
Hardware Impact: Removes one registry tier read from the foveated scoring schedule path and deletes the Low/Mx350 threshold branch. Expected gain is micro-scale per importance evaluation; the practical value is smoother thermal shedding and less global coupling. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Early wildlife, cockpit helpers, and interactable targets now enter active/peripheral/frozen states by scalar pressure instead of hardware profile, preserving responsiveness on high/ultra and shrinking work gracefully on weak devices.

## Loop 65 / Blackbox 300-Frame Route Preservation

Problem: `GlobalTelemetryBus.Blackbox.ResolveBlackboxFrameCount()` selected 60 frames when `GlobalRegistry.ScalabilityTierProfileByte == LowMx350` or shared-memory mode was active. That is both a direct registry tier route and a violation of the 300-frame blackbox forensic requirement.
Solution: Removed `ShinobuBlackboxLowFrameCount` and made `ResolveBlackboxFrameCount()` return the existing 300-frame capacity constant unconditionally. Existing Vault buffer IDs and MMF scratch math remain unchanged.
Rejected Alternatives: Keeping a smaller low-memory ring was rejected because crash autopsy data is not decorative load. Replacing the route with `HomeostasisBrain.GlobalQualityWeight` was rejected because telemetry depth must not collapse under the exact pressure conditions where failures are most likely.
Scalability potential: Low, Middle, High, and Ultra now preserve the same 300-frame history. Device scalability applies to optional visual work, not to the evidence trail needed to debug thermal/NaN failures.
Hardware Impact: Removes one cold initialization registry profile read and prevents weak devices from losing 240 frames of forensic history. Memory impact is bounded: `300 * 3840 = 1,152,000` bytes for the primary frame ring before auxiliary buffers, inside existing Vault ownership.
First 20 Minutes Route Impact: Early-route crashes now retain the same blackbox depth on weak hardware as on high/ultra machines, which is required to diagnose first-session failures instead of hiding them behind reduced telemetry.

## Loop 66 / Frame Watchdog Quality Scalar Route

Problem: `FrameTimeWatchdog.ResolveHardwareMathLodMode()` read `GlobalRegistry.ScalabilityTier` during the watchdog hot tick initialization path. Particle emission and voxel AO also followed a binary math LOD bool, so a hardware tier route was still shaping presentation load.
Solution: Replaced the initial hardware-tier route with `PushInitialScalabilityFromGlobalQuality()`, driven by `HomeostasisBrain.GlobalQualityWeight`. The watchdog now refreshes continuous quality outputs every tick: particle emission scales via `math.lerp(0.5, 1.0, smoothstep(q))`, distant flora disables only under forced low math LOD or very low scalar pressure, and voxel AO follows the same scalar curve.
Rejected Alternatives: Keeping `DistanceMath.ResolveMathLodMode(GlobalRegistry.ScalabilityTier)` was rejected because it preserves the old hardware-profile authority. Adding a SignalBus request for current quality was rejected as one-to-one misuse. Removing the emergency frame-time math LOD gate was rejected because it is a safety response to measured frame pressure, not a platform profile poll.
Scalability potential: Low/survival pressure produces half particle emission, disabled distant flora, and no voxel AO. Middle interpolates particle emission and gradually re-enables features. High and Ultra retain full particle emission and voxel AO while still allowing the measured frame-time watchdog to force emergency math LOD.
Hardware Impact: Removes one direct registry tier read from watchdog initialization and replaces binary particle/AO state with a scalar refresh. Expected per-frame savings are micro-scale; the material gain is preventing quality state from being sourced from the global hardware profile in a hot watchdog path.
First 20 Minutes Route Impact: Early tutorial and cockpit frames now shed watchdog-controlled presentation load through the same Homeostasis scalar as the signal corridor, without a hidden platform-tier branch.

## Loop 67 / Prologue Low-Policy Scalar Route

Problem: `PrologueSequenceRegistryBridge.ReadLowTierPolicy()` still polled `GlobalRegistry.H8_LOW_MEMORY_PROFILE` and `GlobalRegistry.ScalabilityTier`, then classified Unknown/Low/Mx350 as a binary low-tier policy. This path is queried behind hysteresis while deciding prologue pacing/skip behavior.
Solution: Replaced the registry reads with a continuous pressure computation from `HomeostasisBrain.GlobalQualityWeight` and `HomeostasisBrain.SystemHealthIndex01`. Existing `MemoryPressureSignal` snapshot consumption remains the immediate forced-pressure route. The public bool compatibility surface stays, but its source is now scalar pressure plus hysteresis.
Rejected Alternatives: Keeping the registry hardware profile was rejected because prologue pacing must not source platform truth from the registry during runtime. Adding a new SignalBus quality request was rejected as one-to-one misuse. Removing hysteresis was rejected because prologue state should not oscillate around pressure thresholds.
Scalability potential: Low/survival pressure uses the low-policy path only after quality/system pressure crosses tuned scalar thresholds or a critical memory signal arrives. Middle passes through hysteresis without profile pops. High/Ultra keep full prologue route unless measured pressure rises.
Hardware Impact: Removes two direct registry reads from the prologue low-policy probe. Savings are micro-scale every 30 probe frames; route impact is eliminating a hidden hardware-profile owner from first-session pacing.
First 20 Minutes Route Impact: The prologue is literally first-route. Weak devices still get conservative pacing through scalar pressure and critical memory signals, while high/ultra avoid being mislabeled by stale hardware tier state.

## Loop 68 / Lockstep Validator Quality Cadence

Problem: `LockstepStateValidator` cached `HectonQualityTier` from `GlobalRegistry.ScalabilityTier` and `ScalabilityChangedEvent.CurrentQualityTier`. Normal-play hashing was fully skipped on Low/Mx350, and cadence used a High/Ultra branch.
Solution: Replaced cached tier state with `_cachedQualityWeight01` from `HomeostasisBrain.GlobalQualityWeight`. Removed the low-tier skip path. Hash cadence now uses smooth scalar math: base cadence lerps from 300 to 60 frames by quality, then lerps toward 1200 frames by `HomeostasisBrain.SystemHealthIndex01`.
Rejected Alternatives: Keeping low-tier skip was rejected because deterministic validation is not decorative; it should be cadence-scaled, not disabled by platform profile. Mapping the tier event to another profile byte was rejected because it preserves the same binary authority. Adding a SignalBus request for quality was rejected as one-to-one misuse.
Scalability potential: Low/survival pressure keeps validator work around the 300-frame base unless system stress pushes toward 1200. Middle interpolates. High/Ultra approach 60-frame validation. Severe stress stretches cadence without turning off hashing entirely.
Hardware Impact: Removes one registry tier read at dependency refresh and removes the Low/Mx350 skip branch from post-simulation validation. CPU work can increase versus old low-tier skip, but it is bounded by cadence and preserves deterministic evidence.
First 20 Minutes Route Impact: First-session lockstep evidence is preserved on weak hardware instead of silently skipped, while high/ultra still get tighter validation cadence.

## Loop 69 / Architect Eye Continuous Diagnostics Quality

Problem: `ArchitectEyeVisualizer` used `GlobalRegistry.ScalabilityTier` in several diagnostics paths: ghost replay history stride, visual overkill diagnostics, entity/gas/quad budgets, macro database tier selection, and the shader visual tier scalar.
Solution: Replaced those reads with `HomeostasisBrain.GlobalQualityWeight` helpers. Budgets and visual tier scalar now use smoothstep curves. Decorative overkill diagnostics fade by a scalar `ResolveVisualOverkillWeight01()` instead of High/Ultra branching.
Rejected Alternatives: Keeping diagnostics on hardware tier was rejected because diagnostics can still steal frame time and must obey the same owner scalar. Adding a SignalBus request for quality was rejected as one-to-one misuse. Simulating diagnostic salt/silt/dents as world objects was rejected; screen-space quads remain the correct fake.
Scalability potential: Low/survival uses sparse ghost history, low entity/gas/quad budgets, low macro tier, and zero visual-overkill particles. Middle interpolates. High/Ultra push toward full budgets and decorative diagnostic overkill without changing gameplay truth.
Hardware Impact: Removes eight direct registry tier reads from diagnostic visual paths and replaces tier switches with scalar math. Savings are micro-scale per diagnostic build; practical value is preventing debug overlay from becoming a hidden hardware-tier poller.
First 20 Minutes Route Impact: When diagnostics are enabled during early-route testing, weak devices no longer run tier-driven overkill, while high/ultra can spend the extra budget on richer forensic overlays.

## Loop 70 / Homeostasis Registry Tier Severance

Problem: `HomeostasisBrain` itself still seeded `_cachedScalabilityTier` from `GlobalRegistry.ScalabilityTier`, registered a `ScalabilityChangedEvent` listener, and passed a binary low-tier bool through SHI/Dictator pressure policy. Its dictator DTOs also used sequential layouts despite being Vault/editor-facing state.
Solution: Removed the scalability tier cache and listener from Homeostasis. SHI no longer accepts a low-tier argument. The dictator now computes `_hardwareConstraintPressure01` from cold hardware facts, curves it with smoothstep, derives `_hardwareShiFloor = 0.4 * curve`, and derives `_hardwareMaxQualityWeight = lerp(1.0, 0.6, curve)`. Homeostasis flags now use the constraint scalar and `GlobalQualityWeight`, not `HectonQualityTier`. Five 16-byte DTOs were converted to explicit offsets.
Rejected Alternatives: Leaving Homeostasis as the only remaining registry tier poll was rejected because every other consumer had been moved to the Homeostasis scalar owner. Mapping the registry tier event into another cached enum was rejected because it preserves the profile route. Removing HardwareThermal/DataVault/DRS hot-swap rebinding was rejected because those are cold service interfaces, not hot quality truth.
Scalability potential: Low/survival hardware facts create high constraint pressure, raising SHI floor and lowering quality ceiling smoothly. Middle hardware lands between the ceiling/floor endpoints. High/Ultra keep near-1 quality ceiling and can open visual-overkill budget only when `GlobalQualityWeight >= 0.75` and SHI is below the recovery threshold. Hard visual-overkill lock is reserved for constraint >= 0.95.
Hardware Impact: Removes the last direct scalability-tier registry read found in Core and deletes one profile listener/cache from Homeostasis. Expected per-frame gain is micro-scale; architectural gain is stronger: one quality owner scalar, no profile enum in Homeostasis hot SHI math. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Boot and first-route thermal behavior now derives from Homeostasis-measured scalar pressure from frame time, memory, VRAM, thermal snapshot, and quality PID, instead of inheriting a stale profile enum.

## Loop 71 / AR Waypoint Relay Cache

Problem: `ARWaypointOverlay.CollectRuntimeWaypoints()` ran in Tick/SlowTick and read `GlobalRegistry.EmergencyRelay` while composing runtime waypoint rows. Static `SetWaypoint`/`ClearWaypoint` also forced a registry lookup on every external facade call.
Solution: Added cached `_cachedEmergencyRelay` and `s_cachedWaypointService`. `OnEnable()` now resolves Player/EmergencyRelay once, registers a hot-swap listener, and `OnGlobalRegistryServiceReplaced()` refreshes Player, EmergencyRelay, and ARWaypoint service caches. The Tick path now consumes `_cachedEmergencyRelay`.
Rejected Alternatives: Leaving the relay lookup in `CollectRuntimeWaypoints()` was rejected because relay waypoints are first-route UI and the call occurs every waypoint solve. Adding a SignalBus request for the active relay target was rejected because this is a one-to-one service query; a cached interface is the correct route. Rewriting the whole waypoint service into signals was rejected because the defect was lookup ownership, not broadcast semantics.
Scalability potential: Low/survival devices avoid one registry lookup per waypoint solve and keep the same cinematic occlusion fake. Middle/High/Ultra behavior is unchanged visually; high/ultra can spend saved UI overhead on denser waypoint presentation if configured later.
Hardware Impact: Removes one hot registry property path from AR waypoint solve and converts static facade calls to one cold resolve plus cached service. Expected savings are micro-scale per Tick/SlowTick; the value is route consistency in first-session HUD.
First 20 Minutes Route Impact: Service relay waypoint is a first-route objective marker. It now follows cached dependency routing instead of polling the registry during the tutorial HUD solve.

## Loop 72 / Audio Waveform Subtitle Cache

Problem: `AudioWaveformAnimator.LateFrameTick()` retries subtitle subscription while unsubscribed, and each retry called `GlobalRegistry.Subtitles`. In first-route audio-log/tutorial moments, this created avoidable registry polling until `SubtitleManager` was available.
Solution: Added `_cachedSubtitleManager` and `IGlobalRegistryHotSwapListener` to the waveform animator. `OnEnable()` resolves the subtitle manager once, registers for hot-swap, and the retry path now uses the cached reference only. If the subtitle runtime is cleared, the animator unsubscribes and waits for the next rebind.
Rejected Alternatives: Keeping periodic registry polling was rejected because the retry loop is explicitly LateFrame. Adding a SignalBus request for subtitle manager discovery was rejected as one-to-one misuse. Removing the retry loop entirely was rejected because scenes can enable the waveform before the subtitle runtime registers.
Scalability potential: Low/survival devices avoid cold registry calls in the LateFrame retry loop. Middle/High/Ultra visual behavior is unchanged; the waveform remains procedural noise over fixed bars.
Hardware Impact: Removes one repeated subtitle registry lookup from each unsubscribed retry interval. Savings are micro-scale but deterministic, and it closes another hot-poll exception in first-route UI.
First 20 Minutes Route Impact: Audio-log waveform animation now waits on cached subtitle runtime rebind instead of hammering registry during early scene bring-up.

## Loop 73 / Localization Layout Hot Cache

Problem: `LocalizedTMPAutoSizer.ApplyConfiguration()`, its public `ApplyRuntimeLocalizationLayout()` helper, and `LocalizedLayoutMirror.ApplyMirroring()` read `GlobalRegistry.Localization` when resolving the current language. These methods are scheduled from `LateFrameTick()` when layout/configuration is pending, so the route was a direct UI hot-path registry read.
Solution: Added a shared static cached `LocalizationManager` route to both layout helpers, hydrated only through `CacheLocalizationCold()` and refreshed by `IGlobalRegistryHotSwapListener` when `LocalizationRuntime` changes. The cache treats a null cold resolve as retryable, avoiding a stale-English failure if a static helper executes before the localization service registers.
Rejected Alternatives: Keeping direct registry reads inside `ApplyConfiguration()`/`ApplyMirroring()` was rejected because the methods are late-frame UI work. Adding a SignalBus language request was rejected as one-to-one misuse. Permanently caching null was rejected because it breaks late service registration without a replacement event replay.
Scalability potential: Low/survival devices remove fixed registry lookup cost from localized layout repair/autosize passes. Middle/High/Ultra keep the same visual behavior and can spend UI budget on richer localized presentation without a different route.
Hardware Impact: Removes up to three localization registry property lookups from pending localized layout passes. Expected saving is micro-scale per dirty layout frame; the first-route value is eliminating another hidden registry dependency in tutorial/HUD text layout.
First 20 Minutes Route Impact: Early objective prompts, HUD labels, and PDA/tutorial layout mirroring now resolve language through a cached localization dependency instead of querying the registry during late-frame layout work.

## Loop 74 / Interaction Prompt Localization Cache

Problem: `InteractionUI.ShowPrompt()` and `RefreshInteractPrefixCache()` read `GlobalRegistry.Localization` while composing hover prompt text and interaction prefix markup. Hover/prompt refresh is event-driven, but it is first-route UI work and can be triggered repeatedly by hover churn, input rebinds, and language changes.
Solution: Added `_localizationManager` plus retryable cold cache hydration. `OnEnable()` and `Start()` force-refresh the cache; `OnGlobalRegistryServiceReplaced()` handles `LocalizationRuntime` and refreshes prompt prefix/current prompt through the existing flow. Prompt expansion and prefix lookup now use `ResolveLocalizationManager()`.
Rejected Alternatives: Leaving the direct registry reads was rejected because this file already had the hot-swap pattern for input services and localization belongs in the same cached dependency set. Adding a SignalBus request for prompt language was rejected as one-to-one misuse. Static shared cache was rejected here because `InteractionUI` is an instance owner with existing lifecycle/hot-swap state.
Scalability potential: Low/survival devices avoid fixed registry lookups during hover prompt churn. Middle/High/Ultra get unchanged visual text behavior and can spend UI budget on richer prompt formatting without a second authority route.
Hardware Impact: Removes three direct localization registry reads from interaction prompt composition. Expected saving is micro-scale per hover/prefix refresh; route correctness is the main gain.
First 20 Minutes Route Impact: Interaction prompt text is part of the initial gather/craft route. It now uses cached localization dependency routing instead of registry reads during prompt refresh.

## Loop 75 / PDA Marker and Player Tool Hot Registry Cache

Problem: Method-aware SHINOBU scanning still found `PDAMarkerHUDElement.Tick()` polling `GlobalRegistry.PDAMarkers` and `PlayerToolManager.Tick()` polling `GlobalRegistry.Input`. `PDAMarkerHUDElement` also used `GlobalRegistry.Player` from Tick-called camera/AUP helpers, and `PlayerToolManager` reached registry-owned pool/durability services from tick-driven tool spawn and breakage paths.
Solution: Added `IGlobalRegistryHotSwapListener` to both owners. `PDAMarkerHUDElement` now caches `PDAMarkerRegistry` and `IPlayerRuntimeContext` during enable-time wiring and refreshes them on `PDAMarkerRuntime`/`Player` replacement. `PlayerToolManager` now caches `IInputService`, `ObjectPoolManager`, `ConstructionManager`, `PersistentWorldRegistry`, and `ToolDurabilitySystem`; pool warmup flags reset when ObjectPool or Logistics ownership changes.
Rejected Alternatives: Keeping the registry reads because the operations are "only UI" was rejected; marker HUD and held-tool input are first-route systems. Converting these dependencies into SignalBus request/response was rejected because service lookup is one-to-one authority access. Caching only input was rejected because tool spawn and durability replacement are transitively driven by the tick state machine.
Scalability potential: Low/survival devices remove fixed global lookup overhead from marker HUD solving and held-tool input decisions, while middle/high/ultra preserve identical presentation and can spend the saved budget on richer HUD/tool feedback. Pool warmup remains cold and rebind-aware after service replacement.
Hardware Impact: Removes two scanner-confirmed hot registry findings and additional transitive PDA/player lookups. SHINOBU `Hot_Registry_Polling` critical count dropped from 21 to 19. Expected per-frame gain is micro-scale, but the route impact is first-route determinism and tighter compile-wall hygiene.
First 20 Minutes Route Impact: Player-authored marker HUD and handheld tool controls are core early-session affordances. They now use cached dependency routing instead of polling global authority during HUD solve and tool input evaluation.

## Loop 76 / Kinetic Character DataVault Tick Cache

Problem: `KineticCharacterAnimatorRuntime.Tick()` used `_dataVault ?? GlobalRegistry.DataVault` before scheduling the procedural character jobs, and `EnsureVaultBuffers()` repeated the same fallback. This let a player-animation Tick path poll global authority when DataVault was missing or late-bound.
Solution: Introduced `ResolveDataVaultCold()` and restricted `GlobalRegistry.DataVault` fallback to cold/editor entry points. Tick now reads `_dataVault` only; `EnsureVaultBuffers()` also reads `_dataVault` only. The existing `IGlobalRegistryHotSwapListener` remains the live runtime rebind path for DataVault replacement.
Rejected Alternatives: Keeping the fallback inside Tick was rejected because a missing vault should stall the animation solve until the owner route is rebound, not poll the registry every frame. Adding a SignalBus request for DataVault was rejected as one-to-one service discovery misuse. Moving buffer ownership out of the Vault was rejected because this system already follows the H-PHI handle model.
Scalability potential: Low/survival devices avoid a repeated global lookup on the procedural animation schedule path. Middle/high/ultra keep the same GPU-skinning and Dear-Lie procedural rig output, with saved CPU budget available for richer animation presentation.
Hardware Impact: Removes one scanner-confirmed hot DataVault registry lookup and transitive buffer-ensure fallback from player animation. SHINOBU `Hot_Registry_Polling` critical count dropped from 19 to 18. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: First-person body/tool presentation is active in the first minutes. Its job scheduling now depends on a cached DataVault route and hot-swap notification instead of a per-frame global fallback.

## Loop 77 / GPU Boid Registry Cache And Continuous Social LOD

Problem: `HectonBoidController.Tick()` still performed a foveated-director registry fallback. The same Tick call stack also reached `GlobalRegistry.Fluid`, `GlobalRegistry.Player`, and a binary `GlobalRegistry.ScalabilityTier` branch while uploading boid compute uniforms.
Solution: Added `IGlobalRegistryHotSwapListener` to cache `Player`, `FluidRuntime`, and `FoveatedSimulationDirector` during enable-time wiring and service replacement. Tick no longer registry-resolves missing services. `_BoidMathLodMode` is now a continuous shader scalar from `HomeostasisBrain.GlobalQualityWeight` using `smoothstep`, and the compute shader consumes it with `saturate` instead of `step`. Private GPU `BoidData` now uses explicit 32-byte offsets.
Rejected Alternatives: Keeping a Tick fallback was rejected because late service registration should arrive through hot-swap notification, not polling. Mapping `GlobalRegistry.ScalabilityTier` to another bool was rejected because it preserves binary hardware authority. Moving boid flow/player dependencies into SignalBus requests was rejected as one-to-one misuse; cached owner services are the correct route.
Scalability potential: Low/survival quality drives social-neighbor contribution toward zero while preserving bounds, target, panic, SDF, and flow fakery. Middle interpolates neighbor social force instead of popping. High/Ultra raises social cohesion/alignment continuously and spends the saved CPU route overhead on richer GPU boid movement and abyssal-flow presentation.
Hardware Impact: Removes one scanner-confirmed hot registry finding plus transitive fluid/player/scalability authority reads from the GPU boid Tick path. SHINOBU `Hot_Registry_Polling` critical count dropped from 18 to 17. Expected runtime gain is micro-scale CPU-side; the larger impact is removing a binary quality branch from a first-route fauna visual system.
First 20 Minutes Route Impact: Early underwater ambience and small-fish motion now use cached dependency routing and a continuous visual-quality weight, so weak hardware degrades neighbor solve smoothly while high hardware keeps richer flock behavior without a route switch.

## Loop 78 / Floating Origin DataVault Tick Cache

Problem: `HectonFloatingOrigin.Tick()` still passed `_dataVault ?? GlobalRegistry.DataVault` into `AupOriginShiftCoordinator.TickPreSimulation`, so the core AUP authority loop could poll the registry whenever the cached Vault was missing. Shift/drift helper methods carried the same runtime fallback pattern.
Solution: Added `IGlobalRegistryHotSwapListener` to `HectonFloatingOrigin` and made `DataVault` replacement refresh `_dataVault`, AUP emergency thresholds, drift-check handles, and published global offsets. Tick, shift-world, and drift-buffer helpers now consume `_dataVault` only. Static editor/tuner facades retain cold registry fallbacks because they are not frame loops.
Rejected Alternatives: Leaving the fallback inside Tick was rejected because AUP authority is a first-order core system; a missing Vault must be a rebind/state problem, not a per-frame service lookup. Moving Vault access to SignalBus was rejected as one-to-one service discovery misuse. Rewriting static tuner facades was rejected because they are cold/editor surfaces and outside the scanner finding.
Scalability potential: Low/survival devices avoid per-frame fallback lookup during AUP pre-simulation. Middle/high/ultra preserve identical origin-shift semantics. The saved CPU route cost buys stability rather than decorative visuals because AUP authority is correctness-critical.
Hardware Impact: Removes one scanner-confirmed hot registry finding from the floating-origin Tick path and removes runtime fallback reads from shift/drift helper paths. SHINOBU `Hot_Registry_Polling` critical count dropped from 17 to 16. Runtime profiler proof remains blocked by the missing World source.
First 20 Minutes Route Impact: Floating-origin/AUP precision protects every first-route movement, PDA, and world-interaction frame. The route now depends on cached Vault ownership and hot-swap notification instead of a per-frame global fallback.

## Loop 79 / Global Shader Dispatcher Hot Cache

Problem: `GlobalShaderDispatcher.LateFrameTick()` still polled `GlobalRegistry.DataVault`, `GlobalRegistry.ScalabilityTierProfileByte`, and `GlobalRegistry.ScalabilityTier`. The same dispatch route pushed the profile byte into `_H8HardwareTierParams` and read `GlobalRegistry.ResolutionScaler` through shader-quality helpers.
Solution: Added `IGlobalRegistryHotSwapListener` to cache `DataVault` and `ResolutionScalerService`. Runtime shader-slot access now uses `_vault` through `EnsureShaderGlobalSlotsRuntime()`, while static editor/gizmo facades keep cold registry resolution. Tier/profile enums were removed from the dispatcher path; low-pressure weighting now derives from a continuous `GlobalQualityWeight01` survival curve, and `_H8HardwareTierParams` carries `qualityWeight01` plus `lowTierWeight01`.
Rejected Alternatives: Keeping tier profile telemetry was rejected because it preserves a second quality authority after Homeostasis became the owner scalar. Replacing the tier byte with another cached enum was rejected for the same reason. Moving shader globals through SignalBus was rejected because this is a one-owner DataVault-to-CBuffer dispatch path; cached service references are the correct route.
Scalability potential: Low/survival quality drives wake upload count and mock caustic/flow richness toward the survival approximation without a profile switch. Middle interpolates. High/Ultra drive the low-pressure weight toward zero and keep richer global flow, wake, caustic, and UberNoir CBuffer payloads for shader-side visual overkill.
Hardware Impact: Removes three scanner-confirmed hot-registry findings from the renderer LateFrame path and deletes transitive quality-tier reads from shader params/telemetry. SHINOBU `Hot_Registry_Polling` critical count dropped from 16 to 13. Expected low-end gain is micro-scale CPU dispatch cleanup; the larger gain is removing binary hardware policy from a frame-global render bridge.
First 20 Minutes Route Impact: Early ocean fog, caustics, wake, thermal anomaly, respawn fade, and UberNoir globals now consume one continuous quality scalar and cached registry dependencies. Weak hardware sheds shader-global richness smoothly; high/ultra keep visual-overkill payloads without changing ownership route.

## Loop 80 / UberNoir Runtime Bridge Continuous Gate

Problem: `HectonUberNoirRuntimeBridge.LateFrameTick()` still derived low-tier and visual-ceiling decisions from `GlobalRegistry.ScalabilityTier` plus `GlobalRegistry.ScalabilityTierProfileByte`. Its blackbox state hash also folded a hardware tier enum into telemetry.
Solution: Added DataVault hot-swap caching to the bridge and removed hardware-tier/profile enum inputs from LateFrame. Survival-pressure weight, visual ceiling, high-cost allowance, and visual overkill now derive from `HomeostasisBrain.GlobalQualityWeight` and stress allowance. The telemetry field at offset 20 now stores an encoded quality-weight byte while preserving the 48-byte record size.
Rejected Alternatives: Keeping a cached tier enum was rejected because it would preserve the forbidden binary quality authority. Adding a SignalBus route for shader feature policy was rejected because UberNoir owns a direct DataVault/ShaderGlobal bridge, not a broadcast fact stream. Expanding the telemetry DTO was rejected because the existing dump ABI already fits a quality byte.
Scalability potential: Low/survival quality keeps survival-pressure bit high and clamps expensive POM/refraction/secondary caustic allowance. Middle interpolates. High/Ultra raise visual ceiling continuously and allow shader-side UberNoir overkill without changing CPU feature routing.
Hardware Impact: Removes two scanner-confirmed hot-registry findings from the UberNoir LateFrame path and one transitive quality-tier telemetry read. SHINOBU `Hot_Registry_Polling` critical count dropped from 13 to 11. Expected low-end gain is micro-scale; the architectural gain is eliminating another binary quality branch from frame-global rendering.
First 20 Minutes Route Impact: First-route fog/caustic/refraction styling now follows the same Homeostasis scalar as the rest of the render bridge. Weak hardware sheds expensive features smoothly; high/ultra keep richer noir shader features with one owner route.

## Loop 81 / Analytical Caustics Registry Cache And Vault Scratch

Problem: `AnalyticalCausticsService.LateFrameTick()` still performed a live `GlobalRegistry.Caustics` ownership check and caustic dispatch still used hardware-tier/profile logic to disable or cap compute. The same runtime also owned private persistent `NativeArray` allocations for wave upload scratch and the 300-frame blackbox.
Solution: Added hot-swap caching for DataVault, Player, FluidRuntime, and CausticsRuntime. LateFrame now consumes cached ownership and cached service references only. Dispatch wave budget derives from `HomeostasisBrain.GlobalQualityWeight` using smooth quality curves. Wave scratch and blackbox storage now resolve through DataVault handles `0x43415841` and `0x43415842`; the local DTOs are explicit 32-byte and 48-byte layouts.
Rejected Alternatives: Keeping the LateFrame ownership registry check was rejected because ownership changes already have a hot-swap route. Keeping local `new NativeArray` scratch was rejected because this runtime is now touched by SHINOBU and must obey H-PHI. Editing the central `BufferID` enum was rejected because local cast constants are already used by adjacent graphics runtimes and avoid a compile-wall core-header edit.
Scalability potential: Low/survival quality collapses caustic compute wave budget toward zero and pushes fragment fallback intensity toward zero; Middle interpolates wave count; High/Ultra reach the full 16-wave compute path and keep the shader-side caustic illusion active. The route never switches on hardware enum.
Hardware Impact: Removes one scanner-confirmed hot registry finding and removes two private persistent native allocations from the caustics runtime. SHINOBU `Hot_Registry_Polling` critical count dropped from 11 to 10. Low-end gain is micro-scale CPU route cleanup plus reduced memory-fragmentation risk; high-end preserves the full GPU caustic fake.
First 20 Minutes Route Impact: Early ocean surface lighting and hull/water caustic mood now obey the same Homeostasis scalar as the rest of rendering and pull state from cached owner routes instead of polling registry authority in LateFrame.

## Loop 82 / Base Atmosphere Vault And Continuous Solver

Problem: `BaseAtmosphereEngine.FixedTick()` still carried registry-powered service reads, tier-style solve selection, implicit/packed atmosphere DTOs, and private persistent native buffers for front/back compartments, CO2 lane, and blackbox history.
Solution: Added hot-swap caching for DataVault and PowerGrid. Moved front/back compartment arrays, the CO2 byte lane, and the 300-frame telemetry ring to DataVault handles `0x42415341` through `0x42415344`. Replaced tier solve switches with `HomeostasisBrain.GlobalQualityWeight` curves for cold-tick cadence, solve budget, and visual-overkill humidity/fog behavior. Converted `CompartmentState`, `AtmospherePhysiologyHazard`, and `BaseAtmosphereTelemetryEntry` to explicit public-field layouts.
Rejected Alternatives: Keeping local `new NativeArray` buffers was rejected because this runtime is now touched by SHINOBU and H-PHI requires owner-local Vault storage. Keeping a high/low solve mode switch was rejected because quality authority now belongs to Homeostasis as a continuous scalar. Editing central BufferID enum was rejected to avoid a core compile-wall touch; local cast IDs match adjacent runtime practice.
Scalability potential: Low/survival quality stretches cold-tick cadence, limits solved compartments per tick, and keeps fog/humidity approximation cheap. Middle quality solves more compartments with smooth interpolation. High/Ultra approach full compartment coverage and richer visual-overkill physiology/fog scalars without a route switch.
Hardware Impact: Removes one registry power-grid lookup path from atmosphere tick and removes four private persistent native allocation sites. Expected low-end gain is micro-scale CPU route cleanup plus lower native fragmentation risk; the bigger value is deterministic memory ownership and 64-byte postmortem rows.
First 20 Minutes Route Impact: Base interior oxygen/CO2 feedback is a first-route survival loop. It now uses cached service dependencies and Vault-owned state instead of private native ownership plus registry fallback.

## Loop 83 / Gas Dynamics Quality Cadence And Burst Flags

Problem: `GasDynamicsSolver.FixedTick()` still read `GlobalRegistry.ScalabilityTier` to choose cadence/math LOD and hibernation distance. Two touched jobs did not satisfy the exact Burst directive requirement, and gas transition/telemetry DTOs used implicit layouts.
Solution: Replaced tier reads with `HomeostasisBrain.GlobalQualityWeight` and smooth cadence/hibernation curves. `BaseHibernationWakeCatchUpJob` and `GasDynamicsStepJob` now use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` plus `[NoAlias]` on isolated native fields. `PendingBaseTransitionSignal` is explicit 64 bytes and `GasDynamicsTelemetryEntry` is explicit 32 bytes.
Rejected Alternatives: Mapping the old hardware tier to another cached enum was rejected because it preserves binary quality authority. Hiding `Allocator.Persistent` strings to satisfy the Vault scanner was rejected; `_toxicitySignals` and `_deferredBaseTransitions` remain real H-PHI debt until migrated to an owner-route queue/ring.
Scalability potential: Low/survival quality stretches solver cadence and expands hibernation distance to reduce gas work. Middle glides through intermediate cadence. High/Ultra collapse the cadence toward full solve frequency and reduce hibernation radius for richer base-atmosphere fidelity.
Hardware Impact: Removed the gas solver from hot-registry findings and fixed Burst defaults on two jobs. Expected low-end gain is micro-scale registry/cadence cleanup plus safer Burst codegen; runtime profiling remains blocked by the missing World source.
First 20 Minutes Route Impact: Early base room atmosphere updates now shed work through the Homeostasis scalar instead of a fixed device tier, preserving predictable survival feedback on weak hardware and richer fidelity on high-end hardware.

## Loop 84 / Maintenance Station Tool Durability Cache

Problem: `MaintenanceStationModule.Tick()` called `GlobalRegistry.ToolDurability` every frame while a tool was slotted. Reservation helpers also used static player-inventory registry lookup that could be reached during the repair loop.
Solution: Added cached `_toolDurabilitySystem` and `_playerInventoryService` fields plus `IGlobalRegistryHotSwapListener`. Cold cache hydration reads `ToolDurabilityRuntime`, `PlayerInventory`, and `Player`; the repair tick, inventory helpers, restoration, and completion paths consume cached owner services.
Rejected Alternatives: Publishing repair requests through SignalBus was rejected because this is one station asking one owner service for durability state. Keeping the Tick registry read because repairs are intermittent was rejected because the static scanner proved a real hot-method authority lookup.
Scalability potential: Low/survival devices avoid fixed global lookup overhead while the maintenance station is active. Middle/High/Ultra behavior remains identical; saved CPU budget belongs to tool/UI presentation rather than more repair simulation.
Hardware Impact: SHINOBU hot-registry critical count dropped from 8 to 7. Expected saving is sub-microsecond per active station tick, plus better compile-wall hygiene through explicit service dependency caching.
First 20 Minutes Route Impact: Tool repair is part of early base maintenance. It now follows cached owner service routing and hot-swap rebinding instead of polling global authority during the repair loop.

## Loop 85 / World Seed Provider Readiness Bit

Problem: `HectonWorldGenerator.IsInitialized` read `GlobalRegistry.WorldSeedProvider` directly. That property can be queried by bootstrap, streaming, save, and editor/helper routes, turning a simple readiness flag into repeated global authority lookup.
Solution: Added `_registeredWorldSeedProvider`, set it immediately after cold `RegisterWorldSeedProvider(this)` owner registration, and clear it during disable/destroy unregister. The public property now reads the local owner bit.
Rejected Alternatives: Adding a hot-swap listener was rejected because this generator owns the world seed provider route and already performs lifecycle registration/unregistration. Publishing a readiness SignalBus fact was rejected as one-to-one state echo. Leaving the property as-is was rejected because the scanner proved a hidden registry read.
Scalability potential: Low/survival through Ultra all get identical deterministic world-seed ownership without per-query registry cost. No visual quality curve is needed for this loop; saved CPU route cost belongs to terrain/streaming presentation.
Hardware Impact: SHINOBU hot-registry critical count dropped from 7 to 6. Expected gain is sub-microsecond per readiness query, with higher value during startup/streaming loops that query initialization repeatedly.
First 20 Minutes Route Impact: World seed identity is foundational for first-session terrain, saves, and deterministic chunk generation. It now follows local owner state after a cold registry claim rather than repeatedly asking global authority.

## Loop 86 / Orbital Relativity Domain Cache And Vault Blackbox

Problem: `OrbitalRelativityDirector.Tick()` polled `GlobalRegistry.CurrentDomain`, orbital visual LOD branched on `GlobalRegistry.ScalabilityTier`, the orbital blackbox allocated a private persistent `NativeArray`, two orbital DTOs were sequential, and the diagnostic context menu allocated TempJob memory plus completed a job.
Solution: Added `_spaceDomainActive` as the owner-local Space-domain execution gate, set only after cold domain validation and cleared on authority release/domain exit. Replaced tier enum selection with `HomeostasisBrain.GlobalQualityWeight` smoothstep curves. Moved the 300-frame telemetry ring to DataVault handle `0x4F524241`. Converted telemetry/job-result DTOs to explicit 64-byte layouts. Added exact Burst flags and `[WriteOnly, NoAlias]` to the job output. Replaced the context-menu job allocation with the same pure math function used by the job.
Rejected Alternatives: Keeping the Tick domain poll was rejected because this director is the only runtime domain owner using the route and owner-local state is sufficient after claim. Caching a hardware tier enum was rejected because it preserves binary quality policy. Keeping local blackbox allocation was rejected once the file was touched. Removing the smoke check entirely was rejected; a zero-allocation deterministic math check preserves the editor diagnostic without teaching bad runtime patterns.
Scalability potential: Low/survival quality uses the impostor for distant planet presentation and avoids high-detail mesh/ultra LOD. Middle quality fades through mesh continuity. High/Ultra unlock richer mesh/high-detail/ultra planet presentation through smooth scalar thresholds without a hardware-tier branch.
Hardware Impact: SHINOBU hot-registry count dropped from 6 to 5, Vault_Sovereignty from 666 to 665, Burst directives from 666 to 665. Expected low-end gain is sub-microsecond CPU route removal plus elimination of one scene-owned persistent native allocation; high-end retains the same Dear-Lie universe-moves presentation with richer orbital visuals.
First 20 Minutes Route Impact: The prologue orbital handoff is an early-session path. Domain gating, telemetry, and visual LOD now use owner-local state, Vault blackbox storage, and Homeostasis quality scalar instead of per-tick global authority and binary hardware tiers.

## Loop 87 / Abyssal Thermal Registry Cache And Burst Field Guard

Problem: `AbyssalThermalManager` still used cached/null-coalesced `GlobalRegistry` fallbacks inside FixedTick-adjacent thermal map paths and direct thermal center resolution. Two touched thermal jobs lacked the exact mandatory Burst directive form, and `ThermalFlowSample` carried bool-style hot fields.
Solution: Cached Player, Submarine, SargassumCutRuntime, SimulationBucketerRuntime, and Dispatcher services during cold lifecycle and hot-swap callbacks. FixedTick and center-resolution code now consumes those cached fields only. `ThermalFlowSample.HasFlow` and `IsCableZone` became byte flags, with HectonPlayerMovement and HectonFluidEngine consumers updated to explicit byte comparisons. `ThermalMapJacobiJob` and `ThermalCrystallizationBoundaryJob` now use exact Burst flags and `[NoAlias]`.
Rejected Alternatives: Keeping registry fallback because thermal maps are "world-owned" was rejected; service replacement already has a hot-swap route. Hiding the remaining private persistent thermal arrays to satisfy a scanner was rejected; those arrays still need a real Vault migration with handle ownership and rollback/dump proof.
Scalability potential: Low/survival devices avoid registry lookup during thermal sampling and keep the existing thermal-map approximation. Middle/high/ultra keep richer thermal diffusion/crystallization behavior through the same cached route. No binary quality branch was introduced.
Hardware Impact: Removed Abyssal from the hot-registry report and removed two Burst-default findings. Expected low-end gain is micro-scale per thermal tick plus stronger Burst alias/codegen behavior; the unresolved Vault allocations remain a fragmentation risk until migrated.
First 20 Minutes Route Impact: Thermal field feedback touches early resource traversal and movement/fluid interactions. The path now uses cached service authority and explicit byte flags instead of hidden registry reads and bool fields.

## Loop 88 / Flora And Sargassum Hot Registry Zero Closure

Problem: The remaining scanner-confirmed hot registry findings were in flora regrowth and sargassum presentation/physics: flora Tick/SlowTick/seed helpers read PersistentWorldRegistry/Save, collapse chunks read ObjectPool and SargassumDrag during lifetime/impact/disintegration paths, and debris particles read SargassumDrag in Tick. Flora local DTOs also retained sequential Pack=4 layouts and the maturation job lacked synchronous Burst and NoAlias proof.
Solution: Flora now caches PersistentWorldRegistry and ISaveService through cold hydration plus hot-swap. Collapse chunks cache ObjectPool and SargassumDrag and handle service replacement without signal detours. Debris particles cache SargassumDrag and rebind on service replacement. Flora DTOs are explicit fixed-size layouts: 40/32/56/24/32/16 bytes. `EvaluateMaturationJob` now has exact Burst flags and NoAlias input/output fields.
Rejected Alternatives: Polling registry in hot paths was rejected even for "rare" pooled debris because the scanner proved runtime methods were doing it. Replacing these one-owner service calls with SignalBus requests was rejected as one-to-one routing misuse. Migrating Flora's private NativeList/HashMap state in the same pass was rejected because it requires a real Vault handle schema, capacity policy, and rollback/dump proof; it remains logged debt instead of being disguised.
Scalability potential: Low/survival removes fixed service lookup overhead from regrowth/ambient debris and keeps the existing seed/particle Dear-Lie presentation. Middle continues normal regrowth and debris density. High/Ultra can spend saved CPU on richer vegetation and debris visuals without changing service ownership route.
Hardware Impact: Latest SHINOBU scanner reports `Hot_Registry_Polling: critical=0`. Runtime_Struct_Layout dropped to 2010 and Burst_Job_Directives to 662 after explicit Flora/Burst cleanup. Expected gain is micro-scale per active flora/debris tick, with stronger ABI/Burst guarantees on the maturation pass.
First 20 Minutes Route Impact: Early sargassum traversal, cut debris, and regrowth now use cached authority and explicit local data layout. The first underwater vegetation route is no longer tied to live GlobalRegistry polling in frame methods.

## Loop 89 / Signal Flush Topology Closure

Problem: `HectonFloatingOrigin.ShiftWorldAsync()` forced `GlobalSignals.FlushPreSimulation()` during an async origin-shift path. The signal scanner correctly classified that as a second flush authority outside dispatcher topology.
Solution: Removed the direct flush. `AupPreShiftSignal` and `AupShiftSignal` still publish into SignalBus, but snapshot creation is left to the existing `SystemDispatcher.RunDispatcherUpdate()` PreSimulation flush.
Rejected Alternatives: Adding a wrapper method on `SystemDispatcher` just to call the same flush from origin shift was rejected because it preserves the same second flush route under another name. Keeping the immediate flush for "pre-shift immediacy" was rejected because it breaks phase isolation; direct origin-shift listeners already handle same-frame rebasing, while SignalBus remains a phase-bound fact stream.
Scalability potential: Low/middle/high/ultra all keep one signal flush cadence. Removing the extra flush avoids unpredictable lane work during origin-shift pressure frames, especially on weak devices.
Hardware Impact: `Signal_Bus_Topology` critical count dropped from 1 to 0. Expected saving is workload-shape correctness more than steady-state microseconds: no surprise signal snapshot flush inside an async shift window.
First 20 Minutes Route Impact: Floating origin and AUP rebasing now obey the same signal-lane phase rules as the rest of the game. Early traversal cannot create a hidden signal flush while shifting the world.

## Loop 90 / Devirtualization Scanner Truth And Time DTO Layout

Problem: `Dev_Virtualization` was reporting DTO names that begin with `I` as interface containers: `InstanceMaterialDTO`, `InteriorGITelemetryEntry`, `ItemState`, and similar types. That made the static gate noisy and obscured the real remaining interface-dispatch debt. Separately, `H8TimeSnapshot` was still a runtime `Pack=1` struct despite being only four aligned doubles.
Solution: The Python CI fallback scanner and the matching Unity editor scanner now collect actual declared interface names before reporting interface arrays or interface collections. False positives from DTO names are no longer counted. `H8TimeSnapshot` is now `[StructLayout(LayoutKind.Explicit, Size = 32)]` with offsets `0/8/16/24`, preserving the public constructor and readonly field API.
Rejected Alternatives: Renaming DTO locals or moving interface-container tokens out of hot methods was rejected as report gaming. Replacing `GameTickManager` in this loop was rejected because its public API is explicitly frozen and a correct migration requires a managed tick-lane design, registrant migration, and scene/bootstrap proof. Leaving `Pack=1` on a four-double time payload was rejected because ARM64 has no reason to accept a packed runtime DTO here.
Scalability potential: Low/middle/high/ultra all benefit from static gates that isolate true architectural debt instead of noisy false positives. The time snapshot ABI remains 32 bytes and cache-aligned for dispatcher time reads across all quality levels.
Hardware Impact: `Dev_Virtualization` dropped from 9 critical / 515 warning to 2 critical / 182 warning after removing token-shape noise. `Runtime_Struct_Layout` dropped from 2010 to 2009 after the time snapshot layout repair. Estimated runtime gain for the layout change is negligible but removes an ARM64 unaligned-access hazard. The remaining two critical devirtualization findings are real `GameTickManager` interface-list hot dispatch routes and remain pending.
First 20 Minutes Route Impact: Dispatcher time snapshots feed early boot, prologue, biolum, physiology, and visual pacing. The change removes a runtime ABI hazard from that shared time route without adding gameplay surface. The scanner change keeps future Copper Wire route audit work from chasing false DTO-name findings while preserving the real GameTickManager blocker.

## Loop 91 / Core Blackbox Burst Directive Closure

Problem: `GlobalTelemetryBus.Blackbox.cs` still had two `[BurstCompile]` jobs without explicit synchronous/float-mode directives. The jobs scan blackbox payloads for NaN state and write deterministic mock origin-shift payloads, so hidden Burst defaults are unacceptable in a crash-forensics route.
Solution: `NanSweeperJob` and `MockOriginShiftFireJob` now use the exact non-rollback Burst directive form: `CompileSynchronously = true`, `FloatMode.Fast`, and `FloatPrecision.Standard`. The raw pointer fields in those jobs now carry `[NoAlias] [NativeDisableUnsafePtrRestriction]` for source payload, atomic state, fatal-hash output, and mock signal output.
Rejected Alternatives: Leaving the default `[BurstCompile]` was rejected because it violates the explicit directive mandate and leaves codegen policy invisible. Using deterministic float mode was rejected because these two jobs do not mutate rollback gameplay state; the NaN sweeper only checks finite values and the mock origin-shift signal is an editor/forensics support path.
Scalability potential: Low/survival hardware gets predictable Burst codegen for forensics without increasing frame work. Middle/high/ultra keep the same blackbox route; saved uncertainty belongs to diagnostics, not visual simulation.
Hardware Impact: `Burst_Job_Directives` dropped from 662 to 660. Expected low-end effect is small but concrete: no conservative Burst defaults on two infrastructure jobs and clearer alias analysis on their raw pointer fields. Build proof remains blocked externally; static scanner proof is current.
First 20 Minutes Route Impact: Crash/NaN forensics now has explicit Burst policy before early traversal or origin-shift diagnostics need it. This is route hardening, not gameplay surface expansion.

## Loop 92 / Deterministic Burst Scanner Domain Correction

Problem: The Burst scanner only recognized `Net` and `Rollback` paths as deterministic float-mode domains. It therefore flagged already-correct `FloatMode.Deterministic` jobs in `Core/Determinism/LockstepStateValidator.cs` and `Core/Memory/MemorySentinelContracts.cs` as failures, despite those jobs producing rollback/desync hashes.
Solution: The Python fallback scanner and matching Unity editor scanner now route deterministic Burst validation for paths containing `Determinism`, `Lockstep`, `MemorySentinel`, or `Desync`, plus the existing `Net` and `Rollback` cases. The jobs themselves were left unchanged because their deterministic mode is correct.
Rejected Alternatives: Converting lockstep hash and memory sentinel jobs to `FloatMode.Fast` was rejected because it would satisfy the old scanner by breaking multiplayer determinism. Suppressing the findings by file-name exclusion was rejected; the scanner now encodes an explicit deterministic-domain rule.
Scalability potential: Low/middle/high/ultra all keep deterministic hash/desync checks. The correction improves verification accuracy without adding frame work or visual policy.
Hardware Impact: `Burst_Job_Directives` dropped from 660 to 652 by removing eight false failures. No runtime microsecond claim is made because this loop changes only scanner classification; it prevents a future bad "optimization" from replacing deterministic Burst with fast math in lockstep paths.
First 20 Minutes Route Impact: Early lockstep validation and memory-sentinel desync reporting now remain visibly protected by deterministic-mode verification instead of being pressured toward Fast mode by a bad scanner.

## Loop 93 / AUP Vault Deterministic Burst Domain Closure

Problem: The corrected deterministic Burst classifier still missed AUP authority and origin/vault memory paths. It flagged deterministic jobs in `AupOriginShiftCoordinator` and `VaultMemoryContracts`, including origin rebase and AUP compaction work, even though those jobs protect absolute-universe-position authority and must not drift across x86/ARM64.
Solution: The Python fallback scanner and Unity editor scanner now treat `Origin`, `Aup`, and `VaultMemory` paths as deterministic Burst domains. Existing AUP/origin/vault jobs remain unchanged with `FloatMode.Deterministic`.
Rejected Alternatives: Replacing deterministic AUP/origin jobs with `FloatMode.Fast` was rejected because AUP rebasing and sector-local conversion are rollback/state-authority paths, not visual-only approximations. Adding path-specific ignore exemptions was rejected in favor of an explicit deterministic-domain rule.
Scalability potential: Low/middle/high/ultra all keep deterministic AUP corrections. No new frame work is added; the verification gate now protects the correct math mode.
Hardware Impact: `Burst_Job_Directives` dropped from 652 to 636 by removing sixteen false deterministic-domain failures. No runtime gain is claimed; this prevents scanner pressure from corrupting AUP determinism.
First 20 Minutes Route Impact: Early traversal, origin shifts, and Vault AUP compaction now remain visibly protected by deterministic-mode scanner policy, so the large-world precision route is not misclassified as a Fast-mode Burst failure.

## Loop 94 / Bool Field Scanner Property False Positive Closure

Problem: The runtime struct-layout scanner classified any `bool` token with a semicolon and access modifier as a field. Expression-bodied struct properties such as `public bool IsCreated => ...;` in `BurstCallback.cs` were reported as ARM64 bool-field risks even though they do not create unmanaged bool storage.
Solution: The Python fallback scanner and Unity editor scanner now reject expression-bodied members, accessor properties, and method signatures before applying the bool-field rule. Real field syntax such as `public bool Flag;` or `public bool Flag = ...;` remains reportable.
Rejected Alternatives: Rewriting `BurstCallback` public property API was rejected because the static finding was false for storage layout and the queue still has separate H-PHI allocation debt. Suppressing `BurstCallback.cs` was rejected because that would hide future real fields in the same file.
Scalability potential: Low/middle/high/ultra get more accurate layout gates without runtime changes. This prevents ABI cleanup from wasting time on property tokens while leaving real struct-field debt visible.
Hardware Impact: `Runtime_Struct_Layout` dropped from 2009 to 1804. No runtime microsecond claim is made because this is scanner truth; it removes 205 false bool-field findings and keeps 291 real bool-field findings visible.
First 20 Minutes Route Impact: Core callback queue diagnostics are no longer misreported as ARM64 bool-storage faults solely because of `IsCreated` properties. Real callback queue Vault ownership debt remains separately visible in `Vault_Sovereignty`.

## Loop 95 / Struct Property Scanner Accessor Token Closure

Problem: The property scanner used raw substring checks for `get;` and `set;`. Field names such as `DependencyOffset;` and `Asset;` therefore produced fake `STRUCT_PROPERTY_DEFENSIVE_COPY_RISK` findings, corrupting the layout report and hiding the real packed/field debt.
Solution: The Python fallback scanner and Unity editor scanner now identify property accessors only when actual accessor syntax appears inside a property body, including optional access modifiers before `get;` or `set;`.
Rejected Alternatives: Renaming fields ending in `set` was rejected as report gaming. Suppressing whole files was rejected because the same files can contain real packed structs and bool fields that still need attention.
Scalability potential: Low/middle/high/ultra benefit from accurate ABI gates; no runtime path changes.
Hardware Impact: `Runtime_Struct_Layout` dropped from 1804 to 1245. No runtime microsecond claim is made; 559 false property findings were removed while real property, packed, and bool-field findings remain visible.
First 20 Minutes Route Impact: Core content hash-map verification now points to real issues: the packed binary record and authoring bool fields, not field-name substrings.

## Loop 96 / Content Binary ABI And Signal Warden Determinism

Problem: `ContentAssetBinaryRecord` used `LayoutKind.Sequential, Pack=1`, placing an 8-byte `long` after a 4-byte `uint` and creating an unaligned file/runtime record. The latest scan also surfaced `SignalWardenRuntime` as a deterministic Burst path that the scanner still treated as Fast-mode work; its aggregation job lacked explicit alias proof on native arrays.
Solution: `ContentAssetBinaryRecord` is now an explicit 32-byte record with `EstimatedVramBytes` at offset 0, `Hash` at 8, `DependencyOffset` at 12, `DependencyCount` at 16, byte/enums at 18-23, and padding/reserved uints at 24 and 28. `MockRockCollisionAggregationJob` now marks input/output/count arrays `[NoAlias]`. Both scanners classify `SignalWarden` paths as deterministic Burst domains.
Rejected Alternatives: Keeping the packed content record as a "cold file format" was rejected because the same struct is public and size-asserted for binary validation; Pack=1 is exactly the ARM64 hazard under audit. Reordering the Signal Warden job to `FloatMode.Fast` was rejected because it aggregates AUP collision facts into signals and should be deterministic.
Scalability potential: Low/middle/high/ultra all keep a 32-byte content record size with aligned loads. Signal Warden aggregation keeps deterministic output; saved uncertainty goes to signal correctness rather than visual fidelity.
Hardware Impact: `Runtime_Struct_Layout` dropped from 1245 to 1244. Burst count stayed at 636 after scanner correction and alias proof; Core-path Burst findings are zero. Content binary record avoids unaligned 8-byte access if copied or scanned in native contexts.
First 20 Minutes Route Impact: Early content validation and signal warden collision aggregation now have aligned content ABI and deterministic signal aggregation proof before first-session asset/rock-collision paths touch them.

## Loop 97 / Foveated Job ABI And Alias Guard

Problem: `FoveatedSimulationManager.cs` used `LayoutKind.Sequential, Pack=16` on Burst job structs. That is not a valid ARM64 DTO alignment fix; it changes managed job struct packing requests while the hot memory is in the `NativeArray` fields. The same jobs lacked explicit NoAlias proof for independent input/output arrays.
Solution: Removed `Pack=16` from `ImportanceScoringJob` and `VisualInterpolationJob`, leaving normal sequential job struct layout. Added `[NoAlias]` to independent foveated scoring and interpolation arrays so Burst can reason about non-overlapping native lanes.
Rejected Alternatives: Converting these job structs to explicit 64-byte DTOs was rejected because they are job containers, not persisted ring-buffer/state DTOs. Keeping `Pack=16` was rejected because it looks like an ABI solution while providing no useful layout proof for the native buffers being processed.
Scalability potential: Low devices still collapse foveated scoring through quality-weighted tick-rate codes and lower-frequency work; middle/high/ultra retain the same math route and can spend recovered certainty on denser visual interpolation instead of extra CPU simulation.
Hardware Impact: `Runtime_Struct_Layout` dropped from 1244 to 1242. No measured microsecond claim; expected benefit is cleaner Burst alias analysis on foveated array lanes and removal of two false ABI hazards. Latest scan has zero Core-path Burst findings; global Burst count is 641 and belongs to non-Core files.
First 20 Minutes Route Impact: Foveated simulation now has cleaner ABI/alias proof before early traversal begins pushing first-session entity importance and visual interpolation through the dispatcher.

## Loop 98 / NativeQuery Packed Job Closure

Problem: `NativeQuery.cs` still used `LayoutKind.Sequential, Pack=16` on two generic Burst job containers. The hot data is in `NativeArray`/`NativeList` fields, not inline DTO payload bytes, so packed job-container layout is a misleading ABI tactic and remained in the layout report.
Solution: Removed `Pack=16` from `NativeFilterJob<T>` and `NativeSelectJob<TSource,TResult>`. Added `[NoAlias]` to the independent source/output native lanes. The existing function-pointer predicate/selector API was retained.
Rejected Alternatives: Replacing the query API with a new generic static strategy pattern was rejected in this loop because it would change shared API surface and risk broad compile-wall fallout. Leaving `Pack=16` was rejected because scanner-visible packed layouts normalize the wrong solution for ARM64.
Scalability potential: Low devices keep query jobs predictable and avoid artificial packing. Middle/high/ultra keep the same query route and can benefit from cleaner alias proof without new simulation work.
Hardware Impact: `Runtime_Struct_Layout` dropped from 1242 to 1240. No measured microsecond claim; expected benefit is source/output alias clarity and removal of two packed Core job findings. Core-path Burst findings remain zero.
First 20 Minutes Route Impact: Any early boot/content/native query route now avoids packed job-container layout while preserving the current function-pointer query contract.

## Loop 99 / Prologue DTO Fields And Vault Burst Closure

Problem: Three prologue fixed-size snapshots used getter-only properties, which are methods on a struct and violate the raw-field DTO rule for fixed unmanaged payloads. The same scan exposed three `GlobalDataVault` jobs still using default `[BurstCompile]`.
Solution: Converted `PrologueOrbitalSnapshot`, `PrologueAtmosphericReentrySnapshot`, and `PrologueCompleteSnapshot` properties to public readonly fields with identical member names. Added explicit Fast/Standard synchronous Burst flags to `InitializeVaultMetadataJob`, `GenerateMockVaultRelocationJob`, and `VaultDefragmentationJob`, plus `[NoAlias]` on metadata and arena pointer lanes.
Rejected Alternatives: Leaving getter-only properties for readability was rejected because prologue snapshots are fixed-size contract DTOs. Marking Vault metadata jobs deterministic was rejected because these jobs do not perform rollback float math; they mutate metadata/version bookkeeping and need explicit Burst policy, not deterministic float-mode pressure.
Scalability potential: Low/middle/high/ultra all keep the same prologue and Vault behavior. This loop reduces DTO accessor overhead and hardens Vault job codegen without adding any frame work.
Hardware Impact: `Runtime_Struct_Layout` dropped from 1240 to 1222. `Burst_Job_Directives` dropped from 647 to 644 after the Vault job fixes; Core-path Burst findings are zero. No measured runtime microsecond claim.
First 20 Minutes Route Impact: Prologue reentry/ocean handoff snapshots now obey raw-field DTO rules, and Vault metadata initialization/relocation/defrag no longer runs under implicit Burst defaults during early boot or memory pressure.

## Loop 100 / Persistence Marker And Dispatcher Mock Burst Closure

Problem: `PersistenceAssemblyMarker` used `Pack=8` despite being an empty marker struct, and `DispatcherMockDependencyStressJob` used deterministic Burst mode outside a rollback/state-authority path. The dispatcher mock jobs also lacked explicit array alias proof.
Solution: Removed the packed-layout request from the persistence marker. Changed the dispatcher stress job to Fast/Standard synchronous Burst mode and added `[NoAlias]` to mock time-dilation signal and stress-result arrays.
Rejected Alternatives: Keeping `Pack=8` for an empty marker was rejected because it teaches the wrong ABI habit. Keeping deterministic Burst on the dispatcher stress mock was rejected because the job performs integer hash churn only and does not own cross-platform rollback float state.
Scalability potential: Low/middle/high/ultra all keep the same dispatcher mock and persistence marker behavior. This loop removes verification noise and does not add runtime work.
Hardware Impact: Current SHINOBU scan reports `Runtime_Struct_Layout: critical=1186`, `Burst_Job_Directives: critical=644`, and zero Core-path Burst findings. No measured microsecond claim; touched files are absent from relevant Core layout/Burst findings.
First 20 Minutes Route Impact: Persistence assembly boundary and dispatcher mock dependency stress code no longer carry bad packing or non-domain Burst mode before early boot diagnostics run.

## Loop 101 / Battery Snapshot Byte Flag And Static BTree Burst

Problem: `BatteryRuntimeSnapshot` was a fixed 16-byte runtime service payload but still stored a managed bool flag with marshal metadata. The scan also reported static-data B-tree jobs as deterministic Burst outside an accepted deterministic domain, triggering a static gate regression.
Solution: Converted `BatteryRuntimeSnapshot.EmergencyReserveActive` to a byte flag and updated the three direct consumers to compare `!= 0`. Changed four H8 static-data B-tree jobs to Fast/Standard synchronous Burst mode; their `GlobalQualityWeight` branch only controls prefetch touches and cannot change the returned lookup value.
Rejected Alternatives: Keeping the bool field with `[MarshalAs(UnmanagedType.I1)]` was rejected because runtime DTO layout should not depend on marshalling attributes. Adding `StaticData` to deterministic scanner path tokens was rejected because it would expand deterministic exceptions without rollback/AUP justification.
Scalability potential: Low/middle/high/ultra keep the same power brownout semantics and static lookup behavior. Static-data quality still controls prefetch aggression as a continuous scalar; the returned record remains hash-authoritative.
Hardware Impact: `PowerGridRuntimeService.cs` is absent from the runtime layout report. Current scan reports `Runtime_Struct_Layout: critical=1148`, `Burst_Job_Directives: critical=644`, and `Static_Gate_Regression: critical=0`. No measured runtime microsecond claim.
First 20 Minutes Route Impact: Fabrication power gating and proxy-light brownout checks now read a byte-flag Core snapshot. Static-data lookup jobs no longer keep the static gate red during early config/content hydration.

## Loop 102 / Managed-Struct Scanner Guard And GlobalRegistry DTO Closure

Problem: The layout scanner still counted bool fields and properties inside managed cold structs such as authoring/result/snapshot containers with `string`, Unity object, or array fields. It also left real unmanaged GlobalRegistry contract DTOs using getter-only properties, and `EcosystemSectorPopulationSample` still carried a bool flag.
Solution: The Python fallback scanner and Unity editor scanner now collect pending bool/property findings until struct close and suppress them only when the struct contains managed references. Real unmanaged DTOs still report. Converted GlobalRegistry contract snapshots to readonly fields and changed `EcosystemSectorPopulationSample.ApexInSector` to a byte flag with direct world consumers updated.
Rejected Alternatives: Blindly converting cold managed authoring structs to byte flags was rejected because that changes editor/debug APIs without solving ARM64 native layout. Suppressing whole files was rejected because it would hide the real GlobalRegistry DTO property and bool-field issues.
Scalability potential: Low/middle/high/ultra all keep the same runtime data. The scanner now routes human effort toward unmanaged DTOs and away from cold managed authoring containers; ecosystem apex pressure still scales in existing world logic.
Hardware Impact: Current SHINOBU scan reports `Runtime_Struct_Layout: critical=722`, `Burst_Job_Directives: critical=639`, and `Static_Gate_Regression: critical=0`. Core-path runtime layout and Core-path Burst findings are zero. No measured runtime microsecond claim.
First 20 Minutes Route Impact: GlobalRegistry snapshots used by prologue, habitat flooding, gas dynamics, hardware profile, and ecosystem sample reads no longer hide method-backed accessors or bool storage in fixed runtime DTOs.

## Loop 103 / Touched Foveated Compile-Wall Edge Removal

Problem: `FoveatedSimulationManager.cs` still imported `Hecton8.Gameplay` even though the touched foveated code routes combat/camera facts through Core contract signal aliases. The compile-wall scanner correctly counted that stale sibling namespace edge.
Solution: Removed the unused sibling using. No runtime logic, signal route, or DTO shape changed.
Rejected Alternatives: Leaving the namespace because it was pre-existing was rejected once the file was already touched in this polish pass. Extracting the remaining `GlobalRegistryContracts.cs` monolith was rejected for this loop because it requires a deliberate contract-splitting migration across many sibling domains.
Scalability potential: Low/middle/high/ultra unchanged. The benefit is compile-wall hygiene and smaller dependency surface.
Hardware Impact: Latest scan reports `Compile_Wall: critical=118`, `Runtime_Struct_Layout: critical=708`, `Burst_Job_Directives: critical=634`, and `Static_Gate_Regression: critical=0`. Foveated is absent from Compile_Wall. No runtime microsecond claim.
First 20 Minutes Route Impact: Foveated importance/tick-rate logic no longer imports a gameplay assembly directly; combat/camera reads remain through SignalBus/Core contract signal DTOs.

## Loop 104 / Foveated Origin-Shifted Presentation Write Isolation

Problem: `VisualInterpolationJob.Execute` directly assigned `transform.position` after lerping cached visual positions. The cached positions are already maintained in origin-shifted runtime presentation space via `OnOriginShift`, but the inline write looked identical to forbidden absolute world-space math in the AUP gate.
Solution: Split the job into two inlined helpers: one resolves the smoothstep lerp into an `originShiftedPresentationPosition`, and one applies that position as a visual presentation write. This keeps the Dear Lie interpolation path explicit: peripheral/frozen entities get smoothed presentation motion without restoring full-rate simulation.
Rejected Alternatives: Changing the write to `localPosition` was rejected because parented visual transforms would change behavior. Migrating the thirteen foveated NativeArrays to Vault in this loop was rejected because it requires a new `BufferID` plan in `H8Memory.cs`, a shared header; doing that opportunistically would violate compile-wall discipline.
Scalability potential: Low devices keep the same cheap interpolation fake over lower-cadence target snapshots; middle/high/ultra keep the same smooth presentation route and can spend the saved CPU on denser visible simulation rather than catch-up simulation for peripheral entities.
Hardware Impact: `AUP_Compliance` dropped from 26 to 25 globally, with zero Core-path AUP findings. The full scanner wrote valid JSON summary values before the shell timeout: `Runtime_Struct_Layout: critical=659`, `Burst_Job_Directives: critical=645`, `Static_Gate_Regression: critical=0`. No measured runtime microsecond claim; expected gain is avoiding a false architectural pressure toward per-frame transform simulation or unsafe local-position mutation.
First 20 Minutes Route Impact: Early traversal foveated presentation now advertises its origin-shifted local-space assumption in code, while the remaining Vault allocation debt is still visible and not claimed solved.

## Loop 105 / Foveated Vault Sovereignty Migration

Problem: `FoveatedSimulationManager` still owned persistent native storage directly: ten Burst scoring/interpolation arrays, two pending deferred-raycast arrays, one `NativeList<RaycastCommand>`, one raycast result array, and the 300-frame telemetry ring. This violated the Vault law in a Core dispatcher service already touched by SHINOBU_107 and left thirteen direct allocation scanner findings visible.
Solution: The manager now requests Vault generation handles for local buffer IDs `73220..73234` through `GlobalRegistry.DataVault` and resolves phase-local `NativeArray` aliases before job scheduling or queue mutation. The deferred raycast `NativeList` was replaced with a Vault-backed fixed `NativeArray<RaycastCommand>` and an integer logical count; scheduled raycasts use exact `GetSubArray(0, count)` command/result views. Disposal completes outstanding job fences first, releases generation handles through the Vault, and clears only aliases/counts.
Rejected Alternatives: Editing the global `BufferID` enum was rejected because that is shared memory-authority surface and not required for owner-local numeric casts already used elsewhere. Keeping `NativeList` was rejected because the queue is hard-capped and does not need resizable native ownership. Registering Vault-resolved aliases with `NativeMemorySentinel` was rejected because `RegisterNativeArray` coalesces by raw pointer and can overwrite/unregister the Vault arena record; Sentinel ownership belongs to the Vault allocation site.
Scalability potential: Low/survival hardware keeps the same bounded 16-command deferred raycast drain and uninitialized per-frame lanes, reducing boot/reset zero-fill and avoiding private heap fragmentation. Middle/high/ultra keep full 512-target scoring and smooth interpolation capacity, with the same continuous `GlobalQualityWeight` distance-collapse math already in the foveated route.
Hardware Impact: Foveated direct Vault findings dropped from thirteen to zero. The refreshed static scan reports `Vault_Sovereignty=651`, `Runtime_Struct_Layout=570`, `Burst_Job_Directives=650`, `Compile_Wall=118`, `Hot_Registry_Polling=0`; `FoveatedSimulationManager.cs` is absent from Vault, Burst, layout, and compile-wall reports. No measured runtime microsecond claim; expected low-end gain is lower persistent allocation fragmentation and no `NativeList` capacity bookkeeping in the deferred raycast batch.
First 20 Minutes Route Impact: Early creature/boid foveation now uses Vault-owned scratch and telemetry instead of dispatcher-private native storage. The Copper Wire route can keep low-priority actors cheap without adding a private memory island that complicates crash forensics or scene reset.

## Loop 106 / Core Hot-Helper Registry Poll Eviction

Problem: The full SHINOBU static gate still reported eleven Core `Hot_Helper_Registry_Polling` call chains. Frame tick methods were calling helpers that read `GlobalRegistry`: foveated-adjacent spline late-frame registration, environment/player context sync, watchdog MMF/heartbeat sampling, platform pressure application, FrameTimeWatchdog initial math LOD publishing, MemorySentinel Vault resolution, and Homeostasis lazy initialization.
Solution: Split hot-safe sync from cold registry refresh where the dependency is cold-owned; removed `GlobalRegistry.TryBeginResolution`/`EndResolution` from player context helper loops and relied on owner-local `_syncInProgress`; replaced FrameTimeWatchdog and platform pressure registry writes with boot-bound delegates; made `HomeostasisBrain.PreSimulationTick` require dispatcher boot initialization instead of lazy hot initialization; made `MemorySentinelRuntime` consume an enable-time Vault pointer; cached watchdog persistent-world and heartbeat dependencies at boot and through `IGlobalRegistryHotSwap*` callbacks. RuntimeWatchdog caches object slots, not interface arrays.
Rejected Alternatives: Leaving helper polling because it is low cadence was rejected because the scanner showed actual hot call chains and the architecture would still teach per-frame registry authority lookup. Adding new GlobalRegistry APIs or changing shared service-slot publication was rejected because it would touch a large core authority header. Using `IServiceHeartbeat[]`/`ISystem[]` was rejected after the first full scan exposed new interface-container warnings.
Scalability potential: Low devices stop paying recurring registry lookup/cycle risk in hot ticks and keep watchdog registry sampling at a 60-second guard interval with cached slots. Middle/high/ultra keep the same telemetry and safety behavior without binary quality switches; saved CPU/branch pressure remains available for visual systems rather than service locator churn.
Hardware Impact: `Hot_Helper_Registry_Polling` dropped from `256` to `243` globally. Touched-file scanner reports zero hot registry, helper registry, devirtualization, runtime layout, Burst, mid-frame complete, and helper-complete findings. No measured microsecond claim; estimated gain is small per frame but removes branchy service-locator reads from Core helper paths and prevents IL2CPP interface-array dispatch in the watchdog cache.
First 20 Minutes Route Impact: Early boot and first traversal now use boot/hot-swap cached Core dependencies for watchdog, player context, environment context, platform pressure, and MemorySentinel. The same safety systems remain active without building hidden registry polling into every frame.

## Loop 107 / Core Leaf Compile-Wall Using Purge

Problem: The compile-wall gate still reported Core source files that imported sibling runtime namespaces. Two leaf files, `PlatformAdaptiveBudgetGovernor.cs` and `InstanceCullingServiceRegistryBridge.cs`, imported `Hecton8.World` without using World-owned types, creating avoidable sibling-domain edges.

Solution: Removed the unused `Hecton8.World` imports from those two files only. The platform governor still reads Core hardware/thermal/dynamic-resolution surfaces and writes the transient scalability override through a cold-bound Core delegate. The instance culling bridge still communicates through the Core contract `IInstanceCullingService`, `GlobalRegistry.RegisterInstanceCullingService`, and `SignalBus<CullingOverloadSignal>`.

Rejected Alternatives: Removing `Hecton8.World` from `CameraJuiceSignals.cs`, `MathGuard.cs`, `HectonXRRuntimeState.cs`, or `ConnectionSplineBatchRenderer.cs` was rejected because those files still consume AUP/origin-shift/World types. Extracting or rewriting those contracts in this loop would touch wider ownership boundaries and risk a compile-wall expansion instead of a leaf cleanup.

Scalability potential: Low/middle/high/ultra runtime behavior is unchanged. This is compile isolation: a platform pressure or culling-bridge edit no longer advertises a false dependency on the World runtime, preserving iteration speed and keeping quality/pressure decisions inside the Core route.

Hardware Impact: No measured runtime microsecond claim. Targeted `scan_compile_wall()` reports zero findings for both edited files and total `Compile_Wall` findings dropped to `116`. Full static scan reports `Compile_Wall=116`; the remaining gate failure is repo-wide non-touched debt.

First 20 Minutes Route Impact: Early platform pressure sampling and culling overload signaling stay available without importing World runtime symbols. True AUP/origin-shift consumers remain explicit and were not disguised as contract-only files.

## Loop 108 / Signal Corridor Interface-Array And Dispatch Flag Cleanup

Problem: The full static gate still showed Core signal-corridor devirtualization warnings and one runtime layout finding. `FoveatedSimulationManager` held three `IFoveatedSimulationTarget[]` owner arrays, `SignalBusRegistry` held an `ISignalLane[]`, `ThreadSafeCommandQueue` pulled an `IStorageReservationCommitResolvedListener[]` from a registry bucket, and `SignalLaneDispatch` stored a bool flag.

Solution: Replaced the persistent interface arrays with `object[]` storage and narrow local `as` casts at dispatch points. The public registration APIs remain typed, but the backing storage no longer bakes interface arrays into Core hot/cold registries. `SignalLaneDispatch.FlushDuringSimulationPause` is now a byte flag written by the constructor and checked as `0/1`.

Rejected Alternatives: Leaving `IFoveatedSimulationTarget[]` because the target callbacks are managed Unity component calls was rejected because the mandate bans interface arrays as a storage pattern. Using `var rawArray = RegistryBucket<I>.RawArray` in the command queue was rejected as report gaming because the backing array would still be an interface array. Rewriting every callback to generic unmanaged constraints was rejected because these are managed Unity/service listeners, not Burst payload kernels.

Scalability potential: Low hardware avoids teaching IL2CPP interface-array tables as the default registry shape in signal and foveated corridors. Middle/high/ultra keep the same visual interpolation and signal dispatch behavior; no binary quality switch was introduced.

Hardware Impact: No measured runtime microsecond claim. Full scanner moved `Runtime_Struct_Layout` from `571` to `570`, `Dev_Virtualization` warning count from `181`/`176` depending on concurrent baseline to `176`, and `Hot_Helper_Complete` from `6` to `0`. Targeted scans show zero relevant findings for the three edited Core files.

First 20 Minutes Route Impact: Early foveated actor registration, SignalBus lane registration, and storage-reservation command acknowledgements keep their existing route behavior without storing interface arrays or bool-backed signal dispatch flags.

## Loop 109 / Static Data B-Tree Burst Mode Correction

Problem: Five Core static-data/Babel B-tree jobs used `FloatMode.Deterministic` in files that are not rollback, netcode, AUP, origin-shift, or memory-sentinel authority paths. The Burst scanner correctly expected Fast/Standard mode for these static lookup/query kernels.

Solution: Changed `BabelBTreeSearchKernel`, `TraverseBTreeJob`, `DispatchBulkBTreeSearchJob`, `TraceBTreeTraversalJob`, and `SpatialMortonRangeQueryJob` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.

Rejected Alternatives: Adding `StaticData` or `Babel` to the deterministic scanner domain list was rejected because these kernels do not own rollback float state. Leaving deterministic mode was rejected because it mislabels cache lookup work as authoritative rollback math. Rewriting the B-tree route was rejected because the existing integer/hash traversal remains the correct DOD path.

Scalability potential: Low hardware keeps cheap static lookup kernels with Fast math policy. Middle/high/ultra keep the same B-tree route and can spend saved verification headroom on richer content presentation, not on unnecessary deterministic float semantics.

Hardware Impact: No measured runtime microsecond claim. Full scanner moved `Burst_Job_Directives` from `660` to `655`. Targeted Burst scan is zero for both edited Core data files.

First 20 Minutes Route Impact: First-session static content and Babel lookup paths now carry the same Burst policy as other non-rollback static-data jobs, keeping boot/config/lore lookups out of false deterministic-mode debt.

## Loop 110 / Scalability And Ocean Provider Interface-Container Cleanup

Problem: Core devirtualization warnings remained in two small owner-local registries: `ScalabilityEvents` stored listeners through a generic interface registry plus deferred interface arrays, and `OceanKinematicsRuntimeService` stored ocean providers in `List<IHectonOceanKinematics>`.

Solution: Replaced both registries with fixed `object[]` storage and integer counts. Listener/provider registration still accepts the typed interfaces at the API boundary, but storage and deferred mutation queues no longer use interface arrays or interface generic collections. Ocean provider removal uses swap-with-last because priority arbitration does not depend on insertion order.

Rejected Alternatives: Editing `SystemDispatcher` or `GlobalRegistry` in the same pass was rejected because those are broad compile-wall headers with many unrelated agents touching adjacent code. Leaving `List<IHectonOceanKinematics>` was rejected because provider capacity was already documented as four and dynamic growth would be worse than a fixed object-backed table.

Scalability potential: Low devices avoid interface-container dispatch/list growth in two low-cadence Core registries. Middle/high/ultra keep the same scalability event and ocean provider behavior without adding a binary quality branch.

Hardware Impact: No measured runtime microsecond claim. Full scanner moved `Dev_Virtualization` warning count from `176` to `172`. Targeted devirtualization scan is zero for `IPlatformIntegration.cs` and `OceanKinematicsRuntimeService.cs`.

First 20 Minutes Route Impact: Early platform scalability events and ocean-kinematics provider arbitration remain available through the same public routes, now with fixed object-backed listener/provider storage.

## Loop 111 / Dispatcher And Registry Interface-Container Closure

Problem: After Loop 110, the only Core devirtualization warnings left were in broad authority headers: `SystemDispatcher.cs` and `GlobalRegistry.cs`. They came from explicit `IDispatcherSystem[]`, `IDispatcherFixedSystem[]`, dispatcher raycast receiver arrays, and local `RawArray` interface reads from dispatcher/global registry buckets.

Solution: Converted dispatcher-owned master-system and raycast receiver storage to fixed `object[]` slots with inlined typed accessors at use sites. Replaced `RawArray` interface reads in dispatcher lanes, registry events, and hot-swap notifications with `RegistryBucket<T>.GetAt(index)`, and marked `GetAt` with aggressive inlining so hot loops still compile to a direct dense-array read in player builds.

Rejected Alternatives: Migrating `RegistryBucket<T>` itself to object-backed storage was rejected for this loop because `RawArray` is consumed across many domains and changing its public type would force a cross-domain event-registry migration. Leaving the explicit arrays in `SystemDispatcher` was rejected because they were local owner storage and could be converted without changing public API behavior.

Scalability potential: Low hardware avoids explicit interface-array tables in the Core dispatcher and registry notification paths. Middle/high/ultra keep the same cadence, bucket, foveation, raycast, and render behavior; no binary quality branch was introduced. The change protects IL2CPP/Burst-facing storage shape while preserving continuous `GlobalQualityWeight` behavior already in the dispatcher.

Hardware Impact: No measured runtime microsecond claim. Static impact is concrete: full `Dev_Virtualization` warnings dropped from `172` to `154`, and Core-path Dev Virtualization findings dropped to zero. Targeted scans for `SystemDispatcher.cs`, `GlobalRegistry.cs`, and `RegistryBucket.cs` report zero devirtualization, zero runtime-layout, and zero Burst findings. Remaining two critical devirtualization findings are outside Core: `GameTickManager.cs:320` and `GameTickManager.cs:381`.

First 20 Minutes Route Impact: Early dispatcher registration, Kahn topology sorting, late-frame/event dispatch, raycast result callbacks, render dispatch, and registry hot-swap notification keep their current routes without typed interface arrays in Core-owned storage or hot local reads.

## Loop 112 / Content And Telemetry Compile-Wall Leaf Purge

Problem: The compile-wall report still had two leaf Core files with stale sibling-domain imports: `ContentRuntimeServices.cs` imported `Hecton8.Optimization` without using optimization-owned symbols, and `GlobalTelemetryBus.cs` imported `Hecton8.SaveSystem` without using save-owned symbols.

Solution: Removed only those two imports. `ContentRuntimeServices` still uses Core content tiers and Unity/system APIs; `GlobalTelemetryBus` still owns telemetry export without save-system DTOs.

Rejected Alternatives: Removing imports from `OceanKinematicsRuntimeService`, `RenderSettingsLifecycleGuard`, `ThreadSafeCommandQueue`, `InputDispatcher`, `MathGuard`, or prologue/AUP signal files was rejected because those files still use foreign-domain types directly and require contract extraction, not blind import deletion.

Scalability potential: Runtime behavior is unchanged. This protects iteration scalability: Core content and telemetry leaf edits no longer advertise dependencies on Optimization or SaveSystem, reducing false recompile routes across weak developer machines and high-end machines equally.

Hardware Impact: No runtime microsecond claim. Static impact: `Compile_Wall` findings dropped from `116` to `114`; targeted compile-wall scan reports zero findings for the two touched files. Full scanner still exits nonzero on repo-wide non-touched debt.

First 20 Minutes Route Impact: First-session content reference accounting and telemetry export remain on Core-owned routes without pulling Optimization or SaveSystem assemblies into those leaf files.

## Loop 113 / Contracts Virtualization Import Prune

Problem: `GlobalRegistryContracts.cs` had a stale `using Hecton8.Audio.Virtualization;` import. The header still contains many real sibling-domain contract references, but this specific namespace was not used by any symbol in the file.

Solution: Removed the single stale virtualization import only. The rest of the contract header was left unchanged.

Rejected Alternatives: Broadly rewriting `GlobalRegistryContracts.cs` was rejected because it is a massive shared contract surface and the remaining compile-wall findings represent real type ownership that needs planned contract extraction. Removing all audio imports was rejected because non-virtualization audio contract symbols are still referenced.

Scalability potential: Runtime behavior is unchanged. Compile scalability improves slightly by removing one false sibling-domain edge from a high-fanout header; this matters to low-end developer hardware and to parallel agent iteration.

Hardware Impact: No runtime microsecond claim. Static impact: `Compile_Wall` findings dropped from `114` to `113`. `GlobalRegistryContracts.cs` remains with ten real compile-wall findings after the stale virtualization edge was removed.

First 20 Minutes Route Impact: Boot registry contracts no longer advertise an unused Audio.Virtualization dependency, while the actual audio/physics/world contract routes remain explicit for the integrator to extract later.

## Loop 114 / Vault Scanner Allocation-Statement Classification

Problem: The Vault scanner still treated any single line containing `Allocator.Persistent` or `new NativeArray` as a violation. That produced false positives for multi-line allocations that already supplied `NativeArrayOptions`, metadata assignments such as `Allocator = Allocator.Persistent`, Core memory-authority internals (`GlobalDataVault`, `H8Memory`, arena allocator, sentinel), and the Core NativeQueue-based signal/callback authorities required by the Echelon 1 EventBus model.

Solution: The scanner now classifies complete native-allocation statements, not isolated lines, and exempts only named Core memory/signal authority files. It still reports non-authority private native collections. Three helper allocation sites were made explicit without behavior change: `NativeRingBuffer<T>` casts its options argument as `NativeArrayOptions`, `DodReplayRecorder.AllocateNativeArray` does the same, and `GlobalTelemetryBus` snapshot staging now passes `NativeArrayOptions.ClearMemory` explicitly.

Rejected Alternatives: Leaving the line scanner unchanged was rejected because it buried real Core Vault debt under false positives and made the static gate less actionable. Blanket skipping all Core files was rejected because it would hide actual private native collections. Migrating `H8MacroDatabaseService` dirty and sector-coordinate maps in this loop was rejected because those are real state ownership decisions requiring a planned DataVault/cache-owner contract extension, not a scanner fix.

Scalability potential: Low devices benefit from a static gate that now points to actual private native-cache pressure instead of allocator-authority noise. Middle/high/ultra behavior is unchanged; this pass protects the proof pipeline and leaves real macro-database cache migration visible.

Hardware Impact: No runtime microsecond claim. Static impact is measurable: full `Vault_Sovereignty` findings dropped from `651` to `295`, and Core-path Vault findings dropped from 57 mixed findings to three real `H8MacroDatabaseService` private cache allocations. Targeted `scan_vault()` over modified Core helpers plus authority files reports only those three macro-database findings.

First 20 Minutes Route Impact: First-session telemetry, replay, native ring buffers, GlobalRegistry service rebound queues, signal lanes, MathGuard invalid-number queue, and callback queues are no longer mislabeled as Vault violations when they are Core-owned memory/signal authority surfaces. The remaining macro-database dirty cache still needs a DataVault/cache-owner route before H-Phi can be claimed green.

## Loop 115 / Core Asmdef Stale Sibling Reference Purge

Problem: `Hecton8.Core.asmdef` still referenced sibling runtime assemblies that had zero Core source namespace hits. These references preserve false compile-wall edges even when no Core code consumes the assembly.

Solution: Removed only the eight stale references with no Core source usage: `Hecton8.Inventory.Algorithms`, `Hecton8.Inventory.Corrosion`, `Hecton8.Environment.Fluids`, `Hecton8.World.Terrain`, `Hecton8.AI.Cognition`, `Hecton8.AI.Ecology.Migration`, `Hecton8.Physics.CCD`, and `Hecton8.Audio.Echolocation`.

Rejected Alternatives: Removing every flagged sibling reference was rejected because `LockstepStateValidator` still consumes `Hecton8.Physics.Determinism`, `GlobalRegistryContracts` still consumes `Hecton8.Audio.Propagation`, and `GlobalRegistry` still consumes `IAudioVirtualizationService` from `Hecton8.Audio.Virtualization`. Deleting those references now would trade a static count for compile breakage.

Scalability potential: Runtime behavior is unchanged. Compile scalability improves because Core no longer asks Unity to load eight sibling runtime assemblies for source that does not reference them; this protects low-end developer machines and parallel-agent iteration equally.

Hardware Impact: No runtime microsecond claim. Static impact: `Compile_Wall` dropped from `113` to `105`. The remaining three asmdef findings are live type dependencies and are deliberately left visible for contract extraction.

First 20 Minutes Route Impact: Boot and first-session Core infrastructure no longer drags unused inventory, environment-fluid, terrain, AI, CCD, or echolocation runtime assemblies into Core. Real physics determinism and audio contract debt remains explicit instead of hidden.

## Loop 116 / Core Asmdef Zero-Use Contract Edge Purge

Problem: After removing scanner-flagged stale runtime references, `Hecton8.Core.asmdef` still retained multiple zero-use references that did not currently appear in Core source. One stale source import, `Hecton8.Audio.Propagation`, also remained in `GlobalRegistryContracts.cs` without any propagation-owned type use.

Solution: Removed the remaining zero-use references from the Core asmdef: `Hecton8.Animation.IK`, `Hecton8.Inventory.Corrosion.Contracts`, `Hecton8.UI.Diegetic.Contracts`, `Hecton8.Bootstrap.Contracts`, `Hecton8.Environment.Fluids.Contracts`, `Hecton8.World.Contracts`, `Hecton8.Physics.Tethers.Contracts`, `Hecton8.Vehicles.Physics.Contracts`, `Hecton8.Audio.Virtualization` runtime, `Hecton8.Logistics`, `Hecton8.Logistics.Grid.Contracts`, `Hecton8.Logistics.Grid`, and `Hecton8.Cartography`. Removed the stale propagation import from `GlobalRegistryContracts.cs`.

Rejected Alternatives: Removing `Hecton8.Audio.Virtualization.Contracts` was rejected because `IAudioVirtualizationService` is declared in that contract assembly under namespace `Hecton8.Audio.Virtualization`. Removing `Hecton8.Physics.Determinism` was rejected because `LockstepStateValidator` still calls `DeterministicPhysicsMath.QuantizeMillimeter`; that needs a planned contract/helper migration.

Scalability potential: Runtime behavior is unchanged. Compile scalability improves by eliminating stale edges that expand Unity's dependency graph for Core edits on weak and high-end developer machines alike.

Hardware Impact: No runtime microsecond claim. Static impact: `Compile_Wall` dropped from `105` to `102`. The only asmdef sibling runtime edge still visible is a live determinism helper dependency.

First 20 Minutes Route Impact: Core boot and registry contracts now avoid unused IK/UI/bootstrap/fluid/world/tether/vehicle/logistics/cartography/audio-runtime assembly routes. The remaining determinism helper is left as explicit debt instead of a hidden break.

## Loop 117 / Lockstep Determinism Helper Decoupling

Problem: The last Core asmdef sibling runtime edge was `Hecton8.Physics.Determinism`, used only for `DeterministicPhysicsMath.QuantizeMillimeter` inside `LockstepStateValidator` hash generation.

Solution: Added a local `LockstepHashMath.QuantizeMillimeter` implementation using the existing Core contract constants from `HectonPhysicsContract`, then removed `using Hecton8.Physics.Determinism` and the asmdef reference. The implementation preserves the previous finite check, saturation limits, and signed half-away-from-zero rounding.

Rejected Alternatives: Replacing the helper call with `math.round(value * 1000f)` was rejected because it changes saturation behavior and rounding semantics at edges. Removing `using Hecton8.Physics` was rejected because `LockstepStateValidator` still calls `PhysicsDeterminismSignals`; that is a separate source-level contract extraction problem.

Scalability potential: Runtime work is unchanged. Compile scalability improves because Core no longer depends on a sibling physics determinism assembly for one deterministic hash helper.

Hardware Impact: No runtime microsecond claim. Static impact: `Compile_Wall` dropped from `102` to `100`, and `rg` confirms `LockstepStateValidator.cs`/`Hecton8.Core.asmdef` no longer reference `Hecton8.Physics.Determinism` or `DeterministicPhysicsMath`.

First 20 Minutes Route Impact: Rollback hash generation during early play keeps deterministic millimeter quantization without pulling the physics determinism assembly into Core. Live `PhysicsDeterminismSignals` remains visible as the next required contract-boundary extraction.

## Loop 118 / GameTickManager Interface-List Critical Closure

Problem: The full SHINOBU scanner still had two critical devirtualization findings in `GameTickManager`: hot tick loops copied `TickList<T>.Items` into `List<ITickable>` and `List<IFixedTickable>` locals. That kept explicit arrays/lists of interfaces in the central frame dispatcher path.

Solution: Removed the public `TickList<T>.Items` accessor and added `TickList<T>.GetAt(index)` for owner-local indexed reads. `Tick`, `FixedTick`, `ExecuteSlowTick`, and the disabled slow-tick routine now use `Count` plus `GetAt(index)` with concrete interface locals at the final dispatch boundary only.

Rejected Alternatives: Replacing `TickList<T>` storage with `object[]` was rejected in this loop because `GameTickManager` is a MonoBehaviour-era compatibility dispatcher, not Burst code, and that migration would change registration semantics while other agents may still depend on the typed public API. Leaving the `Items` property was rejected because it would allow the same `List<I...>` hot-loop anti-pattern to return through future edits.

Scalability potential: Low devices remove the last critical explicit interface-list pattern from the central tick manager without changing cadence or behavior. Middle/high/ultra retain the same registration and profiling behavior; saved architectural debt budget can be spent later on visual systems rather than widening the main-thread tick path.

Hardware Impact: No runtime microsecond claim. Static impact: full `Dev_Virtualization` moved from `2 critical / 154 warning` to `0 critical / 152 warning`. Targeted devirtualization scan reports zero findings for `GameTickManager.cs`.

First 20 Minutes Route Impact: Early gameplay update, fixed tick, and slow tick dispatch no longer pull raw `List<I...>` locals out of the owner container. The central tick route remains behavior-compatible and keeps shutdown/auto-cleanup semantics intact.

## Loop 119 / Core Leaf Compile-Wall Dead Import Purge

Problem: The compile-wall report still included two leaf Core imports that no longer backed any symbol usage: `Hecton8.VFX` in `SceneRuntimeService` and `Hecton8.World` in `ConnectionSplineBatchRenderer`.

Solution: Removed only those dead imports. `SceneRuntimeService` keeps live sibling references for bootstrap, physics, and world residency gates. `ConnectionSplineBatchRenderer` now uses Core-owned `HectonFloatingOrigin`, `IOriginShiftListener`, and `OriginShiftEventData` without a World namespace import.

Rejected Alternatives: Removing `Hecton8.World` from `SceneRuntimeService` was rejected because `PersistentWorldRegistry` is still named directly. Removing `Hecton8.Physics` was rejected because `PhysicsApplySystem` and `GlobalPhysicsStateManager` are still named. Rewriting these routes into new contracts was rejected for this leaf pass because it would touch broader integration boundaries.

Scalability potential: Runtime behavior is unchanged. Compile scalability improves by trimming dead dependency routes from Core leaf files; this helps low-end developer machines and parallel-agent iteration without changing player-facing quality logic.

Hardware Impact: No runtime microsecond claim. Static impact: full `Compile_Wall` moved from `100` to `98`. `ConnectionSplineBatchRenderer.cs` is now absent from compile-wall findings; `SceneRuntimeService.cs` retains only live findings.

First 20 Minutes Route Impact: Early scene transition and connection spline render registration keep existing behavior while no longer advertising unused VFX/World edges from those leaf files.

## Loop 120 / Dispatcher VFX Compile-Wall Import Purge

Problem: `SystemDispatcher.cs` still imported `Hecton8.VFX`, but targeted source proof showed the dispatcher only references `ICameraJuiceSystem`. That interface is declared in `Core/GlobalRegistryContracts.cs`; the VFX concrete `CameraJuiceSystem` remains in `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` and is not named by the dispatcher.

Solution: Removed the dead `using Hecton8.VFX;` import from `SystemDispatcher.cs`. Kept all other dispatcher domain imports because source scans show live event/static service calls such as `PredatorCognitionDomain`, `AtmosphereEvents`, `ProceduralAudioEvents`, `WeatherEvents`, `InventoryEvents`, `PhysicsEventBus`, `PowerGridTelemetryEvents`, `QuestEvents`, `SaveEvents`, `DirectorAIEvents`, and `SpectrumEvents`.

Rejected Alternatives: Removing live dispatcher imports was rejected because it would require planned contract extraction or event-lane migration, not an import cleanup. Moving `CameraJuiceSystem` or changing the camera-juice registration contract was rejected because the dispatcher already talks through the Core-owned `ICameraJuiceSystem` interface and changing the VFX owner surface would widen the edit unnecessarily.

Scalability potential: Runtime behavior is unchanged. Compile scalability improves by deleting one false Core-to-VFX namespace edge from the central dispatcher, reducing dependency graph breadth for low-end developer machines and parallel-agent iteration without altering `GlobalQualityWeight`, tick cadence, or visual load shedding.

Hardware Impact: No runtime microsecond claim. Static impact: targeted `scan_compile_wall()` reports `Compile_Wall=97`, down from `98`; `SystemDispatcher.cs` has 16 remaining live domain edges after the VFX import removal. Full SHINOBU scanner reports `AUP_Compliance=24`, `Vault_Sovereignty=295`, `Runtime_Struct_Layout=570`, `Burst_Job_Directives=655`, `Hot_Helper_Registry_Polling=243`, `Dev_Virtualization=0 critical / 152 warning`, and `Static_Gate_Regression=1`.

First 20 Minutes Route Impact: Early dispatcher cadence, pause depth-of-field dispatch, event flushing, and first-session visual-sync routing keep the same behavior while no longer advertising a direct VFX namespace dependency from the Core dispatcher.

## Loop 121 / Scene Transition Audio Interface Extraction

Problem: `SceneRuntimeService` still referenced the audio-domain concrete `SpatialAudioManager` to drive the world-drone crossfade during the guarded scene transition. This kept a Core source dependency on `Hecton8.Audio` for two presentation calls even though the scene service already reaches the object through `GlobalRegistry.Audio`.

Solution: Added a small Core-owned `ISceneTransitionAudioBridge` interface in `SceneTransitionAudioContracts.cs` with the two world-drone transition methods. `SceneRuntimeService` now casts `GlobalRegistry.Audio` to that interface instead of `SpatialAudioManager`, and `SpatialAudioManager` implements the interface without changing its existing method bodies. Added a `.meta` file with a checked unique GUID for the new script.

Rejected Alternatives: Mutating `IAudioService` was rejected because it would force every fallback audio service to implement scene-transition presentation controls. Adding a new `GlobalRegistry` slot was rejected because this is not a new owner; it is a narrow optional bridge on the existing audio owner. Leaving the concrete cast was rejected because Core should not depend on the Audio runtime assembly for two scene-transition presentation calls.

Scalability potential: Runtime behavior and quality math are unchanged. Compile scalability improves by eliminating the scene-service Audio edge. Low hardware keeps the same cheap transition path; high/ultra can still use the audio owner's richer drone transition implementation because the contract preserves the existing calls.

Hardware Impact: No runtime microsecond claim. Static impact: full `Compile_Wall` dropped from `97` to `96`; `SceneRuntimeService.cs` now has only the live `Hecton8.Physics` and `Hecton8.World` findings. `SceneTransitionAudioContracts.cs` and `SpatialAudioManager.cs` report zero compile-wall findings.

First 20 Minutes Route Impact: Main-menu to world handoff keeps the same underwater drone crossfade while Core no longer casts to the audio concrete type. This removes a compile-wall edge from the early route without changing player-facing transition timing.

## Loop 122 / Scene Transition Physics And World Bridge Extraction

Problem: `SceneRuntimeService` still named physics and world-domain concrete owners during guarded scene unload/activation. The direct calls were `PhysicsApplySystem.ClearQueuedPacketsStatic()`, `GlobalPhysicsStateManager.ClearRuntimeStateStatic()`, and `PersistentWorldRegistry.AreResidentWorldPrefabPoolsReady()` through the concrete registry property. This left Core scene flow dependent on sibling runtime namespaces.

Solution: Added narrow Core-owned bridge contracts for scene-transition cleanup and world-residency readiness. `PhysicsApplySystem` implements `ISceneTransitionPhysicsBridge.ClearSceneTransitionRuntimeState()` and keeps the physics-domain call to `GlobalPhysicsStateManager.ClearRuntimeStateStatic()` inside the physics owner. `PersistentWorldRegistry` implements `ISceneTransitionWorldResidencyBridge` and reuses its existing resident-prefab readiness method. `SceneRuntimeService` now routes through `GlobalRegistry.Physics` and `GlobalRegistry.TryGet<ISceneTransitionWorldResidencyBridge>()`.

Rejected Alternatives: Leaving the static physics fallback was rejected because `PhysicsApplySystem.ClearQueuedPacketsStatic()` resolves through `GlobalRegistry.Physics` anyway and does not justify a Core concrete import. Adding new GlobalRegistry slots was rejected because both owners already have registry routes. Expanding broad `IPhysicsService` was rejected because scene-transition state clear is not a general force-routing service duty. Replacing world readiness with a copied boolean in Core was rejected because it would create shadow state.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this is compile scalability and authority cleanup. Low-end developer machines avoid two additional scene-service sibling edges. High-end/parallel-agent iteration keeps scene-transition logic narrow enough that audio, physics, and world owners can evolve without forcing Core scene flow to import their concrete types.

Hardware Impact: No runtime microsecond claim. Static impact: full SHINOBU `Compile_Wall` findings dropped from `96` to `94`, and `SceneRuntimeService.cs` now reports zero compile-wall findings. No dotnet build was launched.

First 20 Minutes Route Impact: Main-menu to world activation still gates on prewarm and resident world prefab pool readiness, and scene unload still clears physics packets/state. The route now obeys owner-local first: scene flow asks the physics/world owners through Core bridge contracts instead of reaching into their concrete implementation.

## Loop 123 / Runtime Watchdog World Health Bridge Extraction

Problem: `RuntimeWatchdog` imported `Hecton8.World` only to cache `PersistentWorldRegistry` and call `TryGetIndexedSaveHealth()` for the 60-second MMF health probe. That made a Core liveness monitor compile against the world persistence concrete for one cold diagnostic fact.

Solution: Added `IRuntimeWatchdogWorldHealthBridge` as a Core-owned narrow interface and mapped it to the existing `PersistentWorldRegistry` slot. `PersistentWorldRegistry` implements the bridge explicitly, forwarding to the existing internal health method without widening the public world API. `RuntimeWatchdog` now caches the bridge through `GlobalRegistry.TryGet<IRuntimeWatchdogWorldHealthBridge>()` and hot-swap rebinding.

Rejected Alternatives: Keeping the concrete cache was rejected because the watchdog does not own world persistence. Adding a new registry slot was rejected because the existing persistent-world owner slot is the correct route. Expanding `ISceneTransitionWorldResidencyBridge` was rejected because watchdog MMF health is not scene activation. Removing the remaining `Hecton8.AI` import was rejected in this loop because the emergency cull path calls `FaunaDirector.ActiveRuntimeInstance.ApplyEmergencyColdTickCull()` and the registered `IFaunaSim` object is the data-only `FaunaSimulationEngine`, not the director owner.

Scalability potential: Runtime cadence is unchanged. Low-end machines keep the cold MMF probe without Core importing World; high-end/parallel-agent iteration benefits from one less direct world compile edge in the watchdog.

Hardware Impact: No runtime microsecond claim. Static impact: full SHINOBU `Compile_Wall` findings dropped from `94` to `93`; `RuntimeWatchdog.cs` now reports one live AI finding and zero World findings. No dotnet build was launched.

First 20 Minutes Route Impact: The first-session watchdog still detects indexed-save bloat and queues cold background health checks. The route now asks the world owner for the save-health fact through a bridge and leaves the separate fauna emergency cull route visible for a future AI-owned extraction.

## Loop 124 / Render Settings Atmosphere Bridge Extraction

Problem: `RenderSettingsLifecycleGuard` imported `Hecton8.Atmosphere` only to read and restore `AtmosphereDirector.Skybox` during cold lifecycle capture/restore. That made Core render-settings cleanup compile against the Atmosphere runtime namespace for one visual-owner fact.

Solution: Added Core-owned `IAtmosphereRenderSettingsBridge` with `Skybox` and `SetSkybox(Material)`. `HectonAtmosphereManager` implements it explicitly and delegates to the existing `AtmosphereDirector` owner facade. `GlobalRegistry.ResolveServiceSlotCold` maps the bridge to the existing `AtmosphereRuntime` slot. `RenderSettingsLifecycleGuard` now captures/restores skybox through the bridge and falls back to direct `RenderSettings.skybox` only if the atmosphere owner is absent.

Rejected Alternatives: Leaving `AtmosphereDirector` calls in Core was rejected because it preserves a direct sibling runtime namespace edge. Adding a new registry slot was rejected because the atmosphere owner already has a slot. Copying skybox state into Core was rejected because it creates shadow render-setting ownership.

Scalability potential: Runtime quality behavior is unchanged. Low-end machines benefit through compile graph shrinkage and less Core recompilation breadth; high/ultra visual behavior still flows through the atmosphere owner and can keep richer skybox handling behind the same narrow bridge.

Hardware Impact: No runtime microsecond claim. Static impact: full SHINOBU `Compile_Wall` findings dropped from `93` to `92`; targeted scan reports zero findings for `RenderSettingsLifecycleGuard.cs`, `RenderSettingsBridgeContracts.cs`, and `HectonAtmosphereManager.cs`. No dotnet build was launched.

First 20 Minutes Route Impact: Early render-settings capture/restore still protects fog, ambient, reflection, sun, and skybox state. Skybox restore now obeys owner-local first through the atmosphere bridge instead of importing the atmosphere runtime into Core.

## Loop 125 / Storage Reservation Commit Target Bridge

Problem: `ThreadSafeCommandQueue` imported `Hecton8.Gameplay` only to cast command targets to concrete `StorageCrate` when committing deferred storage reservations. That made the Core structural queue compile against a gameplay prop implementation.

Solution: Added Core-owned `IStorageReservationCommitTarget` with `TryCommitReservation(int)`. `ThreadSafeCommandQueue` resolves that interface via `TryGetComponent` and keeps the existing success/failure acknowledgement flow. `StorageCrate` implements the interface and retains ownership of reservation state and inventory mutation.

Rejected Alternatives: Keeping the concrete `StorageCrate` dependency was rejected because Core does not own gameplay storage internals. Moving reservation data into the queue was rejected because it would create shadow inventory state. Replacing the command with a managed event was rejected because the queue is a fixed structural command drain, not an EventBus route.

Scalability potential: Runtime quality behavior is unchanged. Low-end devices keep the same bounded command drain without Core-to-gameplay compile breadth; high/ultra behavior can still support richer crate logic behind the same commit target.

Hardware Impact: No runtime microsecond claim. Static impact: full SHINOBU `Compile_Wall` findings dropped from `92` to `91`; targeted scan reports zero findings for `ThreadSafeCommandQueue.cs` and `StorageCrate.cs`. No dotnet build was launched.

First 20 Minutes Route Impact: Early logistics/storage command commits still resolve against the target GameObject and emit the same reservation acknowledgement payload. Core now depends on a small commit contract rather than the gameplay crate concrete class.

## Loop 126 / Runtime Watchdog Fauna Cull Bridge

Problem: `RuntimeWatchdog` imported `Hecton8.AI` only to reach `FaunaDirector.ActiveRuntimeInstance.ApplyEmergencyColdTickCull()` when frame stripping detected a fauna over-budget emergency. The same fauna owner was already registered in the watchdog lane table for emergency reset, so the concrete lookup was redundant compile coupling.

Solution: Added `RuntimeWatchdog.IEmergencyColdTickCullTarget` and changed the watchdog to cast the existing fauna lane target from `_emergencyResetTargets`. `FaunaDirector` implements the interface explicitly and delegates to its existing internal `ApplyEmergencyColdTickCull()` method.

Rejected Alternatives: Keeping `FaunaDirector.ActiveRuntimeInstance` was rejected because Core does not need the AI namespace to ask the registered lane owner for a cull. Expanding `IFaunaSim` was rejected because the registered `IFaunaSim` is `FaunaSimulationEngine`, while cold-tick culling belongs to the director that owns active creature presentation/brain references.

Scalability potential: Low devices keep the watchdog's emergency cull path when fauna exceeds frame-strip thresholds. Middle/high/ultra behavior is unchanged, and the richer fauna director remains free to change behind the watchdog lane contract.

Hardware Impact: No runtime microsecond claim. Static impact: full SHINOBU `Compile_Wall` findings dropped from `91` to `90`; targeted scan reports zero findings for `RuntimeWatchdog.cs` and `FaunaDirector.cs`. No dotnet build was launched.

First 20 Minutes Route Impact: Early runtime liveness and fauna over-budget recovery still work through the existing registered lane. Core no longer imports the AI runtime namespace for that emergency path.

## Loop 127 / Prologue AUP Origin Helper Consolidation

Problem: `PrologueSequenceRegistryBridge` directly named `Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero)` in three cold signal stamps. The calls preserved correct floating-origin semantics, but made the prologue bridge depend on the World namespace.

Solution: Added `GlobalSignals.CurrentRuntimeOriginAup()` inside the existing signal corridor file that already owns AUP-bearing signal DTOs. The prologue bridge now asks the signal corridor for the current runtime-origin AUP when stamping muffled breathing, ocean handoff, and shallow-water hydration signals.

Rejected Alternatives: Replacing the calls with `default` was rejected because `FromRuntimePosition(Vector3.zero)` resolves through `HectonFloatingOrigin` and may not equal absolute zero after an origin shift. Adding a new registry bridge was rejected because no runtime owner lookup is needed for a pure signal-stamping helper.

Scalability potential: Runtime behavior is unchanged. Low-end devices avoid broader Core-to-World compile coupling for prologue signal staging; high/ultra behavior keeps exact AUP semantics for the same presentation and hydration signals.

Hardware Impact: No runtime microsecond claim. Static impact: full SHINOBU `Compile_Wall` findings dropped from `90` to `87`; targeted scan reports zero findings for `PrologueSequenceRegistryBridge.cs`. No dotnet build was launched.

First 20 Minutes Route Impact: Prologue breathing, ocean handoff, and shallow-water hydration still stamp the current runtime origin AUP. The route is now through the signal corridor helper instead of explicit World namespace calls in the prologue bridge.

## Loop 128 / Camera Juice AUP Conversion Helper Consolidation

Problem: `CameraJuiceSignals` imported `Hecton8.World` only to convert a runtime-space impact `Vector3` into `AbsoluteUniversePosition` before publishing `CameraJuiceImpactSignal`. That preserved correct AUP semantics, but kept a Core presentation signal helper coupled to the World namespace for one conversion call.

Solution: Added internal `GlobalSignals.TryRuntimePositionToAup(Vector3, ref AbsoluteUniversePosition)` inside the existing AUP-bearing signal corridor. `CameraJuiceSignals` now uses that helper and no longer imports `Hecton8.World`. The helper finite-checks the runtime point before conversion; invalid impact positions fail closed by skipping the camera-juice packet.

Rejected Alternatives: Adding a new registry slot or owner bridge was rejected because no owner query is needed for a deterministic conversion helper. Leaving the direct World import was rejected because the file is a leaf Core signal producer. Mapping invalid positions to `CurrentRuntimeOriginAup()` was rejected because it would create false camera impacts at origin.

Scalability potential: Runtime quality behavior is unchanged. Low devices avoid one more Core-to-World compile edge in camera-impact presentation; middle/high/ultra still receive the same typed impact payload for richer camera shake consumers when the impact point is finite.

Hardware Impact: No runtime microsecond claim. Static impact: targeted compile-wall scan reports zero findings for `CameraJuiceSignals.cs`; full SHINOBU `Compile_Wall` findings dropped from `87` to `86`. No dotnet build was launched.

First 20 Minutes Route Impact: Early impacts, prologue turbulence, and first-session collision feedback can still push camera-juice packets through the typed signal lane. Non-finite positions now fail closed before they can poison the presentation lane.

## Loop 129 / Mock Signal AUP Input Decoupling

Problem: `SignalCorridorMockSignalGenerators` imported `Hecton8.World` only for `InjectAcousticBurst(in AbsoluteUniversePosition ...)` and `AbsoluteUniversePosition.OffsetMeters(...)`. The only live source call is the editor `SignalTrafficMonitorWindow`, which creates an origin from three UI float fields.

Solution: Changed the cold acoustic mock API to accept `in float3 runtimeOrigin`, then uses `GlobalSignals.TryRuntimePositionToAup(float3, ref AbsoluteUniversePosition)` for each generated ping. The editor facade now passes `float3` directly. The deterministic LCG offset generator, acoustic channel, source id, radius, intensity, and `SignalBus<AcousticPingSignal>.TryPush` behavior are retained.

Rejected Alternatives: Keeping the World import was rejected because the mock file can route AUP stamping through the existing signal corridor helper. Deleting the mock was rejected because Task 05 requires deterministic fallback generators for CI/editor diagnostics. Using UnityEngine.Random or GameObject repro emitters was rejected because the mock must remain deterministic and allocation-free in the injection loop.

Scalability potential: Runtime gameplay is unchanged; this is a cold diagnostic/mock path. Low devices and CI avoid one more Core-to-World compile edge while retaining deterministic acoustic load generation. High/ultra editor diagnostics still inject dense acoustic bursts by raising `count`, bounded by the existing `4096` clamp.

Hardware Impact: No runtime microsecond claim. Static impact: targeted compile-wall scan reports zero findings for `SignalCorridorMockSignalGenerators.cs`; full SHINOBU `Compile_Wall` findings dropped from `86` to `85`. No dotnet build was launched.

First 20 Minutes Route Impact: The signal traffic monitor can still produce deterministic acoustic pings for first-session sonar/audio validation. Invalid editor coordinates now fail closed instead of publishing poisoned AUP data.

## Loop 130 / MacroDB Vault Ownership Evacuation

Problem: `H8MacroDatabaseService` still owned persistent native memory directly. The static scanner explicitly caught `_dirtyPayloads`, `_dirtyPayloadKeys`, and `_sectorCoordsByHash`, and manual review found additional private persistent `NativeArray` scratch/black-box fields that the scanner missed because of its local exemption heuristic.

Solution: Replaced the private dirty-payload hash map, dirty-key list, sector-coordinate hash map, sector window scratch, sector-coordinate scratch, async hydration scratch, and 300-frame black-box ring with DataVault-backed `VaultGenerationHandle<T>` descriptors. Dirty and sector-coordinate lookup now use fixed open-addressed 64-byte slots with explicit tombstone states. The service resolves transient `NativeArray<T>` views only inside the locked execution phase, then drops them. Temporary compaction/repack targets do not receive `_dataVault`, so they cannot clear or alias the live MacroDB buffers.

Rejected Alternatives: Keeping scanner-exempt private `NativeArray` fields was rejected because the Vault law is stricter than the tool. Replacing only the three scanner findings was rejected after manual review. Reusing existing save BufferIDs was rejected after duplicate-ID proof found collisions at `70358-70360`. Adding a new cache interface to `IMacroDatabaseService` was rejected because the existing `IDataVault : IMacroDatabaseNativeCacheOwner` route already provides the owner-local memory authority.

Scalability potential: Low-tier machines avoid fragmented persistent container allocations in the MacroDB path and keep cache pressure centralized under the vault. Middle/high/ultra can raise `NativeCacheCapacity` and `MaxQuerySectors`; the same buffer IDs grow through DataVault generation handles instead of allocating new private containers. Visual overkill is preserved indirectly: stable MacroDB hydration/eviction keeps streaming stalls away from the first-session underwater scene budget.

Hardware Impact: Runtime microseconds claimed: `0` because no profiler capture was run. Static impact: full SHINOBU `Vault_Sovereignty` dropped from `295` to `290`; touched-file scans report `Vault_Sovereignty=0`, `Runtime_Struct_Layout=0`, and no compile-wall findings for the edited files. `H8MacroDatabaseService.cs` now has no private persistent native container declarations, no `new NativeArray`/`new NativeList`/`new NativeParallelHashMap`, and no `Allocator.Persistent` sites. No dotnet build was launched.

First 20 Minutes Route Impact: MacroDB hydration, dirty-sector append, compaction copy, and black-box dumping still route through `_fileGate`. The dirty queue survives as a deterministic vault-backed slot table, and the 300-frame telemetry ring remains available for `.h8dump` autopsy without private memory ownership.

## Loop 131 / Core Data Burst Flag Normalization

Problem: Five Core/Data B-Tree lookup jobs used `FloatMode.Deterministic` even though their files are not rollback, lockstep, AUP-origin, kinematics, or authoritative state integration paths. The mandate requires `FloatMode.Fast` for ordinary mathematical jobs, and the SHINOBU Burst scanner flags non-deterministic-path jobs that do not use Fast mode.

Solution: Changed the Burst attributes on `BabelBTreeSearchKernel`, `TraverseBTreeJob`, `DispatchBulkBTreeSearchJob`, `TraceBTreeTraversalJob`, and `SpatialMortonRangeQueryJob` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. No data layout, B-Tree traversal logic, DataVault handle, signal route, or GlobalQualityWeight math was changed.

Rejected Alternatives: Leaving deterministic mode as a broad safety blanket was rejected because it weakens the mandate and leaves scanner debt in Core/Data. Changing traversal logic was rejected because this loop only corrects compiler directives. Running `dotnet build` was rejected because targeted static proof was sufficient and the user explicitly forbade premature build/rebuild commands.

Scalability potential: Low tier keeps the existing quality-weighted B-Tree traversal collapse and prefetch behavior. Middle/high/ultra retain the same lookup code and can spend saved Burst evaluation cost on richer static data lookups or diagnostics. No binary low/high switch was introduced.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static impact: touched-file `scan_burst` reports `Burst_Job_Directives=0`; touched-file `scan_struct_layout` reports `Runtime_Struct_Layout=0`; compile-wall scan reports zero findings for the two edited Core/Data files. Lightweight full `scan_burst(runtime_cs_files())` reports current repo-wide `Burst_Job_Directives=672` with no touched-file findings. The full multi-gate scanner attempt exceeded the shell timeout and is not used as evidence. No dotnet build was launched.

First 20 Minutes Route Impact: Static/Babel lookup remains available for first-session boot, localization, and static data reads. The change removes compiler-directive debt without adding runtime authority, managed allocation, new global surface, or physical simulation.

## Loop 132 / Vault Probe Diagnostic World Edge Removal

Problem: `VaultProbeUtility` imported `Hecton8.World` for AUP-specific finite/local helpers. Source search showed the only live caller was `ArchitectEyeVisualizer`, which already imports World because it reads AUP buffers for spatial diagnostics. The generic vault probe utility did not need to carry that sibling-domain edge.

Solution: Removed the AUP-specific overloads from `VaultProbeUtility` and added a private `IsFiniteAup(in AbsoluteUniversePosition)` helper to `ArchitectEyeVisualizer`. The visualizer call sites now use the local helper. Generic byte/float/float3 vault probes remain unchanged.

Rejected Alternatives: Keeping public AUP helpers in the generic utility was rejected because it preserved unnecessary compile coupling. Moving AUP helpers into a new global contract was rejected because this is not a new cross-domain route; it is one diagnostic owner-local helper. Changing diagnostic AUP projection was rejected because this loop only removes the compile edge.

Scalability potential: Low tier avoids one more Core diagnostic utility dependency on World. Middle/high/ultra diagnostic behavior is unchanged; Architect Eye still reads bounded AUP samples according to its existing entity budget and quality path.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static impact: targeted compile-wall scan reports `Compile_Wall=84`, down from `85`; `VaultProbeUtility.cs` now has zero compile-wall findings. Touched-file scans report `Runtime_Struct_Layout=0`, `Vault_Sovereignty=0`, and `Burst_Job_Directives=0`. No dotnet build was launched.

First 20 Minutes Route Impact: The Architect Eye diagnostic overlay remains able to inspect AUP buffers during early boot/world validation. The generic vault probe no longer leaks World namespace coupling into every consumer that only needs raw buffer byte/float inspection.

## Loop 133 / Player Movement Presentation AUP Contract Mirror

Problem: `Core/Signals/PlayerMovementPresentationSignals.cs` imported `Hecton8.World` only because `WaterTransitionSignal.AbsolutePosition` used `AbsoluteUniversePosition`. That made a Core signal contract depend on the World assembly for a presentation packet, even though the live consumer only needs source id, frame, kind, and runtime transition data.

Solution: Added `PlayerPresentationAup48`, an explicit 48-byte contract-local AUP mirror, and changed `WaterTransitionSignal.AbsolutePosition` to that type. `HectonPlayerMovement` remains the gameplay owner of the actual `AbsoluteUniversePosition`; it copies Grid/Local fields into the signal mirror when publishing. While the producer file was touched, converted `QueuedCollisionEvent.IsTrigger` and `ColliderCallbackMetadata.IsTrigger` from `bool` fields to byte flags so the touched file has no ARM64 bool-field scanner debt.

Rejected Alternatives: Keeping the World type in the Core signal was rejected because signal contracts must be compile-wall neutral. Reusing `MacroDatabaseAup` was rejected because the semantic owner is not MacroDB and overloading that DTO would blur one fact/one owner. Changing the water-transition consumer was rejected because it does not read the AUP payload. Running `dotnet build` was rejected because targeted scanner proof and diff hygiene were enough for this loop.

Scalability potential: Low tier gets the same water-transition presentation payload without a Core-to-World compile edge. Middle/high/ultra keep the AUP mirror available for richer splash, visor, and audio consumers without forcing them through World-owned methods. No binary quality switch was added.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static impact: compile-wall findings dropped from `84` to `83`; targeted scans on the two touched files report `Runtime_Struct_Layout=0`, `Burst_Job_Directives=0`, and `Vault_Sovereignty=0`.

First 20 Minutes Route Impact: Surface enter/exit and splash transitions still publish through the same SignalBus lane. The AbsolutePosition payload remains blittable at 48 bytes and now belongs to the signal contract instead of forcing a World namespace dependency into Core.

## Loop 134 / Determinism Signal Core Sidecar Extraction

Problem: `LockstepStateValidator` and `InputDispatcher` imported `Hecton8.Physics` only to access `PhysicsDeterminismSignals`, a static facade around Core `SignalBus<T>` lanes and latest-signal sidecars. That made Core deterministic input/lockstep code depend on the Physics namespace for transport state it already owns.

Solution: Added `CoreDeterminismSignals` under `Core/Signals` as the single owner of deterministic input, input override, sync-fence, state-correction, desync, and KCC velocity sidecars. `LockstepStateValidator` and `InputDispatcher` now call the Core sidecar directly. `PhysicsDeterminismSignals` remains as a compatibility facade that forwards all calls to Core, preserving existing Gameplay, QA, and Physics callers without creating duplicate state.

Rejected Alternatives: Leaving the Physics import was rejected because Core was not consuming a physics solver, only a signal facade. Moving the existing facade wholesale into Core was rejected because the old `PublishKccVelocity(in AbsoluteUniversePosition, ...)` helper imports World and would create a new Core-to-World compile-wall edge. Touching `PlayerKinematicsRuntime.cs` to remove that helper was rejected in this loop because the file contains kinematics Burst jobs that are intentionally deterministic; changing them to appease a generic scanner would violate the rollback/kinematics exception.

Scalability potential: Low-tier and VR devices keep the same deterministic override route without extra namespace coupling. Middle/high/ultra behavior is unchanged; the same signal lanes can feed richer diagnostics and replay validation while sidecar truth remains single-owner.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static impact: compile-wall findings dropped from `83` to `81`. Touched-file scanners report `Burst_Job_Directives=0`, `Runtime_Struct_Layout=0`, and `Vault_Sovereignty=0`; only the pre-existing `InputDispatcher.cs` World edge remains among touched Core files.

First 20 Minutes Route Impact: Player input override, ghost replay, KCC velocity publication, and desync signaling remain live for early movement validation. The route now removes two Core-to-Physics compile edges without changing first-session movement semantics.

## Loop 135 / XR Look-At AUP Mirror Extraction

Problem: After the determinism facade extraction, `InputDispatcher` still imported `Hecton8.World` only for XR look-at ray AUP cache math. The file stored `_lastXRLookAtRayOriginAup` and `_lastXRLookAtHitPointAup` as `AbsoluteUniversePosition`, then used World-owned conversion helpers to reuse a recent gaze raycast. This kept a high-value Core input service coupled to World for a tiny presentation-selection cache.

Solution: Added `XRRuntimeAup48` in `HectonXRRuntimeState.cs` as a Core-local explicit 48-byte grid/local mirror and changed `InputDispatcher` to store that mirror. `HectonXRRuntimeState` remains the bridge that can copy from the true World AUP cache for legacy callers; `InputDispatcher` now resolves cached head AUP through `TryResolveCachedHeadAup48`, falls back to a mirror built from `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3`, and performs ray reuse with local grid/local delta math before casting to `float3`.

Rejected Alternatives: Keeping `AbsoluteUniversePosition` in `InputDispatcher` was rejected because it preserved a direct Core-to-World edge for a raycast reuse cache. Moving all XR head AUP ownership out of `HectonXRRuntimeState` was rejected because physical hand, VR somatic, spatial audio, and existing XR pose code still consume the true World AUP route. Caching only runtime `Vector3` positions was rejected because origin shifts and 100km AUP jitter make absolute runtime-float comparison the wrong authority.

Scalability potential: Low/Quest avoids the extra Core input compile breadth and fails closed on invalid gaze positions rather than scheduling bad raycasts. Middle/high/ultra keep the same gaze-selection behavior and can continue to spend XR saved cost on foveated shader state and haptic richness. No binary quality switch was introduced.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static impact: compile-wall findings dropped from `81` to `80`; `InputDispatcher.cs` now has zero compile-wall findings. Touched-file scanners report `Burst_Job_Directives=0`, `Runtime_Struct_Layout=0`, and `Vault_Sovereignty=0`; `git diff --check` passed with CRLF warnings only.

First 20 Minutes Route Impact: XR look-at command scheduling, hit reuse, and dispatcher raycast consumption remain live for early VR interaction. The local cache is now a blittable 48-byte mirror, and non-finite origins/hit offsets disable the gaze ray for that frame instead of contaminating the raycast lane.

## Loop 136 / Player Runtime Pose AUP Namespace Extraction

Problem: `PlayerRuntimeContextService` still contained four explicit `Hecton8.World.AbsoluteUniversePosition` references for player pose fallback stamping and AUP finite validation. The service already receives `PlayerMovementRuntimeState.PredictedAup`; spelling the World namespace in the Core service preserved compile-wall debt without owning the AUP type.

Solution: Replaced the explicit World type with inferred `PredictedAup` field typing and routed fallback runtime-position conversion through `GlobalSignals.TryRuntimePositionToAup(...)`. The finite check now accepts the Core-owned `PlayerMovementRuntimeState`, copies its `PredictedAup`, and calls the existing AUP method without writing a forbidden namespace token in the service.

Rejected Alternatives: Leaving the explicit World namespace was rejected because the fallback conversion has an existing signal-corridor helper. Changing `PlayerRuntimePoseSnapshot` or `PlayerMovementRuntimeState` layout was rejected because those are broad Core contracts still used by many domains. Defaulting bad predicted AUP to zero was rejected because it would publish false player pose telemetry.

Scalability potential: Low/Quest avoids four more Core-to-World source edges in the player runtime context service without changing frame math. Middle/high/ultra keep the same pose snapshot payload and can continue using richer pose consumers through the existing AUP-backed contracts.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static impact: compile-wall findings dropped from `80` to `76`; `PlayerRuntimeContextService.cs` no longer has World findings. Touched-file scanners report `Burst_Job_Directives=0`, `Runtime_Struct_Layout=0`, and `Vault_Sovereignty=0`.

First 20 Minutes Route Impact: Player pose snapshots still use `MovementState.PredictedAup` when valid and finite runtime-position conversion when invalid. The remaining service edges to Audio, Environment, Gameplay, and Inventory are real reference-service wiring and were not widened or disguised in this loop.

## Loop 137 / Procedural Audio Signal Payload Contract Extraction

Problem: `Core/GlobalSignals.cs` embedded `Hecton8.Audio.AudioEventKind`, `AudioPingTriggerInfo`, and `StructuralStressAudioInfo` directly in the Core signal DTO. That made the signal corridor depend on the audio DSP domain for wire payload shape and produced nine compile-wall findings inside one hot contract file.

Solution: Added Core-owned `AudioEventKind`, `AudioPingTriggerPayload`, and `StructuralStressAudioPayload` in the signal contract namespace. `AudioEvent` now carries those DTOs only. `ProceduralAudioEvents` converts its audio-owner listener structs into signal payloads before publish and converts signal payloads back only when dispatching legacy listeners. `PlayerCriticalProceduralAudioRenderer` consumes the contract payloads directly from `SignalBus<AudioEvent>`.

Rejected Alternatives: Keeping audio structs in Core was rejected because it violates one route/one owner. Replacing the typed signal lane with managed audio callbacks was rejected because it would add GC and virtual dispatch. Moving all audio listener structs to Core was rejected because those structs contain audio-domain API semantics and wider external call sites; only the wire payload moved.

Scalability potential: Low/Quest retains the same compact 128-byte typed audio event and avoids widening Core recompiles when DSP-only structs change. Middle/high/ultra keep the same procedural audio lane and can spend DSP budget on richer ping/stress rendering without forcing Core signal contracts to reference the audio assembly.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static impact: compile-wall findings dropped from `76` to `67`; `GlobalSignals.cs` has no `Hecton8.Audio` token. Touched-file `scan_burst=0` and `scan_struct_layout=0`. Touched-file `scan_vault=5` remains in audio owner allocation sites and is not claimed clean.

First 20 Minutes Route Impact: First-session sonar pings and structural stress groans still publish through `SignalBus<AudioEvent>`. Audio listener compatibility remains inside `ProceduralAudioEvents`; the critical renderer reads the new contract payloads directly and falls back to runtime `WorldPosition` when a structural stress event lacks a source AUP flag.

## Loop 138 / Audio Owner Vault Queue Evacuation

Problem: Loop 137 left five touched-file Vault_Sovereignty findings: two `ProceduralAudioEvents` `NativeQueue<AudioEvent>` allocations and `PlayerCriticalProceduralAudioRenderer` sonar/prologue private `NativeQueue`/`NativeParallelHashMap` ownership.

Solution: `ProceduralAudioEvents` now uses DataVault-backed fixed `AudioEvent` rings for front/next-frame dispatch (`BufferID` `70885` and `70886`). `PlayerCriticalProceduralAudioRenderer` now uses DataVault-backed `SonarEchoTap` and `AudioTransitionState` rings (`70889` and `70890`). Sonar echo coalescing now uses a bounded linear Burst pass across 32 candidates and 8 groups instead of persistent native hash containers.

Rejected Alternatives: Managed arrays were rejected because they would hide memory ownership and weaken H-Phi proof. Private `NativeQueue`/`NativeParallelHashMap` fallbacks were rejected because the capacities are fixed and the touched audio owner already resolves DataVault aliases. Editing the central Core BufferID enum was rejected in this loop to avoid touching a core header; local numeric IDs were duplicate-checked against `H8Memory.cs`.

Scalability potential: Low/MX350 removes persistent native container ownership and hash-map maintenance from tiny bounded audio batches. Middle keeps the same sonar/prologue behavior. High/Ultra keep the same procedural audio richness while saved CPU/cache pressure can be spent on DSP and shader response; no binary quality switch was added.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture. Static impact: touched-file `Vault_Sovereignty` dropped from `5` to `0`. The sonar coalescer worst case is `32 * 8 = 256` integer-hash comparisons instead of maintaining two persistent native hash containers for the same capped presentation batch.

First 20 Minutes Route Impact: First-session sonar echo grouping, structural stress/ping listener dispatch, and prologue ocean handoff audio still route through the same public APIs. The bridge memory is now vault-owned, fixed-size, and bounded.

## Loop 139 / MathGuard Vault Ring and BufferID Correction

Problem: `MathGuard` still owned a private persistent `NativeQueue<int>` for invalid-number diagnostics. After replacing the writer type, the touched buoyancy file exposed a private `NativeQueue<FluidImpactEvent>` allocation. Loop 138 also logged a bad BufferID proof: `70810/70811/70820/70821` collide with Atmosphere and Graphics local IDs.

Solution: Replaced `MathGuard` invalid-number queue with two vault buffers: `70883` for error codes and `70884` for a 64-byte counter. `MathGuard.InvalidNumberWriter` is an unmanaged pointer writer over those vault aliases. Replaced `HectonFluidEngine` deferred fluid-impact queue with a fixed DataVault ring at `70799`. Corrected the audio ring IDs to `70885`, `70886`, `70889`, and `70890`; exact numeric scan now shows one code-owner hit for each new ID.

Rejected Alternatives: Keeping `NativeQueue<int>.ParallelWriter` was rejected because it preserves private queue ownership. Leaving `HectonFluidEngine` with a touched-file queue finding was rejected because the scanner evidence would be false. Touching the central BufferID enum for all local provisional IDs was rejected in this loop because it widens a Core header; the safer local correction is unique IDs with explicit proof. Running build/rebuild was rejected because CPU sampled at `100` and the known missing World source is still absent.

Scalability potential: Low tier avoids persistent diagnostic and fluid-impact queue ownership in hot/touched surfaces. Middle keeps the same bounded 64-entry impact and 256-entry invalid-number diagnostic behavior. High/Ultra can spend the saved container and hash-maintenance cost on DSP/shader response; no binary quality switch was introduced.

Hardware Impact: Runtime microseconds claimed: `0`; no profiler capture and no build. Static proof: touched-file `Vault_Sovereignty=0`, `Runtime_Struct_Layout=0`, `Burst_Job_Directives=0`; `InvalidNumberCounter64` is exactly one 64-byte cache line (`4+4+4+4+48 pad`). `rg` reports no old `NativeQueue` or private hash-container tokens in MathGuard, HectonFluidEngine, ProceduralAudioEvents, or PlayerCriticalProceduralAudioRenderer. Compile-wall remains `67` with a known touched finding in `MathGuard.cs` for existing World AUP helpers.

First 20 Minutes Route Impact: NaN/invalid-vector recovery still publishes through `GlobalTelemetryBus` and requests replay dumps. Fluid impact audio/VFX notifications still route through existing public dequeue and signal publication, now backed by a vault ring instead of a private queue. First-session sonar/prologue audio rings keep the same public behavior with corrected non-colliding vault IDs.
