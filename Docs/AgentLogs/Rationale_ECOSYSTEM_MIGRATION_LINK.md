# Rationale_ECOSYSTEM_MIGRATION_LINK

State: DOTNET BUILD GREEN / PLATFORM RUNTIME NOT EXECUTED

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

## Decision 18 - Current-Disk Green Revalidation

Problem: The previous final state was dependency-blocked, but integration work landed after that record. Leaving the status blocked without a fresh build would be stale evidence.
Solution: Re-run `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` on current disk and rerun focused ecology scans. Build succeeded with 0 warnings / 0 errors in 59.97 s; ecology bridge scans stayed clean for hot-path stalls, Unity update loops, prefab spawning, managed formatting, local native allocation calls, sequential/Pack=4 ecology DTOs, and active legacy event buses/delegates.
Rejected Alternatives: Keep the stale dependency-blocked state, or claim Unity/Quest/Android/Metal/Steam Deck runtime verification from a desktop C# compile.
Scalability potential: Low = fixed vault import and stress-halved border hydration. Middle = full SOA visual hydration. High = SDF emergence flags. Ultra = high-tier overkill flags for downstream silt/salt/material response without AI owning VFX.
Hardware Impact: Desktop build proof cost 59.97 s. No new runtime cost added; existing microsecond estimates remain estimates, not profiler measurements.

## Decision 19 - Per-Frame Macro Blackbox Heartbeat

Problem: The macro-swarm blackbox had a 300-entry vault ring and event pushes, but the stability mandate requires the last 300 frames of high-level state. Event-only records do not prove frame-by-frame heartbeat continuity.
Solution: Add `PushMacroSwarmHeartbeatBlackBox()` to the existing `ILateFrameTickable` lane. The heartbeat writes one O(1) record per frame using cached macro state hash, active count, arrival count, biomass sum, and hydration estimates. Full macro event pushes still run the heavier swarm validation scan and dump on invalid state.
Rejected Alternatives: Add a standard Unity `Update()`, scan all 256 macro swarms every frame, or leave event-only blackbox evidence.
Scalability potential: Low = one compact heartbeat write while visual biomass is stress-halved. Middle = same O(1) heartbeat with full SOA hydration. High/Ultra = heartbeat remains fixed-cost while downstream visual overkill consumes signal flags.
Hardware Impact: Estimated 1-2 us per frame for one vault ring write, avoiding an estimated 8-20 us full 256-swarm scan on every frame. Measured profiler proof absent.

## Decision 20 - Duplicate Laser Cutter Signal Cleanup

Problem: Integration introduced or exposed duplicate `LaserCutterEventPayload` definitions in gameplay and core signal namespaces, causing ambiguous listener signatures and violating the no-duplicate typed-lane rule.
Solution: Current disk already removed the gameplay duplicate; the canonical `Hecton8.Core.Contracts.Signals.LaserCutterEventPayload` was hardened to explicit Pack=1 offsets.
Rejected Alternatives: Type-alias every listener to preserve duplicate signal definitions, or reintroduce a gameplay-local payload.
Scalability potential: No ecology runtime change. Signal topology remains single-lane and deterministic.
Hardware Impact: 0 us runtime for ecology. Compile wall moved past this duplicate-signal issue.

## Decision 21 - Latest External Compile Wall

Problem: After the heartbeat and signal cleanup, latest `dotnet build` fails outside ecology: `DroneFleetManager.cs` has double3-to-float3 conversion errors, and `PredatorCognitionDomain.cs` has NativeArray/NativeParallelHashMap ownership/type mismatches plus a missing `_speciesTuningById` field.
Solution: Stop at the dependency wall rather than mutating Construction/Fauna ownership from this ecology agent. Record exact errors and keep ecology bridge scans clean.
Rejected Alternatives: Patch unrelated Construction/Fauna systems, revert other agents' edits, or claim build-green from the prior snapshot.
Scalability potential: No ecology runtime change. Macro-swarm low/middle/high/ultra behavior remains implemented; final assembly validation is blocked by external domains.
Hardware Impact: Latest failed validation cost 65.97 s. No ecology runtime cost added by the dependency wall.

## Decision 22 - Vault-Handle NativeArray Field Eviction

Problem: `EcosystemDirector` still carried long-lived `NativeArray<T>` field aliases after earlier vault eviction. That satisfied allocator ownership but still left stale-alias risk under DataVault relocation and failed the stricter data-sovereignty review.
Solution: Replace those fields with `VaultNativeArray<T>` wrappers that store `IDataVault` plus `VaultBufferHandle<T>` and resolve the live array view at use sites. Convert `HeadlessEntitySoA` to the same wrapper model. Copy bound sector food heatmaps into `BufferID.EcosystemSectorFoodHeatmapR8` so ecology does not retain an external native-array alias. Use explicit generic pointer/GPU upload calls after wrapper resolution.
Rejected Alternatives: Keep raw long-lived `NativeArray<T>` fields, expose property-returned `NativeArray<T>` views that break indexed assignment, or redesign DataVault hash-map ownership inside the ecology pass.
Scalability potential: Low = vault-resolved border hydration and copied heatmap avoid external alias/I/O jitter. Middle = full SOA visual hydration over the same handles. High = SDF emergence and richer residency flags without new ownership. Ultra = downstream visual overkill can spend GPU cycles while ecology keeps fixed-capacity vault lanes.
Hardware Impact: Wrapper resolution adds tiny cold-path pointer lookup cost, but removes stale alias and allocator ownership failure classes. Static scan found no private NativeArray owner fields in ecology bridge files. Remaining local native ownership is `_biomassIndexByKey` and `_sectorIndexByKey` NativeHashMap lookup indices because the current DataVault API has no hash-map buffer contract.

## Decision 23 - Latest Physics Compile Wall

Problem: Current validation now fails outside ecology in `Assets/_Project/Scripts/PhysicsApplySystem.cs`: missing `TryResolveVaultBuffer`, `ClearForcePacketBuffer`, `ClearByteBuffer`, `EnsureForcePacketBuffers`, release/swap/clamp helpers, force packet queue fields, validation buffer fields, and BufferID entries for physics force-command lanes.
Solution: Record the external dependency wall and stop before editing physics force-packet ownership from the AI/ecology agent. Re-run focused ecology static scans to prove the macro bridge did not reintroduce hot-path stalls, managed spawn/format paths, local NativeArray/List/Queue allocations, private NativeArray owners, bad ABI pack, or legacy bus/delegate usage.
Rejected Alternatives: Patch `PhysicsApplySystem.cs` from outside the assigned AI/ecology boundary, revert another agent's physics refactor, or claim the earlier build-green result as current.
Scalability potential: No ecology runtime change. Low/Middle/High/Ultra macro-swarm behavior remains implemented through vault handles, stress-halved hydration, SDF gating, and high-tier overkill flags.
Hardware Impact: Latest validation wall time was 31.47 s, or 31,470,000 us of build evidence only. No Unity PlayMode, profiler, Quest/Android, Metal/Mac, Steam Deck, or IL2CPP runtime verification was executed.

## Decision 24 - Vault-Backed Open Address Indexes

Problem: After raw `NativeArray<T>` field eviction, `EcosystemDirector` still owned two persistent `NativeHashMap<long,int>` lookup indices. They were cold-path allocations, but they still violated the stricter stateless/data-sovereignty review.
Solution: Replace both maps with DataVault buffers of explicit Pack=1 `EcosystemIndexEntry` records. Sector and biomass lookups now use bounded open addressing over `EcosystemSectorIndexEntries` and `EcosystemBiomassIndexEntries`; table capacity is power-of-two and twice the configured logical slot count. Burst biomass diffusion consumes the vault index as `NativeArray<EcosystemIndexEntry>` instead of `NativeHashMap`.
Rejected Alternatives: Keep the `NativeHashMap` exception, use linear scans over 512 biomass cells during diffusion, or introduce managed dictionaries.
Scalability potential: Low = compact fixed index buffers and no native hash-map allocator ownership. Middle = same O(1)-style lookup for full SOA hydration/dehydration. High = SDF emergence and high-tier signal flags remain unchanged. Ultra = downstream overkill spends saved stability budget while ecology stays vault-resolved.
Hardware Impact: Expected lookup cost remains micro-scale and bounded; no profiler proof was executed. Memory cost is predictable: 16-byte entries, 2x logical capacity, about 16 KB for default 512 biomass cells and 4 KB for default 128 sectors.

## Decision 25 - Current Build Green Evidence

Problem: The previous validation wall was external, but source and generated obj state changed again. A stale blocked status would now be false.
Solution: Run `dotnet restore Hecton8.Core.csproj /p:UseSharedCompilation=false`, then `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false`. Build succeeded with 0 warnings and 0 errors in 75.17 s. Focused ecology scans found no local native owner fields/allocations, no `Schedule().Complete`, no Unity update loops, no prefab spawn path, no managed formatting, no sequential/Pack=4 ecology DTOs, and no active legacy bus/delegate usage.
Rejected Alternatives: Trust the earlier build wall, claim platform runtime validation without running it, or broaden edits outside the AI/ecology boundary after the build was already green.
Scalability potential: Low/Middle/High/Ultra behavior unchanged from Decision 24; this is validation evidence.
Hardware Impact: Build wall time was 75.17 s, or 75,170,000 us of compile validation only. Quest/Android, Metal/Mac, Steam Deck, PlayMode, profiler, GC monitor, and IL2CPP strip builds remain unexecuted.

## Decision 26 - Player-Build Coefficient I/O Quarantine

Problem: `EcosystemPopulationBalancer` still performed `File.Exists`, `FileInfo`, `FileStream`, `StreamReader.ReadToEnd`, and `JsonUtility.FromJson` for coefficient loading. Even though cold, that is managed allocation and disk I/O on a gameplay service, with direct Steam Deck/MicroSD hitch risk.
Solution: Gate `TryReadCoefficientJson` behind `#if UNITY_EDITOR`. Player/non-editor builds return false and use the sanitized default coefficient already written into the DataVault coefficient lane. Required blackbox dump I/O remains only for fault export.
Rejected Alternatives: Keep runtime JSON for convenience, move JSON parsing into cold tick, or add a managed cache that still allocates and reads disk in player builds.
Scalability potential: Low = no coefficient disk read or managed JSON allocation on toaster/Steam Deck. Middle = default coefficient lane remains deterministic. High/Ultra = runtime visuals still scale through macro-swarm flags and downstream rendering, not JSON reads.
Hardware Impact: Removes a cold-start/player-build I/O hitch class. Exact microseconds saved were not profiler-measured; build evidence only shows compile integrity.

## Decision 27 - Current Build Green After I/O Pass

Problem: Any preprocessor gate can silently break the non-Unity C# project if symbols or references are wrong.
Solution: Re-run `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` after the I/O quarantine. Build succeeded with 0 warnings and 0 errors in 126.92 s. Static ecology scans stayed clean for local native ownership, update loops, prefab spawn, managed formatting, bad ABI pack, and active legacy bus/delegate usage.
Rejected Alternatives: Record the edit without compiling, or claim Steam Deck runtime verification without running a Steam Deck build.
Scalability potential: No behavior change beyond removing player-build JSON I/O; low/middle/high/ultra macro behavior remains as previously recorded.
Hardware Impact: Build wall time was 126.92 s, or 126,920,000 us of compile validation only. Unity PlayMode, Quest/Android, Metal/Mac, Steam Deck, profiler, GC monitor, and IL2CPP strip builds remain unexecuted.

## Decision 28 - ARM/Quest 16-Byte Vault Stride Padding

Problem: `EcosystemPopulationCoefficient`, `EcosystemPopulationCullEvent`, and `EcosystemPopulationFreeSlot` were explicit Pack=1 records but had 52, 88, and 24-byte sizes. That is deterministic in C#, but it is still a bad vault stride for ARM/Quest and GPU-adjacent fixed lanes because adjacent records do not land on a 16-byte boundary.
Solution: Pad the records to 64, 96, and 32 bytes with explicit reserved fields while keeping all existing field offsets stable.
Rejected Alternatives: Leave the odd strides because desktop build accepts them, widen only one record, or reorder fields and risk producer/consumer ABI drift.
Scalability potential: Low = same compact records with safer lane stride on Quest/Android. Middle = unchanged population-balancer math. High = the same ABI supports heavier visual residency without changing ecology data contracts. Ultra = downstream visual overkill can consume stable vault lanes while ecology records remain deterministic.
Hardware Impact: Runtime microsecond savings were not measured; this is crash-class and cache-line discipline. Memory cost increased by 12 bytes per coefficient record, 8 bytes per cull event, and 8 bytes per free-slot record.

## Decision 29 - Current Build Green After ABI Padding

Problem: ABI padding can expose stale assumptions in serializers, vault sizing, or unsafe copies even when field offsets are preserved.
Solution: Re-run `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` and focused ecology scans after the padding pass. Build succeeded with 0 warnings and 0 errors in 76.90 s. Static scans found no old 52/88/24 population record sizes, no sequential/Pack=4 ecology DTOs, no standard Unity update loop, no prefab spawn path, no managed formatting, no forced `Schedule().Complete`, and no local NativeArray/List/Queue/HashMap allocation or private owner fields in the bridge files. A follow-up domain-wide inventory found the assigned `Assets/_Project/Scripts/AI/Ecosystem/` domain contains one source file, and the same forbidden-pattern scans were clean there.
Rejected Alternatives: Record ABI edits without compile proof, or claim Quest/Android/Metal/Steam Deck runtime validation from a desktop C# build.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; only the vault record stride was hardened.
Hardware Impact: Build wall time was 76.90 s, or 76,900,000 us of compile validation only. Unity PlayMode, Quest/Android, Metal/Mac, Steam Deck, profiler, GC monitor, and IL2CPP strip builds remain unexecuted.

## Decision 30 - Agent-ID Population Dump Path

Problem: `EcosystemPopulationBalancer` used `Docs/AgentLogs/Dump_ECOSYSTEM_POPULATION_BALANCER.bin` while the batch and blackbox protocol require `Docs/AgentLogs/Dump_ECOSYSTEM_MIGRATION_LINK.bin` for this agent. Faults from the population bridge could be written under a private subsystem name and missed during post-mortem review.
Solution: Change the population-balancer dump constant to `Docs/AgentLogs/Dump_ECOSYSTEM_MIGRATION_LINK.bin`, matching the macro-swarm bridge dump target.
Rejected Alternatives: Keep two dump names for the same agent, add a second duplicate dump file, or rely on chat/report text to reconcile fault ownership.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is failure-analysis routing, not simulation math.
Hardware Impact: 0 us runtime on non-fault frames. Fault-path file name resolution is unchanged except for the target string.

## Decision 31 - Current-Disk Player I/O Guard Repair

Problem: Current disk showed `TryReadCoefficientJson` no longer had the `#if UNITY_EDITOR` guard even though the status log said player-build coefficient I/O had been quarantined. That reopened `FileStream`, `StreamReader.ReadToEnd`, and `JsonUtility.FromJson` for player builds.
Solution: Restore the `#if UNITY_EDITOR` / `#else return false` guard around the coefficient JSON path. Player/non-editor builds again use the sanitized default/vault coefficient lane and skip disk JSON reads.
Rejected Alternatives: Trust stale documentation, keep player-build JSON for convenience, or add a managed cache that still allocates.
Scalability potential: Low = no coefficient file read or JSON allocation on toaster/Steam Deck. Middle = deterministic default coefficient lane. High/Ultra = visual overkill remains driven through macro-swarm/SDF/signal flags, not runtime balance-file I/O.
Hardware Impact: Removes a player-build I/O hitch class. Exact microseconds saved were not profiler-measured.

## Decision 32 - Fixed-Capacity Blackbox Dump Export

Problem: `DumpBlackBox` wrote `min(writtenCount, capacity)` records. If a fault happened before the ring had wrapped, the exported artifact could contain fewer than the mandated fixed 300-frame buffer.
Solution: Bump the dump format to v3 and write `dumpCount = capacity` for every created telemetry ring. The ordering still starts at zero until the ring fills, then wraps from the current cursor.
Rejected Alternatives: Keep shortened early-session dumps, write a second manifest-only count, or claim 300-frame blackbox compliance from an undersized file.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; only fault export completeness changes.
Hardware Impact: 0 us on non-fault frames. Fault-path binary output grows to the fixed ring capacity; with the 64-byte telemetry entry layout and 300-entry mandated capacity, the payload is about 19.2 KB plus header.

## Decision 33 - Current Build Green After Dump/I/O Repairs

Problem: After the dump-path repair and current-disk I/O guard repair, the first two build attempts were blocked by unrelated concurrent gameplay edits: player/tether/interaction errors after 60.12 s, then missing `MethodImpl` imports in `HectonPlayerMovement.cs` after 36.68 s. A later successful build emitted one copy-retry warning because a DLL was temporarily locked.
Solution: Re-run `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /p:UseSharedCompilation=false` after the external wall moved. Final build succeeded with 0 warnings and 0 errors in 142.24 s. Focused scans confirmed the JSON coefficient read path is under `#if UNITY_EDITOR`, no private population-balancer dump target remains, the dump writes full capacity, and the assigned ecology domain plus bridge files remain clean for update loops, prefab spawns, managed formatting, forced `Schedule().Complete`, local native collection ownership, bad ABI sizes, and legacy bus/delegate patterns.
Rejected Alternatives: Patch player movement/tether/interaction domains from the ecology agent, ignore the stale missing I/O guard, or leave the status as build-blocked after a clean rerun.
Scalability potential: No ecology simulation behavior change beyond restored I/O quarantine, correct dump routing, and complete fixed-size fault evidence.
Hardware Impact: Latest clean build wall time was 142.24 s, or 142,240,000 us of compile validation only. Unity PlayMode, Quest/Android, Metal/Mac, Steam Deck, profiler, GC monitor, and IL2CPP strip builds remain unexecuted.
