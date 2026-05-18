# Rationale_SHINOBU_19

Date: 2026-05-17
Agent: SHINOBU_19
Domain: ECHELON 4 / S.O.A. Inventory + Crafting Fast-Fail
State: IMPLEMENTED / COMPILE BLOCKED BY EXTERNAL DOMAINS
Hygiene: Rationale file was absent at session start; created fresh for current batch.

## Initial Decision Baseline
Problem: Object-oriented inventories and `List<Item>` create cache misses and managed allocation pressure during looting/crafting.
Solution: Use a Structure of Arrays ledger with unmanaged `NativeArray<uint>`, `NativeArray<int>`, and `NativeArray<float>` storage, with atomic quantity mutation and preflight craft rollback.
Rejected Alternatives: Managed item classes, polymorphic resources, dictionaries in hot paths, and ScriptableObject mutation at runtime. They are slower, GC-prone, or editor-persistent.
Scalability potential: Low uses fixed SoA scans and time-sliced recipes; Middle increases recipe batch size; High keeps all recipe masks warm; Ultra can spend saved CPU on richer UI/editor visualization without touching runtime transactions.
Hardware Impact: Estimated looting/craft validation gain on i3/MX350 is removal of managed allocations and branch-heavy object traversal; exact profiler proof absent until Unity/GCMonitor verification.

## Loop 1 - Recon / DTO Boundary
Problem: Existing inventory already exposes hash/count/condition SoA mirrors, but hot mutations still live behind a grid owner and crafting uses managed recipe authoring lists at the edges.
Solution: Add a separate SHINOBU_19 transaction kernel over raw `NativeArray<uint/int/float>` lanes and keep ScriptableObject/List usage editor-only or cold authoring-only.
Rejected Alternatives: Rewriting `PlayerInventory` in-place would touch thousands of lines and collide with parallel agents; managed `List<Item>` or dictionary ledgers would violate the SoA mandate and add GC.
Scalability potential: Low uses fixed linear scans; Middle batches recipe checks; High keeps recipe masks and counts warm; Ultra spends saved budget on richer debug/tuner views.
Hardware Impact: Expected i3/MX350 gain is eliminating per-loot object traversal and per-craft managed allocations; target transaction path remains fixed-buffer and branch-bounded.

Problem: Runtime DTOs in this domain must survive ARM64 padding and CS1612 pitfalls.
Solution: Use sequential unmanaged structs with explicit total sizes, public fields, and static layout audit functions instead of mutable struct properties.
Rejected Alternatives: `Pack=1` was rejected for runtime DTOs because it creates unaligned loads; C# properties returning structs were rejected because they trigger copy-mutate bugs.
Scalability potential: Stable DTOs can be memory-mapped, dumped, copied, and processed by Burst jobs across Low through Ultra tiers without authoring-object hydration.
Hardware Impact: Cache-line predictable 32/64-byte records reduce unaligned access penalties on weak mobile-class CPUs and low-end desktop silicon.

## Loop 2 - Atomic Ledger / Crafting Core
Problem: Looting and crafting need one mathematical source of truth, but `Dictionary`/object inventory models fragment reads and create GC risk.
Solution: Added `Shinobu19EconomyLedger` with raw hash/quantity/durability `NativeArray` lanes, `IndexOf`, `TryTransactItem`, Vault buffer resolution, and `Interlocked.CompareExchange` CAS mutation to prevent underflow and partial writes.
Rejected Alternatives: `Interlocked.Add` alone was rejected for negative deltas because it cannot fail before underflow; managed locks and dictionaries were rejected for cache misses and allocation pressure.
Scalability potential: Low scans fixed contiguous arrays; Middle batches recipe masks; High keeps DTO/mask buffers in DataVault; Ultra can overdraw editor diagnostics without changing runtime truth.
Hardware Impact: Estimated i3/MX350 gain is 100-150 us per loot/craft burst versus managed item traversal; exact profiler proof remains blocked by project compile wall.

Problem: Crafting could delete components if output insertion or second ingredient deduction fails.
Solution: `TryCraftAtomicRollback` preflights all required quantities and output capacity, then rolls back already-deducted components if a later atomic step conflicts.
Rejected Alternatives: Direct sequential `RemoveItem` calls were rejected because they create partial-craft failure states.
Scalability potential: Low tier uses two-component DTO masks; High/Ultra can layer DAG closure and recipe batches while preserving the same rollback kernel.
Hardware Impact: Prevents save/economy corruption without managed transaction objects; estimated failure-path recovery stays under a few contiguous scans.

Problem: Tools, lockers, hotbar, and consume/metabolism dependencies are parallel-agent boundaries.
Solution: Added unmanaged local signal DTOs implementing `ISignal`, mock consume generation, durability degradation, transfer job scheduling with `JobHandle.CombineDependencies`, and hotbar/equip routing through hash slots.
Rejected Alternatives: Direct calls into UI, metabolism, locker, audio, and VR bridge systems were rejected as compile-coupled and fragile under 20-agent churn.
Scalability potential: Low emits bounded signals; Middle/High prewarms lanes; Ultra can keep richer signal diagnostics without object payloads.
Hardware Impact: Avoids managed callbacks and cross-domain component lookups during item use; saves roughly 20-40 us per event fanout on low-end CPUs.

Problem: Core compile verification cannot currently complete because `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs` has an unrelated missing-context `EnsureDirectoryPage` compile fault after a brace drift.
Solution: Treated this as an external compile wall and did not edit SaveSystem. SHINOBU code reached the compiler after the local `NativeParallelMultiHashMap` correction.
Rejected Alternatives: Patching SaveSystem from the inventory domain was rejected as architectural overreach.
Scalability potential: Keeping the fix isolated preserves concurrent-agent integration boundaries.
Hardware Impact: No runtime impact; compile proof remains blocked outside SHINOBU scope.

## Loop 3 - Carry Lie / Save / Loot
Problem: Backpack volume can become a fake 3D packing simulation if left unconstrained.
Solution: Encumbrance is a Dear Lie: a scalar mass/volume sum from OSHINO constants, output as `EncumbranceSignal`; no item positions or packing solver exist in the runtime truth.
Rejected Alternatives: 3D backpack coordinates, physics packing, collider volume checks, and per-item Transform searches.
Scalability potential: Low uses one scalar pass; Middle caches totals; High/Ultra can render fancy UI packing as pure presentation.
Hardware Impact: Saves an estimated 0.05-0.1 ms on i3/MX350 when inventory opens or loot changes by avoiding fake geometry math.

Problem: Loot magnet must not walk world GameObjects or allocate query lists.
Solution: Added `ShinobuLootMagnetSpatialQueryJob` over `NativeParallelMultiHashMap<int, DebrisSpatialEntry>`, converts AUP to sector-local `float3` before distance checks, then inserts via the same atomic ledger.
Rejected Alternatives: `Physics.OverlapSphere`, `FindObjectsOfType`, and per-loot `GetComponent` were rejected as allocation/hitch risks.
Scalability potential: Low checks adjacent cells only; Middle can widen cells; High/Ultra can run larger radius or richer pickup VFX through decoupled signals.
Hardware Impact: Estimated 100+ us saved per pickup sweep versus GameObject scans on low-end silicon.

Problem: Save export can bloat WAL payloads with empty slots.
Solution: Added RLE export to Vault scratch byte buffer with empty-run compression and no managed byte arrays in the runtime path.
Rejected Alternatives: JSON, ScriptableObject snapshots, BinaryFormatter, and per-slot managed serialization.
Scalability potential: Low keeps compact WAL writes on MicroSD; Ultra can increase inventory capacity with proportional compression of empty spans.
Hardware Impact: Reduces Steam Deck MicroSD write pressure; exact byte gain depends on empty-slot density.

## Loop 4 - Human Control / Editor Facade
Problem: Designers need recipe and item constants control without recompiling C#.
Solution: Added `EconomyRecipeTunerWindow` with searchable recipes, raw SoA x-ray, DataVault recipe DTO writeback, CSV physical override parser, and optional editor polling.
Rejected Alternatives: Runtime ScriptableObject mutation as source of truth and hand-editing binary blobs.
Scalability potential: Low runtime remains pure Vault data; High/Ultra editor can expose richer diagnostics without touching gameplay jobs.
Hardware Impact: No hot-path cost; editor allocations are intentionally outside runtime.

Problem: CSV ingestion needs stable hashes but must not allocate in the parser.
Solution: Runtime parser accepts `ReadOnlySpan<char>`, uses FNV-1a and manual numeric parsing, and writes `ItemPhysicalConstantsDTO` directly into unmanaged constants.
Rejected Alternatives: `string.Split`, LINQ, reflection, and culture-sensitive parsing were rejected for GC and nondeterminism.
Scalability potential: Low parses changed rows only when driven by the editor/file watcher; High can bulk-reload large balance tables.
Hardware Impact: Removes GC spikes during balance reload; editor file I/O remains cold/human-facing.

## Loop 5 - H8CR Import / Full Ingredient Rollback

Problem: The first implementation could craft from fallback two-component DTOs, but OSHINO H8CR binaries carry a separate 171-row ingredient table. Ignoring that table could undercharge recipes with more than two ingredients.
Solution: Added `CraftingIngredientDTO` as a 16-byte ARM64-aligned runtime DTO, `BufferID.ShinobuRecipeIngredients`, `TryResolveRecipeIngredientBuffer`, and `HydrateCraftingRecipesFromH8Cr`. The importer validates H8CR magic/version/endian probe/header bytes/64-byte recipe stride/16-byte ingredient stride/ranges/reserved fields using fixed little-endian byte reads, then fills Vault recipe DTOs, requirement masks, and ingredient DTOs.
Rejected Alternatives: Runtime JSON parse, ScriptableObject ingredient lists, or trusting the first two ingredient fields were rejected because they either allocate or weaken economy correctness.
Scalability potential: Low tier can hydrate the 7,424-byte H8CR blob once into flat Vault arrays; Middle/High/Ultra reuse the same authoritative masks and can spend presentation budget on fabricator effects without changing item truth.
Hardware Impact: Expected low-end gain is eliminating text parse and managed authoring-object traversal during fabricator open. Verifier proof: `VerifyCraftingCosts.py` passed with H8CR 7,424 bytes, 50 recipes, 171 ingredients, 38 tools, 50 God-Mode visual records, CRC32 `1295072744`, 16-byte alignment, and 0 hash collisions.

Problem: Craft rollback needed to be bulletproof for duplicate ingredient hashes and ingredient counts greater than two.
Solution: Added a full-table overload of `TryCraftAtomicRollback` that preflights each unique ingredient hash by summing duplicate rows, validates quantity sufficiency before mutation, removes all components with CAS-backed `TryTransactItem`, and rolls back every already-deducted unique hash if a later atomic conflict or output insert failure occurs. `ShinobuCraftTransactionJob` and `ShinobuRecipeFastFailJob` now optionally consume `RecipeIngredients`.
Rejected Alternatives: Per-row sequential remove calls were rejected because duplicate hashes can pass naive checks and partial deletion can corrupt the economy.
Scalability potential: The same transaction math handles small fallback recipes and full H8CR recipes; Ultra can add larger recipe surfaces without changing the rollback invariant.
Hardware Impact: Saves failure-path debugging time and avoids player-visible item loss. CPU remains bounded by fixed native-array scans and no heap-backed transaction object.

Problem: Designer-facing binary import still needed a human bridge without making runtime depend on editor/asset domains.
Solution: Added a cold EditorWindow H8CR importer that reads `Data/Economy/Crafting_Costs.h8bin`, copies bytes into `NativeArray<byte>`, resolves Vault recipe/ingredient buffers, and calls the same runtime parser. All file I/O and managed byte arrays are editor-only.
Rejected Alternatives: Adding runtime `File.ReadAllBytes`, JSON hot reload, or direct calls into crafting asset classes from the runtime ledger were rejected for GC, I/O, and compile-coupling risk.
Scalability potential: Low devices ship with prehydrated or cold-imported binary data; high-end editor workflows can inspect and tune without touching the hot-path kernel.
Hardware Impact: No gameplay-frame cost; import work is cold/editor-only.

Problem: Current project build still cannot complete, and the compile wall moved after other agents changed core/gameplay/VFX domains.
Solution: Ran one controlled `dotnet build Hecton8.Core.csproj --no-restore /v:minimal` and wrote the output to `Docs/AgentLogs/Build_SHINOBU_19_latest.txt`. The build failed in external domains: `AupOriginShiftCoordinator` missing `DispatcherJobSwap`, `SomaticKinematicsRuntime` missing `_state/_tuning/_blackBox` and related fields, `SpatialAudioManager` ref-return errors, and `BiolumPulseSyncRuntime` missing GPU/CSV fields. Search of the build log found 0 SHINOBU/inventory/economy errors.
Rejected Alternatives: Patching origin, somatic, audio, or VFX domains was rejected as cross-domain sabotage and would hide the real compile wall.
Scalability potential: The inventory work remains isolated behind Vault buffers and typed signals, preserving parallel-agent integration.
Hardware Impact: No runtime impact; compile proof is blocked outside SHINOBU scope.

## Loop 6 - L1 Layout Polish / CRC Guard / Build Truth

Problem: The final ARM64 audit needed byte-level proof, not a verbal claim. A stale probe initially used the wrong telemetry field shape, so it was discarded. The actual source layout then exposed a real defect: `ShinobuCarryTotalsDTO` contained 36 bytes of fields while the struct contract claimed 32 bytes.
Solution: Verified actual DTO offsets with a local `Marshal.OffsetOf` probe, corrected `ShinobuCarryTotalsDTO` to `Size = 40`, added `Reserved0`, initialized it on writeback, and updated `RuntimeLayoutValid()` to require 40 bytes. Rechecked all SHINOBU runtime DTO families; every probed struct is now a multiple of 8 bytes.
Rejected Alternatives: Leaving the declared size at 32 was rejected because runtimes ignore impossible undersized layout contracts once fields exceed the declared size. Shrinking carry totals by deleting fields was rejected because the scalar Dear Lie needs mass, volume, load, multiplier, and frame stamp for blackbox/debug correlation.
Scalability potential: Low and ARM64 targets avoid unaligned array stride; High/Ultra keep the same Vault stride while presentation layers can read totals without defensive copies.
Hardware Impact: Prevents a 36-byte stride from crossing cache-line boundaries unpredictably in the carry totals buffer. The gain is correctness and ARM64 alignment stability, not a claimed profiler win.

Problem: H8CR import needed corruption detection inside the runtime parser, not only in the Python verifier.
Solution: Added payload CRC32 validation against the H8CR header value, plus strict header/stride/range/alignment checks for recipe, ingredient, tool, and God-Mode visual sections. The runtime parser still receives a `NativeArray<byte>` from cold editor/bootstrap code; it does not perform hot-path file I/O.
Rejected Alternatives: Trusting the binary because the offline verifier passed was rejected; silent recipe corruption would become an economy exploit.
Scalability potential: Low tier hydrates once and uses masks; High/Ultra can ship larger recipe banks while keeping the same O(1) mask gate.
Hardware Impact: Cold-load CRC cost is paid once; it prevents undefined craft behavior without touching frame simulation.

Problem: Editor tuning was initially able to edit fallback two-component DTO quantities, but full H8CR recipes need designer control over the whole ingredient window.
Solution: The editor facade now resolves `ShinobuRecipeIngredients`, detects `Reserved1/Reserved2` ingredient windows, displays every ingredient row, writes quantities back into Vault, recomputes `TotalMassGrams`, mirrors the first two rows into fallback DTO fields, and rebuilds full masks from the complete ingredient table. CSV hash fallback was also corrected to match the project `LocHash.Compute` UTF-16 FNV-1a contract.
Rejected Alternatives: Reflection over recipe assets and `string.Split` CSV parsing were rejected for editor coupling and GC habits bleeding into runtime code.
Scalability potential: Low runtime remains flat Vault data; editor-only rich tuning can grow without polluting Burst jobs.
Hardware Impact: No gameplay-frame cost. Human iteration cost drops because binary recipe data can be inspected and edited without recompiling C#.

Problem: A fresh full build after the carry DTO fix did not complete inside the 129-second command timeout and other agents had concurrent `dotnet build` processes active.
Solution: Stopped rebuild spam. The latest partial log contains no SHINOBU/inventory/economy diagnostics before timeout. Previous complete build evidence remains external-domain blocked, and current process inspection showed unrelated concurrent builds that were not killed.
Rejected Alternatives: Killing unknown `dotnet` processes or repeatedly rebuilding was rejected because this project is explicitly running 20+ agents in parallel.
Scalability potential: Compile-time isolation is preserved; SHINOBU changes remain in isolated runtime/editor files plus a single BufferID reservation.
Hardware Impact: Protects developer iteration hardware from unnecessary rebuild churn.

## Loop 7 - Atomic Split-Stack Transaction Polish

Problem: Re-audit found a real economy race. Negative `TryTransactItem` deducted from only the first matching stack, so a recipe could fail even when total quantity existed across duplicate SoA slots. The insertion path also had a ghost-slot risk: after claiming an empty hash lane, a failed quantity CAS could clear the hash while another writer had already made the quantity positive.
Solution: Split the transaction kernel into acyclic positive and negative helpers. Positive deltas use `TryApplyPositiveDelta`; existing-stack quantity mutation locks the quantity lane by CASing `current` to `-current`, computes the new value, then publishes the final non-negative count with `Interlocked.Exchange`. Negative deltas use `TryApplyNegativeDeltaAcrossSlots`, deduct across every matching hash until the full requested quantity is removed, and re-add any partial removal through the non-recursive positive helper if a late conflict prevents completion. `int.MinValue` deltas are rejected before negation.
Rejected Alternatives: Keeping one-stack subtraction was rejected because it turns duplicate resource stacks into false craft failures. A recursive rollback call back into `TryTransactItem` was rejected because Burst can reject static call cycles even when the runtime branch is bounded. Hash-lane cleanup of nonzero-hash/zero-quantity stale slots was rejected because it can clear a slot another writer has reserved but not yet populated.
Scalability potential: Low tier still does contiguous O(N) scans and avoids managed transaction objects. Middle/High/Ultra can tolerate duplicate stack lanes from parallel pickups/transfers without corrupting craft truth or blocking richer fabricator presentation.
Hardware Impact: The new lock is an `int` CAS on the existing quantity lane, not a managed lock. Expected cost is a few extra atomic instructions only on contended slots; it buys correctness under loot burst/crafting overlap and prevents save-visible ghost quantities.

Problem: Build verification needed to be retried after the transaction patch without creating rebuild spam.
Solution: Ran one controlled `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly`. It failed in `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs` because `ISignal` is unresolved at lines 80 and 88. No SHINOBU/inventory/economy diagnostics were emitted.
Rejected Alternatives: Patching TerminalOS from the inventory domain was rejected as cross-domain overreach. Re-running builds after an unrelated UI contract error was rejected under the compile-wall protocol.
Scalability potential: SHINOBU stays isolated behind Vault and typed unmanaged lanes while the UI owner repairs its missing contract import/reference.
Hardware Impact: Compile work was limited to one pass; no additional rebuild loop was started.

## Loop 8 - Ghost Slot Scrub / Blackbox Fault Trigger

Problem: A mathematically correct ledger still needs a repair path for corrupted or externally-mutated SoA lanes. A slot with `Hash != 0` and `Quantity <= 0`, `Hash == 0` with orphan quantity/durability, or non-finite durability can clog capacity and mislead the editor x-ray even if normal SHINOBU transactions do not create that shape.
Solution: Added `ScrubGhostSlots` and `ShinobuGhostSlotScrubJob`. The scrubber clears orphan hash/quantity/durability combinations with `Interlocked` reads/writes and resets non-finite durability to `DefaultDurability01`. This is a boot/pre-simulation/fatal-recovery repair pass, not a concurrent producer path.
Rejected Alternatives: Ignoring ghost lanes because RLE export treats zero/negative quantities as empty was rejected; save export is not the same as runtime capacity repair. Running a managed cleanup list was rejected because it creates heap state and undermines the SoA contract.
Scalability potential: Low runs one contiguous scrub pass only when requested. Middle/High/Ultra get deterministic repair without changing the gameplay transaction surface or adding object ownership.
Hardware Impact: No per-frame cost. A cold O(N) pass prevents capacity loss from stale external writes and protects the raw memory debugger from false occupied slots.

Problem: The blackbox path recorded telemetry and had dump functions, but the fatal trigger was not explicit enough. A spike flag written by the Burst job needed a cold-path bridge that actually emits `.h8dump`.
Solution: Added `TelemetryFlagSpike`, `TelemetryFlagFatal`, `EconomyDumpMagic`, and `TryDumpTelemetryOnFault`. The telemetry job now writes `TelemetryFlagSpike`; after the producer fence, a caller can scan the 300-entry ring and synchronously emit `Docs/AgentLogs/Dump_ECONOMY.h8dump` only if a spike/fatal flag or threshold breach is present.
Rejected Alternatives: File I/O inside the Burst telemetry job was rejected because managed I/O is illegal there. Dumping every frame was rejected for Steam Deck MicroSD pressure.
Scalability potential: Low pays no disk cost unless faulted; Middle/High/Ultra can keep richer postmortem fields in the fixed 64-byte entry without changing gameplay truth.
Hardware Impact: Hot path remains a single flag OR and ring write. Disk write is fault-only and outside the job.

Problem: A fresh compile pass was required after Loop 8, but the project has concurrent agents and compile walls outside the inventory domain.
Solution: Ran one controlled `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly`. The build is now blocked by external core/physics errors: `GlobalTelemetryBus.Blackbox.cs` missing `TryBindBlackboxVaultBuffersNoLock`, `GlobalPhysicsStateManager.cs` missing many `Shinobu37PhysicsCulling` partial members, and `SubmarineDynamicsRuntime.cs` ambiguous `math.min`. No SHINOBU/inventory/economy diagnostics were emitted.
Rejected Alternatives: Patching GlobalTelemetryBus, GlobalPhysicsStateManager, or SubmarineDynamicsRuntime from this inventory task was rejected under the domain-boundary and 3-strikes compile-wall protocols.
Scalability potential: SHINOBU remains compile-isolated except for the existing Core project wall; no new sibling runtime dependency was added.
Hardware Impact: One build pass only; no rebuild spam.
