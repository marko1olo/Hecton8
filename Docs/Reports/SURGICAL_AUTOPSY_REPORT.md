# SURGICAL AUTOPSY REPORT: THE MONOLITHS

**CLASSIFICATION:** STRICT READ-ONLY RECONNAISSANCE
**AUTHORITY:** Principal Technical Director / Chief Systems Architect
**PROTOCOL:** ZERO-SYCOPHANCY, FACTS-ONLY

The following autopsy dissects three of the most critical structural bottlenecks identified in the codebase. These classes represent the pinnacle of architectural bloat, violating the "Meat vs. Bones" doctrine by tightly coupling high-level gameplay rules with low-level Unity/Physics state.

---

## 1. `Assets/_Project/Scripts/HectonPlayerMovement.cs`

**REAL SIZE / ESSENCE:**
- **Size:** 14,643 Lines (810 KB)
- **Composition:** 1 Class, 593 Methods, 520 Fields, 14 Interface Implementations.
- **Essence:** What should theoretically be a pure Rigidbody state-machine and force accumulator has mutated into a God Class. 
- **The Bloat:** It is heavily infected with gameplay logic that belongs in separate systems. 
  - Tracks `CinematicFocusTelemetryEntry` (96-byte explicit layout struct) entirely unrelated to movement physics.
  - Hardcoded dependencies on `ApplyFaunaHypnosisPull`, `ApplyParasiteLatchInfluence`, and `ApplyExternalThermalUpdraft`.
  - Violates single-responsibility completely by injecting narrative/gameplay specifics directly into the movement solver.

**BLOAT SCORE: 98/100** (CRITICAL HYPERTROPHY)
- Over 90% of this file is non-movement state, wrapper bureaucracy, or isolated mechanic logic forced into the locomotion pipeline.

**VERDICT: [SPLIT] and [TRIM]**
- The rigid body solver and input translator must be aggressively **[SPLIT]** into a pure `PlayerKinematicsSolver` (The Bones).
- The gameplay reactions (Parasites, Hypnosis, Thermal) must be decoupled into independent modules responding to `SignalBus<T>` or applying external forces via an `IForceApplier` interface. 
- The 520 fields must be gutted. State should be derived from the `DataVault` or injected components, not hoarded in a single 14k line script.

---

## 2. `Assets/_Project/Scripts/World/EcosystemDirector.cs`

**REAL SIZE / ESSENCE:**
- **Size:** 8,744 Lines
- **Composition:** 2 Classes/Structs, 353 Methods.
- **Essence:** A monolithic spatial manager that attempts to simultaneously govern spawning budgets, biome gradients, and raw byte-level logic (`NativeArray<byte> heatmapR8`). 
- **The Bloat:** The script contains low-level buffer management (`TryAcquireWriteLock`, `TryResolveReadOnly`) mixed right beside high-level semantic rules (`ResolveFaunaMood`, `RefundSpawnCredit(CreatureArchetypeData...)`). 
- It attempts to do Data-Oriented Design (Burst/Jobs via NativeArrays) but wraps it in massive amounts of OOP abstraction and state management.

**BLOAT SCORE: 85/100** (STRUCTURAL SCHIZOPHRENIA)
- It suffers from severe identity crisis. It's half low-level native memory allocator and half high-level gameplay director. 

**VERDICT: [SPLIT]**
- The low-level `NativeArray` read/write locks and buffer management must be stripped out into a pure `EcosystemDataRegistry` (The Bones).
- The high-level credit economy (`ResolveSpawnCreditSelectionWeight`) belongs in an `EcosystemEconomySystem` (The Meat) that merely reads from the Data Registry without caring about memory locks.

---

## 3. `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs`

**REAL SIZE / ESSENCE:**
- **Size:** 12,895 Lines
- **Composition:** 14 Classes/Structs, 0 Methods, 0 Switch Statements.
- **Essence:** A pure static hash cache for localization keys. 
- **The Bloat:** It is a 12,000+ line auto-generated data dump baked directly into C# syntax. Loading this massive file causes excessive parsing overhead in the editor and pollutes the IDE's symbol index. While it has 0 complex methods, baking pure data into C# structs at this scale is an anti-pattern.

**BLOAT SCORE: 70/100** (STATIC DATA OBESITY)
- It contains no logic, but the sheer volume of C# symbols generated for string hashes is entirely unnecessary for runtime performance if properly serialized into binary or a lightweight lookup table.

**VERDICT: [TRIM] / [DELETE] (RE-ARCHITECT)**
- We need to stop generating 12,000 lines of C# for LocHashes. 
- **[TRIM]** the file down to only explicitly referenced/critical engine-level strings. 
- The remainder should be packed into a binary dictionary or `ScriptableObject` loaded at runtime, removing this massive dead-weight from the compiler.

---

### SURGICAL SUMMARY
The core problem is evident: **We are using Scripts as Databases.**
- `HectonPlayerMovement` is a database of gameplay interactions.
- `EcosystemDirector` is a database of memory buffers mixed with logic.
- `H8LocHashes` is a literal string database disguised as C#.

Next phase must be targeted decoupling. No refactoring loops on the monoliths themselves. We extract the "Bones", wire them to the DataVault, and leave the "Meat" to specialized systems.
