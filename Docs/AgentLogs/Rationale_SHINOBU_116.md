# Rationale_SHINOBU_116

Status: IMPLEMENTED_STATIC / BUILD_BLOCKED_CPU_100
Evidence Class: STATIC_SOURCE until Unity/Burst/GCMonitor evidence exists.

## Global Phase Record
- Phase: SIMULATION for population/diffusion scheduling; POST_SIMULATION for completed-handle swap and telemetry; VISUAL_SYNC only for editor/debug gizmos.
- Owner Assembly: current source folder inherits `Hecton8.Core`; spawn consumer path is routed through `IEcosystemDirectorService`/`GlobalRegistry`, not direct Ambient-to-macro static coupling.
- DataVault Buffers Read: `ShinobuMacroEcosystemSectorFront`, `ShinobuMacroEcosystemTuning`, `ShinobuMacroEcosystemBiomeSpecs`, `ShinobuMacroEcosystemSectorCoords`, `ShinobuMacroEcosystemIndexEntries`.
- DataVault Buffers Written: `ShinobuMacroEcosystemSectorBack`, `ShinobuMacroEcosystemRemainders`, `ShinobuMacroEcosystemCounters`, `ShinobuMacroEcosystemTelemetryRing`, `ShinobuMacroEcosystemCsvScratch`, `ShinobuMacroEcosystemIndexEntries`, `ShinobuMacroEcosystemBiomeSpecs`.
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
Solution: Added isolated BufferIDs 70433-70442 and a headless runtime that owns sector front/back, remainder, coord, index, biome spec, tuning, counters, telemetry, and CSV scratch buffers through `VaultBufferHandle<T>`.
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
Problem: Local policy forbids starting dotnet/Unity compilation while CPU is under heavy work.
Solution: Static source checks were run; build is deferred until processor counter is below 50% and no dotnet/csc process is active.
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
Solution: Ambient now calls only `IEcosystemDirectorService`; `EcosystemDirector.TryGetBiomassAvailability` returns macro Vault biomass first when available, then falls back to legacy local ecology.
Rejected Alternatives: Direct sibling runtime call from Ambient or adding a second consumer-owned population cache. Both create one-fact/two-owner ambiguity.
Scalability potential: Low devices still hydrate only near-player visuals from scalar biomass; High/Ultra can draw richer boids without changing the authoritative route.
Hardware Impact: Avoids compile-wall spread into Ambient and keeps spawn hydration O(1) through the existing service slot.

### D11: Deterministic Telemetry Tick
Problem: `Time.frameCount` is a Unity presentation counter, not a rollback-safe simulation tick.
Solution: Macro telemetry now increments `_simulationTick` only when a FrostTick job is scheduled.
Rejected Alternatives: Unity frame counters or wall-clock time. Both drift under pause, replay, and rollback.
Scalability potential: Same tick semantics across all hardware tiers.
Hardware Impact: No measurable CPU gain; removes a determinism failure class.
