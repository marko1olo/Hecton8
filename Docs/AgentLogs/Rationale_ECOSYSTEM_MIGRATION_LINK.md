# Rationale_ECOSYSTEM_MIGRATION_LINK

State: PENDING VERIFICATION

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
