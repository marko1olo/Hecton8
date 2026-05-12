# Rationale_CORE_EVENT_BUS

Prompt: CORE_EVENT_BUS
Role: SIGNAL_MASTER
Status: VERIFIED MASTER GRADE

## Session Start
Problem: Prompt path requested as Docs/Tasks/CURRENT_BATCH.md, but only Docs/Tasks/CURRENT_BATCH.txt exists.
Solution: Extracted `<AGENT_PROMPT id="CORE_EVENT_BUS">` from CURRENT_BATCH.txt using PowerShell raw read and regex, preserving cover-to-cover XML block.
Rejected Alternatives: Did not infer tasks from neighboring prompts or use stale chat memory.
Scalability potential: Low uses compact NativeQueue signals; Middle adds capped drains; High/Ultra can spend saved CPU on richer consumers without changing the signal contract.
Hardware Impact: Avoids direct cross-system calls and managed dispatch; expected low-end i3/MX350 gain is lower compile coupling and hot-path GC risk, measured proof absent.

Problem: Need 2-8 task-relevant mandates before code.
Solution: Selected Zero-GC, Native collections/job lifetime, GlobalRegistry/init, bootstrap safety, telemetry blackbox, AUP, DSP/SPSC audio, and damage routing mandates.
Rejected Alternatives: Did not bulk-load all registry files; not relevant and would pollute decisions.
Scalability potential: Signal contract stays small for weak devices and can feed richer High/Ultra presentation systems through drain caps.
Hardware Impact: Keeps producers Burst-safe and O(1); estimated saved dispatch overhead is microsecond-scale but pending measurement.

## Loop 1 Decisions - Tasks 1-5
Problem: Existing GlobalSignals already owned most queues but lacked the prompt-exact AupShiftSignal lane and did not expose an AUP shift ParallelWriter.
Solution: Added a dedicated NativeQueue<AupShiftSignal>, bootstrap prewarm, disposal, main-thread enqueue compatibility, dequeue access, and ParallelWriter. Kept existing RebaseSignal because other agents may already depend on it.
Rejected Alternatives: Replacing RebaseSignal with AupShiftSignal would be a public API mutation during an active batch and could break concurrent agents.
Scalability potential: Low uses one compact 32-byte shift packet. Middle/High/Ultra consumers can perform richer post-shift rebuilds from the same sector delta without changing producer contracts.
Hardware Impact: One 64-slot queue prewarmed at boot is roughly 2KB payload storage plus NativeQueue overhead; no gameplay heap churn. Estimated runtime gain is avoiding direct origin-shift callbacks, pending profiler proof.

Problem: DamageSignal had to become a prompt-exact 32-byte struct with SubjectHash while preserving numeric target routing.
Solution: Converted DamageSignal to [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)] with float3 local point, uint DamageType, uint SubjectHash, ushort SourceId, byte channel fields, and uint TargetId compatibility.
Rejected Alternatives: Keeping explicit layout would violate the requested sequential layout. Removing TargetId would block legacy numeric damage routing.
Scalability potential: SubjectHash enables cheap FNV-1a identity on toaster hardware and richer top-tier diagnostics/replay without string RPCs.
Hardware Impact: 32-byte payload fits half a 64-byte cache line; linear NativeQueue access stays cache predictable on i3/MX350.

Problem: Physics-to-sound task wants velocity/mass/material hash while existing ImpactSignal consumers use force/intensity/material bytes.
Solution: Added explicit-layout aliases for Velocity/Mass/MaterialHash over the existing force/intensity/body-id fields so new producers can use the prompt vocabulary without breaking current Soundscape and physics code.
Rejected Alternatives: Expanding ImpactSignal above 64 bytes would break cache-line sizing; changing existing fields would create compile risk.
Scalability potential: Low tier can treat velocity/mass as coarse scalars; Ultra can reinterpret the same 64-byte payload for richer acoustic rendering.
Hardware Impact: Zero added bytes; alias fields compile to the same payload offsets.

Problem: HectonFluidEngine already published GlobalSignals ImpactSignal but did not import the new Core.Signals namespace, causing CS0246 after the corridor type remained in its existing signal namespace.
Solution: Added `using Hecton8.Core.Signals;` to HectonFluidEngine. This is a compile-only bridge for an existing producer and does not add a direct dependency to an out-of-domain concrete system.
Rejected Alternatives: Moving ImpactSignal into Hecton8.Core would risk ambiguous references and break existing `using Hecton8.Core.Signals` consumers.
Scalability potential: Keeps impact payload production centralized in the corridor while fluid/audio consumers scale independently by drain caps and quality tier.
Hardware Impact: Import-only change; zero runtime cost.

## Loop 2 Decisions - Tasks 6-10
Problem: SlowTick drains can become audio or UI spam under collision storms or power-grid churn.
Solution: Kept Soundscape impact drains bounded by quality tier and added a hard visor brownout drain cap of 4 signals per UI tick.
Rejected Alternatives: Unbounded `while(TryDequeue)` loops and per-event direct presentation calls; both let producer storms dictate frame time.
Scalability potential: Low drains only a tiny presentation budget. Middle/High/Ultra can raise consumer-side effects from the same compact signal without changing producers.
Hardware Impact: i3/MX350 avoids bursty audio/HUD work; estimated saved worst-case frame time is producer-dependent and unprofiled, but the hard cap prevents unbounded growth.

Problem: AUP shifts were still broadcast through object listener callbacks, leaving jobified consumers without a typed signal lane.
Solution: HectonFloatingOrigin now publishes a 32-byte AupShiftSignal at the committed origin-shift point, including int3 SectorDelta computed from AUP grid deltas.
Rejected Alternatives: Replacing OriginShiftEventData listeners during a multi-agent batch, or using Vector3-only shifts that force every consumer to recompute sector movement.
Scalability potential: Low consumers can ignore sub-sector detail; High/Ultra consumers can rebuild streaming, VFX, and acoustic caches from the same shift packet.
Hardware Impact: One NativeQueue enqueue per origin shift; expected low-end cost under 1 us, pending profiler proof.

Problem: Logistics brownout state reached UI through direct telemetry/listener paths, not the global corridor requested by the batch.
Solution: PowerGridManager publishes BrownoutSignal from aggregate telemetry, and VisorHUDController drains a capped lane to update HUD brownout intensity.
Rejected Alternatives: Making the visor subscribe directly to PowerGridTelemetryEvents; that preserves the old direct coupling and multiplies UI listeners.
Scalability potential: Low maps supply ratio to a cheap shader scalar. High/Ultra can layer dither, scanline, and material overdrive from severity/priority.
Hardware Impact: One slow-tick enqueue plus <=4 UI dequeues; estimated i3/MX350 cost ~2 us pending profiler proof.

Problem: Damage producers needed a single corridor packet, but CombatDamageRuntime owned the health-array queue.
Solution: CombatDamageRuntime drains GlobalSignals DamageSignal before scheduling its existing job, converts to CombatDamageSignal/Detail, and keeps producer code unaware of the damage runtime.
Rejected Alternatives: Asking physics/fauna/tools to call CombatDamageRuntime.TryQueueDamage directly; that recreates circular compile pressure.
Scalability potential: Low uses dominant-axis/simple metadata already in the runtime. High/Ultra can spend saved coupling budget on high-fidelity wound feedback through existing LOD.
Hardware Impact: Capped 64 conversions per frame; worst-case low-end estimate <=40 us pending profiler proof, with no managed allocation.

Problem: Compile verification after loop 2 could be polluted by concurrent agents outside the signal domain.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal -p:UseSharedCompilation=false`; current failures are isolated to ProceduralWreckGenerator missing methods.
Rejected Alternatives: Editing the wreck generator from CORE_EVENT_BUS; it is outside the assigned global signal corridor domain and would be architectural overreach.
Scalability potential: Signal corridor remains compile-clean as far as emitted diagnostics show; unresolved wreck work belongs to that domain owner/integrator.
Hardware Impact: No runtime impact from the build blocker. Integration risk remains until the external compile wall is cleared.

## Loop 3 Decisions - Tasks 11-15
Problem: The batch names `AnomalySignal`, `AcousticPingSignal`, `HypoxiaSignal`, and `ScanCompleteSignal`, while the existing corridor used older semantic names.
Solution: Added prompt-exact unmanaged signal structs, NativeQueue lanes, ParallelWriter properties, Publish methods, TryDequeue methods, bootstrap prewarm, disposal, and editor size validation.
Rejected Alternatives: Relying only on legacy aliases like TelemetryAnomalySignal/SonarPingSignal/OxygenCriticalSignal/ReconDataSignal; that would leave concurrent agents compiling against prompt-exact names with CS0246.
Scalability potential: Low uses the exact compact packets. Middle/High/Ultra can map the same payloads into richer watchdog, predator, hypoxia, and PDA consumers without producer coupling.
Hardware Impact: Added fixed persistent queues only at bootstrap. Runtime hot path remains O(1) enqueue/dequeue with 32-byte or 64-byte packets.

Problem: Wreck scan producers referenced scan event types from the gameplay namespace without importing them, blocking the compile after scan corridor work progressed.
Solution: Added explicit aliases for `ScanEvents` and `ScanEntryKind` in ProceduralWreckGenerator instead of importing the whole gameplay namespace.
Rejected Alternatives: `using Hecton8.Gameplay` created an `InteractionSignal` ambiguity against `Hecton8.Interaction`; direct full-namespace import was too broad.
Scalability potential: The wreck producer can raise scan completion without tying itself to all gameplay symbols.
Hardware Impact: Compile-only alias fix; zero runtime cost.

Problem: Habitat construction producers referenced `HabitatConstructionSignal` without the signal namespace.
Solution: Added `using Hecton8.Core.Signals;` to ConstructionManager and removed the duplicate `Hecton8.Core` import.
Rejected Alternatives: Moving all signal structs into `Hecton8.Core`; that risks broad ambiguous references during the batch.
Scalability potential: Keeps signal contracts centralized while allowing construction producers to publish compact events.
Hardware Impact: Compile-only import fix; zero runtime cost.

Problem: Rigidbody sleep transitions had a signal type but no producer path, leaving Scatter-style consumers forced to poll.
Solution: GlobalPhysicsStateManager now publishes RigidbodySleepSignal on distance sleep/wake transitions with AUP position, body id, distance, and state.
Rejected Alternatives: Scatter Overseer polling all tracked rigidbodies each frame or direct physics-to-scatter method calls.
Scalability potential: Low disables far BRG updates from one sleep packet. High/Ultra can use wake packets to restore richer rendering without polling.
Hardware Impact: One 64-byte enqueue per transition; avoids repeated polling work on i3/MX350.

Problem: Compile verification after loop 3 still fails but the remaining diagnostics are not prompt signal types.
Solution: Fixed the signal-related CS0246/ambiguity errors and stopped at unrelated save/construction compile errors.
Rejected Alternatives: Editing SaveBinaryPayloadCodec, SaveBinaryStorage, HabitatGraphManager, or Physics.SyncTransforms from the signal corridor assignment.
Scalability potential: Leaves domain ownership intact for integrator handoff.
Hardware Impact: No runtime impact from remaining external compile wall.

## Loop 4 Decisions - Tasks 16-20
Problem: Global time sync had a queue type but no authoritative producer.
Solution: HectonCelestialEngine now publishes GlobalTimeSyncSignal when its runtime snapshot changes, carrying absolute universe time, debug time scale, max moon phase, sequence, and validity flag.
Rejected Alternatives: Consumers polling CelestialEngine or subscribing directly to celestial event callbacks for timeline state.
Scalability potential: Low clients align moon state from a compact 32-byte packet. High/Ultra can drive richer atmosphere/ocean sync from the same sequence.
Hardware Impact: One enqueue per changed celestial snapshot; expected low-end cost under 1 us pending profiler proof.

Problem: NativeQueue may be awkward for some strict single-producer/single-consumer job dependency paths.
Solution: Added `SpscSignalRingBuffer<T>` using power-of-two capacity, `(index & mask)` wrapping, and volatile head/tail fields.
Rejected Alternatives: `ConcurrentQueue<T>`, locks, or `%` modulo indexing; each either allocates/locks or costs more than bit masking.
Scalability potential: Low devices use cheap deterministic ring behavior; high-end systems can reserve larger power-of-two rings without changing producer/consumer contracts.
Hardware Impact: One mask and one volatile read/write per operation; no managed heap churn.

Problem: Signal structs must fail fast if a managed reference enters the corridor.
Solution: Kept `where T : unmanaged` validation methods and added `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` inside editor/development validation.
Rejected Alternatives: Reflection field walking; slower, more complex, and unnecessary when the generic constraint already forces compile-time failure for managed payloads.
Scalability potential: The invariant protects all tiers because signal payloads remain blittable for Burst/native lanes.
Hardware Impact: Editor/bootstrap-only validation; no frame cost.

Problem: Final compile had to prove signal-related CS0246 and ambiguity issues were resolved.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal -p:UseSharedCompilation=false`; build succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Leaving previous unrelated compile walls unresolved after they disappeared; the final pass used the actual current tree.
Scalability potential: Integrator receives a compile-clean signal corridor.
Hardware Impact: No runtime impact; compile verification completed.

## OMEGA POLISH CHANGES
Problem: The OMEGA audit found two honest calculations inside CORE_EVENT_BUS bridge code: `math.normalizesafe` while converting `DamageSignal` into combat runtime packets, and `math.sqrt` while publishing rigidbody sleep distance.
Solution: Damage bridge now uses the existing dominant-axis direction helper instead of unit-vector normalization. Rigidbody sleep packets now reconstruct distance with `distanceSq * math.rsqrt(distanceSq)` and keep the work on sleep/wake transition only.
Rejected Alternatives: Kept exact `math.normalize`/`math.sqrt`; both violate the Dear Lie audit for data that is used as a routing/presentation hint, not authoritative collision truth.
Scalability potential: Low uses dominant-axis damage direction and capped signal drains. Middle raises consumer-side effects from the same packets. High/Ultra can spend saved cycles on richer hit feedback, HUD brownout shaders, acoustic layers, and scatter wake visuals without changing producer contracts.
Hardware Impact: i3/MX350 avoids square-root normalization in the global damage bridge and avoids exact sqrt in distance-sleep events. Static estimate: <=64 damage bridge conversions per frame save microsecond-scale scalar math; profiler proof absent.

Problem: Signal corridor must adapt without turning the EventBus into another visual or physics owner.
Solution: Kept all lanes tier-neutral and compact: 32-byte or 64-byte unmanaged structs, NativeQueue MPSC lanes, hard consumer caps, and PO2 SPSC fallback with `(index & mask)`. Quality scaling belongs to consumers: impact drain caps, visor brownout effects, combat math LOD, and scatter wake visuals.
Rejected Alternatives: Adding heavy `GlobalRegistry.ScalabilityTier` branches inside GlobalSignals; that would put presentation policy inside the transport layer and create another coupling point.
Scalability potential: Low = compact payload, minimal drains, scalar visual hints. Middle = moderate drain caps and basic effects. High = same packets drive richer VFX/audio. Ultra = same packets can drive overkill presentation layers while producers remain O(1).
Hardware Impact: Low-end silicon gets bounded enqueue/dequeue and no managed heap churn. Top-tier hardware can increase consumer presentation work without widening payloads or adding producer dependencies.

Problem: Zero-GC purge required a final scan for managed constructs in the new corridor.
Solution: Targeted scan found cold `new NativeQueue<T>` and cold `new NativeArray<T>` setup in GlobalSignals/SPSC fallback, plus struct construction only in bridge methods. No strings, `string.Format`, `.ToString()`, or managed foreach were added to CORE_EVENT_BUS hot bridge code. Existing exact direction code remains elsewhere for player-critical combat behavior outside the new bridge.
Rejected Alternatives: Bulk rewriting large dirty files with concurrent agent edits. That would risk clobbering unrelated work and violate domain ownership.
Scalability potential: Transport stays deterministic and NativeQueue-backed; richer tiers are consumer-local.
Hardware Impact: No hot managed allocations introduced by the signal corridor; measured GC proof requires Unity/GCMonitor, not available in this terminal-only pass.

Problem: Silo audit showed edits in physics, power, visor, combat, celestial, construction, and wreck files outside the narrow Core folder.
Solution: Every cross-domain edit is a bridge or compile alias for the global signal corridor: floating origin publishes AUP shift, power publishes brownout, visor drains brownout, combat drains damage, physics publishes sleep/wake, celestial publishes time sync, construction imports signal namespace, and wreck uses explicit scan aliases to avoid ambiguity.
Rejected Alternatives: Direct concrete dependencies between systems, broad namespace imports, or editing unrelated gameplay logic.
Scalability potential: Cross-domain behavior now uses EventBus packets rather than direct calls. Weak devices can ignore or cap signals; high-end devices can drain the same stream into richer presentation.
Hardware Impact: Compile-only aliases have 0 runtime cost. Runtime bridge packets are bounded and O(1), with estimates recorded in the status checklist.

Problem: Final build health had to be verified after OMEGA code edits.
Solution: Build attempt 8 caught a duplicate helper introduced during polish; removed the duplicate and reused the existing `ResolveDominantAxisDirection`. Build attempt 9 succeeded with 0 warnings and 0 errors using `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal -p:UseSharedCompilation=false`.
Rejected Alternatives: Reporting the pre-polish build as final or leaving a compile wall.
Scalability potential: Compile-clean corridor is ready for integration testing.
Hardware Impact: No runtime impact from the compile step; runtime frame/GC proof still requires Unity Play Mode and GCMonitor.

Honest calculations replaced with cinematic cheats:
- `math.normalizesafe(localPoint, up)` in the global damage bridge -> existing dominant-axis direction helper.
- `math.sqrt(distanceSq)` in rigidbody sleep signal publication -> `distanceSq * math.rsqrt(distanceSq)`.
- Consumer recomputation of origin sectors -> single AUP `int3 SectorDelta` packet at the committed shift.
- UI-side power graph inspection -> scalar `BrownoutSignal` carrying supply/severity/flags.
- Ring fallback modulo/locks -> power-of-two mask wrapping with volatile SPSC indices.

Final Git Diff:
- `?? Assets/_Project/Scripts/Core/GlobalSignals.cs` contains the corridor lanes, prompt-exact structs, writers, validation, disposal, and SPSC fallback.
- Modified bridge/alias files: `HectonFluidEngine.cs`, `HectonFloatingOrigin.cs`, `PowerGridManager.cs`, `VisorHUDController.cs`, `CombatDamageRuntime.cs`, `ConstructionManager.cs`, `ProceduralWreckGenerator.cs`, `GlobalPhysicsStateManager.cs`, `HectonCelestialEngine.cs`.
- `git diff --stat` for tracked touched files: 9 files changed, 2482 insertions, 169 deletions. This working tree contains concurrent agent edits in the same files, so that stat is not solely CORE_EVENT_BUS-owned.
- Docs updated: `Docs/Tasks/Status_CORE_EVENT_BUS.md`, `Docs/AgentLogs/Rationale_CORE_EVENT_BUS.md`, and `Docs/AgentLogs/LOG_CORE_EVENT_BUS.md`.
