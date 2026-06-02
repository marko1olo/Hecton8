# Rationale 1325 - Persistent World Registry Memory Sovereignty

Status: REAUDIT37 PRIMARY DOMAIN STATIC GREEN WITH EXTERNAL RESIDUALS. Reaudit37 build attempt failed on external Candice SQLite references; runtime profiler proof absent.

## Decision 0001 - Scope Discipline
Problem: The prompt names a broad world sweep but the authoritative primary target is `PersistentWorldRegistry.cs`, and sibling files may be actively modified by other agents.
Solution: Run primary AST scan and migration first; run `git status` before any sibling file inspection or mutation.
Rejected Alternatives: Blind folder-wide rewrites would create merge conflicts and route ownership damage.
Scalability potential: Low keeps memory traffic stable by removing stale aliases; Middle/High/Ultra can spend saved memory safety margin on larger residency windows without changing DTO truth.
Hardware Impact: i3/MX350 gain is crash-risk reduction and avoided relocation stalls; no measured frame delta yet.

## Decision 0002 - Proof Before Rewrite
Problem: The prompt asserts 19 forbidden fields, but source reality must decide the actual patch.
Solution: Build a machine-readable hit list from source syntax, then patch only confirmed field aliases.
Rejected Alternatives: Trusting prompt counts and deleting names that may not exist would break compilation.
Scalability potential: Low through Ultra all benefit from deterministic owner maps; no quality-tier branch introduced.
Hardware Impact: Static pass cost is offline; runtime impact pending source findings.

## Decision 0003 - Mandate Set
Problem: Memory sovereignty touches native collections, DTO layout, DataVault routes, telemetry, and registry hot-path law.
Solution: Read 8 targeted mandates before code mutation and bind status ledger to those mandates.
Rejected Alternatives: AGENTS-only interpretation misses DataVault and ARM64 details.
Scalability potential: Low/Middle/High/Ultra behavior must remain continuous via `GlobalQualityWeight`, not binary device gates.
Hardware Impact: Prevents weak-silicon ARM64 misalignment and MX350 memory churn; exact microseconds pending implementation.

## Decision 0004 - Primary Alias Count
Problem: The assignment claims exactly 19 forbidden aliases, while source line scan also shows three adjacent `NativeParallelHashSet` fields.
Solution: Use the existing Roslyn AST scanner as the formal Task 01 proof for the exact 19 prompt-targeted aliases and track the three hash sets as sovereignty-relevant adjacent aliases for the migration plan.
Rejected Alternatives: Inflating the prompt count to 22 would falsify the task metric; ignoring the hash sets would leave persistent unmanaged aliases that still block defragmentation safety.
Scalability potential: Low removes relocation crash risk first; Middle keeps O(1)-style lookup capacity; High/Ultra can raise residency and save snapshot density through continuous budget math after aliases are handle-backed.
Hardware Impact: Scan-only decision has no runtime gain; it prevents an estimated 0.2-1.0 ms stall class from stale pointer recovery paths on i3/MX350 by forcing DataVault ownership before runtime proof.

## Decision 0005 - Vault Identity Route
Problem: The prompt suggests a speculative `SystemID.WorldRegistry`, but `H8Memory.cs` does not define that identity.
Solution: Use `SystemID.WorldStreaming` for all PersistentWorldRegistry DataVault handles and assign local stable `BufferID` constants in the unused 74450-74517 range.
Rejected Alternatives: Adding a core `SystemID` during a localized registry patch would mutate shared identity API and create cross-agent risk; using `SystemID.External` would erase ownership evidence.
Scalability potential: Low keeps a single owner for survival capacity; Middle/High/Ultra can scale buffer capacities through continuous quality math without changing authority route.
Hardware Impact: i3/MX350 avoids registry lookup ambiguity and release-lock stalls; expected direct frame gain is only after migration, not from identity selection.

## Decision 0006 - DTO Churn Rejection
Problem: Task 04 demands DTO layout extraction, but the target buffer DTOs already use explicit layouts with 8-byte-multiple sizes.
Solution: Preserve existing DTO definitions and add later validator proof instead of rewriting correct layouts.
Rejected Alternatives: Mechanical DTO rewrites would risk save-format drift and break binary persistence for no measurable gain.
Scalability potential: Low through Ultra preserve save identity and cache footprints; compact delta remains 4 records per 64-byte line.
Hardware Impact: Avoids regression risk; no runtime microseconds claimed.

## Decision 0007 - Descriptor Wrapper Route
Problem: Replacing 19 field aliases with raw individual handle fields would require hundreds of invasive call-site edits in one pass and would raise compile-risk without adding safety beyond DataVault ownership.
Solution: Replace direct native fields with `VaultBacked*` descriptor structs that store only `IDataVault`, `VaultGenerationHandle<T>`, `BufferID`, `SystemID`, and scalar capacity, while resolving physical arrays only inside method scope.
Rejected Alternatives: Keeping any `Native*` persistent field fails the Roslyn proof; rewriting every method to raw handles immediately risks behavioral drift and wider save/load breakage.
Scalability potential: Low remains fail-closed if vault is unavailable; Middle preserves current capacity; High/Ultra can raise capacity through `maxTrackedItems` without changing public authority.
Hardware Impact: On i3/MX350 this removes stale-pointer relocation risk; direct microsecond savings are not claimed until compile/runtime profiling. Added per-access handle resolution is a known cost to optimize after correctness proof.

## Decision 0008 - Tombstone Job Lock Bridge [SUPERSEDED]
Problem: The tombstone decay job formerly wrote to a persistent `NativeList<int>` and could not accept a descriptor wrapper.
Solution: Initial bridge reconciled the job to resolved `NativeArray` input/output plus a count buffer. This was later rejected because `TryLockBuffer` ownership could cross frame/dispatcher boundaries.
Rejected Alternatives: Passing DataVault handles into Burst; retaining a pinned job view after the APEX lock audit.
Scalability potential: Superseded by Decision 0017. Current path uses frost-cadence metadata pruning and no job/pin.
Hardware Impact: The bridge is no longer in touched source. Current gain is compaction safety, not measured frame time.

## Decision 0009 - Telemetry Dump Scope
Problem: A hot-path managed log cannot prove the last 300 frames after a relocation fault.
Solution: Add a 64-byte explicit `WorldTelemetryEntry` ring in DataVault and queue a cold binary dump only on catastrophic event codes.
Rejected Alternatives: Synchronous `FileStream` writes in frame code would stall weak CPUs; managed string warning logs are not a post-mortem artifact.
Scalability potential: Low writes sparse failure telemetry; Middle/High/Ultra can increase visual systems without changing telemetry truth.
Hardware Impact: Hot-path cost is two small DataVault writes only on failure; expected normal-frame cost is 0 us on i3/MX350.

## Decision 0010 - Broad Sweep Collision Boundary
Problem: The XML asks for a broader world sweep after the primary target, but `git status` shows active sibling edits and untracked vegetation sovereignty files.
Solution: Stop broad mutation at the primary target, record the collision list, and preserve the after-ledger as evidence that broader debt remains.
Rejected Alternatives: Editing sibling files during active parallel ownership would create merge conflicts and false authority over other agents' work.
Scalability potential: Low stays stable by fixing the registry crash vector first; Middle/High/Ultra retain existing broader world behavior until owners migrate their buffers.
Hardware Impact: i3/MX350 receives primary registry relocation safety; broader files still carry 310 static candidates and remain unclaimed by this patch.

## Decision 0011 - Validator Placement
Problem: Task 18 needs fail-closed ARM64 layout proof without expanding ownership into a new editor file that could collide with another validator agent.
Solution: Place `WorldMemorySovereigntyValidator1325` in `PersistentWorldRegistry.cs` under `#if UNITY_EDITOR`, with explicit size and offset checks for every affected DTO.
Rejected Alternatives: A separate editor script would be clean structurally but increases cross-agent file surface; comment-only layout tables do not halt bad changes.
Scalability potential: Low through Ultra share the same DTO binary identity; visual quality scaling does not mutate save layout.
Hardware Impact: Prevents weak-silicon unaligned DTO regressions; normal runtime cost is 0 us outside editor validation.

## Decision 0012 - Static Proof Instead Of Build Fraud [SUPERSEDED BY COMPILE WALL]
Problem: Project rules forbid build launch under active `dotnet`/`csc` or CPU pressure; the machine initially reported 100% CPU with compiler/runtime processes.
Solution: Do not launch a build while the guard is red. When the guard later cleared, launch `Assembly-CSharp.csproj` once and record the real failure.
Rejected Alternatives: Starting a build under guard; claiming compile success without a build.
Scalability potential: Low/Middle/High/Ultra source gates remain static-only until dependency owners clear compile errors.
Hardware Impact: Initial guard avoided CPU contention; later build failed on non-1325 files and produced no runtime microsecond proof.

## Decision 0013 - No Broad Zero Claim
Problem: The after-ledger reports 310 broader forbidden candidates, while the primary target reports zero.
Solution: Claim zero only for `PersistentWorldRegistry.cs`; report broader world as blocked debt with top offender files visible in the ledger.
Rejected Alternatives: Reporting the whole world scope as clean would be a fake optimization report.
Scalability potential: Low gets one owner route corrected; Middle/High/Ultra still need later staged vault migrations for remaining world systems.
Hardware Impact: Exact frame gain is not measured; estimated savings are limited to removing primary stale-alias crash/stall paths.

## Decision 0014 - AUP Clamp Repair
Problem: Reaudit found `AUPMath.ResolveCameraRelative` subtracting in double but casting to `float3` without clamping the resulting delta.
Solution: Patch `AUPMath` so camera-relative and runtime float conversions subtract in double, clamp via `ClampRuntimeFloatDelta`, then cast to `float3`.
Rejected Alternatives: Claiming the path was compliant because subtraction happened before casting; that misses the explicit clamp requirement.
Scalability potential: Low prevents far-boundary jitter from invalid float overflow; Middle/High/Ultra preserve the same AUP truth and can render larger scenes without changing save identity.
Hardware Impact: i3/MX350 pays a tiny deterministic clamp cost on conversion and avoids catastrophic far-field precision artifacts.

## Decision 0015 - Validator Expansion
Problem: The first validator covered the main registry DTOs but omitted `AbsoluteUniversePositionBlit128`, `PersistentThermalVentRecord`, `ResourceNodeTombstoneRecord`, and several public field offsets.
Solution: Expand `WorldMemorySovereigntyValidator1325` to cover those DTOs and field offsets, then write the complete byte map into `APEX_PURGE_REPORT_1325.json`.
Rejected Alternatives: Byte maps in chat only would not guard future source edits.
Scalability potential: Low/Middle/High/Ultra all keep stable binary payloads; quality scaling cannot mutate layout.
Hardware Impact: Prevents ARM64 unaligned regressions; runtime player cost is 0 us because validator is editor-only.

## Decision 0016 - Static Green Boundary
Problem: The override demands `VERIFIED_GREEN`, but AGENTS still forbids runtime optimism without compile/player/profiler proof.
Solution: Mark the APEX report green only for static gates over touched C# files and keep compile/runtime verification explicitly absent in status/log text.
Rejected Alternatives: Claiming runtime proof without a build, or returning failed gates after fixing the static findings.
Scalability potential: Low through Ultra source gates pass; runtime performance still requires a quiet machine and profiler run.
Hardware Impact: No measured frame delta; compile guard protected shared CPU from more contention.

## Decision 0017 - Cross-Frame Pin Rejection
Problem: The tombstone decay collection job used resolved Vault views and would require `TryLockBuffer` ownership until the scheduled job was finalized, creating a pinned-view window that could span frames.
Solution: Delete the tombstone collection job path and replace it with cadence-bound synchronous metadata pruning inside the registry phase; all remaining mutation happens through descriptor wrappers that acquire and release write locks inside the same method call.
Rejected Alternatives: Keeping transient job fields as a loophole; holding `TryLockBuffer` across dispatcher boundaries; force-completing the job in the same frame.
Scalability potential: Low uses the cheapest frost-tick metadata pass; Middle/High/Ultra can increase tombstone capacity or cadence through continuous budget variables later without changing save truth or DTO layout.
Hardware Impact: i3/MX350 loses a tiny amortized job opportunity but removes compaction deadlock and stale-pointer risk; expected cost is bounded to the existing frost cadence, not every frame.

## Decision 0018 - Compile Wall Boundary
Problem: The guarded `Assembly-CSharp.csproj` build failed with 192 errors in files outside this agent's ownership, including submarine atmosphere, PDA, audio, vegetation memory, and fluid engine sources.
Solution: Record the compile wall as an external dependency failure and do not mutate unrelated domains to manufacture a green build.
Rejected Alternatives: Editing foreign systems outside the assigned world registry domain; claiming compile success from static scans.
Scalability potential: Low/Middle/High/Ultra source behavior in the touched files remains statically green; executable proof remains blocked until dependency owners repair their files.
Hardware Impact: Build wall has no runtime microsecond claim. It prevents a false readiness report.

## Decision 0019 - Proof Trail Correction
Problem: The final override pass found stale proof metadata: the APEX JSON carried the previous touched-file hash and the exorcism report still referenced re-audit3.
Solution: Rerun the Roslyn scanner into `VAULT_NATIVE_ALIAS_LEDGER_1325_APEX_REAUDIT4.json`, recompute the touched-file hash, and update status/report/log artifacts to match the files on disk.
Rejected Alternatives: Returning a chat JSON that disagrees with disk artifacts; treating the stale hash as harmless.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime. The gain is audit determinism: one source state, one hash, one report route.
Hardware Impact: Runtime cost is 0 us. Offline scan cost is accepted because it prevents false-green memory sovereignty reporting.

## Decision 0020 - Reaudit 5 Boundary
Problem: The override was repeated after a clean static result, so stale proof drift and concurrent workspace edits had to be ruled out again.
Solution: Rerun native field and zero-GC syntax scanners, write re-audit5 proof artifacts, and keep the final claim scoped to the two touched C# files instead of the full dirty workspace.
Rejected Alternatives: Reverting or editing unrelated reports from other agents; launching `dotnet build` while CPU is above the documented 50% guard; counting cold async save allocations as Tick allocations.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. The pass improves audit integrity only.
Hardware Impact: Runtime cost is 0 us. Offline scan wall time is accepted; no measured frame-time savings are claimed.

## Decision 0021 - Reaudit 6 Repeat Rejection Handling
Problem: The same override was issued again, and relying on the prior clean JSON would be a stale proof path.
Solution: Re-extract the XML prompt, rerun native and hot-path syntax scanners into re-audit6 files, and update the proof ledger pointer without touching source code because no new defect was found.
Rejected Alternatives: Rewriting already-green code to create activity; relaunching build while CPU is 86.05% with active `csc`/`dotnet`; reverting unrelated dirty reports from other agents.
Scalability potential: Low/Middle/High/Ultra unchanged. The only value is proof freshness under concurrent workspace churn.
Hardware Impact: Runtime cost is 0 us. No frame-time gain claimed; compile remains blocked by guard and prior non-1325 compile wall.

## Decision 0022 - PoolSlot AUP Width Repair
Problem: `PoolSlotData` stored AUP grid coordinates as `int3`, while `AbsoluteUniversePosition` uses 64-bit grid cells. `WritePoolSlotPosition` cast `long` to `int`, corrupting hydration/dehydration positions beyond signed 32-bit cell range.
Solution: Expand `PoolSlotData` to 72 bytes and store `GridX`, `GridY`, and `GridZ` as explicit 64-bit fields before 4-byte local offset data. Update the validator, read/write helpers, and JSON byte map.
Rejected Alternatives: Saturating to `int.MinValue/int.MaxValue` would avoid wraparound but would still collapse valid far-sector identity; leaving the old `int3` because the older registry mandate showed it would violate the current AUP precision law.
Scalability potential: Low/Middle/High/Ultra keep one save/runtime coordinate truth. Wider pool slots cost 8 bytes per tracked item but remove a deterministic far-world corruption path.
Hardware Impact: At 8192 pool slots the extra memory is roughly 64 KB. i3/MX350 cost is negligible compared with avoiding wrong-slot hydration, transform jumps, and recovery scans.

## Decision 0023 - DataVault Hot-Swap Rebind
Problem: `PersistentWorldRegistry` listened for several `GlobalRegistry` service replacements but ignored `DataVault`, so a vault service replacement could leave generation handles bound to an obsolete vault instance.
Solution: Handle `GlobalRegistryServiceSlot.DataVault` by canceling pending hydration, dehydrating live proxies before descriptor disposal, releasing old Vault-backed buffers, rebinding to the current vault, reinitializing descriptors, and refreshing memory budget data.
Rejected Alternatives: Polling `GlobalRegistry.DataVault` in hot getters would violate the global registry doctrine; doing nothing leaves stale handle state under compaction or replacement.
Scalability potential: Low fails closed by dropping live proxy state if the vault disappears; Middle/High/Ultra can recover through load/restore paths without changing DTO layout or authority ownership.
Hardware Impact: Normal runtime cost is 0 us. Replacement path is cold and intentionally heavy; it prevents stale-pointer failures that would otherwise force crash recovery.

## Decision 0024 - Direct Native View Surface Reduction
Problem: After the descriptor migration, several unused helpers still exposed direct `NativeArray` views or native collection byte-count signatures inside the primary file.
Solution: Remove unused `VaultBackedArray.AsArray`, `AsCapacityArray`, `CountArray`, and dead `GetNative*Bytes` helpers. The cold telemetry dump now reads ring entries through the wrapper indexer rather than resolving a mutable array view.
Rejected Alternatives: Keeping unused methods for convenience increases accidental escape surface; rewriting external save/PDA consumers would cross into files currently owned by other agents.
Scalability potential: Low/Middle/High/Ultra unchanged at gameplay level. The benefit is a smaller native alias API inside the registry.
Hardware Impact: Runtime hot cost is unchanged. Cold dump reads are slower by at most 300 wrapper reads, accepted because dumps only run on fault.

## Decision 0025 - Save Snapshot View Escape Repair
Problem: `SaveManager` held `PersistentWorldRegistry.GetSaveSnapshotArray()` and `EcosystemDirector.GetSaveSnapshotArray()` views across `await` and background compression, so transient native views could outlive the owner phase.
Solution: Add `PersistentWorldRegistry.SaveSnapshotCount` and `CopySaveSnapshotDeltas`, then make `SaveManager` allocate owned persistent snapshot copies before leaving the snapshot pause phase. Ecosystem snapshot views are copied immediately on the same route.
Rejected Alternatives: Pinning read-only aliases in `GlobalDataVault` has no release API in the current contract and would block compaction; editing dirty PDA/cartography code would interfere with another agent.
Scalability potential: Low pays a save-time copy only during explicit save; Middle/High/Ultra retain the same save payload truth while allowing vault compaction between async phases.
Hardware Impact: Runtime frame cost is 0 us outside saves. Save cost is O(snapshot count) memory copy, accepted because it replaces a stale-pointer failure route.

## Decision 0026 - Hot Scratch Growth Guard
Problem: `RefreshHydrationWindow` used `_recordIndexScratch` with capacity 128 and `VaultBackedMultiHashMap.CopyValuesForKey` appended without a capacity guard, allowing managed `List<T>` growth in the hot hydration scan.
Solution: Pre-size `_recordIndexScratch` to `maxTrackedItems`, pre-size `_floraSpawnStateScratch` to `maxTrackedItems`, and add `destination.Count >= destination.Capacity` guards in wrapper copy methods.
Rejected Alternatives: Allowing managed list growth because dense chunks are uncommon would violate the zero-GC policy; replacing the hydration pass with a new job would reintroduce pinned view scheduling complexity.
Scalability potential: Low avoids unpredictable GC in dense chunks; Middle/High/Ultra can increase tracked object density without changing save identity or Vault DTO layout.
Hardware Impact: i3/MX350 pays roughly 64 KB for the int scratch at 16k capacity and avoids resize spikes in residency scanning.

## Decision 0027 - Locked Save Snapshot Copy
Problem: The new `CopySaveSnapshotDeltas` route initially copied through a pure read view. That removed async escape, but a large save snapshot could still read from an unpinned Vault view for longer than a trivial accessor.
Solution: Add `VaultBackedList.CopyTo` with short `TryAcquireWriteLock` fences on the items and count buffers, then release both in `finally` immediately after the copy.
Rejected Alternatives: Per-element wrapper reads are compaction-safe but too slow for large saves; read-only alias pinning lacks a release route and would block compaction.
Scalability potential: Low copies under one bounded fence during explicit save; Middle/High/Ultra can carry larger save snapshots without increasing per-element resolve overhead.
Hardware Impact: i3/MX350 save-time CPU drops from O(N) handle resolves to one locked linear copy. Normal frame cost remains 0 us.

## Decision 0028 - Fail-Closed Save Snapshot Count
Problem: Allocating the save-copy buffer from `SaveSnapshotCount` depended on a pure Vault count read. If the read failed under a compaction fence, save could silently treat the persistent-world snapshot as empty.
Solution: Add `SaveSnapshotCapacity` and `TryCopySaveSnapshotDeltas`. `SaveManager` allocates capacity, then fails the save if the locked copy cannot acquire the source buffers. Zero copied records is accepted only after the locked copy succeeds.
Rejected Alternatives: Silent zero-record save; retry loops during save; pinning aliases across async compression.
Scalability potential: Low/Middle/High/Ultra get the same fail-closed save contract. Larger snapshots over-allocate only during explicit saves and are trimmed by read-only subarray length.
Hardware Impact: Save-time memory may temporarily allocate up to the registry capacity instead of current count. At 16k records this is about 1 MB, accepted to prevent world-state truncation.

## Decision 0029 - Final Proof Aggregation Correction
Problem: The final PowerShell proof aggregation initially used the wrong JSON schema assumptions: native findings were read from a non-existent per-file array, and hot value-type creations were compared against `ObjectCreation` instead of the scanner's `value_type_creation` marker.
Solution: Recompute proof from the actual scanner schema: root `findings` for the native ledger and `kind == value_type_creation` for permitted stack/value creations in hot owners.
Rejected Alternatives: Manually editing the numbers to match the status file; treating the aggregation mismatch as harmless; rerunning source mutations without a real defect.
Scalability potential: Low/Middle/High/Ultra unchanged. This is audit correctness only: the disk report now has one verifiable interpretation.
Hardware Impact: Runtime cost is 0 us. Offline proof cost is accepted to prevent false positive or false green reporting.

## Decision 0030 - Save Snapshot Alias API Removal
Problem: `PersistentWorldRegistry.GetSaveSnapshotArray()` still exported a `NativeArray<PersistentWorldDeltaRecord>.ReadOnly` view. `PlayerExplorationTracker` consumed that view immediately, but the API itself kept a native alias escape route alive.
Solution: Delete the registry save-snapshot view exporter and `VaultBackedList.AsArray()`. Add `TryReadSaveSnapshotDelta(index, out record)` for bounded, method-local Vault reads, and update only the two PDA call-sites that consumed the old API.
Rejected Alternatives: Leaving the obsolete API with an `[Obsolete]` attribute still permits unsafe callers; adding a PDA bulk native scratch would create another persistent native owner outside 1325; broad PDA migration would interfere with active dirty work by another agent.
Scalability potential: Low reads at most `MaxPoiRevealPerSlowTick` deltas with no alias retention. Middle/High/Ultra can later use a PDA-owned copy scratch if profiling proves per-index reads are too expensive.
Hardware Impact: i3/MX350 may pay several extra method-local Vault resolves per slow tick, bounded by POI reveal cap. The gain is compaction safety and removal of a stale-view class, not measured frame-time speed.

## Decision 0031 - Fail-Closed Persistent Save Snapshot Copy
Problem: The save-boundary repair still used `throw new InvalidOperationException` when `PersistentWorldRegistry.TryCopySaveSnapshotDeltas` failed. The path is cold, but the failure reason is a known Vault copy/lock/stale-handle condition and should not depend on managed exception handling.
Solution: Replace that throw with an explicit fail-closed branch: dispose the owned copy buffer, release snapshot pause, write async persistence telemetry with numeric failure code `3`, publish failed save status, dump the save black box, record failure state, raise the save-failed event, and return.
Rejected Alternatives: Keeping catch-based handling for a predictable Vault refusal; retrying during save and risking frame stalls; broad rewriting of unrelated SaveManager exception paths outside 1325 ownership.
Scalability potential: Low/Middle/High/Ultra behavior is identical: a blocked persistent-world snapshot refuses the save instead of silently truncating data or throwing through the async pipeline.
Hardware Impact: Normal frame cost is 0 us. Failure path avoids exception allocation/unwind cost on weak CPUs and leaves a deterministic telemetry code for post-mortem.

## Decision 0032 - PersistentWorldDeltaRecord Layout ABI Repair
Problem: `PersistentWorldDeltaRecord` was treated as runtime/Vault DTO data but still used the old file-order layout: `int3` at offset 0, 4-byte padding at 12, then `ulong` at 16. Size was valid, but the pointer-first ARM64 rule was violated for the runtime array/list/snapshot payload.
Solution: Move `ItemPersistentIdHash` to offset 0, then `ChunkId`, then 4-byte, 2-byte, and byte fields, with explicit 8-byte padding through offset 56. Preserve pre-v5 raw save compatibility through a private cold `PersistentWorldDeltaRecordLegacy64` in `SaveBinaryStorage` and convert legacy records field-by-field into the runtime DTO.
Rejected Alternatives: Keeping the misordered runtime DTO because v5 saves are compact; that ignores NativeArray/Vault residency. Raw-copying legacy bytes into the new layout would silently corrupt old saves. Changing v5 compact ABI would create unnecessary save-format churn.
Scalability potential: Low devices keep deterministic save/load without alignment traps or compatibility loss. Middle/High/Ultra can increase snapshot density/cadence without changing DTO identity; compact v5 remains the bandwidth-efficient path.
Hardware Impact: Normal frame cost is 0 us. Legacy load pays one cold field-copy per pre-v5 delta. ARM64 runtime reads avoid the misordered 8-byte field hazard and keep the DTO validator enforceable.

## Decision 0033 - Hydration Service Cache and Warmup Fail-Closed
Problem: `HydrateRecord`, `DehydrateRecord`, and platform velocity inheritance still reached through `GlobalRegistry` during residency work. `HydrateRecord` also called `pool.Warmup(prefab, 1)` when a prefab was loaded but its pool was absent, which can create runtime instantiation pressure in the exact path that should only consume prewarmed reserves.
Solution: Add cached `IObjectPoolService` and `ISubmarineRuntimeContext` fields, refresh them from the cold cache and hot-swap listener, and make hydration/dehydration use only cached services. Remove the runtime pool warmup; missing reserve returns false and is requeued through the existing catalog prewarm flow. Delete the unused async hydration session method that still polled `GlobalRegistry.PersistentWorldRegistry`.
Rejected Alternatives: Keeping service-locator reads because they are convenient; calling `Warmup` with count 1 as an emergency fallback; rewriting object-pool ownership outside this agent's domain.
Scalability potential: Low devices avoid surprise instantiation in residency frames. Middle/High/Ultra can raise hydration density without changing the rule that pools are prepared before use, not grown from the hot path.
Hardware Impact: Normal frame cost removes two service-locator property reads and eliminates a potential runtime instantiate spike. Exact microseconds are scene-dependent; safe estimate is 1-20 us saved per hydration burst plus avoided millisecond-scale prefab creation if a reserve was missing.

## Decision 0034 - Hydration Item Lookup Cache Split
Problem: `HydrateRecord` and `QueueWorldPrefabPrewarmForRecord` resolved item data through `TryResolveItemData`, which could call `TryEnsureItemLookup`. That cold method clears and refills a managed `Dictionary<ulong, ItemData>` and `List<ItemData>`, so a missing or changed catalog could push managed cache rebuild work into residency/hydration.
Solution: Keep catalog rebuild in cold routes (`CacheRegistryServicesCold`, player/player-inventory hot-swap, and async sector prewarm), add `TryResolveCachedItemData` for hydration, and fail closed when the cache is not ready. Null catalog clears stale lookup state.
Rejected Alternatives: Allowing lazy lookup rebuild in hydration; adding a NativeHashMap mirror of catalog data without ItemCatalog ownership; rebuilding every residency scan to chase mod catalog mutations.
Scalability potential: Low devices avoid dictionary/list churn in hydration bursts. Middle/High/Ultra can raise hydration density while item catalog work remains a cold prep cost, not a frame residency cost.
Hardware Impact: Avoids a scene-dependent managed cache rebuild spike in hydration. Safe estimate is 10-200 us avoided on small catalogs and higher on modded catalogs; normal cached read cost remains a dictionary probe.

## Decision 0035 - Finite AUP Rails And Hydration Slot Identity
Problem: `AUPAxisDeltaClamped` used extreme double rails that could overflow squared-distance math, while dehydration cleanup confused record indexes with pool-slot indexes. A record index is not guaranteed to equal a pool index after restore, tombstone pruning, or compaction-era sparse records.
Solution: Return finite runtime clamp rails from AUP delta math and remove the unused unchecked `AUPDelta` helper. In registry cleanup, resolve the pool slot from the record before touching hydrated slot arrays, and size the hydrated record lookup to `maxTrackedItems`.
Rejected Alternatives: Keeping the extreme double sentinel because it "works" for comparisons would still poison `math.lengthsq`; assuming record index equals pool index is only true in the narrow append-only case and breaks after sparse lifecycle paths.
Scalability potential: Low avoids far-field overflow and wrong proxy cleanup. Middle/High/Ultra keep the same AUP truth while increasing residency density without slot identity corruption.
Hardware Impact: i3/MX350 avoids infinite distance branches and wrong-slot dehydrate churn. Expected normal-frame cost is a few scalar comparisons; avoided recovery work can be milliseconds if a proxy slot is corrupted.

## Decision 0036 - Fail-Closed Vault Append Transactions
Problem: Multiple registry paths wrote state flags, indexes, or lookup maps after unchecked `AddNoResize`, `TryAdd`, or `Enqueue` calls. Under DataVault lock refusal, full buffers, or compaction pressure, that creates partial state: stuck hydration flags, orphan delta records, missing chunk indexes, or stale map entries.
Solution: Add transactional helpers for record append plus chunk index, compact delta append plus entity index, and record chunk moves. Queue flags now roll back when append/enqueue fails. Auxiliary maps write numeric `WorldTelemetryCapacityMismatch` entries instead of silently dropping data.
Rejected Alternatives: Trusting capacity prechecks; they do not prove the subsequent DataVault lock will succeed. Retrying in hot paths was rejected because contention loops would eat the frame budget and block compaction.
Scalability potential: Low fails closed with telemetry and no partial publish. Middle/High/Ultra can raise capacities and hydration density while the same append contracts preserve one fact -> one owner -> one route.
Hardware Impact: Normal path adds branch checks only. Failure path avoids persistent corruption and recovery scans; on weak CPUs this is worth more than the branch cost because it prevents save/residency repair work.

## Decision 0037 - Delta Truth Before Runtime Success
Problem: Several registry write paths mutated `_records`, resource/flora state, or chunk indexes before verifying that the compact delta/save truth was accepted. On delta buffer/index refusal, the caller could receive success while the next save missed the fact.
Solution: Make `UpsertDeltaRecord` and `UpsertDeletedTombstone` return `bool`; append callers now roll back the appended record when upsert fails, and existing-record updates write `_records` only after the delta accepts the new state. `MarkRecordCollected` and live-instance sync also stop before runtime mutation if tombstone/delta publication fails.
Rejected Alternatives: Keeping telemetry-only failure reporting would still let runtime and save truth diverge. Retrying in the frame path was rejected because DataVault contention must fail closed, not spin.
Scalability potential: Low devices get deterministic refusal instead of silent save loss under full buffers. Middle/High/Ultra can raise capacities without changing the authority route: compact delta is accepted first, runtime state follows.
Hardware Impact: Normal path adds branch checks and no allocation. Failure path avoids save corruption and later recovery scans; expected saved time is scene-dependent, but preventing one corrupted hydration/save repair can avoid millisecond-class work.

## Decision 0038 - Tombstone Index Ownership Split
Problem: Hibernated fauna victims were registered as fauna tombstones and also inserted into the resource-node tombstone set using the fauna instance UID. That crossed fact ownership and could make unrelated resource nodes appear tombstoned if IDs collided.
Solution: Remove the resource tombstone registration from fauna predation. Fauna uses `TryRegisterFaunaTombstone` only, and that method now registers deleted UID state only after compact tombstone DTO construction succeeds.
Rejected Alternatives: Treating `_resourceNodeTombstoneIds` as a generic tombstone cache violates the one fact -> one owner rule and wastes resource tombstone capacity.
Scalability potential: Low/Middle/High/Ultra keep separate tombstone indexes for fauna and resource nodes. This prevents rare but severe ID alias bugs without adding a solver or per-frame work.
Hardware Impact: Saves one hash-set insertion on hibernated predation and avoids false resource suppression. Normal frame cost is unchanged outside rare fauna population trimming.

## Decision 0039 - Full-Capacity Chunk Move Rollback
Problem: Using the forward `TryMoveRecordIndexToChunk` as a rollback path adds the old chunk index before removing the current one. On a full multi-map this can fail during rollback even though removing the current entry would have freed capacity.
Solution: Add `RollbackRecordChunkMove`, which removes the current chunk mapping first and then restores the previous mapping. Use it only on failed delta publication after a tentative chunk move.
Rejected Alternatives: Reusing the forward move helper is simpler but not capacity-safe. Removing first in the normal forward path was rejected because it would lose the old mapping if the new add failed.
Scalability potential: Low devices with tight capacities get reliable rollback behavior. Middle/High/Ultra keep dense chunk movement without duplicate/stale index routes.
Hardware Impact: Runtime success path unchanged. Failure path does one remove then one add; the cost is accepted because it preserves map consistency under capacity pressure.

## Decision 0040 - Fail-Closed Restore Publication
Problem: Cold restore still contained best-effort tombstone and compact-delta publication. If a loaded deleted/resource/metamorphosis record hit a full DataVault-backed set/list or stale handle, restore could keep partial runtime state while save truth or lookup truth was missing.
Solution: Convert loaded tombstone pre-registration to `TryPreRegisterLoadedRecordTombstones`, add `AbortRestoreAfterFailure`, and route deleted/resource/metamorphosis/normal restore branches through checked compact-delta, chunk, tombstone, and record append contracts. Abort clears restored runtime state and writes numeric capacity telemetry.
Rejected Alternatives: Continuing restore after a missing tombstone is a silent persistence lie. Retrying in the load phase was rejected because a full/stale DataVault handle needs operator-visible refusal, not unbounded contention.
Scalability potential: Low fails closed with no partial world. Middle/High/Ultra can raise capacities and indexed-save density without changing the one fact -> one owner route.
Hardware Impact: Normal load adds branch checks only. Failure path avoids corrupt saves and follow-up repair scans; expected i3/MX350 gain is stability, not frame-time speed.

## Decision 0041 - Persistent Save Snapshot Refusal Contract
Problem: `CaptureSaveSnapshot()` returned `void`, so staging failure, save-snapshot capacity exhaustion, or resource tombstone index refusal could produce an empty/partial persistent-world snapshot while SaveManager still continued.
Solution: Make `CaptureSaveSnapshot` return `bool`, clear partial snapshot buffers on refusal, and make `SaveManager` abort the save with failure code `3`, black-box dump, status signal, and save-failed event. Resource tombstone staging now checks both delta upsert and hash-set registration.
Rejected Alternatives: Saving a partial world snapshot is worse than failing the save. Throwing was rejected because this is a known DataVault refusal path and belongs to deterministic telemetry, not managed exception handling.
Scalability potential: Low devices avoid silent world loss under tight capacity. Middle/High/Ultra can carry larger deltas while save truth remains atomic: capture succeeds or the save fails.
Hardware Impact: Normal save adds one boolean branch. Failure path avoids exception allocation/unwind and prevents a later corrupt-load recovery class on weak CPUs.

## Decision 0042 - Checked Vault Element Writes
Problem: `VaultBackedList<T>` exposed an indexer setter that called a Vault write lock and silently returned on lock/stale-handle failure. Critical record, compact-delta, pool-slot, and telemetry writes could therefore look successful to the caller while no native write occurred.
Solution: Add `TryWrite` and route critical writes through `TryWriteRecordAt`, `TryWriteCompactDeltaRecordAt`, `TryWritePoolSlotDataAt`, and checked telemetry writes. Callers now return false, abort restore/save, or roll back local state instead of publishing partial truth.
Rejected Alternatives: Leaving the indexer as a convenience API violates fail-closed semantics. Retrying under contention was rejected because compaction fences must not be spin-waited from frame paths.
Scalability potential: Low devices fail closed under pressure instead of accumulating corrupt state. Middle/High/Ultra can raise capacities and hydration density while preserving the same one-fact ownership route.
Hardware Impact: Normal path adds branch checks only. Failure path prevents save/residency corruption that would cost millisecond-scale recovery work on i3/MX350-class hardware.

## Decision 0043 - Checked Vault Clears And Hydration Queue Cursor Safety
Problem: Vault-backed `Clear()` calls returned no status. `_pendingHydrationRecords` could fail to clear but still reset `_pendingHydrationReadIndex` to 0, causing duplicate hydration attempts over stale native records. Save snapshot capture could also continue over a stale list after a failed clear.
Solution: Make Vault-backed clear routes return `bool`, add telemetry helpers for save-snapshot and pending-hydration clears, and reset hydration cursors only after the clear succeeds. Failed pending-queue clear leaves the cursor at the drained end so the system retries clearing instead of replaying stale work.
Rejected Alternatives: Ignoring clear failure because the path is rare is a persistence lie. Allocating a managed fallback queue was rejected because it moves the problem into GC memory.
Scalability potential: Low avoids duplicate hydration bursts under Vault contention. Middle/High/Ultra can process larger hydration queues without cursor/list divergence.
Hardware Impact: Normal path adds one branch per drain/clear. Failure path avoids duplicate residency work and possible object-pool churn; on weak CPUs that can avoid hundreds of microseconds in dense sectors.

## Decision 0044 - Dehydrate Slot-State Before Despawn
Problem: `DehydrateRecord` could despawn or deactivate the instance before proving that `_poolSlotData` accepted the cleared hydrated/dirty/queued flags. If the Vault write failed, the system could retain a hydrated slot pointing to a despawned object.
Solution: Capture the rigidbody reference, sync the live transform, then call `ClearHydratedSlot` and only proceed with component cleanup, physics sleep, and despawn after the Vault slot write succeeds. Failed slot write keeps mappings intact for retry.
Rejected Alternatives: Despawning first and relying on later cleanup creates a dangling slot state. Forcing a second immediate write retry was rejected because Vault contention should not be spun inside the frame.
Scalability potential: Low devices avoid slot/object divergence during memory pressure. Middle/High/Ultra can increase residency churn while slot-state transitions remain transactional.
Hardware Impact: Normal path branch cost is negligible. Failure path avoids pool-slot corruption and rehydration churn that can cost milliseconds when object pools are dense.

## Decision 0045 - Resource Tombstone DTO Largest-First Layout
Problem: `ResourceNodeTombstoneRecord` placed `InstanceUid` and `Reserved0` before `AbsoluteUniversePosition`. That kept total size aligned, but a nested AUP payload with long fields lived after 4-byte fields, weakening the explicit layout law.
Solution: Move `Position` to offset 8 directly after `TombstoneId`, move `ChunkId` to 56, and put the 4-byte tail at 68/72/76. Update the runtime layout validator and final offset map.
Rejected Alternatives: Treating the struct as safe because size was 80 bytes ignores the project's pointer/largest-first rule. Creating a second tombstone DTO was rejected because this record is not a persistent save ABI and only needs one authoritative layout.
Scalability potential: Low/Middle/High/Ultra get one deterministic tombstone DTO layout with no platform-specific alignment ambiguity.
Hardware Impact: Runtime frame cost is 0 us. The gain is correctness on ARM64 and simpler validator proof, not measured speed.

## Decision 0046 - Saturating AUP Quantization Rail
Problem: AUP conversion and sector hashing still contained unchecked numeric narrowing: `FromAbsolutePosition` cast `floor(double)` to `long`, sector quantizers cast `floor(double)` to `int`, and compact save Y packing cast absolute depth to `float` before subtracting the depth origin.
Solution: Add guarded grid-coordinate resolution for `FromAbsolutePosition`, expose `AbsoluteUniversePosition.FloorToIntSaturated`, route runtime/save sector quantization through it, widen grid multiplication before multiplying by sector size, and pack compact Y after double-origin subtraction.
Rejected Alternatives: Relying on normal playable coordinates; corrupted saves, mod payloads, and AUP boundary tests are exactly where unchecked casts become persistence bugs. Throwing was rejected because this path must fail closed.
Scalability potential: Low/Middle/High/Ultra keep one deterministic sector hash and compact save contract. Higher density sectors do not change the numeric route.
Hardware Impact: Normal path adds scalar finite/range branches only. It prevents undefined far-coordinate sector hashes and precision loss; expected frame cost is below measurement noise, save/load correctness gain is material.

## Decision 0047 - Vault Hash Map In-Place Upsert
Problem: Entity state, flora spawn state, and pool-guid updates used remove-then-add against Vault-backed hash maps. If the second write failed under compaction fence or lock contention, the old fact was already gone.
Solution: Add `VaultBackedHashMap.TrySet` that updates an existing value in-place or inserts into an empty/tombstone slot under one lock window. Convert state and guid publish routes to `TrySet`.
Rejected Alternatives: Restoring the previous value after failure is still a second failing write window. Managed fallback dictionaries were rejected because they split truth ownership and add GC pressure.
Scalability potential: Low devices fail closed without losing old state under pressure. Middle/High/Ultra can raise state density while preserving one fact -> one owner -> one route.
Hardware Impact: Normal update path avoids remove+insert probe churn and keeps lock windows bounded. On i3/MX350 this saves small but repeatable hash work and prevents millisecond-class repair scans after state loss.

## Decision 0048 - Dehydrate Queue Flag Publication Order
Problem: `QueueRecordForDehydration` set the `DehydrationQueued` flag before checking queue capacity. If enqueue then failed, rollback depended on another Vault write and could leave a permanently queued slot.
Solution: Check queue count/capacity before mutating slot flags, and make sentinel prewarm stop on the first enqueue refusal instead of ignoring failures.
Rejected Alternatives: Moving flag write after enqueue would allow duplicate queue entries. Spinning or retrying on Vault refusal was rejected because compaction must not be blocked by frame work.
Scalability potential: Low avoids stuck slots when capacity is tight. Middle/High/Ultra can increase queue size without changing the publish order.
Hardware Impact: Adds one bounded count read before dehydrate enqueue. It prevents repeated dead-slot scans and object-pool churn; worst-case savings are scene-dependent, normal cost is a few scalar operations.

## Decision 0049 - Persistent World Binary Manifest Sync
Problem: `BinaryLayoutManifest.VerifyPersistentWorldLayouts()` still described old persistent-world layouts (`PoolSlotData` size 40, tombstone `Position` at 16, no runtime delta map). That verifier was now either false-red or false-green.
Solution: Update only the persistent-world manifest block to current offsets for `PoolSlotData`, `ResourceNodeTombstoneRecord`, `PersistentWorldDeltaRecord`, and `PersistentWorldCompactDeltaRecord`.
Rejected Alternatives: Leaving the core manifest stale because it is outside the nominal world file would break the one proof artifact rule. Duplicating another validator was rejected because the boot manifest is already the cross-domain contract.
Scalability potential: Low/Middle/High/Ultra get the same boot-time proof and no hidden platform layout divergence.
Hardware Impact: Runtime frame cost is 0 us after boot. Boot verification cost is reflection-only and accepted to catch layout drift before save/native memory corruption.

## Decision 0050 - Invalid AUP Must Not Become Origin
Problem: `FromAbsolutePosition` and `FromRuntimePosition` returned `default` for non-finite or out-of-range coordinates. `default` is a finite origin AUP, so downstream `IsFinite()` checks reported success and could convert corrupted or overflowed persistence coordinates into a valid world-origin fact.
Solution: Add `AbsoluteUniversePosition.Invalid()` with NaN local rails, return it for invalid conversion paths, and log/skip the local entity migration and hibernation seed routes when a derived AUP is invalid.
Rejected Alternatives: Keeping origin fallback because it avoids NaN propagation is a persistence lie. Throwing was rejected because conversion failure is data validation, not an exception-driven control path.
Scalability potential: Low devices avoid expensive recovery from wrong-sector teleports. Middle/High/Ultra keep the same AUP authority route while supporting larger worlds without silent coordinate collapse.
Hardware Impact: Normal path adds finite/range branches only. The saved cost is correctness-driven: avoiding one bad hydration/save fact prevents millisecond-class repair or reload work on i3/MX350.

## Decision 0051 - Pool GUID Change Needs One Locked Map Transaction
Problem: `RegisterOrUpdatePoolSlot` removed the old GUID mapping before proving the new GUID insert/update. Under Vault contention, full hash-map capacity, or compaction fence, the previous route could be lost before the replacement existed.
Solution: Add `VaultBackedHashMap.TrySetReplacing(previousKey,nextKey,value)` that locks keys, values, states, and count once, tombstones the previous slot only inside that window, inserts the replacement, and restores the previous active slot if insertion fails.
Rejected Alternatives: Add-then-remove fails when the map is full and the previous slot must be freed. Remove-then-add plus best-effort restore keeps the same two-window data loss bug. Managed fallback dictionaries split truth ownership and add GC pressure.
Scalability potential: Low devices fail closed without losing hydrated pool identity under tight capacity. Middle/High/Ultra can increase tracked item density while preserving one GUID -> one pool slot -> one Vault route.
Hardware Impact: Normal path saves a separate remove probe and keeps the write-lock window bounded. The branch cost is negligible; avoiding orphaned pool GUIDs prevents residency churn that can become millisecond-scale in dense sectors.

## Decision 0052 - Voxel Snapshot Copy Refusal Is Not Exceptional
Problem: `SaveManager` still threw `InvalidOperationException` when `VoxelDeltaProcessor.TryCopyNativeSnapshotToBorrowedScratch` reported a positive byte count but copy failure. This is a predictable native snapshot refusal, not an unknown exception.
Solution: Replace the throw with deterministic save failure publication: release snapshot pause, write async persistence telemetry with failure code `3`, publish failed status/completion, dump the save black box, record failure state, raise `SaveEvents.OnSaveFailed`, and return.
Rejected Alternatives: Letting `catch (Exception)` handle the path allocates exception state and obscures the numeric refusal code. Retrying during save was rejected because native snapshot contention should not spin inside the save boundary.
Scalability potential: Low/Middle/High/Ultra save behavior is consistent: native snapshot refusal fails the save instead of unwinding through managed exceptions. The first-20-minutes route benefits by making early save failures diagnosable, not corrupt or vague.
Hardware Impact: Normal frame cost is 0 us. Failure path avoids managed exception allocation/unwind on weak CPUs and records a deterministic telemetry code.

## Decision 0053 - Invalid AUP Math Must Poison, Not Preserve
Problem: `AUPMath.OffsetAbsoluteMeters` returned the previous absolute position when delta input or shifted output was invalid, and `WeightedAbsoluteAverage3` returned `a` when the weighted result was invalid. Both paths preserve a valid-looking world fact after failed math.
Solution: Return a NaN `double3` rail via `InvalidDouble3()` and let `FromAbsolutePosition` convert it to `AbsoluteUniversePosition.Invalid()`.
Rejected Alternatives: Keeping previous/first point as a visual fallback is a persistence lie; throwing was rejected because these are predictable numeric validation failures.
Scalability potential: Low devices avoid costly wrong-sector hydration after corrupted math. Middle/High/Ultra retain the same AUP truth route with larger worlds and no binary quality switch.
Hardware Impact: Normal path unchanged except scalar finite checks. Failure path prevents wrong-origin/wrong-sector recovery work that can become millisecond-class on i3/MX350 in dense scenes.

## Decision 0054 - Hydration Cannot Spawn From Invalid AUP
Problem: Stored registry positions can become invalid through corrupted saves or failed AUP math. `HydrateRecord` could convert them to runtime positions and call `pool.Spawn` before proving finite scene coordinates.
Solution: Check `hydratedPosition.IsFinite()` and `math.isfinite(runtimePosition)` before spawn; write `WorldTelemetryInvalidAup` and return false on refusal.
Rejected Alternatives: Relying on downstream Transform/pool code to survive NaN is not deterministic and can contaminate scene state.
Scalability potential: Low avoids bad object-pool churn. Middle/High/Ultra can hydrate more aggressively while the same finite gate protects all quality levels.
Hardware Impact: Normal path adds finite checks only. Failure path avoids NaN transform propagation and follow-up object cleanup costs.

## Decision 0055 - Save Metadata Player Position Must Fail Closed
Problem: `SaveBinaryStorage` metadata read converted prefix AUP to `Vector3` without a finite gate, while the write prefix used a helper that returned default origin AUP when runtime player position could not be resolved.
Solution: Add `TryToRuntimePosition` for read paths and use `TryResolveAupFromRuntimeOrigin` directly in the writer. Invalid metadata/player position now returns false with a fixed error string.
Rejected Alternatives: Serializing origin as a fallback loses the player's actual world truth; allowing NaN `Vector3` in metadata leaks corruption into UI/load previews.
Scalability potential: Low avoids corrupt save previews and origin respawn lies. Middle/High/Ultra keep the same metadata contract while larger AUP ranges remain deterministic.
Hardware Impact: Cold path only. Normal save/load cost is one finite check; failure path avoids corrupt-load recovery and misleading UI state.

## Decision 0056 - Runtime Chunk Bridge Must Not Map Invalid Input To Origin
Problem: `PersistentWorldRegistry.ResolveRuntimeChunkId` returned `default int3` on invalid runtime coordinates, so external resource-event bridges could query chunk 0/0/0 for bad input. `CopyResourceNodeTombstonesInChunk` also did not reject invalid tombstone AUP before returning records.
Solution: Add `TryResolveRuntimeChunkId`, sentinel `int.MinValue` chunk, invalid-AUP telemetry, and sentinel/position finite gates in tombstone copy.
Rejected Alternatives: Editing the active sibling `ResourceDistributionDirector.cs` would interfere with another agent. Keeping the old origin fallback violates fail-closed AUP semantics.
Scalability potential: Low prevents spurious origin-sector resource reinstatement. Middle/High/Ultra preserve event-driven resource visuals without adding per-frame solver work.
Hardware Impact: Normal path adds no allocation and a few scalar checks. Failure path avoids wrong-sector scans and false resource respawns.

## Decision 0057 - AUP Quantization Must Never Default To Origin
Problem: `FloorToIntSaturated` returned `0` for non-finite values, and sector/chunk hash callers could therefore convert NaN/Infinity AUP payloads into valid origin-adjacent keys.
Solution: Make NaN saturate to an invalid rail, add finite gates before chunk/sector quantization, return `InvalidPagedSectorHash` for invalid AUPs, abort indexed save grouping on invalid persistent-world sector records, and prevalidate compact sector entity-state writes.
Rejected Alternatives: Keeping origin fallback because the normal player path is finite. Corrupt saves, mod payloads, and failed math are exactly the paths that need fail-closed behavior.
Scalability potential: Low devices avoid wrong-origin hydration and sector paging churn. Middle/High/Ultra can scale sector density without changing the AUP truth route.
Hardware Impact: Normal path adds scalar finite checks only. Failure path avoids wrong-sector IO and object churn; avoided cost is scene-dependent and can be millisecond-class on i3/MX350.

## Decision 0058 - Known Persistence Refusals Are Return Codes, Not Exceptions
Problem: Save/load had known refusal paths (`no load candidate`, voxel snapshot load refusal, binary writer failure, missing temp, promotion failure) implemented as `throw new`, then handled by broad catch.
Solution: Convert those paths to `false`/fixed message routes, publish numeric save failure code `3` where applicable, and keep outer catch for unexpected IO/runtime exceptions only.
Rejected Alternatives: Letting expected refusal unwind through managed exceptions allocates and hides the numeric failure contract. Removing all IO catches was rejected because filesystem failure is outside deterministic simulation control.
Scalability potential: Low devices avoid exception allocation/unwind under save pressure. Middle/High/Ultra retain the same atomic save contract with more capacity.
Hardware Impact: Normal success path unchanged except branches. Failure path avoids managed exception cost and records deterministic telemetry.

## Decision 0059 - Out-Of-Range AUP Must Not Saturate Into Real Keys
Problem: Reaudit 23 fixed NaN/Infinity quantization, but finite AUP values outside the int chunk/sector key space still saturated to `int.MinValue` or `int.MaxValue`. That aliases many impossible positions into a small set of valid-looking chunk and sector keys.
Solution: Replace quantization callers with `TryFloorToInt`, reject boundary rails, make delta records with rail chunks invalid, and route registry chunk resolution through `TryResolveRegistryChunkId` with numeric invalid-AUP telemetry.
Rejected Alternatives: Keeping saturation because normal gameplay is far from int rails was rejected; corrupted saves, mod payloads, and failed origin math are exactly the inputs that reach impossible ranges.
Scalability potential: Low avoids wrong-sector object churn and save pollution under bad data. Middle/High/Ultra keep the same AUP authority route while larger worlds still fail closed at key-space limits.
Hardware Impact: Normal path adds scalar range branches only. Failure path prevents wrong chunk/sector scans and corrupt save groups that can cost milliseconds on i3/MX350-class hardware.

## Decision 0060 - Invalid Persistent Delta Saves Must Refuse, Not Emit Zero Records
Problem: The non-indexed persistent-world section writer could skip invalid records during table construction but still write a fixed record count, causing default zero save records to occupy payload slots.
Solution: Make both table builders fail on invalid `PersistentWorldDeltaRecord` and dispose every Temp native table before returning false.
Rejected Alternatives: Continuing to write default records was rejected because it hides corruption and creates load-time ambiguity. Filtering records into a new Temp list was rejected because failure should be explicit, not silently lossy.
Scalability potential: Low devices avoid loading polluted persistent sections. Middle/High/Ultra can increase save density without changing the fail-closed contract.
Hardware Impact: Cold save path only. Failure path avoids later load repair and object registry reconciliation work; normal save adds one validity branch per delta.

## Decision 0061 - AUP Blit Imports Must Canonicalize Grid-Local Payloads
Problem: `AbsoluteUniversePosition.FromAlignedBlit` accepted any finite local offset. A corrupted or modded payload with `LocalX < 0` or `LocalX >= CellSizeMeters` was still finite and could propagate as a non-canonical AUP, creating multiple binary representations for the same absolute position.
Solution: Add `FromGridLocal`, route `FromAlignedBlit` and `ReadPoolSlotPosition` through it, and add carry normalization in `FromAbsolutePosition` so float-local rounding cannot leave a local offset at the cell edge.
Rejected Alternatives: Treating finite local offsets as valid was rejected because it hides save/pool corruption. Hard-failing every non-canonical finite offset was rejected because canonicalization preserves the same absolute world fact without data loss.
Scalability potential: Low devices avoid repeated wrong-sector checks after corrupted AUP payloads. Middle/High/Ultra keep one canonical route while larger streamed worlds remain deterministic.
Hardware Impact: Normal canonical path adds only finite/range branches. Non-canonical cold/import path pays a double recomposition once and avoids later wrong-sector IO or hydration churn.

## Decision 0062 - Indexed Sector Payloads Must Prove Sector Ownership On Read And Write
Problem: Sector override writes validated record structure but did not prove each delta belonged to the header sector. Indexed sector reads appended records from a sector block before proving record-sector membership, allowing cross-sector pollution if a block or temp override was corrupt.
Solution: Add `TryValidatePersistentWorldSectorRecords` overloads for NativeArray and managed read arrays, validate every record against `entry.SectorHash`/override hash before appending or writing, and make registry merge reject invalid or cross-sector override records.
Rejected Alternatives: Re-bucketing records by their embedded AUP was rejected because it silently repairs a corrupt file and hides the owner-route violation. Skipping invalid records was rejected because save/load must fail closed, not become lossy.
Scalability potential: Low avoids loading polluted sectors and generating object churn. Middle/High/Ultra can raise paging density without changing sector authority semantics.
Hardware Impact: Cold IO path only. The validation is O(records in sector) scalar math and prevents wrong-sector hydration work that can exceed the validation cost on weak CPUs.

## Decision 0063 - Broad Touched-File Native Scan Exposes External Residuals
Problem: A full `Assets/_Project/Scripts` native alias scan over touched boundary files reports 27 forbidden candidates outside the primary world registry: 13 true persistent SaveManager staging/telemetry arrays and 14 SaveBinaryStorage transient handle/mapping fields that the broad scanner cannot classify as ephemeral.
Solution: Record the residuals in `DOMAIN_REAUDIT25_REPORT_1325.json` and do not refactor SaveManager ownership in this pass. The primary domain (`PersistentWorldRegistry.cs`) remains at 0 native collection fields; the broader World scan has one sibling residual in `VoxelDynamicNavGridRuntime.cs:705`.
Rejected Alternatives: Claiming all touched files are native-clean is false. Refactoring SaveManager persistent buffers into DataVault handles in this pass was rejected as cross-domain scope expansion with high collision risk for other agents.
Scalability potential: The domain fix remains deterministic. SaveManager exorcism should be a separate owner task because it changes global save buffer lifetime and fallback IO behavior.
Hardware Impact: No runtime cost. The value is risk containment: the unfixed residuals are named instead of hidden in a green report.

## Decision 0064 - Temp Native Handles Must Be Stack-Only Or Immediate
Problem: `PersistentWorldRegistry` queued `SectorEntityStateWriteWork` objects that owned `IndexedSectorEntityStateWriteHandle`; that handle contained Temp/TempJob `NativeArray` fields and could cross frames through a managed list.
Solution: Make `IndexedSectorEntityStateWriteHandle` a `ref struct`, delete the pending work list and drain helpers, and complete indexed entity-state override writes inside the same call boundary before disposing the native buffers.
Rejected Alternatives: Keeping deferred completion for IO smoothness was rejected because it stores native aliases in managed object state. Replacing the queue with a class wrapper around handles was the same violation with a different name.
Scalability potential: Low devices lose a small deferred-write smoothing path but gain deterministic native lifetime. Middle/High/Ultra can recover smoothness later with a DataVault-owned or file-IO-owned descriptor queue, not unmanaged arrays in registry state.
Hardware Impact: Cold save path may block slightly longer per sector override. Runtime frame safety improves by eliminating cross-frame TempJob native ownership and compaction ambiguity.

## Decision 0065 - SaveBinaryStorage Read Cache Must Not Own Persistent Native Collections
Problem: `AsyncWriteManager.CachedReadWindow` stored a persistent `NativeArray<byte>` and raw `byte*` inside a static cache array. That is a long-lived native alias in a static manager, not a transient view.
Solution: Replace cached native windows with pooled managed `byte[]` storage from `ArrayPool<byte>`. Pin only inside `TryCopyFromCachedReadWindow` and `TryUploadCachedReadWindowToGraphicsBuffer` while holding the read-window lock, then release the fixed pointer before leaving the method.
Rejected Alternatives: Marking `CachedReadWindow` as stack-only is impossible because the cache is intentionally static. Keeping native memory and documenting it as cold was rejected because the memory sovereignty rule is about ownership escape, not frame cadence. Removing the cache entirely was rejected because save/load metadata and sector reads would regress IO locality.
Scalability potential: Low avoids native allocator pressure and stale pointer risk on save/load; Middle keeps the 4-window cache; High/Ultra can raise read-window count later through a continuous budget without changing native ownership.
Hardware Impact: Cached read misses now rent a managed array instead of allocating native memory. Normal copy cost remains memcpy plus a fixed pin window; on i3/MX350 this trades native lifetime risk for cold ArrayPool pressure and keeps GPU upload locality.

## Decision 0066 - SaveManager Residual Is A Separate Data Archivist Exorcism
Problem: After SaveBinaryStorage cleanup, the touched-file ledger still reports 13 forbidden persistent native fields in `SaveManager`, all owned by the Data Archivist save manager, not by the persistent-world registry or AUP persistence bridge.
Solution: Declare those fields explicitly and stop short of migrating them in this pass. They need a dedicated SaveManager DataVault descriptor design because the fields cover payload buffers, compression staging, WFC outpost mutable state, snapshot cache, and telemetry rings.
Rejected Alternatives: A quick wrapper around `NativeArray` fields would hide, not solve, ownership. Full migration in this turn would cross the documented domain boundary and risk breaking save/load while another build/compiler process is active.
Scalability potential: Low/Middle/High/Ultra still need a future SaveManager-native exorcism to make save buffer capacity continuously scalable through Vault descriptors. This pass removes all SaveBinaryStorage-native residuals without changing global save authority.
Hardware Impact: No runtime cost from this decision. The remaining risk is named: `SaveManager` can still hold persistent native buffers and should be assigned to the Data Archivist owner.

## Decision 0067 - Runtime AUP Conversion Must Fail Closed
Problem: `AbsoluteUniversePosition.ToRuntimeFloat3()` returned `float3.zero` when the runtime origin was invalid. Save metadata read code then treated that zero as a valid player position, so a missing origin could masquerade as world origin.
Solution: Add `TryToRuntimeFloat3(out float3)` and make `ToRuntimeFloat3()` return NaN rails on failure. `SaveBinaryStorage.TryToRuntimePosition` now uses the try route and refuses invalid metadata.
Rejected Alternatives: Keeping zero as a visual fallback was rejected because persistence coordinates are authority data, not presentation sugar. Throwing was rejected because missing origin is a predictable fail-closed condition.
Scalability potential: Low avoids origin-spawn lies on corrupted load; Middle/High/Ultra keep the same AUP route while supporting larger streaming worlds.
Hardware Impact: Normal path adds finite/origin branches only. Failure path avoids wrong-origin hydration and save-preview pollution that can become millisecond-class cleanup on weak CPUs.

## Decision 0068 - Async Paging Cannot Own Native Collections
Problem: Indexed sector paging allocated `NativeArray<long>` and `NativeList<PersistentWorldDeltaRecord>` inside `async Awaitable`; those locals are lifted into the async state machine and can live through `await`, violating transient native ownership rules.
Solution: Replace the desired-sector native array with explicit 72-byte `PagedSectorHashWindow` value state and move native load/write views into synchronous helper methods. TempJob native arrays/lists are registered, used, and disposed before the helper returns.
Rejected Alternatives: Documenting the async locals as cold was rejected because lifetime escape is structural, not cadence-based. A managed `long[9]` array was rejected after review because a value-type window is cleaner and avoids one extra managed object.
Scalability potential: Low avoids native allocator leaks under paging churn; Middle/High/Ultra can raise sector density later without changing the lifetime contract.
Hardware Impact: Frame hot path unchanged. Cold paging loses cross-await native lifetime risk; helper allocation/disposal remains bounded and local.

## Decision 0069 - Entity-State-Only Sector Overrides Must Survive Refactor
Problem: While extracting native snapshot writes out of the async method, an empty persistent-delta bucket could have been skipped. Some sectors intentionally carry entity-state overrides with zero delta records, especially flora spawn timestamp state.
Solution: The synchronous snapshot helper now writes zero-length sector override files and entity-state override files for such buckets, matching the previous behavior while still keeping native arrays local to the helper.
Rejected Alternatives: Skipping empty buckets was rejected because it would drop valid entity state. Re-bucketing into another sector was rejected because sector ownership must remain exact.
Scalability potential: Low keeps paged flora/entity state coherent without extra scanning. Middle/High/Ultra can page larger regions without losing state-only override facts.
Hardware Impact: Cold IO path only. The cost is one empty-sector write when needed; the avoided failure is state loss and later repair/hydration churn.

## Decision 0070 - Runtime Origin Must Return Invalid AUP, Not Default
Problem: `RuntimeOriginRoute.CurrentRuntimeOriginAup()` returned `default` when `HectonFloatingOrigin.CurrentTotalOffsetDouble` was non-finite. `default AbsoluteUniversePosition` has zero locals and passes `IsFinite()`, so invalid origin state could masquerade as absolute origin.
Solution: Return `AbsoluteUniversePosition.Invalid()` on non-finite origin and add an explicit origin finite gate in `PersistentWorldRegistry.TryResolveAupFromRuntimeOrigin`.
Rejected Alternatives: Leaving the downstream finite checks unchanged was rejected because those checks accepted default zero. Throwing was rejected because invalid origin is a predictable fail-closed route condition.
Scalability potential: Low avoids wrong-origin save/load/hydration on weak machines after origin corruption. Middle/High/Ultra keep the same route while larger streamed worlds do not alias failure to sector zero.
Hardware Impact: Normal path adds no allocation and only an existing finite branch. Failure path avoids wrong-sector hydration and save repair work that can become millisecond-class on i3/MX350.

## Decision 0071 - Save Safe-Snap Must Not Use Zero Offset On Missing Origin
Problem: Save safe-snap vertical lift used a helper that returned `double3.zero` when runtime origin could not be resolved. During a load/teleport repair path, that zero fallback can compute a believable but wrong vertical correction.
Solution: Replace the helper with `TryResolveCurrentRuntimeOriginDouble3`; invalid origin returns NaN and the caller refuses the snap because NaN is not greater than the minimum lift threshold.
Rejected Alternatives: Keeping zero as a conservative fallback was rejected because persistence origin is authority data, not presentation fallback. Expanding the public route API was unnecessary for this local repair.
Scalability potential: Low avoids bad player rescue teleports after origin failure. Middle/High/Ultra retain the same deterministic AUP snap contract.
Hardware Impact: Cold load path only. Normal path cost is one branch; failure path avoids invalid teleport correction and follow-up physics recovery.

## Decision 0072 - Paged Sector Window Needs Executable Layout Proof
Problem: `PagedSectorHashWindow` is private nested value state and therefore was not covered by the generic editor DTO layout assertions.
Solution: Add editor-only reflection validation in `WorldMemorySovereigntyValidator1325`: size 72, explicit layout, offsets `Hash0..Hash8` at `0..64`.
Rejected Alternatives: Making the struct internal only for validation was rejected because it expands assembly surface. Duplicating the struct outside the registry was rejected because it creates another owner route.
Scalability potential: Low/Middle/High/Ultra all benefit from the same fixed 3x3 sector window; larger windows must intentionally change the validator.
Hardware Impact: Runtime impact is 0 us; validation is editor-only. The protected failure mode is accidental ARM64 misalignment in future edits.

## Decision 0073 - Sanitize Must Canonicalize Finite AUP Payloads
Problem: `AbsoluteUniversePosition.Sanitize` accepted any finite AUP. A corrupted/modded `Grid + Local` pair with local offsets outside `[0, CellSize)` stayed valid and could diverge from canonical sector/hash calculations.
Solution: Route sanitize through `CanonicalizeOrInvalid`, using `FromGridLocal` for finite non-canonical local offsets. Player runtime snapshot and movement fallback now sanitize against explicit invalid AUP.
Rejected Alternatives: Keeping finite-only validation was rejected because it preserves duplicate binary encodings for one absolute world fact. Hard-failing all non-canonical finite payloads was rejected because canonicalization preserves the same position without data loss.
Scalability potential: Low devices avoid repeated wrong-sector scans and repair work. Middle/High/Ultra can increase streamed-world density while keeping one AUP authority route.
Hardware Impact: Normal canonical path adds scalar range checks only. Corrupt/import path pays one double recomposition and avoids potentially millisecond-class wrong-sector IO on i3/MX350.

## Decision 0074 - Vault Copy Must Not Take Writer Fences For Read-Only Source Data
Problem: `VaultBackedList.TryCopyTo` acquired write locks on source items/count just to export a snapshot. That blocks compaction and misrepresents the operation as mutation.
Solution: Resolve source items/count through read-only handles and copy into caller-owned native destination without writer locks.
Rejected Alternatives: Keeping writer locks because it was mechanically safe was rejected; writer fences are a scarce synchronization primitive and should describe mutation only.
Scalability potential: Low/Middle reduce unnecessary fence contention during save snapshots. High/Ultra can raise snapshot cadence without multiplying writer lock pressure.
Hardware Impact: Same O(n) copy, fewer lock operations. The saved cost is small per call but removes a compaction stall risk.

## Decision 0075 - Vault Hash-Map Count Buffers Must Be Self-Healing At Mutation Boundary
Problem: `VaultBackedHashMap` trusted its count buffer. If count became negative or above capacity due to stale/corrupt state, future `TryAdd`, `TrySet`, and `Remove` could reject valid writes or inflate counts permanently.
Solution: Clamp count to `[0, capacity]` inside every mutation after locks are acquired, write the repaired count back, and base capacity checks on the clamped active count.
Rejected Alternatives: Adding a cold full-table recount before every mutation was rejected as too expensive. Ignoring corrupt counts was rejected because one bad value can poison persistence maps indefinitely.
Scalability potential: Low devices get O(1) self-healing without a full scan. Middle/High/Ultra retain deterministic map semantics under larger capacities.
Hardware Impact: Adds two scalar clamps per mutation. Avoids repeated failed writes and later save/load recovery passes.

## Decision 0076 - Telemetry Dump Must Snapshot On Owner Phase, Not Read Vault In Background
Problem: A previous telemetry dump path used a background worker to read `_worldTelemetryRing`. `VaultBackedArray` read-only handles are current-phase views and are not owned/pinned across threads, so a background read can race compaction or owner-phase mutation.
Solution: Fault request only sets a numeric flag. `LateFrameTick`, disable, and destroy capture the 300-frame ring on the owner phase into a cold preallocated `WorldTelemetryEntry[]` using one read-only handle. A pre-owned worker thread writes only that snapshot to `Docs/AgentLogs/Dump_1325_WorldRegistry.bin`.
Rejected Alternatives: `ThreadPool.QueueUserWorkItem` was rejected because it allocates/queues into a managed global pool and hides ownership. Direct background Vault reads were rejected because they can outlive the phase contract. Synchronous file writes in the simulation frame were rejected because they can stall the frame on IO.
Scalability potential: Low devices keep fault dump IO off the frame while preserving Vault ownership. Middle/High/Ultra get the same fixed 300-frame black box without increasing hot-frame cost.
Hardware Impact: Normal frames pay one branch. Fault frame copies 300 unmanaged DTOs once; file IO is off-thread and cold.

## Decision 0077 - Grid-Local Offsets Must Not Recompose Huge Absolute Doubles
Problem: `FromGridLocal` and `OffsetMeters` could normalize by recomposing `grid * cell + local` into an absolute double and then decomposing again. At large grid magnitudes this loses low bits before the local carry is resolved.
Solution: Canonicalize by carrying `floor(local / CellSizeMeters)` into the grid per axis, then clamp the remaining local cell offset. `OffsetMeters` now adds delta to local cell space and uses this carry route.
Rejected Alternatives: Keeping absolute recomposition was rejected because it violates the AUP rule in spirit even when no float cast occurs. Hard-clamping local offsets was rejected because it corrupts movement deltas.
Scalability potential: Low devices get deterministic sector identity without repair scans. Middle/High/Ultra can stream farther from origin without changing DTO layout or save identity.
Hardware Impact: Normal offset path adds three scalar carry operations. It avoids wrong-sector hydration and save re-bucketing that can cost milliseconds after precision aliasing.

## Decision 0078 - Runtime AUP Float Bridges Must Subtract AUP Origin First
Problem: Some runtime bridge paths still passed absolute origin doubles into conversion helpers or computed absolute attractor/current vectors before delta calculation.
Solution: Registry hydration, mod-protection, apex migration, and SaveManager vertical safe-snap now use `AUPDeltaClamped` or `ResolveCameraRelative`, subtracting observer/origin AUP in double grid-local space before float conversion.
Rejected Alternatives: Allowing `originAup.ToAbsoluteDouble3()` in runtime conversion was rejected because it makes a future direct absolute-to-float regression easy. Keeping apex migration absolute deltas was rejected because migration is spatial authority, not presentation.
Scalability potential: Low avoids jitter/wrong snap near large map boundaries. Middle/High/Ultra can use the same deterministic origin-relative route for denser world streaming.
Hardware Impact: Same scalar complexity. The benefit is precision stability; it prevents downstream transform jitter and correction churn.

## Decision 0079 - Paged Sector Hashes Need Integer Grid-Local Quantization
Problem: Common 1000m paged sectors were derived through absolute-position floor paths even though the AUP cell is 5000m and divisible by the sector edge.
Solution: Add `TryResolveSectorCoord` and use integer grid-local quantization for registry sectors, indexed sector save/load, and mod payload hashes when the sector edge divides the AUP cell.
Rejected Alternatives: Leaving `floor(absolute / sectorEdge)` was rejected because it pays double precision debt for a route that can be exact. General rational-sector math was rejected as unnecessary until a non-divisor sector edge becomes a real requirement.
Scalability potential: Low devices avoid wrong page lookups under large coordinates. Middle/High/Ultra can raise paging density while keeping exact sector ownership for divisor edges.
Hardware Impact: Replaces double floor with integer multiply/add on the common path. Cold and paging-path cost is lower and more deterministic.

## Decision 0080 - Telemetry Dump Queue Must Reset When Worker Signal Is Gone
Problem: If a dump snapshot is captured after worker teardown or before signal initialization, `_worldTelemetryDumpQueued` could remain at state `2`, blocking future dump requests.
Solution: Reset the queue to 0 when `_worldTelemetryDumpSignal` is null after snapshot capture. Replace the remaining broad `catch (Exception)` in the owned dump writer with specific IO/permission/disposal catches.
Rejected Alternatives: Ignoring a null signal was rejected because it creates a sticky black-box failure. Keeping filtered `catch (Exception)` was rejected because the owned path can name its expected cold IO failures.
Scalability potential: Low/Middle/High/Ultra keep the same fixed 300-frame crash ring and deterministic failure behavior.
Hardware Impact: One null branch only on queued dump capture. It prevents losing future crash dumps without adding hot-frame allocation.

## Decision 0081 - Committed Offset Runtime Bridge Must Rebuild An AUP Origin
Problem: `AUPMath.ToRuntimeFloat3(position, committedOffset)` still subtracted a committed absolute `double3` from `position.ToAbsoluteDouble3()`. External callers used this shared bridge, so fixing only local call sites would leave a reusable precision trap.
Solution: Convert the committed offset to `AbsoluteUniversePosition.FromAbsolutePosition`, reject invalid origin, then use `AUPDeltaClamped(position, origin)` before clamping and casting to `float3`.
Rejected Alternatives: Editing every caller in fauna/fluid/destructible domains was rejected as cross-domain churn; leaving the shared bridge absolute-subtract path was rejected because future callers would regress.
Scalability potential: Low devices avoid far-boundary jitter without per-caller patch debt. Middle/High/Ultra can keep larger loaded worlds while the same origin-relative conversion remains authoritative.
Hardware Impact: Adds one AUP origin construction on conversion. The cost is scalar and cheaper than downstream transform correction or wrong-sector hydration.

## Decision 0082 - Invalid Persistence Dimensions Must Fail Closed
Problem: Several sector/entity-state save paths used `math.max(1, chunkSizeMeters)` to keep processing. That converts invalid authority data into a valid one-meter chunk route and can write plausible but wrong sector files.
Solution: Reject `chunkSizeMeters <= 0` in `SaveBinaryStorage` validators/group builders and in `PersistentWorldRegistry.TryWriteEntityStateTempBlock` before native temp allocation.
Rejected Alternatives: Clamping was rejected because identity dimensions are not presentation quality. Auto-repairing to `DefaultChunkSizeMeters` was rejected because it hides the upstream owner bug.
Scalability potential: Low fails closed instead of creating repair work. Middle/High/Ultra preserve one save identity route while capacity/fidelity can still scale independently.
Hardware Impact: Normal path adds one branch. Failure path avoids cold IO writes and later re-bucketing scans that can cost milliseconds on i3/MX350.

## Decision 0083 - Weighted AUP Centroids Must Be Anchored
Problem: `WeightedAverage3` summed three absolute double coordinates. At large grid magnitudes, small triangle offsets can disappear before centroid calculation.
Solution: Anchor on point A, compute `AUPDeltaClamped(B,A)` and `AUPDeltaClamped(C,A)`, apply the weight, then offset A through grid-local carry. `OffsetAbsoluteMeters` follows the same carry route before absolute export.
Rejected Alternatives: Keeping absolute-double summation was rejected because the only current triangulation caller uses a 1/3 centroid where anchored math is exact and safer. A general matrix solver was rejected as unnecessary CPU work.
Scalability potential: Low avoids beacon/runtime position jitter. Middle/High/Ultra can triangulate farther from origin without changing save DTOs.
Hardware Impact: Similar scalar math, less precision-repair churn. No frame-time claim without profiler.

## Decision 0084 - Reaudit 33 Boundary Is Primary Green, SaveManager Still External
Problem: The all-scripts ledger still reports 13 forbidden persistent native fields in `SaveManager`. That file is a touched boundary from earlier safe-snap work but the persistent native ownership is Data Archivist scope, not the world registry domain.
Solution: Keep 1325 reports green only for primary world/AUP persistence files and declare the `SaveManager` residual in APEX/domain JSON instead of hiding it.
Rejected Alternatives: Migrating `SaveManager` native buffers here was rejected as a cross-domain rewrite with save-system authority risk. Reporting an empty `failedGates` array would be false.
Scalability potential: Low/Middle/High/Ultra still need a dedicated SaveManager exorcism to make save buffers fully Vault-owned. This pass removes the world/AUP precision and invalid-dimension defects.
Hardware Impact: No runtime cost from the boundary decision. It prevents false certification of a known native-lifetime debt.

## Decision 0085 - Non-Finite AUP Bridges Must Fail Closed
Problem: Reaudit34 found remaining bridge edges where invalid AUP payloads or non-finite centroid weights could still produce plausible coordinates: camera-relative conversion did not explicitly reject invalid target/camera AUP before math, and weighted centroid helpers silently treated invalid weight as `1/3`.
Solution: Reject invalid target/camera AUP in `AUPMath.ResolveCameraRelative`, reject invalid source AUP in committed-offset runtime conversion, and reject non-finite weights in both AUP weighted-average helpers. Failure returns invalid AUP/NaN rails and records numeric invalid-float telemetry where the AUPMath route owns telemetry.
Rejected Alternatives: Keeping the `1/3` fallback was rejected because authority math must not reinterpret corrupt input as a valid centroid. Throwing was rejected because bad persistence/origin input is a predictable fail-closed case.
Scalability potential: Low avoids wrong-sector hydration and visual jumps after corrupt coordinates. Middle/High/Ultra keep the same bridge and can render farther from origin without changing save identity.
Hardware Impact: Normal path adds finite checks only. Failure path avoids wrong-sector IO, transform correction, and save repair churn that can become millisecond-class on i3/MX350.

## Decision 0086 - Resident Sector Snapshot Must Validate Delta Identity Before Native Write
Problem: The page-out snapshot path converted live records to compact `PersistentWorldDeltaRecord` buckets, then wrote a native TempJob array. If a compact delta was invalid or unpacked into a different sector, the registry could persist a wrong fact under the correct sector file name.
Solution: Validate every delta immediately after `FromRecord`, unpack it through the same chunk size, recompute sector hash, and fail closed if the compact payload is invalid or cross-sector. The background writer repeats the check before filling the native array.
Rejected Alternatives: Skipping bad records was rejected because it hides authority corruption and can lose entities. Re-bucketing was rejected because a page-out writer must not silently rewrite sector ownership.
Scalability potential: Low gets deterministic refusal instead of later repair scans. Middle/High/Ultra can page larger resident windows while preserving one-sector-one-file truth.
Hardware Impact: Cold page-out path pays O(records) validation. The avoided cost is corrupt save payload recovery and wrong-sector hydration on weak CPUs.

## Decision 0087 - Native Sentinel Lifetime Must Match Actual Allocator
Problem: `TryWriteEntityStateTempBlock` allocated `NativeArray<EntityDataRecord>` with `Allocator.Temp` but registered it as `NativeAllocationLifetime.TempJob`. That is not a direct leak, but it poisons memory telemetry and hides allocator misuse during audits.
Solution: Register the array with `NativeAllocationLifetime.Temp`, matching allocation and disposal in the same synchronous call boundary.
Rejected Alternatives: Changing allocation to TempJob was rejected because the data does not cross a job boundary. Leaving inaccurate telemetry was rejected because memory sovereignty depends on truthful allocator accounting.
Scalability potential: Low/Middle/High/Ultra behavior unchanged; audit precision improves across all quality levels.
Hardware Impact: Runtime cost unchanged. Diagnostic value improves because allocator lifetime reports no longer overstate TempJob ownership.

## Decision 0088 - Reaudit34 Compile Wall Is External Candice SQLite Dependency
Problem: Build guard cleared, but `Assembly-CSharp.csproj` still failed before runtime verification because `CandiceSQLiteProvider.cs` cannot resolve `Mono.Data`/`SqliteDataReader`.
Solution: Record the wall and stop at the domain boundary. The visible build errors are outside world registry/AUP persistence ownership, and no visible build error referenced touched 1325 files.
Rejected Alternatives: Adding SQLite references or editing Candice vendor code inside a world-registry audit was rejected as cross-domain dependency surgery. Claiming compile green was rejected as a false report.
Scalability potential: Source-side 1325 paths are static-clean, but executable proof remains blocked until the external SQLite/reference issue is fixed by its owner.
Hardware Impact: No runtime microsecond claim. The build attempt cost 18.9 s wall time and provided a concrete integration blocker.

## Decision 0089 - Snapshot Writer Boolean Must Be Authority, Not Log Text
Problem: `SnapshotResidentSectorOverridesAsync` called `TryWriteResidentSectorOverrideSnapshots` and ignored the boolean result. A false result with an empty or lost message could advance resident-sector state as if the page-out succeeded.
Solution: Capture the return value, fail closed on false, and keep a fallback failure message for impossible no-detail failures.
Rejected Alternatives: Treating `failureMessage` as the only contract was rejected because it couples correctness to diagnostic text.
Scalability potential: Low/Middle/High/Ultra all preserve one sector override truth; quality scaling does not change persistence authority.
Hardware Impact: Normal success path adds one scalar boolean. Failure path avoids wrong resident-state churn and corrupt page-out retries that can cost milliseconds on i3/MX350.

## Decision 0090 - Validate Resident Buckets Before TempJob Allocation
Problem: Reaudit34 validated sector delta buckets before native write, but still allocated `Allocator.TempJob` arrays before the repeat validation inside the writer.
Solution: Add explicit resident-sector delta and entity-state bucket validators before TempJob allocation; SaveBinaryStorage keeps its own final write-boundary validation.
Rejected Alternatives: Relying only on the storage validator was rejected because invalid DTOs should fail before native temporary memory is requested.
Scalability potential: Low avoids avoidable native allocator pressure on corrupt input. Middle/High/Ultra can page larger resident windows without hiding bad sector identity.
Hardware Impact: Cold page-out pays O(records) scalar checks. Corrupt input avoids TempJob allocation and IO, saving allocator churn on weak CPUs.

## Decision 0091 - Sector Commit Must Not Touch Runtime Dictionaries On Background Thread
Problem: `RunSectorOverrideCommitAsync` switched to the background thread and then read/mutated `_sectorOverrideStates`. `Dictionary<TKey,TValue>` and registry state ownership are main-thread runtime data, not background IO data.
Solution: Capture `SectorOverrideCommitWork` entries on the main thread, run only file commit/delete operations on captured strings in the background, then reconcile `SectorOverrideState` fields after `Awaitable.MainThreadAsync`.
Rejected Alternatives: Locking the dictionary was rejected because it would make an owner-phase data structure cross-thread. Keeping background mutation was rejected as a race with paging and residency updates.
Scalability potential: Low removes rare but catastrophic async race risk. Middle/High/Ultra can increase commit cadence or sector counts later without changing thread ownership.
Hardware Impact: Normal path keeps the same IO cost. The gain is deterministic state ownership and removal of a race that could cause stale temp files or wrong resident restore.

## Decision 0092 - File Deletion Must Fail Closed In Owned Runtime Paths
Problem: Several owned `PersistentWorldRegistry` temp-file cleanup paths used raw `File.Delete`, which can throw and tear down async/cold runtime flows outside explicit fail-closed contracts.
Solution: Route owned deletes through `TryDeleteFileIfExists`, catching only expected IO/path/permission exceptions and returning false to the caller.
Rejected Alternatives: Broad `catch (Exception)` was rejected by no-throw policy. Ignoring delete failures was rejected because stale entity-state temp files can resurrect wrong sector state.
Scalability potential: Low/Middle/High/Ultra keep the same persistence authority; stale temp cleanup becomes deterministic.
Hardware Impact: Successful cleanup is unchanged. Failure path avoids unhandled exception recovery and gives the caller a bounded refusal route.

## Decision 0093 - Reaudit35 Build Guard Boundary
Problem: After source repair, CPU was below 50% but an active `dotnet` process PID 62864 was present. AGENTS forbids launching another build while any `dotnet`/`csc` process is active.
Solution: Do not launch `dotnet build`; record `BUILD_GUARD_BLOCKED_ACTIVE_DOTNET_PID_62864` in the Reaudit35 report.
Rejected Alternatives: Racing another build process would violate the shared 20-agent workspace rules and poison timing/proof data.
Scalability potential: Runtime behavior unchanged; proof integrity improves by respecting shared build ownership.
Hardware Impact: Build contention avoided. No runtime microsecond claim.

## Decision 0094 - Paging Override Reads Need A Main-Thread Snapshot
Problem: `RunIndexedSectorPagingAsync` switched to a background thread and then `ApplySectorOverrides` / `TryLoadSectorEntityStateOverrides` read `_sectorOverrideStates`, a main-thread runtime dictionary.
Solution: Capture `SectorOverrideReadWork` entries before the background hop. Background IO receives only sector hash and temp/entity-state path strings. Runtime dictionary reads stay on the owner phase.
Rejected Alternatives: Locking `_sectorOverrideStates` was rejected because it would export owner-phase state across dispatcher boundaries. Re-reading the dictionary after the background hop was rejected as the same race under a different shape.
Scalability potential: Low devices avoid rare corrupt restore paths without extra hot-frame work. Middle/High/Ultra can raise indexed paging radius/cadence later because the thread-ownership route remains deterministic.
Hardware Impact: Normal paging adds at most nine `List.Add` operations on the main thread with preallocated capacity. Background IO cost is unchanged; avoided failure cost is stale override merge or wrong entity-state resurrection.

## Decision 0095 - Quarantine Reset Must Not Use Live Registry Fields On Background Thread
Problem: Quarantine reset was invoked from the background side of indexed paging and still read `_indexedSectorSavePath` / `chunkSizeMeters`, and emitted Unity debug logging from that phase.
Solution: Pass captured save path and chunk size into `ResetQuarantinedIndexedSectorsToPristine`, and remove background Unity logging from that helper.
Rejected Alternatives: Keeping field reads was rejected because registry configuration belongs to the main thread. Moving the whole reset to main thread was rejected because the actual reset is cold file IO and should not block the frame.
Scalability potential: Low keeps frame thread clear of quarantine IO. Middle/High/Ultra preserve the same authority route while larger sector stores can still reset off-thread.
Hardware Impact: No hot-frame cost. Prevents background/main config races and avoids Unity logging from a non-owner phase.

## Decision 0096 - SaveBinaryStorage Temp Deletes Must Report Failure Instead Of Throwing Or Vanishing
Problem: Indexed sector commit/reset/backup cleanup used raw `File.Delete` or an empty catch. A delete failure after writing the save could throw out of a `Try*` method or hide stale temp files.
Solution: Add `TryDeleteFileIfExists` in `SaveBinaryStorage` with specific IO/path/permission catches. Commit returns false if cleanup fails; reset/backup cleanup is bounded and no longer uses empty catch blocks.
Rejected Alternatives: Broad `catch (Exception)` was rejected by no-throw/fail-closed policy. Ignoring cleanup failure was rejected because stale sector override files can replay old resident state.
Scalability potential: Low/Middle/High/Ultra keep identical save truth. The cleanup path now fails closed, allowing higher paging density without accumulating silent temp-file debt.
Hardware Impact: Successful cleanup is the same system call path. Failure path avoids unhandled exception recovery and prevents stale temp-file repair scans on weak CPUs.

## Decision 0097 - Reaudit36 Build Guard Boundary
Problem: Static proof was refreshed, but the machine sampled CPU at 100%. AGENTS forbids launching `dotnet build` above 50% CPU even when no compiler process is active.
Solution: Do not launch build; record `BUILD_GUARD_BLOCKED_CPU_100` in `DOMAIN_REAUDIT36_REPORT_1325.json`.
Rejected Alternatives: Forcing a build on a saturated shared workstation would invalidate timing and violate the multi-agent protocol.
Scalability potential: Runtime behavior unchanged; proof remains static until a clean build window exists.
Hardware Impact: Avoided adding build contention. No runtime microsecond claim.

## Decision 0098 - Indexed Async Sessions Need Generation Fences
Problem: `RestoreFromIndexedSave` and `DisableIndexedSavePaging` reset in-flight booleans while old paging/commit async methods could still resume and mutate the new save session.
Solution: Add `_indexedSectorAsyncGeneration`, invalidate it on restore/disable/lifecycle teardown, and make paging/commit continuations check the captured generation before applying state.
Rejected Alternatives: Trusting `_indexedSectorPagingInFlight = false` was rejected because it does not cancel an already-running async state machine. Holding a lock across awaits was rejected because it would block compaction and owner phases.
Scalability potential: Low avoids rare wrong-session hydration after load/unload churn. Middle/High/Ultra can increase paging cadence or radius without stale continuations corrupting resident truth.
Hardware Impact: Normal path cost is a few integer/volatile checks on cold paging/commit routes. Avoided failure is wrong save-page application, prefab residency churn, and recovery scans that can become millisecond-class on i3/MX350.

## Decision 0099 - Shared Work Lists Must Not Survive Across Await Boundaries
Problem: `_sectorOverrideReadWork`, `_dueSectorOverrideCommitWork`, and `_quarantinedSectorResetScratch` were shared runtime scratch objects. A reset could start a new operation while an old background phase still referenced the same container.
Solution: Remove the persistent read-work and quarantine scratch fields. Store read work in a fixed value window, allocate quarantine reset scratch per recovery operation, and copy commit work into a per-operation array before background IO.
Rejected Alternatives: Reusing the field lists was rejected because it is only safe while one async session exists. Locking the lists was rejected because it would export main-thread runtime ownership into background IO.
Scalability potential: Low keeps paging deterministic under save/load churn. Middle/High/Ultra can use larger sector override queues while the same per-operation ownership rule remains valid.
Hardware Impact: The per-operation commit array and rare quarantine scratch are cold allocations around disk IO. They buy deterministic correctness; no hot-frame microsecond win is claimed.

## Decision 0100 - Snapshot Writer Must Capture Configuration Before Background IO
Problem: Resident sector snapshot writing ran on a background thread and still resolved `_indexedSectorOverrideDirectory` and `chunkSizeMeters` through instance fields.
Solution: Capture sector override directory and chunk size on the main thread, pass them into write helpers, and use static path resolution in the background writer.
Rejected Alternatives: Reading the fields from background was rejected because restore/disable can change them concurrently. Moving all file IO to main thread was rejected because it would stall frames during sector page-out.
Scalability potential: Low keeps frame thread clear of file IO. Middle/High/Ultra can page out more resident sectors while configuration authority stays main-thread owned.
Hardware Impact: No hot-frame allocation change. It removes a race that could write temp overrides to the wrong directory or chunk-size identity after a load transition.

## Decision 0101 - Reaudit37 Compile Wall Is Still External
Problem: After the Reaudit37 patch, build guard allowed one compile attempt, but `Assembly-CSharp.csproj` failed before domain verification completed because Candice SQLite code cannot resolve `Mono.Data` and `SqliteDataReader`.
Solution: Record the compile wall in reports and stop at the domain boundary. The visible build errors do not reference `PersistentWorldRegistry.cs`, `AUPMath.cs`, `SaveBinaryStorage.cs`, or `SaveManager.cs`.
Rejected Alternatives: Editing vendor Candice SQLite references from the world registry domain was rejected as cross-domain dependency surgery. Claiming compile green was rejected as false.
Scalability potential: Source-side 1325 gates are static-clean, but executable proof remains blocked until the save/vendor dependency owner fixes the SQLite reference route.
Hardware Impact: Build attempt cost 31.5 s wall time. No runtime frame-time claim is made.

## Decision 0102 - Sector Commit Candidate Capture Must Not Probe Disk On Owner Thread
Problem: `RunSectorOverrideCommitAsync` used `File.Exists(state.TempPath)` while walking `_sectorOverrideStates` on the main thread, and used another disk probe during post-commit reconciliation. Commit cadence is cold, but it is still launched from runtime owner phases and can stall a frame on slow storage.
Solution: Capture due sector override work from in-memory state only. Keep the actual temp-file existence check in the background IO phase. Reconcile `state.TempPath` on the main thread only after `SaveBinaryStorage.TryCommitIndexedPersistentWorldSectorOverride` returned success.
Rejected Alternatives: Keeping owner-thread disk probes was rejected because sector count scales with world activity and storage latency is not frame-budget deterministic. Clearing state without storage success was rejected because a vanished or failed temp block must remain visible to the fail-closed path.
Scalability potential: Low devices avoid random storage stalls during commit scheduling. Middle/High/Ultra can commit larger resident-sector sets without changing ownership rules.
Hardware Impact: No profiler-backed steady-state microsecond claim. The repair removes O(due sectors) main-thread file-stat calls from the commit cadence and confines storage latency to the background IO phase.

## Decision 0103 - Mapped Sector Scan Must Reject Invalid Chunk Identity
Problem: `TryReadIndexedDirectoryHeaderForMappedScan` normalized `directoryHeader.ChunkSizeMeters` with `math.max(1, ...)`. That made corrupted or incompatible save payload identity look valid under mapped scans, while the primary directory reader already failed closed.
Solution: Replace the clamp with `directoryHeader.ChunkSizeMeters <= 0` refusal and a concrete error string. The mapped scan now has the same invalid-dimension contract as the primary indexed directory reader.
Rejected Alternatives: Leaving the clamp was rejected because it can route records through a one-meter identity and produce wrong sector hashes. Repairing corrupt payloads in-place was rejected because save identity is authority data, not a visual approximation target.
Scalability potential: Low/Middle/High/Ultra keep deterministic save identity. Larger sector stores can be scanned without silent bad-dimension remapping.
Hardware Impact: Successful reads pay one branch. Corrupt payloads fail before record scan and decompression work, saving cold IO/CPU on weak CPUs.

## Decision 0104 - Reaudit38 Build Guard Boundary
Problem: Static proof was refreshed after source mutation, but the workstation sampled CPU at 100%. AGENTS forbids launching `dotnet build` when CPU is above 50%, even with no active compiler process.
Solution: Do not launch build; record `BUILD_GUARD_BLOCKED_CPU_100` in `DOMAIN_REAUDIT38_REPORT_1325.json` and `APEX_PURGE_REPORT_1325.json`.
Rejected Alternatives: Forcing a build on a saturated shared workstation was rejected because it violates the multi-agent protocol and produces bad timing/proof data.
Scalability potential: Runtime behavior unchanged; executable proof remains pending a clean build window.
Hardware Impact: Avoided build contention. No runtime frame-time claim.
