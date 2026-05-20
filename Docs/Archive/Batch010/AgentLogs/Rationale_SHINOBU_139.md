# Rationale_SHINOBU_139

Agent: SHINOBU_139
Domain: Echelon 2 World Generation / Procedural Coral Growth Engine
Status: STATIC SOURCE VERIFIED / COMPILE PENDING

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

Solution: Every Burst job uses `FloatMode.Deterministic`; procedural variation uses hash functions plus `Unity.Mathematics.Random` seeded from deterministic uints derived from sector hash, world seed, stable branch id, and instruction index.

Rejected Alternatives: `UnityEngine.Random`, `Unity.Mathematics.Random` with unstable frame-time seeds, discovery-frame-dependent reef layout, or time-based sway baked into matrices.

Scalability potential: Quality changes are explicit inputs. Low/Middle/High/Ultra remain deterministic for a given quality value.

Hardware Impact: Determinism is not a raw speed gain; it prevents replay divergence and the expensive postmortem path it causes.

### D13: HZB Before Matrix Emission

Problem: Sending every generated branch matrix to the GPU wastes vertex work when terrain already occludes the reef.

Solution: Added Vault-owned `CoralHzbTileDTO` tiles and HZB testing inside `ExtractCoralRenderMatricesJob` before `UnsafeUtility.MemCpy`.

Rejected Alternatives: Blind BRG submission and per-object renderer culling. Blind submission burns GPU vertex cost; renderer culling requires GameObjects.

Scalability potential: Low benefits most because occluded branches are skipped before matrix upload. Middle/High/Ultra can draw denser reefs while still dropping hidden branches.

Hardware Impact: HZB check is O(n) with simple projected tile lookup. It trades a few scalar ops for avoided vertex shader work; expected savings depend on occlusion density, not claimed as measured.

### D14: Black Box Ring and Fault Dump

Problem: A procedural solver failure without recent state history is not debuggable.

Solution: The Vault owns a 300-entry `CoralGenerationTelemetryEntry` ring and cursor. Generation writes root AUP, counts, depth, hash, faults, tips, matrix count, and a Burst microsecond slot. `TryRecordMeasuredBurstTimeUs()` lets the dispatcher overwrite the placeholder with measured scheduler timing. Dump code writes `Dump_CORAL_ARCHITECT.bin` and agent-specific dump on fault.

Rejected Alternatives: Editor console logs and managed string telemetry. Those are not fixed-size, not replay-friendly, and not valid for hot paths.

Scalability potential: Same fixed 300-frame history across Low/Middle/High/Ultra; telemetry does not grow with reef density.

Hardware Impact: Ring write is ~2-5 us per generation event; dump is cold fault-path IO only.

### D15: Human Control Without Runtime Debt

Problem: Designers need tuning control for grammar and quality without recompiling C#.

Solution: Added UI Toolkit tuner plus byte-level CSV ingestion into Vault scratch/rule DTOs. Runtime generation reads unmanaged rules only.

Rejected Alternatives: ScriptableObjects in hot paths, `string.Split`, LINQ, and per-frame file polling.

Scalability potential: Human-edited quality and max-depth fields directly control Low/Middle/High/Ultra behavior through continuous curves.

Hardware Impact: Editor-only controls add zero runtime cost. CSV parsing is cold/slow-tick and bounded by the 32KB scratch buffer.

### D16: Self-Audit as Code, Not Claim

Problem: The architecture needs a repeatable local audit for layout, capacity, NaN, and Vault readiness.

Solution: `CoralSelfAuditJob` runs in the job chain and `TryRunArchitectureAudit()` checks DTO sizes, branch caps, Vault readiness, and finite branch state.

Rejected Alternatives: Report-only verification. It creates no runtime evidence and misses future regressions.

Scalability potential: Audit work is bounded. Low scans fewer live branches; higher tiers can still cap scans and branch count.

Hardware Impact: Static architecture scan is ~10-40 us depending branch count and cache state; the job-chain audit is bounded to the generated branch count.

### D17: Count-Window Ownership Beats Blanket Clears

Problem: `EvaluateCoralLSystemJob` still performed full-buffer default writes over `Branches` and `DebugSegments`, and downstream jobs cleared full pulse/proxy/spatial buffers before writing the current logical window. That contradicts the zero-init bypass mandate: the Vault already allocates uninitialized memory and the pipeline has explicit logical counts.

Solution: Removed the blanket clears. `Counters.BranchCount` is the authority for branches/debug segments, `Counters.SpatialCellCount` for spatial cells, `Counters.SyncPulseCount` for pulse records, and `Counters.CollisionProxyCount` for proxy records. Data beyond those counts is stale by contract and must not be consumed, matching the project's NonAlloc buffer window pattern.

Rejected Alternatives: Keeping full clears for psychological safety was rejected because it writes cache lines the current generation never reads. Adding separate managed lists or runtime NativeLists was rejected because Vault-owned fixed buffers already provide the logical count boundary.

Scalability potential: Low tiers gain the most because shallow reefs avoid paying for unused high-capacity buffers. Middle/High/Ultra can keep large capacity for richer reefs without turning every generation into a capacity-sized memset.

Hardware Impact: Removes up to 4096 branch DTO default writes, 4096 debug DTO default writes, 2048 spatial DTO default writes, 1024 pulse DTO default writes, and 1024 proxy DTO default writes per generation. Static estimate: ~55-220 us saved depending cache warmth and target CPU; profiler proof remains pending.

### D18: GPU Upload No-Grow Default

Problem: `ProceduralCoralGpuUploadDispatcher.UploadFromVault()` could allocate or resize `GraphicsBuffer` resources if called before prewarm or with a larger requested instance count. That is not a managed GC allocation, but it is still a gameplay allocation surface and can hitch rendering.

Solution: `UploadFromVault()` now defaults to `allowAllocation=false`. It succeeds only when double-buffered matrix/args buffers are already valid and large enough. Cold boot or editor tooling can still call `EnsureGraphicsResources()` explicitly, or pass `allowAllocation=true` in an intentional prewarm/import path.

Rejected Alternatives: Keeping implicit growth inside upload was rejected because it hides allocation behind what looks like a copy operation. Allocating a new buffer each draw was rejected because it would destroy GPU upload cadence and VRAM stability.

Scalability potential: Low/Middle/High/Ultra can prewarm the same path at different capacities, but the per-frame upload cadence remains no-grow. Ultra can reserve more capacity without making low-tier runtime pay resize risk.

Hardware Impact: Removes surprise render-thread/driver allocation from upload cadence. Microsecond savings are platform-driver dependent and not claimed without profiler/Frame Debugger proof.

### D19: Fail-Closed Count Dependency

Problem: Once uninitialized tail slots are valid stale data, any fallback that scans `Branches.Length` when `Counters` is missing can read undefined payload and emit invalid matrices, pulses, proxies, or audit results.

Solution: Count-consuming jobs now require `Counters` to be created. If the count buffer is absent, they return without scanning capacity. This preserves the zero-init contract and makes missing Vault wiring visible as no output instead of corrupt output.

Rejected Alternatives: Fallback scanning the full buffer was rejected because it converts stale capacity into apparent live coral. Reintroducing blanket clears was rejected because it pays per-generation capacity cost to hide missing dependency wiring.

Scalability potential: Low tiers keep shallow valid windows without paying for high-tier capacity. Ultra can reserve high capacity and still only process the current logical window.

Hardware Impact: Avoids undefined full-capacity scans under miswired conditions and removes additional spatial/proxy clear passes. Runtime microsecond proof remains pending.

### D20: GPU Lock Lifetime and Sway Ownership Proof

Problem: `GraphicsBuffer.LockBufferForWrite` produced a mapped range and then relied on a straight-line unlock. In editor/runtime debug paths, an exception during pointer acquisition, memcpy, or indirect-args staging could leave the buffer locked. `CoralGpuSwayDTO` also exposed a `SectorHash` field but the extraction job only wrote `StateHash`, leaving a zero owner fact for Vault/debug consumers.

Solution: Wrapped matrix and indirect-args lock ranges in `try/finally` unlock guards. Captured the first live branch's `SectorHash` from the logical `[0, BranchCount)` window with an explicit boolean capture flag, not a `0u` sentinel, and published it into the Vault `CoralGpuSwayDTO` together with the existing state hash and quality scalars. The current shader global route still publishes only float4 sway vectors.

Rejected Alternatives: Ignoring lock lifetime was rejected because it creates driver-side state corruption risk after a managed exception. Filling sector hash from a global singleton or streaming manager was rejected because coral already owns the generated branch window and must not create a sibling-domain dependency.

Scalability potential: Low/Middle/High/Ultra keep the same no-grow upload path; higher tiers only increase prewarmed capacity and valid matrix count. The sway owner hash remains constant-cost metadata.

Hardware Impact: No claimed hot-path microsecond gain; this is a stability fix. It prevents a rare locked-buffer failure mode and avoids a downstream lookup or missing-owner branch in shader/debug integration.

### D21: Editor Facade Project Root Cache

Problem: The UI Toolkit tuner facade called `Application.dataPath.Substring(...)` on CSV poll and button paths. This is editor-only, but it still creates avoidable managed string churn in the exact facade requested by the prompt.

Solution: Cached the project root string per `ProceduralCoralTunerWindow` instance and reused it for CSV/H8BIN/dump calls. The telemetry readout continues to rebuild only when the telemetry hash changes.

Rejected Alternatives: Leaving repeated substring allocation was rejected because the facade is part of the assignment and should not normalize sloppy editor churn. Replacing UI Toolkit labels with a custom native text control was rejected as disproportionate and outside current project UI patterns.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Editor tuning remains predictable while designers adjust continuous quality curves.

Hardware Impact: No runtime frame impact. Editor-only allocation reduction: one cached string per window instead of one substring per poll/button path.

### D22: Offset Proof for Counter Windows and GPU Sway DTO

Problem: The editor layout validator checked `CoralPaddedCounterDTO` and `CoralGpuSwayDTO` sizes but did not explicitly assert offsets for the newly important logical-count fields or sway owner fields. A future field reorder could keep the size at 64 bytes while silently breaking valid-window consumers or shader/debug metadata.

Solution: Added offset checks for `BranchCount`, `CollisionProxyCount`, `RenderMatrixCount`, `SyncPulseCount`, `SpatialCellCount`, `FlowAndAmplitude`, `BoundsAndDensity`, `FaultAndFrame`, `SectorHash`, and `StateHash`.

Rejected Alternatives: Relying on `[StructLayout(LayoutKind.Explicit, Size=64)]` alone was rejected because explicit size does not prove the field offsets that downstream consumers depend on.

Scalability potential: Runtime scalability is unchanged. Proof quality improves across all device classes because the editor import gate can catch ARM64 layout drift before a Quest build.

Hardware Impact: Runtime cost is zero. Editor validation cost is reflection-only at domain load/menu validation.

### D23: Transactional Rule Hydration

Problem: `ParseCsvRules()` and `ParseBinaryRules()` cleared the live `Rules` table before proving that the incoming file contained valid rule records. A corrupt CSV/H8BIN could return `loaded <= 0` after destroying the previous deterministic grammar, forcing `FaultNoRules` on the next generation.

Solution: Both parsers now stage parsed rules into a 16-record `stackalloc` scratch table and commit into the Vault rule buffer only after `parsedCount > 0`. The commit writes the active records and a single default sentinel after the active window so `ResolveActiveRuleCount()` stops without scanning stale rules.

Rejected Alternatives: Allocating a temporary `NativeArray<CoralLSystemRuleDTO>` was rejected because the parser is a Vault-owned cold path and does not need another allocation. Clearing the live table first was rejected because it violates fail-closed content hydration.

Scalability potential: Low/Middle/High/Ultra all retain the last valid rule set on bad tuning input. Designers can edit CSV without destabilizing running sectors.

Hardware Impact: Stack staging is 16 * 64 bytes = 1024 bytes on the stack in a cold/editor path. Runtime generation cost is unchanged; failure recovery avoids a no-rule generation pass.

### D24: Boot-Wide Memset Purge

Problem: `HydrateDefaultsIfNeeded()` still called `ClearArray()` over large uninitialized buffers on first hydration: instruction scratch, branches, turtle stack, spatial cells, render matrices, collision proxies, sync pulses, debug segments, and CSV scratch. That contradicted the zero-init bypass architecture already enforced during generation.

Solution: First hydration now writes only small sentinel records (`IndirectArgs[0]`, `SectorTriggers[0]`, `TelemetryCursor[0]`, `GpuSway[0]`, `SelfAudit[0]`, `Counters[0]`) plus fallback rules and default tuning. Large buffers remain uninitialized and are governed by logical counts.

Rejected Alternatives: Keeping cold boot clears was rejected because it preserves an expensive habit under the weaker label of "initialization safety." Clearing full telemetry was rejected as unnecessary because cursor/count ownership defines the valid window.

Scalability potential: Low tiers no longer pay to clear high-tier capacity on boot. Ultra can reserve large buffers for reef overkill without forcing startup memset across the whole lane.

Hardware Impact: Removes cold memclear of up to scratch A/B, branch, turtle, spatial, matrix, proxy, pulse, debug, and CSV buffers. Exact startup microseconds are platform dependent and not claimed without profiler proof.

### D25: Single-Read CSV Hot Reload

Problem: `TryPollCsvRules()` read `coral_lsystem_rules.csv` into Vault scratch to hash it, then called `TryLoadCsvRules()`, which resolved and read the same file again. That doubled cold IO and created a race window where the committed payload might not match the hashed payload.

Solution: Added `TryCommitCsvRules()` and routed both explicit load and poll paths through it. Poll now reads the file once into Vault scratch, hashes that exact byte window, and commits that same byte window if the payload hash changed.

Rejected Alternatives: Keeping the double-read path was rejected because it violates one fact -> one route -> one proof. Adding managed file caches was rejected because the Vault scratch buffer already owns the byte window.

Scalability potential: Runtime generation unchanged. Editor/slow-tick tuning remains stable from weak devices to ultra because content commits are atomic relative to the scratch bytes read.

Hardware Impact: Saves one CSV file open/read on each changed poll. Exact microseconds depend on storage and file length; no runtime frame claim.

### D26: Debug Gizmo Count Window Over Hash Sentinel

Problem: `ProceduralCoralDebugGizmo` skipped debug segments when `SectorHash == 0u`. A zero sector hash is unlikely but valid under folded hash math, and the count-window architecture already defines which debug slots are live.

Solution: Removed the hash sentinel check. The gizmo now trusts `Counters.BranchCount` clamped to `DebugSegments.Length`, matching the runtime valid-window contract.

Rejected Alternatives: Keeping `0u` as an invalid sentinel was rejected because it creates a hidden correctness hole for one legal sector. Adding another validity flag was rejected because `BranchCount` is already the owner-local truth.

Scalability potential: Runtime unaffected. Editor debug remains accurate for all sector hash values.

Hardware Impact: Runtime 0. Editor removes one branch per drawn segment.

### D27: Cold Self-Audit Sector Ownership

Problem: `CoralSelfAuditJob` wrote a sector hash into `CoralSelfAuditResultDTO`, but the cold `TryRunArchitectureAudit()` path left `SectorHash` at zero. That made the two audit routes disagree and weakened one fact -> one owner -> one proof.

Solution: The cold audit now captures the first live branch sector hash with a boolean capture flag and publishes it to `CoralSelfAuditResultDTO.SectorHash`.

Rejected Alternatives: Leaving the field zero was rejected because zero may be a valid sector hash and cannot double as "missing." Pulling the sector from a streaming service was rejected because branches already carry the owner-local sector fact.

Scalability potential: Runtime scalability unchanged. Audit records remain comparable across low and ultra density runs.

Hardware Impact: One boolean branch on the cold audit scan; runtime generation unaffected.

### D28: Zero Is a Valid Quality Weight

Problem: `EvaluateCoralLSystemJob` selected trigger quality only when `trigger.GlobalQualityWeight > 0f`. That made `0.0f` impossible to express from a valid trigger, even though the global continuum explicitly defines 0.0 as Minimum Survival.

Solution: Quality selection now uses `trigger.Flags & 1u` to determine whether the trigger payload is initialized. A flagged trigger can carry any saturated value from 0.0 to 1.0.

Rejected Alternatives: Keeping `> 0f` was rejected because it turns a continuous control into a hidden binary fallback. Adding a second bool field to the trigger DTO was rejected because `Flags` already exists in the 64-byte layout.

Scalability potential: Low-tier/thermal-throttle paths can now deliberately request exact 0.0 without being promoted to tuning default. Middle/High/Ultra remain unchanged.

Hardware Impact: Runtime cost is one bit test. It restores the cheapest math path when quality reaches zero.

### D29: Cold Audit Utilization Uses Logical Window

Problem: The cold architecture audit calculated branch utilization as live branches divided by full `Branches.Length`, while the Burst self-audit uses live divided by logical `BranchCount`. This made shallow/low-quality reefs look underutilized just because the Vault reserve capacity was high.

Solution: `TryRunArchitectureAudit()` now divides by the logical `branchCount` from `Counters[0].BranchCount`, clamped earlier to capacity for scanning.

Rejected Alternatives: Capacity-based utilization was rejected because capacity is a reservation fact, not current reef density. Adding another metric was rejected because the existing field should match the job-chain audit.

Scalability potential: Low-tier reefs now report honest utilization against their intended branch window. Ultra density still reports against its larger generated window.

Hardware Impact: Runtime generation 0. Cold audit math changes one denominator.

### D30: Effective Quality Is a Counter-Owned Fact

Problem: After fixing `EvaluateCoralLSystemJob` to accept exact trigger quality `0.0f`, downstream jobs still recomputed quality from `Tuning[0].GlobalQualityWeight`. A sector trigger could therefore generate a minimum-survival reef but render/pulse/proxy it as the tuning default, violating one fact -> one owner -> one route.

Solution: Replaced the last 4-byte counter padding slot with `CoralPaddedCounterDTO.EffectiveQualityWeight` at offset 60. `MockSectorTriggerJob` seeds it, `EvaluateCoralLSystemJob` writes the resolved trigger/tuning quality, and constraint/render/pulse/proxy jobs consume `ProceduralCoralMath.ResolveEffectiveQuality(counter.EffectiveQualityWeight, tuning.GlobalQualityWeight)`.

Rejected Alternatives: Passing another NativeArray or direct trigger read into every job was rejected because the counter already owns the logical generation window and fits the 64-byte cache-line contract. Re-reading tuning downstream was rejected because it silently loses externally supplied sector quality.

Scalability potential: Low, Middle, High, and Ultra tiers now propagate the same continuous quality scalar through generation, collision fakery, matrix density, shader sway, bioluminescence pulses, and collision proxy staging. Exact `0.0f` remains a valid minimum-survival input.

Hardware Impact: Runtime cost is one 4-byte read from the existing cache-line counter per job and replaces repeated divergent quality ownership. It does not add persistent memory or change DTO stride.

### D31: NaN Vaccination Must Reset the Turtle, Not Just the Branch

Problem: `EmitBranch()` could detect a non-finite matrix/AUP and publish a fallback `CoralBranchDTO`, but the turtle's local `end` position and debug segment could still carry the poisoned value. `ConstrainCoralGrowthJob` had the same pattern: it repaired the branch matrix but continued using the old `local` variable in SDF/overlap math.

Solution: Added finite-first helpers for positive scalars, saturates, and quaternion normalization. `EmitBranch()` now sanitizes turtle position/rotation/step/radius before matrix composition and resets start/end/mid when fallback is needed. `ConstrainCoralGrowthJob` reloads `local` after fallback, sanitizes branch radius/stiffness, skips non-finite neighbors, and refuses to publish non-finite overlap adjustments. Pulse/proxy/audit candidates now skip non-finite AUP/matrices.

Rejected Alternatives: Relying on `CoralSelfAuditJob` to catch poison after publication was rejected because the render/proxy/pulse jobs run before a human reads the audit. Clearing full buffers was rejected because count windows already own validity and would not fix poisoned live slots.

Scalability potential: All tiers keep the same visual math, but low-tier shallow reefs no longer get promoted to corrupt debug/proxy state if a single branch receives invalid input. Ultra-density reefs get bounded failure isolation instead of full-window contamination.

Hardware Impact: Adds a few finite checks and guarded scalar clamps inside generation. The cost is lower than postmortem corruption; no measured microsecond gain is claimed.

### D32: CSV/H8BIN Rule Scalars Must Shape the Reef

Problem: Rule DTOs carried `BranchAngleRadians`, `LengthScale`, and `RadiusScale`, but the interpreter mostly used global tuning. Designers could edit CSV rule scalars without seeing proportional shape changes, violating the human-control facade requirement.

Solution: `InterpretStream()` now resolves the active opcode rule and applies finite-clamped angle, length, and radius scalars to turn/pitch/roll, grow/fork, thin, and push operations. CSV/H8BIN ingress clamps those scalars before commit so a bad content file cannot inject absurd radius or length growth into Burst.

Rejected Alternatives: Keeping rule scalars as future metadata was rejected because it creates fake tuning control. Adding managed grammar objects was rejected because the integer-opcode/Vault scratch path already carries the needed data.

Scalability potential: Low tiers still cap depth and density through `EffectiveQualityWeight`, while Middle/High/Ultra can spend the same generated branch budget on species-specific silhouettes from CSV/H8BIN scalars.

Hardware Impact: Adds one bounded rule lookup already capped by `MaxRules=16` per interpreted opcode and removes no hot allocations. Cost is deterministic and small relative to the existing expansion pass.

### D33: SpatialCellCount Must Be a Compact Live Window

Problem: `ConstrainCoralGrowthJob` wrote spatial cells at the branch index and then reported `SpatialCellCount = min(BranchCount, SpatialCells.Length)`. If a branch was dead or pruned, stale spatial records could remain inside the advertised `[0, SpatialCellCount)` window.

Solution: Added a separate `spatialWrite` cursor. Only live branches publish `CoralSpatialCellDTO` records, records are compacted from index zero, and `SpatialCellCount` is set to the write cursor.

Rejected Alternatives: Clearing the full spatial buffer was rejected because it violates the zero-init/count-window design. Keeping branch-index addressing was rejected because it makes the count window lie after pruning.

Scalability potential: Low-quality reefs prune more aggressively and therefore benefit most from compact spatial windows. High/Ultra retain dense occupancy without stale tail exposure.

Hardware Impact: Replaces sparse index writes with compact writes and one integer cursor. No measured speed claim; the benefit is correctness of the NonAlloc consumer contract.

### D34: HZB Must Not See Unsanitized Branch Radius

Problem: `ExtractCoralRenderMatricesJob` passed `branch.Radius` into `IsOccluded()` before sanitizing it and checked `branch.LocalMatrix` finiteness only after the HZB path. A corrupted live branch could inject a NaN radius into occlusion math before being rejected.

Solution: Matrix extraction now rejects non-finite branch matrices before occlusion testing and passes a `SafePositive()` radius into `IsOccluded()`. Camera-relative AUP subtraction still happens in double precision before the float cast.

Rejected Alternatives: Relying on `ConstrainCoralGrowthJob` to sanitize every branch was rejected because extraction is the last render-data gate and must fail closed independently. Letting HZB return false on NaN comparison was rejected because implicit NaN behavior is not an audit-proof contract.

Scalability potential: Low/Middle/High/Ultra use the same HZB route; higher tiers can submit denser visible reefs without accepting poisoned cull inputs.

Hardware Impact: Adds one finite matrix check and one scalar clamp before HZB. No speed claim; the value is render-path NaN isolation before GPU matrix staging.

### D35: Blackbox Telemetry Must Not Accept NaN Measurements

Problem: `TryRecordMeasuredBurstTimeUs()` accepts a dispatcher-supplied measured microsecond value after the job writes its placeholder estimate. If the external timing route supplies NaN or Infinity, the 300-frame telemetry ring can become non-finite even when the coral solver output is clean.

Solution: The overwrite path now checks `math.isfinite()`. Non-finite measurements write `0f` and OR `FaultNonFinite` into that telemetry row; finite measurements still clamp below zero to zero.

Rejected Alternatives: Trusting the profiler/dispatcher value was rejected because blackbox dumps must remain parseable during exactly the failure cases they are meant to diagnose. Throwing an exception was rejected because this is a fault-recorder path, not a gameplay control path.

Scalability potential: All tiers use the same fixed 300-frame ring. Higher tiers may record more expensive generation, but the telemetry format stays finite and fixed-size.

Hardware Impact: Adds one scalar finite check on the cold/dispatcher measurement update path. Runtime generation math and Vault capacity are unchanged.

### D36: Job-Chain Self-Audit Must Match Owner-Local Proof

Problem: The cold architecture audit captured the first live branch sector hash, but `CoralSelfAuditJob` overwrote the sector hash on every live branch and reported the last live branch. The job-chain audit also used raw branch radii in overlap probes, so a non-finite radius could poison overlap telemetry after matrix/AUP checks passed.

Solution: `CoralSelfAuditJob` now captures the first live sector hash with a boolean proof flag, checks `CoralPaddedCounterDTO.EffectiveQualityWeight`, flags non-finite branch radii, and uses `SafePositive()` radii in overlap probes.

Rejected Alternatives: Leaving last-live sector hash was rejected because two audit routes must describe the same owner-local fact. Trusting constraint sanitization was rejected because self-audit is the final forensic proof and must be independently fail-closed.

Scalability potential: All quality tiers keep the same bounded 256-branch overlap probe cap; lower tiers scan fewer logical branches, while ultra remains bounded.

Hardware Impact: Adds one boolean capture branch and scalar finite checks inside the bounded audit job. No runtime rendering or generation capacity change.

### D37: Self-Audit Must Carry Solver Faults Forward

Problem: `CoralSelfAuditJob` initialized its local `faultFlags` to zero. Any fault already recorded by generation or constraint stages in `CoralPaddedCounterDTO.FaultFlags` could be erased from the final `CoralSelfAuditResultDTO.Flags`.

Solution: The audit job now seeds `faultFlags` from `counter.FaultFlags` and ORs additional audit-local findings on top. Solver faults and audit faults share one final proof field.

Rejected Alternatives: Keeping solver faults only in `Counters` was rejected because integrators and blackbox readers should not need to correlate two payloads to know whether the final pipeline was faulted. Recomputing every solver fault in audit was rejected because capacity/stack/no-rule conditions are already owner-recorded at the producing stage.

Scalability potential: The change is independent of quality tier. Low through Ultra keep the same bounded audit cost and get a more faithful fault summary.

Hardware Impact: One cached 4-byte read already pulled with the counter. No measurable frame impact.

### D38: Size-Only Layout Validation Is Not Enough

Problem: The editor validator checked sizes for several DTOs but did not assert offsets for rule scalar fields, telemetry fault fields, or self-audit fault fields. A future reorder could keep the same 64-byte size and silently break binary payload readers or forensic tools.

Solution: Added explicit offset checks for `CoralLSystemRuleDTO` scalar/fault route fields, `CoralGenerationTelemetryEntry` timing/fault/matrix fields, `CoralPaddedCounterDTO.FaultFlags`, and `CoralSelfAuditResultDTO` fault/utilization fields.

Rejected Alternatives: Relying on `[StructLayout(Explicit)]` size alone was rejected because explicit size proves only the envelope, not the ABI offsets. Adding runtime checks in Burst jobs was rejected because this is editor/import proof and must have zero runtime cost.

Scalability potential: Runtime tier behavior is unchanged. The benefit is preventing mobile ARM64 layout drift before low-tier player builds.

Hardware Impact: Runtime cost is zero. Editor validation does additional reflection checks only during domain load or menu validation.

### D39: GPU Upload Must Fail Closed On Corrupt Counts And Sway Scalars

Problem: `UploadFromVault()` cast `uint InstanceCount` to `int` before clamping, so a corrupt indirect-args count above `int.MaxValue` could wrap negative. The dispatcher also published raw `CoralGpuSwayDTO` float4 lanes into shader globals without a final finite gate.

Solution: Added a uint-safe instance-count clamp against the matrix capacity, forced `VertexCountPerInstance >= 1`, tracked active instance count to skip zero-instance draws, and converted sway float4 lanes through a finite fallback helper before `Shader.SetGlobalVector`.

Rejected Alternatives: Trusting the Burst extraction job was rejected because the dispatcher is the last CPU boundary before driver/shader state. Reading back GPU args was rejected because it would stall the render path and violate the upload cadence.

Scalability potential: Low tiers frequently upload zero or sparse matrices after HZB/density cuts and now avoid an unnecessary draw call. High/Ultra still use the same no-grow double-buffer route at larger prewarmed capacity.

Hardware Impact: Adds scalar validation and a zero-count branch on the main-thread upload/draw facade. It avoids malformed driver submissions and shader NaN propagation; no measured speed claim.

### D40: GPU Prewarm Must Not Escalate Past Coral Capacity

Problem: `EnsureGraphicsResources()` accepted arbitrary requested capacity and allocated buffers before proving every `GraphicsBuffer` constructor succeeded. A bad caller or driver exception could either request beyond the coral budget or leave a partially-created buffer set alive.

Solution: Cold prewarm capacity is clamped to `ProceduralCoralConstants.MaxRenderMatrices`. Graphics buffer creation is wrapped in a fail-closed `try/catch`; any exception releases partial resources and returns false. Active instance count resets on every prewarm.

Rejected Alternatives: Trusting callers was rejected because the dispatcher is a public facade. Letting a constructor exception propagate was rejected because prewarm is a recoverable allocation boundary and should leave no partial GPU state.

Scalability potential: Low through Ultra still prewarm different effective capacities, but never beyond the coral matrix budget. Ultra can reserve the full branch budget without corrupting the no-grow upload path.

Hardware Impact: Runtime upload cadence unchanged. Cold allocation failure now cleans up immediately instead of leaking partial driver resources.
