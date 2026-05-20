# Rationale_SHINOBU_224

Status: POLISH STATIC / COMPILE BLOCKED BY DEPENDENCY WALL

## Decision 001 - Authority Owner
Problem: Active equipment scalar truth already exists in `ModularEquipmentEngine`; adding a new processor would create two authoritative owners.
Solution: Keep `ModularEquipmentEngine` as the owner and repair/complete it for SHINOBU_224 requirements.
Rejected Alternatives: New `ActiveEquipmentProcessor` MonoBehaviour was rejected because it would duplicate GlobalRegistry surface and create cross-owner state races.
Scalability potential: Low uses the existing fixed capacity and cadence throttling; Middle/High/Ultra can consume the same read buffer and richer signal consumers without bloating gameplay truth.
Hardware Impact: Avoiding a second owner prevents another per-frame service lookup and duplicate scan of 16 slots; estimated low-end gain 2-5 microseconds vs duplicate processor.

## Decision 002 - Thermodynamics Boundary
Problem: Equipment cooling must depend on the thermodynamic grid, but SHINOBU_224 must not own SHINOBU_117's solver.
Solution: Use cached `IThermodynamicsService`/thermal readback and AUP-relative sampling in the equipment Burst job.
Rejected Alternatives: A private equipment temperature grid was rejected because it creates stale thermal truth and extra native memory.
Scalability potential: Low samples nearest thermal cell; Middle/High/Ultra blend toward trilinear sampling by continuous `GlobalQualityWeight`.
Hardware Impact: Nearest-cell low tier costs one grid read per active tool; trilinear high tier costs up to eight reads only when quality permits.

## Decision 003 - Wear Integration
Problem: Tool wear is still partly drained through `PlayerTool.ApplyDurabilityDrain`, outside the equipment solver.
Solution: Move active-use durability decrement into the central equipment integration path and mirror the result back to the durability owner after the job fence.
Rejected Alternatives: Leaving per-use durability drain in each tool was rejected because it keeps scalar wear ownership fragmented. Adding wear fields to `ActiveEquipmentDTO` was rejected because the 32-byte ABI is fixed.
Scalability potential: Low updates wear at throttled equipment cadence with accumulated dt; Middle/High/Ultra use the same deterministic math with no quality step.
Hardware Impact: One additional `ToolState` stream in the 16-slot job is below 1 microsecond on i3/MX350-class hardware; removing scattered per-tool drain avoids managed service calls during tool use.

## Decision 004 - Dump Identity
Problem: Current equipment dump path and layout error messages carry prior agent IDs, which violates SHINOBU_224 forensic ownership.
Solution: Rename dump path to `Docs/AgentLogs/Dump_SHINOBU_224.bin` and layout errors to SHINOBU_224.
Rejected Alternatives: Keeping prior IDs was rejected because crash artifacts must map to the active owner.
Scalability potential: Same binary dump size, same 300-entry ring; no runtime visual tradeoff.
Hardware Impact: No frame impact; forensic correctness only.

## Decision 005 - Wear Rate ABI
Problem: `ActiveEquipmentDTO` is locked to 32 bytes, but central wear still needs a per-slot drain rate without polluting the network snapshot ABI.
Solution: Add `ShinobuActiveEquipmentWearDrainRates` as a separate Vault-backed `NativeArray<float>` stream and pass it read-only to the Burst integration job beside `ToolState`.
Rejected Alternatives: Expanding `ActiveEquipmentDTO` was rejected because it violates the mandated 32-byte ARM64/network snapshot layout. Reusing DTO padding bytes was rejected because it hides scalar meaning in ABI padding and risks future unsafe copies.
Scalability potential: Low/Middle/High/Ultra all use one extra contiguous float read only for active tools; high-tier visuals can still consume the published DTO without knowing about wear-rate internals.
Hardware Impact: One additional 64-byte cache line covers all 16 local tracked tools; estimated cost below 0.5 microseconds on i3/MX350-class hardware.

## Decision 006 - Absolute Wear Units
Problem: Tool metadata exposes max durability and absolute drain rates, while the durability native state stores normalized durability.
Solution: Convert authored drain to normalized rate by dividing by max durability before both fallback drain and centralized equipment wear.
Rejected Alternatives: Preserving the old raw-rate call was rejected because `durabilityDrainRate=1` with `maxDurability=1000` would destroy a tool in one second instead of one point per second.
Scalability potential: The math is a single reciprocal in the cold/input side and a multiply in Burst; all hardware tiers keep identical durability outcomes.
Hardware Impact: No measurable frame cost; prevents excessive repair churn and false broken-tool signals on low-end devices.

## Decision 007 - Inquisition Boundary
Problem: The task requires an architectural validator, but running source scans in gameplay would be managed allocation and I/O in the wrong phase.
Solution: Add `Equipment_Update_Inquisition` as an editor-only scanner that writes `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` and keeps all file parsing out of runtime assemblies.
Rejected Alternatives: Runtime self-scan was rejected because `Directory.GetFiles`, regex, and JSON string building are managed allocations and irrelevant to player-frame execution.
Scalability potential: Low devices never load the editor scanner; development machines can run the report on demand.
Hardware Impact: Zero player-frame impact; editor-only scan cost is outside runtime budget.

## Decision 008 - Signal-Only Consequences
Problem: Battery depletion and overheating are equipment math outcomes, but spawning VFX/audio or disabling GameObjects from the solver would couple simulation to presentation.
Solution: Keep the Burst job as a data transform and emit only unmanaged `EquipmentOverheatSignal` and `ToolDepletedSignal` payloads through typed `SignalBus<T>.ParallelWriter` lanes.
Rejected Alternatives: Direct particle/audio calls and GameObject disable were rejected because they are managed, non-deterministic, and cross-domain.
Scalability potential: Low can ignore or coalesce signals; Middle/High/Ultra can spend saved CPU on stronger VFX without changing simulation truth.
Hardware Impact: Signal writes are bounded by typed lane capacity and only occur on threshold crossings; expected low-end cost below 1 microsecond in the normal no-signal frame.

## Decision 009 - Read Buffer Fence
Problem: UI and networking need coherent snapshots while the simulation writer buffer is mutable.
Solution: Publish `ActiveEquipmentDTO` through a POST_SIMULATION `UnsafeUtility.MemCpy` into `_publishedActiveEquipmentStates`.
Rejected Alternatives: Letting UI read the writer buffer was rejected because it exposes mid-frame mutation and breaks rollback snapshot assumptions.
Scalability potential: Low gets one 512-byte copy for 16 tools; Ultra can add richer readers without touching the writer path.
Hardware Impact: 16 DTOs at 32 bytes is 512 bytes per publish; estimated below 0.2 microseconds on i3/MX350-class hardware.

## Decision 010 - Continuous Cadence
Problem: Per-frame equipment integration wastes CPU under thermal pressure, but binary low/ultra switches violate the project scalability law.
Solution: Drive integration cadence with `math.lerp(MinimumTickInterval, MaximumTickInterval, 1 - GlobalQualityWeight)` and accumulate dt so battery/heat/wear totals remain accurate.
Rejected Alternatives: Fixed Update cadence and hardware-tier branches were rejected because they either waste CPU or produce discontinuous behavior.
Scalability potential: Low integrates at roughly 5 Hz with accumulated dt; Middle/High interpolate; Ultra can run near-frame cadence for tighter VFX response.
Hardware Impact: Low-end devices skip redundant frames and buy back roughly 3-8 microseconds when no equipment threshold changes occur.

## Decision 011 - Black Box Trigger
Problem: NaN heat or invalid grid data must be diagnosable after the fact.
Solution: Keep a 300-entry `EquipmentTelemetryEntry` ring and dump it to `Docs/AgentLogs/Dump_SHINOBU_224.bin` on first fault flag.
Rejected Alternatives: `Debug.Log`-only reporting was rejected because it loses the previous frame history and allocates strings.
Scalability potential: Same fixed 300-entry buffer on all tiers; high-tier can visualize richer telemetry from the same ring.
Hardware Impact: One ring write per integration tick; estimated below 0.3 microseconds for 16-slot aggregation.

## Decision 012 - Editor Facades
Problem: Tuning, CSV ingestion, and thermal gizmos are required for control, but runtime gameplay cannot pay managed UI or file parsing costs.
Solution: Keep UI Toolkit tuner and gizmo hooks behind editor-only paths, and keep CSV ingestion cold through `ReadOnlySpan<byte>` into Vault-backed specs.
Rejected Alternatives: Runtime debug prefabs, runtime text labels, and managed CSV strings were rejected because they allocate and pollute the equipment frame budget.
Scalability potential: Low devices ship only the native runtime; High/Ultra editor/development builds can visualize and tune overkill thermal behavior.
Hardware Impact: Player-frame impact is 0 us for the editor windows/scanner; cold CSV parse happens only on explicit ingest.

## Decision 013 - Polish Fail-Closed Wear/AUP Ownership
Problem: `AreEquipmentBuffersReady()` did not require the wear-rate Vault stream, and equipment refresh still sampled `owner.transform.position` per active slot.
Solution: Require `_activeEquipmentWearDrainRates` before initialization, resolve equipped-tool AUP from cached `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`, and use `PlayerTool` cached-transform helpers only as a non-equipped fallback.
Rejected Alternatives: Allowing service readiness with a missing wear stream was rejected because it silently disables centralized durability. Per-slot Transform reads were rejected because they keep Unity object state in the active equipment sampling path.
Scalability potential: Low uses one player-pose AUP for all equipped tools; Middle/High/Ultra preserve per-tool fallback only for detached or non-equipped registered tools.
Hardware Impact: Avoids up to 16 managed Transform bridge reads per equipment refresh; estimated low-end gain 0.3-1.2 microseconds and lower ARM64 cache/engine-object pressure.

## Decision 014 - Compile Wall Boundary
Problem: `Hecton8.Core.csproj` build failed with 139 cross-domain missing-symbol errors unrelated to SHINOBU_224 files.
Solution: Treat the result as a dependency wall and preserve SHINOBU_224 changes; do not edit Logistics/Grid, docking, static data, construction socket, fluid, fauna kinematics, or binary pager domains.
Rejected Alternatives: Patching foreign domains from the equipment agent was rejected because it violates the domain boundary and risks merge conflicts with other agents.
Scalability potential: No runtime scalability impact; compile proof remains blocked until owning agents restore referenced contract assemblies/types.
Hardware Impact: No player-frame impact; build failed in 6.90s and did not produce a runtime artifact.

## Decision 015 - Hardware Specs CSV Key Bridge
Problem: `tool_hardware_specs.csv` was absent, and the parser keyed rows by lower-case FNV while `PlayerTool.RuntimeToolId` uses `Animator.StringToHash`; a name-keyed CSV row could parse successfully but never affect battery, heat, or cooldown math.
Solution: Add a live CSV source, support numeric/hex row keys, cache a second FNV spec key in `PlayerTool`, and resolve hardware specs by runtime hash first plus cached spec hash second.
Rejected Alternatives: Changing `RuntimeToolId` to FNV was rejected because that public runtime identifier already feeds modular equipment, signals, and durability mirrors. Allocating strings in the parser to call `Animator.StringToHash` was rejected because Task 17 requires a `ReadOnlySpan<byte>` parser and cold zero-GC bridge.
Scalability potential: Low devices retain one contiguous spec table and no file I/O in gameplay; Middle/High/Ultra can tune richer power/thermal curves from CSV without recompiling C# or changing the 32-byte DTO ABI.
Hardware Impact: Runtime cost is one cached `uint` compare per candidate spec row only during equipment input refresh; expected cost below 0.1 microseconds for the 16-slot local equipment cap, with 0 B GC in player-frame integration.

## Decision 016 - Typed SignalBus Writers And Vault Descriptors
Problem: The Burst equipment job emitted overheat/depletion edges into two equipment-owned `NativeQueue` buffers, then `CompleteActiveEquipmentJob` drained those queues and re-published each payload into `SignalBus<T>`. Buffer acquisition also used direct `IDataVault.GetBuffer<T>` external views.
Solution: Remove the two private equipment signal queues, configure typed `SignalBus<EquipmentOverheatSignal>` and `SignalBus<ToolDepletedSignal>` lanes during cold init, pass their `ParallelWriter`s directly into `EquipmentStateIntegrationJob`, and acquire Vault streams through `GetGenerationHandle<T>` plus `TryResolveHandle`.
Rejected Alternatives: Keeping the local queues was rejected because it created duplicate native signal ownership and an avoidable post-fence loop. Creating a new cross-domain signal owner was rejected because `SignalBus<T>` already owns typed unmanaged broadcast lanes and bounded snapshots.
Scalability potential: Low devices skip the post-fence queue-drain/re-publish pass; Middle/High/Ultra keep the same richer VFX/audio payload surface through the typed lane without widening the 32-byte equipment DTO.
Hardware Impact: Removes two scene-lifetime private queue allocations from the equipment domain and one bounded main-thread dequeue/enqueue loop per completed equipment tick; expected low-end gain is below 1 microsecond normally, with higher savings on threshold-burst frames.

## Decision 017 - Power Runtime Coupling Reduction
Problem: `ModularEquipmentEngine` directly referenced the `Hecton8.Power` runtime namespace only to subscribe as an `IPowerGridTelemetryListener`, widening compile-wall coupling beyond the Core power service contract.
Solution: Remove the Power telemetry listener/subscription route and derive the equipment brownout scalar from cached Core `IPowerGridService.TotalGeneration`, `TotalConsumption`, and `BatterySnapshot`.
Rejected Alternatives: Keeping `PowerGridTelemetryEvents` was rejected because it creates a sibling runtime dependency for a scalar already exposed by the Core registry contract. Adding a new signal was rejected because the needed value is private request/response state for this owner, not a multi-consumer broadcast.
Scalability potential: Low devices avoid the listener bucket and event queue path; Middle/High/Ultra keep the same brownout flicker scalar, with visual intensity still driven by downstream presentation quality.
Hardware Impact: Removes one cold listener registration and one cross-domain telemetry callback route from the equipment domain; per-tick cached service scalar reads are bounded and allocation-free.

## Decision 018 - PlayerTool Hot Registry Cache Closure
Problem: `PlayerTool` still read `GlobalRegistry.ToolDurability`, `GlobalRegistry.Input`, `GlobalRegistry.InteractionSignals`, and `GlobalRegistry.PlayerInventoryRuntime` from methods that can sit on active equipment/tool-use call paths. That violated the cold-discovery rule even though the central Burst solver itself did not poll the registry.
Solution: Extend the existing `PlayerTool` hot-swap cache to durability, input, interaction-signal, and player-inventory services. `CurrentDurability`, `IsBroken`, fallback durability drain, overcharge input checks, queued raycast helper calls, and overcharge inventory removal now use cached fields. `ModularEquipmentEngine` durability mirror registration now uses its cached durability service and refreshes mirrors when `ToolDurabilityRuntime` is rebound.
Rejected Alternatives: Leaving getter-level registry reads was rejected because `ModularEquipmentEngine.Tick()` samples `DurabilityNormalized` while preparing equipment inputs. Adding new signal lanes for private durability/input/inventory queries was rejected because these are direct owner-service reads, not broadcasts.
Scalability potential: Low devices avoid repeated registry property traversal during active use; Middle/High/Ultra preserve the same richer overcharge and durability behavior through cached services without widening the 32-byte equipment DTO.
Hardware Impact: Removes up to 16 durability registry reads per equipment refresh plus per-use input/interaction lookups; expected low-end gain is sub-microsecond to roughly 1 microsecond under active tool spam, with the main value being deterministic ownership and 0 B GC call-stack hygiene.

## Decision 019 - Empty World Namespace Coupling Sweep
Problem: `ToolDurabilitySystem.cs` imported `Hecton8.World` without using a symbol from that namespace, leaving a false sibling-domain coupling marker in SHINOBU_224 static scans.
Solution: Remove the unused `using Hecton8.World;` and keep the file on `UnityEngine.Transform` plus existing gameplay/core contracts.
Rejected Alternatives: Keeping the import was rejected because compile-wall scans treat namespace edges as evidence, and the import provided no runtime behavior.
Scalability potential: No gameplay curve changes; Low/Middle/High/Ultra all keep the same centralized equipment/durability path with less dependency noise.
Hardware Impact: No measurable frame cost change; it protects iteration/build routing by removing one unnecessary compile-surface edge.

## Decision 020 - Durability Vault And Save Cache Boundary
Problem: `ToolDurabilitySystem.Tick()` and durability owner resolution still reached `GlobalRegistry.DataVault`, `GlobalRegistry.Save`, and `GlobalRegistry.Player` through helper calls that can run during active equipment sampling or durability job scheduling.
Solution: Make `ToolDurabilitySystem` an `IGlobalRegistryHotSwapListener`/`IGlobalRegistryHotSwapRefListener`, cache `IDataVault`, `ISaveService`, and `IPlayerRuntimeContext` during cold bootstrap, and rebind those references on registry replacement. `TryResolveBuffer<T>` now resolves through `_dataVault`, save registration uses `_saveService`, and player tool ownership uses `_playerRuntimeContext`.
Rejected Alternatives: Polling `GlobalRegistry` in `Tick()` was rejected because it violates the cold-discovery rule even if the lookup is allocation-free. Moving durability state into `ModularEquipmentEngine` was rejected because durability already owns repair/save/event mirrors and SHINOBU_224 should not create a second durability owner.
Scalability potential: Low devices avoid repeated registry traversal around durability scheduling; Middle/High/Ultra preserve the same centralized wear bridge, editor tuning, and overkill VFX signal surface without widening the 32-byte active equipment DTO.
Hardware Impact: Removes per-tick vault/save/player registry lookups from the durability bridge. Estimated i3/MX350 gain is sub-microsecond normally and roughly 0.5-1.0 microsecond under active tool spam; the larger value is deterministic ownership and no hidden registry polling in equipment-adjacent runtime paths.

## Decision 021 - Durability Generation Descriptor Migration
Problem: `ToolDurabilitySystem` still stored five obsolete `VaultBufferHandle<T>` fields. Those handles carry cached pointer metadata and violate the current ledger rule that persistent Vault state must be a pointer-free `VaultGenerationHandle<T>` descriptor.
Solution: Replace durability handles with `VaultGenerationHandle<T>`, resolve method-local `NativeArray<T>` views through cached `IDataVault.TryResolveHandle`, reacquire missing or undersized lanes through `GetGenerationHandle<T>`, and release descriptors through `IDataVault.ReleaseBuffer` on DataVault rebind or destroy.
Rejected Alternatives: Keeping legacy handles was rejected because even if the resolver ignores stale pointers, the field type teaches future code to persist pointer-bearing metadata. Migrating the entire `ModularEquipmentEngine` NativeArray surface in the same patch was rejected because it touches the main active-equipment owner across many runtime methods and needs its own isolated compile-risk loop.
Scalability potential: Low devices remove stale-pointer validation risk without extra per-frame allocation; Middle/High/Ultra keep the same durability job throughput and can still feed richer active-equipment VFX through the existing scalar lanes.
Hardware Impact: Runtime arithmetic cost is essentially unchanged; metadata safety improves by removing 24-byte pointer-bearing handles and retaining 16-byte descriptors. The practical gain is relocation/defrag safety and less false H-Phi debt around the durability bridge.

## Decision 022 - Modular Equipment Vault View Migration
Problem: `ModularEquipmentEngine` still persisted Vault-resolved `NativeArray<T>` aliases for equipment state, published state, AUP samples, grid-load requests, telemetry, tuning, and hardware specs. That violates the Vault law because a relocation/defrag generation bump can invalidate long-lived native views while the owner still appears initialized.
Solution: Replace the persistent native aliases with 16-byte `VaultGenerationHandle<T>` descriptors and a method-local `EquipmentVaultViews` struct. Every tick, editor read, publish pass, telemetry pass, and mutation path resolves current phase-local views through cached `IDataVault.TryResolveHandle`; missing, undersized, or stale descriptors are released and reacquired through `GetGenerationHandle<T>`. Shutdown and DataVault rebind complete the pending Burst job before releasing descriptors. Thermodynamic readback is no longer stored as a private field; the resolved grid view is local to scheduling and is handed directly to `EquipmentStateIntegrationJob`.
Rejected Alternatives: Keeping scene-lifetime `NativeArray<T>` fields was rejected because it hides pointer lifetime under a safe-looking struct and makes Vault relocation unsafe. Replacing Vault buffers with private persistent arrays was rejected because it violates one owner/one route and breaks rollback snapshot routing. Polling `GlobalRegistry.DataVault` in each hot method was rejected because discovery remains cold and rebinds are handled by cached listener state.
Scalability potential: Low devices keep the same O(N) 16-slot equipment pass and avoid defrag-era stale pointer checks. Middle, High, and Ultra keep the same continuous quality cadence, trilinear thermal blend, and richer signal/VFX surface while still resolving only local views for the active phase.
Hardware Impact: The hot math cost is unchanged except for bounded descriptor validation before scheduling; the safety gain is removal of 17 persistent `NativeArray<T>` aliases from the main equipment owner. Stale-descriptor release prevents Vault refcount drift after relocation and protects ARM64/Quest-class hardware from dereferencing invalid native views.

## Decision 023 - Runtime Transform Fallback And Descriptor Reacquire Closure
Problem: After the Vault view migration, `ToolDurabilitySystem.TryResolveBuffer<T>()` could overwrite a stale or undersized generation descriptor without releasing it first. `ModularEquipmentEngine` also still had runtime fallback paths that could derive equipment water/depth/AUP from Unity object position and `TryResolveSlot()` could resolve Vault views before finding a later owner mirror slot.
Solution: Release stale durability descriptors before reacquire, remove central runtime Transform/AUP fallbacks from equipment sampling, resolve player water/depth once per refresh or publish pass, make non-equipped tool AUP fail closed, and split `TryResolveSlot()` into owner-mirror scan followed by one Vault fallback scan. Layout offset reflection remains editor/development only; release player builds retain unmanaged size checks without runtime reflection.
Rejected Alternatives: Replacing a generation handle without `ReleaseBuffer` was rejected because it risks refcount drift after Vault relocation. Keeping Transform fallback was rejected because the active equipment processor must not query MonoBehaviour hierarchy state during the active execution phase. Resolving Vault views during the owner mirror scan was rejected because most local lookups should complete from the 16-slot mirror.
Scalability potential: Low devices use one cached player pose, one water state, and one depth scalar for all equipped tools in the refresh. Middle/High/Ultra keep deterministic thermal and battery truth while presentation can still spend signal-driven VFX budget on overheat/depletion effects.
Hardware Impact: Removes late-slot Vault resolves and Unity object position fallback from the common equipment lookup/sampling path. Estimated i3/MX350 gain is 0.3-1.5 microseconds under active tool churn; Quest-class ARM benefits mostly from lower engine-object bridge and stale-descriptor safety.

## Decision 024 - Durability Tick Guard Ordering
Problem: `ToolDurabilitySystem.Tick()` still resolved all five durability Vault lanes before checking `enableDurabilityDrain` and `_decayScheduled`, then `HasPendingDecay()` resolved the pending-decay lane a second time before scheduling the decay job.
Solution: Run the cheap boolean guards before `TryResolveNativeState()`, pass the already-resolved `NativeArray<float>` pending-decay view into `HasPendingDecay()`, and clamp the scan length to the resolved buffer length. The active equipment cadence/no-acquire tuning path was rechecked and left unchanged because source already gates full equipment view resolution behind `GlobalQualityWeight` cadence.
Rejected Alternatives: Resolving all lanes every dispatcher tick was rejected because disabled or already-scheduled durability frames do not need any Vault view. Re-resolving pending decay was rejected because the descriptor was already proven in the same phase. Scheduling the editor mock job asynchronously was rejected because CI/editor readback should be deterministic immediately after the cold facade call.
Scalability potential: Low devices now pay only cached save-service registration plus scalar guards when durability drain is disabled or a decay job is already pending. Middle/High/Ultra keep the same scheduled decay math and can still use richer equipment heat/battery signals without widening the active equipment DTO.
Hardware Impact: Removes five descriptor validations on disabled/already-scheduled durability frames and one duplicate pending-decay validation before each scheduled decay. Estimated i3/MX350 gain is 0.2-1.0 microseconds depending on durability cadence and pending work density; Quest-class ARM benefits from less Vault metadata traffic.

## Decision 025 - Service Heartbeat Resolver Purge
Problem: `ModularEquipmentEngine.IsServiceReady` called `TryResolveEquipmentViews(out _)` and read `GlobalRegistry.ModularEquipment`. Any watchdog/bootstrap readiness poll could therefore touch the full equipment Vault descriptor set and potentially reacquire buffers from a property that should be side-effect free.
Solution: Make readiness a local descriptor/flag check: `_isInitialized`, `_registeredService`, `_equipmentSignalLanesReady`, and all 17 `VaultGenerationHandle<T>` descriptors must be created. Add `GlobalRegistryServiceSlot.ModularEquipment` hot-swap handling so `_registeredService` tracks registry replacement without a live registry read in the property.
Rejected Alternatives: Keeping `TryResolveEquipmentViews(out _)` in readiness was rejected because service heartbeat cannot be a hidden Vault acquisition path. Keeping `ReferenceEquals(GlobalRegistry.ModularEquipment, this)` in the property was rejected because registry reads belong to cold registration/rebind paths, not watchdog probes.
Scalability potential: Low devices avoid descriptor validation bursts from watchdog/bootstrap service scans. Middle/High/Ultra preserve the same service health semantics while active equipment still reacquires exact views only in initialization, tick, editor, or post-fence phases.
Hardware Impact: Removes up to one 17-lane equipment descriptor resolve/acquire attempt per readiness poll. Estimated low-end gain is 1-4 microseconds during service probe bursts, with the larger value being elimination of side effects from a property path.

## Decision 026 - Brownout Feedback Narrow View
Problem: `TryGetWirelessBrownoutFeedback()` is a presentation-only query from tool VFX/UI, but it resolved the full `EquipmentVaultViews` set just to read one `ToolState.UpgradeBitmask` bit. `TryGetToolBrownoutFeedback()` also used `TryResolveSlot()`, whose fallback can resolve full active equipment views on a mirror miss.
Solution: Add `TryResolveToolStatesNoAcquire()` for descriptor-only `ToolState` reads and `TryResolveOwnerMirrorSlot()` for cosmetic owner-local lookup. Brownout feedback now uses the 16-slot owner mirror and one ToolState view, then fails closed when the mirror is absent.
Rejected Alternatives: Keeping full view resolution in a visual flicker query was rejected because cosmetic feedback must not acquire all equipment lanes. Expanding `IModularEquipmentService` was rejected because this is an internal implementation cut and changing a public service surface would widen compile risk.
Scalability potential: Low devices skip full Vault metadata traffic during brownout pulse polling. Middle/High/Ultra keep the same flicker scalar and can still spend saved CPU on stronger shader/audio presentation downstream.
Hardware Impact: Removes one 17-lane equipment view resolve from wireless brownout feedback and prevents full-view fallback for brownout pulse lookup. Estimated i3/MX350 gain is 0.5-3 microseconds under UI/VFX polling, with Quest-class ARM benefiting from lower metadata/cache traffic.

## Decision 027 - Public Tool Scalar Getter Narrowing
Problem: `TryGetToolState()` and `TryGetToolStats()` are read-only public getters used by many tool scalar accessors, but each call resolved the full equipment view set after slot lookup. A range, heat, cooldown, drain, recoil, or efficiency query therefore touched many unrelated Vault lanes.
Solution: Reuse descriptor-only `TryResolveToolStatesNoAcquire()` and add `TryResolveToolStatsNoAcquire()`. The state getter reads only ToolState and clamps battery locally; the stats getter reads only ToolStats. Authoritative mutations, publication, telemetry, and mirror writes still use the full view where multiple lanes must stay coherent.
Rejected Alternatives: Keeping getter convenience on `TryResolveEquipmentViews()` was rejected because scalar reads should not validate AUP, grid-load, telemetry, tuning, hardware-spec, published-state, and wear-rate lanes. Changing public method signatures was rejected because existing callers do not need a contract change.
Scalability potential: Low devices avoid broad Vault metadata traffic from active tool spam. Middle/High/Ultra retain identical scalar outputs while downstream presentation can use saved CPU on stronger tool feedback.
Hardware Impact: Removes up to 16 unrelated descriptor validations from each scalar getter call layered on `TryGetToolStats()` or `TryGetToolState()`. Estimated i3/MX350 gain is 0.5-4 microseconds under active use/UI polling; ARM benefits from lower cache churn.

## Decision 028 - Published State And Telemetry Getter Narrowing
Problem: Published read-buffer, telemetry, and tuning getter APIs were resolving the full equipment view set even when they only needed the published DTO stream, telemetry ring/cursor, or one tuning row. UI/editor/debug callers could therefore validate many unrelated lanes.
Solution: Add no-acquire helpers for published active equipment and telemetry ring/cursor, and reuse the existing no-acquire tuning helper for `TryGetEquipmentTuning()`. Mutation/editor write APIs still resolve the full view when they must keep multiple lanes coherent.
Rejected Alternatives: Leaving UI/debug readers on the full resolver was rejected because read buffers and black-box telemetry are already separated Vault lanes. Expanding public method signatures was rejected because caller semantics did not need to change.
Scalability potential: Low devices read HUD/telemetry with only the minimum descriptor set. Middle/High/Ultra preserve exact data while high-tier presentation can poll richer overlays without pulling unrelated gameplay lanes.
Hardware Impact: Removes unrelated tool-state, stats, AUP, grid-load, wear-rate, hardware-spec, and active-state descriptor checks from common published-state and telemetry reads. Estimated i3/MX350 gain is 0.5-5 microseconds under HUD/tuner/debug polling.

## Decision 029 - Telemetry Cursor Wrap Guard
Problem: After narrowing telemetry getters to the ring/cursor lanes, the read path still trusted `telemetryCursor[0]` as if it were always inside `[0, ringLength]`. A stale Vault generation, corrupted cursor, or partial diagnostic write could produce an out-of-range positive index and break the black-box read path.
Solution: Add `ResolveTelemetryHistoryIndex()` to fail closed on invalid ring length, clamp requested history to the ring capacity, and wrap arbitrary cursor deltas with modulo only when the fast unsigned bounds check fails. Latest telemetry now uses the same helper with history 0.
Rejected Alternatives: Keeping the negative-only `while (index < 0)` wrap was rejected because it handles underflow but not cursor values greater than the ring length. Saturating the cursor was rejected because it would bias the black-box reader toward the last slot instead of preserving circular-buffer semantics.
Scalability potential: Low devices keep the fast path as one unsigned range check and one read; Middle/High/Ultra retain exact 300-frame forensic access even if debug tooling requests deep history during heavy polling.
Hardware Impact: Normal path adds no modulo and remains sub-0.1 microseconds. Corrupted cursor recovery pays one integer modulo in a debug/read path to prevent an out-of-bounds telemetry read.
