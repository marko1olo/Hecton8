# Rationale_ECOSYSTEM_MIGRATION_LINK

State: CORE COMPLETE / BUILD BLOCKED BY DEPENDENCY

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
