# Rationale_SHINOBU_116

Status: IMPLEMENTED_STATIC / BUILD_BLOCKED_CPU_GATE
Evidence Class: STATIC_SOURCE until Unity/Burst/GCMonitor evidence exists.

## Global Phase Record
- Phase: SIMULATION for population/diffusion scheduling; POST_SIMULATION for completed-handle swap and telemetry; VISUAL_SYNC only for editor/debug gizmos.
- Owner Assembly: current source folder inherits `Hecton8.Core`; spawn consumer path is routed through `IEcosystemDirectorService`/`GlobalRegistry`, not direct Ambient-to-macro static coupling.
- DataVault Buffers Read: `ShinobuMacroEcosystemSectorFront`, `ShinobuMacroEcosystemTuning`, `ShinobuMacroEcosystemBiomeSpecs`, `ShinobuMacroEcosystemSectorCoords`, `ShinobuMacroEcosystemIndexEntries`, `ShinobuMacroEcosystemFaultFlags`.
- DataVault Buffers Written: `ShinobuMacroEcosystemSectorBack`, `ShinobuMacroEcosystemRemainders`, `ShinobuMacroEcosystemCounters`, `ShinobuMacroEcosystemTelemetryRing`, `ShinobuMacroEcosystemCsvScratch`, `ShinobuMacroEcosystemIndexEntries`, `ShinobuMacroEcosystemBiomeSpecs`, `ShinobuMacroEcosystemFaultFlags`.
- Snapshot Stability Flag: `MacroEcosystemTuningDTO.Flags & MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight` blocks consumer reads while macro diffusion may write the front sector buffer.
- SignalBus Lanes Consumed: none planned unless existing environment lane exists.
- SignalBus Lanes Published: none planned unless existing ecosystem snapshot lane exists.
- MX350/i3 Budget: FrostTick simulation target <1000 us, amortized <200 us/s.
- Load-Shed Fallback: continuous GlobalQualityWeight drives diffusion steps and cadence; no binary low/high switch.

## Decisions

### D00: Authority Bootstrap
Problem: Need macro ecosystem truth without legacy GameObject spawner authority.
Solution: Use explicit-layout unmanaged sector DTOs and Burst job chains over Vault-backed buffers, then expose read-only math snapshots to downstream spawn systems.
Rejected Alternatives: Static scene spawn points and managed population dictionaries were rejected because they create stale authority, string keys, and allocation risk.
Scalability potential: Low uses one diffusion pass and sparse active sectors; Middle uses two to three passes; High uses four passes; Ultra uses five passes plus richer telemetry/editor visualization.
Hardware Impact: i3/MX350 gains from contiguous 32-byte sectors and FrostTick cadence; estimated saved cost versus per-creature simulation is unbounded at world scale, but source-only until measured.

### D01: GlobalDataVault Route Card
Problem: Macro biomass must be a single source of truth without direct dependencies on future spawn implementations.
Solution: Added isolated BufferIDs 70433-70442 plus 70447 and a headless runtime that owns sector front/back, remainder, coord, index, biome spec, tuning, counters, telemetry, CSV scratch, and per-sector fault flag buffers through `VaultBufferHandle<T>`.
Rejected Alternatives: Direct calls into swarm/apex systems, scene components, and local persistent arrays were rejected because they create dependency collisions with parallel agents and bypass snapshot authority.
Scalability potential: Low reads one 10k-sector front buffer with one diffusion step; Middle uses 2-3 passes; High uses 4; Ultra uses 5 plus dense editor heatmap inspection.
Hardware Impact: i3/MX350 avoids per-spawner scans and keeps authoritative state around 320 KB for sectors plus side buffers; expected low-end gain is hundreds of microseconds per local spawn hydration window, pending profiler proof.

### D02: Lotka-Volterra As Visual Currency
Problem: Millions of biomass units cannot be represented as GameObjects or per-agent AI.
Solution: Represent prey/predator populations as integer biomass in 1 km sectors and spend saved CPU on local presentation systems that hydrate only near-player visual lies.
Rejected Alternatives: Subnautica-style static respawn points and real fish for global ecology were rejected as uncontrollable and expensive.
Scalability potential: Low uses coarse 1 km sectors and one pass; Middle increases diffusion; High/Ultra can raise active-sector count or visual hydration richness without changing authority.
Hardware Impact: Low-end silicon runs pure Burst math at FrostTick cadence instead of per-frame object logic; high-end devices buy denser local life without changing deterministic truth.

### D03: Explicit DTO Layout
Problem: Rollback and ARM64 loads require deterministic byte offsets.
Solution: `EcosystemSectorDTO` is `[StructLayout(LayoutKind.Explicit, Size = 32)]` with offsets 0, 8, 12, 16, 20 and byte padding through 31; `MacroEcosystemLayoutManifest` validates size/offsets.
Rejected Alternatives: Auto-layout, properties, and packed structs. Auto-layout is compiler-dependent; Pack=1 risks unaligned loads.
Scalability potential: Same DTO layout serves all tiers; quality changes algorithm passes, not memory schema.
Hardware Impact: 10,000 sectors stride through 320 KB, cache-fit enough for predictable low-end FrostTick work.

### D04: Toxicity and Temperature Cascades
Problem: Rare resource and predator placement must react to sector conditions without binary switches.
Solution: Temperature suitability and toxin penalty continuously modulate birth, starvation, predation, carrying capacity, and resource weights.
Rejected Alternatives: `if (toxic) disable spawn` and hand-authored resource spots. Both produce hard seams and designer drift.
Scalability potential: Low uses same scalar curves with fewer diffusion passes; Ultra can consume the same weights to spawn richer local set dressing.
Hardware Impact: Scalar math adds negligible cost versus table lookups and avoids physics overlap queries entirely.

### D05: Black Box Telemetry
Problem: Ecosystem failures must be reconstructable, not guessed.
Solution: A 300-entry `MacroEcosystemTelemetryEntry` ring records tick, biomass totals, toxic/sterile counts, solver microseconds, and flags; invalid math dumps to `Docs/AgentLogs/Dump_MACRO_ECOSYSTEM.bin`.
Rejected Alternatives: `Debug.Log` spam and post-hoc reproduction. Logs miss previous-frame numeric state.
Scalability potential: All tiers write identical telemetry; Ultra editor tools render more of it.
Hardware Impact: Reduction is one FrostTick pass over 10k sectors; estimated 10-35 us, pending Burst profile.

### D06: Verification Blocker
Problem: Local policy forbids starting dotnet/Unity compilation while CPU is under heavy work or any dotnet/csc process is active.
Solution: Static source checks were run; build is deferred until processor counter is below 50% and no dotnet/csc process is active. Latest gate check reported no dotnet/csc process rows, but `Get-Counter` samples were 72.2/100/99.2, so compile was not launched.
Rejected Alternatives: Faking compile proof or violating CPU gate.
Scalability potential: None; this is process safety.
Hardware Impact: Avoids starving other 20+ agents on the same machine.

### D07: Compile-Risk Hardening
Problem: Several source constructs were legal-looking but weak for Unity.Mathematics/Burst overload resolution.
Solution: Replaced long-based `math.clamp` biomass quantization with `QuantizeBiomass(float)`, replaced ulong graph `math.max` with explicit comparisons, removed an unused diffusion parameter, and made AUP long-to-double conversion explicit.
Rejected Alternatives: Waiting for compiler failure while CPU is pinned. Static hardening is cheaper and does not violate the no-build gate.
Scalability potential: No algorithmic change; all tiers keep the same continuous diffusion curve.
Hardware Impact: Negligible direct gain; reduces compile-wall risk and keeps Burst math deterministic.

### D08: Vault-Backed Open Address Lookup
Problem: The first implementation used private persistent `NativeParallelHashMap` lookup state, violating Vault ownership and increasing hidden allocator surface.
Solution: Replaced private maps with Vault-owned `EcosystemSectorIndexEntryDTO` and `BiomeEcosystemSpecDTO` open-address tables. Cold boot clears tables, mock generation writes sectors, and `BuildSectorIndexJob` populates the sector index deterministically.
Rejected Alternatives: Keeping native maps inside the runtime class or resolving sectors by linear scans. Local maps break Data Sovereignty; linear scans turn every spawn query into O(n).
Scalability potential: Low/Middle/High/Ultra all use O(1) expected lookup; higher tiers spend saved CPU on more diffusion passes and richer visual hydration.
Hardware Impact: i3/MX350 avoids allocator fragmentation and turns sector queries into a bounded probe over contiguous Vault memory; expected per-query cost stays sub-microsecond for 10k sectors until measured.

### D09: Counter False-Sharing Fence
Problem: Adjacent int counters written by jobs can share an L1 cache line and invalidate across worker cores.
Solution: Replaced macro counter storage with `[StructLayout(LayoutKind.Explicit, Size = 64)] MacroEcosystemCounterDTO`; each counter occupies a full cache line.
Rejected Alternatives: `NativeArray<int>` counters and managed telemetry aggregation. Adjacent int counters are cheap until contention starts; then they become false-sharing stalls.
Scalability potential: All tiers keep the same counter layout; Ultra can add more counters without changing the job ABI.
Hardware Impact: Low-end silicon gains determinism under worker contention; estimated savings are workload-dependent but prevents cache-line ping-pong during telemetry/fault writes.

### D10: GlobalRegistry Spawn Route
Problem: Direct `AmbientBiotaDirector -> MacroEcosystemMathematicianRuntime` coupling bypassed the established ecosystem service boundary.
Solution: Ambient now calls only `IEcosystemDirectorService`; `EcosystemDirector.TryGetBiomassAvailability` reads macro Vault biomass through `MacroEcosystemVaultContract` ABI records first when available, then falls back to legacy local ecology.
Rejected Alternatives: Direct sibling runtime call from Ambient, direct World-to-macro concrete call, or adding a second consumer-owned population cache. These create compile-wall spread or one-fact/two-owner ambiguity.
Scalability potential: Low devices still hydrate only near-player visuals from scalar biomass; High/Ultra can draw richer boids without changing the authoritative route.
Hardware Impact: Avoids compile-wall spread into Ambient and keeps spawn hydration O(1) through the existing service slot.

### D11: Deterministic Telemetry Tick
Problem: `Time.frameCount` is a Unity presentation counter, not a rollback-safe simulation tick.
Solution: Macro telemetry now increments `_simulationTick` only when a FrostTick job is scheduled.
Rejected Alternatives: Unity frame counters or wall-clock time. Both drift under pause, replay, and rollback.
Scalability potential: Same tick semantics across all hardware tiers.
Hardware Impact: No measurable CPU gain; removes a determinism failure class.

### D12: Per-Sector Fault Flags
Problem: A parallel population job writing a shared fault counter creates a race and can hide invalid-math sectors behind last-writer-wins behavior.
Solution: Added `ShinobuMacroEcosystemFaultFlags` as a Vault-owned `uint` array with one flag slot per sector. `EcosystemPopulationJob` writes only its own sector index; telemetry reduction ORs those flags into the 300-frame black box entry.
Rejected Alternatives: Atomic counter increments and a shared `MacroEcosystemCounterDTO` write from `IJobParallelFor`. Atomics would add contention; shared writes are not deterministic enough for rollback forensics.
Scalability potential: Low/Middle/High/Ultra keep identical fault truth while higher tiers can visualize or dump more of it outside the gameplay path.
Hardware Impact: i3/MX350 avoids cache-line contention on fault paths and keeps steady-state writes linear and disjoint; expected win is 5-30 us only under fault-heavy FrostTick windows, pending profiler proof.

### D13: Default Biome Specs Plus Editor-Only CSV
Problem: Player runtime file probing during FrostTick violates the CSV bridge boundary, while relying only on editor CSV data leaves the solver weak when the source file is absent.
Solution: Cold boot seeds five deterministic biome spec records into the Vault open-address table. Editor builds may hot-reload `biome_ecosystem_specs.csv` into the same table through the preallocated scratch buffer; player builds do not compile the CSV file probe.
Rejected Alternatives: Runtime `File.Exists` polling in player builds, hardcoded C# values as the only tuning path, and managed `string.Split` CSV parsing.
Scalability potential: Low uses the default table with one diffusion pass; Middle/High/Ultra use the same specs with more diffusion passes and richer local visual hydration.
Hardware Impact: Removes player hot-path file-system risk. The cold seed is tiny relative to sector generation; editor reload cost is outside player frame budget.

### D14: Layout Manifest Expansion
Problem: Primary sector layout was asserted, but secondary Vault DTOs also cross Burst, telemetry, rollback, and DataVault boundaries.
Solution: Expanded `MacroEcosystemLayoutManifest` to assert sizes and representative offsets for sector coords, remainders, open-address index entries, biome specs, tuning, telemetry, and 64-byte counters.
Rejected Alternatives: Document-only layout proof and waiting for IL2CPP/Burst to catch drift. Both are too late for ARM64 alignment regressions.
Scalability potential: Same memory ABI across all tiers; quality changes work count, not binary layout.
Hardware Impact: No runtime hot-path gain after the cold one-time check; prevents misaligned DTO drift that can cost far more on ARM64.

### D15: Macro Vault ABI Contract
Problem: `EcosystemDirector` directly referenced `MacroEcosystemMathematicianRuntime`, creating a concrete World-to-Ecosystem dependency around the spawn hydration route.
Solution: Added `MacroEcosystemVaultContract` under `Hecton8.Core.Contracts` with explicit-layout sector/index/tuning contract records and shared hash/probe math. `EcosystemDirector` now reads macro buffers by `BufferID` through `IDataVault` and contract records; the macro runtime remains the only writer.
Rejected Alternatives: Adding a new GlobalRegistry service slot for one query, editing massive registry headers, reflection, or retaining the direct concrete call. Registry expansion is broader than the need; reflection is AOT/GC-hostile; direct calls spread compile dependencies.
Scalability potential: Low/Middle/High/Ultra keep one read path. The saved coupling budget protects iteration speed rather than frame time.
Hardware Impact: Spawn hydration stays expected O(1). No new allocations; the contract read uses existing Vault metadata and contiguous buffers.

### D16: Snapshot Write-In-Flight Gate
Problem: The macro diffusion chain ping-pongs through front/back sector buffers, so a consumer reading `ShinobuMacroEcosystemSectorFront` during the scheduled FrostTick window could observe a partially written sector snapshot.
Solution: Added `TuningFlagSnapshotWriteInFlight` to the contract/tuning ABI. Macro sets the bit before scheduling population/diffusion/copy/telemetry jobs and clears it only after `_activeJobHandle.Complete()` in the allowed LateFrame/forced teardown path. `EcosystemDirector` reads tuning first and fails closed to legacy ecology while the bit is set; macro static direct readers also reject reads when `_jobScheduled` is true.
Rejected Alternatives: Third sector scratch buffer, blocking consumers on the macro JobHandle, or trusting DataVault locks as reader fences. A third buffer increases Vault surface; blocking consumers violates job discipline; current Vault locks protect relocation/compaction, not read/write coherency.
Scalability potential: All hardware tiers use the same guard. Low-tier long FrostTick windows degrade to legacy local biomass reads rather than torn macro truth; High/Ultra still get macro truth immediately after completion.
Hardware Impact: One tuning flag test in the spawn query path. Expected cost is sub-microsecond; correctness gain is deterministic snapshot fencing.

### D17: Consumer Position NaN Gate
Problem: The contract biomass reader in `EcosystemDirector` accepted a `Vector3` and computed sector hash coordinates before proving all lanes were finite.
Solution: Added a local `float3` finite probe and fail-closed return before `math.floor` and hash computation.
Rejected Alternatives: Letting the macro runtime sanitize after the hash route or clamping NaNs to zero. Both can route an invalid position into a real sector and hide the caller fault.
Scalability potential: Identical for all tiers; invalid input falls back to legacy service behavior instead of corrupting macro query truth.
Hardware Impact: Three finite checks and one branch on spawn query path; cost is below measurement noise, pending profiler proof.

### D18: Cached Vault Consumer Route
Problem: `TryGetMacroVaultBiomassAvailability` used `ResolveDataVault()`, which can fall through to `GlobalRegistry.DataVault` if the local cache is empty. It also reacquired three `TryGetBuffer<T>` views for every macro biomass query.
Solution: The macro Vault consumer now reads the existing `_dataVault` field directly and fails closed when it is absent. It caches only `VaultBufferHandle<T>` metadata for the macro sector snapshot, index table, and tuning record, then resolves those handles per query. Cold allocation still populates `_dataVault` through the existing initialization route, and runtime-state disposal clears the handles.
Rejected Alternatives: Keeping a hot fallback to `GlobalRegistry.DataVault`, adding a new macro service slot, caching `NativeArray<T>` views, or caching a second static Vault reference in the contract layer. Hot registry lookup violates the route discipline; a new service slot is broader than this read; persistent NativeArray views risk stale aliases; contract-layer mutable state would create a second owner.
Scalability potential: Low/Middle/High/Ultra keep the same O(1) sector lookup; the difference is routing discipline, not visual tier behavior.
Hardware Impact: Removes a potential service-locator property read and repeated view acquisition from each biomass hydration query. Expected gain is sub-microsecond per query, but it closes a policy violation before runtime proof.

### D19: Contract Mirror Layout Proof
Problem: The World consumer resolves macro Vault buffers through contract mirror records. `GlobalDataVault` validates stride and alignment, so any drift between writer DTOs and contract records would fatal at read time.
Solution: `MacroEcosystemLayoutManifest` now asserts size and offsets for `MacroEcosystemSectorVaultRecord`, `MacroEcosystemSectorIndexRecord`, and `MacroEcosystemTuningVaultRecord` in the same cold-boot pass that verifies writer DTOs.
Rejected Alternatives: Documentation-only ABI proof, waiting for a Vault type mismatch, or making World reference writer DTOs directly. Documentation cannot stop drift; runtime fatal is too late; direct writer DTO references reintroduce concrete coupling.
Scalability potential: All tiers share the same ABI; no quality branching.
Hardware Impact: One-time boot/editor validation only. Prevents ARM64 stride/alignment regressions in the spawn hydration route.

### D20: Pack=1 Purge In Touched Ecosystem Route
Problem: `EcosystemDirector` contained explicit-layout native/save/telemetry structs with `Pack=1`. Field offsets and sizes were explicit already, so `Pack=1` added ARM64 alignment risk without adding schema clarity.
Solution: Removed `Pack=1` from those explicit-layout structs while preserving every `Size` and `FieldOffset`. The macro runtime and contract records also remain Pack-free.
Rejected Alternatives: Leaving Pack=1 because the structs were legacy, or rewriting the save schema. Leaving it violates the active ARM64 mandate; rewriting schema is unnecessary because explicit offsets and sizes already preserve binary layout.
Scalability potential: No quality-tier difference; this is ABI hygiene.
Hardware Impact: Prevents unaligned-layout drift in native arrays used by the ecosystem consumer/save telemetry route. No measured runtime claim until compile/profiler proof exists.

### D21: Snapshot Fence And Hot Registry Tightening
Problem: The World consumer checked `TuningFlagSnapshotWriteInFlight` only after hash/index resolution and only once, leaving the proof stronger than the implementation. Macro `EnsureVaultState()` could also fall through to `GlobalRegistry.DataVault` when called from `FrostTick`, which weakened cold-discovery discipline.
Solution: `TryGetMacroVaultBiomassAvailability` now reads macro tuning before hash/index work, rejects write-in-flight snapshots, reads the sector, then reads tuning again and rejects if the flag, `Flags`, `StateHash`, or carrying-capacity fields drifted during the query. Macro DataVault binding now occurs in `TryBindDataVaultCold()` during activation or through `OnGlobalRegistryServiceReplaced`; `FrostTick` uses only cached `_vault`. The telemetry reduction job no longer uses `NativeDisableParallelForRestriction`, so no safety suppression remains on those NativeArray fields.
Rejected Alternatives: Taking a reader lock in the spawn query, blocking on the macro JobHandle, adding a third sector snapshot buffer, or keeping a hot registry fallback. Reader locks and JobHandle waits add main-thread coupling; a third buffer expands Vault surface; hot registry fallback violates the route discipline.
Scalability potential: All tiers keep the same O(1) lookup. Low-tier long macro jobs fail closed to legacy local ecology instead of torn macro sectors; Middle/High/Ultra get macro truth after the completed snapshot is stable.
Hardware Impact: Adds one extra tuning record read and two scalar comparisons to the biomass query. Expected cost is below measurement noise; correctness gain is closing a torn-read race. Compile/profiler proof remains blocked by CPU gate.

### D22: Polynomial Quality Curve And Completion Fence
Problem: The diffusion LOD used raw `math.lerp` plus integer cast for pass count, but migration amplitude stayed unchanged. The completion path also set `_jobScheduled` false before clearing the write-in-flight flag, which made the proof rely on no same-frame reentrant reader.
Solution: Added `ResolveQualityCurve` using sanitized `GlobalQualityWeight`, a polynomial thermal band, `math.lerp`, and `math.step`; `ResolveDiffusionSteps` now derives pass count from that curve and `ResolveQualityFlowWeight` scales diffusion migration from 0.25 to 1.0. `_jobScheduled` is now cleared only after the tuning flag is cleared and telemetry is patched.
Rejected Alternatives: Hardware-name branches, low/high enum switches, a separate quality service dependency, or leaving migration amplitude constant at one low-tier pass. Branching violates the scalability pillar; a new service is not needed; constant amplitude spends the same per-neighbor math effect even when quality is shedding work.
Scalability potential: Low q=0.10 gives 1 pass and 0.2500 migration flow; weak-middle q=0.29 gives 1 pass and 0.2763 flow; middle q=0.50 gives 2 passes and 0.4873 flow; high q=0.75 gives 4 passes and 0.8260 flow; ultra q=1.00 gives 5 passes and full flow.
Hardware Impact: Low-end silicon keeps Jacobi passes collapsed and reduces migration delta amplitude without changing Vault ABI. Expected gain is indirect frame budget stability on FrostTick; compile/profiler proof remains blocked because dotnet processes are active.

### D23: Direct Macro Reader Snapshot Fence
Problem: Same-domain static macro readers rejected `_jobScheduled`, but they did not independently verify the tuning snapshot before and after sector reads. That left direct future consumers weaker than the World contract reader.
Solution: `TryGetSectorBiomass` and `TryGetSectorSpawnWeights` now read tuning before hash/index work, reject write-in-flight snapshots, read the sector, then re-read tuning and reject drift through `Flags`, `StateHash`, carrying capacities, `TemperatureOptimum`, and `TemperatureHalfRange`.
Rejected Alternatives: Removing static readers entirely, forcing callers through `EcosystemDirector`, or blocking on `_activeJobHandle`. Removing public helpers is a broader API break; forced routing is not enforceable for same-domain debug/editor code; JobHandle waits violate the async FrostTick contract.
Scalability potential: All tiers use the same O(1) direct read when stable; low-tier longer jobs fail closed rather than reading a torn sector.
Hardware Impact: Adds one extra tuning record read and scalar comparisons only for same-domain direct readers. Expected cost is sub-microsecond and not measured; correctness gain is symmetric snapshot fencing.
