# Rationale 1317 - MEMORY_SOVEREIGN_PLAYER_INVENTORY_EXORCIST

Status: SCOPE PASS; GLOBAL COMPILE BLOCKED BY OTHER DOMAIN.

## D0 - Batch Hygiene And Domain Lock

Problem: Agent 1317 had no status or rationale files in the active batch folder.
Solution: Created fresh per-agent files before code mutation. Primary domain is `PlayerInventory.cs` and `Assets/_Project/Scripts/Inventory`.
Rejected Alternatives: Reusing another agent's log or writing chat-only state would violate batch traceability.
Scalability potential: Low/Middle/High/Ultra unaffected; this is execution trace only.
Hardware Impact: 0 us runtime impact on i3/MX350.

## D1 - PlayerInventory Native Owner Exorcism

Problem: `PlayerInventory.cs` held 63 forbidden native field candidates before migration, including 49 persistent owner arrays and 14 kernel fields that were not recognized as transient jobs.
Solution: Replaced persistent owner fields with `InventoryVaultLane<T>` descriptors backed by `VaultGenerationHandle<T>` and `GlobalDataVault`; marked radioactive/reactive kernels as `IJob` so native fields classify as transient job parameters.
Rejected Alternatives: Keeping `NativeArray<T>` fields and only adding comments was rejected because it leaves relocation-dangling aliases. Replacing with managed arrays was rejected because it breaks Burst/DOD and GC policy.
Scalability potential: Low uses same data with lower cadence already controlled elsewhere; Middle/High/Ultra retain contiguous vault arrays and Burst job inputs for visual-overkill inventory chemistry/radiation effects.
Hardware Impact: Removes long-lived unmanaged aliases; expected memory-compaction safety gain is structural. Per-call vault resolution/write-lock cost is pending profiler verification; direct microsecond saving is not claimed.

## D2 - Buffer ID Ownership

Problem: Fixed buffer IDs around 73200 risked collision with existing routing IDs and with multiple `PlayerInventory` instances.
Solution: Assigned an instance-strided runtime range starting at 410000, above the current observed `BufferID` enum maximum, and derived 49 lane IDs from the component instance bucket.
Rejected Alternatives: Reusing legacy `ShinobuInventory*` IDs was rejected because only three lanes existed. Fixed IDs were rejected because bulk transfer can involve another `PlayerInventory` instance.
Scalability potential: Low/Middle/High/Ultra all keep one descriptor route per lane; extra instances receive separate vault lanes instead of aliasing.
Hardware Impact: 0 us in hot loops after cold base resolution; avoids data corruption that would cost a crash, not microseconds.

## D3 - Locking And Fault Telemetry

Problem: Migration created write paths that could silently fail when vault resolution or write-lock acquisition fails.
Solution: Wrapped mutating helpers with `TryAcquireWriteLock`/`finally ReleaseWriteLock`; encoded vault fault `BufferID` and generation into the existing 64-byte telemetry ring without managed logs.
Rejected Alternatives: Throwing exceptions or logging strings was rejected due GC and main-thread stall. Holding locks across frames was rejected by mandate.
Scalability potential: Low skips failed visual/state maintenance safely; Middle/High/Ultra preserve deterministic data path and can spend saved stability budget on chemistry/radiation presentation.
Hardware Impact: Failure-only telemetry write is a fixed struct assignment. Normal-path cost is one vault resolve or lock acquisition at mutation boundaries; profiler proof still required.

## D4 - Inventory Folder Sweep

Problem: `Assets/_Project/Scripts/Inventory` still had 29 forbidden candidates before the sweep: stack-like resolver buffer structs plus a static `NativeHashMap` in `ItemTemplateRegistry`.
Solution: Converted resolver buffer structs to `ref struct` stack-only views and removed the static native map, replacing lookup with a bounded linear scan of the managed template snapshot.
Rejected Alternatives: A persistent static `NativeHashMap` was rejected because it is a non-vault native owner. A new vault hash table was deferred because template lookup is cold and no profiler evidence showed O(n) cost above 0.1 ms.
Scalability potential: Low/Middle tolerate cold O(n) lookup; High/Ultra can later upgrade to a vault-owned open-address table if profiling proves need.
Hardware Impact: Removes one persistent native static allocation. Low-end cost risk is cold lookup only, not per-frame inventory tick.

## D5 - Layout Guard And Compile Gate

Problem: DTO layout had to remain ARM64 explicit and provable after migration.
Solution: Added editor-only `ValidateInventoryMemorySovereigntyLayouts1317()` with exact `UnsafeUtility.SizeOf` and offset checks for inventory telemetry DTOs and vault generation handle size.
Rejected Alternatives: New editor source file was rejected because generated project files are stale and would not compile it until Unity regeneration. Runtime hot validation was rejected.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; editor guard stops unsafe DTO drift.
Hardware Impact: 0 us runtime outside editor. First build attempt found local unsafe pointer inference errors; fixed. Second build has 0 `PlayerInventory.cs` errors and is blocked by `SubmarineStructuralGrid.cs`, outside this domain.

## D6 - APEX Re-Audit Purge

Problem: Broad post-review gates required no visible `throw new`, no `catch (Exception)`, and a fresh native-field proof over the exact C# files touched by agent 1317.
Solution: Re-ran the Roslyn native field audit and narrowed its ledger to five touched C# files. Converted routing layout validation from exception to fail-closed `bool`. Replaced broad dump catches with specific I/O/path catches. Kept cold binary dump writers because they are crash/postmortem paths, not simulation loops.
Rejected Alternatives: Reporting broad catches as "cold only" was rejected because the scanner would still show a managed exception surface. Removing binary dumps was rejected because the black-box mandate requires fixed-size postmortem artifacts.
Scalability potential: Low/Middle/High/Ultra unchanged. Runtime hot loops get no new managed allocation. Failure paths return numeric false-state and keep telemetry/dump lanes intact.
Hardware Impact: 0 us normal-frame cost. Re-audit shows 145 native field declarations in scope: 117 transient job parameters, 28 stack-only view fields, 0 persistent native aliases.

## D7 - APEX Final Gate Closure

Problem: The rejection protocol required a fresh prompt extraction, syntax-tree native-field audit, hot-path allocation purge proof, AUP cast audit, compaction-lock proof, and refreshed machine-readable artifacts.
Solution: Re-extracted the `AGENT_PROMPT id="1317"` block, reran the Roslyn native-field audit, filtered the ledger to the five touched C# files, verified branchless inner job loops, and rewrote AUP conversions to clamp double local deltas before every float cast.
Rejected Alternatives: Running a compile while CPU was above 50 percent and seven compiler processes were active was rejected by the repository build gate. Treating unclamped residual-to-float casts as harmless was rejected because the AUP mandate requires clamp-before-cast even after origin subtraction.
Scalability potential: Low tier keeps inventory math bounded and branchless in inner loops; Middle tier retains continuous `GlobalQualityWeight` cadence; High and Ultra tiers keep Burst-compatible contiguous vault views for richer chemistry/radiation presentation without persistent native aliases.
Hardware Impact: Persistent native owner count in touched scope is 0. Hot-loop scanner reports 0 managed string/LINQ/foreach hits and 0 branch hits in the audited inner job loop bodies. No microsecond savings beyond structural compaction safety are claimed without profiler capture.

## D8 - Rejection Rerun Corrections

Problem: The prior proof still tolerated hot-method value-type `new` syntax and briefly introduced explicit layout on generic descriptor structs, which is a compile-risk on the CLR.
Solution: Replaced hot job object initializers with `default` plus direct field writes; converted `InventorySoaVaultLane` and `InventoryRoutingVaultLane` into non-generic explicit 24-byte descriptors that reconstruct typed `VaultGenerationHandle<T>` only as method-local values.
Rejected Alternatives: Keeping generic explicit layout was rejected because it can fail type loading. Reporting value-type `new` as harmless was rejected because the audit protocol demanded hostile token-level proof in hot methods.
Scalability potential: Low tier keeps the same bounded inventory/routing math without managed allocations; Middle/High/Ultra retain continuous `GlobalQualityWeight`-governed cadence and can spend saved stability budget on richer presentation, not more CPU truth.
Hardware Impact: Hot-method token scanner now reports 0 hits across audited `Execute`, `SlowTick`, and `LateFrameTick` bodies. Roslyn native audit remains 0 persistent native aliases in scope. No profiler microseconds are claimed.

## D9 - Descriptor Compile Repair

Problem: The non-generic descriptor rewrite removed generic type evidence at `OpenLane`/`ReadLane` call sites and removed the old `.Handle` member used by `PlayerInventory_SoaQuery.cs`.
Solution: Added explicit generic type arguments at every SOA, CargoSync, and routing lane open/read call; changed the partial generation check to reconstruct a method-local `VaultGenerationHandle<uint>` through `ToHandle<uint>()`.
Rejected Alternatives: Reverting to generic explicit-layout descriptor structs was rejected because it reintroduces CLR layout risk. Adding a public untyped `.Handle` field was rejected because it would hide type ownership and weaken the descriptor contract.
Scalability potential: Low/Middle/High/Ultra unchanged; this is compile correctness and metadata reconstruction only. Continuous `GlobalQualityWeight` math remains untouched.
Hardware Impact: 0 us hot-loop cost beyond existing method-local handle reconstruction. Static hot-path scan remains 0 forbidden allocation/string/LINQ tokens. `dotnet build` now fails only in non-1317 files according to `Docs/AgentLogs/Build_1317_rerun5.log`.

## D10 - Rejection Rerun 5 Verification

Problem: The rejection protocol required another prompt extraction, syntax-tree native and hot-path proof, full offset-map refresh, and a build retry under the repository CPU/compiler gate.
Solution: Re-extracted the 1317 XML block; reran `VaultNativeAliasRoslynAudit`; compiled a temporary Roslyn scanner outside the repository for hot-method syntax-node scanning; regenerated offset, branch, AUP, and machine report artifacts; ran `dotnet build Hecton8.Core.csproj --no-restore` when CPU was below 50 percent and no compiler process existed.
Rejected Alternatives: Treating the range scanner alone as syntax-tree proof was rejected. Starting the build while CPU was above 50 percent was rejected. Editing world vegetation compile blockers was rejected because they are outside agent 1317 domain.
Scalability potential: Low/Middle/High/Ultra unchanged. Inventory truth remains vault-owned; presentation/routing quality remains continuously governed by `GlobalQualityWeight`; no binary quality switch or CPU physical solver was added.
Hardware Impact: 0 us claimed. Static result remains 0 persistent native aliases, 0 Roslyn hot-path allocation/string/LINQ hits, and 0 direct AUP cast hits in the six-file scope. Build failure is external: `HectonMapMagicVegetationBridge.cs` unresolved vegetation-memory symbols, with 0 scoped error lines.

## D11 - Layout Law Correction After Rejection

Problem: The previous offset proof was size-valid but not strict enough: several explicit DTOs placed 64-bit fields after 32-bit fields, and padding-only fields used `ulong`/`uint`, hiding layout holes instead of proving them byte by byte.
Solution: Reordered true 64-bit data fields to the front of their DTOs, converted all padding-only `ulong`/`uint`/`int` fields in the 1317 scope to explicit private byte fields, and added cold offset guards to SOA, cargo, and routing layout validators.
Rejected Alternatives: Keeping old offsets and explaining that ARM64 tolerates them was rejected because the mandate requires pointer-first ordering. Using large numeric padding fields was rejected because it obscures byte holes and weakens auditability.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime. DTO truth layout is now deterministic for weak ARM64 hardware and high-tier Burst paths without changing gameplay ownership or `GlobalQualityWeight` behavior.
Hardware Impact: 0 us claimed. The gain is structural: `RERUN6_OFFSETS` reports 27 explicit structs, 0 violations, 0 non-byte private padding fields, and 0 8-byte fields after offset 9. Build retry failed only in `HectonVoxelEngine.cs`, outside this domain.

## D12 - Evidence Pass After Rejection Rerun 7

Problem: The previous response still depended on RERUN6 proof for branch-loop evidence and did not separately write a RERUN7 strict-final artifact after the repeated rejection.
Solution: Re-extracted the 1317 prompt; reran Roslyn native, explicit layout, Roslyn hot-path, token/AUP, compaction-lock, and branch-loop scanners; wrote both expanded and strict-final JSON artifacts for RERUN7.
Rejected Alternatives: Relaunching `dotnet build` while CPU was 92.2 percent with active `dotnet/csc` workers was rejected by the repository rule. Expanding edits outside inventory to silence unrelated build blockers was rejected by domain boundary.
Scalability potential: Low/Middle/High/Ultra unchanged. No new CPU solver or binary quality switch was added; inventory truth stays vault-owned, and solver-like inventory effects stay bounded by continuous `GlobalQualityWeight`.
Hardware Impact: 0 us claimed. Static evidence: 16-domain-file native scan has 264 declarations and 0 persistent fields; touched six-file scan has 145 declarations and 0 persistent fields; 27 explicit DTO maps have 0 layout violations; modified solver inner loops have 0 branch hits.

## D13 - Rerun 8 Branch Scope Correction

Problem: A full hot-loop Roslyn branch scan found 58 branch nodes, but the gate only bans branches in mathematical solver inner loops. Treating all transactional fail-closed loops as solver loops would force unsafe rewrites of validation and CAS logic.
Solution: Kept the raw full-loop branch artifact and added a filtered solver proof for `InventoryMassVolumeJob`, `InventoryRadioactiveHalfLifeKernel`, and `InventoryReactiveChemistryKernel`. Solver branch count is 0; non-solver branch count is explicitly recorded as 58. Corrected the offset scanner regex before final emission so `[FieldOffset(...), SerializeField]` fields are included in the DTO maps.
Rejected Alternatives: Rewriting cargo merge and routing transaction loops branchlessly was rejected because those are authority/validation paths where fail-closed bounds checks are required. Hiding the 58 raw hits was rejected because it would be a false report.
Scalability potential: Low/Middle/High/Ultra unchanged. Solver-like inventory presentation work stays branch-clean and controlled by continuous `GlobalQualityWeight`; transactional correctness keeps explicit guards.
Hardware Impact: 0 us claimed. Fresh compile was attempted under the CPU/compiler gate and failed only in `HectonVoxelEngine.cs`; 1317 scope error count is 0.
