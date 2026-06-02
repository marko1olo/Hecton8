# Rationale 1329 - MEMORY_SOVEREIGN_FABRICATOR_EXORCIST

Status: TASKS 01-20 COMPLETE / DOTNET BUILD GATED BY EXISTING DOTNET PROCESSES

## Initial Domain Decision
Problem: `Fabricator.cs` owns 13 persistent native aliases in a MonoBehaviour: one `NativeParallelHashMap<int,int>` and twelve `NativeArray<T>` fields.
Solution: Move persistent storage identity to `GlobalDataVault` descriptors and resolve physical views only inside Fabricator methods that need them.
Rejected Alternatives: Managed `List/Dictionary` fallback would violate zero-GC and DOD. Keeping `Allocator.Persistent` fields would violate memory sovereignty.
Scalability potential: Low uses fixed small scratch capacities; Middle/High/Ultra can keep the same truth buffers and spend saved ownership complexity on presentation detail, not gameplay drift.
Hardware Impact: i3/MX350 avoids dangling alias risk during vault relocation; expected hot-path cost is microsecond-scale handle validation, not frame-scale GC.

## Mandate Selection
Problem: The task crosses native memory, inventory resources, struct layout, read accessor purity, and telemetry.
Solution: Loaded six mandates: native collections/job protocol, zero-GC, ARM64 layout, inventory/resource SOA, GlobalRegistry/DI, telemetry blackbox.
Rejected Alternatives: Reading unrelated graphics/audio mandates would inflate process without affecting Fabricator memory ownership.
Scalability potential: Continuous `GlobalQualityWeight` preserved where publication/cadence is touched; no binary quality split.
Hardware Impact: Static proof and cold-buffer ownership reduce runtime allocator churn on low-end silicon.

## Buffer Route Decision
Problem: `Fabricator.cs` scratch state needs 13 former persistent containers, but the existing `ShinobuFabricationTelemetryRing` is already type-owned by `FabricationAssemblerRuntime`.
Solution: Use unused IDs `71144`, `71148`, `71149`, and `71169`-`71179` for Fabricator-specific scratch and blackbox telemetry. Keep `71150`-`71168` untouched because ChemicalInfluenceGrid uses them as raw `BufferID` casts.
Rejected Alternatives: Reusing `ShinobuFabricationTelemetryRing` would create a type collision with `FabricationTelemetryEntry`. Editing dirty inventory files to create new routes would widen conflict surface.
Scalability potential: Low and middle tiers use fixed capacities with no hot resize. High and ultra tiers preserve the same gameplay truth route and can spend cycles in visual fabrication systems.
Hardware Impact: i3/MX350 avoids hash map ownership and allocator churn in Fabricator; expected lock/resolve overhead remains sub-10 us per user craft check.

## Loop 1 Verification
Problem: The first five tasks are static-discovery work with no code mutation yet.
Solution: Created a JSON proof artifact and updated status before starting code edits. Compile was not launched for Loop 1 because no runtime source file was changed.
Rejected Alternatives: Running a build after zero source changes would consume coordinator CPU budget without increasing proof quality.
Scalability potential: The artifact records low/middle/high/ultra behavior explicitly instead of a binary quality switch.
Hardware Impact: No runtime impact; this is static ownership proof.

## Descriptor Substitution
Problem: Fabricator previously retained physical native aliases across frames, blocking vault relocation.
Solution: Replaced native fields with 16-byte `VaultGenerationHandle<T>` descriptors. Method bodies acquire vault write locks, pass transient `NativeArray<T>` views to jobs/helpers, then release in `finally`.
Rejected Alternatives: Keeping a Fabricator-owned `NativeParallelHashMap<int,int>` for availability would preserve a stale pointer. Editing dirty `PlayerInventory.cs` to add another route would collide with another agent.
Scalability potential: Low tier pays fixed scratch capacities; Middle/High/Ultra preserve one truth route and can increase visual assembly work elsewhere without changing crafting authority.
Hardware Impact: i3/MX350 avoids persistent hash-map allocator ownership and relocation faults. Expected user-action lock/resolve overhead remains microsecond-scale.

## Availability Job Reconciliation
Problem: Existing `CraftingSystem.CanCraft` consumed `NativeParallelHashMap<int,int>`, which would force Fabricator to keep a hash map alias or modify dirty inventory code.
Solution: Added `EvaluateRecipeAvailabilityLinearJob` and an overload using `NativeArray<int2>` availability pairs filled from local inventory plus accessible logistics counts.
Rejected Alternatives: Reusing `PlayerInventory.TryCopyAvailableItemCountsNonAlloc` would require either a Fabricator hash map or edits to dirty inventory ownership files.
Scalability potential: Pair scan is bounded by recipe cost count, not total inventory. Low devices avoid hash-map mutation; high devices still use the same deterministic recipe truth.
Hardware Impact: On i3/MX350, 32-pair linear scan is cheaper and more predictable than maintaining a persistent native hash table for user-driven craft checks.

## Black Box Route
Problem: Vault lock failures need a postmortem artifact without managed string logging in the hot path.
Solution: Added a 300-entry DataVault telemetry ring and a cold fault path that snapshots the ring and queues a background write to `Docs/AgentLogs/Dump_1329_Fabricator.bin`.
Rejected Alternatives: `Debug.Log` or exception-only traces would allocate strings and lose the last-frame state. Synchronous write on the main path would stall the craft caller.
Scalability potential: Minimum hardware pays nothing unless vault failure occurs. High/Ultra receive the same fault artifact without gameplay divergence.
Hardware Impact: Normal path cost is a fixed native entry write on failure only; disk I/O is background and cold.

## Validator Route
Problem: Explicit telemetry layout must fail loudly if a future field edit breaks 64-byte ARM64 alignment.
Solution: Added `FabricatorMemorySovereigntyValidator1329.cs` with `UnsafeUtility.SizeOf`, `UnsafeUtility.GetFieldOffset`, BufferID checks, and `FatalArchitectureException`.
Rejected Alternatives: Runtime-only `Marshal` checks or prose in docs would not halt broken editor builds.
Scalability potential: No runtime tier behavior; layout stays stable across all hardware.
Hardware Impact: Editor-only validation; zero player-frame cost.

## Build Gate
Problem: Project rules forbid launching `dotnet build` while CPU is above 50 percent or another `dotnet`/`csc` is running.
Solution: Checked CPU/processes twice; CPU was 72-100 percent with external `dotnet`/`csc` activity. Build and Roslyn tool execution are deferred until the gate clears.
Rejected Alternatives: Forcing a build would violate the coordinator rule and compete with other agents.
Scalability potential: No runtime effect.
Hardware Impact: Avoids saturating shared workstation CPU during parallel agent execution.

## Final Audit
Problem: The final proof had to be AST-based and file-backed, not chat-only.
Solution: Ran `VaultNativeAliasRoslynAudit` after source edits and produced `Docs/Reports/VAULT_EXORCISM_REPORT_1329.json`. Audited scope: `Fabricator.cs` and uncontested `CraftingSystem.cs`. Result: before 13 forbidden Fabricator aliases, after 0; parseFailures=0; auditedFilesSha256=`abe620d9d591296c94c3f0891b2ae777e433afc703c4aa004e469cef71362489`.
Rejected Alternatives: Regex-only final proof would miss field ownership semantics. Broad inventory scan edits were rejected because inventory files are dirty under other agents.
Scalability potential: The proof locks ownership route count to zero without changing quality tiers or gameplay truth.
Hardware Impact: No runtime cost; static artifact prevents regression.

## Compile Status
Problem: A full `dotnet build` remains forbidden while external `dotnet` processes are present.
Solution: Did not launch build. Roslyn parse audit completed because it is an audit executable, not a build, and CPU had dropped below 50 percent.
Rejected Alternatives: Starting a build despite active `dotnet` processes would violate AGENTS and could corrupt parallel integration timing.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional CPU load on shared workstation.

## APEX Reaudit Correction
Problem: Prior telemetry DTO placed StateHash after 4-byte fields and Fabricator cold dump used catch(Exception) plus per-fault managed snapshot allocation.
Solution: Reordered FabricatorMemoryTelemetryEntry to 8-byte fields at offsets 0 and 8, 4-byte fields from 16 to 56, explicit byte pads at 60-63. Replaced per-dump allocation with static preallocated snapshot and removed catch(Exception).
Rejected Alternatives: Leaving cold catch(Exception) because it was not hot path was rejected under APEX gate. Keeping uint Reserved0 as implicit padding was rejected because the mandate asks for explicit padding bytes.
Scalability potential: Low/Middle/High/Ultra share the same 64-byte telemetry DTO and fixed 300-entry ring; no binary quality switch or gameplay truth drift.
Hardware Impact: i3/MX350 avoids unaligned 64-bit telemetry loads and avoids cold fault-path allocation churn during repeated vault failures.

Problem: Native collection and zero-GC proof needed scanner artifacts beyond prose.
Solution: Ran prebuilt Roslyn scanner exes only, not dotnet build. Native gate: 4 scoped files, parseFailures=0, total fields=24, forbidden persistent=0. Zero-GC gate: 3 files, parseFailures=0, hot owner managed-risk hits=0.
Rejected Alternatives: Full dotnet build was rejected because external dotnet.exe processes and CPU=100% violate project build gate.
Scalability potential: Static gates prove ownership route; runtime/profiler claims remain pending external verification.
Hardware Impact: No extra frame cost; proof prevents reintroducing persistent Fabricator native aliases.

## APEX Repeat Reaudit
Problem: The repeated rejection required re-running the prompt extraction and all six gates after the APEX correction, not trusting prior prose.
Solution: Re-extracted `<AGENT_PROMPT id="1329">`, re-read AGENTS/domain/mandates, refreshed the four-file audit scope, and reran native and hot-path Roslyn scanners. One source cleanup was applied: `activeCost & (ResolveAvailableCount(cost.x) < cost.y)` now makes the linear job's boolean precedence explicit.
Rejected Alternatives: Leaving the expression precedence implicit was rejected because it creates avoidable review ambiguity. Rewriting Kahn graph traversal into branchless code was rejected because it is deterministic recipe graph truth, not a visual/physical solver eligible for a cinematic fake.
Scalability potential: Low uses fixed vault scratch and linear availability scan; Middle/High/Ultra preserve the same recipe truth while visual fabrication presentation can consume saved frame budget through presentation-only effects.
Hardware Impact: i3/MX350 avoids persistent NativeHashMap ownership in Fabricator and avoids managed allocations in hot craft checks. The repeat patch has no measurable runtime cost; it reduces compiler/reviewer ambiguity.

Problem: Build verification is still blocked by process policy.
Solution: Checked active processes and CPU. CPU was below 50%, but seven external `dotnet.exe` processes were active, so no new `dotnet build` was launched.
Rejected Alternatives: Forcing a build would violate AGENTS and compete with other agents.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional workstation contention during parallel agent execution.

## APEX Third Reaudit
Problem: The user required a fresh proof pass after rejecting prior prose. The risk was stale confidence: native ownership, hot-path allocation, compaction locks, AUP casts, and no-throw behavior had to be rechecked against files on disk.
Solution: Re-extracted `<AGENT_PROMPT id="1329">`, rebuilt an isolated four-file audit scope, reran the native alias Roslyn scanner and hot-path Roslyn scanner, and rechecked AUP/throw/lock routes with direct file searches. No 1329 source defect remained, so no production code was patched in this pass.
Rejected Alternatives: Editing `Construction/DroneFleetManager_Transactions.cs` or inventory lane definitions to satisfy the build was rejected as cross-domain interference. The compile error is a dirty inventory/construction dependency: one file still uses `InventorySoaVaultLane<T>` while the changed inventory owner defines non-generic `InventorySoaVaultLane`.
Scalability potential: Low tier continues to use fixed vault scratch and linear recipe-cost scans; Middle/High/Ultra preserve identical crafting truth and can spend saved frame budget on visual fabrication only. No binary quality switch was introduced.
Hardware Impact: i3/MX350 avoids Fabricator-owned persistent native alias relocation risk and hot managed allocations. The third audit changed no runtime instructions; it proves that the prior source hash `2161879615dab92336a3a8dabef84afaf5549be011e9a6ff71601d8521bde3a2` still satisfies the 1329 gates.

Problem: Full compilation was finally allowed by the process gate, then failed in an unrelated dependency.
Solution: Launched `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal` only after CPU was below 50% and no `dotnet`/`csc` process was present. The build failed with CS0308 in `DroneFleetManager_Transactions.cs`, not in `Fabricator.cs`, `CraftingSystem.cs`, `H8Memory.cs`, or the editor validator.
Rejected Alternatives: Applying a speculative cross-domain fix was rejected. It would violate the 1329 domain boundary and could overwrite another agent's inventory migration.
Scalability potential: No runtime effect.
Hardware Impact: One compile attempt consumed about 63 seconds of workstation time; no additional build retries were launched after identifying the dependency wall.

## APEX Fourth Reaudit
Problem: The previous compile blocker became actionable after re-reading the user rejection. `DroneFleetManager_Transactions.cs` used obsolete generic `InventorySoaVaultLane<T>` API while the inventory owner had already migrated the descriptor to non-generic `InventorySoaVaultLane`.
Solution: Applied a minimal cross-domain compile-medic patch only at the broken interface seam: `TryBindDroneInventoryLane<int>` now writes non-generic `InventorySoaVaultLane` with `SetHandle`, checks `Generation`, and resolves read access through `ToHandle<int>`. The same touched file had a broad cold `catch(Exception)`, so it was replaced with typed IO/security/path catches.
Rejected Alternatives: Reverting inventory migration was rejected because those files are dirty under another agent. Reintroducing `InventorySoaVaultLane<T>` was rejected because it would reverse the new descriptor route. Editing the later PDA/Atmosphere/Vegetation/Fluid errors was rejected as a compile wall outside 1329.
Scalability potential: No gameplay truth, quality tier, or Fabricator route changes. The patch only restores descriptor API consistency at compile time.
Hardware Impact: Runtime delta is effectively 0 us. The change removes stale generic descriptor access and one broad managed exception catch from a cold dump path.

Problem: A fresh build after the Drone patch still cannot verify the full project.
Solution: Cleared idle MSBuild node-reuse workers left by the previous build, ran one gated `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -p:nodeReuse=false`, and stopped there when the error list moved to unrelated dirty domains.
Rejected Alternatives: Launching repeated build/fix loops across PDA, Atmosphere, Vegetation, Fluid, InventoryRouting, and Audio would violate the 1329 domain boundary and collide with other agents.
Scalability potential: No runtime effect.
Hardware Impact: One additional build attempt consumed about 60 seconds. The 1329 static gates remain green; global compile remains blocked by other-agent dependency walls.

## Deep Domain Audit - 2026-05-27
Problem: The Fabricator blackbox dump copied `ShinobuFabricatorMemoryTelemetryRing` from a Vault read-only view without first pinning the buffer. A compaction pass between view resolution and the 300-entry copy could relocate the arena while the dump path was reading stale memory.
Solution: Wrapped the ring copy in `TryLockBuffer(BufferID.ShinobuFabricatorMemoryTelemetryRing, SystemID.Crafting)` and `TryUnlockBuffer` in `finally`. The pin is held only for the synchronous copy into the preallocated snapshot; file I/O still happens later on the background worker without holding a Vault view.
Rejected Alternatives: Holding a write lock across file I/O was rejected because it blocks compaction across an async boundary. Copying without pin was rejected because the DataVault contract only proves fence checks at resolution time, not pointer stability after return.
Scalability potential: Low/Middle/High/Ultra all use the same fixed 300-entry ring. Higher tiers can add richer presentation diagnostics elsewhere; gameplay DTO layout and Vault ownership do not change.
Hardware Impact: Normal frame cost remains 0 us because this is a fault path. On i3/MX350, the fix prevents rare stale-pointer dump corruption without adding persistent native fields.

Problem: The previous dump path marked `_fabricatorBlackBoxDumped` before the background write was actually accepted. If the global dump queue was busy or `QueueUserWorkItem` refused the callback, this Fabricator could permanently suppress its own postmortem dump.
Solution: `_fabricatorBlackBoxDumped` and the failure streak reset now occur only after `ThreadPool.QueueUserWorkItem` returns true. Queue-busy and queue-refused paths leave the failure state retryable and reset only the static in-flight gate.
Rejected Alternatives: Treating another Fabricator's queued dump as proof for this instance was rejected because each Fabricator owns distinct recent failure state. Allocating a fresh dump task was rejected under the telemetry mandate.
Scalability potential: Same logic across all hardware tiers; no quality switch and no gameplay truth change.
Hardware Impact: Runtime hot-path delta is 0 us. Fault-path reliability improves without heap churn beyond the pre-existing static snapshot.

Problem: `IsRecipeUnlockBitSet` resolved an unlocked-recipe read-only Vault view without a matching pin, creating the same compaction race in a cold recipe-cache path.
Solution: Added owner-tagged `TryLockBuffer`/`TryUnlockBuffer` around the read-only view lifetime. The pin is scoped to one word read and released in `finally`.
Rejected Alternatives: Converting the read to a write lock was rejected because it would lie about mutation and increase contention. Leaving the route unpinned was rejected after the blackbox finding.
Scalability potential: Low tier pays the pin only when recipe cache/unlock state is rebuilt; Middle/High/Ultra preserve identical craft truth.
Hardware Impact: Estimated cost is sub-microsecond on cold recipe visibility checks; no Tick/SlowTick allocation or persistent native field was introduced.

## Deep Domain Audit Continuation - 2026-05-27
Problem: Public Fabricator recipe getters were not pure. `AvailableRecipes`, `TotalRecipeCount`, and `LockedRecipeCount` rebuilt the recipe cache through `EnsureRecipeCache()`, which could refresh ScanLog state, rebuild lists, and touch Vault-backed unlock state from UI/presentation reads.
Solution: Runtime getters now return cached state only. Cache refresh moved to owner/cold routes: `Awake`, `OnEnable`, interaction open, `SlowTick`, and GlobalRegistry service replacement. Editor `TotalRecipeCount` uses `CountAuthoredRecipeReferencesCold()` so validators keep working without mutating runtime cache.
Rejected Alternatives: Leaving lazy getter rebuild was rejected because read accessors must not publish, allocate/grow buffers, or mutate global state. Removing editor count behavior was rejected because it would break cold authoring validation.
Scalability potential: Low/Middle/High/Ultra all read the same cached recipe list; device tier affects presentation only, not recipe authority.
Hardware Impact: UI list reads no longer trigger cache rebuild work. On i3/MX350 this removes avoidable cold spikes when panels query recipe counts.

Problem: `TryAcquireFabricatorWrite` could leak a Vault write lock if `TryAcquireWriteLock` returned true but the returned view was default or shorter than the required capacity.
Solution: The method now tracks `lockAcquired` separately from `buffer.IsCreated` and releases the lock before returning failure.
Rejected Alternatives: Using `buffer.IsCreated` as a proxy for lock ownership was rejected because lock ownership and view validity are separate contracts.
Scalability potential: No quality-tier behavior change; the fix preserves compaction safety under all device tiers.
Hardware Impact: Normal successful path adds one bool local. Failure path avoids a stuck lock that could block compaction and stall later crafting work.

Problem: After purifying UI reads, the unlock-mask writer became unreachable, leaving `_unlockedRecipesHandle` as dead descriptor state and forcing direct `RecipeData.IsUnlocked` checks during cache rebuilds.
Solution: `EnsureRecipeCache()` now builds the unlock mask in the owner refresh route before classifying recipes. `EnsureRecipeUnlockMask()` no longer calls `EnsureCraftingScratch()`, because the unlock buffer can be ensured independently and should not warm unrelated recipe-cost/graph buffers.
Rejected Alternatives: Rebuilding unlock mask from `IsRecipeUnlocked()` was rejected because that reintroduced hidden mutation in read-like routes. Keeping the dead descriptor was rejected because it hides a false optimization.
Scalability potential: Low tier pays only the unlock buffer refresh when recipe/ScanLog state changes; higher tiers keep identical recipe truth while visual fabrication can scale separately.
Hardware Impact: Avoids unnecessary Vault scratch warmup during recipe cache refresh and keeps repeated classification on a fixed bitset.

Problem: The previously touched Drone transaction seam had the same lock/view conflation: a successful Construction write-lock with an invalid or short view returned false without release.
Solution: `TryAcquireDroneTransactionWriteBuffer` now uses an explicit `locked` flag, validates the view, and releases immediately on failure.
Rejected Alternatives: Broadly sweeping all Construction `TryAcquireWriteLock` call sites was rejected as out-of-domain interference. This specific file was already touched for a compile-seam repair and was part of the 1329 audit scope.
Scalability potential: No gameplay truth or quality-tier behavior change.
Hardware Impact: Failure-path only; prevents Construction write-lock starvation caused by stale/undersized Vault views.

Problem: A full compile proof was requested, but project process rules forbid starting a build while CPU is above 50 percent or any `dotnet`/`csc` process is already running.
Solution: Rechecked the gate after static scanners. CPU was 96.88% and an external `dotnet.exe build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was active, so no new build was launched.
Rejected Alternatives: Starting another build would violate AGENTS and compete with another agent's verification run.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional workstation contention during parallel integration.

## Deep Domain Audit Continuation 2 - 2026-05-27
Problem: Recipe visibility used a managed `List<RecipeData>` initialized with capacity 16 while the Fabricator unlock mask supports 512 recipe slots. `EnsureRecipeCache()` can run from owner/cold routes after scan-log or mod-registry changes, so a large authored/modded recipe set could grow the list and allocate during gameplay-facing refresh.
Solution: Introduced `MaxRecipeCacheEntries = MaxUnlockedRecipeWords * 64`, preallocated `_visibleRecipes` to that cap, and added `_overflowRecipeCount`. `AppendRecipeToCache` now refuses entries once visible + locked + overflow reaches the bitset cap, and overflow contributes to locked/total counts.
Rejected Alternatives: Growing the unlock bitset dynamically was rejected because it would add more Vault capacity churn and widen the DTO contract. Leaving `List<T>` growth as a cold-only event was rejected because mod registry invalidation can be flushed during late-frame dispatcher work.
Scalability potential: Low tier keeps a hard 512 recipe classification budget. Middle/High/Ultra can expose more visual recipe presentation, but gameplay recipe authority remains bounded until the DataMonolith recipe index contract is expanded.
Hardware Impact: On i3/MX350 this removes a managed growth spike and copy from recipe cache rebuild. Runtime cost is one integer cap check per recipe during cache refresh.

Problem: Complex recipe raw-cost expansion treated any recipe that produced a subcomponent as usable, regardless of scan-log unlock or biome lock. A player could craft a high-level recipe from raw materials while bypassing a locked intermediate recipe gate.
Solution: Added `Fabricator.CanUseRecipeAsRawCostExpansion(RecipeData)` and required it in `CraftingSystem.TryAppendComplexRecipeChildren`. The predicate uses existing unlock and biome rules, so root recipe truth and subrecipe truth share the same authority.
Rejected Alternatives: Checking only `RecipeData.IsUnlocked` was rejected because it ignores Fabricator biome locks. Disabling recursive raw-cost expansion entirely was rejected because multiplier crafting still needs deterministic raw-cost fallback when all subrecipes are valid.
Scalability potential: All device tiers preserve identical crafting truth. The scalability lever remains presentation fidelity and cadence, not recipe authority.
Hardware Impact: One predicate per expanded graph node. No allocation, no job schedule change, no new persistent native field.

Problem: A fresh verification pass was required after these fixes.
Solution: Reran native alias Roslyn audit and zero-GC hot-path audit. Native gate: files=5, parseFailures=0, total fields=24, forbidden persistent candidates=0, jobTransientFields=13, coreMemoryAllowedFields=11. Hot-path gate: files=4, parseFailures=0, Tick/SlowTick/LateFrameTick/job Execute managed-risk hits=0. `git diff --check` showed no whitespace errors, only CRLF warnings.
Rejected Alternatives: Reporting manual inspection only was rejected because previous defects were in cold/owner routes and require scanner-backed proof.
Scalability potential: Static proof does not change runtime behavior; it prevents regression into persistent native ownership in Fabricator.
Hardware Impact: No frame cost.

Problem: Full compile verification remains blocked by project process policy.
Solution: Sampled CPU and active compiler processes. CPU was 69.76% and seven external `dotnet.exe` MSBuild processes were active, so no `dotnet build` was launched.
Rejected Alternatives: Starting another build would violate AGENTS and create noisy contention with other agents.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional workstation contention.

## Deep Domain Audit Continuation 3 - 2026-05-27
Problem: The previous hard recipe cap made cache overflow unavailable in `AppendRecipeToCache`, but `IsRecipeUnlocked` could still resolve a known recipe beyond the bitset as "not found" and then fall back to direct `RecipeData.IsUnlocked`. That created an authority bypass for public craft checks and raw-cost expansion.
Solution: `IsRecipeUnlocked` now succeeds only when the current Fabricator unlock bitset is clean and contains an in-range index. Dirty masks, unknown recipes, and overflow recipes all fail closed. Cache rebuild now passes the known unlock index into `AppendRecipeToCache`, removing O(N^2) index rediscovery and making overflow classification explicit.
Rejected Alternatives: Preserving the fallback for convenience was rejected because it violates the hard cap contract and can bypass Vault-backed visibility state. Expanding the bitset dynamically was rejected because it reintroduces Vault capacity churn.
Scalability potential: Low/Middle/High/Ultra use the same authoritative recipe cap. Larger recipe universes require a deliberate DataMonolith/index expansion, not an implicit managed fallback.
Hardware Impact: On i3/MX350 this removes repeated list scans during cache rebuild and prevents hidden managed/authority drift. Hot Tick cost remains unchanged.

Problem: Complex raw-cost expansion resolved subcomponent recipes by scanning all active Fabricators. That means Fabricator A could use Fabricator B's authored recipe as an intermediate recipe if scan/biome gates happened to pass under A's context.
Solution: Added `TryResolveOwnedRecipeForResultHash` on the current Fabricator and routed `CraftingSystem.TryAppendComplexRecipeChildren` through it. The resolver checks only current authored recipes and registered runtime recipes within `MaxRecipeCacheEntries`.
Rejected Alternatives: Keeping global active-fabricator search was rejected because instance crafting truth must be owned by the current station. Removing recursive raw-cost expansion entirely was rejected because valid multiplier crafting still needs deterministic raw-material fallback.
Scalability potential: Device tier can change presentation fidelity only. Recipe ownership stays deterministic and station-local.
Hardware Impact: Cold graph-build path becomes more predictable. No allocation, no new native field, no job schedule change.

Problem: Static active Fabricator registry used a managed `List<Fabricator>` and called `Add` without a hard capacity guard. Spawning the 65th Fabricator could allocate a larger managed backing array during runtime.
Solution: Raised the cold preallocation to 512 and added a hard cap with `s_activeFabricatorRegistryOverflowCount`. Overflow registrations are counted for telemetry and do not grow the list.
Rejected Alternatives: Leaving `List<T>` growth because registration is cold was rejected; runtime construction/spawn can still occur during play. Replacing the list with a native collection was rejected because this path is managed cold identity and the mandate targets avoiding persistent native aliases in Fabricator.
Scalability potential: Low tier supports 512 registered stations without growth. Higher tiers can place more visual stations, but active cross-station recipe lookup is capped until a first-party registry/index exists.
Hardware Impact: Avoids managed array allocation/copy on registry overflow. Normal registration remains a bounded linear duplicate scan.

Problem: Verification had to prove the new contract edits did not reopen memory violations.
Solution: Reran native alias Roslyn audit and zero-GC hot-path audit. Native gate: files=5, parseFailures=0, total fields=24, forbidden persistent candidates=0, jobTransientFields=13, coreMemoryAllowedFields=11. Hot-path gate: files=4, parseFailures=0, Tick/SlowTick/LateFrameTick/job Execute managed-risk hits=0.
Rejected Alternatives: Manual source proof was rejected because the defect was a route contract, not an obvious syntax token.
Scalability potential: Static gates prevent regression without runtime cost.
Hardware Impact: No frame cost.

Problem: Full compile verification remains blocked by AGENTS process policy.
Solution: Sampled CPU and compiler processes. CPU was 65.05% and external `dotnet.exe` MSBuild processes were active, so no `dotnet build` was launched.
Rejected Alternatives: Starting another build would violate the shared-agent CPU/compiler gate.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional workstation contention.

## Deep Domain Audit Continuation 4 - 2026-05-27
Problem: Fabricator now exposes a hard 512-entry visible recipe budget, but `HectonFabricatorUI` filtered that list through `_filteredRecipes = new List<RecipeData>(32)`. Opening the UI or switching groups with more than 32 visible recipes could grow the managed backing array, reintroducing a presentation-side allocation against the Fabricator contract.
Solution: Exposed `Fabricator.MaxRecipeCacheEntries` as an internal constant and preallocated the UI filtered recipe cache to that value. This keeps the UI scratch capacity aligned with the producer cap.
Rejected Alternatives: Duplicating a magic 512 in UI was rejected because the two caps could drift. Replacing UI filtering with a native container was rejected because this is managed presentation state, and the immediate defect is managed growth from an undersized scratch list.
Scalability potential: Low tier still shows only fixed visible rows, but filtering can traverse the full bounded Fabricator recipe set without allocation. Middle/High/Ultra can use richer hologram presentation without changing recipe truth.
Hardware Impact: On i3/MX350, avoids a managed array allocation and copy when the UI filters more than 32 recipes. Hot `LateFrameTick` scanner remains green.

Problem: Verification needed to include the new dependency file, not only Fabricator/CraftingSystem.
Solution: Expanded isolated audit scope to six files by adding `HectonFabricatorUI.cs`. Native gate: files=6, parseFailures=0, total fields=24, forbidden persistent candidates=0. Hot-path gate: files=5, parseFailures=0, Tick/SlowTick/LateFrameTick/Update/FixedUpdate/job Execute managed-risk hits=0.
Rejected Alternatives: Keeping the report at five files would hide the dependency fix from machine-readable proof.
Scalability potential: No runtime behavior drift; proof now covers the producer-consumer cap contract.
Hardware Impact: Static verification only.

Problem: Full compile verification remains blocked by AGENTS process policy.
Solution: Sampled CPU after static gates. CPU was 100%, so no `dotnet build` was launched.
Rejected Alternatives: Running build under CPU saturation would violate shared-agent build discipline.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional workstation contention.

## Deep Domain Audit Continuation 5 - 2026-05-27
Problem: Craft reservations were tied to implicit current state. `RefundIngredients()` returned early when `_activeRecipe` was null or the current `PlayerInventory.Grid` was unavailable, even though local reservation locks belong to the concrete inventory instance that reserved them. A hot-swap, null-recipe completion, or scratch disposal could therefore clear Fabricator state without releasing the original local locks.
Solution: Added `_craftReservationOwner` and routed local reserve, commit, refund, cancel, null-recipe completion, and scratch disposal through the original owner. `RefundIngredients()` now releases any counted local reservations regardless of `_activeRecipe`, then clears local count, owner pointer, network reservation, and network cost count.
Rejected Alternatives: Keeping `_playerInventory` as the release authority was rejected because GlobalRegistry/hot-swap can replace the current inventory while old reservation locks still live on the old owner. Adding a managed reservation list was rejected because the fixed `CraftReservation[]` is already the bounded route.
Scalability potential: Low/Middle/High/Ultra share identical crafting truth and rollback behavior. No quality tier changes recipe authority; visual Fabricator presentation can still scale separately.
Hardware Impact: Normal frame cost remains 0 us. Reserve/commit paths add one managed reference read and avoid stale local craft-lock starvation on i3/MX350 after hot-swap or fault recovery.

Problem: Network-cost duplicate accumulation used unchecked `int` addition. Most current buffers already merge or cap costs, but leaving the accumulator unchecked would allow a future recipe/mod route to wrap the amount and reserve less than required.
Solution: `TryAccumulateNetworkCost` now fails closed if `_networkCostAmounts[i] > int.MaxValue - amount`. Existing callers already refund local and network reservations when the method returns false.
Rejected Alternatives: Saturating to `int.MaxValue` was rejected because it can still create a materially wrong reservation request. Throwing was rejected because this is production crafting flow and must fail closed.
Scalability potential: Same deterministic recipe truth on every device. Large modded recipes must fit explicit bounds instead of relying on overflow behavior.
Hardware Impact: One integer branch only when merging duplicate network costs; no hot Tick cost.

Problem: `ModRecipeRegistry` had `List<RecipeData>(32)` and unbounded `Add`. Fabricator can classify 512 recipes without growth, but a mod registering the 33rd runtime recipe could allocate and copy the managed backing array before Fabricator even sees the cache.
Solution: Bound `ModRecipeRegistry` to `Fabricator.MaxRecipeCacheEntries`, preallocate the list to that cap, and reject new non-duplicate registrations beyond the cap with a stable error string.
Rejected Alternatives: Letting Fabricator overflow handle it later was rejected because the managed allocation happens inside the mod registry first. Duplicating the magic number in mod code was rejected because producer and dependency caps would drift.
Scalability potential: Low tier gets a fixed 512 runtime recipe overlay with no managed growth. Middle/High/Ultra need a deliberate DataMonolith/runtime recipe index expansion before exposing more authority, while presentation richness remains independent.
Hardware Impact: Avoids managed backing-array growth during mod registration and late-frame recipe invalidation. Runtime cost is one count comparison in cold registration.

Problem: Verification needed to include the newly touched mod dependency, not only Fabricator/UI.
Solution: Expanded isolated scope to eight files and reran both Roslyn scanners. Native gate: files=8, parseFailures=0, total native fields=24, forbidden persistent candidates=0, job transient fields=13, core memory allowed fields=11. Hot gate: files=5, parseFailures=0, hot owner managed-risk hits=0.
Rejected Alternatives: Reporting the ModRecipeRegistry fix without scanner coverage was rejected as a false proof artifact.
Scalability potential: Static proof only; no gameplay tier behavior changes.
Hardware Impact: No frame cost. Full build was not launched because CPU sampled at 96%, above the AGENTS 50% gate.

## Deep Domain Audit Continuation 6 - 2026-05-27
Problem: Deconstruction consumed the source item before proving the full yield route. Invalid yield entries were silently skipped, `TryEmitDeconstructionYield` only restored one-unit quantities to inventory, and any partial emission was reported as success.
Solution: `TryDeconstructItem` now builds and validates the Vault-backed yield buffer before removing the source item, then removes from a captured `PlayerInventory` owner, preflights the entire output batch through `CanAcceptItemQuantityBatch`, and restores the source if batch capacity fails. The emission fallback now uses `TryAddItem(itemHashId, quantity)` for full stack quantities.
Rejected Alternatives: Trusting world-drop registration alone was rejected because `PersistentWorldRegistry.TryRegisterDroppedItem` has no atomic batch preflight surface. Letting partial salvage stand was rejected because it breaks one input -> one deterministic output contract. Adding a managed list for output staging was rejected; stackalloc spans stay within the 256-byte limit and only live in the user-action route.
Scalability potential: Low/Middle/High/Ultra use identical deconstruction truth. Stronger hardware can spend saved correctness confidence on richer catch-bin visuals, but resource authority stays inventory/world-route deterministic.
Hardware Impact: No Tick/LateFrame cost. Deconstruction pays one bounded 32-entry stackalloc pair and one inventory simulation pass; this is user-action latency, not frame cadence.

Problem: Craft completion could publish `CraftCompleted` and restart continuous crafting when output delivery was zero or partial. That is a false success event and can hide lost output under a green UI state.
Solution: Completion now requires `deliveredQuantity == outputQuantity`. Full delivery publishes `CraftCompleted`; zero or partial delivery raises failure feedback and blocks continuous restart. Overflow feedback remains a storage bark; craft failure is only raised when a remaining stack could not be emitted.
Rejected Alternatives: Publishing completion for partial output was rejected because it makes event consumers believe the recipe transaction succeeded. Rolling back already committed ingredients at this late point was rejected because the ingredient reservation has already been committed into the inventory/logistics authorities; a separate output reservation contract is required for that future improvement.
Scalability potential: All device tiers share the same transaction truth. Visual/presentation scaling remains independent through existing Fabricator hologram and audio paths.
Hardware Impact: Normal successful completion adds one integer equality check. Failure path avoids false UI/audio state rather than saving CPU.

Problem: Verification had to be refreshed after touching source transaction semantics.
Solution: Rebuilt `.tmp/agent1329_domain_audit_scope_10`, reran `VaultNativeAliasRoslynAudit.exe` and `VoxelRuntimeHotPathAudit.exe`, and refreshed `APEX_PURGE_REPORT_1329.json`. Native gate reports files=8, parseFailures=0, totalNativeFieldDeclarations=24, forbiddenPersistentCandidates=0. Hot gate reports files=5, parseFailures=0, hot owner managed-risk hits=0. Source proof hash is `b8c204792e570af34516e4849149146957ee99c0cd703d68fea777221ec43488`.
Rejected Alternatives: Reporting the transaction fix without a new scanner pass was rejected. Launching `dotnet build` was rejected because CPU was 53% with active `dotnet.exe`/`VBCSCompiler.exe`; after a 30-second wait CPU was still 56% with active `VBCSCompiler.exe`.
Scalability potential: Static proof only; no quality-tier behavior changes.
Hardware Impact: No runtime cost. Build proof remains pending under the shared-agent CPU/compiler gate.

## Deep Domain Audit Continuation 7 - 2026-05-27
Problem: The previous deconstruction fix still left one partial-failure seam. Once the source item was removed and the first salvage yield was emitted, a later yield failure returned false but left earlier output in the world/inventory and did not restore the transaction to its original resource state.
Solution: Deconstruction output now uses a single atomic authority route: captured `PlayerInventory`. The method validates the Vault yield buffer, removes the source, preflights the entire batch against the owner inventory, adds every yield, records emitted hashes/quantities in two bounded 32-int stackalloc spans, and on any impossible post-preflight failure removes emitted quantities before restoring the source.
Rejected Alternatives: Keeping world-drop-first salvage was rejected because `PersistentWorldRegistry` exposes no atomic batch preflight or rollback API. Adding a managed transaction list was rejected because the yield count is already bounded by `CraftingSystem.MaxDeconstructionOutputCount` and stackalloc keeps the user-action route allocation-free.
Scalability potential: Low/Middle/High/Ultra use identical deconstruction authority. Low-tier machines avoid heap churn; high/ultra can spend presentation budget on richer deconstruction sparks or catch-bin visuals without changing resource truth.
Hardware Impact: Normal Tick/LateFrame cost remains 0 us. Deconstruction pays at most 32 inventory-add attempts, 32 rollback attempts only on rare post-preflight failure, and two 128-byte stackalloc spans.

Problem: Deconstruction events were emitted during the add loop. If a later output failed, already-published `CraftOutputSynthesized` events described output that could then be rolled back.
Solution: Output synthesis events are now raised in a second pass after all inventory additions have succeeded. The second pass reads the already validated Vault output buffer and does not allocate.
Rejected Alternatives: Emitting early was rejected because event consumers should observe committed transaction facts, not speculative mutation.
Scalability potential: Same behavior on every device tier; event cadence remains user-action only.
Hardware Impact: At most 32 extra loop iterations on deconstruction success. No hot-frame cost.

Problem: `CompleteCraft` could still commit reserved ingredients if a mutable recipe lost its result item or resolved to non-positive output between `StartCraft` and completion.
Solution: Added a fail-closed result/output guard before local/network reservation commit. The Fabricator refunds reservations, ends presentation state, writes blackbox active count, and raises failure feedback without consuming ingredients.
Rejected Alternatives: Letting the later delivery check catch this was rejected because ingredient authority would already be committed. Rolling back committed ingredients is not available without a new inventory/logistics transaction contract.
Scalability potential: Resource truth remains identical across low/middle/high/ultra. Presentation fidelity can still scale separately.
Hardware Impact: One completion-branch check. No Tick cost.

Problem: `CompleteCraft` still cannot be mathematically atomic for every output route because `PlayerInventory` has no public reservation-aware output preflight that simulates freeing the exact craft reservations, and `PersistentWorldRegistry` has no batch preflight/rollback for dropped-item output.
Solution: Documented the limitation and avoided editing dirty `PlayerInventory.cs`. The current Fabricator fix prevents proven losses inside its domain: invalid result before commit and deconstruction partial-yield rollback. A future cross-domain route card should add output reservation/preflight to inventory/logistics/world-drop authority.
Rejected Alternatives: Editing dirty `PlayerInventory.cs` was rejected under the cross-agent ownership rule. Calling world-drop registration before ingredient commit was rejected because the dropped item also has no rollback surface if ingredient commit fails.
Scalability potential: Future output reservation should be owner-local and bounded; it must not introduce quality-tier-dependent recipe truth.
Hardware Impact: No runtime change beyond the current Fabricator guards. Remaining risk is a documented dependency contract, not hidden optimism.

Problem: Verification had to cover the new transaction edits.
Solution: Rebuilt `.tmp/agent1329_domain_audit_scope_11`, reran native alias and hot-path Roslyn scanners, checked throw/catch/AUP text gates, and refreshed `APEX_PURGE_REPORT_1329.json`. Native gate: files=8, parseFailures=0, total fields=24, forbidden persistent candidates=0. Hot-owner gate: files=5, parseFailures=0, Tick/SlowTick/LateFrameTick/Update/FixedUpdate/job Execute managed-risk hits=0. Source proof hash is `99bbe4c9f2c36c943fc6799626975cdeb8a93eb798736b5ba769de36834ecd50`.
Rejected Alternatives: Reporting the patch without scanners was rejected. Launching another build was rejected because an external `dotnet.exe build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` and `VBCSCompiler.exe` were active; after a 30-second wait they were still active and CPU was 76%.
Scalability potential: Static proof only; no quality-tier behavior changes.
Hardware Impact: No frame cost. Build proof remains pending under the shared-agent compiler gate.

## Deep Domain Audit Continuation 8 - 2026-05-27
Problem: `EnsureRecipeCache()` rebuilt visible recipe state by calling `IsRecipeUnlockBitSet()` for each recipe. Each call pinned and unpinned the same Vault buffer, so a 512-recipe cache rebuild performed hundreds of redundant compaction-fence lock operations. On unlock-mask failure, the method could also publish an all-locked snapshot and mark the cache clean, preventing retry after transient Vault contention.
Solution: `EnsureRecipeCache()` now builds the unlock mask first, pins `ShinobuFabricatorUnlockedRecipes` once, reads a single read-only view, and passes it into `RebuildRecipeCacheFromUnlockMask`. Failure paths call `BuildFailClosedRecipeCacheSnapshot()` and keep `_recipeCacheDirty = true`, so the next owner refresh retries instead of freezing stale locked state.
Rejected Alternatives: Keeping per-recipe `TryLockBuffer` calls was rejected because compaction-safe does not mean cheap. Marking fail-closed snapshots clean was rejected because it turns transient lock contention into persistent recipe invisibility.
Scalability potential: Low hardware avoids O(recipe count) lock churn during cache refresh. Middle/High/Ultra keep the same recipe authority and can spend saved CPU on Fabricator presentation only.
Hardware Impact: On i3/MX350, a full 512-recipe refresh removes up to 511 redundant lock/unlock pairs. Normal Tick cost remains unchanged.

Problem: `RecipeRequirementDTO` and `CraftingFastFailTelemetryEntry` violated the ARM64 pointer-first layout law. Both placed 8-byte mask fields after 4-byte lanes, and `RuntimeLayoutValid()` only compared constants against literals instead of proving the actual field offsets.
Solution: Reordered both DTOs so all 8-byte masks start at offsets 0 and 8, followed by 4-byte fields. `RuntimeLayoutValid()` now checks `UnsafeUtility.SizeOf` and real offsets through `Marshal.OffsetOf` for every public field. `RecipeRequirementDTO` remains 32 bytes; `CraftingFastFailTelemetryEntry` remains 64 bytes.
Rejected Alternatives: Leaving binary-compatible but rule-violating order was rejected because the mandate prioritizes ARM64 deterministic layout. Adding padding around the old order was rejected because it preserves the wrong 8-byte-first contract.
Scalability potential: Layout is identical across device tiers and does not alter gameplay truth. High-end devices get no special binary path; presentation scaling remains separate.
Hardware Impact: No hot algorithmic cost. The binary telemetry dump layout becomes predictable and avoids unaligned 8-byte mask reads on ARM64.

Problem: Verification needed to include the newly touched fast-fail DTO file and the broader Fabricator dependency scope.
Solution: Rebuilt `.tmp/agent1329_domain_audit_scope_12`, reran `VaultNativeAliasRoslynAudit.exe` and `VoxelRuntimeHotPathAudit.exe`, reran throw/catch/AUP/lock text gates, and refreshed `APEX_PURGE_REPORT_1329.json`. Native gate: files=9, parseFailures=0, total native field declarations=35, forbidden persistent candidates=0, job transient fields=24, core memory allowed fields=11. Hot gate: files=7, parseFailures=0; hot owner findings are value-type `uint4`/`Vector3` constructions only, not managed heap allocations.
Rejected Alternatives: Reusing audit 11 was rejected because it did not include the DTO layout patch. Launching `dotnet build` was rejected because CPU sampled at 91% and an external `dotnet.exe` process was active.
Scalability potential: Static proof only; no quality-tier behavior changes.
Hardware Impact: No frame cost. Full compile proof remains pending under the shared-agent CPU/compiler gate.
