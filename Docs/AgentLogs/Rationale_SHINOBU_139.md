# Rationale_SHINOBU_139

Agent: SHINOBU_139
Domain: Echelon 2 World Generation / Procedural Coral Growth Engine
Status: PENDING VERIFICATION

## Decision Journal

### D00: Initial Architecture Boundary

Problem: Procedural coral must be generated as pure native data and uploaded as matrices; GameObject hierarchy generation would violate rendering and scene-graph constraints.

Solution: Keep the implementation inside Echelon 2 World Generation. Use integer L-System opcodes, explicit-layout DTOs, Burst jobs, AUP-relative matrix extraction, and Vault-facing adapter interfaces instead of direct cross-domain dependencies.

Rejected Alternatives: Unity GameObjects, recursion with strings, managed Lists, mesh-collider generation, and direct calls into absent Bioluminescence/World Streaming concrete systems. These either allocate, create coupling, or block on other agents.

Scalability potential: Low uses shallow depth, sparse tips, coarse collision proxies. Middle increases branch density and SDF avoidance. High raises depth and bioluminescent tip count. Ultra spends saved CPU on denser render matrices and richer shader sway data.

Hardware Impact: Avoiding GameObjects and string expansion prevents scene-hierarchy load spikes and managed GC on i3/MX350. Expected gain is load-time and GC avoidance, not runtime proof until Unity profiling exists.

### D01: Rule Source Failover

Problem: `coral_growth_rules.h8bin` is absent from the current project tree, but generation cannot block on content delivery.

Solution: Implemented `TryFindLegacyRuleBinary()` for direct StreamingAssets lookup plus project-tree reconnaissance, then deterministic `GenerateEmergencyMockCoralRules()` using integer opcodes only.

Rejected Alternatives: Throwing on missing binary or embedding string-based L-System axioms. Missing content would stall downstream agents; strings violate the assignment.

Scalability potential: Low gets three compact rules with shallow generated density. Middle/High/Ultra use the same rules but the continuous quality weight raises depth, instructions, tips, pulses, and instance density.

Hardware Impact: On i3/MX350 the fallback avoids repeated file probes after Vault hydration and keeps rule expansion in native buffers. Estimated cold-path saving after hydration: ~25 us versus retry-based loading.

### D02: Domain-Safe Spawner Purge

Problem: No exact `CoralSpawner.cs` or `ReefGenerator.cs` exists; a broad world scatter director contains existing `Instantiate` calls but owns more than coral.

Solution: Added a new coral-only pipeline with no GameObject instantiation and left the broad scatter system untouched.

Rejected Alternatives: Deleting or rewriting `WorldProceduralScatterDirector.cs`. That would cross the domain boundary and risk unrelated world placement systems.

Scalability potential: Low writes fewer matrices; Middle/High/Ultra write progressively denser GPU instance buffers without scene hierarchy growth.

Hardware Impact: Avoiding per-branch GameObjects prevents Transform and renderer registration cost. Estimate: ~3-15 us saved per 100 coral branches depending device and editor/runtime state.

### D03: Explicit DTO Layout

Problem: Native jobs and ARM64 cache behavior require predictable struct layout; DTO properties risk copy mutation failures.

Solution: All coral DTOs are public-field structs. `CoralBranchDTO` is exact 128 bytes with the requested offsets and explicit tail padding. Editor validator checks size and offsets.

Rejected Alternatives: Sequential layout, auto-properties, and managed wrapper models. These are less auditable and break the CS1612 mandate.

Scalability potential: Low consumes the same compact DTO with fewer live entries; Ultra fills more of the Vault but keeps a fixed stride for GPU upload.

Hardware Impact: Fixed 128-byte branch entries keep matrix extraction linear and predictable. Estimated saving: ~2-4 us per 4k branch scan versus mixed/unaligned payloads.

### D04: Mock Sector Boundary

Problem: The coral engine needs sector seed/root data, but direct dependency on a not-guaranteed streaming implementation would block parallel agents.

Solution: `MockSectorTriggerJob` writes `CoralSectorTriggerDTO` from root AUP and world seed. It uses `SystemID.WorldStreaming` Vault ownership but no concrete streaming type.

Rejected Alternatives: Polling scene objects or direct streaming manager calls. Both couple to absent or mutable systems.

Scalability potential: Low/Middle/High/Ultra all use identical deterministic sector identity; quality only changes local expansion density.

Hardware Impact: Test and standalone generation avoid manager lookup work. Estimated saving: ~10 us per generation dispatch in isolated harnesses.

### D05: L-System Scratch Ownership

Problem: The assignment asks for integer L-System expansion and no strings, while project memory sovereignty requires buffers to come from `GlobalDataVault`.

Solution: `EvaluateCoralLSystemJob` expands integer opcodes through two fixed-capacity Vault scratch buffers and counters, giving NativeList-style append/clear semantics without runtime allocation.

Rejected Alternatives: Allocating `NativeList<uint>` inside every generation dispatch. The current Vault API exposes `NativeArray<T>` handles, so direct `NativeList` ownership would violate Vault sovereignty or require a new memory contract outside this domain.

Scalability potential: Low caps expansion at small instruction counts. Middle increases branch density. High and Ultra consume more of the same fixed scratch capacity without changing the algorithm.

Hardware Impact: Avoids allocator and resize cost on weak CPUs. Estimated generation saving versus per-sector dynamic list allocation: ~8-20 us plus zero GC risk.

### D06: Collision As Visual Constraint

Problem: Coral cannot grow through itself or the seabed, but full physics/voxel coupling would exceed the frame-time budget and create dependency walls.

Solution: `ConstrainCoralGrowthJob` uses a fake seabed SDF clearance, bounded local overlap probes, deterministic offset/prune decisions, and a staged spatial cell buffer.

Rejected Alternatives: Mesh colliders, Unity Physics queries, or direct Voxel SDF dependency. Those are slower and couple to other agents' unfinished domains.

Scalability potential: Low prunes aggressively with a short probe window. Middle offsets more branches. High/Ultra spends saved cycles on denser tips and better collision-preserved silhouettes.

Hardware Impact: Bounded O(n*k) probes keep i3/MX350 cost contained. Estimated cost: ~30-75 us for 4k branches with no per-frame physics calls.

### D07: Sway as Shader Lie

Problem: Per-frame CPU matrix updates for current sway would destroy instancing throughput.

Solution: Keep matrices stable and publish `CoralGpuSwayDTO` scalar fields for shader-side current sway.

Rejected Alternatives: Updating every branch matrix each frame or adding transform bones. Both scale linearly on CPU and fight GPU instancing.

Scalability potential: Low uses small amplitude and sparse matrices. Middle/High/Ultra increase shader deformation amplitude and instance density without CPU animation work.

Hardware Impact: Avoids roughly ~65-130 us per 4k instances per frame on low-end CPUs, shifting the cost to parallel GPU vertex work.

### D08: Camera-Relative Matrix Extraction

Problem: AUP coordinates exceed safe float precision for direct GPU matrix upload.

Solution: `ExtractCoralRenderMatricesJob` subtracts `CameraAUP` while values are still `double3`, then casts to float and writes Vault matrices with `UnsafeUtility.MemCpy`.

Rejected Alternatives: Global float world matrices and GameObject renderer placement. Both are precision-unsafe at AUP scale.

Scalability potential: Low filters by distance and density. Middle/High/Ultra raise matrix count and publish larger draw args using the same camera-relative contract.

Hardware Impact: Linear copy into a contiguous matrix buffer is cache-friendly. Estimated cost: ~20-50 us per 4k visible branches before graphics upload.

### D09: Data-Only Bioluminescence

Problem: Coral tips need synchronized glow pulses, but VFX concrete classes are outside this assembly and may not exist at compile time for this domain.

Solution: Defined a coral `SyncPulseDTO` with the same compact data shape and wrote `InjectBioluminescenceNodesJob` to fill a Vault pulse buffer from tip branches.

Rejected Alternatives: Adding lights, particles, or direct Bioluminescence manager calls. Those add scene objects or cross-domain compile dependencies.

Scalability potential: Low emits sparse pulses. Middle raises tip density. High/Ultra spends saved CPU/GPU budget on richer shader/VFX consumers without changing coral generation.

Hardware Impact: Data-only pulse staging costs ~8-25 us for 1k candidate tips and avoids component/object work on low-end hardware.

### D10: Persist Seed, Not Branches

Problem: A giant reef can contain thousands of branch matrices, but persistence should not serialize generated data.

Solution: Added `CoralSectorSaveDTO` and `BuildSectorSaveRecord()`; saved state is sector hash, seed, rule payload hash, and flags only.

Rejected Alternatives: Saving `CoralBranchDTO` arrays or render matrices. That bloats disk IO and creates version-fragile payloads.

Scalability potential: Low through Ultra regenerate from deterministic seed and quality settings; save size remains fixed.

Hardware Impact: Avoids MB-scale reads/writes for large reefs. On i3/MX350 storage-constrained devices this prevents save/load spikes.

### D11: Proxy Collisions Only

Problem: Collision representation is needed for thick coral trunks, but runtime collider construction would create component churn.

Solution: `StageCollisionProxiesJob` writes `CapsuleColliderDTO` records for low-depth branches only, with quality controlling proxy depth continuously.

Rejected Alternatives: Mesh colliders, GameObject capsule colliders, and per-branch physics queries.

Scalability potential: Low emits trunk-only proxies. Middle includes major branches. High/Ultra can expose more proxy depth if physics budget allows.

Hardware Impact: Data proxy staging avoids scene/component work; estimated saving is >100 us per proxy batch compared with constructing Unity collider objects.

### D12: Deterministic State Fence

Problem: Rollback/netcode and reproducible world paging need the same input seed to regenerate the same reef.

Solution: Every Burst job uses `FloatMode.Deterministic`; procedural variation uses hash functions from sector hash, world seed, stable branch id, and instruction index.

Rejected Alternatives: `UnityEngine.Random`, `Unity.Mathematics.Random` with frame-time seeds, or time-based sway baked into matrices.

Scalability potential: Quality changes are explicit inputs. Low/Middle/High/Ultra remain deterministic for a given quality value.

Hardware Impact: Determinism is not a raw speed gain; it prevents replay divergence and the expensive postmortem path it causes.
