# Rationale_SHINOBU_121

Date: 2026-05-19
Status: ACTIVE / PENDING VERIFICATION

## Decision 01 - Stop Before Source Mutation

Problem: The user assigned `SHINOBU_121`, but `Docs/Tasks/CURRENT_BATCH.md` contains no `<AGENT_PROMPT id="SHINOBU_121">` block. The batch protocol requires exact extraction from current batch before architectural decisions or code changes.

Solution: Halt source mutation and record the prompt absence in disk-backed status/rationale/log files. This preserves the anti-amnesia protocol and prevents cross-contamination from adjacent agent prompts.

Rejected Alternatives: Inferring tasks from chat text was rejected because the XML block is the mandated primary directive. Reading archived batches was rejected because current-batch authority explicitly forbids stale prompt leakage. Borrowing `SHINOBU_105` or `SHINOBU_120` GPU tasks was rejected as domain contamination.

Scalability potential: No runtime path changed. Low, middle, high, and ultra device behavior remains untouched until an authoritative task block defines the exact WFC/GPU scope.

Hardware Impact: 0 us runtime change on i3/MX350. No GPU, CPU, VRAM, GC, or PCIe behavior changed.

## Decision 02 - Mandate Readiness Without Implementation

Problem: Procedural wreckage work would touch WFC, AUP, deterministic RNG, native memory, GPU buffers, and `DrawProceduralIndirect`; coding without mandate context would violate pre-code analysis rules.

Solution: Read the relevant registry entries only: `TOOL_Procedural_Wreckage_Generator`, `MATH_AUP_Determinism_Sync`, `MATH_Deterministic_RNG_SlotMachine`, `REND_GPU_Sovereignty`, `GPU_Compute_Kernels_Kernels_Optimization_MX350`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, and `ARCH_Global_Registry_ServiceLocator_DI_Init`.

Rejected Alternatives: Skipping mandate review was rejected. Creating a generic WFC implementation was rejected because the batch may demand a specific DataVault, shader, or renderer ownership route.

Scalability potential: The selected mandates define the required Low/Middle/High/Ultra continuum: MX350 uses bounded WFC and minimal upload cadence; high and ultra tiers spend saved CPU/GPU budget on denser wreck matrices and richer presentation after the real task block is restored.

Hardware Impact: 0 us runtime change. The expected implementation envelope, once authorized, must remain below suspicious 0.1 ms frame cost for steady-state orchestration and avoid `SetData()`/`Instantiate()` hot paths.

## Decision 03 - Reject Chat-Inferred 20-Task Reconciliation

Problem: The follow-up mandate assumes a 20-task matrix and orders reconciliation, but the current authoritative batch file still lacks `SHINOBU_121`. Regex inventory found exactly 20 agent prompts and 400 task rows, all assigned to other IDs ending at `SHINOBU_120`.

Solution: Preserve strict parsing. Do not copy neighboring tasks, do not infer hidden tasks, and do not mutate source. The correct action is to report the prompt absence and wait for the current batch file to be repaired.

Rejected Alternatives: Treating the prose goal as a substitute XML directive was rejected because it has no task list, no exact DoD, no editor facade requirements, and no buffer IDs. Executing a generic WFC/GPU architecture was rejected because it could create wrong Vault routes, wrong asmdef boundaries, and wrong shader ownership.

Scalability potential: No runtime behavior changed. Once the real block exists, implementation must expose continuous `GlobalQualityWeight` controls rather than low/high switches: low weight collapses WFC breadth, matrix upload cadence, and decorative module count; high/ultra weight increases ghost hull density, rust/caustic scalar richness, and draw count through indirect args without GameObjects.

Hardware Impact: 0 us runtime change on i3/MX350. This prevents speculative source churn and avoids unnecessary C# rebuilds under parallel-agent load.

## Decision 04 - Restore Active Implementation After Current Batch Repair

Problem: The active `Docs/Tasks/CURRENT_BATCH.md` now contains the authoritative `SHINOBU_121` XML block with 20 tasks. The previous blocker status is stale and would now obstruct required work. Existing `ProceduralWreckGenerator.cs` is a large legacy mixed-object path with local persistent native containers, `Pack=1` DTOs, mesh creation, object-pool collision proxies, and loot spawn queues. Directly deleting or rewriting it risks breaking current World references owned by other agents.

Solution: Keep legacy source untouched for compatibility in this pass and add a new isolated `Hecton8.World.ProceduralWreckage` assembly under the World domain. The new path owns explicit 128-byte `WreckageNodeDTO`, Vault buffer handles, deterministic fallback rules, Burst WFC jobs, AUP-relative matrix extraction, collision/loot staging DTOs, editor validation, and debug facades. Buffer IDs are cast constants local to the assembly to avoid editing `H8Memory.cs` while other agents are modifying Core.

Rejected Alternatives: Deleting `ProceduralWreckGenerator.cs` was rejected because it exports public contracts and is referenced by World systems; doing so would be architectural sabotage outside a narrow replacement migration. Patching every `Pack=1` World DTO was rejected as cross-domain churn. Waiting for `wreckage_module_rules.h8bin` was rejected because Task 01 demands an emergency deterministic mock.

Scalability potential: Low devices receive bounded WFC breadth, shorter visibility distance, lower debris scatter count, and reduced interior detail by continuous `GlobalQualityWeight`; middle devices keep macro silhouette and moderate debris; high devices increase wreck density, shear variety, and collision proxy fidelity; ultra devices push richer indirect matrix payloads and shader scalar data without changing gameplay truth.

Hardware Impact: Expected hot-path gain on i3/MX350 comes from replacing legacy hierarchy/object-pool hydration and mesh work with Vault-native DTO writes and indirect arguments. Static estimate before profiling: tens to hundreds of microseconds saved during generation frames and 0 B managed allocation in new generation jobs. Measured proof remains pending.

## Decision 05 - Data-Only WFC and GPU Upload Route

Problem: The assignment requires WFC generation, AUP-safe render matrices, loot/collision staging, and `DrawProceduralIndirect` without allowing persistent private `NativeArray` ownership or GameObject hierarchies. Unity `GraphicsBuffer` cannot be directly owned by Burst jobs, so a literal "job writes GraphicsBuffer" design would either be invalid Burst code or force main-thread stalls.

Solution: Jobs write all simulation/render payloads into Vault-owned unmanaged DTO arrays. `ExtractRenderMatricesJob` subtracts `CameraAUP` from each node `double3 SectorAUP` before casting to `float3`, culls by continuous quality and optional HZB tiles, and writes `WreckageIndirectArgsDTO`. `ProceduralWreckageGpuUploadDispatcher` then uses `GraphicsBuffer.LockBufferForWrite` and `UnsafeUtility.MemCpy` to move matrices/args into double-buffered GPU buffers before `Graphics.DrawProceduralIndirect`.

Rejected Alternatives: Direct `GraphicsBuffer` access inside Burst was rejected because `GraphicsBuffer` is a managed Unity object and not a legal Burst field. `GraphicsBuffer.SetData` was rejected because it adds driver-managed copy behavior and violates the explicit lock/memcpy upload mandate. `DrawMeshInstancedIndirect` was rejected because the task asks for `DrawProceduralIndirect`.

Scalability potential: Low quality writes fewer nodes and debris matrices, shorter cull distance, and lower detail probability. Middle quality keeps silhouette and a controlled debris ring. High quality increases shear/debris variety. Ultra quality uses the saved CPU cost for richer shader scalar lanes rather than gameplay-truth bloat.

Hardware Impact: Expected MX350 benefit is fewer submitted matrices and no scene hierarchy updates. Upload cost becomes linear in visible matrix count and uses a single contiguous memcpy per buffer. Measured microseconds remain pending until Unity import/profiler.

## Decision 06 - Legacy Generator Quarantine Instead of Deletion

Problem: `ProceduralWreckGenerator.cs` contains legacy object-pool collision and loot application, plus `Pack=1` DTOs and local persistent native containers. It also exports public World contracts and has references from existing systems, so deletion would be a cross-domain migration rather than a local SHINOBU_121 fix.

Solution: Do not call or extend the legacy path. Implement a separate asmdef and document the legacy path as incompatible with the new procedural wreckage route. The new path has no `Instantiate`, no object-pool calls, no managed collections in jobs, and no local persistent native arrays.

Rejected Alternatives: Deleting the legacy file was rejected because it can break existing scene/world references and violates the simultaneous-agent compile-wall rule. Half-patching individual pool calls was rejected because it would leave the old mesh/local allocation architecture intact and create a false compliance report.

Scalability potential: Low/middle/high/ultra behavior now exists in the new route and can be integrated without inheriting the legacy generator's mesh/hierarchy costs.

Hardware Impact: New path avoids the old collision proxy GameObject and loot spawn queue during generation. Exact savings require Unity-side integration proof.

## Decision 07 - Build Gate Obeyed

Problem: The workflow asks for compilation verification after task loops, but the active mandate forbids `dotnet build` while CPU load is above 50% or another compiler is running. Current CPU samples were `99.42%` and `100%`.

Solution: Do not launch `dotnet build` under the prohibited system load. Run static scans and `git diff --check` only. Mark compile status as `PENDING VERIFICATION` instead of fabricating a green build.

Rejected Alternatives: Running a build anyway was rejected because it violates the user's explicit hardware protection rule. Claiming compile success from static scans was rejected because Unity asmdef import and Burst compile are not proven by text inspection.

Scalability potential: No runtime behavior changed by this decision. It preserves developer hardware and avoids adding load during a parallel-agent batch.

Hardware Impact: Prevented a high-load C# compile on an already saturated system. Runtime impact is 0 us; verification remains pending.
