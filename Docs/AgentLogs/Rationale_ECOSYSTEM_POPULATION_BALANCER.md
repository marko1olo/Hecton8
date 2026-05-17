# ECOSYSTEM_POPULATION_BALANCER Rationale

## Initial Scope

Problem: Batch prompt requires Lotka-Volterra enforcement over active entities, but the requested `Assets/_Project/Scripts/AI/Ecosystem/` folder is absent on disk. Existing surfaces are `Assets/_Project/Scripts/Ecosystem`, `Assets/_Project/Scripts/World/EcosystemDirector.cs`, and `Assets/_Project/Scripts/AI/Ecology/Migration`.

Solution: Inspect existing contracts first, then implement through the narrowest data-only AI/Ecology surface that can write entity AUP/flags or consume existing director/state buffers without `Instantiate`/`Destroy`.

Rejected Alternatives: Do not move the existing `World/EcosystemDirector.cs`; relocation is architecture drift and unsafe under 20+ concurrent agents. Do not create a detached balancer that cannot reach the active entity buffers; that would be fake compliance.

Scalability potential: Low uses 1Hz ColdTick and invisible/unloaded chunk culling only. Middle runs full local biomass clamps. High permits Tier 1 flee-down state before cull. Ultra keeps richer telemetry and more precise sector diagnostics while preserving gameplay cost caps.

Hardware Impact: Target i3/MX350; expected active-entity flag pass stays bounded and cold cadence. Estimated saving is workload-dependent; no profiler proof yet, so status is PENDING VERIFICATION.

## Loop 1 - Purge, Coefficients, AUP Sectoring

Problem: The assignment names `Ecosystem_Coefficients.json`, while the actual baked payload on disk is `Data/Precomputed/ecosystem_coefficients.json`. It also requires no private ecology arrays under the later H-Phi/DataVault mandate.

Solution: Load the JSON once on boot, sanitize it, and store it in `BufferID.EcosystemPopulationCoefficients` owned by `SystemID.AIEcology`. Sector state, cull events, telemetry, counters, and free-list storage also use DataVault buffer IDs. The runtime keeps only `VaultBufferHandle<T>` fields and a job handle.

Rejected Alternatives: Rejected hardcoding the coefficients into inspector fields because OSHINO's bake already exists. Rejected private persistent `NativeArray<T>` fields because they violate data sovereignty and complicate defrag/relocation. Rejected touching `World/EcosystemDirector` internals because its biomass arrays are not exposed through a stable interface.

Scalability potential: Low/Toaster uses 1 Hz ColdTick and Tier 2 frozen-only culls. Middle uses sector biomass from `IEcosystemDirectorService` where available. High flags Tier 1 entities with `Flag_EcologyFleeDown` instead of vanishing. Ultra can consume the same flags for richer SDF dive presentation without changing the population kernel.

Hardware Impact: JSON I/O is one cold boot read. Runtime math is a bounded sector/entity pass at 1 Hz; expected i3/MX350 cost is under the 0.1 ms frame budget because the work is off the render tick and completes in the dispatcher swap window. Exact microseconds are not claimed without profiler capture.

Problem: Active entity flags are shared with loot. Clearing bit 0 blindly would deactivate non-ecology pickups.

Solution: Ecology culling requires `Flag_IsActive | Flag_IsPrey | Flag_Tier2Frozen`; ecology ownership bits live in high flag bits and leave loot's low bits alone.

Rejected Alternatives: Rejected clearing every active entity in an overpopulated sector. That would be fast but corrupt cross-domain data and violate signal-lane segregation.

Scalability potential: Low devices get invisible freezer culls; high devices can render flee-down for loaded Tier 1 entities because the active flag remains set until a presentation consumer hides them.

Hardware Impact: The extra bitmask guard is one integer test per candidate and prevents expensive repair of cross-domain data corruption.

Problem: `AbsoluteUniversePosition` has no public `SectorHash` field despite the prompt wording.

Solution: Compute a deterministic 1 km macro-sector hash from AUP grid/local coordinates using integer packing. This keeps sectoring Burst-safe and independent of runtime world-origin shifts.

Rejected Alternatives: Rejected runtime `Vector3` position as the authority for cull sectoring because origin shifts and float precision make it weaker than AUP.

Scalability potential: The hash is device-neutral and deterministic across PC, Android/Quest, Steam Deck, and Mac.

Hardware Impact: Two double additions, two floors, and a pack per ecology entity on ColdTick; no hot-frame cost.

## Loop 2 - SoA Culling, Signal Lane, Visual States

Problem: Ecology enforcement must touch `GlobalDataVault.EntityAUPs` and `EntityFlags` directly without object lifetimes, while avoiding cross-domain damage to loot and other entity lanes.

Solution: The Burst job scans SoA buffers by index. It only clears `Flag_IsActive` for entries carrying ecology prey and Tier 2 frozen bits in the matching AUP sector, then writes the same index into a DataVault free ring.

Rejected Alternatives: Rejected `Destroy(gameObject)`, `Instantiate`, managed registries, and per-entity MonoBehaviour callbacks. Rejected clearing active flags for non-ecology entities.

Scalability potential: Low uses the cheapest invisible data cull. Middle/High/Ultra can consume the same free-list and flee-down flags for richer presentation without changing the population math.

Hardware Impact: Sequential SoA access is cache-friendly on PC and ARM64. Exact microseconds are not claimed until Burst profiler capture.

Problem: Ecology deaths already have a first-party signal type. Creating a new signal would duplicate the lane and split biomass consumers.

Solution: Reuse `EntityDeathSignal` and push through `SignalBus<EntityDeathSignal>.Push` with `SourceHash = ECOL`. `World/EcosystemDirector` already drains `ReadOnlySpan<EntityDeathSignal>` for native biomass impact.

Rejected Alternatives: Rejected managed delegates, legacy EventBus-style fanout, and a new ecology death DTO.

Scalability potential: One typed lane remains deterministic across platforms; high-end presentation can listen to existing snapshots without extra allocation.

Hardware Impact: One fixed payload enqueue per cull event. Event count is bounded by the DataVault cull-event buffer.

Problem: Loaded Tier 1 entities cannot vanish without visual debt, but the population kernel cannot own visual SDF animation.

Solution: For Tier 1 prey, set `Flag_EcologyFleeDown` and leave active state intact. Tier 2 frozen prey are culled immediately because they are unloaded/invisible.

Rejected Alternatives: Rejected spawning VFX or controlling transforms from the balancer; that would cross presentation ownership and violate the prompt's data-only signal rule.

Scalability potential: Toaster mode gets no visible work. High/Ultra can attach SDF dive, particles, and richer biome response to the flag later.

Hardware Impact: Tier 1 path is one flag write, not an animation simulation.

## Loop 3 - Stability, Bounds, Blackbox

Problem: Population math divides by capacity and biomass-per-entity. Non-finite biomass would poison counters and could propagate into telemetry.

Solution: Clamp biomass to `[0, MaxCapacity]`, guard all reciprocal denominators with `math.max(1f, value)`, sanitize coefficients before use, and set an invalid-math counter when a non-finite next biomass is recovered.

Rejected Alternatives: Rejected trusting offline coefficients alone; runtime biomass can still be corrupted by save data, player actions, or external producers.

Scalability potential: Same scalar clamps run on all devices. There is no shader or GPU dependence, so Android/Quest, Mac/Metal, Steam Deck, and PC behave identically.

Hardware Impact: Clamp/finite guards add scalar ALU on a 1 Hz job. Exact microseconds are not claimed until profiler capture.

Problem: Critical ecology state needs postmortem evidence without managed growth or strings in the hot path.

Solution: Keep a 300-entry `EcosystemPopulationTelemetryEntry` ring in `GlobalDataVault`; on invalid math, dump it to `Docs/AgentLogs/Dump_ECOSYSTEM_POPULATION_BALANCER.bin`.

Rejected Alternatives: Rejected Debug.Log spam and managed lists because they allocate and do not survive crash analysis cleanly.

Scalability potential: Low devices keep the same fixed-size ring. High-end builds can inspect richer telemetry by consuming the ring without changing runtime storage.

Hardware Impact: One fixed telemetry write per ColdTick. Dump cost only occurs on fault.

Problem: The Loop 3 compile verification became blocked by external `LaserCutter.cs` edits after Loop 2 had already compiled cleanly.

Solution: Do not edit or revert `LaserCutter.cs`; it is outside this assignment. Record the external compile wall and continue the task loop because owned files had a clean Loop 2 build after the last owned code change.

Rejected Alternatives: Rejected killing shared `csc` processes or repairing another agent's incomplete LaserCutter event-buffer work from this ecology task.

Scalability potential: No runtime change.

Hardware Impact: No runtime change.

## Loop 4 - Stress Cull, Player Impact, Reuse

Problem: The first stress path reused the Lotka-Volterra prey cull target only. That satisfies population correction, but it does not answer the explicit homeostasis requirement to shed Tier 2 cost when `SystemStress01 > 0.8`.

Solution: Split the job into two bounded passes per sector: normal Lotka-Volterra culls still target Tier 2 prey only, while the stress pass can cull any active Tier 2 ecology-owned entity carrying prey or predator kind bits. Non-ecology lanes remain protected by the high-bit ecology mask.

Rejected Alternatives: Rejected clearing every Tier 2 active flag in the shared `EntityFlags` buffer because loot and other systems share low bits. Rejected moving the cull into GameObject lifetime code because the prompt forbids `Destroy(gameObject)`.

Scalability potential: Low devices get aggressive invisible Tier 2 shedding under pressure. Middle keeps predator/prey balance through the primary LV pass. High and Ultra still preserve Tier 1 entities for flee-down presentation instead of visible popping.

Hardware Impact: Stress adds a second sequential SoA scan only when pressure exceeds 0.8 and only at ColdTick cadence. Runtime microseconds are unmeasured without profiler capture; Loop 4 build wall-clock was blocked externally after 53,720,000 us reported by `dotnet build`.

Problem: Player mining and killing must alter biomass natively without inventing a duplicate signal or writing private biomass arrays from the balancer.

Solution: Reuse the existing `World/EcosystemDirector` native biomass path. It drains `ReadOnlySpan<EntityDeathSignal>` and `ReadOnlySpan<ItemAcquiredSignal>` from typed `SignalBus<T>` snapshots, and the balancer emits `EntityDeathSignal` with the ecology source hash.

Rejected Alternatives: Rejected a new ecology biomass signal and rejected direct writes into `EcosystemDirector` private biomass arrays from this domain.

Scalability potential: One biomass consumer owns the grid on all hardware tiers. High-end visual response can layer on the same signal snapshots without changing the balancer.

Hardware Impact: No additional player-impact scan was added. The balancer contributes bounded death events only for culls it already performed.

Problem: New spawns cannot allocate new objects or rely on `Instantiate`; they must reuse dead indices.

Solution: Store culled indices in `BufferID.EcosystemPopulationFreeRing`. Prey respawns reactivate valid prey slots in-place by setting `Flag_IsActive` and clearing cull/free/flee bits. Emergency predator culls are recorded as valid free slots but are not reclassified into prey slots, avoiding archetype corruption.

Rejected Alternatives: Rejected spawning fresh GameObjects, managed queues, and reusing non-prey presentation slots as prey without a dedicated archetype remap contract.

Scalability potential: Low devices amortize population recovery through index reuse. High/Ultra can attach richer presentation to reactivation while the data path stays constant.

Hardware Impact: Free-ring scan is bounded by the DataVault ring capacity and ColdTick cadence. Runtime microseconds are unmeasured until profiler capture.

## Loop 5 - Self-Inquisition, Omega, Final Build

Problem: The earlier full-build attempts were blocked by unrelated files, and the final task requires evidence rather than a chat claim.

Solution: Re-ran the prompt extraction, status/rationale readback, forbidden-pattern scan, struct layout scan, whitespace check, and final `dotnet build`. The final build succeeded with 0 warnings and 0 errors in 2,950,000 us wall-clock. No standalone `<POLISH_MANDATE>` tag exists in `CURRENT_BATCH.md`; the in-prompt Omega requirement is `STATUS: MUST BE "VERIFIED MASTER GRADE"`.

Rejected Alternatives: Rejected editing outside the ecology domain to chase earlier external compile walls. Rejected claiming exact runtime microseconds because no Unity profiler or Burst capture was available in this CLI session.

Scalability potential: Low uses ColdTick, Tier 2 invisible cull, fixed-size rings, and scalar clamps. Middle uses native biomass from `EcosystemDirector`. High uses Tier 1 flee-down flags. Ultra can consume telemetry/flee flags for richer SDF dive and ecological presentation without changing the balancer storage contract.

Hardware Impact: Final compile wall-clock was 2,950,000 us. Runtime impact remains profiler-unmeasured; design is bounded by 1 Hz scheduling, sequential SoA reads, DataVault-owned fixed buffers, and no GameObject lifetime churn.

## Loop 6 - Multiplatform Inquisition

Problem: The sector hash originally packed two macro-sector coordinates into 32-bit halves after `(int)math.floor(...)`. That was adequate for local play space, but it is an unnecessary narrowing point for AUP-scale worlds and weak evidence for Android/Quest/Steam Deck determinism.

Solution: Replace 32-bit packing with saturated 64-bit sector coordinates and a deterministic 64-bit FNV mix. Non-finite coordinates fall back to sector zero through the existing AUP finite gate and saturated floor helper.

Rejected Alternatives: Rejected relying on `int` truncation because the AUP model exists specifically to avoid local-world coordinate ceilings. Rejected a managed dictionary sector key because this job needs Burst-safe scalar math.

Scalability potential: Low/MX350 still gets the same 1 Hz ColdTick and invisible Tier 2 cull. Middle/High/Ultra gain larger deterministic world coverage without changing the DataVault surface. Visual overkill remains delegated to Tier 1 flee-down consumers and telemetry consumers rather than adding render work to this AI kernel.

Hardware Impact: Two extra 64-bit FNV mixes per ecology entity on ColdTick. Runtime microseconds are not measured. Latest `dotnet build` after this change succeeded with 0 warnings and 0 errors in 64,840,000 us wall-clock.

Problem: The multiplatform audit asked for ABI proof on mobile/ARM64.

Solution: Re-scanned `EcosystemPopulationCoefficient`, `EcosystemPopulationSectorState`, `EcosystemPopulationCullEvent`, `EcosystemPopulationFreeSlot`, and `EcosystemPopulationTelemetryEntry`; all are `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = ...)]` with explicit field offsets.

Rejected Alternatives: Rejected assuming default sequential C# layout is stable enough for Quest/Android.

Scalability potential: Identical binary layout across PC, Steam Deck, Mac, Quest, and Android keeps DataVault telemetry/cull/free-ring payloads portable.

Hardware Impact: No runtime cost; this is ABI hardening.

## Loop 7 - Static Dispatch And ABI Hook Hardening

Problem: The ecology runtime installer and central binary layout verifier still used `System.Reflection`/`AppDomain.GetAssemblies()` to locate the balancer and its ABI payloads. That is cold-path work, but it is still forbidden outside editor paths and weak IL2CPP/AOT evidence.

Solution: Replace type-name reflection with direct `EcosystemPopulationBalancer` component installation and direct generic `AssertSize<T>`/`AssertOffset<T>` checks for the five ecology Pack=1 payload structs.

Rejected Alternatives: Rejected keeping optional reflection because missing ecology types should be a compile-time error, not a silent runtime skip. Rejected a new duplicate layout manifest inside the balancer because the central binary sentinel already owns cold-boot ABI checks.

Scalability potential: Low/Quest/Steam Deck avoid assembly scans and AOT ambiguity at boot. High/Ultra keep the same ABI sentinel path without adding runtime simulation work.

Hardware Impact: Cold-start reflection scan removed. Exact microseconds are unmeasured. `dotnet build` after this pass was blocked externally after 61,490,000 us wall-clock by unrelated `ArchitectEyeVisualizer`, `PlayerCriticalProceduralAudioRenderer`, and `AbyssalThermalManager` errors; no owned ecology file error was emitted.

## Loop 8 - Data Sovereignty And Steam Deck I/O Polish

Problem: The balancer still resolved `GlobalRegistry.DataVault` and `GlobalRegistry.EcosystemDirector` inside ColdTick/LateFrame paths, and coefficient import used `File.ReadAllText`. Both were acceptable only as cold conveniences, not as final H-Phi evidence.

Solution: Cache DataVault and ecosystem director dependencies on the component, use the cached DataVault for ColdTick/LateFrame completion, and refresh the director only when absent or not initialized. Replace whole-file coefficient loading with bounded sequential cold I/O: max 16 KiB JSON, 2 KiB read buffer, `FileOptions.SequentialScan`.

Rejected Alternatives: Rejected direct registry polling every tick because dependency access must be staged. Rejected inventing a new binary coefficient format because OSHINO's current artifact is JSON and changing the bake contract is outside this task. Rejected local persistent NativeArrays; remaining `NativeArray<T>` mentions are DataVault-resolved views, method boundaries, or Burst job payloads required by Unity's job API.

Scalability potential: Low/Steam Deck gets bounded cold disk read behavior and no repeated registry service lookup once dependencies are cached. Middle/High/Ultra keep identical Lotka-Volterra behavior; visual overkill remains through `Flag_EcologyFleeDown` consumers instead of extra AI simulation.

Hardware Impact: Runtime microseconds remain unmeasured. Cold coefficient file read is capped to 16 KiB and sequentially read with a 2 KiB buffer to reduce MicroSD pressure. A later compile attempt was blocked externally after 103,870,000 us wall-clock by unrelated `HectonMarineSnowRenderer` missing kernel thread-group helpers; no owned ecology file error was emitted.

## Loop 9 - Registry Hot-Swap And Tick-Lane Hardening

Problem: After dependency caching, a runtime `GlobalRegistry` DataVault or ecosystem-director replacement could leave the balancer holding stale handles or stale service references. That is a cross-agent crash path because the scheduled Burst job owns DataVault views until completion.

Solution: Wire `EcosystemPopulationBalancer` into `IGlobalRegistryHotSwapListener`, complete any scheduled job before DataVault handle reset, clear coefficient/sector state after DataVault replacement, refresh director cache only through the registry replacement callback, and unregister ColdTick/LateFrame lanes when replacement vault setup fails.

Rejected Alternatives: Rejected polling `GlobalRegistry` every ColdTick because Loop 8 deliberately removed recurring registry lookups from the runtime path. Rejected local ownership of backup NativeArrays because the prompt and H-Phi mandate require DataVault-owned storage.

Scalability potential: Low/Steam Deck/Quest avoid stale native-handle crashes during service replacement without adding per-frame lookups. Middle/High/Ultra keep the same Lotka-Volterra kernel and visual overkill delegation through `Flag_EcologyFleeDown`.

Hardware Impact: Runtime microseconds remain unmeasured. The added hot-swap listener only runs on registry replacement, not per frame. `dotnet build` first stopped after 18,970,000 us wall-clock in shared `GlobalSignals.cs`; after that shared duplicate helper was no longer present on disk, the retry stopped after 89,580,000 us wall-clock in unrelated `PhysicsApplySystem` fields `_queueHash` and `PendingEventCapacity`. No owned AI/Ecosystem compiler error was emitted.

## Loop 10 - Atomic Tick Registration And Telemetry Reset

Problem: ColdTick and LateFrame registration were independent. If ColdTick registered and LateFrame failed, the balancer could schedule a Burst job that never reaches the signal-publish completion lane. DataVault replacement also reset handles but left the telemetry cursor and fault-dump latch from the prior storage generation.

Solution: Make tick registration all-or-none by unregistering both lanes when either registration fails. Reset `_telemetryCursor` and `_dumpedFault` when DataVault storage is replaced or cached dependencies are cleared.

Rejected Alternatives: Rejected leaving partial registration alive because silent unpublished culls are harder to diagnose than an unregistered system. Rejected a managed retry timer because tick manager/hot-swap ownership belongs to `GlobalRegistry`, not this ecology kernel.

Scalability potential: Low/Quest/Steam Deck avoid stranded scheduled jobs and stale blackbox offsets. Middle/High/Ultra keep identical population math and event-driven hot-swap behavior.

Hardware Impact: Runtime microseconds remain unmeasured. The additional branch only runs on registration attempts; telemetry reset runs only on service replacement/disable. `dotnet build` after this patch stopped after 104,140,000 us wall-clock with 194 external errors in `World/EcosystemDirector`, `SystemDispatcher`, and `TetherManager`; no owned AI/Ecosystem compiler error was emitted.

## Loop 11 - ABI Tail Fill, Event Capacity, And Cold I/O Fault Containment

Problem: Two Pack=1 ecology payloads used explicit `Size` values with unnamed tail bytes. That is legal C# explicit layout, but it is weak ABI evidence for ARM64/Quest because binary sentinels did not name every byte. The free-ring write cursor also used a monotonic `int`, which can wrap in long sessions. Finally, the cull job could clear active flags after the cull-event buffer was full, causing deaths without `EntityDeathSignal`.

Solution: Add explicit reserved fields to fill `EcosystemPopulationSectorState` and `EcosystemPopulationCullEvent` tails, and assert those offsets in `BinaryLayoutManifest`. Bound the free-ring cursor to `[0, FreeRing.Length)`. Add `TelemetryCullEventOverflowFlag` and stop Tier 2 culling when the cull-event buffer is full, preserving the one-cull-one-signal contract. Wrap cold coefficient JSON read/parse in fallback handling so malformed or inaccessible baked data falls back to sanitized defaults instead of aborting boot.

Rejected Alternatives: Rejected relying on unnamed explicit-size tail padding. Rejected dropping death signals under event pressure because biomass consumers require the typed lane. Rejected an ever-increasing ring cursor because overflow is avoidable with a bounded write index. Rejected crashing on coefficient JSON faults because the default Lotka-Volterra coefficients already provide a safe deterministic fallback.

Scalability potential: Low/Quest/Steam Deck get stronger ABI portability, bounded ring state, and no silent signal loss. Middle/High/Ultra keep identical population math and presentation hooks while gaining clearer overflow telemetry.

Hardware Impact: Runtime microseconds remain unmeasured. Added hot-path work is one bounded cursor branch and one cull-event-capacity branch inside the 1 Hz job. `dotnet build` after this pass succeeded in 54,770,000 us wall-clock with 4 external `ArchitectEyeVisualizer` warnings and 0 errors; no owned AI/Ecosystem warning or error was emitted.

## Loop 12 - Blackbox Fault Containment

Problem: The invalid-math blackbox dump path could return without publishing a math-guard marker when telemetry storage was missing, and a filesystem exception during dump creation could escape the fault-report path. That weakens postmortem evidence exactly when the AI kernel is already reporting invalid Lotka-Volterra math.

Solution: Keep the 300-frame DataVault telemetry ring as the authority, but harden the fault export path. Missing telemetry now publishes `BlackBoxMissingTelemetryHash`; dump I/O failure publishes `BlackBoxDumpIoFaultHash`; both paths still publish `GlobalTelemetryBus.PublishMathGuardInvalidNumber(ECOL)`. The Burst job and ColdTick math path are unchanged.

Rejected Alternatives: Rejected throwing on dump failure because a blackbox export cannot become a second crash source. Rejected managed `Debug.Log` fallback because it creates string/log churn and is weaker than the existing typed telemetry bus. Rejected adding another local buffer because the DataVault ring already owns the 300-frame state.

Scalability potential: Low/Quest/Steam Deck get deterministic fault markers without new frame work. Middle/High/Ultra keep the same population and presentation hooks; richer analysis can consume the same hashed telemetry events and binary dump when disk is writable.

Hardware Impact: Runtime microseconds remain unmeasured. Added work is fault-only after invalid math detection, not steady-state. `dotnet build` after this pass was blocked externally by 8 `SaturateFinite01` errors in unrelated `World/SargassumMicroFaunaBoids.cs`; command wrapper wall-clock was 123,654,153 us, `dotnet` reported 104,080,000 us elapsed, and no owned AI/Ecosystem warning or error was emitted.

## Loop 13 - Free-Ring Rebuild From Authoritative Flags

Problem: The free-ring counters were retained across ColdTicks. That preserved useful reuse state, but it could also preserve stale slots after hot-swap, external flag repair, or any interrupted cull/spawn cycle. A stale free slot can reactivate the wrong ecology index or block valid inactive prey reuse.

Solution: Rebuild `BufferID.EcosystemPopulationFreeRing` during the existing ColdTick SoA scan from authoritative `EntityFlags` and `EntityAUPs`. Inactive prey carrying `Flag_FreeList` repopulate bounded ring slots; the cursor/count counters are rewritten from the rebuilt state; overflow sets `TelemetryFreeRingOverflowFlag`.

Rejected Alternatives: Rejected trusting retained counters because counters are derived state, not authority. Rejected a managed hash set for duplicate detection because the ring can be rebuilt deterministically from SoA flags. Rejected cross-domain spawn remapping because this balancer only owns ecology prey reuse, not prefab/archetype conversion.

Scalability potential: Low/Quest/Steam Deck get deterministic reuse without stale slots or managed allocation. Middle/High/Ultra keep the same cull/spawn behavior and can still consume `Flag_EcologyFleeDown` for presentation overkill.

Hardware Impact: Runtime microseconds remain unmeasured. Added work is one bounded free-ring clear plus reuse-slot writes inside the 1 Hz ColdTick preparation pass, not a per-frame hot path. `dotnet build` after this pass succeeded with 0 warnings and 0 errors; command wrapper wall-clock was 40,580,935 us and `dotnet` reported 40,220,000 us elapsed.

## Loop 14 - Death-Signal Lane Capacity Alignment

Problem: The balancer defaulted to 256 cull events per ColdTick while the existing `EntityDeathSignal` lane is configured with a 64-signal expected/prewarm capacity. The frame snapshot can hold more, but pushing four times the prewarm budget can force typed-lane queue growth and native allocation under a heavy ecology cull.

Solution: Align ecology cull-event production with the existing `EntityDeathSignal` budget. `DefaultCullEventCapacity` now equals 64, runtime `cullEventCapacity` is clamped to `[1, 64]`, and the Burst job receives a scalar `CullEventLimit` so even an already-larger DataVault event buffer cannot publish beyond the lane budget. Existing overflow telemetry handles remaining cull demand without unsignaled culls.

Rejected Alternatives: Rejected editing `GlobalSignals` to expand the death lane because this ecology task should not widen a shared core signal budget. Rejected allowing queue growth because zero-GC evidence matters more than one large ColdTick cull burst. Rejected dropping signals after flag mutation because biomass consumers require one signal per actual cull.

Scalability potential: Low/Quest/Steam Deck avoid native queue growth under stress. Middle/High/Ultra still get deterministic overflow telemetry and can spend visual budget through `Flag_EcologyFleeDown` rather than unbounded death-signal bursts.

Hardware Impact: Runtime microseconds remain unmeasured. The cap is scalar and uses the existing overflow branch; it should reduce worst-case native queue pressure but needs Unity profiler/GCMonitor proof. `dotnet build` after this pass was blocked externally by `Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs` missing `DebugSignal`; command wrapper wall-clock was 21,443,453 us, `dotnet` reported 20,630,000 us elapsed, and no owned AI/Ecosystem compiler error was emitted.

## Loop 15 - Empty-Sector Heartbeat Completion

Problem: The empty-sector telemetry path wrote frame, active count, flags, and hash, but omitted free-ring count and system stress. That leaves weaker postmortem evidence when the blackbox captures a no-sector crash or a DataVault state where entity buffers exist but no ecology sector was active.

Solution: Add `FreeRingCount` and `SystemStress01` to `RecordEmptyTelemetry` using existing DataVault counters and `SignalBusRegistry.SystemStress01`. This keeps the 300-frame heartbeat shape consistent with the scheduled Burst job telemetry without adding new storage.

Rejected Alternatives: Rejected a separate empty-state telemetry struct because that would split blackbox parsing. Rejected managed logging for no-sector states because the fixed DataVault ring already owns crash context.

Scalability potential: Low/Quest/Steam Deck get better crash evidence with only scalar reads on the rare empty path. Middle/High/Ultra preserve the same telemetry schema for tooling and visual-overkill consumers.

Hardware Impact: Runtime microseconds remain unmeasured. Added work is two scalar reads/writes only when no ecology sector job is scheduled. `dotnet build` after this pass succeeded with 0 warnings and 0 errors; command wrapper wall-clock was 90,999,387 us and `dotnet` reported 90,210,000 us elapsed.

## Loop 16 - Chronological Blackbox Dump Ordering

Problem: The DataVault telemetry ring correctly retained the last 300 ecology frames, but the invalid-math dump wrote raw ring slots in storage order. Once the ring wrapped, postmortem readers had to infer the oldest entry from frame values, and partially filled rings included unwritten default slots.

Solution: Pass the current telemetry cursor into `DumpBlackBox`, write `DumpFormatVersion = 2`, capacity, written count, cursor, and oldest slot, then serialize only written telemetry entries in chronological order with bounded wraparound indexing.

Rejected Alternatives: Rejected leaving raw slot order because it weakens the crash blackbox requirement. Rejected sorting by frame because frame counters can wrap and sorting would add unnecessary managed work in a fault path. Rejected adding a second local telemetry buffer because the DataVault ring is the authoritative 300-frame storage.

Scalability potential: Low/Quest/Steam Deck get deterministic postmortem ordering without new steady-state work. Middle/High/Ultra retain the same telemetry schema and can parse richer dump metadata for visual-overkill/debug tooling.

Hardware Impact: Runtime microseconds remain unmeasured. Added work is fault-only binary dump metadata and bounded index arithmetic after invalid math detection. First compile attempt timed out before log creation under concurrent workspace builds; retry succeeded with 0 warnings and 0 errors, command wrapper wall-clock `164,498,586 us`, `dotnet` elapsed `146,390,000 us`.

## Loop 17 - Telemetry Cursor Rollover Guard

Problem: Telemetry writes still incremented `_telemetryCursor` as a raw `int`. At 1 Hz this is a very long-session edge case, but `int.MaxValue` rollover would turn the cursor negative, which could break chronological blackbox ordering and partially hide the most recent ecology state.

Solution: Route all telemetry slot reservations through `ReserveTelemetryIndex`. The helper uses positive modulo for the DataVault ring slot and, at `int.MaxValue`, folds the cursor back to `telemetryLength + nextIndex` so the ring remains marked full and the next write index remains deterministic.

Rejected Alternatives: Rejected widening the cursor to `long` because dump metadata and existing counters are `int`-based and the simpler bounded fold avoids ABI churn. Rejected resetting to zero because that would make a full long-running ring look partially empty to the dump path.

Scalability potential: Low/Quest/Steam Deck get deterministic crash evidence even in pathological long sessions without extra storage. Middle/High/Ultra keep identical telemetry payloads and chronological dump parsing.

Hardware Impact: Runtime microseconds remain unmeasured. Added work is one helper call and one rare overflow branch per telemetry write at 1 Hz. First build attempt failed on transient missing Unity editor metadata in `Temp/bin/Debug`; retry succeeded with 0 warnings and 0 errors, command wrapper wall-clock `18,547,608 us`, `dotnet` elapsed `16,160,000 us`.

## Loop 18 - Stale Free-Slot Guard

Problem: The free ring is rebuilt from authoritative entity flags before the job, but a corrupted or cross-system-mutated slot could still point at an active entity, a non-prey entity, an entity without `Flag_FreeList`, or an entity whose AUP moved out of the slot sector before reactivation. The previous spawn path cleared active stale slots, but it did not validate the full prey/free-list/AUP contract.

Solution: Add `TelemetryStaleFreeSlotFlag` and make `ReactivateFreePreyInSector` validate slot index bounds, active state, `Flag_IsPrey | Flag_FreeList`, finite AUP, and matching sector hash before reusing the entity index. Invalid valid-slots are cleared through `ClearStaleFreeSlot`, decrement free-count, and set telemetry evidence.

Rejected Alternatives: Rejected trusting the ring entry alone because the authoritative state is `EntityFlags` plus `EntityAUPs`. Rejected a managed duplicate-detection set because the existing DataVault SoA scan and fixed ring already provide deterministic rebuild without allocation. Rejected repairing unrelated `SubmarineFluidDynamics.cs` compile errors from this AI/Ecology pass.

Scalability potential: Low/Quest/Steam Deck get safer index reuse without object spawning or managed allocation. Middle/High/Ultra keep the same data-only cull/spawn behavior and can consume stale-slot telemetry for richer diagnostics.

Hardware Impact: Runtime microseconds remain unmeasured. Added work is spawn-path-only scalar validation and AUP sector hash check, not per-frame rendering work. `dotnet build` attempt 1 was `[BLOCKED BY DEPENDENCY]` in unrelated `SubmarineFluidDynamics.cs` with 40 missing-field errors after `331,933,296 us` wrapper wall-clock and `324,570,000 us` dotnet elapsed. Retry 1 exited `-1` after `49,606,445 us`; Retry 2 exited `-1` after `10,125,233 us`; no owned AI/Ecosystem compiler error was emitted.

## Loop 19 - Prey-Only Free Ring

Problem: Stress culling could target predators and other Tier 2 ecology-owned non-prey. The previous cull helper allowed those non-prey entries into `EcosystemPopulationFreeRing`, but the only implemented reactivation path consumes prey slots. Under ring pressure, non-prey entries could evict reusable prey indices before the next ColdTick rebuild.

Solution: Make the free ring explicitly prey-only. `CullTier2EntitiesInSector` now sets `Flag_FreeList` and writes a ring slot only when the culled entity carries `Flag_IsPrey`; non-prey stress culls still clear active state and publish `EntityDeathSignal`, but they do not consume prey reuse capacity.

Rejected Alternatives: Rejected adding a predator spawn/reuse path because the current assignment's active spawn enforcement is prey-focused and no predator reactivation consumer exists. Rejected leaving non-prey slots in the prey ring because that turns stress relief into future spawn starvation. Rejected a second ring because that would expand the DataVault contract without a proven consumer.

Scalability potential: Low/Quest/Steam Deck preserve fixed prey reuse capacity and avoid object churn. Middle/High/Ultra keep the same data-only cull flow; visual overkill still uses existing loaded-prey flee-down flags.

Hardware Impact: Runtime microseconds remain unmeasured. The change removes non-prey ring writes on stress culls and keeps spawn scans prey-focused. First build attempt was `[BLOCKED BY DEPENDENCY]` in unrelated `SubmarineFluidDynamics.cs(5095)` ambiguous `Vector3`/`float3` subtraction after `65,675,626 us` wrapper wall-clock and `63,170,000 us` dotnet elapsed. Retry succeeded with 0 warnings and 0 errors, wrapper wall-clock `106,841,284 us`, dotnet elapsed `105,410,000 us`.

## Loop 20 - Player Build Coefficient Read

Problem: `TryReadCoefficientJson` was wrapped in `#if UNITY_EDITOR`. That meant PC, Steam Deck, Mac, Quest, and Android player builds would never attempt to read shipped `Data/Precomputed/ecosystem_coefficients.json`; they would silently use default Lotka-Volterra constants even when OSHINO's baked data was available beside the build.

Solution: Remove the editor-only preprocessor gate while preserving the existing bounded file length check, sequential read hint, JSON validation, and exception-safe fallback to sanitized defaults.

Rejected Alternatives: Rejected keeping editor-only behavior because it contradicts the multiplatform mandate and the original "read baked coefficients" task. Rejected inventing a new binary coefficient artifact because the existing bake contract is JSON. Rejected changing packaging paths in this pass because asset/build packaging is outside the AI/Ecology domain.

Scalability potential: Low/Steam Deck can use the same bounded 16 KiB cold read when the file is shipped as loose data. Quest/Android/Mac safely fall back if the platform path is unavailable. Middle/High/Ultra preserve the same LV kernel with baked tuning instead of defaults when available.

Hardware Impact: Runtime microseconds remain unmeasured. This is cold boot I/O only; steady-state 1 Hz ecology cost is unchanged. Build attempt 1 was `[BLOCKED BY DEPENDENCY]` in unrelated `HectonPlayerMovement.cs`, `EquipmentInteractionContracts.cs`, and `TetherManager.cs` after `67,908,428 us` wrapper wall-clock and `61,490,000 us` dotnet elapsed. Retry 1 was blocked in unrelated `HectonPlayerMovement.cs` after `35,300,501 us` wrapper wall-clock and `33,040,000 us` dotnet elapsed. Retry 2 was blocked in unrelated `AcousticZoneController.cs` and `TetherManager.cs` after `65,473,487 us` wrapper wall-clock and `58,740,000 us` dotnet elapsed. No owned AI/Ecosystem compiler error was emitted.

## Loop 21 - ABI, Player Coefficients, Blackbox Path

Problem: The code and reports drifted from disk truth. `BinaryLayoutManifest` still asserted old ecology struct sizes after explicit Pack=1 tail fields were added; `TryReadCoefficientJson` still contained `UNITY_EDITOR` gating; and invalid-math blackbox dumps targeted `Dump_ECOSYSTEM_MIGRATION_LINK.bin` instead of the population balancer dump file.

Solution: Update the manifest to assert the actual 64 byte coefficient, 96 byte cull event, and 32 byte free-slot layouts with every reserved tail offset named. Remove the editor-only guard from coefficient loading while keeping bounded cold I/O and fallback behavior. Retarget the blackbox dump to `Dump_ECOSYSTEM_POPULATION_BALANCER.bin` and write only telemetry entries that have actually been reserved in the 300-frame ring.

Rejected Alternatives: Rejected leaving the manifest stale because runtime binary sentinels would fail even when compilation passes. Rejected editor-only coefficient reads because shipped PC, Steam Deck, Mac, Quest, and Android builds must use baked LV data when it exists. Rejected writing unwritten default telemetry slots because that pollutes postmortem chronology.

Scalability potential: Low/Quest/Steam Deck gain ABI sentinel correctness and safe fallback if loose JSON is unavailable. Middle/High/Ultra keep the same LV kernel and can rely on the same dump parser and coefficient contract.

Hardware Impact: Runtime microseconds remain unmeasured. Manifest checks and coefficient file reads are boot/fault-path work; steady-state 1 Hz Burst job cost is unchanged. First build attempt exited `-1` after `191,013,723 us` without diagnostics; retry 1 timed out under concurrent workspace builds after `611,209,000 us`; retry 2 succeeded with 0 warnings and 0 errors, wrapper wall-clock `37,701,855 us`, dotnet elapsed `18,630,000 us`.

## Loop 22 - Shared Entity Buffer Ownership

Problem: The balancer could create `BufferID.EntityAUPs` and `BufferID.EntityFlags` when they were missing. That makes the ecology limiter a hidden owner of the shared entity universe, masks missing migration/loot/entity bootstrap ownership, and weakens data sovereignty.

Solution: Replace shared-buffer creation with handle-only resolution through `TryGetBufferHandle`. Missing entity handles now clear the cached handles, set `TelemetryEntityBuffersMissingFlag`, and let the existing empty-heartbeat path write blackbox telemetry into the balancer-owned ring.

Rejected Alternatives: Rejected continuing to allocate shared entity buffers from `SystemID.AIEcology` because this system should limit populations, not author the global entity table. Rejected failing before telemetry allocation because that would hide missing entity-universe faults from the 300-frame blackbox. Rejected another full dotnet rebuild because the user explicitly told this agent not to rebuild every pass; this loop used targeted `rg` scans and `git diff --check`.

Scalability potential: Low/Quest/Steam Deck avoid silent allocation of a fake entity universe and keep deterministic fault telemetry. Middle/High/Ultra preserve the same LV job when the proper shared buffers exist and get clearer diagnostics when another owner fails to provide them.

Hardware Impact: Runtime microseconds remain unmeasured. The hot path loses potential cold allocations for shared entity buffers; steady-state work is one handle-resolution result branch during setup. No runtime savings are claimed without profiler capture.

## Loop 23 - Missing-Buffer Heartbeat Continuity

Problem: Loop 22 made shared entity buffers handle-only, but `ColdTick` still returned immediately when `TryBuildSectorState` failed. That meant `TelemetryEntityBuffersMissingFlag` could be set without a heartbeat entry proving it in the 300-frame blackbox.

Solution: Route the `TryBuildSectorState` failure path through `RecordEmptyTelemetry` before returning. The telemetry ring and counters are owned by this balancer, so missing shared entity handles now produce a deterministic empty heartbeat with the setup fault flags.

Rejected Alternatives: Rejected reintroducing shared entity buffer allocation because that hides bootstrap faults. Rejected managed logging because the fixed telemetry ring is the required crash evidence. Rejected another full dotnet rebuild because the user explicitly asked not to rebuild every pass; this was a one-branch behavioral correction verified by targeted source read, `rg`, and `git diff --check`.

Scalability potential: Low/Quest/Steam Deck keep fault evidence without allocating fake shared entity buffers. Middle/High/Ultra preserve the normal LV job path when the proper shared buffers exist and gain cleaner postmortem evidence when they do not.

Hardware Impact: Runtime microseconds remain unmeasured. The added work is only on setup-failure/no-entity paths and writes one telemetry entry; no steady-state LV job cost is added.

## Loop 24 - DataVault Job Locks and H8Memory Fence

Problem: The Burst job resolved DataVault `NativeArray` views and scheduled work over them without first locking the underlying buffers. That left the ecology limiter dependent on handle validity alone during a scheduled job, while the memory sentinel/DataVault relocation path expects explicit buffer locks and active-job fences.

Solution: Add `TryLockJobBuffers` and `UnlockJobBuffers` around the scheduled job. The lock set covers coefficients, sector state, cull events, telemetry ring, free ring, counters, shared `EntityAUPs`, and shared `EntityFlags`. Register the scheduled handle with `H8Memory.RegisterActiveJob(SystemID.AIEcology, _balancerHandle)`. Unlock on resolve failure, schedule rejection, late-frame publish completion, forced completion, and disable cleanup. During review, remove a bad draft assignment that set `_jobLocksHeld` at the end of `TryBuildSectorState`; that assignment would have prevented the job from scheduling before any buffer lock was acquired.

Rejected Alternatives: Rejected relying on `VaultBufferHandle<T>` alone because resolved `NativeArray` views still need relocation/defrag protection while a job owns them. Rejected private copied arrays because that violates DataVault sovereignty and doubles memory traffic. Rejected another full dotnet rebuild because the user explicitly told this agent not to rebuild every pass; this loop used targeted source read, `rg`, and `git diff --check`.

Scalability potential: Low/Quest/Steam Deck avoid relocation/teardown races while the 1 Hz ecology job owns shared SoA buffers. Middle/High/Ultra keep the same Lotka-Volterra math and visual-overkill hooks; the change protects the data lane rather than changing presentation.

Hardware Impact: Runtime microseconds remain unmeasured. Added work is fixed lock/unlock scalar bookkeeping around a 1 Hz scheduled job, not per-frame entity math. No runtime savings or costs are claimed without Unity Profiler/Burst evidence.

