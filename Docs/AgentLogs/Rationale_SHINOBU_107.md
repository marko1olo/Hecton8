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
