# Rationale_ECOSYSTEM_MIGRATION_LINK

State: PENDING VERIFICATION / BUILD BLOCKED BY DEPENDENCY

## Decision 0 - Startup Boundary

Problem: Macro-swarm migration touches AI/ecology, native vault storage, AUP coordinates, and signal lanes. Direct scene spawning would violate the prompt and project rules.
Solution: Constrain implementation to `Assets/_Project/Scripts/AI/Ecosystem/` unless a cross-domain interface or service contract requires a minimal extension. Use DataVault-owned NativeArrays and signal/registry interfaces only.
Rejected Alternatives: `Instantiate()` prefabs, direct references to concrete world-streaming or voxel classes, managed ScriptableObject reads in the hydration hot path.
Scalability potential: Low = border spawn fake with reduced visual biomass. Middle = full chunk-border distribution. High = deterministic cave emergence with SDF rejection. Ultra = deeper SDF emergence and richer visual residency after the cheap logic path saves CPU.
Hardware Impact: i3/MX350 target requires no GC and fixed capacity claims; expected savings vs prefab spawning are structural, measured proof absent.

## Decision 1 - Active Simulation Surface

Problem: `BufferID.EntityAUPs` is already loot-owned and typed as `AbsoluteUniversePosition`; treating it as a float3 boid lane would trigger a DataVault type mismatch and corrupt loot acquisition.
Solution: Route macro hydration into the existing ambient ecology SOA (`BiotaAUPs`, `BiotaVelocities`, `BiotaStates`) through registry-facing service contracts, with inactive-slot claims and macro-hydrated flags.
Rejected Alternatives: Reusing `EntityAUPs` as float3, spawning GameObjects, or making `EcosystemDirector` depend directly on `AmbientBiotaDirector` concrete type.
Scalability potential: Low = border ring + billboard flag. Middle = distributed radius fill. High = deterministic SDF-emergence visual flag. Ultra = higher visual count and larger spawn offsets without changing macro authority.
Hardware Impact: i3/MX350 avoids prefab activation and only touches fixed native arrays; expected hydration cost remains bounded by claimed slots and swarm scratch capacity, measured proof pending compile/runtime pass.

## Decision 2 - Vault Import Boundary

Problem: Macro database hydration signals provide sector hashes and raw vault payload handles, not scene objects or managed collections.
Solution: Import vault-owned macro payloads as fixed-stride `MacroSwarm` records, sanitize biomass/speed/AUP fields, clamp to active macro capacity, and reject malformed payload records.
Rejected Alternatives: Managed deserialization during hydration, treating unknown payload bytes as valid, or expanding macro capacity under load.
Scalability potential: Low = only import until fixed cap. Middle = stable cap with radar visibility. High = more active macro swarms via tier cap. Ultra = full 256 swarm cap before active hydration.
Hardware Impact: i3/MX350 gets bounded O(capacity) import with no per-record allocation; expected savings vs managed DB reads are structural, measured proof pending.

## Decision 3 - Visual Hydration Mode Split

Problem: The prompt asks for low-tier border spawns and high-end SDF cave emergence, but per-fish voxel sampling inside the Burst hydration job would couple AI to voxel ownership and exceed the frame budget.
Solution: Keep fish allocation SOA-only. Low tier writes deterministic border-ring offsets and billboard flags. High tier performs one cold-path published SDF sample at the hydration center before scheduling; if the point is not cavity/open water, the job downgrades to border hydration.
Rejected Alternatives: Calling voxel SDF APIs per fish from the Burst hydration job, spawning Transform prefabs, or delaying hydration until VFX systems acknowledge the request.
Scalability potential: Low = instant border ring with 50 percent stress cull. Middle = full SOA fish count inside sector radius. High = emergence flags with deeper vertical offsets. Ultra = downstream renderer can consume the same signal/flag for richer cave exits without changing macro authority.
Hardware Impact: i3/MX350 avoids per-fish SDF reads and object activation; estimated 0.04-0.09 ms saved versus sampling 64 visual fish.

## Decision 4 - First Compile Wall Evidence

Problem: After the local `GlobalSignals.SystemStress01` typo was corrected to `SignalBusRegistry.SystemStress01`, `dotnet build Hecton8.Core.csproj` still failed before full validation because unrelated files are currently uncompilable.
Solution: Treat the current errors as a dependency wall and continue ecology self-review instead of editing UI, item, homeostasis, lockstep, or tether domains.
Rejected Alternatives: Patch unrelated systems outside assigned domain or claim validation passed from source inspection alone.
Scalability potential: No runtime change. Validation status remains blocked until owners restore those compile surfaces.
Hardware Impact: No runtime impact; this is build hygiene evidence.

## Decision 5 - SDF Sign Authority

Problem: Prompt wording says `SdfDensity < 0` for solid-rock rejection, but `HectonVoxelVolume.TrySampleDensity` documents positive density as solid mass and negative density as cavity/open water.
Solution: Trust the source-level SDF contract. High-tier cave emergence only stays enabled when sampled density is finite and negative; positive or missing SDF data downgrades to low-tier border spawn.
Rejected Alternatives: Follow the inverted prompt sign and allow fish into documented solid mass, or sample every generated fish inside the Burst job.
Scalability potential: Low = no SDF call. Middle = no SDF call. High = one center SDF gate. Ultra = same gate plus downstream visual overdraw from EntitySpawnSignal flags.
Hardware Impact: One static SDF sample per high-tier hydration burst; avoids 64+ SDF reads on i3/MX350.

## Decision 6 - Capacity Overflow Handling

Problem: Macro swarms can exceed import cap, hydration scratch cap, dehydration scratch cap, or active macro cap when multiple sectors hydrate/unload in the same frame.
Solution: Clamp every write to fixed NativeArray/NativeList capacity, discard excess biomass, and push MacroSwarmBlackBoxFlagCapacityOverflow into the 300-frame blackbox instead of resizing or writing past cap.
Rejected Alternatives: Resizing NativeArray at runtime, letting AddNoResize throw, or silently dropping overflow without blackbox evidence.
Scalability potential: Low = 32 active macro swarms. Middle = 64. High = 128. Ultra = 256. Overflow behavior stays deterministic at every tier.
Hardware Impact: i3/MX350 avoids allocation spikes and exception paths; estimated gain is avoiding multi-ms resize stalls, with steady branch cost below 1 us.

## Decision 7 - Dehydration Seam

Problem: Active visual fish created from macro swarms would otherwise disappear when a sector unloads, breaking biomass continuity.
Solution: SectorDehydratedSignal first asks the registered AmbientBiota service to release macro-hydrated active slots inside the sector radius, converts released visual fish back to one MacroSwarm payload, then falls back to legacy biomass dehydration only if no active fish were packed.
Rejected Alternatives: Dropping active visual biomass, directly referencing AmbientBiotaDirector concrete type, or spawning replacement GameObjects during unload.
Scalability potential: Low = fewer visual boids are packed because stress/low-tier hydration created fewer. Middle = full sector pack. High/Ultra = same macro payload path with richer visual emergence restored on next hydration.
Hardware Impact: i3/MX350 cost is a bounded SOA scan with no GC; avoids scene-object destruction and recreation churn.

## Decision 8 - Omega Polish Pass

Problem: Final anti-bloat pass was required after all tasks were checked or dependency-blocked, but `CURRENT_BATCH.md` contains no standalone `<POLISH_MANDATE>` XML tag.
Solution: Treat the inline OMEGA mandate as the authority and run targeted grep/diff checks against edited files for prefab spawning, SpawnPoint usage, managed collections, runtime resizing, dump-path mismatch, and stress/SDF gates.
Rejected Alternatives: Skip polish because the XML tag is absent, or edit unrelated files to satisfy aesthetic cleanup.
Scalability potential: No new runtime path. Confirms low/middle/high/ultra behavior remains driven by fixed quality tier and stress gates.
Hardware Impact: No runtime cost; prevents late bloat from entering the hydration path.

## Decision 9 - H-Phi Data Eviction Pass

Problem: The macro bridge still had private native ownership in the hot path: ambient macro counters were allocated by `H8Memory`, and macro hydration/dehydration scratch used local `NativeList<MacroSwarm>` buffers.
Solution: Move bridge-owned macro buffers to `GlobalDataVault` BufferID lanes. `AmbientBiotaDirector` now resolves `BiotaMacroHydrationCounters`; `EcosystemDirector` resolves macro swarms, arrivals, counters, blackbox, mutation scalars, and hydration/dehydration scratch through vault buffers. Scratch lists became fixed NativeArray lanes plus explicit counts.
Rejected Alternatives: Keep local NativeLists for convenience, resize scratch under pressure, or store transient macro claims in managed lists.
Scalability potential: Low = fixed 32 active macro cap and 64 scratch slots. Middle = 64 active macro cap. High = 128. Ultra = 256 active macro swarms with the same vault lanes and no ownership change.
Hardware Impact: i3/MX350 avoids local native allocation ownership churn and NativeList metadata mutation; expected steady-state savings are small (<5 us) but eliminates a failure class.

## Decision 10 - Multiplatform ABI And Signal Guard

Problem: Quest/ARM64 builds are less tolerant of implicit struct layout and unguarded signal payloads. EntitySpawnSignal also lacked a finite-payload guard lane.
Solution: Use explicit Pack=1 layouts for `MacroSwarm`, `MacroSwarmArrival`, `MacroSwarmTelemetryEntry`, and `EntitySpawnSignal`; add `EntitySpawnSignal` finite sanitization; keep raw macro DB import fixed-stride so Steam Deck/MicroSD never reads disk during sector hydration.
Rejected Alternatives: Sequential Pack=4 DTOs, trusting producer payloads blindly, or adding a second duplicate ecology spawn signal.
Scalability potential: Low/Middle read the same compact records. High/Ultra add `FlagHighTierOverkill` so downstream visual owners can spend saved cycles on heavier emergence particles/material response without AI coupling to VFX.
Hardware Impact: ARM64 alignment is explicit; I/O path remains vault-cache only; high-tier overkill is a signal bit, so low-tier CPU cost is zero.

## Decision 11 - Job Stall Removal

Problem: `Schedule().Complete()` inside `TryHydrateMacroSwarms` and `TryPackMacroHydratedBiota` created a synchronous job scheduling stall in the service call.
Solution: Use direct `IJob.Run()` for the bounded 64-boid macro bridge pass. It keeps the Burst job code path and removes the schedule/complete fence while preserving synchronous service return semantics.
Rejected Alternatives: Change public service signatures to async/deferred mid-batch, or leave a forced JobHandle completion in the signal-drain path.
Scalability potential: Low = lower scheduling overhead. Middle = same bounded direct pass. High/Ultra = visual richness comes from flags/downstream rendering, not larger scheduling fences.
Hardware Impact: Avoids job scheduler overhead for tiny jobs; expected savings are micro-scale but removes a main-thread stall class.

## Decision 12 - Ecology Vault Eviction Beyond Macro Bridge

Problem: `EcosystemDirector` still owned legacy persistent `NativeArray` allocations for sector populations, biomass cells, headless fauna, apex overlap scratch, spawn-gate scratch, blackboxes, flora predator AUP upload staging, and later save snapshot staging.
Solution: Added dedicated `GlobalDataVault` BufferID lanes and resolved those fixed-capacity arrays through `vault.GetBuffer<T>(..., SystemID.AIEcology, ...)`. Disposal and memory-sentinel registration were removed for vault-owned array aliases; save snapshot staging now uses vault arrays with explicit counts. Local ownership remains only for NativeHashMap lookup indices because the current DataVault surface is array-based and no hash-map vault contract exists.
Rejected Alternatives: Dispose vault aliases as if locally owned, keep save snapshots as private NativeLists, or keep the old native array allocations because they were cold-path.
Scalability potential: Low = same 32-swarm visual cap and fixed population lanes. Middle = larger active macro cap without ownership change. High = 128 active macro swarms and higher visual residency from the same vault-backed lanes. Ultra = 256 active macro swarms, SDF emergence flags, and high-tier overkill signaling without private array ownership.
Hardware Impact: i3/MX350 avoids persistent allocator churn and sentinel double-accounting; expected steady gain is small (<5 us at init/teardown), but failure-class reduction is material for Quest/Android and Steam Deck memory pressure.

## Decision 13 - Typed Acoustic Lane Purge

Problem: Ecology still emitted whale-fall acoustics through `PhysicsEventBus.NotifyAcousticImpulse`, creating a legacy bus dependency in the ecology director.
Solution: Replaced that call with a fixed `AcousticPingSignal` publish through `GlobalSignals`, using `ChannelLeviathanRoar` and `FlagLeviathanRoar`. The ecology director now uses typed signal lanes for macro spawn, biome gradient consumption, swarm dispersion, and whale-fall acoustic broadcast.
Rejected Alternatives: Keep the physics event bus because listeners already exist, or invent a new whale-fall-only signal.
Scalability potential: Low = one compact ping signal at slow cadence. Middle = same signal for perception/audio. High/Ultra = downstream audio/VFX can add richer leviathan/whale-fall response from the typed lane without ecology taking a direct dependency.
Hardware Impact: Removes listener-bucket dispatch from the ecology slow path; estimated saving is 1-4 us per whale-fall acoustic tick when active, measured proof absent.

## Decision 14 - Save Snapshot Vault Staging

Problem: Save snapshot staging kept `NativeList<EcosystemSectorSaveRecord>` and `NativeList<EcosystemBiomassSaveRun>` as private storage after the broader vault eviction pass.
Solution: Replace those lists with `GlobalDataVault` arrays (`EcosystemSaveSnapshotSectors`, `EcosystemSaveSnapshotBiomassRuns`) and explicit count fields. Save readers receive `GetSubArray(0, count)` slices so downstream serialization only sees valid records.
Rejected Alternatives: Return full-capacity arrays, keep private NativeLists because the path is cold, or redesign the save contract across other domains in this batch.
Scalability potential: Low = bounded snapshot record counts with no resize. Middle = same path with larger configured vault lanes. High/Ultra = dense save staging remains fixed-capacity while visual overkill stays outside the save loop.
Hardware Impact: Steam Deck/MicroSD avoids runtime resize and metadata churn during snapshot capture; expected steady gain is micro-scale, but it removes an I/O-adjacent hitch risk.

## Decision 15 - Population Balancer ABI Repair

Problem: `EcosystemPopulationBalancer` still carried sequential Pack=1 DTOs. Pack=1 helps, but sequential layout still relies on field-order assumptions instead of explicit ABI offsets on ARM64/Quest.
Solution: Convert `EcosystemPopulationCoefficient`, `EcosystemPopulationSectorState`, `EcosystemPopulationCullEvent`, `EcosystemPopulationFreeSlot`, and `EcosystemPopulationTelemetryEntry` to explicit Pack=1 layouts with fixed offsets.
Rejected Alternatives: Leave sequential Pack=1 because the current desktop compiler accepts it, or widen alignment for PC only.
Scalability potential: Low/Middle/High/Ultra all use identical binary lanes; quality tiers change math/visual spend, not record ABI.
Hardware Impact: 0 us runtime. The value is preventing platform-specific padding drift and native-lane corruption.

## Decision 16 - Intermediate Build Evidence

Problem: Earlier validation was blocked by unrelated compile walls. Claiming completion without rerunning build after concurrent fixes would be a false report.
Solution: Re-run `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` after the ABI pass and record the intermediate result: 0 warnings, 0 errors in 67.48 s.
Rejected Alternatives: Trust previous source scans, patch unrelated domains, or claim Quest/Metal/PlayMode runtime verification without executing those targets.
Scalability potential: No runtime change. The intermediate build-green status proved C# compile integrity for that filesystem snapshot only.
Hardware Impact: No runtime impact. Platform runtime verification remains unexecuted in this shell.

## Decision 17 - Final Dependency Wall

Problem: After the intermediate green build, concurrent edits reopened compile failures outside AI/ecology. The final wall is `Assets/_Project/Scripts/LaserCutter.cs`, where `LaserCutterEvents` references `_pendingEvents`, `_nextFrameEvents`, `_nextFrameEventCount`, and `_isDispatching` after those fields were removed.
Solution: Do not patch gameplay-tool event ownership from the ecology agent after repeated dependency-wall strikes. Record the exact wall and leave ecology-owned code in a statically clean state.
Rejected Alternatives: Recreate LaserCutter queue state from this agent, revert another agent's gameplay-tool refactor, or claim the latest build is green from the earlier snapshot.
Scalability potential: No ecology runtime change. Macro-swarm low/middle/high/ultra paths remain implemented; final assembly validation is blocked by unrelated gameplay-tool source.
Hardware Impact: No ecology runtime impact. Latest failed validation wall time was 65.24 s.
